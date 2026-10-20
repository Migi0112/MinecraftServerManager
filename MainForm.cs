// [소스 안내] 서버 한 대를 관리하는 화면의 중심입니다. 실행/종료, 명령 전송, 설정 저장, 백업과 상태 갱신을 각 서비스에 연결합니다.
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NeoForgeServerManager;

public sealed partial class MainForm : WarmWindow
{
    private readonly ServerService server = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 500 };
    private readonly WarmCombo profiles = new() { Width = 230, DropDownStyle = ComboBoxStyle.DropDownList };
    private List<Profile> saved = [];
    private readonly WarmTextBox profileName = new() { Width = 200 };
    private readonly WarmTextBox folder = new() { Width = 620 };
    private readonly WarmTextBox java = new() { Width = 460, Text = "java.exe" };
    private readonly WarmTextBox backupFolder = new() { Width = 600 };
    private readonly WarmTextBox tpsCommand = new() { Width = 220, Text = "neoforge tps" };
    private readonly RamCombo xms = new(4), xmx = new(8);
    readonly WarmNumber restartDelay = Number(5,3600,30), restartLimit = Number(1,20,3);
    private readonly WarmNumber interval = Number(5, 10080, 60);
    private readonly WarmNumber backupKeep = Number(0, 10000, 0);
    private Profile current = new();
    private TabPage? extensionPage;
    private string ContentFolder => current.Kind is "Paper" or "Purpur" ? "plugins" : "mods";
    private readonly WarmToggle autoRestart = new() { Text = "크래시 후 자동 재시작", AutoSize = true };
    private readonly WarmToggle autoBackup = new() { Text = "자동 백업 (정상 종료 → 압축 → 재실행)", AutoSize = true };
    private readonly Label status = new ServerStateBadge { AutoSize = true, Font = new Font("Noto Sans KR", 12) };
    private readonly Label metrics = new() { AutoSize = true };

    private readonly Label backupStatus = new() { AutoSize = true, Text = "백업 대기" };
    private readonly RichTextBox console = new() { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.FromArgb(23, 28, 36), ForeColor = Color.FromArgb(220, 231, 240), Font = new Font("Consolas", 10), WordWrap = false };
    private readonly WarmTextBox command = new() { Dock = DockStyle.Fill };
    private readonly ListBox players = new() { Dock = DockStyle.Fill };
    private readonly ListBox whitelist = new() { Dock = DockStyle.Fill };
    private readonly WarmTextBox playerName = new() { Width = 180 };
    private readonly ListBox mods = new() { Dock = DockStyle.Fill, DisplayMember = "Name" };
    private readonly DataGridView properties = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false };
    private readonly PropertiesFile propertyFile = new();
    private readonly List<Control> offlineOnly = [];
    private Button start = null!, stop = null!, restart = null!, backup = null!;
    private bool busy, tickBusy, closing, allowClose, loading;
    private int restartAttempts;
    private DateTime nextPoll, nextBackup, restartAt = DateTime.MaxValue;
    private DateTime onlineSince = DateTime.MaxValue;
    private string port = "25565", address = "모든 인터페이스", loadedFolder = "";

    public MainForm(bool loadProfiles = true, Profile? initial = null)
    {
        Scenic=true;Text = "Minecraft Server Manager · 서버 관리"; StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1536, 1024); MinimumSize = new Size(1280, 900);
        Font = new Font("Noto Sans KR", 9); AutoScaleMode = AutoScaleMode.Dpi;
        BuildUI(); BuildDashboard(); WarmTheme.Apply(this); ApplyConsoleTheme(); FormClosed += (_, _) => { timer.Dispose(); metrics.Dispose(); statusLogs.Dispose(); foreach (var icon in modIcons) icon.Dispose(); foreach (var face in faces.Values) face.Dispose(); };
        server.Exited += OnExited;
        timer.Tick += async (_, _) => await TickAsync();
        FormClosing += ClosingAsync;
        Shown += (_, _) =>
        {
            if (!loadProfiles || HomeHosted) return;
            try { saved = Storage.Load(); }
            catch (Exception e) { ShowError(e); }
            if (saved.Count == 0) saved.Add(new Profile());
            ReloadProfiles(initial == null ? 0 : Math.Max(0, saved.FindIndex(p => p.Id == initial.Id))); timer.Start();
        };
    }
    private static WarmNumber Number(int min, int max, int value) => new() { Minimum = min, Maximum = max, Value = value, Width = 80 };
    private static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true, Padding = new Padding(4), Margin = new Padding(0) };
        row.Controls.AddRange(controls); return row;
    }
    private static Label Label(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(6, 7, 5, 3) };
    private Button Button(string text, Func<Task> action, bool offline = false)
    {
        var b = new WarmButton { Text = text, AutoSize = true, MinimumSize = new Size(90, 30) };
        b.Click += async (_, _) => await RunAsync(action);
        if (offline) offlineOnly.Add(b);
        return b;
    }
    private Button Button(string text, Action action, bool offline = false) => Button(text, () => { action(); return Task.CompletedTask; }, offline);
    private static TabPage Page(TabControl tabs, string title, Control body, params Control[] top)
    {
        var page = new TabPage(title) { Padding = new Padding(8) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(Row(top), 0, 0); layout.Controls.Add(body, 0, 1); page.Controls.Add(layout); tabs.TabPages.Add(page); return page;
    }
    // 관리 화면의 탭과 버튼을 코드로 배치합니다. 버튼 작업은 RunAsync를 통해 중복 실행을 막습니다.
    // [동작 설명] 관리 화면의 탭과 기본 컨트롤을 만들고 버튼 이벤트를 연결합니다.
    private void BuildUI()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1, Padding = new Padding(12) };
        for (int i = 0; i < 4; i++) root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); Controls.Add(root);
        root.Controls.Add(Row(Button("설정 저장", SaveProfile, true), Button("서버 삭제", () => DeleteStoredProfile(this), true)), 0, 0);
        root.Controls.Add(Row(Label("서버 폴더"), folder, Button("찾아보기", () =>
        {
            using var dialog = new FolderBrowserDialog { Description = "run.bat이 있는 NeoForge 서버 폴더", UseDescriptionForTitle = true };
            if (dialog.ShowDialog(this) == DialogResult.OK) { folder.Text = dialog.SelectedPath; RefreshFiles(); }
        }, true), Button("폴더 열기", () => OpenFolder(Folder()))), 0, 1);
        start = Button("▶ 시작", async () => { restartAttempts = 0; restartAt = DateTime.MaxValue; await StartAsync(); });
        stop = Button("■ 정상 종료", async () => { if(WarmDialog.ConfirmStop(this,current,server.Players.Count)!=DialogResult.Yes)return; restartAt = DateTime.MaxValue; await ClearBackupNotice(); await server.StopAsync(); });
        restart = Button("↻ 재시작", async () => { restartAt = DateTime.MaxValue; await ClearBackupNotice(); await server.StopAsync(); await StartAsync(); });
        var force = Button("강제 종료", () =>
        {
            restartAt = DateTime.MaxValue;
            if (server.Running && WarmDialog.Show(this, "저장 중인 월드가 손상될 수 있습니다. 강제로 종료할까요?", "강제 종료", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) server.ForceStop();
        });
        root.Controls.Add(Row(Label("최소 RAM (GB)"), xms, Label("최대 RAM (GB)"), xmx, start, stop, restart, force), 0, 2);
        root.Controls.Add(Row(status, metrics), 0, 3);
        var tabs = new PageDeck { Dock = DockStyle.Fill }; root.Controls.Add(tabs, 0, 4);

        var consolePanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        consolePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); consolePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        consolePanel.Controls.Add(console, 0, 0);
        var input = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
        input.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); input.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        input.Controls.Add(command, 0, 0); input.Controls.Add(Button("명령 전송", SendCommandAsync), 1, 0); consolePanel.Controls.Add(input, 0, 1);
        command.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await RunAsync(SendCommandAsync); } };
        Page(tabs, "콘솔", consolePanel, Button("화면 지우기", () => console.Clear()), Button("로그 저장", () =>
        {
            using var d = new SaveFileDialog { Filter = "텍스트|*.txt", FileName = "server-console.txt" };
            if (d.ShowDialog(this) == DialogResult.OK) File.WriteAllText(d.FileName, console.Text);
        }), Button("crash-reports 열기", () => OpenFolder(Path.Combine(Folder(), "crash-reports"))), Label("콘솔 표시: 최근 약 200,000자 / 전체 서버 로그: logs/latest.log"));

        Page(tabs, "접속자", players, Button("접속자 갱신", () => server.SendAsync("list")), Label("15초마다 자동 갱신 · 온라인 상태는 서버 시작 완료 로그 기준"));
        properties.Columns.Add(new DataGridViewTextBoxColumn { Name = "Key", HeaderText = "설정", ReadOnly = true, FillWeight = 38 });
        properties.Columns.Add(new DataGridViewTextBoxColumn { Name = "Value", HeaderText = "값 (Java 이스케이프 형식)", FillWeight = 62 });
        Page(tabs, "server.properties", properties, Button("다시 읽기", LoadProperties, true), Button("설정 저장", SaveProperties, true), Label("서버 정지 중 편집 · 다음 시작 시 적용 · 주석 보존 / .manager.bak 저장"));
        Page(tabs, "화이트리스트", whitelist, playerName, Button("추가", () => WhitelistAsync("add")), Button("삭제", () => WhitelistAsync("remove")), Button("사용", () => server.SendAsync("whitelist on")), Button("해제", () => server.SendAsync("whitelist off")), Button("목록 갱신", LoadWhitelist), Label("추가/삭제는 온라인 상태에서 서버 명령으로 처리"));
        whitelist.SelectedIndexChanged += (_, _) => { if (whitelist.SelectedItem is string s) playerName.Text = s.Split(' ')[0]; };
        extensionPage = Page(tabs, "Mods", mods, Button("목록 갱신", LoadMods), Button("JAR 추가", AddMods, true), Button("선택 JAR 제거", RemoveMod, true), Button("파일 폴더 열기", () => OpenFolder(Path.Combine(Folder(), ContentFolder))), Label("추가·삭제는 서버 정지 중에만 가능 · 삭제 파일은 mods-disabled로 이동"));

        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        options.Controls.Add(Row(Label("Java 실행 파일"), java, Button("찾아보기", () =>
        {
            using var d = new OpenFileDialog { Filter = "Java|java.exe" };
            if (d.ShowDialog(this) == DialogResult.OK) java.Text = d.FileName;
        }, true), Button("Java 버전 확인", async () => WarmDialog.Show(this, await ServerService.CheckJavaAsync(java.Text.Trim()), "Java 버전"))));
        options.Controls.Add(Label("Minecraft/로더 요구사항에 맞는 Java를 선택하세요. 새 설치 서버는 선택한 Java를 직접 실행합니다."));
        options.Controls.Add(Row(Label("재시작 대기 (초)"), restartDelay, Label("최대 재시도"),restartLimit));
        options.Controls.Add(Row(autoRestart, Button("재시작 예약 취소", () => { restartAt = DateTime.MaxValue; server.Log("자동 재시작 예약을 취소했습니다."); })));
        options.Controls.Add(Row(Label("TPS 명령"), tpsCommand, Label("기본: neoforge tps · 빈칸이면 자동 조회 안 함")));
        options.Controls.Add(Label("CPU: 전체 논리 코어 기준 0~100% / RAM: Java 프로세스 작업 집합(힙 외 메모리 포함)"));
        options.Controls.Add(Label("변경 사항은 ‘설정 저장’을 눌러 보관하세요. 서버 폴더를 바꾸면 ‘파일 목록 새로고침’을 누르세요."));
        options.Controls.Add(Button("파일 목록 새로고침", RefreshFiles, true));
        options.Controls.Add(Button("설정 저장 위치 열기", () => OpenFolder(Storage.Root)));
        AddAdvancedTabs(tabs);
        Page(tabs, "실행 옵션", options, Label("Java / 자동 재시작 / 모니터링"));
        var backupPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        backupPanel.Controls.Add(Row(Label("백업 위치"), backupFolder, Button("선택", () =>
        {
            using var d = new FolderBrowserDialog(); if (d.ShowDialog(this) == DialogResult.OK) backupFolder.Text = d.SelectedPath;
        }, true)));
        backupPanel.Controls.Add(Row(autoBackup, Label("간격 (분)"), interval));
        backupPanel.Controls.Add(Row(backupChat, backupBossbar, Button("백업 설정 저장", SaveProfile), Button("예약 공지 취소", () => backupCancel?.Cancel())));
        backup = Button("지금 백업", () => BackupAsync(false));
        backupPanel.Controls.Add(Row(backup, Button("백업 폴더 열기", () => OpenFolder(backupFolder.Text)), backupStatus));
        backupPanel.Controls.Add(Label("실행 중 백업: stop으로 저장·종료 → 전체 서버 ZIP → 재실행. 백업 중에는 접속할 수 없습니다."));
        backupPanel.Controls.Add(Label("서버 폴더 밖의 실제 폴더를 선택하세요. 자동 삭제 없음. 디스크 여유 공간과 백업 보관 개수를 관리하세요."));
        backupPanel.Controls.Add(Label("복원: 서버 종료 → ZIP을 빈 폴더에 압축 해제 → 해당 폴더를 새 프로필로 등록. 기존 월드에 덮어쓰지 마세요."));
        Page(tabs, "백업", backupPanel, Label("월드 · mods · config · 라이브러리 · 설정 전체 백업 (잠금 파일 제외)"));
        offlineOnly.AddRange([profiles, profileName, folder, java, xms, xmx, properties, backupFolder, tpsCommand]);
        profiles.SelectedIndexChanged += (_, _) => { if (!loading && profiles.SelectedItem is Profile p) Apply(p); };
        autoRestart.CheckedChanged += (_, _) => { if (!autoRestart.Checked) restartAt = DateTime.MaxValue; };
        autoBackup.CheckedChanged += (_, _) => nextBackup = DateTime.UtcNow.AddMinutes((double)interval.Value);
        interval.ValueChanged += (_, _) => nextBackup = DateTime.UtcNow.AddMinutes((double)interval.Value);
        UpdateUI();
    }
    // 오래 걸리는 작업 동안 관련 버튼을 잠그고, 오류를 사용자에게 표시한 뒤 잠금을 해제합니다.
    // [동작 설명] 비동기 작업 중 중복 실행을 막고 예외 안내와 busy 상태 복구를 공통 처리합니다.
    private async Task RunAsync(Func<Task> action)
    {
        if (busy) return;
        busy = true; UpdateUI();
        try { await action(); }
        catch (Exception e) { ShowError(e); }
        finally { busy = false; UpdateUI(); }
    }
    private void ShowError(Exception e) { server.Log(e.Message); WarmDialog.Show(this, e.Message, "확인 필요", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    private string Folder()
    {
        if (string.IsNullOrWhiteSpace(folder.Text) || !Directory.Exists(folder.Text)) throw new DirectoryNotFoundException("유효한 서버 폴더를 선택하세요.");
        return Path.GetFullPath(folder.Text);
    }
    private static void OpenFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("폴더 경로가 비어 있습니다.");
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo(Path.GetFullPath(path)) { UseShellExecute = true });
    }
    // 현재 화면에서 편집한 값을 프로필로 모읍니다. 기존 식별자와 설치 정보는 유지합니다.
    // [동작 설명] 현재 화면 입력을 서버 등록 정보 객체로 모읍니다. 이 함수 자체가 서버를 실행하지는 않습니다.
    private Profile CaptureProfile() => new()
    {
        Id = current.Id,
        ImagePath = current.ImagePath,
        BackupChat = backupChat.Checked,
        BackupBossbar = backupBossbar.Checked,
        Kind = current.Kind,
        MinecraftVersion = current.MinecraftVersion,
        LoaderVersion = current.LoaderVersion,
        CreatedAt = current.CreatedAt,
        LastRun = current.LastRun,
        Port = int.TryParse(port, out int number) ? number : 25565,
        LaunchJar = current.LaunchJar,
        ArgumentFile = current.ArgumentFile,
        ManagedInstall = current.ManagedInstall,
        Name = profileName.Text.Trim(),
        Folder = folder.Text.Trim(),
        Java = java.Text.Trim(),
        MinRam = (int)xms.Value,
        MaxRam = (int)xmx.Value,
        RestartDelaySeconds = (int)restartDelay.Value, RestartLimit = (int)restartLimit.Value,
        AutoRestart = autoRestart.Checked,
        AutoBackup = autoBackup.Checked,
        BackupMinutes = (int)interval.Value,
        BackupKeepCount = (int)backupKeep.Value,
        BackupFolder = backupFolder.Text.Trim(),
        TpsCommand = tpsCommand.Text.Trim()
    };
    private void Apply(Profile p)
    {
        current = p; imagePicker?.SetPath(p.ImagePath); backupChat.Checked = p.BackupChat; backupBossbar.Checked = p.BackupBossbar;
        if (extensionPage != null) extensionPage.Text = ContentFolder == "plugins" ? "Plugins" : "Mods";
        profileName.Text = p.Name; folder.Text = p.Folder; java.Text = p.Java;
        xms.Value = Math.Clamp(p.MinRam, 1, 256); xmx.Value = Math.Clamp(p.MaxRam, 1, 256);
        restartDelay.Value = Math.Clamp(p.RestartDelaySeconds,5,3600); restartLimit.Value = Math.Clamp(p.RestartLimit,1,20);
        autoRestart.Checked = p.AutoRestart; autoBackup.Checked = p.AutoBackup;
        backupKeep.Value=Math.Clamp(p.BackupKeepCount,0,10000);
        interval.Value = Math.Clamp(p.BackupMinutes, 5, 10080); backupFolder.Text = p.BackupFolder; tpsCommand.Text = p.TpsCommand;
        restartAt = DateTime.MaxValue; restartAttempts = 0; nextBackup = DateTime.UtcNow.AddMinutes((double)interval.Value);
        try { RefreshFiles(); } catch (Exception e) { server.Log(e.Message); }
        RefreshDashboard();
    }
    private void ReloadProfiles(int index)
    {
        loading = true; profiles.Items.Clear(); profiles.Items.AddRange(saved.Cast<object>().ToArray()); profiles.SelectedIndex = index; loading = false;
        if (index >= 0) Apply(saved[index]);
    }
    private void SaveProfile()
    {
        var p = CaptureProfile();
        if (p.Name.Length == 0) throw new ArgumentException("서버 이름을 입력하세요.");
        if (p.MinRam > p.MaxRam) throw new ArgumentException("최소 RAM은 최대 RAM 이하여야 합니다.");
        saved = Storage.Load(); ServerRegistrations.ValidateSave(saved,p); int i = saved.FindIndex(x => x.Id == p.Id); if (i >= 0) saved[i] = p; else { saved.Add(p); i = saved.Count - 1; }
        Storage.Save(saved);
        // 백업 ZIP에도 실행 정보가 들어가도록 서버 폴더의 복원용 프로필을 갱신합니다.
        if (p.ManagedInstall && Directory.Exists(p.Folder)) Storage.AtomicWrite(Path.Combine(p.Folder, "manager-launch.json"), JsonSerializer.Serialize(p, Storage.Json));
        if (server.Running) current = p; else ReloadProfiles(i); server.Log("서버 설정 저장 완료");
    }
    // Java 확인 → 설정 읽기 → 서버 실행 → 마지막 실행 시간 저장 순서로 진행합니다.
    // [동작 설명] 화면 설정을 확인하고 서버 시작을 요청하는 UI 진입점입니다. 실제 프로세스 실행은 ServerService가 맡습니다.
    private async Task StartAsync()
    {
        Folder();
        if (xms.Value > xmx.Value) throw new ArgumentException("최소 RAM은 최대 RAM 이하여야 합니다.");
        string version = await ServerService.CheckJavaAsync(java.Text.Trim()); server.Log(version.Trim()); javaBadge.Text = "Java 확인됨\n" + version.Split('\n')[0];
        RefreshFiles(); server.Start(CaptureProfile()); current.LastRun = DateTime.Now;
        var allProfiles = Storage.Load(); var stored = allProfiles.FirstOrDefault(p => p.Id == current.Id); if (stored != null) { stored.LastRun = current.LastRun; Storage.Save(allProfiles); }
        nextPoll = DateTime.UtcNow.AddSeconds(5); onlineSince = DateTime.MaxValue;
        nextBackup = DateTime.UtcNow.AddMinutes((double)interval.Value);
    }
    private async Task SendCommandAsync()
    {
        string text = command.Text.Trim(); if (text.Length == 0) return;
        if (text.TrimStart('/').Equals("stop", StringComparison.OrdinalIgnoreCase)) restartAt = DateTime.MaxValue;
        await server.SendAsync(text); server.Log("> " + text); command.Clear();
    }
    private void RefreshFiles()
    {
        properties.Rows.Clear(); mods.Items.Clear(); whitelist.Items.Clear(); loadedFolder = "";
        port = "25565"; address = "모든 인터페이스";
        if (string.IsNullOrWhiteSpace(folder.Text) || !Directory.Exists(folder.Text)) return;
        LoadProperties(); LoadMods(); LoadWhitelist(); RefreshModsCatalog(); RefreshRosters();
    }
    // 디스크 파일을 읽어 원래 주석과 알 수 없는 키를 보존합니다.
    private void LoadProperties()
    {
        string f = Folder(); propertyFile.Load(Path.Combine(f, "server.properties")); loadedFolder = f;
        properties.Rows.Clear();
        foreach (var (key, value) in propertyFile.Values) properties.Rows.Add(key, value);
        RefreshTypedProperties();
        port = propertyFile.Values.GetValueOrDefault("server-port", "25565");
        address = propertyFile.Values.GetValueOrDefault("server-ip", ""); if (address.Length == 0) address = "모든 인터페이스";
    }
    private void SaveProperties()
    {
        if (server.Running) throw new InvalidOperationException("서버를 먼저 종료하세요.");
        if (!string.Equals(loadedFolder, Folder(), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("서버 폴더가 변경되었습니다. 다시 읽기를 누르세요.");
        properties.EndEdit();
        var values = properties.Rows.Cast<DataGridViewRow>().ToDictionary(r => (string)r.Cells[0].Value, r => Convert.ToString(r.Cells[1].Value) ?? "");
        propertyFile.Save(values); LoadProperties(); server.Log("server.properties 저장 완료");
    }
    private void LoadWhitelist()
    {
        whitelist.Items.Clear(); string file = Path.Combine(Folder(), "whitelist.json"); if (!File.Exists(file)) return;
        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        foreach (var entry in doc.RootElement.EnumerateArray())
            whitelist.Items.Add($"{entry.GetProperty("name").GetString()}  ({entry.GetProperty("uuid").GetString()})");
    }
    private async Task WhitelistAsync(string operation)
    {
        if (!server.Online) throw new InvalidOperationException("서버가 온라인일 때 화이트리스트를 변경하세요.");
        string name = playerName.Text.Trim();
        if (!Regex.IsMatch(name, @"^[A-Za-z0-9_]{1,16}$")) throw new ArgumentException("플레이어 이름은 영문·숫자·밑줄 1~16자입니다.");
        await server.SendAsync("whitelist " + operation + " " + name); await Task.Delay(800); LoadWhitelist();
        server.Log("화이트리스트 명령 전송 완료. 성공 여부는 서버 콘솔에서 확인하세요.");
    }
    private void LoadMods()
    {
        mods.Items.Clear(); string dir = Path.Combine(Folder(), ContentFolder); if (!Directory.Exists(dir)) return;
        foreach (var file in new DirectoryInfo(dir).EnumerateFiles("*.jar").OrderBy(f => f.Name)) mods.Items.Add(file);
    }
    private void AddMods()
    {
        if (server.Running) throw new InvalidOperationException("서버를 먼저 종료하세요.");
        using var d = new OpenFileDialog { Filter = "모드 JAR|*.jar", Multiselect = true };
        if (d.ShowDialog(this) != DialogResult.OK) return;
        string dir = Path.Combine(Folder(), ContentFolder); Directory.CreateDirectory(dir);
        foreach (string file in d.FileNames)
        {
            string target = Path.Combine(dir, Path.GetFileName(file));
            if (string.Equals(Path.GetFullPath(file), target, StringComparison.OrdinalIgnoreCase)) continue;
            if (File.Exists(target)) throw new IOException("같은 이름이 있습니다. 기존 모드를 먼저 삭제하세요: " + target);
            File.Copy(file, target);
        }
        LoadMods();
    }
    private void RemoveMod()
    {
        if (server.Running) throw new InvalidOperationException("서버를 먼저 종료하세요.");
        if (mods.SelectedItem is not FileInfo file) return;
        string expected = Path.Combine(Folder(), ContentFolder);
        if (!string.Equals(file.DirectoryName, expected, StringComparison.OrdinalIgnoreCase)) throw new IOException("폴더가 변경되었습니다. 목록을 갱신하세요.");
        if (WarmDialog.Show(this, $"{file.Name}\n이 모드를 mods-disabled 폴더로 이동할까요?", "모드 삭제", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        string target = Path.Combine(Folder(), "mods-disabled", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(target); File.Move(file.FullName, Path.Combine(target, file.Name)); LoadMods();
    }
    // 실행 중인 월드를 압축하지 않습니다. 저장/정상 종료를 기다린 뒤 백업하고 필요하면 다시 실행합니다.
    // [동작 설명] 수동/자동 백업 요청을 처리합니다. 실행 상태에 맞춰 서버 종료·백업·재시작 흐름을 연결합니다.
    private async Task BackupAsync(bool automatic)
    {
        string source = Folder(); string dest = BackupPaths.ForServer(CaptureProfile());
        string[] backupWorlds = BackupService.WorldFolders(source);
        bool resume = server.Running;
        if (resume && !server.Online) throw new InvalidOperationException("시작/종료 중에는 백업하지 않습니다.");
        if (resume && !automatic && WarmDialog.Show(this, "백업 동안 서버를 정상 종료하며 접속이 끊어집니다. 완료 후 다시 시작합니다. 계속할까요?", "서버 백업", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        restartAt = DateTime.MaxValue;
        await ClearBackupNotice(); if (resume) { await server.SendAsync("say [서버 알림] 월드를 저장하고 백업 후 재시작합니다."); await server.SendAsync("save-all flush"); await server.StopAsync(); }
        // Holds the manager lock during the archive; world lock checks also reject external servers.
        using (var managerLock = new FileStream(Path.Combine(source, ".manager.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            string session = Path.Combine(backupWorlds[0], "session.lock");
            using var worldLock = File.Exists(session) ? new FileStream(session, FileMode.Open, FileAccess.ReadWrite, FileShare.None) : null;
            backupStatus.Text = "압축 중…";
            try
            {
                var progress = new Progress<string>(file => backupStatus.Text = "압축 중: " + (file.Length > 60 ? "…" + file[^60..] : file));
                string result = await BackupService.CreateAsync(source, dest, progress);
                try{BackupService.RetainCreatedBackup(result,(int)backupKeep.Value);}catch(Exception retentionError){server.Log("백업은 완료됐지만 보관 개수 정리에 실패했습니다: "+retentionError.Message);}
                backupStatus.Text = "완료: " + Path.GetFileName(result); server.Log("백업 완료: " + result);
            }
            catch { backupStatus.Text = "백업 실패 · 서버는 정지 상태 유지"; throw; }
        }
        if (resume && !closing) await StartAsync();
        backupCancel?.Dispose(); backupCancel = null; nextBackup = DateTime.UtcNow.AddMinutes((double)interval.Value);
    }
    // 반복 크래시로 무한 재시작하지 않도록 30초 대기와 최대 3회 제한을 둡니다.
    private void OnExited(bool unexpected, int code)
    {
        if (unexpected && autoRestart.Checked && !closing && restartAttempts < restartLimit.Value)
        { restartAttempts++; restartAt = DateTime.UtcNow.AddSeconds((double)restartDelay.Value); server.Log($"{restartDelay.Value}초 후 자동 재시작 예약 ({restartAttempts}/{restartLimit.Value})"); }
        else if (unexpected && autoRestart.Checked && restartAttempts >= restartLimit.Value) server.Log($"자동 재시작 {restartLimit.Value}회 제한에 도달했습니다. 원인을 해결한 뒤 시작 버튼을 누르세요.");
    }
    // 주기적으로 로그를 화면에 반영하고, 자동 백업 및 예약된 크래시 재시작을 처리합니다.
    // [동작 설명] 주기적으로 로그 큐와 상태를 확인하고 예약 작업을 처리합니다. 화면 깜빡임 수정 시 목록 전체 재생성을 추가하지 않도록 주의합니다.
    private async Task TickAsync()
    {
        if (tickBusy || IsDisposed) return;
        tickBusy = true;
        try
        {
            var lines = new List<string>();
            for (int i = 0; i < 600 && server.Output.TryDequeue(out string? line); i++) { server.Parse(line); RecordPlayerLine(line); RememberLog(line); if (ShowConsoleLine(line)) lines.Add(line); }
            if (lines.Count > 0)
            {
                AppendConsoleLines(lines);
                if (console.TextLength > 220000) { console.Select(0, console.TextLength - 180000); console.SelectedText = ""; }
                if (autoScroll.Checked) { console.SelectionStart = console.TextLength; console.ScrollToCaret(); }
            }
            var now = DateTime.UtcNow;
            if (server.Online)
            {
                if (onlineSince == DateTime.MaxValue) onlineSince = now;
                if ((now - onlineSince).TotalMinutes >= 10) restartAttempts = 0;
            }
            else onlineSince = DateTime.MaxValue;
            UpdateUI();
            if (busy || closing) return;
            if (!server.Running && now >= restartAt)
            { restartAt = DateTime.MaxValue; await RunAsync(StartAsync); return; }
            await UpdateBackupNotice(now);
            if (autoBackup.Checked && now >= nextBackup && (!server.Running || server.Online))
            {
                nextBackup = now.AddMinutes((double)interval.Value);
                await RunAsync(() => BackupAsync(true)); return;
            }
            if (server.Online && now >= nextPoll)
            {
                nextPoll = now.AddSeconds(15); await server.SendAsync("list");
                if (!string.IsNullOrWhiteSpace(server.Active?.TpsCommand)) await server.SendAsync(server.Active.TpsCommand);
            }
        }
        catch (Exception e) { server.Log("자동 갱신: " + e.Message); }
        finally { tickBusy = false; }
    }
    // [동작 설명] 현재 서버 상태에 맞춰 버튼 사용 가능 여부와 표시를 갱신합니다.
    private void UpdateUI()
    {
        foreach (var c in offlineOnly) c.Enabled = !server.Running && !busy;
        typedEditor?.SetReadOnly(server.Running||busy);
        start.Enabled = !server.Running && !busy; stop.Enabled = server.Running && !busy;
        restart.Enabled = server.Online && !busy; backup.Enabled = !busy && (!server.Running || server.Online);
        status.Text = server.State + (restartAt != DateTime.MaxValue ? $" · {Math.Max(0, (restartAt - DateTime.UtcNow).TotalSeconds):F0}초 후 재시작" : "");
        status.ForeColor = server.Online ? Color.SeaGreen : Color.DimGray;
        metrics.Text = server.Metrics;
        RefreshDashboard(); RefreshCatalogTick();
        bool recentTps = server.Online && (DateTime.UtcNow - server.TpsSample).TotalSeconds < 45;
        bool recentPlayers = server.Online && (DateTime.UtcNow - server.PlayerSample).TotalSeconds < 45;
        string[] names = recentPlayers ? server.Players.Order().ToArray() : [];
        if (!players.Items.Cast<string>().SequenceEqual(names)) { players.Items.Clear(); players.Items.AddRange(names); }
    }
    // 관리 창을 닫기 전에 정상 종료 명령을 보내 월드 저장이 끝날 때까지 기다립니다.
    public event Action? ProfileDeleted;
    public void DeleteStoredProfile(IWin32Window owner)
    {
        if(server.Running||busy)return;
        var target=Storage.Load().FirstOrDefault(p=>p.Id==current.Id);if(target==null)return;
        busy=true;
        try
        {
            var plan=ServerDeletion.Plan(target);using var dialog=new ServerDeleteWindow(plan);
            if(WarmDialog.Present(owner,dialog)!=DialogResult.OK)return;
            ServerDeletion.Execute(plan,dialog.ServerOption.Checked,dialog.BackupOption.Checked);
        }
        catch(Exception error){WarmDialog.Show(owner,"삭제를 완료하지 못했습니다. 일부 파일은 이미 삭제되었을 수 있습니다.\n"+error.Message,"서버 삭제 안내");return;}
        finally{busy=false;}
        timer.Stop();closing=true;restartAt=DateTime.MaxValue;allowClose=true;
        ProfileDeleted?.Invoke();
        // 현재 버튼의 작업 처리가 끝난 뒤 세션을 닫습니다. 다른 프로필을 선택하지 않습니다.
        BeginInvoke(new Action(Close));
    }
    private async void ClosingAsync(object? sender, FormClosingEventArgs e)
    {
        if (HomeHosted && !allowClose) { e.Cancel = true; Hide(); return; }
        if (allowClose) return;
        if (busy) { e.Cancel = true; WarmDialog.Show(this, "현재 작업이 끝난 뒤 창을 닫아주세요."); return; }
        if (!server.Running) { timer.Stop(); allowClose = true; return; }
        e.Cancel = true;
        if (WarmDialog.Show(this, "서버를 정상 종료한 후 프로그램을 닫을까요?", "종료", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        closing = true; restartAt = DateTime.MaxValue;
        await RunAsync(server.StopAsync);
        if (!server.Running) { allowClose = true; timer.Stop(); Close(); } else closing = false;
    }
}











