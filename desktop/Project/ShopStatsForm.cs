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
        private ComboBox _preset;
        private DateTimePicker _dtFrom;
        private DateTimePicker _dtTo;
        private Button _btnApply;
        private bool _syncingPreset;
        private readonly CheckBox[] _shopChecks = new CheckBox[4];

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
            Theme.ApplyHubForm(this);
            Text          = "🏪 商城熱賣分析";
            Size          = new Size(1100, 720);
            MinimumSize   = new Size(860, 520);
            BackColor = Theme.BgPage;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;

            BuildUI();
            // SelectedIndex=2 已觸發 Preset_SelectedIndexChanged → 首次載入
        }

        private void BuildUI()
        {
            var toolbar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 116,
                BackColor = Theme.BgDark,
                Padding   = new Padding(10, 6, 10, 6)
            };

            var tbl = new TableLayoutPanel
            {
                Dock          = DockStyle.Fill,
                ColumnCount   = 1,
                RowCount      = 3,
                BackColor     = Color.Transparent
            };
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));

            var row1 = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            _lblStatus = new Label
            {
                Text      = "載入中…",
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            var btnRefresh = Theme.MakePrimaryButton("🔄 重新整理", 110, 26);
            btnRefresh.Dock   = DockStyle.Right;
            btnRefresh.Margin = new Padding(0, 2, 0, 2);
            btnRefresh.Click += (s, e) => _ = LoadAllAsync();
            row1.Controls.Add(_lblStatus);
            row1.Controls.Add(btnRefresh);

            var rowShop = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents  = false,
                BackColor     = Color.Transparent,
                Padding       = new Padding(0, 2, 0, 0)
            };
            rowShop.Controls.Add(MakeMutedLabel("載入商城："));
            for (int i = 0; i < Shops.Length; i++)
            {
                var (title, _, icon, _, _) = Shops[i];
                _shopChecks[i] = new CheckBox
                {
                    Text   = $"{icon} {title}",
                    AutoSize = true,
                    Checked  = true,
                    Margin   = new Padding(10, 4, 0, 0)
                };
                Theme.StyleCheckBox(_shopChecks[i]);
                rowShop.Controls.Add(_shopChecks[i]);
            }

            var row2 = new FlowLayoutPanel
            {
                Dock               = DockStyle.Fill,
                FlowDirection      = FlowDirection.LeftToRight,
                WrapContents       = false,
                AutoSize           = false,
                BackColor          = Color.Transparent,
                Padding            = new Padding(0, 4, 0, 0)
            };

            row2.Controls.Add(MakeMutedLabel("📅 統計區間"));
            _preset = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width         = 120,
                Margin        = new Padding(0, 2, 12, 0),
                BackColor     = Theme.BgLight,
                ForeColor     = Theme.TextPrimary,
                FlatStyle     = FlatStyle.Flat
            };
            _preset.Items.AddRange(new object[] { "全部（累計）", "最近 7 天", "最近 30 天", "本月", "自訂區間…" });
            _preset.SelectedIndex = 2;
            _preset.SelectedIndexChanged += Preset_SelectedIndexChanged;

            _dtFrom = MakeDatePicker();
            _dtTo   = MakeDatePicker();

            row2.Controls.Add(_preset);
            row2.Controls.Add(MakeMutedLabel("自"));
            row2.Controls.Add(_dtFrom);
            row2.Controls.Add(MakeMutedLabel("至"));
            row2.Controls.Add(_dtTo);

            _btnApply = Theme.MakeSecondaryButton("套用", 72, 26);
            _btnApply.Margin = new Padding(8, 2, 0, 0);
            _btnApply.Click += (s, e) => _ = LoadAllAsync();
            row2.Controls.Add(_btnApply);

            _dtFrom.ValueChanged += DatePicker_ValueChanged;
            _dtTo.ValueChanged   += DatePicker_ValueChanged;

            tbl.Controls.Add(row1, 0, 0);
            tbl.Controls.Add(rowShop, 0, 1);
            tbl.Controls.Add(row2, 0, 2);
            toolbar.Controls.Add(tbl);

            _tabs = new TabControl
            {
                Dock      = DockStyle.Fill,
                Font      = new Font(Theme.FontFamily, 10f),
                BackColor = Theme.BgPage
            };
            Theme.StyleTabControl(_tabs);

            foreach (var (title, table, icon, unit, accent) in Shops)
            {
                var tp = new TabPage($"  {icon} {title}  ");
                tp.BackColor = Theme.BgPage;
                tp.Tag       = (table, unit, accent);
                _tabs.TabPages.Add(tp);
            }

            Controls.Add(_tabs);
            Controls.Add(toolbar);

            UpdateDatePickersEnabled();
        }

        private void RunUi(Action a)
        {
            if (InvokeRequired) Invoke(a);
            else a();
        }

        private static void ShowShopSkipped(TabPage tp, string title)
        {
            tp.Controls.Clear();
            tp.Controls.Add(new Label
            {
                Text      = $"未勾選「{title}」\n請在上方勾選商城後按「重新整理」",
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontHeader,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            });
        }

        private static Label MakeMutedLabel(string t) =>
            new Label
            {
                Text      = t,
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                AutoSize  = true,
                Margin    = new Padding(0, 6, 4, 0)
            };

        private DateTimePicker MakeDatePicker() =>
            new DateTimePicker
            {
                Format    = DateTimePickerFormat.Short,
                Width     = 118,
                Margin    = new Padding(0, 2, 6, 0),
                CalendarMonthBackground = Theme.BgCard,
                BackColor = Theme.BgLight,
                ForeColor = Theme.TextPrimary
            };

        private void Preset_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_syncingPreset) return;
            if (_preset.SelectedIndex >= 0 && _preset.SelectedIndex <= 3)
                ApplyPresetToPickers(_preset.SelectedIndex);
            UpdateDatePickersEnabled();
            if (_preset.SelectedIndex != 4)
                _ = LoadAllAsync();
        }

        private void DatePicker_ValueChanged(object? sender, EventArgs e)
        {
            if (_syncingPreset) return;
            if (_preset.SelectedIndex == 4)
                return;
            _syncingPreset = true;
            _preset.SelectedIndex = 4;
            _syncingPreset = false;
            UpdateDatePickersEnabled();
        }

        private void ApplyPresetToPickers(int presetIndex)
        {
            _syncingPreset = true;
            try
            {
                var today = DateTime.Today;
                switch (presetIndex)
                {
                    case 0:
                        _dtFrom.Value = today.AddYears(-5);
                        _dtTo.Value   = today;
                        break;
                    case 1:
                        _dtFrom.Value = today.AddDays(-6);
                        _dtTo.Value   = today;
                        break;
                    case 2:
                        _dtFrom.Value = today.AddDays(-29);
                        _dtTo.Value   = today;
                        break;
                    case 3:
                        _dtFrom.Value = new DateTime(today.Year, today.Month, 1);
                        _dtTo.Value   = today;
                        break;
                }
            }
            finally { _syncingPreset = false; }
        }

        private void UpdateDatePickersEnabled()
        {
            bool custom = _preset.SelectedIndex == 4;
            _dtFrom.Enabled = true;
            _dtTo.Enabled   = true;
            _btnApply.Visible = custom;
            if (!custom)
            {
                _dtFrom.CalendarForeColor = Theme.TextPrimary;
                _dtTo.CalendarForeColor   = Theme.TextPrimary;
            }
        }

        /// <summary>回傳查詢用日期：Item1/Item2 皆 null 表示全時段。</summary>
        private (DateTime? from, DateTime? to) GetDateRangeForQuery()
        {
            switch (_preset.SelectedIndex)
            {
                case 0: return (null, null);
                case 1: return (DateTime.Today.AddDays(-6), DateTime.Today);
                case 2: return (DateTime.Today.AddDays(-29), DateTime.Today);
                case 3: return (new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), DateTime.Today);
                default:
                    return (_dtFrom.Value.Date, _dtTo.Value.Date);
            }
        }

        private string FormatRangeLabel(DateTime? from, DateTime? to)
        {
            if (!from.HasValue || !to.HasValue)
                return "全時段（累計）";
            if (from.Value.Date == to.Value.Date)
                return $"單日 {from.Value:yyyy/MM/dd}";
            return $"{from.Value:yyyy/MM/dd} ～ {to.Value:yyyy/MM/dd}";
        }

        private async Task LoadAllAsync()
        {
            var (from, to) = GetDateRangeForQuery();

            int enabled = 0;
            for (int i = 0; i < _shopChecks.Length; i++)
                if (_shopChecks[i].Checked) enabled++;
            if (enabled == 0)
            {
                MessageBox.Show("請至少勾選一個要載入的商城。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _lblStatus.Text = "查詢中…";
            _lblStatus.ForeColor = Theme.TextMuted;

            var tasks = new List<Task>();
            for (int i = 0; i < _tabs.TabPages.Count; i++)
            {
                var tp = _tabs.TabPages[i];
                if (!_shopChecks[i].Checked)
                {
                    var t = Shops[i].Title;
                    RunUi(() => ShowShopSkipped(tp, t));
                    continue;
                }
                var (table, unit, accent) = ((string, string, Color))tp.Tag;
                tasks.Add(LoadTabAsync(tp, table, unit, accent, from, to));
            }
            await Task.WhenAll(tasks);

            _lblStatus.Text = $"✓ 已更新  ·  {FormatRangeLabel(from, to)}  ·  {DateTime.Now:HH:mm:ss}";
            _lblStatus.ForeColor = Theme.AccentGreen;
        }

        private async Task LoadTabAsync(TabPage tp, string table, string unit, Color accent, DateTime? from, DateTime? to)
        {
            try
            {
                var (items, spenders) = await DatabaseManager.Instance.GetShopTopItemsAsync(table, 20, from, to);

                Invoke(new Action(() =>
                {
                    tp.Controls.Clear();

                    if (items.Count == 0 && spenders.Count == 0)
                    {
                        tp.Controls.Add(new Label
                        {
                            Text      = $"此區間尚無購買記錄\n（{FormatRangeLabel(from, to)} · {table}）",
                            ForeColor = Theme.TextMuted,
                            Font      = Theme.FontHeader,
                            Dock      = DockStyle.Fill,
                            TextAlign = System.Drawing.ContentAlignment.MiddleCenter
                        });
                        return;
                    }

                    const int p1min = 300, p2min = 240;
                    var split = new SplitContainer
                    {
                        Dock          = DockStyle.Fill,
                        Orientation   = Orientation.Vertical,
                        BackColor = Theme.BgPage,
                        SplitterWidth = 6
                    };

                    BuildItemPanel(split.Panel1, items, unit, accent);
                    BuildSpenderPanel(split.Panel2, spenders, unit, accent);

                    tp.Controls.Add(split);

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

        private static Panel MakeSectionHeader(string title, Color accent, Color? subtitleColor = null)
        {
            var wrap = new Panel { Dock = DockStyle.Top, Height = Theme.ToolbarHeight, BackColor = Theme.BgCard };
            var accentBar = new Panel { Width = 4, Dock = DockStyle.Left, BackColor = accent };
            var inner = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 10, 12, 8), BackColor = Color.Transparent };
            inner.Controls.Add(new Label
            {
                Text      = title,
                ForeColor = subtitleColor ?? accent,
                Font      = Theme.FontHeader,
                Dock      = DockStyle.Top,
                AutoSize  = true
            });
            wrap.Controls.Add(accentBar);
            wrap.Controls.Add(inner);
            return wrap;
        }

        private void BuildItemPanel(SplitterPanel panel, List<ShopSaleRecord> items, string unit, Color accent)
        {
            panel.Controls.Add(MakeSectionHeader($"🔥 熱賣道具 TOP（依購買數量） · 共 {items.Count} 筆", accent));

            var dgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(dgv);
            Theme.EnableSmoothPaint(dgv);
            dgv.ReadOnly = true;
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
                if (col == "colQty") { e.CellStyle.ForeColor = accent; e.CellStyle.Font = Theme.FontCell9Bold; e.FormattingApplied = true; }
                if (col == "colCost") { e.CellStyle.ForeColor = Color.FromArgb(220, 180, 80); e.FormattingApplied = true; }
            };

            foreach (var rec in items)
            {
                string last = string.IsNullOrEmpty(rec.LastBuyTime) ? "—" : (rec.LastBuyTime.Length > 16 ? rec.LastBuyTime[..16] : rec.LastBuyTime);
                int i = dgv.Rows.Add(
                    rec.Rank == 1 ? "🥇 1" : rec.Rank == 2 ? "🥈 2" : rec.Rank == 3 ? "🥉 3" : $"  {rec.Rank}",
                    $"#{rec.ItemId}",
                    rec.ItemName,
                    $"{rec.TotalQty:N0}",
                    $"{rec.OrderCount:N0} 筆",
                    rec.TotalCost > 0 ? $"{rec.TotalCost:N0}" : "—",
                    last);
                dgv.Rows[i].Tag = rec;
            }

            panel.Controls.Add(dgv);
        }

        private void BuildSpenderPanel(SplitterPanel panel, List<ShopSpenderRecord> spenders, string unit, Color accent)
        {
            panel.Controls.Add(MakeSectionHeader($"💸 玩家消費排行 · 共 {spenders.Count} 人", Color.FromArgb(255, 160, 80)));

            var dgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(dgv);
            Theme.EnableSmoothPaint(dgv);
            dgv.ReadOnly = true;
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
                if (col == "colCost") { e.CellStyle.ForeColor = Color.FromArgb(255, 160, 60); e.CellStyle.Font = Theme.FontCell9Bold; e.FormattingApplied = true; }
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
