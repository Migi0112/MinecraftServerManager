// [소스 안내] Modrinth 모드팩 검색과 서버 설치입니다. 검증된 새 폴더만 서버목록에 등록합니다.
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace NeoForgeServerManager;

public sealed record ModpackProject(string Id,string Title,string Description,string IconUrl)
{
    public override string ToString()=>Title;
}
public sealed record ModpackRelease(string Id,string Title,string Game,string Loader,string Url,string Hash)
{
    public override string ToString()=>$"{Title} · Minecraft {Game} · {Loader}";
}
public sealed record ModpackFile(string Path,string Hash,string[] Downloads,long Size,bool Optional);
public sealed record ModpackManifest(string Game,string Kind,string Loader,ModpackFile[] Files);

public sealed class ModpackService(DownloadClient net)
{
    const string Api="https://api.modrinth.com/v2";
    static string S(JsonNode? node)=>node?.ToString()??"";
    public async Task<(ModpackProject[] Items,int Total)> Search(string query,int offset,CancellationToken ct)
    {
        string facets=Uri.EscapeDataString("""[["project_type:modpack"],["server_side:required","server_side:optional"],["categories:fabric","categories:forge","categories:neoforge"]]""");
        var json=await net.Json($"{Api}/search?query={Uri.EscapeDataString(query)}&facets={facets}&limit=20&offset={offset}&index={(query.Trim().Length==0?"downloads":"relevance")}",ct);
        return (json["hits"]!.AsArray().Select(x=>new ModpackProject(S(x!["project_id"]),S(x["title"]),S(x["description"]),S(x["icon_url"]))).ToArray(),(int?)json["total_hits"]??0);
    }
    public async Task<ModpackRelease[]> Releases(ModpackProject pack,CancellationToken ct)
    {
        var json=await net.Json($"{Api}/project/{Uri.EscapeDataString(pack.Id)}/version",ct);
        return json.AsArray().Where(v=>v!["loaders"]!.AsArray().Any(l=>S(l) is "fabric" or "forge" or "neoforge"))
            .Select(v=>(Version:v!,File:v!["files"]!.AsArray().OrderByDescending(f=>(bool?)f!["primary"]==true).FirstOrDefault(f=>S(f!["filename"]).EndsWith(".mrpack",StringComparison.OrdinalIgnoreCase))))
            .Where(x=>x.File!=null).Select(x=>new ModpackRelease(S(x.Version["id"]),S(x.Version["name"]),
                string.Join(", ",x.Version["game_versions"]!.AsArray().Select(S)),string.Join(", ",x.Version["loaders"]!.AsArray().Select(S)),
                S(x.File!["url"]),S(x.File["hashes"]?["sha512"]))).ToArray();
    }
    // 압축 파일과 색인의 경로가 상위 폴더·다른 드라이브·대체 데이터 스트림을 가리키지 못하게 검증합니다.
    public static string SafePath(string root,string relative)
    {
        var parts=relative.Replace('\\','/').Split('/');
        if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative)||parts.Any(p=>p.Length==0||p is "." or ".."||p.EndsWith('.')||p.EndsWith(' ')||
            p.IndexOfAny(Path.GetInvalidFileNameChars())>=0||Regex.IsMatch(p.Split('.')[0],@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$",RegexOptions.IgnoreCase)))
            throw new IOException("모드팩에 안전하지 않은 파일 경로가 있습니다: "+relative);
        string full=Path.GetFullPath(Path.Combine(root,Path.Combine(parts)));
        if(!full.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root))+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
            throw new IOException("모드팩 파일이 설치 폴더를 벗어납니다.");
        return full;
    }
    static void ValidHash(string hash)
    {
        if(!Regex.IsMatch(hash,@"\A[0-9a-fA-F]{128}\z"))throw new IOException("모드팩의 SHA512 검증 정보가 없습니다.");
    }
    static void ValidUrl(string url)
    {
        if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https"||!uri.IsDefaultPort||uri.UserInfo.Length>0||
            !new[]{"cdn.modrinth.com","github.com","raw.githubusercontent.com","gitlab.com"}.Contains(uri.Host))
            throw new IOException("지원하지 않는 모드팩 다운로드 주소입니다.");
    }
    public static ModpackManifest ReadManifest(ZipArchive zip)
    {
        var entries=zip.Entries.Where(e=>e.FullName=="modrinth.index.json").ToArray();
        if(entries.Length!=1||entries[0].Length>8*1024*1024)throw new IOException("올바른 모드팩 색인 파일이 없습니다.");
        using var stream=entries[0].Open();var json=JsonNode.Parse(stream)??throw new IOException("모드팩 정보가 비어 있습니다.");
        if((int?)json["formatVersion"]!=1||S(json["game"])!="minecraft")throw new IOException("지원하지 않는 모드팩 형식입니다.");
        var deps=json["dependencies"]?.AsObject()??throw new IOException("게임과 로더 정보가 없습니다.");
        var loaders=deps.Where(x=>x.Key!="minecraft").ToArray();
        if(loaders.Length!=1||loaders[0].Key is not ("fabric-loader" or "forge" or "neoforge"))
            throw new IOException("현재는 Fabric · Forge · NeoForge 모드팩 서버를 지원합니다.");
        string game=S(deps["minecraft"]),loader=S(loaders[0].Value);
        if(!MinecraftVersions.Valid(game)||!Regex.IsMatch(loader,@"\A[\w.+-]+\z"))throw new IOException("게임 또는 로더 버전이 올바르지 않습니다.");
        string kind=loaders[0].Key switch{"fabric-loader"=>"Fabric","forge"=>"Forge",_=>"NeoForge"};
        if(kind=="Forge"&&!loader.StartsWith(game+"-",StringComparison.Ordinal))loader=game+"-"+loader;
        var files=new List<ModpackFile>();var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var file in json["files"]?.AsArray()??throw new IOException("파일 목록이 없습니다."))
        {
            string path=S(file!["path"]);SafePath(Path.GetTempPath(),path);
            if(!paths.Add(path.Replace('\\','/')))throw new IOException("중복 파일 경로가 있습니다: "+path);
            string side=S(file["env"]?["server"]);
            if(side=="unsupported")continue;
            if(side is not ("" or "required" or "optional"))throw new IOException("알 수 없는 서버 설치 조건입니다.");
            string hash=S(file["hashes"]?["sha512"]);ValidHash(hash);
            string[] urls=file["downloads"]?.AsArray().Select(S).ToArray()??[];
            if(urls.Length==0)throw new IOException("다운로드 주소가 없는 파일입니다: "+path);
            foreach(string url in urls)ValidUrl(url);
            long size=(long?)file["fileSize"]??-1;
            if(size<0||size>2L*1024*1024*1024)throw new IOException("모드팩 파일 크기를 확인할 수 없습니다.");
            files.Add(new(path,hash,urls,size,side=="optional"));
        }
        if(files.Count>10000||files.Sum(f=>f.Size)>20L*1024*1024*1024)throw new IOException("모드팩 설치 크기가 너무 큽니다.");
        return new(game,kind,loader,files.ToArray());
    }
    async Task DownloadVerified(string url,string path,string hash,IProgress<string> log,CancellationToken ct)
    {
        ValidUrl(url);ValidHash(hash);await net.FileAsync(url,path,log,ct);
        await using var stream=File.OpenRead(path);
        if(!Convert.ToHexString(await SHA512.HashDataAsync(stream,ct)).Equals(hash,StringComparison.OrdinalIgnoreCase))
            throw new IOException("파일 SHA512 검증에 실패했습니다: "+Path.GetFileName(path));
    }
    // 공통 설정 다음에 서버용 설정을 덮어씁니다. 클라이언트 전용 폴더는 가져오지 않습니다.
    public static async Task ExtractOverrides(ZipArchive zip,string root,CancellationToken ct)
    {
        long total=0;int count=0;
        foreach(string prefix in new[]{"overrides/","server-overrides/"})
        {
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var entry in zip.Entries.Where(e=>e.FullName.StartsWith(prefix,StringComparison.Ordinal)&&!e.FullName.EndsWith('/')))
            {
                ct.ThrowIfCancellationRequested();string target=SafePath(root,entry.FullName[prefix.Length..]);
                if(!seen.Add(target)||((entry.ExternalAttributes>>16)&0xF000)==0xA000)throw new IOException("중복 파일 또는 연결 파일이 포함된 모드팩입니다.");
                total+=entry.Length;
                if(++count>50000||entry.Length>2L*1024*1024*1024||total>20L*1024*1024*1024)throw new IOException("압축 해제 크기가 너무 큽니다.");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await using var input=entry.Open();await using var output=File.Create(target);await input.CopyToAsync(output,ct);
            }
        }
    }
    public async Task<string> Image(ModpackProject project,CancellationToken ct)
    {
        if(project.IconUrl.Length==0)return "";
        ValidUrl(project.IconUrl);
        string root=Path.Combine(Storage.Root,"images");Directory.CreateDirectory(root);
        string target=Path.Combine(root,"modpack-"+Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(project.IconUrl)))+".png");
        if(File.Exists(target))return target;
        string download=Path.Combine(root,Guid.NewGuid().ToString("N")+".download");
        try
        {
            await net.FileAsync(project.IconUrl,download,new Progress<string>(),ct);
            if(new FileInfo(download).Length>20_000_000)throw new IOException("대표 이미지가 너무 큽니다.");
            using var decoded=SkiaSharp.SKBitmap.Decode(download)??throw new IOException("대표 이미지 형식을 읽을 수 없습니다.");
            using var skImage=SkiaSharp.SKImage.FromBitmap(decoded);using var data=skImage.Encode(SkiaSharp.SKEncodedImageFormat.Png,100);
            using var memory=new MemoryStream(data.ToArray());using var image=System.Drawing.Image.FromStream(memory);
            using var resized=new Bitmap(image,Artwork.Fit(image,new Rectangle(0,0,512,512)).Size);
            resized.Save(target,System.Drawing.Imaging.ImageFormat.Png);return target;
        }
        finally{if(File.Exists(download))File.Delete(download);}
    }
    // 실패/취소 시 등록하지 않습니다. 새로 만든 임시 폴더만 정리하며 기존 서버에는 접근하지 않습니다.
    public async Task<Profile> Install(ModpackProject project,ModpackRelease release,string parent,string name,string java,
        int minRam,int maxRam,bool optional,IProgress<string> log,CancellationToken ct,
        Func<Profile,IProgress<string>,CancellationToken,Task>? installLoader=null)
    {
        if(string.IsNullOrWhiteSpace(name)||minRam<1||maxRam<minRam||maxRam>128)throw new ArgumentException("서버 이름과 메모리 설정을 확인하세요.");
        string dest=ServerFolders.Available(parent,name),stage=dest+".importing-"+Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(stage);
        try
        {
            string archive=Path.Combine(stage,".pack-download.mrpack");
            log.Report("모드팩 파일을 다운로드하고 검증합니다.");
            await DownloadVerified(release.Url,archive,release.Hash,log,ct);
            ModpackManifest manifest;
            using(var zip=ZipFile.OpenRead(archive))
            {
                manifest=ReadManifest(zip);
                // 게임·로더는 검색 결과가 아닌 다운로드한 팩의 정확한 의존 버전을 따릅니다.
                var wanted=manifest.Files.Where(f=>optional||!f.Optional).ToArray();
                for(int i=0;i<wanted.Length;i++)
                {
                    var file=wanted[i];string target=SafePath(stage,file.Path);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    log.Report($"모드팩 파일 {i+1}/{wanted.Length} · {file.Path}");
                    Exception? failure=null;
                    foreach(string url in file.Downloads)
                    {
                        try{await DownloadVerified(url,target,file.Hash,log,ct);failure=null;break;}
                        catch(Exception e) when(e is not OperationCanceledException){failure=e;if(File.Exists(target))File.Delete(target);}
                    }
                    if(failure!=null)throw new IOException("다운로드 실패: "+file.Path,failure);
                    if(new FileInfo(target).Length!=file.Size)throw new IOException("파일 크기 검증 실패: "+file.Path);
                }
                await ExtractOverrides(zip,stage,ct);
            }
            File.Delete(archive);
            var profile=new Profile{Name=name.Trim(),Folder=stage,Java=java.Trim(),MinRam=minRam,MaxRam=maxRam,Kind=manifest.Kind,
                MinecraftVersion=manifest.Game,LoaderVersion=manifest.Loader,ManagedInstall=true,
                TpsCommand=manifest.Kind switch{"Forge"=>"forge tps","NeoForge"=>"neoforge tps",_=>""}};
            log.Report($"{manifest.Kind} {manifest.Loader} 서버 로더를 설치합니다.");
            if(installLoader==null)
            {
                await JavaService.ValidateAsync(profile.Java,manifest.Game,net,ct);
                // 팩 내 런처가 공식 설치 파일과 충돌하지 않도록 별도 폴더에서 공식 로더를 준비합니다.
                string loaderRoot=Path.Combine(stage,".manager-loader-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(loaderRoot);
                profile.Folder=loaderRoot;
                await InstallerFactory.Create(manifest.Kind,net).Install(profile,log,ct);
                foreach(string file in Directory.GetFiles(loaderRoot,"*",SearchOption.AllDirectories))
                {
                    string target=SafePath(stage,Path.GetRelativePath(loaderRoot,file));
                    if(Path.GetFileName(target)=="user_jvm_args.txt"&&File.Exists(target))continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target,true);
                }
                Directory.Delete(loaderRoot,true);profile.Folder=stage;
            }
            else await installLoader(profile,log,ct);
            Storage.AtomicWrite(Path.Combine(stage,"eula.txt"),"eula=true\n");
            string jvmArgs=Path.Combine(stage,"user_jvm_args.txt");
            Storage.AtomicWrite(jvmArgs,ServerService.UpdateHeapArguments(File.Exists(jvmArgs)?File.ReadAllText(jvmArgs):"",minRam,maxRam));
            profile.Port=int.TryParse(PropertiesFile.ReadValue(Path.Combine(stage,"server.properties"),"server-port","25565"),out int port)?port:25565;
            log.Report("모드팩 대표 이미지를 저장합니다.");
            profile.ImagePath=await Image(project,ct);
            Storage.AtomicWrite(Path.Combine(stage,"manager-modpack.json"),JsonSerializer.Serialize(new{Provider="Modrinth",ProjectId=project.Id,VersionId=release.Id,project.Title,manifest.Game,manifest.Loader},Storage.Json));
            ct.ThrowIfCancellationRequested();
            profile.Folder=dest;
            Storage.AtomicWrite(Path.Combine(stage,"manager-launch.json"),JsonSerializer.Serialize(profile,Storage.Json));
            Directory.Move(stage,dest);
            ServerRegistrations.Register(profile);
            log.Report("모드팩 서버를 서버목록에 추가했습니다.");
            return profile;
        }
        finally{if(Directory.Exists(stage))Directory.Delete(stage,true);}
    }
}
