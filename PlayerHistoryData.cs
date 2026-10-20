// [소스 안내] 플레이어 기록과 저장된 플레이어 데이터를 읽는 계층입니다. 기록 화면에서 사용하는 정보로 변환합니다.
using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace NeoForgeServerManager;

public sealed class Visit
{
    public DateTime Start {get;set;}
    public DateTime? End {get;set;}
    public string EndReason {get;set;}="";
}
public sealed class PlayerRecord
{
    public string Name {get;set;}="";
    public string Uuid {get;set;}="";
    public DateTime FirstRecorded {get;set;}
    public DateTime? LastJoined {get;set;}
    public double ObservedSeconds {get;set;}
    public List<Visit> Visits {get;set;}=[];
}
public sealed class PlayerHistoryStore
{
    readonly string path;
    readonly HashSet<string> online=new(StringComparer.OrdinalIgnoreCase);
    DateTime? last;
    public List<PlayerRecord> Records {get;private set;}
    public PlayerHistoryStore(string folder)
    {
        string key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(folder).TrimEnd('\\').ToUpperInvariant())));
        path=Path.Combine(Storage.Root,"player-history",key+".json");
        Records=File.Exists(path)?JsonSerializer.Deserialize<List<PlayerRecord>>(File.ReadAllText(path))??[]:[];
        // A previous manager exit is not proof of a player logout. Keep that distinction.
        foreach(var r in Records)foreach(var v in r.Visits.Where(v=>v.End==null))v.EndReason="기록 중단 · 종료 시각 없음";
    }
    public void Import(string folder,DateTime now)
    {
        foreach(var row in PlayerData.Read(folder,"usercache.json"))
        {
            string name=(string?)row?["name"]??"",id=(string?)row?["uuid"]??"";
            if(!Guid.TryParse(id,out var uuid)||!Regex.IsMatch(name,@"\A[A-Za-z0-9_]{1,16}\z"))continue;
            var r=Records.FirstOrDefault(x=>x.Uuid==uuid.ToString())??Records.FirstOrDefault(x=>x.Uuid==""&&x.Name.Equals(name,StringComparison.OrdinalIgnoreCase));
            if(r==null){r=new(){FirstRecorded=now};Records.Add(r);}r.Name=name;r.Uuid=uuid.ToString();
        }
        try{foreach(var file in Directory.EnumerateFiles(Path.Combine(BackupService.WorldFolders(folder)[0],"playerdata"),"*.dat"))if(Guid.TryParse(Path.GetFileNameWithoutExtension(file),out var id)&&!Records.Any(x=>x.Uuid==id.ToString()))Records.Add(new(){Name="이름 미확인",Uuid=id.ToString(),FirstRecorded=now});}catch(IOException){}catch(UnauthorizedAccessException){}
        Save();
    }
    public void Observe(IEnumerable<string> names,DateTime now,bool authoritative=true)
    {
        if(!authoritative)return;
        var current=names.ToHashSet(StringComparer.OrdinalIgnoreCase);
        double delta=last.HasValue?Math.Clamp((now-last.Value).TotalSeconds,0,60):0;
        foreach(var name in online){var r=Records.First(x=>x.Name.Equals(name,StringComparison.OrdinalIgnoreCase));r.ObservedSeconds+=delta;if(!current.Contains(name)){var v=r.Visits.LastOrDefault();if(v!=null&&v.End==null&&v.EndReason==""){v.End=now;v.EndReason="접속 종료";}}}
        foreach(var name in current.Except(online,StringComparer.OrdinalIgnoreCase))
        {
            if(!Regex.IsMatch(name,@"\A[A-Za-z0-9_]{1,16}\z"))continue;
            var r=Records.FirstOrDefault(x=>x.Name.Equals(name,StringComparison.OrdinalIgnoreCase));if(r==null){r=new(){Name=name,FirstRecorded=now};Records.Add(r);}
            r.LastJoined=now;r.Visits.Add(new(){Start=now});
        }
        online.Clear();online.UnionWith(current);last=now;Save();
    }
    void Save()=>Storage.AtomicWrite(path,JsonSerializer.Serialize(Records,Storage.Json));
}

