// [소스 안내] 업데이트 한 버전의 제목·설명·설치 버튼을 배치하는 카드입니다. 설명과 버튼이 겹치지 않게 영역을 나눕니다.
namespace NeoForgeServerManager;

// Reserve a separate action column and measure wrapped text when the window changes size.
public sealed class ReleaseHistoryCard : SurfaceCard
{
    readonly Label title,notes;readonly WarmButton action;
    public ReleaseHistoryCard(string heading,string description,WarmButton button)
    {
        title=Ui.Heading(heading,18);notes=Ui.Note(description);action=button;
        title.AutoSize=false;notes.AutoSize=false;action.AutoSize=false;
        Controls.AddRange([title,notes,action]);Height=128;
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);if(title==null)return;
        int actionWidth=190,textWidth=Math.Max(120,ClientSize.Width-36-actionWidth-24);
        var flags=TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl;
        int th=TextRenderer.MeasureText(title.Text,title.Font,new Size(textWidth,int.MaxValue),flags).Height+4;
        int nh=TextRenderer.MeasureText(notes.Text,notes.Font,new Size(textWidth,int.MaxValue),flags).Height+6;
        int desired=Math.Max(128,36+th+8+nh);if(Height!=desired)Height=desired;
        title.SetBounds(18,18,textWidth,th);notes.SetBounds(18,18+th+8,textWidth,nh);
        action.SetBounds(ClientSize.Width-18-actionWidth,(Height-54)/2,actionWidth,54);
    }
}
