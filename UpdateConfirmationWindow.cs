// [소스 안내] 업데이트/이전 버전 설치 전에 버전과 변경 내용을 확인하는 창입니다.
namespace NeoForgeServerManager;

public sealed class UpdateConfirmationWindow:WarmWindow
{
    public UpdateConfirmationWindow(AppRelease release)
    {
        bool older=Version.Parse(release.Version)<AppUpdates.Current;Size=new(760,610);Text="업데이트 확인";MaximizeBox=MinimizeBox=false;EmbedInShell();Padding=Padding.Empty;
        var layout=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=Size};Controls.Add(layout);
        var icon=new PictureBox{Image=new Bitmap(Artwork.Default),SizeMode=PictureBoxSizeMode.Zoom};icon.Disposed+=(_,_)=>icon.Image?.Dispose();layout.Place(icon,30,28,82,82);
        layout.Place(Ui.Heading(older?"이전 버전으로 바꿀까요?":"새 버전으로 업데이트할까요?",23),134,28,574,70);
        var close=Ui.Action("×",()=>{DialogResult=DialogResult.Cancel;Close();});close.Name="UpdateCloseButton";close.MinimumSize=Size.Empty;close.Padding=Padding.Empty;close.Ghost=true;layout.Place(close,698,16,40,40);
        var card=new SurfaceCard{Padding=Padding.Empty};layout.Place(card,30,136,700,242);var content=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new(700,242),BackColor=WarmTheme.Surface};card.Controls.Add(content);
        content.Place(Ui.Heading("v"+release.Version.Replace("1.12.","12."),22),24,14,640,54);
        var title=Ui.Note(release.Title);title.Font=new Font("Noto Sans KR",15,FontStyle.Bold);content.Place(title,24,76,650,52);
        var notes=Ui.Note(release.Notes);notes.Font=new Font("Noto Sans KR",12);content.Place(notes,24,138,650,90);
        var info=Ui.Note(older?"이전 버전에서는 일부 새 기능을 사용할 수 없어요.\n설치가 끝나면 메인 화면으로 다시 시작합니다.":"다운로드 → 파일 확인 → 설치 → 메인 화면으로 재시작\n서버와 백업 데이터는 그대로 유지됩니다.");info.Font=new Font("Noto Sans KR",13);layout.Place(info,34,400,690,76);
        var cancel=Ui.Action("나중에",()=>{DialogResult=DialogResult.Cancel;Close();});layout.Place(cancel,30,500,334,64);
        var yes=Ui.Action("다운로드 · 설치",()=>{DialogResult=DialogResult.OK;Close();},true);layout.Place(yes,380,500,350,64);AcceptButton=yes;CancelButton=cancel;
        var credit=Ui.Note("제작자 Migi0112");credit.TextAlign=ContentAlignment.MiddleRight;layout.Place(credit,440,572,290,30);WarmTheme.Apply(this);
    }
}
