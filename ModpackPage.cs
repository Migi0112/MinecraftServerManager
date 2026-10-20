// [소스 안내] 새 서버 만들기 안에서 모드팩을 검색하고 설치합니다. 선택한 대표 이미지를 서버 프로필에 자동 연결합니다.
namespace NeoForgeServerManager;

public sealed class ModpackPage : WarmWindow
{
    readonly DownloadClient net;
    readonly CancellationTokenSource lifetime=new();
    CancellationTokenSource? installCancel;
    readonly WarmTextBox search=new(){Dock=DockStyle.Fill,PlaceholderText="Modrinth 모드팩 검색"};
    readonly ModpackResultsList results=new(){Dock=DockStyle.Fill};
    readonly WarmCombo versions=new(){Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly WarmTextBox serverName=new(){Dock=DockStyle.Fill};
    readonly WarmTextBox java=new(){Dock=DockStyle.Fill,Text="java.exe"};
    readonly WarmCombo minimum=new(){Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly WarmCombo maximum=new(){Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly WarmToggle optional=new(){Text="서버용 선택 모드도 함께 설치",Checked=true,AutoSize=true};
    readonly WarmToggle agreement=new(){Text="Minecraft EULA에 동의합니다",AutoSize=true};
    readonly Label status=new(){Dock=DockStyle.Fill,AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft};
    readonly Label description=new(){Dock=DockStyle.Fill,AutoEllipsis=true};
    readonly PictureBox picture=new(){Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,Tag="modpack-preview"};
    readonly WarmButton install=new(){Text="모드팩 서버 추가",Primary=true,Dock=DockStyle.Fill,Enabled=false};
    readonly WarmButton previous=new(){Text="← 이전",Width=110};
    readonly WarmButton following=new(){Text="다음 →",Width=110};
    readonly WarmButton returnButton=new(){Text="← 서버 종류 선택",Dock=DockStyle.Fill};
    readonly Label pageInfo=new(){AutoSize=true,Padding=new Padding(8)};
    readonly Panel editor=new(){Dock=DockStyle.Fill};
    readonly Panel browser=new(){Dock=DockStyle.Fill};
    readonly Dictionary<string,string> icons=new();
    ModpackProject? selected;
    int searchRequest,selectionRequest,offset,total;
    bool searching,loading;
    public bool Installing{get;private set;}
    public Profile? Installed{get;private set;}
    public ModpackPage(DownloadClient? client=null)
    {
        net=client??new DownloadClient();
        Text="모드팩 서버 추가";Size=new(1200,820);MinimumSize=new(960,720);WideBrand=false;
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=3};
        root.RowStyles.Add(new(SizeType.Absolute,66));root.RowStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.Absolute,54));
        var head=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};
        head.RowStyles.Add(new(SizeType.Percent,100));
        head.ColumnStyles.Add(new(SizeType.Absolute,180));head.ColumnStyles.Add(new(SizeType.Percent,100));
        returnButton.Click+=(_,_)=>Close();head.Controls.Add(returnButton,0,0);
        var heading=Ui.Heading("모드팩 서버 추가 · Modrinth",23);heading.Dock=DockStyle.Fill;head.Controls.Add(heading,1,0);root.Controls.Add(head,0,0);
        var columns=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};
        columns.RowStyles.Add(new(SizeType.Percent,100));
        columns.ColumnStyles.Add(new(SizeType.Percent,52));columns.ColumnStyles.Add(new(SizeType.Percent,48));
        var left=new SurfaceCard{Dock=DockStyle.Fill,Padding=new Padding(16),Margin=new Padding(0,8,10,8)};
        var right=new SurfaceCard{Dock=DockStyle.Fill,Padding=new Padding(18),Margin=new Padding(0,8,0,8)};
        left.Controls.Add(browser);right.Controls.Add(editor);columns.Controls.Add(left,0,0);columns.Controls.Add(right,1,0);root.Controls.Add(columns,0,1);
        var listLayout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3};
        listLayout.RowStyles.Add(new(SizeType.Absolute,52));listLayout.RowStyles.Add(new(SizeType.Percent,100));listLayout.RowStyles.Add(new(SizeType.Absolute,48));
        var searchBar=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};searchBar.RowStyles.Add(new(SizeType.Percent,100));searchBar.ColumnStyles.Add(new(SizeType.Percent,100));searchBar.ColumnStyles.Add(new(SizeType.Absolute,90));
        var find=Ui.Action("검색",async()=>await Search(0));find.AutoSize=false;find.MinimumSize=new(36,34);find.Dock=DockStyle.Fill;searchBar.Controls.Add(search,0,0);searchBar.Controls.Add(find,1,0);
        listLayout.Controls.Add(searchBar,0,0);listLayout.Controls.Add(results,0,1);
        var paging=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};paging.Controls.AddRange([previous,pageInfo,following]);listLayout.Controls.Add(paging,0,2);browser.Controls.Add(listLayout);
        var form=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=9,AutoScroll=true};
        form.ColumnStyles.Add(new(SizeType.Absolute,100));form.ColumnStyles.Add(new(SizeType.Percent,100));
        int[] heights=[76,46,42,42,42,42,36,36,42];foreach(int height in heights)form.RowStyles.Add(new(SizeType.Absolute,height));
        form.Controls.Add(picture,0,0);form.Controls.Add(description,1,0);
        void Field(string text,Control control,int row){form.Controls.Add(new Label{Text=text,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,row);form.Controls.Add(control,1,row);}
        Field("팩 버전",versions,1);Field("서버 이름",serverName,2);Field("Java",java,3);Field("최소 RAM",minimum,4);Field("최대 RAM",maximum,5);
        minimum.Items.AddRange(Enumerable.Range(1,64).Select(n=>(object)n.ToString()).ToArray());
        maximum.Items.AddRange(Enumerable.Range(1,64).Select(n=>(object)n.ToString()).ToArray());minimum.SelectedItem="4";maximum.SelectedItem="8";
        form.Controls.Add(optional,0,6);form.SetColumnSpan(optional,2);form.Controls.Add(agreement,0,7);form.SetColumnSpan(agreement,2);
        var eula=Ui.Action("EULA 읽기 ↗",()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://www.minecraft.net/eula"){UseShellExecute=true}));
        eula.AutoSize=false;eula.MinimumSize=new(36,34);eula.Dock=DockStyle.Fill;form.Controls.Add(eula,0,8);form.SetColumnSpan(eula,2);
        var actions=new TableLayoutPanel{Dock=DockStyle.Bottom,Height=90,RowCount=2,ColumnCount=1};
        actions.RowStyles.Add(new(SizeType.Absolute,54));actions.RowStyles.Add(new(SizeType.Percent,100));actions.Controls.Add(install,0,0);
        var hint=Ui.Note("대표 이미지는 서버 카드에 자동으로 적용돼요.");hint.Font=new Font("Noto Sans KR",11);hint.Dock=DockStyle.Fill;actions.Controls.Add(hint,0,1);
        editor.Controls.Add(form);editor.Controls.Add(actions);root.Controls.Add(status,0,2);Controls.Add(root);
        results.SelectedIndexChanged+=async(_,_)=>await SelectPack();
        search.KeyDown+=async(_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;await Search(0);}};
        search.TextChanged+=async(_,_)=>{if(search.Text.Length==0)await Search(0);};
        previous.Click+=async(_,_)=>await Search(Math.Max(0,offset-20));following.Click+=async(_,_)=>await Search(offset+20);
        agreement.CheckedChanged+=(_,_)=>RefreshButtons();versions.SelectedIndexChanged+=(_,_)=>RefreshButtons();
        install.Click+=async(_,_)=>await Install();
        FormClosing+=(_,e)=>{if(Installing){installCancel?.Cancel();status.Text="설치를 취소하고 임시 파일을 정리하고 있어요…";e.Cancel=true;}};
        FormClosed+=(_,_)=>{lifetime.Cancel();net.Dispose();picture.Image?.Dispose();lifetime.Dispose();};
        WarmTheme.Apply(this);Shown+=async(_,_)=>await Search(0);
    }
    void RefreshButtons()
    {
        install.Enabled=!Installing&&!searching&&!loading&&selected!=null&&versions.SelectedItem is ModpackRelease&&agreement.Checked;
        previous.Enabled=!Installing&&!searching&&offset>0;following.Enabled=!Installing&&!searching&&offset+20<total;
    }
    async Task Search(int requestedOffset)
    {
        if(Installing)return;
        int request=++searchRequest;searching=true;RefreshButtons();status.Text="모드팩을 찾고 있어요…";
        try
        {
            var page=await new ModpackService(net).Search(search.Text.Trim(),requestedOffset,lifetime.Token);
            if(IsDisposed||request!=searchRequest)return;
            offset=requestedOffset;total=page.Total;results.BeginUpdate();results.Items.Clear();results.Items.AddRange(page.Items);results.EndUpdate();
            pageInfo.Text=total==0?"검색 결과 없음":$"{offset+1}–{offset+page.Items.Length} / {total:N0}";
            status.Text=search.Text.Length==0?"인기 모드팩 · 다운로드순":"모드팩 검색 결과";
            if(page.Items.Length>0)results.SelectedIndex=0;
            else{selected=null;versions.Items.Clear();description.Text="검색 결과가 없어요.";picture.Image?.Dispose();picture.Image=null;}
        }
        catch(OperationCanceledException){}
        catch(Exception error){if(!IsDisposed&&request==searchRequest)status.Text="검색 실패 · "+error.Message;}
        finally{if(!IsDisposed&&request==searchRequest){searching=false;RefreshButtons();}}
    }
    async Task SelectPack()
    {
        if(Installing)return;
        int request=++selectionRequest;selected=results.SelectedItem as ModpackProject;
        versions.Items.Clear();picture.Image?.Dispose();picture.Image=null;
        if(selected==null){RefreshButtons();return;}
        var pack=selected;serverName.Text=pack.Title;description.Text=pack.Title+Environment.NewLine+pack.Description;
        loading=true;RefreshButtons();
        try
        {
            var service=new ModpackService(net);
            var releases=await service.Releases(pack,lifetime.Token);
            if(IsDisposed||request!=selectionRequest)return;
            versions.Items.AddRange(releases);if(releases.Length>0)versions.SelectedIndex=0;
            status.Text=releases.Length==0?"이 팩은 지원하는 서버용 버전이 없어요.":"팩 버전을 선택하고 서버를 추가하세요.";
            if(!icons.TryGetValue(pack.Id,out var path)){path=await service.Image(pack,lifetime.Token);icons[pack.Id]=path;}
            if(!IsDisposed&&request==selectionRequest)picture.Image=Artwork.Read(path);
        }
        catch(OperationCanceledException){}
        catch(Exception error){if(!IsDisposed&&request==selectionRequest)status.Text="정보 조회 실패 · "+error.Message;}
        finally{if(!IsDisposed&&request==selectionRequest){loading=false;RefreshButtons();}}
    }
    async Task Install()
    {
        if(Installing||!agreement.Checked||selected==null||versions.SelectedItem is not ModpackRelease release)return;
        Installing=true;returnButton.Text="설치 취소";browser.Enabled=editor.Enabled=false;RefreshButtons();
        installCancel=new CancellationTokenSource();
        try
        {
            Installed=await new ModpackService(net).Install(selected,release,AppSettings.Load().ServerRoot,serverName.Text,java.Text,
                int.Parse(minimum.Text),int.Parse(maximum.Text),optional.Checked,
                new Progress<string>(message=>{if(!IsDisposed)status.Text=message;}),installCancel.Token);
            Installing=false;DialogResult=DialogResult.OK;Close();
        }
        catch(OperationCanceledException){status.Text="설치를 취소했어요. 서버목록에는 추가하지 않았어요.";}
        catch(Exception error){status.Text="설치 실패 · "+error.Message;WarmDialog.Show(this,error.Message,"모드팩 설치 안내");}
        finally{Installing=false;installCancel.Dispose();installCancel=null;if(!IsDisposed){returnButton.Text="← 서버 종류 선택";browser.Enabled=editor.Enabled=true;RefreshButtons();}}
    }
}

