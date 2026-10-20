// [소스 안내] 기존 서버를 찾아 실행 방식과 버전을 분석하고 등록하는 기능입니다. 새 서버 설치 화면과는 별도 경로입니다.
using System.Text.Json;
namespace NeoForgeServerManager;

// 원본을 수정하지 않고 새 폴더로 복사합니다. 프로필 저장은 복사가 모두 끝난 후 호출자가 수행합니다.
public static class ServerImportService
{
    public static string ManagedRoot => AppSettings.Load().ServerRoot;
    public static string[] LaunchFiles(string source)
    {
        var all = Walk(source).ToArray();
        return all.Where(x => Path.GetFileName(x) == "win_args.txt").Select(x => Path.GetRelativePath(source, x))
            .Concat(Directory.EnumerateFiles(source, "*.jar").Where(x => !Path.GetFileName(x).Contains("installer", StringComparison.OrdinalIgnoreCase)).Select(x => Path.GetFileName(x)))
            .Concat(File.Exists(Path.Combine(source, "run.bat")) ? ["run.bat"] : []).ToArray();
    }
    static IEnumerable<string> Walk(string folder)
    {
        if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) throw new IOException("연결된 폴더(정션/링크)는 복사할 수 없습니다. 실제 서버 폴더를 선택하세요.");
        foreach (var entry in Directory.EnumerateFileSystemEntries(folder))
        {
            var attributes = File.GetAttributes(entry); if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("서버 폴더 안에 링크가 있습니다: " + entry);
            if ((attributes & FileAttributes.Directory) != 0) { foreach (var file in Walk(entry)) yield return file; } else yield return entry;
        }
    }
    public static async Task<Profile> CopyAsync(string source, string destination, Profile settings, string launch, IProgress<string> progress, CancellationToken ct = default)
    {
        source = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar); destination = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar);
        if (destination.Equals(source, StringComparison.OrdinalIgnoreCase) || destination.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("복사할 위치는 원본 서버 폴더 밖이어야 합니다.");
        if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("아직 없는 새 폴더로만 복사할 수 있습니다.");
        // 목적지 상위 폴더가 정션이면 원본 안으로 우회될 수 있으므로 거부합니다.
        for (var dir = new DirectoryInfo(destination).Parent; dir != null; dir = dir.Parent) if (dir.Exists && (dir.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("복사 대상에는 실제 폴더 경로를 사용하세요.");
        if (!LaunchFiles(source).Contains(launch, StringComparer.OrdinalIgnoreCase)) throw new IOException("서버 실행 파일을 다시 선택하세요.");
        string stage = destination + ".importing-" + Guid.NewGuid().ToString("N");
        var locks = new List<FileStream>();
        try
        {
            var files = Walk(source).ToArray();
            // 실행 중인 Minecraft/다른 매니저가 잡은 잠금을 확인하고 복사 완료까지 유지합니다.
            foreach (var file in files.Where(x => Path.GetFileName(x) is "session.lock" or ".manager.lock")) locks.Add(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None));
            long total = files.Sum(x => new FileInfo(x).Length); var drive = new DriveInfo(Path.GetPathRoot(destination)!); if (drive.AvailableFreeSpace < total + 1048576) throw new IOException("서버를 복사할 디스크 공간이 부족합니다.");
            Directory.CreateDirectory(stage);
            await Task.Run(async () =>
            {
                // 빈 mods/plugins 폴더도 보존합니다. Walk에서 링크가 없는지 먼저 확인했습니다.
                foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories)) { ct.ThrowIfCancellationRequested(); Directory.CreateDirectory(Path.Combine(stage, Path.GetRelativePath(source, directory))); }
                int count = 0;
                foreach (var file in files)
                {
                    ct.ThrowIfCancellationRequested(); if (Path.GetFileName(file) is "session.lock" or ".manager.lock") continue;
                    string relative = Path.GetRelativePath(source, file); string target = Path.Combine(stage, relative); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    var before = new FileInfo(file); long length = before.Length; DateTime stamp = before.LastWriteTimeUtc;
                    await using (var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
                    await using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true)) await input.CopyToAsync(output, ct);
                    var after = new FileInfo(file); if (after.Length != length || after.LastWriteTimeUtc != stamp) throw new IOException("복사 중 원본이 변경되었습니다. 서버를 완전히 종료하고 다시 시도하세요.");
                    File.SetLastWriteTimeUtc(target, stamp); if (++count % 20 == 0) progress.Report($"{count} / {files.Length}개 복사 · {relative}");
                }
            }, ct);
            ct.ThrowIfCancellationRequested();
            var profile = JsonSerializer.Deserialize<Profile>(JsonSerializer.Serialize(settings))!;
            profile.Id = Guid.NewGuid().ToString("N"); profile.Folder = destination; profile.CreatedAt = DateTime.Now; profile.LastRun = null; profile.AutoRestart = false; profile.AutoBackup = false;
            profile.ManagedInstall = launch != "run.bat"; profile.ArgumentFile = launch.EndsWith("win_args.txt", StringComparison.OrdinalIgnoreCase) ? launch : ""; profile.LaunchJar = launch.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ? launch : "";
            profile.Port = int.TryParse(PropertiesFile.ReadValue(Path.Combine(stage, "server.properties"), "server-port", "25565"), out int port) ? port : 25565;
            Storage.AtomicWrite(Path.Combine(stage, "manager-launch.json"), JsonSerializer.Serialize(profile, Storage.Json));
            Directory.Move(stage, destination); progress.Report("복사 완료"); return profile;
        }
        finally
        {
            foreach (var handle in locks) handle.Dispose();
            // stage는 이 함수가 만든 고유 폴더이며 재귀 삭제 대상은 목적지 옆의 정확한 임시 경로로 제한됩니다.
            if (Directory.Exists(stage) && Path.GetFullPath(stage).StartsWith(destination + ".importing-", StringComparison.OrdinalIgnoreCase)) Directory.Delete(stage, true);
        }
    }
}
public sealed class ImportServerForm : WarmWindow
{
    public bool Importing => busy;
    readonly WarmTextBox source = new() { Dock = DockStyle.Fill }, name = new() { Dock = DockStyle.Fill }, version = new() { Text = "미확인", Dock = DockStyle.Fill };
    readonly WarmCombo kind = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill }, launch = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    readonly Label progress = new() { AutoSize = true }; readonly WarmButton copy = new() { Text = "복사해서 가져오기", Primary = true, AutoSize = true }; readonly CancellationTokenSource cancel = new(); bool busy;
    readonly WarmTextBox destination=new();
    string importParent=ServerImportService.ManagedRoot;
    string imagePath="";
    readonly PictureBox image=new(){SizeMode=PictureBoxSizeMode.Zoom};
    public ImportServerForm()
    {
        Text = "기존 서버 등록"; WideBrand=true;Size = new(1536, 1024); MinimumSize = new(1280, 900);
        kind.Items.AddRange(["Paper", "Purpur", "Fabric", "Forge", "NeoForge"]); kind.SelectedIndex = 4;
        var root=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new Size(1532,892)};Controls.Add(root);var actions=root;
        root.Place(Ui.Action("⌂  홈",Close),38,16,136,56);root.Place(Ui.Heading("기존 서버 등록",34),206,8,900,66);root.Place(Ui.Note("기존 서버 폴더를 복사해서 서버목록에 추가해요."),208,72,1200,42);
        ReferencePanel Section(int y,int height,string title,string note){var card=new SurfaceCard{Padding=Padding.Empty};root.Place(card,38,y,1460,height);var panel=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new Size(1460,height)};card.Controls.Add(panel);panel.Place(Ui.Heading(title,20),28,8,350,50);panel.Place(Ui.Note(note),70,56,title.Contains("등록할")?700:320,title.Contains("등록할")?36:62);return panel;}
        var original=Section(114,150,"❶  원본 서버 폴더","등록할 기존 서버 폴더를 선택하세요.");original.Place(source,406,32,858,52);original.Place(progress,406,94,1030,44);var choose=new WarmButton{Text="찾아보기",IconName="folder"};original.Place(choose,1276,32,160,52);
        var infoPanel=Section(280,302,"❷  등록할 서버 정보","감지된 정보를 바탕으로 새 서버 정보를 설정하세요.");
        void Field(string label,Control control,int y){infoPanel.Place(Ui.Note(label),72,y,150,44);infoPanel.Place(control,222,y,558,44);}
        Field("서버 이름",name,94);Field("서버 종류",kind,146);Field("Minecraft 버전",version,198);Field("실행 파일",launch,250);
        infoPanel.Place(Ui.Heading("서버 이미지",17),862,48,480,40);infoPanel.Place(image,862,92,188,184);
        void RefreshImage(){image.Image?.Dispose();image.Image=Artwork.Read(imagePath);}
        infoPanel.Place(Ui.Action("이미지 선택",()=>{string selected=Artwork.Import(this);if(selected.Length>0){imagePath=selected;RefreshImage();}},true),1084,106,324,60);
        infoPanel.Place(Ui.Action("기본 이미지로",()=>{imagePath="";RefreshImage();}),1084,182,324,60);RefreshImage();
        var target=Section(598,168,"❸  복사할 위치","원본 서버 폴더를 복사할 위치를 선택하세요.");target.Place(destination,406,20,882,52);
        target.Place(Ui.Action("변경",()=>{using var d=new FolderBrowserDialog();if(d.ShowDialog(this)==DialogResult.OK){importParent=d.SelectedPath;destination.Text=ServerFolders.Available(importParent,name.Text);}}),1300,20,136,52);
        target.Place(Ui.Note("원본 파일은 그대로 두고, 월드와 설정을 포함한 서버 폴더 전체를 복사합니다."),406,100,1000,44);
        root.Place(Ui.Note("ⓘ  복사하기 전에 원본 서버를 정상 종료해 주세요. 복사가 완료되면 서버목록에 등록됩니다."),54,780,1430,50);
        root.Place(Ui.Action("취소",Close),38,838,154,50);copy.Text="복사 후 등록";root.Place(copy,1064,838,270,50);
        destination.ReadOnly=true;destination.Text=ServerFolders.Available(importParent,name.Text);
        name.TextChanged+=(_,_)=>destination.Text=ServerFolders.Available(importParent,name.Text);
        choose.Click += async (_, _) => { using var picker = new FolderBrowserDialog { Description = "기존 서버가 들어 있는 폴더" }; if (picker.ShowDialog(this) != DialogResult.OK) return; source.Text = picker.SelectedPath; await Inspect(); };
        source.Leave += async (_, _) => { if (!busy && Directory.Exists(source.Text)) await Inspect(); };
        copy.Click += async (_, _) =>
        {
            if (busy) return;
            try
            {
                if (string.IsNullOrWhiteSpace(name.Text) || string.IsNullOrWhiteSpace(version.Text) || launch.SelectedItem == null) throw new ArgumentException("서버 이름, 버전과 실행 파일을 확인하세요.");
                var settings = new Profile { ImagePath=imagePath,Name = name.Text.Trim(), Kind = kind.Text, MinecraftVersion = version.Text.Trim(), MinRam = 4, MaxRam = 8, TpsCommand = kind.Text switch { "Paper" or "Purpur" => "tps", "Forge" => "forge tps", "NeoForge" => "neoforge tps", _ => "" } };
                busy = true; actions.Enabled = false; source.Enabled = false; kind.Enabled = false; launch.Enabled = false;
                string targetPath = Path.GetFullPath(destination.Text);
                var result = await ServerImportService.CopyAsync(source.Text, targetPath, settings, launch.Text, new Progress<string>(s => progress.Text = s), cancel.Token);
                ServerRegistrations.Register(result); busy = false; DialogResult = DialogResult.OK; Close();
            }
            catch (OperationCanceledException) { busy = false; Close(); }
            catch (Exception e) { WarmDialog.Show(this, e.Message, "가져오기 확인"); }
            finally { busy = false; actions.Enabled = true; source.Enabled = kind.Enabled = launch.Enabled = true; }
        };
        FormClosing += (_, e) => { if (busy) { cancel.Cancel(); e.Cancel = true; progress.Text = "복사를 취소하고 정리하고 있어요…"; } }; FormClosed += (_, _) => {cancel.Dispose();image.Image?.Dispose();}; WarmTheme.Apply(this);
    }
    async Task Inspect()
    {
        try
        {
            copy.Enabled = false; string selected = source.Text; var files = await Task.Run(() => ServerImportService.LaunchFiles(selected)); if (source.Text != selected) return;
            launch.Items.Clear(); launch.Items.AddRange(files); if (files.Length > 0) launch.SelectedIndex = 0; name.Text = new DirectoryInfo(selected).Name;
            string descriptor = Path.Combine(selected, "manager-launch.json");
            if (File.Exists(descriptor)) { var profile = JsonSerializer.Deserialize<Profile>(File.ReadAllText(descriptor)); if (profile != null) { name.Text = profile.Name; version.Text = profile.MinecraftVersion; if (kind.Items.Contains(profile.Kind)) kind.SelectedItem = profile.Kind; } }
            progress.Text = files.Length == 0 ? "실행 가능한 JAR, win_args.txt 또는 run.bat이 없어요." : "서버 종류와 버전을 확인한 뒤 가져오세요.";
        }
        catch (Exception e) { WarmDialog.Show(this, e.Message, "폴더 확인"); }
        finally { copy.Enabled = true; }
    }
}


