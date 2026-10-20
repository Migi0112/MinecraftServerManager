// [소스 안내] 공식 배포처 다운로드와 로더별 설치 어댑터입니다. Paper·Purpur·Fabric·Forge·NeoForge마다 버전 조회와 설치 절차를 구분합니다.
using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace NeoForgeServerManager;

// 온라인 통신을 한곳에 모읍니다. 공급자가 정한 연락처/식별 헤더를 사용하며 파일은 임시 이름으로 받은 뒤 확정합니다.
public sealed class DownloadClient : IDisposable
{
    private readonly HttpClient http;
    public DownloadClient(string contact = "", HttpMessageHandler? handler=null)
    {
        http=handler==null?new HttpClient():new HttpClient(handler);http.Timeout=TimeSpan.FromMinutes(15);
        if (!string.IsNullOrEmpty(contact) && !ApiIdentity.Valid(contact)) throw new ArgumentException("올바른 이메일 또는 프로젝트 URL을 입력하세요.");
        http.DefaultRequestHeaders.UserAgent.ParseAdd("MinecraftServerManager/2.1" + (contact.Length > 0 ? $" ({contact})" : ""));
    }
    public async Task<JsonNode> Json(string url, CancellationToken ct = default) => JsonNode.Parse(await http.GetStringAsync(url, ct)) ?? throw new IOException("빈 응답입니다.");
    public async Task<string> Text(string url, CancellationToken ct = default) => await http.GetStringAsync(url, ct);
    public async Task FileAsync(string url, string target, IProgress<string> log, CancellationToken ct, string? sha256 = null)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https") throw new IOException("HTTPS 다운로드만 허용합니다.");
        string temp = target + ".partial";
        try
        {
            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct); response.EnsureSuccessStatusCode();
            await using (var input = await response.Content.ReadAsStreamAsync(ct))
            await using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write))
            {
                byte[] buffer = new byte[81920]; long total = 0; int count; var last = DateTime.MinValue;
                while ((count = await input.ReadAsync(buffer, ct)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, count), ct); total += count;
                    if ((DateTime.UtcNow - last).TotalMilliseconds > 500) { log.Report($"다운로드 {total / 1048576.0:F1} MB / {(response.Content.Headers.ContentLength / 1048576.0)?.ToString("F1") ?? "?"} MB"); last = DateTime.UtcNow; }
                }
            }
            if (sha256 != null)
            { await using var stream = System.IO.File.OpenRead(temp); if (!Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).Equals(sha256, StringComparison.OrdinalIgnoreCase)) throw new IOException("파일 SHA256 검증 실패"); }
            System.IO.File.Move(temp, target, false);
        }
        finally { if (System.IO.File.Exists(temp)) System.IO.File.Delete(temp); }
    }
    public void Dispose() => http.Dispose();
}

