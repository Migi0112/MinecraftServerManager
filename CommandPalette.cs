// [소스 안내] 명령어 팔레트입니다. 명령 검색, 즐겨찾기/최근 기록, 아이템·플레이어 선택과 실행 전 명령문 생성을 담당합니다.
using System.Text.Json;
using System.Text.RegularExpressions;
namespace NeoForgeServerManager;

public sealed record PaletteCommand(string Id,string Name,string Category,string Description,string Example,string Form="direct",bool Confirm=false,string Minimum="1.13",string Group="기본");
public sealed class PalettePreferences
{
    public List<PaletteCommand> Custom {get;set;}=[];
    public HashSet<string> Favorites {get;set;}=[];
    public List<string> Recent {get;set;}=[];
    static string PathName=>Path.Combine(Storage.Root,"command-palette.json");
    public static PalettePreferences Load(){try{return JsonSerializer.Deserialize<PalettePreferences>(File.ReadAllText(PathName))??new();}catch{return new();}}
    public void Save()=>Storage.AtomicWrite(PathName,JsonSerializer.Serialize(this));
}
public static class PaletteCommands
{
    public static readonly PaletteCommand[] BuiltIn=[
        new("give","아이템 지급","아이템","이름으로 찾은 아이템을 지급합니다.","give <플레이어> <아이템> <수량>","give"),
        new("time","시간 설정","월드","월드 시간을 바꿉니다.","time set day","time"),
        new("weather","날씨 변경","월드","월드 날씨를 바꿉니다.","weather clear","weather"),
        new("gamemode","게임 모드","플레이어","플레이어의 게임 모드를 바꿉니다.","gamemode survival <플레이어>","gamemode"),
        new("tp","위치 이동","플레이어","플레이어를 지정한 좌표로 이동합니다.","tp <플레이어> 0 80 0","tp"),
        new("kick","강퇴","플레이어","플레이어의 접속을 끊습니다.","kick <플레이어>","player",true),
        new("ban","블랙리스트 추가","플레이어","플레이어의 접속을 차단합니다.","ban <플레이어>","player",true),
        new("pardon","블랙리스트 해제","플레이어","차단한 플레이어를 해제합니다.","pardon <플레이어>","player",true),
        new("op","운영자 권한 부여","플레이어","운영자 권한을 부여합니다.","op <플레이어>","player",true),
        new("deop","운영자 권한 해제","플레이어","운영자 권한을 해제합니다.","deop <플레이어>","player",true),
        new("list","접속자 조회","서버","현재 접속자 목록을 요청합니다.","list"),
        new("help","명령어 도움말","서버","서버가 제공하는 명령어 도움말을 확인합니다.","help"),
        new("save-all","월드 저장","서버","월드 데이터를 저장합니다.","save-all flush"),
        new("say","전체 공지","서버","서버 전체에 메시지를 보냅니다.","say 안녕하세요"),
        new("difficulty","난이도","월드","월드 난이도를 변경합니다.","difficulty normal"),
        new("gamerule","게임 규칙","월드","규칙 이름과 값을 입력합니다.","gamerule keepInventory true"),
        new("experience","경험치","플레이어","경험치 또는 레벨을 더합니다.","experience add <플레이어> 1 levels"),
        new("effect","효과","플레이어","상태 효과를 적용합니다.","effect give <플레이어> minecraft:speed 30 0"),
        new("enchant","인챈트","아이템","손에 든 아이템에 인챈트를 적용합니다.","enchant <플레이어> minecraft:unbreaking 1"),
        new("clear","아이템 제거","아이템","대상의 아이템을 제거합니다.","clear <플레이어>",Confirm:true),
        new("kill","대상 제거","플레이어","지정한 대상을 제거합니다.","kill <플레이어>",Confirm:true),
        new("whitelist","화이트리스트","서버","접속 허용 목록을 관리합니다.","whitelist list",Confirm:true),
        new("stop","서버 종료","서버","서버를 정상 종료합니다.","stop",Confirm:true),
        new("reload","데이터 다시 읽기","서버","데이터팩을 다시 읽습니다.","reload",Confirm:true),
        new("summon","엔티티 소환","월드","지정한 엔티티를 소환합니다.","summon minecraft:pig 0 80 0"),
        new("setblock","블록 설치","월드","좌표의 블록을 교체합니다.","setblock 0 80 0 minecraft:stone",Confirm:true),
        new("fill","영역 채우기","월드","좌표 범위를 블록으로 채웁니다.","fill 0 70 0 2 72 2 minecraft:stone",Confirm:true),
        new("clone","영역 복사","월드","블록 영역을 복사합니다.","clone 0 70 0 2 72 2 10 70 10",Confirm:true),
        new("seed","시드 확인","월드","월드 시드를 조회합니다.","seed"),
        new("spawnpoint","개인 스폰 위치","플레이어","부활 위치를 설정합니다.","spawnpoint <플레이어> 0 80 0"),
        new("setworldspawn","월드 스폰 위치","월드","월드 스폰을 설정합니다.","setworldspawn 0 80 0"),
        new("tellraw","서식 메시지","플레이어","JSON 메시지를 전송합니다.","tellraw @a {\"text\":\"안녕하세요\"}"),
        new("title","화면 제목","플레이어","플레이어 화면에 제목을 표시합니다.","title @a title {\"text\":\"안녕하세요\"}"),
        new("playsound","소리 재생","플레이어","플레이어에게 소리를 재생합니다.","playsound minecraft:block.note_block.pling master <플레이어>"),
        new("particle","파티클","월드","지정 위치에 파티클을 표시합니다.","particle minecraft:flame 0 80 0 0 0 0 0 1"),
        new("execute","조건 실행","서버","복잡한 실행 조건은 직접 입력합니다.","execute as @a run say 안녕하세요",Confirm:true),
        new("data","데이터 조회·변경","월드","NBT 데이터 명령입니다. 사용 구문을 확인하세요.","data get entity <플레이어>",Confirm:true),
        new("scoreboard","점수판","서버","점수판을 관리합니다.","scoreboard objectives list"),
        new("team","팀","플레이어","팀을 관리합니다.","team list"),
        new("bossbar","보스바","서버","보스바를 관리합니다.","bossbar list"),
        new("datapack","데이터팩","서버","데이터팩을 조회합니다.","datapack list"),
        new("advancement","발전 과제","플레이어","발전 과제를 지급합니다.","advancement grant <플레이어> only minecraft:story/root"),
        new("recipe","제작법","플레이어","제작법을 해제합니다.","recipe give <플레이어> *"),
        new("worldborder","월드 경계","월드","경계 크기를 조회하거나 설정합니다.","worldborder get",Confirm:true),
        new("locate","구조물 찾기","월드","서버 버전별 구문을 확인하세요.","locate",Minimum:"1.13"),
        new("attribute","능력치","플레이어","엔티티 능력치를 조회하거나 변경합니다.","attribute",Minimum:"1.16",Confirm:true),
        new("item","아이템 슬롯 조작","아이템","서버 버전별 슬롯 구문을 입력합니다.","item",Minimum:"1.17",Confirm:true),
        new("place","구조물 배치","월드","구조물 배치 구문을 입력합니다.","place",Minimum:"1.19",Confirm:true),
        new("damage","피해 적용","플레이어","지정 대상에게 피해를 줍니다.","damage <플레이어> 1",Minimum:"1.19.4",Confirm:true),
        new("tick","틱 제어","서버","서버 틱 상태를 조회합니다.","tick query",Minimum:"1.20.3",Confirm:true),
        new("ban-ip","IP 차단","서버","IP 접속을 차단합니다.","ban-ip <IP>",Confirm:true),
        new("banlist","차단 목록","서버","차단 목록을 조회합니다.","banlist"),
        new("pardon-ip","IP 차단 해제","서버","IP 차단을 해제합니다.","pardon-ip <IP>",Confirm:true),
        new("defaultgamemode","기본 게임 모드","서버","기본 게임 모드를 지정합니다.","defaultgamemode survival"),
        new("debug","디버그 기록","서버","성능 기록을 시작합니다.","debug start",Confirm:true),
        new("forceload","청크 강제 로드","월드","강제 로드 청크를 조회합니다.","forceload query"),
        new("function","함수 실행","서버","데이터팩 함수를 실행합니다.","function <함수>",Confirm:true),
        new("me","행동 메시지","플레이어","행동 메시지를 보냅니다.","me 안녕하세요"),
        new("msg","귓속말","플레이어","개인 메시지를 보냅니다.","msg <플레이어> 안녕하세요"),
        new("save-off","자동 저장 중지","서버","자동 저장을 중지합니다. 완료 후 save-on으로 복구하세요.","save-off",Confirm:true),
        new("save-on","자동 저장 시작","서버","자동 저장을 재개합니다.","save-on",Confirm:true),
        new("setidletimeout","자리 비움 제한","서버","자동 퇴장 대기 시간을 분으로 지정합니다.","setidletimeout 0"),
        new("spectate","관전 대상","플레이어","대상을 관전합니다.","spectate <대상> <플레이어>",Minimum:"1.15"),
        new("spreadplayers","플레이어 분산","플레이어","플레이어를 지정 영역에 분산합니다.","spreadplayers 0 0 10 100 false @a",Confirm:true),
        new("stopsound","소리 중지","플레이어","대상의 소리를 중지합니다.","stopsound @a"),
        new("tag","태그","플레이어","대상의 태그를 관리합니다.","tag <플레이어> list"),
        new("teammsg","팀 채팅","플레이어","플레이어 실행 문맥이 필요한 명령입니다.","execute as <플레이어> run teammsg 안녕하세요",Minimum:"1.14"),
        new("schedule","함수 예약","서버","데이터팩 함수를 예약합니다.","schedule function <함수> 10s",Minimum:"1.14",Confirm:true),
        new("jfr","성능 프로파일","서버","Java Flight Recorder 기록을 시작합니다.","jfr start",Minimum:"1.18",Confirm:true),
        new("random","난수","서버","범위의 난수를 생성합니다.","random value 1..10",Minimum:"1.20.2"),
        new("return","함수 반환","서버","함수 실행 문맥에서 사용합니다.","return 1",Minimum:"1.20",Confirm:true),
        new("transfer","서버 이동","플레이어","다른 서버로 플레이어를 이동시킵니다.","transfer <서버주소> 25565 <플레이어>",Minimum:"1.20.5",Confirm:true)
    ];
    public static bool Compatible(PaletteCommand cmd,string version)=>Version.TryParse(version,out var current)&&Version.TryParse(cmd.Minimum,out var min)&&current>=min;
    // [동작 설명] 선택한 명령 종류와 입력으로 전송할 명령문을 만듭니다. 이 단계는 명령 작성이며 실제 전송은 UI 실행 동작에서 합니다.
    public static string Build(PaletteCommand cmd,string player,string option,int count,GiveItem? item)
    {
        if(cmd.Form is "give" or "gamemode" or "tp" or "player")PlayerData.ValidateName(player);
        string result=cmd.Form switch{
            "give"=>item==null?throw new ArgumentException("아이템을 검색해서 선택하세요."):$"give {player} {item.Id} {count}",
            "time"=>"time set "+option,"weather"=>"weather "+option,"gamemode"=>$"gamemode {option} {player}",
            "tp"=>Regex.IsMatch(option,@"^-?\d+(?:\.\d+)? -?\d+(?:\.\d+)? -?\d+(?:\.\d+)?$")?$"tp {player} {option}":throw new ArgumentException("X Y Z 좌표를 숫자 세 개로 입력하세요."),
            "player"=>cmd.Id+" "+player,_=>option.Trim().TrimStart('/')};
        if(result.Length==0||result.Any(char.IsControl)||result.Contains('<')||result.Contains('>'))throw new ArgumentException("예시의 입력값을 실제 값으로 바꿔 주세요. 명령은 한 줄이어야 합니다.");
        return result;
    }
}

