using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
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

        // ── 發送對象 ──
        private CheckBox _chkOnlineOnly;

        // ── 指定名單（白名單，與 onlineOnly 互斥；非空時只發給名單裡的人）──
        private readonly HashSet<string> _includeSet = new(StringComparer.OrdinalIgnoreCase);
        private ListBox  _includeListBox;
        private TextBox  _includeSearchBox;
        private Label    _includeCountLbl;

        // ── 排除名單（黑名單）──
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
            Theme.ApplyHubForm(this);

            StartPosition = FormStartPosition.CenterParent;
            FormClosing  += (s, e) => { if (_isSending) e.Cancel = true; };

            // ── ① Header ──────────────────────────────────────────
            var header = new Panel { Dock = DockStyle.Top, Height = Theme.ToolbarHeight, BackColor = Theme.BgDialogHeader };
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
                    split.Panel1MinSize    = 260;
                    split.Panel2MinSize    = 260;
                    if (split.Width >= 520)
                        split.SplitterDistance = Math.Max(260, Math.Min(split.Width - 260, 580));
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
            _itemDgv.MultiSelect           = true;
            _itemDgv.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            _itemDgv.AllowUserToResizeRows = false;
            // 勿覆寫 Theme 列高（見 SendForm 註解）

            _itemDgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colIdx", HeaderText = "序號", Width = 52,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _itemDgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colName", HeaderText = "道具名稱",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 52, MinimumWidth = 140,
                SortMode = DataGridViewColumnSortMode.NotSortable,
            });
            _itemDgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colId", HeaderText = "編號", Width = 70, MinimumWidth = 55,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _itemDgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDesc", HeaderText = "說明",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 48, MinimumWidth = 120,
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

            // ── 購物車標題列（TableLayoutPanel：左邊 Label + 右邊兩顆按鈕）──
            var cartHdrPanel = new TableLayoutPanel
            {
                Location    = new Point(x, y),
                Size        = new Size(460, 32),
                BackColor   = Theme.BgDark,
                ColumnCount = 3,
                RowCount    = 1,
                Margin      = new Padding(0),
                Padding     = new Padding(0, 2, 4, 2),
            };
            cartHdrPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            cartHdrPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            cartHdrPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            cartHdrPanel.RowStyles   .Add(new RowStyle(SizeType.Percent, 100));

            var lblCartTitle = new Label
            {
                Text      = "  🛒  道具購物車 — 雙擊左側加入（可多種道具）",
                ForeColor = Color.FromArgb(100, 180, 255),
                Font      = new Font(Theme.FontFamily, 9f, FontStyle.Bold),
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin    = new Padding(0),
            };
            var btnUploadItems = Theme.MakeButton("📤 上傳道具", Theme.AccentBlue, Color.White, 96, 26);
            btnUploadItems.Font   = Theme.FontSmall;
            btnUploadItems.Margin = new Padding(2, 1, 2, 1);
            btnUploadItems.Click += (s, e) => UploadItemsToCart();

            var btnClear = Theme.MakeButton("🗑 清空", Theme.AccentRed, Color.White, 64, 26);
            btnClear.Font   = Theme.FontSmall;
            btnClear.Margin = new Padding(2, 1, 2, 1);
            btnClear.Click += (s, e) =>
            {
                if (_cart.Count == 0) return;
                if (!Theme.Confirm("確定要清空購物車嗎？", "確認", defaultButtonNo: true)) return;
                _cart.Clear(); RefreshCartDgv();
            };

            cartHdrPanel.Controls.Add(lblCartTitle,   0, 0);
            cartHdrPanel.Controls.Add(btnUploadItems, 1, 0);
            cartHdrPanel.Controls.Add(btnClear,       2, 0);
            scroll.Controls.Add(cartHdrPanel);
            y += 36;

            // ── 醒目的「上傳道具清單」大按鈕（標題下方獨立一條）──
            var btnUploadHint = Theme.MakeButton(
                "📤  上傳道具清單到購物車（CSV / TXT / Excel）",
                Color.FromArgb(36, 96, 200), Color.White, 460, 30);
            btnUploadHint.Location  = new Point(x, y);
            btnUploadHint.Font      = new Font(Theme.FontFamily, 9.5f, FontStyle.Bold);
            btnUploadHint.TextAlign = ContentAlignment.MiddleCenter;
            btnUploadHint.Click    += (s, e) => UploadItemsToCart();
            scroll.Controls.Add(btnUploadHint);
            y += 36;

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
                MultiSelect           = true,
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

            // 範本按鈕（含購物車儲存/載入，與網頁版範例紀錄一致）
            var tplBtn = Theme.MakeTemplateButton(_txtTitle, _txtContent,
                getCart: () => _cart.Select(c => new MailTemplateCartItem
                {
                    ItemId = c.Item.Id,
                    Qty    = c.Qty,
                    Type   = c.Item.IsPet ? 2 : 1,
                    Name   = c.Item.Name ?? ""
                }).ToList(),
                onApplyTemplate: t =>
                {
                    _cart.Clear();
                    var gm = GameDataManager.Instance;
                    foreach (var it in t.Cart ?? new List<MailTemplateCartItem>())
                    {
                        var info = it.Type == 2 ? gm.GetPetById(it.ItemId) : gm.GetItemById(it.ItemId);
                        if (info != null)
                            _cart.Add(new CartEntry { Item = info, Qty = Math.Max(1, it.Qty) });
                    }
                    RefreshCartDgv();
                });

            AddRow("標      題：", _txtTitle,  260);
            sy += 10;
            tplBtn.Location = new Point(84, sy);
            tplBtn.Size = new Size(140, 40);
            settingPanel.Controls.Add(tplBtn);
            tplBtn.BringToFront();
            sy += tplBtn.Height + 10;

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

            // ── 發送對象 ──
            {
                settingPanel.Controls.Add(new Label
                {
                    Text = "發送對象：", ForeColor = Theme.TextSecondary, Font = Theme.FontBody,
                    Width = 72, Location = new Point(8, sy + 3), TextAlign = ContentAlignment.MiddleRight
                });
                _chkOnlineOnly = new CheckBox
                {
                    Text      = "🟢  僅發送線上玩家",
                    ForeColor = Color.FromArgb(80, 220, 140),
                    Font      = new Font(Theme.FontFamily, 9f, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.Transparent,
                    AutoSize  = true,
                    Checked   = false,
                    Location  = new Point(84, sy + 2)
                };
                settingPanel.Controls.Add(_chkOnlineOnly);
                settingPanel.Controls.Add(new Label
                {
                    Text = "（不勾 = 全服所有角色）", ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                    AutoSize = true, Location = new Point(84 + _chkOnlineOnly.PreferredSize.Width + 8, sy + 6)
                });
                sy += 30;
            }

            settingPanel.Height = sy + 10;
            scroll.Controls.Add(settingPanel);
            y += settingPanel.Height + 10;

            // ── 指定名單區塊（白名單；非空時只發給名單裡的人，可再被排除名單剃除）──
            var includePanel = BuildAccountListSection(
                title: "🎯  指定發送名單（0 人）— 非空時只發給名單裡的人",
                titleColor: Theme.AccentGreen,
                headerBg:  Color.FromArgb(20, 56, 36),
                listBg:    Color.FromArgb(16, 30, 22),
                placeholder: "輸入帳號加入指定…",
                set: _includeSet,
                setListBox: lb => _includeListBox = lb,
                setCountLbl: lb => _includeCountLbl = lb,
                setSearchBox: tb => _includeSearchBox = tb,
                refreshTitle: cnt => _includeCountLbl.Text = $"🎯  指定發送名單（{cnt} 人）" + (cnt > 0 ? "  ※ 將忽略「全服/僅線上」設定，只發給此清單" : ""),
                onChanged: RefreshIncludeList);
            includePanel.Location = new Point(x, y);
            includePanel.Size     = new Size(460, 180);
            scroll.Controls.Add(includePanel);
            y += 190;

            // ── 排除名單區塊（黑名單）──
            var excludePanel = BuildAccountListSection(
                title: "🚫  排除名單（0 人）",
                titleColor: Theme.AccentRed,
                headerBg:  Color.FromArgb(60, 30, 30),
                listBg:    Color.FromArgb(28, 18, 18),
                placeholder: "輸入帳號加入排除…",
                set: _excludeSet,
                setListBox: lb => _excludeListBox = lb,
                setCountLbl: lb => _excludeCountLbl = lb,
                setSearchBox: tb => _excludeSearchBox = tb,
                refreshTitle: cnt => _excludeCountLbl.Text = $"🚫  排除名單（{cnt} 人）",
                onChanged: RefreshExcludeList);
            excludePanel.Location = new Point(x, y);
            excludePanel.Size     = new Size(460, 180);
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
                btnUploadHint.Width = w;
                _cartDgv.Width      = w;
                settingPanel.Width  = w;
                includePanel.Width  = w;
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

        // ═══════════════════════════════════════════════════════════
        // 一鍵上傳道具清單到購物車（CSV / TXT / Excel）
        // ═══════════════════════════════════════════════════════════
        private void UploadItemsToCart()
        {
            using var ofd = new OpenFileDialog
            {
                Title  = "選擇道具清單檔案",
                Filter = ItemListImporter.DialogFilter,
            };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            string sheetSel = SheetPickerDialog.AskIfMultiSheet(this, ofd.FileName);
            if (sheetSel == "") return;

            ItemListImporter.ParseResult parsed;
            try { parsed = ItemListImporter.ParseFile(ofd.FileName, sheetSel); }
            catch (Exception ex)
            {
                MessageBox.Show("檔案解析失敗：\n" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (parsed.Rows.Count == 0)
            {
                MessageBox.Show($"檔案內沒有有效的道具編號。\n來源：{parsed.DetectedSource}", "上傳道具", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var gm = GameDataManager.Instance;
            if (!gm.ItemsLoaded)
            {
                MessageBox.Show("尚未載入 items.xlsx，請先到「⚙ 資料設定」載入道具資料。", "資料未載入", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string mode = "append";
            if (_cart.Count > 0)
            {
                var dr = MessageBox.Show(
                    $"購物車目前有 {_cart.Count} 種道具。\n\n" +
                    $"檔案中讀到 {parsed.Rows.Count} 筆。\n" +
                    $"來源：{parsed.DetectedSource}\n\n" +
                    $"【是】=覆蓋（清空後加入）\n" +
                    $"【否】=追加（合併、同 ID 累加數量）\n" +
                    $"【取消】=取消上傳",
                    "上傳道具清單", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (dr == DialogResult.Cancel) return;
                mode = dr == DialogResult.Yes ? "replace" : "append";
            }
            if (mode == "replace") _cart.Clear();

            int added = 0, merged = 0, missing = 0;
            var missingIds = new List<int>();
            foreach (var r in parsed.Rows)
            {
                ItemInfo info = r.Type == 2 ? gm.GetPetById(r.Id) : gm.GetItemById(r.Id);
                if (info == null) info = gm.FindItemById(r.Id);
                if (info == null) { missing++; missingIds.Add(r.Id); continue; }

                var existing = _cart.FirstOrDefault(c => c.Item.Id == r.Id);
                if (existing != null) { existing.Qty = Math.Max(1, existing.Qty + r.Qty); merged++; }
                else                  { _cart.Add(new CartEntry { Item = info, Qty = Math.Max(1, r.Qty) }); added++; }
            }
            RefreshCartDgv();

            ShowItemUploadReport(parsed, added, merged, missing, missingIds);
        }

        private void ShowItemUploadReport(ItemListImporter.ParseResult parsed, int added, int merged, int missing, List<int> missingIds)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"來源：{parsed.DetectedSource}");
            if (!string.IsNullOrEmpty(parsed.DetectedColumns))
                sb.AppendLine($"欄位：{parsed.DetectedColumns}");
            sb.AppendLine();
            sb.AppendLine($"✓ 成功讀入 {parsed.Rows.Count} 筆 → 新增 {added} 種、累加 {merged} 種");

            if (parsed.Skipped > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"⚠ 跳過 {parsed.Skipped} 列（ID 解析失敗 / 空白）：");
                foreach (var s in parsed.SkippedDetails.Take(15))
                {
                    string raw = s.Raw ?? "";
                    if (raw.Length > 80) raw = raw.Substring(0, 80) + "…";
                    sb.AppendLine($"  · 第 {s.LineNo} 列：{s.Reason}  ｜  {raw}");
                }
                if (parsed.SkippedDetails.Count > 15)
                    sb.AppendLine($"  … 另有 {parsed.SkippedDetails.Count - 15} 列");
            }

            if (missing > 0)
            {
                sb.AppendLine();
                string sample = string.Join(", ", missingIds.Take(20));
                if (missingIds.Count > 20) sample += " …";
                sb.AppendLine($"⚠ {missing} 個 ID 在 items.xlsx 找不到：{sample}");
            }

            MessageBox.Show(sb.ToString(), "上傳結果", MessageBoxButtons.OK,
                parsed.Skipped > 0 || missing > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
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
                _sendBtn.Text      = $"🚀  批量發送 {totalTypes} 種 × {totalItems} 件";
                _sendBtn.BackColor = Theme.AccentOrange;
                _sendBtn.ForeColor = Color.White;
                _sendBtn.Enabled   = true;
                new ToolTip().SetToolTip(_sendBtn, $"批量發送 {totalTypes} 種道具 × 共 {totalItems} 件，給所有符合條件角色");
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
            if (_isSending) return;   // 防重入：避免快速雙擊造成全服重複發送
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

            bool onlineOnly = _chkOnlineOnly.Checked;
            bool useWhitelist = _includeSet.Count > 0;

            string excludeNote = _excludeSet.Count > 0
                ? $"  ⛔ 排除 {_excludeSet.Count} 人：{string.Join("、", _excludeSet.Take(5))}{(_excludeSet.Count > 5 ? "…" : "")}\n"
                : "";

            string targetNote;
            if (useWhitelist)
                targetNote = $"  🎯 發送對象：指定名單 {_includeSet.Count} 人（已忽略「全服/僅線上」設定）\n";
            else if (onlineOnly)
                targetNote = "  🟢 發送對象：僅線上玩家\n";
            else
                targetNote = "  🌐 發送對象：全服所有角色\n";

            if (MessageBox.Show(
                $"確定要批量發送？\n\n" +
                $"【道具清單】（共 {_cart.Count} 種）\n{itemsSummary}\n\n" +
                targetNote +
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

                    string targetStr = onlineOnly ? "線上玩家" : "全服";
                    AppendLog($"▶ [{itemIdx}/{_cart.Count}] 開始發送（{targetStr}）：{entry.Item.Name} × {entry.Qty}", Color.FromArgb(100, 180, 255));

                    var req = new SendMailRequest
                    {
                        Type      = 1,
                        Operator  = GmLogger.Instance.OperatorName,
                        Buff1     = itemTitle,
                        Buff2     = itemContent,
                        Data      = entry.Item.Id,
                        StartTime = startTs,
                        EndTime   = endTs,
                        Buff3     = entry.Item.Name,   // ★ 遊戲用 buff3=道具名稱
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

                    var (success, fail) = await DatabaseManager.Instance.BatchSendMailAsync(
                        req, progress, _cts.Token, batchSize,
                        excludeSet:   _excludeSet.Count > 0 ? _excludeSet : null,
                        onlineOnly:   onlineOnly,
                        whitelistSet: useWhitelist ? _includeSet : null);
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

        private void RefreshIncludeList()
        {
            if (InvokeRequired) { Invoke(new Action(RefreshIncludeList)); return; }
            _includeListBox.Items.Clear();
            foreach (var acc in _includeSet.OrderBy(a => a))
                _includeListBox.Items.Add(acc);
            _includeCountLbl.Text = $"🎯  指定發送名單（{_includeSet.Count} 人）"
                + (_includeSet.Count > 0 ? "  ※ 將忽略「全服/僅線上」設定，只發給此清單" : "");
            // 更新發送按鈕的提示文字（若有道具）
            RefreshCartDgv();
        }

        // ═══════════════════════════════════════════════════════════
        // 共用：建立「帳號名單」區塊（指定/排除 共用樣板）
        //    - Header：標題 + 「📤 上傳檔案」+「全部移除」
        //    - 中間：手動輸入帳號 + 「+ 加入」
        //    - 下方：清單 + 「移除選中」
        // ═══════════════════════════════════════════════════════════
        private Panel BuildAccountListSection(
            string title,
            Color titleColor,
            Color headerBg,
            Color listBg,
            string placeholder,
            HashSet<string> set,
            Action<ListBox> setListBox,
            Action<Label> setCountLbl,
            Action<TextBox> setSearchBox,
            Action<int> refreshTitle,
            Action onChanged)
        {
            var panel = new Panel
            {
                BackColor   = Theme.BgCard,
                BorderStyle = BorderStyle.FixedSingle
            };

            // ── Header ──
            var hdr = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = headerBg };
            var countLbl = new Label
            {
                Text      = title,
                ForeColor = titleColor,
                Font      = new Font(Theme.FontFamily, 9f, FontStyle.Bold),
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(6, 0, 0, 0)
            };
            setCountLbl(countLbl);
            var btnUpload = Theme.MakeButton("📤 上傳", Theme.AccentBlue, Color.White, 64, 22);
            btnUpload.Font   = Theme.FontSmall;
            btnUpload.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            var btnClear = Theme.MakeButton("全部移除", titleColor, Color.White, 72, 22);
            btnClear.Font   = Theme.FontSmall;
            btnClear.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClear.Click += (s, e) => { set.Clear(); onChanged(); };
            hdr.Controls.Add(countLbl);
            hdr.Controls.Add(btnUpload);
            hdr.Controls.Add(btnClear);
            hdr.Resize += (s, e) =>
            {
                btnClear.Left  = hdr.ClientSize.Width - 4 - btnClear.Width;
                btnUpload.Left = btnClear.Left - 4 - btnUpload.Width;
            };
            panel.Controls.Add(hdr);

            // ── 手動輸入 + 「+ 加入」──
            var searchBox = new TextBox
            {
                PlaceholderText = placeholder,
                BackColor       = Theme.BgLight,
                ForeColor       = Theme.TextPrimary,
                BorderStyle     = BorderStyle.FixedSingle,
                Font            = Theme.FontBody,
                Location        = new Point(6, 32),
                Height          = 24
            };
            setSearchBox(searchBox);
            var btnAdd = Theme.MakeButton("＋ 加入", Theme.AccentOrange, Color.White, 70, 24);
            btnAdd.Font     = Theme.FontSmall;
            btnAdd.Location = new Point(0, 32);
            btnAdd.Anchor   = AnchorStyles.Top | AnchorStyles.Right;

            void Add()
            {
                var acc = searchBox.Text.Trim();
                if (string.IsNullOrEmpty(acc)) return;
                set.Add(acc);
                searchBox.Clear();
                onChanged();
            }
            btnAdd.Click    += (s, e) => Add();
            searchBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { Add(); e.Handled = true; } };
            panel.Controls.Add(searchBox);
            panel.Controls.Add(btnAdd);

            // ── 名單 + 移除選中 ──
            var listBox = new ListBox
            {
                Top           = 62,
                Left          = 6,
                Height        = 100,
                BackColor     = listBg,
                ForeColor     = Theme.TextPrimary,
                Font          = Theme.FontSmall,
                BorderStyle   = BorderStyle.None,
                SelectionMode = SelectionMode.MultiExtended
            };
            setListBox(listBox);
            var btnRemoveSelected = Theme.MakeButton("移除選中", titleColor, Color.White, 80, 24);
            btnRemoveSelected.Font   = Theme.FontSmall;
            btnRemoveSelected.Top    = 62;
            btnRemoveSelected.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnRemoveSelected.Click += (s, e) =>
            {
                foreach (var item in listBox.SelectedItems.Cast<string>().ToList())
                    set.Remove(item);
                onChanged();
            };
            panel.Controls.Add(listBox);
            panel.Controls.Add(btnRemoveSelected);

            // 上傳按鈕：解析檔案 → ask 覆蓋/追加 → 加入 set
            btnUpload.Click += (s, e) => UploadIntoSet(set, title, onChanged);

            panel.Resize += (s, e) =>
            {
                int pw = panel.ClientSize.Width;
                btnAdd.Left            = pw - 6 - btnAdd.Width;
                searchBox.Width        = Math.Max(60, btnAdd.Left - 12);
                btnRemoveSelected.Left = pw - 6 - btnRemoveSelected.Width;
                listBox.Width          = Math.Max(60, pw - 12 - btnRemoveSelected.Width - 6);
            };
            return panel;
        }

        // ═══════════════════════════════════════════════════════════
        // 上傳檔案 → 加入指定/排除名單
        // ═══════════════════════════════════════════════════════════
        private void UploadIntoSet(HashSet<string> set, string sectionTitle, Action onChanged)
        {
            using var ofd = new OpenFileDialog
            {
                Title  = $"匯入到「{sectionTitle.Trim().Split('（')[0]}」",
                Filter = PlayerListImporter.DialogFilter
            };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            PlayerListImporter.ParseResult parsed;
            try { parsed = PlayerListImporter.ParseFile(ofd.FileName); }
            catch (Exception ex)
            {
                MessageBox.Show("解析失敗：" + ex.Message, "匯入失敗",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (parsed.Rows.Count == 0)
            {
                MessageBox.Show($"檔案內沒有有效的識別編號。\n\n{parsed.DetectedSource}",
                    "無資料", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 預覽前 5 筆
            var preview = string.Join("\n", parsed.Rows.Take(5).Select(r =>
                string.IsNullOrEmpty(r.OnlineName) ? $"  • {r.Cdkey}" : $"  • {r.Cdkey}（{r.OnlineName}）"));
            if (parsed.Rows.Count > 5) preview += $"\n  …（共 {parsed.Rows.Count} 筆）";

            // 詢問覆蓋 / 追加 / 取消
            string msg =
                $"已從檔案讀到 {parsed.Rows.Count} 筆。\n" +
                $"來源：{parsed.DetectedSource}\n" +
                (parsed.Skipped > 0 ? $"略過空白列：{parsed.Skipped}\n" : "") +
                $"\n預覽：\n{preview}\n\n" +
                $"目前清單已有 {set.Count} 筆，要如何處理？\n" +
                "[是] = 覆蓋（清空後匯入）\n[否] = 追加（合併，相同帳號去重）\n[取消] = 不動作";
            var rsp = MessageBox.Show(msg, "匯入名單", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (rsp == DialogResult.Cancel) return;
            if (rsp == DialogResult.Yes) set.Clear();

            int before = set.Count;
            foreach (var r in parsed.Rows)
            {
                var cdkey = (r.Cdkey ?? "").Trim();
                if (!string.IsNullOrEmpty(cdkey)) set.Add(cdkey);
            }
            onChanged();
            int added = set.Count - before;
            MessageBox.Show(
                $"✓ 匯入完成\n新增 {added} 筆，目前清單共 {set.Count} 筆。",
                "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
