// [소스 안내] JAR 내부의 모드 ID·버전·필수 의존성을 읽습니다. Kotlin처럼 내부 JAR에 정보가 있는 경우와 버전 범위 검사도 처리합니다.
using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace NeoForgeServerManager;

public sealed record LocalRequirement(string Id,string Range);
public sealed record LocalModMetadata(string Id,string Version,LocalRequirement[] Required)
{
    // [동작 설명] JAR을 읽기 전용으로 열어 모드 선언을 읽습니다. 읽을 수 없으면 추측 대신 null을 반환합니다.
    public static LocalModMetadata? Read(string path)
    {
        try{
            using var zip=ZipFile.OpenRead(path);return ReadArchive(zip);
        }catch(Exception e) when(e is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException){return null;}
    }
    // [동작 설명] 바깥 JAR에 선언이 없으면 제한된 크기/깊이의 내부 JAR을 확인합니다. 후보가 여러 개면 임의로 하나를 고르지 않습니다.
    static LocalModMetadata? ReadArchive(ZipArchive zip,int depth=0)
    {
            // Fabric의 환경과 필수 의존성은 로컬 파일만으로도 검증할 수 있습니다.
            if(zip.GetEntry("fabric.mod.json") is {} fabric&&fabric.Length<=1024*1024){
                using var stream=fabric.Open();var json=JsonNode.Parse(stream);
                string fid=(string?)json?["id"]??"",fv=(string?)json?["version"]??"";
                if(fid.Length==0||fv.Length==0)return null;
                var deps=new List<LocalRequirement>();
                if(json?["depends"] is JsonObject dependencies)foreach(var dep in dependencies){
                    if(dep.Key is "minecraft" or "java" or "fabricloader")continue;
                    string range=dep.Value is JsonArray alternatives?string.Join(" || ",alternatives.Select(x=>x?.ToString()??"")):dep.Value?.ToString()??"*";
                    deps.Add(new(dep.Key,"fabric:"+range));
                }
                return new(fid,fv,deps.ToArray());
            }
            string text=JarBranding.Document(zip);
            var mod=Regex.Match(text,@"(?ms)^\s*\[\[mods\]\]\s*(.*?)(?=^\s*\[|\z)");
            if(!mod.Success){
                if(depth>=1)return null;
                var found=new List<LocalModMetadata>();long total=0;
                foreach(var entry in zip.Entries.Where(e=>e.FullName.StartsWith("META-INF/jarjar/")&&e.FullName.EndsWith(".jar")&&e.Length<16*1024*1024).Take(24)){
                    total+=entry.Length;if(total>64*1024*1024)break;
                    using var input=entry.Open();using var memory=new MemoryStream();input.CopyTo(memory);memory.Position=0;using var nested=new ZipArchive(memory);
                    if(ReadArchive(nested,depth+1) is {} item)found.Add(item);
                }
                var unique=found.DistinctBy(x=>(x.Id,x.Version)).ToArray();
                return unique.Length==1?unique[0]:null;
            }
            string id=JarBranding.Field(mod.Groups[1].Value,"modId"),version=JarBranding.Field(mod.Groups[1].Value,"version");
            if(id.Length==0||version.Length==0||version.Contains("${"))return null;
            var required=new List<LocalRequirement>();
            foreach(Match block in Regex.Matches(text,@"(?ms)^\s*\[\[dependencies\."+Regex.Escape(id)+@"\]\]\s*(.*?)(?=^\s*\[|\z)")){
                string body=block.Groups[1].Value,dep=JarBranding.Field(body,"modId");
                if(dep is "minecraft" or "forge" or "neoforge" || JarBranding.Field(body,"side")=="CLIENT")continue;
                if(JarBranding.Field(body,"type")=="required"||Regex.IsMatch(body,@"(?m)^\s*mandatory\s*=\s*true\b"))
                    required.Add(new(dep,JarBranding.Field(body,"versionRange")));
            }
            return new(id,version,required.ToArray());
    }
    public static string Normalize(string value)=>Regex.Replace(value.ToLowerInvariant(),"[^a-z0-9]","");
    // [동작 설명] 설치된/다운로드한 버전이 선언된 범위에 들어가는지 확인합니다. 해석하지 못하는 형식은 허용하지 않습니다.
    public static bool Accepts(string range,string version)
    {
        if(range.StartsWith("fabric:")){
            // 처리하지 못하는 범위는 자동 변경을 멈추고 사용자에게 확인을 요청합니다.
            if(range[7..].Trim()=="*")return true;
            if(!System.Version.TryParse(version,out var v))return false;
            return range[7..].Split(" || ").Any(option=>option.Split(' ',StringSplitOptions.RemoveEmptyEntries).All(term=>{
                var m=Regex.Match(term,@"^(>=|<=|>|<|=)?(\d+\.\d+(?:\.\d+){0,2})$");
                if(!m.Success||!System.Version.TryParse(m.Groups[2].Value,out var wanted))return false;
                return m.Groups[1].Value switch{">="=>v>=wanted,"<="=>v<=wanted,">"=>v>wanted,"<"=>v<wanted,_=>v==wanted};
            }));
        }
        if(range is "" or "*")return true;
        if(!System.Version.TryParse(version,out var actual))return false;
        if(range.StartsWith('[')&&range.EndsWith(']')&&!range.Contains(','))return System.Version.TryParse(range[1..^1],out var exact)&&actual==exact;
        var match=Regex.Match(range,@"^([\[(])([^,]*),([^\])]*)([\])])$");
        if(!match.Success)return false;
        string low=match.Groups[2].Value.Trim(),high=match.Groups[3].Value.Trim();
        if(low.Length>0&&(!System.Version.TryParse(low,out var min)||actual<min||(actual==min&&match.Groups[1].Value=="(")))return false;
        if(high.Length>0&&(!System.Version.TryParse(high,out var max)||actual>max||(actual==max&&match.Groups[4].Value==")")))return false;
        return true;
    }
    // [동작 설명] 검색 결과에서 정규화한 프로젝트 slug가 모드 ID와 일치하는 유일한 항목만 연결합니다.
    public static async Task<ModPluginService.Result?> Project(DownloadClient net,string id)
    {
        // Match a unique canonical slug, never a fuzzy display-name search result.
        foreach(string query in new[]{id,id=="kotlinforforge"?"kotlin for forge":id[..Math.Min(8,id.Length)]}.Distinct()){
            var json=await net.Json("https://api.modrinth.com/v2/search?limit=100&query="+Uri.EscapeDataString(query));
            var hits=json["hits"]!.AsArray().Where(x=>Normalize(x!["slug"]!.ToString())==Normalize(id)).ToArray();
            if(hits.Length==1)return new(hits[0]!["project_id"]!.ToString(),hits[0]!["title"]!.ToString(),hits[0]!["slug"]!.ToString(),(string?)hits[0]!["icon_url"],(string?)hits[0]!["client_side"]??"unknown",(string?)hits[0]!["server_side"]??"unknown");
        }
        return null;
    }
}