// 서버 종류마다 다른 메타데이터와 설치 절차를 이 계약 뒤에 숨깁니다. 새 종류는 어댑터만 추가하면 됩니다.
public interface IInstallerAdapter
{
    Task<string[]> Games(CancellationToken ct);
    Task<string[]> Builds(string game, CancellationToken ct);
    Task Install(Profile p, IProgress<string> log, CancellationToken ct);
}
public static class InstallerFactory
{
    public static IInstallerAdapter Create(string kind, DownloadClient net) => kind switch
    {
        "Paper" => new PaperInstaller(net),
        "Purpur" => new PurpurInstaller(net),
        "Fabric" => new FabricInstaller(net),
        "Forge" => new ForgeInstaller(net),
        "NeoForge" => new NeoForgeInstaller(net),
        _ => throw new ArgumentException("알 수 없는 서버 종류")
    };
}
public sealed class PaperInstaller(DownloadClient net) : IInstallerAdapter
{
    const string Root = "https://fill.papermc.io/v3/projects/paper";
    public async Task<string[]> Games(CancellationToken ct) => (await net.Json(Root, ct))["versions"]!.AsObject().SelectMany(x => x.Value!.AsArray().Select(v => v!.ToString())).ToArray();
    public async Task<string[]> Builds(string game, CancellationToken ct) => (await net.Json($"{Root}/versions/{game}/builds", ct)).AsArray().Where(x => x!["channel"]!.ToString() == "STABLE").Select(x => x!["id"]!.ToString()).ToArray();
    public async Task Install(Profile p, IProgress<string> log, CancellationToken ct)
    {
        var build = (await net.Json($"{Root}/versions/{p.MinecraftVersion}/builds", ct)).AsArray().First(x => x!["id"]!.ToString() == p.LoaderVersion)!;
        var file = build["downloads"]!["server:default"]!;
        await net.FileAsync(file["url"]!.ToString(), Path.Combine(p.Folder, "server.jar"), log, ct, file["checksums"]?["sha256"]?.ToString()); p.LaunchJar = "server.jar";
    }
}
public sealed class PurpurInstaller(DownloadClient net) : IInstallerAdapter
{
    const string Root = "https://api.purpurmc.org/v2/purpur";
    public async Task<string[]> Games(CancellationToken ct) => (await net.Json(Root, ct))["versions"]!.AsArray().Select(x => x!.ToString()).Reverse().ToArray();
    public async Task<string[]> Builds(string game, CancellationToken ct) => (await net.Json($"{Root}/{game}", ct))["builds"]!["all"]!.AsArray().Select(x => x!.ToString()).Reverse().ToArray();
    public async Task Install(Profile p, IProgress<string> log, CancellationToken ct)
    { await net.FileAsync($"{Root}/{p.MinecraftVersion}/{p.LoaderVersion}/download", Path.Combine(p.Folder, "server.jar"), log, ct); p.LaunchJar = "server.jar"; }
}
public sealed class FabricInstaller(DownloadClient net) : IInstallerAdapter
{
    public bool IncludePrerelease {get;set;}
    const string Root = "https://meta.fabricmc.net/v2/versions";
    public async Task<string[]> Games(CancellationToken ct) => (await net.Json(Root + "/game", ct)).AsArray().Where(x => IncludePrerelease || (bool?)x!["stable"] == true).Select(x => x!["version"]!.ToString()).ToArray();
    public async Task<string[]> Builds(string game, CancellationToken ct) => (await net.Json(Root + "/loader/" + game, ct)).AsArray().Select(x => x!["loader"]!["version"]!.ToString()).ToArray();
    public async Task Install(Profile p, IProgress<string> log, CancellationToken ct)
    {
        string installer = (await net.Json(Root + "/installer", ct)).AsArray().First(x => (bool?)x!["stable"] == true)!["version"]!.ToString();
        await net.FileAsync($"{Root}/loader/{p.MinecraftVersion}/{p.LoaderVersion}/{installer}/server/jar", Path.Combine(p.Folder, "server.jar"), log, ct);
        p.LaunchJar = "server.jar"; log.Report("Fabric 런처 준비 완료. Minecraft 및 라이브러리는 첫 실행에서 내려받습니다.");
    }
}
public abstract class MavenInstaller(DownloadClient net) : IInstallerAdapter
{
    protected abstract string Root { get; }
    protected abstract string Artifact { get; }
    protected abstract string Game(string version);
    protected async Task<string[]> Versions(CancellationToken ct) => XDocument.Parse(await net.Text(Root + "/maven-metadata.xml", ct)).Descendants("version").Select(x => x.Value).ToArray();
    public virtual async Task<string[]> Games(CancellationToken ct) => (await Versions(ct)).Select(Game).Where(x => x != "").Distinct().Reverse().ToArray();
    public virtual async Task<string[]> Builds(string game, CancellationToken ct) => (await Versions(ct)).Where(v => Game(v) == game).Reverse().ToArray();
    public async Task Install(Profile p, IProgress<string> log, CancellationToken ct)
    {
        string file = Path.Combine(p.Folder, "installer.jar");
        await net.FileAsync($"{Root}/{p.LoaderVersion}/{Artifact}-{p.LoaderVersion}-installer.jar", file, log, ct);
        var info = new ProcessStartInfo(p.Java) { WorkingDirectory = p.Folder, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-jar", file, "--installServer" }) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new IOException("설치 프로그램 실행 실패");
        async Task Pump(StreamReader r) { while (await r.ReadLineAsync(ct) is { } line) log.Report(line); }
        try { await Task.WhenAll(Pump(process.StandardOutput), Pump(process.StandardError), process.WaitForExitAsync(ct)); }
        catch { if (!process.HasExited) process.Kill(true); await process.WaitForExitAsync(); throw; }
        if (process.ExitCode != 0) throw new IOException($"설치 실패: 종료 코드 {process.ExitCode}");
        string? args = Directory.EnumerateFiles(p.Folder, "win_args.txt", SearchOption.AllDirectories).FirstOrDefault();
        if (args != null) p.ArgumentFile = Path.GetRelativePath(p.Folder, args);
        else
        {
            string? jar = Directory.EnumerateFiles(p.Folder, "forge-*.jar").FirstOrDefault(x => !x.Contains("installer"));
            if (jar == null) throw new IOException("서버 실행 파일을 찾지 못했습니다. 설치 로그를 확인하세요.");
            p.LaunchJar = Path.GetFileName(jar);
        }
    }
}
public sealed class ForgeInstaller(DownloadClient net) : MavenInstaller(net)
{
    protected override string Root => "https://maven.minecraftforge.net/net/minecraftforge/forge";
    protected override string Artifact => "forge";
    protected override string Game(string version) => version.Split('-')[0];
}
public sealed class NeoForgeInstaller(DownloadClient net) : MavenInstaller(net)
{
    protected override string Root => "https://maven.neoforged.net/releases/net/neoforged/neoforge";
    protected override string Artifact => "neoforge";
    protected override string Game(string version) => MinecraftVersion(version);

