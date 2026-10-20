// [소스 안내] 백업 복원 전에 대상과 내용을 확인하는 창입니다.
namespace NeoForgeServerManager;

// 복원 대상은 항상 새로운 폴더입니다. 확인 화면에서 이름과 위치를 함께 검토합니다.
public sealed class RestoreDialog : WarmWindow
{
    readonly WarmTextBox name=new(),destination=new();
    public string ServerName=>name.Text.Trim();
    public string Destination=>destination.Text.Trim();
    public RestoreDialog(string archive,Profile profile)
    {
        Size=new Size(590,670);MinimumSize=Size;StartPosition=FormStartPosition.CenterParent;EmbedInShell();Padding=Padding.Empty;
        var root=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=Size};Controls.Add(root);
        var close=Ui.Action("×",()=>{DialogResult=DialogResult.Cancel;Close();});close.Ghost=true;root.Place(close,526,14,44,44);
        var symbol=Ui.Heading("⟲",35);symbol.ForeColor=WarmTheme.Accent;symbol.TextAlign=ContentAlignment.MiddleCenter;root.Place(symbol,242,20,106,70);
        var heading=Ui.Heading("이 백업을 복원할까요?",24);heading.TextAlign=ContentAlignment.MiddleCenter;root.Place(heading,42,96,506,54);
        root.Place(Ui.Note("백업 파일을 새 폴더에 풀고 별도 서버로 등록합니다."),42,150,506,38);
        var card=new SurfaceCard{Padding=new Padding(18)};root.Place(card,42,184,506,90);
        var file=new Label{Dock=DockStyle.Fill,Text=$"{Path.GetFileName(archive)}\n{profile.Name} · {new FileInfo(archive).Length/1073741824d:F2} GB",Font=new Font("Noto Sans KR",14),AutoEllipsis=true};card.Controls.Add(file);
        root.Place(Ui.Note("등록할 서버 이름"),42,292,150,44);name.Text=profile.Name+" (복원)";root.Place(name,188,290,360,46);
        root.Place(Ui.Note("복원 위치"),42,344,506,38);
        destination.Text=Path.Combine(AppSettings.Load().ServerRoot,"Restored-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..6]);root.Place(destination,42,382,360,46);
        root.Place(Ui.Action("찾아보기",()=>{using var picker=new FolderBrowserDialog();if(picker.ShowDialog(this)==DialogResult.OK)destination.Text=Path.Combine(picker.SelectedPath,Path.GetFileName(destination.Text));}),414,382,134,46);
        root.Place(Ui.Note("ⓘ  기존 서버와 파일은 그대로 유지됩니다."),42,444,506,52);
        root.Place(Ui.Note("복원한 서버는 자동으로 시작하지 않습니다.\n같은 포트를 쓰는 서버는 동시에 실행할 수 없습니다."),42,504,506,66);
        var no=new WarmButton{Text="취소",DialogResult=DialogResult.Cancel};var yes=new WarmButton{Text="새 서버로 복원",Primary=true};root.Place(no,42,578,240,54);root.Place(yes,296,578,252,54);CancelButton=no;AcceptButton=yes;
        yes.Click+=(_,_)=>{if(string.IsNullOrWhiteSpace(ServerName)||string.IsNullOrWhiteSpace(Destination)){WarmDialog.Show(this,"서버 이름과 복원 위치를 입력하세요.");return;}if(Directory.Exists(Destination)||File.Exists(Destination)){WarmDialog.Show(this,"아직 없는 새 폴더를 지정하세요.");return;}DialogResult=DialogResult.OK;Close();};
        var credit=Ui.Note("제작자 Migi0112");credit.TextAlign=ContentAlignment.MiddleRight;root.Place(credit,296,636,252,30);WarmTheme.Apply(this);
    }
}
