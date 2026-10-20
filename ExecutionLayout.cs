// [소스 안내] 실행 옵션 화면의 배치입니다. Java 경로, 메모리, 재시작 관련 입력 컨트롤을 정렬합니다.
namespace NeoForgeServerManager;
public sealed partial class MainForm
{
    void ArrangeExecutionOptions(TabControl tabs)
    {
        var page=tabs.TabPages.Cast<TabPage>().First(p=>p.Text=="실행 옵션");
        IEnumerable<Control> Descend(Control parent){foreach(Control c in parent.Controls){yield return c;foreach(var d in Descend(c))yield return d;}}
        var buttons=Descend(page).OfType<WarmButton>().ToArray();var old=page.Controls.Cast<Control>().ToArray();
        var root=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new Size(1220,742)};
        root.Place(Ui.Heading("실행 옵션",24),18,0,1160,50);root.Place(Ui.Note("서버를 실행할 Java와 메모리를 설정하세요."),18,48,1160,32);
        ReferencePanel Section(int y,int h,string title,string description)
        {
            var card=new SurfaceCard{Padding=Padding.Empty};root.Place(card,0,y,1220,h);var panel=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new Size(1220,h),BackColor=WarmTheme.Surface};card.Controls.Add(panel);panel.Place(new ReferenceGlyph(title.StartsWith("Java")?"java":title.StartsWith("메모리")?"memory":title.StartsWith("크래시")?"clock":"settings"),24,22,42,42);
            panel.Place(Ui.Heading(title,18),90,14,1100,42);panel.Place(Ui.Note(description),90,54,1100,32);return panel;
        }
        var j=Section(84,192,"Java 설정","서버를 실행할 Java 실행 파일을 선택하세요.");j.Place(Ui.Note("Java 실행 파일"),90,92,180,44);j.Place(java,275,86,780,48);var browse=buttons.First(b=>b.Text=="찾아보기");j.Place(browse,1066,86,140,48);var check=buttons.First(b=>b.Text=="Java 버전 확인");j.Place(check,1020,140,186,44);
        var javaState=Ui.Note("Java 버전은 ‘버전 확인’에서 확인할 수 있습니다.");j.Place(javaState,275,143,720,40);
        var memory=Section(286,150,"메모리 설정","");memory.Place(Ui.Note("최소 메모리 (Xms)"),310,22,260,32);memory.Place(xms,310,58,244,48);memory.Place(Ui.Note("≤"),578,65,50,40);memory.Place(Ui.Note("최대 메모리 (Xmx)"),650,22,260,32);memory.Place(xmx,650,58,260,48);
        var restartCard=Section(446,148,"크래시 후 자동 재시작","");autoRestart.Text="";restartCard.Place(autoRestart,1140,18,60,36);restartCard.Place(Ui.Note("재시작 대기 (초)"),390,22,330,30);restartCard.Place(restartDelay,390,57,350,48);restartCard.Place(Ui.Note("최대 재시도"),840,22,330,30);restartCard.Place(restartLimit,840,57,360,48);
        var cancelRestart=buttons.First(b=>b.Text=="재시작 예약 취소");restartCard.Place(cancelRestart,90,102,260,38);
        var advanced=Section(604,72,"고급 실행 설정","");advanced.Place(Ui.Note("TPS 조회 명령"),310,17,170,40);advanced.Place(tpsCommand,490,12,490,48);var refresh=buttons.First(b=>b.Text=="파일 목록 새로고침");advanced.Place(refresh,996,12,210,48);
        var save=Button("설정 저장",SaveProfile,true);root.Place(save,1062,688,146,48);var reset=Button("변경 취소",()=>Apply(current));root.Place(reset,900,688,150,48);root.Place(Ui.Note("변경한 실행 설정은 다음 서버 시작부터 적용됩니다."),16,694,860,40);
        foreach(var c in old){page.Controls.Remove(c);c.Dispose();}page.Controls.Add(root);
    }
}
