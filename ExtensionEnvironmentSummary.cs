// [소스 안내] 온라인 검색 상단에 배포처·로더·Minecraft 버전 조건을 표시하는 컨트롤입니다.
namespace NeoForgeServerManager;
public sealed class ExtensionEnvironmentSummary : Control
{
    public string Kind{get;set;}="";public string Version{get;set;}="";
    public ExtensionEnvironmentSummary(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);AccessibleName="검색 환경";}
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(WarmTheme.Surface);using var label=new Font("Noto Sans KR",9);using var value=new Font("Noto Sans KR",12,FontStyle.Bold);
        string[] labels=["배포처","서버 종류","게임 버전"],values=["Modrinth",Kind,Version];int width=Math.Max(100,Math.Min(220,Width/3));
        for(int i=0;i<3;i++){int x=8+i*width;TextRenderer.DrawText(e.Graphics,labels[i],label,new Rectangle(x,0,width-16,20),WarmTheme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);TextRenderer.DrawText(e.Graphics,values[i],value,new Rectangle(x,19,width-16,30),WarmTheme.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);}
    }
}
