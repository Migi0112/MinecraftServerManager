// [소스 안내] 모드/플러그인 및 플레이어 목록에 쓰는 카드·표 보기입니다. 선택 상태, 이미지, 행 동작과 공통 이미지 선택기도 포함합니다.
using System.IO.Compression;
using System.Text.Json;
namespace NeoForgeServerManager;

public record CatalogItem(string Id, string Name, string Detail, string State, Image? Picture = null,string Version="—",string Permission="없음",ModPluginService.Result? Project=null);
// 체크박스 클릭은 기존 다중 선택을 지우지 않고 해당 항목만 토글합니다.
public sealed class SelectionGrid : DataGridView
{
    public SelectionGrid(){DoubleBuffered=true;}
    protected override void OnCellPainting(DataGridViewCellPaintingEventArgs e)
    {
        if(e.Graphics==null){base.OnCellPainting(e);return;}
        var state=e.Graphics.Save();
        try
        {
            // 셀별로 그리기 범위와 상태를 격리해 아이콘/체크박스가 다른 셀에 영향을 주지 않게 합니다.
            e.Graphics.SetClip(Rectangle.Intersect(e.CellBounds,e.ClipBounds),System.Drawing.Drawing2D.CombineMode.Intersect);
            e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.None;
            var style=e.CellStyle??DefaultCellStyle;
            var color=e.RowIndex<0?ColumnHeadersDefaultCellStyle.BackColor:
                (e.State&DataGridViewElementStates.Selected)!=0?style.SelectionBackColor:style.BackColor;
            using(var brush=new SolidBrush(color))e.Graphics.FillRectangle(brush,e.CellBounds);
            base.OnCellPainting(e);
            if(!e.Handled)e.Paint(e.ClipBounds,DataGridViewPaintParts.All);
            e.Handled=true;
        }
        finally{e.Graphics.Restore(state);}
    }
    public event Action<int>? ToggleRequested;
    protected override void OnMouseDown(MouseEventArgs e)
    {
        var hit=HitTest(e.X,e.Y);
        if(e.Button==MouseButtons.Left&&hit.ColumnIndex>=0&&Columns[hit.ColumnIndex].Name=="Select")
        {ToggleRequested?.Invoke(hit.RowIndex);return;}
        base.OnMouseDown(e);
    }
}
// 카드/자세히 보기가 동일한 선택 집합을 공유합니다. 검색해도 숨겨진 항목의 선택은 유지합니다.
public sealed class CatalogView : UserControl
{
    readonly WarmTextBox search = new() { Width = 330, PlaceholderText = "이름 또는 파일명 검색", LeadingIcon = "search" }; readonly FlowLayoutPanel grid = new() { Dock = DockStyle.Fill, AutoScroll = true }; readonly SelectionGrid table = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowTemplate = { Height = 72 } };
    readonly Label count = Ui.Note(""); readonly Panel body = new() { Dock = DockStyle.Fill }; readonly HashSet<string> selected = []; List<CatalogItem> items = []; bool refreshing; int anchor = -1;
    public FlowLayoutPanel Actions { get; } = new() { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(6) };
    readonly Label heading;
    WarmButton? iconView,detailView;
    public string Title { get => heading.Text; set => heading.Text = value; }
    string cardAction="";
    public string CardAction { get=>cardAction;set{cardAction=value;if(table.Columns["Remove"] is DataGridViewButtonColumn remove)remove.Text=value;} }
    public FlowLayoutPanel Inputs {get;} = new(){Dock=DockStyle.Top,AutoSize=true,Padding=new Padding(0,12,0,12)};
    public string EditLabel {set {if(table.Columns["Edit"] is DataGridViewButtonColumn edit)edit.Text=value;}}
    public event Action<CatalogItem, string>? RowAction;
    public string[] SelectedIds => selected.ToArray();
    public CatalogView(string title, bool rowActions = false)
    {
        Dock=DockStyle.Fill;Padding=new Padding(12,8,12,8);
        if(rowActions)search.PlaceholderText="플레이어 이름 검색";
        var top=new Panel{Dock=DockStyle.Top,Height=100};heading=Ui.Heading(title.Replace(" (ops)",""),24);heading.AutoSize=false;heading.SetBounds(0,0,440,52);
        var description=Ui.Note(title=="운영자 (ops)"?"서버 관리 권한을 가진 플레이어를 관리해요.":title=="화이트리스트"?"서버 접속을 허용할 플레이어를 관리해요.":title=="블랙리스트"?"서버 접속이 제한된 플레이어를 관리해요.":"검색하고 선택하여 한 번에 관리하세요.");description.AutoSize=false;description.SetBounds(0,52,600,36);
        var icons=Ui.Action("아이콘",()=>ShowMode(false));var details=Ui.Action("자세히",()=>ShowMode(true));icons.IconName="grid";details.IconName="list";iconView=icons;detailView=details;icons.Primary=true;
        top.Controls.AddRange([heading,description,search,icons,details]);
        top.Resize+=(_,_)=>{search.SetBounds(Math.Max(360,top.Width-600),10,330,50);icons.AutoSize=details.AutoSize=false;icons.SetBounds(top.Width-254,10,120,50);details.SetBounds(top.Width-124,10,120,50);};
        var selection = Ui.Row(Ui.Action("전체 선택", () => { foreach (var i in VisibleItems()) selected.Add(i.Id); Render(); }), Ui.Action("선택 해제", () => { selected.Clear(); Render(); }), count); selection.Dock = DockStyle.Top;
        Controls.Add(body); Controls.Add(Actions); Controls.Add(selection); Controls.Add(Inputs); Controls.Add(top); body.Controls.Add(grid); body.Controls.Add(table); table.Visible = false;
        if(title=="모드 · 플러그인 관리"){description.Visible=false;top.Height=70;Controls.Remove(Inputs);Inputs.Dock=DockStyle.None;Inputs.Padding=Padding.Empty;selection.Controls.Add(Inputs);}
        grid.Resize+=(_,_)=>{int columns=Math.Max(1,grid.ClientSize.Width/280);foreach(Control c in grid.Controls)c.Width=Math.Max(220,(grid.ClientSize.Width-18)/columns-14);};
        table.Font=new Font("Noto Sans KR",14);table.ColumnHeadersHeight=56;table.RowTemplate.Height=80;table.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;table.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None;
        table.DefaultCellStyle.SelectionBackColor=Color.FromArgb(255,241,235);table.DefaultCellStyle.SelectionForeColor=WarmTheme.Ink;table.DefaultCellStyle.Padding=new Padding(10,0,10,0);
        table.Columns.Add(new DataGridViewImageColumn { Name = "Image", HeaderText = "이미지", ImageLayout = DataGridViewImageCellLayout.Zoom, FillWeight = 15 }); table.Columns.Add("Name", "이름"); table.Columns.Add("Detail", "정보 / 사유"); table.Columns.Add("State", "상태 / 날짜");
        if (rowActions) { table.Columns.Add(new DataGridViewButtonColumn { Name = "Edit", HeaderText = "", Text = "수정", UseColumnTextForButtonValue = true, FillWeight = 30 }); table.Columns.Add(new DataGridViewButtonColumn { Name = "Remove", HeaderText = "", Text = "해제", UseColumnTextForButtonValue = true, FillWeight = 30 }); }
        if(title=="운영자 (ops)")
        {
            table.Columns["Image"].HeaderText="";table.Columns["Image"].FillWeight=16;
            table.Columns["Name"].HeaderText="플레이어";table.Columns["Name"].FillWeight=60;
            table.Columns["Detail"].HeaderText="권한 수준";table.Columns["Detail"].FillWeight=40;
            table.Columns["State"].Visible=false;
            table.Columns["Edit"].FillWeight=25;table.Columns["Remove"].FillWeight=25;
        }
        table.Columns["Image"].HeaderText="";table.Columns["Image"].AutoSizeMode=DataGridViewAutoSizeColumnMode.None;table.Columns["Image"].Width=86;
        table.Columns["Name"].HeaderText=title=="모드 · 플러그인 관리"?"파일 이름":"플레이어";
        if(rowActions){foreach(string column in new[]{"Edit","Remove"}){table.Columns[column].AutoSizeMode=DataGridViewAutoSizeColumnMode.None;table.Columns[column].Width=148;}}
        table.Columns.Add(new DataGridViewCheckBoxColumn{Name="Select",HeaderText="",Width=42,AutoSizeMode=DataGridViewAutoSizeColumnMode.None});table.Columns["Select"].DisplayIndex=0;
        if(title=="모드 · 플러그인 관리")
        {
            table.Columns.Add("Environment","설치 위치");table.Columns["Environment"].AutoSizeMode=DataGridViewAutoSizeColumnMode.None;table.Columns["Environment"].Width=250;table.Columns.Add("Filename","파일명");table.Columns.Add("Version","버전");table.Columns["Filename"].DisplayIndex=3;table.Columns["Version"].DisplayIndex=4;
            table.Columns["Detail"].HeaderText="크기";table.Columns["State"].HeaderText="상태";table.RowTemplate.Height=54;
        }
        if(title=="접속자")
        {
            table.Columns["Edit"].Visible=table.Columns["Remove"].Visible=false;table.Columns["Detail"].HeaderText="화이트리스트";table.Columns["State"].HeaderText="상태";table.Columns["State"].DisplayIndex=3;table.Columns.Add("Permission","관리자 권한");table.RowTemplate.Height=102;
        }
        table.ToggleRequested+=row=>{if(row<0){var visible=VisibleItems().ToArray();bool allSelected=visible.All(i=>selected.Contains(i.Id));foreach(var item in visible){if(allSelected)selected.Remove(item.Id);else selected.Add(item.Id);}}else if(row<table.Rows.Count){string id=((CatalogItem)table.Rows[row].Tag!).Id;if(!selected.Add(id))selected.Remove(id);}Render();};
        table.CellPainting+=(_,e)=>
        {
            if(e.RowIndex>=0&&e.ColumnIndex>=0&&table.Columns[e.ColumnIndex].Name=="Environment"){
                e.PaintBackground(e.ClipBounds,true);var item=(CatalogItem)table.Rows[e.RowIndex].Tag!;using var badgeFont=new Font("Noto Sans KR",9);ModEnvironment.Draw(e.Graphics!,new Rectangle(e.CellBounds.X+6,e.CellBounds.Y+(e.CellBounds.Height-28)/2,e.CellBounds.Width-12,28),item.Project,badgeFont);e.Handled=true;return;
            }
            if(e.ColumnIndex>=0&&table.Columns[e.ColumnIndex].Name=="Select")
            {
                e.PaintBackground(e.ClipBounds,true);bool check=e.RowIndex>=0?selected.Contains(((CatalogItem)table.Rows[e.RowIndex].Tag!).Id):VisibleItems().Any()&&VisibleItems().All(i=>selected.Contains(i.Id));
                var box=new Rectangle(e.CellBounds.X+(e.CellBounds.Width-24)/2,e.CellBounds.Y+(e.CellBounds.Height-24)/2,24,24);e.Graphics!.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var shape=WarmTheme.Round(box,5);using var checkFill=new SolidBrush(check?WarmTheme.Accent:WarmTheme.Surface);using var checkPen=new Pen(check?WarmTheme.Accent:WarmTheme.Ink,2);e.Graphics.FillPath(checkFill,shape);e.Graphics.DrawPath(checkPen,shape);
                if(check)TextRenderer.DrawText(e.Graphics,"✓",table.Font,box,Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);e.Handled=true;return;
            }
            if(e.RowIndex<0||e.ColumnIndex<0||table.Columns[e.ColumnIndex] is not DataGridViewButtonColumn)return;
            e.PaintBackground(e.ClipBounds,true);var rect=Rectangle.Inflate(e.CellBounds,-7,-14);
            e.Graphics!.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var path=WarmTheme.Round(rect,9);using var fill=new SolidBrush(WarmTheme.Surface);using var pen=new Pen(table.Columns[e.ColumnIndex].Name=="Remove"?WarmTheme.Accent:WarmTheme.Line);
            e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(pen,path);TextRenderer.DrawText(e.Graphics,e.FormattedValue?.ToString(),table.Font,rect,table.Columns[e.ColumnIndex].Name=="Remove"?WarmTheme.Accent:WarmTheme.Ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);e.Handled=true;
        };
        table.CellClick+=(_,e)=>{if((Title is "모드 관리" or "플러그인 관리")&&e.RowIndex>=0&&e.ColumnIndex>=0&&table.Columns[e.ColumnIndex].Name!="Select")RowAction?.Invoke((CatalogItem)table.Rows[e.RowIndex].Tag!,"Details");};
        table.SelectionChanged += (_, _) => { if (refreshing) return; foreach (var i in VisibleItems()) selected.Remove(i.Id); foreach (DataGridViewRow row in table.SelectedRows) selected.Add(((CatalogItem)row.Tag!).Id); foreach(DataGridViewRow row in table.Rows)row.Cells["Select"].Value=row.Selected; count.Text = $"{selected.Count}개 선택됨"; }; table.CellContentClick += (_, e) => { if(e.RowIndex>=0&&e.ColumnIndex>=0&&table.Columns[e.ColumnIndex].Name=="Select"){table.Rows[e.RowIndex].Cells["Select"].Value=table.Rows[e.RowIndex].Selected;return;} if (e.RowIndex >= 0 && e.ColumnIndex>=0 && table.Columns[e.ColumnIndex] is DataGridViewButtonColumn) RowAction?.Invoke((CatalogItem)table.Rows[e.RowIndex].Tag!, table.Columns[e.ColumnIndex].Name); }; search.TextChanged += (_, _) => Render();
    }
    IEnumerable<CatalogItem> VisibleItems() => items.Where(i => (i.Id + " " + i.Name + " " + i.Detail).Contains(search.Text, StringComparison.OrdinalIgnoreCase));
    public void SetItems(IEnumerable<CatalogItem> values) { var next=values.ToList();if(items.SequenceEqual(next))return;items = next; selected.IntersectWith(items.Select(i => i.Id)); Render(); }
    public void ShowMode(bool details) { table.Visible = details; grid.Visible = !details; if(iconView!=null){iconView.Primary=!details;iconView.Invalidate();}if(detailView!=null){detailView.Primary=details;detailView.Invalidate();} Render(); }
    void Render()
    {
        refreshing = true; grid.SuspendLayout(); foreach (Control c in grid.Controls.Cast<Control>().ToArray()) c.Dispose(); table.Rows.Clear(); var visible = VisibleItems().ToList();
        for (int index = 0; index < visible.Count; index++)
        {
            int currentIndex = index; var item = visible[index]; var card = new CatalogCard(item) { Selected = selected.Contains(item.Id), QuickLabel = CardAction, QuickAction = () => RowAction?.Invoke(item, "Remove") }; card.Click += (_, _) => { if (Title is "모드 관리" or "플러그인 관리" && card.PointToClient(Cursor.Position).X>48) { RowAction?.Invoke(item,"Details"); return; } if ((ModifierKeys & Keys.Shift) != 0 && anchor >= 0) { for (int j = Math.Min(anchor, currentIndex); j <= Math.Max(anchor, currentIndex) && j < visible.Count; j++) selected.Add(visible[j].Id); } else { if (!selected.Add(item.Id)) selected.Remove(item.Id); anchor = currentIndex; } Render(); }; card.DoubleClick += (_,_)=>RowAction?.Invoke(item,"Details");grid.Controls.Add(card);card.Width=Math.Max(220,(grid.ClientSize.Width-18)/Math.Max(1,grid.ClientSize.Width/280)-14);card.Height=(Title is "모드 관리" or "플러그인 관리" or "모드 · 플러그인 관리")?274:CardAction.Length>0?270:240;
            int row = table.Rows.Add(item.Picture ?? Artwork.Default, item.Name, item.Detail, item.State); table.Rows[row].Tag = item; table.Rows[row].Selected = selected.Contains(item.Id);table.Rows[row].Cells["Select"].Value=selected.Contains(item.Id);
            if(table.Columns.Contains("Filename")){table.Rows[row].Cells["Filename"].Value=item.Id;table.Rows[row].Cells["Version"].Value=item.Version;table.Rows[row].Cells["Environment"].Value=ModEnvironment.Summary(item.Project);}
            if(table.Columns.Contains("Permission"))table.Rows[row].Cells["Permission"].Value=item.Permission;
        }
        count.Text = $"{items.Count}개 · {selected.Count}개 선택됨"; if(selected.Count==0){table.CurrentCell=null;table.ClearSelection();} grid.ResumeLayout(); refreshing = false;
    }
}
public sealed class CatalogCard(CatalogItem item) : Control
{
    public bool Selected { get; set; }
    public string QuickLabel { get; set; } = ""; public Action? QuickAction;
    protected override void OnClick(EventArgs e) { if (QuickLabel.Length > 0 && PointToClient(Cursor.Position).Y > Height - 34) { QuickAction?.Invoke(); return; } base.OnClick(e); }
    protected override Size DefaultSize => new(255, (item.Id.EndsWith(".jar",StringComparison.OrdinalIgnoreCase)||item.Id.EndsWith(".disabled",StringComparison.OrdinalIgnoreCase))?274:240);
    protected override void OnCreateControl() { base.OnCreateControl(); Margin = new Padding(7); Cursor = Cursors.Hand; DoubleBuffered = true; SetStyle(ControlStyles.StandardDoubleClick,true); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;g.Clear(Parent?.BackColor??WarmTheme.Surface);using var shape=WarmTheme.Round(new Rectangle(1,1,Width-3,Height-3),14);using var fill=new SolidBrush(Selected?WarmTheme.Peach:WarmTheme.Surface);using var pen=new Pen(Selected?WarmTheme.Accent:WarmTheme.Line,Selected?2:1);g.FillPath(fill,shape);g.DrawPath(pen,shape);
        Artwork.DrawFit(g,item.Picture??Artwork.Default,new Rectangle((Width-104)/2,24,104,104));
        var check=new Rectangle(18,18,24,24);using(var path=WarmTheme.Round(check,5)){if(Selected){using var accent=new SolidBrush(WarmTheme.Accent);g.FillPath(accent,path);}else g.DrawPath(pen,path);}if(Selected)TextRenderer.DrawText(g,"✓",Font,check,Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        using var bold=new Font("Noto Sans KR",15,FontStyle.Bold);using var normal=new Font("Noto Sans KR",12);
        bool jar=item.Id.EndsWith(".jar",StringComparison.OrdinalIgnoreCase)||item.Id.EndsWith(".disabled",StringComparison.OrdinalIgnoreCase);string name=item.Name;
        TextRenderer.DrawText(g,name,bold,new Rectangle(12,133,Width-24,32),WarmTheme.Ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g,jar?Path.GetFileName(item.Id):item.Detail,normal,new Rectangle(12,167,Width-24,28),WarmTheme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g,(jar?item.Detail+"   ":"")+item.State,normal,new Rectangle(12,200,Width-24,28),item.State=="활성"?Color.SeaGreen:WarmTheme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.EndEllipsis);
        if(jar){using var badgeFont=new Font("Noto Sans KR",9);ModEnvironment.Draw(g,new Rectangle(12,234,Width-24,28),item.Project,badgeFont);}
        if(QuickLabel.Length>0)TextRenderer.DrawText(g,QuickLabel,normal,new Rectangle(12,Height-30,Width-24,26),WarmTheme.Accent,TextFormatFlags.Right|TextFormatFlags.VerticalCenter);
    }
}
public static class Artwork
{
    static readonly Lazy<Bitmap> icon = new(() => { using var s = typeof(Artwork).Assembly.GetManifestResourceStream("NeoForgeServerManager.Assets.app-icon.png")!; using var i = Image.FromStream(s); return new Bitmap(i); });
    public static Bitmap Default => icon.Value;
    // 지정한 상자 안에 원본 비율 그대로 맞춥니다. 가로/세로를 따로 늘리지 않습니다.
    public static Rectangle Fit(Image image,Rectangle box)
    {double scale=Math.Min(box.Width/(double)image.Width,box.Height/(double)image.Height);int w=(int)Math.Round(image.Width*scale),h=(int)Math.Round(image.Height*scale);return new Rectangle(box.X+(box.Width-w)/2,box.Y+(box.Height-h)/2,w,h);}
    public static void DrawFit(Graphics g,Image image,Rectangle box)=>g.DrawImage(image,Fit(image,box));
    // 승인 이미지 중 로고 자산만 사용합니다. 화면 배경이나 버튼을 이미지로 대체하지 않습니다.
    public static Bitmap Brand()
    {
        using var stream=typeof(Artwork).Assembly.GetManifestResourceStream("NeoForgeServerManager.Assets.brand-reference.png")!;
        using var atlas=Image.FromStream(stream);var result=new Bitmap(80,80);using var g=Graphics.FromImage(result);
        g.DrawImage(atlas,new Rectangle(0,0,80,80),new Rectangle(38,12,80,80),GraphicsUnit.Pixel);return result;
    }
    public static Image Read(string? path) { if (path != null && path.StartsWith("preset:")) { int n = int.Parse(path[7..]); using var atlasStream = typeof(Artwork).Assembly.GetManifestResourceStream("NeoForgeServerManager.Assets.presets.png")!; using var atlas = Image.FromStream(atlasStream); var image = new Bitmap(170, 178); using var g = Graphics.FromImage(image); g.DrawImage(atlas, new Rectangle(0, 0, 170, 178), new Rectangle(n == 0 ? 60 : n == 1 ? 562 : 1038, 296, 170, 178), GraphicsUnit.Pixel); return image; } if (string.IsNullOrEmpty(path) || !File.Exists(path)) return new Bitmap(Default); using var stream = File.OpenRead(path); using var img = Image.FromStream(stream); return new Bitmap(img); }
    public static string Import(IWin32Window owner) { using var d = new OpenFileDialog { Filter = "이미지|*.png;*.jpg;*.jpeg" }; if (d.ShowDialog(owner) != DialogResult.OK) return ""; if (new FileInfo(d.FileName).Length > 20_000_000) throw new IOException("20MB 이하 이미지를 선택하세요."); using var img = Read(d.FileName); Directory.CreateDirectory(Path.Combine(Storage.Root, "images")); string target = Path.Combine(Storage.Root, "images", Guid.NewGuid().ToString("N") + ".png"); using var resized = new Bitmap(img, Artwork.Fit(img,new Rectangle(0,0,512,512)).Size); resized.Save(target, System.Drawing.Imaging.ImageFormat.Png); return target; }
}
public sealed class ImagePicker : UserControl
{
    public string ImagePath {get;private set;}="";
    public event Action? ImageChanged;
    readonly PictureBox preview=new(){SizeMode = PictureBoxSizeMode.Zoom};
    readonly ServerImagePreview cardPreview=new();
    public FlowLayoutPanel EditorControls {get;}=new(){FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};
    public ImagePicker(string path="")
    {
        ImagePath=path;Dock=DockStyle.Fill;
        var layout=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new Size(1424,620)};Controls.Add(layout);
        ReferencePanel Section(int x,int w){var card=new SurfaceCard{Padding=Padding.Empty};layout.Place(card,x,0,w,620);var panel=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new Size(w,620),BackColor=WarmTheme.Surface};card.Controls.Add(panel);return panel;}
        var left=Section(0,720);var right=Section(736,688);
        left.Place(Ui.Heading("서버 이미지",23),24,12,650,52);left.Place(preview,20,74,680,302);
        var choose=Ui.Action("이미지 선택",()=>{try{string next=Artwork.Import(this);if(next.Length>0)SetPath(next);}catch(Exception error){WarmDialog.Show(this,error.Message);}});choose.Font=new Font("Noto Sans KR",15,FontStyle.Bold);left.Place(choose,20,390,320,52);
        var reset=Ui.Action("기본 이미지로",()=>SetPath(""));reset.Font=choose.Font;left.Place(reset,350,390,350,52);
        var hint=Ui.Note("PNG · JPG 이미지 지원");hint.TextAlign=ContentAlignment.MiddleCenter;left.Place(hint,20,445,680,36);
        for(int i=0;i<4;i++){string choice=i==0?"":"preset:"+(i-1);var picture=new PictureBox{Image=Artwork.Read(choice),SizeMode=PictureBoxSizeMode.Zoom,Cursor=Cursors.Hand};left.Place(picture,20+i*172,496,156,108);picture.Click+=(_,_)=>SetPath(choice);picture.Disposed+=(_,_)=>picture.Image?.Dispose();}
        right.Place(Ui.Heading("서버목록 미리보기",23),24,12,640,52);right.Place(cardPreview,20,84,648,430);
        var note=Ui.Note("선택한 이미지는 메인 화면과 서버 관리 화면에 표시됩니다.");note.TextAlign=ContentAlignment.MiddleCenter;right.Place(note,20,524,648,40);
        right.Place(EditorControls,20,566,648,50);RefreshImage();
    }
    public void SetProfile(string name,string kind,string version,int min,int max){cardPreview.ServerName=name;cardPreview.Software=kind;cardPreview.Version=version;cardPreview.Memory=$"{min} ~ {max} GB";cardPreview.Invalidate();}
    public void SetPath(string path){ImagePath=path;RefreshImage();ImageChanged?.Invoke();}
    void RefreshImage(){preview.Image?.Dispose();preview.Image=Artwork.Read(ImagePath);cardPreview.Picture=preview.Image;cardPreview.Invalidate();}
    protected override void Dispose(bool disposing){if(disposing)preview.Image?.Dispose();base.Dispose(disposing);}
}
// 명확하게 '미리보기'라고 표시된 비상호작용 카드입니다. 서버 실행은 메인 화면의 실제 버튼에서만 합니다.
public sealed class ServerImagePreview : SurfaceCard
{
    public Image? Picture{get;set;}
    public string ServerName{get;set;}="내 서버";
    public string Software{get;set;}=""; public string Version{get;set;}=""; public string Memory{get;set;}="";
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var g=e.Graphics;float sx=Width/648f,sy=Height/430f,scale=Math.Min(sx,sy);
        Rectangle R(int x,int y,int w,int h)=>new((int)(x*sx),(int)(y*sy),(int)(w*sx),(int)(h*sy));
        using var title=new Font("Noto Sans KR",Math.Max(13,23*scale),FontStyle.Bold);using var text=new Font("Noto Sans KR",Math.Max(9,14*scale));using var bold=new Font("Noto Sans KR",Math.Max(10,15*scale),FontStyle.Bold);
        if(Picture!=null)Artwork.DrawFit(g,Picture,R(28,26,130,126));
        TextRenderer.DrawText(g,ServerName,title,R(178,38,448,58),WarmTheme.Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g,Software+" · Minecraft "+Version,text,R(178,100,448,40),WarmTheme.Muted,TextFormatFlags.Left|TextFormatFlags.EndEllipsis);
        using var pen=new Pen(WarmTheme.Line);g.DrawLine(pen,28*sx,174*sy,Width-28*sx,174*sy);
        string[] labels=["마지막 실행","접속자","할당 RAM"],values=["아직 실행 안 함","—",Memory],icons=["clock","users","memory"];
        for(int i=0;i<3;i++){int y=194+i*44;ReferenceIcons.Draw(g,icons[i],R(30,y,26,26),WarmTheme.Ink);TextRenderer.DrawText(g,labels[i],bold,R(76,y-5,160,38),WarmTheme.Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);TextRenderer.DrawText(g,values[i],text,R(240,y-5,378,38),WarmTheme.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);}
        var start=R(28,334,290,70);var manage=R(330,334,290,70);
        using(var shape=WarmTheme.Round(start,12))using(var fill=new SolidBrush(WarmTheme.Accent))g.FillPath(fill,shape);
        using(var shape=WarmTheme.Round(manage,12))g.DrawPath(pen,shape);
        TextRenderer.DrawText(g,"▶ 서버 시작",bold,start,Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);TextRenderer.DrawText(g,"관리 →",bold,manage,WarmTheme.Ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
    }
}
// JAR 내부에 선언된 로고가 있을 때만 읽고, 없는 모드는 공통 아이콘을 사용합니다.
public static class ModFiles
{
    public static (string Name,string Version) Metadata(string file)
    {
        string fallback=Path.GetFileNameWithoutExtension(file.EndsWith(".disabled",StringComparison.OrdinalIgnoreCase)?file[..^9]:file);
        try
        {
            using var zip=ZipFile.OpenRead(file);var entry=zip.GetEntry("fabric.mod.json");
            if(entry!=null&&entry.Length<200000){using var stream=entry.Open();using var json=JsonDocument.Parse(stream);return(json.RootElement.TryGetProperty("name",out var name)?name.ToString():fallback,json.RootElement.TryGetProperty("version",out var version)?version.ToString():"—");}
            entry=zip.GetEntry("META-INF/neoforge.mods.toml")??zip.GetEntry("META-INF/mods.toml")??zip.GetEntry("plugin.yml")??zip.GetEntry("paper-plugin.yml");
            if(entry!=null&&entry.Length<200000)
            {
                using var reader=new StreamReader(entry.Open());string text=reader.ReadToEnd();
                string Field(string key){var match=System.Text.RegularExpressions.Regex.Match(text,@"(?m)^\s*"+key+"\\s*[:=]\\s*[\"']?([^\"'\\r\\n]+)");return match.Success?match.Groups[1].Value.Trim():"";}
                string name=Field("displayName");if(name.Length==0)name=Field("name");string version=Field("version");if(version.Contains("${")||version.Length==0)version="—";return(name.Length==0?fallback:name,version);
            }
        if(JarBranding.NestedMetadata(zip) is {} nested)return nested;
        }catch(InvalidDataException){}catch(IOException){}catch(JsonException){}
        return(fallback,"—");
    }
    public static Image Icon(string file){try{using var zip=ZipFile.OpenRead(file);return JarBranding.Icon(zip)??new Bitmap(Artwork.Default);}catch{return new Bitmap(Artwork.Default);}}
    public static void Change(string folder, IEnumerable<string> names, string operation)
    {
        if (operation is not ("enable" or "disable" or "delete")) throw new ArgumentException("알 수 없는 작업입니다."); string root = Path.GetFullPath(folder); var plan = new List<(string, string)>(); foreach (var name in names.Distinct(StringComparer.OrdinalIgnoreCase)) { if (Path.GetFileName(name) != name) throw new IOException("잘못된 파일 이름입니다."); string source = Path.Combine(root, name); if (!File.Exists(source)) throw new IOException("파일이 변경되었습니다. 새로고침하세요."); if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0) throw new IOException("링크 파일은 변경할 수 없습니다."); string target = operation == "delete" ? Path.Combine(root, "removed", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6], name) : operation == "enable" ? source.EndsWith(".disabled") ? source[..^9] : source : source.EndsWith(".disabled") ? source : source + ".disabled"; if (source != target && File.Exists(target)) throw new IOException("같은 파일이 이미 있습니다: " + target); if (source != target) plan.Add((source, target)); }
        var done = new Stack<(string, string)>(); try { foreach (var pair in plan) { Directory.CreateDirectory(Path.GetDirectoryName(pair.Item2)!); File.Move(pair.Item1, pair.Item2); done.Push(pair); } } catch { foreach (var pair in done) File.Move(pair.Item2, pair.Item1); throw; }
    }
}



