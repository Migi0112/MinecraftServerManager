// [소스 안내] 필수 의존성의 설치 계획과 실행, 확인 목록 UI입니다. 재귀 의존성, 충돌, 기존 파일 변경, 해시·버전 검사 후 설치합니다.
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace NeoForgeServerManager;

public sealed record DependencyFile(ModPluginService.Result Project,string VersionId,string Version,string Filename,string Url,string Hash,bool Installed){public string? ModId {get;init;} public string? Range {get;init;}};
public sealed record DependencyPlan(DependencyFile[] Files,string[] Conflicts,Dictionary<string,string> Snapshot)
{
    public Dictionary<string,string[]> Required {get;init;}=new();
    public DependencyPlan Select(IEnumerable<string> selected){
        var ids=selected.ToHashSet();void Include(string id){if(Required.TryGetValue(id,out var children))foreach(var child in children)if(ids.Add(child))Include(child);}
        foreach(var id in ids.ToArray())Include(id);
        return this with{Files=Files.Where(f=>f.Installed||ids.Contains(f.Project.Id)).ToArray()};
    }
    public bool CanInstall => Conflicts.Length==0&&Files.Any(f=>!f.Installed&&f.Project.ServerSide!="unsupported");
    public string Summary => string.Join("\n\n",Files.Select(f=>$"{f.Project.Title} · {f.Version}\n{(f.Installed?"설치됨":"추가 설치")}"))+ (Conflicts.Length==0?"":"\n\n설치 보류\n"+string.Join("\n",Conflicts));
}

