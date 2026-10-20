// [소스 안내] 서버 등록 정보·실제 폴더·연결된 백업의 삭제 계획과 실행입니다. 삭제 대상과 경로를 확인하는 처리입니다.
namespace NeoForgeServerManager;

public sealed record ServerDeletionPlan(Profile Profile,string ServerFolder,string[] Backups)
{
    public string[] RegistrationIds {get;init;} = [Profile.Id];
}
// Build a read-only confirmation snapshot. Only marker-owned ZIPs are eligible, never the backup root.
public static class ServerDeletion
{
    static string Full(string path)=>Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
    static bool Within(string path,string parent)=>Full(path).Equals(Full(parent),StringComparison.OrdinalIgnoreCase)||Full(path).StartsWith(Full(parent)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
    static void NoLinks(string path)
    {
        FileSystemInfo item=Directory.Exists(path)?new DirectoryInfo(path):new FileInfo(path);
        if(item.Exists&&item.LinkTarget!=null)throw new IOException("연결된 경로는 삭제할 수 없습니다: "+path);
        for(var d=new DirectoryInfo(Path.GetDirectoryName(Full(path))!);d!=null;d=d.Parent)
            if(d.Exists&&d.LinkTarget!=null)throw new IOException("연결된 경로는 삭제할 수 없습니다: "+d.FullName);
    }
    static void InspectTree(string folder)
    {
        NoLinks(folder);
        foreach(string path in Directory.EnumerateFileSystemEntries(folder)){NoLinks(path);if(Directory.Exists(path))InspectTree(path);}
    }
    public static ServerDeletionPlan Plan(Profile profile)
    {
        var registrations=Storage.Load().Where(p=>ServerRegistrations.SameFolder(p,profile)).ToList();
        if(registrations.All(p=>p.Id!=profile.Id))registrations.Add(profile);
        var ids=registrations.Select(p=>p.Id).ToHashSet(StringComparer.Ordinal);
        string folder=Full(profile.Folder);var files=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(string root in registrations.Select(p=>p.BackupFolder).Append(AppSettings.Load().BackupRoot).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if(!Directory.Exists(root))continue;NoLinks(root);
            foreach(string directory in Directory.EnumerateDirectories(root))
            {
                if(new DirectoryInfo(directory).LinkTarget!=null)continue;
                string marker=Path.Combine(directory,".server-id");
                if(File.Exists(marker)&&new FileInfo(marker).Length<256&&new FileInfo(marker).LinkTarget==null&&ids.Contains(File.ReadAllText(marker)))
                    foreach(string file in Directory.EnumerateFiles(directory,"*.zip")){NoLinks(file);files.Add(Full(file));}
            }
        }
        return new(profile,folder,files.Order(StringComparer.OrdinalIgnoreCase).ToArray()){RegistrationIds=ids.Order().ToArray()};
    }
    public static void Execute(ServerDeletionPlan plan,bool deleteServer,bool deleteBackups)
    {
        var profiles=Storage.Load();var current=profiles.SingleOrDefault(p=>p.Id==plan.Profile.Id)??throw new IOException("서버 프로필이 변경되었습니다. 다시 확인하세요.");
        if(!Full(current.Folder).Equals(plan.ServerFolder,StringComparison.OrdinalIgnoreCase))throw new IOException("서버 경로가 변경되었습니다. 다시 확인하세요.");
        var aliases=profiles.Where(p=>ServerRegistrations.SameFolder(p,current)).Select(p=>p.Id).ToHashSet(StringComparer.Ordinal);
        if(!aliases.SetEquals(plan.RegistrationIds))throw new IOException("서버 등록 정보가 변경되었습니다. 다시 확인하세요.");
        if(deleteServer&&Directory.Exists(plan.ServerFolder))
        {
            string[] protectedPaths=[Path.GetPathRoot(plan.ServerFolder)!,AppSettings.Load().ServerRoot,AppSettings.Load().BackupRoot,current.BackupFolder,Storage.Root,AppContext.BaseDirectory,Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)];
            if(protectedPaths.Concat(profiles.Where(p=>aliases.Contains(p.Id)).Select(p=>p.BackupFolder)).Where(p=>!string.IsNullOrWhiteSpace(p)).Any(p=>Within(p,plan.ServerFolder)))throw new IOException("서버 저장 루트나 사용자 기본 폴더는 삭제할 수 없습니다.");
            if(profiles.Where(p=>!aliases.Contains(p.Id)&&!string.IsNullOrWhiteSpace(p.Folder)).Any(p=>Within(p.Folder,plan.ServerFolder)||Within(plan.ServerFolder,p.Folder)||(!string.IsNullOrWhiteSpace(p.BackupFolder)&&Within(p.BackupFolder,plan.ServerFolder))))throw new IOException("다른 서버 프로필과 공유하는 경로입니다. 서버 폴더 삭제 선택을 해제하세요.");
            if(!deleteBackups&&plan.Backups.Any(p=>Within(p,plan.ServerFolder)))throw new IOException("보관할 백업이 서버 폴더 안에 있습니다. 서버 폴더 삭제 선택을 해제하세요.");
            InspectTree(plan.ServerFolder);
            foreach(string name in new[]{".manager.lock"}.Select(n=>Path.Combine(plan.ServerFolder,n)).Concat(Directory.EnumerateFiles(plan.ServerFolder,"session.lock",SearchOption.AllDirectories)))
                if(File.Exists(name))using(new FileStream(name,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}
        }
        // Recheck exact ownership after the dialog, but never add new files the user did not review.
        if(deleteBackups)
        {
            var eligible=Plan(current).Backups.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach(string file in plan.Backups.Where(File.Exists))
            {
                if(!eligible.Contains(file))throw new IOException("백업 소유 정보가 변경되었습니다. 다시 확인하세요.");
                if(profiles.Where(p=>!aliases.Contains(p.Id)&&!string.IsNullOrWhiteSpace(p.Folder)).Any(p=>Within(file,p.Folder)))throw new IOException("다른 서버 폴더에 있는 백업은 삭제할 수 없습니다.");
                NoLinks(file);using(new FileStream(file,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}
            }
        }
        // All paths above were resolved and checked before the first destructive operation.
        if(deleteBackups)foreach(string file in plan.Backups)if(File.Exists(file))File.Delete(file);
        if(deleteServer&&Directory.Exists(plan.ServerFolder))Directory.Delete(plan.ServerFolder,true);
        profiles.RemoveAll(p=>aliases.Contains(p.Id));Storage.Save(profiles);
    }
}
