using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    /// <summary>VIP 玩家管理介面</summary>
    public class VipForm : Form
    {
        private DataGridView _dgv;
        private Label        _lblStatus;
        private Label        _lblGoldCount, _lblDiamondCount;
        private Button       _btnAll, _btnGold, _btnDiamond;
        private List<PlayerInfo> _all = new();
        private int _filter = 0; // 0=全部, 1=黃金, 2=鑽石

        public VipForm()
        {
            Text          = "💎 VIP 玩家管理";
            Size          = new Size(980, 680);
            MinimumSize   = new Size(750, 480);
            BackColor     = Theme.BgMid;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;
            BuildUI();
            _ = LoadAsync();
        }

        private void BuildUI()
        {
            // ── 標題列 ──────────────────────────────────────────────────
            var header = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme.BgDark };
            header.Controls.Add(new Label
            {
                Text      = "  💎  VIP 玩家管理  —  黃金 / 鑽石 VIP 特權與回饋加成",
                ForeColor = Color.FromArgb(255, 215, 80),
                Font      = Theme.FontBody,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(8, 0, 0, 0)
            });

            // ── VIP 等級說明卡片 ────────────────────────────────────────
            var infoPanel = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 76,
                BackColor = Color.FromArgb(28, 22, 8),
                Padding   = new Padding(16, 8, 16, 8)
            };

            // 黃金 VIP 說明
            var goldCard = MakeInfoCard(
                "🔸 黃金 VIP",
                $"累計儲值達 NT$ {VipHelper.GoldThreshold:N0}",
                $"+{VipHelper.GoldBonus * 100:0}% 金幣回饋",
                Color.FromArgb(60, 44, 8),
                Color.FromArgb(255, 200, 60));

            // 鑽石 VIP 說明
            var diamCard = MakeInfoCard(
                "🔹 鑽石 VIP",
                $"累計儲值達 NT$ {VipHelper.DiamondThreshold:N0}",
                $"+{VipHelper.DiamondBonus * 100:0}% 金幣回饋",
                Color.FromArgb(8, 28, 55),
                Color.FromArgb(100, 180, 255));

            goldCard.Location = new Point(16, 8);
            diamCard.Location = new Point(322, 8);
            infoPanel.Controls.AddRange(new Control[] { goldCard, diamCard });
            infoPanel.Resize += (s, e) =>
            {
                int half = (infoPanel.ClientSize.Width - 48) / 2;
                goldCard.Width = half;
                diamCard.Width = half;
                diamCard.Left  = goldCard.Right + 16;
            };

            // ── 篩選 & 統計列 ───────────────────────────────────────────
            var filterPanel = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 48,
                BackColor = Theme.BgCard,
                Padding   = new Padding(12, 8, 12, 8)
            };
            var lblFilter = new Label
            {
                Text      = "篩選：",
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                AutoSize  = true,
                Location  = new Point(14, 15)
            };
            _btnAll     = MakeFilterBtn("全部 VIP",  0, 80);
            _btnGold    = MakeFilterBtn("🔸 黃金",   1, 75);
            _btnDiamond = MakeFilterBtn("🔹 鑽石",   2, 75);
            _btnAll.Location     = new Point(60,  10);
            _btnGold.Location    = new Point(148, 10);
            _btnDiamond.Location = new Point(231, 10);

            _lblGoldCount = new Label
            {
                Text      = "黃金：─",
                ForeColor = Color.FromArgb(255, 200, 60),
                Font      = Theme.FontSmall,
                AutoSize  = true,
                Location  = new Point(340, 16)
            };
            _lblDiamondCount = new Label
            {
                Text      = "鑽石：─",
                ForeColor = Color.FromArgb(100, 180, 255),
                Font      = Theme.FontSmall,
                AutoSize  = true,
                Location  = new Point(430, 16)
            };

            var btnRefresh = Theme.MakePrimaryButton("🔄 重整", 80, 28);
            btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnRefresh.Click += (s, e) => _ = LoadAsync();

            filterPanel.Controls.AddRange(new Control[]
            { lblFilter, _btnAll, _btnGold, _btnDiamond, _lblGoldCount, _lblDiamondCount, btnRefresh });
            filterPanel.Resize += (s, e) =>
                btnRefresh.Left = filterPanel.ClientSize.Width - 12 - btnRefresh.Width;

            // ── 狀態列 ──────────────────────────────────────────────────
            var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = Theme.BgDark };
            _lblStatus = new Label
            {
                Text      = "載入中…",
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(12, 0, 0, 0)
            };
            statusBar.Controls.Add(_lblStatus);

            // ── DataGridView ─────────────────────────────────────────────
            _dgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(_dgv);
            _dgv.RowTemplate.Height = 34;
            _dgv.ReadOnly           = true;

            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cVip",       HeaderText = "VIP 等級",    Width = 110 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cName",      HeaderText = "角色名稱",    Width = 120 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cAccount",   HeaderText = "帳號 (cdkey)", Width = 120 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cMaster",    HeaderText = "👑 主帳號",    Width = 100 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cPay",       HeaderText = "累計儲值 (NT$)", Width = 120 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cBonus",     HeaderText = "金幣回饋加成", Width = 100 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cGapToNext", HeaderText = "距下一等級",   Width = 110 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cOnline",    HeaderText = "狀態",         Width = 72 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cLogin",
                HeaderText = "最後登入", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

            _dgv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                if (_dgv.Rows[e.RowIndex].Tag is not PlayerInfo p) return;
                var col = _dgv.Columns[e.ColumnIndex].Name;
                var (_, _, _, rate) = VipHelper.GetTier(p.PayTotal);
                if (col == "cVip")
                {
                    e.CellStyle.ForeColor = p.PayTotal >= VipHelper.DiamondThreshold
                        ? Color.FromArgb(100, 180, 255)
                        : Color.FromArgb(255, 200, 60);
                    e.CellStyle.Font = new Font(Theme.FontFamily, 10f, FontStyle.Bold);
                    e.FormattingApplied = true;
                }
                if (col == "cBonus" && rate > 0)
                {
                    e.CellStyle.ForeColor = Color.FromArgb(80, 220, 120);
                    e.CellStyle.Font = new Font(Theme.FontFamily, 10f, FontStyle.Bold);
                    e.FormattingApplied = true;
                }
                if (col == "cPay")
                {
                    e.CellStyle.ForeColor = Color.FromArgb(255, 200, 60);
                    e.FormattingApplied = true;
                }
                if (col == "cOnline")
                {
                    e.CellStyle.ForeColor = p.IsOnline ? Theme.AccentGreen : Theme.TextMuted;
                    e.FormattingApplied = true;
                }
                if (col == "cGapToNext")
                {
                    e.CellStyle.ForeColor = Theme.TextMuted;
                    e.FormattingApplied = true;
                }
            };

            Theme.AddNumericAwareSort(_dgv, "cPay", "cBonus");

            SetActiveFilter(0);

            Controls.Add(_dgv);
            Controls.Add(statusBar);
            Controls.Add(filterPanel);
            Controls.Add(infoPanel);
            Controls.Add(header);
        }

        // ── 篩選按鈕輔助 ────────────────────────────────────────────────
        private Button MakeFilterBtn(string text, int tag, int w)
        {
            var btn = new Button
            {
                Text      = text,
                Tag       = tag,
                Size      = new Size(w, 28),
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand,
                Font      = Theme.FontSmall,
                UseVisualStyleBackColor = false
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.Click += (s, e) =>
            {
                _filter = (int)btn.Tag;
                SetActiveFilter(_filter);
                RefreshGrid();
            };
            return btn;
        }

        private void SetActiveFilter(int active)
        {
            foreach (var b in new[] { _btnAll, _btnGold, _btnDiamond })
            {
                bool on = (int)b.Tag == active;
                b.BackColor = on ? Theme.AccentBlue : Theme.BgLight;
                b.ForeColor = on ? Color.White       : Theme.TextSecondary;
                b.FlatAppearance.BorderColor = on ? Theme.AccentBlue : Theme.Border;
            }
        }

        // ── 資訊卡片輔助 ────────────────────────────────────────────────
        private static Panel MakeInfoCard(string title, string threshold, string bonus,
                                          Color bg, Color accent)
        {
            var p = new Panel
            {
                Size      = new Size(290, 58),
                BackColor = bg,
                Padding   = new Padding(12, 6, 12, 6)
            };
            p.Controls.Add(new Label
            {
                Text      = title,
                ForeColor = accent,
                Font      = new Font(Theme.FontFamily, 11f, FontStyle.Bold),
                AutoSize  = true,
                Location  = new Point(12, 6)
            });
            p.Controls.Add(new Label
            {
                Text      = threshold + "   " + bonus,
                ForeColor = Color.FromArgb(210, 210, 210),
                Font      = Theme.FontSmall,
                AutoSize  = true,
                Location  = new Point(12, 32)
            });
            return p;
        }

        // ── 載入 ────────────────────────────────────────────────────────
        private async System.Threading.Tasks.Task LoadAsync()
        {
            _lblStatus.Text = "查詢中…";
            _dgv.Rows.Clear();
            try
            {
                _all = await DatabaseManager.Instance.GetVipPlayersAsync();
                int goldCnt    = _all.Count(p => VipHelper.GetTier(p.PayTotal).level == 1);
                int diamondCnt = _all.Count(p => VipHelper.GetTier(p.PayTotal).level == 2);
                _lblGoldCount.Text    = $"黃金：{goldCnt} 人";
                _lblDiamondCount.Text = $"鑽石：{diamondCnt} 人";
                RefreshGrid();
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "✗ " + ex.Message;
            }
        }

        private void RefreshGrid()
        {
            _dgv.Rows.Clear();
            var list = _filter == 0 ? _all
                     : _all.Where(p => VipHelper.GetTier(p.PayTotal).level == _filter).ToList();

            foreach (var p in list)
            {
                var (_, emoji, label, rate) = VipHelper.GetTier(p.PayTotal);
                long gap  = VipHelper.GapToNext(p.PayTotal);
                string gapText = gap < 0 ? "🏆 已達最高" : $"還差 NT$ {gap:N0}";
                string bonus   = rate > 0 ? $"+{rate * 100:0}% 金幣回饋" : "—";

                int i = _dgv.Rows.Add(
                    $"{emoji} {label}",
                    p.OnlineName,
                    p.Account,
                    string.IsNullOrEmpty(p.MasterName) ? "—" : p.MasterName,
                    $"NT$ {p.PayTotal:N0}",
                    bonus,
                    gapText,
                    p.IsOnline ? "🟢 在線" : "⚫ 離線",
                    p.LoginTime);
                _dgv.Rows[i].Tag = p;
            }

            int total = list.Count;
            _lblStatus.Text = total == 0
                ? "目前沒有符合條件的 VIP 玩家"
                : $"共 {total} 名 VIP 玩家  |  " +
                  $"黃金：{_all.Count(x => VipHelper.GetTier(x.PayTotal).level == 1)} 人  ·  " +
                  $"鑽石：{_all.Count(x => VipHelper.GetTier(x.PayTotal).level == 2)} 人";
        }
    }
}
