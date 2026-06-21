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
            Dock            = DockStyle.Fill;
            FormBorderStyle = FormBorderStyle.None;
            Theme.ApplyHubForm(this);
            BuildUI();
        }

        private void BuildUI()
        {
            var pageHeader = Theme.MakeHubPageHeader("📨  郵件操作", Theme.AccentBlue, "個別發送 · 批量全服 · 維護工具");

            // ── 頁籤按鈕列 ──
            var tabBar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = Theme.HubTabBarHeight,
                BackColor = Theme.BgDialogHeader,
                Padding   = new Padding(Theme.UiPadSm, Theme.GapXs, Theme.UiPadSm, Theme.GapXs),
            };
            tabBar.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Color.FromArgb(80, 90, 130) });

            string[] labels = { "  📬  個別發送  ", "  📢  批量全服發送  ", "  🔧  維護工具  " };
            _navBtns = new Button[labels.Length];
            int bx = Theme.UiPadSm;
            for (int i = 0; i < labels.Length; i++)
            {
                var btn = new Button
                {
                    Text      = labels[i],
                    Font      = Theme.FontNav,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Theme.BgCard,
                    ForeColor = Theme.TextSecondary,
                    Height    = Theme.BtnHeightSm,
                    Width     = 168,
                    Left      = bx,
                    Top       = Theme.GapXs,
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

            // 加入順序（WinForms docking 規則）：
            //   Fill 必須「最先加」(z-order 最底)，後加的 Top 由下往上堆疊；
            //   否則 Fill 被夾在兩個 Top 之間時，最後加的 Top(標頭)會蓋住內容區頂端，
            //   把頁面最上方的工具列(如「搜尋收件人」)整條遮掉。
            Controls.Add(_contentPanel);  // Fill 先加
            Controls.Add(pageHeader);     // Top
            Controls.Add(tabBar);         // Top（最後加 = 視覺最頂端）

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

            Theme.ApplyHubForm(page);
            _contentPanel.Controls.Add(page);
            page.Show();
            _currentPage = page;
            page.BeginInvoke(new Action(() => Theme.ApplyComfortableControls(page)));
        }
    }
}
