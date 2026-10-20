// [소스 안내] 프로필 저장소, 프로세스 자원 측정, Java 관련 조회 등 여러 화면이 공유하는 보조 서비스입니다.
using System.Diagnostics;
using System.Text.RegularExpressions;
namespace NeoForgeServerManager;

// 기존 프로필 파일 위치를 유지하여 이전 버전에서 만든 서버 목록도 그대로 읽습니다.
public sealed class ProfileRepository
{
    public List<Profile> Load() => Storage.Load();
    public void Save(List<Profile> profiles) => Storage.Save(profiles);
}
// CPU는 전체 논리 코어 대비 비율, RAM은 힙 크기가 아닌 프로세스 실제 작업 집합입니다.
public sealed class SystemMetricsService
{
    TimeSpan lastCpu; DateTime lastSample;
    public string Sample(Process process)
    {
        process.Refresh(); var now = DateTime.UtcNow; var cpu = process.TotalProcessorTime;
        double percent = lastSample == default ? 0 : (cpu - lastCpu).TotalMilliseconds / Math.Max(1, (now - lastSample).TotalMilliseconds) / Environment.ProcessorCount * 100;
        lastCpu = cpu; lastSample = now;
        return $"RAM {process.WorkingSet64 / 1048576.0:N0} MB  |  CPU {Math.Clamp(percent, 0, 100):F1}%";
    }
}
public static class JavaService
{
    // 설치 폴더와 PATH만 조사합니다. Java를 새로 설치하거나 시스템 환경 변수는 변경하지 않습니다.
    public static async Task<(string Path,string Version)> FindInstalledAsync()
    {
        var candidates=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string? root){if(!string.IsNullOrWhiteSpace(root)){string path=Path.Combine(root,"java.exe");if(File.Exists(path))candidates.Add(path);}}
        var home=Environment.GetEnvironmentVariable("JAVA_HOME");if(!string.IsNullOrWhiteSpace(home))Add(Path.Combine(home,"bin"));
        foreach(var path in (Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator))Add(path.Trim('"'));
        foreach(var vendor in new[]{"Java","Eclipse Adoptium","Microsoft","Amazon Corretto","Zulu"})
        {
            string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),vendor);
            if(!Directory.Exists(root))continue;
            foreach(var install in Directory.EnumerateDirectories(root))Add(Path.Combine(install,"bin"));
        }
        var found=new List<(string Path,string Version,int Major)>();
        foreach(var candidate in candidates)
        {
            try{string version=await ServerService.CheckJavaAsync(candidate);var match=Regex.Match(version,"version \\\"(\\d+)(?:\\.(\\d+))?");if(match.Success){int major=int.Parse(match.Groups[1].Value);if(major==1)major=int.Parse(match.Groups[2].Value);found.Add((candidate,version,major));}}
            catch(IOException){}catch(System.ComponentModel.Win32Exception){}
        }
        if(found.Count==0)throw new IOException("설치된 Java를 찾지 못했습니다. 찾아보기에서 java.exe를 선택하세요.");
        var best=found.OrderByDescending(x=>x.Major).First();return(best.Path,best.Version);
    }
    // Minecraft 공식 버전 메타데이터의 Java 요구 버전을 읽습니다. 최신 버전을 숫자로 추측하지 않습니다.
    public static async Task ValidateAsync(string executable, string game, DownloadClient net, CancellationToken ct)
    {
        string text = await ServerService.CheckJavaAsync(executable);
        var match = Regex.Match(text, "version \\\"(?<major>\\d+)(?:\\.(?<minor>\\d+))?");
        if (!match.Success) throw new IOException("Java 버전 출력을 해석할 수 없습니다: " + text);
        int major = int.Parse(match.Groups["major"].Value); if (major == 1) major = int.Parse(match.Groups["minor"].Value);
        var manifest = await net.Json("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json", ct);
        var version = manifest["versions"]!.AsArray().FirstOrDefault(x => x!["id"]!.ToString() == game) ?? throw new IOException("Minecraft 공식 버전 정보를 찾을 수 없습니다.");
        var details = await net.Json(version["url"]!.ToString(), ct); int required = (int?)details["javaVersion"]?["majorVersion"] ?? 8;
        if (major < required) throw new IOException($"Minecraft {game}은 Java {required} 이상이 필요합니다. 선택된 Java: {major}");
    }
}

