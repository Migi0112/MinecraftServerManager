// [소스 안내] Paper·Fabric 등 서버 소프트웨어의 대표 이미지를 제공하는 코드입니다.
using System.Reflection;
namespace NeoForgeServerManager;
// 공식 사이트에서 받은 로고 원본을 종횡비를 유지해 그립니다.
public static class SoftwareArtwork
{
    static readonly Dictionary<string, Bitmap> logos = new(StringComparer.OrdinalIgnoreCase);
    public static void Draw(Graphics g, string kind, Rectangle target)
    {
        if (!logos.TryGetValue(kind, out var image))
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("NeoForgeServerManager.Assets." + kind.ToLowerInvariant() + ".png");
            if (stream == null) return;
            using var original = Image.FromStream(stream); image = new Bitmap(original); logos[kind] = image;
        }
        // Paper 워드마크에서 공식 심볼 부분을 사용합니다. 옆의 제목과 글자가 중복되지 않습니다.
        var source=kind=="Paper"?new Rectangle(0,0,91,91):new Rectangle(Point.Empty,image.Size);
        float scale = Math.Min((float)target.Width / source.Width, (float)target.Height / source.Height);
        var rect = new Rectangle(target.X + (target.Width - (int)(source.Width * scale)) / 2, target.Y + (target.Height - (int)(source.Height * scale)) / 2, (int)(source.Width * scale), (int)(source.Height * scale));
        if (kind == "Forge") { using var brush = new SolidBrush(Color.FromArgb(48,44,40)); g.FillRectangle(brush, rect); }
        g.DrawImage(image, rect,source,GraphicsUnit.Pixel);
    }
}

public sealed class SoftwareLogo(string kind) : Control
{
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);SoftwareArtwork.Draw(e.Graphics,kind,ClientRectangle);}
}
