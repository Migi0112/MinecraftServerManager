// [소스 안내] 서버 카드/행의 핸들을 잡아 순서를 바꾸는 기능입니다. 드래그 중 위치 표시, 이동과 순서 저장을 처리합니다.
namespace NeoForgeServerManager;

public sealed class DragGrip : Control
{
    public DragGrip(){DoubleBuffered=true;Cursor=Cursors.SizeAll;BackColor=WarmTheme.Surface;AccessibleName="서버 순서 이동";AccessibleRole=AccessibleRole.Grip;TabStop=true;SetStyle(ControlStyles.Selectable,true);}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using var b=new SolidBrush(ClientRectangle.Contains(PointToClient(MousePosition))?WarmTheme.Accent:WarmTheme.Muted);for(int y=10;y<=26;y+=8){e.Graphics.FillEllipse(b,4,y,3,3);e.Graphics.FillEllipse(b,13,y,3,3);}}
    protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);Invalidate();}
    protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);Invalidate();}
}
// Captures only the grip: server controls retain their normal click behavior.
public sealed class HomeDragReorder : IDisposable
{
    readonly Panel host;readonly Action<string,string,bool> save;
    readonly System.Windows.Forms.Timer scroll=new(){Interval=70};
    DragAnimation? animation;bool settling;
    HomeServerCard? source,target;Point origin,last;bool moving,after;
    public HomeDragReorder(Panel host,Action<string,string,bool> save){this.host=host;this.save=save;scroll.Tick+=(_,_)=>{if(!moving)return;var p=host.PointToClient(last);int step=p.Y<36?-28:p.Y>host.Height-36?28:0;if(step!=0){host.AutoScrollPosition=new Point(0,Math.Max(0,-host.AutoScrollPosition.Y+step));Update(last);}};}
    public void Attach(HomeServerCard card){var grip=card.Grip;grip.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Escape){Cancel();e.Handled=true;}};grip.MouseDown+=(_,e)=>{if(e.Button==MouseButtons.Left){grip.Focus();Begin(card,grip.PointToScreen(e.Location));grip.Capture=true;}};grip.MouseMove+=(_,e)=>{if(source==card)Update(grip.PointToScreen(e.Location));};grip.MouseUp+=(_,e)=>{if(e.Button==MouseButtons.Left&&source==card)Finish();};grip.MouseCaptureChanged+=(_,_)=>{if(!grip.Capture&&source==card&&!settling)Cancel();};}
    public void Begin(HomeServerCard card,Point screen){if(settling)return;Cancel();source=card;origin=last=screen;}
    public void Update(Point screen){if(source==null||settling)return;last=screen;if(!moving&&Math.Abs(screen.X-origin.X)+Math.Abs(screen.Y-origin.Y)<SystemInformation.DragSize.Width)return;if(!moving)animation=new DragAnimation(host,source,origin);moving=true;scroll.Start();animation?.Follow(screen);
        if(target!=null){target.DropHighlight=false;target.Invalidate();}target=null;
        var p=host.PointToClient(screen);if(p.X<0||p.X>host.ClientSize.Width||p.Y<0||p.Y>host.ClientSize.Height)return;
        var candidates=host.Controls.OfType<HomeServerCard>().Where(c=>c!=source).ToArray();if(candidates.Length==0)return;
        target=candidates.OrderBy(c=>{double x=Math.Clamp(p.X,c.Left,c.Right)-p.X,y=Math.Clamp(p.Y,c.Top,c.Bottom)-p.Y;return x*x+y*y;}).First();
        after=source.Compact?p.Y>target.Top+target.Height/2:p.Y>target.Bottom?true:p.Y<target.Top?false:p.X>target.Left+target.Width/2;
        target.DropHighlight=true;target.Invalidate();animation?.Arrange(source,target,after);
    }
    public void Finish(){if(settling)return;var from=source;var to=target;bool insertAfter=after;bool valid=moving&&from!=null&&to!=null;
        if(!valid){Cancel();return;}scroll.Stop();settling=true;
        animation?.Land(()=>{Cancel();save(from!.Profile.Id,to!.Profile.Id,insertAfter);});
    }
    public void Cancel(){settling=false;animation?.Dispose();animation=null;scroll.Stop();var old=source;source=null;moving=false;if(target!=null){target.DropHighlight=false;target.Invalidate();}target=null;if(old!=null&&!old.IsDisposed)old.Grip.Capture=false;}
    public void Dispose(){Cancel();scroll.Dispose();}
}

