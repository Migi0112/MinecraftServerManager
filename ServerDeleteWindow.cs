// [소스 안내] 서버 삭제 확인 화면입니다. 서버 폴더와 백업을 삭제할지 각각 선택하게 합니다.
using System.Drawing.Drawing2D;
namespace NeoForgeServerManager;
public sealed class ServerDeleteWindow : WarmWindow
{
    public DeleteOption ServerOption{get;}=new("서버 폴더도 삭제","월드, 모드, 플러그인과 서버 설정이 함께 삭제됩니다.");
    public DeleteOption BackupOption{get;}=new("백업 파일도 삭제","이 서버로 확인된 백업 파일만 삭제합니다.");
    public ServerDeleteWindow(ServerDeletionPlan plan)
    {
        Text="서버 삭제";Size=new(800,820);MaximizeBox=MinimizeBox=false;EmbedInShell();Padding=Padding.Empty;
        var layout=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=Size};Controls.Add(layout);
        var icon=new DeleteMark();layout.Place(icon,36,30,100,100);
        layout.Place(Ui.Heading("서버를 삭제할까요?",25),164,28,570,56);
        var name=Ui.Heading(plan.Profile.Name,21);layout.Place(name,164,84,560,48);
        layout.Place(Ui.Note("삭제할 항목을 각각 확인해 주세요."),164,136,560,38);
        var close=Ui.Action("×",()=>{DialogResult=DialogResult.Cancel;Close();});close.MinimumSize=Size.Empty;close.Ghost=true;layout.Place(close,738,20,40,40);
        layout.Place(Ui.Note("서버 프로필은 서버목록에서 제거됩니다."),36,192,728,38);
        ServerOption.PathText=plan.ServerFolder;BackupOption.PathText=plan.Backups.Length==0?"이 서버로 확인된 백업 파일이 없습니다.":$"백업 {plan.Backups.Length}개 · "+Path.GetDirectoryName(plan.Backups[0]);
        layout.Place(ServerOption,36,240,728,166);layout.Place(BackupOption,36,422,728,166);
        var notice=new Label{Text="선택을 해제한 항목은 그대로 보관됩니다.\n삭제한 파일은 되돌릴 수 없어요. 선택 항목을 확인해 주세요.",BackColor=WarmTheme.Peach,ForeColor=WarmTheme.Ink,Font=new Font("Noto Sans KR",12),Padding=new Padding(18,12,18,8)};layout.Place(notice,36,606,728,82);
        var summary=Ui.Note("");summary.Font=new Font("Noto Sans KR",10);summary.TextAlign=ContentAlignment.MiddleCenter;layout.Place(summary,36,772,728,30);
        void Update()=>summary.Text="서버 프로필"+(ServerOption.Checked?" + 서버 폴더":"")+(BackupOption.Checked?" + 백업 파일":"")+" 삭제";
        ServerOption.CheckedChanged+=(_,_)=>Update();BackupOption.CheckedChanged+=(_,_)=>Update();Update();
        var cancel=Ui.Action("취소",()=>{DialogResult=DialogResult.Cancel;Close();});layout.Place(cancel,36,710,350,58);
        var confirm=Ui.Action("선택 항목 삭제",()=>{DialogResult=DialogResult.OK;Close();},true);confirm.IconName="trash";layout.Place(confirm,402,710,362,58);AcceptButton=confirm;CancelButton=cancel;
        var paths=new ToolTip{AutoPopDelay=15000};paths.SetToolTip(ServerOption,plan.ServerFolder);paths.SetToolTip(BackupOption,string.Join("\n",plan.Backups));FormClosed+=(_,_)=>paths.Dispose();
        WarmTheme.Apply(this);
    }
}
public sealed class DeleteOption : CheckBox
{
    readonly string heading,description;public string PathText{get;set;}="";
    public DeleteOption(string title,string detail){heading=title;description=detail;Checked=true;Cursor=Cursors.Hand;AccessibleName=title;AutoSize=false;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;g.Clear(WarmTheme.Background);g.SmoothingMode=SmoothingMode.AntiAlias;
        using var border=WarmTheme.Round(new Rectangle(1,1,Width-3,Height-3),16);using var fill=new SolidBrush(WarmTheme.Surface);using var line=new Pen(WarmTheme.Line);g.FillPath(fill,border);g.DrawPath(line,border);
        var check=new Rectangle(24,24,30,30);using var box=WarmTheme.Round(check,6);using var color=new SolidBrush(Checked?WarmTheme.Accent:WarmTheme.Surface);g.FillPath(color,box);g.DrawPath(line,box);
        if(Checked){using var tick=new Pen(Color.White,3){StartCap=LineCap.Round,EndCap=LineCap.Round};g.DrawLines(tick,new Point[]{new(31,39),new(38,46),new(48,32)});}
        using var bold=new Font("Noto Sans KR",16,FontStyle.Bold);using var normal=new Font("Noto Sans KR",12);using var small=new Font("Noto Sans KR",10);
        TextRenderer.DrawText(g,heading,bold,new Rectangle(76,19,Width-200,40),WarmTheme.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g,"기본 선택",small,new Rectangle(Width-112,23,96,34),WarmTheme.Accent,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter);
        TextRenderer.DrawText(g,description,normal,new Rectangle(76,62,Width-92,34),WarmTheme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        var path=new Rectangle(76,107,Width-98,42);using var pathFill=new SolidBrush(WarmTheme.Background);using var pathShape=WarmTheme.Round(path,9);g.FillPath(pathFill,pathShape);ReferenceIcons.Draw(g,"folder",new Rectangle(88,117,22,22),WarmTheme.Muted);
        TextRenderer.DrawText(g,PathText,small,new Rectangle(122,107,Width-152,42),WarmTheme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.PathEllipsis);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,Rectangle.Inflate(ClientRectangle,-8,-8));
    }
}

public sealed class DeleteMark:Control
{
    protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(WarmTheme.Background);g.SmoothingMode=SmoothingMode.AntiAlias;using var shape=WarmTheme.Round(ClientRectangle,24);using var fill=new SolidBrush(WarmTheme.Peach);g.FillPath(fill,shape);ReferenceIcons.Draw(g,"trash",new Rectangle(24,24,Width-48,Height-48),WarmTheme.Accent);}
}
