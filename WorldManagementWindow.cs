// [소스 안내] 월드 선택·가져오기·적용 관련 화면입니다. 실제 파일 처리는 WorldImportService에 연결합니다.
namespace NeoForgeServerManager;

public sealed class WorldManagementWindow:WarmWindow
{
    readonly string server;readonly Label selected=Ui.Note("새 월드 ZIP 또는 폴더를 선택하세요.");readonly Label status=Ui.Note("월드 교체는 서버가 꺼진 상태에서 진행됩니다.");
    readonly WarmButton apply;string source="";bool busy;
    public WorldManagementWindow(Profile profile)
    {
        server=profile.Folder;Text="월드 관리";Size=new(1100,760);BackColor=WarmTheme.Background;MaximizeBox=false;
        var root=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new(1096,680)};Controls.Add(root);root.Place(Ui.Heading("월드 관리",24),26,16,950,52);root.Place(Ui.Note("다운로드한 맵을 선택해 서버의 월드를 교체합니다."),26,74,1000,44);
        var current=new SurfaceCard{Padding=Padding.Empty};root.Place(current,26,136,504,344);var currentLayout=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new(504,344),BackColor=WarmTheme.Surface};current.Controls.Add(currentLayout);currentLayout.Place(Ui.Heading("현재 월드",19),22,18,450,48);currentLayout.Place(Ui.Note(profile.Name),22,84,450,44);
        string world;try{world=BackupService.WorldFolders(server,false)[0];}catch(Exception e){world=e.Message;}currentLayout.Place(Ui.Note(world),22,142,450,100);currentLayout.Place(Ui.Note("서버 프로그램·모드·설정은 유지됩니다."),22,268,450,50);
        var next=new SurfaceCard{Padding=Padding.Empty};root.Place(next,548,136,522,344);var nextLayout=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new(522,344),BackColor=WarmTheme.Surface};next.Controls.Add(nextLayout);nextLayout.Place(Ui.Heading("새 월드 선택",19),22,18,478,48);nextLayout.Place(selected,22,82,478,126);
        nextLayout.Place(Ui.Action("ZIP 선택",()=>{if(busy)return;using var pick=new OpenFileDialog{Filter="월드 ZIP|*.zip"};if(pick.ShowDialog(this)==DialogResult.OK)Select(pick.FileName);}),22,248,230,52);
        nextLayout.Place(Ui.Action("폴더 선택",()=>{if(busy)return;using var pick=new FolderBrowserDialog{Description="level.dat가 있는 월드 폴더"};if(pick.ShowDialog(this)==DialogResult.OK)Select(pick.SelectedPath);}),268,248,230,52);
        root.Place(status,26,500,1044,70);root.Place(Ui.Note("새 맵에 포함된 플레이어 데이터가 적용됩니다. 현재 인벤토리는 자동 이식하지 않습니다."),26,576,1044,48);
        apply=Ui.Action("새 월드 적용",async()=>await Apply(),true);apply.Enabled=false;root.Place(apply,828,626,242,48);
        FormClosing+=(_,e)=>{if(busy)e.Cancel=true;};WarmTheme.Apply(this);
    }
    void Select(string path){source=path;selected.Text=Path.GetFileName(path)+"\n"+path;apply.Enabled=true;}
    async Task Apply()
    {
        if(busy)return;using var confirm=new WorldImportConfirmation(AppSettings.Load().BackupRoot);if(WarmDialog.Present(this,confirm)!=DialogResult.OK)return;
        busy=true;apply.Enabled=false;
        try{var result=await WorldImportService.ApplyAsync(server,source,confirm.BackupDirectory,new Progress<string>(s=>{if(!IsDisposed)status.Text=s;}));WarmDialog.Show(this,"새 월드를 적용했습니다."+(result.BackupPath==null?"":"\n기존 월드 백업: "+result.BackupPath));busy=false;DialogResult=DialogResult.OK;Close();}
        catch(Exception e){status.Text="적용하지 못했습니다.";WarmDialog.Show(this,e.Message,"월드 적용 확인");}
        finally{busy=false;apply.Enabled=true;}
    }
}

public sealed class WorldImportConfirmation:WarmWindow
{
    readonly WarmToggle backup=new(){Text="기존 월드를 백업하고 새 맵 적용",Checked=true};readonly WarmTextBox path=new();
    public string? BackupDirectory=>backup.Checked?path.Text:null;
    public WorldImportConfirmation(string initial)
    {
        Text="월드 교체 확인";Size=new(720,550);BackColor=WarmTheme.Background;MaximizeBox=false;MinimizeBox=false;path.Text=initial;path.ReadOnly=true;
        var root=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new(716,470)};Controls.Add(root);root.Place(Ui.Heading("기존 월드를 백업할까요?",22),28,16,660,54);root.Place(Ui.Note("백업을 선택하면 백업 완료 후에만 새 맵을 적용합니다."),28,80,660,50);root.Place(backup,28,146,660,48);root.Place(path,28,216,490,48);
        var browse=Ui.Action("찾아보기",()=>{using var picker=new FolderBrowserDialog();if(picker.ShowDialog(this)==DialogResult.OK)path.Text=picker.SelectedPath;});root.Place(browse,530,216,158,48);
        var notice=Ui.Note("기존 월드를 ZIP으로 보관합니다.");root.Place(notice,28,296,660,66);var okay=Ui.Action("백업 후 적용",()=>{if(!backup.Checked&&WarmDialog.Show(this,"기존 월드의 백업 없이 교체할까요?", "월드 교체",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;DialogResult=DialogResult.OK;Close();},true);root.Place(okay,468,394,220,48);root.Place(Ui.Action("취소",()=>Close()),312,394,140,48);
        backup.CheckedChanged+=(_,_)=>{path.Enabled=browse.Enabled=backup.Checked;okay.Text=backup.Checked?"백업 후 적용":"백업 없이 적용";notice.Text=backup.Checked?"기존 월드를 ZIP으로 보관합니다.":"기존 월드를 복구할 백업이 만들어지지 않습니다.";};WarmTheme.Apply(this);
    }
}
