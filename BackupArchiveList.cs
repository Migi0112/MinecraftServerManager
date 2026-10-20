// [소스 안내] 백업 파일 목록을 표시하는 전용 컨트롤입니다.
namespace NeoForgeServerManager;

// 기본 표 대신 실제 둥근 버튼을 가진 스크롤 가능한 백업 행입니다.
public sealed class BackupArchiveList : Panel
{
    public event Action<string,bool>? FileAction;
    public BackupArchiveList(){AutoScroll=true;BackColor=WarmTheme.Surface;Resize+=(_,_)=>Arrange();}
    public void SetFiles(IEnumerable<FileInfo> files)
    {
        SuspendLayout();
        foreach(Control c in Controls.Cast<Control>().ToArray())c.Dispose();
        foreach(var file in files)
        {
            var row=new SurfaceCard{Height=68,Padding=Padding.Empty,Tag=file.FullName};
            var name=Ui.Note(file.Name);name.ForeColor=WarmTheme.Ink;name.AutoEllipsis=true;
            var size=Ui.Note($"{file.Length/1048576d:F1} MB");
            var restore=Ui.Action("복원",()=>FileAction?.Invoke(file.FullName,true));
            var delete=Ui.Action("삭제",()=>FileAction?.Invoke(file.FullName,false));delete.TextColor=WarmTheme.Accent;delete.OutlineColor=WarmTheme.Accent;
            row.Controls.AddRange([name,size,restore,delete]);
            row.Layout+=(_,_)=>{name.SetBounds(16,12,Math.Max(60,row.Width-350),44);size.SetBounds(row.Width-320,12,100,44);restore.SetBounds(row.Width-208,12,92,44);delete.SetBounds(row.Width-106,12,92,44);};
            Controls.Add(row);
        }
        if(Controls.Count==0){var empty=Ui.Note("아직 백업이 없어요. ‘지금 백업’으로 첫 백업을 만들어 보세요.");empty.TextAlign=ContentAlignment.MiddleCenter;Controls.Add(empty);}
        ResumeLayout();Arrange();
    }
    void Arrange()
    {
        int y=AutoScrollPosition.Y;
        foreach(Control row in Controls){row.SetBounds(0,y,Math.Max(100,ClientSize.Width-22),68);y+=78;}
        AutoScrollMinSize=new Size(0,Controls.Count*78);
    }
}
