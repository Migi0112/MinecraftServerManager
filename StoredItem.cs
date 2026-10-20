// [소스 안내] 저장된 아이템의 ID·개수·슬롯·NBT 정보 모델과 컨테이너 읽기 계약입니다.
namespace NeoForgeServerManager;

public sealed class StoredItem
{
    public string Id {get;init;}="";
    public int Count {get;init;}
    public string Slot {get;init;}="?";
    public Dictionary<string,object> Data {get;init;}=[];
    public override string ToString()=>$"슬롯 {Slot}   {Id}   × {Count}";
    public bool Contains(string text)=>ToString().Contains(text);
    public static List<StoredItem> ReadList(object? value)=>(value as List<object>??[]).OfType<Dictionary<string,object>>().Select(raw=>{
        var item=raw.GetValueOrDefault("item") as Dictionary<string,object>??raw;
        return new StoredItem{Id=item.GetValueOrDefault("id","").ToString()??"",Count=Convert.ToInt32(item.GetValueOrDefault("count",item.GetValueOrDefault("Count",0))),Slot=raw.GetValueOrDefault("slot",raw.GetValueOrDefault("Slot","?")).ToString()??"?",Data=item};
    }).Where(i=>i.Id.Length>0&&i.Id!="minecraft:air"&&i.Count>0).ToList();
}

public sealed record ContainerContents(string Message,List<StoredItem> Items,bool Supported);
public interface IItemContainerReader
{
    bool CanRead(StoredItem item);
    ContainerContents Read(string folder,StoredItem item);
}
public static class ItemContainers
{
    // Register a new reader here; inventory controls never depend on a specific mod.
    static readonly IItemContainerReader[] Readers=[new SophisticatedBackpackReader(),new VanillaContainerReader()];
    public static bool IsContainer(StoredItem i)=>Readers.Any(r=>r.CanRead(i))||i.Id.Contains("backpack")||i.Id.Contains("bag");
    public static ContainerContents Read(string folder,StoredItem item)
    {
        try{return Readers.FirstOrDefault(r=>r.CanRead(item))?.Read(folder,item)??new("이 모드의 내부 조회는 아직 지원하지 않습니다.",[],false);}
        catch(Exception e)when(e is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or OverflowException){return new("가방 데이터 읽기 실패: "+e.Message,[],false);}
    }
}
internal static class ItemNbt
{
    public static Dictionary<string,object> Compound(Dictionary<string,object> d,string key)=>d.GetValueOrDefault(key) as Dictionary<string,object>??[];
    public static string Uuid(object? value)=>value is int[] a&&a.Length==4?string.Join("",a.Select(n=>unchecked((uint)n).ToString("x8"))):value is string s?s.Replace("-",""):"";
}
