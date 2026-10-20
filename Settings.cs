// [소스 안내] 프로그램 설정 데이터와 설정 화면, 네트워크/방화벽 관련 작업을 담습니다. 서버 저장 폴더와 백업 폴더도 여기서 관리합니다.
using System.Diagnostics;
using System.Text;
using System.Text.Json;
namespace NeoForgeServerManager;

public sealed class AppSettings
{
    public string ServerRoot { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MinecraftServers");
    public string BackupRoot { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MinecraftBackups");
    static string FilePath => Path.Combine(Storage.Root, "settings.json");
    public static AppSettings Load() => File.Exists(FilePath) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new() : new();
    public void Save() { ServerRoot = Path.GetFullPath(ServerRoot); BackupRoot = Path.GetFullPath(BackupRoot); if (ServerRoot.Equals(BackupRoot, StringComparison.OrdinalIgnoreCase) || BackupRoot.StartsWith(ServerRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("백업은 서버 저장 폴더 밖으로 지정하세요."); Directory.CreateDirectory(ServerRoot); Directory.CreateDirectory(BackupRoot); Storage.AtomicWrite(FilePath, JsonSerializer.Serialize(this, Storage.Json)); }
}
// 관리자 권한은 방화벽 변경을 요청한 순간에만 Windows UAC로 받습니다. 전체 방화벽은 끄지 않습니다.
public static class NetworkSettingsService
{
    public static string RuleName(Profile p) => "MinecraftManager-" + p.Id;
    public static async Task SetFirewall(Profile p, int port, string network, bool remove = false)
    {
        if (port is < 1 or > 65535 || !new[] { "Private", "Public" }.Contains(network) || !System.Text.RegularExpressions.Regex.IsMatch(p.Id, "^[a-zA-Z0-9-]+$")) throw new ArgumentException("방화벽 입력을 확인하세요.");
        string id = RuleName(p);
        string script = "$ErrorActionPreference='Stop'; " + (remove ? $"Get-NetFirewallRule -Name '{id}' -ErrorAction SilentlyContinue | Remove-NetFirewallRule" : $"Get-NetFirewallRule -Name '{id}' -ErrorAction SilentlyContinue | Remove-NetFirewallRule; New-NetFirewallRule -Name '{id}' -DisplayName '{id}' -Direction Inbound -Action Allow -Protocol TCP -LocalPort {port} -Profile {network}");
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden }; start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-EncodedCommand"); start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(script))); using var process = Process.Start(start) ?? throw new IOException("권한 요청 실패"); await process.WaitForExitAsync(); if (process.ExitCode != 0) throw new IOException("방화벽 변경에 실패했습니다. 관리자 권한과 Windows 정책을 확인하세요.");
    }
    public static string FirewallStatus(Profile p)
    {
        dynamic policy = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2")!)!;
        try { foreach (dynamic rule in policy.Rules) if ((string)rule.Name == RuleName(p)) return "허용 규칙 등록됨 · TCP " + rule.LocalPorts; return "아직 허용 규칙이 없어요."; } finally { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(policy); }
    }
    // Windows UPnP COM은 STA에서 호출합니다. 공유기 비밀번호를 수집하거나 웹 관리페이지를 자동 조작하지 않습니다.
    public static Task<string> Upnp(Profile p, int external, int internalPort, string address, string action)
    {
        var result = new TaskCompletionSource<string>(); var thread = new Thread(() =>
        {
            object? instance = null;
            try
            {
                if (external is < 1 or > 65535 || internalPort is < 1 or > 65535 || !System.Net.IPAddress.TryParse(address, out var parsed) || parsed.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) throw new ArgumentException("IPv4 주소와 포트를 확인하세요.");
                instance = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.NATUPnP")!); dynamic nat = instance!; dynamic mappings = nat.StaticPortMappingCollection; if (mappings == null) throw new IOException("UPnP 공유기를 찾지 못했습니다. 공유기 설정에서 UPnP를 확인하거나 수동 설정을 사용하세요.");
                string desc = RuleName(p); dynamic? found = null; foreach (dynamic map in mappings) if ((int)map.ExternalPort == external && (string)map.Protocol == "TCP") { found = map; break; }
                if (action == "add") { if (found != null && (string)found!.Description != desc) throw new IOException("다른 프로그램이 이 외부 포트를 사용하고 있습니다. 덮어쓰지 않았습니다."); if (found != null) mappings.Remove(external, "TCP"); mappings.Add(external, "TCP", internalPort, address, true, desc); result.SetResult("연결 규칙 생성 완료 · 외부 인터넷 접속은 별도 확인이 필요합니다."); }
                else if (action == "remove") { if (found == null || (string)found!.Description != desc) throw new IOException("이 프로그램이 만든 규칙만 삭제할 수 있습니다."); mappings.Remove(external, "TCP"); result.SetResult("연결 규칙 삭제 완료"); }
                else result.SetResult(found == null ? "공유기 발견 · UPnP 사용 가능 · 해당 포트 규칙 없음" : "규칙 발견: " + found!.InternalClient + ":" + found.InternalPort + " · 외부 접속 가능 여부는 확인되지 않았습니다.");
            }
            catch (Exception e) { result.SetException(e); }
            finally { if (instance != null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(instance); }
        })
        { IsBackground = true }; thread.SetApartmentState(ApartmentState.STA); thread.Start(); return result.Task;
    }
}
public sealed partial class SettingsForm : WarmWindow
{
    readonly Panel content = new() { Dock = DockStyle.Fill, Padding = Padding.Empty };
    readonly Dictionary<string,WarmButton> navigation=[]; readonly Func<bool> anyRunning;
    public SettingsForm(Func<bool>? running = null)
    {
        WideBrand=true; anyRunning = running ?? (() => false); Text = "설정"; Size = new(1536, 1024); MinimumSize = new(1000, 760);
        var side=new ReferencePanel{Dock=DockStyle.Left,Width=344,ReferenceSize=new Size(344,892)};Controls.Add(content);Controls.Add(side);
        var home=Ui.Action("← 서버목록",Close);home.Font=new Font("Noto Sans KR",17,FontStyle.Bold);side.Place(home,26,24,292,64);
        side.Place(Ui.Heading("설정",14),28,118,260,36);
        string[] names=["경로 설정","방화벽 설정","포트포워딩 설정"];
        for(int i=0;i<names.Length;i++){string page=names[i];var b=Ui.Action(i==0?"경로":page,()=>ShowPage(page));b.IconName=i==0?"folder":i==1?"shield":"server";b.Navigation=true;b.Ghost=true;b.Font=new Font("Noto Sans KR",17,FontStyle.Bold);navigation[page]=b;side.Place(b,22,158+i*72,300,64);}
        ShowPage("경로 설정");
    }
    void ShowPage(string page)
    {
        foreach(var item in navigation){item.Value.Primary=item.Key==page;item.Value.Invalidate();}
        foreach (Control c in content.Controls.Cast<Control>().ToArray()) c.Dispose();
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true }; var card = new SurfaceCard { Dock = DockStyle.Fill, Padding = new Padding(24) }; content.Controls.Add(card); card.Controls.Add(flow); flow.Controls.Add(Ui.Heading(page));
        if (page == "경로 설정")
        {
            var settings = AppSettings.Load(); var servers = new WarmTextBox { Text = settings.ServerRoot, Width = 650 }; var backups = new WarmTextBox { Text = settings.BackupRoot, Width = 650 };
            void Browse(WarmTextBox box) { using var d = new FolderBrowserDialog(); if (d.ShowDialog(this) == DialogResult.OK) box.Text = d.SelectedPath; }
            flow.Controls.Add(Ui.Heading("서버 저장 위치", 16)); flow.Controls.Add(Ui.Note("새로 만들거나 가져오는 서버 폴더를 저장합니다.")); flow.Controls.Add(Ui.Row(servers, Ui.Action("찾아보기", () => Browse(servers))));
            var move = new WarmToggle { Text = "기존 서버도 새 위치로 복사하여 등록 경로 변경", AutoSize = true }; flow.Controls.Add(move); flow.Controls.Add(Ui.Note("원본은 복사가 모두 끝난 후에도 보존합니다. 실행 중인 서버는 이동할 수 없어요."));
            flow.Controls.Add(Ui.Heading("백업 저장 위치", 16)); flow.Controls.Add(Ui.Row(backups, Ui.Action("찾아보기", () => Browse(backups)))); flow.Controls.Add(Ui.Note("백업 폴더 / 서버 이름 / 2026-10-18_18-30-45.zip\n서버 이름이 같으면 고유 ID로 구분합니다. ZIP 파일은 덮어쓰지 않습니다."));
            var save = Ui.Action("경로 저장", () => { }, true); save.Click += async (_, _) =>
            {
                save.Enabled = false; try
                {
                    var list = Storage.Load(); var next = new AppSettings { ServerRoot = servers.Text, BackupRoot = backups.Text }; string oldBackup = settings.BackupRoot; if (move.Checked) { if (anyRunning()) throw new IOException("모든 서버를 먼저 종료하세요."); if (WarmDialog.Show(this, "기존 서버를 새 위치에 복사한 후 등록 경로를 바꿀까요? 원본은 보존됩니다.", "경로 변경", MessageBoxButtons.YesNo) != DialogResult.Yes) return; foreach (var p in list) { string target = Path.Combine(Path.GetFullPath(next.ServerRoot), p.Id); if (Path.GetFullPath(p.Folder) == target) continue; var launches = ServerImportService.LaunchFiles(p.Folder); string launch = p.ArgumentFile.Length > 0 ? p.ArgumentFile : p.LaunchJar.Length > 0 ? p.LaunchJar : launches.First(); await ServerImportService.CopyAsync(p.Folder, target, p, launch, new Progress<string>(), default); p.Folder = target; Storage.AtomicWrite(Path.Combine(target, "manager-launch.json"), JsonSerializer.Serialize(p, Storage.Json)); } }
                    // 기존 개별 백업 경로는 유지하고, 이전 전역 기본값을 쓰던 서버만 새 기본값으로 옮깁니다.
                    foreach (var p in list) if (p.BackupFolder == oldBackup) p.BackupFolder = next.BackupRoot; next.Save(); Storage.Save(list); WarmDialog.Show(this, "저장 위치를 변경했습니다."); Close();
                }
                catch (Exception e) { WarmDialog.Show(this, e.Message); }
                finally { save.Enabled = true; }
            }; flow.Controls.Add(save);
            var arranged=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new Size(1188,892)};
            arranged.Place(Ui.Heading("경로 설정",36),40,12,1000,72);var sub=Ui.Note("서버와 백업을 저장할 위치를 지정하세요.");sub.Font=new Font("Noto Sans KR",16);arranged.Place(sub,42,86,1100,44);
            ReferencePanel PathCard(string title,string description,int y,int height)
            {var surface=new SurfaceCard{Padding=new Padding(2)};arranged.Place(surface,42,y,1112,height);var panel=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new Size(1112,height),BackColor=WarmTheme.Surface};surface.Controls.Add(panel);panel.Place(new ReferenceGlyph("folder"),28,26,48,48);panel.Place(Ui.Heading(title,21),108,20,940,46);var note=Ui.Note(description);note.Font=new Font("Noto Sans KR",14);panel.Place(note,108,67,940,40);return panel;}
            void PathButtons(ReferencePanel panel,WarmTextBox box)
            {box.Font=new Font("Noto Sans KR",16);panel.Place(box,24,110,660,58);var browse=Ui.Action("찾아보기",()=>Browse(box));browse.Font=new Font("Noto Sans KR",16,FontStyle.Bold);panel.Place(browse,698,110,190,58);var open=Ui.Action("폴더 열기",()=>{try{if(!Directory.Exists(box.Text))throw new IOException("저장된 폴더가 없습니다.");Process.Start(new ProcessStartInfo(box.Text){UseShellExecute=true});}catch(Exception error){WarmDialog.Show(this,error.Message);}});open.Font=browse.Font;panel.Place(open,900,110,190,58);}
            var serverCard=PathCard("서버 저장 위치","새로 만들거나 가져오는 서버를 저장할 기본 폴더예요.",140,264);PathButtons(serverCard,servers);var example=Ui.Note("예시   "+Path.Combine(settings.ServerRoot,"우리 서버"));example.Font=new Font("Noto Sans KR",14);serverCard.Place(example,26,188,1058,56);
            var backupCard=PathCard("백업 저장 위치","서버 이름별 폴더에 날짜와 시간으로 압축 저장해요.",422,362);PathButtons(backupCard,backups);var backupExample=Ui.Note(settings.BackupRoot+"\n  └ 우리 서버\n       └ "+DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")+".zip\n\nZIP 압축  ·  서버 이름 / 날짜_시간.zip");backupExample.Font=new Font("Noto Sans KR",14);backupCard.Place(backupExample,26,188,1058,156);
            move.Font=new Font("Noto Sans KR",11);arranged.Place(move,42,810,630,66);var reset=Ui.Action("변경 취소",()=>ShowPage("경로 설정"));reset.Font=new Font("Noto Sans KR",16,FontStyle.Bold);arranged.Place(reset,704,812,204,70);save.Text="설정 저장";save.Font=reset.Font;arranged.Place(save,922,812,232,70);
            content.Controls.Remove(card);card.Dispose();content.Controls.Add(arranged);
        }
        else
        {
            var profiles = new WarmCombo { Width = 650 }; profiles.Items.AddRange(Storage.Load().ToArray()); profiles.SelectedIndex = profiles.Items.Count > 0 ? 0 : -1; flow.Controls.Add(Ui.Note("대상 서버")); flow.Controls.Add(profiles);
            var external = new WarmNumber { Minimum = 1, Maximum = 65535, Value = 25565, Width = 150 }; var internalPort = new WarmNumber { Minimum = 1, Maximum = 65535, Value = 25565, Width = 150 }; profiles.SelectedIndexChanged += (_, _) => { if (profiles.SelectedItem is Profile p) external.Value = internalPort.Value = p.Port; }; if (profiles.SelectedItem is Profile selected) external.Value = internalPort.Value = selected.Port;
            var status = Ui.Note("설정 전 상태를 확인해 주세요."); status.MaximumSize = new Size(850, 0); flow.Controls.Add(status);
            async Task Run(Func<Profile, Task<string>> job) { content.Enabled = false; try { if (profiles.SelectedItem is not Profile p) throw new IOException("등록된 서버를 선택하세요."); status.Text = await job(p); } catch (Exception e) { status.Text = e.Message; } finally { content.Enabled = true; } }
            if (page == "방화벽 설정")
            {
                var scope = new WarmCombo { Width = 240 }; scope.Items.AddRange(new[] { "개인 네트워크", "공용 네트워크" }); scope.SelectedIndex = 0; flow.Controls.Add(Ui.Row(Ui.Note("서버 포트 · TCP"), external, Ui.Note("적용 네트워크"), scope));
                var add = Ui.Action("방화벽 자동 설정", () => { }, true); add.Click += async (_, _) => await Run(async p => { await NetworkSettingsService.SetFirewall(p, (int)external.Value, scope.SelectedIndex == 0 ? "Private" : "Public"); return NetworkSettingsService.FirewallStatus(p); }); var check = Ui.Action("상태 다시 확인", () => { }); check.Click += async (_, _) => await Run(p => Task.FromResult(NetworkSettingsService.FirewallStatus(p))); var remove = Ui.Action("규칙 삭제", () => { }); remove.Click += async (_, _) => { if (WarmDialog.Show(this, "선택한 서버의 매니저 방화벽 규칙을 삭제할까요?", "규칙 삭제", MessageBoxButtons.YesNo) == DialogResult.Yes) await Run(async p => { await NetworkSettingsService.SetFirewall(p, (int)external.Value, "Private", true); return "규칙 삭제 완료"; }); }; flow.Controls.Add(Ui.Row(add, check, remove)); flow.Controls.Add(Ui.Note("설정할 때 Windows 관리자 권한 승인이 필요해요.\n방화벽을 끄지 않고 선택한 TCP 서버 포트만 허용합니다."));
            }
            else
            {
                string ip = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up).SelectMany(n => n.GetIPProperties().UnicastAddresses).FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(a.Address))?.Address.ToString() ?? "192.168.0.2";
                var address = new WarmTextBox { Text = ip, Width = 240 }; flow.Controls.Add(Ui.Row(Ui.Note("내 PC 주소"), address)); flow.Controls.Add(Ui.Row(Ui.Note("외부 포트"), external, Ui.Note("서버 포트 · TCP"), internalPort));
                foreach (var action in new[] { ("포트포워딩 자동 설정", "add"), ("연결 상태 확인", "check"), ("연결 규칙 삭제", "remove") }) { var b = Ui.Action(action.Item1, () => { }, action.Item2 == "add"); b.Click += async (_, _) => { if (action.Item2 == "remove" && WarmDialog.Show(this, "선택한 포트의 매니저 연결 규칙을 삭제할까요?", "규칙 삭제", MessageBoxButtons.YesNo) != DialogResult.Yes) return; await Run(p => NetworkSettingsService.Upnp(p, (int)external.Value, (int)internalPort.Value, address.Text, action.Item2)); }; flow.Controls.Add(b); }
                flow.Controls.Add(Ui.Note("공유기 UPnP가 켜져 있어야 자동 설정할 수 있어요.\n이중 공유기 또는 통신사 NAT 환경에서는 별도 설정이 필요합니다.")); flow.Controls.Add(Ui.Action("수동 설정 안내", () => WarmDialog.Show(this, "공유기 관리 페이지의 포트포워딩 메뉴에서 TCP 외부 포트를 내 PC 주소와 서버 포트에 연결하세요.\nPC 주소는 DHCP 예약을 권장합니다. 통신사 NAT라면 통신사에 공인 IP를 문의하세요.\n규칙 생성이 성공해도 인터넷 외부 접속 성공을 보장하지 않습니다."))); flow.Controls.Add(Ui.Action("공유기 설정 열기", () => { var gateway = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up).SelectMany(n => n.GetIPProperties().GatewayAddresses).FirstOrDefault(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.Address; if (gateway != null) Process.Start(new ProcessStartInfo("http://" + gateway) { UseShellExecute = true }); }));
            }
            ArrangeNetwork(page,card,flow,profiles,external,internalPort,status);
        }
        WarmTheme.Apply(this);
    }
}
