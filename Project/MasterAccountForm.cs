using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    public class MasterAccountForm : Form
    {
        private TextBox      _searchBox;
        private Button       _btnSearch;
        private Panel        _masterListPanel;   // AutoScroll 容器
        private DataGridView _dgvSub;
        private Label        _lblStatus;
        private Label        _lblSubTitle;
        private Label        _lblSubStatus;
        private SplitContainer _split;
        private string       _currentFilter = "";
        private List<MasterAccount> _masters = new();
        private int          _subLoadToken  = 0;
        private Panel        _selectedRow   = null;

        // ──────────────────────────────────────────────────────────────
        public MasterAccountForm()
        {
            Text          = "👑 主帳號管理";
            Size          = new Size(1200, 720);
            MinimumSize   = new Size(900, 540);
            BackColor     = Theme.BgMid;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;

            BuildUI();

            // Load 後：① 設定 SplitterDistance → ② 載入資料（確保有正確寬度）
            Load += (_, __) =>
            {
                BeginInvoke(new Action(() =>
                {
                    try
                    {
                        _split.Panel1MinSize = 220;
                        _split.Panel2MinSize = 420;
                        int d = Math.Max(220, Math.Min(_split.Width - 425,
                                        (int)(_split.Width * 0.26)));
                        if (d > 0) _split.SplitterDistance = d;
                    }
                    catch { }
                }));
                BeginInvoke(new Action(async () => await LoadMastersAsync("")));
            };
        }

        // ══════════════════════════════════════════════════════════════
        // UI 建構
        // ══════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            // ── 正確 DockStyle 順序：Fill → Bottom → Top（最後加的 Top 在最上方）

            // ② Fill
            _split = new SplitContainer
            {
                Dock = DockStyle.Fill, Orientation = Orientation.Vertical,
                BackColor = Theme.BgMid, SplitterWidth = 6
            };
            BuildMasterPanel(_split.Panel1);
            BuildSubPanel(_split.Panel2);
            Controls.Add(_split);

            // ③ Bottom：狀態列（放在 Fill 之後）
            var statusBar = new Panel
            {
                Dock = DockStyle.Bottom, Height = 26,
                BackColor = Color.FromArgb(8, 10, 18)
            };
            _lblStatus = new Label
            {
                Text = "載入中…", ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0)
            };
            statusBar.Controls.Add(_lblStatus);
            Controls.Add(statusBar);

            // ④ Top：標題 + 搜尋（最後加 = 最頂）
            var topBar = new Panel
            {
                Dock = DockStyle.Top, Height = 54,
                BackColor = Color.FromArgb(14, 18, 30)
            };
            topBar.Controls.Add(new Panel
            {
                Dock = DockStyle.Bottom, Height = 1,
                BackColor = Color.FromArgb(40, 50, 80)
            });
            topBar.Controls.Add(new Label
            {
                Text = "👑  主帳號管理", ForeColor = Color.FromArgb(190, 165, 255),
                Font = new Font(Theme.FontFamily, 12f, FontStyle.Bold),
                AutoSize = true, Location = new Point(16, 14)
            });
            topBar.Controls.Add(new Label
            {
                Text = "搜尋主帳號：", ForeColor = Theme.TextMuted, Font = Theme.FontBody,
                AutoSize = true, Location = new Point(210, 18)
            });
            _searchBox = new TextBox
            {
                BackColor   = Color.FromArgb(22, 28, 46), ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle, Font = Theme.FontBody,
                Width = 240, Location = new Point(302, 15),
                PlaceholderText = "帳號名稱（空白=全部）"
            };
            _searchBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) _ = LoadMastersAsync(_searchBox.Text.Trim());
            };
            topBar.Controls.Add(_searchBox);
            _btnSearch = Theme.MakeButton("🔍 查詢", Theme.AccentBlue, Color.White, 88, 28);
            _btnSearch.Location = new Point(552, 13);
            _btnSearch.Click   += (s, e) => _ = LoadMastersAsync(_searchBox.Text.Trim());
            topBar.Controls.Add(_btnSearch);
            Controls.Add(topBar);
        }

        // ══════════════════════════════════════════════════════════════
        // 左側：主帳號列表
        // 用 TableLayoutPanel 保證 header + scrollList 不重疊
        // ══════════════════════════════════════════════════════════════
        private void BuildMasterPanel(SplitterPanel panel)
        {
            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2, ColumnCount = 1,
                BackColor = Color.FromArgb(12, 16, 28)
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));   // header
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));   // list

            // ① Header
            var hdr = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(16, 22, 40) };
            hdr.Controls.Add(new Label
            {
                Text = "  👑  主帳號列表", ForeColor = Color.FromArgb(160, 140, 220),
                Font = new Font(Theme.FontFamily, 9f, FontStyle.Bold),
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft
            });
            hdr.Controls.Add(new Panel
            {
                Dock = DockStyle.Bottom, Height = 1,
                BackColor = Color.FromArgb(35, 45, 70)
            });
            tbl.Controls.Add(hdr, 0, 0);

            // ② 可捲動主帳號列表容器
            _masterListPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor  = Color.FromArgb(13, 17, 28)
            };
            tbl.Controls.Add(_masterListPanel, 0, 1);

            panel.Controls.Add(tbl);
        }

        // ══════════════════════════════════════════════════════════════
        // 右側：子帳號列表
        // 用 TableLayoutPanel 保證 title + grid + status 不重疊
        // ══════════════════════════════════════════════════════════════
        private void BuildSubPanel(SplitterPanel panel)
        {
            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3, ColumnCount = 1,
                BackColor = Theme.BgMid
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));   // title
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));   // grid
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));   // status

            // ① Title
            _lblSubTitle = new Label
            {
                Text = "  📋  請點選左側主帳號查看旗下角色",
                ForeColor = Color.FromArgb(120, 150, 200),
                Font = new Font(Theme.FontFamily, 9f, FontStyle.Bold),
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.FromArgb(14, 20, 36)
            };
            tbl.Controls.Add(_lblSubTitle, 0, 0);

            // ② DataGridView
            _dgvSub = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(_dgvSub);
            _dgvSub.RowTemplate.Height = 38;
            _dgvSub.ReadOnly           = true;

            _dgvSub.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "colOnline", HeaderText = "狀態",        Width = 72 });
            _dgvSub.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "colSName",  HeaderText = "角色名稱",    Width = 130 });
            _dgvSub.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "colSCdkey", HeaderText = "帳號 (cdkey)", Width = 130 });
            _dgvSub.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "colSPets",  HeaderText = "🐾 寵物",     Width = 70 });
            _dgvSub.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colSPay", HeaderText = "💳 累積儲值",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 100
            });
            _dgvSub.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "colSLogin", HeaderText = "最後登入",    Width = 130 });
            _dgvSub.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "colSProfile", HeaderText = "", Width = 72,
                FlatStyle = FlatStyle.Flat, UseColumnTextForButtonValue = true, Text = "👤 詳情",
                DefaultCellStyle =
                {
                    BackColor = Color.FromArgb(35, 75, 145), ForeColor = Color.White,
                    SelectionBackColor = Color.FromArgb(35, 75, 145), SelectionForeColor = Color.White,
                    Font = new Font(Theme.FontFamily, 8.5f), Alignment = DataGridViewContentAlignment.MiddleCenter
                }
            });
            _dgvSub.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "colSSend", HeaderText = "", Width = 65,
                FlatStyle = FlatStyle.Flat, UseColumnTextForButtonValue = true, Text = "✉ 發送",
                DefaultCellStyle =
                {
                    BackColor = Color.FromArgb(20, 90, 160), ForeColor = Color.White,
                    SelectionBackColor = Color.FromArgb(20, 90, 160), SelectionForeColor = Color.White,
                    Font = new Font(Theme.FontFamily, 8.5f), Alignment = DataGridViewContentAlignment.MiddleCenter
                }
            });

            _dgvSub.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                var p   = _dgvSub.Rows[e.RowIndex].Tag as PlayerInfo;
                if (p  == null) return;
                string col = _dgvSub.Columns[e.ColumnIndex].Name;
                if (col == "colOnline")
                {
                    e.CellStyle.ForeColor = p.IsOnline  ? Theme.AccentGreen
                                          : p.IsBanned  ? Theme.AccentRed
                                                        : Theme.TextMuted;
                    e.CellStyle.Font = new Font(Theme.FontFamily, 9f,
                        p.IsOnline ? FontStyle.Bold : FontStyle.Regular);
                    e.FormattingApplied = true;
                }
                if (col == "colSPets"  && p.PetCount > 0)
                { e.CellStyle.ForeColor = Color.FromArgb(110, 205, 140); e.FormattingApplied = true; }
                if (col == "colSPay"   && p.PayTotal > 0)
                { e.CellStyle.ForeColor = Color.FromArgb(255, 195, 60);  e.FormattingApplied = true; }
                if (p.IsOnline) e.CellStyle.BackColor = Color.FromArgb(12, 32, 12);
            };

            _dgvSub.CellClick += async (s, e) =>
            {
                if (e.RowIndex < 0) return;
                var p = _dgvSub.Rows[e.RowIndex].Tag as PlayerInfo;
                if (p == null) return;
                switch (_dgvSub.Columns[e.ColumnIndex].Name)
                {
                    case "colSProfile": new PlayerProfileForm(p).ShowDialog(this); break;
                    case "colSSend":    new SendForm(p).ShowDialog(this);           break;
                }
            };

            tbl.Controls.Add(_dgvSub, 0, 1);

            // ③ Status bar
            _lblSubStatus = new Label
            {
                Text = "", ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
                BackColor = Color.FromArgb(8, 10, 18)
            };
            tbl.Controls.Add(_lblSubStatus, 0, 2);

            panel.Controls.Add(tbl);
        }

        // ══════════════════════════════════════════════════════════════
        // 建立主帳號列（DockStyle.Top，加入順序反轉 → 第一筆排最頂）
        // ══════════════════════════════════════════════════════════════
        private Panel BuildMasterRow(MasterAccount ma, bool isExact)
        {
            var bg      = isExact ? Color.FromArgb(130, 95, 5)    : Color.FromArgb(18, 24, 40);
            var bgSel   = isExact ? Color.FromArgb(165, 120, 10)  : Color.FromArgb(38, 55, 95);
            var bgHover = isExact ? Color.FromArgb(148, 108, 8)   : Color.FromArgb(28, 40, 68);
            var fg      = isExact ? Color.FromArgb(255, 240, 100) : Color.FromArgb(195, 210, 240);
            var fgSub   = isExact ? Color.FromArgb(210, 175, 60)  : Color.FromArgb(70, 90, 130);
            var accent  = isExact ? Color.FromArgb(255, 195, 20)  : Color.FromArgb(45, 75, 135);

            string name    = (isExact ? "★  " : "      ") + ma.Name;
            string subInfo = $"子帳號 {ma.SubCount} 個  ·  " +
                             (ma.CreatedAt.Length > 10 ? ma.CreatedAt[..10] : ma.CreatedAt);

            // DockStyle.Top：WinForms 自動撐滿寬度，不需要手動設定 Width
            var row = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 58,
                BackColor = bg,
                Cursor    = Cursors.Hand,
                Tag       = ma
            };

            // 左側彩色指示條
            row.Controls.Add(new Panel
            {
                Location  = new Point(0, 0),
                Size      = new Size(4, 58),
                BackColor = accent
            });

            var lblName = new Label
            {
                Text      = name,
                ForeColor = fg,
                Font      = new Font(Theme.FontFamily, isExact ? 10f : 9.5f,
                                     isExact ? FontStyle.Bold : FontStyle.Regular),
                AutoSize  = false,
                Left = 12, Top = 9, Height = 22,
                Anchor    = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand
            };
            row.Controls.Add(lblName);

            var lblSub = new Label
            {
                Text      = subInfo,
                ForeColor = fgSub,
                Font      = new Font(Theme.FontFamily, 8f),
                AutoSize  = false,
                Left = 14, Top = 35, Height = 18,
                Anchor    = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand
            };
            row.Controls.Add(lblSub);

            // 分隔線
            row.Controls.Add(new Panel
            {
                Dock = DockStyle.Bottom, Height = 1,
                BackColor = Color.FromArgb(25, 32, 52)
            });

            void onClick(object s, EventArgs e)
            {
                SelectRow(row, bg, bgSel);
                _ = LoadSubAccountsAsync(ma);
            }
            row.Click      += onClick;
            lblName.Click  += onClick;
            lblSub.Click   += onClick;

            void onEnter(object s, EventArgs e)
            { if (row != _selectedRow) row.BackColor = bgHover; }
            void onLeave(object s, EventArgs e)
            { if (row != _selectedRow) row.BackColor = bg; }
            row.MouseEnter    += onEnter; lblName.MouseEnter += onEnter; lblSub.MouseEnter += onEnter;
            row.MouseLeave    += onLeave; lblName.MouseLeave += onLeave; lblSub.MouseLeave += onLeave;

            return row;
        }

        private void SelectRow(Panel row, Color normalBg, Color selBg)
        {
            if (_selectedRow != null && _selectedRow != row)
            {
                var old = _selectedRow.Tag as MasterAccount;
                bool wasExact = old != null && !string.IsNullOrWhiteSpace(_currentFilter) &&
                                old.Name.Equals(_currentFilter, StringComparison.OrdinalIgnoreCase);
                _selectedRow.BackColor = wasExact
                    ? Color.FromArgb(130, 95, 5)
                    : Color.FromArgb(18, 24, 40);
            }
            row.BackColor = selBg;
            _selectedRow  = row;
        }

        // ══════════════════════════════════════════════════════════════
        // 資料載入
        // ══════════════════════════════════════════════════════════════
        private async Task LoadMastersAsync(string filter)
        {
            _currentFilter     = filter;
            _btnSearch.Enabled = false;
            _lblStatus.Text    = "查詢中…";
            _masterListPanel.Controls.Clear();
            _selectedRow = null;

            try
            {
                _masters = await DatabaseManager.Instance.GetMasterAccountsAsync(filter);

                int exactIdx = string.IsNullOrWhiteSpace(filter) ? -1 :
                    _masters.FindIndex(m => m.Name.Equals(filter, StringComparison.OrdinalIgnoreCase));

                Panel exactPanel = null;
                MasterAccount exactMa = null;

                // DockStyle.Top 行列必須以「反序」加入
                // 最後加入的 DockStyle.Top 排在最頂端
                // → 把 _masters 從後往前加入，讓 index=0（精確符合）排在最頂
                _masterListPanel.SuspendLayout();
                for (int i = _masters.Count - 1; i >= 0; i--)
                {
                    var ma      = _masters[i];
                    bool exact  = exactIdx >= 0 &&
                                  ma.Name.Equals(filter, StringComparison.OrdinalIgnoreCase);
                    var row     = BuildMasterRow(ma, exact);
                    _masterListPanel.Controls.Add(row);
                    if (exact) { exactPanel = row; exactMa = ma; }
                }
                _masterListPanel.ResumeLayout(true);

                if (exactPanel != null && exactMa != null)
                {
                    _selectedRow = exactPanel;
                    exactPanel.BackColor = Color.FromArgb(165, 120, 10);
                    _masterListPanel.ScrollControlIntoView(exactPanel);

                    int similar = _masters.Count - 1;
                    _lblStatus.Text =
                        $"★ 精確符合：{exactMa.Name}（{exactMa.SubCount} 個子帳號）" +
                        (similar > 0 ? $"  ·  另有 {similar} 個相似帳號" : "");

                    _ = LoadSubAccountsAsync(exactMa);
                }
                else
                {
                    _lblStatus.Text = _masters.Count > 0
                        ? $"共 {_masters.Count} 個主帳號" +
                          (!string.IsNullOrWhiteSpace(filter) ? "（無精確符合）" : "")
                        : "查無符合帳號";
                }
            }
            catch (Exception ex) { _lblStatus.Text = "✗ " + ex.Message; }
            finally { _btnSearch.Enabled = true; }
        }

        private async Task LoadSubAccountsAsync(MasterAccount ma)
        {
            int token = System.Threading.Interlocked.Increment(ref _subLoadToken);

            _lblSubTitle.Text  = $"  📋  【{ma.Name}】旗下子帳號（共 {ma.SubCount} 個）";
            _lblSubStatus.Text = "載入中…";
            _dgvSub.Rows.Clear();

            try
            {
                var subs = await DatabaseManager.Instance.GetSubAccountsAsync(ma.Id);
                if (token != _subLoadToken) return;

                foreach (var p in subs)
                {
                    try
                    {
                        var (banned, endTime) = await DatabaseManager.Instance.GetBanStatusAsync(p.Account);
                        if (token != _subLoadToken) return;
                        p.IsBanned = banned; p.BanEndTime = endTime;
                    }
                    catch { }
                }

                if (token != _subLoadToken) return;
                _dgvSub.Rows.Clear();

                foreach (var p in subs)
                {
                    string status = p.IsBanned ? "🔴 封禁"
                                  : p.IsOnline ? "🟢 在線"
                                               : "⚫ 離線";
                    int i = _dgvSub.Rows.Add(
                        status, p.OnlineName, p.Account,
                        p.PetCount > 0 ? $"{p.PetCount} 隻" : "—",
                        p.PayTotal > 0 ? $"{p.PayTotal:N0}" : "—",
                        p.LoginTime);
                    _dgvSub.Rows[i].Tag = p;
                }

                int online = subs.FindAll(p => p.IsOnline).Count;
                _lblSubStatus.Text = subs.Count > 0
                    ? $"共 {subs.Count} 個子帳號  ·  在線 {online} 個"
                    : $"查無子帳號（主帳號 ID={ma.Id}，名稱：{ma.Name}）";
            }
            catch (Exception ex) { _lblSubStatus.Text = "✗ " + ex.Message; }
        }
    }
}
