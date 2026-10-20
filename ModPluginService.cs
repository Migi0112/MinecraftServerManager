// [소스 안내] Modrinth 검색 결과와 공식 배포 파일을 다룹니다. 로더·게임 버전별 검색, 인기순 100개 조회와 설치 보조 기능입니다.
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Security.Cryptography;
namespace NeoForgeServerManager;

// Modrinth에서 제작자가 공개한 다운로드만 사용합니다. 게임 버전과 로더를 검색/버전 조회 양쪽에서 검사합니다.
public sealed class ModPluginService(DownloadClient net)
{
    public record Result(string Id, string Title, string Slug, string? IconUrl=null,string ClientSide="unknown",string ServerSide="unknown") { public override string ToString() => Title; }
    internal static string Loader(Profile p) => p.Kind.ToLowerInvariant() is "purpur" ? "paper" : p.Kind.ToLowerInvariant();
    public static string SearchUrl(Profile p,string query,bool compatibleOnly=true,int offset=0)
    {
        if(compatibleOnly&&!MinecraftVersions.Valid(p.MinecraftVersion))throw new IOException("Minecraft 버전을 먼저 지정하세요. 미확인 버전으로 호환 검색할 수 없습니다.");
        var filters=new List<string[]>{new[]{"categories:"+Loader(p)},new[]{"server_side:required","server_side:optional"}};
        if(compatibleOnly)filters.Add(new[]{"versions:"+p.MinecraftVersion});
        return "https://api.modrinth.com/v2/search?limit=100&offset="+Math.Max(0,offset)+"&index="+(string.IsNullOrWhiteSpace(query)?"downloads":"relevance")+"&query="+Uri.EscapeDataString(query)+"&facets="+Uri.EscapeDataString(JsonSerializer.Serialize(filters));
    }
    public record SearchPage(Result[] Items,int Total,int Offset);
    public async Task<SearchPage> SearchPageAsync(Profile p,string query,bool compatibleOnly=true,int offset=0)
    {
        var json=await net.Json(SearchUrl(p,query,compatibleOnly,offset));
        var hits=json["hits"]!.AsArray().Select(x=>new Result(x!["project_id"]!.ToString(),x["title"]!.ToString(),x["slug"]!.ToString(),(string?)x["icon_url"],(string?)x["client_side"]??"unknown",(string?)x["server_side"]??"unknown")).ToArray();
        return new(hits,(int?)json["total_hits"]??hits.Length,offset);
    }
    public async Task<Result[]> Search(Profile p,string query,bool compatibleOnly=true)=>(await SearchPageAsync(p,query,compatibleOnly)).Items;
    public async Task Install(Profile p, Result result, IProgress<string> log)
    {
        if((await InstalledExtensions.ProjectIds(p)).Contains(result.Id))throw new IOException("이미 설치된 모드 또는 플러그인입니다.");
        if(!MinecraftVersions.Valid(p.MinecraftVersion))throw new IOException("설치 전에 Minecraft 버전을 지정하세요.");
        string suffix = "?loaders=" + Uri.EscapeDataString(JsonSerializer.Serialize(new[] { Loader(p) })) + "&game_versions=" + Uri.EscapeDataString(JsonSerializer.Serialize(new[] { p.MinecraftVersion }));
        var versions = (await net.Json("https://api.modrinth.com/v2/project/" + result.Id + "/version" + suffix)).AsArray();
        var v = versions.FirstOrDefault(x => x!["version_type"]!.ToString() == "release") ?? throw new IOException("일치하는 정식 배포 버전이 없습니다.");
        // 의존성을 자동으로 잘못 선택하지 않도록 필요한 의존성이 있으면 제작자 페이지에서 검토하도록 안내합니다.
        var dependencies = v["dependencies"]!.AsArray().Where(x => x!["dependency_type"]!.ToString() == "required").ToArray();
        if (dependencies.Length > 0) throw new IOException("필수 의존성이 있는 배포입니다. 프로젝트 페이지에서 의존성을 확인하고 파일을 함께 내려받아 로컬 JAR 추가를 사용하세요. 자동 의존성 설치는 후속 단계입니다.");
        var files = v["files"]!.AsArray(); var file = files.FirstOrDefault(x => (bool?)x!["primary"] == true) ?? files.First()!;
        string filename = file!["filename"]!.ToString(); if (Path.GetFileName(filename) != filename || !filename.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)) throw new IOException("올바르지 않은 JAR 파일명");
        string dir = Path.Combine(p.Folder, p.Kind is "Paper" or "Purpur" ? "plugins" : "mods"); Directory.CreateDirectory(dir);
        string target = Path.Combine(dir, filename); if (File.Exists(target)) throw new IOException("동일 파일이 이미 있습니다.");
        await net.FileAsync(file["url"]!.ToString(), target, log, CancellationToken.None);
        using var stream = File.OpenRead(target); string actual = Convert.ToHexString(SHA512.HashData(stream));
        if (!actual.Equals(file["hashes"]!["sha512"]!.ToString(), StringComparison.OrdinalIgnoreCase)) { stream.Close(); File.Delete(target); throw new IOException("배포 파일 무결성 검사 실패"); }
        InstalledExtensions.Remember(actual,result);
    }
}


