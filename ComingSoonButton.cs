// [소스 안내] 자동 모드 설치의 준비 중 버튼입니다. 클릭 실행을 막고 아직 제공되지 않는 기능임을 안내합니다.
namespace NeoForgeServerManager;

// A non-interactive surface receives hover events that a disabled native button cannot.
public sealed class ComingSoonButton:Control
{
    readonly ToolTip tip=new(){InitialDelay=200,ReshowDelay=100,AutoPopDelay=5000};
    public ComingSoonButton(){Text="자동 모드 설치";Size=new(164,44);Margin=new Padding(4);TabStop=false;Cursor=Cursors.Default;AccessibleRole=AccessibleRole.StaticText;AccessibleDescription="아직 준비중입니다";SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);tip.SetToolTip(this,"아직 준비중입니다");}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Parent?.BackColor??WarmTheme.Surface);using var path=WarmTheme.Round(new Rectangle(1,1,Width-3,Height-3),10);using var fill=new SolidBrush(WarmTheme.Background);using var pen=new Pen(WarmTheme.Line);e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(pen,path);TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,WarmTheme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);}
    protected override void OnClick(EventArgs e){} // No mouse, keyboard or accessibility action.
    protected override void Dispose(bool disposing){if(disposing)tip.Dispose();base.Dispose(disposing);}
}
