using System;
using System.Drawing;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    /// <summary>
    /// 郵件操作整合頁 — 對應網頁版 BatchOpsPage
    /// Tab 1: 個別發送  Tab 2: 批量發送  Tab 3: 維護工具
    /// </summary>
    public class BatchOpsHubForm : Form
    {
        public BatchOpsHubForm()
        {
            BackColor        = Theme.BgPage;
            ForeColor        = Theme.TextPrimary;
            Font             = Theme.FontBody;
            Dock             = DockStyle.Fill;
            FormBorderStyle  = FormBorderStyle.None;
            Padding          = new Padding(0);

            BuildUI();
        }

        private void BuildUI()
        {
            // ── 頁面標題 ──
            var hdr = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 52,
                BackColor = Theme.BgDark,
                Padding   = new Padding(20, 0, 0, 0)
            };
            hdr.Controls.Add(new Label
            {
                Text      = "  📨  郵件操作",
                ForeColor = Theme.TextPrimary,
                Font      = new Font(Theme.FontFamily, 14, FontStyle.Bold),
                Dock      = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            });
            Controls.Add(hdr);

            // ── TabControl ──
            var tabs = new TabControl
            {
                Dock         = DockStyle.Fill,
                DrawMode     = TabDrawMode.OwnerDrawFixed,
                ItemSize     = new Size(140, 34),
                SizeMode     = TabSizeMode.Fixed,
                Padding      = new Point(0, 0),
                Font         = new Font(Theme.FontFamily, 9.5f, FontStyle.Bold),
                Appearance   = TabAppearance.FlatButtons
            };
            tabs.DrawItem += Tabs_DrawItem;

            // ── Tab 1: 個別發送 ──
            var tabSingle = new TabPage("  📬  個別發送  ")
            {
                BackColor = Theme.BgPage,
                Padding   = new Padding(0)
            };
            EmbedForm(tabSingle, new SendForm());
            tabs.TabPages.Add(tabSingle);

            // ── Tab 2: 批量發送 ──
            var tabBatch = new TabPage("  📢  批量發送  ")
            {
                BackColor = Theme.BgPage,
                Padding   = new Padding(0)
            };
            EmbedForm(tabBatch, new BatchSendForm());
            tabs.TabPages.Add(tabBatch);

            // ── Tab 3: 維護工具 ──
            var tabMaint = new TabPage("  🔧  維護工具  ")
            {
                BackColor = Theme.BgPage,
                Padding   = new Padding(0)
            };
            EmbedForm(tabMaint, new MailClearForm());
            tabs.TabPages.Add(tabMaint);

            Controls.Add(tabs);
        }

        /// <summary>把一個 Form 嵌入到 TabPage 內（WinForms 標準做法）</summary>
        private static void EmbedForm(TabPage page, Form form)
        {
            form.TopLevel        = false;
            form.FormBorderStyle = FormBorderStyle.None;
            form.Dock            = DockStyle.Fill;
            form.BackColor       = Theme.BgPage;
            page.Controls.Add(form);
            form.Show();
        }

        // ── 自訂 Tab 樣式 ──
        private void Tabs_DrawItem(object sender, DrawItemEventArgs e)
        {
            var tabs = (TabControl)sender;
            var tab  = tabs.TabPages[e.Index];
            bool sel = e.Index == tabs.SelectedIndex;

            var bg = sel ? Color.FromArgb(139, 92, 246) : Theme.BgDark;
            using var br = new SolidBrush(bg);
            e.Graphics.FillRectangle(br, e.Bounds);

            // 底部邊線
            if (!sel)
            {
                using var pen = new Pen(Color.FromArgb(50, 50, 60), 1);
                e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }

            var fg   = sel ? Color.White : Theme.TextSecondary;
            var font = new Font(Theme.FontFamily, 9.5f, sel ? FontStyle.Bold : FontStyle.Regular);
            var rect = new RectangleF(e.Bounds.X, e.Bounds.Y + 6, e.Bounds.Width, e.Bounds.Height - 6);
            using var sfg = new SolidBrush(fg);
            e.Graphics.DrawString(tab.Text, font, sfg, rect, new StringFormat { Alignment = StringAlignment.Center });
            font.Dispose();
        }
    }
}
