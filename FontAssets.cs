// [소스 안내] 프로그램에 포함된 글꼴을 불러와 화면 컨트롤에서 사용할 수 있게 합니다.
using System.Runtime.InteropServices;
namespace NeoForgeServerManager;

public static class FontAssets
{
    // 이 프로세스 안에서만 글꼴을 등록합니다. Windows 글꼴 설정은 변경하지 않습니다.
    public static void Load()
    {
        // Windows에 같은 글꼴이 설치되어 있으면 중복 등록하지 않습니다.
        using(var installed=new System.Drawing.Text.InstalledFontCollection())
            if(installed.Families.Any(f=>f.Name=="Noto Sans KR"))return;
        string path=Path.Combine(AppContext.BaseDirectory,"Assets","Fonts","NotoSansKR-VF.ttf");
        if(!File.Exists(path)||AddFontResourceEx(path,0x10,IntPtr.Zero)==0)
            throw new IOException("화면 글꼴을 불러오지 못했습니다. Assets/Fonts/NotoSansKR-VF.ttf를 Windows에 설치한 뒤 다시 실행해 주세요.");
        AppDomain.CurrentDomain.ProcessExit+=(_,_)=>RemoveFontResourceEx(path,0x10,IntPtr.Zero);
    }
    [DllImport("gdi32.dll",CharSet=CharSet.Unicode)]static extern int AddFontResourceEx(string file,uint flags,IntPtr reserved);
    [DllImport("gdi32.dll",CharSet=CharSet.Unicode)]static extern bool RemoveFontResourceEx(string file,uint flags,IntPtr reserved);
}
