// [소스 안내] 아이템 지급용 검색 목록입니다. 게임 버전에 맞는 아이템과 한국어/영어 이름·이미지 정보를 불러옵니다.
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace NeoForgeServerManager;

public sealed record GiveItem(string Id,string Korean,string English)
{
    public override string ToString()=>Korean+" · "+English;
    public bool Matches(string query)=>Korean.Contains(query,StringComparison.OrdinalIgnoreCase)||English.Contains(query,StringComparison.OrdinalIgnoreCase)||Id.Contains(query,StringComparison.OrdinalIgnoreCase);
}
public static class GiveItemCatalog
{
    public static async Task<GiveItem[]> Load(Profile p)
    {
        if(!MinecraftVersions.Valid(p.MinecraftVersion))throw new IOException("아이템 목록을 불러오려면 서버 버전을 지정하세요.");
        string root=Path.Combine(Storage.Root,"item-catalog",p.MinecraftVersion);Directory.CreateDirectory(root);
        string local=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),".minecraft","versions",p.MinecraftVersion,p.MinecraftVersion+".jar");
        string jar=File.Exists(local)?local:Path.Combine(root,"client.jar"),korean=Path.Combine(root,"ko_kr.json");
        if(!File.Exists(jar)||!File.Exists(korean)){
            using var net=new DownloadClient();var manifest=await net.Json("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json");
            string url=manifest["versions"]!.AsArray().FirstOrDefault(v=>(string?)v!["id"]==p.MinecraftVersion)?["url"]?.ToString()??throw new IOException("해당 Minecraft 버전의 아이템 자료를 찾지 못했습니다.");var version=await net.Json(url);
            async Task Fetch(string address,string target,string hash){string temp=target+"."+Guid.NewGuid().ToString("N");try{await net.FileAsync(address,temp,new Progress<string>(),CancellationToken.None);using(var stream=File.OpenRead(temp))if(!Convert.ToHexString(SHA1.HashData(stream)).Equals(hash,StringComparison.OrdinalIgnoreCase))throw new IOException("아이템 자료 검증 실패");File.Move(temp,target,true);}finally{if(File.Exists(temp))File.Delete(temp);}}
            if(!File.Exists(jar)){var download=version["downloads"]!["client"]!;await Fetch(download["url"]!.ToString(),jar,download["sha1"]!.ToString());}
            if(!File.Exists(korean)){var index=await net.Json(version["assetIndex"]!["url"]!.ToString());var entry=index["objects"]?["minecraft/lang/ko_kr.json"];if(entry!=null){string hash=entry["hash"]!.ToString();await Fetch("https://resources.download.minecraft.net/"+hash[..2]+"/"+hash,korean,hash);}}
        }
        var archives=new List<string>{jar};string mods=Path.Combine(p.Folder,"mods");if(Directory.Exists(mods))archives.AddRange(Directory.GetFiles(mods,"*.jar"));
        return await Task.Run(()=>Read(archives,File.Exists(korean)?File.ReadAllText(korean):"{}"));
    }
    // Asset-derived IDs are suggestions; the server is the final authority on registered items.
    public static GiveItem[] Read(IEnumerable<string> archives,string koreanJson="{}")
    {
        var en=new Dictionary<string,string>();var ko=new Dictionary<string,string>();var ids=new HashSet<string>();
        void Language(JsonNode? data,Dictionary<string,string> to){if(data is JsonObject obj)foreach(var pair in obj)if(pair.Value is JsonValue v&&v.TryGetValue<string>(out var value))to[pair.Key]=value;}
        Language(JsonNode.Parse(koreanJson),ko);
        foreach(string archive in archives)try{using var z=ZipFile.OpenRead(archive);foreach(var e in z.Entries){if(e.Length>4*1024*1024)continue;var lang=Regex.Match(e.FullName,@"^assets/([^/]+)/lang/(en_us|ko_kr)\.json$");if(lang.Success){using var stream=e.Open();Language(JsonNode.Parse(stream),lang.Groups[2].Value=="ko_kr"?ko:en);}
                var item=Regex.Match(e.FullName,@"^assets/([^/]+)/(?:models/item|items)/([a-z0-9_./-]+)\.json$");if(item.Success)ids.Add(item.Groups[1].Value+":"+item.Groups[2].Value);
            }}catch(Exception e)when(e is IOException or InvalidDataException or System.Text.Json.JsonException){}
        string Name(Dictionary<string,string> values,string id){string key=id.Replace(':','.');return values.GetValueOrDefault("item."+key)??values.GetValueOrDefault("block."+key)??"";}
        return ids.Where(id=>Name(en,id).Length>0||Name(ko,id).Length>0).Select(id=>{string english=Name(en,id),korean=Name(ko,id);return new GiveItem(id,korean.Length>0?korean:english,english.Length>0?english:id);}).OrderBy(x=>x.Korean).ToArray();
    }
}

