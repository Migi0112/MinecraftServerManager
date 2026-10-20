// [소스 안내] 풍경 배경의 로딩·캐시와 그리기입니다. 페이지와 카드 배경을 일관되게 표시합니다.
using System.Drawing.Drawing2D;
namespace NeoForgeServerManager;
public static class Scenery
{
    static readonly Lazy<Image> wallpaper=new(()=>{using var stream=typeof(Scenery).Assembly.GetManifestResourceStream("NeoForgeServerManager.Assets.landscape.png")!;using var image=Image.FromStream(stream);return new Bitmap(image);});
    sealed class Cache : IDisposable { public Bitmap? Image; public Size Size; public void Dispose(){Image?.Dispose();Image=null;} }
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control,Cache> caches=new();
    public static int RenderCount {get;private set;}
    public static void Make(Control control){control.Tag="landscape";control.BackColor=Color.Transparent;}
    public static void Paint(Graphics g,Control control)
    {
        Control root=control;var offset=Point.Empty;
        while(root.Parent!=null){offset.Offset(root.Left,root.Top);root=root.Parent;}
        if(root.Width<=0||root.Height<=0)return;
        var cache=caches.GetValue(root,key=>{var item=new Cache();key.Disposed+=(_,_)=>item.Dispose();return item;});
        if(cache.Image==null||cache.Size!=root.Size){
            cache.Dispose();cache.Size=root.Size;cache.Image=new Bitmap(root.Width,root.Height);
            using var target=Graphics.FromImage(cache.Image);var image=wallpaper.Value;
            float scale=Math.Max(root.Width/(float)image.Width,root.Height/(float)image.Height),w=image.Width*scale,h=image.Height*scale;
            target.InterpolationMode=InterpolationMode.HighQualityBilinear;
            target.DrawImage(image,new RectangleF((root.Width-w)/2,(root.Height-h)/2,w,h));
            using var veil=new SolidBrush(Color.FromArgb(48,WarmTheme.Background));target.FillRectangle(veil,new Rectangle(Point.Empty,root.Size));RenderCount++;
        }
        g.DrawImageUnscaled(cache.Image,-offset.X,-offset.Y);
    }
    public static void CardBase(Graphics g,Control control)
    {
        if(control.Parent?.BackColor==Color.Transparent)Paint(g,control);else g.Clear(control.Parent?.BackColor??WarmTheme.Background);
    }
}
