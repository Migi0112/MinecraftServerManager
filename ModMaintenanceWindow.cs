// [소스 안내] 설치 파일을 한 번씩 검사하고 선택한 항목에만 업데이트·서버용 정리를 적용하는 화면입니다.
namespace NeoForgeServerManager;

public sealed class ModMaintenanceWindow : WarmWindow
{
    readonly Profile profile;
    readonly Func<bool> canChange;
    readonly bool cleanup;
    readonly DownloadClient net;
    readonly ModMaintenance service;
    readonly CancellationTokenSource cancellation=new();
    readonly ModArtwork artwork=new();
    readonly MaintenanceList list=new(){Dock=DockStyle.Fill,AutoScroll=true};
    readonly Label status=Ui.Note("");
    readonly FlowLayoutPanel actions=new(){Dock=DockStyle.Bottom,AutoSize=true,Padding=new Padding(0,12,0,0)};
    readonly Dictionary<ModAudit,DependencySelectionBox> picks=[];
    readonly List<Image> localImages=[];
    ModAuditReport? report;
    bool working,resourcesDisposed;
    public ModMaintenanceWindow(Profile profile,bool cleanup,Func<bool> canChange,DownloadClient? client=null)
    {
        this.profile=profile;this.cleanup=cleanup;this.canChange=canChange;net=client??new();service=new(net);
        Text=cleanup?"서버용 모드 정리":"설치된 모드 업데이트";Size=new(1040,800);MinimumSize=new(820,620);
        var body=new Panel{Dock=DockStyle.Fill,Padding=new Padding(24)};Controls.Add(body);
        var heading=Ui.Heading(Text);heading.Dock=DockStyle.Top;heading.Height=50;
        status.Dock=DockStyle.Top;status.Height=96;
        void Add(string title,Func<Task> run,bool primary=false)=>actions.Controls.Add(Ui.Action(title,()=>_=Run(run),primary));
        Add("다시 확인",Scan);Add("전체 선택",()=>{foreach(var pair in picks.Where(x=>Eligible(x.Key)))pair.Value.Checked=true;return Task.CompletedTask;});
        if(cleanup){Add("서버용 활성화",()=>Organize("enable"),true);Add("클라이언트 전용 끄기",()=>Organize("disable"));Add("클라이언트 전용 삭제",()=>Organize("delete"));}
        else Add("선택 업데이트",ApplyUpdates,true);
        actions.Controls.Add(Ui.Action("닫기",Close));
        body.Controls.Add(list);body.Controls.Add(actions);body.Controls.Add(status);body.Controls.Add(heading);
        list.SizeChanged+=(_,_)=>{foreach(Control row in list.Controls)row.Width=Math.Max(400,list.ClientSize.Width-22);};
        Shown+=(_,_)=>_=Run(Scan);
        FormClosing+=(_,e)=>{if(working){e.Cancel=true;cancellation.Cancel();status.Text="작업을 취소하고 있어요. 완료 후 닫아주세요.";}};WarmTheme.Apply(this);
    }
    async Task Run(Func<Task> action)
    {
        if(working)return;working=true;actions.Enabled=false;list.Enabled=false;
        try{await action();}
        catch(OperationCanceledException){status.Text="작업을 취소했어요.";CloseAfterCancellation();}
        catch(Exception e){status.Text=e.Message;WarmDialog.Show(this,e.Message,"모드 확인");}
        finally{working=false;if(!IsDisposed){actions.Enabled=true;list.Enabled=true;}}
    }
    void CloseAfterCancellation(){BeginInvoke(new Action(()=>{working=false;Close();}));}
    IProgress<string> Progress()=>new Progress<string>(text=>{if(!IsDisposed)status.Text=text;});
    async Task Scan()
    {
        report=await service.Scan(profile,Progress(),cancellation.Token);Render();
    }
    // 상태는 검사 완료/사용자 동작 때만 다시 그립니다. 타이머로 목록을 재생성하지 않습니다.
    public void ShowReport(ModAuditReport value){report=value;Render();}
    void Render()
    {
        if(report==null)return;
        list.SuspendLayout();foreach(Control c in list.Controls.Cast<Control>().ToArray())c.Dispose();foreach(var icon in localImages)icon.Dispose();localImages.Clear();picks.Clear();list.AutoScrollPosition=Point.Empty;
        int y=0;
        foreach(var item in report.Items.Where(x=>cleanup||x.HasUpdate).OrderByDescending(x=>cleanup?x.Side=="unsupported":x.HasUpdate))
        {
            var row=new DependencyCard{Left=0,Top=y,Width=Math.Max(400,list.ClientSize.Width-22),Height=156,Padding=new Padding(12)};
            var image=new PictureBox{SizeMode=PictureBoxSizeMode.Zoom,Location=new(16,18),Size=new(64,64)};
            var localIcon=ModFiles.Icon(item.Path);localImages.Add(localIcon);image.Image=localIcon;
            var name=Ui.Heading(item.Name,14);name.AutoSize=false;name.AutoEllipsis=true;name.SetBounds(96,10,row.Width-154,36);name.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            var detail=Ui.Note(item.CurrentVersion+(item.HasUpdate?"  →  "+item.Latest!["version_number"]:"")+" · "+(item.Disabled?"비활성":"활성")+" · "+item.SideLabel);
            detail.AutoSize=false;detail.AutoEllipsis=true;detail.SetBounds(96,48,row.Width-118,32);detail.Anchor=name.Anchor;
            var note=Ui.Note(cleanup?(item.Evidence.Length==0?"용도 미확인 · 그대로 유지":item.Evidence):item.Message);
            note.AutoSize=false;note.AutoEllipsis=true;note.SetBounds(96,82,row.Width-118,30);note.Anchor=name.Anchor;
            var filename=Ui.Note(Path.GetFileName(item.Path));filename.AutoSize=false;filename.AutoEllipsis=true;filename.Font=new Font(filename.Font.FontFamily,10);filename.SetBounds(96,114,row.Width-118,26);filename.Anchor=name.Anchor;
            var pick=new DependencySelectionBox{Location=new(row.Width-46,14),Anchor=AnchorStyles.Top|AnchorStyles.Right,
                Enabled=Eligible(item)};
            picks[item]=pick;
            pick.CheckedChanged+=(_,_)=>{row.Selected=pick.Checked;foreach(Control c in row.Controls)c.BackColor=pick.Checked?WarmTheme.Peach:WarmTheme.Surface;row.Invalidate();};
            row.Controls.AddRange([image,name,detail,note,filename,pick]);list.Controls.Add(row);y+=168;
            if(item.Project is {} project){image.Image=artwork.Find(project.Id)??localIcon;_=artwork.Load(project,()=>{if(!image.IsDisposed)image.Image=artwork.Find(project.Id)??localIcon;});}
        }
        if(!cleanup&&picks.Count==0){
            var empty=Ui.Note("현재 게임 버전·로더에 맞는 새 업데이트가 없어요.");empty.AutoSize=false;empty.SetBounds(12,18,Math.Max(400,list.ClientSize.Width-24),64);list.Controls.Add(empty);
        }
        list.ResumeLayout();
        status.Text=cleanup
            ?$"클라이언트 전용 {report.Items.Count(x=>x.Side=="unsupported")}개 · 용도 미확인 {report.Items.Count(x=>x.Side=="unknown")}개\n선택적 서버 모드는 유지해요. 삭제한 파일은 mods/removed에 보관해요."
            :$"새 버전 {report.Items.Count(x=>x.HasUpdate)}개 · 설치 파일 {report.Items.Length}개\n현재 게임 버전·로더에 맞는 정식 버전을 확인해요. Modrinth에서 식별되지 않은 파일은 자동 업데이트하지 않아요.";
        if(!canChange())status.Text+="\n서버 실행 중에는 확인만 가능해요.";
    }
    // Enabled는 부모 목록을 잠그면 함께 false가 됩니다. 작업 대상 판정은 화면 잠금과 독립된 검사 결과를 사용합니다.
    bool Eligible(ModAudit item)=>cleanup?(item.Side=="unsupported"||(item.Disabled&&item.Side is "required" or "optional")):item.HasUpdate;
    ModAudit[] Selected()=>picks.Where(x=>x.Value.Checked&&Eligible(x.Key)).Select(x=>x.Key).ToArray();
    async Task ApplyUpdates()
    {
        if(report==null)return;if(!canChange())throw new IOException("서버를 먼저 종료하세요.");
        var selected=Selected();if(selected.Length==0)throw new IOException("업데이트할 모드를 선택하세요.");
        var plan=await service.PlanUpdates(profile,report,selected,cancellation.Token);
        using var confirm=new DependencyWindow("업데이트 및 필수 의존성 확인",plan.Dependencies);
        if(WarmDialog.Present(this,confirm)!=DialogResult.OK)return;
        string backup=await service.Update(profile,plan,canChange,Progress(),cancellation.Token);
        await Scan();WarmDialog.Show(this,"업데이트했어요.\n이전 파일 보관 위치:\n"+backup,"업데이트 완료");
    }
    async Task Organize(string operation)
    {
        if(report==null)return;if(!canChange())throw new IOException("서버를 먼저 종료하세요.");
        var selected=Selected().Where(x=>operation=="enable"?x.Disabled&&x.Side is "required" or "optional":x.Side=="unsupported"&&(operation=="delete"||!x.Disabled)).ToArray();
        if(selected.Length==0)throw new IOException(operation=="enable"?"비활성 상태의 서버용 모드를 선택하세요.":"클라이언트 전용 모드를 선택하세요.");
        string label=operation=="enable"?"활성화":operation=="disable"?"비활성화":"삭제 (removed 폴더로 이동)";
        if(WarmDialog.Show(this,$"선택한 {selected.Length}개 모드를 {label}할까요?\n\n"+string.Join("\n",selected.Take(8).Select(x=>x.Name)),"서버용 모드 정리",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
        await service.Organize(profile,report,selected.Select(x=>new ModFileAction(x,operation)).ToArray(),canChange,cancellation.Token);
        await Scan();
    }
    protected override void Dispose(bool disposing){if(disposing&&!resourcesDisposed){resourcesDisposed=true;cancellation.Cancel();cancellation.Dispose();net.Dispose();artwork.Dispose();foreach(var icon in localImages)icon.Dispose();localImages.Clear();}base.Dispose(disposing);}
    sealed class MaintenanceList:BufferedDependencyPanel
    {
        bool arranging;
        protected override void OnLayout(LayoutEventArgs e)
        {
            if(arranging)return;arranging=true;
            try{foreach(Control row in Controls){row.Left=0;row.Width=Math.Max(400,ClientSize.Width-24);}base.OnLayout(e);}
            finally{arranging=false;}
        }
    }
}

public sealed partial class MainForm
{
    void OpenModMaintenance(bool cleanup)
    {
        using var dialog=new ModMaintenanceWindow(CaptureProfile(),cleanup,()=>!server.Running);
        WarmDialog.Present(this,dialog);RefreshModsCatalog();
    }
}
