using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    public class SendForm : Form
    {
        private readonly PlayerInfo _player;

        // ── 購物車資料結構 ──
        private class CartEntry
        {
            public ItemInfo Item { get; set; }
            public int Qty  { get; set; } = 1;
            public int Type { get; set; } = 1;
        }
        private readonly List<CartEntry> _cart = new();

        // ── 搜尋欄（放在主表單頂層，永遠可見）──
        private TextBox _searchBox;

        // ── 左側清單 ──
        private Label        _itemCountLbl;
        private DataGridView _itemDgv;
        private Label        _pageLabel;
        private Button       _btnPrev, _btnNext;

        // ── 右側發送 ──
        private DataGridView  _cartDgv;
        private Label         _lblCartHint;
        private TextBox       _txtTitle, _txtContent;
        private NumericUpDown _nudQty, _nudType;
        private CheckBox      _chkSchedule;
        private DateTimePicker _dtStart;
        private Button        _sendBtn;
        private Label         _statusLbl;
        private DataGridView  _histDgv;

        // ── 資料狀態 ──
        private List<ItemInfo> _filteredItems = new();
        private int            _currentPage   = 0;
        private const int      PageSize       = 50;
        private int            MaxPage        => Math.Max(1, (_filteredItems.Count + PageSize - 1) / PageSize);

        public SendForm(PlayerInfo player)
        {
            _player = player;
            InitUI();
            _ = LoadHistoryAsync();
            ApplyFilter();
        }

        // ═══════════════════════════════════════════════════════════
        // 主佈局：Header（Top）→ SearchPanel（Top）→ SplitContainer（Fill）
        // 加入順序必須是：Fill 最先，然後 Top 由後往前
        // ═══════════════════════════════════════════════════════════
        private void InitUI()
        {
            Text          = $"✉ 道具發送 — {_player.OnlineName}";
            Size          = new Size(1120, 740);
            MinimumSize   = new Size(860, 560);
            BackColor     = Theme.BgMid;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;

            // ── ① 玩家資訊 Header ──────────────────────────────
            var header = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme.BgDark };
            header.Controls.Add(new Label
            {
                Text      = $"  {(_player.IsOnline ? "🟢" : "⚫")}  {_player.OnlineName}（{_player.Account}）" +
                            $"  {(_player.IsOnline ? "在線中" : "離線")}    " +
                            $"⚠ 發送後玩家重新登入即可在信件欄領取道具",
                ForeColor = _player.IsOnline ? Theme.AccentGreen : Theme.TextSecondary,
                Font      = Theme.FontBody,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(8, 0, 0, 0)
            });

            // ── ② 搜尋列 ────────────────────────────────────────
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
                ForeColor = Theme.TextSecondary,
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

            // ── ③ SplitContainer（Fill）────────────────────────
            var split = new SplitContainer
            {
                Dock          = DockStyle.Fill,
                Orientation   = Orientation.Vertical,
                BackColor     = Theme.Border,
                SplitterWidth = 3
            };
            split.Panel1.BackColor = Theme.BgMid;
            split.Panel2.BackColor = Theme.BgMid;
            Load += (s, e) =>
            {
                try
                {
                    split.Panel1MinSize    = 320;
                    split.Panel2MinSize    = 320;
                    split.SplitterDistance = Math.Max(320, Math.Min(split.Width - 320, 440));
                }
                catch { }
            };
            BuildLeftPanel(split.Panel1);
            BuildRightPanel(split.Panel2);

            // 加入順序：Fill → Top（後加的 Top 視覺上更靠頂端）
            Controls.Add(split);        // Fill
            Controls.Add(searchPanel);  // Top（視覺第二，在 header 正下方）
            Controls.Add(header);       // Top（視覺最頂端，最後加）
        }

        // ═══════════════════════════════════════════════════════════
        // 左側：道具清單（不含搜尋，搜尋已移至頂層）
        // ═══════════════════════════════════════════════════════════
        private void BuildLeftPanel(Panel p)
        {
            // TableLayoutPanel：3列（標題 / 清單 / 翻頁）
            var layout = new TableLayoutPanel
            {
                Dock            = DockStyle.Fill,
                ColumnCount     = 1,
                RowCount        = 3,
                Margin          = Padding.Empty,
                Padding         = Padding.Empty,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));    // 標題
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));   // 清單
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));    // 翻頁

            // 標題列（含 STEP 2 引導）
            var titleBar = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(14, 28, 52) };
            _itemCountLbl = new Label
            {
                Text      = "📦  道具清單  ←  STEP 2：雙擊任一列選取道具",
                ForeColor = Theme.AccentBlue,
                Font      = Theme.FontHeader,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(10, 0, 0, 0),
            };
            titleBar.Controls.Add(_itemCountLbl);
            layout.Controls.Add(titleBar, 0, 0);

            // 道具 DataGridView
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
                Name = "colName", HeaderText = "道具名稱",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                SortMode = DataGridViewColumnSortMode.NotSortable,
            });
            _itemDgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colId", HeaderText = "道具編號", Width = 76,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _itemDgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDesc", HeaderText = "說明", Width = 100,
                SortMode = DataGridViewColumnSortMode.NotSortable,
            });

            _itemDgv.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0) ConfirmSelectRow(e.RowIndex);
            };
            _itemDgv.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && _itemDgv.CurrentRow != null)
                { ConfirmSelectRow(_itemDgv.CurrentRow.Index); e.Handled = true; }
            };
            _itemDgv.CellToolTipTextNeeded += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= _itemDgv.Rows.Count) return;
                if (_itemDgv.Rows[e.RowIndex].Tag is ItemInfo item)
                    e.ToolTipText = $"名稱：{item.Name}\n道具編號：{item.Id}\n說明：{item.Description}";
            };
            layout.Controls.Add(_itemDgv, 0, 1);

            // 翻頁列
            var navBar = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Theme.BgDark,
                Padding   = new Padding(6, 6, 6, 6)
            };
            _btnPrev = new Button
            {
                Text      = "◀ 上頁",
                Width     = 80,
                Dock      = DockStyle.Left,
                BackColor = Theme.BgLight,
                ForeColor = Theme.TextMuted,
                FlatStyle = FlatStyle.Flat,
                Font      = Theme.FontSmall,
                Cursor    = Cursors.Hand,
                UseVisualStyleBackColor = false,
            };
            _btnPrev.FlatAppearance.BorderColor = Theme.Border;
            _btnPrev.Click += (s, e) => { if (_currentPage > 0) { _currentPage--; FillPage(); } };

            _btnNext = new Button
            {
                Text      = "下頁 ▶",
                Width     = 80,
                Dock      = DockStyle.Right,
                BackColor = Theme.BgLight,
                ForeColor = Theme.TextMuted,
                FlatStyle = FlatStyle.Flat,
                Font      = Theme.FontSmall,
                Cursor    = Cursors.Hand,
                UseVisualStyleBackColor = false,
            };
            _btnNext.FlatAppearance.BorderColor = Theme.Border;
            _btnNext.Click += (s, e) => { if (_currentPage < MaxPage - 1) { _currentPage++; FillPage(); } };

            _pageLabel = new Label
            {
                Text      = "載入中…",
                ForeColor = Theme.TextSecondary,
                Font      = Theme.FontSmall,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
            };
            // DockStyle 順序：Left → Right → Fill
            navBar.Controls.Add(_btnPrev);
            navBar.Controls.Add(_btnNext);
            navBar.Controls.Add(_pageLabel);
            layout.Controls.Add(navBar, 0, 2);

            p.Controls.Add(layout);
        }

        // ═══════════════════════════════════════════════════════════
        // 右側：購物車 + 發送設定 + 郵件記錄
        // ═══════════════════════════════════════════════════════════
        private void BuildRightPanel(Panel p)
        {
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            p.Controls.Add(scroll);

            int y = 12, x = 14;

            // ── STEP 2 購物車標題列 ─────────────────────────────────
            var cartHdrPanel = new Panel
            {
                Location  = new Point(x, y),
                Size      = new Size(460, 30),
                BackColor = Theme.BgDark
            };
            cartHdrPanel.Controls.Add(new Label
            {
                Text      = "  🛒  STEP 2 — 雙擊左側道具加入清單（可加多個不同道具）",
                ForeColor = Color.FromArgb(100, 180, 255),
                Font      = new Font(Theme.FontFamily, 9f, FontStyle.Bold),
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            });
            var btnClearCart = Theme.MakeButton("🗑 清空", Theme.AccentRed, Color.White, 62, 24);
            btnClearCart.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClearCart.Font   = Theme.FontSmall;
            btnClearCart.Click += (s, e) => { _cart.Clear(); RefreshCartDgv(); };
            cartHdrPanel.Controls.Add(btnClearCart);
            cartHdrPanel.Resize += (s, e) => btnClearCart.Left = cartHdrPanel.ClientSize.Width - 4 - btnClearCart.Width;
            scroll.Controls.Add(cartHdrPanel);
            y += 32;

            // ── 購物車 DataGridView ─────────────────────────────────
            _cartDgv = new DataGridView
            {
                Location             = new Point(x, y),
                Size                 = new Size(460, 160),
                ReadOnly             = false,
                AllowUserToAddRows   = false,
                AllowUserToDeleteRows= false,
                SelectionMode        = DataGridViewSelectionMode.FullRowSelect,
                RowTemplate          = { Height = 26 },
                ColumnHeadersHeight  = 26,
                MultiSelect          = false,
                BackgroundColor      = Theme.BgCard,
                GridColor            = Theme.Border,
                BorderStyle          = BorderStyle.None
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
                Name = "cQty", HeaderText = "數量", Width = 55,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _cartDgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "cType", HeaderText = "Type", Width = 48,
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

            // 編輯 Qty / Type 直接改 cart
            _cartDgv.CellEndEdit += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= _cart.Count) return;
                var col = _cartDgv.Columns[e.ColumnIndex].Name;
                var raw = _cartDgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? "1";
                if (col == "cQty"  && int.TryParse(raw, out int q)) _cart[e.RowIndex].Qty  = Math.Max(1, q);
                if (col == "cType" && int.TryParse(raw, out int t)) _cart[e.RowIndex].Type = Math.Max(0, t);
                RefreshCartDgv();
            };
            // 移除按鈕
            _cartDgv.CellClick += (s, e) =>
            {
                if (e.RowIndex < 0 || _cartDgv.Columns[e.ColumnIndex].Name != "cRemove") return;
                _cart.RemoveAt(e.RowIndex);
                RefreshCartDgv();
            };

            _lblCartHint = new Label
            {
                Text      = "← 從左側清單雙擊道具以加入購物車",
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock      = DockStyle.Fill
            };

            scroll.Controls.Add(_cartDgv);
            y += 168;

            var sendPanel = new Panel
            {
                Location    = new Point(x, y),
                Size        = new Size(440, 210),
                BackColor   = Theme.BgCard,
                BorderStyle = BorderStyle.FixedSingle
            };

            int sy = 10;
            void AddSendRow(string lblTxt, Control ctrl, int ctrlW, string hint = null)
            {
                sendPanel.Controls.Add(new Label
                {
                    Text = lblTxt, ForeColor = Theme.TextSecondary, Font = Theme.FontBody,
                    Width = 76, Location = new Point(8, sy + 3), TextAlign = ContentAlignment.MiddleRight
                });
                ctrl.Location = new Point(88, sy);
                ctrl.Width    = ctrlW;
                sendPanel.Controls.Add(ctrl);
                if (hint != null)
                    sendPanel.Controls.Add(new Label
                    {
                        Text = hint, ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                        AutoSize = true, Location = new Point(88 + ctrlW + 6, sy + 4)
                    });
                sy += ctrl.Height + 8;
            }

            _txtTitle = new TextBox
            {
                Height = 26, MaxLength = 60,
                BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary, Font = Theme.FontBody,
                PlaceholderText = "預設使用道具名稱"
            };
            _txtContent = new TextBox
            {
                Height = 26, MaxLength = 120,
                BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary, Font = Theme.FontBody,
                PlaceholderText = "預設使用道具名稱"
            };
            // 數量/type 改為在購物車欄直接編輯
            _nudQty  = new NumericUpDown { Minimum = 1, Maximum = 99, Value = 1 };
            _nudType = new NumericUpDown { Minimum = 0, Maximum = 9,  Value = 1 };

            // 預約發送
            _chkSchedule = new CheckBox
            {
                Text = "預約發送時間", ForeColor = Theme.TextSecondary, Font = Theme.FontBody,
                AutoSize = true, Checked = false
            };
            _dtStart = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy/MM/dd  HH:mm",
                Width = 180, Height = 26,
                Value = DateTime.Now.AddHours(1), Enabled = false
            };
            _chkSchedule.CheckedChanged += (s, e) => _dtStart.Enabled = _chkSchedule.Checked;

            // 範本按鈕（放在標題行右側）
            var tplBtn = Theme.MakeTemplateButton(_txtTitle, _txtContent);

            AddSendRow("標      題：", _txtTitle,  260);
            tplBtn.Location = new Point(88 + 266, sy - 26 - 8 + 2);
            sendPanel.Controls.Add(tplBtn);

            AddSendRow("信件內容：", _txtContent, 260);
            // 數量/type 改為在購物車欄直接編輯，此處移除

            // 預約行（特殊：checkbox + picker 並排）
            {
                sendPanel.Controls.Add(new Label
                {
                    Text = "發送時間：", ForeColor = Theme.TextSecondary, Font = Theme.FontBody,
                    Width = 76, Location = new Point(8, sy + 3), TextAlign = ContentAlignment.MiddleRight
                });
                _chkSchedule.Location = new Point(88, sy + 2);
                _dtStart.Location     = new Point(88 + _chkSchedule.Width + 6, sy);
                sendPanel.Controls.AddRange(new Control[] { _chkSchedule, _dtStart });
                sendPanel.Controls.Add(new Label
                {
                    Text = "（不勾選 = 立即）", ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                    AutoSize = true, Location = new Point(_dtStart.Right + 6, sy + 4)
                });
                sy += 32;
            }

            sendPanel.Controls.Add(new Label
            {
                Text = "📬 道具寫入郵件信箱（maildata），玩家重新登入後在信件欄領取",
                ForeColor = Theme.AccentGreen, Font = Theme.FontSmall,
                AutoSize = false, Size = new Size(420, 20), Location = new Point(8, sy + 4)
            });
            sendPanel.Height = sy + 28;

            scroll.Controls.Add(sendPanel);
            y += sendPanel.Height + 10;

            y += 8;
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
            _sendBtn.FlatAppearance.BorderSize  = 0;
            _sendBtn.Click += SendBtn_Click;
            scroll.Controls.Add(_sendBtn);
            y += 54;

            _statusLbl = new Label
            {
                Text      = "",
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                AutoSize  = false,
                Size      = new Size(440, 44),
                Location  = new Point(x, y)
            };
            scroll.Controls.Add(_statusLbl);
            y += 50;

            scroll.Controls.Add(new Label
            {
                Text      = "📋  此角色的郵件記錄",
                ForeColor = Theme.TextSecondary, Font = Theme.FontSmall,
                AutoSize  = true, Location = new Point(x, y)
            });
            y += 22;

            _histDgv = new DataGridView { Location = new Point(x, y), Size = new Size(440, 170) };
            Theme.StyleDataGridView(_histDgv);
            _histDgv.ReadOnly = true;
            _histDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "hType",  HeaderText = "類型", Width = 58 });
            _histDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "hBuff1", HeaderText = "名稱", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _histDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "hData",  HeaderText = "編號", Width = 54 });
            _histDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "hSend",  HeaderText = "發送時間", Width = 100 });
            _histDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "hStat",  HeaderText = "狀態", Width = 58 });
            _histDgv.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "hDel", HeaderText = "", Width = 34, FlatStyle = FlatStyle.Flat,
                UseColumnTextForButtonValue = true, Text = "🗑",
                DefaultCellStyle = { BackColor = Theme.AccentRed, ForeColor = Color.White, SelectionBackColor = Theme.AccentRed }
            });
            _histDgv.CellClick += HistDgv_CellClick;
            scroll.Controls.Add(_histDgv);

            p.Resize += (s, e) =>
            {
                int w = Math.Max(260, p.Width - 28);
                cartHdrPanel.Width = w;
                _cartDgv.Width    = w;
                sendPanel.Width   = w;
                _txtTitle.Width   = Math.Max(100, w - 100);
                _txtContent.Width = Math.Max(100, w - 100);
                _sendBtn.Width    = w;
                _statusLbl.Width  = w;
                _histDgv.Width    = w;
            };
        }

        // ═══════════════════════════════════════════════════════════
        // 篩選 & 翻頁
        // ═══════════════════════════════════════════════════════════
        private void ApplyFilter()
        {
            var gm = GameDataManager.Instance;
            if (!gm.ItemsLoaded)
            {
                _itemCountLbl.Text    = "📦  道具清單  ⚠ 請至「⚙ 資料設定」載入 items.xlsx";
                _itemCountLbl.ForeColor = Theme.AccentOrange;
                _filteredItems.Clear();
                _itemDgv.Rows.Clear();
                _pageLabel.Text   = "未載入道具資料";
                _btnPrev.Enabled  = false;
                _btnNext.Enabled  = false;
                return;
            }

            string q = _searchBox.Text.Trim();
            _filteredItems = string.IsNullOrEmpty(q)
                ? gm.GetAllItems()
                : gm.SearchItems(q);

            int total = gm.ItemCount;
            _itemCountLbl.ForeColor = Theme.AccentBlue;
            _itemCountLbl.Text = string.IsNullOrEmpty(q)
                ? $"📦  道具清單  共 {total} 筆（每頁 {PageSize} 筆）"
                : _filteredItems.Count == 0
                    ? $"📦  無符合「{q}」的道具（共 {total} 筆）"
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
            int mp    = MaxPage;
            if (total == 0)
            {
                _pageLabel.Text = "無資料";
            }
            else
            {
                int from = _currentPage * PageSize + 1;
                int to   = Math.Min((_currentPage + 1) * PageSize, total);
                _pageLabel.Text = $"第 {_currentPage + 1} / {mp} 頁  ·  第 {from}～{to} 筆 / 共 {total} 筆";
            }
            _btnPrev.Enabled   = _currentPage > 0;
            _btnNext.Enabled   = _currentPage < mp - 1;
            _btnPrev.ForeColor = _btnPrev.Enabled ? Theme.TextPrimary : Theme.TextMuted;
            _btnNext.ForeColor = _btnNext.Enabled ? Theme.TextPrimary : Theme.TextMuted;
        }

        private void ConfirmSelectRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _itemDgv.Rows.Count) return;
            if (_itemDgv.Rows[rowIndex].Tag is not ItemInfo item) return;

            // 加入購物車（已有同 ID 則累加數量）
            var existing = _cart.Find(c => c.Item.Id == item.Id);
            if (existing != null)
            {
                existing.Qty = Math.Min(existing.Qty + 1, 99);
                ShowStatus($"✓ 數量 +1：{item.Name}（購物車共 {_cart.Count} 種道具）", null);
            }
            else
            {
                _cart.Add(new CartEntry { Item = item, Qty = 1, Type = 1 });
                ShowStatus($"✓ 已加入：{item.Name}（購物車共 {_cart.Count} 種道具）", null);
            }
            RefreshCartDgv();
        }

        private void RefreshCartDgv()
        {
            _cartDgv.Rows.Clear();
            foreach (var entry in _cart)
            {
                int ri = _cartDgv.Rows.Add(entry.Item.Name, entry.Item.Id, entry.Qty, entry.Type, "✕");
            }
            bool hasItems = _cart.Count > 0;
            _sendBtn.Enabled   = hasItems;
            _sendBtn.BackColor = hasItems ? Theme.AccentGreen : Color.FromArgb(60, 62, 78);
            _sendBtn.ForeColor = hasItems ? Color.White       : Theme.TextMuted;
            _sendBtn.Text      = hasItems
                ? $"✉  發送 {_cart.Count} 種道具（共 {_cart.Sum(c => c.Qty)} 份）至 {_player.OnlineName} 郵件信箱"
                : "🛒  請先從左側清單加入道具至購物車";
        }

        // ═══════════════════════════════════════════════════════════
        // 發送（支援多道具購物車）
        // ═══════════════════════════════════════════════════════════
        private async void SendBtn_Click(object sender, EventArgs e)
        {
            if (_cart.Count == 0)
            { ShowStatus("⚠ 購物車是空的，請先雙擊左側清單加入道具！", false); return; }

            string title   = _txtTitle.Text.Trim();
            string content = _txtContent.Text.Trim();
            int nowTs   = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            int startTs = _chkSchedule.Checked
                ? (int)new DateTimeOffset(DateTime.SpecifyKind(_dtStart.Value, DateTimeKind.Local))
                        .ToUniversalTime().ToUnixTimeSeconds()
                : nowTs;
            string scheduleNote = _chkSchedule.Checked
                ? $"  預約時間：{_dtStart.Value:yyyy/MM/dd HH:mm}\n"
                : "  發送時間：立即\n";

            // 組合購物車清單文字
            var cartLines = string.Join("\n", _cart.Select(c =>
                $"  • {c.Item.Name}（編號 {c.Item.Id}）× {c.Qty}"));

            if (MessageBox.Show(
                $"確認發送以下 {_cart.Count} 種道具給玩家？\n\n" +
                $"  玩家：{_player.OnlineName}（{_player.Account}）\n" +
                $"  標　　題：{(string.IsNullOrEmpty(title) ? "（各道具名稱）" : title)}\n" +
                $"  信件內容：{(string.IsNullOrEmpty(content) ? "（各道具名稱）" : content)}\n" +
                scheduleNote +
                $"  道具清單（每種各寫入一封郵件）：\n{cartLines}",
                "確認批量發送", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            _sendBtn.Enabled = false;
            int success = 0, fail = 0;
            ShowStatus("發送中…", null);
            try
            {
                foreach (var entry in _cart)
                {
                    string itemTitle   = string.IsNullOrEmpty(title)   ? entry.Item.Name : title;
                    string itemContent = string.IsNullOrEmpty(content) ? entry.Item.Name : content;

                    for (int q = 0; q < entry.Qty; q++)
                    {
                        bool ok = await DatabaseManager.Instance.SendMailAsync(new SendMailRequest
                        {
                            Type      = entry.Type,
                            Cdkey     = _player.Account,
                            Buff1     = itemTitle,
                            Buff2     = itemContent,
                            Buff3     = entry.Item.Description,
                            Data      = entry.Item.Id,
                            StartTime = startTs,
                            EndTime   = startTs + 30 * 24 * 3600,
                            Quantity  = 1,
                            Operator  = GmLogger.Instance.OperatorName
                        });
                        if (ok) success++; else fail++;
                    }
                    ShowStatus($"發送中… {entry.Item.Name}", null);
                }
                int total = success + fail;
                ShowStatus(fail == 0
                    ? $"✓ 全部 {total} 封郵件發送成功！玩家重新登入後可在信件欄領取。"
                    : $"⚠ {success} 成功 / {fail} 失敗，請確認資料庫連線", fail == 0);
                if (success > 0)
                {
                    _cart.Clear();
                    RefreshCartDgv();
                    await LoadHistoryAsync();
                }
            }
            catch (Exception ex) { ShowStatus("✗ 錯誤：" + ex.Message, false); }
            finally { _sendBtn.Enabled = _cart.Count > 0; }
        }

        private void ShowStatus(string msg, bool? ok)
        {
            _statusLbl.Text = msg;
            _statusLbl.ForeColor = ok == null ? Theme.AccentOrange
                                 : ok == true  ? Theme.AccentGreen : Theme.AccentRed;
        }

        // ═══════════════════════════════════════════════════════════
        // 郵件記錄
        // ═══════════════════════════════════════════════════════════
        private async Task LoadHistoryAsync()
        {
            try
            {
                var records = await DatabaseManager.Instance.GetMailHistoryAsync(_player.Account);
                if (InvokeRequired) Invoke(new Action(() => FillHistory(records)));
                else FillHistory(records);
            }
            catch { }
        }

        private void FillHistory(List<MailRecord> records)
        {
            _histDgv.Rows.Clear();
            foreach (var r in records)
            {
                int i = _histDgv.Rows.Add(r.TypeStr, r.Buff1, r.Data, r.SendTimeStr, r.StatusStr, "🗑");
                _histDgv.Rows[i].Tag = r;
                if (r.CheckFlag == 1)
                    _histDgv.Rows[i].DefaultCellStyle.ForeColor = Theme.TextMuted;
            }
        }

        private async void HistDgv_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || _histDgv.Columns[e.ColumnIndex].Name != "hDel") return;
            if (_histDgv.Rows[e.RowIndex].Tag is not MailRecord rec) return;
            if (MessageBox.Show($"確定刪除郵件「{rec.Buff1}」？",
                "確認刪除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                if (await DatabaseManager.Instance.DeleteMailAsync(rec.Id))
                    _histDgv.Rows.RemoveAt(e.RowIndex);
                else MessageBox.Show("刪除失敗");
            }
            catch (Exception ex) { MessageBox.Show("錯誤：" + ex.Message); }
        }
    }

    // ── 範本名稱對話框 ──────────────────────────────────────────
    public class TemplateNameDialog : Form
    {
        private TextBox _box;
        public string TemplateName => _box.Text.Trim();
        public TemplateNameDialog()
        {
            Text = "儲存範本"; Size = new Size(360, 140);
            BackColor = Theme.BgMid; ForeColor = Theme.TextPrimary;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            var nameLbl = Theme.MakeLabel("範本名稱："); nameLbl.Location = new Point(20, 16); Controls.Add(nameLbl);
            _box = Theme.MakeTextBox(300); _box.Location = new Point(20, 40); _box.PlaceholderText = "例：新春限定禮包"; Controls.Add(_box);
            var ok = Theme.MakeButton("確 定", Theme.AccentBlue, Color.White, 100, 30);
            ok.Location = new Point(130, 76); ok.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); }; Controls.Add(ok);
        }
    }
}
