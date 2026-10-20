// [소스 안내] 크림·코랄 색상과 공통 입력/버튼 디자인입니다. 여러 화면이 같은 스타일을 사용하도록 합니다.
using System.Drawing.Drawing2D;
namespace NeoForgeServerManager;

// 모든 화면이 공유하는 색상표입니다. 이곳만 바꾸면 목록·마법사·관리 탭이 함께 바뀝니다.
public static class WarmTheme
{
    public static readonly Font BodyFont=new("Noto Sans KR",14);
    public static readonly Color Background = Color.FromArgb(250, 248, 244), Surface = Color.FromArgb(255, 254, 252), Ink = Color.FromArgb(65, 43, 33), Muted = Color.FromArgb(108, 110, 113), Line = Color.FromArgb(233, 227, 220), Accent = Color.FromArgb(242, 116, 83), Peach = Color.FromArgb(255, 237, 226);
    public static GraphicsPath Round(Rectangle r, int radius = 14)
    {
        var p = new GraphicsPath(); int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height)); if (d < 2) { p.AddRectangle(r); return p; }
        p.AddArc(r.Left, r.Top, d, d, 180, 90); p.AddArc(r.Right - d, r.Top, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
    public static void Apply(Control root)
    {
        if (root.ForeColor == SystemColors.ControlText) root.ForeColor = Ink;
        if (root is Form f) { f.BackColor = Background; f.Font = BodyFont; f.AutoScaleMode = AutoScaleMode.Dpi; }
        else if (Equals(root.Tag,"landscape")){root.BackColor=Color.Transparent;}
        else if (root is TabPage) { root.BackColor = Surface; root.Padding = new Padding(14); }
        else if (root is TextBoxBase text) { text.BackColor = Surface; text.ForeColor = Ink; if (text is RichTextBox && text.Font.Name == "Consolas") { text.BackColor = Color.FromArgb(49, 43, 39); text.ForeColor = Color.FromArgb(247, 231, 207); } }
        else if (root is ListBox lb) { lb.BackColor = Surface; lb.BorderStyle = BorderStyle.None; if(lb.DrawMode==DrawMode.Normal)lb.ItemHeight = 30; }
        else if (root is ListView lv) { lv.BackColor = Surface; lv.BorderStyle = BorderStyle.None; lv.FullRowSelect = true; }
        else if (root is WarmCombo cb) { cb.BackColor = Surface; cb.FlatStyle = FlatStyle.Flat; }
        else if (root is NumericUpDown n) { n.BackColor = Surface; n.BorderStyle = n.Parent is WarmNumber || n.Parent?.Parent is WarmNumber ? BorderStyle.None : BorderStyle.FixedSingle; }
        else if (root is DataGridView g) { g.BackgroundColor = Surface; g.BorderStyle = BorderStyle.None; g.EnableHeadersVisualStyles = false; g.ColumnHeadersDefaultCellStyle.BackColor = Background; g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Background; g.ColumnHeadersDefaultCellStyle.SelectionForeColor = Ink; g.ColumnHeadersDefaultCellStyle.ForeColor = Ink; g.DefaultCellStyle.BackColor = Surface; g.DefaultCellStyle.SelectionBackColor = Peach; g.DefaultCellStyle.SelectionForeColor = Ink; g.GridColor = Line; g.RowTemplate.Height = Math.Max(34,g.RowTemplate.Height); }
        else if (root is PageDeck) { root.BackColor = Surface; }
        else if (root is TabControl tabs && !Equals(tabs.Tag, "warm")) { tabs.Tag = "warm"; tabs.DrawMode = TabDrawMode.OwnerDrawFixed; tabs.Padding = new Point(14, 10); tabs.DrawItem += (_, e) => { var r = e.Bounds; using var brush = new SolidBrush(e.Index == tabs.SelectedIndex ? Peach : Background); e.Graphics.FillRectangle(brush, r); TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, tabs.Font, r, e.Index == tabs.SelectedIndex ? Accent : Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter); }; }
        else if (root is ReferencePanel) { }
        else if (root is SurfaceCard || Equals(root.Tag, "surface")) { root.BackColor = Surface; }
        else if (root is Panel or UserControl) { root.BackColor = root.Parent?.BackColor ?? Background; }
        foreach (Control child in root.Controls) Apply(child);
    }
}
// 기본 Button의 클릭·키보드·접근성 동작은 유지하고 둥근 테두리만 직접 그립니다.
public class WarmButton : Button
{
    public string IconName { get; set; } = "";
    public Color? OutlineColor { get; set; }
    public Color? TextColor { get; set; }
    public bool Navigation { get; set; }
    public bool SoftSelected {get;set;}
    public bool Dark { get; set; }
    public bool Ghost { get; set; }
    bool hover, primaryValue; public bool Primary { get => primaryValue; set { if(primaryValue==value)return; primaryValue=value; Invalidate(); } }
    public WarmButton() { FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; BackColor = WarmTheme.Surface; Cursor = Cursors.Hand; Padding = new Padding(10, 4, 10, 4); MinimumSize = new Size(36, 34); SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); MouseEnter += (_, _) => { hover = true; Invalidate(); }; MouseLeave += (_, _) => { hover = false; Invalidate(); }; }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; Scenery.CardBase(e.Graphics,this);
        bool primary = Primary || Text is "다음" or "만들기" or "＋ 서버 추가" or "▶ 시작" or "선택 설치";
        using var path = WarmTheme.Round(new Rectangle(1, 1, Width - 3, Height - 3), 10);
        using var fill = new SolidBrush(!Enabled ? WarmTheme.Background : primary ? WarmTheme.Accent : Dark ? Color.FromArgb(47,46,44) : hover||SoftSelected ? WarmTheme.Peach : WarmTheme.Surface); using var pen = new Pen(primary && Enabled ? WarmTheme.Accent : Dark ? Color.FromArgb(88,86,82) : WarmTheme.Line);
        if(primary && Enabled) { using var gradient=new LinearGradientBrush(ClientRectangle,Color.FromArgb(255,133,107),Color.FromArgb(249,101,76),90f); e.Graphics.FillPath(gradient,path); }
        else if(!Ghost || hover) e.Graphics.FillPath(fill,path);
        if (OutlineColor is { } outline && !primary && Enabled) pen.Color = outline;
        if (!Ghost || primary || hover) e.Graphics.DrawPath(pen, path);
        var foreground=!Enabled ? WarmTheme.Muted : primary || Dark ? Color.White : TextColor ?? WarmTheme.Ink;
        var textBounds=ClientRectangle;Font? iconFont=null;
        if(IconName.Length>0)
        {
            // 일반 버튼은 아이콘과 문구를 한 묶음으로 가운데 정렬합니다.
            // 탐색 메뉴의 고정 들여쓰기를 작은 설정 버튼에도 적용하면 글자가 잘립니다.
            int iconSize=Navigation?26:24, gap=Navigation?20:10;
            iconFont=FitText.FontFor(Text,Font,new Size(Math.Max(1,Width-iconSize-gap-20),Height));
            int textWidth=TextRenderer.MeasureText(Text,iconFont,Size.Empty,TextFormatFlags.NoPadding).Width;
            int left=Navigation?16:Math.Max(10,(Width-iconSize-gap-textWidth)/2);
            ReferenceIcons.Draw(e.Graphics,IconName,new Rectangle(left,(Height-iconSize)/2,iconSize,iconSize),foreground);
            int textLeft=left+iconSize+gap;
            textBounds=new Rectangle(textLeft,0,Math.Max(0,Width-textLeft-8),Height);
        }
        using var fitted=iconFont??FitText.FontFor(Text,Font,textBounds.Size);
        TextRenderer.DrawText(e.Graphics, Text, fitted, textBounds, foreground, (Navigation||IconName.Length>0?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter) | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -6, -6));
    }
}
// 모달 대화상자가 아닌 포커스를 빼앗지 않는 도움말입니다. 포인터가 ?를 떠나면 즉시 닫습니다.
public sealed class HoverHelp : Form
{
    readonly Label content = new() { Dock = DockStyle.Fill, Padding = new Padding(20,8,20,8), AutoSize = false };
    readonly Label heading=new(){Dock=DockStyle.Top,Height=54,Padding=new Padding(20,12,20,0),Font=new Font("Noto Sans KR",16,FontStyle.Bold)};
    public HoverHelp() { FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; BackColor = WarmTheme.Surface; Font = new Font("Noto Sans KR",12); content.ForeColor = WarmTheme.Muted; Controls.Add(content); Controls.Add(heading); var credit = new Label {Text="제작자 Migi0112",Dock=DockStyle.Bottom,Height=24,TextAlign=ContentAlignment.MiddleRight,Padding=new Padding(0,0,12,0),ForeColor=WarmTheme.Muted};Controls.Add(credit); }
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x08000080; return p; } }
    public void Present(Control anchor, string text, Point screenPoint)
    {
        int split=text.IndexOf('\n');heading.Text=split<0?text:text[..split];content.Text=split<0?"":text[(split+1)..].Trim(); int width = (int)(500 * anchor.DeviceDpi / 96f); var measured = TextRenderer.MeasureText(content.Text, Font, new Size(width - 36, 1000), TextFormatFlags.WordBreak);
        Size = new Size(width, Math.Min(measured.Height + 114, Screen.FromPoint(screenPoint).WorkingArea.Height - 24));
        var area = Screen.FromPoint(screenPoint).WorkingArea;
        if(anchor.FindForm() is { } owner) area=Rectangle.Intersect(area,owner.RectangleToScreen(owner.ClientRectangle));
        int x = screenPoint.X + 20; if (x + Width > area.Right) x = screenPoint.X - Width - 20;
        Location = new Point(Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - Width)), Math.Clamp(screenPoint.Y + 8, area.Top, Math.Max(area.Top, area.Bottom - Height)));
        using var path = WarmTheme.Round(ClientRectangle, 14); Region?.Dispose(); Region = new Region(path);
        if (!Visible) Show(anchor.FindForm());
    }
    public static void Attach(Control anchor, string text)
    {
        var tip = new HoverHelp(); Form? owner = null;
        anchor.AccessibleDescription = text;
        void ShowTip() { if (anchor.FindForm() is not { } form) return; if (owner == null) { owner = form; owner.Deactivate += Deactivate; } tip.Present(anchor, text, Cursor.Position); }
        void Deactivate(object? s, EventArgs e) => tip.Hide();
        anchor.MouseEnter += (_, _) => ShowTip(); anchor.MouseMove += (_, _) => ShowTip(); anchor.MouseLeave += (_, _) => tip.Hide();
        anchor.Enter += (_, _) => tip.Present(anchor, text, anchor.PointToScreen(new Point(anchor.Width, 0))); anchor.Leave += (_, _) => tip.Hide();
        anchor.Disposed += (_, _) => { if (owner != null) owner.Deactivate -= Deactivate; tip.Dispose(); };
        anchor.VisibleChanged += (_, _) => { if (!anchor.Visible) tip.Hide(); };
    }
}
// 카드 내부의 문구와 선택 표시를 그립니다. 실제 선택 동작은 Button이라 키보드로도 사용할 수 있습니다.
public sealed class ChoiceCard : Button
{
    public bool ShowSoftwareIcon { get; set; }
    public string Caption { get; set; } = ""; public string Description { get; set; } = ""; public bool Selected { get; set; }
    public ChoiceCard() { FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Hand; SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); Margin = new Padding(7); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; e.Graphics.Clear(Parent?.BackColor ?? WarmTheme.Background);
        using var path = WarmTheme.Round(new Rectangle(1, 1, Width - 3, Height - 3), 16); using var fill = new SolidBrush(Selected ? WarmTheme.Peach : WarmTheme.Surface); using var pen = new Pen(Selected ? WarmTheme.Accent : WarmTheme.Line, Selected ? 2 : 1); e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(pen, path);
        int left = ShowSoftwareIcon ? Math.Min(192, Width / 3+14) : 22;
        if (ShowSoftwareIcon) SoftwareArtwork.Draw(e.Graphics, Text, new Rectangle(22, (Height - 132) / 2, left - 40, 132));
        bool compact=ShowSoftwareIcon && Height<165;
        int top = compact ? 12 : ShowSoftwareIcon ? Math.Max(18, (Height - 106) / 2) : 21;
        using var heading = new Font(Font.FontFamily, compact ? 17 : ShowSoftwareIcon ? 22 : 17, FontStyle.Bold); using var small = new Font(Font.FontFamily, compact ? 10 : 13);
        int headingHeight=Math.Max(36,TextRenderer.MeasureText(e.Graphics,Text,heading,Size.Empty,TextFormatFlags.SingleLine).Height);
        TextRenderer.DrawText(e.Graphics, Text, heading, new Rectangle(left, top-(headingHeight-36), Width - left - 34, headingHeight), WarmTheme.Ink, TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(e.Graphics, Caption, Font, new Rectangle(left, top + (compact?34:42), Width - left - 28, Math.Max(26,TextRenderer.MeasureText(e.Graphics,Caption,Font,Size.Empty,TextFormatFlags.SingleLine).Height)), WarmTheme.Accent, TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(e.Graphics, Description, small, new Rectangle(left, top + (compact?63:77), Width - left - 24, Math.Max(32, Height - top - (compact?67:81))), WarmTheme.Muted, TextFormatFlags.WordBreak);
        e.Graphics.DrawEllipse(pen, Width - 36, 23, 17, 17); if (Selected) { using var dot = new SolidBrush(WarmTheme.Accent); e.Graphics.FillEllipse(dot, Width - 32, 27, 9, 9); }
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -8, -8));
    }
}
public sealed class WizardHeader : Control
{
    public int Step {get;set;}
    public event EventHandler? HomeRequested;
    public WizardHeader(){Height=248;Dock=DockStyle.Top;DoubleBuffered=true;Cursor=Cursors.Hand;}
    protected override void OnClick(EventArgs e){base.OnClick(e);if(new Rectangle(144*Width/1532,34,170*Width/1532,46).Contains(PointToClient(Cursor.Position)))HomeRequested?.Invoke(this,EventArgs.Empty);}
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;Scenery.Paint(g,this);g.SmoothingMode=SmoothingMode.AntiAlias;
        float sx=Width/1532f;g.ScaleTransform(sx,1);
        using(var panel=WarmTheme.Round(new Rectangle(126,104,1390,136),20))using(var surface=new SolidBrush(WarmTheme.Surface))g.FillPath(surface,panel);
        using var pen=new Pen(WarmTheme.Line,2);using var home=WarmTheme.Round(new Rectangle(144,34,170,46),14);using(var homeFill=new SolidBrush(WarmTheme.Surface))g.FillPath(homeFill,home);g.DrawPath(pen,home);
        using var heading=new Font("Noto Sans KR",30,FontStyle.Bold);using var normal=new Font("Noto Sans KR",13);using var bold=new Font("Noto Sans KR",15,FontStyle.Bold);
        // GDI+ 문자열은 그래픽 좌표 변환을 따르므로 작은 창에서도 단계 표시가 겹치지 않습니다.
        using var ink=new SolidBrush(WarmTheme.Ink);using var muted=new SolidBrush(WarmTheme.Muted);
        g.DrawString("← 서버목록",bold,ink,new PointF(164,42));
        string[] headings=["새 서버 만들기","버전 선택","메모리 및 Java 설정","서버 기본 설정","서버 이미지 선택","확인 및 만들기"];
        g.DrawString(headings[Math.Clamp(Step,0,5)],heading,ink,new PointF(144,120));
        string[] subtitles=["어떤 서버 종류로 시작할지 선택하세요.","설치할 버전과 로더 버전을 선택하세요.","서버에 할당할 메모리와 Java 실행 환경을 설정하세요.","서버 환경을 설정하세요.","서버를 나타낼 이미지를 선택하세요.","설정을 확인하고 서버를 만들어 보세요."];
        g.DrawString(subtitles[Math.Clamp(Step,0,5)],normal,muted,new PointF(146,185));
        string[] labels=["서버 종류","버전 선택","메모리","서버 설정","서버 이미지","확인 및 만들기"];
        using var centered=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center};
        for(int i=0;i<6;i++)
        {
            int x=622+i*150;if(i<5)g.DrawLine(pen,x+62,170,x+136,170);
            using var fill=new SolidBrush(i<=Step?WarmTheme.Accent:Color.FromArgb(239,234,226));using var fg=new SolidBrush(i<=Step?Color.White:WarmTheme.Muted);using var label=new SolidBrush(i<=Step?WarmTheme.Accent:WarmTheme.Ink);
            g.FillEllipse(fill,x,145,50,50);g.DrawString(i<Step?"✓":(i+1).ToString(),bold,fg,new RectangleF(x,145,50,50),centered);
            g.DrawString(labels[i],i<=Step?bold:normal,label,new RectangleF(x-48,200,146,34),centered);
        }
    }
}
// 체크박스도 코랄색 토글로 통일합니다. CheckBox를 상속하므로 Checked와 키보드 스페이스 동작은 그대로입니다.
public sealed class WarmToggle : CheckBox
{
    // 설정 행에서는 승인 시안의 60×32 스위치를 사용하고 좁은 도구 모음은 기본 크기를 유지합니다.
    public bool Large {get;set;}
    public WarmToggle() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); MinimumSize = new Size(48, 30); Cursor = Cursors.Hand; }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; e.Graphics.Clear(Parent?.BackColor ?? WarmTheme.Background);
        int w=Large?60:38,h=Large?32:22,y=Math.Max(2,(Height-h)/2);
        using var path=WarmTheme.Round(new Rectangle(2,y,w,h),h/2);
        using var fill=new SolidBrush(Checked&&Enabled?WarmTheme.Accent:Color.FromArgb(177,171,163));e.Graphics.FillPath(fill,path);
        using var knob=new SolidBrush(WarmTheme.Surface);e.Graphics.FillEllipse(knob,Checked?w-h+5:5,y+3,h-6,h-6);
        if(Text.Length>0)TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(w+14,0,Math.Max(0,Width-w-14),Height),Enabled?ForeColor:WarmTheme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -2, -2));
    }
    public override Size GetPreferredSize(Size proposedSize) { var size = TextRenderer.MeasureText(Text, Font); return new Size(size.Width + 55, Math.Max(34, size.Height + 8)); }
}