    // 공식 규칙: 21.11.48 → 1.21.11, 26.1.0.10-beta → 26.1, 26.1.2.122 → 26.1.2.
    // snapshot/pre/rc 빌드를 정식 Minecraft 버전으로 잘못 묶지 않도록 별도 접미사는 제외합니다.
    public static string MinecraftVersion(string version)
    {
        var match = System.Text.RegularExpressions.Regex.Match(version, @"\A(?<core>\d+\.\d+\.\d+(?:\.\d+)?)(?:-beta)?\z");
        if (!match.Success || !Version.TryParse(match.Groups["core"].Value, out var v)) return "";
        if (v.Major is >= 20 and <= 21 && v.Revision < 0)
            return $"1.{v.Major}" + (v.Minor == 0 ? "" : $".{v.Minor}");
        if (v.Major >= 26 && v.Revision >= 0)
            return $"{v.Major}.{v.Minor}" + (v.Build == 0 ? "" : $".{v.Build}");
        return "";
    }

    // Maven XML은 게시 순서입니다. 이전 게임 버전의 패치가 뒤에 올라와도 최신 게임이 먼저 보이게 정렬합니다.
    public static string[] GameList(IEnumerable<string> versions) => versions.Select(MinecraftVersion)
        .Where(v => v.Length > 0).Distinct().OrderByDescending(v => Version.Parse(v)).ToArray();
    public static string[] BuildList(IEnumerable<string> versions, string game) => versions
        .Where(v => MinecraftVersion(v) == game).Distinct()
        .OrderByDescending(v => Version.Parse(v.Split('-')[0]))
        .ThenBy(v => v.EndsWith("-beta", StringComparison.Ordinal)).ToArray();
    public override async Task<string[]> Games(CancellationToken ct) => GameList(await Versions(ct));
    public override async Task<string[]> Builds(string game, CancellationToken ct) => BuildList(await Versions(ct), game);
}


