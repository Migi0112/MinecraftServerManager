// [소스 안내] 설치된 JAR과 배포처 프로젝트를 연결합니다. 파일 해시 조회, 메타데이터 캐시, 대표 이미지와 설치 여부 확인을 담당합니다.
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace NeoForgeServerManager;

// A file hash identifies a release even when its local filename has been changed.
public static class InstalledExtensions
{
    static readonly HttpClient http=new(){Timeout=TimeSpan.FromSeconds(10)};
    static readonly SemaphoreSlim slots=new(4);
    public sealed record Cached(ModPluginService.Result? Project,DateTime CheckedAt);
    static string Cache(string hash)=>Path.Combine(Storage.Root,"extension-metadata",hash.ToLowerInvariant()+".json");
    public static string[] Files(Profile profile){string dir=Path.Combine(profile.Folder,profile.Kind is "Paper" or "Purpur"?"plugins":"mods");return Directory.Exists(dir)?Directory.GetFiles(dir).Where(f=>f.EndsWith(".jar",StringComparison.OrdinalIgnoreCase)||f.EndsWith(".jar.disabled",StringComparison.OrdinalIgnoreCase)).ToArray():[];}
    public static void Remember(string hash,ModPluginService.Result project){try{Storage.AtomicWrite(Cache(hash),JsonSerializer.Serialize(new Cached(project,DateTime.UtcNow)));}catch(IOException){}catch(UnauthorizedAccessException){}}
    // [동작 설명] 파일의 SHA-512를 계산해 캐시 또는 배포처에서 정확한 프로젝트를 찾습니다.
    public static async Task<ModPluginService.Result?> Resolve(string file)
    {
        try{
            string hash=await Task.Run(()=>{using var input=File.OpenRead(file);return Convert.ToHexString(SHA512.HashData(input)).ToLowerInvariant();});
            await slots.WaitAsync();try{
                string cache=Cache(hash);
                if(File.Exists(cache)){try{var saved=JsonSerializer.Deserialize<Cached>(File.ReadAllText(cache));if(saved!=null&&(saved.Project!=null||saved.CheckedAt>DateTime.UtcNow.AddDays(-1)))return saved.Project;}catch(JsonException){}}
                using var request=new HttpRequestMessage(HttpMethod.Get,"https://api.modrinth.com/v2/version_file/"+hash+"?algorithm=sha512");request.Headers.UserAgent.ParseAdd("MinecraftServerManager/1.12.8");
                using var response=await http.SendAsync(request);
                if(response.StatusCode==HttpStatusCode.NotFound){Storage.AtomicWrite(cache,JsonSerializer.Serialize(new Cached(null,DateTime.UtcNow)));return null;}
                response.EnsureSuccessStatusCode();var version=JsonNode.Parse(await response.Content.ReadAsStringAsync())!;string id=(string?)version["project_id"]??throw new JsonException("Missing project ID");
                using var projectRequest=new HttpRequestMessage(HttpMethod.Get,"https://api.modrinth.com/v2/project/"+Uri.EscapeDataString(id));projectRequest.Headers.UserAgent.ParseAdd("MinecraftServerManager/1.12.8");
                using var projectResponse=await http.SendAsync(projectRequest);projectResponse.EnsureSuccessStatusCode();var data=JsonNode.Parse(await projectResponse.Content.ReadAsStringAsync())!;
                var project=new ModPluginService.Result(id,data["title"]!.ToString(),data["slug"]!.ToString(),(string?)data["icon_url"],(string?)data["client_side"]??"unknown",(string?)data["server_side"]??"unknown");Remember(hash,project);return project;
            }finally{slots.Release();}
        }catch(Exception e) when(e is IOException or UnauthorizedAccessException or HttpRequestException or OperationCanceledException or JsonException){return null;}
    }
    // Identity hints are used for pictures only, never to claim an exact installed release.
    // [동작 설명] 정확한 해시 조회와 로컬 정보 보완으로 대표 이미지 및 서버/클라이언트 표시 정보를 구합니다.
    public static async Task<ModPluginService.Result?> ResolveArtwork(string file){
        var project=await Resolve(file);await slots.WaitAsync();
        try{
            using var net=new DownloadClient("");
            if(project==null&&LocalModMetadata.Read(file) is {} local)project=await LocalModMetadata.Project(net,local.Id);
            string? id=project?.Id??JarBranding.KnownArtworkProject(file);if(id==null)return project;
            if(project?.ServerSide is not (null or "unknown")&&project?.ClientSide is not (null or "unknown"))return project;
            var data=await net.Json("https://api.modrinth.com/v2/project/"+Uri.EscapeDataString(id));
            return new(data["id"]!.ToString(),data["title"]!.ToString(),data["slug"]!.ToString(),(string?)data["icon_url"],(string?)data["client_side"]??"unknown",(string?)data["server_side"]??"unknown");
        }catch(Exception e) when(e is IOException or HttpRequestException or JsonException or OperationCanceledException or UnauthorizedAccessException){return project;}
        finally{slots.Release();}
    }
    // [동작 설명] 설치된 파일에 대응하는 프로젝트 ID 집합을 만들어 온라인 검색의 중복 설치를 막는 데 사용합니다.
    public static async Task<HashSet<string>> ProjectIds(Profile profile){
        async Task<string?> Identify(string file){
            var project=await Resolve(file);
            if(project==null&&LocalModMetadata.Read(file) is {} metadata){
                await slots.WaitAsync();try{using var net=new DownloadClient();project=await LocalModMetadata.Project(net,metadata.Id);}finally{slots.Release();}
            }
            return project?.Id;
        }
        return (await Task.WhenAll(Files(profile).Select(Identify))).OfType<string>().ToHashSet(StringComparer.Ordinal);
    }
}


