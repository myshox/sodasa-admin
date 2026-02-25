using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    public class RechargeHistoryForm : Form
    {
        private DataGridView _dgv;
        private TextBox      _searchBox;
        private Label        _lblStatus;
        private Button       _btnSearch;
        private List<RechargeRecord> _records = new();

        public RechargeHistoryForm()
        {
            Text          = "💰 充值記錄";
            Size          = new Size(1150, 640);
            MinimumSize   = new Size(900, 480);
            BackColor     = Theme.BgMid;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;

            BuildUI();
            _ = LoadAsync("");
        }

        private void BuildUI()
        {
            // ── Header ──
            var header = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme.BgDark };
            header.Controls.Add(new Label
            {
                Text      = "  💰  充值記錄  —  查詢玩家儲值訂單",
                ForeColor = Color.FromArgb(140, 220, 140),
                Font      = Theme.FontBody,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(8, 0, 0, 0)
            });

            // ── 搜尋列 ──
            var searchPanel = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Theme.BgCard };
            searchPanel.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme.Border });
            searchPanel.Controls.Add(new Label
            {
                Text = "輸入角色名稱、帳號或商品名稱搜尋",
                ForeColor = Theme.TextMuted, Font = new Font(Theme.FontFamily, 8.5f),
                AutoSize = true, Location = new Point(42, 4)
            });
            var searchIcon = new Label { Text = "🔍", Font = new Font("Segoe UI Emoji", 14f), AutoSize = true, Location = new Point(12, 22) };
            _searchBox = new TextBox
            {
                BackColor = Theme.BgPage, ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle, Font = new Font(Theme.FontFamily, 11f),
                PlaceholderText = "角色名稱、帳號或商品（留空 = 全部）",
                Location = new Point(42, 22), Height = 28,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _searchBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) _ = LoadAsync(_searchBox.Text.Trim()); };
            _btnSearch = Theme.MakePrimaryButton("查詢", 80, 28);
            _btnSearch.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnSearch.Click += (s, e) => _ = LoadAsync(_searchBox.Text.Trim());
            searchPanel.Controls.AddRange(new Control[] { searchIcon, _searchBox, _btnSearch });
            searchPanel.Resize += (s, e) =>
            {
                int pw = searchPanel.ClientSize.Width;
                _btnSearch.Left  = pw - 12 - _btnSearch.Width;
                _btnSearch.Top   = 22;
                _searchBox.Width = Math.Max(100, _btnSearch.Left - _searchBox.Left - 8);
            };

            // ── 說明條 ──
            var infoBar = new Panel { Dock = DockStyle.Top, Height = 26, BackColor = Color.FromArgb(14, 35, 14) };
            infoBar.Controls.Add(new Label
            {
                Text      = "  💡  元寶 = 遊戲內貨幣  |  台幣 = 實際付款金額  |  換算：1 台幣 = 100 元寶",
                ForeColor = Color.FromArgb(100, 200, 100), Font = Theme.FontSmall,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft
            });

            // ── 狀態列 ──
            var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = Theme.BgDark };
            _lblStatus = new Label
            {
                Text = "載入中…", ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0)
            };
            statusBar.Controls.Add(_lblStatus);

            // ── DataGridView ──
            _dgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(_dgv);
            _dgv.RowTemplate.Height = 32;
            _dgv.ReadOnly = true;
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colAt",       HeaderText = "充值時間",       Width = 140 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCharName", HeaderText = "角色名稱",       Width = 110 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colAccount",  HeaderText = "帳號 (cdkey)",   Width = 115 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colProduct",  HeaderText = "充值商品",       Width = 165 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colYuanbao",  HeaderText = "元寶（遊戲幣）", Width = 110 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTwd",      HeaderText = "台幣（÷100）",   Width = 100 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colStatus",   HeaderText = "狀態",           Width = 68  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colOrder",    HeaderText = "訂單編號",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 150 });
            _dgv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                if (_dgv.Rows[e.RowIndex].Tag is not RechargeRecord rec) return;
                var col = _dgv.Columns[e.ColumnIndex].Name;
                if (col == "colYuanbao") { e.CellStyle.ForeColor = Color.FromArgb(80, 200, 255); e.CellStyle.Font = new Font(Theme.FontBody, FontStyle.Bold); e.FormattingApplied = true; }
                else if (col == "colTwd") { e.CellStyle.ForeColor = Color.FromArgb(255, 200, 80); e.CellStyle.Font = new Font(Theme.FontBody, FontStyle.Bold); e.FormattingApplied = true; }
            };

            // 數值感知排序（元寶和台幣欄位用數值比較）
            Theme.AddNumericAwareSort(_dgv, "colYuanbao", "colTwd");

            // 加入順序：Bottom → Fill → Top（後加的 Top 排最上方）
            Controls.Add(statusBar);
            Controls.Add(_dgv);
            Controls.Add(infoBar);
            Controls.Add(searchPanel);
            Controls.Add(header);
        }

        private async Task LoadAsync(string filter)
        {
            _btnSearch.Enabled = false;
            _lblStatus.Text    = "查詢中…";
            _dgv.Rows.Clear();
            try
            {
                _records = await DatabaseManager.Instance.GetRechargeOrdersAsync(filter);
                decimal totalYuanbao = 0, totalTwd = 0;
                foreach (var rec in _records)
                {
                    if (rec.Status == "completed")
                    {
                        totalYuanbao += rec.Amount;
                        totalTwd     += rec.TwdAmount;
                    }
                    int i = _dgv.Rows.Add(
                        rec.CreatedAt,
                        string.IsNullOrEmpty(rec.CharName) ? "—" : rec.CharName,
                        rec.RoleName,
                        rec.ProductName,
                        rec.YuanbaoText, rec.TwdText, rec.StatusText, rec.OrderNo);
                    _dgv.Rows[i].Tag = rec;
                    if (rec.Status == "failed")
                        _dgv.Rows[i].DefaultCellStyle.ForeColor = Theme.TextMuted;
                }
                _lblStatus.Text =
                    $"共 {_records.Count} 筆  |  " +
                    $"合計元寶：{totalYuanbao:N0}  |  " +
                    $"換算台幣：NT$ {totalTwd:N0}（÷100）";
            }
            catch (Exception ex) { _lblStatus.Text = "✗ " + ex.Message; }
            finally { _btnSearch.Enabled = true; }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 交易記錄
    // ══════════════════════════════════════════════════════════════
    public class TradeLogForm : Form
    {
        private DataGridView _dgv;
        private TextBox      _searchBox;
        private Label        _lblStatus;
        private Button       _btnSearch;

        public TradeLogForm()
        {
            Text          = "📊 交易記錄";
            Size          = new Size(1150, 640);
            MinimumSize   = new Size(900, 480);
            BackColor     = Theme.BgMid;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;

            BuildUI();
            _ = LoadAsync("");
        }

        private void BuildUI()
        {
            // ── Header ──
            var header = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme.BgDark };
            header.Controls.Add(new Label
            {
                Text      = "  📊  交易記錄  —  查詢玩家之間的道具 / 金幣交易紀錄",
                ForeColor = Color.FromArgb(180, 200, 255),
                Font      = Theme.FontBody,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(8, 0, 0, 0)
            });

            // ── 搜尋列 ──
            var searchPanel = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Theme.BgCard };
            searchPanel.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme.Border });
            searchPanel.Controls.Add(new Label
            {
                Text = "輸入帳號或角色名稱搜尋（買方 / 賣方皆可）",
                ForeColor = Theme.TextMuted, Font = new Font(Theme.FontFamily, 8.5f),
                AutoSize = true, Location = new Point(42, 4)
            });
            var searchIcon = new Label { Text = "🔍", Font = new Font("Segoe UI Emoji", 14f), AutoSize = true, Location = new Point(12, 22) };
            _searchBox = new TextBox
            {
                BackColor = Theme.BgPage, ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle, Font = new Font(Theme.FontFamily, 11f),
                PlaceholderText = "帳號或角色名稱（留空 = 全部）",
                Location = new Point(42, 22), Height = 28,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _searchBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) _ = LoadAsync(_searchBox.Text.Trim()); };
            _btnSearch = Theme.MakePrimaryButton("查詢", 80, 28);
            _btnSearch.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnSearch.Click += (s, e) => _ = LoadAsync(_searchBox.Text.Trim());
            searchPanel.Controls.AddRange(new Control[] { searchIcon, _searchBox, _btnSearch });
            searchPanel.Resize += (s, e) =>
            {
                int pw = searchPanel.ClientSize.Width;
                _btnSearch.Left  = pw - 12 - _btnSearch.Width;
                _btnSearch.Top   = 22;
                _searchBox.Width = Math.Max(100, _btnSearch.Left - _searchBox.Left - 8);
            };

            // ── 狀態列 ──
            var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = Theme.BgDark };
            _lblStatus = new Label
            {
                Text = "載入中…", ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0)
            };
            statusBar.Controls.Add(_lblStatus);

            // ── DataGridView ──
            _dgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(_dgv);
            _dgv.RowTemplate.Height = 32;
            _dgv.ReadOnly = true;
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTime",     HeaderText = "交易時間", Width = 140 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colFromName", HeaderText = "賣方角色", Width = 100 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colFromAcc",  HeaderText = "賣方帳號", Width = 110 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colToName",   HeaderText = "買方角色", Width = 100 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colToAcc",    HeaderText = "買方帳號", Width = 110 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colType",     HeaderText = "類型",     Width = 68  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colContent",  HeaderText = "交易內容",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 180 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colGold",     HeaderText = "金幣",     Width = 85  });

            Theme.AddNumericAwareSort(_dgv, "colGold");

            // 加入順序：Bottom → Fill → Top
            Controls.Add(statusBar);
            Controls.Add(_dgv);
            Controls.Add(searchPanel);
            Controls.Add(header);
        }

        private async Task LoadAsync(string filter)
        {
            _btnSearch.Enabled = false;
            _lblStatus.Text    = "查詢中…";
            _dgv.Rows.Clear();
            try
            {
                var records = await DatabaseManager.Instance.GetTradeLogAsync(filter);
                foreach (var rec in records)
                {
                    int i = _dgv.Rows.Add(
                        rec.Time,
                        string.IsNullOrEmpty(rec.FromName) ? "—" : rec.FromName,
                        rec.FromCdkey,
                        string.IsNullOrEmpty(rec.ToName)   ? "—" : rec.ToName,
                        rec.ToCdkey,
                        rec.TypeText, rec.ContentSummary, rec.GoldText);
                    _dgv.Rows[i].Tag = rec;
                }
                _lblStatus.Text = $"共 {records.Count} 筆交易記錄";
            }
            catch (Exception ex) { _lblStatus.Text = "✗ " + ex.Message; }
            finally { _btnSearch.Enabled = true; }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 金幣異動日誌
    // ══════════════════════════════════════════════════════════════
    public class GoldLogForm : Form
    {
        private DataGridView _dgv;
        private TextBox      _searchBox;
        private Label        _lblStatus;
        private Button       _btnSearch;

        public GoldLogForm()
        {
            Text          = "💎 金幣異動日誌";
            Size          = new Size(1050, 640);
            MinimumSize   = new Size(700, 460);
            BackColor     = Theme.BgMid;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;

            BuildUI();
            _ = LoadAsync("");
        }

        private void BuildUI()
        {
            // ── Header ──
            var header = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme.BgDark };
            header.Controls.Add(new Label
            {
                Text      = "  💎  金幣異動日誌  —  查詢所有玩家金幣增減記錄",
                ForeColor = Color.FromArgb(255, 210, 100),
                Font      = Theme.FontBody,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(8, 0, 0, 0)
            });

            // ── 搜尋列 ──
            var searchPanel = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Theme.BgCard };
            searchPanel.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme.Border });
            searchPanel.Controls.Add(new Label
            {
                Text = "輸入角色名稱、帳號或異動原因搜尋",
                ForeColor = Theme.TextMuted, Font = new Font(Theme.FontFamily, 8.5f),
                AutoSize = true, Location = new Point(42, 4)
            });
            var searchIcon = new Label { Text = "🔍", Font = new Font("Segoe UI Emoji", 14f), AutoSize = true, Location = new Point(12, 22) };
            _searchBox = new TextBox
            {
                BackColor = Theme.BgPage, ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle, Font = new Font(Theme.FontFamily, 11f),
                PlaceholderText = "角色名稱、帳號或原因（留空 = 全部）",
                Location = new Point(42, 22), Height = 28,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _searchBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) _ = LoadAsync(_searchBox.Text.Trim()); };
            _btnSearch = Theme.MakePrimaryButton("查詢", 80, 28);
            _btnSearch.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnSearch.Click += (s, e) => _ = LoadAsync(_searchBox.Text.Trim());
            searchPanel.Controls.AddRange(new Control[] { searchIcon, _searchBox, _btnSearch });
            searchPanel.Resize += (s, e) =>
            {
                int pw = searchPanel.ClientSize.Width;
                _btnSearch.Left  = pw - 12 - _btnSearch.Width;
                _btnSearch.Top   = 32;
                _searchBox.Width = Math.Max(100, _btnSearch.Left - _searchBox.Left - 8);
            };

            // ── 狀態列 ──
            var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = Theme.BgDark };
            _lblStatus = new Label
            {
                Text = "載入中…", ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0)
            };
            statusBar.Controls.Add(_lblStatus);

            // ── DataGridView ──
            _dgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(_dgv);
            _dgv.RowTemplate.Height = 32;
            _dgv.ReadOnly = true;
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTime",     HeaderText = "時間",           Width = 140 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCharName", HeaderText = "角色名稱",       Width = 110 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCdkey",    HeaderText = "帳號 (cdkey)",   Width = 115 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPoint",    HeaderText = "異動量（元寶）", Width = 105 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colOld",      HeaderText = "異動前餘額",     Width = 100 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colNew",      HeaderText = "異動後餘額",     Width = 100 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colBuff",     HeaderText = "異動原因",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 160 });
            _dgv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || _dgv.Columns[e.ColumnIndex].Name != "colPoint") return;
                if (_dgv.Rows[e.RowIndex].Tag is not GoldLogRecord rec) return;
                e.CellStyle.ForeColor = rec.Point >= 0 ? Color.FromArgb(80, 220, 100) : Color.FromArgb(230, 80, 80);
                e.CellStyle.Font = new Font(Theme.FontBody, FontStyle.Bold);
                e.FormattingApplied = true;
            };

            Theme.AddNumericAwareSort(_dgv, "colPoint", "colOld", "colNew");

            // 加入順序：Bottom → Fill → Top
            Controls.Add(statusBar);
            Controls.Add(_dgv);
            Controls.Add(searchPanel);
            Controls.Add(header);
        }

        private async Task LoadAsync(string filter)
        {
            _btnSearch.Enabled = false;
            _lblStatus.Text    = "查詢中…";
            _dgv.Rows.Clear();
            try
            {
                var records = await DatabaseManager.Instance.GetGoldLogAsync(filter);
                foreach (var rec in records)
                {
                    int i = _dgv.Rows.Add(
                        rec.Time,
                        string.IsNullOrEmpty(rec.CharName) ? "—" : rec.CharName,
                        rec.Cdkey,
                        rec.PointText, $"{rec.OldPoint:N0}", $"{rec.NewPoint:N0}", rec.Buff);
                    _dgv.Rows[i].Tag = rec;
                }
                _lblStatus.Text = $"共 {records.Count} 筆異動記錄";
            }
            catch (Exception ex) { _lblStatus.Text = "✗ " + ex.Message; }
            finally { _btnSearch.Enabled = true; }
        }
    }
}
