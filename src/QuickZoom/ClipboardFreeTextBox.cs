using System;
using System.Windows.Forms;

namespace QuickZoom;

// Native edit controls access the clipboard themselves. Block their messages
// as well as shortcuts so settings inputs cannot import or export clipboard data.
internal class ClipboardFreeTextBox : TextBox
{
    internal ClipboardFreeTextBox()
    {
        ShortcutsEnabled = false;
        AllowDrop = false;
    }

    protected override void WndProc(ref Message m)
    {
        const int WmContextMenu = 0x007B;
        const int WmCut = 0x0300;
        const int WmCopy = 0x0301;
        const int WmPaste = 0x0302;
        if (m.Msg is WmContextMenu or WmCut or WmCopy or WmPaste)
        {
            m.Result = IntPtr.Zero;
            return;
        }

        base.WndProc(ref m);
    }
}
