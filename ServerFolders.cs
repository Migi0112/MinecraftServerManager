// [소스 안내] 서버 폴더 이름과 경로를 다루는 공통 규칙입니다. 화면 이름을 실제 저장 폴더 경로에 연결할 때 사용합니다.
using System.Text.RegularExpressions;
namespace NeoForgeServerManager;

public static class ServerFolders
{
    public static string Available(string parent,string name)
    {
        string safe=new(name.Trim().Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c).ToArray());
        safe=safe.TrimEnd(' ','.');if(safe.Length>80)safe=safe[..80].TrimEnd(' ','.');
        if(safe.Length==0)safe="내 서버";
        if(Regex.IsMatch(safe.Split('.')[0],@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$",RegexOptions.IgnoreCase))safe="_"+safe;
        string root=Path.GetFullPath(parent),result=Path.Combine(root,safe);int index=2;
        while(Directory.Exists(result)||File.Exists(result))result=Path.Combine(root,$"{safe} ({index++})");
        return result;
    }
}

public static class MinecraftVersions
{
    public static bool Valid(string value)=>Regex.IsMatch(value,@"^(?:\d+\.\d+(?:\.\d+)?(?:-(?:pre|rc)\d+)?|\d{2}w\d{2}[a-z])$");
    public static string? Detect(string folder)
    {
        string log=Path.Combine(folder,"logs","latest.log");if(!File.Exists(log))return null;
        try
        {
            using var stream=new FileStream(log,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
            using var reader=new StreamReader(stream);string? result=null;
            for(int i=0;i<2000&&reader.ReadLine() is { } line;i++)
            {
                var match=Regex.Match(line,@"Starting minecraft server version\s+(\S+)",RegexOptions.IgnoreCase);
                if(match.Success&&Valid(match.Groups[1].Value))result=match.Groups[1].Value;
            }
            return result;
        }
        catch(IOException){return null;}
        catch(UnauthorizedAccessException){return null;}
    }
    public static void Save(Profile profile,string version)
    {
        if(!Valid(version))throw new ArgumentException("Minecraft 버전을 확인하세요.");
        var all=Storage.Load();var existing=all.FirstOrDefault(p=>p.Id==profile.Id);
        if(existing!=null){existing.MinecraftVersion=version;Storage.Save(all);}
        profile.MinecraftVersion=version;
    }
}
