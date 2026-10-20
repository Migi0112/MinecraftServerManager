// [소스 안내] 콘솔 표시와 실행 중 화면 동작을 보완하는 MainForm의 일부입니다. 로그 필터·원문 보존과 컨트롤 배치를 처리합니다.
namespace NeoForgeServerManager;

public sealed partial class MainForm
{
    void AppendConsoleLines(IEnumerable<string> lines)
    {
        foreach(string line in lines)
        {
            int start=console.TextLength;console.SelectionStart=start;
            console.SelectionColor=line.Contains("ERROR",StringComparison.OrdinalIgnoreCase)?Color.FromArgb(255,91,83):line.Contains("WARN",StringComparison.OrdinalIgnoreCase)?Color.FromArgb(255,215,69):Color.WhiteSmoke;
            console.AppendText(line+Environment.NewLine);
            // 정상 로그는 INFO 표시만 초록색으로 강조하고 원래 텍스트는 보존합니다.
            int info=line.IndexOf("INFO",StringComparison.Ordinal);if(info>=0){console.Select(start+info,4);console.SelectionColor=Color.FromArgb(67,205,92);}
        }
        console.Select(console.TextLength,0);console.SelectionColor=Color.WhiteSmoke;
    }
    // 기존 이벤트를 가진 컨트롤을 이동하므로 화면을 바꿔도 명령 전송/저장 기능이 유지됩니다.
    void ArrangeConsole(TabPage page,FlowLayoutPanel tools,Button send)
    {
        var buttons=tools.Controls.OfType<WarmButton>().ToArray();
        var canvas=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(12,8,12,12)};
        canvas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));canvas.RowStyles.Add(new RowStyle(SizeType.Absolute,166));canvas.RowStyles.Add(new RowStyle(SizeType.Percent,100));canvas.RowStyles.Add(new RowStyle(SizeType.Absolute,64));
        var toolbar=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new Size(1236,166)};
        toolbar.Place(Ui.Heading("콘솔",24),4,0,360,48);
        string[] filters=["메인 로그","전체 로그","오류 로그"];
        for(int i=0;i<filters.Length;i++){var b=buttons.First(x=>x.Text==filters[i]);b.Font=new Font("Noto Sans KR",14,FontStyle.Bold);toolbar.Place(b,i*134,56,132,48);}
        var save=buttons.First(x=>x.Text=="로그 저장");save.IconName="file";toolbar.Place(save,696,56,138,48);
        var clear=buttons.First(x=>x.Text=="화면 지우기");clear.Text="콘솔 비우기";toolbar.Place(clear,846,56,154,48);
        toolbar.Place(autoScroll,1014,56,222,48);
        toolbar.Place(hideDimensionLogs,4,112,620,44);var filterHint=Ui.Note("전체 로그에서 확인");filterHint.TextAlign=ContentAlignment.MiddleRight;toolbar.Place(filterHint,900,112,330,44);
        console.Parent?.Controls.Remove(console);command.Parent?.Controls.Remove(command);send.Parent?.Controls.Remove(send);
        var entry=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(0,10,0,0)};entry.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));entry.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,168));
        command.Margin=new Padding(0,0,10,0);command.Dock=DockStyle.Fill;command.PlaceholderText="명령어를 입력하세요… (예: /time set day)";
        send.Text="전송";send.Dock=DockStyle.Fill;send.Margin=Padding.Empty;send.AutoSize=false;
        entry.Controls.Add(command,0,0);entry.Controls.Add(send,1,0);canvas.Controls.Add(toolbar,0,0);canvas.Controls.Add(console,0,1);canvas.Controls.Add(entry,0,2);
        foreach(Control old in page.Controls.Cast<Control>().ToArray())old.Dispose();page.Controls.Add(canvas);
    }

    public bool HomeHosted { get; set; }
    public bool IsRunning => server.Running;
    public bool IsBusy => busy;
    public string RuntimeState => server.State;
    public int PlayerCount => server.Players.Count;
    public Profile ManagedProfile => current;
    public void InitializeForHome(Profile p) { HomeHosted = true; _ = Handle; saved = Storage.Load(); int index = saved.FindIndex(x => x.Id == p.Id); if (index >= 0) ReloadProfiles(index); else Apply(p); timer.Start(); }
    public Task StartFromHome() => RunAsync(StartAsync);
    public Task StopFromHome() => RunAsync(async () => { restartAt = DateTime.MaxValue; await ClearBackupNotice(); await server.StopAsync(); });
    public Task RestartFromHome() => RunAsync(async () => { await ClearBackupNotice(); await server.StopAsync(); await StartAsync(); });
    public async Task ShutdownForHome() { if (busy) throw new IOException("진행 중인 작업을 마친 뒤 종료하세요."); closing = true; restartAt = DateTime.MaxValue; await ClearBackupNotice(); if (server.Running) await server.StopAsync(); if (server.Running) throw new IOException("서버 종료를 확인하지 못했습니다."); allowClose = true; Close(); }
    readonly WarmToggle backupChat = new() { Text = "채팅 공지 (60초 전)", AutoSize = true, Checked = true }, backupBossbar = new() { Text = "화면 상단 카운트다운", AutoSize = true, Checked = true };
    CancellationTokenSource? backupCancel; bool barVisible; int lastNotice = -1;
    string BarId => "migi:backup_" + new string(current.Id.Where(char.IsAsciiLetterOrDigit).ToArray()).ToLowerInvariant();
    async Task ClearBackupNotice() { backupCancel?.Dispose(); backupCancel = null; if (barVisible && server.Online) await server.SendAsync("bossbar remove " + BarId); barVisible = false; lastNotice = -1; }
    async Task UpdateBackupNotice(DateTime now)
    {
        if (!autoBackup.Checked || !server.Online) { if (barVisible) await ClearBackupNotice(); return; }
        int remaining = (int)Math.Ceiling((nextBackup - now).TotalSeconds); if (remaining > 60 || remaining < 0) return;
        if (backupCancel?.IsCancellationRequested == true) { nextBackup = now.AddMinutes((double)interval.Value); await ClearBackupNotice(); return; }
        backupCancel ??= new(); if (remaining == lastNotice) return; lastNotice = remaining;
        string text = $"자동 백업까지 {remaining}초 · 저장 후 재시작합니다";
        if (backupChat.Checked && (remaining is 60 or 30 or 10 || remaining is >= 1 and <= 5)) await server.SendAsync($"say [서버 알림] {remaining}초 후 백업을 위해 월드를 저장하고 서버를 재시작합니다. 재시작 후 다시 접속해 주세요.");
        if (backupBossbar.Checked)
        {
            string title = System.Text.Json.JsonSerializer.Serialize(text); if (!barVisible) { await server.SendAsync($"bossbar add {BarId} {title}"); await server.SendAsync($"bossbar set {BarId} max 60"); await server.SendAsync($"bossbar set {BarId} color yellow"); barVisible = true; }
            await server.SendAsync($"bossbar set {BarId} name {title}"); await server.SendAsync($"bossbar set {BarId} value {remaining}"); await server.SendAsync($"bossbar set {BarId} players @a");
        }
    }
    readonly Queue<string> fullLogs = new(); bool showFullLog, errorsOnly; readonly WarmToggle autoScroll = new() { Text="실시간 자동 스크롤", Checked=true, AutoSize=true };
    readonly WarmToggle hideDimensionLogs=new(){Text="반복 차원 로그 숨기기",Checked=true,AutoSize=false};
    bool ShowConsoleLine(string line) => errorsOnly ? IsErrorLog(line) : showFullLog || !IsStatusLog(line) || (!hideDimensionLogs.Checked&&ConsoleLogFilter.IsDimensionRoutine(line));
    static bool IsErrorLog(string line) => line.Contains("ERROR", StringComparison.OrdinalIgnoreCase) || line.Contains("WARN", StringComparison.OrdinalIgnoreCase) || line.Contains("Exception", StringComparison.OrdinalIgnoreCase) || line.Contains("FATAL", StringComparison.OrdinalIgnoreCase); readonly RichTextBox statusLogs = new() { Dock = DockStyle.Fill, ReadOnly = true, Font = new Font("Consolas", 10) };
    public static bool IsStatusLog(string line)
    {
        if (line.Contains("WARN", StringComparison.OrdinalIgnoreCase) || line.Contains("ERROR", StringComparison.OrdinalIgnoreCase) || line.Contains("Exception", StringComparison.OrdinalIgnoreCase) || line.Contains("FATAL", StringComparison.OrdinalIgnoreCase)) return false;
        return ConsoleLogFilter.IsDimensionRoutine(line)||System.Text.RegularExpressions.Regex.IsMatch(line, @"There are \d+ of a max of \d+ players online|Mean tick time:|Overall:.*TPS|The Nether:.*TPS|The End:.*TPS|Overworld:.*TPS|RecipeTrie of depth|bossbar.*migi:backup_|custom bossbar", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
    void RememberLog(string line) { fullLogs.Enqueue(line); while (fullLogs.Count > 3000) fullLogs.Dequeue(); if (IsStatusLog(line)) { statusLogs.AppendText(line + Environment.NewLine); if (statusLogs.TextLength > 180000) { statusLogs.Select(0, 80000); statusLogs.SelectedText = ""; } } }
    void InstallConsoleFilters(FlowLayoutPanel tools)
    {
        hideDimensionLogs.CheckedChanged+=(_,_)=>{console.Clear();AppendConsoleLines(fullLogs.Where(ShowConsoleLine));};
        var filters = new List<WarmButton>();
        void Mode(string text, bool all, bool errors)
        {
            var button = (WarmButton)Button(text, () => { showFullLog=all; errorsOnly=errors; console.Clear();AppendConsoleLines(fullLogs.Where(ShowConsoleLine)); foreach(var f in filters){f.Primary=f.Text==text;f.Invalidate();} });
            button.Primary = !all && !errors; filters.Add(button); tools.Controls.Add(button); tools.Controls.SetChildIndex(button, filters.Count-1);
        }
        Mode("메인 로그",false,false); Mode("전체 로그",true,false); Mode("오류 로그",false,true);
        tools.Controls.Add(autoScroll);
        tools.Controls.Add(Button("상태 조회 로그", () => { using var window = new WarmWindow { Text = "상태 조회 로그", Size = new Size(1000, 700) }; var copy = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, Font = new Font("Consolas", 10), Text = statusLogs.Text }; window.Controls.Add(copy); WarmTheme.Apply(window); window.ShowDialog(this); }));

    }
}
public static class BackupPaths
{
    public static string ForServer(Profile profile)
    {
        string name = new string(profile.Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.'); if (name.Length == 0) name = "서버"; if (name.Length > 70) name = name[..70]; if (System.Text.RegularExpressions.Regex.IsMatch(name, @"^(CON|PRN|AUX|NUL|COM\d|LPT\d)(\.|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) name = "서버_" + name;
        string root = BackupService.ValidateDestination(profile.Folder, profile.BackupFolder); string path = Path.Combine(root, name); string marker = Path.Combine(path, ".server-id"); if (Directory.Exists(path) && (!File.Exists(marker) || File.ReadAllText(marker) != profile.Id)) path += "-" + new string(profile.Id.Where(char.IsAsciiLetterOrDigit).Take(12).ToArray()); BackupService.ValidateDestination(profile.Folder, path); Directory.CreateDirectory(path); Storage.AtomicWrite(Path.Combine(path, ".server-id"), profile.Id); return path;
    }
}
