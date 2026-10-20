// [소스 안내] 서버 설정 옆 도움말을 보여주는 팝업입니다. 설정의 의미를 입력 화면 가까이에서 확인하게 합니다.
using System.Drawing.Drawing2D;
namespace NeoForgeServerManager;

// 도움말은 입력을 막는 대화상자가 아닙니다. ? 위에 있는 동안만 표시하고 창 밖으로 벗어나지 않습니다.
public sealed class PropertyHelpPopup(PropertyDefinition definition) : Form
{
    protected override bool ShowWithoutActivation=>true;
    protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x08000080;return p;}}
    public static void Attach(Control anchor,PropertyDefinition definition)
    {
        var popup=new PropertyHelpPopup(definition){FormBorderStyle=FormBorderStyle.None,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,BackColor=WarmTheme.Surface,Size=new Size(508,306)};
        anchor.AccessibleDescription=definition.Help;
        void Show(){if(anchor.FindForm() is not {} owner)return;var r=anchor.RectangleToScreen(anchor.ClientRectangle);var area=Rectangle.Intersect(owner.RectangleToScreen(owner.ClientRectangle),Screen.FromControl(anchor).WorkingArea);int x=r.Right+36;if(x+popup.Width>area.Right)x=r.Left-popup.Width-20;popup.Location=new Point(Math.Clamp(x,area.Left,Math.Max(area.Left,area.Right-popup.Width)),Math.Clamp(r.Top-116,area.Top,Math.Max(area.Top,area.Bottom-popup.Height)));if(!popup.Visible)popup.Show(owner);}
        anchor.MouseEnter+=(_,_)=>Show();anchor.MouseLeave+=(_,_)=>popup.Hide();anchor.Leave+=(_,_)=>popup.Hide();anchor.VisibleChanged+=(_,_)=>{if(!anchor.Visible)popup.Hide();};anchor.Disposed+=(_,_)=>popup.Dispose();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        using var border=new Pen(Color.FromArgb(196,178,156));using var shape=WarmTheme.Round(new Rectangle(1,1,Width-3,Height-3),12);g.DrawPath(border,shape);
        using var titleFont=new Font("Noto Sans KR",17,FontStyle.Bold);using var labelFont=new Font("Noto Sans KR",14,FontStyle.Bold);using var textFont=new Font("Noto Sans KR",14);using var creditFont=new Font("Noto Sans KR",10);
        string title=definition.Key=="pvp"?"PVP (pvp)":definition.Korean+" ("+definition.Key+")";
        ReferenceIcons.Draw(g,"info",new Rectangle(22,22,30,30),WarmTheme.Ink);
        TextRenderer.DrawText(g,title,titleFont,new Rectangle(66,14,Width-88,48),WarmTheme.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g,definition.Description,textFont,new Rectangle(22,67,Width-44,58),WarmTheme.Ink,TextFormatFlags.WordBreak);
        g.DrawLine(border,22,128,Width-22,128);
        string values=definition.Type=="bool"?"true / false":definition.Options!=null?string.Join(" / ",definition.Options):definition.Type=="number"?$"{definition.Min} ~ {definition.Max}":"줄바꿈 없는 문자열";
        string caution=definition.Key=="pvp"?"비활성화하면 플레이어끼리 직접 피해를 줄 수 없습니다.":"서버 종료 후 변경하세요. 버전에 따라 기본값과 지원 여부가 다를 수 있습니다.";
        string[] labels=["기본값","허용 값","주의사항"],content=[definition.Default,values,caution];
        for(int i=0;i<3;i++){int y=139+i*36;TextRenderer.DrawText(g,labels[i],labelFont,new Rectangle(22,y,92,34),WarmTheme.Ink,TextFormatFlags.NoPadding);TextRenderer.DrawText(g,content[i],textFont,new Rectangle(118,y,Width-140,i==2?68:34),WarmTheme.Muted,TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);}
        TextRenderer.DrawText(g,"제작자 Migi0112",creditFont,new Rectangle(200,278,Width-218,24),WarmTheme.Muted,TextFormatFlags.Right|TextFormatFlags.VerticalCenter);
    }
}
