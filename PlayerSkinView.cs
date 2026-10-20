// [소스 안내] 플레이어 스킨을 표시하는 화면 컨트롤입니다. 플레이어 상세 화면에서 외형을 확인할 때 사용합니다.
using System.Drawing.Drawing2D;
using System.Net.Http;
using System.Text.Json.Nodes;
namespace NeoForgeServerManager;

public static class PlayerSkins
{
    static readonly HttpClient http=new(){Timeout=TimeSpan.FromSeconds(10)};
    static readonly SemaphoreSlim slots=new(3);
    public static async Task<Bitmap?> Load(string uuid)
    {
        if(!Guid.TryParse(uuid,out var id))return null;
        string cache=Path.Combine(Storage.Root,"skins",id+".png");
        try{
            if(File.Exists(cache)&&DateTime.UtcNow-File.GetLastWriteTimeUtc(cache)<TimeSpan.FromDays(3)){using var source=Image.FromFile(cache);var cached=new Bitmap(source);NormalizeLegacy(cached);cached.Tag=File.Exists(cache+".slim");return cached;}
            await slots.WaitAsync();try{
                var session=JsonNode.Parse(await http.GetStringAsync("https://sessionserver.mojang.com/session/minecraft/profile/"+id.ToString("N")));
                var prop=session?["properties"]?.AsArray().FirstOrDefault(x=>(string?)x?["name"]=="textures");
                var texture=JsonNode.Parse(Convert.FromBase64String((string?)prop?["value"]??""));
                if(!Uri.TryCreate((string?)texture?["textures"]?["SKIN"]?["url"],UriKind.Absolute,out var uri)||uri.Host!="textures.minecraft.net")return null;
                using var stream=new MemoryStream(await http.GetByteArrayAsync(new UriBuilder(uri){Scheme="https",Port=-1}.Uri));using var image=Image.FromStream(stream);
                if(image.Width!=64||image.Height is not (32 or 64))return null;
                var result=new Bitmap(image);NormalizeLegacy(result);bool slim=(string?)texture?["textures"]?["SKIN"]?["metadata"]?["model"]=="slim";result.Tag=slim;Directory.CreateDirectory(Path.GetDirectoryName(cache)!);result.Save(cache,System.Drawing.Imaging.ImageFormat.Png);if(slim)File.WriteAllText(cache+".slim","");else if(File.Exists(cache+".slim"))File.Delete(cache+".slim");return result;
            }finally{slots.Release();}
        }catch{return null;}
    }
    static void NormalizeLegacy(Bitmap image)
    {
        if(image.Height!=32)return;
        bool opaque=true;for(int y=0;y<16&&opaque;y++)for(int x=32;x<64;x++)if(image.GetPixel(x,y).A<128){opaque=false;break;}
        if(opaque)for(int y=0;y<16;y++)for(int x=32;x<64;x++)image.SetPixel(x,y,Color.Transparent);
    }
    public static Bitmap Face(Image skin){var result=new Bitmap(72,72);using var g=Graphics.FromImage(result);g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;g.DrawImage(skin,new Rectangle(0,0,72,72),new Rectangle(8,8,8,8),GraphicsUnit.Pixel);g.DrawImage(skin,new Rectangle(0,0,72,72),new Rectangle(40,8,8,8),GraphicsUnit.Pixel);return result;}
}

public sealed class PlayerSkinView : Control
{
    public Image? Skin {get;set;}
    public float Angle {get;set;}=-.18f;
    public float Zoom {get;set;}=1;
    int lastX;bool dragging;
    public PlayerSkinView(){DoubleBuffered=true;BackColor=WarmTheme.Background;Cursor=Cursors.SizeWE;MouseDown+=(_,e)=>{if(e.Button==MouseButtons.Left){dragging=true;lastX=e.X;Capture=true;}};MouseUp+=(_,_)=>{dragging=false;Capture=false;};MouseMove+=(_,e)=>{if(dragging){Angle+=(e.X-lastX)*.02f;lastX=e.X;Invalidate();}};MouseWheel+=(_,e)=>{Zoom=Math.Clamp(Zoom+e.Delta/120f*.1f,.6f,1.6f);Invalidate();};}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);if(Skin==null){TextRenderer.DrawText(e.Graphics,"스킨을 불러올 수 없습니다",Font,ClientRectangle,WarmTheme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak);return;}
        var faces=new List<(float depth,PointF[] points,Rectangle uv)>();
        float scale=Math.Min(Width/22f,Height/36f)*Zoom,cs=MathF.Cos(Angle),sn=MathF.Sin(Angle);
        (PointF p,float z) Project(float x,float y,float z){float xx=x*cs+z*sn,zz=-x*sn+z*cs;return(new(Width/2+xx*scale,Height/2+(y-16+zz*.12f)*scale),zz);}
        void Box(float x,float y,float z,int w,int h,int d,int u,int v,float grow=0)
        {
            float l=x-grow,r=x+w+grow,t=y-grow,b=y+h+grow,n=z-grow,f=z+d+grow;
            void Face((float x,float y,float z) a,(float x,float y,float z) bb,(float x,float y,float z) c,Rectangle uv){var p=Project(a.x,a.y,a.z);var q=Project(bb.x,bb.y,bb.z);var rr=Project(c.x,c.y,c.z);faces.Add(((p.z+q.z+rr.z)/3,new[]{p.p,q.p,rr.p},uv));}
            Face((l,t,f),(r,t,f),(l,b,f),new(u+d,v+d,w,h));
            Face((r,t,n),(l,t,n),(r,b,n),new(u+d+w+d,v+d,w,h));
            Face((l,t,n),(l,t,f),(l,b,n),new(u,v+d,d,h));
            Face((r,t,f),(r,t,n),(r,b,f),new(u+d+w,v+d,d,h));
            Face((l,t,n),(r,t,n),(l,t,f),new(u+d,v,w,d));
            Face((l,b,f),(r,b,f),(l,b,n),new(u+d+w,v,w,d));
        }
        int arm=Skin.Tag is true?3:4;
        Box(-4,0,-4,8,8,8,0,0);Box(-4,8,-2,8,12,4,16,16);Box(-4-arm,8,-2,arm,12,4,40,16);Box(4,8,-2,arm,12,4,Skin.Height==64?32:40,Skin.Height==64?48:16);Box(-4,20,-2,4,12,4,0,16);Box(0,20,-2,4,12,4,Skin.Height==64?16:0,Skin.Height==64?48:16);
        Box(-4,0,-4,8,8,8,32,0,.25f);
        if(Skin.Height==64){Box(-4,8,-2,8,12,4,16,32,.12f);Box(-4-arm,8,-2,arm,12,4,40,32,.12f);Box(4,8,-2,arm,12,4,48,48,.12f);Box(-4,20,-2,4,12,4,0,32,.12f);Box(0,20,-2,4,12,4,0,48,.12f);}
        e.Graphics.InterpolationMode=InterpolationMode.NearestNeighbor;e.Graphics.PixelOffsetMode=PixelOffsetMode.Half;
        foreach(var face in faces.OrderBy(f=>f.depth))e.Graphics.DrawImage(Skin,face.points,face.uv,GraphicsUnit.Pixel);
    }
}
