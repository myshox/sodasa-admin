using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    internal class PetRankingForm : UserControl
    {
        // ---- column zh name mapping ----
        private static readonly Dictionary<string,string> _colZh = new(StringComparer.OrdinalIgnoreCase)
        {
            ["cdkey"]       = "\u5E33\u865F",
            ["cdkeyluo"]    = "\u5E33\u865F(luo)",
            ["account"]     = "\u5E33\u865F",
            ["userid"]      = "\u7528\u6236ID",
            ["username"]    = "\u7528\u6236\u540D",
            ["petid"]       = "\u5BF5\u7269ID",
            ["pet_id"]      = "\u5BF5\u7269ID",
            ["name"]        = "\u5BF5\u7269\u540D\u7A31",
            ["petname"]     = "\u5BF5\u7269\u540D\u7A31",
            ["pet_name"]    = "\u5BF5\u7269\u540D\u7A31",
            ["type"]        = "\u7A2E\u985E",
            ["pettype"]     = "\u7A2E\u985E",
            ["pet_type"]    = "\u7A2E\u985E",
            ["id"]          = "\u7DE8\u865F",
            ["lv"]          = "\u7B49\u7D1A",
            ["level"]       = "\u7B49\u7D1A",
            ["hp"]          = "\u8840\u91CF",
            ["maxhp"]       = "\u6700\u5927\u8840\u91CF",
            ["attack"]      = "\u653B\u64CA",
            ["atk"]         = "\u653B\u64CA",
            ["def"]         = "\u9632\u7A61",
            ["defense"]     = "\u9632\u7A61",
            ["quick"]       = "\u654F\u6377",
            ["spd"]         = "\u654F\u6377",
            ["speed"]       = "\u654F\u6377",
            ["sum"]         = "\u6230\u9B25\u529B",
            ["power"]       = "\u6230\u9B25\u529B",
            ["combat"]      = "\u6230\u9B25\u529B",
            ["rank"]        = "\u6392\u540D",
            ["createtime"]  = "\u5EFA\u7ACB\u6642\u9593",
            ["updatetime"]  = "\u66F4\u65B0\u6642\u9593",
            ["time"]        = "\u6642\u9593",
            ["author"]      = "\u6355\u6349\u8005",
            ["owner"]       = "\u64C1\u6709\u8005",
            ["serverid"]    = "\u4F3A\u670D\u5668",
            ["server"]      = "\u4F3A\u670D\u5668",
            ["_playername"] = "\u73A9\u5BB6\u540D\u7A31",
            ["_online"]     = "\u5728\u7DDA",
            ["imageid"]     = "\u5916\u89C0ID",
            ["image_id"]    = "\u5916\u89C0ID",
            ["skinid"]      = "\u5916\u89C0ID",
            ["score"]       = "\u8A55\u5206",
            ["point"]       = "\u7A4D\u5206",
            ["exp"]         = "\u7D93\u9A57",
            ["star"]        = "\u661F\u7D1A",
            ["quality"]     = "\u54C1\u8CEA",
            ["color"]       = "\u54C1\u8CEA\u8272",
            ["remark"]      = "\u5099\u8A3B",
            ["memo"]        = "\u5099\u8A3B",
            ["status"]      = "\u72C0\u614B",
            ["flag"]        = "\u65D7\u6A19",
            ["basehp"]      = "\u521D\u59CB\u8840\u91CF",
            ["baseatk"]     = "\u521D\u59CB\u653B\u64CA",
            ["basedef"]     = "\u521D\u59CB\u9632\u7A61",
            ["basespd"]     = "\u521D\u59CB\u654F\u6377",
            ["growhp"]      = "\u8840\u91CF\u6210\u9577",
            ["growatk"]     = "\u653B\u64CA\u6210\u9577",
            ["growdef"]     = "\u9632\u7A61\u6210\u9577",
            ["growspd"]     = "\u654F\u6377\u6210\u9577",
            ["oldlv"]       = "\u521D\u59CB\u7B49\u7D1A",
            ["oldhp"]       = "\u521D\u59CB\u8840\u91CF",
            ["oldattack"]   = "\u521D\u59CB\u653B\u64CA",
            ["olddef"]      = "\u521D\u59CB\u9632\u7A61",
            ["oldquick"]    = "\u521D\u59CB\u654F\u6377",
        };

        private static string ColZh(string col) =>
            _colZh.TryGetValue(col, out var zh) ? $"{zh}\uFF08{col}\uFF09" : col;

        private class ColItem
        {
            public string Col { get; }
            public ColItem(string c) { Col = c; }
            public override string ToString() => ColZh(Col);
        }
        private static string SelCol(ComboBox cmb) =>
            (cmb.SelectedItem as ColItem)?.Col ?? cmb.SelectedItem?.ToString() ?? "";

        // ---- state ----
        private string   _tableName = "";
        private List<string> _allCols = new();
        private List<Dictionary<string,string>> _allRows = new();

        // ---- page panels ----
        private Panel _pgRank, _pgActivity, _pgDb;

        // ---- rank tab controls ----
        private Label          _lblSrc, _lblStatus;
        private ComboBox       _cmbClassifyCol, _cmbSortCol, _cmbTopN;
        private TextBox        _txtSearch, _txtKeyword;
        private ListBox        _lstValues;
        private DataGridView   _dgvMain;

        // ---- activity tab controls ----
        private ComboBox     _cmbActivityPet, _cmbActivityView;
        private DataGridView _dgvActivity;
        private Label        _lblActivityStatus;
        private Panel        _activityRankPanel;
        private TextBox      _txtPlayerSearch;
        private DataGridView _dgvPlayerEntries;
        private List<Dictionary<string,string>> _activityRows = new();
        private List<string> _activityCols = new();
        // 練寵排行榜（capturepet）
        private List<(int id, string name, int entryCount, double topScore, string lastEntry)> _capturePetTypes = new();
        private List<CaptureRankEntry> _captureLeaderboard = new();

        // ---- db tab controls ----
        private List<(string table, long rows, string columns)> _allTablesCache = new();
        private DataGridView _dgvTables, _dgvPreview;
        private Label        _lblPreview;
        private List<string> _previewCols = new();
        private List<Dictionary<string,string>> _previewRows = new();
        private string       _previewTableName = "";

        // ---- nav buttons (shared refs) ----
        private Button _navRank, _navActivity, _navDb;

        public PetRankingForm()
        {
            BackColor  = Theme.BgPage;
            ForeColor  = Theme.TextPrimary;
            Font       = Theme.FontBody;
            AutoScroll = false;
            Dock       = DockStyle.Fill;
            BuildUI();
            _ = LoadRankAsync();
        }

        // ===================================================================
        //  BuildUI  --  TableLayoutPanel: Row0=navBar(fixed), Row1=content(fill)
        //  This guarantees navBar is ALWAYS at y=0, regardless of Dock ordering.
        // ===================================================================
        private void BuildUI()
        {
            // Root layout: 2 rows - navBar row (fixed 48px) + content row (fill)
            var root = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                RowCount    = 2,
                ColumnCount = 1,
                BackColor   = Theme.BgPage,
                Padding     = Padding.Empty,
                Margin      = Padding.Empty,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));  // Row 0: navBar
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // Row 1: content
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            // ---- NavBar (Row 0) ----
            var navBar = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 24, 38),
                Margin    = Padding.Empty,
            };
            navBar.Controls.Add(new Label
            {
                Text      = "\u5BF5\u7269\u6392\u884C\u699C",
                Font      = new Font(Theme.FontFamily, 12f, FontStyle.Bold),
                ForeColor = Theme.AccentOrange,
                AutoSize  = true,
                Location  = new Point(12, 10),
            });

            Button MakeNav(string text, int x)
            {
                var btn = new Button
                {
                    Text      = text,
                    Location  = new Point(x, 8),
                    Size      = new Size(128, 32),
                    FlatStyle = FlatStyle.Flat,
                    Font      = new Font(Theme.FontFamily, 9.5f, FontStyle.Bold),
                    BackColor = Color.FromArgb(40, 52, 78),
                    ForeColor = Color.FromArgb(200, 215, 240),
                    Cursor    = Cursors.Hand,
                };
                btn.FlatAppearance.BorderSize         = 0;
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(65, 85, 120);
                return btn;
            }

            _navRank     = MakeNav("\u5BF5\u7269\u6392\u884C", 148);
            _navActivity = MakeNav("\u7DF4\u5BF5\u6392\u884C", 284);
            _navDb       = MakeNav("\u8CC7\u6599\u5EAB\u63A2\u7D22", 420);
            navBar.Controls.AddRange(new Control[] { _navRank, _navActivity, _navDb });
            root.Controls.Add(navBar, 0, 0);

            // ---- Content container (Row 1): page panels overlap here ----
            var content = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Theme.BgPage,
                Margin    = Padding.Empty,
            };

            _pgDb       = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgPage, Visible = false };
            _pgActivity = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgPage, Visible = false };
            _pgRank     = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgPage, Visible = true };

            BuildRankTab(_pgRank);
            BuildActivityTab(_pgActivity);
            BuildDbExploreTab(_pgDb);

            // Add pages - all Fill, only one visible at a time
            content.Controls.Add(_pgDb);
            content.Controls.Add(_pgActivity);
            content.Controls.Add(_pgRank);
            root.Controls.Add(content, 0, 1);

            // Wire nav button clicks
            _navRank.Click += async (s, e) =>
            {
                ShowPage(0);
                if (_allRows.Count == 0) await LoadRankAsync();
            };
            _navActivity.Click += (s, e) => ShowPage(1);
            _navDb.Click += async (s, e) =>
            {
                ShowPage(2);
                if (_dgvTables.Rows.Count == 0) await LoadAllTablesAsync();
            };

            ShowPage(0);
            Controls.Add(root);
        }

        private void ShowPage(int idx)
        {
            _pgRank.Visible     = idx == 0;
            _pgActivity.Visible = idx == 1;
            _pgDb.Visible       = idx == 2;

            void Style(Button b, bool sel)
            {
                b.BackColor = sel ? Theme.AccentOrange : Color.FromArgb(40, 52, 78);
                b.ForeColor = sel ? Color.White : Color.FromArgb(200, 215, 240);
            }
            Style(_navRank,     idx == 0);
            Style(_navActivity, idx == 1);
            Style(_navDb,       idx == 2);
        }

        // ===================================================================
        //  Rank Tab
        // ===================================================================
        private void BuildRankTab(Panel p)
        {
            // Status bar at bottom
            _lblStatus = new Label
            {
                Dock      = DockStyle.Bottom,
                Height    = 26,
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Padding   = new Padding(12, 0, 0, 0),
                BackColor = Theme.BgCard,
            };
            p.Controls.Add(_lblStatus);

            // Toolbar (added to page BEFORE navBar so navBar docks above it)
            var toolbar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 112,
                BackColor = Theme.BgCard,
            };

            // Row 1: source label + action buttons
            _lblSrc = new Label
            {
                Text      = "\u5075\u6E2C\u4E2D...",
                Location  = new Point(10, 8),
                AutoSize  = true,
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
            };
            toolbar.Controls.Add(_lblSrc);

            var btnLoad     = Theme.MakePrimaryButton("\u91CD\u65B0\u8F09\u5165", 86, 26);
            var btnCsv      = Theme.MakeButton("CSV \u532F\u51FA", Theme.BgMid, Theme.AccentGreen, 76, 26);
            var btnCopy     = Theme.MakeButton("\u8907\u88FD",     Theme.BgMid, Theme.TextSecondary, 56, 26);
            var btnClrFilter = Theme.MakeButton("\u6E05\u9664\u7BE9\u9078", Color.FromArgb(50,50,70), Theme.TextSecondary, 72, 26);
            var btnResetAll = Theme.MakeButton("\u6E05\u7A7A\u5168\u90E8", Color.FromArgb(110,15,15), Color.FromArgb(255,90,90), 80, 26);

            foreach (var b in new[] { btnLoad, btnCsv, btnCopy, btnClrFilter, btnResetAll })
                b.Font = Theme.FontSmall;

            btnLoad.Click     += async (s, e) => await LoadRankAsync();
            btnCsv.Click      += (s, e) => ExportRawCsv(_allCols, GetVisibleRows(), "petbilling");
            btnCopy.Click     += (s, e) => ExportRawCsv(_allCols, GetVisibleRows(), null);
            btnClrFilter.Click += (s, e) => { _lstValues.SelectedIndex = 0; if (_txtKeyword != null) _txtKeyword.Text = ""; RefreshMainDgv(); };
            btnResetAll.Click += async (s, e) => await ResetRecordsAsync(allRecords: true);

            var actionBtns = new[] { btnResetAll, btnClrFilter, btnCopy, btnCsv, btnLoad };
            toolbar.Resize += (s, e) =>
            {
                int r = toolbar.ClientSize.Width - 8;
                foreach (var b in actionBtns) { r -= b.Width + 5; b.Left = r; b.Top = 8; }
            };
            toolbar.Controls.AddRange(actionBtns);

            // Row 2: filter combos
            var row2 = new Panel
            {
                Location  = new Point(0, 42),
                Height    = 34,
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent,
            };
            toolbar.Layout += (s, e) => row2.Width = toolbar.ClientSize.Width;

            _cmbClassifyCol = new ComboBox
            {
                Location = new Point(68, 4), Size = new Size(170, 26),
                BackColor = Theme.BgInput, ForeColor = Theme.TextPrimary,
                FlatStyle = FlatStyle.Flat, Font = Theme.FontSmall,
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            _cmbClassifyCol.SelectedIndexChanged += OnClassifyColChanged;

            _cmbSortCol = new ComboBox
            {
                Location = new Point(312, 4), Size = new Size(170, 26),
                BackColor = Theme.BgInput, ForeColor = Theme.TextPrimary,
                FlatStyle = FlatStyle.Flat, Font = Theme.FontSmall,
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            _cmbSortCol.SelectedIndexChanged += (s, e) => RefreshMainDgv();

            row2.Controls.Add(new Label { Text = "\u5206\u985E\uFF1A", Location = new Point(6,8),   AutoSize = true, ForeColor = Theme.TextSecondary, Font = Theme.FontSmall });
            row2.Controls.Add(_cmbClassifyCol);
            row2.Controls.Add(new Label { Text = "\u6392\u5E8F\uFF1A", Location = new Point(246,8), AutoSize = true, ForeColor = Theme.TextSecondary, Font = Theme.FontSmall });
            row2.Controls.Add(_cmbSortCol);

            var btnResetSel = Theme.MakeButton("\u91CD\u7F6E\u9078\u9805", Color.FromArgb(80,20,20), Theme.AccentRed, 72, 24);
            btnResetSel.Location = new Point(490, 6); btnResetSel.Font = Theme.FontSmall;
            btnResetSel.Click += async (s, e) => await ResetRecordsAsync(allRecords: false);
            row2.Controls.Add(btnResetSel);

            // Row 3: keyword search + Top-N selector
            var row3 = new Panel
            {
                Location  = new Point(0, 78),
                Height    = 30,
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent,
            };
            toolbar.Layout += (s, e) => { row2.Width = toolbar.ClientSize.Width; row3.Width = toolbar.ClientSize.Width; };

            row3.Controls.Add(new Label
            {
                Text      = "\u641C\u5C0B\uFF1A",
                Location  = new Point(8, 8),
                AutoSize  = true,
                ForeColor = Theme.TextSecondary,
                Font      = Theme.FontSmall,
            });

            _txtKeyword = new TextBox
            {
                Location        = new Point(60, 4),
                Size            = new Size(320, 22),
                BackColor       = Theme.BgInput,
                ForeColor       = Theme.TextPrimary,
                Font            = Theme.FontSmall,
                PlaceholderText = "\u8F38\u5165\u95DC\u9375\u5B57\u904E\u6FFE\uFF08\u73A9\u5BB6\u540D\u3001\u5BF5\u7269\u540D\u3001\u5E33\u865F\u2026\uFF09",
                BorderStyle     = BorderStyle.FixedSingle,
            };
            _txtKeyword.TextChanged += (s, e) => RefreshMainDgv();

            var btnClrKw = Theme.MakeButton("\u6E05\u9664", Theme.BgMid, Theme.TextSecondary, 46, 22);
            btnClrKw.Font  = Theme.FontSmall;
            btnClrKw.Click += (s, e) => _txtKeyword.Text = "";

            var lblTopN = new Label
            {
                Text      = "\u986F\u793A\uFF1A",
                AutoSize  = true,
                ForeColor = Theme.TextSecondary,
                Font      = Theme.FontSmall,
            };
            _cmbTopN = new ComboBox
            {
                Size          = new Size(112, 22),
                BackColor     = Theme.BgInput,
                ForeColor     = Theme.TextPrimary,
                FlatStyle     = FlatStyle.Flat,
                Font          = Theme.FontSmall,
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            _cmbTopN.Items.AddRange(new object[] {
                "\u5168\u90E8",
                "\u524D 10 \u540D",
                "\u524D 25 \u540D",
                "\u524D 50 \u540D",
                "\u524D 100 \u540D",
            });
            _cmbTopN.SelectedIndex = 0;
            _cmbTopN.SelectedIndexChanged += (s, e) => RefreshMainDgv();

            row3.Controls.AddRange(new Control[] { _txtKeyword, btnClrKw, lblTopN, _cmbTopN });

            row3.Resize += (s, e) =>
            {
                int w = row3.ClientSize.Width;
                _cmbTopN.Left  = w - _cmbTopN.Width - 8;
                _cmbTopN.Top   = 4;
                lblTopN.Left   = _cmbTopN.Left - lblTopN.Width - 4;
                lblTopN.Top    = 8;
                btnClrKw.Left  = lblTopN.Left - btnClrKw.Width - 10;
                btnClrKw.Top   = 4;
                _txtKeyword.Width = Math.Max(80, btnClrKw.Left - _txtKeyword.Left - 6);
            };

            toolbar.Controls.Add(row2);
            toolbar.Controls.Add(row3);

            // SplitContainer for list + grid
            var split = new SplitContainer
            {
                Dock          = DockStyle.Fill,
                FixedPanel    = FixedPanel.Panel1,
                Panel1MinSize = 160,
                BackColor     = Theme.BgPage,
                SplitterWidth = 5,
            };
            bool splitReady = false;
            split.Layout += (s, e) =>
            {
                if (splitReady || split.Width < 300) return;
                splitReady = true;
                split.SplitterDistance = Math.Min(220, split.Width - 200);
            };

            // Left panel: classify list
            split.Panel1.BackColor = Color.FromArgb(28, 36, 52);

            var lhdr = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Color.FromArgb(22, 30, 46), Padding = new Padding(8, 6, 8, 4) };
            lhdr.Controls.Add(new Label { Text = "\u9EDE\u9078\u5206\u985E\u5024", AutoSize = true, Location = new Point(8, 4), ForeColor = Theme.TextMuted, Font = Theme.FontSmall });
            _txtSearch = new TextBox { Location = new Point(8, 24), Size = new Size(180, 22), BackColor = Theme.BgInput, ForeColor = Theme.TextPrimary, Font = Theme.FontSmall, PlaceholderText = "\u641C\u5C0B...", BorderStyle = BorderStyle.FixedSingle };
            _txtSearch.TextChanged += (s, e) => FilterValueList(_txtSearch.Text.Trim());
            lhdr.Controls.Add(_txtSearch);
            split.Panel1.Controls.Add(lhdr);

            _lstValues = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(28, 36, 52), ForeColor = Theme.TextPrimary,
                Font = Theme.FontSmall, BorderStyle = BorderStyle.None,
                ItemHeight = 28, DrawMode = DrawMode.OwnerDrawFixed,
            };
            _lstValues.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;
                bool sel = (e.State & DrawItemState.Selected) != 0;
                e.Graphics.FillRectangle(new SolidBrush(sel ? Theme.AccentOrange : Color.Transparent), e.Bounds);
                TextRenderer.DrawText(e.Graphics, _lstValues.Items[e.Index]?.ToString() ?? "",
                    Theme.FontSmall, new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height),
                    sel ? Color.White : Theme.TextPrimary, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            };
            _lstValues.SelectedIndexChanged += (s, e) => RefreshMainDgv();
            split.Panel1.Controls.Add(_lstValues);

            // Reset button for selected item - put in the LEFT PANEL HEADER area, not at bottom
            var btnResetItem = Theme.MakeButton("\u91CD\u7F6E\u6B64\u5206\u985E\u6392\u884C", Color.FromArgb(80,18,18), Theme.AccentRed, 0, 28);
            btnResetItem.Dock = DockStyle.Top; btnResetItem.Font = Theme.FontSmall;
            btnResetItem.Click += async (s, e) =>
            {
                if (_lstValues.SelectedItem?.ToString() == "\uFF08\u5168\u90E8\uFF09" || _lstValues.SelectedIndex <= 0)
                {
                    MessageBox.Show("\u8ACB\u5148\u5728\u6E05\u55AE\u9078\u4E00\u500B\u5206\u985E\u5024",
                        "\u63D0\u793A", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                await ResetRecordsAsync(allRecords: false);
            };
            // Add in order: lhdr (top), btnResetItem (top, below lhdr), lstValues (fill)
            split.Panel1.Controls.Add(btnResetItem);

            // Right panel: data grid
            split.Panel2.BackColor = Theme.BgPage;
            _dgvMain = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(_dgvMain);
            _dgvMain.RowHeadersVisible = false; _dgvMain.ColumnHeadersHeight = 32;
            _dgvMain.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.A) { _dgvMain.SelectAll(); e.Handled = true; }
                if (e.Control && e.KeyCode == Keys.C) { CopySelectionToClipboard(_dgvMain); e.Handled = true; }
            };
            _dgvMain.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    var ctx = new ContextMenuStrip();
                    ctx.Items.Add("\u5168\u9078 (Ctrl+A)", null, (_, __) => _dgvMain.SelectAll());
                    ctx.Items.Add("\u8907\u88FD\u9078\u53D6\u5217 (Ctrl+C)", null, (_, __) => CopySelectionToClipboard(_dgvMain));
                    ctx.Items.Add(new ToolStripSeparator());
                    ctx.Items.Add("CSV \u532F\u51FA\u9078\u53D6\u5217", null, (_, __) => ExportSelectedToClipboard(_dgvMain));
                    ctx.Show(_dgvMain, e.Location);
                }
            };
            split.Panel2.Controls.Add(_dgvMain);

            var rankTbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1,
                Margin = Padding.Empty, Padding = Padding.Empty,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
            };
            rankTbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 112f));
            rankTbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            rankTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            toolbar.Dock = DockStyle.Fill;
            rankTbl.Controls.Add(toolbar, 0, 0);
            rankTbl.Controls.Add(split,   0, 1);
            p.Controls.Add(rankTbl);
        }

        // ===================================================================
        //  Activity Tab  (capturepet 練寵活動排行榜) — 大改版
        // ===================================================================
        private void BuildActivityTab(Panel p)
        {
            // ── 底部狀態列 ──
            _lblActivityStatus = new Label
            {
                Dock = DockStyle.Bottom, Height = 42,
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                TextAlign = System.Drawing.ContentAlignment.TopLeft,
                Padding = new Padding(14, 4, 14, 4), BackColor = Theme.BgCard,
            };
            p.Controls.Add(_lblActivityStatus);

            // ── 上方工具列（兩列：寵物 + 顯示方式）──
            var toolbar = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Theme.BgCard };

            toolbar.Controls.Add(new Label
            {
                Text = "本期練寵：", Location = new Point(10, 14),
                AutoSize = true, ForeColor = Theme.TextSecondary,
                Font = new Font(Theme.FontFamily, 9.5f),
            });
            _cmbActivityPet = new ComboBox
            {
                Location = new Point(82, 10), Size = new Size(240, 28),
                BackColor = Theme.BgInput, ForeColor = Theme.TextPrimary,
                FlatStyle = FlatStyle.Flat, Font = new Font(Theme.FontFamily, 9.5f),
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            _cmbActivityPet.SelectedIndexChanged += async (s, e) => await LoadActivityRankAsync();
            toolbar.Controls.Add(_cmbActivityPet);

            toolbar.Controls.Add(new Label
            {
                Text = "顯示：", Location = new Point(10, 50),
                AutoSize = true, ForeColor = Theme.TextSecondary,
                Font = new Font(Theme.FontFamily, 9f),
            });
            _cmbActivityView = new ComboBox
            {
                Location = new Point(52, 46), Size = new Size(420, 28),
                BackColor = Theme.BgInput, ForeColor = Theme.TextPrimary,
                FlatStyle = FlatStyle.Flat, Font = new Font(Theme.FontFamily, 9f),
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            _cmbActivityView.Items.AddRange(new object[]
            {
                "每人最高戰力一筆（名次＝人數，管理用）",
                "全部提交列・僅依戰力 sum 排序（等同 WHERE id=本期 後 ORDER BY sum DESC）",
            });
            _cmbActivityView.SelectedIndex = 0;
            _cmbActivityView.SelectedIndexChanged += async (s, e) => await LoadActivityRankAsync();
            toolbar.Controls.Add(_cmbActivityView);

            // 右側按鈕群組
            var btnResetAct = Theme.MakeButton("清空此排行", Color.FromArgb(110,15,15), Color.FromArgb(255,90,90), 84, 30);
            var btnCsv2     = Theme.MakeButton("📥 CSV匯出", Theme.BgMid, Theme.AccentGreen, 84, 30);
            var btnRef      = Theme.MakePrimaryButton("🔄 重新載入", 90, 30);
            foreach (var b in new[] { btnRef, btnCsv2, btnResetAct }) b.Font = new Font(Theme.FontFamily, 8.5f);
            btnRef.Click      += async (s, e) =>
            {
                _capturePetTypes.Clear();
                await LoadCapturePetTypesAsync();
                await LoadActivityRankAsync();
            };
            btnCsv2.Click     += (s, e) => ExportActivityCsv();
            btnResetAct.Click += async (s, e) => await ResetActivityAsync();

            var actBtns = new[] { btnResetAct, btnCsv2, btnRef };
            toolbar.Resize += (s, e) =>
            {
                int x = toolbar.ClientSize.Width - 8;
                foreach (var b in actBtns) { x -= b.Width + 6; b.Left = x; b.Top = 11; }
            };
            toolbar.Controls.AddRange(actBtns);

            // ── 主體 SplitContainer：左=排行卡片+查詢, 右=DataGridView ──
            var split2 = new SplitContainer
            {
                Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1,
                Panel1MinSize = 220, BackColor = Theme.BgPage, SplitterWidth = 6,
            };
            bool s2Ready = false;
            split2.Layout += (s, e) =>
            {
                if (s2Ready || split2.Width < 500) return;
                s2Ready = true;
                split2.SplitterDistance = 240;
            };

            // ── 左面板 ──
            split2.Panel1.BackColor = Color.FromArgb(20, 28, 44);
            _activityRankPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, AutoScroll = true };

            // 左下：玩家查詢
            var searchPanel = new Panel
            {
                Dock = DockStyle.Bottom, Height = 120,
                BackColor = Color.FromArgb(14, 22, 36), Padding = new Padding(10, 8, 10, 8)
            };
            var lblSearchTitle = new Label
            {
                Text = "🔍 查玩家所有記錄", Location = new Point(10, 8),
                AutoSize = true, ForeColor = Theme.AccentOrange,
                Font = new Font(Theme.FontFamily, 8.5f, FontStyle.Bold),
            };
            _txtPlayerSearch = new TextBox
            {
                Location = new Point(10, 30), Size = new Size(160, 24),
                BackColor = Theme.BgInput, ForeColor = Theme.TextPrimary,
                Font = Theme.FontSmall, BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "帳號或角色名…",
            };
            var btnSearch = Theme.MakePrimaryButton("查詢", 48, 24);
            btnSearch.Location = new Point(174, 30); btnSearch.Font = Theme.FontSmall;
            btnSearch.Click += async (s, e) => await QueryPlayerEntriesAsync(_txtPlayerSearch.Text.Trim());
            _txtPlayerSearch.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) await QueryPlayerEntriesAsync(_txtPlayerSearch.Text.Trim()); };

            _dgvPlayerEntries = new DataGridView
            {
                Location = new Point(10, 60), Size = new Size(200, 52),
                BackgroundColor = Color.FromArgb(14, 22, 36),
                RowHeadersVisible = false, ColumnHeadersVisible = false,
                BorderStyle = BorderStyle.None, Font = Theme.FontSmall,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(14, 22, 36), ForeColor = Theme.TextPrimary,
                    SelectionBackColor = Theme.AccentOrange, SelectionForeColor = Color.White,
                    Padding = new Padding(4, 2, 4, 2),
                },
                ReadOnly = true, AllowUserToAddRows = false,
                ScrollBars = ScrollBars.Vertical,
            };
            _dgvPlayerEntries.Columns.Add(new DataGridViewTextBoxColumn { AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            searchPanel.Resize += (s, e) =>
            {
                int w = searchPanel.ClientSize.Width - 20;
                _txtPlayerSearch.Width = Math.Max(80, w - 56);
                btnSearch.Left = _txtPlayerSearch.Right + 4;
                _dgvPlayerEntries.Width  = w;
                _dgvPlayerEntries.Height = searchPanel.ClientSize.Height - 64;
            };
            searchPanel.Controls.AddRange(new Control[] { lblSearchTitle, _txtPlayerSearch, btnSearch, _dgvPlayerEntries });

            split2.Panel1.Controls.Add(searchPanel);
            split2.Panel1.Controls.Add(_activityRankPanel);

            // ── 右面板：DataGridView ──
            split2.Panel2.BackColor = Theme.BgPage;
            _dgvActivity = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(_dgvActivity);
            _dgvActivity.RowHeadersVisible     = false;
            _dgvActivity.ColumnHeadersHeight   = 34;
            _dgvActivity.RowTemplate.Height    = 30;
            _dgvActivity.ScrollBars            = ScrollBars.Both;
            _dgvActivity.AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.None;
            _dgvActivity.ColumnHeadersDefaultCellStyle.Font = new Font(Theme.FontFamily, 9f, FontStyle.Bold);

            // 欄位定義（移除冗餘的寵物名，每欄給足夠寬度）
            void AddCol(string name, string header, int width, bool fill = false, bool center = false)
            {
                var col = new DataGridViewTextBoxColumn
                {
                    Name = name, HeaderText = header, ReadOnly = true,
                    MinimumWidth = width,
                };
                if (fill) col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                else { col.Width = width; col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None; }
                if (center)
                    col.DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter };
                _dgvActivity.Columns.Add(col);
            }

            AddCol("a_rank",   "#",      46,  false, true);
            AddCol("a_online", "在線",   46,  false, true);
            AddCol("a_player", "角色名", 130);
            AddCol("a_cdkey",  "帳號",   150);
            AddCol("a_score",  "戰鬥力", 80,  false, true);
            AddCol("a_hp",     "HP",     72,  false, true);
            AddCol("a_atk",    "攻擊",   68,  false, true);
            AddCol("a_def",    "防禦",   68,  false, true);
            AddCol("a_spd",    "速度",   68,  false, true);
            AddCol("a_count",  "提交",   68,  false, true);
            AddCol("a_time",   "提交時間", 136);
            AddCol("a_check",  "審核",   90,  true);

            // 右鍵選單：審核 & 刪除
            _dgvActivity.MouseClick += async (s, e) =>
            {
                if (e.Button != MouseButtons.Right) return;
                var hit = _dgvActivity.HitTest(e.X, e.Y);
                if (hit.RowIndex < 0) return;
                _dgvActivity.ClearSelection();
                _dgvActivity.Rows[hit.RowIndex].Selected = true;
                var entry = _captureLeaderboard.ElementAtOrDefault(hit.RowIndex);
                if (entry == null) return;
                var ctx = new ContextMenuStrip();
                ctx.Font = Theme.FontSmall;
                ctx.Items.Add(entry.Check ? "↩ 取消審核" : "✅ 通過審核", null, async (_, __) =>
                {
                    bool ok = await DatabaseManager.Instance.SetCapturePetCheckAsync(entry.Unicode, !entry.Check);
                    if (ok)
                    {
                        entry.Check = !entry.Check;
                        RefreshActivityDgv();
                        _lblActivityStatus.ForeColor = Theme.AccentGreen;
                        _lblActivityStatus.Text = $"[OK] 已{(entry.Check ? "通過" : "取消")}審核：{entry.Author}";
                    }
                });
                ctx.Items.Add(new ToolStripSeparator());
                ctx.Items.Add("🗑 刪除此記錄", null, async (_, __) =>
                {
                    if (MessageBox.Show(
                        $"確定刪除 {entry.Author} 的記錄（分數 {entry.Sum}）？\n此操作不可還原！",
                        "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                    bool ok = await DatabaseManager.Instance.DeletePetAsync(entry.Unicode, entry.PetName);
                    if (ok)
                    {
                        _captureLeaderboard.Remove(entry);
                        RefreshActivityDgv();
                        _lblActivityStatus.ForeColor = Theme.AccentOrange;
                        _lblActivityStatus.Text = $"[OK] 已刪除 {entry.Author} 的記錄";
                    }
                });
                ctx.Show(_dgvActivity, e.Location);
            };

            // 雙擊複製角色名
            _dgvActivity.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                var entry = _captureLeaderboard.ElementAtOrDefault(e.RowIndex);
                if (entry == null) return;
                string colName = e.ColumnIndex >= 0 ? _dgvActivity.Columns[e.ColumnIndex].Name : "";
                string val = colName == "a_cdkey" ? entry.Cdkey : entry.Author;
                if (!string.IsNullOrEmpty(val)) { Clipboard.SetText(val); _lblActivityStatus.Text = $"已複製：{val}"; }
            };

            split2.Panel2.Controls.Add(_dgvActivity);

            var actTbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1,
                Margin = Padding.Empty, Padding = Padding.Empty,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
            };
            actTbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 78f));
            actTbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            actTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            toolbar.Dock = DockStyle.Fill;
            actTbl.Controls.Add(toolbar, 0, 0);
            actTbl.Controls.Add(split2,  0, 1);
            p.Controls.Add(actTbl);
        }

        private async Task LoadCapturePetTypesAsync()
        {
            if (_capturePetTypes.Count > 0) return;
            _capturePetTypes = await DatabaseManager.Instance.GetCapturePetRankTypesAsync();
            _cmbActivityPet.SelectedIndexChanged -= async (s, e) => await LoadActivityRankAsync();
            _cmbActivityPet.Items.Clear();
            foreach (var (id, name, cnt, top, last) in _capturePetTypes)
                _cmbActivityPet.Items.Add($"{name}  (最高:{top}  共{cnt}筆)");
            if (_cmbActivityPet.Items.Count > 0) _cmbActivityPet.SelectedIndex = 0;
            _cmbActivityPet.SelectedIndexChanged += async (s, e) => await LoadActivityRankAsync();
        }

        private async Task QueryPlayerEntriesAsync(string account)
        {
            if (string.IsNullOrWhiteSpace(account)) return;
            _dgvPlayerEntries.Rows.Clear();
            var entries = await DatabaseManager.Instance.GetCapturePetPlayerEntriesAsync(account);
            if (entries.Count == 0) { _dgvPlayerEntries.Rows.Add("無記錄"); return; }
            foreach (var e in entries)
                _dgvPlayerEntries.Rows.Add($"{e.PetName} 分數:{e.Sum}  {e.InsertTime}");
        }

        // ===================================================================
        //  Database Explorer Tab
        // ===================================================================
        private void BuildDbExploreTab(Panel p)
        {
            var toolbar = new Panel { Dock = DockStyle.Top, Height = 54, BackColor = Theme.BgCard, Padding = new Padding(10, 8, 10, 8) };
            toolbar.Controls.Add(new Label { Text = "\u5217\u51FA\u6240\u6709\u8CC7\u6599\u8868\uFF0C\u96D9\u64CA\u8868\u540D\u9810\u89BD\u5167\u5BB9", AutoSize = true, Location = new Point(10, 8), ForeColor = Theme.TextMuted, Font = Theme.FontSmall });

            var btnLoad = Theme.MakePrimaryButton("\u8F09\u5165\u5168\u90E8\u8868", 90, 28);
            btnLoad.Location = new Point(10, 16); btnLoad.Font = Theme.FontSmall;
            btnLoad.Click += async (s, e) => await LoadAllTablesAsync();

            var txtFilter = new TextBox { Location = new Point(108, 16), Size = new Size(180, 28), BackColor = Theme.BgInput, ForeColor = Theme.TextPrimary, Font = Theme.FontSmall, PlaceholderText = "\u641C\u5C0B\u8868\u540D" };
            txtFilter.TextChanged += (s, e) => FilterTableList(txtFilter.Text.Trim());

            var btnHasData = Theme.MakeButton("\u53EA\u986F\u793A\u6709\u8CC7\u6599", Theme.BgMid, Theme.AccentBlue, 90, 28);
            btnHasData.Location = new Point(296, 16); btnHasData.Font = Theme.FontSmall;
            btnHasData.Click += (s, e) => FilterTableList(txtFilter.Text.Trim(), onlyWithData: true);

            toolbar.Controls.AddRange(new Control[] { btnLoad, txtFilter, btnHasData });
            p.Controls.Add(toolbar);

            var split3 = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, FixedPanel = FixedPanel.Panel1, Panel1MinSize = 180, BackColor = Theme.BgPage, SplitterWidth = 5 };
            bool s3Ready = false;
            split3.Layout += (s, e) => { if (s3Ready || split3.Height < 300) return; s3Ready = true; split3.SplitterDistance = Math.Min(240, split3.Height - 200); };

            _dgvTables = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(_dgvTables);
            _dgvTables.Columns.Add(new DataGridViewTextBoxColumn { Name = "tbl",  HeaderText = "\u8868\u540D",    Width = 200, ReadOnly = true });
            _dgvTables.Columns.Add(new DataGridViewTextBoxColumn { Name = "rows", HeaderText = "\u4F30\u8A08\u7B46\u6578", Width = 90,  ReadOnly = true });
            _dgvTables.Columns.Add(new DataGridViewTextBoxColumn { Name = "cols", HeaderText = "\u6B04\u4F4D\uFF08\u524D8\uFF09", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            _dgvTables.CellDoubleClick += async (s, e) => { if (e.RowIndex < 0) return; string tbl = _dgvTables.Rows[e.RowIndex].Cells["tbl"].Value?.ToString() ?? ""; if (!string.IsNullOrEmpty(tbl)) await PreviewTableAsync(tbl); };
            split3.Panel1.Controls.Add(_dgvTables);

            var previewTop = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Theme.BgMid, Padding = new Padding(8, 8, 8, 4) };
            _lblPreview = new Label { Text = "\u96D9\u64CA\u4E0A\u65B9\u8868\u540D\u5373\u53EF\u9810\u89BD", AutoSize = true, Location = new Point(8, 10), ForeColor = Theme.TextMuted, Font = Theme.FontSmall };
            var btnExport = Theme.MakeButton("CSV\u532F\u51FA", Theme.BgMid, Theme.AccentGreen, 70, 26);
            btnExport.Font = Theme.FontSmall;
            btnExport.Click += (s, e) => ExportPreviewCsv();
            previewTop.Controls.AddRange(new Control[] { _lblPreview, btnExport });
            previewTop.Resize += (s, e) => btnExport.Location = new Point(previewTop.ClientSize.Width - btnExport.Width - 8, 6);

            _dgvPreview = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(_dgvPreview);

            split3.Panel2.Controls.Add(_dgvPreview);
            split3.Panel2.Controls.Add(previewTop);
            p.Controls.Add(split3);
        }

        // ===================================================================
        //  Data Loading
        // ===================================================================
        private async Task LoadRankAsync()
        {
            if (IsDisposed) return;
            _lblStatus.ForeColor = Theme.AccentOrange;
            _lblStatus.Text = "\u5075\u6E2C\u4E2D...";
            _dgvMain.Columns.Clear(); _dgvMain.Rows.Clear();

            var (tbl, cols, rows) = await DatabaseManager.Instance.GetGamePetRankRawAsync(2000);
            if (IsDisposed) return;

            if (tbl == null)
            {
                _lblSrc.ForeColor = Theme.AccentOrange; _lblSrc.Text = "[\u672A\u627E\u5230] \u6392\u884C\u8868";
                _lblStatus.ForeColor = Theme.TextMuted; _lblStatus.Text = "\u67E5\u7121 petbilling/petrank \u8868";
                return;
            }
            if (tbl.StartsWith("ERROR:"))
            {
                _lblSrc.ForeColor = Theme.AccentRed; _lblSrc.Text = tbl;
                _lblStatus.ForeColor = Theme.AccentRed; _lblStatus.Text = "\u67E5\u8A62\u5931\u6557";
                return;
            }

            _tableName = tbl; _allCols = cols; _allRows = rows;
            _lblSrc.ForeColor = Theme.AccentGreen;
            _lblSrc.Text = $"[OK] {tbl}  |  {cols.Count} \u6B04  |  {rows.Count} \u7B46";

            // Populate classify combo
            _cmbClassifyCol.SelectedIndexChanged -= OnClassifyColChanged;
            _cmbClassifyCol.Items.Clear();
            foreach (var c in cols) _cmbClassifyCol.Items.Add(new ColItem(c));
            string bestClassify = FindBestClassifyCol(cols, rows);
            _cmbClassifyCol.SelectedItem = _cmbClassifyCol.Items.Cast<ColItem>().FirstOrDefault(i => i.Col == bestClassify);
            _cmbClassifyCol.SelectedIndexChanged += OnClassifyColChanged;

            // Populate sort combo
            _cmbSortCol.SelectedIndexChanged -= (s, e) => RefreshMainDgv();
            _cmbSortCol.Items.Clear();
            _cmbSortCol.Items.Add("\uFF08\u4E0D\u6392\u5E8F\uFF09");
            foreach (var c in cols) _cmbSortCol.Items.Add(new ColItem(c));
            string bestSort = FindBestSortCol(cols, rows);
            _cmbSortCol.SelectedItem = _cmbSortCol.Items.Cast<object>().FirstOrDefault(i => i is ColItem ci && ci.Col == bestSort) ?? "\uFF08\u4E0D\u6392\u5E8F\uFF09";
            _cmbSortCol.SelectedIndexChanged += (s, e) => RefreshMainDgv();

            // Build columns: # rank first, then priority order - online, player name, pet name, then rest
            _dgvMain.SuspendLayout();
            _dgvMain.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            _dgvMain.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name       = "c__rank",
                HeaderText = "#",
                ReadOnly   = true,
                Width      = 40,
                MinimumWidth = 36,
                Resizable  = DataGridViewTriState.False,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Font      = Theme.FontCell9Bold,
                },
            });
            var priority = new[] { "_online", "_playerName", "name", "petname" };
            var ordered  = priority.Where(p => cols.Contains(p, StringComparer.OrdinalIgnoreCase))
                                    .Concat(cols.Where(c => !priority.Contains(c, StringComparer.OrdinalIgnoreCase)))
                                    .ToList();
            for (int i = 0; i < ordered.Count; i++)
            {
                string c    = ordered[i];
                bool isOnline = c.Equals("_online",     StringComparison.OrdinalIgnoreCase);
                bool isPlayer = c.Equals("_playerName", StringComparison.OrdinalIgnoreCase);
                bool isName   = c.Equals("name",        StringComparison.OrdinalIgnoreCase) || c.Equals("petname", StringComparison.OrdinalIgnoreCase);
                bool isLast   = i == ordered.Count - 1;
                _dgvMain.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name        = "c_" + c,
                    HeaderText  = ColZh(c),
                    ReadOnly    = true,
                    MinimumWidth = 50,
                    Width       = isOnline ? 54 : isPlayer ? 140 : isName ? 130 : 110,
                    AutoSizeMode = isLast ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None,
                });
            }
            _dgvMain.ResumeLayout();

            OnClassifyColChanged(null, EventArgs.Empty);
            _ = LoadCapturePetTypesAsync();

            _lblStatus.ForeColor = Theme.TextMuted;
            _lblStatus.Text = rows.Count > 0 ? $"\u5171 {rows.Count} \u7B46" : $"[OK] {tbl} \u8868\u5B58\u5728\u4F46\u7121\u8CC7\u6599";
        }


        private bool ActivityViewIsRaw() =>
            _cmbActivityView != null && _cmbActivityView.SelectedIndex == 1;

        private async Task LoadActivityRankAsync()
        {
            if (IsDisposed) return;
            if (_capturePetTypes.Count == 0) { await LoadCapturePetTypesAsync(); return; }
            int idx = _cmbActivityPet.SelectedIndex;
            if (idx < 0 || idx >= _capturePetTypes.Count) return;

            var (petId, petName, _, _, _) = _capturePetTypes[idx];
            bool raw = ActivityViewIsRaw();
            _lblActivityStatus.ForeColor = Theme.AccentOrange;
            _lblActivityStatus.Text = $"載入中…【{petName}】";

            _captureLeaderboard = raw
                ? await DatabaseManager.Instance.GetCapturePetLeaderboardRawAsync(petId, 500)
                : await DatabaseManager.Instance.GetCapturePetLeaderboardAsync(petId, 100);
            if (IsDisposed) return;

            if (_dgvActivity.Columns.Contains("a_rank"))
                _dgvActivity.Columns["a_rank"].HeaderText = raw ? "列#" : "#";

            RefreshActivityDgv();
            BuildActivityRankCards(_activityRankPanel, _captureLeaderboard, petName, raw);
            _lblActivityStatus.ForeColor = Theme.TextMuted;
            if (raw)
            {
                _lblActivityStatus.Text =
                    $"【{petName}】共 {_captureLeaderboard.Count} 筆（每筆提交一列，僅 ORDER BY sum；時間顯示至秒）\r\n" +
                    "※ 與技術匯出/排序同一邏輯；若仍不同請確認兩邊是否連同一資料庫、且技術是否只篩本期 id。";
            }
            else
            {
                _lblActivityStatus.Text =
                    $"【{petName}】共 {_captureLeaderboard.Count} 人（每人一列：最高戰力；同分取較晚提交）\r\n" +
                    "※ 數值為提交當下快照；若要對齊技術全表排序，請改選上方「全部提交列」。";
            }
        }

        private void RefreshActivityDgv()
        {
            _dgvActivity.SuspendLayout(); _dgvActivity.Rows.Clear();
            foreach (var e in _captureLeaderboard)
            {
                int i = _dgvActivity.Rows.Add();
                _dgvActivity.Rows[i].Cells["a_rank"].Value   = e.Rank == 1 ? "🥇" : e.Rank == 2 ? "🥈" : e.Rank == 3 ? "🥉" : $"#{e.Rank}";
                _dgvActivity.Rows[i].Cells["a_online"].Value = e.IsOnline ? "🟢" : "—";
                _dgvActivity.Rows[i].Cells["a_player"].Value = e.Author;
                _dgvActivity.Rows[i].Cells["a_cdkey"].Value  = e.Cdkey;
                _dgvActivity.Rows[i].Cells["a_score"].Value  = e.Sum;
                _dgvActivity.Rows[i].Cells["a_hp"].Value     = e.Hp;
                _dgvActivity.Rows[i].Cells["a_atk"].Value    = e.Attack;
                _dgvActivity.Rows[i].Cells["a_def"].Value    = e.Def;
                _dgvActivity.Rows[i].Cells["a_spd"].Value    = e.Quick;
                _dgvActivity.Rows[i].Cells["a_time"].Value   = e.InsertTime;
                _dgvActivity.Rows[i].Cells["a_check"].Value  = e.Check ? "✅ 已審核" : "⏳ 待審";

                if (e.EntryCount > 1)
                {
                    _dgvActivity.Rows[i].Cells["a_count"].Value = $"⚠ {e.EntryCount}";
                    _dgvActivity.Rows[i].Cells["a_count"].Style.ForeColor = Color.FromArgb(255, 193, 7);
                    _dgvActivity.Rows[i].Cells["a_count"].Style.Font = Theme.FontCell9Bold;
                }
                else
                {
                    _dgvActivity.Rows[i].Cells["a_count"].Value = "1";
                }

                // 名次背景高亮
                var st = _dgvActivity.Rows[i].DefaultCellStyle;
                if      (e.Rank == 1) { st.BackColor = Color.FromArgb(62,52,8);   st.ForeColor = Color.FromArgb(255,210,50);  st.Font = Theme.FontCell9Bold; }
                else if (e.Rank == 2) { st.BackColor = Color.FromArgb(36,44,56);  st.ForeColor = Color.FromArgb(200,215,230); st.Font = Theme.FontCell9Bold; }
                else if (e.Rank == 3) { st.BackColor = Color.FromArgb(52,34,8);   st.ForeColor = Color.FromArgb(215,148,80);  st.Font = Theme.FontCell9Bold; }
                else if (e.Rank <= 10){ st.BackColor = Color.FromArgb(26,34,50);  st.ForeColor = Theme.TextPrimary; }

                // 戰鬥力欄位加粗
                _dgvActivity.Rows[i].Cells["a_score"].Style.Font      = Theme.FontCell9Bold;
                _dgvActivity.Rows[i].Cells["a_score"].Style.ForeColor = e.Rank <= 3 ? Color.FromArgb(255,210,50) : Theme.AccentOrange;
            }
            _dgvActivity.ResumeLayout();
        }

        private void BuildActivityRankCards(Panel panel, List<CaptureRankEntry> rows, string petName, bool rawRows)
        {
            panel.SuspendLayout(); panel.Controls.Clear();

            // 標題
            panel.Controls.Add(new Label
            {
                Text = rawRows ? "練寵（全表排序）" : "練寵排行榜",
                Dock = DockStyle.None,
                Font = new Font(Theme.FontFamily, 12, FontStyle.Bold),
                ForeColor = Theme.AccentOrange, AutoSize = true, Location = new Point(10, 10),
            });
            panel.Controls.Add(new Label
            {
                Text = rawRows
                    ? "本期：" + petName + "　·　前 20 筆（與 DB 依戰力順序相同）"
                    : "本期：" + petName,
                Dock = DockStyle.None,
                Font = Theme.FontSmall, ForeColor = Theme.TextMuted,
                AutoSize = true, Location = new Point(10, 38),
            });

            // 表頭
            var hdr = new Panel { Location = new Point(0, 62), Height = 26, BackColor = Theme.BgMid };
            panel.Controls.Add(hdr);
            hdr.Controls.Add(new Label { Text = "#",    Location = new Point(8,5),  AutoSize = true, ForeColor = Theme.AccentOrange, Font = Theme.FontSmall });
            hdr.Controls.Add(new Label { Text = "玩家", Location = new Point(38,5), AutoSize = true, ForeColor = Theme.AccentOrange, Font = Theme.FontSmall });
            hdr.Controls.Add(new Label { Text = "戰鬥力", Location = new Point(165,5), AutoSize = true, ForeColor = Theme.AccentOrange, Font = Theme.FontSmall });

            // 讓 hdr 和資料行在 Resize 時自動填滿寬度
            panel.Resize += (s, e2) =>
            {
                int w = panel.ClientSize.Width;
                hdr.Width = w;
                foreach (Control c in panel.Controls)
                    if (c is Panel rp && rp != hdr) rp.Width = w;
            };

            int top = 90;
            for (int i = 0; i < Math.Min(rows.Count, 20); i++)
            {
                var e = rows[i];
                int r = e.Rank;
                bool isPodium = r <= 3;
                Color rankColor = r == 1 ? Color.FromArgb(255,210,50)
                               : r == 2 ? Color.FromArgb(210,210,220)
                               : r == 3 ? Color.FromArgb(210,150,80)
                               : Theme.TextMuted;
                Color bgColor = isPodium ? Color.FromArgb(38,48,68) : Color.Transparent;
                string badge = e.EntryCount > 1 ? $" △{e.EntryCount}" : "";
                string rankText = r == 1 ? "🥇" : r == 2 ? "🥈" : r == 3 ? "🥉" : $"{r,2}";

                var rp = new Panel
                {
                    Location = new Point(0, top), Height = 28,
                    BackColor = bgColor,
                };

                var lblRank = new Label
                {
                    Text = rankText, Location = new Point(6, 5), AutoSize = true,
                    ForeColor = rankColor,
                    Font = isPodium ? Theme.FontCell9Bold : Theme.FontSmall,
                };
                var lblName = new Label
                {
                    Text = e.Author + badge, Location = new Point(36, 5),
                    Size = new Size(126, 20),
                    ForeColor = isPodium ? Color.White : Theme.TextPrimary,
                    Font = isPodium ? Theme.FontCell9Bold : Theme.FontSmall,
                    AutoEllipsis = true,
                };
                var lblScore = new Label
                {
                    Text = e.Sum.ToString(), Location = new Point(164, 5), AutoSize = true,
                    ForeColor = isPodium ? Theme.AccentOrange : Theme.TextSecondary,
                    Font = isPodium ? Theme.FontCell9Bold : Theme.FontSmall,
                };
                if (e.EntryCount > 1)
                    lblName.ForeColor = Color.FromArgb(255, 193, 7);

                rp.Controls.AddRange(new Control[] { lblRank, lblName, lblScore });
                panel.Controls.Add(rp);
                top += 28;
            }

            // 觸發一次 Resize 設定初始寬度
            int pw = panel.ClientSize.Width > 0 ? panel.ClientSize.Width : 240;
            hdr.Width = pw;
            foreach (Control c in panel.Controls)
                if (c is Panel rp2 && rp2 != hdr) rp2.Width = pw;

            panel.ResumeLayout();
        }

        private void ExportActivityCsv()
        {
            if (_captureLeaderboard.Count == 0) { MessageBox.Show("無資料"); return; }
            int idx = _cmbActivityPet.SelectedIndex;
            string petName = idx >= 0 && idx < _capturePetTypes.Count ? _capturePetTypes[idx].name : "act";
            using var dlg = new SaveFileDialog { Filter = "CSV|*.csv", FileName = $"練寵_{petName}_{DateTime.Now:yyyyMMdd_HHmm}.csv" };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            var sb = new StringBuilder();
            sb.AppendLine((ActivityViewIsRaw() ? "列號" : "名次") + ",角色名,帳號,寵物名,戰鬥力,HP,攻擊,防禦,速度,提交次數,提交時間,審核");
            foreach (var e in _captureLeaderboard)
                sb.AppendLine($"{e.Rank},{Esc(e.Author)},{Esc(e.Cdkey)},{Esc(e.PetName)},{e.Sum},{e.Hp},{e.Attack},{e.Def},{e.Quick},{e.EntryCount},{Esc(e.InsertTime)},{(e.Check?"已審核":"待審")}");
            File.WriteAllText(dlg.FileName, sb.ToString(), new System.Text.UTF8Encoding(true));
            MessageBox.Show($"[OK] 已匯出 {_captureLeaderboard.Count} 筆\n{dlg.FileName}");
        }

        private async Task ResetActivityAsync()
        {
            int idx = _cmbActivityPet.SelectedIndex;
            if (idx < 0 || idx >= _capturePetTypes.Count) { MessageBox.Show("請先選擇本期練寵"); return; }
            var (petId, petName, _, _, _) = _capturePetTypes[idx];
            if (MessageBox.Show($"確定清空【{petName}】的全部練寵排行？\n此操作不可還原！",
                "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            _lblActivityStatus.ForeColor = Theme.AccentOrange; _lblActivityStatus.Text = "清空中...";
            try
            {
                using var conn = DatabaseManager.Instance.GetConnection();
                await conn.OpenAsync();
                using var cmd = new MySqlConnector.MySqlCommand("DELETE FROM capturepet WHERE id=@pid", conn);
                cmd.Parameters.AddWithValue("@pid", petId);
                int n = await cmd.ExecuteNonQueryAsync();
                _capturePetTypes.Clear();
                _lblActivityStatus.ForeColor = Theme.AccentGreen;
                _lblActivityStatus.Text = $"[OK] 已清空 {n} 筆【{petName}】";
                await LoadCapturePetTypesAsync();
                await LoadActivityRankAsync();
            }
            catch (Exception ex) { _lblActivityStatus.ForeColor = Theme.AccentRed; _lblActivityStatus.Text = "[ERR] " + ex.Message; }
        }

        // ===================================================================
        //  Database Explorer Logic
        // ===================================================================
        private async Task LoadAllTablesAsync()
        {
            if (IsDisposed) return;
            _lblPreview.Text = "\u8F09\u5165\u4E2D...";
            _dgvTables.Rows.Clear();
            _allTablesCache = await DatabaseManager.Instance.GetAllTablesInfoAsync();
            if (IsDisposed) return;
            FillTableDgv(_allTablesCache);
            _lblPreview.Text = $"\u5171 {_allTablesCache.Count} \u500B\u8868\uFF0C\u96D9\u64CA\u8868\u540D\u9810\u89BD";
        }

        private void FilterTableList(string keyword, bool onlyWithData = false)
        {
            var filtered = _allTablesCache.Where(t => (string.IsNullOrEmpty(keyword) || t.table.Contains(keyword, StringComparison.OrdinalIgnoreCase)) && (!onlyWithData || t.rows > 0)).ToList();
            FillTableDgv(filtered);
            _lblPreview.Text = $"\u986F\u793A {filtered.Count} / {_allTablesCache.Count} \u500B\u8868";
        }

        private void FillTableDgv(List<(string table, long rows, string columns)> tables)
        {
            _dgvTables.SuspendLayout(); _dgvTables.Rows.Clear();
            foreach (var (tbl, rowCnt, colStr) in tables)
            {
                int idx = _dgvTables.Rows.Add();
                _dgvTables.Rows[idx].Cells["tbl"].Value  = tbl;
                _dgvTables.Rows[idx].Cells["rows"].Value = rowCnt;
                _dgvTables.Rows[idx].Cells["cols"].Value = colStr;
                if (rowCnt > 0) _dgvTables.Rows[idx].DefaultCellStyle.ForeColor = Theme.AccentGreen;
            }
            _dgvTables.ResumeLayout();
        }

        private async Task PreviewTableAsync(string tableName)
        {
            if (IsDisposed) return;
            _lblPreview.Text = $"\u8F09\u5165 {tableName}...";
            _dgvPreview.Columns.Clear(); _dgvPreview.Rows.Clear();
            var (cols, rows) = await DatabaseManager.Instance.PreviewTableAsync(tableName, 50);
            if (IsDisposed) return;
            _previewTableName = tableName; _previewCols = cols; _previewRows = rows;
            _dgvPreview.SuspendLayout();
            foreach (var col in cols) _dgvPreview.Columns.Add(new DataGridViewTextBoxColumn { Name = "c_" + col, HeaderText = col, Width = 100, ReadOnly = true });
            foreach (var row in rows) { int idx = _dgvPreview.Rows.Add(); foreach (var col in cols) _dgvPreview.Rows[idx].Cells["c_" + col].Value = row.ContainsKey(col) ? row[col] : ""; }
            _dgvPreview.ResumeLayout();
            _lblPreview.Text = $"[{tableName}]  {rows.Count} \u7B46  |  \u6B04\u4F4D\uFF1A{string.Join(", ", cols)}";
        }

        private void ExportPreviewCsv()
        {
            if (_previewRows.Count == 0) { MessageBox.Show("\u8ACB\u5148\u96D9\u64CA\u9810\u89BD\u4E00\u500B\u8868"); return; }
            using var dlg = new SaveFileDialog { Filter = "CSV|*.csv", FileName = $"{_previewTableName}_{DateTime.Now:yyyyMMdd_HHmm}.csv" };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", _previewCols.Select(Esc)));
            foreach (var row in _previewRows) sb.AppendLine(string.Join(",", _previewCols.Select(c => Esc(row.ContainsKey(c) ? row[c] : ""))));
            File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
            MessageBox.Show($"[OK] {_previewRows.Count} \u7B46\n{dlg.FileName}");
        }

        // ===================================================================
        //  Classify / Filter / Sort Logic
        // ===================================================================
        private static readonly HashSet<string> _skipClassify = new(StringComparer.OrdinalIgnoreCase) { "_online","_playerName","imageId","image_id","petId","pet_id","id","createtime","updatetime","time","serverid","server" };
        private static readonly HashSet<string> _skipSort     = new(StringComparer.OrdinalIgnoreCase) { "_online","_playerName","imageId","image_id","petId","pet_id","id","type","createtime","updatetime","time","serverid","server","lv","level" };
        private static readonly string[] _preferClassify = { "name","petname","pet_name","cdkey","account" };
        private static readonly string[] _preferSort     = { "sum","power","combat","hp","maxhp","attack","atk","def","defense","quick","spd","speed" };

        private static string FindBestClassifyCol(List<string> cols, List<Dictionary<string,string>> rows)
        {
            foreach (var p in _preferClassify) { var f = cols.FirstOrDefault(c => c.Equals(p, StringComparison.OrdinalIgnoreCase)); if (f != null) return f; }
            if (rows.Count == 0) return cols.FirstOrDefault();
            return cols.Where(c => !_skipClassify.Contains(c)).Select(c => new { col = c, cnt = rows.Select(r => r.ContainsKey(c) ? r[c] : "").Distinct().Count() }).Where(x => x.cnt >= 2 && x.cnt <= 200).OrderBy(x => x.cnt).FirstOrDefault()?.col ?? cols.FirstOrDefault();
        }
        private static string FindBestSortCol(List<string> cols, List<Dictionary<string,string>> rows)
        {
            foreach (var p in _preferSort) { var f = cols.FirstOrDefault(c => c.Equals(p, StringComparison.OrdinalIgnoreCase)); if (f != null) return f; }
            if (rows.Count == 0) return null;
            var sample = rows.Take(20).ToList();
            return cols.Where(c => !_skipSort.Contains(c)).FirstOrDefault(c => sample.Any(r => r.ContainsKey(c) && !string.IsNullOrEmpty(r[c])) && sample.Where(r => r.ContainsKey(c) && !string.IsNullOrEmpty(r[c])).All(r => double.TryParse(r[c], out _)));
        }

        private void OnClassifyColChanged(object sender, EventArgs e)
        {
            string col = SelCol(_cmbClassifyCol);
            _lstValues.SelectedIndexChanged -= (s2, e2) => RefreshMainDgv();
            _lstValues.Items.Clear();
            _lstValues.Items.Add("\uFF08\u5168\u90E8\uFF09");
            if (!string.IsNullOrEmpty(col))
                foreach (var v in _allRows.Select(r => r.ContainsKey(col) ? r[col] : "").Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().OrderBy(v => v))
                    _lstValues.Items.Add(v);
            _lstValues.SelectedIndex = 0;
            _lstValues.SelectedIndexChanged += (s2, e2) => RefreshMainDgv();
            RefreshMainDgv();
        }

        private void FilterValueList(string keyword)
        {
            string col = SelCol(_cmbClassifyCol);
            _lstValues.SelectedIndexChanged -= (s2, e2) => RefreshMainDgv();
            _lstValues.Items.Clear();
            _lstValues.Items.Add("\uFF08\u5168\u90E8\uFF09");
            if (!string.IsNullOrEmpty(col))
                foreach (var v in _allRows.Select(r => r.ContainsKey(col) ? r[col] : "").Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().OrderBy(v => v))
                    if (string.IsNullOrEmpty(keyword) || v.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        _lstValues.Items.Add(v);
            _lstValues.SelectedIndex = _lstValues.Items.Count > 0 ? 0 : -1;
            _lstValues.SelectedIndexChanged += (s2, e2) => RefreshMainDgv();
            RefreshMainDgv();
        }

        private void RefreshMainDgv()
        {
            if (_dgvMain.Columns.Count == 0) return;
            string classifyCol = SelCol(_cmbClassifyCol);
            string selectedVal = _lstValues.SelectedItem?.ToString() ?? "\uFF08\u5168\u90E8\uFF09";
            string sortCol     = SelCol(_cmbSortCol);
            if (sortCol == "\uFF08\u4E0D\u6392\u5E8F\uFF09") sortCol = "";
            string keyword     = _txtKeyword?.Text.Trim() ?? "";

            IEnumerable<Dictionary<string,string>> filtered = _allRows;
            if (selectedVal != "\uFF08\u5168\u90E8\uFF09" && !string.IsNullOrEmpty(classifyCol))
                filtered = filtered.Where(r => r.ContainsKey(classifyCol) && r[classifyCol] == selectedVal);
            // Keyword search: matches any column value (case-insensitive)
            if (!string.IsNullOrEmpty(keyword))
                filtered = filtered.Where(r => r.Values.Any(v => v != null && v.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
            if (!string.IsNullOrEmpty(sortCol))
                filtered = filtered.OrderByDescending(r => r.ContainsKey(sortCol) && double.TryParse(r[sortCol], out double d) ? d : double.MinValue);

            // Apply Top-N limit
            int topN = GetTopN();
            var rows = (topN > 0 ? filtered.Take(topN) : filtered).ToList();
            int totalFiltered = filtered is ICollection<Dictionary<string,string>> col2 ? col2.Count : filtered.Count();

            _dgvMain.SuspendLayout(); _dgvMain.Rows.Clear();
            int rank = 1;
            foreach (var row in rows)
            {
                int idx = _dgvMain.Rows.Add();
                if (_dgvMain.Columns.Contains("c__rank"))
                    _dgvMain.Rows[idx].Cells["c__rank"].Value = rank;
                foreach (var c in _allCols)
                    if (_dgvMain.Columns.Contains("c_" + c))
                        _dgvMain.Rows[idx].Cells["c_" + c].Value = row.ContainsKey(c) ? row[c] : "";

                // Gold / Silver / Bronze + Top-10 subtle highlight
                var st = _dgvMain.Rows[idx].DefaultCellStyle;
                if (rank == 1)
                {
                    st.BackColor = Color.FromArgb(62, 52, 8);
                    st.ForeColor = Color.FromArgb(255, 210, 50);
                    st.Font      = Theme.FontCell9Bold;
                }
                else if (rank == 2)
                {
                    st.BackColor = Color.FromArgb(36, 44, 56);
                    st.ForeColor = Color.FromArgb(200, 215, 230);
                    st.Font      = Theme.FontCell9Bold;
                }
                else if (rank == 3)
                {
                    st.BackColor = Color.FromArgb(52, 34, 8);
                    st.ForeColor = Color.FromArgb(215, 148, 80);
                    st.Font      = Theme.FontCell9Bold;
                }
                else if (rank <= 10)
                {
                    st.BackColor = Color.FromArgb(28, 36, 50);
                    st.ForeColor = Theme.TextPrimary;
                }
                rank++;
            }
            _dgvMain.ResumeLayout();
            _lblStatus.ForeColor = Theme.TextMuted;
            string kwSuffix   = !string.IsNullOrEmpty(keyword) ? $"  \u30FB\u95DC\u9375\u5B57\u300C{keyword}\u300D" : "";
            string topNSuffix = topN > 0 && totalFiltered > topN ? $"  \u30FB\u986F\u793A\u524D {topN} \uFF0F {totalFiltered} \u7B46" : $"  \u30FB\u5171 {rows.Count} \u7B46";
            _lblStatus.Text = selectedVal == "\uFF08\u5168\u90E8\uFF09"
                ? $"\u5168\u90E8{topNSuffix}{kwSuffix}"
                : $"\u3010{selectedVal}\u3011{topNSuffix}{kwSuffix}";
        }

        private int GetTopN()
        {
            if (_cmbTopN == null) return 0;
            return _cmbTopN.SelectedIndex switch { 1 => 10, 2 => 25, 3 => 50, 4 => 100, _ => 0 };
        }

        private List<Dictionary<string,string>> GetVisibleRows()
        {
            string classifyCol = SelCol(_cmbClassifyCol);
            string selectedVal = _lstValues.SelectedItem?.ToString() ?? "\uFF08\u5168\u90E8\uFF09";
            if (selectedVal == "\uFF08\u5168\u90E8\uFF09" || string.IsNullOrEmpty(classifyCol)) return _allRows;
            return _allRows.Where(r => r.ContainsKey(classifyCol) && r[classifyCol] == selectedVal).ToList();
        }

        private async Task ResetRecordsAsync(bool allRecords = false)
        {
            string classifyCol = SelCol(_cmbClassifyCol);
            string selectedVal = _lstValues.SelectedItem?.ToString() ?? "\uFF08\u5168\u90E8\uFF09";
            bool isFiltered    = !allRecords && selectedVal != "\uFF08\u5168\u90E8\uFF09" && !string.IsNullOrEmpty(classifyCol);
            string scope  = isFiltered ? $"\u3010{selectedVal}\u3011 \u7684\u6392\u884C" : $"\u5168\u90E8\u6392\u884C\uFF08{_allRows.Count} \u7B46\uFF09";
            if (MessageBox.Show($"\u78BA\u5B9A\u6E05\u9664 {scope}\uFF1F\n\u6B64\u64CD\u4F5C\u4E0D\u53EF\u9006\uFF01",
                "\u78BA\u8A8D", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            _lblStatus.ForeColor = Theme.AccentOrange; _lblStatus.Text = "\u91CD\u7F6E\u4E2D...";
            try
            {
                int deleted = await DatabaseManager.Instance.ResetPetBillingAsync(_allCols, isFiltered ? classifyCol : "", isFiltered ? selectedVal : "");
                _lblStatus.ForeColor = Theme.AccentGreen;
                _lblStatus.Text = $"[OK] \u5DF2\u6E05\u9664 {deleted} \u7B46\uFF0C\u73A9\u5BB6\u53EF\u91CD\u65B0\u6392\u540D";
                await LoadRankAsync();
            }
            catch (Exception ex) { _lblStatus.ForeColor = Theme.AccentRed; _lblStatus.Text = "[ERR] " + ex.Message; }
        }

        // ===================================================================
        //  Export helpers
        // ===================================================================
        private void ExportRawCsv(List<string> cols, List<Dictionary<string,string>> rows, string prefix)
        {
            if (rows.Count == 0) { MessageBox.Show("\u7121\u8CC7\u6599"); return; }
            if (prefix == null)
            {
                var sb = new StringBuilder();
                sb.AppendLine(string.Join("\t", cols));
                foreach (var r in rows) sb.AppendLine(string.Join("\t", cols.Select(c => r.ContainsKey(c) ? r[c] : "")));
                Clipboard.SetText(sb.ToString());
                MessageBox.Show($"[OK] \u5DF2\u8907\u88FD {rows.Count} \u7B46\uFF08\u53EF\u8CBC\u5165 Excel\uFF09");
                return;
            }
            using var dlg = new SaveFileDialog { Filter = "CSV|*.csv", FileName = $"{prefix}_{DateTime.Now:yyyyMMdd_HHmm}.csv" };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            var sb2 = new StringBuilder();
            sb2.AppendLine(string.Join(",", cols.Select(Esc)));
            foreach (var r in rows) sb2.AppendLine(string.Join(",", cols.Select(c => Esc(r.ContainsKey(c) ? r[c] : ""))));
            File.WriteAllText(dlg.FileName, sb2.ToString(), new UTF8Encoding(true));
            MessageBox.Show($"[OK] \u5DF2\u532F\u51FA {rows.Count} \u7B46\n{dlg.FileName}");
        }

        private void CopySelectionToClipboard(DataGridView dgv)
        {
            if (dgv.SelectedRows.Count == 0 && dgv.SelectedCells.Count == 0) return;
            var sb = new StringBuilder();
            var cols = Enumerable.Range(0, dgv.Columns.Count).Select(i => dgv.Columns[i].HeaderText).ToList();
            sb.AppendLine(string.Join("\t", cols));
            var rows = dgv.SelectedRows.Count > 0
                ? dgv.SelectedRows.Cast<DataGridViewRow>().OrderBy(r => r.Index).ToList()
                : dgv.SelectedCells.Cast<DataGridViewCell>().Select(c => c.OwningRow).Distinct().OrderBy(r => r.Index).ToList();
            foreach (var row in rows)
                sb.AppendLine(string.Join("\t", Enumerable.Range(0, dgv.Columns.Count).Select(i => row.Cells[i].Value?.ToString() ?? "")));
            Clipboard.SetText(sb.ToString());
            MessageBox.Show($"[OK] \u5DF2\u8907\u88FD {rows.Count} \u7B46\uFF08\u53EF\u8CBC\u5165 Excel\uFF09");
        }

        private void ExportSelectedToClipboard(DataGridView dgv)
        {
            if (dgv.SelectedRows.Count == 0) { MessageBox.Show("\u8ACB\u5148\u9078\u53D6\u8981\u532F\u51FA\u7684\u5217"); return; }
            var sb = new StringBuilder();
            var cols = Enumerable.Range(0, dgv.Columns.Count).Select(i => dgv.Columns[i].HeaderText).ToList();
            sb.AppendLine(string.Join(",", cols.Select(Esc)));
            foreach (var row in dgv.SelectedRows.Cast<DataGridViewRow>().OrderBy(r => r.Index))
                sb.AppendLine(string.Join(",", Enumerable.Range(0, dgv.Columns.Count).Select(i => Esc(row.Cells[i].Value?.ToString() ?? ""))));
            Clipboard.SetText(sb.ToString());
            MessageBox.Show($"[OK] \u5DF2\u8907\u88FD {dgv.SelectedRows.Count} \u7B46 CSV\uFF08\u53EF\u8CBC\u5165 Excel\uFF09");
        }

        private static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return (s.Contains(',') || s.Contains('"') || s.Contains('\n')) ? $"\"{s.Replace("\"","\"\"")}\"" : s;
        }
    }
}
