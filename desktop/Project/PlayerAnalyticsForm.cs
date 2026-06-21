using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    public class PlayerAnalyticsForm : Form
    {
        // ── 圖表面板 ──
        private Panel _hourPanel, _weekPanel, _growthPanel;
        private Panel _retentionArea;
        private DataGridView _sleepDgv;

        // ── KPI 標籤 ──
        private Label _lblTotal, _lblOnline, _lblTodayNew, _lblTodayActive;
        private Label _lblStatus;
        private Button _btnRefresh;

        // ── 資料 ──
        private int[]     _hourData   = new int[24];
        private int[]     _weekData   = new int[7];
        private DateTime[] _growthDates;
        private int[]      _growthCounts;
        private Dictionary<string, (int cohort, int retained, double rate)> _retention;

        public PlayerAnalyticsForm()
        {
            Theme.ApplyHubForm(this);
            InitUI();
            _ = LoadAllAsync();
        }

        private void InitUI()
        {
            Text          = "📊 玩家活躍分析";
            Size          = new Size(1260, 820);
            MinimumSize   = new Size(900, 640);
            BackColor     = Theme.BgPage;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;

            // ── Header ──────────────────────────────────────────
            var header = new Panel { Dock = DockStyle.Top, Height = Theme.ToolbarHeight, BackColor = Theme.BgDialogHeader };
            header.Controls.Add(new Label
            {
                Text      = "  📊  玩家活躍分析",
                ForeColor = Color.FromArgb(100, 200, 255),
                Font      = new Font(Theme.FontFamily, 13f, FontStyle.Bold),
                Dock      = DockStyle.Left, Width = 280,
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0)
            });
            _btnRefresh = Theme.MakePrimaryButton("↺ 重新整理", 110, 32);
            _btnRefresh.Dock    = DockStyle.Right;
            _btnRefresh.Margin  = new Padding(0, 10, 14, 10);
            _btnRefresh.Click  += (s, e) => _ = LoadAllAsync();
            header.Controls.Add(_btnRefresh);

            // ── KPI 卡片列 ──────────────────────────────────────
            var kpiPanel = new Panel { Dock = DockStyle.Top, Height = Theme.HubKpiPanelHeight, BackColor = Theme.BgCard };
            kpiPanel.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme.Border });
            var kpiFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(12, 10, 12, 10), WrapContents = false
            };

            Panel MakeKpi(string icon, string title, ref Label valueLbl)
            {
                var p = new Panel { Width = 210, Height = 54, BackColor = Theme.BgCard, Margin = new Padding(0, 0, 10, 0) };
                p.Controls.Add(new Label { Text = icon, Font = new Font("Segoe UI Emoji", 18f), Size = new Size(44, 44), Location = new Point(8, 5), TextAlign = ContentAlignment.MiddleCenter });
                p.Controls.Add(new Label { Text = title, ForeColor = Theme.TextMuted, Font = Theme.FontSmall, Location = new Point(56, 6), AutoSize = true });
                valueLbl = new Label { Text = "—", ForeColor = Color.FromArgb(100, 200, 255), Font = new Font(Theme.FontFamily, 14f, FontStyle.Bold), Location = new Point(56, 24), AutoSize = true };
                p.Controls.Add(valueLbl);
                return p;
            }
            kpiFlow.Controls.Add(MakeKpi("👥", "總玩家數",     ref _lblTotal));
            kpiFlow.Controls.Add(MakeKpi("🟢", "目前在線",     ref _lblOnline));
            kpiFlow.Controls.Add(MakeKpi("🆕", "今日新增",     ref _lblTodayNew));
            kpiFlow.Controls.Add(MakeKpi("🕹", "今日活躍",     ref _lblTodayActive));
            kpiPanel.Controls.Add(kpiFlow);

            // ── 狀態列 ──────────────────────────────────────────
            var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = Theme.BgDark };
            _lblStatus = new Label { Text = "載入中…", ForeColor = Theme.TextMuted, Font = Theme.FontSmall, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0) };
            statusBar.Controls.Add(_lblStatus);

            // ── TabControl ──────────────────────────────────────
            var tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Appearance = TabAppearance.FlatButtons,
                Font       = new Font(Theme.FontFamily, 10f),
                Padding    = new Point(14, 6)
            };
            // 深色 tab 樣式
            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabs.DrawItem += (s, e) =>
            {
                bool sel = e.Index == tabs.SelectedIndex;
                e.Graphics.FillRectangle(new SolidBrush(sel ? Color.FromArgb(50, 52, 65) : Color.FromArgb(36, 37, 50)), e.Bounds);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString(tabs.TabPages[e.Index].Text,
                    new Font(Theme.FontFamily, 9.5f, sel ? FontStyle.Bold : FontStyle.Regular),
                    new SolidBrush(sel ? Color.FromArgb(100, 200, 255) : Theme.TextSecondary), e.Bounds, sf);
            };

            var tab1 = new TabPage("⏰  登入時段");
            var tab2 = new TabPage("📈  帳號成長");
            var tab3 = new TabPage("🔄  留存分析");
            var tab4 = new TabPage("💤  沉睡玩家");
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
        // Tab1 — 登入時段（24小時熱力條 + 週日分佈）
        // ═══════════════════════════════════════════════
        private void BuildTab1(TabPage tab)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1,
                Padding = new Padding(16), BackColor = Theme.BgPage
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 140));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // 上：熱力條說明框
            var heatCard = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgCard, Padding = new Padding(8) };
            heatCard.Controls.Add(new Label
            {
                Text = "24 小時登入熱力圖  ─  顏色越深代表該時段登入人數越多",
                ForeColor = Theme.TextSecondary, Font = Theme.FontSmall,
                AutoSize = true, Location = new Point(10, 6)
            });
            _hourPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgCard };
            _hourPanel.Paint += (s, e) =>
                ChartRenderer.DrawHourHeatBar(e.Graphics, _hourPanel.ClientRectangle, _hourData, "");
            heatCard.Controls.Add(_hourPanel);
            layout.Controls.Add(heatCard, 0, 0);

            // 下：週分佈長條圖
            var weekCard = MakeChartCard("星期幾登入分佈", ref _weekPanel);
            _weekPanel.Paint += (s, e) =>
            {
                string[] wLabels = { "日", "一", "二", "三", "四", "五", "六" };
                ChartRenderer.DrawBarChart(e.Graphics, _weekPanel.ClientRectangle,
                    _weekData.Select(x => (double)x).ToArray(), wLabels,
                    ChartRenderer.Palette[0], "各星期登入人次", "人");
            };
            layout.Controls.Add(weekCard, 0, 1);

            tab.Controls.Add(layout);
        }

        // ═══════════════════════════════════════════════
        // Tab2 — 帳號成長（30天新增折線）
        // ═══════════════════════════════════════════════
        private void BuildTab2(TabPage tab)
        {
            var outer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16), BackColor = Theme.BgPage };

            var info = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = Theme.BgPage };
            info.Controls.Add(new Label
            {
                Text = "過去 30 天每日新增帳號數量",
                ForeColor = Theme.TextSecondary, Font = Theme.FontSmall, AutoSize = true, Location = new Point(4, 8)
            });

            var card = MakeChartCard("30 天帳號成長曲線", ref _growthPanel);
            _growthPanel.Paint += (s, e) =>
            {
                if (_growthDates == null || _growthDates.Length == 0) { ChartRenderer.DrawBarChart(e.Graphics, _growthPanel.ClientRectangle, null, null, Color.White); return; }
                var series = new[] { new ChartRenderer.LineSeries
                {
                    Label = "新增帳號", Color = ChartRenderer.Palette[0],
                    Values = _growthCounts.Select(x => (double)x).ToArray(),
                    FillArea = true
                }};
                string[] xlbls = _growthDates.Select(d => d.ToString("M/d")).ToArray();
                ChartRenderer.DrawLineChart(e.Graphics, _growthPanel.ClientRectangle,
                    series, xlbls, "每日新增帳號", "人");
            };

            card.Dock = DockStyle.Fill;
            outer.Controls.Add(card);
            outer.Controls.Add(info);
            tab.Controls.Add(outer);
        }

        // ═══════════════════════════════════════════════
        // Tab3 — 留存分析
        // ═══════════════════════════════════════════════
        private void BuildTab3(TabPage tab)
        {
            var outer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16), BackColor = Theme.BgPage };
            outer.Controls.Add(new Label
            {
                Text = "玩家留存率：計算各時間段內新註冊玩家中，有在該時間段內登入過的比例",
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                Location = new Point(4, 4), AutoSize = true
            });

            _retentionArea = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgPage };
            outer.Controls.Add(_retentionArea);
            tab.Controls.Add(outer);
        }

        private void RebuildRetentionCards()
        {
            if (_retention == null || _retentionArea == null) return;
            _retentionArea.Controls.Clear();

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 160, FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false, Padding = new Padding(0, 24, 0, 0)
            };

            Color[] rColors = {
                Color.FromArgb(64,158,255), Color.FromArgb(103,194,58),
                Color.FromArgb(230,162,60), Color.FromArgb(245,108,108)
            };
            int ci = 0;
            foreach (var kv in _retention)
            {
                var (cohort, retained, rate) = kv.Value;
                Color c = rColors[ci++ % rColors.Length];
                var card = new Panel { Width = 240, Height = 120, BackColor = Theme.BgCard, Margin = new Padding(0, 0, 16, 0) };

                card.Controls.Add(new Label { Text = kv.Key + " 留存率", ForeColor = Theme.TextSecondary, Font = Theme.FontSmall, Location = new Point(14, 12), AutoSize = true });
                card.Controls.Add(new Label
                {
                    Text = $"{rate:0.#}%", ForeColor = c,
                    Font = new Font(Theme.FontFamily, 26f, FontStyle.Bold),
                    Location = new Point(14, 30), AutoSize = true
                });
                card.Controls.Add(new Label
                {
                    Text = $"樣本：{cohort} 人  回訪：{retained} 人",
                    ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                    Location = new Point(14, 86), AutoSize = true
                });

                // 底部進度條
                var prog = new Panel { Size = new Size(212, 6), Location = new Point(14, 76), BackColor = Color.FromArgb(40, 42, 55) };
                var fill = new Panel { Size = new Size((int)(212 * rate / 100), 6), BackColor = c, Location = new Point(0, 0) };
                prog.Controls.Add(fill);
                card.Controls.Add(prog);

                flow.Controls.Add(card);
            }
            _retentionArea.Controls.Add(flow);

            // 說明文字
            var note = new Label
            {
                Text = "⚠ 留存率以「有無在統計期間內再度登入」計算，若 created_at 欄位無資料則數字可能偏低。",
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                AutoSize = false, Size = new Size(_retentionArea.Width - 16, 24),
                Location = new Point(0, 180), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _retentionArea.Controls.Add(note);
        }

        // ═══════════════════════════════════════════════
        // Tab4 — 沉睡玩家
        // ═══════════════════════════════════════════════
        private void BuildTab4(TabPage tab)
        {
            var outer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 12, 16, 12), BackColor = Theme.BgPage };

            var toolbar = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Theme.BgPage };
            toolbar.Controls.Add(new Label { Text = "顯示超過以下天數未登入的玩家：", ForeColor = Theme.TextSecondary, Font = Theme.FontBody, AutoSize = true, Location = new Point(0, 10) });

            var cmb = new ComboBox { Location = new Point(230, 8), Width = 120, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary };
            cmb.Items.AddRange(new object[] { "30 天", "60 天", "90 天", "180 天", "365 天" });
            cmb.SelectedIndex = 0;
            var btnLoad = Theme.MakePrimaryButton("查詢", 70, 28);
            btnLoad.Location = new Point(358, 7);
            toolbar.Controls.Add(cmb);
            toolbar.Controls.Add(btnLoad);

            _sleepDgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(_sleepDgv);
            _sleepDgv.ReadOnly = true;
            _sleepDgv.AllowUserToAddRows = false;
            _sleepDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cRank", HeaderText = "#", Width = 46, SortMode = DataGridViewColumnSortMode.NotSortable, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            _sleepDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cName",    HeaderText = "角色名稱", Width = 160, SortMode = DataGridViewColumnSortMode.NotSortable });
            _sleepDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cAccount", HeaderText = "帳號",    Width = 160, SortMode = DataGridViewColumnSortMode.NotSortable });
            _sleepDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cLast",    HeaderText = "最後登入", Width = 180, SortMode = DataGridViewColumnSortMode.NotSortable });
            _sleepDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cDays",    HeaderText = "離開天數", Width = 100, SortMode = DataGridViewColumnSortMode.NotSortable, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });

            int[] dayOptions = { 30, 60, 90, 180, 365 };
            btnLoad.Click += async (s, e) =>
            {
                btnLoad.Enabled = false;
                _lblStatus.Text = "查詢沉睡玩家…";
                try
                {
                    int days = dayOptions[cmb.SelectedIndex];
                    var rows = await DatabaseManager.Instance.GetInactivePlayersAsync(days, 500);
                    _sleepDgv.Rows.Clear();
                    int rank = 1;
                    foreach (var (name, acc, last, d) in rows)
                        _sleepDgv.Rows.Add(rank++, name, acc, last, $"{d} 天");
                    _lblStatus.Text = $"找到 {rows.Count} 位超過 {days} 天未登入的玩家";
                }
                finally { btnLoad.Enabled = true; }
            };

            outer.Controls.Add(_sleepDgv);
            outer.Controls.Add(toolbar);
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
                    _lblTotal.Text       = stats.TotalPlayers.ToString("N0");
                    _lblOnline.Text      = stats.OnlineCount.ToString("N0");
                    _lblTodayNew.Text    = stats.TodayNewPlayers.ToString("N0");
                    _lblTodayActive.Text = stats.TodayActive.ToString("N0");
                });

                // 時段分佈
                _hourData = await db.GetLoginHourDistributionAsync();
                _weekData = await db.GetLoginWeekdayDistributionAsync();

                // 成長曲線
                var (dates, counts) = await db.GetDailyNewAccountsAsync(30);
                _growthDates  = dates;
                _growthCounts = counts;

                // 留存
                _retention = await db.GetRetentionAsync();

                // 重繪
                SafeSet(() =>
                {
                    _hourPanel?.Invalidate();
                    _weekPanel?.Invalidate();
                    _growthPanel?.Invalidate();
                    RebuildRetentionCards();
                    _lblStatus.Text = $"✓ 資料已更新  {DateTime.Now:HH:mm:ss}";
                });
            }
            catch (Exception ex)
            {
                SafeSet(() => _lblStatus.Text = "✗ 載入失敗：" + ex.Message);
            }
            finally { SafeSet(() => _btnRefresh.Enabled = true); }
        }

        private void SafeSet(Action a) { if (!IsDisposed) try { if (InvokeRequired) Invoke(a); else a(); } catch { } }

        // ── 通用帶標題的圖表卡片 ────────────────────────────────
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
