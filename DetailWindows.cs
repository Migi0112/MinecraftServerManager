// [소스 안내] 설치된 모드의 의존성 확인/선택 설치 창과 서버가 꺼져 있을 때의 운영자 기록 변경 기능입니다.
using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json.Nodes;
namespace NeoForgeServerManager;

public sealed partial class MainForm
{
    // [동작 설명] 설치된 모드를 누르면 열리는 의존성 창입니다. 확인된 계획에서 선택한 누락 모드를 최종 확인 후 설치합니다.
    void ShowModDetails(CatalogItem item)
    {
        string path=Path.Combine(Folder(),ContentFolder,item.Id);var profile=CaptureProfile();
        using var dialog=new WarmWindow{Text=item.Name+" · 필수 의존성",Size=new Size(800,700),MinimumSize=new Size(680,550)};
        var card=new Panel{Dock=DockStyle.Fill,Padding=new Padding(24)};dialog.Controls.Add(card);
        var heading=Ui.Heading(item.Name+" · 필수 의존성",20);heading.Dock=DockStyle.Top;heading.Height=68;
        var list=new DependencyListPanel{Dock=DockStyle.Fill};list.ShowMessage("필수 의존성 확인 중…");string? slug=null;DependencyPlan? shownPlan=null;bool busy=false;
        var official=Ui.Action("공식 페이지 ↗",()=>{if(slug!=null)Process.Start(new ProcessStartInfo("https://modrinth.com/project/"+Uri.EscapeDataString(slug)){UseShellExecute=true});});official.Enabled=false;
        var install=Ui.Action("선택 설치",()=>{},true);install.Enabled=false;var bottom=Ui.Row(official,install,Ui.Action("닫기",dialog.Close));bottom.Dock=DockStyle.Bottom;card.Controls.Add(list);card.Controls.Add(bottom);card.Controls.Add(heading);
        dialog.Shown+=async(_,_)=>{try{using var net=new DownloadClient();var resolver=new ExtensionDependencies(net);var version=await resolver.VersionOf(path,profile);if(list.IsDisposed)return;if(version==null){list.ShowMessage("배포처에서 이 파일을 식별하지 못해 의존성을 확인할 수 없습니다.");return;}string id=version["project_id"]!.ToString();var plan=await resolver.Plan(profile,new(id,item.Name,id),version);if(list.IsDisposed)return;shownPlan=plan;list.SetPlan(plan,true);install.Enabled=plan.CanInstall&&!server.Running;install.Text=server.Running?"서버 종료 후 설치":"선택 설치";slug=plan.Files.FirstOrDefault()?.Project.Slug;official.Enabled=slug!=null;}catch(Exception e){if(!list.IsDisposed)list.ShowMessage("의존성 확인 불가: "+e.Message);}};
                install.Click+=async(_,_)=>{
            if(busy||shownPlan==null)return;
            if(server.Running){WarmDialog.Show(dialog,"서버를 종료한 뒤 설치하세요.");return;}
            var selected=shownPlan.Select(list.SelectedIds);if(!selected.CanInstall){WarmDialog.Show(dialog,"설치할 의존 모드를 선택하세요.");return;}
            using var confirmation=new DependencyWindow("선택한 의존 모드 설치",selected);
            if(WarmDialog.Present(dialog,confirmation)!=DialogResult.OK)return;
            busy=true;install.Enabled=false;
            try{if(server.Running)throw new IOException("서버를 종료한 뒤 설치하세요.");using var net=new DownloadClient();await new ExtensionDependencies(net).Install(profile,selected,new Progress<string>(s=>heading.Text=s));
                shownPlan=shownPlan with{Files=shownPlan.Files.Select(f=>selected.Files.Any(x=>x.Project.Id==f.Project.Id&&(x.Installed||x.Project.ServerSide!="unsupported"))?f with{Installed=true}:f).ToArray(),Snapshot=ExtensionDependencies.Snapshot(profile)};
                list.SetPlan(shownPlan,true);RefreshModsCatalog();heading.Text=item.Name+" · 설치 완료";
            }catch(Exception e){WarmDialog.Show(dialog,e.Message);}finally{busy=false;install.Enabled=shownPlan.CanInstall&&!server.Running;}
        };
        dialog.FormClosing+=(_,e)=>{if(busy)e.Cancel=true;};
        WarmTheme.Apply(dialog);WarmDialog.Present(this,dialog);
    }
}
public static class OfflineRoster
{
    public static void GrantOperator(string folder,string name,int level)
    {
        PlayerData.ValidateName(name);if(level is <1 or >4)throw new ArgumentException("권한 단계는 1~4입니다.");
        using var manager=new FileStream(Path.Combine(folder,".manager.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        string world=PropertiesFile.ReadValue(Path.Combine(folder,"server.properties"),"level-name","world");
        string worldLockPath=Path.Combine(folder,world,"session.lock");using var worldLock=File.Exists(worldLockPath)?new FileStream(worldLockPath,FileMode.Open,FileAccess.Read,FileShare.None):null;
        var rows=PlayerData.Read(folder,"ops.json");
        var item=rows.FirstOrDefault(x=>string.Equals((string?)x?["name"],name,StringComparison.OrdinalIgnoreCase));
        if(item==null)
        {
            var known=PlayerData.Read(folder,"usercache.json").Concat(PlayerData.Read(folder,"whitelist.json")).FirstOrDefault(x=>string.Equals((string?)x?["name"],name,StringComparison.OrdinalIgnoreCase));
            if(!Guid.TryParse((string?)known?["uuid"],out var uuid))throw new IOException("이 서버에 기록된 플레이어가 아닙니다. 서버 실행 후 접속한 플레이어에게 권한을 부여하세요.");
            item=new JsonObject{["uuid"]=uuid.ToString(),["name"]=(string?)known?["name"]??name,["bypassesPlayerLimit"]=false};rows.Add(item);
        }
        item["level"]=level;Storage.AtomicWrite(Path.Combine(folder,"ops.json"),rows.ToJsonString(Storage.Json));
    }
    // 서버가 정지한 경우에만 원자적으로 JSON을 수정합니다. 실행 중에는 서버 명령을 사용합니다.
    public static void SetOperatorLevel(string folder,string name,int level)
    {
        PlayerData.ValidateName(name);if(level is <1 or >4)throw new ArgumentException("권한 단계는 1~4입니다.");
        using var manager=new FileStream(Path.Combine(folder,".manager.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        string world=PropertiesFile.ReadValue(Path.Combine(folder,"server.properties"),"level-name","world");string lockPath=Path.Combine(folder,world,"session.lock");
        using var worldLock=File.Exists(lockPath)?new FileStream(lockPath,FileMode.Open,FileAccess.Read,FileShare.None):null;
        var rows=PlayerData.Read(folder,"ops.json");var item=rows.FirstOrDefault(x=>string.Equals((string?)x?["name"],name,StringComparison.OrdinalIgnoreCase))??throw new IOException("운영자 목록이 변경되었습니다.");item["level"]=level;Storage.AtomicWrite(Path.Combine(folder,"ops.json"),rows.ToJsonString(Storage.Json));
    }
}


