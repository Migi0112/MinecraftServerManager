// [소스 안내] 기존 월드 폴더나 압축 파일을 서버에 적용하는 서비스입니다. 경로 검증과 작업 상태를 관리합니다.
using System.IO.Compression;
namespace NeoForgeServerManager;

public sealed record WorldImportResult(string? BackupPath);
public static class WorldImportService
{
    public static void EnsureNoPending(string server){foreach(string stage in Directory.GetDirectories(server,".world-import-*"))if(Directory.Exists(Path.Combine(stage,"previous"))&&!File.Exists(Path.Combine(stage,".committed")))throw new IOException("완료되지 않은 월드 교체가 있습니다. 보존된 기존 월드를 확인하세요: "+stage);}
    static void NoLinks(string path){for(var d=new DirectoryInfo(path);d!=null;d=d.Parent)if(d.Exists&&(d.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("링크/정션 대신 실제 폴더를 선택하세요.");}
    static IEnumerable<string> Files(string root)
    {
        NoLinks(root);foreach(string entry in Directory.EnumerateFileSystemEntries(root)){if((File.GetAttributes(entry)&FileAttributes.ReparsePoint)!=0)throw new IOException("월드에 링크가 포함되어 있습니다.");if(Directory.Exists(entry)){foreach(var file in Files(entry))yield return file;}else yield return entry;}
    }
    static int DataVersion(string root)
    {
        var n=Nbt.Read(Path.Combine(root,"level.dat"));if(n.GetValueOrDefault("Data") is not Dictionary<string,object> data)throw new IOException("Java Edition 월드의 level.dat가 아닙니다.");return data.GetValueOrDefault("DataVersion") is int v?v:0;
    }
    static string FindWorld(string source)
    {
        if(File.Exists(Path.Combine(source,"level.dat")))return source;
        var found=Files(source).Where(f=>Path.GetFileName(f)=="level.dat").Select(f=>Path.GetDirectoryName(f)!).Where(d=>!d.EndsWith("_nether",StringComparison.OrdinalIgnoreCase)&&!d.EndsWith("_the_end",StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        if(found.Length!=1)throw new IOException("월드를 하나만 포함한 ZIP 또는 level.dat가 있는 월드 폴더를 선택하세요.");return found[0];
    }
    public static async Task<WorldImportResult> ApplyAsync(string server,string source,string? backupDirectory,IProgress<string> progress,CancellationToken ct=default)
    {
        server=Path.GetFullPath(server);source=Path.GetFullPath(source);NoLinks(server);
        if(server.StartsWith(source+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||source.Equals(server,StringComparison.OrdinalIgnoreCase)||source.StartsWith(server+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("새 월드는 현재 서버 폴더 밖에서 선택하세요.");
        using var guard=new FileStream(Path.Combine(server,".manager.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        EnsureNoPending(server);var old=BackupService.WorldFolders(server,false);string stage=Path.Combine(server,".world-import-"+Guid.NewGuid().ToString("N"));
        var locks=new List<FileStream>();var movedOld=new List<(string Original,string Saved)>();var movedNew=new List<string>();bool preserve=false;
        try{
            foreach(var root in old){string file=Path.Combine(root,"session.lock");if(File.Exists(file))locks.Add(new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Delete));}
            Directory.CreateDirectory(stage);string input=source;
            if(File.Exists(source)){
                if(!source.EndsWith(".zip",StringComparison.OrdinalIgnoreCase))throw new IOException("ZIP 파일 또는 월드 폴더를 선택하세요.");
                progress.Report("새 월드 ZIP을 확인하고 있어요…");input=Path.Combine(stage,"extracted");await BackupService.RestoreAsync(source,input);
            }else if(!Directory.Exists(source))throw new IOException("선택한 월드를 찾을 수 없습니다.");
            ct.ThrowIfCancellationRequested();string world=FindWorld(input);int nextVersion=DataVersion(world),previousVersion=File.Exists(Path.Combine(old[0],"level.dat"))?DataVersion(old[0]):0;
            if(nextVersion>previousVersion&&previousVersion>0)throw new IOException("현재 월드보다 새로운 Minecraft 버전의 맵입니다. 서버 버전을 먼저 확인·업데이트한 뒤 적용하세요.");
            string prepared=Path.Combine(stage,"prepared");Directory.CreateDirectory(prepared);var replacements=new List<(string From,string To)>();
            foreach(string suffix in new[]{"","_nether","_the_end"}){
                string from=world+suffix;if(!Directory.Exists(from))continue;if(File.Exists(Path.Combine(from,"session.lock")))locks.Add(new FileStream(Path.Combine(from,"session.lock"),FileMode.Open,FileAccess.Read,FileShare.Read));string to=Path.Combine(prepared,Path.GetFileName(old[0])+suffix);Directory.CreateDirectory(to);
                await Task.Run(()=>{foreach(string file in Files(from)){ct.ThrowIfCancellationRequested();if(Path.GetFileName(file)=="session.lock")continue;string dest=Path.Combine(to,Path.GetRelativePath(from,file));Directory.CreateDirectory(Path.GetDirectoryName(dest)!);using var read=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read);using var write=new FileStream(dest,FileMode.CreateNew,FileAccess.Write);read.CopyTo(write);}},ct);
                replacements.Add((to,old[0]+suffix));
            }
            _=DataVersion(replacements[0].From);ct.ThrowIfCancellationRequested();string? backup=null;
            if(backupDirectory!=null&&Directory.Exists(old[0])){progress.Report("기존 월드를 백업하고 있어요…");backup=await BackupService.CreateAsync(server,backupDirectory,progress);}
            ct.ThrowIfCancellationRequested();progress.Report("새 월드를 적용하고 있어요…");
            // Keep the old directories until every replacement has committed.
            string previous=Path.Combine(stage,"previous");Directory.CreateDirectory(previous);
            // Windows cannot rename a directory with our own child-file handles open.
            // An external server lock still makes Directory.Move fail; rollback retains its world.
            foreach(var handle in locks)handle.Dispose();locks.Clear();
            try{
                foreach(string original in old.Where(Directory.Exists)){string saved=Path.Combine(previous,Path.GetFileName(original));Directory.Move(original,saved);movedOld.Add((original,saved));}
                foreach(var pair in replacements){Directory.Move(pair.From,pair.To);movedNew.Add(pair.To);}
                File.WriteAllText(Path.Combine(stage,".committed"),"complete");
            }catch{
                try{foreach(string path in movedNew)Directory.Move(path,Path.Combine(prepared,Path.GetFileName(path)));foreach(var pair in movedOld.AsEnumerable().Reverse())Directory.Move(pair.Saved,pair.Original);}
                catch(Exception recovery){preserve=true;throw new IOException("자동 복구를 완료하지 못했습니다. 서버를 시작하지 말고 보존된 폴더를 확인하세요: "+stage,recovery);}throw;
            }
            progress.Report("월드 적용 완료");return new(backup);
        }finally{
            foreach(var handle in locks)handle.Dispose();
            if(!preserve&&Directory.Exists(stage)&&Path.GetDirectoryName(Path.GetFullPath(stage))==server)try{Directory.Delete(stage,true);}catch(IOException){progress.Report("정리 대기 폴더: "+stage);}catch(UnauthorizedAccessException){progress.Report("정리 대기 폴더: "+stage);}
        }
    }
}
