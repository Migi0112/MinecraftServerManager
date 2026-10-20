// [소스 안내] 기준 크기에 맞춰 컨트롤을 배치하는 레이아웃과 아이콘 표시 도구입니다.
using System.Drawing.Drawing2D;
namespace NeoForgeServerManager;

// 승인 시안의 좌표를 한 곳에 보관합니다. 부모가 작아져도 각 요소의 상대 간격을 유지합니다.
public sealed class ReferencePanel : Panel
{
    readonly List<(Control Control, Rectangle Bounds)> placements = [];
    public Size ReferenceSize { get; set; } = new(1532, 892);
    public bool DrawCardBackground {get;set;}=true;
    public ReferencePanel() { DoubleBuffered = true; }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if(Equals(Tag,"landscape")){Scenery.Paint(e.Graphics,this);return;}
        if(!DrawCardBackground){e.Graphics.Clear(BackColor);return;}
        if(Parent is not SurfaceCard){base.OnPaintBackground(e);return;}
        Scenery.CardBase(e.Graphics,Parent!);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using var path=WarmTheme.Round(new Rectangle(1,1,Width-3,Height-3),18);using var fill=new SolidBrush(WarmTheme.Surface);using var pen=new Pen(WarmTheme.Line);
        e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(pen,path);
    }
    public void Place(Control control, int x, int y, int width, int height)
    {
        control.AutoSize = false; control.Dock = DockStyle.None;
        placements.Add((control, new Rectangle(x,y,width,height))); Controls.Add(control); control.BringToFront(); PerformLayout();
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e); if (placements == null) return;
        float sx = ClientSize.Width / (float)ReferenceSize.Width, sy = ClientSize.Height / (float)ReferenceSize.Height;
        foreach(var p in placements) p.Control.SetBounds((int)Math.Round(p.Bounds.X*sx),(int)Math.Round(p.Bounds.Y*sy),(int)Math.Round(p.Bounds.Width*sx),(int)Math.Round(p.Bounds.Height*sy));
    }
}

