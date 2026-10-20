// [소스 안내] 접속자/기록 화면의 표시와 사용자 동작입니다. 저장된 플레이어 정보와 상세 보기 기능을 연결합니다.
namespace NeoForgeServerManager;

public sealed class PlayerHistoryPage : UserControl
{
    readonly ReferencePanel layout=new(){Dock=DockStyle.Fill,ReferenceSize=new(1260,760)};
    readonly SurfaceCard left=new(),right=new();
    readonly BufferedScrollPanel grid=new(){AutoScroll=true};
    readonly WarmTextBox search=new(){PlaceholderText="이름 또는 UUID 검색"};
    readonly WarmButton current,history,all,online,offline;
    readonly Label heading=Ui.Heading("접속자"),note=Ui.Note("지금 접속한 사람과 이전 방문 기록을 한곳에서 확인하세요.");
    readonly Dictionary<string,Bitmap> skins=[],faces=[];
    readonly HashSet<string> requested=[];
    readonly Func<string,string,Task> action;
    readonly Action refresh;
    List<PlayerRecord> records=[];HashSet<string> connected=new(StringComparer.OrdinalIgnoreCase);
    string folder="",selected="",filter="전체",detailTab="기본 정보";bool onlyCurrent;int generation;
    bool hasData,refreshRequested;
    string displayedIdentity=""; Label? detailStatus; InventoryView? inventoryView;
    public PlayerHistoryPage(Action reload,Func<string,string,Task> command)
    {
        action=command;refresh=reload;BackColor=WarmTheme.Surface;Controls.Add(layout);
        current=Button("현재 접속",()=>{onlyCurrent=true;filter="전체";Render();});history=Button("접속 기록",()=>{onlyCurrent=false;Render();});
        all=Button("전체",()=>{filter="전체";Render();});online=Button("온라인",()=>{filter="온라인";Render();});offline=Button("오프라인",()=>{filter="오프라인";Render();});
        var reloadButton=Button("기록 새로고침",()=>{displayedIdentity="";refreshRequested=true;refresh();});reloadButton.IconName="refresh";
        layout.Place(heading,18,6,500,48);layout.Place(note,18,58,750,36);layout.Place(reloadButton,1070,100,175,46);
        layout.Place(current,14,100,220,46);layout.Place(history,234,100,220,46);layout.Place(search,630,100,426,46);
        layout.Place(left,12,160,480,588);layout.Place(right,504,160,744,588);
        var title=Ui.Heading("플레이어 목록");title.Font=new Font("Noto Sans KR",16,FontStyle.Bold);left.Controls.AddRange([title,all,online,offline,grid]);
        left.Resize+=(_,_)=>{title.SetBounds(20,12,left.Width-40,40);int w=(left.Width-44)/3;all.SetBounds(16,58,w,42);online.SetBounds(22+w,58,w,42);offline.SetBounds(28+2*w,58,w,42);grid.SetBounds(12,onlyCurrent?58:110,left.Width-24,left.Height-(onlyCurrent?72:124));LayoutTiles();};
        grid.Resize+=(_,_)=>LayoutTiles();search.TextChanged+=(_,_)=>Render();
    }
    static WarmButton Button(string text,Action action,bool primary=false){var b=Ui.Action(text,action,primary);b.MinimumSize=Size.Empty;b.AutoSize=false;b.Font=new Font("Noto Sans KR",11);return b;}
    static string Key(PlayerRecord r)=>r.Uuid.Length>0?r.Uuid:r.Name;
    public void SetData(string root,IEnumerable<PlayerRecord> entries,IEnumerable<string> names)
    {bool show=!hasData||folder!=root||refreshRequested;folder=root;records=entries.OrderByDescending(r=>r.LastJoined).ThenBy(r=>r.Name).ToList();connected=names.ToHashSet(StringComparer.OrdinalIgnoreCase);hasData=true;refreshRequested=false;if(show)Render();}
    void Render()
    {
        if(IsDisposed)return;current.Text=$"현재 접속  {connected.Count}";history.Text=$"접속 기록  {records.Count}";current.Primary=history.Primary=false;current.OutlineColor=onlyCurrent?WarmTheme.Accent:WarmTheme.Line;history.OutlineColor=!onlyCurrent?WarmTheme.Accent:WarmTheme.Line;current.TextColor=onlyCurrent?WarmTheme.Accent:WarmTheme.Ink;history.TextColor=!onlyCurrent?WarmTheme.Accent:WarmTheme.Ink;current.Invalidate();history.Invalidate();
        all.Text=$"전체 ({records.Count})";online.Text=$"온라인 ({records.Count(r=>connected.Contains(r.Name))})";offline.Text=$"오프라인 ({records.Count(r=>!connected.Contains(r.Name))})";
        all.Visible=online.Visible=offline.Visible=!onlyCurrent;grid.SetBounds(12,onlyCurrent?58:110,left.Width-24,left.Height-(onlyCurrent?72:124));all.Primary=filter=="전체";online.Primary=filter=="온라인";offline.Primary=filter=="오프라인";
        foreach(Control c in grid.Controls.Cast<Control>().ToArray())c.Dispose();
        var filtered=records.Where(r=>(!onlyCurrent||connected.Contains(r.Name))&&(filter=="전체"||(filter=="온라인")==connected.Contains(r.Name))&&(r.Name.Contains(search.Text,StringComparison.OrdinalIgnoreCase)||r.Uuid.Contains(search.Text,StringComparison.OrdinalIgnoreCase))).ToArray();
        if(!filtered.Any(r=>Key(r)==selected))selected=filtered.FirstOrDefault() is {} first?Key(first):"";
        foreach(var r in filtered){var tile=new PlayerTile(r,connected.Contains(r.Name),faces.GetValueOrDefault(Key(r)),Key(r)==selected){Cursor=Cursors.Hand};tile.Click+=(_,_)=>{selected=Key(r);displayedIdentity="";BeginInvoke(new Action(Render));};grid.Controls.Add(tile);if(r.Uuid.Length>0&&requested.Add(r.Uuid))_=LoadSkin(r);}
        if(filtered.Length==0)grid.Controls.Add(new ReadableLabel{Text="표시할 접속 기록이 없습니다.",ForeColor=WarmTheme.Muted,AutoSize=false,Size=new(360,70)});
        LayoutTiles();_=RenderDetails(records.FirstOrDefault(r=>Key(r)==selected));
    }
    void LayoutTiles(){int w=Math.Max(100,(grid.ClientSize.Width-24)/2),i=0;foreach(var tile in grid.Controls.OfType<PlayerTile>()){tile.SetBounds(i%2*(w+8),i/2*150,w,140);i++;}}
    async Task LoadSkin(PlayerRecord r){var skin=await PlayerSkins.Load(r.Uuid);if(IsDisposed){skin?.Dispose();return;}if(skin==null)return;skins[Key(r)]=skin;faces[Key(r)]=PlayerSkins.Face(skin);foreach(var t in grid.Controls.OfType<PlayerTile>().Where(t=>Key(t.Record)==Key(r))){t.Face=faces[Key(r)];t.Invalidate();}if(selected==Key(r)){var panel=right.Controls.OfType<ReferencePanel>().FirstOrDefault();var portrait=panel?.Controls.OfType<PictureBox>().FirstOrDefault();if(portrait!=null)portrait.Image=faces[Key(r)];var viewer=panel?.Controls.OfType<PlayerSkinView>().FirstOrDefault();if(viewer!=null){viewer.Skin=skin;viewer.Invalidate();}}}
    static string Date(DateTime? d)=>d?.ToLocalTime().ToString("yyyy.MM.dd HH:mm")??"기록 없음";
    async Task RenderDetails(PlayerRecord? r){try{await RenderDetailsCore(r);}catch(Exception e){if(!IsDisposed){var error=Ui.Note("정보 표시 실패: "+e.Message);error.Dock=DockStyle.Fill;right.Controls.Add(error);error.BringToFront();}}}
    async Task RenderDetailsCore(PlayerRecord? r)
    {
        string identity=r==null?"":folder+"|"+Key(r);
        // Presence polling must not destroy a reader's open container and scroll position.
        if(r!=null&&detailTab=="인벤토리"&&displayedIdentity==identity&&inventoryView is {IsDisposed:false})
        {
            if(detailStatus!=null)detailStatus.Text=connected.Contains(r.Name)?"● 온라인":"● 오프라인";
            var panel=right.Controls.OfType<ReferencePanel>().FirstOrDefault();
            var viewer=panel?.Controls.OfType<PlayerSkinView>().FirstOrDefault();if(viewer!=null){viewer.Skin=skins.GetValueOrDefault(Key(r));viewer.Invalidate();}
            var portrait=panel?.Controls.OfType<PictureBox>().FirstOrDefault();if(portrait!=null)portrait.Image=faces.GetValueOrDefault(Key(r));
            return;
        }
        displayedIdentity=identity;inventoryView=null;
        float angle=right.Controls.OfType<ReferencePanel>().SelectMany(p=>p.Controls.OfType<PlayerSkinView>()).FirstOrDefault()?.Angle??-.18f;float zoom=right.Controls.OfType<ReferencePanel>().SelectMany(p=>p.Controls.OfType<PlayerSkinView>()).FirstOrDefault()?.Zoom??1;int request=++generation;foreach(Control c in right.Controls.Cast<Control>().ToArray())c.Dispose();if(r==null){right.Controls.Add(Ui.Note("플레이어를 선택하세요."));return;}
        var p=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new(720,580)};right.Padding=Padding.Empty;right.Controls.Add(p);
        var face=new PictureBox{Image=faces.GetValueOrDefault(Key(r)),SizeMode=PictureBoxSizeMode.Zoom};p.Place(face,18,16,104,104);
        var name=Ui.Heading(r.Name);name.Font=new Font("Noto Sans KR",20,FontStyle.Bold);p.Place(name,154,12,390,42);detailStatus=Ui.Note(connected.Contains(r.Name)?"● 온라인":"● 오프라인");p.Place(detailStatus,550,18,150,34);
        var uuid=Ui.Note("UUID   "+(r.Uuid.Length>0?r.Uuid:"기록 없음"));uuid.Font=new Font("Noto Sans KR",10);p.Place(uuid,154,60,500,32);p.Place(Button("복사",()=>{if(r.Uuid.Length>0)Clipboard.SetText(r.Uuid);}),650,58,58,36);
        string[] tabs=["기본 정보","인벤토리","접속 이력"];for(int i=0;i<tabs.Length;i++){string t=tabs[i];var tab=Button(t,()=>{detailTab=t;BeginInvoke(new Action(()=>_=RenderDetails(r)));});tab.OutlineColor=detailTab==t?WarmTheme.Accent:WarmTheme.Line;tab.TextColor=detailTab==t?WarmTheme.Accent:WarmTheme.Ink;p.Place(tab,154+i*184,102,182,40);}
        var skin=new PlayerSkinView{Skin=skins.GetValueOrDefault(Key(r)),Angle=angle,Zoom=zoom};p.Place(skin,16,150,242,332);
        p.Place(Button("앞면",()=>{skin.Angle=0;skin.Invalidate();}),28,484,104,34);p.Place(Button("뒷면",()=>{skin.Angle=MathF.PI;skin.Invalidate();}),134,484,104,34);
        p.Place(Button("↶",()=>{skin.Angle-=.3f;skin.Invalidate();}),20,522,42,34);var zoomSlider=new TrackBar{Minimum=60,Maximum=160,Value=(int)(skin.Zoom*100),TickStyle=TickStyle.None,AutoSize=false,BackColor=WarmTheme.Surface,AccessibleName="스킨 확대 축소"};zoomSlider.ValueChanged+=(_,_)=>{skin.Zoom=zoomSlider.Value/100f;skin.Invalidate();};p.Place(zoomSlider,66,522,128,34);p.Place(Button("↷",()=>{skin.Angle+=.3f;skin.Invalidate();}),198,522,42,34);
        var hint=Ui.Note("드래그하여 회전 · 휠로 확대");hint.Font=new Font("Noto Sans KR",9);p.Place(hint,20,556,238,24);
        var content=new Panel{BackColor=WarmTheme.Surface};p.Place(content,274,152,430,416);
        var loading=Ui.Note("저장 정보를 읽는 중…");loading.Dock=DockStyle.Fill;content.Controls.Add(loading);
        PlayerSnapshot snapshot=await Task.Run(()=>PlayerSnapshot.Read(folder,r.Uuid));if(IsDisposed||request!=generation||content.IsDisposed)return;loading.Dispose();
        if(detailTab=="인벤토리"){inventoryView=new InventoryView(folder,snapshot){Dock=DockStyle.Fill};content.Controls.Add(inventoryView);return;}
        if(detailTab!="기본 정보"){
            var text=new RichTextBox{Dock=DockStyle.Fill,ReadOnly=true,BorderStyle=BorderStyle.None,BackColor=WarmTheme.Surface,ForeColor=WarmTheme.Ink,Font=new Font("Noto Sans KR",11)};
            text.Text=detailTab=="인벤토리"?"마지막 저장 기준 · 편집할 수 없습니다.\n\n인벤토리\n"+(snapshot.Inventory.Count>0?string.Join("\n\n",snapshot.Inventory):"기록 없음 / 비어 있음")+"\n\n엔더 상자\n"+(snapshot.EnderItems.Count>0?string.Join("\n\n",snapshot.EnderItems):"기록 없음 / 비어 있음"):"관리자가 기록한 접속 이력\n\n"+(r.Visits.Count==0?"기록 없음":string.Join("\n\n",r.Visits.AsEnumerable().Reverse().Select(v=>$"{Date(v.Start)}  접속 확인\n{(v.End.HasValue?Date(v.End):v.EndReason.Length>0?v.EndReason:connected.Contains(r.Name)?"접속 중":"종료 시각 없음")}")));
            content.Controls.Add(text);return;
        }
        var info=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new(430,416)};content.Controls.Add(info);
        string mode=snapshot.Mode switch{0=>"서바이벌",1=>"크리에이티브",2=>"모험",3=>"관전자",_=>"기록 없음"};var duration=TimeSpan.FromSeconds(r.ObservedSeconds);
        var values=new[]{("최초 기록",Date(r.FirstRecorded)),("마지막 접속",Date(r.LastJoined)),("누적 접속 시간",$"{(int)duration.TotalHours}시간 {duration.Minutes}분 {duration.Seconds}초"),("게임 모드",mode),("마지막 위치",snapshot.Position),("경험치 레벨",snapshot.Level?.ToString()??"기록 없음")};
        int y=0;foreach(var (label,value) in values){var a=Ui.Note(label);a.ForeColor=WarmTheme.Ink;a.Font=new Font("Noto Sans KR",11);var b=Ui.Note(value);b.ForeColor=WarmTheme.Ink;b.Font=new Font("Noto Sans KR",11);int h=label=="마지막 위치"?48:30;info.Place(a,4,y,125,h);info.Place(b,133,y,294,h);y+=h;}
        info.Place(new VitalMeter("체력",snapshot.Health,false),0,200,210,80);info.Place(new VitalMeter("배고픔",snapshot.Food,true),218,200,210,80);
        var saved=Ui.Note(snapshot.Error.Length>0?snapshot.Error:$"마지막 저장 {Date(snapshot.SavedAt)}\n실시간 정보가 아닙니다. 접속 시간은 기록 이후 집계됩니다.");saved.Font=new Font("Noto Sans KR",9);info.Place(saved,0,282,430,48);
        bool allowed=false,op=false,banned=false;try{bool Has(string f)=>PlayerData.Read(folder,f).Any(x=>string.Equals((string?)x?["name"],r.Name,StringComparison.OrdinalIgnoreCase));allowed=Has("whitelist.json");op=Has("ops.json");banned=Has("banned-players.json");}catch{}
        var role=Ui.Note($"서버 권한\n화이트리스트 {(allowed?"등록":"미등록")} · 관리자 {(op?"권한 있음":"권한 없음")}");role.Font=new Font("Noto Sans KR",10);info.Place(role,0,332,430,50);
        string[] cmds=[allowed?"whitelist remove":"whitelist add",op?"deop":"op",banned?"pardon":"ban"];string[] labels=[allowed?"허용 해제":"화이트리스트",op?"권한 해제":"관리자 부여",banned?"블랙리스트 해제":"블랙리스트"];
        for(int i=0;i<3;i++){string cmd=cmds[i];var button=Button(labels[i],async()=>{try{await action(cmd,r.Name);refresh();}catch(Exception e){WarmDialog.Show(FindForm()!,e.Message);}});button.Font=new Font("Noto Sans KR",9);button.Enabled=r.Name!="이름 미확인";info.Place(button,i*144,384,140,32);}
    }
    protected override void Dispose(bool disposing){if(disposing){generation++;foreach(var i in skins.Values)i.Dispose();foreach(var i in faces.Values)i.Dispose();}base.Dispose(disposing);}
}
public sealed class PlayerTile(PlayerRecord record,bool online,Image? face,bool selected):Control
{
    public PlayerRecord Record=>record;public Image? Face {get;set;}=face;
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(WarmTheme.Surface);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using var path=WarmTheme.Round(new Rectangle(1,1,Width-3,Height-3),14);using var fill=new SolidBrush(selected?WarmTheme.Peach:WarmTheme.Surface);using var pen=new Pen(selected?WarmTheme.Accent:WarmTheme.Line);e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(pen,path);if(Face!=null)e.Graphics.DrawImage(Face,new Rectangle(14,16,64,64));else ReferenceIcons.Draw(e.Graphics,"users",new(24,26,44,44),WarmTheme.Muted);using var bold=new Font("Noto Sans KR",11,FontStyle.Bold);using var small=new Font("Noto Sans KR",9);TextRenderer.DrawText(e.Graphics,record.Name,bold,new Rectangle(88,18,Width-96,42),WarmTheme.Ink,TextFormatFlags.WordBreak);TextRenderer.DrawText(e.Graphics,online?"● 온라인":"● 오프라인",small,new Rectangle(88,64,Width-96,30),online?Color.SeaGreen:WarmTheme.Muted);TextRenderer.DrawText(e.Graphics,"마지막 접속 "+(record.LastJoined?.ToLocalTime().ToString("MM.dd HH:mm")??"기록 없음"),small,new Rectangle(14,105,Width-24,26),WarmTheme.Muted,TextFormatFlags.EndEllipsis);}
}
public sealed class VitalMeter(string title,double? value,bool food):SurfaceCard
{
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using var font=new Font("Noto Sans KR",10,FontStyle.Bold);TextRenderer.DrawText(e.Graphics,title+"  "+(value.HasValue?value.Value.ToString("0.#")+(food?" / 20":""):"기록 없음"),font,new Rectangle(12,8,Width-20,28),WarmTheme.Ink);if(!value.HasValue)return;float unit=(Width-24)/10f;for(int i=0;i<10;i++){var r=new RectangleF(12+i*unit,44,unit-3,18);using var path=new System.Drawing.Drawing2D.GraphicsPath();if(food){path.AddEllipse(r.X+2,r.Y,r.Width-2,13);path.AddRectangle(new RectangleF(r.X,r.Y+11,5,7));}else{path.AddPolygon(new[]{new PointF(r.X,r.Y+5),new PointF(r.X+3,r.Y),new PointF(r.X+r.Width/2,r.Y+3),new PointF(r.Right-3,r.Y),new PointF(r.Right,r.Y+5),new PointF(r.Right,r.Y+10),new PointF(r.X+r.Width/2,r.Bottom),new PointF(r.X,r.Y+10)});}e.Graphics.FillPath(Brushes.LightGray,path);var state=e.Graphics.Save();e.Graphics.SetClip(new RectangleF(r.X,r.Y,r.Width*(float)Math.Clamp((value.Value-i*2)/2,0,1),r.Height));using var b=new SolidBrush(food?Color.Goldenrod:Color.Coral);e.Graphics.FillPath(b,path);e.Graphics.Restore(state);}}
}
