using NeoForgeServerManager;
static class DependencyUiChecks
{
    static T Wait<T>(Task<T> task){while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(10);}return task.GetAwaiter().GetResult();}
    public static void Run(string root,string source,Action<bool,string> check)
    {
        string kotlin=Directory.GetFiles(source,"kotlinforforge*.jar").Single();
        var local=LocalModMetadata.Read(kotlin);
        check(local?.Id=="kotlinforforge"&&local.Version=="5.12.0","nested Kotlin mod identity and version");
        check(local!.Required.Length==0,"bundled language libraries not treated as external dependencies");
        var p=new Profile{Folder=Path.Combine(root,"isolated-kotlin"),Kind="NeoForge",MinecraftVersion="1.21.1"};
        Directory.CreateDirectory(Path.Combine(p.Folder,"mods"));string copy=Path.Combine(p.Folder,"mods",Path.GetFileName(kotlin));File.Copy(kotlin,copy,true);
        using var net=new DownloadClient();var resolver=new ExtensionDependencies(net);
        var version=Wait(resolver.VersionOf(copy,p));
        check(version!=null,"Kotlin distribution recognized");
        var plan=Wait(resolver.Plan(p,new(version!["project_id"]!.ToString(),"Kotlin","kotlin-for-forge"),version));
        check(plan.Files.Length==1&&plan.Files[0].Installed&&plan.Conflicts.Length==0,"Kotlin dependency inspection works without reinstall");
        check(plan.Files[0].Project.ClientSide=="optional"&&plan.Files[0].Project.ServerSide=="optional","official client and server requirements retained");
        check(Wait(InstalledExtensions.ProjectIds(p)).Contains(plan.Files[0].Project.Id),"Kotlin online duplicate install blocked");
        var required=new DependencyFile(new("core","Sophisticated Core","sophisticated-core",null,"required","required"),"core1","1.5.7","core.jar","",new string('a',128),false);
        var client=new DependencyFile(new("fusion","Fusion (Connected Textures)","fusion-connected-textures",null,"required","unsupported"),"fusion1","1.3.16","fusion.jar","",new string('b',128),false);
        var preview=new DependencyPlan([plan.Files[0],required,client],[],new());
        check(!(preview with{Files=[client]}).CanInstall,"client-only dependency cannot be installed into server");
        using var window=new WarmWindow{Text="필수 의존성",Size=new(800,700)};
        var list=new DependencyListPanel{Dock=DockStyle.Fill};window.Controls.Add(list);window.Show();list.SetPlan(preview,true);Application.DoEvents();
        var box=list.Controls.OfType<DependencyCard>().SelectMany(c=>c.Controls.OfType<DependencySelectionBox>()).Single();
        check(box.Text==""&&box.Parent is DependencyCard{Selected:true},"textless coral checkbox and selected card");
        box.Checked=false;check(!((DependencyCard)box.Parent!).Selected&&list.SelectedIds.Length==0,"selection deselect updates card and selected plan");
        box.Checked=true;
        foreach(int width in new[]{800,680,1000}){
            window.Width=width;Application.DoEvents();
            check(box.Parent!.ClientRectangle.Contains(box.Bounds)&&box.Top<30,"checkbox stays inside upper right at width "+width);
        }
        check(list.SelectedIds.SequenceEqual(new[]{"core"}),"client-only and installed rows cannot be selected");
        check(ModEnvironment.Summary(null)=="서버 미확인 · 클라 미확인","unknown environment never inferred from installation");
        window.Size=new(800,700);Application.DoEvents();
        check(!list.HorizontalScroll.Visible,"resizing leaves no stale horizontal scrollbar");
        using(var bitmap=new Bitmap(window.Width,window.Height)){window.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(root,"dependency-selection.png"));}
        window.Controls.Remove(list);list.Dispose();
        using var catalog=new CatalogView("모드 · 플러그인 관리"){Title="모드 관리",Dock=DockStyle.Fill};window.Controls.Add(catalog);
        catalog.SetItems([new("kotlinforforge.jar","Kotlin For Forge","6.9 MB","활성",null,"5.12.0",Project:plan.Files[0].Project)]);Application.DoEvents();
        IEnumerable<Control> Children(Control control){foreach(Control c in control.Controls){yield return c;foreach(var child in Children(c))yield return child;}}
        check(Children(catalog).OfType<CatalogCard>().Single().Height>=274,"installed card reserves space for environment icons");
        catalog.ShowMode(true);Application.DoEvents();check(Children(catalog).OfType<DataGridView>().Single().Columns.Contains("Environment"),"details list includes environment icon column");window.Close();
    }
}
