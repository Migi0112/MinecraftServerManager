// [소스 안내] 플레이어·권한 목록과 설치된 모드/플러그인 목록을 관리 화면에 연결합니다. 목록 갱신과 모드 대표 이미지 로딩도 포함합니다.
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace NeoForgeServerManager;

public static class PlayerData
{
    static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(8) }; static readonly SemaphoreSlim slots = new(3);
    public static void ValidateName(string name) { if (!Regex.IsMatch(name, @"\A[A-Za-z0-9_]{1,16}\z")) throw new ArgumentException("플레이어 이름은 영문·숫자·밑줄 1~16자입니다."); }
    public static JsonArray Read(string folder, string file) { string path = Path.Combine(folder, file); if (!File.Exists(path)) return []; using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); return JsonNode.Parse(stream)?.AsArray() ?? []; }
    public static string Reason(string text) { text = text.Trim(); if (text.Length == 0) text = "서버 규칙 위반"; if (text.Length > 200 || text.Any(char.IsControl)) throw new ArgumentException("사유는 줄바꿈 없이 200자 이내로 입력하세요."); return text; }
    public static void EditReason(string folder, string name, string reason)
    {
        ValidateName(name); reason = Reason(reason); using var guard = new FileStream(Path.Combine(folder, ".manager.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        string world = PropertiesFile.ReadValue(Path.Combine(folder, "server.properties"), "level-name", "world"); string lockPath = Path.Combine(folder, world, "session.lock"); using var worldLock = File.Exists(lockPath) ? new FileStream(lockPath, FileMode.Open, FileAccess.Read, FileShare.None) : null;
        var entries = Read(folder, "banned-players.json"); var entry = entries.FirstOrDefault(x => string.Equals((string?)x?["name"], name, StringComparison.OrdinalIgnoreCase)) ?? throw new IOException("차단 목록이 변경되었습니다."); entry["reason"] = reason; Storage.AtomicWrite(Path.Combine(folder, "banned-players.json"), entries.ToJsonString(Storage.Json));
    }
    // 공식 계정 스킨을 가져온 뒤 얼굴과 겉 레이어만 그립니다. 실패하면 기본 그림을 유지합니다.
    public static async Task<Image?> Face(string name)
    {
        ValidateName(name); string cache = Path.Combine(Storage.Root, "faces", name.ToLowerInvariant() + ".png"); if (File.Exists(cache) && DateTime.UtcNow - File.GetLastWriteTimeUtc(cache) < TimeSpan.FromDays(3)) return Artwork.Read(cache);
        await slots.WaitAsync(); try
        {
            var profile = JsonNode.Parse(await http.GetStringAsync("https://api.mojang.com/users/profiles/minecraft/" + name)); string id = (string?)profile?["id"] ?? ""; if (!Regex.IsMatch(id, "^[a-fA-F0-9]{32}$")) return null;
            var session = JsonNode.Parse(await http.GetStringAsync("https://sessionserver.mojang.com/session/minecraft/profile/" + id)); var prop = session?["properties"]?.AsArray().FirstOrDefault(x => (string?)x?["name"] == "textures"); var textures = JsonNode.Parse(Convert.FromBase64String((string?)prop?["value"] ?? "")); string url = (string?)textures?["textures"]?["SKIN"]?["url"] ?? ""; if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Host != "textures.minecraft.net") return null;
            using var input = new MemoryStream(await http.GetByteArrayAsync(new UriBuilder(uri) { Scheme = "https", Port = -1 }.Uri)); using var skin = Image.FromStream(input); if (skin.Width < 64 || skin.Height < 32) return null; var face = new Bitmap(72, 72); using (var g = Graphics.FromImage(face)) { g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor; g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half; g.DrawImage(skin, new Rectangle(0, 0, 72, 72), new Rectangle(8, 8, 8, 8), GraphicsUnit.Pixel); g.DrawImage(skin, new Rectangle(0, 0, 72, 72), new Rectangle(40, 8, 8, 8), GraphicsUnit.Pixel); }
            Directory.CreateDirectory(Path.GetDirectoryName(cache)!); face.Save(cache, System.Drawing.Imaging.ImageFormat.Png); return face;
        }
        catch { return null; }
        finally { slots.Release(); }
    }
}
public sealed partial class MainForm
{
    // 오프라인/스킨 조회 실패 때도 플레이어라는 것을 알 수 있도록 기본 픽셀 얼굴을 표시합니다.
    static readonly Image DefaultFace = MakeDefaultFace();
    static Image MakeDefaultFace() { var image = new Bitmap(72, 72); using var g = Graphics.FromImage(image); g.Clear(Color.FromArgb(190, 143, 107)); using var hair = new SolidBrush(Color.FromArgb(78, 55, 41)); g.FillRectangle(hair, 0, 0, 72, 22); g.FillRectangle(hair, 0, 20, 9, 17); g.FillRectangle(hair, 63, 20, 9, 17); g.FillRectangle(Brushes.White, 10, 34, 18, 9); g.FillRectangle(Brushes.White, 44, 34, 18, 9); g.FillRectangle(Brushes.SlateBlue, 20, 34, 8, 9); g.FillRectangle(Brushes.SlateBlue, 44, 34, 8, 9); g.FillRectangle(hair, 25, 56, 22, 8); return image; }
    readonly Dictionary<string, CatalogView> catalogs = []; readonly Dictionary<string, Image> faces = []; readonly HashSet<string> requestedFaces = []; readonly List<Image> modIcons = [];
    string playerFingerprint = ""; DateTime nextRosterRefresh; bool refreshingRosters;
    void InstallCatalogPages(TabControl tabs)
    {
        void Replace(string name, Control body) { var page = tabs.TabPages.Cast<TabPage>().FirstOrDefault(x => x.Text == name); if (page == null) { page = new TabPage(name); tabs.TabPages.Add(page); } foreach (Control c in page.Controls.Cast<Control>().ToArray()) page.Controls.Remove(c); page.Controls.Add(body); body.Dock = DockStyle.Fill; }
        var modView = new CatalogView("모드 · 플러그인 관리"); catalogs["mods"] = modView; Replace("Mods", modView);
        void ModAction(string operation) { if (server.Running) throw new IOException("서버를 먼저 종료하세요."); var selected = modView.SelectedIds; if (selected.Length == 0) return; if (WarmDialog.Show(this, $"선택한 {selected.Length}개 파일에 적용할까요?\n삭제 파일은 removed 폴더에 보관합니다.", "모드 변경", MessageBoxButtons.YesNo) != DialogResult.Yes) return; ModFiles.Change(Path.Combine(Folder(), ContentFolder), selected, operation); RefreshModsCatalog(); }
        modView.Actions.Controls.AddRange([Button("+ 모드 추가", () => { AddMods(); RefreshModsCatalog(); }, true), Button("온라인 검색", () => { OpenExtensionSearch(); }), Button("폴더 열기", () => OpenFolder(Path.Combine(Folder(), ContentFolder))), Button("활성화", () => ModAction("enable"), true), Button("비활성화", () => ModAction("disable"), true), Button("삭제", () => ModAction("delete"), true), Button("새로고침", RefreshModsCatalog)]);
        foreach(Control control in modView.Actions.Controls.Cast<Control>().Take(3).ToArray())modView.Inputs.Controls.Add(control);
        modView.Actions.Controls.Add(Button("업데이트 확인",()=>OpenModMaintenance(false)));
        modView.Actions.Controls.Add(Button("서버용 모드 정리",()=>OpenModMaintenance(true)));
        var comingSoon=new ComingSoonButton{Font=new Font("Noto Sans KR",11)};modView.Inputs.Controls.Add(comingSoon);modView.Inputs.Controls.SetChildIndex(comingSoon,2);
        modView.RowAction+= (item,action)=>{if(action=="Details")ShowModDetails(item);};
        foreach (string key in new[] { "접속자", "화이트리스트", "블랙리스트", "운영자 (ops)" })
        {
            string role = key; var view = new CatalogView(key, true); catalogs[role] = view; view.CardAction = role == "운영자 (ops)" ? "권한 해제" : role == "화이트리스트" ? "허용 해제" : role == "블랙리스트" ? "블랙리스트 해제" : ""; Replace(role, view); if (role is "블랙리스트" or "운영자 (ops)") view.ShowMode(true);if(role=="운영자 (ops)")view.EditLabel="권한 변경"; var name = new WarmTextBox { Width = 410, Height=50, PlaceholderText = "추가할 플레이어 이름" }; var reason = new WarmTextBox { Width = 210, PlaceholderText = "블랙리스트 사유" };
            var operatorLevel=new WarmCombo{Width=360,Height=50};operatorLevel.Items.AddRange(new object[]{"1","2","3","4"});operatorLevel.SelectedItem="4";if(role=="운영자 (ops)")operatorLevel.AccessibleName="권한 수준";
            if (role != "접속자") { view.Inputs.Controls.Add(name); if(role=="운영자 (ops)"){view.Inputs.Controls.Add(Ui.Note("권한 수준"));view.Inputs.Controls.Add(operatorLevel);} if (role == "블랙리스트") view.Inputs.Controls.Add(reason); view.Inputs.Controls.Add(Button(role == "블랙리스트" ? "블랙리스트 추가" : role == "운영자 (ops)" ? "권한 부여" : "허용 추가", () => role=="운영자 (ops)" ? GrantOperator(name.Text,int.Parse(operatorLevel.Text)) : PlayerCommand(role == "블랙리스트" ? "ban" : "whitelist add", [name.Text], reason.Text))); }
            string[] commands = role switch { "접속자" => ["kick", "ban", "whitelist add", "op"], "화이트리스트" => ["whitelist remove", "ban"], "블랙리스트" => ["pardon"], _ => ["deop"] };
            foreach (string cmd in commands) { string label = cmd switch { "kick" => "강퇴", "ban" => "블랙리스트", "op" => "관리자 권한 부여", "deop" => "권한 해제", "pardon" => "블랙리스트 해제", "whitelist add" => "화이트리스트 추가", _ => "허용 해제" }; view.Actions.Controls.Add(Button(label, async () => { var names = view.SelectedIds; if (names.Length == 0) return; string why = ""; if (cmd is "ban" or "kick") { var input = WarmDialog.Input(this, "사유 입력", "선택한 플레이어에게 표시할 사유를 입력하세요."); if (input == null) return; why = input; } await PlayerCommand(cmd, names, why, role == "화이트리스트" && cmd == "ban"); })); }
            if (role == "화이트리스트") { view.Actions.Controls.Add(Button("화이트리스트 사용", () => server.SendAsync("whitelist on"))); view.Actions.Controls.Add(Button("사용 해제", () => server.SendAsync("whitelist off"))); }
            view.Actions.Controls.Add(Button("새로고침", async () => { if (server.Online) await server.SendAsync("list"); RefreshRosters(); }));
            view.RowAction += (item, action) => { _ = RunAsync(async () => { if (action == "Edit" && role == "블랙리스트") { if (server.Running) throw new IOException("차단 사유 수정은 서버를 정상 종료한 뒤 사용할 수 있습니다."); var value = WarmDialog.Input(this, "차단 사유 수정", "새 차단 사유", item.Detail); if (value != null) PlayerData.EditReason(Folder(), item.Name, value); RefreshRosters(); } else if(action=="Edit" && role=="운영자 (ops)") {if(server.Running)throw new IOException("권한 단계 변경은 서버를 정상 종료한 뒤 사용할 수 있습니다.");var value=WarmDialog.Input(this,"운영자 권한 변경","1~4 사이의 권한 단계를 입력하세요.");if(value!=null){if(!int.TryParse(value,out int level))throw new ArgumentException("숫자 1~4를 입력하세요.");OfflineRoster.SetOperatorLevel(Folder(),item.Name,level);RefreshRosters();}} else if (action is "Edit" or "Details") WarmDialog.Show(this, item.Name + "\n" + item.Detail + "\n" + item.State, "플레이어 정보"); else await PlayerCommand(role == "블랙리스트" ? "pardon" : role == "운영자 (ops)" ? "deop" : role == "화이트리스트" ? "whitelist remove" : "kick", [item.Name], ""); }); };
        }
        historyPage=new PlayerHistoryPage(()=>{RefreshPlayerHistory(true);},async(cmd,name)=>{string reason="";if(cmd=="ban"){var input=WarmDialog.Input(this,"블랙리스트","사유를 입력하세요.");if(input==null)return;reason=input;}await PlayerCommand(cmd,[name],reason);});Replace("접속자",historyPage);
        var info=new ImagePicker(current.ImagePath);imagePicker=info;Replace("서버 정보",BuildServerInformation(info));
        InstallLogViewer(tabs);
    }
    PlayerHistoryPage? historyPage;PlayerHistoryStore? historyStore;string historyFolder="";DateTime nextHistory;
    void RefreshPlayerHistory(bool force=false)
    {
        if(!Directory.Exists(folder.Text))return;
        try{
            if(historyFolder!=folder.Text){historyFolder=folder.Text;historyStore=new(historyFolder);nextHistory=DateTime.MinValue;}
            if(!force&&DateTime.UtcNow<nextHistory)return;nextHistory=DateTime.UtcNow.AddSeconds(15);
            historyStore!.Import(historyFolder,DateTime.UtcNow);historyStore.Observe(server.Online?server.Players:[],DateTime.UtcNow);
            historyPage?.SetData(historyFolder,historyStore.Records,server.Online?server.Players:[]);
        }catch(Exception e){server.Log("접속 기록: "+e.Message);}
    }
    void RecordPlayerLine(string line)
    {
        if(line.Contains(" joined the game")||line.Contains(" left the game")){nextHistory=DateTime.MinValue;RefreshPlayerHistory(true);}
    }
    ImagePicker? imagePicker;
    async Task GrantOperator(string name,int level)
    {
        PlayerData.ValidateName(name);
        if(server.Running)
        {
            int configured=int.TryParse(PropertiesFile.ReadValue(Path.Combine(Folder(),"server.properties"),"op-permission-level","4"),out int value)?value:4;
            if(configured!=level)throw new IOException($"실행 중인 서버의 기본 운영자 권한은 {configured}입니다. 다른 수준은 서버를 정상 종료한 뒤 지정하세요.");
            await PlayerCommand("op",[name],"");return;
        }
        if(WarmDialog.Show(this,$"{name}에게 권한 수준 {level}을 부여할까요?","운영자 권한",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
        OfflineRoster.GrantOperator(Folder(),name,level);RefreshRosters();
    }
    async Task PlayerCommand(string command, IEnumerable<string> values, string reason, bool removeWhitelist = false)
    {
        if (!server.Online) throw new IOException("서버가 온라인일 때 명령을 적용할 수 있습니다."); var names = values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(); foreach (var name in names) PlayerData.ValidateName(name); if (names.Length == 0) return;
        string suffix = command is "ban" or "kick" ? " " + PlayerData.Reason(reason) : "";
        if (WarmDialog.Show(this, string.Join(", ", names) + $"\n{command} 작업을 적용할까요?", "플레이어 관리", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        foreach (var name in names) { await server.SendAsync(command + " " + name + suffix); if (removeWhitelist) await server.SendAsync("whitelist remove " + name); }
        await Task.Delay(850); RefreshRosters(); server.Log("명령 전송 완료. 서버 응답은 전체 로그에서 확인하세요.");
    }
    int modCatalogGeneration;
    // [동작 설명] mods/plugins 폴더에서 설치 목록을 읽고 이름·버전·기본 이미지를 먼저 표시합니다.
    void RefreshModsCatalog()
    {
        if (!catalogs.TryGetValue("mods", out var view) || !Directory.Exists(folder.Text)) return; view.Title = ContentFolder == "plugins" ? "플러그인 관리" : "모드 관리"; foreach(var button in view.Actions.Controls.OfType<Button>().Concat(view.Inputs.Controls.OfType<Button>()))if(button.Text.StartsWith("+ "))button.Text=ContentFolder=="plugins"?"+ 플러그인 추가":"+ 모드 추가"; string root = Path.Combine(folder.Text, ContentFolder); Directory.CreateDirectory(root); var items = new List<CatalogItem>(); foreach (var file in new DirectoryInfo(root).GetFiles().Where(f => f.Name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) || f.Name.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase))) { var icon = ModFiles.Icon(file.FullName); modIcons.Add(icon); var metadata=ModFiles.Metadata(file.FullName);items.Add(new(file.Name, metadata.Name, $"{file.Length / 1048576.0:F1} MB", file.Name.EndsWith(".disabled") ? "비활성" : "활성", icon,metadata.Version)); }
        int generation=++modCatalogGeneration;
        _=LoadInstalledArtwork(view,items,root,generation);
        var oldIcons = modIcons.Take(modIcons.Count - items.Count).ToArray(); view.SetItems(items); foreach (var old in oldIcons) { modIcons.Remove(old); old.Dispose(); }
    }
    // [동작 설명] 대표 이미지와 설치 위치를 비동기로 보완합니다. 이전 요청의 결과가 새 목록을 덮지 않도록 세대 번호를 확인합니다.
    async Task LoadInstalledArtwork(CatalogView view,List<CatalogItem> items,string root,int generation)
    {
        using var artwork=new ModArtwork();
        var projects=await Task.WhenAll(items.Select(i=>InstalledExtensions.ResolveArtwork(Path.Combine(root,i.Id))));
        if(IsDisposed||view.IsDisposed||generation!=modCatalogGeneration)return;
        await Task.WhenAll(projects.Where(p=>p!=null).DistinctBy(p=>p!.Id).Select(p=>artwork.Load(p!,()=>{})));
        if(IsDisposed||view.IsDisposed||generation!=modCatalogGeneration)return;
        for(int i=0;i<items.Count;i++)items[i]=items[i] with{Project=projects[i]};
        for(int i=0;i<items.Count;i++)if(projects[i] is {} project&&artwork.Find(project.Id) is {} image){var copy=new Bitmap(image);modIcons.Add(copy);items[i]=items[i] with {Picture=copy};}
        view.SetItems(items);
        var used=items.Select(i=>i.Picture).ToHashSet();foreach(var old in modIcons.Where(i=>!used.Contains(i)).ToArray()){modIcons.Remove(old);old.Dispose();}
    }
    void RefreshRosters()
    {
        if (refreshingRosters || !Directory.Exists(folder.Text)) return; refreshingRosters = true;
        try
        {
            foreach (var pair in catalogs.Where(p => p.Key != "mods"))
            {
                var items = new List<CatalogItem>(); if (pair.Key == "접속자") { if (server.Online) foreach (var name in server.Players) {bool allowed=PlayerData.Read(folder.Text,"whitelist.json").Any(x=>string.Equals((string?)x?["name"],name,StringComparison.OrdinalIgnoreCase));var op=PlayerData.Read(folder.Text,"ops.json").FirstOrDefault(x=>string.Equals((string?)x?["name"],name,StringComparison.OrdinalIgnoreCase));items.Add(new(name,name,allowed?"등록됨":"미등록","온라인",faces.GetValueOrDefault(name)??DefaultFace,Permission:op==null?"없음":"권한 수준 "+op["level"]));} }
                else { string file = pair.Key == "화이트리스트" ? "whitelist.json" : pair.Key == "블랙리스트" ? "banned-players.json" : "ops.json"; foreach (var entry in PlayerData.Read(folder.Text, file)) { string name = (string?)entry?["name"] ?? ""; if (name.Length == 0) continue; items.Add(new(name, name, (string?)entry?["reason"] ?? (pair.Key == "운영자 (ops)" ? "권한 단계 " + entry?["level"] : "접속 허용"), (string?)entry?["created"] ?? (server.Players.Contains(name) && server.Online ? "온라인" : "오프라인"), faces.GetValueOrDefault(name) ?? DefaultFace)); } }
                pair.Value.SetItems(items); foreach (var item in items) if (requestedFaces.Add(item.Name)) _ = LoadFace(item.Name);
            }
        }
        catch (Exception e) { server.Log("플레이어 목록: " + e.Message); }
        finally { refreshingRosters = false; }
    }
    async Task LoadFace(string name) { Image? face = null; try { face = await PlayerData.Face(name); if (IsDisposed) { face?.Dispose(); return; } if (face != null) { faces[name] = face; RefreshRosters(); } } catch { face?.Dispose(); } }
    void RefreshCatalogTick() { RefreshPlayerHistory(); string fingerprint = server.Online + string.Join(",", server.Players.Order()); if (fingerprint != playerFingerprint || DateTime.UtcNow >= nextRosterRefresh) { playerFingerprint = fingerprint; nextRosterRefresh = DateTime.UtcNow.AddSeconds(15); RefreshRosters(); } }
    void InstallLegacyLogViewer(TabControl tabs)
    {
        var page = tabs.TabPages.Cast<TabPage>().First(x => x.Text == "로그 뷰어"); page.Controls.Clear(); var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 250, FixedPanel = FixedPanel.Panel1 }; var files = new ListBox { Dock = DockStyle.Fill, DisplayMember = "Name" }; var text = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, Font = new Font("Consolas", 10), WordWrap = false }; split.Panel1.Controls.Add(files); split.Panel2.Controls.Add(text);
        int generation = 0; async Task Open(string path) { int request = ++generation; try { var result = await LogFiles.Read(path); if (request == generation && !text.IsDisposed) text.Text = result; } catch (Exception e) { if (request == generation && !text.IsDisposed) text.Text = e.Message; } }
        void Reload() { files.Items.Clear(); if (!Directory.Exists(folder.Text)) return; foreach (var dir in new[] { Path.Combine(folder.Text, "logs"), Path.Combine(folder.Text, "crash-reports") }) if (Directory.Exists(dir)) files.Items.AddRange(new DirectoryInfo(dir).GetFiles().Where(f => f.Extension is ".log" or ".txt" or ".gz").OrderByDescending(f => f.LastWriteTime).Cast<object>().ToArray()); foreach (var f in new DirectoryInfo(folder.Text).GetFiles("*.log")) files.Items.Add(f); }
        files.SelectedIndexChanged += async (_, _) => { if (files.SelectedItem is FileInfo f) await Open(f.FullName); }; var top = Ui.Row(Button("로그 파일 열기", async () => { using var dialog = new OpenFileDialog { Filter = "로그|*.log;*.txt;*.gz|모든 파일|*.*" }; if (dialog.ShowDialog(this) == DialogResult.OK) await Open(dialog.FileName); }), Button("목록 새로고침", Reload)); page.Controls.Add(split); page.Controls.Add(top); page.Enter += (_, _) => Reload();
    }
}
public static class LogFiles
{
    public static async Task<string> Read(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); using Stream input = path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ? new System.IO.Compression.GZipStream(file, System.IO.Compression.CompressionMode.Decompress) : file; using var reader = new StreamReader(input); var buffer = new char[8192]; var tail = new System.Text.StringBuilder(); int n; while ((n = await reader.ReadAsync(buffer)) > 0) { tail.Append(buffer, 0, n); if (tail.Length > 2_100_000) tail.Remove(0, tail.Length - 2_000_000); }
        return tail.ToString();
    }
}