public sealed class CommandPaletteWindow:WarmWindow
{
    readonly PalettePreferences preferences=PalettePreferences.Load();
    readonly System.Windows.Forms.Timer timer=new(){Interval=400};
    public CommandPaletteWindow(Profile profile,Func<bool> online,Func<string,Task> send,Func<string> console, IEnumerable<string> players,Func<Task<GiveItem[]>>? loadItems=null,Func<string[]>? livePlayers=null)
    {
        Text="명령어 팔레트";Size=new(1180,850);MinimumSize=new(1000,760);
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),ColumnCount=2,RowCount=4};root.ColumnStyles.Add(new(SizeType.Percent,34));root.ColumnStyles.Add(new(SizeType.Percent,66));root.RowStyles.Add(new(SizeType.Absolute,56));root.RowStyles.Add(new(SizeType.Absolute,116));root.RowStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.Absolute,50));Controls.Add(root);
        var heading=Ui.Heading("명령어 팔레트");heading.Dock=DockStyle.Fill;root.Controls.Add(heading,0,0);var back=Ui.Action("← 콘솔",Close);back.Dock=DockStyle.Right;root.Controls.Add(back,1,0);
        var filters=new FlowLayoutPanel{Dock=DockStyle.Fill};root.Controls.Add(filters,0,1);root.SetColumnSpan(filters,2);
        var tab=new WarmCombo{Width=200};tab.Items.AddRange(["자주 사용","전체 기본 명령어","모드·플러그인","내 명령어","즐겨찾기"]);tab.SelectedIndex=0;
        var search=new WarmTextBox{Width=300,PlaceholderText="이름·설명·명령 검색"};var category=new WarmCombo{Width=150};category.Items.AddRange(["전체","플레이어","월드","아이템","서버"]);category.SelectedIndex=0;var compatible=new WarmToggle{Text="호환 버전만",Checked=true,Width=180};
        var tabs=new FlowLayoutPanel{Width=1080,Height=48,WrapContents=false};foreach(string name in tab.Items.Cast<string>()){var b=Ui.Action(name,()=>tab.SelectedItem=name);b.AutoSize=false;b.Size=new Size(name=="전체 기본 명령어"?220:180,42);tabs.Controls.Add(b);}filters.Resize+=(_,_)=>{tabs.Width=Math.Max(500,filters.ClientSize.Width-8);foreach(Control b in tabs.Controls)b.Width=Math.Max(100,(tabs.Width-40)/5);};filters.Controls.Add(tabs);filters.Controls.AddRange([search,category,compatible]);
        var list=new ExtensionResultsList{Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,Font=new Font("Noto Sans KR",12),DisplayMember="Name",DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=74,IntegralHeight=false};
        list.DrawItem+=(_,e)=>{if(e.Index<0)return;var c=(PaletteCommand)list.Items[e.Index];var r=Rectangle.Inflate(e.Bounds,-5,-4);using var shape=WarmTheme.Round(r,10);using var fill=new SolidBrush((e.State&DrawItemState.Selected)!=0?WarmTheme.Peach:WarmTheme.Surface);using var pen=new Pen((e.State&DrawItemState.Selected)!=0?WarmTheme.Accent:WarmTheme.Line);e.Graphics.FillPath(fill,shape);e.Graphics.DrawPath(pen,shape);TextRenderer.DrawText(e.Graphics,c.Name,list.Font,new Rectangle(r.X+14,r.Y+8,r.Width-28,28),WarmTheme.Ink,TextFormatFlags.EndEllipsis);using var small=new Font("Noto Sans KR",9);TextRenderer.DrawText(e.Graphics,c.Category+" · "+c.Description,small,new Rectangle(r.X+14,r.Y+37,r.Width-28,24),WarmTheme.Muted,TextFormatFlags.EndEllipsis);};root.Controls.Add(list,0,2);
        var body=new TableLayoutPanel{Dock=DockStyle.Top,Height=730,ColumnCount=1,RowCount=10,Padding=new Padding(20,0,0,0),BackColor=WarmTheme.Surface};var bodyScroll=new BufferedScrollPanel{Dock=DockStyle.Fill,AutoScroll=true};bodyScroll.Controls.Add(body);var rightHost=new Panel{Dock=DockStyle.Fill};rightHost.Controls.Add(bodyScroll);root.Controls.Add(rightHost,1,2);
        foreach(float height in new[]{64,48,48,48,48,48,48,60,62})body.RowStyles.Add(new(SizeType.Absolute,height));body.RowStyles.Add(new(SizeType.Percent,100));
        var title=Ui.Heading("명령 선택",18);title.Dock=DockStyle.Fill;body.Controls.Add(title,0,0);var description=Ui.Note("");description.Dock=DockStyle.Fill;body.Controls.Add(description,0,1);
        void Field(string caption,Control control,int row){var line=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};line.ColumnStyles.Add(new(SizeType.Absolute,136));line.ColumnStyles.Add(new(SizeType.Percent,100));var label=Ui.Note(caption);label.Font=new Font("Noto Sans KR",10);label.Dock=DockStyle.Fill;line.Controls.Add(label,0,0);line.Controls.Add(control,1,0);body.Controls.Add(line,0,row);control.VisibleChanged+=(_,_)=>label.Visible=control.Visible;}
        var target=new OnlinePlayerPicker(()=>online()?(livePlayers?.Invoke()??players.ToArray()):[]){Dock=DockStyle.Fill};Field("대상 플레이어",target,2);
        var picker=new GiveItemPicker(profile.Folder){Dock=DockStyle.Fill};Field("아이템 선택",picker,3);
        var option=new WarmTextBox{Dock=DockStyle.Fill,PlaceholderText="명령 입력 / 좌표"};Field("입력값 / 명령",option,4);
        var count=new WarmNumber{Minimum=1,Maximum=2304,Value=1,Dock=DockStyle.Left,Width=180};Field("지급 수량",count,5);
        var preview=new WarmTextBox{Dock=DockStyle.Fill,ReadOnly=true};Field("명령 미리보기",preview,6);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=58,Padding=new Padding(18,4,0,0)};rightHost.Controls.Add(actions);body.RowStyles[7].Height=0;
        var status=Ui.Note("서버 응답은 공유 콘솔에서 확인합니다.");status.Dock=DockStyle.Fill;body.Controls.Add(status,0,8);
        var output=new RichTextBox{Dock=DockStyle.Fill,ReadOnly=true,BorderStyle=BorderStyle.None,BackColor=WarmTheme.Background,Font=new Font("Noto Sans KR",10)};body.Controls.Add(output,0,9);
        var note=Ui.Note("아이템은 해당 버전의 게임·모드 자료 기준입니다. 명령 지원과 실행 결과는 서버에서 최종 확인됩니다.");note.Font=new Font("Noto Sans KR",9);note.Dock=DockStyle.Fill;root.Controls.Add(note,0,3);root.SetColumnSpan(note,2);
        PaletteCommand? selected=null;string latest="";bool sending=false;
        void UpdatePreview(){try{preview.Text=selected==null?"":PaletteCommands.Build(selected,target.SelectedName,option.Text,(int)count.Value,picker.Selected);}catch(Exception e){preview.Text="";status.Text=e.Message;}}
        var run=Ui.Action("실행",async()=>{if(selected==null||sending)return;try{target.RefreshPlayers();string command=PaletteCommands.Build(selected,target.SelectedName,option.Text,(int)count.Value,picker.Selected);if(!online())throw new IOException("서버가 온라인일 때 실행할 수 있습니다.");if(!PaletteCommands.Compatible(selected,profile.MinecraftVersion))throw new IOException("서버 버전을 확인하세요. 이 명령의 지원 범위와 맞지 않습니다.");if(selected.Confirm||selected.Group!="기본"||selected.Form=="direct"){if(WarmDialog.Show(this,command+"\n실행할까요?","명령 실행 확인",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;}sending=true;await send(command);preferences.Recent.Remove(selected.Id);preferences.Recent.Insert(0,selected.Id);preferences.Recent=preferences.Recent.Take(20).ToList();preferences.Save();status.Text="명령을 전송했습니다. 아래 서버 응답을 확인하세요.";}catch(Exception e){WarmDialog.Show(this,e.Message);}finally{sending=false;}},true);
        run.Enabled=false;
        void Render(){foreach(var b in tabs.Controls.OfType<WarmButton>())b.Primary=b.Text==tab.Text;var keep=selected?.Id;IEnumerable<PaletteCommand> items=PaletteCommands.BuiltIn.Concat(preferences.Custom);items=tab.Text switch{"전체 기본 명령어"=>items.Where(c=>c.Group=="기본"),"모드·플러그인"=>items.Where(c=>c.Group=="모드·플러그인"),"내 명령어"=>preferences.Custom,"즐겨찾기"=>items.Where(c=>preferences.Favorites.Contains(c.Id)),_=>items.OrderBy(c=>{int i=preferences.Recent.IndexOf(c.Id);return i<0?999:i;})};items=items.Where(c=>(category.Text=="전체"||c.Category==category.Text)&&(!compatible.Checked||PaletteCommands.Compatible(c,profile.MinecraftVersion))&&(c.Name+" "+c.Description+" "+c.Example).Contains(search.Text,StringComparison.OrdinalIgnoreCase));list.BeginUpdate();list.Items.Clear();list.Items.AddRange(items.Cast<object>().ToArray());list.EndUpdate();if(list.Items.Count>0)list.SelectedItem=list.Items.Cast<PaletteCommand>().FirstOrDefault(c=>c.Id==keep)??list.Items[0];else{selected=null;title.Text="표시할 명령이 없습니다";preview.Text="";}}
        actions.Controls.AddRange([Ui.Action("☆ 즐겨찾기",()=>{if(selected==null)return;if(!preferences.Favorites.Add(selected.Id))preferences.Favorites.Remove(selected.Id);preferences.Save();status.Text=preferences.Favorites.Contains(selected.Id)?"즐겨찾기에 추가했습니다.":"즐겨찾기에서 해제했습니다.";}),Ui.Action("복사",()=>{UpdatePreview();if(preview.Text.Length>0)Clipboard.SetText(preview.Text);}),run]);
        var add=Ui.Action("내 명령어 추가",()=>{string? name=WarmDialog.Input(this,"내 명령어","표시할 이름");if(string.IsNullOrWhiteSpace(name))return;string? command=WarmDialog.Input(this,"내 명령어","한 줄 명령을 입력하세요",preview.Text);if(string.IsNullOrWhiteSpace(command)||command.Any(char.IsControl))return;var group=tab.Text=="모드·플러그인"?"모드·플러그인":"내 명령어";preferences.Custom.Add(new("custom-"+Guid.NewGuid().ToString("N"),name,"서버","직접 등록한 명령 · 서버 지원 여부를 확인하세요.",command,Confirm:true,Group:group));preferences.Save();tab.SelectedItem=group;Render();});filters.Controls.Add(add);
        filters.Controls.Add(Ui.Action("선택 명령 삭제",()=>{if(selected==null||selected.Group=="기본")return;if(WarmDialog.Show(this,selected.Name+"을 삭제할까요?","명령 삭제",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;preferences.Custom.RemoveAll(c=>c.Id==selected.Id);preferences.Favorites.Remove(selected.Id);preferences.Save();Render();}));
        list.SelectedIndexChanged+=(_,_)=>{selected=list.SelectedItem as PaletteCommand;if(selected==null)return;title.Text=selected.Name;description.Text=selected.Description;target.Visible=selected.Form is "give" or "player" or "tp" or "gamemode";picker.Visible=selected.Form=="give";count.Visible=selected.Form=="give";option.Visible=selected.Form!="give"&&selected.Form!="player";body.RowStyles[2].Height=selected.Form is "give" or "player" or "tp" or "gamemode"?48:0;body.RowStyles[3].Height=selected.Form=="give"?48:0;body.RowStyles[4].Height=selected.Form!="give"&&selected.Form!="player"?48:0;body.RowStyles[5].Height=selected.Form=="give"?48:0;body.Height=selected.Form=="give"?620:560;option.Text=selected.Form switch{"time"=>"day","weather"=>"clear","gamemode"=>"survival","tp"=>"0 80 0",_=>selected.Example};UpdatePreview();};
        search.TextChanged+=(_,_)=>Render();tab.SelectedIndexChanged+=(_,_)=>Render();category.SelectedIndexChanged+=(_,_)=>Render();compatible.CheckedChanged+=(_,_)=>Render();target.SelectionChanged+=UpdatePreview;option.TextChanged+=(_,_)=>UpdatePreview();count.ValueChanged+=(_,_)=>UpdatePreview();picker.SelectionChanged+=UpdatePreview;
        timer.Tick+=(_,_)=>{target.RefreshPlayers();run.Enabled=online()&&!sending&&selected!=null&&(!(selected.Form is "give" or "player" or "tp" or "gamemode")||target.SelectedName.Length>0);string text=console();if(text.Length>5000)text=text[^5000..];if(text!=latest){latest=text;output.Text=text;output.SelectionStart=output.TextLength;output.ScrollToCaret();}};
        Shown+=async(_,_)=>{timer.Start();picker.Enabled=false;picker.Text="아이템 이름 자료 불러오는 중…";try{var items=await (loadItems?.Invoke()??GiveItemCatalog.Load(profile));if(!IsDisposed){picker.Items=items;picker.RefreshArtwork();picker.Enabled=true;picker.Text="한국어·영어로 아이템 검색 ▾";status.Text=$"아이템 {picker.Items.Length:N0}개 · 목록을 스크롤해 선택할 수 있습니다.";}}catch(Exception e){if(!IsDisposed){picker.Text="아이템 자료를 불러오지 못했어요";status.Text=e.Message;}}};FormClosed+=(_,_)=>timer.Stop();Render();WarmTheme.Apply(this);
    }
    protected override void Dispose(bool disposing){if(disposing)timer.Dispose();base.Dispose(disposing);}
}

public sealed partial class MainForm
{
    void OpenCommandPalette()
    {
        Control? parent=console.Parent;while(parent!=null&&parent is not TabPage)parent=parent.Parent;
        if(parent is not TabPage consolePage||consolePage.Parent is not TabControl tabs)return;
        var page=tabs.TabPages.Cast<TabPage>().Single(p=>p.Text=="명령어 팔레트");tabs.SelectedTab=page;
        if(page.Controls.OfType<CommandPaletteWindow>().Any())return;
        var dialog=new CommandPaletteWindow(CaptureProfile(),()=>server.Online,c=>server.SendAsync(c),()=>console.Text,server.Players,livePlayers:()=>server.Players.ToArray()){TopLevel=false,Dock=DockStyle.Fill};
        dialog.EmbedInShell();page.Controls.Add(dialog);dialog.FormClosed+=(_,_)=>{page.Controls.Remove(dialog);tabs.SelectedTab=consolePage;dialog.Dispose();};dialog.Show();dialog.BringToFront();
    }
}