using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;
using NeoForgeServerManager;

internal static class Program
{
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool IsWindowEnabled(IntPtr hwnd);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h,IntPtr dc,uint flags);
    [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]static extern IntPtr GetStyle(IntPtr h,int index);
    [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]static extern IntPtr SetStyle(IntPtr h,int index,IntPtr value);
    static int passed;
    static IEnumerable<Control> AllControls(Control root){foreach(Control c in root.Controls){yield return c;foreach(var child in AllControls(c))yield return child;}}
    static void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); passed++; }
    static void Throws(Action action, string name)
    {
        try { action(); } catch { Check(true, name); return; }
        throw new Exception("Expected rejection: " + name);
    }
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length < 2) throw new ArgumentException("Usage: Verification <workspace> <FakeServer.java> [java.exe]");
        ApplicationConfiguration.Initialize();
        FontAssets.Load();Check(new Font("Noto Sans KR",14).Name=="Noto Sans KR","packaged font is available to native controls");
        string root = Path.GetFullPath(args[0]); Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("MIGI_MANAGER_DATA_ROOT",Path.Combine(root,"app-data"));
        var demo=Seed(root);
        if(args.Contains("--mod-maintenance")){ModMaintenanceChecks.Run(root,Check);Console.WriteLine($"ALL {passed} MOD MAINTENANCE CHECKS PASSED");return;}
        if(args.Contains("--mod-maintenance-live")){ModMaintenanceChecks.Live(root,Check);Console.WriteLine($"ALL {passed} LIVE MOD MAINTENANCE CHECKS PASSED");return;}
        if(args.Contains("--modpacks")){ModpackChecks.Run(root,Check);Console.WriteLine($"ALL {passed} MODPACK CHECKS PASSED");return;}
        if(args.Contains("--modpacks-live")){ModpackChecks.Live(root,Check);Console.WriteLine($"ALL {passed} LIVE MODPACK CHECKS PASSED");return;}
        if(args.Contains("--online-interactions")){OnlineInteractionChecks.Run(root,demo,Check);Console.WriteLine($"ALL {passed} ONLINE INTERACTION CHECKS PASSED");return;}
        if(args.Contains("--dependency-ui")){DependencyUiChecks.Run(root,args[^1],Check);Console.WriteLine($"ALL {passed} DEPENDENCY UI CHECKS PASSED");return;}
        if(args.Contains("--local-dependencies")){LocalDependencyChecks.Run(root,args[^1],Check);Console.WriteLine($"ALL {passed} LOCAL DEPENDENCY CHECKS PASSED");return;}
        if(args.Contains("--new-features")){FeatureChecks.Run(root,Check);Console.WriteLine($"ALL {passed} NEW FEATURE CHECKS PASSED");return;}
        if(args.Contains("--catalog-live")){FeatureChecks.Live(root,Check);Console.WriteLine($"ALL {passed} LIVE FEATURE CHECKS PASSED");return;}
        if(args.Contains("--new-layout")){demo.MinecraftVersion="1.21.1";Storage.Save([demo]);
            void Pump(int ms){var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(10);}}
            using var home=new HomeForm();home.Show();Pump(200);AllControls(home).OfType<HomeServerCard>().First().Controls.OfType<Button>().Single(b=>b.Text=="관리 →").PerformClick();Pump(300);var manager=AllControls(home).OfType<MainForm>().Single();var tabs=AllControls(manager).OfType<TabControl>().First();var nav=AllControls(manager).OfType<WarmButton>().Where(b=>b.Navigation).ToArray();Check(Array.FindIndex(nav,b=>b.Text=="명령어 팔레트")==Array.FindIndex(nav,b=>b.Text=="콘솔")+1,"palette navigation immediately follows console");Check(nav.Any(b=>b.Text=="서버 메뉴"),"server menu moved into sidebar");Check(typeof(MainForm).GetField("details",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)==null,"changing metadata strip removed entirely");tabs.SelectedTab=tabs.TabPages.Cast<TabPage>().First(p=>p.Text=="Mods");Pump(300);var placeholder=AllControls(manager).OfType<ComingSoonButton>().Single();Check(placeholder.Visible&&placeholder.Parent!.ClientRectangle.Contains(placeholder.Bounds),"placeholder fits existing toolbar");
            void Capture(string file){using var image=new Bitmap(home.Width,home.Height);using var graphics=Graphics.FromImage(image);var dc=graphics.GetHdc();PrintWindow(home.Handle,dc,2);graphics.ReleaseHdc(dc);image.Save(Path.Combine(root,file+".png"));}Capture("mods-current-layout");
            tabs.SelectedTab=tabs.TabPages.Cast<TabPage>().First(p=>p.Text=="콘솔");nav.Single(b=>b.Text=="명령어 팔레트").PerformClick();Pump(1800);Check(tabs.SelectedTab!.Text=="명령어 팔레트","sidebar opens dedicated palette page");var palette=AllControls(manager).OfType<CommandPaletteWindow>().Single();Check(!palette.TopLevel&&palette.Visible,"palette stays inside existing management shell");Capture("palette-current-layout");var picker=AllControls(palette).OfType<GiveItemPicker>().Single();Check(picker.Items.Any(i=>i.Id=="minecraft:diamond"),"item selector loaded real item names");Check(picker.Visible&&picker.Height>30&&picker.Parent!.Height>30,"item selector has visible layout space");picker.PerformClick();Pump(200);var popup=(ToolStripDropDown)typeof(GiveItemPicker).GetField("popup",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(picker)!;var popupPanel=((ToolStripControlHost)popup.Items[0]).Control;var itemSearch=AllControls(popupPanel).OfType<WarmTextBox>().Single();var itemList=AllControls(popupPanel).OfType<ExtensionResultsList>().Single();itemList.TopIndex=100;Check(itemList.TopIndex==100,"dropdown scroll position can move through catalog");itemSearch.Text="다이아몬드";Pump(100);Check(itemList.Items.Cast<GiveItem>().Any(i=>i.Id=="minecraft:diamond"),"dropdown Korean search finds diamond");itemSearch.Text="diamond";Pump(100);Check(itemList.Items.Cast<GiveItem>().Any(i=>i.Id=="minecraft:diamond"),"dropdown English search finds diamond");using(var bitmap=new Bitmap(popup.Width,popup.Height)){popup.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(root,"picker-current-layout.png"));}itemList.SelectedItem=itemList.Items.Cast<GiveItem>().First(i=>i.Id=="minecraft:diamond");typeof(Control).GetMethod("OnKeyDown",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(itemList,[new KeyEventArgs(Keys.Enter)]);Pump(100);Check(picker.Selected?.Id=="minecraft:diamond","dropdown selection commits item identity");AllControls(palette).OfType<WarmButton>().Single(b=>b.Text=="← 콘솔").PerformClick();Pump(100);Check(!AllControls(manager).OfType<CommandPaletteWindow>().Any(),"palette returns to existing console");home.Close();Console.WriteLine($"ALL {passed} LAYOUT CHECKS PASSED");return;
        }
        void Preview(Form window, string file)
        {
            window.Opacity=0;window.ShowInTaskbar=false;window.Show();window.PerformLayout();Application.DoEvents();
            Check(AllControls(window).Any(c=>c.Text=="제작자 Migi0112"&&c.Visible&&window.RectangleToScreen(window.ClientRectangle).Contains(c.RectangleToScreen(c.ClientRectangle))),file+" creator footer visible");
            using var bitmap=new Bitmap(window.Width,window.Height);window.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(root,file+".png"));
        }
        if(args.Contains("--paint-stability")){
            void Pump(int ms){var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(10);}}
            using var home=new HomeForm();home.Show();Pump(200);AllControls(home).OfType<HomeServerCard>().First().Controls.OfType<Button>().Single(b=>b.Text=="관리 →").PerformClick();Pump(300);var manager=AllControls(home).OfType<MainForm>().Single();var tabs=AllControls(manager).OfType<TabControl>().First();var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            Check(((System.Windows.Forms.Timer)typeof(MainForm).GetField("timer",flags)!.GetValue(manager)!).Enabled,"real hosted management timer runs during paint test");Check((GetStyle(home.Handle,-20).ToInt64()&0x02000000)==0&&(GetStyle(manager.Handle,-20).ToInt64()&0x02000000)==0,"parent and embedded window do not use recursive composition");
            foreach(string name in new[]{"접속자","설정 · 한국어 도움말","로그 뷰어"}){
                tabs.SelectedTab=tabs.TabPages.Cast<TabPage>().First(p=>p.Text==name);Pump(350);int paints=0;PaintEventHandler handler=(_,_)=>paints++;manager.Paint+=handler;Pump(1800);manager.Paint-=handler;Console.WriteLine($"VISIBLE IDLE {name}: {paints} parent paints / 1800ms");Check(paints<12,name+" has no continuous parent repaint loop");
                using var image=new Bitmap(home.Width,home.Height);using var graphics=Graphics.FromImage(image);var dc=graphics.GetHdc();PrintWindow(home.Handle,dc,2);graphics.ReleaseHdc(dc);image.Save(Path.Combine(root,name=="접속자"?"players.png":name=="로그 뷰어"?"logs.png":"settings.png"));
            }
            var metric=AllControls(manager).OfType<MetricTile>().First();int updates=0;metric.Paint+=(_,_)=>updates++;metric.Value="12%";Pump(150);Check(updates>0,"changed metric still paints with composition disabled");home.Close();Console.WriteLine($"ALL {passed} VISIBLE PAINT CHECKS PASSED");return;
        }
        if(args.Contains("--paint-loop")){
            using var home=new HomeForm();home.Show();Application.DoEvents();AllControls(home).OfType<HomeServerCard>().First().Controls.OfType<Button>().Single(b=>b.Text=="관리 →").PerformClick();Application.DoEvents();var manager=AllControls(home).OfType<MainForm>().Single();var tabs=AllControls(manager).OfType<TabControl>().First();
            foreach(var composite in new[]{(Home:true,Manager:true),(Home:true,Manager:false),(Home:false,Manager:true),(Home:false,Manager:false)}){
                SetStyle(home.Handle,-20,(IntPtr)((GetStyle(home.Handle,-20).ToInt64()&~0x02000000L)|(composite.Home?0x02000000L:0)));
                SetStyle(manager.Handle,-20,(IntPtr)((GetStyle(manager.Handle,-20).ToInt64()&~0x02000000L)|(composite.Manager?0x02000000L:0)));
                foreach(string name in new[]{"접속자","설정 · 한국어 도움말","로그 뷰어"}){
                    tabs.SelectedTab=tabs.TabPages.Cast<TabPage>().First(p=>p.Text==name);Application.DoEvents();var counts=new Dictionary<string,int>();var invalidates=new Dictionary<string,int>();var controls=AllControls(home).Prepend(home).ToArray();PaintEventHandler paint=(sender,_)=>{var c=(Control)sender!;string key=c.GetType().Name+":"+c.Text;counts[key]=counts.GetValueOrDefault(key)+1;};InvalidateEventHandler inv=(sender,_)=>{var c=(Control)sender!;string key=c.GetType().Name+":"+c.Text;invalidates[key]=invalidates.GetValueOrDefault(key)+1;};foreach(var c in controls){c.Paint+=paint;c.Invalidated+=inv;}
                    var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<1800){Application.DoEvents();Thread.Sleep(10);}foreach(var c in controls){c.Paint-=paint;c.Invalidated-=inv;}
                    Console.WriteLine($"PAINT LOOP composite={composite} {name} paints={counts.Values.Sum()} invalidates={invalidates.Values.Sum()}");foreach(var pair in counts.OrderByDescending(p=>p.Value).Take(6))Console.WriteLine("PAINT "+pair.Key+" "+pair.Value);foreach(var pair in invalidates.OrderByDescending(p=>p.Value).Take(6))Console.WriteLine("INVALIDATE "+pair.Key+" "+pair.Value);
                }
            }
            home.Close();return;
        }
        if(args.Contains("--idle-ui")){
            using var manager=new MainForm(false,demo){Opacity=0,ShowInTaskbar=false};manager.Show();Application.DoEvents();var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;var tick=typeof(MainForm).GetMethod("UpdateUI",flags)!;var tabs=AllControls(manager).OfType<TabControl>().First();
            foreach(string pageName in new[]{"접속자","설정 · 한국어 도움말","로그 뷰어"}){
                tabs.SelectedTab=tabs.TabPages.Cast<TabPage>().First(p=>p.Text==pageName);tick.Invoke(manager,null);Application.DoEvents();var invalidations=new Dictionary<string,int>();var tracked=AllControls(manager).ToArray();InvalidateEventHandler handler=(sender,_)=>{var c=(Control)sender!;string key=c.GetType().Name+":"+c.Text;invalidations[key]=invalidations.GetValueOrDefault(key)+1;};foreach(var c in tracked)c.Invalidated+=handler;
                for(int i=0;i<10;i++){tick.Invoke(manager,null);Application.DoEvents();}foreach(var c in tracked)c.Invalidated-=handler;
                foreach(var pair in invalidations.OrderByDescending(p=>p.Value).Take(5))Console.WriteLine($"INVALIDATE {pageName} {pair.Key}: {pair.Value}");Check(invalidations.Values.Sum()==0,pageName+" unchanged screen does not invalidate on repeated ticks");
            }
            var metric=AllControls(manager).OfType<MetricTile>().First();int metricPaints=0;metric.Invalidated+=(_,_)=>metricPaints++;metric.Value="changed";Check(metricPaints>0,"changed metric still repaints");
            using var catalog=new CatalogView("테스트");catalog.SetItems([new CatalogItem("one","one","","")]);var initial=AllControls(catalog).OfType<CatalogCard>().Single();catalog.SetItems([new CatalogItem("one","one","","")]);Check(!initial.IsDisposed&&ReferenceEquals(initial,AllControls(catalog).OfType<CatalogCard>().Single()),"unchanged roster preserves cards and scroll state");manager.Close();Console.WriteLine($"ALL {passed} IDLE UI CHECKS PASSED");return;
        }
        if(args.Contains("--resize")){
            var size=new Size(1280,900);Point[] points=[new(5,20),new(1275,20),new(5,880),new(1275,880),new(5,400),new(1275,400),new(500,5),new(500,895)];int[] hits=[13,14,16,17,10,11,12,15];
            for(int i=0;i<points.Length;i++)Check(WindowResizeInput.HitTest(size,points[i])==hits[i],"edge/corner direction "+hits[i]);
            Check(WindowResizeInput.HitTest(size,new(100,100))==0&&WindowResizeInput.HitTest(size,new(-1,100))==0,"interior and outside untouched");Check(WindowResizeInput.HitTest(size,new(14,30),144)==13,"DPI-scaled corner");
            using var window=new WarmWindow{Size=size,Opacity=0,ShowInTaskbar=false};var cover=new Panel{Dock=DockStyle.Fill};window.Controls.Add(cover);window.Show();Application.DoEvents();var input=new WindowResizeInput(window);
            Check(input.HitForControl(cover,window.PointToScreen(new(5,880)))==16,"child-covered bottom corner routes to parent window");using var unrelated=new Form();Check(input.HitForControl(unrelated,window.PointToScreen(new(5,880)))==0,"unrelated window untouched");
            window.MaximizeBox=false;Check(input.HitForControl(cover,window.PointToScreen(new(5,880)))==0,"fixed dialog does not resize");window.MaximizeBox=true;window.EmbedInShell();Check(input.HitForControl(cover,window.PointToScreen(new(5,880)))==0,"embedded page does not resize independently");window.Close();Console.WriteLine($"ALL {passed} RESIZE CHECKS PASSED");return;
        }
        if(args.Contains("--registrations")){
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var originalRecord=new Profile{Name="같은 서버",Folder=demo.Folder,BackupFolder=Path.Combine(root,"backups")};
            var duplicate=new Profile{Name="같은 서버",Folder=demo.Folder.ToUpperInvariant()+Path.DirectorySeparatorChar,ImagePath="preset:1",BackupFolder=originalRecord.BackupFolder};
            var other=new Profile{Name="다른 서버",Folder=Path.Combine(root,"other"),BackupFolder=originalRecord.BackupFolder};Directory.CreateDirectory(other.Folder);
            Storage.Save([originalRecord,duplicate,other]);
            Check(ServerRegistrations.ForDisplay(Storage.Load()).Count==2,"case and trailing separator duplicates grouped");
            Check(ServerRegistrations.ForDisplay(Storage.Load())[0].Id==duplicate.Id,"custom image record preserved");
            Check(ServerRegistrations.ForDisplay(Storage.Load(),p=>p.Id==originalRecord.Id)[0].Id==originalRecord.Id,"active session takes precedence");
            Throws(()=>ServerRegistrations.Register(new Profile{Folder=originalRecord.Folder}),"duplicate registration rejected");
            Check(Storage.Load().Count==3,"legacy settings retained without rewriting");
            using(var home=new HomeForm(false)){home.Opacity=0;home.ShowInTaskbar=false;home.Show();typeof(HomeForm).GetMethod("Reload",flags)!.Invoke(home,null);Check(AllControls(home).OfType<HomeServerCard>().Count()==2,"home shows one card per folder");Preview(home,"home-clean");home.Close();}
            using(var manager=new MainForm(false)){manager.InitializeForHome(duplicate);manager.Opacity=0;manager.ShowInTaskbar=false;manager.Show();var tabs=AllControls(manager).OfType<TabControl>().First();tabs.SelectedTab=tabs.TabPages.Cast<TabPage>().First(p=>p.Text=="서버 메뉴");Application.DoEvents();Check(!AllControls(manager).Any(c=>c.Text is "새 프로필" or "프로필 저장" or "프로필 삭제"),"profile actions removed");Check(AllControls(manager).Any(c=>c.Text=="서버 삭제"&&c.Visible),"server deletion remains available");typeof(MainForm).GetMethod("SaveProfile",flags)!.Invoke(manager,null);Check(Storage.Load().Count==3&&Storage.Load().Any(p=>p.Id==duplicate.Id),"settings save retains identity without new record");Preview(manager,"server-menu");manager.HomeHosted=false;manager.Close();}
            var owned=BackupPaths.ForServer(originalRecord);File.WriteAllText(Path.Combine(owned,"one.zip"),"originalRecord");var owned2=BackupPaths.ForServer(duplicate);File.WriteAllText(Path.Combine(owned2,"two.zip"),"second");
            var plan=ServerDeletion.Plan(duplicate);Check(plan.Backups.Length==2&&plan.RegistrationIds.Length==2,"delete review includes backup ownership of both aliases");
            ServerDeletion.Execute(plan,false,false);Check(Storage.Load().Count==1&&Directory.Exists(originalRecord.Folder)&&File.Exists(Path.Combine(owned,"one.zip"))&&File.Exists(Path.Combine(owned2,"two.zip")),"deregister removes aliases but preserves unchecked files and backups");
            Storage.Save([originalRecord,duplicate,other]);var stale=ServerDeletion.Plan(duplicate);Storage.Save([originalRecord,duplicate,other,new Profile{Folder=originalRecord.Folder}]);Throws(()=>ServerDeletion.Execute(stale,true,true),"new alias after confirmation rejected before deletion");Check(Directory.Exists(originalRecord.Folder),"stale confirmation leaves server intact");
            Storage.Save([originalRecord,duplicate,other]);ServerDeletion.Execute(ServerDeletion.Plan(duplicate),true,true);Check(Storage.Load().Count==1&&!Directory.Exists(originalRecord.Folder)&&!File.Exists(Path.Combine(owned,"one.zip"))&&!File.Exists(Path.Combine(owned2,"two.zip")),"confirmed server deletion removes aliases and both owned backup sets");
            Console.WriteLine($"ALL {passed} REGISTRATION CHECKS PASSED");return;
        }
        if(args.Contains("--home-view")){
            void Settle(){var until=DateTime.UtcNow.AddMilliseconds(240);while(DateTime.UtcNow<until){Application.DoEvents();Thread.Sleep(10);}}
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            void Native(Form window,string name){window.PerformLayout();Application.DoEvents();using var bitmap=new Bitmap(window.Width,window.Height);using var g=Graphics.FromImage(bitmap);var dc=g.GetHdc();PrintWindow(window.Handle,dc,2);g.ReleaseHdc(dc);bitmap.Save(Path.Combine(root,name+".png"));}
            var profiles=Storage.Load();for(int i=0;i<profiles.Count;i++){profiles[i].Folder=Path.Combine(root,"server-"+i);Directory.CreateDirectory(profiles[i].Folder);}
            profiles.Add(new Profile{Name="삭제된 옛 경로",Folder=Path.Combine(root,"missing-folder")});Storage.Save(profiles);
            new HomePreferences().Save();
            using(var home=new HomeForm()){home.Opacity=0;home.ShowInTaskbar=false;home.Show();Application.DoEvents();
                Check(AllControls(home).OfType<HomeServerCard>().Count()==3,"missing folder excluded from visible servers");
                Check(Storage.Load().Count==4,"missing profile preserved on disk");
                Check(!AllControls(home).Any(c=>c is Label&&c.Text.Contains("실행 중")&&c.Text.Contains("개")),"footer count removed");
                Native(home,"home-cards");int rendered=Scenery.RenderCount;Native(home,"home-cards-again");Check(Scenery.RenderCount==rendered,"repainting reuses wallpaper without resampling");
                home.Size=new Size(1280,900);Native(home,"home-cards-small");
                Check(AllControls(home).OfType<HomeServerCard>().All(c=>c.Right<=c.Parent!.ClientSize.Width&&c.Height>=280&&c.Bottom<=c.Parent!.ClientSize.Height),"compact cards fit completely in first row");
                Check(AllControls(home).OfType<HomeServerCard>().All(c=>c.Controls.OfType<Button>().Where(b=>b.Visible).All(b=>c.ClientRectangle.Contains(b.Bounds))),"all card buttons fit inside compact card");
                typeof(HomeForm).GetMethod("SetView",flags)!.Invoke(home,[true]);Native(home,"home-details-small");
                Check(AllControls(home).OfType<HomeServerCard>().All(c=>c.Compact&&c.Height==76),"details use compact rows");
                Check(AllControls(home).OfType<HomeServerCard>().All(c=>c.Controls.OfType<WarmButton>().Count(b=>b.Visible)==3),"details expose start management delete only");
                Check(HomePreferences.Load().Details,"view preference persisted");
                var drag=(HomeDragReorder)typeof(HomeForm).GetField("reorder",flags)!.GetValue(home)!;
                var rows=AllControls(home).OfType<HomeServerCard>().ToArray();
                Check(!AllControls(home).Any(c=>c.Text=="순서 변경"),"separate order button removed");
                Check(rows.All(c=>c.Grip.Visible&&c.Grip.Cursor==Cursors.SizeAll),"both views expose visible move grips");
                var dragFirst=rows[0];var dragSecond=rows[1];
                var down=typeof(Control).GetMethod("OnMouseDown",flags)!;var move=typeof(Control).GetMethod("OnMouseMove",flags)!;var up=typeof(Control).GetMethod("OnMouseUp",flags)!;
                down.Invoke(dragFirst.Grip,[new MouseEventArgs(MouseButtons.Left,1,12,12,0)]);
                var dest=dragFirst.Grip.PointToClient(dragSecond.PointToScreen(new Point(dragSecond.Width/2,dragSecond.Height-8)));
                move.Invoke(dragFirst.Grip,[new MouseEventArgs(MouseButtons.Left,0,dest.X,dest.Y,0)]);
                Check(dragSecond.DropHighlight,"drag destination highlighted");
                // Release OS capture before timed assertions so real desktop mouse movement cannot steer this fixture.
                drag.Cancel();drag.Begin(dragFirst,dragFirst.Grip.PointToScreen(new Point(12,12)));drag.Update(dragFirst.Grip.PointToScreen(dest));
                var preview=AllControls(home).OfType<DragAnimation>().Single();var beforeMove=preview.PreviewPosition;var neighborBefore=preview.CardPosition(dragSecond.Profile.Id);
                drag.Update(dragFirst.Grip.PointToScreen(new Point(dest.X+12,dest.Y)));Check(preview.PreviewPosition.X==beforeMove.X+12,"drag preview follows cursor path");Settle();Check(preview.CardPosition(dragSecond.Profile.Id)!=neighborBefore,"neighbor animates into vacated slot");Native(home,"drag-preview");
                up.Invoke(dragFirst.Grip,[new MouseEventArgs(MouseButtons.Left,1,dest.X,dest.Y,0)]);Settle();Check(!AllControls(home).OfType<DragAnimation>().Any(),"landing disposes preview surface");
                Check(HomePreferences.Load().Order[0]==profiles[1].Id,"grip mouse drag persists moved item");
                Check(AllControls(home).OfType<HomeServerCard>().First().Profile.Id==profiles[1].Id,"saved order reflected in rows");
                rows=AllControls(home).OfType<HomeServerCard>().ToArray();var beforeCancel=HomePreferences.Load().Order.ToArray();drag.Begin(rows[0],rows[0].PointToScreen(Point.Empty));drag.Update(rows[1].PointToScreen(new Point(50,40)));drag.Cancel();Check(HomePreferences.Load().Order.SequenceEqual(beforeCancel),"cancel does not persist order");
                typeof(HomeForm).GetMethod("SetView",flags)!.Invoke(home,[false]);rows=AllControls(home).OfType<HomeServerCard>().ToArray();drag.Begin(rows[2],rows[2].PointToScreen(Point.Empty));drag.Update(rows[0].PointToScreen(new Point(4,40)));drag.Finish();Settle();Check(HomePreferences.Load().Order[0]==profiles[2].Id,"card grid drag inserts before target");Native(home,"home-drag-cards");
                typeof(HomeForm).GetMethod("SetView",flags)!.Invoke(home,[true]);rows=AllControls(home).OfType<HomeServerCard>().ToArray();drag.Begin(rows[0],rows[0].PointToScreen(Point.Empty));drag.Update(rows[2].PointToScreen(new Point(40,70)));drag.Finish();Settle();Check(HomePreferences.Load().Order[0]==profiles[1].Id,"detail drag supports moving downward again");
                Check(AppUpdates.ReleaseTitle(new AppRelease("1.12.16","제목","","", "",0)).StartsWith("v12.16 · "),"version added when title has no version");
                Check(AppUpdates.ReleaseTitle(new AppRelease("1.12.14","v12.14 · 제목","","", "",0))=="v12.14 · 제목","version not duplicated for old titles");
                Directory.CreateDirectory(profiles[3].Folder);typeof(HomeForm).GetMethod("Reload",flags)!.Invoke(home,null);Check(AllControls(home).OfType<HomeServerCard>().Count()==4,"restored folder automatically reappears");
                for(int i=0;i<3;i++){var watch=Stopwatch.StartNew();AllControls(home).OfType<WarmButton>().Single(b=>b.Text=="＋ 서버 추가").PerformClick();Application.DoEvents();var child=(Form)typeof(HomeForm).GetField("activePage",flags)!.GetValue(home)!;Check(child is CreateServerWizard&&child.Visible,"wizard navigation completes");Native(home,"wizard-transition");child.Close();Application.DoEvents();Check(typeof(HomeForm).GetField("activePage",flags)!.GetValue(home)==null,"home restored after wizard");Console.WriteLine("TRANSITION round trip ms: "+watch.ElapsedMilliseconds);}
                for(int i=0;i<2;i++){var watch=Stopwatch.StartNew();AllControls(home).OfType<HomeServerCard>().First().Controls.OfType<WarmButton>().Single(b=>b.Text=="관리 →").PerformClick();Application.DoEvents();var child=(Form)typeof(HomeForm).GetField("activePage",flags)!.GetValue(home)!;Check(child is MainForm&&child.Visible,"management navigation completes");Native(home,"management-transition");child.Close();Application.DoEvents();Check(typeof(HomeForm).GetField("activePage",flags)!.GetValue(home)==null,"home restored after management");Console.WriteLine("MANAGEMENT round trip ms: "+watch.ElapsedMilliseconds);}
                var many=Storage.Load();for(int n=0;n<7;n++){string folder=Path.Combine(root,"extra-"+n);Directory.CreateDirectory(folder);many.Add(new Profile{Name="추가 서버 "+n,Folder=folder});}Storage.Save(many);typeof(HomeForm).GetMethod("Reload",flags)!.Invoke(home,null);
                rows=AllControls(home).OfType<HomeServerCard>().ToArray();var panel=(Panel)rows[0].Parent!;drag.Begin(rows[0],rows[0].PointToScreen(Point.Empty));drag.Update(panel.PointToScreen(new Point(panel.Width/2,panel.Height-8)));var scrollUntil=DateTime.UtcNow.AddMilliseconds(250);while(DateTime.UtcNow<scrollUntil){Application.DoEvents();Thread.Sleep(15);}Check(panel.AutoScrollPosition.Y<0,"dragging near edge scrolls server list");drag.Cancel();
                home.Size=new Size(1536,1024);Native(home,"home-details");
                using(var updatePage=new UpdatesPage(()=>false,(_,_)=>Task.CompletedTask)){typeof(UpdatesPage).GetField("busy",flags)!.SetValue(updatePage,true);typeof(HomeForm).GetMethod("Navigate",flags)!.Invoke(home,[updatePage]);updatePage.Render([new AppRelease("1.12.17","드래그 순서 변경과 화면 정렬","카드와 자세히 보기의 핸들을 잡고 옮겨 순서를 저장합니다.","", "",0),new AppRelease("1.12.16","화면 전환 · 서버 순서와 자세히 보기","배경 캐시와 서버 목록 개선.","", "",0)]);Native(home,"updates-version-footer");Check(!home.Scenic,"plain page disables outer landscape bleed");using(var shot=new Bitmap(Path.Combine(root,"updates-version-footer.png"))){Check(shot.GetPixel(200,home.Height-28).ToArgb()==WarmTheme.Background.ToArgb(),"footer seam is solid cream");}typeof(UpdatesPage).GetField("busy",flags)!.SetValue(updatePage,false);updatePage.Close();}Check(home.Scenic,"return restores home landscape");
                home.Close();}
            using(var restored=new HomeForm()){restored.Opacity=0;restored.ShowInTaskbar=false;restored.Show();Application.DoEvents();Check(AllControls(restored).OfType<HomeServerCard>().First().Compact&&AllControls(restored).OfType<HomeServerCard>().First().Profile.Id==profiles[1].Id,"view and order survive restart");restored.Close();}
            Console.WriteLine($"ALL {passed} HOME VIEW CHECKS PASSED");return;
        }
        if(args.Contains("--landscape")){
            void Native(Form window,string name){window.Opacity=0;window.ShowInTaskbar=false;window.Show();Application.DoEvents();using var bitmap=new Bitmap(window.Width,window.Height);using var g=Graphics.FromImage(bitmap);var dc=g.GetHdc();PrintWindow(window.Handle,dc,2);g.ReleaseHdc(dc);bitmap.Save(Path.Combine(root,name+".png"));}
            using(var home=new HomeForm()){Native(home,"landscape-home");Check(home.Scenic,"home enables shared scenery");home.Size=new Size(1280,900);Native(home,"landscape-home-small");Check(AllControls(home).OfType<HomeServerCard>().All(c=>c.Left>=0&&c.Right<=c.Parent!.ClientSize.Width),"home cards fit horizontally with vertical scrolling at minimum size");home.Close();}
            using(var wizard=new CreateServerWizard()){Native(wizard,"landscape-wizard");Check(wizard.Scenic,"wizard enables shared scenery");wizard.Size=new Size(1280,900);Native(wizard,"landscape-wizard-small");var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;foreach(int step in new[]{3,4,3}){typeof(CreateServerWizard).GetField("step",flags)!.SetValue(wizard,step);typeof(CreateServerWizard).GetMethod("Render",flags)!.Invoke(wizard,null);}var name=(WarmTextBox)typeof(CreateServerWizard).GetField("name",flags)!.GetValue(wizard)!;Check(name.Parent!=null&&name.Visible,"wizard server name survives back navigation");typeof(CreateServerWizard).GetField("step",flags)!.SetValue(wizard,4);typeof(CreateServerWizard).GetMethod("Render",flags)!.Invoke(wizard,null);Native(wizard,"wizard-image-small");wizard.Size=new Size(1536,1024);Native(wizard,"wizard-image");wizard.Close();}
            using(var manager=new MainForm(false)){manager.InitializeForHome(demo);Native(manager,"landscape-manager");Check(manager.Scenic,"management enables shared scenery");manager.Size=new Size(1280,900);Native(manager,"landscape-manager-small");manager.HomeHosted=false;manager.Close();}
            string area=Path.Combine(root,"deletion-fixtures");Directory.CreateDirectory(area);
            foreach(bool serverChoice in new[]{false,true})foreach(bool backupChoice in new[]{false,true}){
                string part=Path.Combine(area,$"case-{serverChoice}-{backupChoice}");var p=new Profile{Name="CreateMod Neo",Folder=Path.Combine(part,"server"),BackupFolder=Path.Combine(part,"backups")};Directory.CreateDirectory(p.Folder);File.WriteAllText(Path.Combine(p.Folder,"server.properties"),"level-name=world");File.WriteAllText(Path.Combine(p.Folder,"keep.txt"),"server data");string owned=BackupPaths.ForServer(p);File.WriteAllText(Path.Combine(owned,"backup.zip"),"test zip");File.WriteAllText(Path.Combine(owned,"notes.txt"),"keep non-zip");string other=Path.Combine(p.BackupFolder,"other");Directory.CreateDirectory(other);File.WriteAllText(Path.Combine(other,".server-id"),"someone-else");File.WriteAllText(Path.Combine(other,"backup.zip"),"other backup");Storage.Save([p]);var plan=ServerDeletion.Plan(p);Check(plan.Backups.Length==1,"only exact server marker backup selected");
                if(!serverChoice&&!backupChoice){using var dialog=new ServerDeleteWindow(plan);Native(dialog,"delete-confirmation");Check(dialog.ServerOption.Checked&&dialog.BackupOption.Checked,"both deletion options default checked");dialog.ServerOption.Checked=false;dialog.BackupOption.Checked=false;Check(!dialog.ServerOption.Checked&&!dialog.BackupOption.Checked,"deletion options independently deselect");dialog.Close();}
                ServerDeletion.Execute(plan,serverChoice,backupChoice);Check(Directory.Exists(p.Folder)==!serverChoice&&File.Exists(Path.Combine(owned,"backup.zip"))==!backupChoice,$"deletion choice server={serverChoice} backups={backupChoice}");Check(File.Exists(Path.Combine(other,"backup.zip"))&&File.Exists(Path.Combine(owned,"notes.txt")),"unrelated backup and non-zip preserved");Check(Storage.Load().Count==0,"profile removed after successful selection");
            }
            var shared=new Profile{Folder=Path.Combine(area,"shared"),BackupFolder=Path.Combine(area,"shared-backups")};Directory.CreateDirectory(shared.Folder);File.WriteAllText(Path.Combine(shared.Folder,"data"),"keep");var sharedOther=new Profile{Folder=Path.Combine(shared.Folder,"nested-server")};Storage.Save([shared,sharedOther]);Throws(()=>ServerDeletion.Execute(ServerDeletion.Plan(shared),true,false),"shared folder deletion rejected");Check(File.Exists(Path.Combine(shared.Folder,"data"))&&Storage.Load().Count==2,"rejection preserves shared files and profiles");
            Storage.Save([shared]);using(var locked=new FileStream(Path.Combine(shared.Folder,".manager.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){Throws(()=>ServerDeletion.Execute(ServerDeletion.Plan(shared),true,false),"running folder lock rejects deletion");}Check(Directory.Exists(shared.Folder),"locked server remains");using(var management=new MainForm(false)){management.InitializeForHome(shared);management.Opacity=0;management.ShowInTaskbar=false;management.Show();bool deleted=false;management.ProfileDeleted+=()=>deleted=true;using var modalTimer=new System.Windows.Forms.Timer{Interval=30};modalTimer.Tick+=(_,_)=>{var dialog=Application.OpenForms.Cast<Form>().OfType<ServerDeleteWindow>().FirstOrDefault();if(dialog!=null){dialog.ServerOption.Checked=false;dialog.BackupOption.Checked=false;dialog.DialogResult=DialogResult.OK;dialog.Close();}};modalTimer.Start();management.DeleteStoredProfile(management);Application.DoEvents();modalTimer.Stop();Check(deleted&&Storage.Load().Count==0&&Directory.Exists(shared.Folder),"management deletion dialog removes profile and preserves deselected data");management.HomeHosted=false;management.Close();}
            Console.WriteLine($"ALL {passed} LANDSCAPE AND DELETE CHECKS PASSED");return;
        }
        if(args.Contains("--popular")){
            var p=new Profile{Kind="NeoForge",MinecraftVersion="1.21.1",Folder=Path.Combine(root,"empty-server")};Directory.CreateDirectory(p.Folder);
            string url=Uri.UnescapeDataString(ModPluginService.SearchUrl(p,"",true,100));Check(url.Contains("limit=100")&&url.Contains("offset=100")&&url.Contains("index=downloads"),"popular URL requests 100 ranked items and offset");Check(ModPluginService.SearchUrl(p,"create").Contains("index=relevance"),"search keeps relevance order");
            var requests=new List<(string Query,int Offset)>();
            Task<ModPluginService.SearchPage> Fetch(string q,bool compatible,int offset){requests.Add((q,offset));var items=Enumerable.Range(offset+1,Math.Min(100,235-offset)).Select(i=>new ModPluginService.Result("fixture"+i,"테스트 모드 "+i,"fixture"+i)).ToArray();return Task.FromResult(new ModPluginService.SearchPage(items,235,offset));}
            using var form=new ExtensionSearchForm(p,null,Fetch,async(_,_)=>await Task.Delay(30));Preview(form,"popular-list");
            var list=AllControls(form).OfType<ListBox>().Single(c=>c.Name=="ExtensionResults");var query=AllControls(form).OfType<WarmTextBox>().Single(c=>c.Name=="ExtensionQuery");
            Check(requests.SequenceEqual(new[]{("",0)})&&list.Items.Count==100,"initial view loads 100 popular items without search");
            AllControls(form).OfType<Button>().Single(b=>b.Text=="다음 100개 →").PerformClick();Check(requests.Last()==("",100)&&((ModPluginService.Result)list.Items[0]).Id=="fixture101","next page begins at rank 101");
            query.Text="create";AllControls(form).OfType<Button>().Single(b=>b.Text=="검색").PerformClick();Check(requests.Last()==("create",0),"search starts on first page");query.Clear();Check(requests.Last()==("",0)&&list.Items.Count==100,"clearing text restores popular immediately");
            list.SelectedIndex=42;list.TopIndex=40;Application.DoEvents();int invalidations=0;list.Invalidated+=(_,_)=>invalidations++;var buffered=(ExtensionResultsList)list;
            for(int i=0;i<100;i++)buffered.QueueArtwork(list.Items[i]);var repaintWait=Stopwatch.StartNew();while(repaintWait.ElapsedMilliseconds<180){Application.DoEvents();Thread.Sleep(5);}
            Check(invalidations<=2,"100 artwork completions batched into visible repaint");Check(list.TopIndex==40&&list.SelectedIndex==42,"artwork repaint preserves scroll and selection");int beforeRequests=requests.Count;var idle=Stopwatch.StartNew();while(idle.ElapsedMilliseconds<300){Application.DoEvents();Thread.Sleep(10);}Check(requests.Count==beforeRequests,"idle list does not refetch results");
            int top=list.TopIndex;ExtensionListPosition.Restore(list,42,top);Check(list.TopIndex==top&&list.SelectedIndex==42,"scroll and selection restored together");
            using(var timer=new System.Windows.Forms.Timer{Interval=40}){timer.Tick+=(_,_)=>{var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f is DialogWindow);if(dialog!=null){dialog.Opacity=0;dialog.DialogResult=DialogResult.No;dialog.Close();}};timer.Start();AllControls(form).OfType<Button>().Single(b=>b.Name=="ExtensionInstall").PerformClick();timer.Stop();}
            Check(list.TopIndex==top&&list.SelectedIndex==42,"cancelled installation preserves list location");
            using(var timer=new System.Windows.Forms.Timer{Interval=40}){timer.Tick+=(_,_)=>{var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f is DialogWindow);if(dialog!=null){dialog.Opacity=0;dialog.DialogResult=DialogResult.Yes;dialog.Close();}};timer.Start();AllControls(form).OfType<Button>().Single(b=>b.Name=="ExtensionInstall").PerformClick();var wait=Stopwatch.StartNew();while(wait.ElapsedMilliseconds<3000&&AllControls(form).OfType<Button>().Single(b=>b.Name=="ExtensionInstall").Text!="설치됨"){Application.DoEvents();Thread.Sleep(10);}timer.Stop();}
            Check(list.TopIndex==top&&list.SelectedIndex==42&&AllControls(form).OfType<Button>().Single(b=>b.Name=="ExtensionInstall").Text=="설치됨","completed asynchronous installation preserves position and marks installed");
            int paintedRows=0;DrawItemEventHandler drawCounter=(_,_)=>paintedRows++;list.DrawItem+=drawCounter;var scrollWatch=Stopwatch.StartNew();for(int scroll=40;scroll<70;scroll++){list.TopIndex=scroll;list.Invalidate();list.Update();Application.DoEvents();}scrollWatch.Stop();list.DrawItem-=drawCounter;Check(paintedRows>0&&paintedRows<300,"scroll paints visible rows rather than all 100 results");Check(list.TopIndex==69&&requests.Count==beforeRequests,"scroll retains position without fetching again");Console.WriteLine($"SCROLL 30 steps: {scrollWatch.ElapsedMilliseconds} ms, {paintedRows} rows drawn");
            list.SelectedIndex=0;list.TopIndex=0;Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)){using var g=Graphics.FromImage(bitmap);var dc=g.GetHdc();PrintWindow(form.Handle,dc,2);g.ReleaseHdc(dc);bitmap.Save(Path.Combine(root,"popular-native.png"));}form.Close();
            using(var management=new MainForm(false,demo)){Preview(management,"server-menu");var menu=AllControls(management).OfType<Button>().Single(b=>b.Text=="서버 메뉴");Check(menu.Width>=150&&menu.Parent!.ClientRectangle.Contains(menu.Bounds),"server menu button fits sidebar");using var bitmap=new Bitmap(management.Width,management.Height);using var g=Graphics.FromImage(bitmap);var dc=g.GetHdc();PrintWindow(management.Handle,dc,2);g.ReleaseHdc(dc);bitmap.Save(Path.Combine(root,"server-menu-native.png"));management.Close();}
            using var cardHost=new WarmWindow{Size=new Size(1000,560),Opacity=0,ShowInTaskbar=false};var card=new ReleaseHistoryCard("v12.13 · 서버 폴더 새로고침과 설정 디자인",string.Concat(Enumerable.Repeat("서버 이름·설정 입력칸 정렬과 잘림 수정. 크림·코랄 스타일로 통일했습니다. ",4)),Ui.Action("다운로드 · 설치",()=>{},true)){Location=new Point(16,90),Width=940};cardHost.Controls.Add(card);cardHost.Show();WarmTheme.Apply(cardHost);Application.DoEvents();
            foreach(int width in new[]{600,940,1400}){card.Width=width;card.PerformLayout();var action=card.Controls.OfType<Button>().Single();Check(card.Controls.OfType<Label>().All(l=>l.Right<action.Left&&card.ClientRectangle.Contains(l.Bounds)),"release notes stay clear of button at width "+width);}
            card.Width=940;card.PerformLayout();using(var bitmap=new Bitmap(cardHost.Width,cardHost.Height)){using var g=Graphics.FromImage(bitmap);var dc=g.GetHdc();PrintWindow(cardHost.Handle,dc,2);g.ReleaseHdc(dc);bitmap.Save(Path.Combine(root,"release-card-native.png"));}cardHost.Close();Console.WriteLine($"ALL {passed} POPULAR CHECKS PASSED");return;
        }
        if(args.Contains("--settings-style")){
            using var wizard=new CreateServerWizard();var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(CreateServerWizard).GetField("step",flags)!.SetValue(wizard,3);typeof(CreateServerWizard).GetMethod("Render",flags)!.Invoke(wizard,null);
            Preview(wizard,"settings-style");
            var name=(WarmTextBox)typeof(CreateServerWizard).GetField("name",flags)!.GetValue(wizard)!;
            var number=AllControls(wizard).OfType<WarmNumber>().First(n=>n.Visible);
            Check(name.PointToScreen(Point.Empty).X==number.PointToScreen(Point.Empty).X,"name and property inputs align");
            Check(name.Height>=40,"name input has full height");
            var spin=AllControls(number).OfType<NumericUpDown>().Single();decimal value=number.Value;spin.UpButton();Check(number.Value==value+1,"numeric increase works");spin.DownButton();Check(number.Value==value,"numeric decrease works");number.Value=number.Maximum;spin.UpButton();Check(number.Value==number.Maximum,"numeric maximum respected");number.Value=value;
            Check(spin.Right>spin.Parent!.Width,"native spinner clipped from view");
            Check(AllControls(number).OfType<NumberStep>().Count()==2,"custom arrows present");
            foreach(var c in AllControls(wizard).OfType<WarmTextBox>().Where(c=>c.Visible)){var text=AllControls(c).OfType<TextBox>().Single();Check(c.ClientRectangle.Contains(text.Bounds),"text fits input height");}
            using(var bitmap=new Bitmap(wizard.Width,wizard.Height)){using var g=Graphics.FromImage(bitmap);var dc=g.GetHdc();PrintWindow(wizard.Handle,dc,2);g.ReleaseHdc(dc);bitmap.Save(Path.Combine(root,"settings-style-native.png"));}
            typeof(CreateServerWizard).GetField("step",flags)!.SetValue(wizard,4);typeof(CreateServerWizard).GetMethod("Render",flags)!.Invoke(wizard,null);
            typeof(CreateServerWizard).GetField("step",flags)!.SetValue(wizard,3);typeof(CreateServerWizard).GetMethod("Render",flags)!.Invoke(wizard,null);Application.DoEvents();Check(name.Visible&&!name.IsDisposed,"name retained after step navigation");wizard.Close();return;
        }
        if(args.Contains("--discovery-live")){
            string source=args[Array.IndexOf(args,"--discovery-live")+1];var result=ServerDiscovery.Scan(source,[]);Console.WriteLine(string.Join("\n",result.Servers.Select(p=>$"{p.Name} | {p.Kind} | {p.MinecraftVersion}")));Console.WriteLine("Skipped: "+string.Join(", ",result.Skipped));Check(result.Servers.Any(p=>p.Name=="Paper 26.2"&&p.Kind=="Paper"),"actual Paper jar detected without registering or changing server files");return;
        }
        if(args.Contains("--discovery")){
            string servers=Path.Combine(root,"servers");new AppSettings{ServerRoot=servers,BackupRoot=Path.Combine(root,"backups")}.Save();
            void Put(string relative,string text){string path=Path.Combine(servers,relative);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,text);}
            var existing=new Profile{Name="기존 서버",Folder=Path.Combine(servers,"Existing"),Kind="Paper",MinecraftVersion="1.21.1",ManagedInstall=true,LaunchJar="server.jar"};
            Put("Existing/server.jar","existing");Put("Fabric Test/server.jar","fixture");Put("Fabric Test/server.properties","server-port=25570");
            Put("Fabric Test/manager-launch.json",System.Text.Json.JsonSerializer.Serialize(new Profile{Id=existing.Id,Name="old name",Kind="Fabric",MinecraftVersion="1.21.1",ManagedInstall=true,LaunchJar="server.jar",AutoRestart=true,AutoBackup=true}));
            Put("Forge Mod Server/libraries/net/minecraftforge/forge/1.21.1-52.0.0/win_args.txt","fixture");Put("unrelated/readme.txt","keep");Put("broken/manager-launch.json","{");
            var before=Directory.GetFiles(servers,"*",SearchOption.AllDirectories).ToDictionary(p=>p,File.ReadAllText);
            var result=ServerDiscovery.Scan(servers,[existing]);Check(result.Servers.Count==2&&result.Existing==1,"finds two new servers and excludes registered path");
            Check(result.Servers.Single(p=>p.Kind=="Fabric").Name=="Fabric Test"&&result.Servers.Single(p=>p.Kind=="Fabric").Port==25570,"folder name and actual port detected");
            Check(result.Servers.All(p=>p.Id!=existing.Id&&!p.AutoRestart&&!p.AutoBackup),"new unique profile identity without automatic execution");
            Check(result.Servers.Single(p=>p.Kind=="Forge").MinecraftVersion=="1.21.1","Forge launch version detected");Check(result.Skipped.Count==2,"unrecognized and malformed folders reported");
            Storage.Save([existing]);var added=ServerDiscovery.Register(result);Check(added.Count==2&&Storage.Load().Count==3,"registration preserves existing profile");Check(ServerDiscovery.Register(result).Count==0,"repeat registration is idempotent");
            Check(ServerDiscovery.Scan(servers,Storage.Load()).Servers.Count==0,"refresh does not duplicate registered servers");
            Check(before.All(p=>File.Exists(p.Key)&&File.ReadAllText(p.Key)==p.Value)&&Directory.GetFiles(servers,"*",SearchOption.AllDirectories).Length==before.Count,"server files remain unchanged with no copies");
            Put("unsafe/manager-launch.json",System.Text.Json.JsonSerializer.Serialize(new Profile{ManagedInstall=true,LaunchJar="../Existing/server.jar"}));Check(ServerDiscovery.Scan(servers,Storage.Load()).Servers.Count==0,"launch paths outside server are rejected");
            Throws(()=>ServerDiscovery.Scan(Path.Combine(root,"absent"),Storage.Load()),"missing server root reports error");
            Storage.Save([existing]);using(var form=new HomeForm(false)){form.Opacity=0;form.ShowInTaskbar=false;form.Show();var task=form.DiscoverServers();while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(10);}task.GetAwaiter().GetResult();Preview(form,"server-discovery");using(var actual=new Bitmap(form.Width,form.Height)){using var graphics=Graphics.FromImage(actual);var dc=graphics.GetHdc();try{PrintWindow(form.Handle,dc,2);}finally{graphics.ReleaseHdc(dc);}actual.Save(Path.Combine(root,"server-discovery-native.png"));}Check(AllControls(form).OfType<HomeServerCard>().Count(c=>c.NewlyRegistered)==2,"new server badges shown");Check(AllControls(form).Any(c=>c.Text=="서버 2개를 새로 등록했습니다"&&c.Visible),"completion toast shown");form.Close();}
            Console.WriteLine($"ALL {passed} DISCOVERY CHECKS PASSED");return;
        }
        if(args.Contains("--branding-repair")){
            string source=args[Array.IndexOf(args,"--branding-repair")+1];var files=new[]{"kotlinforforge*","twilightforest*","sophisticatedcore*"}.Select(pattern=>Directory.GetFiles(source,pattern).Single()).ToArray();using var art=new ModArtwork();var items=new List<CatalogItem>();foreach(var file in files){var icon=ModFiles.Icon(file);if(file.Contains("sophisticatedcore")){var project=InstalledExtensions.ResolveArtwork(file).GetAwaiter().GetResult();Check(project?.Slug=="sophisticated-core","missing Sophisticated logo resolves verified project");var load=art.Load(project!,()=>{});while(!load.IsCompleted){Application.DoEvents();Thread.Sleep(20);}load.GetAwaiter().GetResult();icon.Dispose();icon=new Bitmap(art.Find(project!.Id)!);}else Check(icon.Width==96,"declared or nested JAR logo decoded: "+Path.GetFileName(file));var meta=ModFiles.Metadata(file);items.Add(new(Path.GetFileName(file),meta.Name,"설치된 모드","활성",icon));}
            using(var form=new WarmWindow{Size=new(1160,640)}){var view=new CatalogView("모드 · 플러그인 관리");form.Controls.Add(view);view.SetItems(items);WarmTheme.Apply(form);Preview(form,"repaired-mod-icons");form.Close();}foreach(var item in items)item.Picture?.Dispose();
            using(var form=new UpdateConfirmationWindow(new AppRelease("1.12.12","v12.12 · 모드 이미지와 설치 화면 개선","모드 대표 이미지 누락 수정, 설치 화면의 글씨 잘림·겹침 개선, 크림·코랄 확인창 적용.","test-file-123",new string('A',64),100))){Preview(form,"update-confirmation");var close=AllControls(form).OfType<Button>().Single(b=>b.Name=="UpdateCloseButton");Check(close.Width==40&&close.Parent!.ClientRectangle.Contains(close.Bounds),"close button fits fully inside confirmation");close.PerformClick();Check(form.DialogResult==DialogResult.Cancel,"close button cancels confirmation");form.Close();}return;
        }
        if(args.Contains("--updater-repair")){
            using(var page=new UpdatesPage(()=>false,(_,_)=>Task.CompletedTask)){Check(!AllControls(page).OfType<Button>().Any(b=>b.Text=="다운로드 취소"),"update list has no cancel button");}
            using var owner=new WarmWindow{Opacity=0,ShowInTaskbar=false};owner.Show();using(var progress=new UpdateProgressWindow(()=>{})){progress.Opacity=0;progress.ShowInTaskbar=false;bool disabled=false;progress.Shown+=(_,_)=>{disabled=!IsWindowEnabled(owner.Handle);progress.BeginInvoke(new Action(progress.Finish));};progress.ShowDialog(owner);Check(disabled&&IsWindowEnabled(owner.Handle),"modal progress blocks owner and restores it after close");Check(AllControls(progress).OfType<Button>().Count(b=>b.Text=="다운로드 취소")==1,"cancel exists only in progress dialog");}owner.Close();
            var command=AppUpdates.InstallCommand(Path.Combine(root,"unused.exe"),new string('A',64));Check(command.WorkingDirectory.EndsWith("WindowsPowerShell\\v1.0"),"helper starts outside bundled application DLL directory");return;
        }
        if(args.Contains("--loader-labels")){using var form=new CreateServerWizard();Preview(form,"loader-labels");form.Close();return;}
        if(args.Contains("--world-import")){
            void World(string path,int version,string marker){Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,"marker.txt"),marker);using var f=File.Create(Path.Combine(path,"level.dat"));using var z=new GZipStream(f,CompressionMode.Compress);using var w=new BinaryWriter(z);void Str(string v){var b=System.Text.Encoding.UTF8.GetBytes(v);w.Write((byte)(b.Length>>8));w.Write((byte)b.Length);w.Write(b);}w.Write((byte)10);Str("");w.Write((byte)10);Str("Data");w.Write((byte)3);Str("DataVersion");w.Write(System.Net.IPAddress.HostToNetworkOrder(version));w.Write((byte)0);w.Write((byte)0);}
            string server=Path.Combine(root,"world-server"),incoming=Path.Combine(root,"incoming"),backups=Path.Combine(root,"backups");Directory.CreateDirectory(server);File.WriteAllText(Path.Combine(server,"server.properties"),"level-name=우리맵");File.WriteAllText(Path.Combine(server,"server.jar"),"keep");World(Path.Combine(server,"우리맵"),4000,"old");World(Path.Combine(server,"우리맵_nether"),4000,"old-nether");File.WriteAllText(Path.Combine(server,"우리맵","session.lock"),"lock");World(incoming,4000,"new");Directory.CreateDirectory(Path.Combine(incoming,"dimensions/mod/moon"));File.WriteAllText(Path.Combine(incoming,"dimensions/mod/moon/test"),"dimension");
            var progress=new Progress<string>();var result=WorldImportService.ApplyAsync(server,incoming,backups,progress).GetAwaiter().GetResult();
            Check(File.ReadAllText(Path.Combine(server,"우리맵/marker.txt"))=="new","new world replaces configured world name");Check(File.ReadAllText(Path.Combine(server,"server.jar"))=="keep","server files preserved");Check(File.Exists(Path.Combine(server,"우리맵/dimensions/mod/moon/test")),"mod dimensions copied");Check(!Directory.Exists(Path.Combine(server,"우리맵_nether")),"old separate dimension removed when absent in new world");Check(File.ReadAllText(Path.Combine(incoming,"marker.txt"))=="new","selected original map unchanged");using(var zip=ZipFile.OpenRead(result.BackupPath!)){Check(zip.GetEntry("우리맵/marker.txt")!=null&&zip.GetEntry("우리맵_nether/marker.txt")!=null&&zip.GetEntry("server.jar")==null,"backup contains all old worlds only");}
            World(incoming,5000,"newer");Throws(()=>WorldImportService.ApplyAsync(server,incoming,null,progress).GetAwaiter().GetResult(),"newer data version rejected");Check(File.ReadAllText(Path.Combine(server,"우리맵/marker.txt"))=="new","validation failure preserves current world");World(incoming,4000,"zip");string archive=Path.Combine(root,"map.zip");ZipFile.CreateFromDirectory(incoming,archive);WorldImportService.ApplyAsync(server,archive,null,progress).GetAwaiter().GetResult();Check(File.ReadAllText(Path.Combine(server,"우리맵/marker.txt"))=="zip","zip world import succeeds");
            using(var held=new FileStream(Path.Combine(server,".manager.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))Throws(()=>WorldImportService.ApplyAsync(server,incoming,null,progress).GetAwaiter().GetResult(),"running manager lock blocks map replacement");
            string badBackup=Path.Combine(root,"not-a-directory");File.WriteAllText(badBackup,"file");Throws(()=>WorldImportService.ApplyAsync(server,incoming,badBackup,progress).GetAwaiter().GetResult(),"backup failure blocks replacement");Check(File.ReadAllText(Path.Combine(server,"우리맵/marker.txt"))=="zip","failed backup retains current world");
            World(Path.Combine(server,"우리맵_nether"),4000,"rollback-nether");using(var held=new FileStream(Path.Combine(server,"우리맵_nether/marker.txt"),FileMode.Open,FileAccess.Read,FileShare.None)){Throws(()=>WorldImportService.ApplyAsync(server,incoming,null,progress).GetAwaiter().GetResult(),"locked second dimension triggers transactional rollback");Check(File.ReadAllText(Path.Combine(server,"우리맵/marker.txt"))=="zip","first world restored after partial rename failure");}
            string fresh=Path.Combine(root,"fresh-server");Directory.CreateDirectory(fresh);WorldImportService.ApplyAsync(fresh,archive,backups,progress).GetAwaiter().GetResult();Check(File.Exists(Path.Combine(fresh,"world/level.dat")),"map can initialize a server without generated world");
            Directory.CreateDirectory(Path.Combine(server,".world-import-interrupted/previous"));Throws(()=>WorldImportService.EnsureNoPending(server),"interrupted replacement blocks server restart");
            using(var form=new WorldManagementWindow(new Profile{Name="테스트 서버",Folder=server})){Preview(form,"world-management");form.Close();}using(var form=new WorldImportConfirmation(backups)){Preview(form,"world-confirmation");form.Close();}return;
        }
        if(args.Contains("--extension-live")){
            var p=new Profile{Folder=Path.Combine(root,"extensions"),Kind="Paper",MinecraftVersion="1.21.1"};Directory.CreateDirectory(Path.Combine(p.Folder,"plugins"));using var net=new DownloadClient("");var service=new ModPluginService(net);var project=(service.Search(p,"WorldEdit").GetAwaiter().GetResult()).First(r=>r.Slug=="worldedit");service.Install(p,project,new Progress<string>()).GetAwaiter().GetResult();var file=InstalledExtensions.Files(p).Single();Check(InstalledExtensions.Resolve(file).GetAwaiter().GetResult()?.Id==project.Id,"installed download resolves project");File.Move(file,file+".disabled");Check(InstalledExtensions.ProjectIds(p).GetAwaiter().GetResult().Contains(project.Id),"disabled renamed file remains installed");Throws(()=>service.Install(p,project,new Progress<string>()).GetAwaiter().GetResult(),"duplicate project install rejected");
            foreach(var cache in Directory.GetFiles(Path.Combine(Storage.Root,"extension-metadata")))File.Delete(cache);Check(InstalledExtensions.Resolve(file+".disabled").GetAwaiter().GetResult()?.IconUrl==project.IconUrl,"existing manual JAR resolves official project icon by hash");using var art=new ModArtwork();var task=art.Load(project,()=>{});while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(20);}task.GetAwaiter().GetResult();Check(art.Find(project.Id)!=null,"actual WorldEdit representative image decoded");using(var form=new ExtensionSearchForm(p)){form.Opacity=0;form.ShowInTaskbar=false;form.Show();var query=AllControls(form).OfType<WarmTextBox>().First(t=>t.PlaceholderText.Contains("이름 검색"));query.Text="WorldEdit";AllControls(form).OfType<Button>().First(b=>b.Text=="검색").PerformClick();var until=DateTime.UtcNow.AddSeconds(40);while(DateTime.UtcNow<until&&!AllControls(form).OfType<Button>().Any(b=>b.Text=="설치됨")){Application.DoEvents();Thread.Sleep(30);}Check(AllControls(form).OfType<Button>().Any(b=>b.Text=="설치됨"&&!b.Enabled),"online UI shows installed and disables install");Preview(form,"extension-search");while(AllControls(form).OfType<Button>().Any(b=>b.Text=="검색"&&!b.Enabled)&&DateTime.UtcNow<until){Application.DoEvents();Thread.Sleep(30);}form.Close();}
            Storage.Save([p]);using(var manager=new MainForm(false,p)){manager.Opacity=0;manager.ShowInTaskbar=false;manager.Show();var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;typeof(MainForm).GetMethod("Apply",flags)!.Invoke(manager,[p]);var view=((Dictionary<string,CatalogView>)typeof(MainForm).GetField("catalogs",flags)!.GetValue(manager)!)["mods"];var field=typeof(CatalogView).GetField("items",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;var until=DateTime.UtcNow.AddSeconds(20);while(DateTime.UtcNow<until&&!((List<CatalogItem>)field.GetValue(view)!).Any(i=>i.Picture?.Width==96)){Application.DoEvents();Thread.Sleep(30);}Check(((List<CatalogItem>)field.GetValue(view)!).Any(i=>i.Name=="WorldEdit"&&i.Picture?.Width==96),"installed catalog receives actual representative icon");if(view.Parent is TabPage page&&page.Parent is TabControl tabs)tabs.SelectedTab=page;Preview(manager,"installed-plugin-icon");view.ShowMode(true);Preview(manager,"installed-plugin-details");manager.Close();}return;
        }
        if(args.Contains("--inventory-readonly")){
            string server=args[Array.IndexOf(args,"--inventory-readonly")+1];var world=BackupService.WorldFolders(server)[0];var files=Directory.GetFiles(Path.Combine(world,"playerdata"),"*.dat").Append(Path.Combine(world,"data","sophisticatedbackpacks.dat")).ToArray();var hashes=files.ToDictionary(f=>f,f=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(f))));
            var snapshots=files.Where(f=>Path.GetDirectoryName(f)!.EndsWith("playerdata")).Select(f=>PlayerSnapshot.Read(server,Path.GetFileNameWithoutExtension(f))).ToArray();
            var bags=snapshots.SelectMany(s=>s.Inventory.Concat(s.EquippedContainers)).Where(i=>i.Id.StartsWith("sophisticatedbackpacks:")).ToArray();Check(bags.Length>0,"equipped and inventory backpacks detected from saved data");foreach(var bag in bags){var contents=ItemContainers.Read(server,bag);Check(contents.Supported,"Sophisticated backpack UUID resolves to saved contents");}
            using(var art=new ItemArtwork(server)){Check(art.Load("minecraft:diamond")!=null,"installed game item texture resolves");Check(art.Load("minecraft:diamond_sword")!=null,"installed item model texture resolves");}
            using(var form=new WarmWindow{Size=new(1000,780)}){var view=new InventoryView(server,snapshots.First(s=>s.Inventory.Count>0)){Dock=DockStyle.Fill};form.Controls.Add(view);form.Opacity=0;form.ShowInTaskbar=false;form.Show();for(int n=0;n<150;n++){Application.DoEvents();Thread.Sleep(30);}Preview(form,"inventory-cards");Check(AllControls(view).OfType<InventoryItemCard>().Any(c=>c.Icon!=null),"inventory cards render actual item icons");form.Close();}
            foreach(var f in files)Check(hashes[f]==Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(f))),"player and backpack source unchanged");return;
        }
        if(args.Contains("--update-ui")){
            var enchanted=new StoredItem{Id="minecraft:diamond_sword",Count=1,Data=new(){["components"]=new Dictionary<string,object>{["minecraft:enchantments"]=new Dictionary<string,object>{["levels"]=new Dictionary<string,object>{["minecraft:sharpness"]=5,["minecraft:unbreaking"]=3,["example:magic"]=2}}}}};
            Check(ItemEnchantments.Read(enchanted).Count==3&&ItemEnchantments.Read(enchanted).Any(e=>e.Display=="날카로움 5"),"modern enchantments and mod IDs decoded");
            var book=new StoredItem{Id="minecraft:enchanted_book",Data=new(){["tag"]=new Dictionary<string,object>{["StoredEnchantments"]=new List<object>{new Dictionary<string,object>{["id"]="minecraft:mending",["lvl"]=(short)1}}}}};Check(ItemEnchantments.Read(book).Single().Display=="수선 1","legacy enchanted book decoded");
            using(var form=new UpdateProgressWindow(()=>{})){Preview(form,"update-connecting");form.DownloadPercent(42);Preview(form,"update-downloading");form.Stage("설치 파일을 검증하고 있어요");Preview(form,"update-verifying");form.Finish();}
            using(var form=new WarmWindow{Size=new(620,600)}){form.Controls.Add(new InventoryView(root,new PlayerSnapshot{Inventory=[enchanted,book]}){Dock=DockStyle.Fill});Preview(form,"item-enchantments");form.Close();}return;
        }
        if(args.Contains("--skin-live")){
            var task=PlayerSkins.Load("069a79f4-44e9-4726-a5be-fca90e38aaf5");while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(20);}using var image=task.GetAwaiter().GetResult();Check(image!=null&&image.Width==64,"official full skin downloaded");using var viewer=new PlayerSkinView{Skin=image,Size=new(300,430)};using var front=new Bitmap(300,430);viewer.Angle=0;viewer.DrawToBitmap(front,viewer.ClientRectangle);front.Save(Path.Combine(root,"skin-front.png"));using var back=new Bitmap(300,430);viewer.Angle=MathF.PI;viewer.DrawToBitmap(back,viewer.ClientRectangle);back.Save(Path.Combine(root,"skin-back.png"));Check(!File.ReadAllBytes(Path.Combine(root,"skin-front.png")).SequenceEqual(File.ReadAllBytes(Path.Combine(root,"skin-back.png"))),"front and back use different skin faces");return;
        }
        if(args.Contains("--player-history"))
        {
            string dir=Path.Combine(root,"history-server"),world=Path.Combine(dir,"world"),id="01234567-89ab-cdef-0123-456789abcdef";Directory.CreateDirectory(Path.Combine(world,"playerdata"));File.WriteAllText(Path.Combine(world,"level.dat"),"fixture");File.WriteAllText(Path.Combine(dir,"server.properties"),"level-name=world");File.WriteAllText(Path.Combine(dir,"usercache.json"),"[{\"name\":\"TestPlayer\",\"uuid\":\""+id+"\"}]");
            string dat=Path.Combine(world,"playerdata",id+".dat");
            using(var f=File.Create(dat))using(var zip=new GZipStream(f,CompressionMode.Compress))using(var w=new BinaryWriter(zip)){
                void I(int n)=>w.Write(System.Net.IPAddress.HostToNetworkOrder(n));void S(string t){var bytes=System.Text.Encoding.UTF8.GetBytes(t);w.Write(System.Net.IPAddress.HostToNetworkOrder((short)bytes.Length));w.Write(bytes);}void Tag(byte t,string key){w.Write(t);S(key);}
                Tag(10,"");Tag(5,"Health");I(BitConverter.SingleToInt32Bits(17.5f));Tag(3,"foodLevel");I(13);Tag(3,"XpLevel");I(18);Tag(3,"playerGameType");I(0);Tag(8,"Dimension");S("minecraft:overworld");Tag(9,"Pos");w.Write((byte)6);I(3);foreach(double n in new[]{128d,64d,-320d})w.Write(System.Net.IPAddress.HostToNetworkOrder(BitConverter.DoubleToInt64Bits(n)));Tag(9,"Inventory");w.Write((byte)10);I(1);Tag(1,"Slot");w.Write((byte)0);Tag(8,"id");S("minecraft:diamond");Tag(3,"count");I(5);w.Write((byte)0);w.Write((byte)0);
            }
            byte[] original=File.ReadAllBytes(dat);var snapshot=PlayerSnapshot.Read(dir,id);Check(snapshot.Health==17.5&&snapshot.Food==13&&snapshot.Level==18,"saved health hunger and experience read from NBT");Check(snapshot.Position.Contains("128.0")&&snapshot.Inventory.Single().Contains("diamond"),"position and modern inventory count parsed");Check(original.SequenceEqual(File.ReadAllBytes(dat)),"player data remains unchanged");Check(PlayerSnapshot.Read(dir,"../bad").Health==null,"invalid UUID cannot become file path");
            var now=DateTime.UtcNow;var store=new PlayerHistoryStore(dir);store.Import(dir,now);Check(store.Records.Count==1&&store.Records[0].LastJoined==null,"usercache import does not invent last login");store.Observe(["TestPlayer"],now);store.Observe(["TestPlayer"],now.AddSeconds(30));store.Observe([],now.AddSeconds(45));Check(store.Records[0].ObservedSeconds==45&&store.Records[0].Visits.Single().End==now.AddSeconds(45),"observed session duration and logout persisted");var reloaded=new PlayerHistoryStore(dir);reloaded.Import(dir,now);Check(reloaded.Records.Count==1&&reloaded.Records[0].ObservedSeconds==45,"restart retains records without duplicates");reloaded.Observe([],now.AddHours(3));Check(reloaded.Records[0].ObservedSeconds==45,"offline manager time is not added");
            string cache=Path.Combine(Storage.Root,"skins");Directory.CreateDirectory(cache);using(var skin=new Bitmap(64,64)){using(var g=Graphics.FromImage(skin)){g.Clear(Color.Transparent);g.FillRectangle(Brushes.Peru,0,0,32,16);g.FillRectangle(Brushes.Teal,16,16,24,16);g.FillRectangle(Brushes.Peru,40,16,16,16);g.FillRectangle(Brushes.Peru,32,48,16,16);g.FillRectangle(Brushes.SlateGray,0,16,16,16);g.FillRectangle(Brushes.SlateGray,16,48,16,16);}skin.Save(Path.Combine(cache,id+".png"));}
            demo.Id=Guid.NewGuid().ToString("N");demo.Folder=dir;demo.Name="접속 기록 테스트";using(var form=new MainForm(false)){form.InitializeForHome(demo);form.Opacity=0;form.ShowInTaskbar=false;form.Show();var deck=AllControls(form).OfType<PageDeck>().Single();deck.SelectedTab=deck.TabPages.Cast<TabPage>().First(p=>p.Text=="접속자");var page=AllControls(form).OfType<PlayerHistoryPage>().Single();page.SetData(dir,reloaded.Records,[]);for(int i=0;i<30;i++){Application.DoEvents();Thread.Sleep(50);}Preview(form,"player-history");AllControls(page).OfType<WarmButton>().Single(b=>b.Text.StartsWith("오프라인 (")).PerformClick();AllControls(page).OfType<WarmButton>().Single(b=>b.Text.StartsWith("전체 (")).PerformClick();Check(AllControls(page).OfType<WarmButton>().Count(b=>b.Primary)==1,"history filter has one selected button");AllControls(page).OfType<WarmButton>().Single(b=>b.Text.StartsWith("현재 접속")).PerformClick();Check(!AllControls(page).OfType<WarmButton>().Single(b=>b.Text.StartsWith("오프라인 (")).Visible,"current tab hides offline filter");AllControls(page).OfType<WarmButton>().Single(b=>b.Text.StartsWith("접속 기록")).PerformClick();for(int i=0;i<30;i++){Application.DoEvents();Thread.Sleep(20);}Check(AllControls(page).OfType<PlayerSkinView>().Single().Skin!=null,"full skin loaded from cache");Check(AllControls(page).OfType<VitalMeter>().Count()==2,"health and hunger remain visible with full skin");Check(AllControls(page).OfType<WarmButton>().All(b=>b.Parent!.ClientRectangle.Contains(b.Bounds)),"player actions remain inside their panels");
            AllControls(page).OfType<WarmButton>().Single(b=>b.Text=="인벤토리").PerformClick();for(int i=0;i<50;i++){Application.DoEvents();Thread.Sleep(20);}Check(AllControls(page).OfType<InventoryItemCard>().Any(c=>c.Item.Id=="minecraft:diamond"),"inventory tab shows saved items");var retained=AllControls(page).OfType<InventoryView>().Single();for(int tick=0;tick<3;tick++){page.SetData(dir,reloaded.Records,tick==1?["TestPlayer"]:[]);Application.DoEvents();}Check(!retained.IsDisposed&&ReferenceEquals(retained,AllControls(page).OfType<InventoryView>().Single()),"presence refresh preserves the same inventory and container navigation");typeof(Control).GetMethod("OnClick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(AllControls(page).OfType<PlayerTile>().First(),new object[]{EventArgs.Empty});for(int tick=0;tick<30;tick++){Application.DoEvents();Thread.Sleep(20);}Check(retained.IsDisposed,"clicking the same player explicitly reloads inventory");var refreshed=AllControls(page).OfType<InventoryView>().Single();AllControls(page).OfType<WarmButton>().Single(b=>b.Text=="기록 새로고침").PerformClick();for(int tick=0;tick<30;tick++){Application.DoEvents();Thread.Sleep(20);}Check(refreshed.IsDisposed,"record refresh explicitly reloads inventory");AllControls(page).OfType<WarmButton>().Single(b=>b.Text=="접속 이력").PerformClick();for(int i=0;i<50;i++){Application.DoEvents();Thread.Sleep(20);}Check(AllControls(page).OfType<RichTextBox>().Single().Text.Contains("접속 확인"),"history tab shows recorded visits");AllControls(page).OfType<WarmTextBox>().Single().Text="no-match";Application.DoEvents();Check(!AllControls(page).OfType<PlayerTile>().Any(),"player search filters without changing saved records");Check(AllControls(form).OfType<Label>().First(l=>l.Text.Contains("가동 시간")).TextAlign==ContentAlignment.MiddleLeft,"server status strip vertically centered");form.HomeHosted=false;form.Close();}
            using(var home=new HomeForm()){home.Opacity=0;home.ShowInTaskbar=false;home.Show();var b=AllControls(home).OfType<WindowCaptionButton>().Single(b=>b.AccessibleName=="최소화");Check(b.Visible&&b.Parent!.ClientRectangle.Contains(b.Bounds),"minimize glyph button visible and inside title bar");b.PerformClick();Application.DoEvents();Check(home.WindowState==FormWindowState.Minimized,"minimize button works");home.Close();}
            var unknown=new StoredItem{Id="example:backpack",Count=1};Check(ItemContainers.IsContainer(unknown)&&!ItemContainers.Read(dir,unknown).Supported,"unknown mod container is not reported as empty");
            var nested=new StoredItem{Id="minecraft:shulker_box",Count=1,Data=new(){["components"]=new Dictionary<string,object>{["minecraft:container"]=new List<object>{new Dictionary<string,object>{["slot"]=2,["item"]=new Dictionary<string,object>{["id"]="minecraft:diamond",["count"]=7}}}}}};Check(ItemContainers.Read(dir,nested).Items.Single().Count==7,"modern container slot wrapper decoded");
            File.WriteAllBytes(dat,[1,2,3]);Check(PlayerSnapshot.Read(dir,id).Error.Length>0,"corrupt NBT reports error without crashing");Console.WriteLine($"ALL {passed} PLAYER HISTORY CHECKS PASSED");return;
        }
        if(args.Contains("--text-layout"))
        {
            var profiles=Storage.Load();profiles[0].Name="CreateMod Neo Forge Bukkit";Storage.Save(profiles);
            using var home=new HomeForm();
            foreach(int width in new[]{1536,1280}){
                home.Size=new Size(width,1024);Preview(home,"home-text-"+width);
                var viewport=(Panel)AllControls(home).OfType<HomeServerCard>().First().Parent!;Check(!viewport.HorizontalScroll.Visible,"no horizontal scrolling at "+width);
                var brand=AllControls(home).OfType<Label>().Single(c=>c.Text=="마인크래프트 서버 매니저");
                var subtitle=AllControls(home).OfType<Label>().Single(c=>c.Text=="Minecraft Server Manager");
                Check(!brand.Bounds.IntersectsWith(subtitle.Bounds),"brand title and subtitle do not overlap at "+width);
                Check(AllControls(home).OfType<HomeServerCard>().All(c=>c.Controls.OfType<WarmButton>().Where(b=>b.Visible).All(b=>c.ClientRectangle.Contains(b.Bounds))),"card buttons remain contained at "+width);
            }
            using var fitted=FitText.FontFor("설정",new Font("Noto Sans KR",16),new Size(36,34));Check(fitted.Size<16,"short narrow buttons fit readable text");
            home.Close();Console.WriteLine($"ALL {passed} TEXT LAYOUT CHECKS PASSED");return;
        }
        if(args.Contains("--handoff-prepare"))
        {
            string installer=Path.GetFullPath(args[3]), directory=Path.GetFullPath(args[4]);
            string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(installer)));
            var command=AppUpdates.InstallCommand(installer,hash,directory);
            string script=command.ArgumentList.Last();File.WriteAllText(Path.Combine(root,"helper-path.txt"),script);
            Check(File.ReadAllText(script).Contains("/VERYSILENT")&&File.ReadAllText(script).Contains("Remove-Item -LiteralPath $taskInstaller"),"helper configured for unattended install and cleanup");return;
        }
        if(args.Contains("--world-backup"))
        {
            string source=Path.Combine(root,"world-source"), dest=Path.Combine(root,"world-zips");Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source,"server.properties"),"level-name=우리맵");
            foreach(string item in new[]{"우리맵/level.dat","우리맵/playerdata/player.dat","우리맵/DIM-1/region/a.mca","우리맵/dimensions/mod/moon/region/a.mca","우리맵/session.lock","우리맵_nether/DIM-1/region/b.mca","우리맵_the_end/DIM1/region/c.mca","mods/large.jar","plugins/large.jar","logs/latest.log","old-world/level.dat","server.jar"})
            {string f=Path.Combine(source,item);Directory.CreateDirectory(Path.GetDirectoryName(f)!);File.WriteAllText(f,item);}
            string archive=BackupService.CreateAsync(source,dest,new Progress<string>()).GetAwaiter().GetResult();
            using(var z=ZipFile.OpenRead(archive)){
                Check(z.Entries.Count==6,"only six world data files archived");
                Check(z.GetEntry("우리맵/playerdata/player.dat")!=null&&z.GetEntry("우리맵/dimensions/mod/moon/region/a.mca")!=null,"player data and mod dimensions preserved");
                Check(z.GetEntry("우리맵_nether/DIM-1/region/b.mca")!=null&&z.GetEntry("우리맵_the_end/DIM1/region/c.mca")!=null,"separate Nether and End preserved");
                Check(!z.Entries.Any(e=>e.FullName.EndsWith("session.lock")||e.FullName.StartsWith("mods/")||e.FullName=="server.properties"),"locks and server software excluded");
            }
            Check(BackupService.IsWorldOnlyArchive(archive),"world archive recognized without creating server profile");
            string restored=Path.Combine(root,"restored-world");BackupService.RestoreAsync(archive,restored).GetAwaiter().GetResult();Check(File.ReadAllText(Path.Combine(restored,"우리맵","level.dat"))=="우리맵/level.dat","world archive restore round trip");
            File.WriteAllText(Path.Combine(source,"server.properties"),@"level-name=\uC6B0\uB9AC\uB9F5");Check(BackupService.WorldFolders(source).Length==3,"escaped Korean world name supported");
            File.WriteAllText(Path.Combine(source,"server.properties"),"level-name=../outside");Throws(()=>BackupService.WorldFolders(source),"outside world path rejected");
            File.WriteAllText(Path.Combine(source,"server.properties"),"level-name=missing");Throws(()=>BackupService.CreateAsync(source,dest,new Progress<string>()).GetAwaiter().GetResult(),"missing world rejects empty backup");
            File.WriteAllText(Path.Combine(source,"server.properties"),"level-name=mods");Throws(()=>BackupService.WorldFolders(source),"non-world directory rejected");
            Check(Directory.GetFiles(dest,"*.partial").Length==0&&Directory.GetFiles(dest,"*.zip").Length==1,"failed backups leave no partial or empty ZIP");
            Console.WriteLine($"ALL {passed} WORLD BACKUP CHECKS PASSED");return;
        }
        if(args.Contains("--updates"))
        {
            using var rsa=System.Security.Cryptography.RSA.Create(2048);
            string Sign(AppRelease[] items){var payload=System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new ReleaseFeed(1,items));return System.Text.Json.JsonSerializer.Serialize(new SignedFeed(Convert.ToBase64String(payload),Convert.ToBase64String(rsa.SignData(payload,System.Security.Cryptography.HashAlgorithmName.SHA256,System.Security.Cryptography.RSASignaturePadding.Pss))));}
            var items=Enumerable.Range(1,22).Select(i=>new AppRelease("1."+i+".0","검증 버전 "+i,"검증용 데이터","test-file-id-123",new string('A',64),100)).ToArray();
            var text=Sign(items);var verified=AppUpdates.VerifyFeed(text,rsa.ExportSubjectPublicKeyInfoPem());
            Check(verified.Length==20&&verified[0].Version=="1.22.0"&&verified[19].Version=="1.3.0","signed history sorted and capped at twenty");
            using var wrong=System.Security.Cryptography.RSA.Create(2048);Throws(()=>AppUpdates.VerifyFeed(text,wrong.ExportSubjectPublicKeyInfoPem()),"wrong signing key rejected");
            Throws(()=>AppUpdates.VerifyFeed(Sign([items[0] with{Sha256="bad"}]),rsa.ExportSubjectPublicKeyInfoPem()),"invalid hash rejected");
            Throws(()=>AppUpdates.DownloadUrl("../bad"),"invalid file ID rejected");
            Throws(()=>AppUpdates.ConfirmationUrl("<html>quota exceeded</html>",items[0].FileId),"quota page not treated as installer");
            string fake=Path.Combine(root,"bad.exe");File.WriteAllText(fake,"MZbad");Throws(()=>AppUpdates.ValidateFile(fake,items[0],default).GetAwaiter().GetResult(),"incorrect installer rejected");
            using(var page=new UpdatesPage(()=>false,(_,_)=>throw new Exception("must not install"))){Preview(page,"updates-page");page.Render(verified);Application.DoEvents();using var bitmap=new Bitmap(page.Width,page.Height);page.DrawToBitmap(bitmap,page.ClientRectangle);bitmap.Save(Path.Combine(root,"updates-history.png"));Check(AllControls(page).OfType<SurfaceCard>().Count()==20,"twenty release cards displayed");page.Close();}
            if(args.Contains("--live")){
                var feed=AppUpdates.Check(default);while(!feed.IsCompleted){Application.DoEvents();Thread.Sleep(30);}var live=feed.GetAwaiter().GetResult();Check(live.Length>0,"public signed feed verified without login");
                var download=AppUpdates.Download(args.Contains("--live-latest")?live.First():live.Last(),new Progress<int>(),default);while(!download.IsCompleted){Application.DoEvents();Thread.Sleep(30);}var file=download.GetAwaiter().GetResult();Check(File.Exists(file),"public installer downloaded and hash verified without executing");
            }
            Console.WriteLine($"ALL {passed} UPDATE CHECKS PASSED");return;
        }
        if(args.Contains("--mod-artwork"))
        {
            using var net=new DownloadClient();demo.MinecraftVersion="1.21.1";
            var request=new ModPluginService(net).Search(demo,"create");while(!request.IsCompleted){Application.DoEvents();Thread.Sleep(20);}var hits=request.GetAwaiter().GetResult();
            Check(hits.Length>0&&hits.Any(h=>h.IconUrl!=null),"real Modrinth results include artwork URLs");
            using var artwork=new ModArtwork();var item=hits.First(h=>h.IconUrl!=null);var load=artwork.Load(item,()=>{});while(!load.IsCompleted){Application.DoEvents();Thread.Sleep(20);}load.GetAwaiter().GetResult();
            Check(artwork.Find(item.Id) is {Width:96,Height:96},"real artwork downloaded and resized");
            using var form=new ExtensionSearchForm(demo);form.Show();Application.DoEvents();
            AllControls(form).OfType<WarmTextBox>().First().Text="create";
            var searchInput=AllControls(form).OfType<WarmTextBox>().First().Controls.OfType<TextBox>().Single();var enter=new KeyEventArgs(Keys.Enter);typeof(Control).GetMethod("OnKeyDown",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(searchInput,[enter]);Check(enter.Handled&&enter.SuppressKeyPress,"Enter triggers search without system beep");
            var until=DateTime.UtcNow.AddSeconds(55);while(DateTime.UtcNow<until&&(AllControls(form).OfType<ListBox>().Single().Items.Count==0||!AllControls(form).OfType<WarmButton>().First(b=>b.Text=="검색").Enabled)){Application.DoEvents();Thread.Sleep(30);}
            Check(AllControls(form).OfType<ListBox>().Single().Items.Count>0,"online result cards populated");Preview(form,"online-artwork-v11");form.Close();
            Console.WriteLine($"ALL {passed} ARTWORK CHECKS PASSED");return;
        }
        if(args.Contains("--installer-changes"))
        {
            var parent=Path.Combine(root,"names");Directory.CreateDirectory(parent);
            Check(Path.GetFileName(ServerFolders.Available(parent,"우리 서버"))=="우리 서버","Korean server folder name");
            Check(Path.GetFileName(ServerFolders.Available(parent,"a/b:c"))=="a_b_c","invalid filename characters replaced");
            Check(Path.GetFileName(ServerFolders.Available(parent,"CON"))=="_CON","reserved Windows filename avoided");
            Directory.CreateDirectory(Path.Combine(parent,"우리 서버"));File.WriteAllText(Path.Combine(parent,"우리 서버 (2)"),"");
            Check(Path.GetFileName(ServerFolders.Available(parent,"우리 서버"))=="우리 서버 (3)","directory and file collisions avoided");
            Check(Path.GetDirectoryName(ServerFolders.Available(parent,".."))==parent,"folder stays within chosen parent");
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            using(var wizard=new CreateServerWizard()){
                var n=(WarmTextBox)typeof(CreateServerWizard).GetField("name",flags)!.GetValue(wizard)!;
                var f=(WarmTextBox)typeof(CreateServerWizard).GetField("folder",flags)!.GetValue(wizard)!;
                n.Text="새 이름";Check(Path.GetFileName(f.Text)=="새 이름","wizard folder follows server name");
                typeof(CreateServerWizard).GetField("installParent",flags)!.SetValue(wizard,parent);n.Text="다른 이름";Check(f.ReadOnly&&f.Text==Path.Combine(parent,"다른 이름"),"chosen parent retained and profile name automatically applied");
            }
            using(var form=new ImportServerForm()){
                var n=(WarmTextBox)typeof(ImportServerForm).GetField("name",flags)!.GetValue(form)!;
                var f=(WarmTextBox)typeof(ImportServerForm).GetField("destination",flags)!.GetValue(form)!;
                n.Text="가져온 서버";Check(Path.GetFileName(f.Text)=="가져온 서버","import folder follows server name");typeof(ImportServerForm).GetField("importParent",flags)!.SetValue(form,parent);n.Text="가져오기 새 이름";Check(f.ReadOnly&&f.Text==Path.Combine(parent,"가져오기 새 이름"),"import retains chosen parent and follows renamed profile");
            }
            demo.MinecraftVersion="미확인";
            Throws(()=>ModPluginService.SearchUrl(demo,"create"),"unknown version requires selection");
            Check(!Uri.UnescapeDataString(ModPluginService.SearchUrl(demo,"create",false)).Contains("versions:"),"unfiltered search omits unknown version");
            Directory.CreateDirectory(Path.Combine(demo.Folder,"logs"));File.WriteAllText(Path.Combine(demo.Folder,"logs","latest.log"),"[INFO] Starting minecraft server version 1.21.1\n");
            Check(MinecraftVersions.Detect(demo.Folder)=="1.21.1","version read from startup log");
            MinecraftVersions.Save(demo,"1.21.1");Check(Storage.Load().Single(p=>p.Id==demo.Id).MinecraftVersion=="1.21.1","version persisted to matching profile");
            var url=Uri.UnescapeDataString(ModPluginService.SearchUrl(demo,"create"));
            Check(url.Contains("versions:1.21.1")&&url.Contains("categories:neoforge"),"search uses version and loader filters");
            using(var form=new ExtensionSearchForm(demo)){
                Preview(form,"online-search-v10");Check(AllControls(form).OfType<WarmToggle>().Any(t=>t.Text=="호환 버전만"&&t.Checked),"compatibility filter is interactive");
                Check(AllControls(form).OfType<WarmButton>().Any(t=>t.Text=="버전 지정"),"version selection available");form.Close();
            }
            Console.WriteLine($"ALL {passed} INSTALLER CHANGE CHECKS PASSED");return;
        }
        if(args.Contains("--grid-paint-tests"))
        {
            using var gridHost=new WarmWindow{Size=new Size(1280,850)};
            using var icon=new Bitmap(64,64);using(var g=Graphics.FromImage(icon)){g.Clear(Color.Magenta);g.FillEllipse(Brushes.Lime,4,4,56,56);}
            var catalog=new CatalogView("모드 · 플러그인 관리");gridHost.Controls.Add(catalog);WarmTheme.Apply(gridHost);
            catalog.SetItems(Enumerable.Range(0,40).Select(i=>new CatalogItem($"test-{i}.jar",$"Test mod {i}","1.0 MB","활성",icon,"1.0")));catalog.ShowMode(true);
            Preview(gridHost,"mods-table");var table=AllControls(catalog).OfType<SelectionGrid>().Single();
            using(var bitmap=new Bitmap(120,100))using(var g=Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Blue);g.SetClip(new Rectangle(0,0,100,90));var before=g.ClipBounds;var smooth=g.SmoothingMode;
                var argsPaint=new DataGridViewCellPaintingEventArgs(table,g,new Rectangle(0,0,100,90),new Rectangle(20,20,60,50),0,table.Columns["Select"].Index,DataGridViewElementStates.Visible,false,false,"",table.DefaultCellStyle,table.AdvancedCellBorderStyle,DataGridViewPaintParts.All);
                typeof(SelectionGrid).GetMethod("OnCellPainting",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(table,[argsPaint]);
                Check(g.SmoothingMode==smooth&&g.ClipBounds==before,"checkbox painting restores graphics smoothing and clipping");Check(bitmap.GetPixel(19,25).ToArgb()==Color.Blue.ToArgb()&&bitmap.GetPixel(80,25).ToArgb()==Color.Blue.ToArgb(),"painting stays within cell bounds");
            }
            for(int i=0;i<5;i++){table.FirstDisplayedScrollingRowIndex=i*4;table.Rows[i*4].Selected=true;gridHost.Width=1240+i*8;Application.DoEvents();}
            using(var bitmap=new Bitmap(table.Width,table.Height))
            {
                table.DrawToBitmap(bitmap,table.ClientRectangle);bitmap.Save(Path.Combine(root,"table-scrolled.png"));
                int leaks=0;foreach(DataGridViewRow row in table.Rows){var bounds=table.GetRowDisplayRectangle(row.Index,false);if(bounds.Height==0||bounds.Bottom<1||bounds.Bottom>bitmap.Height)continue;int y=bounds.Bottom-1;for(int x=0;x<bitmap.Width-24;x++){var color=bitmap.GetPixel(x,y);if(color.ToArgb()==Color.Magenta.ToArgb()||color.ToArgb()==Color.Lime.ToArgb())leaks++;}}
                Check(leaks==0,"colored icons do not bleed into row borders after scrolling and resizing");
            }
            catalog.ShowMode(false);catalog.ShowMode(true);Check(table.Rows.Count==40,"view switching preserves items");
            gridHost.Close();Console.WriteLine($"ALL {passed} GRID PAINT CHECKS PASSED");return;
        }
        if(args.Contains("--deletion-tests"))
        {
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            using var home=new HomeForm();Preview(home,"home-delete");
            var cards=AllControls(home).OfType<HomeServerCard>().ToArray();
            Check(cards.Length==3&&cards.All(c=>c.Controls.OfType<WarmButton>().Any(b=>b.Text=="삭제")),"all home cards expose delete");
            Check(cards.All(c=>c.Controls.OfType<WarmButton>().Where(b=>b.Visible).All(b=>c.ClientRectangle.Contains(b.Bounds))),"home buttons fit within cards");
            void Confirm(Action action,DialogResult result)
            {
                using var tick=new System.Windows.Forms.Timer{Interval=100};tick.Tick+=(_,_)=>{var dialog=Application.OpenForms.OfType<DialogWindow>().FirstOrDefault();if(dialog==null)return;tick.Stop();Check(dialog.AcceptButton is Button b&&b.Text=="삭제","delete confirmation has explicit delete action");dialog.DialogResult=result;};tick.Start();action();Application.DoEvents();
            }
            var delete=cards[0].Controls.OfType<WarmButton>().Single(b=>b.Text=="삭제");
            Confirm(delete.PerformClick,DialogResult.No);Check(Storage.Load().Count==3,"cancel preserves profile");
            Confirm(delete.PerformClick,DialogResult.Yes);Check(Storage.Load().Count==2,"confirmed home delete removes one profile");Check(Directory.Exists(demo.Folder),"home delete preserves server folder");
            var remaining=AllControls(home).OfType<HomeServerCard>().First();var id=remaining.Profile.Id;
            remaining.Controls.OfType<WarmButton>().Single(b=>b.Text=="관리 →").PerformClick();Application.DoEvents();
            var management=(MainForm)typeof(HomeForm).GetField("activePage",flags)!.GetValue(home)!;
            Confirm(()=>management.DeleteStoredProfile(management),DialogResult.Yes);
            Check(typeof(HomeForm).GetField("activePage",flags)!.GetValue(home)==null,"management deletion returns to home");Check(Storage.Load().All(p=>p.Id!=id)&&Storage.Load().Count==1,"management removes only selected profile");Check(management.IsDisposed,"deleted management session is closed");
            var profile=Storage.Load().Single();var backupPath=BackupPaths.ForServer(profile);File.WriteAllText(Path.Combine(backupPath,"2026-10-18_22-24-00.zip"),"test");
            using(var form=new MainForm(false)){form.InitializeForHome(profile);form.Show();var deck=AllControls(form).OfType<PageDeck>().Single();deck.SelectedTab=deck.TabPages.Cast<TabPage>().First(p=>p.Text=="백업");Application.DoEvents();Preview(form,"backup-rounded");var list=AllControls(form).OfType<BackupArchiveList>().Single();Check(list.Controls.OfType<SurfaceCard>().Any(),"backup files rendered as rounded rows");Check(AllControls(list).OfType<WarmButton>().Count()==2,"backup row exposes restore and delete buttons");Check(AllControls(list).OfType<WarmButton>().All(b=>b.Parent!.ClientRectangle.Contains(b.Bounds)),"backup action buttons stay inside row");form.HomeHosted=false;form.Close();}
            home.Close();Console.WriteLine($"ALL {passed} DELETION CHECKS PASSED");return;
        }
        // 실제 팝업을 반복해서 열고 Enter/바깥 클릭/부모 종료 순서를 재현합니다.
        if(args.Contains("--targeted"))
        {
            using(var image=new Bitmap(200,100)){var fit=Artwork.Fit(image,new Rectangle(0,0,130,92));Check(fit.Width==130&&fit.Height==65,"server artwork preserves aspect ratio");}
            using(var owner=new WarmWindow{Size=new Size(1100,800),StartPosition=FormStartPosition.Manual,Location=new Point(180,100),ShowInTaskbar=false,Opacity=0})
            {
                var input=new WarmTextBox{PlaceholderText="설정 이름 또는 키 검색",Font=new Font("Noto Sans KR",15),Size=new Size(180,58)};owner.Controls.Add(input);owner.Show();Application.DoEvents();input.Width=760;input.PerformLayout();
                var native=AllControls(input).OfType<TextBox>().Single();Check(input.ClientRectangle.Contains(native.Bounds)&&native.Width>650,"resized input stays inside rounded container");
                var list=new ListBox{DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=84};owner.Controls.Add(list);WarmTheme.Apply(owner);Check(list.ItemHeight==84,"theme preserves log row height");
                var editor=new PropertiesEditor();owner.Controls.Add(editor);editor.SetReadOnly(true);Check(editor.Toolbar.Enabled&&AllControls(editor.Toolbar).All(c=>c.Enabled),"settings toolbar remains usable while editing locked");
                var textbox=AllControls(editor).OfType<WarmTextBox>().First(x=>x.Text.Length>0);Check(textbox.ReadOnly,"settings strings are read only");Check(AllControls(editor).OfType<WarmCombo>().All(c=>!c.Enabled),"settings choices cannot be changed");
                AllControls(editor.Toolbar).OfType<WarmButton>().First(b=>b.Text=="전체 설정").PerformClick();Check(editor.Values().ContainsKey("server-port"),"all settings remain available for inspection");editor.Dispose();
                var embedded=new WarmWindow{TopLevel=false,Dock=DockStyle.Fill};owner.Controls.Add(embedded);embedded.EmbedInShell();embedded.Show();
                using var dialog=new DialogWindow("중앙 배치 검사","확인");using var timer=new System.Windows.Forms.Timer{Interval=100};
                timer.Tick+=(_,_)=>{timer.Stop();Check(dialog.Owner==owner,"dialog owned by real application window");Check(Math.Abs(dialog.Left+dialog.Width/2-(owner.Left+owner.Width/2))<=1&&Math.Abs(dialog.Top+dialog.Height/2-(owner.Top+owner.Height/2))<=1,"dialog centered on moved parent");Check(!Application.OpenForms.Cast<Form>().Any(f=>f.Opacity==.36),"no dimming overlay window");dialog.DialogResult=DialogResult.OK;};timer.Start();WarmDialog.Present(embedded,dialog);owner.Close();
            }
            using(var wizard=new CreateServerWizard())
            {
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                foreach(int stage in new[]{3,4}){typeof(CreateServerWizard).GetField("step",flags)!.SetValue(wizard,stage);typeof(CreateServerWizard).GetMethod("Render",flags)!.Invoke(wizard,null);Preview(wizard,"wizard-stage-"+stage);}
                Check(AllControls(wizard).OfType<PictureBox>().All(p=>p.SizeMode!=PictureBoxSizeMode.StretchImage),"image selection uses proportional previews");wizard.Close();
            }
            File.WriteAllText(Path.Combine(demo.Folder,"eula.txt"),"eula=true");Directory.CreateDirectory(Path.Combine(demo.Folder,"logs"));File.WriteAllText(Path.Combine(demo.Folder,"logs","latest.log"),"[INFO] Test log");
            using(var form=new MainForm(false)){form.InitializeForHome(demo);form.Show();var deck=AllControls(form).OfType<PageDeck>().Single();deck.SelectedTab=deck.TabPages.Cast<TabPage>().First(p=>p.Text=="로그 뷰어");Application.DoEvents();Preview(form,"logs-preview");var list=AllControls(deck.SelectedTab).OfType<ListBox>().Single();Check(list.ItemHeight==84,"management log rows keep intended height");Check(list.Items.Cast<FileInfo>().All(f=>f.Name!="eula.txt"&&f.Name!="user_jvm_args.txt"),"server config text files excluded from logs");form.HomeHosted=false;form.Close();}
            Console.WriteLine($"ALL {passed} TARGETED CHECKS PASSED");return;
        }
        using(var popupHost=new Form{ShowInTaskbar=false,Size=new Size(500,300)})
        {
            var combo=new WarmCombo();combo.Items.AddRange(["4","8","12","16"]);popupHost.Controls.Add(combo);popupHost.Show();Application.DoEvents();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            for(int i=0;i<30;i++)
            {
                typeof(WarmCombo).GetMethod("Open",flags)!.Invoke(combo,null);Application.DoEvents();
                var popup=(ToolStripDropDown)typeof(WarmCombo).GetField("activePopup",flags)!.GetValue(combo)!;
                var list=(ListBox)((ToolStripControlHost)popup.Items[0]).Control;
                list.SelectedIndex=i%4;
                typeof(Control).GetMethod("OnKeyDown",flags)!.Invoke(list,[new KeyEventArgs(Keys.Enter)]);
                Application.DoEvents();Check(combo.SelectedIndex==i%4,"dropdown repeated selection "+i);
            }
            typeof(WarmCombo).GetMethod("Open",flags)!.Invoke(combo,null);Application.DoEvents();
            combo.Dispose();Application.DoEvents();Check(combo.IsDisposed,"close owner with open dropdown");popupHost.Close();
        }
        using(var shell=new HomeForm(false))
        {
            shell.Show();Application.DoEvents();IntPtr handle=shell.Handle;
            var page=new SettingsForm();typeof(HomeForm).GetMethod("Navigate",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(shell,[page]);Application.DoEvents();
            Check(!page.TopLevel&&page.Parent==shell&&shell.Handle==handle,"settings navigation retains main window");
            Check(page.Padding==Padding.Empty,"embedded page has no duplicate titlebar");page.Close();Application.DoEvents();Check(shell.Visible&&shell.Handle==handle,"return from settings retains main window");shell.Close();
        }
        using(var wizard=new CreateServerWizard())
        {
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var game=(WarmCombo)typeof(CreateServerWizard).GetField("game",flags)!.GetValue(wizard)!;game.Items.Add("1.21.11");game.SelectedIndex=0;
            var build=(WarmCombo)typeof(CreateServerWizard).GetField("build",flags)!.GetValue(wizard)!;build.Items.Add("21.11.48");build.SelectedIndex=0;
            ((WarmCombo)typeof(CreateServerWizard).GetField("kind",flags)!.GetValue(wizard)!).SelectedItem="NeoForge";
            ((ImagePicker)typeof(CreateServerWizard).GetField("serverImage",flags)!.GetValue(wizard)!).SetPath("preset:0");
            Preview(wizard,"wizard-preview");
            for(int stage=1;stage<6;stage++)
            {
                typeof(CreateServerWizard).GetField("step",flags)!.SetValue(wizard,stage);typeof(CreateServerWizard).GetMethod("Render",flags)!.Invoke(wizard,null);Preview(wizard,"wizard-stage-"+stage);
                if(stage==3)
                {
                    var help=AllControls(wizard).OfType<PropertyHelpButton>().First(x=>x.Visible);
                    typeof(Control).GetMethod("OnMouseEnter",flags)!.Invoke(help,[EventArgs.Empty]);Application.DoEvents();
                    var popup=Application.OpenForms.OfType<PropertyHelpPopup>().Single();
                    Check(popup.Visible&&wizard.RectangleToScreen(wizard.ClientRectangle).Contains(popup.Bounds),"property hover help remains inside owner window");
                    using(var bitmap=new Bitmap(popup.Width,popup.Height)){popup.DrawToBitmap(bitmap,popup.ClientRectangle);bitmap.Save(Path.Combine(root,"property-help-preview.png"));}
                    typeof(Control).GetMethod("OnMouseLeave",flags)!.Invoke(help,[EventArgs.Empty]);Check(!popup.Visible,"property help disappears on pointer leave");
                }
            }
            Check(true,"six wizard pages render without network or API field");
            typeof(CreateServerWizard).GetMethod("ShowInstallProgress",flags)!.Invoke(wizard,[demo]);Preview(wizard,"wizard-install-progress");wizard.Close();
        }
        using(var dialog=new DialogWindow("서버를 정상 종료한 뒤 계속할까요?\r\n현재 월드를 저장하고 안전하게 종료합니다.","서버 종료 확인",MessageBoxButtons.YesNo))
        {Preview(dialog,"dialog-preview");Check(dialog.CancelButton?.DialogResult==DialogResult.No,"confirmation cancel does not approve");dialog.Close();}
        using(var import=new ImportServerForm()){Preview(import,"import-preview");Check(true,"copy import dialog renders");import.Close();}
        Check(ApiIdentity.Valid("contact@example.com")&&ApiIdentity.Valid("https://example.com/project")&&!ApiIdentity.Valid("bad\r\nheader"),"API identity validation");
        using(var home=new HomeForm()){Preview(home,"home-preview");var homeCards=AllControls(home).OfType<HomeServerCard>().ToArray();Check(homeCards.Length==3&&homeCards.All(c=>c.Top==homeCards[0].Top),"three home cards remain on one row at reference size");Check(homeCards.All(c=>c.Right<=c.Parent!.ClientSize.Width),"home cards stay inside viewport");home.Size=new Size(1280,900);Preview(home,"home-1280-preview");Check(homeCards.All(c=>c.Right<=c.Parent!.ClientSize.Width),"home cards stay inside smaller viewport");home.Close();}
        using(var settings=new SettingsForm()){Preview(settings,"paths-preview");foreach(string page in new[]{"방화벽 설정","포트포워딩 설정"}){typeof(SettingsForm).GetMethod("ShowPage",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(settings,[page]);Preview(settings,page=="방화벽 설정"?"firewall-preview":"ports-preview");}settings.Close();}
        using (var form = new MainForm(false) { Opacity = 0, ShowInTaskbar = false })
        {
            form.InitializeForHome(demo);
            form.Show(); form.PerformLayout(); Application.DoEvents();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(Path.Combine(root, "ui-preview.png"));
            Check(form.Controls.Count > 0, "WinForms construction and rendering");
            IEnumerable<Control> Descend(Control c){foreach(Control child in c.Controls){yield return child;foreach(var nested in Descend(child))yield return nested;}}
            var deck=Descend(form).OfType<PageDeck>().Single();
            foreach(TabPage page in deck.TabPages){deck.SelectedTab=page;Preview(form,"management-page-"+deck.SelectedIndex);}
            Check(deck.TabPages.Count>=13,"all management features remain reachable");
            deck.SelectedTab=deck.TabPages.Cast<TabPage>().First(p=>p.Text is "Mods" or "Plugins");
            var mainHandle=form.Handle;typeof(MainForm).GetMethod("OpenExtensionSearch",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(form,null);Application.DoEvents();
            var searchPage=AllControls(form).OfType<ExtensionSearchForm>().Single();Check(!searchPage.TopLevel&&form.Handle==mainHandle,"online search stays in the same management window");Preview(form,"online-search-preview");searchPage.Close();
            Check(!AllControls(form).OfType<ExtensionSearchForm>().Any(),"online search returns to content catalog");
            using(var help=new HoverHelp()){help.Present(form,"許可 값: true / false\n기본값: true",form.PointToScreen(new Point(200,200)));Check(help.Visible,"hover help appears without modal dialog");help.Hide();Check(!help.Visible,"hover help hides");}
            form.HomeHosted=false;form.Close();
        }
        using(var required=new DialogWindow("내 서버 목록에 표시할 이름이 필요해요.","서버 이름",MessageBoxButtons.OKCancel,true))
        {
            Preview(required,"required-input-preview");Check(required.AcceptButton is Control {Enabled:false},"required input cannot accept empty value");required.Input!.Text="우리 서버";Check(required.AcceptButton is Control {Enabled:true},"required input accepts supplied value");required.Close();
        }
        string restoreSample=Path.Combine(root,"restore-preview.zip");File.WriteAllText(restoreSample,"preview");
        using(var restore=new RestoreDialog(restoreSample,demo)){Preview(restore,"restore-confirm-preview");restore.Close();}
        using(var selectionHost=new Form{Size=new Size(1200,800),ShowInTaskbar=false,Opacity=0})
        {
            var catalog=new CatalogView("운영자 (ops)",true);selectionHost.Controls.Add(catalog);selectionHost.Show();catalog.SetItems([new("a","A","4",""),new("b","B","2","")]);catalog.ShowMode(true);Application.DoEvents();
            AllControls(catalog).OfType<WarmButton>().First(b=>b.Text=="선택 해제").PerformClick();
            var table=AllControls(catalog).OfType<SelectionGrid>().Single();
            void Toggle(int row){var bounds=table.GetCellDisplayRectangle(table.Columns["Select"].Index,row,true);typeof(SelectionGrid).GetMethod("OnMouseDown",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(table,[new MouseEventArgs(MouseButtons.Left,1,bounds.X+12,bounds.Y+12,0)]);}
            Toggle(0);Toggle(1);Check(catalog.SelectedIds.Length==2,"checkbox clicks retain multiple selections without Ctrl");Toggle(0);Check(catalog.SelectedIds.SequenceEqual(new[]{"b"}),"checkbox toggles only requested row");selectionHost.Close();
        }
        if(args.Contains("--ui-only")){Console.WriteLine($"ALL {passed} UI CHECKS PASSED");return;}
        string roster=Path.Combine(root,"roster-check");Directory.CreateDirectory(roster);
        File.WriteAllText(Path.Combine(roster,"usercache.json"),"[{\"name\":\"Steve\",\"uuid\":\"00000000-0000-0000-0000-000000000001\"}]");
        OfflineRoster.GrantOperator(roster,"Steve",2);
        Check((int?)PlayerData.Read(roster,"ops.json")[0]?["level"]==2,"offline operator grant uses selected level and cached UUID");
        OfflineRoster.GrantOperator(roster,"Steve",4);
        Check(PlayerData.Read(roster,"ops.json").Count==1&&(int?)PlayerData.Read(roster,"ops.json")[0]?["level"]==4,"operator update preserves unique identity");
        Throws(()=>OfflineRoster.GrantOperator(roster,"Unknown",4),"unknown offline player is not assigned an invented UUID");
        Throws(()=>OfflineRoster.GrantOperator(roster,"Steve",5),"invalid operator level rejected");
        string retention=Path.Combine(root,"retention-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(retention);
        string first=Path.Combine(retention,"first.zip"),second=Path.Combine(retention,"second.zip"),external=Path.Combine(retention,"external.zip");
        File.WriteAllText(first,"one");File.WriteAllText(second,"two");File.WriteAllText(external,"imported");
        BackupService.RetainCreatedBackup(first,0);BackupService.RetainCreatedBackup(second,1);
        Check(!File.Exists(first)&&File.Exists(second),"backup retention keeps newest registered backup");
        Check(File.Exists(external),"backup retention preserves unregistered files");
        string metadataJar=Path.Combine(retention,"metadata.jar");using(var zip=ZipFile.Open(metadataJar,ZipArchiveMode.Create)){using var writer=new StreamWriter(zip.CreateEntry("fabric.mod.json").Open());writer.Write("{\"name\":\"Sample Mod\",\"version\":\"1.2.3\"}");}
        Check(ModFiles.Metadata(metadataJar)==("Sample Mod","1.2.3"),"catalog reads declared mod name and version without running jar");
        // Preserve a real UI synchronization context for the lifecycle checks.
        using var host = new Form { ShowInTaskbar = false, WindowState = FormWindowState.Minimized, Opacity = 0 };
        Exception? failure = null;
        host.Shown += async (_, _) =>
        {
            try { await Run(root, Path.GetFullPath(args[1]), args.Length > 2 ? args[2] : "java.exe"); }
            catch (Exception e) { failure = e; }
            finally { host.Close(); }
        };
        Application.Run(host);
        if (failure != null) throw failure;
        Console.WriteLine($"ALL {passed} CHECKS PASSED");
    }

    static Profile Seed(string root)
    {
        new AppSettings{ServerRoot=Path.Combine(root,"servers"),BackupRoot=Path.Combine(root,"backups")}.Save();
        string dir=Path.Combine(root,"demo");Directory.CreateDirectory(dir);Directory.CreateDirectory(Path.Combine(dir,"mods"));File.WriteAllText(Path.Combine(dir,"server.properties"),"server-port=25565\nmax-players=20\nwhite-list=true\n");
        File.WriteAllText(Path.Combine(dir,"whitelist.json"),"[{\"name\":\"Steve\",\"uuid\":\"00000000-0000-0000-0000-000000000001\"},{\"name\":\"Alex\",\"uuid\":\"00000000-0000-0000-0000-000000000002\"}]");
        File.WriteAllText(Path.Combine(dir,"banned-players.json"),"[{\"name\":\"Player_A\",\"reason\":\"채팅 도배\",\"created\":\"2026-10-18\"}]");File.WriteAllText(Path.Combine(dir,"ops.json"),"[{\"name\":\"Migi0112\",\"level\":4}]");
        foreach(string name in new[]{"Create","JEI","Jade","AppleSkin","Waystones","MouseTweaks","FerriteCore","Clumps"}){string file=Path.Combine(dir,"mods",name+".jar");if(!File.Exists(file)){using var zip=ZipFile.Open(file,ZipArchiveMode.Create);var entry=zip.CreateEntry("icon.png");using var stream=entry.Open();Artwork.Default.Save(stream,System.Drawing.Imaging.ImageFormat.Png);}}
        var profile=new Profile{Name="우리 서버",Folder=dir,ImagePath="preset:0",MinecraftVersion="1.21.11",MinRam=8,MaxRam=16};Storage.Save([profile,new Profile{Name="친구들과 야생",Kind="Paper",Folder=dir,ImagePath="preset:1",MinecraftVersion="1.21.11",MaxRam=8},new Profile{Name="모드 테스트",Kind="Fabric",Folder=dir,ImagePath="preset:2",MinecraftVersion="1.21.1",MaxRam=8}]);return profile;
    }
    static async Task Run(string root, string fixture, string java)
    {
        Check(NeoForgeInstaller.MinecraftVersion("26.1.2.122")=="26.1.2" && NeoForgeInstaller.MinecraftVersion("21.11.48")=="1.21.11", "new and legacy NeoForge version mapping");
        Check(NeoForgeInstaller.GameList(["21.11.48","26.1.2.122","26.3.0.90"]).SequenceEqual(new[]{"26.3","26.1.2","1.21.11"}),"NeoForge newest game sorting");
        Check(NeoForgeInstaller.MinecraftVersion("26.1.0.10-beta")=="26.1"&&NeoForgeInstaller.MinecraftVersion("26.1.0.10-rc1")=="","NeoForge prerelease filtering");
        string batch=Path.Combine(root,"batch-mods");Directory.CreateDirectory(batch);File.WriteAllText(Path.Combine(batch,"one.jar"),"one");File.WriteAllText(Path.Combine(batch,"two.jar"),"two");
        ModFiles.Change(batch,["one.jar","two.jar"],"disable");Check(File.Exists(Path.Combine(batch,"one.jar.disabled"))&&File.Exists(Path.Combine(batch,"two.jar.disabled")),"batch disable");
        ModFiles.Change(batch,["one.jar.disabled","two.jar.disabled"],"enable");Check(File.Exists(Path.Combine(batch,"one.jar"))&&File.Exists(Path.Combine(batch,"two.jar")),"batch enable");
        File.WriteAllText(Path.Combine(batch,"two.jar.disabled"),"conflict");Throws(()=>ModFiles.Change(batch,["one.jar","two.jar"],"disable"),"batch conflict rejected before mutation");Check(File.Exists(Path.Combine(batch,"one.jar")),"batch conflict preserves previous files");
        Throws(()=>ModFiles.Change(batch,["../outside.jar"],"delete"),"batch traversal rejected");ModFiles.Change(batch,["one.jar"],"delete");Check(Directory.GetFiles(Path.Combine(batch,"removed"),"one.jar",SearchOption.AllDirectories).Length==1,"removed mods recoverable");
        var bp=new Profile{Name="same",Folder=Path.Combine(root,"source"),BackupFolder=Path.Combine(root,"named-backups")};Directory.CreateDirectory(bp.Folder);string a=BackupPaths.ForServer(bp);Check(a==BackupPaths.ForServer(bp),"backup directory stable per profile");var bp2=new Profile{Name="same",Folder=bp.Folder,BackupFolder=bp.BackupFolder};Check(a!=BackupPaths.ForServer(bp2),"same-name backup separation");
        Check(MainForm.IsStatusLog("There are 0 of a max of 20 players online")&&!MainForm.IsStatusLog("WARN Overworld: TPS low"),"polling logs filtered without hiding warnings");
        string banFolder=Path.Combine(root,"ban-test");Directory.CreateDirectory(banFolder);File.WriteAllText(Path.Combine(banFolder,"banned-players.json"),"[{\"name\":\"Steve\",\"reason\":\"old\",\"expires\":\"forever\"}]");PlayerData.EditReason(banFolder,"Steve","새 사유");Check((string?)PlayerData.Read(banFolder,"banned-players.json")[0]?["expires"]=="forever","ban edit preserves metadata");Throws(()=>PlayerData.Reason("line\ncommand"),"player reason rejects command injection");
        string original=Path.Combine(root,"import-original"), copied=Path.Combine(root,"import-copy-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(original);
        File.WriteAllText(Path.Combine(original,"server.jar"),"fixture");File.WriteAllText(Path.Combine(original,"server.properties"),"server-port=25577\n");Directory.CreateDirectory(Path.Combine(original,"empty-mods"));
        var imported=await ServerImportService.CopyAsync(original,copied,new Profile{Name="복사 서버",Kind="Paper"},"server.jar",new Progress<string>());
        Check(imported.Folder==copied&&imported.ManagedInstall&&imported.LaunchJar=="server.jar"&&imported.Port==25577,"import profile uses copied folder and port");
        Check(File.ReadAllText(Path.Combine(original,"server.jar"))=="fixture"&&File.ReadAllText(Path.Combine(copied,"server.jar"))=="fixture"&&Directory.Exists(Path.Combine(copied,"empty-mods")),"import preserves original and empty directories");
        async Task RejectImport(string target,CancellationToken ct,string label){try{await ServerImportService.CopyAsync(original,target,new Profile(),"server.jar",new Progress<string>(),ct);}catch{Check(!Directory.Exists(target)&&!Directory.EnumerateDirectories(root,Path.GetFileName(target)+".importing-*").Any(),label);return;}throw new Exception("Expected import rejection");}
        await RejectImport(Path.Combine(original,"nested"),default,"nested import rejected");
        await RejectImport(Path.Combine(root,"cancelled-import"),new CancellationToken(true),"cancelled import removes staging folder");
        using(var locked=new FileStream(Path.Combine(original,"session.lock"),FileMode.Create,FileAccess.ReadWrite,FileShare.None))await RejectImport(Path.Combine(root,"locked-import"),default,"running source lock rejects import");
        string updated = ServerService.UpdateHeapArguments("# -Xmx99G comment\n-Xms512M -Xmx2G -XX:+UseG1GC # keep\n-Dflag=yes", 1, 2);
        Check(updated.Contains("# -Xmx99G comment") && updated.Contains("-XX:+UseG1GC") && updated.Contains("-Dflag=yes"), "heap edit preserves comments and unrelated flags");
        Check(Regex.Matches(updated, @"(?m)^-Xms1G\r?$").Count == 1 && updated.Contains("-Xmx2G") && !updated.Contains("512M"), "heap options replaced");
        Throws(() => ServerService.UpdateHeapArguments("", 4, 2), "invalid heap range rejected");
        Throws(() => ServerService.UpdateHeapArguments("-XX:MaxHeapSize=2G", 1, 2), "conflicting heap format rejected");
        string source = Path.Combine(root, "서버 폴더 & test"); Directory.CreateDirectory(source);
        string propPath = Path.Combine(source, "server.properties");
        File.WriteAllText(propPath, "# comment\nserver-port=25565\nmotd=Hello\\u0020World\nlevel-name=world\ncustom=keep\n");
        var props = new PropertiesFile(); props.Load(propPath);
        var edited = new Dictionary<string, string>(props.Values) { ["server-port"] = "25566" }; props.Save(edited);
        Check(File.ReadAllText(propPath).Contains("# comment") && File.ReadAllText(propPath).Contains("motd=Hello\\u0020World"), "properties comments and escapes preserved");
        Check(PropertiesFile.ReadValue(propPath, "server-port", "") == "25566" && File.Exists(propPath + ".manager.bak"), "port saved with backup");
        edited["server-port"] = "70000"; Throws(() => props.Save(edited), "invalid port rejected");
        File.AppendAllText(propPath, "# external change"); Throws(() => props.Save(edited), "external properties edits protected");
        Directory.CreateDirectory(Path.Combine(source, "world")); File.WriteAllText(Path.Combine(source, "world", "level.dat"), "TEST WORLD");
        File.WriteAllText(Path.Combine(source, "world", "session.lock"), "LOCK");
        string dest = Path.Combine(root, "backups");
        Throws(() => BackupService.ValidateDestination(source, Path.Combine(source, "backups")), "nested backup rejected");
        string zipPath = await BackupService.CreateAsync(source, dest, new Progress<string>());
        using (var zip = ZipFile.OpenRead(zipPath))
        {
            Check(zip.GetEntry("world/level.dat") != null && zip.GetEntry("server.properties") == null, "backup contains only world without configuration");
            Check(zip.GetEntry("world/session.lock") == null, "backup excludes session locks");
            using var reader = new StreamReader(zip.GetEntry("world/level.dat")!.Open());
            Check(await reader.ReadToEndAsync() == "TEST WORLD", "ZIP world round-trip");
        }
        string javac = java == "java.exe" ? "javac.exe" : Path.Combine(Path.GetDirectoryName(java)!, "javac.exe");
        var compile = new ProcessStartInfo(javac) { UseShellExecute = false, CreateNoWindow = true };
        compile.ArgumentList.Add("-encoding"); compile.ArgumentList.Add("UTF-8"); compile.ArgumentList.Add("-d"); compile.ArgumentList.Add(source); compile.ArgumentList.Add(fixture);
        using (var p = Process.Start(compile)!) { await p.WaitForExitAsync(); Check(p.ExitCode == 0, "Java fixture compiled"); }
        File.WriteAllText(Path.Combine(source, "run.bat"), "@echo off\r\njava @user_jvm_args.txt -cp . FakeServer\r\npause\r\n");
        var profile = new Profile { Folder = source, MinRam = 1, MaxRam = 1, Java = java };
        var server = new ServerService(); bool? unexpected = null;
        server.Exited += (crash, _) => unexpected = crash;
        var received = new List<string>();
        async Task Until(Func<bool> predicate, string name)
        {
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed < TimeSpan.FromSeconds(15))
            {
                while (server.Output.TryDequeue(out var line)) { received.Add(line); server.Parse(line); }
                if (predicate()) { Check(true, name); return; }
                await Task.Delay(100);
            }
            throw new Exception("Timeout: " + name + "\n" + string.Join('\n', received));
        }
        try
        {
            Check((await ServerService.CheckJavaAsync(java)).Contains("version"), "Java version probe");
            server.Start(profile);
            await Until(() => server.Online, "run.bat starts Java and reports online (Unicode and ampersand path)");
            var duplicate = new ServerService(); Throws(() => duplicate.Start(profile), "duplicate manager start rejected");
            await Until(() => !server.Metrics.Contains('—'), "Java process RAM and CPU monitoring");
            await server.SendAsync("list"); await server.SendAsync("neoforge tps"); await server.SendAsync("한글 콘솔 테스트");
            await Until(() => server.Players.SetEquals(["Alex", "Steve"]), "player list parsing");
            await Until(() => server.Tps == "19.88", "TPS parsing");
            await Until(() => received.Any(x => x.Contains("ECHO: 한글 콘솔 테스트")), "UTF-8 command round-trip");
            server.Parse("Overall: Mean tick time: 12.3 ms. Mean TPS: 19.500"); Check(server.Tps == "19.50", "legacy TPS response parsing");
            await server.StopAsync(); Check(!server.Running && unexpected == false, "graceful stop and batch PAUSE released");
            unexpected = null; server.Start(profile); await Until(() => server.Online, "restart after graceful stop");
            await server.SendAsync("crash"); await Until(() => !server.Running, "crash terminates process and releases PAUSE");
            Check(unexpected == true, "unexpected exit classified as crash candidate");
            server.Start(profile); await Until(() => server.Online, "start after crash"); server.ForceStop();
            await Until(() => !server.Running, "force stop terminates owned process tree");
        }
        finally { if (server.Running) { server.ForceStop(); await Task.Delay(1500); } }
    }
}