// A single buffered preview surface animates snapshots; real controls and server sessions stay put.
public sealed class DragAnimation : Control
{
    sealed class Tile {public required HomeServerCard Card;public required Bitmap Image;public PointF Position;public Rectangle Goal;}
    readonly Panel host;readonly HomeServerCard dragged;readonly List<Tile> tiles=[];
    readonly System.Windows.Forms.Timer frames=new(){Interval=15};
    readonly Point grab;PointF floating,landFrom;Rectangle slot;bool landing;DateTime landStart;Action? completed;
    public DragAnimation(Panel host,HomeServerCard dragged,Point origin){this.host=host;this.dragged=dragged;DoubleBuffered=true;SetStyle(ControlStyles.Selectable,false);TabStop=false;
        grab=dragged.PointToClient(origin);foreach(var card in host.Controls.OfType<HomeServerCard>()){var image=new Bitmap(card.Width,card.Height);card.DrawToBitmap(image,card.ClientRectangle);tiles.Add(new Tile{Card=card,Image=image,Position=card.Location,Goal=card.Bounds});}
        floating=dragged.Location;slot=dragged.Bounds;host.Parent!.Controls.Add(this);Bounds=host.Bounds;BringToFront();frames.Tick+=(_,_)=>Tick();frames.Start();
    }
    public PointF PreviewPosition=>floating;
    public PointF CardPosition(string id)=>tiles.First(t=>t.Card.Profile.Id==id).Position;
    public void Follow(Point screen){if(landing)return;var p=host.PointToClient(screen);floating=new PointF(p.X-grab.X,p.Y-grab.Y-4);Bounds=host.Bounds;Invalidate();}
    public void Arrange(HomeServerCard source,HomeServerCard target,bool after){
        var order=tiles.Select(t=>t.Card).ToList();order.Remove(source);order.Insert(order.IndexOf(target)+(after?1:0),source);
        var slots=tiles.Select(t=>t.Card.Bounds).ToArray();for(int i=0;i<order.Count;i++){var tile=tiles.First(t=>t.Card==order[i]);tile.Goal=slots[i];if(order[i]==source)slot=slots[i];}Invalidate();
    }
    static PointF Ease(PointF p,PointF q)=>new(p.X+(q.X-p.X)*.32f,p.Y+(q.Y-p.Y)*.32f);
    void Tick(){foreach(var tile in tiles)tile.Position=Ease(tile.Position,tile.Goal.Location);
        if(landing){double t=Math.Clamp((DateTime.UtcNow-landStart).TotalMilliseconds/180d,0,1);float ease=(float)(1-Math.Pow(1-t,3));floating=new PointF(landFrom.X+(slot.X-landFrom.X)*ease,landFrom.Y+(slot.Y-landFrom.Y)*ease);if(t>=1){frames.Stop();var done=completed;completed=null;done?.Invoke();return;}}Invalidate();}
    public void Land(Action done){landing=true;landFrom=floating;completed=done;landStart=DateTime.UtcNow;}
    protected override void OnPaintBackground(PaintEventArgs e)=>Scenery.Paint(e.Graphics,this);
    protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using(var shape=WarmTheme.Round(Rectangle.Inflate(slot,-3,-3),16))using(var fill=new SolidBrush(Color.FromArgb(180,WarmTheme.Peach)))using(var pen=new Pen(WarmTheme.Accent,2){DashStyle=System.Drawing.Drawing2D.DashStyle.Dash}){g.FillPath(fill,shape);g.DrawPath(pen,shape);}
        void DrawTile(Bitmap image,Rectangle rect){var state=g.Save();using var clip=WarmTheme.Round(rect,17);g.SetClip(clip,System.Drawing.Drawing2D.CombineMode.Intersect);g.DrawImage(image,rect);g.Restore(state);}
        foreach(var tile in tiles.Where(t=>t.Card!=dragged))DrawTile(tile.Image,new Rectangle((int)tile.Position.X,(int)tile.Position.Y,tile.Goal.Width,tile.Goal.Height));
        var moving=tiles.First(t=>t.Card==dragged);var rect=new Rectangle((int)floating.X,(int)floating.Y,moving.Image.Width,moving.Image.Height);
        for(int n=8;n>=2;n-=2){using var shadow=WarmTheme.Round(new Rectangle(rect.X-n/2,rect.Y+5,rect.Width+n,rect.Height+n),18);using var brush=new SolidBrush(Color.FromArgb(12,50,35,24));g.FillPath(brush,shadow);}
        DrawTile(moving.Image,rect);
        using var edge=WarmTheme.Round(Rectangle.Inflate(rect,-1,-1),17);using var line=new Pen(WarmTheme.Accent,2);g.DrawPath(line,edge);
    }
    protected override void Dispose(bool disposing){if(disposing){frames.Stop();frames.Dispose();foreach(var tile in tiles)tile.Image.Dispose();completed=null;}base.Dispose(disposing);}
}