// 작은 문자 기호 대신 크기에 독립적인 선 아이콘을 그립니다.
public static class ReferenceIcons
{
    public static void Draw(Graphics g, string name, Rectangle bounds, Color color)
    {
        var saved=g.Save(); g.TranslateTransform(bounds.X,bounds.Y);g.ScaleTransform(bounds.Width/24f,bounds.Height/24f);
        g.SmoothingMode=SmoothingMode.AntiAlias;using var p=new Pen(color,2){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round};using var b=new SolidBrush(color);
        switch(name)
        {
            case "refresh":g.DrawArc(p,4,4,16,16,40,290);g.DrawLines(p,new Point[]{new(15,3),new(21,4),new(20,10)});break;
            case "trash":g.DrawLine(p,4,6,20,6);g.DrawLines(p,new Point[]{new(6,6),new(7,21),new(17,21),new(18,6)});g.DrawLines(p,new Point[]{new(9,6),new(9,3),new(15,3),new(15,6)});g.DrawLine(p,10,10,10,17);g.DrawLine(p,14,10,14,17);break;
            case "grid":for(int x=3;x<20;x+=11)for(int y=3;y<20;y+=11)g.DrawRectangle(p,x,y,7,7);break;
            case "list":for(int y=5;y<=19;y+=7){g.FillEllipse(b,2,y-1,2,2);g.DrawLine(p,8,y,22,y);}break;
            case "clock":g.DrawEllipse(p,3,3,18,18);g.DrawLines(p,new Point[] {new(12,6),new(12,12),new(16,15)});break;
            case "users":g.FillEllipse(b,5,3,7,7);g.FillEllipse(b,14,5,6,6);g.FillPie(b,2,11,14,17,180,180);g.FillPie(b,12,12,11,14,180,180);break;
            case "memory":g.DrawRectangle(p,3,6,18,12);for(int x=6;x<=18;x+=4){g.DrawLine(p,x,3,x,6);g.DrawLine(p,x,18,x,21);}g.DrawLine(p,7,10,7,14);g.DrawLine(p,11,10,11,14);g.DrawLine(p,15,10,15,14);break;
            case "folder":using(var path=new GraphicsPath()){path.AddLines(new Point[] {new(2,7),new(2,4),new(9,4),new(12,7),new(22,7),new(22,21),new(2,21)});path.CloseFigure();g.FillPath(b,path);}break;
            case "search":g.DrawEllipse(p,3,3,13,13);g.DrawLine(p,15,15,21,21);break;
            case "server":g.DrawRectangle(p,3,3,18,8);g.DrawRectangle(p,3,13,18,8);g.FillEllipse(b,6,6,2,2);g.FillEllipse(b,6,16,2,2);g.DrawLine(p,13,7,18,7);g.DrawLine(p,13,17,18,17);break;
            case "console":g.DrawRectangle(p,2,3,20,18);g.DrawLines(p,new Point[]{new(7,7),new(12,12),new(7,17)});break;
            case "terminal":g.DrawLines(p,new Point[]{new(3,5),new(9,11),new(3,17)});g.DrawLine(p,12,18,21,18);break;
            case "shield":g.DrawLines(p,new Point[]{new(12,2),new(21,6),new(20,15),new(17,19),new(12,22),new(7,19),new(4,15),new(3,6),new(12,2)});break;
            case "crown":g.DrawLines(p,new Point[]{new(2,6),new(8,11),new(12,3),new(16,11),new(22,6),new(19,21),new(5,21),new(2,6)});break;
            case "info":g.DrawEllipse(p,2,2,20,20);g.DrawLine(p,12,10,12,17);g.FillEllipse(b,11,6,2,2);break;
            case "file":g.DrawLines(p,new Point[]{new(5,2),new(14,2),new(20,8),new(20,22),new(5,22),new(5,2)});g.DrawLines(p,new Point[]{new(14,2),new(14,8),new(20,8)});g.DrawLine(p,9,12,16,12);g.DrawLine(p,9,16,16,16);break;
            case "database":g.DrawEllipse(p,3,2,18,7);g.DrawLine(p,3,5,3,19);g.DrawLine(p,21,5,21,19);g.DrawArc(p,3,15,18,7,0,180);g.DrawArc(p,3,8,18,7,0,180);break;
            case "settings":
                // 톱니와 가운데 구멍을 하나의 도형으로 그려 작은 크기에서도 톱니바퀴로 보이게 합니다.
                using(var gear=new GraphicsPath(System.Drawing.Drawing2D.FillMode.Alternate))
                {var points=new PointF[48];for(int i=0;i<points.Length;i++){double a=i*Math.PI/24;float r=i%6 is 0 or 1 or 4 or 5?9:11.5f;points[i]=new PointF(12+(float)Math.Cos(a)*r,12+(float)Math.Sin(a)*r);}gear.AddPolygon(points);gear.AddEllipse(8,8,8,8);g.FillPath(b,gear);}break;
            case "folderOutline":g.DrawLines(p,new Point[]{new(2,7),new(2,4),new(9,4),new(12,7),new(22,7),new(22,21),new(2,21),new(2,7)});break;
            case "chart":g.DrawLine(p,3,20,3,14);g.DrawLine(p,9,20,9,10);g.DrawLine(p,15,20,15,6);g.DrawLine(p,21,20,21,2);break;
            case "cpu":g.DrawRectangle(p,5,5,14,14);g.DrawRectangle(p,9,9,6,6);for(int n=8;n<=16;n+=4){g.DrawLine(p,n,2,n,5);g.DrawLine(p,n,19,n,22);g.DrawLine(p,2,n,5,n);g.DrawLine(p,19,n,22,n);}break;
            case "java":using(var blue=new Pen(Color.FromArgb(33,121,175),1.5f)){g.DrawArc(blue,4,11,13,9,0,180);g.DrawLine(blue,4,14,6,19);g.DrawLine(blue,17,14,15,19);g.DrawArc(blue,16,12,5,6,270,180);g.DrawArc(blue,2,18,20,5,0,180);}using(var steam=new Pen(WarmTheme.Accent,1.5f))g.DrawBezier(steam,new Point(13,1),new(3,7),new(19,6),new(9,11));break;
        }
        g.Restore(saved);
    }
}

