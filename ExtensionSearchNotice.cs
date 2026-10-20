// [소스 안내] 온라인 검색 실패 시 사용자에게 오류와 재시도 선택을 보여주는 안내 창입니다.
using System.Net.Http;
namespace NeoForgeServerManager;
public sealed class ExtensionSearchNotice:WarmWindow
{
    public ExtensionSearchNotice(Exception error)
    {
        Text="온라인 검색 안내";Size=new(660,420);MinimumSize=Size;MaximizeBox=false;MinimizeBox=false;
        string title=error is HttpRequestException h?(h.StatusCode.HasValue?"배포처 요청 오류":"인터넷 연결을 확인해 주세요"):error is TaskCanceledException?"배포처 응답이 지연되고 있어요":error.Message.Contains("호환")||error.Message.Contains("배포 파일")?"호환되는 파일이 없어요":"설치를 완료하지 못했어요";
        var card=new SurfaceCard{Dock=DockStyle.Fill,Padding=new Padding(24)};Controls.Add(card);var heading=Ui.Heading(title,18);heading.Dock=DockStyle.Top;heading.Height=70;
        var note=Ui.Note(error is HttpRequestException or TaskCanceledException?"잠시 후 다시 시도해 주세요. 인터넷 연결과 배포처 상태를 확인하세요.":error.Message);note.Dock=DockStyle.Fill;
        var row=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=58,FlowDirection=FlowDirection.RightToLeft};row.Controls.Add(Ui.Action("닫기",()=>{DialogResult=DialogResult.Cancel;Close();}));if(error is HttpRequestException or TaskCanceledException)row.Controls.Add(Ui.Action("다시 시도",()=>{DialogResult=DialogResult.Retry;Close();},true));
        card.Controls.Add(note);card.Controls.Add(row);card.Controls.Add(heading);WarmTheme.Apply(this);
    }
}
