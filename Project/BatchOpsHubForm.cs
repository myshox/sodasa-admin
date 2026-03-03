using System;
using System.Drawing;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    /// <summary>
    /// 郵件操作整合頁
    /// 頁面 1: 個別發送  頁面 2: 批量發送  頁面 3: 維護工具
    /// </summary>
    public class BatchOpsHubForm : Form
    {
        private Panel _contentPanel;
        private Button[] _navBtns;

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
            // ── 頁面標題列 ──
            var hdr = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 46,
                BackColor = Theme.BgDark,
            };
            hdr.Controls.Add(new Label
            {
                Text      = "  📨  郵件操作",
                ForeColor = Theme.TextPrimary,
                Font      = new Font(Theme.FontFamily, 13, FontStyle.Bold),
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(16, 0, 0, 0)
            });

            // ── 頁籤按鈕列 ──
            var tabBar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 42,
                BackColor = Color.FromArgb(18, 20, 30),
            };
            tabBar.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Color.FromArgb(80, 90, 130) });

            string[] labels = { "  📬  個別發送  ", "  📢  批量全服發送  ", "  🔧  維護工具  " };
            _navBtns = new Button[labels.Length];
            int bx = 8;
            for (int i = 0; i < labels.Length; i++)
            {
                var btn = new Button
                {
                    Text      = labels[i],
                    Font      = new Font(Theme.FontFamily, 9.5f, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(18, 20, 30),
                    ForeColor = Color.FromArgb(160, 170, 200),
                    Height    = 36,
                    Width     = 150,
                    Left      = bx,
                    Top       = 3,
                    Cursor    = Cursors.Hand,
                    TabStop   = false,
                };
                btn.FlatAppearance.BorderSize      = 0;
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 50, 80);
                int idx = i;
                btn.Click += (s, e) => SwitchPage(idx);
                tabBar.Controls.Add(btn);
                _navBtns[i] = btn;
                bx += 155;
            }

            // ── 內容區 ──
            _contentPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgPage };

            // 加入順序：Fill 先加，Top 後加（WinForms docking 規則）
            Controls.Add(_contentPanel);
            Controls.Add(tabBar);
            Controls.Add(hdr);

            // 預設顯示第 1 頁
            SwitchPage(0);
        }

        private Form _currentPage;

        private void SwitchPage(int idx)
        {
            // 更新按鈕樣式
            for (int i = 0; i < _navBtns.Length; i++)
            {
                bool sel = i == idx;
                _navBtns[i].BackColor = sel ? Color.FromArgb(88, 56, 200) : Color.FromArgb(18, 20, 30);
                _navBtns[i].ForeColor = sel ? Color.White : Color.FromArgb(160, 170, 200);
            }

            // 清除舊頁面
            if (_currentPage != null)
            {
                _contentPanel.Controls.Remove(_currentPage);
                _currentPage.Dispose();
                _currentPage = null;
            }

            // 建立新頁面
            Form page = idx switch
            {
                0 => new SendForm(),
                1 => new BatchSendForm(),
                2 => new MailClearForm(),
                _ => new SendForm()
            };

            page.TopLevel        = false;
            page.FormBorderStyle = FormBorderStyle.None;
            page.Dock            = DockStyle.Fill;
            page.BackColor       = Theme.BgPage;
            page.MinimumSize     = Size.Empty;

            _contentPanel.Controls.Add(page);
            page.Show();
            _currentPage = page;
        }
    }
}
