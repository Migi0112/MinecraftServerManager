// [소스 안내] JAR 안의 로고와 이름 등 표시 정보를 읽습니다. 외부 이미지 조회와 별개로 파일에 포함된 대표 이미지를 찾습니다.
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace NeoForgeServerManager;

public static class JarBranding
{
    public static string Document(ZipArchive zip){var e=zip.GetEntry("META-INF/neoforge.mods.toml")??zip.GetEntry("META-INF/mods.toml");if(e==null||e.Length>200000)return "";using var reader=new StreamReader(e.Open());return reader.ReadToEnd();}
    public static string Field(string text,string name){var match=Regex.Match(text,@"(?m)^\s*"+Regex.Escape(name)+"\\s*=\\s*[\"']([^\"'\\r\\n]+)[\"']");return match.Success?match.Groups[1].Value:"";}
    static IEnumerable<ZipArchiveEntry> Nested(ZipArchive zip)=>zip.Entries.Where(e=>e.FullName.StartsWith("META-INF/jarjar/")&&e.FullName.EndsWith(".jar")&&e.Length<16*1024*1024).Take(24);
    public static Image? Icon(ZipArchive zip,int depth=0)
    {
        string logo=Field(Document(zip),"logoFile");ZipArchiveEntry? entry=logo.Length>0?zip.GetEntry(logo):null;
        var fabric=zip.GetEntry("fabric.mod.json");if(fabric!=null&&fabric.Length<200000){using var doc=JsonDocument.Parse(fabric.Open());if(doc.RootElement.TryGetProperty("icon",out var value)){string? path=value.ValueKind==JsonValueKind.String?value.GetString():value.ValueKind==JsonValueKind.Object?value.EnumerateObject().OrderByDescending(p=>int.TryParse(p.Name,out int size)?size:0).FirstOrDefault().Value.GetString():null;if(path!=null)entry=zip.GetEntry(path);}}
        entry??=zip.Entries.FirstOrDefault(e=>e.FullName is "icon.png" or "logo.png" or "pack.png");
        if(entry!=null&&entry.Length<=4*1024*1024){using var input=entry.Open();using var image=Image.FromStream(input);var result=new Bitmap(96,96);using(var g=Graphics.FromImage(result))g.DrawImage(image,Artwork.Fit(image,new Rectangle(0,0,96,96)));return result;}
        if(depth<1)foreach(var jar in Nested(zip)){using var stream=jar.Open();using var memory=new MemoryStream();stream.CopyTo(memory);memory.Position=0;using var nested=new ZipArchive(memory);var icon=Icon(nested,depth+1);if(icon!=null)return icon;}
        return null;
    }
    public static (string Name,string Version)? NestedMetadata(ZipArchive zip){foreach(var jar in Nested(zip)){using var stream=jar.Open();using var memory=new MemoryStream();stream.CopyTo(memory);memory.Position=0;using var nested=new ZipArchive(memory);string doc=Document(nested),name=Field(doc,"displayName");if(name.Length>0)return(name,Field(doc,"version"));}return null;}
    public static string? KnownArtworkProject(string file){using var zip=ZipFile.OpenRead(file);string doc=Document(zip);return Field(doc,"modId") switch{"sophisticatedcore"=>"sophisticated-core",_=>null};}
}
