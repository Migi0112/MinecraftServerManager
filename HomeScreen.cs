// [소스 안내] 서버목록 화면(HomeForm). 서버 카드/자세히 보기, 검색, 새로고침 등록, 화면 이동과 실행 중인 관리 창을 연결합니다.
namespace NeoForgeServerManager;
// HomeForm이 세션을 소유하므로 화면을 바꿔도 실행 중인 서버는 계속 관리됩니다.
public sealed class HomeForm : WarmWindow
{
    readonly ReferencePanel page = new() { Dock = DockStyle.Fill };
    readonly Panel cards = new() { AutoScroll = true, Padding = Padding.Empty };
    readonly WarmTextBox search = new() { Width = 300, PlaceholderText = "서버 검색…", LeadingIcon = "search" };
    readonly Dictionary<string, MainForm> sessions = [];
    readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    readonly StorageStrip paths;
    readonly HashSet<string> discovered = [];
    readonly WarmButton refresh = new(){Text="↻",MinimumSize=Size.Empty,Font=new Font("Noto Sans KR",24),TextColor=WarmTheme.Accent};
    readonly SurfaceCard scanToast = new(){Padding=Padding.Empty,Visible=false};
    readonly Label scanTitle=Ui.Note(""),scanNote=Ui.Note("");
    readonly ToolTip scanTip=new();
    readonly System.Windows.Forms.Timer toastTimer=new(){Interval=10000};
    readonly HomePreferences preferences=HomePreferences.Load();
    readonly WarmButton gridView=new(){Text="카드",IconName="grid"}, detailView=new(){Text="자세히",IconName="list"};
    readonly HomeDragReorder reorder;
    bool scanning;
    bool shuttingDown;
    Form? activePage;
    public HomeForm(bool loadProfiles = true)
    {
        if(loadProfiles)_=AppUpdates.CleanCurrentInstaller();
        Scenic=true;WideBrand=false;Scenery.Make(page);Scenery.Make(cards);page.ReferenceSize=new Size(1532,944); Text = "Minecraft Server Manager"; Size = new(1536,1024); MinimumSize = new(1280,900);
        page.Place(new SurfaceCard{Padding=Padding.Empty},154,116,800,132);
        var homeHeading=Ui.Heading("서버목록",42);homeHeading.BackColor=WarmTheme.Surface;page.Place(homeHeading,174,126,550,75);
        var subtitle=Ui.Note("여기서 바로 시작하고, 필요할 때 관리해요.");subtitle.BackColor=WarmTheme.Surface;subtitle.ForeColor=WarmTheme.Ink;subtitle.Font=new Font("Noto Sans KR",16);page.Place(subtitle,178,204,720,36);
        var setting=Ui.Action("설정",OpenSettings);setting.IconName="settings";setting.Font=new Font("Noto Sans KR",12);page.Place(setting,1294,66,112,50);
        var updates=Ui.Action("업데이트",()=>Navigate(new UpdatesPage(CanUpdate,InstallUpdate)));page.Place(updates,1148,66,132,50);
        search.Font=new Font("Noto Sans KR",12);var toolbar=new SurfaceCard{Padding=Padding.Empty};page.Place(toolbar,154,264,1252,90);var bar=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new Size(1252,90)};toolbar.Controls.Add(bar);bar.Place(search,18,16,722,58);
        bar.Place(refresh,762,16,62,58);scanTip.SetToolTip(refresh,"서버 폴더 검색 및 등록");refresh.Click+=async(_,_)=>await DiscoverServers();
        var import=Ui.Action("기존 서버 등록",()=>Navigate(new ImportServerForm()));import.IconName="server";import.OutlineColor=Color.FromArgb(157,136,123);import.Font=new Font("Noto Sans KR",16,FontStyle.Bold);bar.Place(import,844,16,180,58);
        var add=Ui.Action("＋ 서버 추가",()=>Navigate(new CreateServerWizard()),true);add.Font=import.Font;bar.Place(add,1044,16,190,58);
        page.Place(gridView,1118,366,124,44);page.Place(detailView,1252,366,154,44);
        gridView.Click+=(_,_)=>SetView(false);detailView.Click+=(_,_)=>SetView(true);
        reorder=new HomeDragReorder(cards,SaveDraggedOrder);
        page.Place(cards,154,422,1252,380);
        paths=new StorageStrip(OpenSettings);page.Place(paths,154,812,1252,96);
        var toastLayout=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new(452,98)};scanToast.Controls.Add(toastLayout);
        var tick=Ui.Note("✓");tick.ForeColor=WarmTheme.Accent;tick.Font=new Font("Noto Sans KR",24,FontStyle.Bold);toastLayout.Place(tick,14,20,42,52);
        scanTitle.Font=new Font("Noto Sans KR",12,FontStyle.Bold);scanNote.Font=new Font("Noto Sans KR",10);toastLayout.Place(scanTitle,62,12,338,38);toastLayout.Place(scanNote,62,49,338,38);
        var dismiss=Ui.Action("×",()=>scanToast.Visible=false);dismiss.MinimumSize=Size.Empty;dismiss.Padding=Padding.Empty;dismiss.Ghost=true;toastLayout.Place(dismiss,404,10,36,36);
        page.Place(scanToast,954,694,452,98);scanToast.Visible=false;toastTimer.Tick+=(_,_)=>{scanToast.Visible=false;toastTimer.Stop();};
        Controls.Add(page);
        cards.Resize+=(_,_)=>LayoutCards(); search.TextChanged+=(_,_)=>Reload(); Shown+=(_,_)=>{if(loadProfiles)Reload();};
        timer.Tick+=(_,_)=>{if(activePage!=null)return;foreach(var card in cards.Controls.OfType<HomeServerCard>())card.RefreshState(sessions.GetValueOrDefault(card.Profile.Id));};timer.Start();
        FormClosed+=(_,_)=>{timer.Dispose();toastTimer.Dispose();scanTip.Dispose();reorder.Dispose();};FormClosing+=OnHomeClosing;WarmTheme.Apply(this);
        if(loadProfiles&&string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MIGI_MANAGER_DATA_ROOT")))Shown+=async(_,_)=>{
            try{using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));var releases=await AppUpdates.Check(timeout.Token);
                if(IsDisposed||Disposing)return;
                if(releases.FirstOrDefault() is {} latest&&Version.Parse(latest.Version)>AppUpdates.Current){updates.Text="새 업데이트";
                    if(activePage==null&&WarmDialog.Show(this,$"새 버전 {latest.Title}이 있습니다.\n업데이트 화면을 열까요?","업데이트 알림",MessageBoxButtons.YesNo)==DialogResult.Yes)Navigate(new UpdatesPage(CanUpdate,InstallUpdate));}
            }catch{if(!IsDisposed)updates.Text="업데이트 확인";}
        };
    }
    bool CanUpdate()=>!sessions.Values.Any(s=>s.IsRunning||s.IsBusy)&&!(activePage is CreateServerWizard {Installing:true})&&!(activePage is ImportServerForm {Importing:true});
    async Task InstallUpdate(string path,AppRelease release)
    {
        if(!CanUpdate())throw new IOException("서버를 종료한 뒤 업데이트하세요.");
        await AppUpdates.StartInstaller(path,release.Sha256);Close();
    }
    // 별도 최상위 창을 띄우지 않고 동일 창 안의 페이지로 배치합니다.
    // [동작 설명] 메인 화면에서 만들기/설정 등의 하위 화면으로 전환합니다.
    void Navigate(Form child)
    {
        if(activePage!=null)return;
        using var painting=BatchVisualChange();Scenic=child is WarmWindow {Scenic:true};activePage=child;child.TopLevel=false;if(child is WarmWindow warm)warm.EmbedInShell();child.FormBorderStyle=FormBorderStyle.None;child.Dock=DockStyle.Fill;
        Controls.Add(child);child.BringToFront();child.FormClosed+=(_,_)=>{using var returning=BatchVisualChange();Controls.Remove(child);activePage=null;Scenic=true;WideBrand=false;page.Visible=true;Reload();};child.Show();page.Visible=false;
    }
    void OpenSettings()=>Navigate(new SettingsForm(()=>sessions.Values.Any(x=>x.IsRunning||x.IsBusy)));
    MainForm Session(Profile p)
    {
        if(!sessions.TryGetValue(p.Id,out var form)||form.IsDisposed){form=new MainForm(false);form.InitializeForHome(p);sessions[p.Id]=form;form.ProfileDeleted+=()=>{sessions.Remove(p.Id);if(activePage==form)form.Hide();else Reload();};}
        return form;
    }
    bool layingOutCards;
    void LayoutCards()
    {
        if(layingOutCards)return;layingOutCards=true;
        int scrollY=-cards.AutoScrollPosition.Y;
        try{cards.AutoScroll=false;
        int width=cards.ClientSize.Width-24;int columns=preferences.Details?1:width>=900?3:width>=650?2:1;
        var list=cards.Controls.OfType<HomeServerCard>().ToArray();int w=(width-16*(columns-1))/columns,height=preferences.Details?76:Math.Max(280,Math.Min((int)Math.Round(w*440d/488),cards.ClientSize.Height-8));
        cards.SuspendLayout();
        // 승인된 첫 화면의 3열 비율(488:458:488)을 유지합니다. 작은 창은 같은 비율로 축소합니다.
        for(int i=0;i<list.Length;i++)
        {
            int col=i%columns,x=col*(w+16),cardWidth=w;
            if(columns==3){int usable=width-32,outer=(int)Math.Round(usable*488d/1434),middle=usable-outer*2;cardWidth=col==1?middle:outer;x=col==0?0:col==1?outer+16:outer+middle+32;}
            list[i].SetBounds(x,(i/columns)*(height+16),cardWidth,height);
        }
        cards.AutoScrollMinSize=new Size(0,list.Length==0?0:((list.Length-1)/columns+1)*(height+16)-16);cards.ResumeLayout();cards.AutoScroll=true;cards.AutoScrollPosition=new Point(0,scrollY);
        }finally{layingOutCards=false;}
    }
    // [동작 설명] 새로고침 버튼으로 저장 폴더를 검색하고 발견한 서버를 등록합니다.
    public async Task DiscoverServers()
    {
        if(scanning)return;scanning=true;refresh.Enabled=false;refresh.Text="…";
        try {
            var existing=Storage.Load();string root=AppSettings.Load().ServerRoot;
            var result=await Task.Run(()=>ServerDiscovery.Scan(root,existing));if(IsDisposed)return;
            var added=ServerDiscovery.Register(result);discovered.Clear();foreach(var p in added)discovered.Add(p.Id);
            search.Text="";Reload();
            scanTitle.Text=added.Count>0?$"서버 {added.Count}개를 새로 등록했습니다":"새로 등록할 서버가 없습니다";
            scanNote.Text=result.Skipped.Count>0?$"기존 서버 제외 · 확인 필요 폴더 {result.Skipped.Count}개":"이미 등록된 서버는 제외했어요.";
            scanTip.SetToolTip(scanNote,result.Skipped.Count>0?"자동 확인하지 못한 폴더: "+string.Join(", ",result.Skipped.Take(12)):"폴더를 이동하거나 복사하지 않고 현재 위치에 등록했습니다.");
            scanToast.Visible=true;scanToast.BringToFront();toastTimer.Stop();toastTimer.Start();
        }catch(Exception e){if(!IsDisposed)WarmDialog.Show(this,e.Message,"서버 폴더 확인");}
        finally {scanning=false;if(!IsDisposed){refresh.Enabled=true;refresh.Text="↻";}}
    }
    void SetView(bool details){preferences.Details=details;preferences.Save();cards.AutoScrollPosition=Point.Empty;Reload();}
    void SaveDraggedOrder(string source,string target,bool after){
        var order=preferences.Sort(Storage.Load()).Select(p=>p.Id).ToList();
        if(!HomePreferences.Reorder(order,source,target,after))return;
        preferences.Order=order;preferences.Save();int scroll=-cards.AutoScrollPosition.Y;Reload();cards.AutoScrollPosition=new Point(0,scroll);
    }

    // [동작 설명] 현재 등록 정보와 표시 설정을 서버 카드/행으로 다시 반영합니다.
    void Reload()
    {
        using var painting=BatchVisualChange();
        gridView.Primary=!preferences.Details;detailView.Primary=preferences.Details;gridView.Invalidate();detailView.Invalidate();
        foreach(Control c in cards.Controls.Cast<Control>().ToArray())c.Dispose();
        try
        {
            var settings=AppSettings.Load();paths.ServerPath=settings.ServerRoot;paths.BackupPath=settings.BackupRoot;paths.Invalidate();
            var all=ServerRegistrations.ForDisplay(preferences.Sort(Storage.Load()),p=>sessions.TryGetValue(p.Id,out var active)&&(active.IsRunning||active.IsBusy));var available=all.Where(p=>Directory.Exists(p.Folder)||sessions.TryGetValue(p.Id,out var session)&&(session.IsRunning||session.IsBusy)).ToList();int missing=all.Count-available.Count;
            foreach(var p in preferences.Sort(available).Where(p=>p.Name.Contains(search.Text,StringComparison.OrdinalIgnoreCase)))
            {
                var card=new HomeServerCard(p,preferences.Details){NewlyRegistered=discovered.Contains(p.Id)};
                card.StartStop+=async()=>{var s=Session(p);if(s.IsRunning){if(WarmDialog.ConfirmStop(this,p,s.PlayerCount)!=DialogResult.Yes)return;await s.StopFromHome();}else await s.StartFromHome();card.RefreshState(s);};
                card.Delete+=()=>{var session=Session(p);if(!session.IsBusy&&!session.IsRunning)session.DeleteStoredProfile(this);};
                card.Restart+=async()=>await Session(p).RestartFromHome();
                card.Manage+=()=>{using var painting=BatchVisualChange();var s=Session(p); if(activePage!=null)return;activePage=s;WideBrand=false;page.Visible=false;s.TopLevel=false;s.EmbedInShell();s.Dock=DockStyle.Fill;Controls.Add(s);s.BringToFront();s.VisibleChanged+=ReturnFromManagement;s.Show();};
                card.RefreshState(sessions.GetValueOrDefault(p.Id));cards.Controls.Add(card);reorder.Attach(card);
            }
            if(cards.Controls.Count==0)cards.Controls.Add(Ui.Note("아직 서버가 없어요. ‘서버 추가’로 시작해 보세요."));LayoutCards();
        }catch(Exception e){WarmDialog.Show(this,e.Message);}WarmTheme.Apply(this);
    }
    void ReturnFromManagement(object? sender,EventArgs e){if(sender is MainForm f&&!f.Visible){using var painting=BatchVisualChange();f.VisibleChanged-=ReturnFromManagement;Controls.Remove(f);activePage=null;Scenic=true;WideBrand=false;page.Visible=true;Reload();}}
    async void OnHomeClosing(object? sender,FormClosingEventArgs e)
    {
        if(shuttingDown)return;e.Cancel=true;
        if((activePage is CreateServerWizard wizard && wizard.Installing)||(activePage is ImportServerForm import && import.Importing)){WarmDialog.Show(this,"서버 생성이 끝나거나 취소된 뒤 종료해 주세요.");return;}
        if(sessions.Values.Any(x=>x.IsBusy)){WarmDialog.Show(this,"진행 중인 작업을 마친 뒤 종료해 주세요.");return;}
        if(sessions.Values.Any(x=>x.IsRunning)&&WarmDialog.Show(this,"실행 중인 모든 서버를 저장하고 정상 종료한 뒤 프로그램을 닫을까요?","프로그램 종료",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
        Enabled=false;try{foreach(var s in sessions.Values.ToArray())await s.ShutdownForHome();shuttingDown=true;Close();}catch(Exception error){WarmDialog.Show(this,error.Message);}finally{Enabled=true;}
    }
}
// 카드의 시각 정보는 같은 기준 좌표로 그리되 실행 버튼은 실제 Windows 컨트롤입니다.
public sealed class HomeServerCard : SurfaceCard
{
    public Profile Profile {get;}
    public bool NewlyRegistered {get;set;}
    public event Action? StartStop,Restart,Manage,Delete;
    readonly WarmButton play,restart,manage,delete;
    Image artwork;
    string imagePath;
    Profile displayed;
    string state="오프라인", players="-";
    bool running;
    public DragGrip Grip {get;}=new();
    public bool DropHighlight {get;set;}
    public bool Compact {get;}
    string lastVisual="";
    public HomeServerCard(Profile profile,bool compact=false)
    {
        Compact=compact;Profile=displayed=profile;imagePath=profile.ImagePath;artwork=compact?new Bitmap(1,1):Artwork.Read(imagePath);
        Size=new(488,548);Padding=Padding.Empty;Margin=new Padding(0,0,16,0);
        play=Ui.Action("▶ 서버 시작",()=>StartStop?.Invoke(),true);
        restart=Ui.Action("⟳ 재시작",()=>Restart?.Invoke());manage=Ui.Action("관리 →",()=>Manage?.Invoke());
        delete=Ui.Action("삭제",()=>Delete?.Invoke());delete.IconName="trash";delete.OutlineColor=WarmTheme.Accent;delete.TextColor=WarmTheme.Accent;
        restart.OutlineColor=manage.OutlineColor=Color.FromArgb(157,136,123);
        foreach(var b in new[]{play,restart,manage,delete}){b.AutoSize=false;b.MinimumSize=Size.Empty;b.Font=new Font("Noto Sans KR",13,FontStyle.Bold);}
        Controls.AddRange([play,restart,manage,delete,Grip]);RefreshState(null);
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);if(play==null)return;Grip.SetBounds(8,Compact?20:12,24,36);Grip.BringToFront();
        if(Compact){play.SetBounds(Width-156,16,140,44);manage.SetBounds(Width-370,16,96,44);delete.SetBounds(Width-264,16,96,44);restart.Visible=false;return;}
        float sx=Width/488f,sy=Height/558f;
        play.SetBounds((int)(24*sx),(int)(384*sy),(int)((running?270:440)*sx),(int)(66*sy));
        restart.SetBounds((int)(304*sx),(int)(384*sy),(int)(160*sx),(int)(66*sy));
        int inset=(int)(24*sx),gap=10,deleteWidth=Math.Max(96,(int)(124*sx)),buttonY=(int)(470*sy),buttonHeight=Math.Max(36,(int)(64*sy));
        manage.SetBounds(inset,buttonY,Width-2*inset-gap-deleteWidth,buttonHeight);
        delete.SetBounds(Width-inset-deleteWidth,buttonY,deleteWidth,buttonHeight);

    }
    public void RefreshState(MainForm? session)
    {
        displayed=session?.ManagedProfile??Profile;running=session?.IsRunning==true;
        state=running?"실행 중":session?.RuntimeState?.Replace("●","").Trim()??"오프라인";
        players=running?session!.PlayerCount.ToString():"-";
        if(!Compact&&imagePath!=displayed.ImagePath){imagePath=displayed.ImagePath;artwork.Dispose();artwork=Artwork.Read(imagePath);}
        play.Text=running?"■ 정상 종료":"▶ 서버 시작";play.Enabled=session?.IsBusy!=true;
        delete.Enabled=!running&&session?.IsBusy!=true;restart.Visible=running&&!Compact;restart.Enabled=running&&session?.IsBusy!=true;
        string visual=$"{displayed.Name}|{displayed.Kind}|{displayed.MinecraftVersion}|{state}|{players}|{displayed.LastRun}|{displayed.MinRam}|{displayed.MaxRam}|{session?.IsBusy}|{imagePath}";
        if(visual==lastVisual)return;lastVisual=visual;PerformLayout();Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        if(DropHighlight){using var outline=new Pen(WarmTheme.Accent,3);using var shape=WarmTheme.Round(new Rectangle(2,2,Width-5,Height-5),16);g.DrawPath(outline,shape);}
        if(Compact){using var compactNameFont=new Font("Noto Sans KR",15,FontStyle.Bold);using var stateFont=new Font("Noto Sans KR",12);
            TextRenderer.DrawText(g,displayed.Name,compactNameFont,new Rectangle(42,12,Math.Max(60,Width-562),52),WarmTheme.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g,"● "+state,stateFont,new Rectangle(Width-516,16,136,44),running?Color.FromArgb(0,153,46):WarmTheme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);return;}
        float sx=Width/488f,sy=Height/558f,scale=Math.Min(sx,sy);
        Rectangle R(int x,int y,int w,int h)=>new((int)(x*sx),(int)(y*sy),(int)(w*sx),(int)(h*sy));
        Artwork.DrawFit(g,artwork,R(32,36,150,150));
        using var heading=new Font("Noto Sans KR",Math.Max(14,22*scale),FontStyle.Bold);using var text=new Font("Noto Sans KR",Math.Max(10,14*scale));
        TextRenderer.DrawText(g,displayed.Name,heading,R(214,24,250,84),WarmTheme.Ink,TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g,displayed.Kind+" · Minecraft "+displayed.MinecraftVersion,text,R(214,114,250,42),WarmTheme.Muted,TextFormatFlags.SingleLine|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
        var badge=R(214,166,146,34);using(var shape=WarmTheme.Round(badge,16))using(var fill=new SolidBrush(running?Color.FromArgb(218,245,216):Color.FromArgb(239,237,234)))g.FillPath(fill,shape);
        TextRenderer.DrawText(g,"● "+state,text,badge,running?Color.FromArgb(0,153,46):WarmTheme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        using var rule=new Pen(WarmTheme.Line);g.DrawLine(rule,24*sx,220*sy,464*sx,220*sy);
        string[] captions=["마지막 실행","접속자","할당 RAM"],values=[displayed.LastRun?.ToString("yyyy.MM.dd HH:mm")??"아직 실행 안 함",players,$"{displayed.MinRam} ~ {displayed.MaxRam} GB"],icons=["clock","users","memory"];
        for(int n=0;n<3;n++){int y=238+n*44;ReferenceIcons.Draw(g,icons[n],R(26,y+5,25,25),WarmTheme.Muted);TextRenderer.DrawText(g,captions[n],text,R(68,y,144,36),WarmTheme.Muted,TextFormatFlags.VerticalCenter);TextRenderer.DrawText(g,values[n],text,R(210,y,254,36),WarmTheme.Ink,TextFormatFlags.Right|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);}
    }

    protected override void Dispose(bool disposing){if(disposing)artwork.Dispose();base.Dispose(disposing);}
}


