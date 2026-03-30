using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    public class TradeAuditForm : Form
    {
        // ── KPI ──
        private Label _lblTotal, _lblPairs, _lblSuspicious, _lblSameIp;
        private Label _lblStatus;
        private Button _btnRefresh;

        // ── DGV ──
        private DataGridView _pairDgv, _sameIpDgv, _goldDgv, _rankDgv;

        public TradeAuditForm()
        {
            InitUI();
            _ = LoadAllAsync();
        }

        private void InitUI()
        {
            Text          = "🔍 交易稽核 & 異常偵測";
            Size          = new Size(1260, 820);
            MinimumSize   = new Size(900, 640);
            BackColor     = Theme.BgPage;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;

            // ── Header ──────────────────────────────────────────
            var header = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Theme.BgDark };
            header.Controls.Add(new Label
            {
                Text = "  🔍  交易稽核 & 異常偵測",
                ForeColor = Color.FromArgb(245, 108, 108),
                Font  = new Font(Theme.FontFamily, 13f, FontStyle.Bold),
                Dock  = DockStyle.Left, Width = 340,
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0)
            });
            header.Controls.Add(new Label
            {
                Text = "⚠ 本模組僅供參考，建議結合人工判斷再進行處置",
                ForeColor = Color.FromArgb(230, 162, 60), Font = Theme.FontSmall,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(4, 0, 0, 0)
            });
            _btnRefresh = Theme.MakePrimaryButton("↺ 重新整理", 110, 32);
            _btnRefresh.Dock   = DockStyle.Right;
            _btnRefresh.Margin = new Padding(0, 10, 14, 10);
            _btnRefresh.Click += (s, e) => _ = LoadAllAsync();
            header.Controls.Add(_btnRefresh);

            // ── KPI 風險卡片列 ──────────────────────────────────
            var kpiPanel = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = Theme.BgMid };
            kpiPanel.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme.Border });
            var kpiFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(12, 10, 12, 10), WrapContents = false
            };
            Panel MakeRisk(string icon, string title, Color valColor, ref Label lbl)
            {
                var p = new Panel { Width = 220, Height = 54, BackColor = Theme.BgCard, Margin = new Padding(0, 0, 10, 0) };
                p.Controls.Add(new Label { Text = icon, Font = new Font("Segoe UI Emoji", 18f), Size = new Size(44, 44), Location = new Point(8, 5), TextAlign = ContentAlignment.MiddleCenter });
                p.Controls.Add(new Label { Text = title, ForeColor = Theme.TextMuted, Font = Theme.FontSmall, Location = new Point(56, 6), AutoSize = true });
                lbl = new Label { Text = "—", ForeColor = valColor, Font = new Font(Theme.FontFamily, 14f, FontStyle.Bold), Location = new Point(56, 24), AutoSize = true };
                p.Controls.Add(lbl);
                return p;
            }
            kpiFlow.Controls.Add(MakeRisk("📊", "總交易筆數",        Color.FromArgb(100, 200, 255), ref _lblTotal));
            kpiFlow.Controls.Add(MakeRisk("👥", "不重複交易配對",     Color.FromArgb(103, 194, 58),  ref _lblPairs));
            kpiFlow.Controls.Add(MakeRisk("🚨", "高頻配對（≥10次）", Color.FromArgb(245, 108, 108),  ref _lblSuspicious));
            kpiFlow.Controls.Add(MakeRisk("🔗", "同IP帳號交易配對",  Color.FromArgb(230, 162, 60),   ref _lblSameIp));
            kpiPanel.Controls.Add(kpiFlow);

            // ── 狀態列 ──────────────────────────────────────────
            var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = Theme.BgDark };
            _lblStatus = new Label { Text = "載入中…", ForeColor = Theme.TextMuted, Font = Theme.FontSmall, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0) };
            statusBar.Controls.Add(_lblStatus);

            // ── TabControl ──────────────────────────────────────
            var tabs = new TabControl
            {
                Dock = DockStyle.Fill, Appearance = TabAppearance.FlatButtons,
                Font = new Font(Theme.FontFamily, 10f), Padding = new Point(14, 6)
            };
            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabs.DrawItem += (s, e) =>
            {
                bool sel = e.Index == tabs.SelectedIndex;
                e.Graphics.FillRectangle(new SolidBrush(sel ? Color.FromArgb(50, 52, 65) : Color.FromArgb(36, 37, 50)), e.Bounds);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString(tabs.TabPages[e.Index].Text,
                    new Font(Theme.FontFamily, 9.5f, sel ? FontStyle.Bold : FontStyle.Regular),
                    new SolidBrush(sel ? Color.FromArgb(245, 108, 108) : Theme.TextSecondary), e.Bounds, sf);
            };

            var tab1 = new TabPage("🚨  高頻配對");
            var tab2 = new TabPage("🔗  同IP交易");
            var tab3 = new TabPage("💰  金幣異動排行");
            var tab4 = new TabPage("📊  交易量排行");
            foreach (var t in new[] { tab1, tab2, tab3, tab4 })
            { t.BackColor = Theme.BgPage; t.ForeColor = Theme.TextPrimary; }

            BuildTab1(tab1);
            BuildTab2(tab2);
            BuildTab3(tab3);
            BuildTab4(tab4);
            tabs.TabPages.AddRange(new[] { tab1, tab2, tab3, tab4 });

            Controls.Add(tabs);
            Controls.Add(statusBar);
            Controls.Add(kpiPanel);
            Controls.Add(header);
        }

        // ═══════════════════════════════════════════════
        // Tab1 — 高頻交易配對
        // ═══════════════════════════════════════════════
        private void BuildTab1(TabPage tab)
        {
            var outer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 10, 16, 12), BackColor = Theme.BgPage };

            var toolbar = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Theme.BgPage };
            toolbar.Controls.Add(new Label
            {
                Text = "兩個帳號之間交易次數超過此閾值視為高頻：",
                ForeColor = Theme.TextSecondary, Font = Theme.FontBody, AutoSize = true, Location = new Point(0, 11)
            });
            var nudMin = new NumericUpDown
            {
                Location = new Point(290, 9), Width = 70, Minimum = 3, Maximum = 999, Value = 10,
                BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary, Font = Theme.FontBody
            };
            var btnQ = Theme.MakePrimaryButton("查詢", 70, 28);
            btnQ.Location = new Point(370, 8);
            toolbar.Controls.Add(nudMin);
            toolbar.Controls.Add(btnQ);
            toolbar.Controls.Add(new Label { Text = "次", ForeColor = Theme.TextMuted, Font = Theme.FontSmall, AutoSize = true, Location = new Point(366, 13) });

            var desc = new Panel { Dock = DockStyle.Top, Height = 26, BackColor = Theme.BgPage };
            desc.Controls.Add(new Label
            {
                Text = "⚠ 高頻配對可能代表洗金、刷資源或工作室互刷行為，請結合其他資訊判斷",
                ForeColor = Color.FromArgb(230, 162, 60), Font = Theme.FontSmall, AutoSize = true, Location = new Point(0, 6)
            });

            _pairDgv = BuildRiskDgv();
            _pairDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cRank",      HeaderText = "#",        Width = 46, SortMode = DataGridViewColumnSortMode.NotSortable, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            _pairDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cFrom",      HeaderText = "發起方帳號", Width = 160, SortMode = DataGridViewColumnSortMode.NotSortable });
            _pairDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cFromName",  HeaderText = "角色名稱",  Width = 130, SortMode = DataGridViewColumnSortMode.NotSortable });
            _pairDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cTo",        HeaderText = "接收方帳號", Width = 160, SortMode = DataGridViewColumnSortMode.NotSortable });
            _pairDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cToName",    HeaderText = "角色名稱",  Width = 130, SortMode = DataGridViewColumnSortMode.NotSortable });
            _pairDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cCount",     HeaderText = "交易次數",  Width = 90,  SortMode = DataGridViewColumnSortMode.NotSortable, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });
            _pairDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cLast",      HeaderText = "最後交易",  AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, SortMode = DataGridViewColumnSortMode.NotSortable });

            btnQ.Click += async (s, e) =>
            {
                btnQ.Enabled = false;
                _lblStatus.Text = "查詢高頻配對…";
                try
                {
                    var rows = await DatabaseManager.Instance.GetFrequentTradePairsAsync((int)nudMin.Value);
                    _pairDgv.Rows.Clear();
                    int rank = 1;
                    foreach (var (from, fromName, to, toName, cnt, last) in rows)
                    {
                        int ri = _pairDgv.Rows.Add(rank++, from, fromName, to, toName, cnt, last);
                        if (cnt >= 50) _pairDgv.Rows[ri].DefaultCellStyle.ForeColor = Color.FromArgb(245, 108, 108);
                        else if (cnt >= 20) _pairDgv.Rows[ri].DefaultCellStyle.ForeColor = Color.FromArgb(230, 162, 60);
                    }
                    _lblStatus.Text = $"找到 {rows.Count} 個高頻配對（閾值 ≥ {(int)nudMin.Value} 次）";
                }
                finally { btnQ.Enabled = true; }
            };

            outer.Controls.Add(_pairDgv);
            outer.Controls.Add(desc);
            outer.Controls.Add(toolbar);
            tab.Controls.Add(outer);
        }

        // ═══════════════════════════════════════════════
        // Tab2 — 同IP帳號之間的交易
        // ═══════════════════════════════════════════════
        private void BuildTab2(TabPage tab)
        {
            var outer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 10, 16, 12), BackColor = Theme.BgPage };

            var toolbar = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Theme.BgPage };
            toolbar.Controls.Add(new Label
            {
                Text = "共用同一 IP 的帳號互相交易，次數超過：",
                ForeColor = Theme.TextSecondary, Font = Theme.FontBody, AutoSize = true, Location = new Point(0, 11)
            });
            var nudMin = new NumericUpDown
            {
                Location = new Point(245, 9), Width = 70, Minimum = 1, Maximum = 999, Value = 5,
                BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary, Font = Theme.FontBody
            };
            var btnQ = Theme.MakePrimaryButton("查詢", 70, 28);
            btnQ.Location = new Point(326, 8);
            toolbar.Controls.Add(nudMin);
            toolbar.Controls.Add(btnQ);

            var desc = new Panel { Dock = DockStyle.Top, Height = 26, BackColor = Theme.BgPage };
            desc.Controls.Add(new Label
            {
                Text = "⚠ 同IP帳號互刷是工作室或多開小號的常見特徵，高次數配對風險較高",
                ForeColor = Color.FromArgb(230, 162, 60), Font = Theme.FontSmall, AutoSize = true, Location = new Point(0, 6)
            });

            _sameIpDgv = BuildRiskDgv();
            _sameIpDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cRank",   HeaderText = "#",       Width = 46, SortMode = DataGridViewColumnSortMode.NotSortable, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            _sameIpDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cFrom",   HeaderText = "帳號A",   Width = 180, SortMode = DataGridViewColumnSortMode.NotSortable });
            _sameIpDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cTo",     HeaderText = "帳號B",   Width = 180, SortMode = DataGridViewColumnSortMode.NotSortable });
            _sameIpDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cCount",  HeaderText = "交易次數", Width = 90,  SortMode = DataGridViewColumnSortMode.NotSortable, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });
            _sameIpDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cIp",     HeaderText = "共用 IP", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, SortMode = DataGridViewColumnSortMode.NotSortable });

            btnQ.Click += async (s, e) =>
            {
                btnQ.Enabled = false;
                _lblStatus.Text = "查詢同IP交易…";
                try
                {
                    var rows = await DatabaseManager.Instance.GetSameIpTradesAsync((int)nudMin.Value);
                    _sameIpDgv.Rows.Clear();
                    int rank = 1;
                    foreach (var (from, to, cnt, ip) in rows)
                    {
                        int ri = _sameIpDgv.Rows.Add(rank++, from, to, cnt, ip);
                        _sameIpDgv.Rows[ri].DefaultCellStyle.ForeColor = cnt >= 20
                            ? Color.FromArgb(245, 108, 108) : Color.FromArgb(230, 162, 60);
                    }
                    _lblStatus.Text = $"找到 {rows.Count} 組同 IP 帳號互相交易";
                }
                finally { btnQ.Enabled = true; }
            };

            outer.Controls.Add(_sameIpDgv);
            outer.Controls.Add(desc);
            outer.Controls.Add(toolbar);
            tab.Controls.Add(outer);
        }

        // ═══════════════════════════════════════════════
        // Tab3 — 金幣異動排行
        // ═══════════════════════════════════════════════
        private void BuildTab3(TabPage tab)
        {
            var outer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 10, 16, 12), BackColor = Theme.BgPage };

            var desc = new Panel { Dock = DockStyle.Top, Height = 26, BackColor = Theme.BgPage };
            desc.Controls.Add(new Label
            {
                Text = "依 goldlog 統計各玩家累積獲得/失去的金幣，顯示前 50 名",
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall, AutoSize = true, Location = new Point(0, 6)
            });

            _goldDgv = BuildRiskDgv();
            _goldDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cRank",    HeaderText = "#",       Width = 46, SortMode = DataGridViewColumnSortMode.NotSortable, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            _goldDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cAccount", HeaderText = "帳號",    Width = 160, SortMode = DataGridViewColumnSortMode.NotSortable });
            _goldDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cName",    HeaderText = "角色名稱", Width = 140, SortMode = DataGridViewColumnSortMode.NotSortable });
            _goldDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cGain",    HeaderText = "累積獲得", Width = 130, SortMode = DataGridViewColumnSortMode.NotSortable, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });
            _goldDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cLoss",    HeaderText = "累積失去", Width = 130, SortMode = DataGridViewColumnSortMode.NotSortable, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });
            _goldDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cNet",     HeaderText = "淨收益",  Width = 130, SortMode = DataGridViewColumnSortMode.NotSortable, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });
            _goldDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cEntries", HeaderText = "記錄筆數", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, SortMode = DataGridViewColumnSortMode.NotSortable, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });

            _goldDgv.CellFormatting += (s, e) =>
            {
                if (_goldDgv.Columns[e.ColumnIndex].Name == "cNet" && e.Value is string netStr)
                {
                    if (long.TryParse(netStr.Replace(",", ""), out long net))
                        e.CellStyle.ForeColor = net >= 0 ? Color.FromArgb(103, 194, 58) : Color.FromArgb(245, 108, 108);
                }
            };

            outer.Controls.Add(_goldDgv);
            outer.Controls.Add(desc);
            tab.Controls.Add(outer);
        }

        // ═══════════════════════════════════════════════
        // Tab4 — 交易量排行
        // ═══════════════════════════════════════════════
        private void BuildTab4(TabPage tab)
        {
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill, Orientation = Orientation.Vertical,
                BackColor = Theme.Border, SplitterWidth = 3
            };
            split.Panel1.BackColor = split.Panel2.BackColor = Theme.BgPage;
            split.Resize += (s, e) => { if (split.Width > 0) try { split.SplitterDistance = split.Width / 2; } catch { } };

            // 左：DGV
            var leftPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 10, 6, 12), BackColor = Theme.BgPage };
            leftPanel.Controls.Add(new Label
            {
                Text = "交易發起方排行（tradelog 統計）",
                ForeColor = Theme.TextSecondary, Font = Theme.FontSmall,
                Dock = DockStyle.Top, Height = 26, TextAlign = ContentAlignment.MiddleLeft
            });
            _rankDgv = BuildRiskDgv();
            _rankDgv.Dock = DockStyle.Fill;
            _rankDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cRank",    HeaderText = "#",       Width = 46, SortMode = DataGridViewColumnSortMode.NotSortable, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            _rankDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cAccount", HeaderText = "帳號",    Width = 160, SortMode = DataGridViewColumnSortMode.NotSortable });
            _rankDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cName",    HeaderText = "角色名稱", Width = 140, SortMode = DataGridViewColumnSortMode.NotSortable });
            _rankDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cCount",   HeaderText = "交易次數", Width = 90,  SortMode = DataGridViewColumnSortMode.NotSortable, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });
            _rankDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cLast",    HeaderText = "最後交易", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, SortMode = DataGridViewColumnSortMode.NotSortable });
            leftPanel.Controls.Add(_rankDgv);
            split.Panel1.Controls.Add(leftPanel);

            // 右：水平長條圖
            var rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6, 10, 16, 12), BackColor = Theme.BgPage };
            rightPanel.Controls.Add(new Label
            {
                Text = "交易量視覺化（Top 10）",
                ForeColor = Theme.TextSecondary, Font = Theme.FontSmall,
                Dock = DockStyle.Top, Height = 26, TextAlign = ContentAlignment.MiddleLeft
            });
            var chartCard = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgCard };
            chartCard.Paint += (s, e) =>
            {
                if (_rankDgv.Rows.Count == 0) return;
                int n = Math.Min(_rankDgv.Rows.Count, 10);
                var vals   = new double[n];
                var labels = new string[n];
                for (int i = 0; i < n; i++)
                {
                    var row = _rankDgv.Rows[i];
                    vals[i]   = double.TryParse(row.Cells["cCount"].Value?.ToString(), out double v) ? v : 0;
                    labels[i] = row.Cells["cName"].Value?.ToString() ?? row.Cells["cAccount"].Value?.ToString() ?? "";
                }
                ChartRenderer.DrawHorizontalBars(e.Graphics, chartCard.ClientRectangle,
                    vals, labels, Color.FromArgb(100, 200, 255), "交易次數 Top 10", "次");
            };
            _rankDgv.Invalidated += (s, e) => chartCard.Invalidate();
            rightPanel.Controls.Add(chartCard);
            split.Panel2.Controls.Add(rightPanel);

            tab.Controls.Add(split);
        }

        // ═══════════════════════════════════════════════
        // 載入資料
        // ═══════════════════════════════════════════════
        private async Task LoadAllAsync()
        {
            _btnRefresh.Enabled = false;
            _lblStatus.Text     = "載入資料中…";
            try
            {
                var db = DatabaseManager.Instance;

                // 摘要 KPI
                var (total, pairs, suspicious, sameIp) = await db.GetTradeAuditSummaryAsync();
                SafeSet(() =>
                {
                    _lblTotal.Text      = total.ToString("N0");
                    _lblPairs.Text      = pairs.ToString("N0");
                    _lblSuspicious.Text = suspicious.ToString("N0");
                    _lblSameIp.Text     = "—";
                });

                // 高頻配對（預設載入 ≥10）
                var pairs10 = await db.GetFrequentTradePairsAsync(10);
                SafeSet(() =>
                {
                    _pairDgv.Rows.Clear();
                    int rank = 1;
                    foreach (var (from, fromName, to, toName, cnt, last) in pairs10)
                    {
                        int ri = _pairDgv.Rows.Add(rank++, from, fromName, to, toName, cnt, last);
                        if (cnt >= 50) _pairDgv.Rows[ri].DefaultCellStyle.ForeColor = Color.FromArgb(245, 108, 108);
                        else if (cnt >= 20) _pairDgv.Rows[ri].DefaultCellStyle.ForeColor = Color.FromArgb(230, 162, 60);
                    }
                });

                // 金幣異動
                var gold = await db.GetGoldAnomalyAsync(50);
                SafeSet(() =>
                {
                    _goldDgv.Rows.Clear();
                    int rank = 1;
                    foreach (var (acc, name, gain, loss, entries) in gold)
                    {
                        long net = gain - loss;
                        _goldDgv.Rows.Add(rank++, acc, name,
                            $"{gain:N0}", $"{loss:N0}", $"{net:N0}", entries.ToString("N0"));
                    }
                });

                // 交易排行
                var traders = await db.GetTopTradersAsync(50);
                SafeSet(() =>
                {
                    _rankDgv.Rows.Clear();
                    int rank = 1;
                    foreach (var (acc, name, cnt, last) in traders)
                        _rankDgv.Rows.Add(rank++, acc, name, cnt, last);
                    _rankDgv.Invalidate();
                });

                SafeSet(() => _lblStatus.Text = $"✓ 資料已更新  {DateTime.Now:HH:mm:ss}");
            }
            catch (Exception ex) { SafeSet(() => _lblStatus.Text = "✗ 載入失敗：" + ex.Message); }
            finally { SafeSet(() => _btnRefresh.Enabled = true); }
        }

        private DataGridView BuildRiskDgv()
        {
            var dgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(dgv);
            dgv.ReadOnly = true; dgv.AllowUserToAddRows = false;
            dgv.RowTemplate.Height = 26; dgv.ColumnHeadersHeight = 28;
            return dgv;
        }

        private void SafeSet(Action a) { if (!IsDisposed) try { if (InvokeRequired) Invoke(a); else a(); } catch { } }
    }
}