// Resolve the complete plan before consent; never replace an existing JAR implicitly.
public sealed class ExtensionDependencies(DownloadClient net)
{
    const string Api="https://api.modrinth.com/v2/";
    public static string Hash(string path){using var input=File.OpenRead(path);return Convert.ToHexString(SHA512.HashData(input)).ToLowerInvariant();}
    public static Dictionary<string,string> Snapshot(Profile p)=>InstalledExtensions.Files(p).ToDictionary(Path.GetFullPath,Hash,StringComparer.OrdinalIgnoreCase);
    // [동작 설명] 해시로 정확한 배포 버전을 먼저 찾습니다. 404이고 프로필이 있으면 로컬 JAR 선언을 읽는 보완 경로로 넘어갑니다.
    public async Task<JsonNode?> VersionOf(string file,Profile? profile=null)
    {
        try{return await net.Json(Api+"version_file/"+await Task.Run(()=>Hash(file))+"?algorithm=sha512");}
        catch(HttpRequestException e) when(e.StatusCode==System.Net.HttpStatusCode.NotFound){return profile==null?null:await LocalVersion(file,profile);}
    }
    // [동작 설명] 파일 안의 버전/의존성으로 설치된 파일 정보를 구성합니다. 원격 배포판과 동일한 해시라고 가정하지 않습니다.
    async Task<JsonNode?> LocalVersion(string file,Profile p)
    {
        var metadata=LocalModMetadata.Read(file);if(metadata==null)return null;
        var project=await LocalModMetadata.Project(net,metadata.Id);if(project==null)return null;
        var dependencies=new JsonArray();
        foreach(var required in metadata.Required){
            var dep=await LocalModMetadata.Project(net,required.Id);
            dependencies.Add(new JsonObject{["project_id"]=dep?.Id,["dependency_type"]="required",["local_mod_id"]=required.Id,["local_range"]=required.Range});
        }
        return new JsonObject{["id"]="local-"+Hash(file),["project_id"]=project.Id,["version_number"]=metadata.Version,["local_metadata"]=true,
            ["game_versions"]=new JsonArray(p.MinecraftVersion),["loaders"]=new JsonArray(ModPluginService.Loader(p)),["dependencies"]=dependencies,
            ["files"]=new JsonArray(new JsonObject{["filename"]=Path.GetFileName(file).Replace(".disabled",""),["url"]="",["hashes"]=new JsonObject{["sha512"]=Hash(file)}})};
    }
    bool Compatible(JsonNode v,Profile p)=>v["game_versions"]!.AsArray().Any(x=>x!.ToString()==p.MinecraftVersion)&&v["loaders"]!.AsArray().Any(x=>x!.ToString()==ModPluginService.Loader(p));
    // [동작 설명] 다운로드 전에 필요한 파일과 충돌, 기존 파일 해시 목록을 계산합니다. 이 단계에서는 서버 폴더를 변경하지 않습니다.
    public async Task<DependencyPlan> Plan(Profile p,ModPluginService.Result project,JsonNode? exact=null,IReadOnlySet<string>? replacing=null)
    {
        if(!MinecraftVersions.Valid(p.MinecraftVersion))throw new IOException("설치 전에 Minecraft 버전을 지정하세요.");
        var snapshot=await Task.Run(()=>Snapshot(p));var local=new Dictionary<string,List<(string Path,JsonNode Version)>>();
        foreach(var path in snapshot.Keys){if(replacing?.Contains(path)==true)continue;var v=await VersionOf(path,p);if(v==null)continue;string id=v["project_id"]!.ToString();if(!local.ContainsKey(id))local[id]=[];local[id].Add((path,v));}
        var chosen=new Dictionary<string,DependencyFile>();var versions=new Dictionary<string,JsonNode>();var conflicts=new List<string>();
        void CheckLocalRange(string id,string modId,string? range){
            if(!local.TryGetValue(id,out var existing))return;
            foreach(var entry in existing){var metadata=LocalModMetadata.Read(entry.Path);if(metadata==null||metadata.Id!=modId||!LocalModMetadata.Accepts(range??"",metadata.Version))conflicts.Add(modId+": 설치된 파일의 필수 버전 범위 "+range+"를 확인하세요.");}
        }
        async Task Visit(string id,string? pinned=null,JsonNode? supplied=null,string? modId=null,string? range=null)
        {
            if(chosen.Count>=100)throw new IOException("의존 모드가 너무 많습니다. 제작자 안내를 확인하세요.");
            if(chosen.TryGetValue(id,out var previous)){
                if(pinned!=null&&previous.VersionId!=pinned)conflicts.Add(previous.Project.Title+": 서로 다른 필수 버전이 요구됩니다.");
                if(modId!=null){
                    CheckLocalRange(id,modId,range);
                    if(previous.Range!=null&&previous.Range!=range)conflicts.Add(previous.Project.Title+": 서로 다른 필수 버전 범위를 확인해야 합니다.");
                    else chosen[id]=previous with{ModId=modId,Range=range};
                    if((bool?)versions[id]["local_metadata"]==true&&!LocalModMetadata.Accepts(range??"",previous.Version))conflicts.Add(previous.Project.Title+": 설치된 버전이 필수 범위에 맞지 않습니다.");
                }
                return;
            }
            JsonNode? v=supplied;
            if(v==null&&pinned!=null)v=await net.Json(Api+"version/"+Uri.EscapeDataString(pinned));
            if(v==null&&local.TryGetValue(id,out var existing))v=existing.Select(x=>x.Version).FirstOrDefault(x=>Compatible(x,p));
            if(v==null){string suffix="?loaders="+Uri.EscapeDataString(JsonSerializer.Serialize(new[]{ModPluginService.Loader(p)}))+"&game_versions="+Uri.EscapeDataString(JsonSerializer.Serialize(new[]{p.MinecraftVersion}));v=(await net.Json(Api+"project/"+Uri.EscapeDataString(id)+"/version"+suffix)).AsArray().FirstOrDefault(x=>x!["version_type"]!.ToString()=="release");}
            if(v==null)throw new IOException(id+": 호환되는 정식 배포 파일이 없습니다.");
            string actualId=v["project_id"]!.ToString();if(actualId!=id)throw new IOException("의존성 프로젝트 정보가 일치하지 않습니다.");
            var metadata=await net.Json(Api+"project/"+Uri.EscapeDataString(id));var info=new ModPluginService.Result(id,metadata["title"]!.ToString(),metadata["slug"]!.ToString(),(string?)metadata["icon_url"],(string?)metadata["client_side"]??"unknown",ModMaintenance.Side(v,metadata));
            if(!Compatible(v,p))conflicts.Add(info.Title+": 서버 버전 또는 로더가 맞지 않습니다.");
            var file=v["files"]!.AsArray().FirstOrDefault(x=>(bool?)x!["primary"]==true)??v["files"]!.AsArray().FirstOrDefault()??throw new IOException("배포 파일이 없습니다.");
            string filename=file["filename"]!.ToString(),hash=file["hashes"]!["sha512"]!.ToString();
            if(filename!=Path.GetFileName(filename)||filename.IndexOfAny(Path.GetInvalidFileNameChars())>=0||!filename.EndsWith(".jar",StringComparison.OrdinalIgnoreCase)||!System.Text.RegularExpressions.Regex.IsMatch(hash,"^[a-fA-F0-9]{128}$"))throw new IOException("올바르지 않은 배포 파일 정보입니다.");
            bool installed=false;
            if(local.TryGetValue(id,out var found)){
                installed=found.Count==1&&found[0].Version["id"]!.ToString()==v["id"]!.ToString()&&!found[0].Path.EndsWith(".disabled",StringComparison.OrdinalIgnoreCase);
                if(!installed)conflicts.Add(info.Title+": 기존 버전 불일치·중복 또는 비활성 파일이 있습니다. 모드 관리에서 확인하세요.");
            }
            chosen[id]=new(info,v["id"]!.ToString(),v["version_number"]!.ToString(),filename,file["url"]!.ToString(),hash,installed);versions[id]=v; if(modId!=null){chosen[id]=chosen[id] with{ModId=modId,Range=range};CheckLocalRange(id,modId,range);}
            foreach(var d in v["dependencies"]!.AsArray().Where(d=>(string?)d!["dependency_type"]=="required")){
                string? depId=(string?)d!["project_id"],versionId=(string?)d["version_id"];JsonNode? dep=null;
                if(versionId!=null){dep=await net.Json(Api+"version/"+Uri.EscapeDataString(versionId));depId=(string?)dep["project_id"];}
                if(depId==null){conflicts.Add(info.Title+": 외부 의존 파일을 자동 확인할 수 없습니다. 원본 페이지를 확인하세요.");continue;}
                d!["project_id"]=depId;await Visit(depId,versionId,dep,(string?)d["local_mod_id"],(string?)d["local_range"]);
            }
        }
        await Visit(project.Id,exact?["id"]?.ToString(),exact);
        foreach(var pair in versions.Concat(local.Where(x=>!versions.ContainsKey(x.Key)).SelectMany(x=>x.Value.Select(v=>new KeyValuePair<string,JsonNode>(x.Key,v.Version)))))foreach(var d in pair.Value["dependencies"]!.AsArray().Where(d=>(string?)d!["dependency_type"]=="incompatible")){
            string? id=(string?)d!["project_id"],vid=(string?)d["version_id"];
            if(id==null&&vid!=null)id=(string?)(await net.Json(Api+"version/"+Uri.EscapeDataString(vid)))["project_id"];
            if(id!=null&&((chosen.TryGetValue(id,out var planned)&&(vid==null||planned.VersionId==vid))||(local.TryGetValue(id,out var installed)&&installed.Any(x=>vid==null||x.Version["id"]!.ToString()==vid))))conflicts.Add((chosen.TryGetValue(pair.Key,out var owner)?owner.Project.Title:pair.Key)+": 함께 사용할 수 없는 모드가 있습니다 ("+id+").");
        }
        foreach(var group in chosen.Values.GroupBy(x=>x.Filename,StringComparer.OrdinalIgnoreCase).Where(g=>g.Count()>1))conflicts.Add(group.Key+": 서로 다른 프로젝트의 파일명이 같습니다.");
        foreach(var f in chosen.Values.Where(f=>!f.Installed))if(snapshot.Keys.Any(path=>replacing?.Contains(path)!=true&&Path.GetFileName(path).Equals(f.Filename,StringComparison.OrdinalIgnoreCase)))conflicts.Add(f.Filename+": 같은 이름의 기존 파일이 있습니다.");
        return new(chosen.Values.ToArray(),conflicts.Distinct().ToArray(),snapshot){Required=versions.ToDictionary(x=>x.Key,x=>x.Value["dependencies"]!.AsArray().Where(d=>(string?)d!["dependency_type"]=="required").Select(d=>(string?)d!["project_id"]).Where(id=>id!=null).Cast<string>().ToArray())};
    }
    // [동작 설명] 확인된 계획을 실행합니다. 잠금 → 기존 파일 재확인 → 임시 다운로드/검증 → 최종 이동 순서이며 실패 시 이번 작업 파일을 정리합니다.
    public async Task Install(Profile p,DependencyPlan plan,IProgress<string> progress)
    {
        if(!plan.CanInstall)throw new IOException("설치할 수 없는 계획입니다. 의존성 상태를 다시 확인하세요.");
        using var guard=new FileStream(Path.Combine(p.Folder,".manager.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        string world=PropertiesFile.ReadValue(Path.Combine(p.Folder,"server.properties"),"level-name","world");string lockPath=Path.Combine(p.Folder,world,"session.lock");using var worldGuard=File.Exists(lockPath)?new FileStream(lockPath,FileMode.Open,FileAccess.Read,FileShare.None):null;
        void CheckSnapshot(){var now=Snapshot(p);if(now.Count!=plan.Snapshot.Count||now.Any(x=>!plan.Snapshot.TryGetValue(x.Key,out var hash)||hash!=x.Value))throw new IOException("설치된 파일이 변경되었습니다. 설치 내용을 다시 확인하세요.");}
        CheckSnapshot();string dir=Path.Combine(p.Folder,p.Kind is "Paper" or "Purpur"?"plugins":"mods");Directory.CreateDirectory(dir);string stage=Path.Combine(dir,".install-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);var moved=new List<string>();
        try{
            foreach(var f in plan.Files.Where(x=>!x.Installed&&x.Project.ServerSide!="unsupported")){progress.Report(f.Project.Title+" 다운로드 중");string path=Path.Combine(stage,f.Filename);await net.FileAsync(f.Url,path,progress,CancellationToken.None);if(!Hash(path).Equals(f.Hash,StringComparison.OrdinalIgnoreCase))throw new IOException(f.Project.Title+": 파일 무결성 검사 실패");if(f.ModId!=null){var metadata=LocalModMetadata.Read(path);if(metadata==null||metadata.Id!=f.ModId||!LocalModMetadata.Accepts(f.Range??"",metadata.Version))throw new IOException(f.Project.Title+": 필수 버전 범위를 확인할 수 없거나 맞지 않습니다 ("+f.Range+").");}}
            CheckSnapshot();foreach(var f in plan.Files.Where(x=>!x.Installed&&x.Project.ServerSide!="unsupported")){string target=Path.Combine(dir,f.Filename);File.Move(Path.Combine(stage,f.Filename),target,false);moved.Add(target);InstalledExtensions.Remember(f.Hash,f.Project);}
        }catch{foreach(var path in moved)File.Delete(path);throw;}finally{Directory.Delete(stage,true);}
    }
}

public sealed class DependencyListPanel:BufferedDependencyPanel
{
    readonly ModArtwork artwork=new();
    readonly Dictionary<string,CheckBox> selection=new();
    public string[] SelectedIds=>selection.Where(x=>x.Value.Checked).Select(x=>x.Key).ToArray();
    public DependencyListPanel(){AutoScroll=true;BackColor=WarmTheme.Background;}
    public void ShowMessage(string message){ClearRows();var note=Ui.Note(message);note.SetBounds(12,12,Math.Max(220,ClientSize.Width-32),140);Controls.Add(note);}
    void ClearRows(){foreach(Control c in Controls.Cast<Control>().ToArray())c.Dispose();}
    // [동작 설명] 의존성 파일을 카드로 보여줍니다. 설치된 항목과 클라이언트 전용 항목은 서버 설치 선택에서 제외합니다.
    public void SetPlan(DependencyPlan plan,bool selectable=false)
    {
        ClearRows();selection.Clear();int y=4;
        foreach(var file in plan.Files){
            var row=new DependencyCard{Left=0,Top=y,Width=Math.Max(300,ClientSize.Width-24),Height=144,Padding=new Padding(12)};
            var image=new PictureBox{SizeMode=PictureBoxSizeMode.Zoom,Size=new Size(70,70),Location=new Point(14,18),Name="OfficialDependencyImage"};
            var name=Ui.Heading(file.Project.Title,14);name.AutoSize=false;name.Margin=Padding.Empty;name.SetBounds(102,12,Math.Max(120,row.Width-168),40);
            var state=Ui.Note(file.Version+" · "+(file.Installed?"설치됨":file.Project.ServerSide=="unsupported"?"클라이언트에 설치":"설치 필요"));state.AutoSize=false;state.SetBounds(102,58,Math.Max(120,row.Width-120),34);
            row.Controls.AddRange([image,name,state]);row.Resize+=(_,_)=>{name.Width=state.Width=Math.Max(120,row.Width-168);};Controls.Add(row);y+=156;
                        var badges=new ModEnvironmentBadges{Project=file.Project};badges.SetBounds(102,100,Math.Min(330,Math.Max(180,row.Width-124)),28);row.Controls.Add(badges);
            row.Resize+=(_,_)=>badges.Width=Math.Min(330,Math.Max(180,row.Width-124));
            if(selectable&&!file.Installed&&file.Project.ServerSide!="unsupported"){
                var pick=new DependencySelectionBox{Checked=true,Location=new Point(row.Width-48,14),Anchor=AnchorStyles.Top|AnchorStyles.Right};selection[file.Project.Id]=pick;row.Controls.Add(pick);
                void Highlight(){row.Selected=pick.Checked;var color=pick.Checked?WarmTheme.Peach:WarmTheme.Surface;foreach(Control child in row.Controls)child.BackColor=color;row.Invalidate();}
                pick.CheckedChanged+=(_,_)=>Highlight();row.Click+=(_,_)=>pick.Checked=!pick.Checked;name.Click+=(_,_)=>pick.Checked=!pick.Checked;state.Click+=(_,_)=>pick.Checked=!pick.Checked;Highlight();pick.BringToFront();
            }
            _=artwork.Load(file.Project,()=>{if(!image.IsDisposed)image.Image=artwork.Find(file.Project.Id);});
        }
        if(plan.Conflicts.Length>0){var warning=Ui.Note("설치 확인 필요\n"+string.Join("\n",plan.Conflicts));warning.SetBounds(12,y,Math.Max(220,ClientSize.Width-32),Math.Max(100,50+plan.Conflicts.Length*52));Controls.Add(warning);}
    }
    protected override void OnResize(EventArgs e){base.OnResize(e);foreach(Control c in Controls)c.Width=Math.Max(220,ClientSize.Width-24);}
    bool arranging;
    protected override void OnLayout(LayoutEventArgs e){
        if(arranging)return;
        arranging=true;
        try{foreach(Control c in Controls){c.Left=0;c.Width=Math.Max(220,ClientSize.Width-24);}base.OnLayout(e);}
        finally{arranging=false;}
    }
    protected override void Dispose(bool disposing){if(disposing)artwork.Dispose();base.Dispose(disposing);}
}
public class BufferedDependencyPanel:Panel
{
    public BufferedDependencyPanel(){DoubleBuffered=true;SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
}
public sealed class DependencyWindow:WarmWindow
{
    public DependencyWindow(string title,DependencyPlan plan,bool inspection=false)
    {
        Text=title;Size=new(800,700);MinimumSize=new(680,550);var body=new Panel{Dock=DockStyle.Fill,Padding=new Padding(24)};Controls.Add(body);
        var heading=Ui.Heading(title);heading.Dock=DockStyle.Top;heading.Height=60;
        var list=new DependencyListPanel{Dock=DockStyle.Fill};
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=58,FlowDirection=FlowDirection.RightToLeft};var close=Ui.Action(inspection?"닫기":"취소",()=>{DialogResult=DialogResult.Cancel;Close();});actions.Controls.Add(close);
        if(!inspection){var install=Ui.Action("함께 설치",()=>{DialogResult=DialogResult.OK;Close();},true);install.Enabled=plan.CanInstall;actions.Controls.Add(install);}
        body.Controls.Add(list);body.Controls.Add(actions);body.Controls.Add(heading);list.SetPlan(plan);WarmTheme.Apply(this);
    }
}



