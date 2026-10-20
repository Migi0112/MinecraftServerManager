// [소스 안내] 서버 저장 폴더를 검색하여 등록 가능한 서버를 찾습니다. 폴더의 실제 서버 파일을 확인하며 경로를 정규화합니다.
using System.IO.Compression;
using System.Text.Json;
namespace NeoForgeServerManager;

// Discovery reads only launch metadata, never traverses worlds or executes server files.
public static class ServerDiscovery
{
    public sealed record Result(List<Profile> Servers, int Existing, List<string> Skipped);
    public static string Key(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    static bool Linked(string path) => new DirectoryInfo(path).LinkTarget != null;
    static bool LocalFile(string folder,string relative)
    {
        if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative))return false;
        string full=Path.GetFullPath(Path.Combine(folder,relative));
        if(!full.StartsWith(Key(folder)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!File.Exists(full))return false;
        if(new FileInfo(full).LinkTarget!=null)return false;
        for(var d=new DirectoryInfo(Path.GetDirectoryName(full)!);d!=null&&!Key(d.FullName).Equals(Key(folder),StringComparison.OrdinalIgnoreCase);d=d.Parent)
            if(d.LinkTarget!=null)return false;
        return true;
    }
    public static Result Scan(string root,IEnumerable<Profile> registered)
    {
        if(!Directory.Exists(root))throw new IOException("서버 저장 폴더를 찾을 수 없습니다. 경로 설정을 확인해 주세요.");
        if(Linked(root))throw new IOException("연결된 폴더 대신 실제 서버 저장 폴더를 선택해 주세요.");
        var known=registered.Select(p=>Key(p.Folder)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added=new List<Profile>();var skipped=new List<string>();int existing=0;
        foreach(string folder in Directory.EnumerateDirectories(root).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase))
        {
            if(known.Contains(Key(folder))){existing++;continue;}
            try {
                if(Linked(folder)||Path.GetFileName(folder).Contains(".importing-",StringComparison.OrdinalIgnoreCase)){skipped.Add(Path.GetFileName(folder));continue;}
                var profile=Inspect(folder);
                if(profile==null){skipped.Add(Path.GetFileName(folder));continue;}
                profile.Id=Guid.NewGuid().ToString("N");profile.Folder=Key(folder);profile.Name=Path.GetFileName(folder);
                profile.AutoRestart=profile.AutoBackup=false;
                profile.Port=int.TryParse(PropertiesFile.ReadValue(Path.Combine(folder,"server.properties"),"server-port","25565"),out int port)&&port is >0 and <=65535?port:25565;
                added.Add(profile);known.Add(Key(folder));
            }catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException or ArgumentException){skipped.Add(Path.GetFileName(folder));}
        }
        return new(added,existing,skipped);
    }
    static Profile? Inspect(string folder)
    {
        string descriptor=Path.Combine(folder,"manager-launch.json");
        if(LocalFile(folder,"manager-launch.json")&&new FileInfo(descriptor).Length<1024*1024){
            var p=JsonSerializer.Deserialize<Profile>(File.ReadAllText(descriptor));
            if(p!=null&&new[]{"Paper","Purpur","Fabric","Forge","NeoForge"}.Contains(p.Kind)&&
               (LocalFile(folder,p.ArgumentFile)||LocalFile(folder,p.LaunchJar)||(!p.ManagedInstall&&LocalFile(folder,"run.bat"))))return p;
        }
        foreach(var (kind,relative) in new[]{("NeoForge","libraries/net/neoforged/neoforge"),("Forge","libraries/net/minecraftforge/forge")}){
            string dir=Path.Combine(folder,relative);
            if(!Directory.Exists(dir)||Linked(dir))continue;
            var args=Directory.EnumerateDirectories(dir).Select(d=>Path.GetRelativePath(folder,Path.Combine(d,"win_args.txt"))).Where(p=>LocalFile(folder,p)).ToArray();
            if(args.Length==1){string loader=Path.GetFileName(Path.GetDirectoryName(args[0])!);return new(){Kind=kind,ManagedInstall=true,ArgumentFile=args[0],LoaderVersion=loader,MinecraftVersion=kind=="Forge"?loader.Split('-')[0]:"미확인",TpsCommand=kind=="Forge"?"forge tps":"neoforge tps"};}
            if(args.Length>1)return null; // Multiple installations need an explicit launch choice.
        }
        var jars=new List<Profile>();
        foreach(string jar in Directory.EnumerateFiles(folder,"*.jar")){
            if(!LocalFile(folder,Path.GetFileName(jar))||Path.GetFileName(jar).Contains("installer",StringComparison.OrdinalIgnoreCase))continue;
            try{
                using var zip=ZipFile.OpenRead(jar);string Read(string name){var e=zip.GetEntry(name);if(e==null||e.Length>200000)return "";using var r=new StreamReader(e.Open());return r.ReadToEnd();}
                string manifest=Read("META-INF/MANIFEST.MF");string? kind=manifest.Contains("net.fabricmc",StringComparison.OrdinalIgnoreCase)?"Fabric":manifest.Contains("paperclip",StringComparison.OrdinalIgnoreCase)?(Path.GetFileName(jar).Contains("purpur",StringComparison.OrdinalIgnoreCase)?"Purpur":"Paper"):null;
                if(kind==null)continue;
                string version="미확인",versionJson=Read("version.json");if(versionJson.Length>0){using var doc=JsonDocument.Parse(versionJson);if(doc.RootElement.TryGetProperty("id",out var id))version=id.GetString()??version;}
                jars.Add(new(){Kind=kind,ManagedInstall=true,LaunchJar=Path.GetFileName(jar),MinecraftVersion=version,TpsCommand=kind=="Fabric"?"":"tps"});
            }catch(Exception e) when(e is IOException or JsonException){ }
        }
        return jars.Count==1?jars[0]:null;
    }
    public static List<Profile> Register(Result result)
    {
        // Reload immediately before saving so a completed import is not overwritten.
        var current=Storage.Load();var known=current.Select(p=>Key(p.Folder)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added=result.Servers.Where(p=>known.Add(Key(p.Folder))).ToList();
        if(added.Count>0){current.AddRange(added);Storage.Save(current);}return added;
    }
}
