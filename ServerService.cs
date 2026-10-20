// [소스 안내] 화면과 분리된 서버 실행 서비스입니다. Java 프로세스, 콘솔 입출력, 플레이어/TPS 상태와 정상·강제 종료를 관리합니다.
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NeoForgeServerManager;

// 서버 실행 전용 서비스(ServerRunner 역할): UI와 분리하여 프로세스, 콘솔 입출력, 종료를 담당합니다.
public sealed class ServerService
{
    private Process? shell, java;
    private FileStream? folderLock;
    private Task lifetime = Task.CompletedTask;
    private bool requestedStop;
    private readonly SystemMetricsService systemMetrics = new();
    private TimeSpan lastCpu;
    private DateTime lastSample;
    public ConcurrentQueue<string> Output { get; } = new();
    public event Action<bool, int>? Exited;
    public bool Running => shell != null;
    public bool Online { get; private set; }
    public string State { get; private set; } = "오프라인";
    public HashSet<string> Players { get; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTime PlayerSample { get; private set; }
    public DateTime TpsSample { get; private set; }
    public string Tps { get; private set; } = "확인 불가";
    public string Metrics { get; private set; } = "RAM —  |  CPU —";
    public Profile? Active { get; private set; }
    public int? LastJavaExitCode { get; private set; }
    public void Log(string text) => Output.Enqueue("[매니저] " + text);

    // [동작 설명] 기존 JVM 옵션 중 Xms/Xmx만 바꿉니다. 다른 실행 옵션과 주석을 보존하고 해석이 모호하면 중단합니다.
    public static string UpdateHeapArguments(string text, int min, int max)
    {
        if (min < 1 || max < min) throw new ArgumentException("Xms는 1GB 이상, Xmx는 Xms 이상이어야 합니다.");
        // Preserve comments and all non-heap arguments, including multiple arguments per line.
        var lines = text.Replace("\r\n", "\n").Split('\n').Select(line =>
        {
            int comment = line.IndexOf('#');
            string code = comment < 0 ? line : line[..comment];
            string tail = comment < 0 ? "" : line[comment..];
            code = Regex.Replace(code, @"(?<!\S)-Xm[sx](?:\s+)?\d+[kKmMgG]?(?!\S)", "");
            if (Regex.IsMatch(code, @"-Xm[sx]|-XX:(?:Initial|Max)HeapSize"))
                throw new InvalidOperationException("기존 JVM 메모리 옵션을 안전하게 해석할 수 없습니다. user_jvm_args.txt의 특수 메모리 옵션을 먼저 정리하세요.");
            return code.TrimEnd() + (tail.Length > 0 && code.Trim().Length > 0 ? " " : "") + tail;
        });
        return string.Join(Environment.NewLine, lines).TrimEnd() + $"{Environment.NewLine}-Xms{min}G{Environment.NewLine}-Xmx{max}G{Environment.NewLine}";
    }

    // 서버 폴더 잠금을 확보한 뒤 RAM을 반영합니다. 새 설치는 Java 직접 실행, 기존 등록은 run.bat 호환 실행입니다.
    // [동작 설명] 폴더/월드 잠금과 실행 경로를 확인한 뒤 Java 또는 run.bat을 시작합니다. 사용자 JVM 파일 변경 전에는 백업을 만듭니다.
    public void Start(Profile profile)
    {
        if (Running) throw new InvalidOperationException("서버가 이미 실행 중입니다.");
        string folder = Path.GetFullPath(profile.Folder);
        if (!profile.ManagedInstall && !File.Exists(Path.Combine(folder, "run.bat"))) throw new FileNotFoundException("선택한 폴더에 run.bat이 없습니다.");
        var p = new Profile
        {
            Name = profile.Name,
            Folder = folder,
            Java = profile.Java,
            MinRam = profile.MinRam,
            MaxRam = profile.MaxRam,
            TpsCommand = profile.TpsCommand,
            ManagedInstall = profile.ManagedInstall
        };
        try
        {
            folderLock = new FileStream(Path.Combine(folder, ".manager.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            WorldImportService.EnsureNoPending(folder);
            // An external server normally holds this file for the lifetime of its world.
            string world = PropertiesFile.ReadValue(Path.Combine(folder, "server.properties"), "level-name", "world");
            string worldLock = Path.Combine(folder, world, "session.lock");
            if (File.Exists(worldLock))
                using (new FileStream(worldLock, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            string argsFile = Path.Combine(folder, "user_jvm_args.txt");
            string original = File.Exists(argsFile) ? File.ReadAllText(argsFile) : "";
            string updated = UpdateHeapArguments(original, p.MinRam, p.MaxRam);
            if (File.Exists(argsFile)) File.Copy(argsFile, argsFile + ".manager.bak", true);
            Storage.AtomicWrite(argsFile, updated);
            var info = new ProcessStartInfo
            {
                FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
                Arguments = "/d /q /c \"chcp 65001 >nul & call run.bat nogui\"",
                WorkingDirectory = folder,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                StandardInputEncoding = new UTF8Encoding(false)
            };
            if (p.Java != "java.exe" && !string.IsNullOrWhiteSpace(p.Java))
            {
                string exe = Path.GetFullPath(p.Java);
                if (!File.Exists(exe) || !Path.GetFileName(exe).Equals("java.exe", StringComparison.OrdinalIgnoreCase))
                    throw new FileNotFoundException("Java 경로는 실제 java.exe를 지정하세요.");
                string bin = Path.GetDirectoryName(exe)!;
                info.Environment["PATH"] = bin + ";" + Environment.GetEnvironmentVariable("PATH");
                info.Environment["JAVA_HOME"] = Path.GetDirectoryName(bin)!;
            }
            // Applied by the JVM even when run.bat invokes Java using an absolute path.
            info.Environment.TryGetValue("JAVA_TOOL_OPTIONS", out string? existingJavaOptions);
            info.Environment["JAVA_TOOL_OPTIONS"] = (existingJavaOptions ?? "") + " -Dfile.encoding=UTF-8 -Dstdout.encoding=UTF-8 -Dstderr.encoding=UTF-8";
            // 새로 설치한 서버는 배치 셸을 거치지 않고 Java를 직접 실행합니다. 경로/인수는 별도로 전달합니다.
            if (profile.ManagedInstall)
            {
                info.FileName = p.Java; info.Arguments = "";
                if (!string.IsNullOrEmpty(profile.ArgumentFile)) info.ArgumentList.Add("@user_jvm_args.txt");
                else { info.ArgumentList.Add($"-Xms{p.MinRam}G"); info.ArgumentList.Add($"-Xmx{p.MaxRam}G"); }
                if (!string.IsNullOrEmpty(profile.ArgumentFile)) info.ArgumentList.Add("@" + profile.ArgumentFile);
                else { info.ArgumentList.Add("-jar"); info.ArgumentList.Add(profile.LaunchJar); }
                info.ArgumentList.Add("nogui");
            }
            shell = Process.Start(info) ?? throw new InvalidOperationException("실행에 실패했습니다.");
            Active = p;
            Online = false; requestedStop = false; LastJavaExitCode = null; Players.Clear(); Tps = "확인 불가";
            PlayerSample = TpsSample = DateTime.MinValue;
            State = "시작 중"; Metrics = "RAM —  |  CPU —";
            Log($"{p.Name} 실행: Xms {p.MinRam}GB / Xmx {p.MaxRam}GB");
            lifetime = MonitorAsync(shell);
        }
        catch { shell?.Dispose(); shell = null; folderLock?.Dispose(); folderLock = null; throw; }
    }

    // [동작 설명] 서버 출력 스트림을 비동기로 읽습니다. 화면 스레드를 기다리게 하지 않고 로그 파싱과 전달로 연결합니다.
    private async Task Pump(StreamReader reader)
    {
        try { while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line) Output.Enqueue(line); }
        catch (IOException e) { Log("출력 스트림: " + e.Message); }
    }
    private bool pIsDirect() => Active?.ManagedInstall == true;
    // 표준 출력과 오류를 동시에 읽어 버퍼 교착을 막고 Java 프로세스의 상태와 사용량을 감시합니다.
    private async Task MonitorAsync(Process process)
    {
        var stdout = Pump(process.StandardOutput);
        var stderr = Pump(process.StandardError);
        int ticks = 0;
        try
        {
            while (!process.HasExited)
            {
                if (java == null)
                {
                    java = Active?.ManagedInstall == true ? Process.GetProcessById(process.Id) : ProcessTree.FindJava(process.Id);
                    if (java != null) { _ = java.Handle; lastCpu = java.TotalProcessorTime; lastSample = DateTime.UtcNow; }
                    // Standard generated run.bat may pause after a failed Java invocation.
                    else if (++ticks % 20 == 0) await process.StandardInput.WriteLineAsync("");
                }
                else if (java.HasExited && !pIsDirect())
                {
                    LastJavaExitCode = TryExitCode(java);
                    Online = false;
                    // Consume run.bat's final PAUSE without changing the user's batch file.
                    await process.StandardInput.WriteLineAsync("");
                    await process.StandardInput.FlushAsync();
                }
                else
                {
                    Metrics = systemMetrics.Sample(java);
                }
                await Task.Delay(500);
            }
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Log("프로세스 감시: " + e.Message);
            await process.WaitForExitAsync();
        }
        await Task.WhenAll(stdout, stderr);
        if (java is { HasExited: true }) LastJavaExitCode = TryExitCode(java);
        int code = LastJavaExitCode ?? process.ExitCode;
        bool unexpected = !requestedStop;
        java?.Dispose(); java = null;
        shell = null; process.Dispose(); folderLock?.Dispose(); folderLock = null;
        Online = false; Players.Clear(); Metrics = "RAM —  |  CPU —"; Tps = "확인 불가";
        State = unexpected ? "예기치 않은 종료" : "오프라인";
        Log($"서버 종료 ({(LastJavaExitCode.HasValue ? "Java" : "배치")} 종료 코드 {code}, {(unexpected ? "예기치 않은 종료 / 크래시 가능" : "정상 종료 요청")})");
        Exited?.Invoke(unexpected, code);
    }
    private static int? TryExitCode(Process process)
    {
        try { return process.ExitCode; }
        catch (InvalidOperationException) { return null; }
        catch (System.ComponentModel.Win32Exception) { return null; }
    }
    // [동작 설명] 실행 중인 서버의 표준 입력으로 명령 한 줄을 보냅니다.
    public async Task SendAsync(string command)
    {
        if (shell == null || shell.HasExited) throw new InvalidOperationException("서버가 실행 중이 아닙니다.");
        command = command.Trim().TrimStart('/');
        if (command.Length == 0 || command.Contains('\n') || command.Contains('\r')) throw new ArgumentException("명령어는 한 줄로 입력하세요.");
        if (command.Equals("stop", StringComparison.OrdinalIgnoreCase)) { requestedStop = true; State = "종료 중"; Online = false; }
        await shell.StandardInput.WriteLineAsync(command);
        await shell.StandardInput.FlushAsync();
    }
    // stop은 월드를 저장하는 정상 종료입니다. 시간 초과 시 자동 강제 종료하지 않습니다.
    // [동작 설명] 서버에 정상 종료 명령을 보내는 경로입니다. 강제 프로세스 종료와 구분합니다.
    public async Task StopAsync()
    {
        if (!Running) return;
        await SendAsync("stop");
        try { await lifetime.WaitAsync(TimeSpan.FromMinutes(2)); }
        catch (TimeoutException) { throw new TimeoutException("2분 안에 종료되지 않았습니다. 저장이 끝날 때까지 기다리거나 강제 종료 버튼을 사용하세요. 백업/재시작은 취소됩니다."); }
        if (LastJavaExitCode is not null and not 0) throw new IOException("Java가 오류 코드로 종료되었습니다. 백업/재시작을 취소합니다. 서버 로그를 확인하세요.");
    }
    // [동작 설명] 정상 종료가 아닌 강제 종료 경로입니다. 실행 중 데이터 저장 여부를 보장할 수 없으므로 호출 위치를 주의합니다.
    public void ForceStop()
    {
        requestedStop = true; Online = false; State = "강제 종료 중";
        shell?.Kill(true);
    }
    // 서버 로그에서 시작 완료, 접속자, TPS를 추출합니다. 읽지 못한 수치는 추측해서 표시하지 않습니다.
    // [동작 설명] 출력 한 줄에서 서버 준비 상태, 접속/퇴장 및 상태 응답을 읽어 Players·TPS 등 모델을 갱신합니다.
    public void Parse(string line)
    {
        if (!Running) return;
        line = Regex.Replace(line, @"\x1B\[[0-?]*[ -/]*[@-~]|§.", "");
        if (!requestedStop && line.Contains("Done (") && line.Contains("For help")) { Online = true; State = "온라인"; }
        var players = Regex.Match(line, @"There are (\d+) of a max of \d+ players online:\s*(.*)");
        if (players.Success)
        {
            Players.Clear();
            foreach (string name in players.Groups[2].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) Players.Add(name);
            PlayerSample = DateTime.UtcNow;
        }
        var join = Regex.Match(line, @"\b([A-Za-z0-9_]{1,16}) joined the game");
        var leave = Regex.Match(line, @"\b([A-Za-z0-9_]{1,16}) left the game");
        if (join.Success) Players.Add(join.Groups[1].Value);
        if (leave.Success) Players.Remove(leave.Groups[1].Value);
        var paperTps = Regex.Match(line, @"TPS from last.*?:\s*\*?([\d.]+)");
        if (paperTps.Success) { Tps = paperTps.Groups[1].Value; TpsSample = DateTime.UtcNow; }
        if (line.Contains("Overall", StringComparison.OrdinalIgnoreCase))
        {
            var tps = Regex.Match(line, @"(?:Mean TPS:|TPS:)\s*([\d.,]+)", RegexOptions.IgnoreCase);
            if (!tps.Success) tps = Regex.Match(line, @"([\d.,]+)\s+TPS", RegexOptions.IgnoreCase);
            if (tps.Success && double.TryParse(tps.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            { Tps = value.ToString("F2", CultureInfo.InvariantCulture); TpsSample = DateTime.UtcNow; }
        }
    }
    public static async Task<string> CheckJavaAsync(string executable)
    {
        using var p = Process.Start(new ProcessStartInfo(executable, "-version")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true }) ?? throw new IOException("Java 실행 실패");
        var error = p.StandardError.ReadToEndAsync();
        var output = p.StandardOutput.ReadToEndAsync();
        try { await p.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
        catch (TimeoutException) { p.Kill(true); throw; }
        string result = (await error) + (await output);
        if (p.ExitCode != 0) throw new IOException("Java 버전 확인 실패: " + result);
        return result;
    }
}