public static class FitText
{
    public static Font FontFor(string text,Font original,Size bounds,bool wrap=false)
    {
        float size=original.Size;var flags=TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix|(wrap?TextFormatFlags.WordBreak:TextFormatFlags.SingleLine);
        while(size>Math.Min(10,original.Size)){
            using var probe=new Font(original.FontFamily,size,original.Style);
            var needed=TextRenderer.MeasureText(text,probe,wrap?new Size(Math.Max(1,bounds.Width),int.MaxValue):Size.Empty,flags);
            if(needed.Width<=bounds.Width-4&&needed.Height<=bounds.Height-4)break;size-=.5f;
        }
        return new Font(original.FontFamily,size,original.Style);
    }
}
public sealed class ReadableLabel : Label
{
    protected override void OnPaint(PaintEventArgs e)
    {
        if(AutoSize){base.OnPaint(e);return;}
        var r=Rectangle.FromLTRB(Padding.Left,Padding.Top,Width-Padding.Right,Height-Padding.Bottom);
        if(r.Width<=0||r.Height<=0)return;
        using var font=FitText.FontFor(Text,Font,r.Size,true);
        var flags=TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding;
        if(TextAlign is ContentAlignment.MiddleLeft or ContentAlignment.MiddleCenter or ContentAlignment.MiddleRight)flags|=TextFormatFlags.VerticalCenter;
        if(TextAlign is ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight)flags|=TextFormatFlags.Right;
        if(TextAlign is ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter)flags|=TextFormatFlags.HorizontalCenter;
        TextRenderer.DrawText(e.Graphics,Text,font,r,Enabled?ForeColor:WarmTheme.Muted,flags);
    }
}
