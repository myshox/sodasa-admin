using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    /// <summary>消費達成獎勵管理（costdata）— 與累積儲值 paydata 對稱</summary>
    public class CostMilestoneForm : Form
    {
        private static readonly long[] Milestones = DatabaseManager.CostMilestones;

        // ── 搜尋列 ────────────────────────────────────────────────
        private TextBox  _txtSearch;
        private Button   _btnSearch;
        private Label    _lblStatus;

        // ── 玩家資訊 ─────────────────────────────────────────────
        private Panel  _infoPanel;
        private Label  _lblName, _lblProgress, _lblCheck;
        private Panel  _progressBarFill;

        // ── 里程碑卡片 ────────────────────────────────────────────
        private Panel  _milestonesPanel;

        // ── 操作區 ────────────────────────────────────────────────
        private NumericUpDown _nudAdd;
        private Button        _btnAdd, _btnReset;

        private string _currentAccount    = "";
        private string _currentOnlineName = "";
        private long   _currentPoint      = 0;
        private int    _currentCheck      = -1;

        public CostMilestoneForm()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            Text        = "💸 消費達成獎勵";
            BackColor   = Theme.BgPage;
            ForeColor   = Theme.TextPrimary;
            Font        = Theme.FontBody;
            MinimumSize = new Size(820, 560);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1,
                BackColor = Color.Transparent, Padding = new Padding(16, 10, 16, 10)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));  // 標題
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));  // 搜尋列
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90f));  // 玩家資訊 + 進度條
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 160f)); // 里程碑卡片
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // 操作區
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            // ── Row 0：標題 ──────────────────────────────────────
            var titleRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
                BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 6)
            };
            titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            titleRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            titleRow.Controls.Add(new Label
            {
                Text = "消費達成獎勵管理", ForeColor = Color.FromArgb(180, 130, 255),
                Font = new Font(Theme.FontFamily, 14f, FontStyle.Bold),
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            _lblStatus = new Label
            {
                Text = "里程碑：3,000 / 5,000 / 10,000 / 50,000 / 100,000 金幣",
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight
            };
            titleRow.Controls.Add(_lblStatus, 1, 0);
            root.Controls.Add(titleRow, 0, 0);

            // ── Row 1：搜尋列 ────────────────────────────────────
            var searchRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
                BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 6)
            };
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _txtSearch = new TextBox
            {
                Dock = DockStyle.Fill, BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary,
                Font = Theme.FontBody, BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "輸入玩家帳號…"
            };
            _txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) _ = SearchAsync(); };
            searchRow.Controls.Add(_txtSearch, 0, 0);

            _btnSearch = Theme.MakePrimaryButton("🔍 查詢", 90, 34);
            _btnSearch.Margin = new Padding(8, 0, 0, 0);
            _btnSearch.Click += async (s, e) => await SearchAsync();
            searchRow.Controls.Add(_btnSearch, 1, 0);
            root.Controls.Add(searchRow, 0, 1);

            // ── Row 2：玩家資訊 + 進度條 ────────────────────────
            _infoPanel = new Panel
            {
                Dock = DockStyle.Fill, BackColor = Theme.BgCard,
                Padding = new Padding(14, 8, 14, 8), Visible = false
            };

            var infoGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
                BackColor = Color.Transparent
            };
            infoGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
            infoGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
            infoGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            infoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            _lblName = new Label { Dock = DockStyle.Fill, ForeColor = Theme.TextPrimary, Font = new Font(Theme.FontFamily, 11f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
            _lblCheck = new Label { Dock = DockStyle.Fill, ForeColor = Theme.TextMuted, Font = Theme.FontSmall, TextAlign = ContentAlignment.MiddleLeft };
            infoGrid.Controls.Add(_lblName,  0, 0);
            infoGrid.Controls.Add(_lblCheck, 0, 1);

            // 進度條
            var barWrap = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgMid, Margin = new Padding(0, 2, 0, 0) };
            _progressBarFill = new Panel { BackColor = Color.FromArgb(150, 80, 255), Dock = DockStyle.Left, Width = 0 };
            barWrap.Controls.Add(_progressBarFill);
            infoGrid.Controls.Add(barWrap, 0, 2);

            _infoPanel.Controls.Add(infoGrid);
            root.Controls.Add(_infoPanel, 0, 2);

            // ── Row 3：里程碑卡片（FlowLayoutPanel）─────────────
            _milestonesPanel = new Panel
            {
                Dock = DockStyle.Fill, BackColor = Color.Transparent, Visible = false
            };
            root.Controls.Add(_milestonesPanel, 0, 3);

            // ── Row 4：操作區 ─────────────────────────────────────
            var opPanel = new Panel
            {
                Dock = DockStyle.Fill, BackColor = Theme.BgCard,
                Padding = new Padding(14, 10, 14, 10), Visible = false
            };
            opPanel.Tag = "opPanel";

            var opGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1,
                BackColor = Color.Transparent
            };
            opGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            opGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130f));
            opGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            opGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            opGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            opGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            opGrid.Controls.Add(new Label
            {
                Text = "增加消費點數：", ForeColor = Theme.TextSecondary, Font = Theme.FontBody,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 0, 8, 0)
            }, 0, 0);

            _nudAdd = new NumericUpDown
            {
                Minimum = 1, Maximum = 10_000_000, Value = 1000, Increment = 1000,
                BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary, Font = Theme.FontBody,
                Dock = DockStyle.Fill, Margin = new Padding(0, 4, 8, 4),
                ThousandsSeparator = true
            };
            opGrid.Controls.Add(_nudAdd, 1, 0);

            _btnAdd = Theme.MakePrimaryButton("➕ 確認增加", 110, 32);
            _btnAdd.Margin = new Padding(0, 4, 8, 4);
            _btnAdd.Click += async (s, e) => await DoAdjustAsync();
            opGrid.Controls.Add(_btnAdd, 2, 0);

            _btnReset = Theme.MakeButton("🗑 重置進度", Color.FromArgb(120, 30, 30), Color.FromArgb(255, 140, 140), 100, 32);
            _btnReset.Margin = new Padding(0, 4, 0, 4);
            _btnReset.Click += async (s, e) => await DoResetAsync();
            opGrid.Controls.Add(_btnReset, 3, 0);

            opPanel.Controls.Add(opGrid);
            root.Controls.Add(opPanel, 0, 4);

            Controls.Add(root);
        }

        private async Task SearchAsync()
        {
            string acc = _txtSearch.Text.Trim();
            if (string.IsNullOrEmpty(acc)) return;
            _btnSearch.Enabled = false;
            _lblStatus.Text    = "查詢中…";
            try
            {
                var (pt, ck, uid, onlineName) = await DatabaseManager.Instance.GetCostDataAsync(acc);
                _currentAccount = uid;
                _currentPoint   = pt;
                _currentCheck   = ck;
                _currentOnlineName = onlineName;
                UpdateUI();
            }
            catch (Exception ex) { _lblStatus.Text = "✗ " + ex.Message; }
            finally { _btnSearch.Enabled = true; }
        }

        private void UpdateUI()
        {
            string display = string.IsNullOrEmpty(_currentOnlineName)
                ? _currentAccount
                : $"{_currentOnlineName}（{_currentAccount}）";
            _lblName.Text  = $"玩家：{display}";
            int claimedCount = _currentCheck < 0 ? 0 : System.Numerics.BitOperations.PopCount((uint)_currentCheck);
            _lblCheck.Text = _currentCheck < 0
                ? "（無 costdata 記錄）"
                : $"累計消費：{_currentPoint:N0} 金幣　已領取：{claimedCount}/5 個里程碑獎勵（check bitmask={_currentCheck}）";

            // 進度條
            float pct = (float)Math.Min(_currentPoint, 100_000L) / 100_000L;
            _progressBarFill.Width = 0;
            _progressBarFill.Parent?.Invoke(new Action(() =>
            {
                _progressBarFill.Width = Math.Max(0, (int)(_progressBarFill.Parent.Width * pct));
            }));

            _infoPanel.Visible  = true;
            _milestonesPanel.Visible = true;

            // 找到 opPanel
            foreach (Control c in Controls[0].Controls)
                if (c.Tag?.ToString() == "opPanel") c.Visible = true;

            BuildMilestoneCards();
            _lblStatus.Text = $"最後更新 {DateTime.Now:HH:mm:ss}";
        }

        private void BuildMilestoneCards()
        {
            _milestonesPanel.Controls.Clear();
            int cardW = Math.Max(130, (_milestonesPanel.Width - Milestones.Length * 8) / Milestones.Length);
            cardW = Math.Min(cardW, 200);
            int x = 0;
            int h = _milestonesPanel.Height - 4;

            for (int i = 0; i < Milestones.Length; i++)
            {
                bool reached  = _currentPoint >= Milestones[i];
                int  bit      = 1 << i;
                bool claimed  = _currentCheck >= 0 && (_currentCheck & bit) != 0;
                bool canClaim = reached && !claimed;

                Color accent = claimed  ? Color.FromArgb(22, 183, 120)
                             : canClaim ? Color.FromArgb(251, 191, 36)
                             :            Color.FromArgb(71, 85, 105);

                var card = new TableLayoutPanel
                {
                    BackColor = Theme.BgCard,
                    Size = new Size(cardW, h),
                    Location = new Point(x, 0),
                    RowCount = 3, ColumnCount = 1,
                    Padding = new Padding(10, 8, 10, 8),
                    CellBorderStyle = TableLayoutPanelCellBorderStyle.None
                };
                if (canClaim)
                    card.BackColor = Color.FromArgb(40, 35, 15); // 金黃色調背景
                card.RowStyles.Add(new RowStyle(SizeType.Absolute, 20f));
                card.RowStyles.Add(new RowStyle(SizeType.Percent,  100f));
                card.RowStyles.Add(new RowStyle(SizeType.Absolute, claimed ? 0f : canClaim ? 30f : 0f));
                card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

                // 狀態文字
                string stateText = claimed ? "✅ 已領取" : canClaim ? "🎁 可領取" : "🔒 未達成";
                card.Controls.Add(new Label
                {
                    Text = $"里程碑 {i + 1}  {stateText}", ForeColor = accent,
                    Font = Theme.FontSmall, Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.BottomLeft
                }, 0, 0);

                // 數字
                card.Controls.Add(new Label
                {
                    Text = $"{Milestones[i]:N0}\n金幣",
                    ForeColor = accent, Font = new Font(Theme.FontFamily, 13f, FontStyle.Bold),
                    Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft
                }, 0, 1);

                // 補發按鈕
                if (canClaim)
                {
                    int idx = i;
                    var btnClaim = Theme.MakeButton("🎁 補發", Color.FromArgb(100, 70, 0), Color.FromArgb(255, 200, 60), cardW - 20, 24);
                    btnClaim.Font = Theme.FontSmall;
                    btnClaim.Dock = DockStyle.Fill;
                    btnClaim.Margin = new Padding(0);
                    btnClaim.Click += async (s, e) => await DoClaimAsync(idx);
                    card.Controls.Add(btnClaim, 0, 2);
                }

                _milestonesPanel.Controls.Add(card);
                x += cardW + 8;
            }
        }

        private async Task DoAdjustAsync()
        {
            if (string.IsNullOrEmpty(_currentAccount)) return;
            long add = (long)_nudAdd.Value;
            if (MessageBox.Show($"確定增加「{_currentAccount}」{add:N0} 消費點數？",
                "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _btnAdd.Enabled = false;
            try
            {
                bool ok = await DatabaseManager.Instance.AdjustCostDataPointAsync(_currentAccount, "", add);
                if (ok)
                {
                    MessageBox.Show($"✅ 已增加 {add:N0} 消費點數。", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    await SearchAsync();
                }
            }
            finally { _btnAdd.Enabled = true; }
        }

        private async Task DoResetAsync()
        {
            if (string.IsNullOrEmpty(_currentAccount)) return;
            if (MessageBox.Show($"確定重置「{_currentAccount}」消費達成進度？\n此操作無法復原！",
                "確認重置", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            _btnReset.Enabled = false;
            try
            {
                bool ok = await DatabaseManager.Instance.ResetCostDataAsync(_currentAccount);
                if (ok) { MessageBox.Show("✅ 消費進度已歸零。", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information); await SearchAsync(); }
                else     MessageBox.Show("⚠ 重置失敗（可能無 costdata 記錄）。", "失敗", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { _btnReset.Enabled = true; }
        }

        private async Task DoClaimAsync(int milestoneIdx)
        {
            if (string.IsNullOrEmpty(_currentAccount)) return;
            using var dlg = new CostClaimDialog(_currentAccount, milestoneIdx, Milestones[milestoneIdx]);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            bool ok;
            if (dlg.UseMailMode)
            {
                ok = await DatabaseManager.Instance.ClaimCostMilestoneByMailAsync(
                    _currentAccount, _currentAccount, milestoneIdx,
                    dlg.ItemId, dlg.ItemName, dlg.ItemQty);
                if (ok) MessageBox.Show(
                    $"✅ 已寄出道具（ID:{dlg.ItemId} x{dlg.ItemQty:N0}）並標記第 {milestoneIdx + 1} 里程碑已領取。\n玩家下次登入信箱可領取。",
                    "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                ok = await DatabaseManager.Instance.ClaimCostMilestoneAsync(_currentAccount, milestoneIdx);
                if (ok) MessageBox.Show(
                    $"✅ 已退回 check={milestoneIdx}，遊戲伺服器下次偵測時將自動發放道具到背包。",
                    "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            if (ok) await SearchAsync();
        }
    }
}