public sealed class GiveItemPicker:WarmButton
{
    public GiveItem[] Items {get;set;}=[];
    public GiveItem? Selected {get;private set;}
    public event Action? SelectionChanged;
    ItemArtwork art;
    readonly string folder;
    ToolStripDropDown? popup;
    public GiveItemPicker(string folder){this.folder=folder;Text="아이템 이름으로 검색 · 선택 ▾";art=new(folder);Click+=(_,_)=>Open();}
    public void RefreshArtwork(){art.Dispose();art=new(folder);}
    void Open()
    {
        if(popup!=null)return;
        var panel=new Panel{Size=new(Math.Max(450,Width),390),BackColor=WarmTheme.Surface,Padding=new Padding(10)};
        var search=new WarmTextBox{Dock=DockStyle.Top,Height=42,PlaceholderText="한국어 또는 영어로 검색 (다이아 / diamond)"};
        var list=new ExtensionResultsList{Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=58,IntegralHeight=false};
        list.DrawItem+=(_,e)=>{if(e.Index<0)return;var item=(GiveItem)list.Items[e.Index];using var fill=new SolidBrush((e.State&DrawItemState.Selected)!=0?WarmTheme.Peach:WarmTheme.Surface);e.Graphics.FillRectangle(fill,e.Bounds);var icon=art.Load(item.Id);if(icon!=null)e.Graphics.DrawImage(icon,Artwork.Fit(icon,new Rectangle(e.Bounds.X+8,e.Bounds.Y+9,40,40)));else ReferenceIcons.Draw(e.Graphics,"grid",new Rectangle(e.Bounds.X+10,e.Bounds.Y+12,30,30),WarmTheme.Muted);TextRenderer.DrawText(e.Graphics,item.Korean,Font,new Rectangle(58,e.Bounds.Y+4,list.Width-86,26),WarmTheme.Ink,TextFormatFlags.EndEllipsis);TextRenderer.DrawText(e.Graphics,item.English,Font,new Rectangle(58,e.Bounds.Y+29,list.Width-86,25),WarmTheme.Muted,TextFormatFlags.EndEllipsis);};
        void Filter(){list.BeginUpdate();try{list.Items.Clear();list.Items.AddRange(Items.Where(x=>x.Matches(search.Text.Trim())).Cast<object>().ToArray());if(list.Items.Count>0)list.SelectedIndex=0;}finally{list.EndUpdate();}}
        void Select(){if(list.SelectedItem is not GiveItem item)return;Selected=item;Text=item.Korean+" ▾";popup?.Close();SelectionChanged?.Invoke();}
        search.TextChanged+=(_,_)=>Filter();search.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;Select();}if(e.KeyCode==Keys.Down){list.Focus();e.Handled=true;}};list.MouseClick+=(_,_)=>Select();list.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;Select();}};
        panel.Controls.Add(list);panel.Controls.Add(search);var host=new ToolStripControlHost(panel){AutoSize=false,Size=panel.Size,Padding=Padding.Empty,Margin=Padding.Empty};popup=new ToolStripDropDown{AutoSize=false,Size=new Size(panel.Width+2,panel.Height+2),Padding=new Padding(1),AutoClose=true};popup.Items.Add(host);popup.Closed+=(_,_)=>{var old=popup;popup=null;BeginInvoke(new Action(()=>old?.Dispose()));};Filter();popup.Show(this,new Point(0,Height));search.Focus();
    }
    protected override void Dispose(bool disposing){if(disposing){popup?.Dispose();art.Dispose();}base.Dispose(disposing);}
}
