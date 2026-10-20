// [소스 안내] 백업/로그 등 보관된 파일을 관리 화면에서 조회하고 관련 작업으로 연결하는 표시 코드입니다.
namespace NeoForgeServerManager;

public sealed partial class MainForm
{
    // 현재 서버의 로그와 외부에서 연 로그를 같은 뷰어로 확인합니다. 필터는 원본 파일을 바꾸지 않습니다.
    void InstallLogViewer(TabControl tabs)
    {
        var page=tabs.TabPages.Cast<TabPage>().First(x=>x.Text=="로그 뷰어");page.Controls.Clear();
        var root=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new Size(1220,742)};page.Controls.Add(root);
        root.Place(Ui.Heading("로그",24),18,0,600,48);root.Place(Ui.Note("서버 기록을 선택해서 확인하세요."),18,48,700,32);
        ReferencePanel Section(int x,int w){var surface=new SurfaceCard{Padding=Padding.Empty};root.Place(surface,x,84,w,590);var panel=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new Size(w,590),BackColor=WarmTheme.Surface};surface.Controls.Add(panel);return panel;}
        var left=Section(0,354);var right=Section(366,854);left.Place(Ui.Heading("서버 로그",19),24,12,260,44);
        var files=new ExtensionResultsList{BorderStyle=BorderStyle.None,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=84,IntegralHeight=false};left.Place(files,16,180,322,350);
        var fileQuery=new WarmTextBox{PlaceholderText="파일 이름 검색"};left.Place(fileQuery,16,64,322,46);
        var query=new WarmTextBox{PlaceholderText="로그 내용 검색"};right.Place(query,16,80,820,46);
        var title=Ui.Heading("로그 파일을 선택하세요",18);right.Place(title,16,12,560,52);
        var text=new RichTextBox{ReadOnly=true,WordWrap=false,BorderStyle=BorderStyle.None,Font=new Font("Consolas",11)};right.Place(text,16,190,820,342);
        var footer=Ui.Note("UTF-8 · 큰 파일은 마지막 약 2 MB를 표시합니다.");right.Place(footer,16,542,610,36);
        var wrap=new WarmToggle{Text="자동 줄바꿈"};wrap.CheckedChanged+=(_,_)=>text.WordWrap=wrap.Checked;right.Place(wrap,650,538,186,42);
        string original="",openedPath="";int generation=0,mode=0;bool crash=false;List<FileInfo> allFiles=[];
        void Filter(){text.Text=string.Join(Environment.NewLine,original.Split('\n').Where(line=>(mode switch{0=>!IsStatusLog(line),2=>IsErrorLog(line),3=>IsStatusLog(line),_=>true})&&(query.Text.Length==0||line.Contains(query.Text,StringComparison.OrdinalIgnoreCase))));}
        async Task Open(string path){int request=++generation;try{var result=await LogFiles.Read(path);if(request!=generation||text.IsDisposed)return;original=result;openedPath=path;title.Text=Path.GetFileName(path);Filter();}catch(Exception e){if(!text.IsDisposed)text.Text=e.Message;}}
        void FilterFiles(){files.Items.Clear();foreach(var f in allFiles.Where(f=>f.Name.Contains(fileQuery.Text,StringComparison.OrdinalIgnoreCase)&&(crash?f.DirectoryName!.EndsWith("crash-reports",StringComparison.OrdinalIgnoreCase):!f.DirectoryName!.EndsWith("crash-reports",StringComparison.OrdinalIgnoreCase))))files.Items.Add(f);if(files.Items.Count>0)files.SelectedIndex=0;}
        void Reload(){allFiles.Clear();if(!Directory.Exists(folder.Text))return;var paths=new[]{Path.Combine(folder.Text,"logs"),Path.Combine(folder.Text,"crash-reports"),folder.Text};allFiles=paths.Where(Directory.Exists).SelectMany(p=>new DirectoryInfo(p).GetFiles()).Where(f=>f.Extension.Equals(".log",StringComparison.OrdinalIgnoreCase)||f.Name.EndsWith(".log.gz",StringComparison.OrdinalIgnoreCase)||(f.DirectoryName!.EndsWith("crash-reports",StringComparison.OrdinalIgnoreCase)&&f.Extension.Equals(".txt",StringComparison.OrdinalIgnoreCase))).OrderByDescending(f=>f.LastWriteTime).ToList();FilterFiles();}
        files.DrawItem+=(_,e)=>{if(e.Index<0)return;var file=(FileInfo)files.Items[e.Index];bool selected=(e.State&DrawItemState.Selected)!=0;e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;var r=Rectangle.Inflate(e.Bounds,-2,-5);using var path=WarmTheme.Round(r,10);using var fill=new SolidBrush(selected?WarmTheme.Peach:WarmTheme.Surface);using var pen=new Pen(selected?WarmTheme.Accent:WarmTheme.Line);e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(pen,path);ReferenceIcons.Draw(e.Graphics,"file",new Rectangle(r.X+16,r.Y+22,24,28),WarmTheme.Ink);using var bold=new Font("Noto Sans KR",12,FontStyle.Bold);TextRenderer.DrawText(e.Graphics,file.Name,bold,new Rectangle(r.X+58,r.Y+10,r.Width-68,30),WarmTheme.Ink,TextFormatFlags.Left|TextFormatFlags.EndEllipsis);TextRenderer.DrawText(e.Graphics,$"{file.LastWriteTime:yyyy.MM.dd HH:mm} · {file.Length/1024:N0} KB",Font,new Rectangle(r.X+58,r.Y+42,r.Width-68,26),WarmTheme.Muted,TextFormatFlags.Left|TextFormatFlags.EndEllipsis);};
        files.SelectedIndexChanged+=async(_,_)=>{if(files.SelectedItem is FileInfo file)await Open(file.FullName);};fileQuery.TextChanged+=(_,_)=>FilterFiles();query.TextChanged+=(_,_)=>Filter();
        left.Place(Button("일반 로그",()=>{crash=false;FilterFiles();}),16,124,158,46);left.Place(Button("크래시",()=>{crash=true;FilterFiles();}),184,124,154,46);left.Place(Button("목록 새로고침",Reload),16,540,322,40);
        string[] modes=["주요 로그","전체 로그","경고 · 오류","상태 조회"];var filters=new List<WarmButton>();for(int i=0;i<4;i++){int index=i;var b=(WarmButton)Button(modes[i],()=>{mode=index;foreach(var candidate in filters){candidate.Primary=candidate==filters[index];candidate.Invalidate();}Filter();});b.Primary=i==0;filters.Add(b);right.Place(b,16+i*162,140,154,42);}
        root.Place(Button("외부 로그 파일 열기",async()=>{using var d=new OpenFileDialog{Filter="로그|*.log;*.txt;*.gz|모든 파일|*.*"};if(d.ShowDialog(this)==DialogResult.OK)await Open(d.FileName);}),816,0,216,50);root.Place(Button("로그 폴더 열기",()=>OpenFolder(Path.Combine(Folder(),"logs"))),1040,0,180,50);
        right.Place(Button("표시된 로그 저장",()=>{using var d=new SaveFileDialog{Filter="로그|*.log",FileName=openedPath.Length==0?"log-export.log":Path.GetFileNameWithoutExtension(openedPath)+"-export.log"};if(d.ShowDialog(this)==DialogResult.OK)File.WriteAllText(d.FileName,text.Text);}),638,14,198,46);
        root.Place(Ui.Note("ⓘ  반복되는 접속자 · TPS 조회 기록은 ‘상태 조회’에서 확인할 수 있어요."),16,696,1190,42);page.Enter+=(_,_)=>Reload();
    }

    void InstallBackupList(TabControl tabs)
    {
        var page=tabs.TabPages.Cast<TabPage>().First(p=>p.Text=="백업");
        IEnumerable<Control> Descend(Control parent){foreach(Control c in parent.Controls){yield return c;foreach(var child in Descend(c))yield return child;}}
        var buttons=Descend(page).OfType<WarmButton>().ToArray();var old=page.Controls.Cast<Control>().ToArray();
        var root=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new Size(1220,742)};
        root.Place(Ui.Heading("백업 · 복원",24),18,0,600,50);root.Place(Ui.Note("월드 폴더만 ZIP으로 저장합니다. 모드·플러그인·실행 파일은 제외됩니다."),18,48,900,32);root.Place(backup,1064,0,146,50);
        var open=buttons.First(b=>b.Text=="백업 폴더 열기");root.Place(open,868,0,186,50);
        ReferencePanel Section(int x,int y,int w,int h,string title)
        {var card=new SurfaceCard{Padding=Padding.Empty};root.Place(card,x,y,w,h);var panel=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new Size(w,h),BackColor=WarmTheme.Surface};card.Controls.Add(panel);if(title.Length>0)panel.Place(Ui.Heading(title,18),20,10,w-40,42);return panel;}
        var location=Section(0,84,1220,52,"");location.Place(Ui.Note("저장 위치"),24,6,140,40);location.Place(backupFolder,166,4,936,44);location.Place(buttons.First(b=>b.Text=="선택"),1112,4,96,44);
        var automatic=Section(0,148,548,198,"자동 백업");autoBackup.Text="";autoBackup.AccessibleName="자동 백업";automatic.Place(autoBackup,140,10,64,38);automatic.Place(Ui.Note("백업 간격 (분)"),24,62,190,44);automatic.Place(interval,220,58,290,48);automatic.Place(Ui.Note("보관 개수 (0: 무제한)"),24,114,250,44);automatic.Place(backupKeep,286,112,224,46);automatic.Place(Ui.Note("개수 초과 시 관리자가 만든 오래된 백업 자동 삭제"),24,160,510,32);
        var notice=Section(560,148,660,198,"게임 내 사전 공지");notice.Place(backupChat,24,54,600,40);notice.Place(backupBossbar,24,96,600,40);notice.Place(Ui.Note("60 · 30 · 10 · 5 · 4 · 3 · 2 · 1초에 안내가 전송됩니다."),24,150,600,36);
        var process=Section(0,358,1220,64,"");process.Place(Ui.Note("사전 공지     →     서버 저장 · 정상 종료     →     월드 ZIP 압축     →     다시 시작"),36,10,1150,44);
        var archive=Section(0,434,1220,244,"백업 목록");
        var list=new BackupArchiveList();archive.Place(list,16,56,1188,174);
        void Reload(){list.SetFiles(Directory.Exists(folder.Text)?new DirectoryInfo(BackupPaths.ForServer(CaptureProfile())).GetFiles("*.zip").OrderByDescending(f=>f.LastWriteTime):[]);}
        list.FileAction+=async(file,restore)=>{await RunAsync(async()=>{if(restore)await RestoreFileAsync(file);else if(WarmDialog.Show(this,$"{Path.GetFileName(file)}\n이 백업을 목록에서 제거할까요? 삭제한 파일은 removed 폴더로 옮깁니다.","백업 삭제",MessageBoxButtons.YesNo)==DialogResult.Yes){string destination=Path.Combine(Path.GetDirectoryName(file)!,"removed",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(destination);File.Move(file,Path.Combine(destination,Path.GetFileName(file)));}Reload();});};
        archive.Place(Button("백업 파일 가져오기",RestoreAsync),1002,8,202,42);archive.Place(Button("새로고침",Reload),866,8,126,42);
        root.Place(backupStatus,18,696,700,40);root.Place(buttons.First(b=>b.Text=="예약 공지 취소"),736,688,220,48);root.Place(buttons.First(b=>b.Text=="백업 설정 저장"),974,688,236,48);
        foreach(var c in old){page.Controls.Remove(c);c.Dispose();}page.Controls.Add(root);page.Enter+=(_,_)=>{try{Reload();}catch(Exception e){backupStatus.Text=e.Message;}};
    }
}
