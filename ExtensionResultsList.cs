// [소스 안내] 온라인 검색 목록의 그리기와 이미지 갱신을 조절합니다. 보이는 항목 중심으로 갱신하여 깜빡임과 불필요한 다시 그리기를 줄입니다.
namespace NeoForgeServerManager;

// Keep native selection/keyboard/scrolling, but paint the visible rows in one buffered frame.
public sealed class ExtensionResultsList : ListBox
{
    readonly HashSet<object> pendingArtwork=[];
    readonly System.Windows.Forms.Timer artworkTimer=new(){Interval=50};
    public ExtensionResultsList()
    {
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
        artworkTimer.Tick+=(_,_)=>FlushArtwork();
    }
    public void QueueArtwork(object item)
    {
        if(IsDisposed||Items.IndexOf(item)<0)return;
        pendingArtwork.Add(item);if(!artworkTimer.Enabled)artworkTimer.Start();
    }
    void FlushArtwork()
    {
        artworkTimer.Stop();if(IsDisposed)return;
        Rectangle changed=Rectangle.Empty;
        foreach(var item in pendingArtwork){int index=Items.IndexOf(item);if(index<0)continue;var row=Rectangle.Intersect(GetItemRectangle(index),ClientRectangle);if(!row.IsEmpty&&row.Width>0&&row.Height>0)changed=changed.IsEmpty?row:Rectangle.Union(changed,row);}
        pendingArtwork.Clear();if(!changed.IsEmpty)Invalidate(changed);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        using(var background=new SolidBrush(BackColor))e.Graphics.FillRectangle(background,e.ClipRectangle);
        for(int i=TopIndex;i<Items.Count;i++)
        {
            var row=GetItemRectangle(i);if(row.Top>=ClientSize.Height)break;
            if(!row.IntersectsWith(e.ClipRectangle))continue;
            var state=i==SelectedIndex?DrawItemState.Selected:DrawItemState.None;
            if(!Enabled)state|=DrawItemState.Disabled;
            OnDrawItem(new DrawItemEventArgs(e.Graphics,Font,row,i,state,ForeColor,BackColor));
        }
    }
    protected override void OnSelectedIndexChanged(EventArgs e){base.OnSelectedIndexChanged(e);Invalidate();}
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        // Native scroll messages move the scroll position; repaint rather than copying partial rows.
        if(m.Msg is 0x115 or 0x20A or 0x197)Invalidate();
    }
    protected override void Dispose(bool disposing){if(disposing){artworkTimer.Stop();artworkTimer.Dispose();pendingArtwork.Clear();}base.Dispose(disposing);}
}
