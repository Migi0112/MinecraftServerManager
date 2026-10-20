// [소스 안내] 프로그램 업데이트 목록 조회·서명 검증·다운로드·설치 시작입니다. 배포 파일의 크기와 해시도 검증합니다.
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace NeoForgeServerManager;

public sealed record AppRelease(string Version,string Title,string Notes,string FileId,string Sha256,long Size);
public sealed record ReleaseFeed(int Schema,AppRelease[] Releases);
public sealed record SignedFeed(string Payload,string Signature);
public static class AppUpdates
{
    public static string DisplayVersion(string version)=>"v"+(version.StartsWith("1.12.",StringComparison.Ordinal)?version[2..]:version);
    public static string ReleaseTitle(AppRelease release)=>DisplayVersion(release.Version)+" · "+Regex.Replace(release.Title,@"^v?\d+(?:\.\d+)+\s*[·:—-]?\s*","");
    public const int HistoryLimit=20;
    public const string FeedId="1RJahZZFdSqjxNfYLmV4bWNxLyqIKYiiD";
    public static readonly Version Current=new(1,12,29);
    static readonly JsonSerializerOptions Json=new(){PropertyNameCaseInsensitive=true};
    public static string DownloadUrl(string id){if(!Regex.IsMatch(id,@"^[A-Za-z0-9_-]{10,150}$"))throw new IOException("올바르지 않은 배포 파일 ID입니다.");return "https://drive.google.com/uc?export=download&id="+id;}
    // [동작 설명] 내장 공개키로 목록 서명을 검증합니다. 서명과 파일 정보가 올바른 배포 항목만 사용합니다.
    public static AppRelease[] VerifyFeed(string text,string? publicKey=null)
    {
        var envelope=JsonSerializer.Deserialize<SignedFeed>(text,Json)??throw new IOException("버전 정보를 읽지 못했습니다.");
        var payload=Convert.FromBase64String(envelope.Payload);var signature=Convert.FromBase64String(envelope.Signature);
        if(publicKey==null){using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("NeoForgeServerManager.Assets.update-public.pem")!;using var reader=new StreamReader(stream);publicKey=reader.ReadToEnd();}
        using var rsa=RSA.Create();rsa.ImportFromPem(publicKey);
        if(!rsa.VerifyData(payload,signature,HashAlgorithmName.SHA256,RSASignaturePadding.Pss))throw new IOException("업데이트 정보의 서명이 일치하지 않습니다.");
        var feed=JsonSerializer.Deserialize<ReleaseFeed>(payload,Json)??throw new IOException("빈 버전 목록입니다.");
        if(feed.Schema!=1||feed.Releases==null||feed.Releases.Length>100)throw new IOException("지원하지 않는 버전 목록입니다.");
        foreach(var r in feed.Releases){if(!Version.TryParse(r.Version,out _)||!Regex.IsMatch(r.Sha256,@"^[0-9a-fA-F]{64}$")||r.Size<2||r.Size>512L*1024*1024)throw new IOException("배포 정보가 올바르지 않습니다.");DownloadUrl(r.FileId);}
        return feed.Releases.DistinctBy(r=>Version.Parse(r.Version)).OrderByDescending(r=>Version.Parse(r.Version)).Take(HistoryLimit).ToArray();
    }
    static HttpClient Client(){var c=new HttpClient{Timeout=TimeSpan.FromMinutes(10)};c.DefaultRequestHeaders.UserAgent.ParseAdd("Migi-MinecraftServerManager/1.12");return c;}
    // [동작 설명] 고정된 업데이트 목록 주소를 읽고 VerifyFeed의 검증을 거쳐 버전 목록을 반환합니다.
    public static async Task<AppRelease[]> Check(CancellationToken ct)
    {
        using var http=Client();using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var response=await http.GetAsync(DownloadUrl(FeedId),HttpCompletionOption.ResponseHeadersRead,timeout.Token);response.EnsureSuccessStatusCode();
        using var input=await response.Content.ReadAsStreamAsync(timeout.Token);using var buffer=new MemoryStream();var data=new byte[8192];int n;
        while((n=await input.ReadAsync(data,timeout.Token))>0){if(buffer.Length+n>256*1024)throw new IOException("버전 목록이 너무 큽니다.");buffer.Write(data,0,n);}
        return VerifyFeed(Encoding.UTF8.GetString(buffer.ToArray()).TrimStart('\uFEFF'));
    }
    // [동작 설명] 받은 설치 파일의 크기와 SHA-256 등이 배포 정보와 일치하는지 검사합니다.
    public static async Task ValidateFile(string path,AppRelease release,CancellationToken ct)
    {
        using var stream=File.OpenRead(path);if(stream.Length!=release.Size)throw new IOException("설치 파일 크기가 일치하지 않습니다.");
        var hash=Convert.ToHexString(await SHA256.HashDataAsync(stream,ct));if(!hash.Equals(release.Sha256,StringComparison.OrdinalIgnoreCase))throw new IOException("설치 파일 검증에 실패했습니다. 실행하지 않습니다.");
        stream.Position=0;if(stream.ReadByte()!=77||stream.ReadByte()!=90)throw new IOException("Windows 설치 파일이 아닙니다.");
    }
    // [동작 설명] 진행률과 취소를 반영하여 설치 파일을 받고 검증된 로컬 경로를 반환합니다.
    public static async Task<string> Download(AppRelease release,IProgress<int> progress,CancellationToken ct,IProgress<string>? stage=null)
    {
        string folder=Path.Combine(Storage.Root,"updates");Directory.CreateDirectory(folder);
        string target=Path.Combine(folder,$"Setup-{Version.Parse(release.Version)}.exe"),temp=target+"."+Guid.NewGuid().ToString("N")+".partial";
        using var http=Client();HttpResponseMessage? response=null;
        try{
            stage?.Report("업데이트 서버에 연결하고 있어요");
            response=await http.GetAsync(DownloadUrl(release.FileId),HttpCompletionOption.ResponseHeadersRead,ct);response.EnsureSuccessStatusCode();
            if(response.Content.Headers.ContentType?.MediaType=="text/html"){
                stage?.Report("다운로드 승인을 확인하고 있어요");
                string html=await response.Content.ReadAsStringAsync(ct);
                string confirmation=ConfirmationUrl(html,release.FileId);response.Dispose();
                response=await http.GetAsync(confirmation,HttpCompletionOption.ResponseHeadersRead,ct);response.EnsureSuccessStatusCode();
                if(response.Content.Headers.ContentType?.MediaType=="text/html")throw new IOException("Drive에서 다운로드를 제한했습니다. 잠시 후 다시 시도하세요.");
            }
            using(var input=await response.Content.ReadAsStreamAsync(ct))using(var output=File.Create(temp)){
                byte[] buffer=new byte[81920];long total=0;int n;
                while((n=await input.ReadAsync(buffer,ct))>0){total+=n;if(total>release.Size)throw new IOException("예상보다 큰 파일입니다.");await output.WriteAsync(buffer.AsMemory(0,n),ct);progress.Report((int)(total*100/release.Size));}
            }
            stage?.Report("설치 파일을 검증하고 있어요");await ValidateFile(temp,release,ct);File.Move(temp,target,true);return target;
        }finally{response?.Dispose();if(File.Exists(temp))File.Delete(temp);}
    }
    public static string ConfirmationUrl(string html,string id)
    {
        var form=Regex.Match(html,"<form[^>]*id=\"download-form\"[^>]*action=\"https://drive.usercontent.google.com/download\"[^>]*>(.*?)</form>",RegexOptions.Singleline);
        if(!form.Success||html.Length>128*1024)throw new IOException("Drive 다운로드 확인 응답을 읽지 못했습니다.");
        var fields=Regex.Matches(form.Groups[1].Value,"name=\"([^\"]+)\" value=\"([^\"]*)\"").ToDictionary(m=>m.Groups[1].Value,m=>WebUtility.HtmlDecode(m.Groups[2].Value));
        if(fields.GetValueOrDefault("id")!=id||fields.GetValueOrDefault("confirm")!="t"||fields.GetValueOrDefault("export")!="download")throw new IOException("다른 파일의 다운로드 응답입니다.");
        return "https://drive.usercontent.google.com/download?"+string.Join("&",fields.Where(p=>p.Key is "id" or "export" or "confirm" or "uuid").Select(p=>Uri.EscapeDataString(p.Key)+"="+Uri.EscapeDataString(p.Value)));
    }
    public static ProcessStartInfo InstallCommand(string installer,string sha256,string? applicationDirectory=null)
    {
        string Quote(string s)=>"'"+s.Replace("'","''")+"'";
        if(!Regex.IsMatch(sha256,@"^[0-9a-fA-F]{64}$"))throw new IOException("잘못된 설치 파일 검증값입니다.");
        string folder=Path.GetDirectoryName(Path.GetFullPath(installer))!,script=Path.Combine(folder,"apply-update-"+Guid.NewGuid().ToString("N")+".ps1");
        string dir=Path.GetFullPath(applicationDirectory??AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
        using var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("NeoForgeServerManager.Assets.apply-update.ps1")!;
        using var reader=new StreamReader(resource);
        string body="$taskInstaller="+Quote(Path.GetFullPath(installer))+"\n$taskHash="+Quote(sha256)+"\n$taskDirectory="+Quote(dir)+"\n$taskParent="+Environment.ProcessId+"\n"+reader.ReadToEnd();
        File.WriteAllText(script,body,new UTF8Encoding(true));
        var psi=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe")){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0")};
        psi.Environment.Remove("PSModulePath");
        foreach(var arg in new[]{"-NoProfile","-NonInteractive","-STA","-ExecutionPolicy","Bypass","-File",script})psi.ArgumentList.Add(arg);return psi;
    }
    public static async Task CleanCurrentInstaller()
    {
        // Also clean the installer used by older v12 updaters after migrating here.
        string path=Path.Combine(Storage.Root,"updates",$"Setup-{Current}.exe");
        for(int i=0;i<30&&File.Exists(path);i++){
            try{if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)return;File.Delete(path);return;}
            catch(IOException){await Task.Delay(1000);}catch(UnauthorizedAccessException){return;}
        }
    }
    // [동작 설명] 준비된 설치 파일을 실행하는 경로입니다. 단순 다운로드와 실제 설치 시작을 구분합니다.
    public static async Task StartInstaller(string installer,string sha256)
    {
        var command=InstallCommand(installer,sha256);string ready=command.ArgumentList.Last()+".ready";
        using var helper=Process.Start(command)??throw new IOException("업데이트 도우미를 시작하지 못했습니다.");
        for(int i=0;i<150;i++){
            if(File.Exists(ready))return;
            if(helper.HasExited){string reason="";string result=command.ArgumentList.Last()+".result.json";try{if(File.Exists(result)){using var report=JsonDocument.Parse(File.ReadAllText(result));reason=report.RootElement.GetProperty("error").GetString()??"";}}catch(Exception e) when(e is IOException or JsonException or KeyNotFoundException){}throw new IOException("설치 준비에 실패했습니다. 프로그램은 종료하지 않았습니다."+(reason.Length==0?"":"\n"+reason));}
            await Task.Delay(100);
        }
        helper.Kill();throw new IOException("설치 준비 시간이 초과됐습니다. 다시 시도하세요.");
    }

}

