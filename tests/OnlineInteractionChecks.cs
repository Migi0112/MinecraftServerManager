using System.Reflection;
using NeoForgeServerManager;
static class OnlineInteractionChecks
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static IEnumerable<Control> All(Control parent){foreach(Control c in parent.Controls){yield return c;foreach(var child in All(c))yield return child;}}
    static void Pump(int ms){var until=DateTime.UtcNow.AddMilliseconds(ms);while(DateTime.UtcNow<until){Application.DoEvents();Thread.Sleep(10);}}
    public static void Run(string root,Profile profile,Action<bool,string> check)
    {
        string routine="[2010월2026 09:37:55.272] [Server thread/INFO] [net.minecraft.server.MinecraftServer/]: Twilight Forest: 20.000 TPS (0.007 ms/tick)";
        check(MainForm.IsStatusLog(routine),"actual mod dimension TPS line classified as status");
        check(ConsoleLogFilter.IsDimensionRoutine(routine.Replace("Twilight Forest","New Mod Dimension")),"dimension filter independent of mod name");
        check(!ConsoleLogFilter.IsDimensionRoutine(routine.Replace("/INFO]","/WARN]")),"dimension warning remains visible");
        check(!ConsoleLogFilter.IsDimensionRoutine(routine.Replace("/INFO]","/ERROR]")),"dimension error remains visible");
        check(!ConsoleLogFilter.IsDimensionRoutine("[Server thread/INFO]: <Steve> Twilight Forest: 20.000 TPS (0.007 ms/tick)"),"player chat is not filtered as status");
        check(!ConsoleLogFilter.IsDimensionRoutine("[Server thread/INFO]: [Server] Twilight Forest: 20.000 TPS (0.007 ms/tick)"),"server announcement is not filtered");
        using(var manager=new MainForm(false,profile)){
            var remember=typeof(MainForm).GetMethod("RememberLog",Private)!;var show=typeof(MainForm).GetMethod("ShowConsoleLine",Private)!;
            remember.Invoke(manager,[routine]);check(!(bool)show.Invoke(manager,[routine])!,"main log hides mod status by default");
            var stored=(Queue<string>)typeof(MainForm).GetField("fullLogs",Private)!.GetValue(manager)!;check(stored.Contains(routine),"full history retains original routine line");
            var toggle=(WarmToggle)typeof(MainForm).GetField("hideDimensionLogs",Private)!.GetValue(manager)!;toggle.Checked=false;check((bool)show.Invoke(manager,[routine])!,"toggle restores mod status in main log");toggle.Checked=true;
            typeof(MainForm).GetField("showFullLog",Private)!.SetValue(manager,true);check((bool)show.Invoke(manager,[routine])!,"all logs bypasses dimension hiding");
            var offline=(List<Control>)typeof(MainForm).GetField("offlineOnly",Private)!.GetValue(manager)!;check(!offline.Any(c=>c.Text=="온라인 검색"),"online search not registered as offline-only action");
        }
        string[] players=["Steve","Alex"];using(var form=new WarmWindow{Size=new(720,540)})using(var picker=new OnlinePlayerPicker(()=>players,_=>Task.FromResult<Image?>(null)){Width=580,Height=46,Top=100,Left=30}){
            form.Controls.Add(picker);form.Show();picker.PerformClick();Pump(100);
            var popup=(ToolStripDropDown)typeof(OnlinePlayerPicker).GetField("popup",Private)!.GetValue(picker)!;var panel=((ToolStripControlHost)popup.Items[0]).Control;var search=All(panel).OfType<WarmTextBox>().Single();var list=All(panel).OfType<ExtensionResultsList>().Single();
            check(list.Items.Count==2,"player dropdown contains currently connected names");search.Text="ste";check(list.Items.Count==1&&(string)list.Items[0]=="Steve","player search filters by name");
            typeof(Control).GetMethod("OnKeyDown",Private)!.Invoke(list,[new KeyEventArgs(Keys.Enter)]);Pump(30);check(picker.SelectedName=="Steve","player dropdown selection updates target");
            players=["Alex"];picker.RefreshPlayers();check(picker.SelectedName=="","departed target clears before execution");
            picker.PerformClick();Pump(50);popup=(ToolStripDropDown)typeof(OnlinePlayerPicker).GetField("popup",Private)!.GetValue(picker)!;panel=((ToolStripControlHost)popup.Items[0]).Control;list=All(panel).OfType<ExtensionResultsList>().Single();players=["Alex","Migi0112"];picker.RefreshPlayers();check(list.Items.Contains("Migi0112"),"open dropdown reflects new connection");
            players=[];picker.RefreshPlayers();check(list.Items.Count==0&&All(panel).OfType<Label>().Any(l=>l.Text.Contains("없습니다")),"empty online roster has explicit empty state");form.Close();
        }
        string sent="";players=["Steve"];var item=new GiveItem("minecraft:diamond","다이아몬드","Diamond");profile.MinecraftVersion="1.21.1";
        using(var palette=new CommandPaletteWindow(profile,()=>true,c=>{sent=c;return Task.CompletedTask;},()=>"",players,()=>Task.FromResult(new[]{item}),()=>players)){
            palette.Show();Pump(100);var target=All(palette).OfType<OnlinePlayerPicker>().Single();target.RefreshPlayers();target.PerformClick();Pump(50);
            var popup=(ToolStripDropDown)typeof(OnlinePlayerPicker).GetField("popup",Private)!.GetValue(target)!;var panel=((ToolStripControlHost)popup.Items[0]).Control;var list=All(panel).OfType<ExtensionResultsList>().Single();typeof(Control).GetMethod("OnKeyDown",Private)!.Invoke(list,[new KeyEventArgs(Keys.Enter)]);
            var picker=All(palette).OfType<GiveItemPicker>().Single();typeof(GiveItemPicker).GetProperty("Selected")!.SetValue(picker,item);Pump(500);var run=All(palette).OfType<WarmButton>().Single(b=>b.Text=="실행");run.PerformClick();Pump(50);check(sent=="give Steve minecraft:diamond 1","selected online player is used by actual palette dispatch");
            players=[];Pump(500);check(!run.Enabled&&target.SelectedName.Length==0,"palette disables targeted command after disconnect");palette.Close();
        }
        bool allowed=false;var result=new ModPluginService.Result("fixture","Test Mod","fixture");
        using(var search=new ExtensionSearchForm(profile,fetchPage:(_,_,offset)=>Task.FromResult(new ModPluginService.SearchPage([result],1,offset)),canInstall:()=>allowed)){
            search.Show();Pump(150);var button=All(search).OfType<WarmButton>().Single(b=>b.Name=="ExtensionInstall");check(!button.Enabled&&button.Text=="서버 종료 후 설치","running server can browse but cannot install");
            allowed=true;Pump(600);check(button.Enabled,"install enables after server stops");allowed=false;Pump(600);check(!button.Enabled,"install disables when server starts while browsing");search.Close();
        }
    }
}
