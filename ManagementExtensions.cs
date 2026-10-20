// [소스 안내] 온라인 모드/플러그인 검색 화면입니다. 인기 목록, 검색·페이지 이동, 설치 확인과 현재 스크롤 위치 보존을 연결합니다.
using System.IO.Compression;
using System.Diagnostics;
using System.Text.RegularExpressions;
namespace NeoForgeServerManager;

// 최소/최대 RAM은 4GB 단위의 목록으로 제한합니다. 기존 2GB 프로필은 화면에서 4GB로 올려 표시합니다.
public sealed class RamCombo : WarmCombo
{
    public decimal Value { get => int.Parse(Text); set => SelectedItem = (Math.Clamp((int)Math.Ceiling(value / 4) * 4, 4, 256)).ToString(); }
    public RamCombo(int value) { DropDownStyle = ComboBoxStyle.DropDownList; Width = 85; Items.AddRange(Enumerable.Range(1, 64).Select(x => (object)(x * 4).ToString()).ToArray()); Value = value; }
}
public sealed partial class MainForm
{
    readonly Panel typedProperties = new() { Dock = DockStyle.Fill }; PropertiesEditor? typedEditor;
    void RefreshTypedProperties() { foreach (var old in typedProperties.Controls.Cast<Control>().ToArray()) old.Dispose(); typedEditor = new(propertyFile.Values); typedEditor.SetReadOnly(server.Running||busy); typedProperties.Controls.Add(typedEditor); }
    void AddAdvancedTabs(TabControl tabs)
    {
        Page(tabs, "설정 · 한국어 도움말", typedProperties, Button("월드 관리",()=>{if(server.Running)throw new IOException("서버를 먼저 종료하세요.");using var dialog=new WorldManagementWindow(CaptureProfile());WarmDialog.Present(this,dialog);}), Button("다시 읽기", LoadProperties, true), Button("설정 적용", () => { if (server.Running) throw new IOException("서버를 먼저 종료하세요."); if (typedEditor == null) return; propertyFile.Save(typedEditor.Values()); LoadProperties(); }, true));
        // 조회 메뉴와 검색은 실행 중에도 사용하고 값 편집만 잠급니다.
        var extensions = new ListBox { Dock = DockStyle.Fill, DisplayMember = "Name" };
        void Reload() { extensions.Items.Clear(); var path = Path.Combine(Folder(), ContentFolder); Directory.CreateDirectory(path); extensions.Items.AddRange(new DirectoryInfo(path).GetFiles().Where(x => x.Name.EndsWith(".jar") || x.Name.EndsWith(".jar.disabled")).Cast<object>().ToArray()); }
        Page(tabs, "파일 활성/비활성", extensions, Button("파일 목록", Reload), Button("활성/비활성 전환", () => { if (server.Running) throw new IOException("서버를 먼저 종료하세요."); if (extensions.SelectedItem is not FileInfo file) return; string dest = file.FullName.EndsWith(".disabled") ? file.FullName[..^9] : file.FullName + ".disabled"; File.Move(file.FullName, dest, false); Reload(); LoadMods(); }, true), Button("온라인 검색", () => { using var dialog = new ExtensionSearchForm(CaptureProfile(),value=>current.MinecraftVersion=value,canInstall:()=>!server.Running); dialog.ShowDialog(this); LoadMods(); }));
        var ops = new ListBox { Dock = DockStyle.Fill }; var opName = new WarmTextBox { Width = 170 };
        void ReadOps() { ops.Items.Clear(); string path = Path.Combine(Folder(), "ops.json"); if (!File.Exists(path)) return; using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)); foreach (var x in json.RootElement.EnumerateArray()) ops.Items.Add(x.GetProperty("name").GetString() + " · level " + x.GetProperty("level")); }
        async Task Op(bool grant) { if (!server.Online) throw new IOException("온라인 서버에서 변경하세요."); if (!Regex.IsMatch(opName.Text, @"^[A-Za-z0-9_]{1,16}$")) throw new ArgumentException("올바른 플레이어 이름을 입력하세요."); await server.SendAsync((grant ? "op " : "deop ") + opName.Text); await Task.Delay(700); ReadOps(); }
        Page(tabs, "운영자 (ops)", ops, opName, Button("권한 부여", () => Op(true)), Button("권한 해제", () => Op(false)), Button("목록 읽기", ReadOps));
        var logs = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, WordWrap = false };
        Page(tabs, "로그 뷰어", logs, Button("로그 파일 열기", async () => { using var d = new OpenFileDialog { InitialDirectory = Path.Combine(Folder(), "logs"), Filter = "로그|*.log;*.txt|모든 파일|*.*" }; if (d.ShowDialog(this) != DialogResult.OK) return; using var stream = new FileStream(d.FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); if (stream.Length > 2_000_000) stream.Seek(-2_000_000, SeekOrigin.End); using var reader = new StreamReader(stream); logs.Text = await reader.ReadToEndAsync(); }), Label("큰 파일은 마지막 약 2MB만 표시합니다."));
        var restoreInfo = new Label { Dock = DockStyle.Fill, Text = "백업을 새로운 빈 폴더에 복원합니다. 원본 서버와 기존 월드는 덮어쓰지 않습니다.\n완료 후 새 프로필이 등록됩니다. 복원한 서버를 동시에 켜려면 포트를 변경하세요." };
        Page(tabs, "백업 복원", restoreInfo, Button("ZIP 복원", RestoreAsync, true));
    }
    Task RestoreAsync()=>RestoreFileAsync(null);
    async Task RestoreFileAsync(string? selectedZip)
    {
        if (server.Running) throw new IOException("서버를 종료하고 복원하세요.");
        using var zipDialog = new OpenFileDialog { Filter = "백업 ZIP|*.zip" }; if(selectedZip==null){if(zipDialog.ShowDialog(this)!=DialogResult.OK)return;selectedZip=zipDialog.FileName;}
        if (BackupService.IsWorldOnlyArchive(selectedZip))
        {
            using var pick = new FolderBrowserDialog { Description = "월드를 풀어 놓을 상위 폴더를 선택하세요. 새 하위 폴더에 복원합니다." };
            if (pick.ShowDialog(this) != DialogResult.OK) return;
            string restored = Path.Combine(pick.SelectedPath, "월드복원-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
            await BackupService.RestoreAsync(selectedZip, restored);
            WarmDialog.Show(this, "월드 폴더를 복원했습니다: " + restored + "\n서버가 종료된 상태에서 해당 월드 폴더를 서버 폴더로 옮겨 적용하세요. 기존 서버 파일은 변경하지 않았습니다.");
            return;
        }
        using var restore=new RestoreDialog(selectedZip,CaptureProfile());if(WarmDialog.Present(this,restore)!=DialogResult.OK)return;
        string destination=Path.GetFullPath(restore.Destination);
        await BackupService.RestoreAsync(selectedZip, destination);
        string snapshot = Path.Combine(destination, "manager-launch.json");
        var p = File.Exists(snapshot) ? System.Text.Json.JsonSerializer.Deserialize<Profile>(File.ReadAllText(snapshot)) ?? CaptureProfile() : CaptureProfile();
        p.Id = Guid.NewGuid().ToString("N"); p.Name = restore.ServerName; p.Folder = destination; p.CreatedAt = DateTime.Now; p.LastRun = null; p.AutoRestart = false; p.AutoBackup = false;
        var all = Storage.Load(); all.Add(p); Storage.Save(all); saved = all; ReloadProfiles(all.Count - 1); WarmDialog.Show(this, "새 프로필로 복원했습니다.");
    }
}
// 공급처가 공개한 호환 파일만 설치합니다. 계정이나 식별용 이메일 입력란은 검색 화면에 두지 않습니다.
public sealed class ExtensionSearchForm : WarmWindow
{
    public ExtensionSearchForm(Profile profile,Action<string>? versionChanged=null,Func<string,bool,int,Task<ModPluginService.SearchPage>>? fetchPage=null,Func<ModPluginService.Result,IProgress<string>,Task>? installProject=null,Func<bool>? canInstall=null)
    {
        var artwork=new ModArtwork();FormClosed+=(_,_)=>artwork.Dispose();
        Text="온라인 검색";Size=new(1260,830);MinimumSize=new(1000,700);
        var root=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new Size(1220,742)};Controls.Add(root);
        string noun=profile.Kind is "Paper" or "Purpur"?"플러그인":"모드";
        root.Place(Ui.Action("← "+noun+" 관리",Close),12,0,194,50);root.Place(Ui.Heading("온라인 검색",24),224,0,440,54);
        var query=new WarmTextBox{Name="ExtensionQuery",PlaceholderText=noun+" 이름 검색"};root.Place(query,730,0,370,50);var search=Ui.Action("검색",()=>{},true);root.Place(search,1110,0,110,50);
        query.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;e.Handled=true;if(search.Enabled)search.PerformClick();}};
        var filter=new SurfaceCard{Padding=new Padding(8)};root.Place(filter,12,64,1208,68);
        var filterLayout=new ReferencePanel{DrawCardBackground=false,BackColor=WarmTheme.Surface,Dock=DockStyle.Fill,ReferenceSize=new Size(1190,50)};filter.Controls.Add(filterLayout);
        var filterLabel=new ExtensionEnvironmentSummary();filterLayout.Place(filterLabel,8,0,780,50);
        var compatible=new WarmToggle{Text="호환 버전만",Checked=true};filterLayout.Place(compatible,990,4,196,44);
        void SaveVersion(string value){MinecraftVersions.Save(profile,value);versionChanged?.Invoke(value);}
        void UpdateFilter(){filterLabel.Kind=profile.Kind;filterLabel.Version=profile.MinecraftVersion;filterLabel.Invalidate();}
        if(!MinecraftVersions.Valid(profile.MinecraftVersion)){var detected=MinecraftVersions.Detect(profile.Folder);if(detected!=null)SaveVersion(detected);}
        bool PickVersion(){var value=WarmDialog.Input(this,"Minecraft 버전","실제 서버 버전을 입력하세요. 예: 1.21.1",MinecraftVersions.Valid(profile.MinecraftVersion)?profile.MinecraftVersion:"");if(value==null)return false;value=value.Trim();if(!MinecraftVersions.Valid(value)){WarmDialog.Show(this,"버전 형식을 확인하세요. 예: 1.21.1");return false;}SaveVersion(value);UpdateFilter();return true;}
        filterLayout.Place(Ui.Action("버전 지정",()=>PickVersion()),800,4,172,44);UpdateFilter();
        ReferencePanel Section(int x,int w){var c=new SurfaceCard{Padding=Padding.Empty};root.Place(c,x,144,w,544);var p=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new Size(w,544),BackColor=WarmTheme.Surface};c.Controls.Add(p);return p;}
        var results=Section(12,640);var detail=Section(664,556);
        var count=Ui.Heading("검색 결과",18);results.Place(count,22,8,594,44);
        var list=new ExtensionResultsList{BorderStyle=BorderStyle.None,DrawMode=DrawMode.OwnerDrawFixed,Name="ExtensionResults",ItemHeight=148,IntegralHeight=false};results.Place(list,18,58,604,412);
        int pageOffset=0,total=0;string shownQuery="";bool loadedInstalled=false;
        var previous=Ui.Action("← 이전 100개",()=>{});results.Place(previous,18,484,160,42);
        var range=Ui.Note("");range.AutoSize=false;range.TextAlign=ContentAlignment.MiddleCenter;results.Place(range,184,484,256,42);
        var more=Ui.Action("다음 100개 →",()=>{});results.Place(more,446,484,176,42);
        void Paging(){previous.Enabled=pageOffset>0;more.Enabled=pageOffset+list.Items.Count<total;range.Text=total==0?"0개":$"{pageOffset+1:N0}–{pageOffset+list.Items.Count:N0} / {total:N0}";}
        Paging();
        list.DrawItem+=(_,e)=>{if(e.Index<0)return;var item=(ModPluginService.Result)list.Items[e.Index];bool selected=(e.State&DrawItemState.Selected)!=0;var r=Rectangle.Inflate(e.Bounds,-2,-6);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using var shape=WarmTheme.Round(r,12);using var fill=new SolidBrush(selected?WarmTheme.Peach:WarmTheme.Surface);using var pen=new Pen(selected?WarmTheme.Accent:WarmTheme.Line);e.Graphics.FillPath(fill,shape);e.Graphics.DrawPath(pen,shape);if(artwork.Find(item.Id) is {} icon)e.Graphics.DrawImage(icon,Artwork.Fit(icon,new Rectangle(r.X+24,r.Y+26,72,72)));else ReferenceIcons.Draw(e.Graphics,"folder",new Rectangle(r.X+28,r.Y+30,62,62),WarmTheme.Ink);using var bold=new Font("Noto Sans KR",18,FontStyle.Bold);TextRenderer.DrawText(e.Graphics,(shownQuery.Length==0?$"{pageOffset+e.Index+1}. ":"")+item.Title,bold,new Rectangle(r.X+118,r.Y+22,r.Width-140,46),WarmTheme.Ink,TextFormatFlags.Left|TextFormatFlags.EndEllipsis);TextRenderer.DrawText(e.Graphics,"Modrinth   ·   "+profile.Kind+" · "+profile.MinecraftVersion,Font,new Rectangle(r.X+118,r.Y+73,r.Width-140,30),WarmTheme.Muted,TextFormatFlags.Left|TextFormatFlags.EndEllipsis);using var badgeFont=new Font("Noto Sans KR",9);ModEnvironment.Draw(e.Graphics,new Rectangle(r.X+118,r.Y+108,Math.Min(330,r.Width-140),26),item,badgeFont);};
        var title=Ui.Heading("항목을 선택하세요",22);detail.Place(title,24,20,508,60);var info=Ui.Note("검색 결과를 선택하면 호환 파일을 설치할 수 있습니다.");info.Font=new Font("Noto Sans KR",14);detail.Place(info,24,106,508,262);
        var site=Ui.Action("원본 페이지 ↗",()=>{});detail.Place(site,24,388,508,50);var install=Ui.Action("설치",()=>{},true);install.Name="ExtensionInstall";detail.Place(install,24,450,508,54);
        var status=Ui.Note("검색 결과와 지원 정보는 배포처가 제공하는 정보를 기준으로 표시합니다.");root.Place(status,12,700,1208,40);
        HashSet<string> installed=[];
        void UpdateInstall(){bool exists=list.SelectedItem is ModPluginService.Result item&&installed.Contains(item.Id);bool allowed=canInstall?.Invoke()??true;install.Text=exists?"설치됨":allowed?"설치":"서버 종료 후 설치";install.Enabled=list.SelectedItem!=null&&!exists&&allowed;}
        UpdateInstall();
        bool busy=false,pendingPopular=false;var installState=new System.Windows.Forms.Timer{Interval=500};installState.Tick+=(_,_)=>{if(!busy)UpdateInstall();};Shown+=(_,_)=>installState.Start();FormClosed+=(_,_)=>installState.Dispose();
        async Task Run(Func<ModPluginService,Task> action,bool preservePosition=false)
        {
            if(busy)return;int top=list.TopIndex,selection=list.SelectedIndex;busy=true;root.Enabled=false;bool retry=false;
            try{using var net=new DownloadClient("");await action(new(net));}
            catch(Exception e){status.Text=e.Message;using var notice=new ExtensionSearchNotice(e);retry=WarmDialog.Present(FindForm()!,notice)==DialogResult.Retry;}
            finally{busy=false;if(!IsDisposed){root.Enabled=true;UpdateInstall();Paging();if(preservePosition)ExtensionListPosition.Restore(list,selection,top);if(pendingPopular){pendingPopular=false;await LoadPage("",0);}else if(retry)await Run(action,preservePosition);}}
        }
        async Task LoadPage(string text,int offset)
        {
            text=text.Trim();if(busy){if(text.Length==0)pendingPopular=true;return;}
            await Run(async service=>{
                // An unknown version can still browse popular projects; installation checks the version separately.
                if(compatible.Checked&&!MinecraftVersions.Valid(profile.MinecraftVersion))compatible.Checked=false;
                status.Text=text.Length==0?"인기 목록을 불러오고 있어요…":"검색 결과를 불러오고 있어요…";
                var page=fetchPage!=null?await fetchPage(text,compatible.Checked,offset):await service.SearchPageAsync(profile,text,compatible.Checked,offset);
                if(!loadedInstalled){installed=await InstalledExtensions.ProjectIds(profile);loadedInstalled=true;}
                shownQuery=text;pageOffset=page.Offset;total=page.Total;
                list.BeginUpdate();try{list.Items.Clear();list.Items.AddRange(page.Items);if(list.Items.Count>0)list.SelectedIndex=0;else{title.Text="검색 결과가 없어요";info.Text="검색어나 호환 버전 필터를 바꿔보세요. 검색창을 비우면 인기 목록으로 돌아갑니다.";}}finally{list.EndUpdate();}
                count.Text=text.Length==0?$"인기 {noun} · 다운로드순":$"검색 결과 · {total:N0}개";Paging();
                status.Text=list.Items.Count==0?"검색 결과가 없습니다. 검색어 또는 호환 버전 필터를 확인하세요.":text.Length==0?"Modrinth 누적 다운로드순 · 현재 서버 종류와 선택한 버전 필터 기준입니다.":"결과를 선택하면 설치 시 호환 파일을 다시 확인합니다.";
                _=Task.WhenAll(page.Items.Select(item=>artwork.Load(item,()=>{if(!list.IsDisposed)list.QueueArtwork(item);})));
            });
        }
        list.SelectedIndexChanged+=(_,_)=>{UpdateInstall();if(list.SelectedItem is ModPluginService.Result item){title.Text=item.Title;info.Text=$"배포처       Modrinth\n\n지원 환경    {profile.Kind} · {profile.MinecraftVersion}\n\n설치 버전    호환되는 정식 배포판 자동 선택\n\n라이선스와 의존성은 원본 페이지에서 확인하세요.";}};
        search.Click+=async(_,_)=>await LoadPage(query.Text,0);
        query.TextChanged+=async(_,_)=>{if(string.IsNullOrWhiteSpace(query.Text))await LoadPage("",0);};
        previous.Click+=async(_,_)=>await LoadPage(shownQuery,Math.Max(0,pageOffset-100));more.Click+=async(_,_)=>await LoadPage(shownQuery,pageOffset+100);
        Shown+=async(_,_)=>await LoadPage("",0);
        install.Click+=async(_,_)=>await Run(async service=>{if(!(canInstall?.Invoke()??true))throw new IOException("검색은 가능하지만 설치하려면 서버를 종료하세요.");if(list.SelectedItem is not ModPluginService.Result result)return;if(!MinecraftVersions.Valid(profile.MinecraftVersion)&&!PickVersion())return;var progress=new Progress<string>(s=>status.Text=s);
            if(installProject!=null){if(WarmDialog.Show(this,result.Title+" 설치할까요?","설치 확인",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;await installProject(result,progress);}
            else{status.Text="필수 의존성과 기존 설치 버전 확인 중…";using var net=new DownloadClient("");var resolver=new ExtensionDependencies(net);var plan=await resolver.Plan(profile,result);using var confirmation=new DependencyWindow("필수 모드 함께 설치",plan);if(WarmDialog.Present(FindForm()!,confirmation)!=DialogResult.OK)return;if(!(canInstall?.Invoke()??true))throw new IOException("서버가 실행 중입니다. 서버를 종료한 뒤 설치하세요.");await resolver.Install(profile,plan,progress);foreach(var entry in plan.Files.Where(f=>f.Installed||f.Project.ServerSide!="unsupported"))installed.Add(entry.Project.Id);}
            installed.Add(result.Id);UpdateInstall();status.Text="설치 완료 · 다음 서버 시작부터 적용됩니다.";},preservePosition:true);
        site.Click+=(_,_)=>{if(list.SelectedItem is ModPluginService.Result result)Process.Start(new ProcessStartInfo("https://modrinth.com/project/"+Uri.EscapeDataString(result.Slug)){UseShellExecute=true});};
        FormClosing+=(_,e)=>{if(busy){e.Cancel=true;status.Text="현재 요청이 끝나면 닫을 수 있습니다.";}};WarmTheme.Apply(this);
    }
}

// Changing enabled/focus state around modal installation must not scroll the list to its selection.
public static class ExtensionListPosition
{
    public static void Restore(ListBox list,int selection,int top)
    {
        if(list.IsDisposed||list.Items.Count==0)return;
        if(selection>=0&&selection<list.Items.Count)list.SelectedIndex=selection;
        list.TopIndex=Math.Clamp(top,0,list.Items.Count-1);
    }
}

