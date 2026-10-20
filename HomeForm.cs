// [소스 안내] 파일 이름과 달리 새 서버 만들기(CreateServerWizard)의 기본 동작입니다. 단계 이동, 입력 검증, 서버 설치와 등록을 담당합니다.
using System.Diagnostics;
namespace NeoForgeServerManager;
// 한 단계씩 입력을 검증합니다. 설치 중 창 닫기는 취소 신호를 보내며 완성되지 않은 프로필은 목록에 등록하지 않습니다.
public sealed partial class CreateServerWizard : WarmWindow
{
    readonly Panel body = new() { Dock = DockStyle.Fill }; readonly WarmButton back = new() { Text = "이전", AutoSize = true }, next = new() { Text = "다음 →", AutoSize = true, Primary = true };
    readonly WizardHeader title = new();
    readonly ImagePicker serverImage = new();
    readonly WarmCombo kind = Combo(["Paper", "Purpur", "Fabric", "Forge", "NeoForge"]), game = Combo([]), build = Combo([]), min = Combo(Enumerable.Range(1, 64).Select(x => (x * 4).ToString()).ToArray()), max = Combo(Enumerable.Range(1, 64).Select(x => (x * 4).ToString()).ToArray());
    readonly WarmTextBox name = new() { Text = "내 서버", Width = 380 }, java = new() { Text = "java.exe", Width = 530 }, folder = new() { Width = 650 };
    readonly WarmToggle eula = new() { Text = "Minecraft EULA를 읽고 동의합니다", AutoSize = true };
    readonly WarmToggle includeBeta=new(){Text="",Checked=false};
    readonly RichTextBox log = new() { ReadOnly = true, Dock = DockStyle.Fill }; readonly CoralProgress progress = new();
    readonly PropertiesEditor props = new(); readonly CancellationTokenSource cancel = new();
    ModpackPage? modpack;
    public bool Installing => busy || modpack?.Installing==true;
    string installParent=AppSettings.Load().ServerRoot;
    int step; bool busy; DownloadClient? net;
    public CreateServerWizard()
    {
        Scenic=true;Scenery.Make(body);WideBrand=false; Text = "새 서버 만들기"; Size = new(1536, 1024); MinimumSize = new(1100, 800); Font = new("Noto Sans KR", 10);
        folder.ReadOnly=true;UpdateInstallFolder();
        name.TextChanged+=(_,_)=>UpdateInstallFolder();
        min.SelectedItem = "8"; max.SelectedItem = "16"; eula.CheckedChanged += (_, _) => { if (step == 5 && !busy) next.Enabled = eula.Checked; };
        includeBeta.CheckedChanged+=async(_,_)=>{if(step==1&&!busy)await RefreshVersions();};
        var bottom = new ReferencePanel {Tag="landscape",BackColor=Color.Transparent, Dock=DockStyle.Bottom, Height=100, ReferenceSize=new Size(1532,100) };
        var dismiss = new WarmButton {Text="취소"};dismiss.Click+=(_,_)=>Close();
        next.AutoSize=back.AutoSize=false;
        foreach(var button in new[]{dismiss,back,next})button.Font=new Font("Noto Sans KR",15,FontStyle.Bold);
        var actionCard=new SurfaceCard{Padding=Padding.Empty};bottom.Place(actionCard,136,4,1264,88);var actionBar=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new Size(1264,88)};actionCard.Controls.Add(actionBar);actionBar.Place(dismiss,24,16,164,56);actionBar.Place(back,828,16,190,56);actionBar.Place(next,1034,16,206,56);
        Controls.Add(body);Controls.Add(title);Controls.Add(bottom);title.HomeRequested+=(_,_)=>{if(!busy)Close();};
        next.Click += async (_, _) => await Next(); back.Click += (_, _) => { if (!busy) { step--; Render(); } };
        game.SelectedIndexChanged += async (_, _) =>
        {
            if (net == null || game.SelectedItem == null || IsDisposed) return;
            string selectedGame = game.Text; next.Enabled = false; game.Enabled = false;
            try
            {
                var versions = await InstallerFactory.Create(kind.Text, net).Builds(selectedGame, cancel.Token);
                if (IsDisposed || Disposing || selectedGame != game.Text) return;
                build.Items.Clear(); build.Items.AddRange(versions.Where(v=>includeBeta.Checked||IsStableVersion(v)).ToArray()); if (build.Items.Count > 0) build.SelectedIndex = 0;
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { if (!IsDisposed && !Disposing) WarmDialog.Show(this, error.Message, "버전 조회 실패"); }
            finally { if (!IsDisposed && !Disposing) { next.Enabled = build.SelectedItem != null; game.Enabled = true; } }
        };
        FormClosing += (_, e) => { if(modpack?.Installing==true){modpack.Close();e.Cancel=true;} if (busy) { cancel.Cancel(); e.Cancel = true; log.AppendText("\n취소 요청 중…"); } };
        FormClosed += (_, _) => { cancel.Cancel(); props.Toolbar.Dispose();props.Dispose(); net?.Dispose(); cancel.Dispose(); }; WarmTheme.Apply(this); Render();
    }
    void UpdateInstallFolder()=>folder.Text=ServerFolders.Available(installParent,name.Text);
    // 모드팩 검색도 같은 만들기 화면 안에 표시합니다. 설치 완료 시 기존 홈 갱신 흐름으로 돌아갑니다.
    void OpenModpack()
    {
        if(modpack!=null)return;
        var child=new ModpackPage();modpack=child;child.TopLevel=false;child.EmbedInShell();child.FormBorderStyle=FormBorderStyle.None;child.Dock=DockStyle.Fill;
        var hidden=Controls.Cast<Control>().Where(c=>c.Visible).ToArray();foreach(var control in hidden)control.Visible=false;
        Controls.Add(child);child.BringToFront();
        child.FormClosed+=(_,_)=>{Controls.Remove(child);modpack=null;if(child.Installed!=null){DialogResult=DialogResult.OK;Close();}else foreach(var control in hidden)control.Visible=true;};
        child.Show();
    }
    static WarmCombo Combo(string[] values) { var c = new WarmCombo { Width = 330, DropDownStyle = ComboBoxStyle.DropDownList }; c.Items.AddRange(values); if (values.Length > 0) c.SelectedIndex = 0; return c; }
    static Control Row(string label, Control c) { var p = new FlowLayoutPanel { AutoSize = true, Width = 930 }; p.Controls.Add(new Label { Text = label, Width = 230, AutoSize = false, Height = 35 }); p.Controls.Add(c); return p; }
    // 카드 레이아웃은 전체 창 크기에 맞춰 늘어나며 실제 입력값은 단계 이동 중에도 유지합니다.
    void Render() => RenderApproved();
    async Task Next()
    {
        if (busy) return; busy = true; next.Enabled = back.Enabled = false;
        try
        {
            if (step == 0) { string identity = ""; if (kind.Text == "Paper") { var answer = ApiIdentity.RequirePaperContact(this); if (answer == null) return; identity = answer; } net?.Dispose(); net = new(identity); game.Items.Clear(); build.Items.Clear(); game.Items.AddRange((await InstallerFactory.Create(kind.Text, net).Games(cancel.Token)).Where(v=>includeBeta.Checked||IsStableVersion(v)).ToArray()); if (game.Items.Count == 0) throw new IOException("지원 버전이 없습니다."); /* 단계 1에서 사용자가 게임 버전을 선택합니다. */}
            if (step == 1 && (game.SelectedItem == null || build.SelectedItem == null)) throw new ArgumentException("Minecraft와 로더 버전을 모두 선택하세요.");
            if(step==3&&string.IsNullOrWhiteSpace(name.Text)){var value=WarmDialog.Input(this,"서버 이름","서버목록에 표시할 이름이 필요해요.");if(string.IsNullOrWhiteSpace(value))return;name.Text=value;}
            if (step == 2) { if (int.Parse(min.Text) > int.Parse(max.Text)) throw new ArgumentException("최소 RAM은 최대 RAM 이하여야 합니다."); await JavaService.ValidateAsync(java.Text.Trim(), game.Text, net!, cancel.Token); WarmDialog.Show(this, await ServerService.CheckJavaAsync(java.Text.Trim()), "Java 확인 — 대상 버전 요구사항을 확인하세요"); }
            if (step < 5) { step++; Render(); return; }
            if (!eula.Checked) throw new ArgumentException("서버 생성 전에 EULA 동의가 필요합니다.");
            if (string.IsNullOrWhiteSpace(name.Text)) { var value = WarmDialog.Input(this, "서버 이름", "목록에 표시할 서버 이름을 입력하세요."); if (string.IsNullOrWhiteSpace(value)) return; name.Text = value; }
            string dest = Path.GetFullPath(folder.Text); if (Directory.Exists(dest) || File.Exists(dest)) throw new IOException("기존 데이터 보호를 위해 아직 없는 새 폴더를 선택하세요.");
            var values = props.Values();
            if (values["level-name"].IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || values["level-name"] is "." or ".." or "") throw new ArgumentException("월드 이름은 한 개의 폴더 이름이어야 합니다.");
            var p = new Profile { ImagePath = serverImage.ImagePath, Name = name.Text.Trim(), Kind = kind.Text, MinecraftVersion = game.Text, LoaderVersion = build.Text, Folder = dest, Java = java.Text.Trim(), MinRam = int.Parse(min.Text), MaxRam = int.Parse(max.Text), ManagedInstall = true, Port = int.Parse(values["server-port"]), TpsCommand = kind.Text switch { "Paper" or "Purpur" => "tps", "Forge" => "forge tps", "NeoForge" => "neoforge tps", _ => "" } };
            ShowInstallProgress(p); Directory.CreateDirectory(dest);
            var reporter = new Progress<string>(s => { File.AppendAllText(Path.Combine(dest, "manager-install.log"), s + Environment.NewLine); ReportInstall(s); });
            log.AppendText("설치를 시작합니다…\n");
            await InstallerFactory.Create(p.Kind, net!).Install(p, reporter, cancel.Token);
            PropertiesService.WriteNew(Path.Combine(dest, "server.properties"), values); Storage.AtomicWrite(Path.Combine(dest, "eula.txt"), "eula=true\n"); Storage.AtomicWrite(Path.Combine(dest, "user_jvm_args.txt"), $"-Xms{p.MinRam}G\n-Xmx{p.MaxRam}G\n");
            Storage.AtomicWrite(Path.Combine(dest, "manager-launch.json"), System.Text.Json.JsonSerializer.Serialize(p, Storage.Json));
            ServerRegistrations.Register(p); busy = false; DialogResult = DialogResult.OK; Close();
        }
        catch (Exception e) { WarmDialog.Show(this, e is OperationCanceledException ? "설치를 취소했습니다. 부분 설치 폴더는 로그 확인을 위해 남겨 둡니다." : e.Message, "생성 안내"); if (cancel.IsCancellationRequested) { busy = false; Close(); } else Render(); }
        finally { busy = false; next.Enabled = step != 5 || eula.Checked; back.Enabled = step > 0; }
    }
}










