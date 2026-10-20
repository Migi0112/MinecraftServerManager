// [소스 안내] 공통 창·둥근 카드·확인/입력 대화상자 등 화면 구성 요소입니다.
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
namespace NeoForgeServerManager;

// 기본 회색 제목줄 대신 예시의 밝은 제목줄을 사용합니다. 드래그·창 크기 조절·최소화는 Windows 기능입니다.
public class WarmWindow : Form
{
    readonly WindowResizeInput resizeInput;
    readonly Label credit = new() { Text = "제작자 Migi0112", TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0,0,22,0), ForeColor = WarmTheme.Muted };
    readonly PictureBox appLogo = new() { SizeMode = PictureBoxSizeMode.Zoom };
    readonly Panel chrome = new() { Height = 50, BackColor = WarmTheme.Surface };
    readonly Label brand = new() { Text = "마인크래프트 서버 매니저", TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Noto Sans KR", 11, FontStyle.Bold) };
    readonly Button minimize = new WindowCaptionButton(0), maximize = new WindowCaptionButton(1), close = new WindowCaptionButton(2);
    static readonly Font CompactBrandFont = new("Noto Sans KR",14,FontStyle.Regular), WideBrandFont = new("Noto Sans KR",16,FontStyle.Bold);
    public bool Scenic {get;set;}
    // Buffer individual surfaces and batch navigation. WS_EX_COMPOSITED repeatedly repaints
    // embedded WinForms pages even while idle, so do not enable it on the window hierarchy.
    public IDisposable BatchVisualChange()=>new RedrawScope(this);
    sealed class RedrawScope : IDisposable {
        readonly WarmWindow owner; readonly bool active;
        public RedrawScope(WarmWindow owner){this.owner=owner;active=owner.IsHandleCreated&&owner.Visible;if(active)SendMessage(owner.Handle,0x000B,0,0);owner.SuspendLayout();}
        public void Dispose(){owner.ResumeLayout(true);if(active&&!owner.IsDisposed){SendMessage(owner.Handle,0x000B,1,0);owner.Invalidate(true);}}
    }
    bool wideBrand,embeddedChrome;
    static readonly Font CreditFont=new("Noto Sans KR",12), SubtitleFont=new("Noto Sans KR",13);
    readonly Label subtitle = new() { Text="Minecraft Server Manager",ForeColor=WarmTheme.Muted };
    public bool WideBrand { get=>wideBrand; set {wideBrand=value;if(TopLevel)Padding=new Padding(2,value?104:52,2,28);PerformLayout();Invalidate();} }
    public WarmWindow()
    {
        resizeInput=new WindowResizeInput(this);Application.AddMessageFilter(resizeInput);
        FormBorderStyle = FormBorderStyle.None; Padding = new Padding(2, 52, 2, 28); DoubleBuffered = true; StartPosition = FormStartPosition.CenterScreen;
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!); appLogo.Image = Artwork.Brand(); chrome.Controls.AddRange([appLogo, brand, subtitle, minimize, maximize, close]); Controls.Add(chrome); Controls.Add(credit);
        minimize.Click += (_, _) => WindowState = FormWindowState.Minimized; maximize.Click += (_, _) => Toggle(); close.Click += (_, _) => Close();
        void Drag(object? s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, 2, 0); } }
        brand.MouseDown += Drag; chrome.MouseDown += Drag; brand.DoubleClick += (_, _) => Toggle();
    }
    // 홈 창이 제목줄과 제작자 표시를 소유합니다. 내부 페이지에는 중복 창 테두리를 만들지 않습니다.
    public void EmbedInShell() { embeddedChrome=true;MinimumSize=Size.Empty;MaximumSize=Size.Empty;chrome.Visible = credit.Visible = false; Padding = Padding.Empty; Region = null; }
    void Toggle() { if (MaximizeBox) { MaximizedBounds = Screen.FromControl(this).WorkingArea; WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; } }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e); if (chrome == null || !TopLevel||embeddedChrome) return; chrome.SetBounds(2, 2, Math.Max(0, ClientSize.Width - 4), WideBrand?100:48); chrome.BringToFront(); appLogo.SetBounds(WideBrand?38:18, WideBrand?14:8, WideBrand?80:32, WideBrand?80:32); brand.SetBounds(WideBrand?134:58, WideBrand?10:0, Math.Max(0, chrome.Width - 250), WideBrand?44:48);
        brand.Font=WideBrand?WideBrandFont:CompactBrandFont;subtitle.Font=SubtitleFont;subtitle.SetBounds(136,58,Math.Max(0,chrome.Width-300),34);subtitle.Visible=WideBrand; credit.Font=CreditFont; credit.BackColor=WarmTheme.Background; credit.SetBounds(0, ClientSize.Height - 28, ClientSize.Width, 28); credit.BringToFront();
        close.SetBounds(chrome.Width - 54, 5, 44, 36); maximize.SetBounds(chrome.Width - 103, 5, 44, 36); minimize.SetBounds(chrome.Width - 152, 5, 44, 36); maximize.Visible = MaximizeBox; minimize.Visible = MinimizeBox; minimize.BringToFront(); maximize.BringToFront(); close.BringToFront();
    }
    protected override void OnResize(EventArgs e) { base.OnResize(e); if (!TopLevel) return; if (Width < 2 || Height < 2) return; using var path = WarmTheme.Round(ClientRectangle, WindowState == FormWindowState.Maximized ? 0 : 14); var old = Region; Region = new Region(path); old?.Dispose(); }
    protected override void OnPaintBackground(PaintEventArgs e){if(Scenic){Scenery.Paint(e.Graphics,this);if(TopLevel){using var fill=new SolidBrush(WarmTheme.Background);e.Graphics.FillRectangle(fill,0,Height-28,Width,28);e.Graphics.FillRectangle(fill,0,0,2,Height);e.Graphics.FillRectangle(fill,Width-2,0,2,Height);}}else base.OnPaintBackground(e);}
    protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); if (!TopLevel) return; e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using var pen = new Pen(WarmTheme.Line); using var path = WarmTheme.Round(new Rectangle(1, 1, Width - 3, Height - 3), 14); e.Graphics.DrawPath(pen, path); if(!embeddedChrome)e.Graphics.DrawLine(pen, 1, WideBrand?103:51, Width - 2, WideBrand?103:51); }
    protected override void Dispose(bool disposing){if(disposing){Application.RemoveMessageFilter(resizeInput);appLogo.Image?.Dispose();}base.Dispose(disposing);}
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);if(m.Msg!=0x84||!CanResizeWindow)return;
        long point=m.LParam.ToInt64();var p=PointToClient(new Point((short)(point&65535),(short)((point>>16)&65535)));
        int hit=WindowResizeInput.HitTest(ClientSize,p,DeviceDpi);if(hit!=0)m.Result=(IntPtr)hit;
    }
    internal bool CanResizeWindow=>TopLevel&&!embeddedChrome&&WindowState==FormWindowState.Normal&&MaximizeBox;

    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
}
public class SurfaceCard : Panel
{
    public SurfaceCard() { DoubleBuffered = true; BackColor = WarmTheme.Surface; Padding = new Padding(18); Margin = new Padding(8); }
    protected override void OnPaintBackground(PaintEventArgs e) { Scenery.CardBase(e.Graphics,this); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using var path = WarmTheme.Round(new Rectangle(1, 1, Width - 3, Height - 3), 18); using var fill = new SolidBrush(WarmTheme.Surface); using var pen = new Pen(WarmTheme.Line); e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(pen, path); }
}
// 경고와 확인도 같은 디자인을 사용합니다. 닫기와 Esc는 동의로 처리하지 않습니다.
public sealed class DialogWindow : WarmWindow
{
    public WarmTextBox? Input {get;private set;}
    public DialogWindow(string text,string title,MessageBoxButtons buttons=MessageBoxButtons.OK,bool input=false,string initial="",Profile? profile=null,int players=0)
    {
        Text=title;Size=new(684,profile!=null?556:input?550:510);MinimumSize=Size;MaximizeBox=MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;
        EmbedInShell();Padding=Padding.Empty;
        if(profile!=null){BuildStop(profile,players);return;}
        if(input){BuildInput(text,title,initial);return;}
        var layout=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=Size};Controls.Add(layout);
        var close=new WarmButton{Text="×",Ghost=true,Font=new Font("Noto Sans KR",22)};close.Click+=(_,_)=>{DialogResult=buttons==MessageBoxButtons.YesNo?DialogResult.No:DialogResult.Cancel;Close();};layout.Place(close,616,16,48,44);
        var badge=new Label{Text=input?"i":"!",BackColor=WarmTheme.Accent,ForeColor=Color.White,Font=new Font("Noto Sans KR",26,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter};badge.Resize+=(_,_)=>{using var path=WarmTheme.Round(badge.ClientRectangle,36);var old=badge.Region;badge.Region=new Region(path);old?.Dispose();};layout.Place(badge,40,34,72,72);
        var heading=Ui.Heading(title,25);heading.TextAlign=ContentAlignment.MiddleCenter;layout.Place(heading,40,112,604,76);
        var description=new Label{Text=text,Font=new Font("Noto Sans KR",14),ForeColor=WarmTheme.Muted,TextAlign=ContentAlignment.TopLeft};layout.Place(description,42,204,600,input?100:142);
        if(input){Input=new WarmTextBox{Text=initial,Font=new Font("Noto Sans KR",15),PlaceholderText="필수 정보를 입력하세요"};layout.Place(Input,42,312,600,56);}
        int y=input?408:368;
        var ok=new WarmButton{Text=buttons==MessageBoxButtons.YesNo?"계속":"확인",Primary=true,Font=new Font("Noto Sans KR",18,FontStyle.Bold),DialogResult=buttons==MessageBoxButtons.YesNo?DialogResult.Yes:DialogResult.OK};layout.Place(ok,350,y,292,70);AcceptButton=ok;
        if(buttons is MessageBoxButtons.YesNo or MessageBoxButtons.OKCancel){var no=new WarmButton{Text="취소",Font=ok.Font,DialogResult=buttons==MessageBoxButtons.YesNo?DialogResult.No:DialogResult.Cancel};layout.Place(no,42,y,292,70);CancelButton=no;}else CancelButton=ok;
        var creator=new Label{Text="제작자 Migi0112",Font=new Font("Noto Sans KR",11),ForeColor=WarmTheme.Muted,TextAlign=ContentAlignment.MiddleRight};layout.Place(creator,350,Height-48,292,32);
        WarmTheme.Apply(this);if(input)Shown+=(_,_)=>Input!.Focus();
    }
    void BuildInput(string text,string title,string initial)
    {
        Size=new Size(690,450);MinimumSize=Size;
        var layout=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=Size};Controls.Add(layout);
        var close=Ui.Action("×",()=>{DialogResult=DialogResult.Cancel;Close();});close.Ghost=true;layout.Place(close,622,16,48,44);
        var badge=Ui.Heading("✎",30);badge.ForeColor=WarmTheme.Accent;layout.Place(badge,36,32,74,66);
        layout.Place(Ui.Heading(title=="서버 이름"?"서버 이름을 입력해 주세요.":title,23),126,32,496,54);
        var description=Ui.Note(text);description.Font=new Font("Noto Sans KR",13);layout.Place(description,126,90,496,68);
        layout.Place(Ui.Heading(title,16),36,148,500,38);
        Input=new WarmTextBox{Text=initial,Font=new Font("Noto Sans KR",16),PlaceholderText=title=="서버 이름"?"예: 친구들과 야생":"필수 정보를 입력하세요"};layout.Place(Input,36,190,618,64);
        layout.Place(Ui.Note("입력한 내용을 확인한 뒤 계속 진행하세요."),36,264,618,42);
        var no=new WarmButton{Text="돌아가기",DialogResult=DialogResult.Cancel,Font=new Font("Noto Sans KR",18,FontStyle.Bold)};
        var yes=new WarmButton{Text="입력 후 계속",DialogResult=DialogResult.OK,Primary=true,Font=no.Font};
        layout.Place(no,36,338,300,64);layout.Place(yes,354,338,300,64);AcceptButton=yes;CancelButton=no;
        yes.Enabled=!string.IsNullOrWhiteSpace(Input.Text);Input.TextChanged+=(_,_)=>yes.Enabled=!string.IsNullOrWhiteSpace(Input.Text);
        var credit=Ui.Note("제작자 Migi0112");credit.TextAlign=ContentAlignment.MiddleRight;layout.Place(credit,354,408,300,30);WarmTheme.Apply(this);
    }
    void BuildStop(Profile profile,int players)
    {
        var layout=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=Size};Controls.Add(layout);
        var close=new WarmButton{Text="×",Ghost=true};close.Click+=(_,_)=>{DialogResult=DialogResult.No;Close();};layout.Place(close,620,18,44,44);
        var heading=Ui.Heading("서버를 정상 종료할까요?",26);heading.TextAlign=ContentAlignment.MiddleCenter;layout.Place(heading,40,76,604,70);
        var description=Ui.Note($"현재 접속 중인 플레이어 {players}명의 연결이 종료됩니다.");description.Font=new Font("Noto Sans KR",15);description.TextAlign=ContentAlignment.MiddleCenter;layout.Place(description,42,146,600,46);
        var card=new SurfaceCard{Padding=Padding.Empty};layout.Place(card,42,198,600,144);
        var picture=new PictureBox{Image=Artwork.Read(profile.ImagePath),SizeMode = PictureBoxSizeMode.Zoom};picture.SetBounds(22,20,130,104);picture.Disposed+=(_,_)=>picture.Image?.Dispose();card.Controls.Add(picture);
        var name=Ui.Heading(profile.Name,23);name.AutoSize=false;name.SetBounds(180,24,390,44);card.Controls.Add(name);
        var version=Ui.Note(profile.Kind+" · Minecraft "+profile.MinecraftVersion);version.AutoSize=false;version.Font=new Font("Noto Sans KR",14);version.SetBounds(180,82,390,40);card.Controls.Add(version);
        var explanation=Ui.Note("월드와 플레이어 데이터를 저장한 뒤 서버를 종료합니다.");explanation.Font=new Font("Noto Sans KR",14);layout.Place(explanation,70,364,570,44);
        var no=new WarmButton{Text="취소",Font=new Font("Noto Sans KR",18,FontStyle.Bold),DialogResult=DialogResult.No};var yes=new WarmButton{Text="정상 종료",Primary=true,Font=no.Font,DialogResult=DialogResult.Yes};layout.Place(no,42,426,290,70);layout.Place(yes,348,426,294,70);AcceptButton=yes;CancelButton=no;
        var creator=new Label{Text="제작자 Migi0112",ForeColor=WarmTheme.Muted,TextAlign=ContentAlignment.MiddleRight};layout.Place(creator,350,506,292,32);WarmTheme.Apply(this);
    }
}
public static class WarmDialog
{
    // 별도의 배경 덮개 없이 실제 최상위 관리 창을 소유자로 지정합니다.
    public static DialogResult Present(IWin32Window owner,WarmWindow dialog)
    {
        var parent=Control.FromHandle(owner.Handle)?.TopLevelControl as Form;
        if(parent==null)return dialog.ShowDialog(owner);
        if(dialog is ServerDeleteWindow)
        {
            using var shade=new Form{FormBorderStyle=FormBorderStyle.None,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Bounds=parent.RectangleToScreen(parent.ClientRectangle),BackColor=Color.Black,Opacity=.22};
            shade.Show(parent);dialog.StartPosition=FormStartPosition.CenterParent;
            try{return dialog.ShowDialog(parent);}finally{shade.Close();}
        }
        // 내부 페이지의 상대 좌표가 아닌 실제 관리 창의 화면 좌표를 기준으로 가운데 배치합니다.
        dialog.StartPosition=FormStartPosition.Manual;
        dialog.Location=new Point(parent.Left+(parent.Width-dialog.Width)/2,parent.Top+(parent.Height-dialog.Height)/2);
        return dialog.ShowDialog(parent);
    }
    public static DialogResult Show(IWin32Window owner,string text,string title="안내",MessageBoxButtons buttons=MessageBoxButtons.OK,MessageBoxIcon icon=MessageBoxIcon.None)
    {using var dialog=new DialogWindow(text,title,buttons);return Present(owner,dialog);}
    public static DialogResult ConfirmProfileDelete(IWin32Window owner,string name)
    {
        using var dialog=new DialogWindow($"‘{name}’ 서버를 목록에서 삭제할까요?\n서버 폴더와 백업 파일은 그대로 유지됩니다.","서버 삭제",MessageBoxButtons.YesNo);
        if(dialog.AcceptButton is Button button)button.Text="삭제";
        return Present(owner,dialog);
    }
    public static DialogResult ConfirmStop(IWin32Window owner,Profile profile,int players)
    {using var dialog=new DialogWindow("","서버를 정상 종료할까요?",MessageBoxButtons.YesNo,profile:profile,players:players);return Present(owner,dialog);}
    public static string? Input(IWin32Window owner,string title,string description,string initial="")
    {using var dialog=new DialogWindow(description,title,MessageBoxButtons.OKCancel,true,initial);return Present(owner,dialog)==DialogResult.OK?dialog.Input!.Text.Trim():null;}
}
public static class ApiIdentity
{
    public static bool Valid(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Any(c => c > 127 || char.IsControl(c) || c is '(' or ')')) return false;
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" && !string.IsNullOrEmpty(uri.Host)) return true;
        try { return new System.Net.Mail.MailAddress(text).Address == text && text.Contains('@'); } catch { return false; }
    }
    public static string? RequirePaperContact(IWin32Window owner)
    {
        string path = Path.Combine(Storage.Root, "api-contact.txt"); string value = File.Exists(path) ? File.ReadAllText(path).Trim() : "";
        while (!Valid(value))
        {
            var input = WarmDialog.Input(owner, "Paper 다운로드 안내", "Paper 공식 다운로드는 요청 식별용 연락처가 필요해요.\n이메일 또는 프로젝트 URL을 한 번 입력하면 이 PC에 기억합니다.\n로그인 정보는 아니며 다운로드 요청에 포함됩니다.", value);
            if (input == null) return null; value = input; if (!Valid(value)) WarmDialog.Show(owner, "올바른 이메일 또는 http(s) 프로젝트 주소를 입력하세요.", "입력 확인");
        }
        Storage.AtomicWrite(path, value); return value;
    }
}


// Window symbols are drawn as geometry so font ascenders and fallback fonts cannot clip them.
public sealed class WindowCaptionButton : Button
{
    readonly int symbol;
    public WindowCaptionButton(int kind){symbol=kind;AccessibleName=kind==0?"최소화":kind==1?"최대화 / 복원":"닫기";Cursor=Cursors.Hand; FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);MouseEnter+=(_,_)=>Invalidate();MouseLeave+=(_,_)=>Invalidate();}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(ClientRectangle.Contains(PointToClient(Cursor.Position))?WarmTheme.Peach:Parent?.BackColor??WarmTheme.Surface);using var p=new Pen(WarmTheme.Ink,1.4f);int x=Width/2,y=Height/2;if(symbol==0)e.Graphics.DrawLine(p,x-7,y,x+7,y);else if(symbol==1)e.Graphics.DrawRectangle(p,x-6,y-6,12,12);else{e.Graphics.DrawLine(p,x-5,y-5,x+5,y+5);e.Graphics.DrawLine(p,x+5,y-5,x-5,y+5);}if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-4,-4));}
}
