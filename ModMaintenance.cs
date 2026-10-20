// [소스 안내] 설치된 모드의 업데이트와 서버용 분류입니다. 정확히 식별된 파일만 업데이트하며 기존 파일을 보관한 뒤 교체합니다.
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace NeoForgeServerManager;

public sealed record ModAudit(string Path,string Hash,string Name,string Side,string Evidence,ModPluginService.Result? Project,JsonNode? Current,JsonNode? Latest,string Message)
{
    public bool Disabled=>Path.EndsWith(".disabled",StringComparison.OrdinalIgnoreCase);
    public bool HasUpdate=>Latest!=null;
    public string CurrentVersion=>Current?["version_number"]?.ToString()??LocalModMetadata.Read(Path)?.Version??ModFiles.Metadata(Path).Version;
    public string SideLabel=>Side switch{"required"=>"서버 필요","optional"=>"서버 사용 가능 · 선택","unsupported"=>"클라이언트 전용",_=>"용도 확인 필요"};
}
public sealed record ModAuditReport(ModAudit[] Items,Dictionary<string,string> Snapshot);
public sealed record ModUpdatePlan(DependencyPlan Dependencies,ModAudit[] Replacements);
public sealed record ModFileAction(ModAudit Mod,string Action);

public sealed class ModMaintenance(DownloadClient net)
{
    const string Api="https://api.modrinth.com/v2/";
    static string S(JsonNode? n)=>n?.ToString()??"";
    static bool Disabled(string path)=>path.EndsWith(".disabled",StringComparison.OrdinalIgnoreCase);
    public static bool Compatible(JsonNode version,Profile p)=>version["game_versions"]?.AsArray().Any(v=>S(v)==p.MinecraftVersion)==true&&version["loaders"]?.AsArray().Any(v=>S(v)==ModPluginService.Loader(p))==true;
    static DateTimeOffset Published(JsonNode v)=>DateTimeOffset.TryParse(S(v["date_published"]),out var date)?date:DateTimeOffset.MinValue;
    // 버전별 환경 정보가 있으면 프로젝트 전체 정보보다 우선합니다.
    public static string Side(JsonNode version,JsonNode project)=>S(version["environment"]) switch{
        "client_only" or "singleplayer_only"=>"unsupported",
        "client_and_server" or "server_only" or "server_only_client_optional" or "dedicated_server_only"=>"required",
        "client_only_server_optional" or "client_or_server" or "client_or_server_prefers_both"=>"optional",
        _=>S(project["server_side"]) is "required" or "optional" or "unsupported"?S(project["server_side"]):"unknown"};
    public static string LocalSide(string path)
    {
        try{using var zip=ZipFile.OpenRead(path);var entry=zip.GetEntry("fabric.mod.json");if(entry==null||entry.Length>1024*1024)return "unknown";
            using var stream=entry.Open();var data=JsonNode.Parse(stream);return S(data?["environment"]) switch{"client"=>"unsupported","server"=>"required",_=>"unknown"};}
        catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException){return "unknown";}
    }
    // 이름 유사 검색 결과는 업데이트/삭제의 근거로 쓰지 않습니다. 수동 복사한 파일도 내용 해시로 확인합니다.
    public async Task<ModAuditReport> Scan(Profile p,IProgress<string> progress,CancellationToken ct)
    {
        var snapshot=await Task.Run(()=>ExtensionDependencies.Snapshot(p),ct);
        var rows=new List<ModAudit>();
        foreach(var pair in snapshot)
        {
            ct.ThrowIfCancellationRequested();string path=pair.Key,side=LocalSide(path),evidence=side=="unknown"?"":"JAR의 Fabric 환경 선언";
            string name=ModFiles.Metadata(path).Name,message="배포 파일 미확인 · 업데이트 확인 불가";ModPluginService.Result? project=null;JsonNode? current=null,latest=null;
            progress.Report($"확인 중 {rows.Count+1}/{snapshot.Count} · {name}");
            try
            {
                current=await net.Json(Api+"version_file/"+pair.Value+"?algorithm=sha512",ct);
                string id=S(current["project_id"]);var data=await net.Json(Api+"project/"+Uri.EscapeDataString(id),ct);
                if(current["files"]?.AsArray().Any(f=>string.Equals(S(f?["hashes"]?["sha512"]),pair.Value,StringComparison.OrdinalIgnoreCase))!=true)
                    throw new IOException("배포처 해시 응답이 일치하지 않습니다.");
                string remote=Side(current,data);
                if(side!="unknown"&&remote!="unknown"&&side!=remote){side="unknown";evidence="JAR과 배포처의 환경 정보 불일치";}
                else if(remote!="unknown"){side=remote;evidence="정확한 파일 해시 · Modrinth 배포 정보";}
                project=new(id,S(data["title"]),S(data["slug"]),(string?)data["icon_url"],(string?)data["client_side"]??"unknown",side);
                name=project.Title;InstalledExtensions.Remember(pair.Value,project);
                if(!MinecraftVersions.Valid(p.MinecraftVersion))message="서버의 Minecraft 버전을 먼저 지정하세요.";
                else if(side=="unsupported")message="서버 업데이트 대상에서 제외";
                else
                {
                    string suffix="?loaders="+Uri.EscapeDataString(JsonSerializer.Serialize(new[]{ModPluginService.Loader(p)}))+"&game_versions="+Uri.EscapeDataString(JsonSerializer.Serialize(new[]{p.MinecraftVersion}));
                    var versions=(await net.Json(Api+"project/"+Uri.EscapeDataString(id)+"/version"+suffix,ct)).AsArray();
                    latest=versions.Where(v=>v!=null&&S(v["project_id"])==id&&S(v["version_type"])=="release"&&Compatible(v,p)&&
                        Side(v,data)!="unsupported"&&Published(v)>Published(current)&&Published(current)!=DateTimeOffset.MinValue)
                        .OrderByDescending(v=>Published(v!)).FirstOrDefault();
                    message=latest!=null?"새 버전 있음":Compatible(current,p)?"호환 새 버전 없음":"현재 파일의 게임 버전·로더 확인 필요";
                }
            }
            catch(HttpRequestException e) when(e.StatusCode==HttpStatusCode.NotFound){message="배포 파일 미확인 · 자동 업데이트 불가";}
            catch(Exception e) when(e is HttpRequestException or IOException or JsonException){message="조회 실패 · "+e.Message;latest=null;}
            rows.Add(new(path,pair.Value,name,side,evidence,project,current,latest,message));
        }
        return new(rows.ToArray(),snapshot);
    }
    static void CheckSnapshot(Profile p,Dictionary<string,string> expected)
    {
        var now=ExtensionDependencies.Snapshot(p);
        if(now.Count!=expected.Count||now.Any(x=>!expected.TryGetValue(x.Key,out var hash)||!hash.Equals(x.Value,StringComparison.OrdinalIgnoreCase)))
            throw new IOException("모드 파일이 변경됐어요. 다시 확인한 뒤 적용하세요.");
    }
    public async Task<ModUpdatePlan> PlanUpdates(Profile p,ModAuditReport report,IEnumerable<ModAudit> selected,CancellationToken ct)
    {
        CheckSnapshot(p,report.Snapshot);var updates=selected.DistinctBy(x=>x.Path).ToArray();
        if(updates.Length==0||updates.Any(x=>x.Latest==null||x.Project==null||x.Side=="unsupported"||!report.Items.Contains(x)))throw new IOException("업데이트할 모드를 선택하세요.");
        var replacing=updates.Select(x=>x.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if(updates.GroupBy(x=>x.Project!.Id).Any(g=>g.Count()>1)||updates.Any(u=>report.Items.Count(x=>x.Project?.Id==u.Project!.Id)>1))
            throw new IOException("같은 모드의 파일이 여러 개 있습니다. 중복 파일을 먼저 확인하세요.");
        var files=new Dictionary<string,DependencyFile>();var conflicts=new List<string>();var resolver=new ExtensionDependencies(net);
        foreach(var update in updates)
        {
            ct.ThrowIfCancellationRequested();var plan=await resolver.Plan(p,update.Project!,update.Latest,replacing);
            conflicts.AddRange(plan.Conflicts);
            foreach(var f in plan.Files)
            {
                if(files.TryGetValue(f.Project.Id,out var previous)&&previous.VersionId!=f.VersionId)conflicts.Add(f.Project.Title+": 선택한 모드 사이의 필수 버전이 다릅니다.");
                else files[f.Project.Id]=f;
                if(!f.Installed&&f.Project.ServerSide=="unsupported")conflicts.Add(f.Project.Title+": 서버에 설치할 수 없는 필수 의존성입니다.");
            }
        }
        // 현재 모드가 특정 배포 버전을 고정해서 요구한다면 함께 업데이트하도록 안내합니다.
        foreach(var row in report.Items.Where(x=>!x.Disabled&&!replacing.Contains(x.Path)&&x.Current!=null))
            foreach(var dep in row.Current!["dependencies"]?.AsArray()??[])
                if(S(dep?["dependency_type"])=="required"&&S(dep?["version_id"]) is {Length:>0} pinned)
                {
                    string id=S(dep?["project_id"]);
                    if(id.Length==0)id=S((await net.Json(Api+"version/"+Uri.EscapeDataString(pinned),ct))["project_id"]);
                    if(files.TryGetValue(id,out var planned)&&planned.VersionId!=pinned)conflicts.Add(row.Name+": 기존 모드가 이전 의존 버전을 요구합니다. 해당 모드도 함께 업데이트하세요.");
                }
        foreach(var group in files.Values.Where(f=>!f.Installed).GroupBy(f=>f.Filename,StringComparer.OrdinalIgnoreCase).Where(g=>g.Count()>1))
            conflicts.Add(group.Key+": 다운로드 파일명이 겹칩니다.");
        // 여러 업데이트 계획을 합쳤을 때 생기는 상호 충돌과 비활성 의존성도 다시 확인합니다.
        var future=report.Items.Where(x=>!x.Disabled&&!replacing.Contains(x.Path)&&x.Current!=null).Select(x=>x.Current!).ToList();
        foreach(var file in files.Values.Where(x=>!x.Installed&&!updates.Any(u=>u.Project!.Id==x.Project.Id&&u.Disabled)))
            future.Add(await net.Json(Api+"version/"+Uri.EscapeDataString(file.VersionId),ct));
        foreach(var v in future)foreach(var dep in v["dependencies"]?.AsArray()??[])
        {
            string type=S(dep?["dependency_type"]),id=S(dep?["project_id"]),vid=S(dep?["version_id"]);
            if(id.Length==0&&vid.Length>0)id=S((await net.Json(Api+"version/"+Uri.EscapeDataString(vid),ct))["project_id"]);
            if(type=="incompatible"&&future.Any(x=>S(x["project_id"])==id&&(vid.Length==0||S(x["id"])==vid)))
                conflicts.Add(S(v["project_id"])+": 함께 사용할 수 없는 업데이트가 선택됐습니다 ("+id+").");
            if(type=="required"&&updates.Any(u=>u.Disabled&&u.Project!.Id==id))
                conflicts.Add(id+": 활성 모드가 요구하는 의존성이 비활성 상태입니다.");
        }
        return new(new(files.Values.ToArray(),conflicts.Distinct().ToArray(),report.Snapshot),updates);
    }
    static FileStream LockWorld(Profile p)
    {
        // 월드 잠금 파일이 없으면 별도 보조 잠금만 잡습니다. 검사로 월드 폴더를 새로 만들지 않습니다.
        string world=PropertiesFile.ReadValue(Path.Combine(p.Folder,"server.properties"),"level-name","world");
        string path=ModpackService.SafePath(p.Folder,world.Replace('\\','/')+"/session.lock");
        return File.Exists(path)?new(path,FileMode.Open,FileAccess.Read,FileShare.None):
            new(Path.Combine(p.Folder,".manager-mod-maintenance.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
    }
    static string Content(Profile p)=>Path.Combine(p.Folder,p.Kind is "Paper" or "Purpur"?"plugins":"mods");
    static void NoLinks(string path)
    {
        for(FileSystemInfo? item=new FileInfo(path);item!=null;item=item is FileInfo f?f.Directory:((DirectoryInfo)item).Parent)
            if((File.Exists(item.FullName)||Directory.Exists(item.FullName))&&(item.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("연결된 파일·폴더에는 자동 변경을 적용하지 않습니다.");
    }
    // 서버가 잠긴 상태에서 전체 다운로드를 끝낸 뒤 교체합니다. 실패하면 원본을 되돌리고 성공하면 복구용 사본을 남깁니다.
    public async Task<string> Update(Profile p,ModUpdatePlan plan,Func<bool> canChange,IProgress<string> progress,CancellationToken ct)
    {
        if(!canChange()||!plan.Dependencies.CanInstall)throw new IOException("서버를 종료하고 업데이트 내용을 확인하세요.");
        using var guard=new FileStream(Path.Combine(p.Folder,".manager.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        using var world=LockWorld(p);CheckSnapshot(p,plan.Dependencies.Snapshot);
        string dir=Content(p);NoLinks(dir);NoLinks(Path.Combine(dir,"removed"));
        string stage=Path.Combine(dir,".update-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
        string backup=Path.Combine(dir,"removed","update-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..8]);
        var moved=new List<(string Original,string Backup)>();var added=new List<string>();
        try
        {
            foreach(var file in plan.Dependencies.Files.Where(f=>!f.Installed))
            {
                ct.ThrowIfCancellationRequested();string temp=ModpackService.SafePath(stage,file.Filename);
                progress.Report(file.Project.Title+" 다운로드 중…");await net.FileAsync(file.Url,temp,progress,ct);
                if(!ExtensionDependencies.Hash(temp).Equals(file.Hash,StringComparison.OrdinalIgnoreCase))throw new IOException(file.Project.Title+": 다운로드 해시 불일치");
            }
            ValidateLocalDependencies(p,plan.Dependencies.Snapshot.Keys.Except(plan.Replacements.Select(x=>x.Path),StringComparer.OrdinalIgnoreCase)
                .Concat(plan.Dependencies.Files.Where(x=>!x.Installed&&!plan.Replacements.Any(r=>r.Project!.Id==x.Project.Id&&r.Disabled)).Select(x=>Path.Combine(stage,x.Filename))),plan.Replacements.Select(x=>x.Path));
            CheckSnapshot(p,plan.Dependencies.Snapshot);ct.ThrowIfCancellationRequested();if(!canChange())throw new IOException("서버가 실행 중입니다.");
            foreach(var old in plan.Replacements)
            {
                NoLinks(old.Path);Directory.CreateDirectory(backup);NoLinks(backup);
                string target=Path.Combine(backup,Path.GetFileName(old.Path));File.Move(old.Path,target,false);moved.Add((old.Path,target));
            }
            foreach(var file in plan.Dependencies.Files.Where(f=>!f.Installed))
            {
                var old=plan.Replacements.SingleOrDefault(x=>x.Project!.Id==file.Project.Id);
                string target=ModpackService.SafePath(dir,file.Filename+(old?.Disabled==true?".disabled":""));
                File.Move(Path.Combine(stage,file.Filename),target,false);added.Add(target);InstalledExtensions.Remember(file.Hash,file.Project);
            }
            return backup;
        }
        catch{foreach(string path in added)File.Delete(path);foreach(var pair in moved.AsEnumerable().Reverse())File.Move(pair.Backup,pair.Original,false);throw;}
        finally{Directory.Delete(stage,true);}
    }
    // Forge/NeoForge 모드가 선언한 필수 버전 범위도 검사합니다. 해석할 수 없는 변경된 의존 버전은 보수적으로 중단합니다.
    static void ValidateLocalDependencies(Profile p,IEnumerable<string> future,IEnumerable<string> changed)
    {
        var changedIds=changed.Select(LocalModMetadata.Read).Where(x=>x!=null).Select(x=>x!.Id).ToHashSet();
        var mods=future.Where(x=>!Disabled(x)).Select(LocalModMetadata.Read).Where(x=>x!=null).Cast<LocalModMetadata>().ToArray();
        foreach(var mod in mods)foreach(var dep in mod.Required.Where(d=>changedIds.Contains(d.Id)))
        {
            var candidates=mods.Where(x=>x.Id==dep.Id).ToArray();
            if(candidates.Length!=1||!LocalModMetadata.Accepts(dep.Range,candidates[0].Version))throw new IOException(mod.Id+": "+dep.Id+" 필수 버전 범위 "+dep.Range+"를 확인하세요.");
        }
    }
    public async Task<string> Organize(Profile p,ModAuditReport report,ModFileAction[] actions,Func<bool> canChange,CancellationToken ct)
    {
        if(!canChange())throw new IOException("서버를 종료한 뒤 모드를 정리하세요.");
        if(actions.Length==0||actions.Select(x=>x.Mod.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=actions.Length)throw new IOException("적용할 모드를 선택하세요.");
        foreach(var a in actions)
            if(!report.Items.Contains(a.Mod)||a.Action is not ("enable" or "disable" or "delete")||
                (a.Action=="enable"?(a.Mod.Side is not ("required" or "optional")||!a.Mod.Disabled):a.Mod.Side!="unsupported"))
                throw new IOException("확인된 서버용/클라이언트 전용 모드만 자동 정리할 수 있습니다.");
        using var guard=new FileStream(Path.Combine(p.Folder,".manager.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        using var world=LockWorld(p);CheckSnapshot(p,report.Snapshot);ct.ThrowIfCancellationRequested();
        // 다른 활성 모드의 필수 의존성은 환경 표시에 관계없이 제거하지 않습니다.
        var removed=actions.Where(a=>a.Action!="enable").Select(a=>a.Mod).ToArray();
        var active=report.Items.Where(row=>!removed.Contains(row)&&(!row.Disabled||actions.Any(a=>a.Mod==row&&a.Action=="enable"))).ToArray();
        foreach(var owner in active)
        {
            foreach(var dep in owner.Current?["dependencies"]?.AsArray()??[])
            {
                if(S(dep?["dependency_type"])!="required")continue;
                string id=S(dep?["project_id"]),version=S(dep?["version_id"]);
                if(removed.Any(x=>(id.Length>0&&x.Project?.Id==id)||(version.Length>0&&S(x.Current?["id"])==version)))throw new IOException(owner.Name+": 선택한 모드를 필수로 사용하고 있어 정리할 수 없습니다.");
                if(actions.Any(a=>a.Mod==owner&&a.Action=="enable")&&(id.Length>0||version.Length>0)&&!active.Any(x=>(id.Length==0||x.Project?.Id==id)&&(version.Length==0||S(x.Current?["id"])==version)))
                    throw new IOException(owner.Name+": 먼저 필수 의존성을 설치·활성화하세요.");
            }
            var local=LocalModMetadata.Read(owner.Path);
            foreach(var dep in local?.Required??[])
            {
                if(removed.Any(x=>LocalModMetadata.Read(x.Path)?.Id==dep.Id))throw new IOException(owner.Name+": "+dep.Id+"를 필수로 사용합니다.");
                if(actions.Any(a=>a.Mod==owner&&a.Action=="enable")&&!active.Any(x=>LocalModMetadata.Read(x.Path) is {} m&&m.Id==dep.Id&&LocalModMetadata.Accepts(dep.Range,m.Version)))
                    throw new IOException(owner.Name+": "+dep.Id+" 필수 의존성을 확인하세요.");
            }
        }
        await Task.CompletedTask;
        string dir=Content(p),backup=Path.Combine(dir,"removed","client-only-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..8]);
        var moves=new List<(string From,string To)>();NoLinks(dir);NoLinks(Path.Combine(dir,"removed"));
        foreach(var action in actions)
        {
            NoLinks(action.Mod.Path);
            string target=action.Action switch{"enable"=>action.Mod.Path[..^9],"disable"=>action.Mod.Disabled?action.Mod.Path:action.Mod.Path+".disabled",_=>Path.Combine(backup,Path.GetFileName(action.Mod.Path))};
            if(target==action.Mod.Path)continue;
            if(File.Exists(target))throw new IOException("같은 이름의 대상 파일이 있습니다: "+Path.GetFileName(target));
            moves.Add((action.Mod.Path,target));
        }
        if(moves.Select(x=>x.To).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=moves.Count)throw new IOException("대상 파일명이 겹칩니다.");
        var committed=new List<(string From,string To)>();
        try{foreach(var move in moves){ct.ThrowIfCancellationRequested();if(!canChange())throw new IOException("서버가 실행 중입니다.");Directory.CreateDirectory(Path.GetDirectoryName(move.To)!);NoLinks(Path.GetDirectoryName(move.To)!);File.Move(move.From,move.To,false);committed.Add(move);}return backup;}
        catch{foreach(var move in committed.AsEnumerable().Reverse())File.Move(move.To,move.From,false);throw;}
    }
}
