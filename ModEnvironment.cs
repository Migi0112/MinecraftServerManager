// [소스 안내] 서버/클라이언트 설치 위치 배지와 의존성 선택 카드 디자인입니다. 코랄 체크박스·선택 배경과 미확인 정보 표시를 담당합니다.
namespace NeoForgeServerManager;

public static class ModEnvironment
{
    static string State(string? value)=>value switch{"required"=>"필수","optional"=>"선택","unsupported"=>"미지원",_=>"미확인"};
    public static string Summary(ModPluginService.Result? project)=>"서버 "+State(project?.ServerSide)+" · 클라 "+State(project?.ClientSide);
    // [동작 설명] 배포처의 required/optional/unsupported/unknown 값을 아이콘과 짧은 문구로 표시합니다. 설치되어 있다는 사실로 지원 환경을 추측하지 않습니다.
    public static void Draw(Graphics g,Rectangle bounds,ModPluginService.Result? project,Font font)
    {
        int width=(bounds.Width-8)/2;
        void Badge(int x,bool server,string? value){
            var rect=new Rectangle(x,bounds.Y,width,bounds.Height);
            using var shape=WarmTheme.Round(rect,7);using var fill=new SolidBrush(WarmTheme.Background);g.FillPath(fill,shape);
            Color color=value=="required"?WarmTheme.Accent:WarmTheme.Muted;using var pen=new Pen(color,1.5f);
            int ix=x+7,iy=rect.Y+(rect.Height-14)/2;
            g.DrawRectangle(pen,ix,iy,16,11);
            if(server){g.DrawLine(pen,ix,iy+5,ix+16,iy+5);g.DrawEllipse(pen,ix+3,iy+2,1,1);g.DrawEllipse(pen,ix+3,iy+8,1,1);}
            else{g.DrawLine(pen,ix+8,iy+11,ix+8,iy+15);g.DrawLine(pen,ix+4,iy+15,ix+12,iy+15);}
            TextRenderer.DrawText(g,(server?"서버 ":"클라 ")+(value=="unsupported"?"불가":value is null or "unknown"?"?":State(value)),font,new Rectangle(ix+22,rect.Y,width-30,rect.Height),color,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        }
        Badge(bounds.X,true,project?.ServerSide);Badge(bounds.X+width+8,false,project?.ClientSide);
    }
}
public sealed class ModEnvironmentBadges:Control
{
    public ModPluginService.Result? Project{get;set;}
    readonly ToolTip tip=new();
    public ModEnvironmentBadges(){DoubleBuffered=true;Font=new Font("Noto Sans KR",10);tip.SetToolTip(this,"배포처 기준 설치 위치 · 선택 항목도 다른 모드의 의존성으로 필요할 수 있어요.");AccessibleName="모드 설치 위치";}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);ModEnvironment.Draw(e.Graphics,ClientRectangle,Project,Font);}
    protected override void Dispose(bool disposing){if(disposing)tip.Dispose();base.Dispose(disposing);}
}
public sealed class DependencySelectionBox:CheckBox
{
    public DependencySelectionBox(){AutoSize=false;Size=new(30,30);Text="";Cursor=Cursors.Hand;AccessibleName="의존 모드 선택";SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnPaint(PaintEventArgs e){
        e.Graphics.Clear(BackColor);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var box=Rectangle.Inflate(ClientRectangle,-3,-3);using var shape=WarmTheme.Round(box,6);
        using var fill=new SolidBrush(Checked?WarmTheme.Accent:WarmTheme.Surface);using var pen=new Pen(Checked?WarmTheme.Accent:WarmTheme.Line,2);
        e.Graphics.FillPath(fill,shape);e.Graphics.DrawPath(pen,shape);
        if(Checked){using var tick=new Pen(Color.White,2.4f);e.Graphics.DrawLines(tick,new Point[]{new Point(box.Left+5,box.Top+12),new Point(box.Left+10,box.Top+17),new Point(box.Left+19,box.Top+7)});}
        if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,ClientRectangle);
    }
    protected override void OnCheckedChanged(EventArgs e){base.OnCheckedChanged(e);Invalidate();}
}
public sealed class DependencyCard:SurfaceCard
{
    public bool Selected{get;set;}
    protected override void OnPaintBackground(PaintEventArgs e){
        e.Graphics.Clear(Parent?.BackColor??WarmTheme.Background);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var shape=WarmTheme.Round(new Rectangle(1,1,Width-3,Height-3),18);using var fill=new SolidBrush(Selected?WarmTheme.Peach:WarmTheme.Surface);using var pen=new Pen(Selected?WarmTheme.Accent:WarmTheme.Line,Selected?2:1);
        e.Graphics.FillPath(fill,shape);e.Graphics.DrawPath(pen,shape);
    }
}


