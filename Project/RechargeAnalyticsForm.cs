using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    public class RechargeAnalyticsForm : Form
    {
        // ── 圖表面板 ──
        private Panel _dailyPanel, _monthlyPanel, _tierPanel, _firstPay;

        // ── KPI 標籤 ──
        private Label _lblToday, _lblMonth, _lblTotal, _lblPayingPlayers;
        private Label _lblStatus;
        private Button _btnRefresh;

        // ── 資料 ──
        private (DateTime[] dates, decimal[] amounts, int[] counts) _daily30;
        private (string[] months, decimal[] amounts, int[] counts)  _monthly;
        private Dictionary<string, int> _tierData;
        private Dictionary<string, int> _firstPayData;

        public RechargeAnalyticsForm()
        {
            InitUI();
            _ = LoadAllAsync();
        }

        private void InitUI()
        {
            Text          = "💰 儲值趨勢分析";
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
                Text = "  💰  儲值趨勢分析",
                ForeColor = Color.FromArgb(255, 195, 60),
                Font  = new Font(Theme.FontFamily, 13f, FontStyle.Bold),
                Dock  = DockStyle.Left, Width = 280,
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0)
            });
            _btnRefresh = Theme.MakePrimaryButton("↺ 重新整理", 110, 32);
            _btnRefresh.Dock   = DockStyle.Right;
            _btnRefresh.Margin = new Padding(0, 10, 14, 10);
            _btnRefresh.Click += (s, e) => _ = LoadAllAsync();
            header.Controls.Add(_btnRefresh);

            // ── KPI 卡片列 ──────────────────────────────────────
            var kpiPanel = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = Theme.BgMid };
            kpiPanel.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme.Border });
            var kpiFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(12, 10, 12, 10), WrapContents = false
            };
            Panel MakeKpi(string icon, string title, Color valColor, ref Label lbl)
            {
                var p = new Panel { Width = 230, Height = 54, BackColor = Theme.BgCard, Margin = new Padding(0, 0, 10, 0) };
                p.Controls.Add(new Label { Text = icon, Font = new Font("Segoe UI Emoji", 18f), Size = new Size(44, 44), Location = new Point(8, 5), TextAlign = ContentAlignment.MiddleCenter });
                p.Controls.Add(new Label { Text = title, ForeColor = Theme.TextMuted, Font = Theme.FontSmall, Location = new Point(56, 6), AutoSize = true });
                lbl = new Label { Text = "—", ForeColor = valColor, Font = new Font(Theme.FontFamily, 14f, FontStyle.Bold), Location = new Point(56, 24), AutoSize = true };
                p.Controls.Add(lbl);
                return p;
            }
            kpiFlow.Controls.Add(MakeKpi("📅", "今日充值（NT$）",   Color.FromArgb(255, 195, 60),  ref _lblToday));
            kpiFlow.Controls.Add(MakeKpi("📆", "本月充值（NT$）",   Color.FromArgb(100, 200, 255), ref _lblMonth));
            kpiFlow.Controls.Add(MakeKpi("🏦", "累計充值（NT$）",   Color.FromArgb(103, 194, 58),  ref _lblTotal));
            kpiFlow.Controls.Add(MakeKpi("🧑‍💼", "付費玩家人數",  Color.FromArgb(245, 108, 108),  ref _lblPayingPlayers));
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
                    new SolidBrush(sel ? Color.FromArgb(255, 195, 60) : Theme.TextMuted), e.Bounds, sf);
            };

            var tab1 = new TabPage("📈  每日趨勢（30天）");
            var tab2 = new TabPage("📅  月度報表（12月）");
            var tab3 = new TabPage("🥧  付費分層");
            var tab4 = new TabPage("⏱  首次付費時機");
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
        // Tab1 — 每日趨勢
        // ═══════════════════════════════════════════════
        private void BuildTab1(TabPage tab)
        {
            var outer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 12, 16, 12), BackColor = Theme.BgPage };
            var card  = MakeChartCard("過去 30 天每日充值金額（NT$）", ref _dailyPanel);
            _dailyPanel.Paint += (s, e) =>
            {
                if (_daily30.dates == null || _daily30.dates.Length == 0) { ChartRenderer.DrawBarChart(e.Graphics, _dailyPanel.ClientRectangle, null, null, Color.White, "無充值資料"); return; }
                var series = new[]
                {
                    new ChartRenderer.LineSeries
                    {
                        Label    = "充值金額（NT$）",
                        Color    = Color.FromArgb(255, 195, 60),
                        Values   = _daily30.amounts.Select(x => (double)x).ToArray(),
                        FillArea = true
                    }
                };
                string[] xlbls = _daily30.dates.Select(d => d.ToString("M/d")).ToArray();
                ChartRenderer.DrawLineChart(e.Graphics, _dailyPanel.ClientRectangle,
                    series, xlbls, "30 天充值趨勢", "NT$");
            };
            card.Dock = DockStyle.Fill;
            outer.Controls.Add(card);
            tab.Controls.Add(outer);
        }

        // ═══════════════════════════════════════════════
        // Tab2 — 月度報表
        // ═══════════════════════════════════════════════
        private void BuildTab2(TabPage tab)
        {
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill, Orientation = Orientation.Horizontal,
                BackColor = Theme.Border, SplitterWidth = 3
            };
            split.Panel1.BackColor = Theme.BgPage;
            split.Panel2.BackColor = Theme.BgPage;
            // 用 Resize 事件動態設定，確保有實際寬度才計算（避免嵌入時 Width=0 問題）
            split.Resize += (s, e) => { if (split.Height > 0) try { split.SplitterDistance = (int)(split.Height * 0.6); } catch { } };

            // 上方：月度長條圖
            var card = MakeChartCard("近 12 個月充值金額（NT$）", ref _monthlyPanel);
            _monthlyPanel.Paint += (s, e) =>
            {
                if (_monthly.months == null || _monthly.months.Length == 0) { ChartRenderer.DrawBarChart(e.Graphics, _monthlyPanel.ClientRectangle, null, null, Color.White, "無月度資料"); return; }
                ChartRenderer.DrawBarChart(e.Graphics, _monthlyPanel.ClientRectangle,
                    _monthly.amounts.Select(x => (double)x).ToArray(),
                    _monthly.months, Color.FromArgb(100, 200, 255), "月度充值金額", "NT$");
            };
            card.Dock = DockStyle.Fill;
            split.Panel1.Controls.Add(card);
            split.Panel1.Padding = new Padding(16, 12, 16, 6);

            // 下方：月度資料表格
            var dgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(dgv);
            dgv.ReadOnly = true; dgv.AllowUserToAddRows = false;
            dgv.RowTemplate.Height = 26; dgv.ColumnHeadersHeight = 28;
            void AddCol(string name, string hdr, int w, DataGridViewContentAlignment align = DataGridViewContentAlignment.MiddleLeft)
                => dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = hdr, Width = w, SortMode = DataGridViewColumnSortMode.NotSortable, DefaultCellStyle = { Alignment = align } });
            AddCol("cMonth",  "月份",          100, DataGridViewContentAlignment.MiddleCenter);
            AddCol("cAmount", "充值金額（NT$）", 180, DataGridViewContentAlignment.MiddleRight);
            AddCol("cCount",  "訂單筆數",       110, DataGridViewContentAlignment.MiddleRight);
            AddCol("cAvg",    "平均單筆（NT$）", 180, DataGridViewContentAlignment.MiddleRight);

            Load += (_, __) => RefreshMonthTable(dgv);

            split.Panel2.Controls.Add(dgv);
            split.Panel2.Padding = new Padding(16, 6, 16, 12);
            tab.Controls.Add(split);
        }

        private void RefreshMonthTable(DataGridView dgv)
        {
            if (_monthly.months == null) return;
            dgv.Rows.Clear();
            for (int i = 0; i < _monthly.months.Length; i++)
            {
                decimal avg = _monthly.counts[i] > 0 ? _monthly.amounts[i] / _monthly.counts[i] : 0;
                dgv.Rows.Add(_monthly.months[i],
                    $"NT$ {_monthly.amounts[i]:N0}",
                    _monthly.counts[i].ToString("N0"),
                    $"NT$ {avg:N0}");
            }
        }

        // ═══════════════════════════════════════════════
        // Tab3 — 付費分層圓餅
        // ═══════════════════════════════════════════════
        private void BuildTab3(TabPage tab)
        {
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill, Orientation = Orientation.Vertical,
                BackColor = Theme.Border, SplitterWidth = 3
            };
            split.Panel1.BackColor = Theme.BgPage;
            split.Panel2.BackColor = Theme.BgPage;
            split.Resize += (s, e) => { if (split.Width > 0) try { split.SplitterDistance = (int)(split.Width * 0.56); } catch { } };

            // 左：圓餅
            var card = MakeChartCard("付費玩家分層分佈", ref _tierPanel);
            _tierPanel.Paint += (s, e) =>
            {
                if (_tierData == null || _tierData.Values.Sum() == 0) { ChartRenderer.DrawPieChart(e.Graphics, _tierPanel.ClientRectangle, null, null); return; }
                ChartRenderer.DrawPieChart(e.Graphics, _tierPanel.ClientRectangle,
                    _tierData.Values.Select(v => (double)v).ToArray(),
                    _tierData.Keys.ToArray(),
                    ChartRenderer.Palette, "玩家付費分層（依累計充值）");
            };
            card.Dock = DockStyle.Fill;
            split.Panel1.Controls.Add(card);
            split.Panel1.Padding = new Padding(16, 12, 6, 12);

            // 右：統計表
            var rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6, 12, 16, 12), BackColor = Theme.BgPage };
            var dgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(dgv);
            dgv.ReadOnly = true; dgv.AllowUserToAddRows = false;
            dgv.RowTemplate.Height = 30; dgv.ColumnHeadersHeight = 28;
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cTier", HeaderText = "分層", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, SortMode = DataGridViewColumnSortMode.NotSortable });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cCount", HeaderText = "人數", Width = 90,  SortMode = DataGridViewColumnSortMode.NotSortable, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cPct",   HeaderText = "佔比", Width = 80,  SortMode = DataGridViewColumnSortMode.NotSortable, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });

            rightPanel.Controls.Add(new Label
            {
                Text = "分層說明", ForeColor = Theme.TextSecondary, Font = Theme.FontSmall,
                Dock = DockStyle.Top, Height = 24, TextAlign = ContentAlignment.MiddleLeft
            });
            rightPanel.Controls.Add(dgv);
            split.Panel2.Controls.Add(rightPanel);

            Load += (_, __) => RefreshTierTable(dgv);
            tab.Controls.Add(split);
        }

        private void RefreshTierTable(DataGridView dgv)
        {
            if (_tierData == null) return;
            dgv.Rows.Clear();
            int total = _tierData.Values.Sum();
            int ci = 0;
            foreach (var kv in _tierData)
            {
                double pct = total > 0 ? (double)kv.Value / total * 100 : 0;
                int ri = dgv.Rows.Add(kv.Key, kv.Value.ToString("N0"), $"{pct:0.#}%");
                dgv.Rows[ri].DefaultCellStyle.ForeColor = ChartRenderer.Palette[ci++ % ChartRenderer.Palette.Length];
            }
            if (total > 0)
            {
                int paying = total - (_tierData.ContainsKey("免費玩家") ? _tierData["免費玩家"] : 0);
                dgv.Rows.Add("─────── 合計 ───────", total.ToString("N0"), "100%");
            }
        }

        // ═══════════════════════════════════════════════
        // Tab4 — 首次付費時機
        // ═══════════════════════════════════════════════
        private void BuildTab4(TabPage tab)
        {
            var outer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 12, 16, 12), BackColor = Theme.BgPage };

            var desc = new Label
            {
                Text = "從玩家「註冊日」到「首次充值日」的天數分佈 — 反映玩家付費轉化速度",
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                Dock = DockStyle.Top, Height = 26, TextAlign = ContentAlignment.MiddleLeft
            };

            var card = MakeChartCard("首次付費距離註冊的天數分佈", ref _firstPay);
            _firstPay.Paint += (s, e) =>
            {
                if (_firstPayData == null || _firstPayData.Values.Sum() == 0) { ChartRenderer.DrawBarChart(e.Graphics, _firstPay.ClientRectangle, null, null, Color.White, "無資料（需要 recharge_orders 資料）"); return; }
                ChartRenderer.DrawBarChart(e.Graphics, _firstPay.ClientRectangle,
                    _firstPayData.Values.Select(v => (double)v).ToArray(),
                    _firstPayData.Keys.ToArray(),
                    Color.FromArgb(103, 194, 58), "首次付費天數分佈", "人");
            };
            card.Dock = DockStyle.Fill;
            outer.Controls.Add(card);
            outer.Controls.Add(desc);
            tab.Controls.Add(outer);
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

                // KPI
                var stats = await db.GetStatsAsync();
                SafeSet(() =>
                {
                    _lblToday.Text  = $"NT$ {stats.TodayRevenue:N0}";
                    _lblTotal.Text  = $"NT$ {stats.TotalRevenue:N0}";
                });

                // 本月充值
                var (months, mAmts, mCnts) = await db.GetMonthlyRechargeAsync();
                _monthly = (months, mAmts, mCnts);
                if (months.Length > 0)
                    SafeSet(() => _lblMonth.Text = $"NT$ {mAmts[^1]:N0}");

                // 付費玩家人數
                var tiers = await db.GetPaymentTierAsync();
                _tierData = tiers;
                if (tiers.Count > 0)
                {
                    int paying = tiers.Values.Sum() - (tiers.ContainsKey("免費玩家") ? tiers["免費玩家"] : 0);
                    SafeSet(() => _lblPayingPlayers.Text = paying.ToString("N0") + " 人");
                }

                // 每日30天
                _daily30 = await db.GetDailyRechargeAsync(30);

                // 首次付費
                _firstPayData = await db.GetTimeToFirstPaymentAsync();

                SafeSet(() =>
                {
                    _dailyPanel?.Invalidate();
                    _monthlyPanel?.Invalidate();
                    _tierPanel?.Invalidate();
                    _firstPay?.Invalidate();
                    // 重整月度表格
                    var dgv = FindDgvInTab2();
                    if (dgv != null) RefreshMonthTable(dgv);
                    var dgvT = FindDgvInTab3();
                    if (dgvT != null) RefreshTierTable(dgvT);
                    _lblStatus.Text = $"✓ 資料已更新  {DateTime.Now:HH:mm:ss}";
                });
            }
            catch (Exception ex) { SafeSet(() => _lblStatus.Text = "✗ 載入失敗：" + ex.Message); }
            finally { SafeSet(() => _btnRefresh.Enabled = true); }
        }

        // 找到 Tab2 / Tab3 的 DGV（簡化：直接用 Tag 記錄）
        private DataGridView FindDgvInTab2() => null; // 月度表由 Invalidate 觸發 Load 回呼，RefreshMonthTable 在 Load 事件呼叫
        private DataGridView FindDgvInTab3() => null;

        private void SafeSet(Action a) { if (!IsDisposed) try { if (InvokeRequired) Invoke(a); else a(); } catch { } }

        private Panel MakeChartCard(string title, ref Panel chartPanel)
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgCard };
            card.Controls.Add(new Label
            {
                Text = title, ForeColor = Theme.TextSecondary, Font = Theme.FontSmall,
                AutoSize = true, Location = new Point(12, 8)
            });
            var cp = new Panel
            {
                BackColor = Theme.BgCard,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(8, 28)
            };
            chartPanel = cp;
            card.Controls.Add(cp);
            card.Resize += (s, e) => cp.Size = new Size(card.Width - 16, card.Height - 36);
            return card;
        }
    }
}