public sealed class UpdatesPage:WarmWindow
{
    readonly FlowLayoutPanel rows=new(){Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false};
    readonly Label status=Ui.Note("버전 확인 중…");
    readonly CancellationTokenSource lifetime=new();CancellationTokenSource? download;
    readonly Func<bool> canInstall;readonly Func<string,AppRelease,Task> install;bool busy;
    public UpdatesPage(Func<bool> canInstall,Func<string,AppRelease,Task> install)
    {
        this.canInstall=canInstall;this.install=install;Text="프로그램 업데이트";Size=new(1280,850);
        var header=new FlowLayoutPanel{Dock=DockStyle.Top,Height=90,Padding=new Padding(16)};
        header.Controls.Add(Ui.Action("← 서버목록",Close));header.Controls.Add(Ui.Heading("프로그램 업데이트",24));header.Controls.Add(Ui.Action("다시 확인",async()=>await RefreshFeed()));
        var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=80,Padding=new Padding(16)};status.Width=800;status.Height=55;footer.Controls.Add(status);
        Controls.Add(rows);Controls.Add(footer);Controls.Add(header);rows.Resize+=(_,_)=>{foreach(Control c in rows.Controls)c.Width=Math.Max(600,rows.ClientSize.Width-42);};
        Shown+=async(_,_)=>await RefreshFeed();FormClosing+=(_,e)=>{if(busy){download?.Cancel();e.Cancel=true;status.Text="다운로드를 취소하고 있습니다. 잠시 후 돌아가세요.";}};FormClosed+=(_,_)=>{lifetime.Cancel();};WarmTheme.Apply(this);
    }
    async Task RefreshFeed(){if(busy)return;busy=true;status.Text=$"현재 {AppUpdates.Current} · 최신 포함 최근 {AppUpdates.HistoryLimit}개를 확인합니다.";try{Render(await AppUpdates.Check(lifetime.Token));}catch(Exception e){if(!IsDisposed)status.Text="확인 실패: "+e.Message;}finally{busy=false;}}
    public void Render(AppRelease[] releases)
    {
        foreach(Control c in rows.Controls.Cast<Control>().ToArray())c.Dispose();
        foreach(var r in releases.Take(AppUpdates.HistoryLimit)){
            bool current=Version.Parse(r.Version)==AppUpdates.Current;
            var button=Ui.Action(current?"현재 버전":"다운로드 · 설치",async()=>await GetRelease(r),!current);button.Enabled=!current;
            var card=new ReleaseHistoryCard(AppUpdates.ReleaseTitle(r)+(current?" · 현재 버전":""),r.Notes,button){Width=Math.Max(600,rows.ClientSize.Width-42),Margin=new Padding(14,6,14,6)};rows.Controls.Add(card);
        }
        status.Text=$"현재 {AppUpdates.Current} · 배포된 {releases.Length}개 버전 · 최대 최근 {AppUpdates.HistoryLimit}개 보관";
    }
    async Task GetRelease(AppRelease release)
    {
        if(busy)return;if(!canInstall()){WarmDialog.Show(this,"모든 서버와 진행 중인 작업을 종료한 뒤 업데이트하세요.");return;}
        using(var confirm=new UpdateConfirmationWindow(release))if(WarmDialog.Present(this,confirm)!=DialogResult.OK)return;
        busy=true;download=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        using var progressWindow=new UpdateProgressWindow(()=>download?.Cancel());var owner=TopLevelControl as Form??this;
        progressWindow.Shown+=async(_,_)=>{
        try{var path=await AppUpdates.Download(release,new Progress<int>(n=>{if(!IsDisposed)status.Text=$"다운로드 {n}% · 완료 후 파일 검증";progressWindow.DownloadPercent(n);}),download.Token,new Progress<string>(message=>progressWindow.Stage(message)));
            if(!canInstall())throw new IOException("실행 중인 서버나 작업이 있어 설치를 중단했습니다.");
            progressWindow.Installing();status.Text="설치 준비 중… 완료 후 메인 화면으로 다시 시작합니다.";busy=false;Enabled=false;await install(path,release);
        }catch(Exception e){if(!IsDisposed)status.Text=e is OperationCanceledException?"다운로드 취소됨":e.Message;}finally{progressWindow.Finish();busy=false;if(!IsDisposed)Enabled=true;download?.Dispose();download=null;}
        };
        await Task.Yield();
        progressWindow.ShowDialog(owner);
    }
}



