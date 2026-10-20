// [소스 안내] 메인 화면의 카드/자세히 보기 선택과 서버 표시 순서를 저장합니다.
using System.Text.Json;
namespace NeoForgeServerManager;

// Presentation preferences are separate from live server settings, so reordering cannot overwrite them.
public sealed class HomePreferences
{
    public bool Details {get;set;}
    public List<string> Order {get;set;}=[];
    static string FilePath=>Path.Combine(Storage.Root,"home-view.json");
    public static HomePreferences Load(){try{return File.Exists(FilePath)?JsonSerializer.Deserialize<HomePreferences>(File.ReadAllText(FilePath))??new():new();}catch(JsonException){return new();}}
    public void Save()=>Storage.AtomicWrite(FilePath,JsonSerializer.Serialize(this,Storage.Json));
    public List<Profile> Sort(IEnumerable<Profile> profiles)=>profiles.Select((p,i)=>(p,i)).OrderBy(x=>{int n=Order.IndexOf(x.p.Id);return n<0?int.MaxValue:n;}).ThenBy(x=>x.i).Select(x=>x.p).ToList();
    public static List<Profile> Available(IEnumerable<Profile> profiles)=>profiles.Where(p=>Directory.Exists(p.Folder)).ToList();
    public static bool Reorder(List<string> order,string source,string target,bool after){if(source==target||!order.Contains(source)||!order.Contains(target))return false;order.Remove(source);int index=order.IndexOf(target)+(after?1:0);order.Insert(index,source);return true;}
    public static bool Move(List<string> order,int index,int delta){int target=index+delta;if(index<0||index>=order.Count||target<0||target>=order.Count)return false;(order[index],order[target])=(order[target],order[index]);return true;}
}
