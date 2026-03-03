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
        // 支援多位收件人
        private readonly List<PlayerInfo> _recipients = new();
        private FlowLayoutPanel _recipientFlow;
        private Panel           _recipientsHdr;

        // ── 購物車資料結構 ──
        private class CartEntry
        {
            public ItemInfo Item { get; set; }
            public int Qty  { get; set; } = 1;
            public int Type { get; set; } = 0;   // 此遊戲用 type=0 + data=itemId 格式
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

        // 從 BatchOpsHubForm 整合頁開啟（無預選玩家）
        public SendForm()
        {
            InitUI();
            ApplyFilter();
        }

        public SendForm(PlayerInfo player)
        {
            _recipients.Add(player);
            InitUI();
            _ = LoadHistoryAsync();
            ApplyFilter();
        }

        // 也可由外部傳入多位玩家
        public SendForm(IEnumerable<PlayerInfo> players)
        {
            _recipients.AddRange(players);
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
            var firstName = _recipients.Count > 0 ? _recipients[0].OnlineName : "—";
            Text          = _recipients.Count == 1
                ? $"✉ 道具發送 — {firstName}"
                : $"✉ 道具發送 — {_recipients.Count} 位玩家";
            Size          = new Size(1120, 740);
            MinimumSize   = new Size(860, 560);
            BackColor     = Theme.BgMid;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;

            // ── ① 收件人工具列（同 RechargeForm / PlayerHistoryForm 風格）──
            // 左：搜尋輸入 + 按鈕    右：收件人 chips
            _recipientsHdr = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 54,
                BackColor = Color.FromArgb(22, 24, 36),
                Padding   = new Padding(10, 0, 10, 0)
            };
            _recipientsHdr.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme.Border });

            // 搜尋標籤
            _recipientsHdr.Controls.Add(new Label
            {
                Text      = "搜尋收件人：",
                ForeColor = Theme.TextMuted,
                Font      = new Font(Theme.FontFamily, 8.5f),
                Left = 12, Top = 18, AutoSize = true
            });

            // 搜尋輸入框
            var txtAddSearch = new TextBox
            {
                PlaceholderText = "主帳號 / 角色名 / UID（Enter 搜尋，主帳號可帶出全部子帳號）",
                BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary,
                Font      = Theme.FontSmall,
                Left = 100, Top = 14, Width = 320, Height = 26,
            };
            _recipientsHdr.Controls.Add(txtAddSearch);

            // 搜尋按鈕
            var btnAdd = Theme.MakeButton("🔍 搜尋加入", Color.FromArgb(0, 60, 120), Color.FromArgb(100, 200, 255), 100, 28);
            btnAdd.Font = new Font(Theme.FontFamily, 8.5f, FontStyle.Bold);
            btnAdd.Left = 428; btnAdd.Top = 13;
            _recipientsHdr.Controls.Add(btnAdd);

            // 分隔線
            _recipientsHdr.Controls.Add(new Panel { Left = 538, Top = 10, Width = 1, Height = 34, BackColor = Theme.Border });

            // 收件人 label
            _recipientsHdr.Controls.Add(new Label
            {
                Text = "收件人：", ForeColor = Theme.TextMuted,
                Font = new Font(Theme.FontFamily, 8.5f, FontStyle.Bold),
                Left = 548, Top = 18, AutoSize = true
            });

            // 收件人 chips
            _recipientFlow = new FlowLayoutPanel
            {
                Left = 618, Top = 12, Height = 30, Width = 600,
                BackColor = Color.Transparent, AutoScroll = false, WrapContents = false,
            };
            _recipientsHdr.Controls.Add(_recipientFlow);

            // 搜尋邏輯
            Func<Task> doSearch = async () =>
            {
                string q = txtAddSearch.Text.Trim();
                if (string.IsNullOrEmpty(q)) return;
                var picked = await PlayerPickerHelper.PickMultiAsync(this, q, multiMode: true);
                if (picked == null || picked.Count == 0) return;
                int added = 0;
                foreach (var p in picked)
                {
                    if (_recipients.Any(r => r.Account == p.Account)) continue;
                    _recipients.Add(p); added++;
                }
                if (added > 0)
                {
                    txtAddSearch.Clear();
                    RefreshRecipientChips(btnAdd);
                    Text = $"✉ 道具發送 — {_recipients.Count} 位玩家";
                    _ = LoadHistoryAsync();
                }
                else
                    MessageBox.Show("選取的角色已全部在收件人列表中", "重複", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            btnAdd.Click         += async (s, e) => await doSearch();
            txtAddSearch.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await doSearch(); } };

            RefreshRecipientChips(btnAdd);

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
                    split.Panel1MinSize    = 260;
                    split.Panel2MinSize    = 260;
                    split.SplitterDistance = Math.Max(260, Math.Min(split.Width - 260, 580));
                }
                catch { }
            };
            BuildLeftPanel(split.Panel1);
            BuildRightPanel(split.Panel2);

            // 加入順序：Fill → Top（後加的 Top 視覺上更靠頂端）
            Controls.Add(split);           // Fill
            Controls.Add(searchPanel);     // Top（視覺第二，在 header 正下方）
            Controls.Add(_recipientsHdr);  // Top（視覺最頂端，最後加）
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
                Name = "colIdx", HeaderText = "序號", Width = 52, MinimumWidth = 40,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _itemDgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colName", HeaderText = "道具名稱",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 52, MinimumWidth = 100,
                SortMode = DataGridViewColumnSortMode.NotSortable,
            });
            _itemDgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colId", HeaderText = "道具編號", Width = 76, MinimumWidth = 60,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _itemDgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDesc", HeaderText = "說明",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 48, MinimumWidth = 80,
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
                ToolTipText = "1=道具 2=寵物 3=金幣 4=元寶 5=道具(不可轉) 6=公會資金 7=寵物糖果 8=VIP點",
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

            // Type 說明提示
            scroll.Controls.Add(new Label
            {
                Text      = "T欄 Type: 1=道具  2=寵物  3=金幣  4=元寶  5=道具(不可轉)  6=公會資金  7=寵物糖果  8=VIP點",
                ForeColor = Theme.TextMuted,
                Font      = new Font(Theme.FontFamily, 7.5f),
                AutoSize  = false, Size = new Size(440, 18),
                Location  = new Point(x, y),
                BackColor = Color.FromArgb(20, 22, 34)
            });
            y += 20;

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
            _nudQty  = new NumericUpDown { Minimum = 1, Maximum = 99, Value = 1, BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary };
            _nudType = new NumericUpDown { Minimum = 0, Maximum = 9,  Value = 0, BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary };

            // 預約發送
            _chkSchedule = new CheckBox
            {
                Text = "預約發送時間", ForeColor = Theme.TextSecondary, Font = Theme.FontBody,
                AutoSize = true, Checked = false, FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent
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

            // 郵件記錄標題列（標題 + 🧬 完整欄位 按鈕）
            var histHdr = new Panel { Location = new Point(x, y), Size = new Size(440, 24), BackColor = Color.Transparent };
            histHdr.Controls.Add(new Label
            {
                Text = "📋  此角色的郵件記錄（雙擊查看完整欄位）",
                ForeColor = Theme.TextSecondary, Font = Theme.FontSmall,
                AutoSize = true, Location = new Point(0, 3)
            });
            var btnMailFull = Theme.MakeButton("🧬 完整欄位", Color.FromArgb(60, 30, 90), Color.FromArgb(139, 92, 246), 80, 22);
            btnMailFull.Font     = new Font(Theme.FontFamily, 7.5f);
            btnMailFull.Location = new Point(320, 0);
            btnMailFull.Anchor   = AnchorStyles.Right | AnchorStyles.Top;
            btnMailFull.Click   += async (s, e) =>
            {
                if (_recipients.Count == 0) return;
                var recs = await DatabaseManager.Instance.GetMailHistoryAsync(_recipients[0].Account);
                ShowMailFullDialog(_recipients[0].OnlineName, recs);
            };
            histHdr.Controls.Add(btnMailFull);
            scroll.Controls.Add(histHdr);
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
            _histDgv.CellClick       += HistDgv_CellClick;
            _histDgv.CellDoubleClick += HistDgv_DoubleClick;
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
                histHdr.Width     = w;
                _histDgv.Width    = w;
                btnMailFull.Left  = Math.Max(200, w - 84);
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
                _cart.Add(new CartEntry { Item = item, Qty = 1, Type = (int)_nudType.Value });
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
                ? $"✉  發送 {_cart.Count} 種道具（共 {_cart.Sum(c => c.Qty)} 份）至 {(_recipients.Count == 1 ? _recipients[0].OnlineName : _recipients.Count + " 位玩家")} 郵件信箱"
                : "🛒  請先從左側清單加入道具至購物車";
        }

        // ═══════════════════════════════════════════════════════════
        // 發送（支援多道具購物車）
        // ═══════════════════════════════════════════════════════════
        private void RefreshRecipientChips(Button addBtn)
        {
            _recipientFlow.Controls.Clear();
            foreach (var p in _recipients.ToList())
            {
                var chip = new Panel
                {
                    BackColor   = Color.FromArgb(20, 50, 100),
                    Size        = new Size(0, 28),
                    Margin      = new Padding(0, 4, 6, 0),
                    BorderStyle = BorderStyle.FixedSingle,
                };
                string statusEmoji = p.IsOnline ? "🟢" : "⚫";
                var lblName = new Label
                {
                    Text      = $"{statusEmoji} {(string.IsNullOrEmpty(p.OnlineName) ? p.Account : p.OnlineName)}",
                    ForeColor = p.IsOnline ? Color.FromArgb(80, 220, 140) : Theme.TextSecondary,
                    Font      = Theme.FontSmall,
                    AutoSize  = true,
                    Location  = new Point(4, 5),
                };
                var btnX = new Button
                {
                    Text      = "✕",
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Theme.TextMuted,
                    BackColor = Color.Transparent,
                    Size      = new Size(20, 20),
                    Font      = new Font(Theme.FontFamily, 7.5f),
                    Cursor    = Cursors.Hand,
                    TabStop   = false,
                };
                btnX.FlatAppearance.BorderSize = 0;
                btnX.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 0, 0);
                var captured = p;
                btnX.Click += (s, ev) =>
                {
                    if (_recipients.Count <= 1) { MessageBox.Show("至少需要一位收件人", "提示"); return; }
                    _recipients.Remove(captured);
                    RefreshRecipientChips(addBtn);
                    Text = _recipients.Count == 1
                        ? $"✉ 道具發送 — {_recipients[0].OnlineName}"
                        : $"✉ 道具發送 — {_recipients.Count} 位玩家";
                };
                chip.Controls.Add(lblName);
                lblName.BringToFront();
                chip.Resize += (s, ev) => btnX.Left = chip.Width - btnX.Width - 2;
                chip.Controls.Add(btnX);
                chip.Width = lblName.PreferredWidth + 30;
                _recipientFlow.Controls.Add(chip);
            }
            _recipientFlow.Controls.Add(addBtn);
        }

        private async void SendBtn_Click(object sender, EventArgs e)
        {
            if (_cart.Count == 0)
            { ShowStatus("⚠ 購物車是空的，請先雙擊左側清單加入道具！", false); return; }
            if (_recipients.Count == 0)
            { ShowStatus("⚠ 尚未選取收件人，請先搜尋並加入玩家！", false); return; }

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

            // 收件人列表文字
            var recipientLines = _recipients.Count == 1
                ? $"  玩家：{_recipients[0].OnlineName}（{_recipients[0].Account}）"
                : "  收件人（共 " + _recipients.Count + " 位）：\n" +
                  string.Join("\n", _recipients.Select(r => $"    • {r.OnlineName}（{r.Account}）"));

            if (MessageBox.Show(
                $"確認發送以下 {_cart.Count} 種道具？\n\n" +
                recipientLines + "\n" +
                $"  標題：{(string.IsNullOrEmpty(title) ? "（各道具名稱）" : title)}\n" +
                $"  內容：{(string.IsNullOrEmpty(content) ? "（各道具名稱）" : content)}\n" +
                scheduleNote +
                $"  道具清單：\n{cartLines}",
                "確認發送", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            _sendBtn.Enabled = false;
            int success = 0, fail = 0;
            ShowStatus("發送中…", null);
            try
            {
                foreach (var recipient in _recipients)
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
                                Cdkey     = recipient.Account,
                                Buff1     = itemTitle,
                                Buff2     = itemContent,
                                Buff3     = entry.Item.Name,   // ★ 遊戲用 buff3=道具名稱 判斷給什麼道具
                                Data      = entry.Item.Id,
                                StartTime = startTs,
                                EndTime   = startTs + 30 * 24 * 3600,
                                Quantity  = 1,
                                Operator  = GmLogger.Instance.OperatorName
                            });
                            if (ok) success++; else fail++;
                        }
                    }
                    ShowStatus($"發送中… {recipient.OnlineName}", null);
                }
                int total = success + fail;
                ShowStatus(fail == 0
                    ? $"✓ 全部 {total} 封郵件發送成功（{_recipients.Count} 位玩家）！重新登入後可在信件欄領取。"
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
                var records = await DatabaseManager.Instance.GetMailHistoryAsync(_recipients.Count > 0 ? _recipients[0].Account : "");
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

        private void HistDgv_DoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (_histDgv.Rows[e.RowIndex].Tag is not MailRecord rec) return;
            ShowMailFullDialog(_recipients.Count > 0 ? _recipients[0].OnlineName : "—", new System.Collections.Generic.List<MailRecord> { rec });
        }

        private void ShowMailFullDialog(string playerName, System.Collections.Generic.List<MailRecord> records)
        {
            var dlg = new Form
            {
                Text = $"🧬 maildata 完整欄位 — {playerName}（最新 {records.Count} 筆）",
                Size = new Size(560, 600),
                MinimumSize = new Size(440, 400),
                BackColor = Theme.BgPage,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontSmall,
                StartPosition = FormStartPosition.CenterParent
            };

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(10) };
            dlg.Controls.Add(scroll);

            int y = 0;
            foreach (var r in records)
            {
                // 記錄卡片
                var card = new Panel
                {
                    Location  = new Point(8, y),
                    Width     = 510,
                    BackColor = Theme.BgCard,
                    Padding   = new Padding(10),
                    Anchor    = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
                };

                // ★ 診斷：判斷此郵件是否可能無法領取
                // 正常 GM 道具郵件：type=0, data=非0（對應遊戲自身格式）
                bool isPureNotification = r.Type == 0 && r.Data == 0;  // 真的無道具通知
                bool wrongType          = r.Type != 0;                   // type非0可能遊戲不認識
                bool endtimeExpired = r.EndTime > 0 && r.EndTime < (int)DateTimeOffset.Now.ToUnixTimeSeconds();
                bool endtimeZero    = r.EndTime == 0;
                var  warnings = new System.Collections.Generic.List<string>();
                if (isPureNotification) warnings.Add("type=0+data=0 → 純通知（無道具可領取）");
                if (wrongType)          warnings.Add($"type={r.Type} → 遊戲可能只認識 type=0，建議改為 0");
                if (endtimeExpired)     warnings.Add($"endtime 已過期：{r.EndTimeStr}");
                if (endtimeZero)        warnings.Add("endtime=0 → 某些遊戲版本視為過期（可用「修正 endtime」功能修復）");
                if (string.IsNullOrWhiteSpace(r.Buff3)) warnings.Add("buff3 為空（可用「修正 buff3」功能回填）");

                var fields = new (string key, string val, bool highlight, bool warn)[]
                {
                    ("id",        r.Id.ToString(),                                    false, false),
                    ("type",      r.Type == 0 ? "0（道具/通知）" : $"{r.Type}（⚠建議改0）", r.Type == 0, wrongType),
                    ("cdkey",     r.Cdkey,                                            false, false),
                    ("buff1",     r.Buff1,                                            false, false),
                    ("buff2",     r.Buff2,                                            false, false),
                    ("data",      r.RawData.Length > 0 ? r.RawData : r.Data.ToString(), r.Data > 0, r.Data == 0 && r.Type > 0),
                    ("sendtime",  r.SendTimeStr,                                      false, false),
                    ("endtime",   r.EndTime == 0 ? "0（永久/不過期）" : r.EndTimeStr,  !endtimeExpired, endtimeExpired),
                    ("check",     r.CheckFlag.ToString(),                             false, false),
                    ("deleamill", r.Deleamill.ToString(),                             false, r.Deleamill != 0),
                    ("buff3",     r.Buff3,                                            false, false),
                };

                string status  = r.CheckFlag == 1 ? "✓ 已領取" : "○ 未領取";
                Color  statClr = r.CheckFlag == 1 ? Color.FromArgb(86, 196, 118) : Color.FromArgb(255, 159, 10);

                string hdrText = $"記錄 #{r.Id} — check={r.CheckFlag} deleamill={r.Deleamill}";
                if (warnings.Count > 0) hdrText += "  ⚠";
                var hdr = new Label
                {
                    Text      = hdrText,
                    ForeColor = warnings.Count > 0 ? Color.FromArgb(255, 159, 10) : Color.FromArgb(139, 92, 246),
                    Font      = new Font(Theme.FontFamily, 8.5f, FontStyle.Bold),
                    AutoSize  = true, Location = new Point(0, 0)
                };
                var statLbl = new Label
                {
                    Text      = status,
                    ForeColor = statClr,
                    Font      = Theme.FontSmall,
                    AutoSize  = true, Location = new Point(350, 0)
                };
                card.Controls.Add(hdr);
                card.Controls.Add(statLbl);

                // 欄位格 2-column grid
                int cy = 22;
                bool left = true;
                foreach (var (k, v, hi, wn) in fields)
                {
                    int cx = left ? 0 : 260;
                    string display = string.IsNullOrEmpty(v) ? "(空)" : v;
                    Color fg = wn ? Color.FromArgb(255, 100, 100)
                             : hi ? Theme.TextPrimary
                             : string.IsNullOrEmpty(v) ? Theme.TextMuted : Theme.TextSecondary;
                    var lbl = new Label
                    {
                        Text      = $"{k}: {display}",
                        ForeColor = fg,
                        Font      = (hi || wn) ? new Font(Theme.FontFamily, 8.5f, FontStyle.Bold) : Theme.FontSmall,
                        AutoSize  = false, Size = new Size(250, 18),
                        Location  = new Point(cx, cy)
                    };
                    card.Controls.Add(lbl);
                    if (!left) cy += 18;
                    left = !left;
                }
                if (!left) cy += 18; // 奇數時補一行

                // 警告訊息
                if (warnings.Count > 0)
                {
                    cy += 4;
                    string warnText = "⚠ " + string.Join("  |  ", warnings);
                    var warnLbl = new Label
                    {
                        Text      = warnText,
                        ForeColor = Color.FromArgb(255, 159, 10),
                        Font      = new Font(Theme.FontFamily, 7.5f),
                        AutoSize  = false, Size = new Size(490, 30),
                        Location  = new Point(0, cy)
                    };
                    card.Controls.Add(warnLbl);
                    cy += 32;
                }

                card.Height = cy + 12;
                scroll.Controls.Add(card);
                y += card.Height + 8;
            }

            if (records.Count == 0)
                scroll.Controls.Add(new Label { Text = "無郵件記錄", ForeColor = Theme.TextMuted, AutoSize = true, Location = new Point(10, 10) });

            // 底部提示
            var hint = new Label
            {
                Text      = "此遊戲郵件格式：type=0 + data=道具ID（遊戲原生格式，可領取）  |  data=0 = 純通知",
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                Dock = DockStyle.Bottom, Height = 22, TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0), BackColor = Theme.BgDark
            };
            dlg.Controls.Add(hint);

            dlg.ShowDialog(this);
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
