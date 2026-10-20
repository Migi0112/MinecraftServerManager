// [소스 안내] 서버 한 대의 경로·버전·메모리 등 등록 정보와 JSON 저장 기능입니다. 실제 월드/JAR 파일과 등록 정보는 별도로 보관합니다.
using System.Text;
using System.Text.Json;

namespace NeoForgeServerManager;

// 서버 한 대의 설정을 JSON으로 저장하는 데이터입니다. 실제 서버 파일은 Folder 경로에 따로 보관합니다.
public sealed class Profile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Kind { get; set; } = "NeoForge";
    public string MinecraftVersion { get; set; } = "미확인";
    public string LoaderVersion { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastRun { get; set; }
    public int Port { get; set; } = 25565;
    public string LaunchJar { get; set; } = "";
    public string ArgumentFile { get; set; } = "";
    public bool ManagedInstall { get; set; }
    public string ImagePath { get; set; } = "";
    public bool BackupChat { get; set; } = true;
    public bool BackupBossbar { get; set; } = true;
    public string Name { get; set; } = "내 NeoForge 서버";
    public string Folder { get; set; } = "";
    public string Java { get; set; } = "java.exe";
    public int MinRam { get; set; } = 4;
    public int MaxRam { get; set; } = 4;
    public int RestartDelaySeconds { get; set; } = 30;
    public int RestartLimit { get; set; } = 3;
    public bool AutoRestart { get; set; }
    public bool AutoBackup { get; set; }
    public int BackupMinutes { get; set; } = 60;
    public int BackupKeepCount {get;set;} // 0이면 자동 삭제 없이 모두 보관합니다.
    public string BackupFolder { get; set; } = AppSettings.Load().BackupRoot;
    public string TpsCommand { get; set; } = "neoforge tps";
    public override string ToString() => Name;
}

// 임시 파일에 먼저 저장하고 이름을 바꿔, 저장 도중 종료되어도 기존 JSON 손상을 줄입니다.
public static class Storage
{
    public static readonly string Root = Environment.GetEnvironmentVariable("MIGI_MANAGER_DATA_ROOT") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NeoForgeServerManager");
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    // [동작 설명] 먼저 임시 파일에 저장한 뒤 최종 파일로 교체하여 중간에 끊긴 JSON이 남을 위험을 줄입니다.
    public static void AtomicWrite(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, text, new UTF8Encoding(false)); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public static List<Profile> Load()
    {
        string path = Path.Combine(Root, "profiles.json");
        return File.Exists(path) ? JsonSerializer.Deserialize<List<Profile>>(File.ReadAllText(path)) ?? [] : [];
    }
    public static void Save(List<Profile> profiles) => AtomicWrite(Path.Combine(Root, "profiles.json"), JsonSerializer.Serialize(profiles, Json));
}



