// [소스 안내] 아이템에 저장된 인챈트 정보를 읽고 표시용 이름/레벨로 다루는 코드입니다.
namespace NeoForgeServerManager;

public sealed record ItemEnchantment(string Id,int Level)
{
    public string Display=>ItemEnchantments.Name(Id)+" "+Level;
}
public static class ItemEnchantments
{
    static readonly Dictionary<string,string> Names=new(){["sharpness"]="날카로움",["smite"]="강타",["bane_of_arthropods"]="살충",["knockback"]="밀치기",["fire_aspect"]="발화",["looting"]="약탈",["sweeping_edge"]="휩쓸기",["efficiency"]="효율",["silk_touch"]="섬세한 손길",["unbreaking"]="내구성",["fortune"]="행운",["mending"]="수선",["protection"]="보호",["fire_protection"]="화염으로부터 보호",["blast_protection"]="폭발로부터 보호",["projectile_protection"]="발사체로부터 보호",["feather_falling"]="가벼운 착지",["respiration"]="호흡",["aqua_affinity"]="친수성",["thorns"]="가시",["depth_strider"]="물갈퀴",["frost_walker"]="차가운 걸음",["soul_speed"]="영혼 가속",["swift_sneak"]="신속한 잠행",["power"]="힘",["punch"]="밀어내기",["flame"]="화염",["infinity"]="무한",["luck_of_the_sea"]="바다의 행운",["lure"]="미끼",["loyalty"]="충성",["impaling"]="찌르기",["riptide"]="급류",["channeling"]="집전",["multishot"]="다중 발사",["piercing"]="관통",["quick_charge"]="빠른 장전",["binding_curse"]="귀속 저주",["vanishing_curse"]="소실 저주",["density"]="육중",["breach"]="격파",["wind_burst"]="돌풍"};
    public static string Name(string id)=>id.StartsWith("minecraft:")&&Names.TryGetValue(id[10..],out var name)?name:id;
    public static IReadOnlyList<ItemEnchantment> Read(StoredItem item)
    {
        var result=new Dictionary<string,int>();
        void Add(string id,object value){if(id.Length>0&&int.TryParse(Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture),out int level))result[id]=level;}
        var components=ItemNbt.Compound(item.Data,"components");
        foreach(string key in new[]{"minecraft:enchantments","minecraft:stored_enchantments"})if(components.GetValueOrDefault(key) is Dictionary<string,object> entries){var levels=entries.GetValueOrDefault("levels") as Dictionary<string,object>??entries;foreach(var pair in levels)Add(pair.Key,pair.Value);}
        var tag=ItemNbt.Compound(item.Data,"tag");foreach(string key in new[]{"Enchantments","StoredEnchantments"})if(tag.GetValueOrDefault(key) is List<object> list)foreach(var entry in list.OfType<Dictionary<string,object>>())Add(entry.GetValueOrDefault("id","").ToString()??"",entry.GetValueOrDefault("lvl",0));
        return result.Select(p=>new ItemEnchantment(p.Key,p.Value)).ToArray();
    }
}
