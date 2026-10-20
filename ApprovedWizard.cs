// [소스 안내] 새 서버 만들기의 단계별 화면 배치입니다. 서버 종류·버전·메모리·설정·이미지·최종 확인을 표시하고 공식 버전을 조회합니다.
namespace NeoForgeServerManager;

// 승인 이미지의 좌표를 기준으로 구성합니다. 단계 전환 시 입력 컨트롤은 재사용합니다.
public sealed partial class CreateServerWizard
{
    static Label Caption(string text,int size=14,bool bold=false)=>new(){Text=text,Font=new Font("Noto Sans KR",size,bold?FontStyle.Bold:FontStyle.Regular),BackColor=Color.Transparent,ForeColor=bold?WarmTheme.Ink:WarmTheme.Muted,TextAlign=ContentAlignment.MiddleLeft};
    static ReferencePanel Section(ReferencePanel parent,int x,int y,int w,int h)
    {
        var card=new SurfaceCard{Padding=Padding.Empty};parent.Place(card,x,y,w,h);
        var panel=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new Size(w,h),BackColor=WarmTheme.Surface};card.Controls.Add(panel);return panel;
    }
    static void LabelAt(ReferencePanel p,string text,int x,int y,int w,int h,int size=14,bool bold=false)=>p.Place(Caption(text,size,bold),x,y,w,h);
    static WarmButton ButtonAt(ReferencePanel p,string text,Action action,int x,int y,int w,int h,bool primary=false)
    {var button=Ui.Action(text,action,primary);button.Font=new Font("Noto Sans KR",14,FontStyle.Bold);p.Place(button,x,y,w,h);return button;}
    void BrowseJava(){using var d=new OpenFileDialog{Filter="Java 실행 파일|java.exe|실행 파일|*.exe"};if(d.ShowDialog(this)==DialogResult.OK)java.Text=d.FileName;}
    static bool IsStableVersion(string version)=>!System.Text.RegularExpressions.Regex.IsMatch(version,@"(?i)alpha|beta|snapshot|pre|rc|\d+w\d+");
    // [동작 설명] 선택한 로더의 공식 배포처에서 버전을 새로 읽고 시험 버전 표시 설정에 맞춰 목록을 채웁니다.
    async Task RefreshVersions()
    {
        try{if(net==null){string contact="";if(kind.Text=="Paper"){var answer=ApiIdentity.RequirePaperContact(this);if(answer==null)return;contact=answer;}net=new(contact);}
            next.Enabled=false;
            var adapter=InstallerFactory.Create(kind.Text,net);if(adapter is FabricInstaller fabric)fabric.IncludePrerelease=includeBeta.Checked;
            var versions=await adapter.Games(cancel.Token);if(IsDisposed)return;game.Items.Clear();game.Items.AddRange(versions.Where(v=>includeBeta.Checked||IsStableVersion(v)).ToArray());build.Items.Clear();}
        catch(OperationCanceledException){}catch(Exception error){WarmDialog.Show(this,error.Message,"버전 조회 실패");}
    }
    void RenderApproved()
    {
        body.SuspendLayout();
        props.Toolbar.Parent?.Controls.Remove(props.Toolbar);
        foreach(var c in new Control[]{java,folder,game,build,min,max,props,serverImage,eula,log,progress,includeBeta})c.Parent?.Controls.Remove(c);
        foreach(Control c in body.Controls.Cast<Control>().ToArray())c.Dispose();
        body.Padding=new Padding(100,0,100,0);title.Step=step;title.Invalidate();back.Enabled=step>0;next.Text=step==5?"서버 만들기":"다음 →";next.Enabled=step!=5||eula.Checked;
        var layout=new ReferencePanel{Tag="landscape",BackColor=Color.Transparent,Dock=DockStyle.Fill,ReferenceSize=new Size(1532,692)};body.Controls.Add(layout);
        foreach(var c in new Control[]{name,java,folder,game,build,min,max,eula})c.Font=new Font("Noto Sans KR",15);
        switch(step)
        {
            case 0:
            {
                var content=Section(layout,36,16,1464,620);
                LabelAt(content,"어떤 서버를 만들까요?",32,24,800,52,25,true);LabelAt(content,"플레이 방식에 맞는 서버 소프트웨어를 선택하세요.",32,82,1000,40,15);
                string[] descriptions=["가볍고 안정적인\n플러그인 서버","Paper 기반의\n고기능 서버","가볍고 빠른\n모드 로더","가장 널리 사용되는\n모드 로더","Forge의 후속\n최신 모드 로더"];
                var buttons=new List<ChoiceCard>();
                for(int i=0;i<5;i++)
                {
                    int index=i;var button=new ChoiceCard{Text=kind.Items[i].ToString(),Caption=i<2?"플러그인 서버":"모드 서버",Description=descriptions[i],Selected=kind.SelectedIndex==i,ShowSoftwareIcon=true,Font=new Font("Noto Sans KR",14)};
                    buttons.Add(button);button.Click+=(_,_)=>{kind.SelectedIndex=index;foreach(var b in buttons){b.Selected=b==button;b.Invalidate();}};
                    if(i<2)content.Place(button,28+i*712,134,696,192);else content.Place(button,28+(i-2)*475,342,459,182);
                }
                LabelAt(content,"모드가 함께 들어 있는 서버를 만들고 싶나요?",30,545,920,48,14);
                ButtonAt(content,"모드팩 서버 추가 →",OpenModpack,1040,541,392,58,true);break;
            }
            case 1:
            {
                var outer=Section(layout,36,16,1464,644);
                var identity=Section(outer,20,16,962,116);
                identity.Place(new SoftwareLogo(kind.Text),22,14,110,88);LabelAt(identity,kind.Text+"  ·  "+(kind.Text is "Paper" or "Purpur"?"플러그인 서버":"모드 서버"),148,14,550,50,21,true);
                LabelAt(identity,"공식 배포 정보에서 서버 버전을 선택하세요.",148,65,550,36,14);
                ButtonAt(identity,"⇄ 서버 종류 변경",()=>{step=0;Render();},736,25,206,62);
                LabelAt(outer,"Minecraft 버전",38,149,216,56,17,true);outer.Place(game,254,149,710,56);
                LabelAt(outer,"서버 빌드",38,449,216,56,17,true);outer.Place(build,254,449,710,56);
                LabelAt(outer,"베타 버전 포함 보기",38,522,216,50,15,true);outer.Place(includeBeta,254,526,80,44);LabelAt(outer,"베타 버전을 포함한 버전 목록을 표시합니다.",348,522,616,50,14);
                var help=Section(outer,1000,16,444,570);
                LabelAt(help,"버전 선택 안내",28,24,388,60,24,true);LabelAt(help,"함께 사용할 모드나 플러그인이 지원하는\nMinecraft 버전을 선택하세요.",28,95,390,90,16);
                var hint1=Section(help,20,188,404,114);LabelAt(hint1,"Minecraft 버전",24,12,356,44,18,true);LabelAt(hint1,"접속할 게임 버전과 맞춰주세요.",24,59,356,40,14);
                var hint2=Section(help,20,316,404,114);LabelAt(hint2,"서버 빌드",24,12,356,44,18,true);LabelAt(hint2,"선택한 게임 버전의 배포판입니다.",24,59,356,40,14);
                ButtonAt(help,"⟳ 버전 목록 새로고침",async()=>await RefreshVersions(),20,490,404,60);
                LabelAt(outer,"ⓘ  버전 목록은 공식 배포 정보를 조회해 표시합니다.",28,590,1400,40,14);break;
            }
            case 2:
            {
                var memory=Section(layout,54,32,942,402);LabelAt(memory,"메모리 설정",145,12,730,55,24,true);LabelAt(memory,"서버에 할당할 메모리 용량을 선택하세요.",145,68,730,40,15);
                LabelAt(memory,"최소 메모리 (Xms)",26,108,390,40,15,true);LabelAt(memory,"최대 메모리 (Xmx)",500,108,414,40,15,true);
                memory.Place(min,26,150,380,56);memory.Place(max,502,150,414,56);LabelAt(memory,"≤",438,150,50,56,25);
                LabelAt(memory,"4 GB 단위로 선택",28,213,400,42,14);LabelAt(memory,"ⓘ  최소 메모리는 최대 메모리보다 클 수 없어요.",28,267,850,46,14);
                var help=Section(layout,1014,32,466,402);LabelAt(help,"메모리 안내",28,18,410,55,24,true);
                ulong total=new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory;double totalGb=total/1073741824d;
                LabelAt(help,"이 컴퓨터의 총 메모리",28,94,290,40,15);LabelAt(help,$"{totalGb:0} GB",330,94,110,40,17,true);
                var allocation=Caption("",17,true);help.Place(allocation,330,136,110,40);LabelAt(help,"선택한 최대 메모리",28,136,290,40,15);
                var usage=new CoralProgress();help.Place(usage,28,188,410,22);
                void UpdateAllocation(object? sender,EventArgs args){if(int.TryParse(max.Text,out int gb)){allocation.Text=$"{gb} GB";usage.Value=(int)(gb/totalGb*100);}}
                max.SelectedIndexChanged+=UpdateAllocation;help.Disposed+=(_,_)=>max.SelectedIndexChanged-=UpdateAllocation;UpdateAllocation(null,EventArgs.Empty);
                LabelAt(help,"Xms : 서버 시작 시 확보할 메모리.\nXmx : 서버가 사용할 최대 메모리.\n\n다른 프로그램이 사용할 메모리도 남겨두세요.",28,232,410,158,14);
                var j=Section(layout,54,450,1426,220);LabelAt(j,"Java 설정",110,4,800,50,24,true);LabelAt(j,"서버에서 사용할 Java 실행 파일을 선택하세요.",110,52,1050,40,14);
                LabelAt(j,"Java 실행 파일",24,94,196,50,14,true);j.Place(java,226,94,1002,50);ButtonAt(j,"찾아보기",BrowseJava,1240,94,160,50);
                var detected=Caption("버전 확인 전",14);j.Place(detected,226,156,510,48);LabelAt(j,"감지된 버전",24,156,196,48,14,true);
                ButtonAt(j,"Java 자동 찾기",async()=>{try{var result=await JavaService.FindInstalledAsync();if(!detected.IsDisposed){java.Text=result.Path;detected.Text=result.Version;}}catch(Exception error){if(!IsDisposed)WarmDialog.Show(this,error.Message);}},970,156,224,50);
                ButtonAt(j,"버전 확인",async()=>{try{detected.Text=await ServerService.CheckJavaAsync(java.Text.Trim());}catch(Exception error){WarmDialog.Show(this,error.Message);}},1210,156,190,50);break;
            }
            case 3:
            {
                layout.Place(props.Toolbar,54,16,1424,64);
                var settings=Section(layout,50,93,824,534);
                settings.Place(new ReferenceGlyph("settings"),28,14,36,36);LabelAt(settings,"기본 설정",78,4,650,48,23,true);
                props.SetServerName(name);settings.Place(props,16,52,790,480);
                Section(layout,890,93,590,534);
                LabelAt(layout,"ⓘ  선택한 버전에 맞는 설정 항목을 표시합니다.",66,636,900,44,13);LabelAt(layout,"?에 마우스를 올리면 도움말이 표시됩니다.",1020,636,458,44,12);break;
            }
            case 4:
            {
                serverImage.SetProfile(name.Text,kind.Text,game.Text,int.Parse(min.Text),int.Parse(max.Text));layout.Place(serverImage,54,16,1424,620);break;
            }
            case 5:
            {
                var info=Section(layout,36,0,718,434);LabelAt(info,"서버 정보",24,10,650,55,22,true);
                var picture=new PictureBox{SizeMode=PictureBoxSizeMode.Zoom,Image=Artwork.Read(serverImage.ImagePath)};picture.Disposed+=(_,_)=>picture.Image?.Dispose();info.Place(picture,24,74,315,168);
                LabelAt(info,name.Text,364,92,324,55,23,true);LabelAt(info,kind.Text+" · Minecraft "+game.Text,364,154,324,60,14);
                LabelAt(info,"로더 / 빌드",24,264,230,42,14);LabelAt(info,build.Text,260,264,425,42,14,true);
                LabelAt(info,"할당 메모리",24,312,230,42,14);LabelAt(info,min.Text+" GB ~ "+max.Text+" GB",260,312,425,42,14,true);
                LabelAt(info,"Java",24,360,230,42,14);LabelAt(info,java.Text,260,360,425,42,13);
                var summary=Section(layout,772,0,728,434);LabelAt(summary,"서버 설정 요약",24,10,510,55,22,true);ButtonAt(summary,"수정",()=>{step=3;Render();},580,18,120,44);
                var values=props.Values();string[] keys=["motd","server-port","max-players","gamemode","difficulty","pvp","online-mode","white-list"];
                for(int i=0;i<keys.Length;i++){var def=PropertiesService.Definitions.First(x=>x.Key==keys[i]);LabelAt(summary,def.Korean,24,76+i*41,260,38,14);LabelAt(summary,values.GetValueOrDefault(keys[i],def.Default),300,76+i*41,400,38,14);}
                var location=Section(layout,36,440,1464,112);LabelAt(location,"서버 설치 위치",24,8,1390,36,14,true);location.Place(folder,24,48,1200,50);ButtonAt(location,"찾아보기",()=>{using var d=new FolderBrowserDialog();if(d.ShowDialog(this)==DialogResult.OK){installParent=d.SelectedPath;UpdateInstallFolder();}},1240,48,198,50);
                var agreement=Section(layout,36,562,1464,96);agreement.Place(eula,24,20,1100,54);ButtonAt(agreement,"EULA 읽기 ↗",()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://www.minecraft.net/eula"){UseShellExecute=true}),1220,20,218,54);break;
            }
        }
        WarmTheme.Apply(layout);body.ResumeLayout(true);
    }
    Label? installStatus;
    bool installAutoScroll=true;
    void ShowInstallProgress(Profile profile)
    {
        foreach(var c in new Control[]{java,folder,eula})c.Parent?.Controls.Remove(c);
        foreach(Control c in body.Controls.Cast<Control>().ToArray())c.Dispose();
        var layout=new ReferencePanel{Tag="landscape",BackColor=Color.Transparent,Dock=DockStyle.Fill,ReferenceSize=new Size(1532,668)};body.Controls.Add(layout);
        var identity=Section(layout,48,10,1436,132);var picture=new PictureBox{Image=Artwork.Read(profile.ImagePath),SizeMode=PictureBoxSizeMode.Zoom};picture.Disposed+=(_,_)=>picture.Image?.Dispose();identity.Place(picture,16,10,265,112);
        LabelAt(identity,profile.Name,308,12,1080,46,23,true);LabelAt(identity,profile.Kind+" · Minecraft "+profile.MinecraftVersion,308,61,1080,32,15);LabelAt(identity,profile.Folder,308,96,1080,28,12);
        var progressCard=Section(layout,48,154,1436,132);installStatus=Caption("서버 파일을 준비하고 있어요…",19,true);progressCard.Place(installStatus,24,14,1380,44);progressCard.Place(progress,24,70,1388,22);
        LabelAt(progressCard,"현재 단계의 진행률입니다. 설치 도구가 진행률을 제공하지 않는 동안에는 준비 중으로 표시합니다.",24,99,1380,28,12);
        LabelAt(layout,"다운로드    →    설치    →    서버 설정    →    프로필 저장    →    완료",64,306,1400,54,16,true);
        var logs=Section(layout,48,384,1436,252);LabelAt(logs,"설치 로그",20,10,280,38,16,true);
        ButtonAt(logs,"로그 저장",()=>{using var d=new SaveFileDialog{Filter="텍스트 로그|*.log",FileName="install.log"};if(d.ShowDialog(this)==DialogResult.OK)File.WriteAllText(d.FileName,log.Text);},1030,8,150,42);
        var auto=new WarmToggle{Text="자동 스크롤",Checked=true};auto.CheckedChanged+=(_,_)=>installAutoScroll=auto.Checked;logs.Place(auto,1200,8,210,42);
        log.Font=new Font("Consolas",11);log.BorderStyle=BorderStyle.None;logs.Place(log,18,60,1400,174);
        progress.Value=0;next.Enabled=back.Enabled=false;next.Text="설치 중…";WarmTheme.Apply(layout);
    }
    void ReportInstall(string message)
    {
        log.AppendText(message+Environment.NewLine);
        if(installAutoScroll){log.SelectionStart=log.TextLength;log.ScrollToCaret();}
        // 다운로드 로그에 있는 실제 바이트 비율만 사용합니다. 전체 설치 진행률을 임의로 만들지 않습니다.
        var m=System.Text.RegularExpressions.Regex.Match(message,@"다운로드 ([0-9.]+) MB / ([0-9.]+) MB");
        if(m.Success && double.TryParse(m.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture,out double done) && double.TryParse(m.Groups[2].Value,System.Globalization.CultureInfo.InvariantCulture,out double total) && total>0)
        {progress.Value=(int)Math.Clamp(done/total*100,0,100);if(installStatus!=null)installStatus.Text=$"서버 파일 다운로드  {progress.Value}%";}
        else if(installStatus!=null)installStatus.Text="서버 설치 중 — 자세한 진행 상황은 아래 로그에 표시됩니다.";
    }
}
