using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using NeoForgeServerManager;

static class ModMaintenanceChecks
{
    static T Wait<T>(Task<T> task){while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(5);}return task.GetAwaiter().GetResult();}
    static string Hash(byte[] bytes)=>Convert.ToHexString(SHA512.HashData(bytes)).ToLowerInvariant();
    static byte[] Jar(string id,string version,string env="*",string? dependency=null)
    {
        using var memory=new MemoryStream();
        using(var zip=new ZipArchive(memory,ZipArchiveMode.Create,true)){
            using var writer=new StreamWriter(zip.CreateEntry("fabric.mod.json").Open());
            writer.Write(JsonSerializer.Serialize(new{id,version,environment=env,depends=dependency==null?new Dictionary<string,string>():new Dictionary<string,string>{{dependency,"*"}}}));
        }
        return memory.ToArray();
    }
    sealed class Api:HttpMessageHandler
    {
        public readonly Dictionary<string,JsonNode> Projects=[];
        public readonly Dictionary<string,JsonNode> Versions=[];
        public readonly Dictionary<string,byte[]> Downloads=[];
        public readonly Dictionary<string,JsonNode> Hashes=[];
        public bool Corrupt;
        public void Add(string id,string side,string version,string date,byte[] bytes,string game="1.21.1",string type="release",JsonArray? deps=null)
        {
            Projects[id]=JsonSerializer.SerializeToNode(new{id,title=id,slug=id,server_side=side,client_side="required"})!;
            string vid=id+version,name=vid+".jar";
            var v=JsonSerializer.SerializeToNode(new{id=vid,project_id=id,version_number=version,date_published=date,game_versions=new[]{game},loaders=new[]{"fabric"},version_type=type,
                files=new[]{new{primary=true,filename=name,url="https://cdn.modrinth.com/"+name,hashes=new{sha512=Hash(bytes)}}}})!;
            v["dependencies"]=deps??new JsonArray();Versions[vid]=v;Hashes[Hash(bytes)]=v;Downloads["/"+name]=bytes;
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();var uri=req.RequestUri!;string path=uri.AbsolutePath;JsonNode? value=null;byte[]? bytes=null;
            if(uri.Host=="cdn.modrinth.com"&&Downloads.TryGetValue(path,out var data))bytes=Corrupt?[1,2]:data;
            else if(path.StartsWith("/v2/version_file/"))Hashes.TryGetValue(path[17..],out value);
            else if(path.StartsWith("/v2/version/"))Versions.TryGetValue(path[12..],out value);
            else if(path.StartsWith("/v2/project/")){
                string id=path[12..];if(id.EndsWith("/version")){id=id[..^8];value=new JsonArray(Versions.Values.Where(x=>x["project_id"]!.ToString()==id).Select(x=>x.DeepClone()).ToArray());}
                else Projects.TryGetValue(id,out value);
            }
            else if(path=="/v2/search")value=new JsonObject{["hits"]=new JsonArray()};
            return Task.FromResult(new HttpResponseMessage(bytes!=null||value!=null?HttpStatusCode.OK:HttpStatusCode.NotFound){Content=bytes!=null?new ByteArrayContent(bytes):new StringContent(value?.ToJsonString()??"missing")});
        }
    }
    public static void Run(string root,Action<bool,string> check)
    {
        using var api=new Api();using var net=new DownloadClient(handler:api);var service=new ModMaintenance(net);var progress=new Progress<string>();
        Profile Profile(string name){var p=new Profile{Folder=Path.Combine(root,name),Kind="Fabric",MinecraftVersion="1.21.1"};Directory.CreateDirectory(Path.Combine(p.Folder,"mods"));return p;}
        string Put(Profile p,string filename,byte[] bytes){string path=Path.Combine(p.Folder,"mods",filename);File.WriteAllBytes(path,bytes);return path;}
        ModAuditReport Scan(Profile p)=>Wait(service.Scan(p,progress,default));
        void Reject(Func<Task> action,string message){bool rejected=false;try{Wait(action().ContinueWith(t=>{t.GetAwaiter().GetResult();return true;}));}catch{rejected=true;}check(rejected,message);}
        byte[] old=Jar("core","1.0.0"),next=Jar("core","2.0.0");
        api.Add("core","required","1.0.0","2025-01-01T00:00:00Z",old);
        api.Add("core","required","2.0.0","2025-02-01T00:00:00Z",next);
        api.Add("core","required","3.0.0","2025-03-01T00:00:00Z",Jar("core","3.0.0"),"1.22");
        api.Add("core","required","4.0.0","2025-04-01T00:00:00Z",Jar("core","4.0.0"),type:"beta");
        var p=Profile("update");string source=Put(p,"renamed-by-user.jar",old);File.WriteAllText(Path.Combine(p.Folder,"server.properties"),"motd=keep");
        var report=Scan(p);check(report.Items.Single().HasUpdate&&report.Items[0].Latest!["version_number"]!.ToString()=="2.0.0","manual renamed JAR matched by exact hash; wrong-game and beta releases excluded");
        var plan=Wait(service.PlanUpdates(p,report,report.Items,default));check(plan.Dependencies.CanInstall,"update replaces known old version without duplicate conflict");
        api.Corrupt=true;Reject(()=>service.Update(p,plan,()=>true,progress,default),"corrupt download rejected");check(File.ReadAllBytes(source).SequenceEqual(old),"corrupt update keeps original");
        api.Corrupt=false;
        Reject(()=>service.Update(p,plan,()=>false,progress,default),"running server rejected");
        using(var held=new FileStream(Path.Combine(p.Folder,".manager.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
            Reject(()=>service.Update(p,plan,()=>true,progress,default),"server manager lock prevents mutation");
        Directory.CreateDirectory(Path.Combine(p.Folder,"world"));
        using(var held=new FileStream(Path.Combine(p.Folder,"world","session.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
            Reject(()=>service.Update(p,plan,()=>true,progress,default),"external world session lock prevents mutation");
        string backup=Wait(service.Update(p,plan,()=>true,progress,default));
        check(!File.Exists(source)&&File.ReadAllBytes(Path.Combine(p.Folder,"mods","core2.0.0.jar")).SequenceEqual(next),"update commits verified new JAR");
        check(File.ReadAllBytes(Path.Combine(backup,"renamed-by-user.jar")).SequenceEqual(old)&&File.ReadAllText(Path.Combine(p.Folder,"server.properties"))=="motd=keep","old JAR recoverable and server settings untouched");
        check(!Scan(p).Items[0].HasUpdate,"new version no longer offered");
        var disabled=Profile("disabled");Put(disabled,"core.jar.disabled",old);var dr=Scan(disabled);
        Wait(service.Update(disabled,Wait(service.PlanUpdates(disabled,dr,dr.Items,default)),()=>true,progress,default));
        check(File.Exists(Path.Combine(disabled.Folder,"mods","core2.0.0.jar.disabled")),"update preserves disabled state");
        var cancelled=Profile("cancelled");var cancelledPath=Put(cancelled,"old.jar",old);var cr=Scan(cancelled);var cplan=Wait(service.PlanUpdates(cancelled,cr,cr.Items,default));
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();Reject(()=>service.Update(cancelled,cplan,()=>true,progress,cancel.Token),"cancelled update cannot commit");}
        check(File.ReadAllBytes(cancelledPath).SequenceEqual(old),"cancellation keeps original bytes");
        var rollback=Profile("rollback");var rollbackPath=Put(rollback,"old.jar",old);var rr=Scan(rollback);var rp=Wait(service.PlanUpdates(rollback,rr,rr.Items,default));
        Directory.CreateDirectory(Path.Combine(rollback.Folder,"mods","core2.0.0.jar"));
        Reject(()=>service.Update(rollback,rp,()=>true,progress,default),"commit target collision rejected");
        check(File.ReadAllBytes(rollbackPath).SequenceEqual(old),"commit failure restores original from backup");
        var duplicate=Profile("duplicate");Put(duplicate,"one.jar",old);Put(duplicate,"two.jar",old);var duplicateReport=Scan(duplicate);
        Reject(()=>service.PlanUpdates(duplicate,duplicateReport,[duplicateReport.Items[0]],default),"duplicate installed project cannot be updated ambiguously");
        var pinned=Profile("pinned");Put(pinned,"core.jar",old);var consumer=Jar("consumer","1.0.0");
        api.Add("consumer","required","1.0.0","2025-01-01T00:00:00Z",consumer,deps:new JsonArray(new JsonObject{["project_id"]="core",["version_id"]="core1.0.0",["dependency_type"]="required"}));
        Put(pinned,"consumer.jar",consumer);var pr=Scan(pinned);
        check(!Wait(service.PlanUpdates(pinned,pr,pr.Items.Where(x=>x.HasUpdate),default)).Dependencies.CanInstall,"existing pinned dependency prevents incompatible update");
        var stale=Profile("stale");var stalePath=Put(stale,"core.jar",old);var sr=Scan(stale);var sp=Wait(service.PlanUpdates(stale,sr,sr.Items,default));File.WriteAllBytes(stalePath,next);
        Reject(()=>service.Update(stale,sp,()=>true,progress,default),"changed local snapshot cannot be overwritten");
        var organize=Profile("organize");
        var client=Jar("client","1.0.0","client");Put(organize,"client.jar",client);
        Put(organize,"unknown.jar",Jar("unknown","1.0.0"));
        Put(organize,"server.jar.disabled",Jar("server","1.0.0","server"));
        var optional=Jar("optional","1.0.0");api.Add("optional","optional","1.0.0","2025-01-01T00:00:00Z",optional);Put(organize,"optional.jar",optional);
        var audit=Scan(organize);
        ModAudit Find(string id)=>audit.Items.Single(x=>Path.GetFileName(x.Path).StartsWith(id));
        check(Find("client").Side=="unsupported"&&Find("server").Side=="required","manual Fabric client/server declarations classified without provider match");
        check(Find("unknown").Side=="unknown"&&!Find("unknown").HasUpdate&&Find("optional").Side=="optional","unknown preserved and server optional distinct from client-only");
        Reject(()=>service.Organize(organize,audit,[new(Find("optional"),"delete")],()=>true,default),"server optional cannot be auto-deleted");
        Reject(()=>service.Organize(organize,audit,[new(Find("unknown"),"disable")],()=>true,default),"unknown cannot be auto-disabled");
        Wait(service.Organize(organize,audit,[new(Find("client"),"disable"),new(Find("server"),"enable")],()=>true,default));
        check(File.Exists(Path.Combine(organize.Folder,"mods","client.jar.disabled"))&&File.Exists(Path.Combine(organize.Folder,"mods","server.jar")),"selected client disabled and selected server enabled");
        audit=Scan(organize);string removed=Wait(service.Organize(organize,audit,[new(Find("client"),"delete")],()=>true,default));
        check(File.Exists(Path.Combine(removed,"client.jar.disabled"))&&!File.Exists(Path.Combine(organize.Folder,"mods","client.jar.disabled")),"delete retains recoverable original in removed");
        var dependency=Profile("dependency");Put(dependency,"client.jar",client);Put(dependency,"owner.jar",Jar("owner","1.0.0","server","client"));var depReport=Scan(dependency);
        Reject(()=>service.Organize(dependency,depReport,[new(depReport.Items.Single(x=>x.Side=="unsupported"),"disable")],()=>true,default),"local Fabric required dependency cannot be disabled");
        var conflict=Jar("conflict","1.0.0","client");api.Add("conflict","required","1.0.0","2025-01-01T00:00:00Z",conflict);var cp=Profile("conflict");Put(cp,"conflict.jar",conflict);
        check(Scan(cp).Items.Single().Side=="unknown","conflicting provider and JAR declarations require review");
        var enable=Profile("enable-missing");Put(enable,"owner.jar.disabled",Jar("owner","1.0.0","server","missing"));var er=Scan(enable);
        Reject(()=>service.Organize(enable,er,[new(er.Items[0],"enable")],()=>true,default),"missing dependency blocks enable");
        using(var window=new ModMaintenanceWindow(organize,true,()=>true,new DownloadClient(handler:new Api()))){
            window.Opacity=0;window.ShowInTaskbar=false;window.Show();Application.DoEvents();window.ShowReport(audit);
            window.Size=new(900,700);window.PerformLayout();Application.DoEvents();
            var buttons=All(window).OfType<Button>().Where(x=>x.Visible).ToArray();
            check(buttons.Where(x=>x.Text.Contains("모드")||x.Text=="서버용 활성화").All(x=>x.Parent!.ClientRectangle.Contains(x.Bounds)),"cleanup actions fit compact window");
            using var image=new Bitmap(window.Width,window.Height);window.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(root,"maintenance-cleanup.png"));
            window.Close();
        }
        check(LocalModMetadata.Accepts("fabric:>=1.0.0 <3.0.0","2.0.0")&&!LocalModMetadata.Accepts("fabric:>=3.0.0","2.0.0"),"Fabric dependency ranges checked conservatively");
        // 서비스 직접 호출이 아니라 실제 화면 버튼으로 선택 → 확인 → 적용까지 검증합니다.
        var uiProfile=Profile("ui-actions");Put(uiProfile,"old.jar",old);Put(uiProfile,"unknown.jar",Jar("unknown-ui","1.0.0"));
        using var uiApi=new Api();uiApi.Add("core","required","1.0.0","2025-01-01T00:00:00Z",old);uiApi.Add("core","required","2.0.0","2025-02-01T00:00:00Z",next);
        using var ui=new ModMaintenanceWindow(uiProfile,false,()=>true,new DownloadClient(handler:uiApi));
        ui.ShowInTaskbar=false;ui.Show();
        Button Button(string text)=>All(ui).OfType<Button>().Single(b=>b.Text==text);
        void Until(Func<bool> done){var deadline=DateTime.UtcNow.AddSeconds(15);while(!done()&&DateTime.UtcNow<deadline){Application.DoEvents();Thread.Sleep(5);}check(done(),"UI action completed before timeout");}
        // 실제 앱과 동일한 바깥 메시지 루프를 유지해야 모달 확인창 이후에도 UI 동기화 컨텍스트가 유지됩니다.
        Exception? uiError=null;
        ui.BeginInvoke(new Action(()=>{try{
        Until(()=>All(ui).OfType<DependencySelectionBox>().Count()==1&&Button("전체 선택").Enabled);
        Button("전체 선택").PerformClick();
        var checks=All(ui).OfType<DependencySelectionBox>().ToArray();
        check(checks.Length==1&&checks[0].Checked,"actual select-all button selects eligible update only; unknown file hidden");
        checks.Single(c=>c.Enabled).Checked=false;checks.Single(c=>c.Enabled).Checked=true;
        bool confirmed=false,complete=false;string unexpected="";
        using var dismiss=new System.Windows.Forms.Timer{Interval=30};
        dismiss.Tick+=(_,_)=>{
            foreach(Form dialog in Application.OpenForms.Cast<Form>().Where(f=>f!=ui&&f.Visible).ToArray()){
                if(dialog is DependencyWindow){confirmed=true;All(dialog).OfType<Button>().Single(b=>b.Text=="함께 설치").PerformClick();}
                else if(dialog is DialogWindow){var texts=string.Join(" ",All(dialog).Select(c=>c.Text));if(texts.Contains("업데이트했어요."))complete=true;else unexpected=texts;All(dialog).OfType<Button>().FirstOrDefault(b=>b.Text=="확인")?.PerformClick();}
            }
        };
        dismiss.Start();Button("선택 업데이트").PerformClick();Until(()=>Button("선택 업데이트").Enabled);dismiss.Stop();
        Console.WriteLine($"UI RESULT confirmed={confirmed} complete={complete} unexpected={unexpected}");check(confirmed&&complete&&unexpected.Length==0,"manual checkbox selection reaches confirmation and actual update through button");
        check(File.Exists(Path.Combine(uiProfile.Folder,"mods","core2.0.0.jar"))&&!File.Exists(Path.Combine(uiProfile.Folder,"mods","old.jar")),"UI update replaces original after confirmation");
        check(!All(ui).OfType<DependencySelectionBox>().Any()&&All(ui).Any(c=>c.Text=="현재 게임 버전·로더에 맞는 새 업데이트가 없어요."),"completed updates disappear and empty state is shown");
        Button("닫기").PerformClick();Application.DoEvents();
        check(ui.IsDisposed,"close button closes immediately when idle");
        var cleanupProfile=Profile("ui-cleanup");Put(cleanupProfile,"client.jar",client);Put(cleanupProfile,"server.jar.disabled",Jar("server","1.0.0","server"));
        using var cleanupUi=new ModMaintenanceWindow(cleanupProfile,true,()=>true,new DownloadClient(handler:new Api()));cleanupUi.ShowInTaskbar=false;cleanupUi.Show();
        Button CleanButton(string text)=>All(cleanupUi).OfType<Button>().Single(b=>b.Text==text);
        Until(()=>All(cleanupUi).OfType<DependencySelectionBox>().Count()==2&&CleanButton("전체 선택").Enabled);
        using var acceptCleanup=new System.Windows.Forms.Timer{Interval=30};
        acceptCleanup.Tick+=(_,_)=>{foreach(var d in Application.OpenForms.OfType<DialogWindow>().ToArray())All(d).OfType<Button>().FirstOrDefault(b=>b.Text is "계속" or "확인")?.PerformClick();};
        acceptCleanup.Start();
        CleanButton("전체 선택").PerformClick();check(All(cleanupUi).OfType<DependencySelectionBox>().Count(c=>c.Checked)==2,"cleanup select-all survives parent locking");
        CleanButton("서버용 활성화").PerformClick();Until(()=>CleanButton("서버용 활성화").Enabled);
        check(File.Exists(Path.Combine(cleanupProfile.Folder,"mods","server.jar")),"actual cleanup enable button uses selected server mod");
        CleanButton("전체 선택").PerformClick();CleanButton("클라이언트 전용 끄기").PerformClick();Until(()=>CleanButton("클라이언트 전용 끄기").Enabled);
        check(File.Exists(Path.Combine(cleanupProfile.Folder,"mods","client.jar.disabled")),"actual cleanup disable button uses selected client mod");
        CleanButton("전체 선택").PerformClick();CleanButton("클라이언트 전용 삭제").PerformClick();Until(()=>CleanButton("클라이언트 전용 삭제").Enabled);
        check(!File.Exists(Path.Combine(cleanupProfile.Folder,"mods","client.jar.disabled"))&&Directory.GetFiles(Path.Combine(cleanupProfile.Folder,"mods","removed"),"client.jar.disabled",SearchOption.AllDirectories).Length==1,"actual cleanup delete button archives selected file");
        acceptCleanup.Stop();CleanButton("닫기").PerformClick();check(cleanupUi.IsDisposed,"cleanup close button closes immediately");
        }catch(Exception e){uiError=e;}finally{Application.ExitThread();}}));
        Application.Run();if(uiError!=null)throw uiError;
    }
    public static void Live(string root,Action<bool,string> check)
    {
        using var net=new DownloadClient();var p=new Profile{Kind="Fabric",MinecraftVersion="1.21.1",Folder=Path.Combine(root,"live-fixture")};Directory.CreateDirectory(Path.Combine(p.Folder,"mods"));
        var versions=Wait(net.Json("https://api.modrinth.com/v2/project/fabric-api/version?loaders=%5B%22fabric%22%5D&game_versions=%5B%221.21.1%22%5D"));
        var candidates=versions.AsArray().Where(v=>v!=null&&ModMaintenance.Compatible(v,p)&&(string?)v["version_type"]=="release").OrderBy(v=>v!["date_published"]!.ToString()).ToArray();
        check(candidates.Length>1,"official provider has multiple compatible Fabric API releases");
        var first=candidates[0]!;var file=first["files"]!.AsArray().First(f=>(bool?)f!["primary"]==true)!;
        string path=Path.Combine(p.Folder,"mods","manually-renamed.jar");
        Wait(net.FileAsync(file["url"]!.ToString(),path,new Progress<string>(),default).ContinueWith(t=>{t.GetAwaiter().GetResult();return true;}));
        check(ExtensionDependencies.Hash(path).Equals(file["hashes"]!["sha512"]!.ToString(),StringComparison.OrdinalIgnoreCase),"downloaded official fixture verified");
        var report=Wait(new ModMaintenance(net).Scan(p,new Progress<string>(),default));var row=report.Items.Single();
        check(row.Project?.Id==first["project_id"]!.ToString()&&row.HasUpdate,"live exact hash detects newer compatible release for renamed file");
        check(row.Latest!=null&&ModMaintenance.Compatible(row.Latest,p)&&row.Side!="unsupported","live latest matches game and loader");
        check(File.ReadAllBytes(path).Length>0&&Directory.GetFiles(Path.Combine(p.Folder,"mods")).Length==1,"read-only audit does not change original fixture");
        using var window=new ModMaintenanceWindow(p,false,()=>true);
        window.Opacity=0;window.ShowInTaskbar=false;window.Show();
        var until=DateTime.UtcNow.AddSeconds(40);while(DateTime.UtcNow<until&&All(window).OfType<Button>().Any(b=>!b.Enabled)){Application.DoEvents();Thread.Sleep(20);}
        window.ShowReport(report);
        var end=DateTime.UtcNow.AddSeconds(2);while(DateTime.UtcNow<end){Application.DoEvents();Thread.Sleep(20);}
        using var image=new Bitmap(window.Width,window.Height);window.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(root,"maintenance-live-update.png"));window.Close();
    }
    static IEnumerable<Control> All(Control root)=>root.Controls.Cast<Control>().SelectMany(c=>All(c).Prepend(c));
}
