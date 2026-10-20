// [소스 안내] 프로그램 시작점. WinForms 초기화와 글꼴 로딩을 마친 뒤 서버목록(HomeForm)을 엽니다.
namespace NeoForgeServerManager;

internal static class Program
{
    [STAThread]
    // [동작 설명] 가장 먼저 실행됩니다. 화면 초기화 → 글꼴 준비 → HomeForm 실행 순서입니다.
    static void Main()
    {
        using var installationGuard = new Mutex(false, @"Local\Migi0112.MinecraftServerManager.Running");
        ApplicationConfiguration.Initialize();
        FontAssets.Load();
        Application.Run(new HomeForm());
    }
}


