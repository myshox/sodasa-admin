using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    public class ShopStatsForm : Form
    {
        private TabControl _tabs;
        private Label _lblStatus;

        // 各商城設定
        private static readonly (string Title, string Table, string Icon, string Unit, Color Accent)[] Shops =
        {
            ("金幣商店",   "vipshop",     "💰", "金幣",  Color.FromArgb(255, 200,  60)),
            ("聲望商店",   "fameshop",    "🏆", "聲望",  Color.FromArgb(100, 200, 255)),
            ("石壁商店",   "csshopnum",   "🪨", "石壁",  Color.FromArgb(180, 145, 100)),
            ("戰點商店",   "csxsshopnum", "⚔",  "戰點",  Color.FromArgb(230, 100, 100)),
        };

        public ShopStatsForm()
        {
            Text          = "🏪 商城熱賣分析";
            Size          = new Size(1050, 680);
            MinimumSize   = new Size(800, 500);
            BackColor = Theme.BgPage;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;

            BuildUI();
            _ = LoadAllAsync();
        }

        private void BuildUI()
        {
            // ── Header ──
            var header = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme.BgDark };
            var btnRefresh = Theme.MakePrimaryButton("🔄 重新整理", 110, 28);
            btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnRefresh.Click += (s, e) => _ = LoadAllAsync();
            header.Controls.Add(new Label
            {
                Text      = "  🏪  商城熱賣分析  —  統計各商城最熱賣道具 & 消費最多玩家",
                ForeColor = Color.FromArgb(255, 200, 80),
                Font      = Theme.FontBody,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(8, 0, 0, 0)
            });
            header.Controls.Add(btnRefresh);
            header.Resize += (s, e) => { btnRefresh.Left = header.Width - btnRefresh.Width - 12; btnRefresh.Top = 8; };
            Controls.Add(header);

            // ── 狀態列 ──
            var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = Theme.BgDark };
            _lblStatus = new Label
            {
                Text = "載入中…", ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0)
            };
            statusBar.Controls.Add(_lblStatus);
            Controls.Add(statusBar);

            // ── 頁籤 ──────────────────────────────────────────────
            _tabs = new TabControl
            {
                Dock      = DockStyle.Fill,
                Font      = new Font(Theme.FontFamily, 10f),
                BackColor = Theme.BgPage
            };

            foreach (var (title, table, icon, unit, accent) in Shops)
            {
                var tp = new TabPage($" {icon} {title} ");
                tp.BackColor = Theme.BgPage;
                tp.Tag       = (table, unit, accent);
                _tabs.TabPages.Add(tp);
            }

            Controls.Add(_tabs);
        }

        private async Task LoadAllAsync()
        {
            _lblStatus.Text = "查詢中…";
            var tasks = new List<Task>();

            foreach (TabPage tp in _tabs.TabPages)
            {
                var (table, unit, accent) = ((string, string, Color))tp.Tag;
                tasks.Add(LoadTabAsync(tp, table, unit, accent));
            }
            await Task.WhenAll(tasks);
            _lblStatus.Text = $"✓ 所有商城資料已更新  {DateTime.Now:HH:mm:ss}";
        }

        private async Task LoadTabAsync(TabPage tp, string table, string unit, Color accent)
        {
            try
            {
                var (items, spenders) = await DatabaseManager.Instance.GetShopTopItemsAsync(table, 20);

                Invoke(new Action(() =>
                {
                    tp.Controls.Clear();

                    if (items.Count == 0 && spenders.Count == 0)
                    {
                        tp.Controls.Add(new Label
                        {
                            Text      = $"⚠ 此商城尚無購買記錄\n（{table} 表格為空）",
                            ForeColor = Theme.TextMuted,
                            Font      = Theme.FontHeader,
                            Dock      = DockStyle.Fill,
                            TextAlign = System.Drawing.ContentAlignment.MiddleCenter
                        });
                        return;
                    }

                    // 分割：左邊熱賣道具 / 右邊消費排行
                    // ⚠ 注意：Panel1MinSize / Panel2MinSize 不能在建構時設定，
                    //   因為此時 Width=0，WinForms 內部驗算會拋 InvalidOperationException。
                    //   必須等加入 parent 後用 BeginInvoke 延遲設定。
                    const int p1min = 300, p2min = 240;
                    var split = new SplitContainer
                    {
                        Dock          = DockStyle.Fill,
                        Orientation   = Orientation.Vertical,
                        BackColor = Theme.BgPage,
                        SplitterWidth = 6
                        // 不設 Panel1MinSize / Panel2MinSize
                    };

                    // ── 左：熱賣道具 ──────────────────────────────
                    BuildItemPanel(split.Panel1, items, unit, accent);

                    // ── 右：消費排行 ──────────────────────────────
                    BuildSpenderPanel(split.Panel2, spenders, unit, accent);

                    tp.Controls.Add(split);

                    // 延遲到下一個 UI 訊息循環（此時 split 已有實際寬度）
                    BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            split.Panel1MinSize = p1min;
                            split.Panel2MinSize = p2min;
                            if (split.Width > p1min + p2min + split.SplitterWidth)
                                split.SplitterDistance = (int)(split.Width * 0.60);
                        }
                        catch { }
                    }));

                    // 視窗縮放時動態調整分割位置
                    split.Resize += (s2, e2) =>
                    {
                        if (split.Width <= p1min + p2min + split.SplitterWidth) return;
                        try
                        {
                            int dist = Math.Max(p1min,
                                       Math.Min(split.Width - p2min - split.SplitterWidth,
                                                (int)(split.Width * 0.60)));
                            if (split.SplitterDistance != dist)
                                split.SplitterDistance = dist;
                        }
                        catch { }
                    };
                }));
            }
            catch (Exception ex)
            {
                Invoke(new Action(() =>
                {
                    tp.Controls.Clear();
                    tp.Controls.Add(new Label
                    {
                        Text      = "✗ 查詢失敗：" + ex.Message,
                        ForeColor = Theme.AccentRed,
                        Font      = Theme.FontBody,
                        Dock      = DockStyle.Fill,
                        TextAlign = System.Drawing.ContentAlignment.MiddleCenter
                    });
                }));
            }
        }

        private void BuildItemPanel(SplitterPanel panel, List<ShopSaleRecord> items, string unit, Color accent)
        {
            // 標題
            var hdr = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Theme.BgCard };
            hdr.Controls.Add(new Label
            {
                Text      = $"  🔥  熱賣道具 TOP {items.Count}（依總購買數量排序）",
                ForeColor = accent,
                Font      = new Font(Theme.FontFamily, 10f, FontStyle.Bold),
                AutoSize  = true,
                Location  = new Point(0, 9)
            });
            panel.Controls.Add(hdr);

            // DataGridView
            var dgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(dgv);
            dgv.ReadOnly = true;
            dgv.RowTemplate.Height = 34;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRank",   HeaderText = "排名",       MinimumWidth = 44,  FillWeight = 25,  DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colId",     HeaderText = "道具 ID",    MinimumWidth = 80,  FillWeight = 55  });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colName",   HeaderText = "道具名稱",   MinimumWidth = 120, FillWeight = 150 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colQty",    HeaderText = "購買總量",   MinimumWidth = 80,  FillWeight = 55,  DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colOrders", HeaderText = "購買筆數",   MinimumWidth = 72,  FillWeight = 50,  DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCost",   HeaderText = $"消耗{unit}", MinimumWidth = 90,  FillWeight = 65,  DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colLast",   HeaderText = "最後購買",   MinimumWidth = 120, FillWeight = 80  });

            dgv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                var col = dgv.Columns[e.ColumnIndex].Name;
                if (col == "colRank")
                {
                    int rank = e.RowIndex + 1;
                    e.CellStyle.Font      = new Font(Theme.FontFamily, 10f, FontStyle.Bold);
                    e.CellStyle.ForeColor = rank == 1 ? Color.FromArgb(255, 215, 0)
                                           : rank == 2 ? Color.FromArgb(192, 192, 192)
                                           : rank == 3 ? Color.FromArgb(205, 127, 50) : Theme.TextMuted;
                    e.FormattingApplied = true;
                }
                if (col == "colQty") { e.CellStyle.ForeColor = accent; e.CellStyle.Font = new Font(Theme.FontFamily, 9f, FontStyle.Bold); e.FormattingApplied = true; }
                if (col == "colCost") { e.CellStyle.ForeColor = Color.FromArgb(220, 180, 80); e.FormattingApplied = true; }
            };

            foreach (var rec in items)
            {
                int i = dgv.Rows.Add(
                    rec.Rank == 1 ? "🥇 1" : rec.Rank == 2 ? "🥈 2" : rec.Rank == 3 ? "🥉 3" : $"  {rec.Rank}",
                    $"#{rec.ItemId}",
                    rec.ItemName,
                    $"{rec.TotalQty:N0}",
                    $"{rec.OrderCount:N0} 筆",
                    rec.TotalCost > 0 ? $"{rec.TotalCost:N0}" : "—",
                    rec.LastBuyTime.Length > 16 ? rec.LastBuyTime[..16] : rec.LastBuyTime);
                dgv.Rows[i].Tag = rec;
            }

            panel.Controls.Add(dgv);
        }

        private void BuildSpenderPanel(SplitterPanel panel, List<ShopSpenderRecord> spenders, string unit, Color accent)
        {
            var hdr = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Theme.BgCard };
            hdr.Controls.Add(new Label
            {
                Text      = $"  💸  消費排行 TOP {spenders.Count}",
                ForeColor = Color.FromArgb(255, 160, 80),
                Font      = new Font(Theme.FontFamily, 10f, FontStyle.Bold),
                AutoSize  = true,
                Location  = new Point(0, 9)
            });
            panel.Controls.Add(hdr);

            var dgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(dgv);
            dgv.ReadOnly = true;
            dgv.RowTemplate.Height = 34;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRank",  HeaderText = "排名",       MinimumWidth = 44,  FillWeight = 30,  DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colName",  HeaderText = "角色名稱",   MinimumWidth = 100, FillWeight = 100 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCdkey", HeaderText = "帳號",       MinimumWidth = 140, FillWeight = 140 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colQty",   HeaderText = "購買數量",   MinimumWidth = 80,  FillWeight = 65,  DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCost",  HeaderText = $"消耗{unit}", MinimumWidth = 90,  FillWeight = 75,  DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });

            dgv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                var col = dgv.Columns[e.ColumnIndex].Name;
                if (col == "colRank")
                {
                    int rank = e.RowIndex + 1;
                    e.CellStyle.Font      = new Font(Theme.FontFamily, 10f, FontStyle.Bold);
                    e.CellStyle.ForeColor = rank == 1 ? Color.FromArgb(255, 215, 0)
                                           : rank == 2 ? Color.FromArgb(192, 192, 192)
                                           : rank == 3 ? Color.FromArgb(205, 127, 50) : Theme.TextMuted;
                    e.FormattingApplied = true;
                }
                if (col == "colCost") { e.CellStyle.ForeColor = Color.FromArgb(255, 160, 60); e.CellStyle.Font = new Font(Theme.FontFamily, 9f, FontStyle.Bold); e.FormattingApplied = true; }
            };

            foreach (var rec in spenders)
            {
                dgv.Rows.Add(
                    rec.Rank == 1 ? "🥇 1" : rec.Rank == 2 ? "🥈 2" : rec.Rank == 3 ? "🥉 3" : $"  {rec.Rank}",
                    rec.Name,
                    rec.Cdkey,
                    $"{rec.TotalQty:N0}",
                    rec.TotalCost > 0 ? $"{rec.TotalCost:N0}" : "—");
            }

            panel.Controls.Add(dgv);
        }
    }
}
