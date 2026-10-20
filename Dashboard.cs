// [소스 안내] 서버 관리 화면의 상태 표시와 탐색 구성을 담당합니다. 서버 실행 상태를 화면 표시로 연결합니다.
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;
namespace NeoForgeServerManager;
// 실제 탭 페이지는 유지하며 왼쪽 메뉴로 전환합니다.
public sealed class PageDeck : TabControl
{
    public PageDeck() { Appearance = TabAppearance.FlatButtons; ItemSize = new Size(1, 1); SizeMode = TabSizeMode.Fixed; }
    protected override void WndProc(ref Message m) { if (m.Msg == 0x1328 && !DesignMode) { m.Result = (IntPtr)1; return; } base.WndProc(ref m); }
    protected override void OnResize(EventArgs e){base.OnResize(e);if(Width<2||Height<2)return;using var path=WarmTheme.Round(ClientRectangle,16);var old=Region;Region=new Region(path);old?.Dispose();}
}
public sealed class MetricTile : SurfaceCard
{
    public string Title { get; set; } = ""; string value="—"; public string Value { get=>value; set {if(this.value==value)return;this.value=value;Invalidate();} } public string Detail { get; set; } = ""; public string Glyph { get; set; } = "";
    public void AddSample(float value) { }
    protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(WarmTheme.Surface);using var pen=new Pen(WarmTheme.Line);e.Graphics.DrawLine(pen,0,14,0,Height-14);}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);using var small=new Font("Noto Sans KR",10);using var large=new Font("Noto Sans KR",14,FontStyle.Bold);
        ReferenceIcons.Draw(e.Graphics,Glyph,new Rectangle(12,28,26,26),WarmTheme.Ink);
        TextRenderer.DrawText(e.Graphics,Title,small,new Rectangle(48,12,Width-52,26),WarmTheme.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(e.Graphics,Value,large,new Rectangle(48,38,Width-52,34),WarmTheme.Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
    }
}
public sealed partial class MainForm
{
    readonly Label dashboardName = new() { AutoSize = true, Font = new Font("Noto Sans KR", 20, FontStyle.Bold) }, dashboardVersion = new() { AutoSize = true };
    readonly MetricTile cpuTile = new() { Title = "CPU", Glyph="cpu" }, ramTile = new() { Title = "RAM", Glyph="memory" }, playersTile = new() { Title = "접속자", Glyph="users" }, tpsTile = new() { Title = "TPS", Glyph="chart" };
    readonly PictureBox dashboardPicture = new() { SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(136, 92) };
    readonly Label javaBadge = new() { Text = "Java\n버전 확인 전", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }; string shownImage = "?";
    void BuildDashboard()
    {
        var oldRoot = Controls.OfType<TableLayoutPanel>().First(); var tabs = oldRoot.Controls.OfType<TabControl>().First();
        var profileSettings = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false }; profileSettings.Controls.Add(oldRoot.GetControlFromPosition(0, 0)!); profileSettings.Controls.Add(oldRoot.GetControlFromPosition(0, 1)!);
        var oldActions = oldRoot.GetControlFromPosition(0, 2)!; profileSettings.Controls.Add(oldActions.Controls.OfType<Button>().First(x => x.Text == "강제 종료")); Page(tabs, "서버 메뉴", profileSettings, Label("서버 설정 · 저장 위치"));
        var options = tabs.TabPages.Cast<TabPage>().First(p => p.Text == "실행 옵션"); var ramControls = Ui.Row(Label("최소 RAM (GB)"), xms, Label("최대 RAM (GB)"), xmx, Button("설정 저장", SaveProfile, true)); options.Controls.Add(ramControls); ramControls.Dock = DockStyle.Bottom;
        // 콘솔의 파일/명령 기능은 기존 이벤트를 유지한 채 새 레이아웃에 재배치합니다.
        var consolePage = tabs.TabPages[0]; var layout = (TableLayoutPanel)consolePage.Controls[0]; var toolsRow = (FlowLayoutPanel)layout.GetControlFromPosition(0, 0)!; foreach (var label in toolsRow.Controls.OfType<Label>().ToArray()) toolsRow.Controls.Remove(label); profileSettings.Controls.Add(toolsRow.Controls.OfType<Button>().First(x => x.Text.Contains("crash-reports"))); InstallConsoleFilters(toolsRow); profileSettings.Controls.Add(toolsRow.Controls.OfType<Button>().First(x=>x.Text=="상태 조회 로그"));
        console.BorderStyle = BorderStyle.None; console.Font = new Font("Consolas", 12); console.Resize += (_, _) => RoundConsole(); command.PlaceholderText = "서버 명령어를 입력하세요…"; var send = command.Parent!.Controls.OfType<Button>().First(); ((WarmButton)send).Primary = true;ArrangeConsole(consolePage,toolsRow,send);
        var backupPage = tabs.TabPages.Cast<TabPage>().First(p => p.Text == "백업"); var backupTools = (TableLayoutPanel)backupPage.Controls[0]; ((FlowLayoutPanel)backupTools.GetControlFromPosition(0, 0)!).Controls.Add(Button("백업 복원", () => tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().First(p => p.Text == "백업 복원")));
        profileSettings.Controls.Add(Button("server.properties 원문 편집", () => tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().First(p => p.Text == "server.properties")));
        foreach (var control in new[] { start, stop, restart }) control.Parent?.Controls.Remove(control);
        metrics.Parent?.Controls.Remove(metrics); status.Parent?.Controls.Remove(status); tabs.Parent?.Controls.Remove(tabs); Controls.Remove(oldRoot); oldRoot.Dispose();
        InstallCatalogPages(tabs); InstallBackupList(tabs); ArrangeExecutionOptions(tabs);
        var root = new TableLayoutPanel { Tag="landscape",BackColor=Color.Transparent,Padding=new Padding(12,18,12,10),Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 }; root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 246)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); Controls.Add(root);
        var side = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8), RowCount = 3, ColumnCount = 1, Tag = "surface" }; side.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); side.RowStyles.Add(new RowStyle(SizeType.Absolute, 64)); side.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); side.RowStyles.Add(new RowStyle(SizeType.Absolute, 76)); root.Controls.Add(side, 0, 0);side.Resize+=(_,_)=>{using var shape=WarmTheme.Round(side.ClientRectangle,18);var old=side.Region;side.Region=new Region(shape);old?.Dispose();}; var back = Ui.Action("← 서버목록", Close); back.Dock = DockStyle.Fill; side.Controls.Add(back, 0, 0);
        var navigation = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Tag = "surface", Padding = new Padding(0) }; side.Controls.Add(navigation, 0, 1); var javaCard = new SurfaceCard { Dock = DockStyle.Fill, Padding = new Padding(6) }; javaBadge.TextAlign=ContentAlignment.MiddleLeft;javaBadge.Padding=new Padding(50,0,0,0);javaBadge.Font=new Font("Noto Sans KR",11);javaCard.Controls.Add(javaBadge);var javaIcon=new ReferenceGlyph("java"){Dock=DockStyle.Left,Width=42};javaCard.Controls.Add(javaIcon);javaIcon.BringToFront(); side.Controls.Add(javaCard, 0, 2);
        var palettePage=new TabPage("명령어 팔레트"){Padding=Padding.Empty};tabs.TabPages.Add(palettePage);
        navigation.Resize+=(_,_)=>{foreach(Control b in navigation.Controls)b.Width=Math.Max(170,navigation.Width-SystemInformation.VerticalScrollBarWidth-6);};
        var navButtons = new Dictionary<TabPage, WarmButton>();
        void Nav(string title, string name) { var page = tabs.TabPages.Cast<TabPage>().First(x => x.Text == name); var button = new WarmButton { Text = title, Font = new Font("Noto Sans KR",16), Width = 196, Height = 51, Ghost = true, Margin = new Padding(0, 2, 0, 2) }; button.Click += (_, _) => { if(name=="명령어 팔레트")OpenCommandPalette();else tabs.SelectedTab = page; }; button.Text=title[(title.IndexOf("  ")+2)..];button.Navigation=true;button.IconName=name switch {"콘솔"=>"console","접속자"=>"users","Mods"=>"folderOutline","설정 · 한국어 도움말" or "서버 메뉴"=>"settings","서버 정보"=>"info","화이트리스트" or "블랙리스트"=>"shield","운영자 (ops)"=>"crown","백업"=>"database","로그 뷰어"=>"file",_=>"terminal"};navigation.Controls.Add(button); navButtons[page] = button; if (name == "Mods") page.TextChanged += (_, _) => button.Text = page.Text == "Plugins" ? "플러그인 관리" : "모드 관리"; }
        Nav("▣  콘솔", "콘솔"); Nav("⌘  명령어 팔레트", "명령어 팔레트"); Nav("♙  접속자", "접속자"); Nav("♧  모드 관리", "Mods"); Nav("⚙  서버 설정", "설정 · 한국어 도움말"); Nav("⚙  서버 메뉴", "서버 메뉴"); Nav("ⓘ  서버 정보", "서버 정보"); Nav("◇  화이트리스트", "화이트리스트"); Nav("⊘  블랙리스트", "블랙리스트"); Nav("♛  운영자", "운영자 (ops)"); Nav("▤  백업 · 복원", "백업"); Nav("▧  로그", "로그 뷰어"); Nav("☷  실행 옵션", "실행 옵션");
        var main = new TableLayoutPanel { Tag="landscape",BackColor=Color.Transparent,Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(6,0,6,6) }; main.RowStyles.Add(new RowStyle(SizeType.Absolute, 116)); main.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.Controls.Add(main, 1, 0);
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 }; header.RowStyles.Add(new RowStyle(SizeType.Percent,100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24)); var headerCard=new SurfaceCard{Dock=DockStyle.Fill,Padding=new Padding(8),Margin=new Padding(0)};headerCard.Controls.Add(header);main.Controls.Add(headerCard,0,0);
        var heading = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(2, 0, 0, 0) }; heading.Controls.Add(dashboardPicture); var words = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false }; words.Controls.AddRange([dashboardName, dashboardVersion, status]); heading.Controls.Add(words); header.Controls.Add(heading, 0, 0);
        var meter = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 }; meter.RowStyles.Add(new RowStyle(SizeType.Percent,100)); foreach (var tile in new[] { cpuTile, ramTile, playersTile, tpsTile }) { meter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); tile.Dock = DockStyle.Fill; tile.Margin = new Padding(3, 12, 3, 12); meter.Controls.Add(tile); }
        header.Controls.Add(meter, 1, 0);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(2, 18, 0, 0), WrapContents = false }; start.Text = "▶ 서버 시작"; stop.Text = "■ 정상 종료"; restart.Text = "↻ 재시작"; foreach (var b in new[] { restart, start, stop }) { b.AutoSize = false; b.Size = new Size(112, 52); b.MinimumSize = new Size(100, 42); actions.Controls.Add(b); }
        header.Controls.Add(actions, 2, 0);
        // 상단 정보는 승인 이미지의 좌표를 공유합니다. FlowLayout의 자동 줄바꿈으로 버튼이 밀리지 않게 합니다.
        var measuredHeader=new ReferencePanel{Dock=DockStyle.Fill,ReferenceSize=new Size(1260,116)};
        headerCard.Padding=Padding.Empty;headerCard.Controls.Add(measuredHeader);
        measuredHeader.Place(dashboardPicture,12,12,134,92);measuredHeader.Place(dashboardName,172,12,236,36);
        dashboardVersion.Font=new Font("Noto Sans KR",12);measuredHeader.Place(dashboardVersion,172,48,236,28);
        measuredHeader.Place(status,172,78,236,28);
        measuredHeader.Place(cpuTile,410,16,124,84);measuredHeader.Place(ramTile,534,16,162,84);measuredHeader.Place(playersTile,696,16,126,84);measuredHeader.Place(tpsTile,822,16,124,84);
        foreach(var button in new[]{restart,start,stop})button.Font=new Font("Noto Sans KR",14);
        measuredHeader.Place(restart,944,28,138,60);measuredHeader.Place(start,1094,28,154,60);measuredHeader.Place(stop,1094,28,154,60);
        header.Dispose();
        var card = new SurfaceCard { Dock = DockStyle.Fill, Padding = new Padding(0) }; card.Controls.Add(tabs); tabs.Dock = DockStyle.Fill; card.Margin=new Padding(0,12,0,0);main.Controls.Add(card, 0, 1);
        tabs.SelectedIndexChanged += (_, _) => { foreach (var pair in navButtons) { pair.Value.Primary = pair.Key == tabs.SelectedTab; pair.Value.Invalidate(); } }; navButtons[consolePage].Primary = true; RoundConsole(); RefreshDashboard();
    }
    void ApplyConsoleTheme()
    {
        // 실제 RichTextBox와 입력 기능을 유지하고 콘솔 영역만 시안의 어두운 팔레트를 사용합니다.
        var page=console.Parent;
        while(page!=null && page is not TabPage)page=page.Parent;
        if(page==null)return;
        page.Padding=Padding.Empty;
        void Shape(){if(page.Width<2||page.Height<2)return;using var path=WarmTheme.Round(page.ClientRectangle,12);var old=page.Region;page.Region=new Region(path);old?.Dispose();}
        page.Resize+=(_,_)=>Shape();Shape();
        void Paint(Control c){if(c is Panel or TableLayoutPanel or FlowLayoutPanel or TabPage)c.BackColor=WarmTheme.Surface;if(c is WarmButton b)b.Dark=false;if(c is WarmToggle)c.ForeColor=WarmTheme.Ink;foreach(Control child in c.Controls)Paint(child);}
        Paint(page);console.BackColor=Color.FromArgb(29,28,27);console.ForeColor=Color.WhiteSmoke;
    }
    void RoundConsole() { if (console.Width < 2 || console.Height < 2) return; using var path = WarmTheme.Round(console.ClientRectangle, 14); var old = console.Region; console.Region = new Region(path); old?.Dispose(); }
    void RefreshDashboard()
    {
        start.Visible = !server.Running; stop.Visible = server.Running; ((WarmButton)start).Primary = ((WarmButton)stop).Primary = true;
        dashboardName.Text = string.IsNullOrWhiteSpace(profileName.Text) ? "내 서버" : profileName.Text; dashboardVersion.Text = current.Kind + " · " + current.MinecraftVersion;
        if (shownImage != current.ImagePath) { shownImage = current.ImagePath; dashboardPicture.Image?.Dispose(); dashboardPicture.Image = Artwork.Read(shownImage); }
        cpuTile.Value = server.Running ? Regex.Match(server.Metrics, @"CPU ([\d.,]+)%").Groups[1].Value + "%" : "—"; if (cpuTile.Value == "%") cpuTile.Value = "—"; var ram = Regex.Match(server.Metrics, @"RAM ([\d.,]+) MB"); ramTile.Value = ram.Success && double.TryParse(ram.Groups[1].Value.Replace(",", ""), out var mb) ? $"{mb / 1024:F1} GB" : "—";
        playersTile.Value = server.Online ? $"{server.Players.Count} / {propertyFile.Values.GetValueOrDefault("max-players", "20")}" : "—"; tpsTile.Value = server.Online && (DateTime.UtcNow - server.TpsSample).TotalSeconds < 45 ? server.Tps : "—";
    }
}