// Bounded, read-only Java NBT reader. Never writes player files or sends save commands.
public sealed class PlayerSnapshot
{
    public DateTime? SavedAt {get;init;}
    public double? Health {get;init;}
    public int? Food {get;init;}
    public int? Level {get;init;}
    public int? Mode {get;init;}
    public string Position {get;init;}="기록 없음";
    public List<StoredItem> Inventory {get;init;}=[];
    public List<StoredItem> EnderItems {get;init;}=[];
    public List<StoredItem> EquippedContainers {get;init;}=[];
    public string Error {get;init;}="";
    static List<StoredItem> FindEquipped(Dictionary<string,object> root)
    {
        var result=new List<StoredItem>();
        void Walk(object value,int depth){if(depth>32)return;if(value is Dictionary<string,object> d){if(d.ContainsKey("id")&&(d.ContainsKey("count")||d.ContainsKey("Count"))){var item=StoredItem.ReadList(new List<object>{d}).FirstOrDefault();if(item!=null&&ItemContainers.IsContainer(item))result.Add(item);return;}foreach(var v in d.Values)Walk(v,depth+1);}else if(value is List<object> list)foreach(var v in list)Walk(v,depth+1);}
        foreach(var entry in root)if(entry.Key is not "Inventory" and not "EnderItems")Walk(entry.Value,0);return result;
    }
    public static PlayerSnapshot Read(string folder,string uuid)
    {
        if(!Guid.TryParse(uuid,out var id))return new();
        try{
            string path=Path.Combine(BackupService.WorldFolders(folder)[0],"playerdata",id+".dat");if(!File.Exists(path))return new();
            var n=Nbt.Read(path);double? Num(string key)=>n.TryGetValue(key,out var value)&&value is IConvertible?Convert.ToDouble(value):null;
            var pos=n.GetValueOrDefault("Pos") as List<object>;
            List<StoredItem> Items(string key)=>StoredItem.ReadList(n.GetValueOrDefault(key));
            return new(){SavedAt=File.GetLastWriteTime(path),Health=Num("Health"),Food=(int?)Num("foodLevel"),Level=(int?)Num("XpLevel"),Mode=(int?)Num("playerGameType"),Position=pos?.Count==3?$"{n.GetValueOrDefault("Dimension","차원 미확인")}\nX {Convert.ToDouble(pos[0]):F1} / Y {Convert.ToDouble(pos[1]):F1} / Z {Convert.ToDouble(pos[2]):F1}":"기록 없음",Inventory=Items("Inventory"),EnderItems=Items("EnderItems"),EquippedContainers=FindEquipped(n)};
        }catch(Exception e)when(e is IOException or UnauthorizedAccessException or InvalidDataException or OverflowException or ArgumentException){return new(){Error="저장 데이터 읽기 실패: "+e.Message};}
    }
}
public static class Nbt
{
    public static Dictionary<string,object> Read(string path)
    {
        using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);using var gzip=new GZipStream(file,CompressionMode.Decompress);using var data=new MemoryStream();byte[] chunk=new byte[8192];int count;
        while((count=gzip.Read(chunk))>0){if(data.Length+count>16*1024*1024)throw new InvalidDataException("NBT 크기 제한 초과");data.Write(chunk,0,count);}data.Position=0;
        using var r=new BinaryReader(data,Encoding.UTF8);byte root=r.ReadByte();if(root!=10)throw new InvalidDataException("NBT 루트 형식 오류");Text(r);return (Dictionary<string,object>)Value(r,10,0);
    }
    static byte[] Bytes(BinaryReader r,int n){var b=r.ReadBytes(n);if(b.Length!=n)throw new EndOfStreamException();return b;}
    static int Int(BinaryReader r)=>BinaryPrimitives.ReadInt32BigEndian(Bytes(r,4));
    static string Text(BinaryReader r)=>Encoding.UTF8.GetString(Bytes(r,BinaryPrimitives.ReadUInt16BigEndian(Bytes(r,2))));
    static int Count(BinaryReader r){int n=Int(r);if(n<0||n>1_000_000)throw new InvalidDataException("NBT 배열 길이 오류");return n;}
    static object Value(BinaryReader r,byte t,int depth)
    {
        if(depth>64)throw new InvalidDataException("NBT 중첩 제한 초과");
        switch(t){case 1:return r.ReadSByte();case 2:return BinaryPrimitives.ReadInt16BigEndian(Bytes(r,2));case 3:return Int(r);case 4:return BinaryPrimitives.ReadInt64BigEndian(Bytes(r,8));case 5:return BitConverter.Int32BitsToSingle(Int(r));case 6:return BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(Bytes(r,8)));case 7:return Bytes(r,Count(r));case 8:return Text(r);
        case 9:{byte kind=r.ReadByte();int n=Count(r);var list=new List<object>();for(int i=0;i<n;i++)list.Add(Value(r,kind,depth+1));return list;}
        case 10:{var fields=new Dictionary<string,object>();byte kind;while((kind=r.ReadByte())!=0){string name=Text(r);fields[name]=Value(r,kind,depth+1);if(fields.Count>100000)throw new InvalidDataException("NBT 필드 제한 초과");}return fields;}
        case 11:{int n=Count(r);var a=new int[n];for(int i=0;i<n;i++)a[i]=Int(r);return a;}case 12:{int n=Count(r);var a=new long[n];for(int i=0;i<n;i++)a[i]=BinaryPrimitives.ReadInt64BigEndian(Bytes(r,8));return a;}default:throw new InvalidDataException("지원하지 않는 NBT 태그");}
    }
}
