// [소스 안내] 서버 설정 항목의 정의와 편집기입니다. 항목별 입력 방식, 표시와 설정 값 수집을 담당합니다.
namespace NeoForgeServerManager;

// 영어 키는 Minecraft가 읽는 이름입니다. 화면 이름만 한국어로 번역하고 파일의 키는 바꾸지 않습니다.
public record PropertyDefinition(string Key, string Korean, string Default, string Description, string Type = "text", decimal Min = 0, decimal Max = 2147483647, string[]? Options = null)
{
    public string Help => $"{Key} · {Korean}\n\n{Description}\n\n기본값(일반적인 최근 버전): {Default}\n허용 값: {(Type == "bool" ? "true / false" : Options != null ? string.Join(", ", Options) : Type == "number" ? $"{Min} ~ {Max}" : "문자열 (줄바꿈 없이)")}\n주의: 서버를 종료하고 변경하세요. 버전에 따라 기본값이나 지원 여부가 다를 수 있습니다. 기존 서버 파일의 값이 우선합니다.";
}
public static class PropertiesService
{
    public static readonly PropertyDefinition[] Definitions =
    [
        new("motd","서버 설명","A Minecraft Server","서버 목록에 표시할 소개 문구입니다."),
        new("server-port","서버 포트","25565","접속을 받을 TCP 포트입니다. 방화벽/공유기 설정과 일치해야 합니다.","number",1,65535),
        new("server-ip","수신 IP","","보통 비워 두면 모든 네트워크 인터페이스에서 접속을 받습니다."),
        new("max-players","최대 접속자","20","동시에 접속할 수 있는 최대 인원입니다.","number",1,100000),
        new("online-mode","정품 인증","true","Mojang 계정 인증을 확인합니다. 끄면 이름 사칭 위험이 있습니다.","bool"),
        new("white-list","화이트리스트","false","허용 목록에 등록한 플레이어만 접속하게 합니다.","bool"),
        new("enforce-whitelist","화이트리스트 강제 적용","false","목록에서 제거한 접속자를 퇴장시킵니다.","bool"),
        new("gamemode","게임 모드","survival","새 플레이어의 기본 모드입니다.","enum",Options:["survival","creative","adventure","spectator"]),
        new("difficulty","난이도","easy","몹과 피해량에 영향을 주는 난이도입니다.","enum",Options:["peaceful","easy","normal","hard"]),
        new("hardcore","하드코어","false","죽으면 일반적인 재접속 플레이가 제한됩니다.","bool"),
        new("force-gamemode","모드 강제 적용","false","접속 시 기본 게임 모드를 강제로 적용합니다.","bool"),
        new("pvp","플레이어 간 전투","true","플레이어끼리 피해를 줄 수 있게 합니다.","bool"),
        new("allow-flight","비행 허용","false","비행 판정으로 플레이어를 퇴장시키는 동작을 조절합니다.","bool"),
        new("allow-nether","네더 허용","true","네더 차원 이동을 허용합니다.","bool"),
        new("enable-command-block","명령 블록","false","명령 블록 실행을 허용합니다. 신뢰하는 운영자만 편집하게 하세요.","bool"),
        new("spawn-protection","스폰 보호 반경","16","스폰 주변 블록 변경을 보호합니다. 0은 비활성화입니다.","number",0,29999984),
        new("view-distance","시야 거리","10","플레이어 주변에 전송할 청크 반경입니다. 높이면 RAM/네트워크 사용량이 늘어납니다.","number",2,32),
        new("simulation-distance","시뮬레이션 거리","10","생물과 작물 등 게임 로직이 동작하는 청크 반경입니다.","number",2,32),
        new("level-name","월드 폴더 이름","world","월드가 저장되는 폴더입니다. 기존 월드 이름을 바꾸면 다른 월드가 열릴 수 있습니다."),
        new("level-seed","월드 시드","","새 월드 생성에 사용할 시드입니다. 기존 월드에는 적용되지 않습니다."),
        new("level-type","월드 유형","minecraft:normal","새 월드의 지형 생성 유형입니다.","enum",Options:["minecraft:normal","minecraft:flat","minecraft:large_biomes","minecraft:amplified","minecraft:single_biome_surface"]),
        new("generate-structures","구조물 생성","true","마을 등 구조물을 새 청크에 생성합니다.","bool"),
        new("generator-settings","생성기 설정","{}","월드 생성기에 전달하는 JSON입니다. 잘못된 값은 월드 생성을 실패하게 할 수 있습니다."),
        new("spawn-monsters","몬스터 생성","true","적대적 몹의 자연 생성을 허용합니다.","bool"),
        new("spawn-animals","동물 생성","true","동물의 자연 생성을 허용합니다. 일부 새 버전에서는 제거된 설정입니다.","bool"),
        new("spawn-npcs","주민 생성","true","주민 생성을 허용합니다. 일부 새 버전에서는 제거된 설정입니다.","bool"),
        new("enable-rcon","원격 콘솔","false","RCON 원격 명령을 허용합니다. 외부에 직접 노출하지 마세요.","bool"),
        new("rcon.port","RCON 포트","25575","원격 콘솔의 TCP 포트입니다.","number",1,65535),
        new("rcon.password","RCON 암호","","원격 관리용 비밀번호입니다. 파일에 평문으로 저장됩니다."),
        new("enable-query","서버 조회","false","외부 도구가 Query 프로토콜로 서버 정보를 읽게 합니다.","bool"),
        new("query.port","조회 포트","25565","Query의 UDP 포트입니다.","number",1,65535),
        new("max-tick-time","틱 시간 제한","60000","한 틱이 이 밀리초를 넘으면 워치독이 서버를 종료합니다. -1은 제한 해제입니다.","number",-1),
        new("player-idle-timeout","자리 비움 제한","0","분 단위 자동 퇴장 시간입니다. 0은 제한 없음입니다.","number"),
        new("max-world-size","월드 최대 크기","29999984","월드 경계 최대 반경입니다.","number",1,29999984),
        new("network-compression-threshold","압축 임계값","256","이 바이트 이상 패킷을 압축합니다. -1은 압축 비활성화입니다.","number",-1),
        new("op-permission-level","운영자 권한 단계","4","운영자 명령 권한입니다. 높은 단계일수록 강한 권한입니다.","number",1,4),
        new("function-permission-level","함수 권한 단계","2","데이터팩 함수의 명령 권한입니다.","number",1,4),
        new("enable-status","목록 상태 응답","true","멀티플레이 목록의 상태 조회에 응답합니다.","bool"),
        new("hide-online-players","접속자 숨기기","false","상태 응답에서 플레이어 명단을 숨깁니다.","bool"),
        new("enforce-secure-profile","보안 프로필 요구","true","서명된 프로필 키를 가진 클라이언트만 받습니다.","bool"),
        new("prevent-proxy-connections","프록시 접속 방지","false","인증 서버와 접속 IP의 일치 검사를 강화합니다.","bool"),
        new("resource-pack","리소스팩 URL","","클라이언트에 제안할 리소스팩 다운로드 주소입니다."),
        new("resource-pack-sha1","리소스팩 SHA1","","리소스팩의 SHA1 해시입니다."),
        new("require-resource-pack","리소스팩 필수","false","리소스팩 적용을 거부하는 접속자를 차단합니다.","bool"),
        new("resource-pack-prompt","리소스팩 안내","","리소스팩 적용 화면에 표시할 JSON 텍스트입니다."),
        new("broadcast-console-to-ops","콘솔 결과 알림","true","콘솔 명령 결과를 운영자에게 알립니다.","bool"),
        new("broadcast-rcon-to-ops","RCON 결과 알림","true","원격 명령 결과를 운영자에게 알립니다.","bool"),
        new("sync-chunk-writes","동기 청크 저장","true","저장 안정성을 위한 동기 쓰기를 사용합니다.","bool"),
        new("use-native-transport","네이티브 네트워크","true","지원 운영체제의 네이티브 네트워크 기능을 사용합니다.","bool"),
        new("enable-jmx-monitoring","JMX 모니터링","false","외부 Java 모니터링 기능을 켭니다.","bool"),
        new("rate-limit","패킷 제한","0","초당 패킷 제한입니다. 0은 제한 없음입니다.","number"),
        new("entity-broadcast-range-percentage","엔티티 전송 거리","100","엔티티 전송 거리의 비율입니다.","number",10,1000),
        new("text-filtering-config","텍스트 필터 설정","","외부 텍스트 필터 서비스 구성입니다. 일반 서버는 비워 두세요."),
        new("initial-enabled-packs","초기 활성 데이터팩","vanilla","새 월드에서 활성화할 데이터팩의 쉼표 구분 목록입니다."),
        new("initial-disabled-packs","초기 비활성 데이터팩","","새 월드에서 비활성화할 데이터팩의 쉼표 구분 목록입니다.")
    ];
    public static void WriteNew(string file, Dictionary<string, string> values)
    {
        foreach (var (k, v) in values) if (v.Contains('\n') || v.Contains('\r')) throw new ArgumentException(k + ": 줄바꿈은 사용할 수 없습니다.");
        // Java properties의 유니코드 이스케이프를 사용해 한글 MOTD도 인코딩 차이 없이 읽게 합니다.
        static string Escape(string s) => string.Concat(s.Select(c => c > 127 ? "\\u" + ((int)c).ToString("x4") : c.ToString()));
        Storage.AtomicWrite(file, string.Join(Environment.NewLine, values.Select(x => x.Key + "=" + Escape(x.Value))));
    }
}
// 기본/전체/검색은 화면에 보이는 행만 바꾸며 숨긴 설정의 값도 보존합니다.
public sealed class PropertiesEditor : UserControl
{
    readonly Dictionary<string, Control> editors = [];
    readonly List<(PropertyDefinition Definition, Control Row)> rows = [];
    readonly WarmTextBox search = new() { Width=420, PlaceholderText="설정 이름 또는 키 검색", LeadingIcon="search" };
    public Control Toolbar {get;}
    readonly BufferedTablePanel grid=new(){Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,Padding=new Padding(10)};
    Control? nameRow;
    bool all;
    public PropertiesEditor(Dictionary<string,string>? values=null)
    {
        Dock=DockStyle.Fill;
        var basicButton=Ui.Action("기본 설정",()=>{all=false;Filter();});basicButton.IconName="settings";basicButton.SoftSelected=true;
        var allButton=Ui.Action("전체 설정",()=>{all=true;Filter();});allButton.IconName="list";
        void Mark(){basicButton.SoftSelected=!all;allButton.SoftSelected=all;basicButton.Invalidate();allButton.Invalidate();}
        basicButton.Click+=(_,_)=>Mark();allButton.Click+=(_,_)=>Mark();var toolbar=Ui.Row(basicButton,allButton,search);
        Toolbar=toolbar;
        foreach(var button in toolbar.Controls.OfType<WarmButton>()){button.AutoSize=false;button.Size=new Size(200,58);button.Font=new Font("Noto Sans KR",15);}
        search.Height=58;toolbar.Resize+=(_,_)=>search.Width=Math.Max(180,toolbar.ClientSize.Width-432);
        var scroll=new BufferedScrollPanel{Dock=DockStyle.Fill,AutoScroll=true};
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));scroll.Controls.Add(grid);Controls.Add(scroll);Controls.Add(toolbar);
        var defs=values==null?PropertiesService.Definitions:values.Keys.Select(k=>PropertiesService.Definitions.FirstOrDefault(x=>x.Key==k)??new(k,"추가 설정",values[k],"이 서버에서 추가한 설정입니다. 서버 문서에서 의미와 허용 값을 확인하세요.")).ToArray();
        string[] orderedKeys=["motd","server-port","max-players","difficulty","gamemode","pvp","online-mode","white-list","view-distance"];
        foreach(var d in defs.OrderBy(d=>{int index=Array.IndexOf(orderedKeys,d.Key);return index<0?100:index;}))
        {
            string v=values?.GetValueOrDefault(d.Key)??d.Default;Control edit;
            if(d.Type=="bool"&&bool.TryParse(v,out bool b)) { var toggle=new WarmToggle{Large=true,Checked=b,Text=b?"활성화":"비활성화"}; toggle.CheckedChanged+=(_,_)=>toggle.Text=toggle.Checked?"활성화":"비활성화";edit=toggle; }
            else if(d.Type=="number"&&decimal.TryParse(v,out decimal n))edit=new WarmNumber{Minimum=Math.Min(d.Min,n),Maximum=Math.Max(d.Max,n),Value=n,Font=new Font("Noto Sans KR",14)};
            else if(d.Options!=null){var combo=new WarmCombo();combo.Items.AddRange(d.Options);if(!combo.Items.Contains(v))combo.Items.Add(v);combo.SelectedItem=v;edit=combo;}
            else edit=new WarmTextBox{Text=v,UseSystemPasswordChar=d.Key.Contains("password")};
            editors[d.Key]=edit;edit.Dock=DockStyle.Fill;
            var row=SettingRow();
            row.Controls.Add(new PropertyNameLabel(d.Korean,d.Key){Dock=DockStyle.Fill},0,0);row.Controls.Add(edit,1,0);
            var help=new PropertyHelpButton();PropertyHelpPopup.Attach(help,d);row.Controls.Add(help,2,0);help.MouseEnter+=(_,_)=>row.BackColor=WarmTheme.Peach;help.MouseLeave+=(_,_)=>row.BackColor=WarmTheme.Surface;
            grid.Controls.Add(row,0,grid.RowCount++);rows.Add((d,row));
        }
        search.TextChanged+=(_,_)=>Filter();Filter();
    }
    static TableLayoutPanel SettingRow()
    {
        var row=new TableLayoutPanel{Dock=DockStyle.Top,Height=46,ColumnCount=3,Margin=Padding.Empty};
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,46));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,54));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,44));return row;
    }
    public void SetServerName(WarmTextBox input)
    {
        if(nameRow!=null)return;grid.Padding=new Padding(10,4,10,4);
        grid.SuspendLayout();foreach(Control row in grid.Controls)grid.SetRow(row,grid.GetRow(row)+1);
        nameRow=SettingRow();input.Dock=DockStyle.Fill;
        ((TableLayoutPanel)nameRow).Controls.Add(new PropertyNameLabel("서버 이름",""){Dock=DockStyle.Fill},0,0);
        ((TableLayoutPanel)nameRow).Controls.Add(input,1,0);grid.RowCount++;grid.Controls.Add(nameRow,0,0);grid.ResumeLayout();
    }
    bool? lastReadOnly;
    public void SetReadOnly(bool readOnly)
    {
        if(lastReadOnly==readOnly)return;lastReadOnly=readOnly;
        foreach(var editor in editors.Values)
        {if(editor is WarmTextBox text)text.ReadOnly=readOnly;else editor.Enabled=!readOnly;}
    }
    void Filter()
    {
        string[] basic=["motd","server-port","max-players","difficulty","gamemode","pvp","online-mode","white-list","view-distance"];
        foreach(var (d,row) in rows)row.Visible=search.Text.Length>0?(d.Key+" "+d.Korean).Contains(search.Text,StringComparison.OrdinalIgnoreCase):all||basic.Contains(d.Key);
    }
    protected override void OnCreateControl(){base.OnCreateControl();WarmTheme.Apply(this);}
    public Dictionary<string,string> Values()=>editors.ToDictionary(x=>x.Key,x=>x.Value switch{CheckBox b=>b.Checked.ToString().ToLowerInvariant(),WarmNumber n=>n.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),_=>x.Value.Text});
}
// 한글 이름은 굵게, 실제 속성 키는 보통 굵기로 표시하여 시안의 정보 위계를 유지합니다.
public sealed class PropertyNameLabel(string korean,string key) : Control
{
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);using var bold=new Font("Noto Sans KR",14,FontStyle.Bold);using var normal=new Font("Noto Sans KR",14);
        string title=key=="pvp"?"PVP":key=="max-players"?"최대 플레이어":korean;
        int width=TextRenderer.MeasureText(title,bold,Size.Empty,TextFormatFlags.NoPadding).Width;
        TextRenderer.DrawText(e.Graphics,title,bold,ClientRectangle,WarmTheme.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
        if(key.Length>0)TextRenderer.DrawText(e.Graphics," ("+key+")",normal,new Rectangle(width+4,0,Math.Max(0,Width-width-4),Height),WarmTheme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
    }
}
public sealed class PropertyHelpButton : WarmButton
{
    public PropertyHelpButton(){Text="?";MinimumSize=Size.Empty;Size=new Size(36,42);Cursor=Cursors.Hand;AccessibleName="설정 도움말";}
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor??WarmTheme.Surface);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var r=new Rectangle((Width-28)/2,(Height-28)/2,28,28);using var fill=new SolidBrush(WarmTheme.Peach);e.Graphics.FillEllipse(fill,r);
        using var font=new Font("Noto Sans KR",11,FontStyle.Bold);TextRenderer.DrawText(e.Graphics,"?",font,r,WarmTheme.Accent,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
    }
}

