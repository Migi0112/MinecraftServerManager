using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NeoForgeServerManager;

static class ModpackChecks
{
    static T Wait<T>(Task<T> task){while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(5);}return task.GetAwaiter().GetResult();}
    static void Wait(Task task){while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(5);}task.GetAwaiter().GetResult();}
    static IEnumerable<Control> All(Control root)=>root.Controls.Cast<Control>().SelectMany(c=>All(c).Prepend(c));
    static string Hash(byte[] bytes)=>Convert.ToHexString(SHA512.HashData(bytes));
    static byte[] Zip(JsonNode manifest)
    {
        using var bytes=new MemoryStream();
        using(var zip=new ZipArchive(bytes,ZipArchiveMode.Create,true))
        {
            void Add(string name,string text){using var writer=new StreamWriter(zip.CreateEntry(name).Open());writer.Write(text);}
            Add("modrinth.index.json",manifest.ToJsonString());
            Add("overrides/config/test.txt","common");Add("server-overrides/config/test.txt","server");
            Add("server-overrides/server.properties","server-port=25577\n");Add("client-overrides/client.txt","no");
            Add("overrides/user_jvm_args.txt","-Xms1G\n-Xmx2G\n-Dpack.fixture=true\n");
        }
        return bytes.ToArray();
    }
    sealed class Api : HttpMessageHandler
    {
        public readonly byte[] Mod=Encoding.UTF8.GetBytes("verified fixture mod");
        public byte[] Archive=[];
        public readonly byte[] Icon;
        public readonly List<string> Requests=[];
        public bool Corrupt;
        public Api(){using var memory=new MemoryStream();using var bitmap=new Bitmap(32,32);using(var g=Graphics.FromImage(bitmap))g.Clear(Color.Coral);bitmap.Save(memory,System.Drawing.Imaging.ImageFormat.Png);Icon=memory.ToArray();}
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();string url=request.RequestUri!.AbsoluteUri;Requests.Add(url);
            byte[]? bytes=url.Contains("pack.mrpack")?Archive:url.Contains("icon.png")?Icon:url.Contains(".jar")?(Corrupt?[0]:Mod):null;
            string? json=null;
            if(url.Contains("/search?"))json="""{"hits":[{"project_id":"fixture","title":"테스트 모드팩","description":"서버용 모드와 설정을 함께 설치하는 테스트 팩","icon_url":"https://cdn.modrinth.com/icon.png"}],"total_hits":1}""";
            if(url.EndsWith("/version"))json=JsonSerializer.Serialize(new[]{new{id="release",name="테스트 1.0",game_versions=new[]{"1.21.1"},loaders=new[]{"neoforge"},files=new[]{new{primary=true,filename="pack.mrpack",url="https://cdn.modrinth.com/pack.mrpack",hashes=new{sha512=Hash(Archive)}}}}});
            return Task.FromResult(new HttpResponseMessage(bytes!=null||json!=null?HttpStatusCode.OK:HttpStatusCode.NotFound){Content=bytes!=null?new ByteArrayContent(bytes):new StringContent(json??"missing")});
        }
    }
    public static void Run(string root,Action<bool,string> check)
    {
        using var api=new Api();using var net=new DownloadClient(handler:api);var service=new ModpackService(net);
        JsonNode File(string name,string side)=>JsonSerializer.SerializeToNode(new{path="mods/"+name,hashes=new{sha512=Hash(api.Mod)},env=new{server=side},downloads=new[]{"https://cdn.modrinth.com/"+name},fileSize=api.Mod.Length})!;
        var manifest=new JsonObject{["formatVersion"]=1,["game"]="minecraft",["dependencies"]=new JsonObject{["minecraft"]="1.21.1",["neoforge"]="21.1.1"},
            ["files"]=new JsonArray(File("required.jar","required"),File("optional.jar","optional"),File("client.jar","unsupported"))};
        api.Archive=Zip(manifest);
        var page=Wait(service.Search("",0,default));var project=page.Items.Single();var release=Wait(service.Releases(project,default)).Single();
        check(page.Total==1&&api.Requests.Any(u=>Uri.UnescapeDataString(u).Contains("project_type:modpack")),"search restricts results to modpacks");
        check(release.Hash==Hash(api.Archive)&&release.Game=="1.21.1","release preserves exact archive hash and version");
        using(var zip=new ZipArchive(new MemoryStream(api.Archive))){var m=ModpackService.ReadManifest(zip);check(m.Files.Length==2&&m.Files.Count(f=>f.Optional)==1,"client-only files excluded and optional files identified");check(m.Kind=="NeoForge"&&m.Loader=="21.1.1","loader version follows manifest");}
        foreach(string path in new[]{"../escape.jar","C:/escape.jar","/escape.jar","mods/file:stream","mods/CON.jar","mods/../escape.jar","mods/test. /file"})
        {
            bool rejected=false;try{ModpackService.SafePath(root,path);}catch(IOException){rejected=true;}check(rejected,"reject unsafe path "+path);
        }
        Task Loader(Profile p,IProgress<string> _,CancellationToken ct){ct.ThrowIfCancellationRequested();FileWrite(Path.Combine(p.Folder,"server.jar"),"fixture launcher");p.LaunchJar="server.jar";return Task.CompletedTask;}
        static void FileWrite(string path,string value)=>System.IO.File.WriteAllText(path,value);
        string parent=Path.Combine(root,"modpack-servers");Directory.CreateDirectory(parent);
        int count=Storage.Load().Count;
        var profile=Wait(service.Install(project,release,parent,"팩 서버","java.exe",4,8,false,new Progress<string>(),default,Loader));
        check(Storage.Load().Count==count+1&&Storage.Load().Any(p=>p.Id==profile.Id),"completed installation registered once");
        check(System.IO.File.ReadAllBytes(Path.Combine(profile.Folder,"mods","required.jar")).SequenceEqual(api.Mod),"required file downloaded and verified");
        check(!System.IO.File.Exists(Path.Combine(profile.Folder,"mods","optional.jar"))&&!System.IO.File.Exists(Path.Combine(profile.Folder,"mods","client.jar")),"optional opt-out and client exclusion respected");
        check(System.IO.File.ReadAllText(Path.Combine(profile.Folder,"config","test.txt"))=="server"&&!System.IO.File.Exists(Path.Combine(profile.Folder,"client.txt")),"server overrides take precedence; client overrides ignored");
        check(profile.Port==25577&&profile.ImagePath.Length>0&&System.IO.File.Exists(profile.ImagePath),"pack port and representative image persist in profile");
        string jvm=System.IO.File.ReadAllText(Path.Combine(profile.Folder,"user_jvm_args.txt"));
        check(jvm.Contains("-Dpack.fixture=true")&&jvm.Contains("-Xms4G")&&jvm.Contains("-Xmx8G")&&!jvm.Contains("-Xmx2G"),"pack JVM options retained while replacing heap settings");
        check(!Directory.EnumerateDirectories(parent).Any(p=>p.Contains(".importing-")),"successful staging folder removed");
        var second=Wait(service.Install(project,release,parent,"팩 서버","java.exe",4,8,true,new Progress<string>(),default,Loader));
        check(second.Folder!=profile.Folder&&System.IO.File.Exists(Path.Combine(second.Folder,"mods","optional.jar")),"repeat install uses separate folder and includes chosen optional files");
        count=Storage.Load().Count;api.Corrupt=true;
        bool rejectedHash=false;try{Wait(service.Install(project,release,parent,"손상된 팩","java.exe",4,8,true,new Progress<string>(),default,Loader));}catch(IOException){rejectedHash=true;}
        check(rejectedHash&&Storage.Load().Count==count,"corrupt file rejected without registration");
        check(!Directory.EnumerateDirectories(parent).Any(p=>p.Contains(".importing-")),"failed staging removed without touching existing servers");
        api.Corrupt=false;
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();bool canceled=false;try{Wait(service.Install(project,release,parent,"취소된 팩","java.exe",4,8,true,new Progress<string>(),cancel.Token,Loader));}catch(OperationCanceledException){canceled=true;}check(canceled&&Storage.Load().Count==count,"canceled install not registered");}
        var bad=manifest.DeepClone();bad["dependencies"]!["quilt-loader"]="0.26";using(var zip=new ZipArchive(new MemoryStream(Zip(bad)))){bool rejected=false;try{ModpackService.ReadManifest(zip);}catch(IOException){rejected=true;}check(rejected,"unsupported or multiple loaders rejected");}
        var fake=new Api{Archive=api.Archive};
        using(var ui=new ModpackPage(new DownloadClient(handler:fake)))
        {
            ui.Show();for(int i=0;i<50;i++){Application.DoEvents();Thread.Sleep(10);}
            check(All(ui).OfType<ModpackResultsList>().Single().Items.Count==1,"embedded search screen displays pack result");
            check(All(ui).OfType<PictureBox>().Single(p=>Equals(p.Tag,"modpack-preview")).Image!=null,"selected pack artwork preview loaded");
            var install=All(ui).OfType<WarmButton>().Single(b=>b.Text=="모드팩 서버 추가");
            check(!install.Enabled,"EULA agreement required before install");
            All(ui).OfType<CheckBox>().Single(c=>c.Text.Contains("EULA")).Checked=true;
            check(install.Enabled,"valid release and agreement enable installation");
            foreach(var size in new[]{new Size(1200,820),new Size(960,720)})
            {
                ui.Size=size;Application.DoEvents();
                check(install.Parent!.ClientRectangle.Contains(install.Bounds),"install button fits editor at "+size.Width);
            }
            ui.Size=new(1200,820);Application.DoEvents();
            using var image=new Bitmap(ui.Width,ui.Height);ui.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(root,"modpack-preview.png"));ui.Close();
        }
        using(var wizard=new CreateServerWizard()){wizard.Show();Application.DoEvents();check(All(wizard).OfType<WarmButton>().Any(b=>b.Text=="모드팩 서버 추가 →"&&b.Visible),"new-server first step includes modpack entry");wizard.Close();}
    }
    public static void Live(string root,Action<bool,string> check)
    {
        using var net=new DownloadClient();var service=new ModpackService(net);
        var page=Wait(service.Search("",0,default));
        check(page.Items.Length==20&&page.Total>20,"live popular modpack search returns paged results");
        var second=Wait(service.Search("",20,default));check(second.Items.Length>0&&second.Items[0].Id!=page.Items[0].Id,"live next page differs");
        var pack=page.Items[0];Console.WriteLine("LIVE PACK "+pack.Title);
        var releases=Wait(service.Releases(pack,default));check(releases.Length>0,"live project has supported releases");
        string icon=Wait(service.Image(pack,default));check(System.IO.File.Exists(icon),"official representative artwork decoded and persisted");
        string archive=Path.Combine(root,"live.mrpack");Wait(net.FileAsync(releases[0].Url,archive,new Progress<string>(),default));
        check(Hash(System.IO.File.ReadAllBytes(archive)).Equals(releases[0].Hash,StringComparison.OrdinalIgnoreCase),"live downloaded pack matches official SHA512");
        using var zip=ZipFile.OpenRead(archive);var manifest=ModpackService.ReadManifest(zip);
        check(manifest.Files.Length>0&&manifest.Game.Length>0,"real pack manifest parses server files and exact versions");
        Console.WriteLine($"LIVE MANIFEST {manifest.Kind} {manifest.Loader} Minecraft {manifest.Game} {manifest.Files.Length} server files");
        // 서버 설치·실행은 하지 않고 공개 메타데이터와 실제 팩 형식만 검증합니다.
    }
}
