// [소스 안내] 셜커 상자/꾸러미와 Sophisticated Backpacks처럼 저장 구조가 다른 컨테이너의 내부 아이템을 읽습니다.
using static NeoForgeServerManager.ItemNbt;
namespace NeoForgeServerManager;

public sealed class VanillaContainerReader:IItemContainerReader
{
    public bool CanRead(StoredItem item){var c=Compound(item.Data,"components");var t=Compound(item.Data,"tag");return c.ContainsKey("minecraft:container")||c.ContainsKey("minecraft:bundle_contents")||Compound(t,"BlockEntityTag").ContainsKey("Items")||item.Id.StartsWith("minecraft:")&&(item.Id.Contains("shulker_box")||item.Id.EndsWith(":bundle"));}
    public ContainerContents Read(string folder,StoredItem item){var c=Compound(item.Data,"components");var t=Compound(item.Data,"tag");foreach(string k in new[]{"minecraft:container","minecraft:bundle_contents"})if(c.ContainsKey(k))return new("마지막 저장 기준",StoredItem.ReadList(c[k]),true);var block=Compound(t,"BlockEntityTag");return new("마지막 저장 기준",StoredItem.ReadList(block.GetValueOrDefault("Items")??t.GetValueOrDefault("Items")),true);}
}

public sealed class SophisticatedBackpackReader:IItemContainerReader
{
    public bool CanRead(StoredItem item)=>item.Id.StartsWith("sophisticatedbackpacks:",StringComparison.Ordinal);
    public ContainerContents Read(string folder,StoredItem item)
    {
        var c=Compound(item.Data,"components");var tag=Compound(item.Data,"tag");
        string uuid=Uuid(c.GetValueOrDefault("sophisticatedcore:storage_uuid")??tag.GetValueOrDefault("contentsUuid"));
        if(uuid.Length==0)return new("가방 저장 식별자가 없습니다. 아직 저장하지 않았거나 지원하지 않는 버전입니다.",[],false);
        string path=Path.Combine(BackupService.WorldFolders(folder)[0],"data","sophisticatedbackpacks.dat");
        if(!File.Exists(path))return new("가방의 별도 저장 파일을 찾을 수 없습니다.",[],false);
        var data=Compound(Nbt.Read(path),"data");
        var entry=(data.GetValueOrDefault("backpackContents") as List<object>??[]).OfType<Dictionary<string,object>>().FirstOrDefault(e=>Uuid(e.GetValueOrDefault("uuid"))==uuid);
        if(entry==null)return new("이 가방에 연결된 저장 기록을 찾을 수 없습니다.",[],false);
        var inventory=Compound(Compound(entry,"contents"),"inventory");
        return new("Sophisticated Backpacks · 마지막 저장 기준",StoredItem.ReadList(inventory.GetValueOrDefault("Items")),true);
    }
}