public sealed class ModpackResultsList : ListBox
{
    public ModpackResultsList()
    {
        DrawMode=DrawMode.OwnerDrawFixed;ItemHeight=88;IntegralHeight=false;BorderStyle=BorderStyle.None;
        BackColor=WarmTheme.Surface;SetStyle(ControlStyles.OptimizedDoubleBuffer,true);
    }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if(e.Index<0||Items[e.Index] is not ModpackProject pack)return;
        bool chosen=(e.State&DrawItemState.Selected)!=0;
        using var fill=new SolidBrush(chosen?WarmTheme.Peach:WarmTheme.Surface);e.Graphics.FillRectangle(fill,e.Bounds);
        var bounds=Rectangle.Inflate(e.Bounds,-12,-8);
        using var title=new Font(Font.FontFamily,12,FontStyle.Bold);
        TextRenderer.DrawText(e.Graphics,pack.Title,title,new Rectangle(bounds.X,bounds.Y,bounds.Width,30),WarmTheme.Ink,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
        TextRenderer.DrawText(e.Graphics,pack.Description,Font,new Rectangle(bounds.X,bounds.Y+34,bounds.Width,28),WarmTheme.Muted,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
        if(chosen){using var border=new Pen(WarmTheme.Accent);e.Graphics.DrawRectangle(border,e.Bounds.X,e.Bounds.Y,e.Bounds.Width-1,e.Bounds.Height-1);}
    }
}
