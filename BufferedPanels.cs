// [소스 안내] 이중 버퍼링을 사용하는 패널입니다. 화면을 그리는 중간 상태가 보여 깜빡이는 현상을 줄입니다.
namespace NeoForgeServerManager;
public sealed class BufferedScrollPanel : Panel
{
    public BufferedScrollPanel(){DoubleBuffered=true;SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
}
public sealed class BufferedTablePanel : TableLayoutPanel
{
    public BufferedTablePanel(){DoubleBuffered=true;SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
}
