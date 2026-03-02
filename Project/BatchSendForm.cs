using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    public class BatchSendForm : Form
    {
        // ── 購物車 ──
        private class CartEntry
        {
            public ItemInfo Item { get; set; }
            public int Qty  { get; set; } = 1;
        }
        private readonly List<CartEntry> _cart = new();

        // ── 左側（道具選擇）──
        private TextBox      _searchBox;
        private Label        _itemCountLbl;
        private DataGridView _itemDgv;
        private Label        _pageLabel;
        private Button       _btnPrev, _btnNext;

        // ── 右側（發送設定）──
        private DataGridView  _cartDgv;
        private TextBox       _txtTitle, _txtContent;
        private CheckBox      _chkSchedule;
        private DateTimePicker _dtStart, _dtEnd;
        private ComboBox      _cmbBatchSize;
        private Button        _sendBtn, _cancelBtn;
        private ProgressBar   _progressBar;
        private Label         _progressLbl, _statusLbl;
        private RichTextBox   _logBox;

        // ── 排除名單 ──
        private readonly HashSet<string> _excludeSet = new(StringComparer.OrdinalIgnoreCase);
        private ListBox  _excludeListBox;
        private TextBox  _excludeSearchBox;
        private Label    _excludeCountLbl;

        // ── 狀態 ──
        private List<ItemInfo> _filteredItems = new();
        private int            _currentPage = 0;
        private const int      PageSize     = 50;
        private int            MaxPage => Math.Max(1, (_filteredItems.Count + PageSize - 1) / PageSize);

        private CancellationTokenSource _cts;
        private bool _isSending;

        public BatchSendForm()
        {
            InitUI();
            ApplyFilter();
        }

        // ═══════════════════════════════════════════════════════════
        // 主佈局（與 SendForm 相同結構）
        // ═══════════════════════════════════════════════════════════
        private void InitUI()
        {
            Text          = "📢 批量發送禮包";
            Size          = new Size(1120, 740);
            MinimumSize   = new Size(860, 560);
            BackColor     = Theme.BgMid;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;
            FormClosing  += (s, e) => { if (_isSending) e.Cancel = true; };

            // ── ① Header ──────────────────────────────────────────
            var header = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme.BgDark };
            header.Controls.Add(new Label
            {
                Text      = "  📢  批量發送禮包 — 將發送給所有角色（郵件信箱）",
                ForeColor = Theme.AccentOrange,
                Font      = Theme.FontBody,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(8, 0, 0, 0)
            });

            // ── ② 搜尋列 ──────────────────────────────────────────
            var searchPanel = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 56,
                BackColor = Theme.BgCard
            };
            searchPanel.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme.Border });
            searchPanel.Controls.Add(new Label
            {
                Text      = "STEP 1 — 輸入名稱、編號或說明關鍵字搜尋，或直接翻頁瀏覽",
                ForeColor = Theme.TextMuted,
                Font      = new Font(Theme.FontFamily, 8.5f),
                AutoSize  = true,
                Location  = new Point(12, 4)
            });

            var searchIcon = new Label
            {
                Text      = "🔍",
                Font      = new Font("Segoe UI Emoji", 14f),
                AutoSize  = true,
                Location  = new Point(12, 22)
            };

            _searchBox = new TextBox
            {
                BackColor       = Theme.BgPage,
                ForeColor       = Theme.TextPrimary,
                BorderStyle     = BorderStyle.FixedSingle,
                Font            = new Font(Theme.FontFamily, 11f),
                PlaceholderText = "道具名稱 / 編號 / 說明關鍵字",
                Location        = new Point(42, 22),
                Height          = 28,
                Anchor          = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _searchBox.TextChanged += (s, e) => { _currentPage = 0; ApplyFilter(); };
            _searchBox.KeyDown     += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { _currentPage = 0; ApplyFilter(); e.Handled = true; }
                if (e.KeyCode == Keys.Down && _itemDgv.Rows.Count > 0)
                    { _itemDgv.Focus(); _itemDgv.CurrentCell = _itemDgv.Rows[0].Cells[1]; }
            };

            var searchBtn = Theme.MakePrimaryButton("搜尋", 80, 28);
            searchBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            searchBtn.Click += (s, e) => { _currentPage = 0; ApplyFilter(); };

            searchPanel.Controls.AddRange(new Control[] { searchIcon, _searchBox, searchBtn });
            searchPanel.Resize += (s, e) =>
            {
                int pw = searchPanel.ClientSize.Width;
                searchBtn.Left   = pw - 12 - searchBtn.Width;
                searchBtn.Top    = 32;
                _searchBox.Width = Math.Max(100, searchBtn.Left - _searchBox.Left - 8);
            };

            // ── ③ SplitContainer（Fill）──────────────────────────
            var split = new SplitContainer
            {
                Dock          = DockStyle.Fill,
                Orientation   = Orientation.Vertical,
                BackColor     = Theme.Border,
                SplitterWidth = 3
            };
            split.Panel1.BackColor = Theme.BgMid;
            split.Panel2.BackColor = Theme.BgMid;
            split.HandleCreated += (_, __) =>
            {
                try
                {
                    split.Panel1MinSize    = 320;
                    split.Panel2MinSize    = 320;
                    if (split.Width >= 640)
                        split.SplitterDistance = Math.Max(320, Math.Min(split.Width - 320, 480));
                }
                catch { }
            };
            BuildLeftPanel(split.Panel1);
            BuildRightPanel(split.Panel2);

            Controls.Add(split);
            Controls.Add(searchPanel);
            Controls.Add(header);
        }

        // ═══════════════════════════════════════════════════════════
        // 左側：道具清單（與 SendForm 完全相同）
        // ═══════════════════════════════════════════════════════════
        private void BuildLeftPanel(Panel p)
        {
            var layout = new TableLayoutPanel
            {
                Dock            = DockStyle.Fill,
                ColumnCount     = 1,
                RowCount        = 3,
                Margin          = Padding.Empty,
                Padding         = Padding.Empty,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

            var titleBar = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(14, 28, 52) };
            _itemCountLbl = new Label
            {
                Text      = "📦  道具清單  ←  雙擊任一列加入購物車",
                ForeColor = Theme.AccentBlue,
                Font      = Theme.FontHeader,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(10, 0, 0, 0),
            };
            titleBar.Controls.Add(_itemCountLbl);
            layout.Controls.Add(titleBar, 0, 0);

            _itemDgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(_itemDgv);
            _itemDgv.ReadOnly              = true;
            _itemDgv.MultiSelect           = false;
            _itemDgv.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            _itemDgv.AllowUserToResizeRows = false;
            _itemDgv.ColumnHeadersHeight   = 28;
            _itemDgv.RowTemplate.Height    = 24;

            _itemDgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colIdx", HeaderText = "序號", Width = 52,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _itemDgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colName", HeaderText = "道具名稱", Width = 160,
                SortMode = DataGridViewColumnSortMode.NotSortable,
            });
            _itemDgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colId", HeaderText = "編號", Width = 70,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _itemDgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDesc", HeaderText = "說明",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                SortMode = DataGridViewColumnSortMode.NotSortable,
            });

            _itemDgv.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0) SelectItem(e.RowIndex);
            };
            _itemDgv.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && _itemDgv.CurrentRow != null)
                { SelectItem(_itemDgv.CurrentRow.Index); e.Handled = true; }
            };
            _itemDgv.CellToolTipTextNeeded += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                if (_itemDgv.Rows[e.RowIndex].Tag is ItemInfo item)
                    e.ToolTipText = $"名稱：{item.Name}\n道具編號：{item.Id}\n說明：{item.Description}";
            };
            layout.Controls.Add(_itemDgv, 0, 1);

            var navBar = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgDark, Padding = new Padding(6) };
            _btnPrev = new Button
            {
                Text = "◀ 上頁", Width = 80, Dock = DockStyle.Left,
                BackColor = Theme.BgLight, ForeColor = Theme.TextMuted,
                FlatStyle = FlatStyle.Flat, Font = Theme.FontSmall, Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
            };
            _btnPrev.FlatAppearance.BorderColor = Theme.Border;
            _btnPrev.Click += (s, e) => { if (_currentPage > 0) { _currentPage--; FillPage(); } };

            _btnNext = new Button
            {
                Text = "下頁 ▶", Width = 80, Dock = DockStyle.Right,
                BackColor = Theme.BgLight, ForeColor = Theme.TextMuted,
                FlatStyle = FlatStyle.Flat, Font = Theme.FontSmall, Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
            };
            _btnNext.FlatAppearance.BorderColor = Theme.Border;
            _btnNext.Click += (s, e) => { if (_currentPage < MaxPage - 1) { _currentPage++; FillPage(); } };

            _pageLabel = new Label
            {
                Text = "載入中…", ForeColor = Theme.TextSecondary, Font = Theme.FontSmall,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
            };
            navBar.Controls.Add(_btnPrev);
            navBar.Controls.Add(_btnNext);
            navBar.Controls.Add(_pageLabel);
            layout.Controls.Add(navBar, 0, 2);

            p.Controls.Add(layout);
        }

        // ═══════════════════════════════════════════════════════════
        // 右側：發送設定 + 進度記錄
        // ═══════════════════════════════════════════════════════════
        private void BuildRightPanel(Panel p)
        {
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            scroll.HorizontalScroll.Enabled = false;
            scroll.HorizontalScroll.Visible = false;
            p.Controls.Add(scroll);

            int y = 12, x = 14;

            // ── 購物車標題列 ────────────────────────────────────────
            var cartHdrPanel = new Panel
            {
                Location  = new Point(x, y),
                Size      = new Size(460, 30),
                BackColor = Theme.BgDark
            };
            cartHdrPanel.Controls.Add(new Label
            {
                Text      = "  🛒  道具購物車 — 雙擊左側加入（可多種道具）",
                ForeColor = Color.FromArgb(100, 180, 255),
                Font      = new Font(Theme.FontFamily, 9f, FontStyle.Bold),
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            });
            var btnClear = Theme.MakeButton("🗑 清空", Theme.AccentRed, Color.White, 62, 24);
            btnClear.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClear.Font   = Theme.FontSmall;
            btnClear.Click += (s, e) => { _cart.Clear(); RefreshCartDgv(); };
            cartHdrPanel.Controls.Add(btnClear);
            cartHdrPanel.Resize += (s, e) => btnClear.Left = cartHdrPanel.ClientSize.Width - 4 - btnClear.Width;
            scroll.Controls.Add(cartHdrPanel);
            y += 32;

            // ── 購物車 DGV ──────────────────────────────────────────
            _cartDgv = new DataGridView
            {
                Location              = new Point(x, y),
                Size                  = new Size(460, 140),
                ReadOnly              = false,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                RowTemplate           = { Height = 26 },
                ColumnHeadersHeight   = 26,
                MultiSelect           = false,
                BackgroundColor       = Theme.BgCard,
                GridColor             = Theme.Border,
                BorderStyle           = BorderStyle.None
            };
            Theme.StyleDataGridView(_cartDgv);
            _cartDgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "cName", HeaderText = "道具名稱",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable
            });
            _cartDgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "cId", HeaderText = "編號", Width = 66, ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _cartDgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "cQty", HeaderText = "每人數量", Width = 72,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _cartDgv.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "cRemove", HeaderText = "", Width = 42,
                Text = "✕", UseColumnTextForButtonValue = true, FlatStyle = FlatStyle.Flat,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = {
                    BackColor = Theme.AccentRed, ForeColor = Color.White,
                    SelectionBackColor = Theme.AccentRed,
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                }
            });
            _cartDgv.CellEndEdit += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= _cart.Count) return;
                if (_cartDgv.Columns[e.ColumnIndex].Name == "cQty")
                {
                    var raw = _cartDgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? "1";
                    if (int.TryParse(raw, out int q)) _cart[e.RowIndex].Qty = Math.Max(1, q);
                    RefreshCartDgv();
                }
            };
            _cartDgv.CellClick += (s, e) =>
            {
                if (e.RowIndex < 0 || _cartDgv.Columns[e.ColumnIndex].Name != "cRemove") return;
                _cart.RemoveAt(e.RowIndex);
                RefreshCartDgv();
            };
            scroll.Controls.Add(_cartDgv);
            y += 148;

            // ── 發送設定面板 ──
            var settingPanel = new Panel
            {
                Location    = new Point(x, y),
                Size        = new Size(440, 110),
                BackColor   = Theme.BgCard,
                BorderStyle = BorderStyle.FixedSingle
            };

            int sy = 10;
            void AddRow(string lblTxt, Control ctrl, int ctrlW, string hint = null)
            {
                settingPanel.Controls.Add(new Label
                {
                    Text = lblTxt, ForeColor = Theme.TextSecondary, Font = Theme.FontBody,
                    Width = 72, Location = new Point(8, sy + 3), TextAlign = ContentAlignment.MiddleRight
                });
                ctrl.Location = new Point(84, sy);
                ctrl.Width    = ctrlW;
                settingPanel.Controls.Add(ctrl);
                if (hint != null)
                    settingPanel.Controls.Add(new Label
                    {
                        Text = hint, ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                        AutoSize = true, Location = new Point(84 + ctrlW + 6, sy + 4)
                    });
                sy += ctrl.Height + 8;
            }

            _txtTitle = new TextBox
            {
                Width = 300, Height = 28, MaxLength = 60,
                BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary, Font = Theme.FontBody,
                PlaceholderText = "預設使用道具名稱"
            };
            _txtContent = new TextBox
            {
                Width = 300, Height = 28, MaxLength = 120,
                BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary, Font = Theme.FontBody,
                PlaceholderText = "預設使用道具名稱"
            };

            // ── 預約發送 ──
            _chkSchedule = new CheckBox
            {
                Text = "預約發送時間", ForeColor = Theme.TextSecondary, Font = Theme.FontBody,
                FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent,
                AutoSize = true, Checked = false
            };
            _dtStart = new DateTimePicker
            {
                Format  = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy/MM/dd  HH:mm",
                Width   = 180, Height = 28,
                Value   = DateTime.Now.AddHours(1),
                Enabled = false
            };
            _chkSchedule.CheckedChanged += (s, e) => _dtStart.Enabled = _chkSchedule.Checked;

            _dtEnd = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short, Width = 140, Height = 28,
                Value  = DateTime.Now.AddDays(30)
            };

            // 範本按鈕（橫跨兩欄，放在標題列右側）
            var tplBtn = Theme.MakeTemplateButton(_txtTitle, _txtContent);

            AddRow("標      題：", _txtTitle,  260);
            // 把範本按鈕貼在標題列右側
            tplBtn.Location = new Point(84 + 266, sy - 28 - 8 + 2);
            settingPanel.Controls.Add(tplBtn);

            AddRow("信件內容：", _txtContent, 260);
            AddRow("到期日期：", _dtEnd,       140, "（預設 30 天）");

            // ── 發送速度 ──
            _cmbBatchSize = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 200, Height = 28,
                BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary, Font = Theme.FontBody
            };
            _cmbBatchSize.Items.AddRange(new object[]
            {
                "🐢  標準（每批 20 筆）",
                "🚶  正常（每批 50 筆）",
                "🚀  快速（每批 100 筆）",
                "⚡  極速（每批 200 筆）",
                "💥  全速（每批 500 筆）"
            });
            _cmbBatchSize.SelectedIndex = 2; // 預設快速
            AddRow("發送速度：", _cmbBatchSize, 200);

            // 預約發送列（特殊排版：checkbox + DateTimePicker 同行）
            {
                settingPanel.Controls.Add(new Label
                {
                    Text = "發送時間：", ForeColor = Theme.TextSecondary, Font = Theme.FontBody,
                    Width = 72, Location = new Point(8, sy + 3), TextAlign = ContentAlignment.MiddleRight
                });
                _chkSchedule.Location = new Point(84, sy + 2);
                _dtStart.Location     = new Point(84 + _chkSchedule.Width + 6, sy);
                settingPanel.Controls.AddRange(new Control[] { _chkSchedule, _dtStart });
                settingPanel.Controls.Add(new Label
                {
                    Text = "（不勾選 = 立即）", ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                    AutoSize = true, Location = new Point(_dtStart.Right + 6, sy + 4)
                });
                sy += 36;
            }

            settingPanel.Height = sy + 10;
            scroll.Controls.Add(settingPanel);
            y += settingPanel.Height + 10;

            // ── 排除名單區塊 ──
            var excludePanel = new Panel
            {
                Location    = new Point(x, y),
                Size        = new Size(460, 180),
                BackColor   = Theme.BgCard,
                BorderStyle = BorderStyle.FixedSingle
            };

            var excludeHdr = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = Color.FromArgb(60, 30, 30) };
            _excludeCountLbl = new Label
            {
                Text      = "🚫  排除名單（0 人）",
                ForeColor = Theme.AccentRed,
                Font      = new Font(Theme.FontFamily, 9f, FontStyle.Bold),
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(6, 0, 0, 0)
            };
            var btnClearExclude = Theme.MakeButton("全部移除", Theme.AccentRed, Color.White, 72, 22);
            btnClearExclude.Font   = Theme.FontSmall;
            btnClearExclude.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClearExclude.Click += (s, e) => { _excludeSet.Clear(); RefreshExcludeList(); };
            excludeHdr.Controls.Add(_excludeCountLbl);
            excludeHdr.Controls.Add(btnClearExclude);
            excludeHdr.Resize += (s, e) => btnClearExclude.Left = excludeHdr.ClientSize.Width - 4 - btnClearExclude.Width;
            excludePanel.Controls.Add(excludeHdr);

            // 搜尋帳號加入排除
            var exSearchRow = new Panel { Top = 30, Left = 0, Height = 30, Dock = DockStyle.None };
            _excludeSearchBox = new TextBox
            {
                PlaceholderText = "輸入帳號加入排除…",
                BackColor       = Theme.BgLight,
                ForeColor       = Theme.TextPrimary,
                BorderStyle     = BorderStyle.FixedSingle,
                Font            = Theme.FontBody,
                Location        = new Point(6, 4),
                Height          = 24
            };
            var btnAddExclude = Theme.MakeButton("＋ 加入", Theme.AccentOrange, Color.White, 70, 24);
            btnAddExclude.Font     = Theme.FontSmall;
            btnAddExclude.Location = new Point(0, 4);
            btnAddExclude.Anchor   = AnchorStyles.Top | AnchorStyles.Right;

            void AddExcludeAccount()
            {
                var acc = _excludeSearchBox.Text.Trim();
                if (string.IsNullOrEmpty(acc)) return;
                _excludeSet.Add(acc);
                _excludeSearchBox.Clear();
                RefreshExcludeList();
            }
            btnAddExclude.Click       += (s, e) => AddExcludeAccount();
            _excludeSearchBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { AddExcludeAccount(); e.Handled = true; } };

            var exRow = new Panel
            {
                Top    = 30,
                Left   = 0,
                Height = 30,
                Dock   = DockStyle.None
            };
            excludePanel.Controls.Add(exRow);
            // 用 Resize 動態排版
            excludePanel.Controls.Add(_excludeSearchBox);
            excludePanel.Controls.Add(btnAddExclude);
            _excludeSearchBox.Top  = 32;
            _excludeSearchBox.Left = 6;
            btnAddExclude.Top      = 32;

            _excludeListBox = new ListBox
            {
                Top             = 62,
                Left            = 6,
                Height          = 100,
                BackColor       = Color.FromArgb(28, 18, 18),
                ForeColor       = Theme.TextPrimary,
                Font            = Theme.FontSmall,
                BorderStyle     = BorderStyle.None,
                SelectionMode   = SelectionMode.MultiExtended
            };
            var btnRemoveSelected = Theme.MakeButton("移除選中", Theme.AccentRed, Color.White, 80, 24);
            btnRemoveSelected.Font     = Theme.FontSmall;
            btnRemoveSelected.Top      = 62;
            btnRemoveSelected.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnRemoveSelected.Click   += (s, e) =>
            {
                foreach (var item in _excludeListBox.SelectedItems.Cast<string>().ToList())
                    _excludeSet.Remove(item);
                RefreshExcludeList();
            };

            excludePanel.Controls.Add(_excludeListBox);
            excludePanel.Controls.Add(btnRemoveSelected);

            excludePanel.Resize += (s, e) =>
            {
                int pw = excludePanel.ClientSize.Width;
                btnAddExclude.Left       = pw - 6 - btnAddExclude.Width;
                _excludeSearchBox.Width  = Math.Max(60, btnAddExclude.Left - 12);
                _excludeListBox.Width    = Math.Max(60, pw - 12 - btnRemoveSelected.Width - 6);
                btnRemoveSelected.Left   = pw - 6 - btnRemoveSelected.Width;
            };

            scroll.Controls.Add(excludePanel);
            y += 190;

            // ── 發送按鈕 ──
            _sendBtn = new Button
            {
                Text      = "🛒  請先從左側清單加入道具至購物車",
                BackColor = Color.FromArgb(60, 62, 78),
                ForeColor = Theme.TextMuted,
                FlatStyle = FlatStyle.Flat,
                Font      = Theme.FontHeader,
                Size      = new Size(460, 44),
                Location  = new Point(x, y),
                Cursor    = Cursors.Hand,
                Enabled   = false,
                UseVisualStyleBackColor = false
            };
            _sendBtn.FlatAppearance.BorderSize = 0;
            _sendBtn.Click += SendBtn_Click;
            scroll.Controls.Add(_sendBtn);
            y += 52;

            _cancelBtn = Theme.MakeButton("■  停止", Theme.AccentRed, Color.White, 90, 32);
            _cancelBtn.Location = new Point(x, y);
            _cancelBtn.Enabled  = false;
            _cancelBtn.Click   += (s, e) => _cts?.Cancel();
            scroll.Controls.Add(_cancelBtn);
            y += 40;

            // ── 進度 ──
            _progressBar = new ProgressBar
            {
                Location = new Point(x, y), Width = 440, Height = 18,
                Style = ProgressBarStyle.Continuous, ForeColor = Theme.AccentGreen
            };
            scroll.Controls.Add(_progressBar);
            y += 24;

            _progressLbl = new Label
            {
                Text = "", ForeColor = Theme.TextSecondary, Font = Theme.FontSmall,
                AutoSize = true, Location = new Point(x, y)
            };
            scroll.Controls.Add(_progressLbl);
            y += 22;

            _statusLbl = new Label
            {
                Text = "", ForeColor = Theme.AccentGreen, Font = Theme.FontBody,
                AutoSize = true, Location = new Point(x, y)
            };
            scroll.Controls.Add(_statusLbl);
            y += 28;

            _logBox = new RichTextBox
            {
                Location    = new Point(x, y),
                Width       = 440,
                Height      = 200,
                BackColor   = Color.FromArgb(14, 22, 36),
                ForeColor   = Theme.TextPrimary,
                Font        = Theme.FontMono,
                ReadOnly    = true,
                BorderStyle = BorderStyle.None,
                ScrollBars  = RichTextBoxScrollBars.Vertical
            };
            scroll.Controls.Add(_logBox);

            p.Resize += (s, e) =>
            {
                int w = Math.Max(260, p.Width - 28);
                cartHdrPanel.Width  = w;
                _cartDgv.Width      = w;
                settingPanel.Width  = w;
                excludePanel.Width  = w;
                _sendBtn.Width      = w;
                _progressBar.Width  = w;
                _logBox.Width       = w;
                _logBox.Height      = Math.Max(100, p.Height - _logBox.Top - 20);
            };
        }

        // ═══════════════════════════════════════════════════════════
        // 篩選 & 翻頁（與 SendForm 相同邏輯）
        // ═══════════════════════════════════════════════════════════
        private void ApplyFilter()
        {
            var gm = GameDataManager.Instance;
            if (!gm.ItemsLoaded)
            {
                _itemCountLbl.Text      = "📦  道具清單  ⚠ 請至「⚙ 資料設定」載入 items.xlsx";
                _itemCountLbl.ForeColor = Theme.AccentOrange;
                _filteredItems.Clear();
                _itemDgv.Rows.Clear();
                _pageLabel.Text  = "未載入道具資料";
                _btnPrev.Enabled = false;
                _btnNext.Enabled = false;
                return;
            }

            string q = _searchBox.Text.Trim();
            _filteredItems = string.IsNullOrEmpty(q) ? gm.GetAllItems() : gm.SearchItems(q);

            _itemCountLbl.ForeColor = Theme.AccentBlue;
            _itemCountLbl.Text = string.IsNullOrEmpty(q)
                ? $"📦  道具清單  共 {gm.ItemCount} 筆（每頁 {PageSize} 筆）"
                : _filteredItems.Count == 0
                    ? $"📦  無符合「{q}」的道具"
                    : $"📦  找到 {_filteredItems.Count} 筆  搜尋：「{q}」";

            _currentPage = 0;
            FillPage();
        }

        private void FillPage()
        {
            _itemDgv.Rows.Clear();
            int start = _currentPage * PageSize;
            int end   = Math.Min(start + PageSize, _filteredItems.Count);
            for (int i = start; i < end; i++)
            {
                var item = _filteredItems[i];
                int ri = _itemDgv.Rows.Add(i + 1, item.Name, item.Id, item.Description);
                _itemDgv.Rows[ri].Tag = item;
            }

            int total = _filteredItems.Count;
            if (total == 0)
            {
                _pageLabel.Text = "無資料";
            }
            else
            {
                int from = _currentPage * PageSize + 1;
                int to   = Math.Min((_currentPage + 1) * PageSize, total);
                _pageLabel.Text = $"第 {_currentPage + 1} / {MaxPage} 頁  ·  {from}～{to} 筆 / 共 {total} 筆";
            }
            _btnPrev.Enabled   = _currentPage > 0;
            _btnNext.Enabled   = _currentPage < MaxPage - 1;
            _btnPrev.ForeColor = _btnPrev.Enabled ? Theme.TextPrimary : Theme.TextMuted;
            _btnNext.ForeColor = _btnNext.Enabled ? Theme.TextPrimary : Theme.TextMuted;
        }

        private void SelectItem(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _itemDgv.Rows.Count) return;
            if (_itemDgv.Rows[rowIndex].Tag is not ItemInfo item) return;

            var existing = _cart.FirstOrDefault(c => c.Item.Id == item.Id);
            if (existing != null)
            {
                existing.Qty++;
            }
            else
            {
                _cart.Add(new CartEntry { Item = item, Qty = 1 });
            }
            RefreshCartDgv();
        }

        private void RefreshCartDgv()
        {
            _cartDgv.Rows.Clear();
            foreach (var e in _cart)
            {
                int ri = _cartDgv.Rows.Add(e.Item.Name, e.Item.Id, e.Qty, "✕");
                _cartDgv.Rows[ri].Tag = e;
            }

            bool hasItems = _cart.Count > 0;
            int totalTypes = _cart.Count;
            int totalItems = _cart.Sum(c => c.Qty);
            if (hasItems)
            {
                _sendBtn.Text      = $"🚀  批量發送 {totalTypes} 種道具 × {totalItems} 件 給所有角色";
                _sendBtn.BackColor = Theme.AccentOrange;
                _sendBtn.ForeColor = Color.White;
                _sendBtn.Enabled   = true;
            }
            else
            {
                _sendBtn.Text      = "🛒  請先從左側清單加入道具至購物車";
                _sendBtn.BackColor = Color.FromArgb(60, 62, 78);
                _sendBtn.ForeColor = Theme.TextMuted;
                _sendBtn.Enabled   = false;
            }
        }

        // ═══════════════════════════════════════════════════════════
        // 批量發送
        // ═══════════════════════════════════════════════════════════
        private async void SendBtn_Click(object sender, EventArgs e)
        {
            if (_cart.Count == 0) return;

            string title   = _txtTitle.Text.Trim();
            string content = _txtContent.Text.Trim();

            int nowTs   = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            int startTs = _chkSchedule.Checked
                ? (int)new DateTimeOffset(DateTime.SpecifyKind(_dtStart.Value, DateTimeKind.Local)).ToUniversalTime().ToUnixTimeSeconds()
                : nowTs;
            int endTs   = (int)new DateTimeOffset(DateTime.SpecifyKind(_dtEnd.Value.Date, DateTimeKind.Utc)).ToUnixTimeSeconds();
            if (endTs <= startTs) endTs = startTs + 30 * 24 * 3600;

            string scheduleNote = _chkSchedule.Checked
                ? $"  預約時間：{_dtStart.Value:yyyy/MM/dd HH:mm}\n"
                : "  發送時間：立即\n";

            string itemsSummary = string.Join("\n", _cart.Select(c =>
                $"  • {c.Item.Name}（#{c.Item.Id}）× {c.Qty} 份"));

            string excludeNote = _excludeSet.Count > 0
                ? $"  ⛔ 排除 {_excludeSet.Count} 人：{string.Join("、", _excludeSet.Take(5))}{(_excludeSet.Count > 5 ? "…" : "")}\n"
                : "";

            if (MessageBox.Show(
                $"確定要批量發送給所有角色？\n\n" +
                $"【道具清單】（共 {_cart.Count} 種）\n{itemsSummary}\n\n" +
                scheduleNote +
                excludeNote +
                $"  到期日期：{_dtEnd.Value:yyyy/MM/dd}\n\n" +
                "⚠ 此操作無法撤銷，每種道具皆會各自發一封郵件！",
                "確認批量發送", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            // 讀取發送速度設定
            int[] batchSizes = { 20, 50, 100, 200, 500 };
            int batchSize = batchSizes[Math.Max(0, Math.Min(_cmbBatchSize.SelectedIndex, batchSizes.Length - 1))];

            _isSending         = true;
            _sendBtn.Enabled   = false;
            _cancelBtn.Enabled = true;
            _logBox.Clear();
            _progressBar.Value = 0;
            _statusLbl.Text    = "";
            _progressLbl.Text  = "";
            _cts = new CancellationTokenSource();

            int totalSuccess = 0, totalFail = 0;
            int itemIdx = 0;

            try
            {
                foreach (var entry in _cart.ToList())
                {
                    if (_cts.Token.IsCancellationRequested) break;
                    itemIdx++;
                    string itemTitle   = string.IsNullOrEmpty(title)   ? entry.Item.Name : title;
                    string itemContent = string.IsNullOrEmpty(content) ? entry.Item.Name : content;

                    AppendLog($"▶ [{itemIdx}/{_cart.Count}] 開始發送：{entry.Item.Name} × {entry.Qty}", Color.FromArgb(100, 180, 255));

                    var req = new SendMailRequest
                    {
                        Type      = 1,
                        Operator  = GmLogger.Instance.OperatorName,
                        Buff1     = itemTitle,
                        Buff2     = itemContent,
                        Data      = entry.Item.Id,
                        StartTime = startTs,
                        EndTime   = endTs,
                        Buff3     = entry.Item.Description,
                        Quantity  = entry.Qty
                    };

                    var progress = new Progress<(int done, int total, string account, bool ok)>(rep =>
                    {
                        _progressBar.Maximum = rep.total;
                        _progressBar.Value   = Math.Min(rep.done, rep.total);
                        _progressLbl.Text    = $"道具 {itemIdx}/{_cart.Count}  ·  {rep.done} / {rep.total} 位玩家";
                        _statusLbl.Text      = $"正在發送：{rep.account}";
                        _statusLbl.ForeColor = Theme.AccentOrange;
                        AppendLog($"  [{rep.done}/{rep.total}] {(rep.ok ? "✓" : "✗")} {rep.account}",
                                  rep.ok ? Theme.AccentGreen : Theme.AccentRed);
                    });

                    var (success, fail) = await DatabaseManager.Instance.BatchSendMailAsync(req, progress, _cts.Token, batchSize, _excludeSet.Count > 0 ? _excludeSet : null);
                    totalSuccess += success;
                    totalFail    += fail;

                    AppendLog($"  ✓ {entry.Item.Name} 完成：成功 {success}，失敗 {fail}", Theme.AccentGreen);
                }

                if (_cts.Token.IsCancellationRequested)
                {
                    _statusLbl.Text      = "⚠ 已手動停止";
                    _statusLbl.ForeColor = Theme.AccentOrange;
                    AppendLog("⚠ 已手動停止", Theme.AccentOrange);
                }
                else
                {
                    _statusLbl.Text      = $"✅ 全部完成！共 {_cart.Count} 種道具，成功 {totalSuccess}，失敗 {totalFail}";
                    _statusLbl.ForeColor = totalFail == 0 ? Theme.AccentGreen : Theme.AccentOrange;
                    AppendLog($"✅ 所有道具發送完畢！成功 {totalSuccess}，失敗 {totalFail}", Theme.AccentGreen);
                    _cart.Clear();
                    RefreshCartDgv();
                }
            }
            catch (Exception ex)
            {
                _statusLbl.Text      = "✗ 錯誤：" + ex.Message;
                _statusLbl.ForeColor = Theme.AccentRed;
                AppendLog("✗ 錯誤：" + ex.Message, Theme.AccentRed);
            }
            finally
            {
                _isSending         = false;
                _sendBtn.Enabled   = _cart.Count > 0;
                _cancelBtn.Enabled = false;
                _cts?.Dispose();
            }
        }

        private void RefreshExcludeList()
        {
            if (InvokeRequired) { Invoke(new Action(RefreshExcludeList)); return; }
            _excludeListBox.Items.Clear();
            foreach (var acc in _excludeSet.OrderBy(a => a))
                _excludeListBox.Items.Add(acc);
            _excludeCountLbl.Text = $"🚫  排除名單（{_excludeSet.Count} 人）";
        }

        private void AppendLog(string text, Color color)
        {
            if (InvokeRequired) { Invoke(new Action(() => AppendLog(text, color))); return; }
            _logBox.SelectionColor = color;
            _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}\n");
            _logBox.ScrollToCaret();
        }
    }
}
