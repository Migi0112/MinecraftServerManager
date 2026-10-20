// [소스 안내] 아이템 이미지를 불러오고 표시용으로 관리합니다. 목록에서 아이템을 그림으로 구분할 때 사용합니다.
using System.IO.Compression;
using System.Text.Json;
namespace NeoForgeServerManager;

// Reads installed assets only. No redistributed game textures and no network dependency.
public sealed class ItemArtwork:IDisposable
{
    readonly List<string> archives=[];
    readonly Dictionary<string,Bitmap?> images=[];
    public ItemArtwork(string serverFolder)
    {
        string mods=Path.Combine(serverFolder,"mods");if(Directory.Exists(mods))archives.AddRange(Directory.GetFiles(mods,"*.jar"));
        string versions=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),".minecraft","versions");
        if(Directory.Exists(versions))foreach(string d in Directory.GetDirectories(versions).OrderDescending())archives.AddRange(Directory.GetFiles(d,"*.jar"));
        string cache=Path.Combine(Storage.Root,"item-catalog");if(Directory.Exists(cache))archives.AddRange(Directory.GetFiles(cache,"client.jar",SearchOption.AllDirectories));
    }
    static string Asset(string resource,string kind,string suffix){var parts=resource.Split(':',2);return $"assets/{(parts.Length==2?parts[0]:"minecraft")}/{kind}/{parts[^1]}{suffix}";}
    static JsonDocument? Json(ZipArchive z,string path){var e=z.GetEntry(path);if(e==null||e.Length>1024*1024)return null;using var stream=e.Open();return JsonDocument.Parse(stream);}
    static string? Texture(ZipArchive z,string resource,int depth=0)
    {
        if(depth>8)return null;using var model=Json(z,Asset(resource,"models",".json"));if(model==null)return null;
        if(model.RootElement.TryGetProperty("textures",out var textures))foreach(string key in new[]{"layer0","all","side","front","texture","particle"})if(textures.TryGetProperty(key,out var v)&&v.GetString() is {} value&&!value.StartsWith('#'))return value;
        return model.RootElement.TryGetProperty("parent",out var p)&&p.GetString() is {} parent?Texture(z,parent,depth+1):null;
    }
    public Bitmap? Load(string id)
    {
        if(images.TryGetValue(id,out var cached))return cached;
        string[] parts=id.Split(':',2);if(parts.Length!=2||parts.Any(p=>p.Contains("..")||p.Contains('\\')))return null;
        foreach(var path in archives){try{using var z=ZipFile.OpenRead(path);string model=parts[0]+":item/"+parts[1];string? texture=Texture(z,model);
            string[] candidates=[Asset(texture??model,"textures",".png"),Asset(parts[0]+":block/"+parts[1],"textures",".png")];
            foreach(var candidate in candidates){var e=z.GetEntry(candidate);if(e==null||e.Length>4*1024*1024)continue;using var stream=e.Open();using var source=Image.FromStream(stream);if(source.Width>4096||source.Height>4096)continue;int side=Math.Min(source.Width,source.Height);var image=new Bitmap(side,side);using(var g=Graphics.FromImage(image))g.DrawImage(source,new Rectangle(0,0,side,side),new Rectangle(0,0,side,side),GraphicsUnit.Pixel);images[id]=image;return image;}
        }catch(Exception e)when(e is IOException or InvalidDataException or ArgumentException or JsonException){}}
        images[id]=null;return null;
    }
    public void Dispose(){foreach(var image in images.Values)image?.Dispose();images.Clear();}
}
