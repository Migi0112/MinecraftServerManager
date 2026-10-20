using NeoForgeServerManager;
static class LocalDependencyChecks
{
    static T Wait<T>(Task<T> task){while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(10);}return task.GetAwaiter().GetResult();}
    public static void Run(string root,string source,Action<bool,string> check)
    {
        var files=Directory.GetFiles(source,"sophisticated*.jar");
        var backpack=files.Single(f=>Path.GetFileName(f).StartsWith("sophisticatedbackpacks-"));
        var metadata=LocalModMetadata.Read(backpack)!;
        check(metadata.Id=="sophisticatedbackpacks"&&metadata.Version=="3.26.9","local jar identity and actual version");
        check(metadata.Required.Length==1&&metadata.Required[0].Id=="sophisticatedcore","required dependencies exclude loader and game");
        check(LocalModMetadata.Accepts("[1.5.6,)","1.5.7")&&!LocalModMetadata.Accepts("[1.5.6,)","1.5.5")&&!LocalModMetadata.Accepts("[1.5.6,1.6)","1.6"),"dependency range boundaries");
        var p=new Profile{Folder=Path.Combine(root,"isolated"),Kind="NeoForge",MinecraftVersion="1.21.1"};
        Directory.CreateDirectory(Path.Combine(p.Folder,"mods"));
        foreach(var f in files)File.Copy(f,Path.Combine(p.Folder,"mods",Path.GetFileName(f)),true);
        using var net=new DownloadClient();var resolver=new ExtensionDependencies(net);
        var version=Wait(resolver.VersionOf(backpack,p));
        check(version!=null,"unindexed installed jar resolved using local metadata");
        var plan=Wait(resolver.Plan(p,new(version!["project_id"]!.ToString(),"Backpacks","sophisticated-backpacks"),version));
        check(plan.Files.Length==2&&plan.Files.All(x=>x.Installed)&&plan.Conflicts.Length==0,"existing backpack and core recognized without reinstall");
        var ids=Wait(InstalledExtensions.ProjectIds(p));
        check(plan.Files.All(x=>ids.Contains(x.Project.Id)),"online search recognizes installed projects");
        var core=plan.Files.Single(x=>x.Project.Slug=="sophisticated-core");
        // Only delete our copied test fixture, never the user's source JAR.
        File.Delete(Path.Combine(p.Folder,"mods",files.Select(Path.GetFileName).Single(f=>f!.StartsWith("sophisticatedcore-"))!));
        var missing=Wait(resolver.Plan(p,plan.Files[0].Project,version));
        check(missing.CanInstall&&missing.Files.Single(x=>x.Project.Id==core.Project.Id).Installed==false,"missing dependency is installable");
        check(!missing.Select([]).CanInstall&&missing.Select([core.Project.Id]).CanInstall,"empty and selected dependency plans");
        using var panel=new DependencyListPanel{Size=new(700,500)};panel.SetPlan(missing,true);
        check(panel.SelectedIds.SequenceEqual(new[]{core.Project.Id}),"only missing dependencies selectable");
        string originalHash=ExtensionDependencies.Hash(backpack);
        var bad=missing with{Files=missing.Files.Select(f=>f.Installed?f:f with{Range="[999.0,)"}).ToArray()};
        bool rejected=false;
        try{Wait(resolver.Install(p,bad,new Progress<string>()).ContinueWith(t=>{t.GetAwaiter().GetResult();return true;}));}catch(IOException){rejected=true;}
        check(rejected&&Directory.GetFiles(Path.Combine(p.Folder,"mods")).Length==1,"wrong required range leaves copied server unchanged");
        Wait(resolver.Install(p,missing.Select([core.Project.Id]),new Progress<string>()).ContinueWith(t=>{t.GetAwaiter().GetResult();return true;}));
        check(Directory.GetFiles(Path.Combine(p.Folder,"mods")).Length==2&&ExtensionDependencies.Hash(Path.Combine(p.Folder,"mods",Path.GetFileName(backpack)))==originalHash,"selected dependency installs without replacing original backpack");
    }
}