public sealed class ReferenceGlyph(string name) : Control
{
    public ReferenceGlyph() : this("folder") { }
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);ReferenceIcons.Draw(e.Graphics,name,Rectangle.Inflate(ClientRectangle,-2,-2),WarmTheme.Ink);}
}

public sealed class ServerStateBadge : Label
{
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Parent?.BackColor??WarmTheme.Surface);
        bool online=Text.StartsWith("온라인");string caption=online?"실행 중":Text;
        Color color=online?Color.FromArgb(0,159,51):WarmTheme.Muted;
        using var dot=new SolidBrush(online?Color.FromArgb(10,181,65):Color.FromArgb(193,192,189));g.FillEllipse(dot,2,7,16,16);
        int width=Math.Min(Width-28,TextRenderer.MeasureText(caption,Font).Width+24);
        if(width<=0)return;using var path=WarmTheme.Round(new Rectangle(26,1,width,27),13);using var fill=new SolidBrush(online?Color.FromArgb(221,247,222):Color.FromArgb(241,240,238));g.FillPath(fill,path);
        TextRenderer.DrawText(g,caption,Font,new Rectangle(26,1,width,27),color,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
    }
}

public sealed class StorageStrip : SurfaceCard
{
    public string ServerPath {get;set;}="";
    public string BackupPath {get;set;}="";
    readonly WarmButton change;
    public StorageStrip(Action open) {change=Ui.Action("경로 설정 →",open);change.Font=new Font("Noto Sans KR",15,FontStyle.Bold);Controls.Add(change);}
    protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);if(change!=null)change.SetBounds(Width-234,Math.Max(10,(Height-52)/2),210,52);}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);using var heading=new Font("Noto Sans KR",14,FontStyle.Bold);using var text=new Font("Noto Sans KR",12);
        int second=(int)(Width*.44);using var pen=new Pen(WarmTheme.Line);e.Graphics.DrawLine(pen,second-35,12,second-35,Height-12);
        for(int i=0;i<2;i++){int x=i==0?28:second+15;using var shape=WarmTheme.Round(new Rectangle(x,12,60,56),12);using var fill=new SolidBrush(WarmTheme.Background);e.Graphics.FillPath(fill,shape);ReferenceIcons.Draw(e.Graphics,"folder",new Rectangle(x+14,24,32,32),WarmTheme.Ink);
            TextRenderer.DrawText(e.Graphics,i==0?"서버 폴더":"백업 폴더",heading,new Rectangle(x+76,10,second-120,30),WarmTheme.Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(e.Graphics,i==0?ServerPath:BackupPath,text,new Rectangle(x+76,42,i==0?second-124:Width-x-330,30),WarmTheme.Muted,TextFormatFlags.EndEllipsis);}
    }
}

public sealed class CoralProgress : Control
{
    int value;
    public int Value{get=>value;set{this.value=Math.Clamp(value,0,100);Invalidate();}}
    public CoralProgress(){DoubleBuffered=true;}
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using var track=WarmTheme.Round(new Rectangle(0,0,Width-1,Height-1),Height/2);using var bg=new SolidBrush(WarmTheme.Line);e.Graphics.FillPath(bg,track);
        if(Value<=0)return;using var fill=WarmTheme.Round(new Rectangle(0,0,Math.Max(2,(Width-1)*Value/100),Height-1),Height/2);using var brush=new LinearGradientBrush(ClientRectangle,Color.FromArgb(255,143,115),WarmTheme.Accent,0f);e.Graphics.FillPath(brush,fill);
    }
}
