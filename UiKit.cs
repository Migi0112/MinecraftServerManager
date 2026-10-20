// [소스 안내] 레이블·버튼·입력기 등 화면을 만드는 공통 도구와 컨트롤입니다.
using System.Collections;
using System.Drawing.Drawing2D;
namespace NeoForgeServerManager;

// 입력값을 감추는 기본 콤보 페인팅 대신 닫힌 상태와 팝업을 같은 색상으로 직접 그립니다.
public class WarmCombo : Control
{
    public sealed class ItemList(WarmCombo owner) : System.Collections.ObjectModel.Collection<object> { public void AddRange(object[] values) { foreach (var value in values) Add(value); } public new void Clear() { base.Clear(); owner.SelectedIndex = -1; } }
    public ItemList Items { get; }
    int selected = -1;
    ToolStripDropDown? activePopup;
    public ComboBoxStyle DropDownStyle { get; set; }
    public FlatStyle FlatStyle { get; set; }
    public event EventHandler? SelectedIndexChanged;
    public int SelectedIndex { get => selected; set { int next = value >= 0 && value < Items.Count ? value : -1; if (selected == next) return; selected = next; base.Text = SelectedItem?.ToString() ?? ""; Invalidate(); SelectedIndexChanged?.Invoke(this, EventArgs.Empty); } }
    public object? SelectedItem { get => selected >= 0 && selected < Items.Count ? Items[selected] : null; set => SelectedIndex = value == null ? -1 : Items.IndexOf(value); }
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string Text { get => SelectedItem?.ToString() ?? ""; set => SelectedItem = value; }
    public WarmCombo() { Items = new(this); SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); Height = 42; Width = 180; TabStop = true; Cursor = Cursors.Hand; }
    protected override void OnPaint(PaintEventArgs e) { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.Clear(Parent?.BackColor ?? WarmTheme.Background); using var p = WarmTheme.Round(new Rectangle(1, 1, Width - 3, Height - 3), 10); using var b = new SolidBrush(WarmTheme.Surface); using var pen = new Pen(Focused ? WarmTheme.Accent : WarmTheme.Line); e.Graphics.FillPath(b, p); e.Graphics.DrawPath(pen, p); TextRenderer.DrawText(e.Graphics, Text.Length == 0 ? "선택하세요" : Text, Font, new Rectangle(13, 0, Width - 45, Height), Enabled ? WarmTheme.Ink : WarmTheme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis); using var arrow=new Pen(Enabled?WarmTheme.Accent:WarmTheme.Muted,1.8f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round};e.Graphics.DrawLines(arrow,new Point[]{new Point(Width-25,Height/2-2),new Point(Width-20,Height/2+3),new Point(Width-15,Height/2-2)}); }
    protected override void OnClick(EventArgs e) { base.OnClick(e); Focus(); Open(); }
    protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.KeyCode is Keys.Space or Keys.Enter or Keys.F4) { Open(); e.Handled = true; } else if (e.KeyCode == Keys.Down) SelectedIndex = Math.Min(Items.Count - 1, selected + 1); else if (e.KeyCode == Keys.Up) SelectedIndex = Math.Max(0, selected - 1); }
    void Open()
    {
        if (Items.Count == 0 || IsDisposed || !Enabled || activePopup != null) return;
        var list = new ListBox { BorderStyle = BorderStyle.None, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 38, Font = Font, BackColor = WarmTheme.Surface, Size = new Size(Width - 12, Math.Min(8, Items.Count) * 38) };
        list.Items.AddRange(Items.ToArray()); list.SelectedIndex = SelectedIndex;
        list.DrawItem += (_, e) => { if (e.Index < 0) return; bool chosen = (e.State & DrawItemState.Selected) != 0; using var fill = new SolidBrush(chosen ? WarmTheme.Peach : WarmTheme.Surface); e.Graphics.FillRectangle(fill, e.Bounds); TextRenderer.DrawText(e.Graphics, list.Items[e.Index].ToString(), Font, Rectangle.Inflate(e.Bounds, -12, 0), chosen ? WarmTheme.Accent : WarmTheme.Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis); };
        var popup = new ToolStripDropDown { Padding = new Padding(6), BackColor = WarmTheme.Surface, AutoClose = true }; popup.Items.Add(new ToolStripControlHost(list) { Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false, Size = list.Size });
        activePopup = popup;
        bool choosing = false;
        // 선택 이벤트에서 화면이 바뀔 수 있으므로 먼저 팝업을 닫습니다.
        // Closed 안에서 Dispose하면 WinForms의 남은 마우스 처리가 폐기된 팝업을 참조합니다.
        void Choose()
        {
            if (choosing || list.IsDisposed) return;
            choosing = true;
            int index = list.SelectedIndex;
            popup.Close();
            if (!IsDisposed) SelectedIndex = index;
        }
        list.MouseClick += (_, _) => Choose();
        list.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Choose(); } else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; popup.Close(); } };
        popup.Closed += (_, _) =>
        {
            activePopup = null;
            if (!IsDisposed) Invalidate();
            // 현재 메시지(선택/닫기)가 완전히 끝난 다음 한 번만 해제합니다.
            EventHandler? idle = null;
            idle = (_, _) => { Application.Idle -= idle; popup.Dispose(); };
            Application.Idle += idle;
        };
        popup.Show(this, new Point(0, Height + 4));
        if (!popup.IsDisposed && popup.Visible) { using var path = WarmTheme.Round(popup.ClientRectangle, 12); popup.Region = new Region(path); list.Focus(); }
    }
    protected override void Dispose(bool disposing) { if (disposing && activePopup is { } popup) { activePopup = null; popup.Close(); } base.Dispose(disposing); }
}
// 둥근 외곽선과 여백은 컨테이너에서 그리며 실제 편집/IME/복사는 Windows 텍스트 컨트롤이 담당합니다.
public class WarmTextBox : UserControl
{
    readonly TextBox input = new() { BorderStyle = BorderStyle.None, Dock = DockStyle.None };
    string leadingIcon = "";
    // 아이콘 영역은 입력 영역과 분리해 검색어·한글 조합 문자가 아이콘에 겹치지 않게 합니다.
    public string LeadingIcon { get => leadingIcon; set { leadingIcon = value; PerformLayout(); Invalidate(); } }
    public WarmTextBox() { SetStyle(ControlStyles.ResizeRedraw|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);Height = 42; Width = 200; BackColor = WarmTheme.Surface; Controls.Add(input);input.TextChanged += (_, _) => OnTextChanged(EventArgs.Empty);input.KeyDown += (_, e) => OnKeyDown(e);input.Enter += (_, _) => Invalidate();input.Leave += (_, _) => {OnLeave(EventArgs.Empty);Invalidate();}; }
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string Text { get => input?.Text ?? ""; set { if (input != null) input.Text = value; } }
    public string PlaceholderText { get => input.PlaceholderText; set => input.PlaceholderText=value; }
    public bool UseSystemPasswordChar { get => input.UseSystemPasswordChar; set => input.UseSystemPasswordChar = value; }
    public bool ReadOnly { get => input.ReadOnly; set => input.ReadOnly = value; }
    public bool Multiline { get => input.Multiline; set {input.Multiline=value;PerformLayout();} }
    public ScrollBars ScrollBars { get => input.ScrollBars; set => input.ScrollBars = value; }
    public new BorderStyle BorderStyle { get => BorderStyle.None; set { } }
    public void Clear() => input.Clear();
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        // 子 컨트롤의 Dock과 외부 레이아웃이 서로 크기를 바꾸지 않도록 편집 영역을 직접 계산합니다.
        if(input!=null){int left=leadingIcon.Length==0?13:60;int height=input.Multiline?Math.Max(1,Height-16):input.PreferredHeight;input.SetBounds(left,Math.Max(4,(Height-height)/2),Math.Max(1,Width-left-13),height);}
    }
    protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);PerformLayout();Invalidate();}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (leadingIcon.Length > 0) ReferenceIcons.Draw(e.Graphics, leadingIcon, new Rectangle(21, (Height - 26) / 2, 26, 26), WarmTheme.Muted);
    }
    protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Parent?.BackColor ?? WarmTheme.Background); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using var path = WarmTheme.Round(new Rectangle(1, 1, Width - 3, Height - 3), 10); using var brush = new SolidBrush(WarmTheme.Surface); using var pen = new Pen(ContainsFocus ? WarmTheme.Accent : WarmTheme.Line); e.Graphics.FillPath(brush, path); e.Graphics.DrawPath(pen, path); }
}
// 숫자 편집/키보드 동작은 표준 컨트롤에 맡기고 바깥 테두리는 다른 입력창과 통일합니다.
public sealed class WarmNumber : UserControl
{
    readonly NumericUpDown input=new(){BorderStyle=BorderStyle.None};
    readonly Panel viewport=new(){BackColor=WarmTheme.Surface};
    readonly NumberStep up=new(true),down=new(false);
    public decimal Minimum {get=>input.Minimum;set=>input.Minimum=value;}
    public decimal Maximum {get=>input.Maximum;set=>input.Maximum=value;}
    public decimal Value {get=>input.Value;set=>input.Value=value;}
    public event EventHandler? ValueChanged;
    public WarmNumber()
    {
        SetStyle(ControlStyles.ResizeRedraw|ControlStyles.OptimizedDoubleBuffer,true);
        Height=42; viewport.Controls.Add(input);Controls.AddRange([viewport,up,down]);
        up.Click+=(_,_)=>{input.UpButton();input.Focus();};down.Click+=(_,_)=>{input.DownButton();input.Focus();};
        input.ValueChanged+=(_,_)=>ValueChanged?.Invoke(this,EventArgs.Empty);
        input.Enter+=(_,_)=>Invalidate();input.Leave+=(_,_)=>Invalidate();
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);if(viewport==null)return;
        int h=input.PreferredHeight,w=32,top=Math.Max(3,(Height-h)/2);
        viewport.SetBounds(12,top,Math.Max(12,Width-w-21),h);
        // Clip only the native spinner; preserve Windows numeric parsing and keyboard editing.
        int spinner=input.Controls.Cast<Control>().Where(c=>c is not TextBox).Select(c=>c.Width).DefaultIfEmpty(20).Max();
        input.SetBounds(0,0,viewport.Width+spinner,h);
        int half=Math.Max(12,(Height-8)/2);
        up.SetBounds(Width-w-6,4,w,half);down.SetBounds(Width-w-6,4+half,w,half);
    }
    protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);PerformLayout();}
    protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent?.BackColor??WarmTheme.Background);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using var path=WarmTheme.Round(new Rectangle(1,1,Width-3,Height-3),10);using var fill=new SolidBrush(WarmTheme.Surface);using var pen=new Pen(ContainsFocus?WarmTheme.Accent:WarmTheme.Line);e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(pen,path);}
}
public sealed class NumberStep : Control
{
    readonly bool up;bool hover;
    public NumberStep(bool increase){up=increase;Cursor=Cursors.Hand;TabStop=false;AccessibleName=increase?"값 증가":"값 감소";AccessibleRole=AccessibleRole.PushButton;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
    protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);hover=true;Invalidate();}
    protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hover=false;Invalidate();}
    protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(WarmTheme.Surface);g.SmoothingMode=SmoothingMode.AntiAlias;using var path=WarmTheme.Round(new Rectangle(0,0,Width-1,Height-1),6);using var fill=new SolidBrush(hover&&Enabled?WarmTheme.Peach:WarmTheme.Background);g.FillPath(fill,path);using var pen=new Pen(Enabled?WarmTheme.Accent:WarmTheme.Muted,1.8f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round};float x=Width/2f,y=Height/2f,sign=up?-1:1;g.DrawLines(pen,[new PointF(x-4,y-sign*2),new PointF(x,y+sign*2),new PointF(x+4,y-sign*2)]);}
}
public static class Ui
{
    public static Label Heading(string text, int size = 22) => new ReadableLabel() { Text = text, AutoSize = true, Font = new Font("Noto Sans KR", size, FontStyle.Bold), Margin = new Padding(4, 8, 4, 16) };
    public static Label Note(string text) => new ReadableLabel() { Text = text, AutoSize = true, Margin = new Padding(4, 10, 4, 10), ForeColor = WarmTheme.Muted };
    public static WarmButton Action(string text, Action action, bool primary = false) { var b = new WarmButton { Text = text, AutoSize = true, MinimumSize = new Size(100, 42), Primary = primary, Margin = new Padding(4) }; b.Click += (_, _) => action(); return b; }
    public static FlowLayoutPanel Row(params Control[] c) { var p = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(2) }; p.Controls.AddRange(c); return p; }
}


