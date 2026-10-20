// [소스 안내] 업데이트 연결·다운로드·검증 진행률과 취소 동작을 표시하는 창입니다.
namespace NeoForgeServerManager;

public sealed class UpdateProgressWindow:WarmWindow
{
    readonly Label stage=Ui.Heading("업데이트 서버에 연결하고 있어요",19);
    readonly Label detail=Ui.Note("연결을 준비하고 있습니다. 잠시만 기다려 주세요.");
    readonly UpdateProgressTrack bar=new();
    readonly WarmButton cancel;
    readonly Action cancelDownload;bool canClose;
    public UpdateProgressWindow(Action cancelAction)
    {
        cancelDownload=cancelAction;Text="프로그램 업데이트";Size=new(680,390);StartPosition=FormStartPosition.CenterParent;MaximizeBox=false;MinimizeBox=false;BackColor=WarmTheme.Background;
        var card=new SurfaceCard{Bounds=new Rectangle(24,76,632,218)};stage.SetBounds(22,18,580,54);detail.SetBounds(22,84,580,62);bar.SetBounds(22,166,580,14);card.Controls.AddRange([stage,detail,bar]);Controls.Add(card);
        cancel=Ui.Action("다운로드 취소",()=>{if(cancel!=null)cancel.Enabled=false;detail.Text="다운로드를 취소하고 있어요…";cancelDownload();});cancel.SetBounds(476,310,180,44);Controls.Add(cancel);
        FormClosing+=(_,e)=>{if(!canClose){e.Cancel=true;cancelDownload();detail.Text="다운로드를 취소하고 있어요…";}};
    }
    public void Stage(string message){if(IsDisposed)return;stage.Text=message;detail.Text="인터넷 연결 상태와 배포 서버 응답에 따라 잠시 걸릴 수 있습니다.";bar.Marquee=true;}
    public void DownloadPercent(int percent){if(IsDisposed)return;stage.Text="새 버전을 다운로드하고 있어요";detail.Text=$"다운로드 {percent}% · 완료되면 파일을 검증합니다.";bar.Marquee=false;bar.Value=Math.Clamp(percent,0,100);}
    public void Installing(){canClose=true;Stage("설치를 준비하고 있어요");detail.Text="설치가 끝나면 메인 화면으로 다시 시작합니다.";cancel.Enabled=false;}
    public void Finish(){canClose=true;Close();}
}

public sealed class UpdateProgressTrack:Control
{
    readonly System.Windows.Forms.Timer timer=new(){Interval=30};int offset,value;bool marquee=true;
    public bool Marquee{get=>marquee;set{marquee=value;Invalidate();}}
    public int Value{get=>value;set{this.value=Math.Clamp(value,0,100);Invalidate();}}
    public UpdateProgressTrack(){DoubleBuffered=true;timer.Tick+=(_,_)=>{if(marquee){offset=(offset+5)%Math.Max(1,Width+100);Invalidate();}};timer.Start();}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Parent?.BackColor??WarmTheme.Surface);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using var path=WarmTheme.Round(new Rectangle(0,0,Width-1,Height-1),Math.Max(2,Height/2));using var track=new SolidBrush(WarmTheme.Peach);using var fill=new SolidBrush(WarmTheme.Accent);e.Graphics.FillPath(track,path);e.Graphics.SetClip(path);e.Graphics.FillRectangle(fill,marquee?offset-100:0,0,marquee?100:Width*value/100,Height);}
    protected override void Dispose(bool disposing){if(disposing)timer.Dispose();base.Dispose(disposing);}
}
