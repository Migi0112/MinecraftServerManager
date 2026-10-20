// [소스 안내] 창 가장자리와 모서리의 크기 조절 입력 판정입니다. 가로·세로·대각선 크기 조절을 지원합니다.
using System.Runtime.InteropServices;
namespace NeoForgeServerManager;

// Borderless windows need to route edge input even when a child control covers the border.
public sealed class WindowResizeInput(WarmWindow owner) : IMessageFilter
{
    public static int HitTest(Size size,Point p,int dpi=96)
    {
        if(p.X<0||p.Y<0||p.X>=size.Width||p.Y>=size.Height)return 0;
        int edge=Math.Max(10,(int)Math.Round(10*dpi/96d)),corner=Math.Max(24,(int)Math.Round(24*dpi/96d));
        bool left=p.X<edge,right=p.X>=size.Width-edge,top=p.Y<edge,bottom=p.Y>=size.Height-edge;
        if((left&&p.Y<corner)||(top&&p.X<corner))return 13;
        if((right&&p.Y<corner)||(top&&p.X>=size.Width-corner))return 14;
        if((left&&p.Y>=size.Height-corner)||(bottom&&p.X<corner))return 16;
        if((right&&p.Y>=size.Height-corner)||(bottom&&p.X>=size.Width-corner))return 17;
        return left?10:right?11:top?12:bottom?15:0;
    }
    public int HitForControl(Control? target,Point screen)
    {
        if(owner.IsDisposed||!owner.Enabled||!owner.CanResizeWindow)return 0;
        for(var control=target;control!=null;control=control.Parent)
            if(control==owner)return HitTest(owner.ClientSize,owner.PointToClient(screen),owner.DeviceDpi);
        return 0;
    }
    public bool PreFilterMessage(ref Message m)
    {
        if(m.Msg is not (0x20 or 0x200 or 0x201))return false;
        int hit=HitForControl(Control.FromChildHandle(m.HWnd),Cursor.Position);if(hit==0)return false;
        Cursor.Current=hit is 13 or 17?Cursors.SizeNWSE:hit is 14 or 16?Cursors.SizeNESW:hit is 10 or 11?Cursors.SizeWE:Cursors.SizeNS;
        if(m.Msg==0x201){ReleaseCapture();SendMessage(owner.Handle,0xA1,(IntPtr)hit,IntPtr.Zero);return true;}
        return m.Msg==0x20;
    }
    [DllImport("user32.dll")]static extern bool ReleaseCapture();
    [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr hwnd,int msg,IntPtr wParam,IntPtr lParam);
}
