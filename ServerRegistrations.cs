// [소스 안내] 한 폴더가 서버목록에 중복 표시되는 것을 막습니다. 표시할 대표 등록 정보 선택과 저장 시 중복 검사를 담당합니다.
namespace NeoForgeServerManager;

// A folder is one server. Legacy records remain intact so settings and backup ownership are not lost.
public static class ServerRegistrations
{
    public static string Key(Profile profile)
    {
        if(string.IsNullOrWhiteSpace(profile.Folder))return "id:"+profile.Id;
        try{return Path.TrimEndingDirectorySeparator(Path.GetFullPath(profile.Folder.Trim()));}
        catch(ArgumentException){return "id:"+profile.Id;}
    }
    public static bool SameFolder(Profile left,Profile right)=>StringComparer.OrdinalIgnoreCase.Equals(Key(left),Key(right));
    public static List<Profile> ForDisplay(IEnumerable<Profile> profiles,Func<Profile,bool>? active=null)
        =>profiles.GroupBy(Key,StringComparer.OrdinalIgnoreCase)
            .Select(group=>group.OrderByDescending(p=>active?.Invoke(p)==true)
                .ThenByDescending(p=>!string.IsNullOrWhiteSpace(p.ImagePath)).First()).ToList();
    public static void ValidateSave(List<Profile> existing,Profile profile)
    {
        var prior=existing.FirstOrDefault(p=>p.Id==profile.Id);
        if(prior!=null&&SameFolder(prior,profile))return;
        if(existing.Any(p=>p.Id!=profile.Id&&SameFolder(p,profile)))
            throw new IOException("이미 등록된 서버 폴더입니다. 서버목록에서 해당 서버를 선택하세요.");
    }
    public static void Register(Profile profile)
    {
        var existing=Storage.Load();ValidateSave(existing,profile);
        if(existing.Any(p=>p.Id==profile.Id))throw new IOException("이미 등록된 서버입니다.");
        existing.Add(profile);Storage.Save(existing);
    }
}
