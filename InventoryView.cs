// [소스 안내] 플레이어의 저장된 인벤토리와 아이템을 보여주는 화면입니다. 실시간 게임 인벤토리를 직접 수정하는 기능은 아닙니다.
namespace NeoForgeServerManager;

public sealed class InventoryView:UserControl
{
    readonly string folder;readonly PlayerSnapshot snapshot;readonly ItemArtwork artwork;
    readonly FlowLayoutPanel rows=new(){Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};
    readonly Stack<(string Title,List<StoredItem> Items)> trail=[];
    readonly Label heading=new(){Dock=DockStyle.Top,Height=56,ForeColor=WarmTheme.Muted};
    readonly WarmButton back=new(){Text="← 인벤토리",Dock=DockStyle.Top,Height=36,Visible=false};
    int revision;bool closing;
    public InventoryView(string root,PlayerSnapshot data)
    {
        folder=root;snapshot=data;artwork=new(root);BackColor=WarmTheme.Surface;
        Controls.Add(rows);Controls.Add(back);Controls.Add(heading);back.Click+=(_,_)=>{if(trail.Count>0)trail.Pop();Render();};rows.SizeChanged+=(_,_)=>{foreach(Control c in rows.Controls)c.Width=Math.Max(80,rows.ClientSize.Width-24);};Render();
    }
    void Render()
    {
        revision++;foreach(Control c in rows.Controls.Cast<Control>().ToArray())c.Dispose();back.Visible=trail.Count>0;
        heading.Text=trail.Count>0?trail.Peek().Title:"마지막 저장 기준 · 편집할 수 없습니다.";
        if(trail.Count>0){AddItems(trail.Peek().Items);return;}
        if(snapshot.Error.Length>0){heading.Text=snapshot.Error;return;}
        AddSection("인벤토리",snapshot.Inventory);AddSection("엔더 상자",snapshot.EnderItems);if(snapshot.EquippedContainers.Count>0)AddSection("장착·추가 보관함",snapshot.EquippedContainers);
    }
    void AddSection(string title,List<StoredItem> items){rows.Controls.Add(new Label{Text=title,Height=32,Width=Math.Max(80,rows.ClientSize.Width-24),ForeColor=WarmTheme.Ink,Font=new Font("Noto Sans KR",11,FontStyle.Bold)});AddItems(items);}
    void AddItems(List<StoredItem> items)
    {
        if(items.Count==0){rows.Controls.Add(new Label{Text="저장된 아이템이 없습니다.",Height=40,Width=Math.Max(80,rows.ClientSize.Width-24),ForeColor=WarmTheme.Muted});return;}
        foreach(var item in items){var row=new InventoryItemCard(item){Width=Math.Max(80,rows.ClientSize.Width-24),Height=88+ItemEnchantments.Read(item).Count*25,Margin=new Padding(0,0,0,8)};rows.Controls.Add(row);_=LoadImage(row,item,revision);if(ItemContainers.IsContainer(item)){row.Cursor=Cursors.Hand;row.Click+=async(_,_)=>{if(trail.Count>=12)return;row.Enabled=false;int request=revision;var contents=await Task.Run(()=>ItemContainers.Read(folder,item));if(closing||row.IsDisposed||request!=revision)return;row.Enabled=true;if(!contents.Supported){WarmDialog.Show(FindForm()!,contents.Message,"가방 내부 조회");return;}trail.Push((item.Id+"\n"+contents.Message,contents.Items));Render();};}}
    }
    async Task LoadImage(InventoryItemCard row,StoredItem item,int request)
    {
        // A single worker owns the cache; avoid disposing it while an asset read is active.
        Bitmap? icon=await Task.Run(()=>{lock(artwork){return closing?null:artwork.Load(item.Id);}});
        if(!closing&&!row.IsDisposed&&request==revision){row.Icon=icon;row.Invalidate();}
    }
    protected override void Dispose(bool disposing){if(disposing){closing=true;lock(artwork)artwork.Dispose();}base.Dispose(disposing);}
}

public sealed class InventoryItemCard(StoredItem item):Control
{
    readonly IReadOnlyList<ItemEnchantment> enchantments=ItemEnchantments.Read(item);
    public StoredItem Item=>item;public Bitmap? Icon{get;set;}
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(WarmTheme.Surface);using var path=WarmTheme.Round(new Rectangle(1,1,Width-3,Height-3),12);using var pen=new Pen(WarmTheme.Line);e.Graphics.DrawPath(pen,path);
        if(Icon!=null){e.Graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;e.Graphics.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.Half;e.Graphics.DrawImage(Icon,new Rectangle(12,16,48,48));}else ReferenceIcons.Draw(e.Graphics,"mods",new Rectangle(16,20,40,40),WarmTheme.Muted);
        using var font=new Font("Noto Sans KR",10);using var small=new Font("Noto Sans KR",9);
        TextRenderer.DrawText(e.Graphics,item.Id,font,new Rectangle(72,8,Width-82,38),WarmTheme.Ink,TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(e.Graphics,$"슬롯 {item.Slot} · × {item.Count}"+(ItemContainers.IsContainer(item)?"   내부 보기 →":""),small,new Rectangle(72,48,Width-82,30),WarmTheme.Muted,TextFormatFlags.EndEllipsis);
        for(int i=0;i<enchantments.Count;i++)TextRenderer.DrawText(e.Graphics,enchantments[i].Display,small,new Rectangle(16,82+i*25,Width-28,25),Color.FromArgb(130,80,160),TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter);
    }
}
