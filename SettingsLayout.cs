// [소스 안내] 설정 화면의 시각적 배치를 구성합니다. 실제 설정 값과 저장 동작은 Settings.cs 등의 코드와 연결됩니다.
namespace NeoForgeServerManager;
public sealed partial class SettingsForm
{
    // 기존 네트워크 작업 이벤트는 유지하고 입력창과 버튼만 승인 시안의 카드에 재배치합니다.
    void ArrangeNetwork(string page,SurfaceCard oldCard,FlowLayoutPanel flow,WarmCombo profiles,WarmNumber external,WarmNumber internalPort,Label status)
    {
        IEnumerable<Control> Descendants(Control root){foreach(Control c in root.Controls){yield return c;foreach(var child in Descendants(c))yield return child;}}
        var controls=Descendants(flow).ToArray();var buttons=controls.OfType<WarmButton>().ToArray();
        var layout=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new Size(1188,892)};
        layout.Place(Ui.Heading(page,36),42,0,1100,70);
        var subtitle=Ui.Note(page=="방화벽 설정"?"서버 접속에 필요한 Windows 방화벽 규칙을 관리하세요.":"외부에서 서버에 접속할 수 있도록 공유기를 설정하세요.");subtitle.Font=new Font("Noto Sans KR",16);layout.Place(subtitle,42,72,1100,44);
        ReferencePanel Section(int y,int height,string title,string caption)
        {
            var surface=new SurfaceCard{Padding=Padding.Empty};layout.Place(surface,42,y,1112,height);
            var panel=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new Size(1112,height),BackColor=WarmTheme.Surface};surface.Controls.Add(panel);panel.Place(new ReferenceGlyph(title.Contains("규칙")||title.Contains("포트")?"file":title.Contains("네트워크")?"shield":"server"),24,22,52,52);
            panel.Place(Ui.Heading(title,21),108,12,970,46);var note=Ui.Note(caption);note.Font=new Font("Noto Sans KR",14);panel.Place(note,108,55,970,38);return panel;
        }
        void PlaceButton(ReferencePanel panel,string text,int x,int y,int width,int height)
        {var b=buttons.FirstOrDefault(b=>b.Text==text);if(b==null)return;b.Font=new Font("Noto Sans KR",15,FontStyle.Bold);panel.Place(b,x,y,width,height);}
        if(page=="방화벽 설정")
        {
            var select=Section(116,194,"서버 선택","방화벽 규칙을 설정할 서버를 선택하세요.");profiles.Font=new Font("Noto Sans KR",15);select.Place(profiles,108,84,760,46);PlaceButton(select,"상태 다시 확인",882,84,210,46);
            select.Place(Ui.Note("서버 포트 · TCP"),108,142,200,40);select.Place(external,310,138,190,42);
            var scope=controls.OfType<WarmCombo>().First(c=>c!=profiles);
            var networks=Section(324,288,"허용할 네트워크","서버에 접속할 수 있는 네트워크 유형을 선택하세요.");scope.Font=new Font("Noto Sans KR",16);networks.Place(scope,108,84,970,70);
            networks.Place(Ui.Note("선택한 네트워크에 서버 TCP 포트의 인바운드 허용 규칙을 등록합니다."),108,172,720,38);
            PlaceButton(networks,"방화벽 자동 설정",850,174,240,64);
            networks.Place(Ui.Note("규칙을 추가하거나 해제할 때 Windows 관리자 권한 승인이 필요합니다."),108,240,970,36);
            var rules=Section(624,218,"등록된 규칙","선택한 서버에 이 프로그램이 만든 규칙의 상태를 표시합니다.");status.MaximumSize=Size.Empty;status.Font=new Font("Noto Sans KR",14);rules.Place(status,24,102,830,88);PlaceButton(rules,"규칙 삭제",866,118,220,50);
        }
        else
        {
            var address=controls.OfType<WarmTextBox>().First();
            var connection=Section(116,206,"공유기 연결 정보","현재 네트워크 연결 상태와 공유기 정보를 확인하세요.");connection.Place(Ui.Note("내 PC 주소"),108,106,220,46);connection.Place(address,330,104,390,48);PlaceButton(connection,"연결 상태 확인",884,104,210,48);
            var options=Section(334,250,"서버 연결 설정","포트포워딩할 서버와 포트 정보를 설정하세요.");options.Place(profiles,108,78,730,46);
            options.Place(Ui.Note("외부 포트"),108,136,110,44);options.Place(external,220,132,140,46);options.Place(Ui.Note("내부 포트"),380,136,110,44);options.Place(internalPort,492,132,140,46);options.Place(Ui.Note("프로토콜  TCP"),660,136,170,44);
            PlaceButton(options,"포트포워딩 자동 설정",858,132,232,64);options.Place(Ui.Note("자동 설정은 UPnP를 지원하고 활성화한 공유기에서 사용할 수 있어요."),108,204,970,36);
            var rules=Section(596,212,"등록된 포트","현재 선택한 연결 규칙의 조회 결과입니다.");status.MaximumSize=Size.Empty;status.Font=new Font("Noto Sans KR",14);rules.Place(status,24,96,830,90);PlaceButton(rules,"연결 규칙 삭제",866,112,220,52);
            PlaceButton(layout,"수동 설정 안내",722,826,200,52);PlaceButton(layout,"공유기 설정 열기",934,826,220,52);
            layout.Place(Ui.Note("자동 설정이 안 되나요? 공유기 연결 상태를 확인해 주세요."),42,830,660,46);
        }
        content.Controls.Remove(oldCard);oldCard.Dispose();content.Controls.Add(layout);
    }
}
