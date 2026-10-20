// [소스 안내] 모드 관리 안에서 온라인 검색 화면을 여는 연결 코드입니다. 검색 종료 후 설치된 목록을 다시 표시합니다.
namespace NeoForgeServerManager;
public sealed partial class MainForm
{
    // 온라인 검색도 관리 화면 안에서 전환합니다. 상단 정보와 왼쪽 메뉴는 그대로 유지합니다.
    void OpenExtensionSearch()
    {
        if(!catalogs.TryGetValue("mods",out var catalog)||catalog.Parent is not TabPage page)return;
        if(page.Controls.OfType<ExtensionSearchForm>().Any())return;
        var search=new ExtensionSearchForm(CaptureProfile(),value=>{current.MinecraftVersion=value;RefreshDashboard();},canInstall:()=>!server.Running){TopLevel=false,Dock=DockStyle.Fill};search.EmbedInShell();catalog.Visible=false;page.Controls.Add(search);
        search.FormClosed+=(_,_)=>{page.Controls.Remove(search);catalog.Visible=true;RefreshModsCatalog();};search.Show();search.BringToFront();
    }
}
