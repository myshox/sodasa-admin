using System.Drawing;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    /// <summary>Hub Form 共用：TabControl 暗色樣式</summary>
    internal static class HubStyle
    {
        public static void Apply(TabControl tc)
        {
            tc.DrawMode  = TabDrawMode.OwnerDrawFixed;
            tc.ItemSize  = new Size(0, 38);
            tc.Padding   = new Point(18, 0);
            tc.BackColor = Theme.BgPage;

            tc.DrawItem += (s, e) =>
            {
                var tab    = tc.TabPages[e.Index];
                bool act   = e.Index == tc.SelectedIndex;
                var bg     = act ? Theme.BgCard : Theme.BgSidebar;
                using var bgBrush = new SolidBrush(bg);
                e.Graphics.FillRectangle(bgBrush, e.Bounds);

                var fg = act ? Theme.TextPrimary : Theme.TextMuted;
                TextRenderer.DrawText(e.Graphics, tab.Text, tc.Font, e.Bounds, fg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                if (act)
                {
                    using var pen = new Pen(Theme.AccentBlue, 3);
                    e.Graphics.DrawLine(pen,
                        e.Bounds.Left,      e.Bounds.Bottom - 2,
                        e.Bounds.Right - 1, e.Bounds.Bottom - 2);
                }
            };
        }
    }
}
