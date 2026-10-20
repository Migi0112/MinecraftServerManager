// [소스 안내] 백업 ZIP 생성·복원과 보관 개수 관리입니다. 대상 경로를 검사하고 작업 중 파일을 보호하는 핵심 처리입니다.
using System.IO.Compression;

namespace NeoForgeServerManager;

public static class BackupService
{
    // 이 관리자가 생성하고 기록한 백업만 정리합니다. 가져온 ZIP과 과거의 미등록 백업은 건드리지 않습니다.
    public static void RetainCreatedBackup(string completedArchive,int keep)
    {
        if(keep<0)throw new ArgumentOutOfRangeException(nameof(keep));
        string directory=Path.GetDirectoryName(Path.GetFullPath(completedArchive))!;
        if(!File.Exists(completedArchive))throw new IOException("완료된 백업 파일이 없습니다.");
        string ledger=Path.Combine(directory,".manager-backups.json");
        var names=File.Exists(ledger)?System.Text.Json.JsonSerializer.Deserialize<List<string>>(File.ReadAllText(ledger))??[]:new List<string>();
        names=names.Where(n=>Path.GetFileName(n)==n&&n.EndsWith(".zip",StringComparison.OrdinalIgnoreCase)&&File.Exists(Path.Combine(directory,n))).Distinct().ToList();
        string latest=Path.GetFileName(completedArchive);names.Remove(latest);names.Add(latest);
        if(keep>0)
        {
            foreach(string name in names.Take(Math.Max(0,names.Count-keep)).ToArray())
            {
                string path=Path.Combine(directory,name);
                if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)continue;
                File.Delete(path);names.Remove(name);
            }
        }
        Storage.AtomicWrite(ledger,System.Text.Json.JsonSerializer.Serialize(names,Storage.Json));
    }
    // [동작 설명] 백업 대상 경로를 검사합니다. 서버 안에 백업을 만들어 자기 자신까지 압축하는 구성을 피합니다.
    public static string ValidateDestination(string source, string destination)
    {
        string root = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string target = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new IOException("백업 폴더는 서버 폴더 밖으로 선택하세요.");
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0) throw new IOException("연결된 서버 폴더는 백업할 수 없습니다. 실제 폴더를 선택하세요.");
        // Reject junctions in destination ancestors to prevent aliases into the source tree.
        for (var d = new DirectoryInfo(target); d != null; d = d.Parent)
            if (d.Exists && (d.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("백업 경로의 링크/정션 대신 실제 폴더를 선택하세요.");
        return target;
    }
    public static string[] WorldFolders(string source,bool requireExisting=true)
    {
        string name = PropertiesFile.ReadValue(Path.Combine(source, "server.properties"), "level-name", "world");
        // Java properties can encode non-ASCII world names as Unicode escapes.
        name = System.Text.RegularExpressions.Regex.Replace(name, @"\\u([0-9a-fA-F]{4})", m => ((char)Convert.ToInt32(m.Groups[1].Value,16)).ToString());
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith('.') || name.EndsWith(' '))
            throw new IOException("월드 폴더 이름이 올바르지 않습니다. server.properties의 level-name을 확인하세요.");
        string main = Path.Combine(Path.GetFullPath(source), name);
        if (requireExisting && !Directory.Exists(main)) throw new IOException("월드 폴더가 없습니다. 서버를 한 번 실행해 월드를 생성하세요: " + name);
        var roots = new List<string> { main };
        foreach (string suffix in new[] { "_nether", "_the_end" })
            if (Directory.Exists(main + suffix)) roots.Add(main + suffix);
        foreach (string folder in roots)
            if (Directory.Exists(folder) && (File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) throw new IOException("월드의 링크/정션 대신 실제 폴더를 사용하세요.");
        if (requireExisting && !File.Exists(Path.Combine(main,"level.dat")) && !File.Exists(Path.Combine(main,"level.dat_old")))
            throw new IOException("선택된 월드 폴더에 level.dat가 없습니다. 월드 경로를 확인하세요.");
        return roots.ToArray();
    }
    public static bool IsWorldOnlyArchive(string archive)
    {
        using var zip = ZipFile.OpenRead(archive);
        return zip.Entries.Count > 0 && zip.Entries.All(e => e.FullName.Contains('/')) &&
            zip.Entries.Any(e => e.FullName.Split('/') is [_, "level.dat" or "level.dat_old"]);
    }
    // [동작 설명] 백업 ZIP을 만들고 완료된 경로를 반환합니다. 진행 메시지는 전달받은 progress로 보고합니다.
    public static async Task<string> CreateAsync(string source, string destination, IProgress<string> progress)
    {
        string target = ValidateDestination(source, destination);
        string[] worlds = WorldFolders(source);
        return await Task.Run(() =>
        {
            Directory.CreateDirectory(target);
            string output = Path.Combine(target, $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.zip");
            if (File.Exists(output)) output = Path.Combine(target, $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fffffff}.zip");
            string temp = output + ".partial";
            try
            {
                using (var zip = ZipFile.Open(temp, ZipArchiveMode.Create))
                {
                    var lastProgress = DateTime.MinValue;
                    foreach (string file in worlds.SelectMany(Enumerate))
                    {
                        string relative = Path.GetRelativePath(source, file);
                        if (Path.GetFileName(file) is ".manager.lock" or "session.lock") continue;
                        if ((DateTime.UtcNow - lastProgress).TotalMilliseconds >= 150)
                        { progress.Report(relative); lastProgress = DateTime.UtcNow; }
                        zip.CreateEntryFromFile(file, relative.Replace('\\', '/'), CompressionLevel.Fastest);
                    }
                }
                File.Move(temp, output);
                return output;
            }
            catch { if (File.Exists(temp)) File.Delete(temp); throw; }
        });
    }
    // ZIP 경로를 먼저 검증하여 ../ 같은 항목이 복원 폴더 밖을 덮어쓰지 못하게 합니다.
    // [동작 설명] 백업 압축을 대상 폴더에 복원합니다. 경로와 파일 검사를 포함하므로 단순 ZIP 풀기로 대체하지 않습니다.
    public static Task RestoreAsync(string archive, string destination) => Task.Run(() =>
    {
        if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("새 폴더에만 복원할 수 있습니다.");
        string root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        using var zip = ZipFile.OpenRead(archive);
        long total = 0;
        foreach (var entry in zip.Entries)
        {
            string path = Path.GetFullPath(Path.Combine(root, entry.FullName));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || entry.FullName.Contains(':')) throw new IOException("안전하지 않은 ZIP 경로입니다.");
            if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000) throw new IOException("심볼릭 링크 ZIP 항목은 지원하지 않습니다.");
            total = checked(total + entry.Length);
        }
        if (new DriveInfo(Path.GetPathRoot(root)!).AvailableFreeSpace < total) throw new IOException("복원 공간이 부족합니다.");
        Directory.CreateDirectory(destination);
        zip.ExtractToDirectory(destination, false);
    });
    private static IEnumerable<string> Enumerate(string directory)
    {
        foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
        {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("링크/정션이 있어 완전한 백업을 보장할 수 없습니다: " + entry);
            if ((attributes & FileAttributes.Directory) != 0)
            { foreach (string file in Enumerate(entry)) yield return file; }
            else yield return entry;
        }
    }
}


