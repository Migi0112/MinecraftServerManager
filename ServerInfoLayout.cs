// [소스 안내] 서버 정보 화면을 구성하고 관련 정보와 작업 버튼을 배치합니다.
namespace NeoForgeServerManager;
public sealed partial class MainForm
{
    Control BuildServerInformation(ImagePicker picker)
    {
        var root=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new Size(1220,714)};
        picker.Dock=DockStyle.None;picker.Visible=false;picker.Size=new Size(1,1);root.Controls.Add(picker);
        root.Place(Ui.Heading("서버 정보",24),18,0,1180,54);
        ReferencePanel Section(int x,int y,int w,int h,string title)
        {var card=new SurfaceCard{Padding=Padding.Empty};root.Place(card,x,y,w,h);var panel=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new Size(w,h),BackColor=WarmTheme.Surface};card.Controls.Add(panel);panel.Place(Ui.Heading(title,19),24,12,w-48,48);return panel;}
        var images=Section(18,68,494,550,"서버 이미지");var picture=new PictureBox{SizeMode=PictureBoxSizeMode.StretchImage};images.Place(picture,122,72,250,208);
        var choose=Button("이미지 변경",()=>{string path=Artwork.Import(this);if(path.Length>0)picker.SetPath(path);});images.Place(choose,24,298,218,52);images.Place(Button("기본 이미지로",()=>picker.SetPath("")),254,298,218,52);images.Place(Ui.Note("PNG · JPG 형식을 지원합니다."),120,360,350,38);
        for(int i=0;i<4;i++){string path=i==0?"":"preset:"+(i-1);var p=new PictureBox{Image=Artwork.Read(path),SizeMode=PictureBoxSizeMode.Zoom,Cursor=Cursors.Hand};images.Place(p,24+i*114,420,104,104);p.Click+=(_,_)=>picker.SetPath(path);p.Disposed+=(_,_)=>p.Image?.Dispose();}
        var basic=Section(532,68,688,326,"기본 정보");basic.Place(Ui.Note("서버 이름"),38,72,142,46);basic.Place(profileName,186,64,486,46);
        var software=Ui.Note("");var version=Ui.Note("");var location=Ui.Note("");
        basic.Place(Ui.Note("서버 종류"),38,134,142,46);basic.Place(software,186,126,486,46);basic.Place(Ui.Note("Minecraft 버전"),38,198,142,46);basic.Place(version,186,190,486,46);basic.Place(Ui.Note("서버 폴더"),38,262,142,46);basic.Place(location,186,254,320,50);basic.Place(Button("폴더 열기",()=>OpenFolder(Folder())),522,254,150,50);
        var preview=Section(532,410,688,208,"메인 화면 미리보기");var small=new PictureBox{SizeMode=PictureBoxSizeMode.StretchImage};preview.Place(small,24,80,114,96);var nameLabel=Ui.Heading("",18);preview.Place(nameLabel,154,80,330,40);var versionLabel=Ui.Note("");preview.Place(versionLabel,154,124,330,40);var memory=Ui.Note("");preview.Place(memory,490,94,174,70);
        void RefreshPreview(){picture.Image?.Dispose();small.Image?.Dispose();picture.Image=Artwork.Read(picker.ImagePath);small.Image=Artwork.Read(picker.ImagePath);nameLabel.Text=profileName.Text;software.Text=current.Kind;version.Text=current.MinecraftVersion;location.Text=current.Folder;location.AutoEllipsis=true;versionLabel.Text=current.Kind+" · Minecraft "+current.MinecraftVersion;memory.Text=$"RAM\n{current.MinRam} ~ {current.MaxRam} GB";}
        picker.ImageChanged+=RefreshPreview;EventHandler changed=(_,_)=>RefreshPreview();profileName.TextChanged+=changed;
        root.Disposed+=(_,_)=>{picker.ImageChanged-=RefreshPreview;profileName.TextChanged-=changed;picture.Image?.Dispose();small.Image?.Dispose();};RefreshPreview();
        root.Place(Button("변경 취소",()=>{profileName.Text=current.Name;picker.SetPath(current.ImagePath);}),98,654,206,54);
        root.Place(Button("변경사항 저장",()=>{current.ImagePath=picker.ImagePath;SaveProfile();RefreshDashboard();},true),324,654,254,54);root.Place(Ui.Note("저장하면 서버목록과 상단 정보에 반영됩니다."),640,660,560,42);
        return root;
    }
}
