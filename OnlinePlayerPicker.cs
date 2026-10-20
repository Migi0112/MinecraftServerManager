// [소스 안내] 명령어 대상 선택 드롭다운입니다. 접속 중인 이름을 검색·선택하고 퇴장한 대상은 선택에서 해제합니다.
namespace NeoForgeServerManager;

public sealed class OnlinePlayerPicker:WarmButton
{
    readonly Func<string[]> readPlayers;
    readonly Func<string,Task<Image?>> loadFace;
    readonly Dictionary<string,Image> faces=new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> requested=new(StringComparer.OrdinalIgnoreCase);
    string[] players=[];
    ToolStripDropDown? popup;
    ExtensionResultsList? list;
    WarmTextBox? search;
    Label? summary;
    public string SelectedName {get;private set;}="";
    public event Action? SelectionChanged;
    public OnlinePlayerPicker(Func<string[]> readPlayers,Func<string,Task<Image?>>? loadFace=null)
    {
        this.readPlayers=readPlayers;this.loadFace=loadFace??PlayerData.Face;
        Text="접속 중인 플레이어 선택 ▾";Click+=(_,_)=>Open();
    }
    public void RefreshPlayers()
    {
        var next=readPlayers().Where(n=>System.Text.RegularExpressions.Regex.IsMatch(n,@"^[A-Za-z0-9_]{1,16}$")).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        if(players.SequenceEqual(next,StringComparer.OrdinalIgnoreCase))return;
        players=next;
        if(SelectedName.Length>0&&!players.Contains(SelectedName,StringComparer.OrdinalIgnoreCase)){SelectedName="";Text="접속 중인 플레이어 선택 ▾";SelectionChanged?.Invoke();}
        Filter();
    }
    void Filter()
    {
        if(list==null||list.IsDisposed)return;
        var keep=list.SelectedItem as string;int top=list.TopIndex;
        list.BeginUpdate();list.Items.Clear();list.Items.AddRange(players.Where(n=>n.Contains(search!.Text.Trim(),StringComparison.OrdinalIgnoreCase)).Cast<object>().ToArray());
        if(list.Items.Count>0){list.SelectedItem=list.Items.Cast<string>().FirstOrDefault(n=>n==keep||n==SelectedName)??list.Items[0];list.TopIndex=Math.Clamp(top,0,list.Items.Count-1);}
        list.EndUpdate();summary!.Text=players.Length==0?"접속 중인 플레이어가 없습니다":list.Items.Count==0?"검색 결과가 없습니다":$"접속 중 · {players.Length}명";
        foreach(string name in list.Items)if(requested.Add(name))_ = Face(name);
    }
    async Task Face(string name)
    {
        Image? image=null;try{image=await loadFace(name);if(IsDisposed){image?.Dispose();return;}if(image!=null){faces[name]=image;list?.Invalidate();}}catch{image?.Dispose();}
    }
    void Choose()
    {
        string? name=list?.SelectedItem as string;RefreshPlayers();
        if(name==null||!players.Contains(name,StringComparer.OrdinalIgnoreCase))return;
        SelectedName=name;Text=name+" ▾";popup?.Close();SelectionChanged?.Invoke();
    }
    void Open()
    {
        if(popup!=null)return;RefreshPlayers();
        var panel=new Panel{Size=new(Math.Max(390,Width),330),Padding=new Padding(10),BackColor=WarmTheme.Surface};
        search=new WarmTextBox{Dock=DockStyle.Top,Height=42,PlaceholderText="플레이어 이름 검색"};summary=Ui.Note("");summary.Dock=DockStyle.Top;summary.Height=38;summary.AutoSize=false;
        list=new ExtensionResultsList{Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=64,IntegralHeight=false};
        list.DrawItem+=(_,e)=>{if(e.Index<0)return;string name=(string)list.Items[e.Index];var row=Rectangle.Inflate(e.Bounds,-2,-3);using var shape=WarmTheme.Round(row,10);using var fill=new SolidBrush((e.State&DrawItemState.Selected)!=0?WarmTheme.Peach:WarmTheme.Surface);using var pen=new Pen((e.State&DrawItemState.Selected)!=0?WarmTheme.Accent:WarmTheme.Line);e.Graphics.FillPath(fill,shape);e.Graphics.DrawPath(pen,shape);var icon=new Rectangle(row.X+10,row.Y+8,40,40);if(faces.TryGetValue(name,out var face))e.Graphics.DrawImage(face,icon);else ReferenceIcons.Draw(e.Graphics,"users",icon,WarmTheme.Muted);TextRenderer.DrawText(e.Graphics,name,Font,new Rectangle(row.X+62,row.Y,row.Width-178,row.Height),WarmTheme.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);TextRenderer.DrawText(e.Graphics,"● 접속 중",Font,new Rectangle(row.Right-110,row.Y,104,row.Height),Color.SeaGreen,TextFormatFlags.VerticalCenter);};
        search.TextChanged+=(_,_)=>Filter();search.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;Choose();}else if(e.KeyCode==Keys.Down){list.Focus();e.Handled=true;}};list.MouseClick+=(_,_)=>Choose();list.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;Choose();}};
        panel.Controls.Add(list);panel.Controls.Add(summary);panel.Controls.Add(search);
        var host=new ToolStripControlHost(panel){AutoSize=false,Size=panel.Size,Margin=Padding.Empty,Padding=Padding.Empty};popup=new ToolStripDropDown{AutoSize=false,Size=new(panel.Width+2,panel.Height+2),Padding=new Padding(1)};popup.Items.Add(host);
        popup.Closed+=(_,_)=>{var old=popup;popup=null;list=null;search=null;summary=null;EventHandler? idle=null;idle=(_,_)=>{Application.Idle-=idle;old?.Dispose();};Application.Idle+=idle;};Filter();popup.Show(this,new Point(0,Height));search.Focus();
    }
    protected override void Dispose(bool disposing){if(disposing){popup?.Dispose();foreach(var image in faces.Values)image.Dispose();faces.Clear();}base.Dispose(disposing);}
}
