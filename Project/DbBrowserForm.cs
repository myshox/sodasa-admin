using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MySqlConnector;

namespace SQ_Email_Tools
{
    /// <summary>資料庫瀏覽器 — 點選表名即可查看資料，支援搜尋、翻頁</summary>
    public class DbBrowserForm : UserControl
    {
        // ── 控件 ─────────────────────────────────────────────
        private ListBox      _lstTables;
        private Label        _lblTableCount = new Label();
        private TextBox      _txtTableSearch;
        private DataGridView _dgvData;
        private Label        _lblStatus;
        private Label        _lblTableName;
        private TextBox      _txtSearch;
        private Button       _btnSearch;
        private Button       _btnPrev;
        private Button       _btnNext;
        private Label        _lblPage;
        private ComboBox     _cmbPageSize;
        private Button       _btnRefresh;

        // ── 狀態 ─────────────────────────────────────────────
        private List<string> _allTables    = new();
        private string       _currentTable = "";
        private string       _searchText   = "";
        private int          _page         = 1;
        private int          _pageSize     = 50;
        private int          _totalRows    = 0;

        public DbBrowserForm()
        {
            Theme.ApplyHubForm(this);
            BuildUI();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _ = LoadTablesAsync();
        }

        public void TriggerLoad() => _ = LoadTablesAsync();

        // ══════════════════════════════════════════════════════
        // UI 建置
        // ══════════════════════════════════════════════════════
        private void BuildUI()
        {
            Dock      = DockStyle.Fill;
            BackColor = Theme.BgPage;
            Font      = Theme.FontBody;

            var root = new SplitContainer
            {
                Dock          = DockStyle.Fill,
                Orientation   = Orientation.Vertical,
                SplitterWidth = 4,
                BackColor     = Theme.BgPage
            };
            root.SplitterDistance = 220;
            Controls.Add(root);

            // ── 左側：表列表 ─────────────────────────────────
            var leftPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
                BackColor = Color.Transparent, Padding = new Padding(0)
            };
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Panel1.Controls.Add(leftPanel);

            var lblLeft = new Label
            {
                Text = "📋 資料表", Dock = DockStyle.Fill,
                Font = Theme.FontCell9Bold, ForeColor = Theme.TextSecondary,
                BackColor = Theme.BgCard, TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0)
            };
            leftPanel.Controls.Add(lblLeft, 0, 0);

            _txtTableSearch = new TextBox
            {
                Dock = DockStyle.Fill, BackColor = Theme.BgInput,
                ForeColor = Theme.TextPrimary, BorderStyle = BorderStyle.None,
                Font = Theme.FontCell9
            };
            _txtTableSearch.TextChanged += (s, e) => FilterTables();
            var searchWrap = new Panel
            {
                Dock = DockStyle.Fill, BackColor = Theme.BgInput,
                Padding = new Padding(6, 5, 6, 4)
            };
            searchWrap.Controls.Add(_txtTableSearch);
            leftPanel.Controls.Add(searchWrap, 0, 1);

            _lstTables = new ListBox
            {
                Dock = DockStyle.Fill, BackColor = Theme.BgCard,
                ForeColor = Theme.TextPrimary, Font = Theme.FontCell9,
                BorderStyle = BorderStyle.None, IntegralHeight = false
            };
            _lstTables.SelectedIndexChanged += LstTables_SelectedIndexChanged;
            leftPanel.Controls.Add(_lstTables, 0, 2);

            // ── 右側：資料檢視 ────────────────────────────────
            var rightPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4,
                BackColor = Color.Transparent
            };
            rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            root.Panel2.Controls.Add(rightPanel);

            // 標題列
            _lblTableName = new Label
            {
                Text = "← 請選擇左側資料表", Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Theme.TextPrimary, BackColor = Theme.BgCard,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            };
            rightPanel.Controls.Add(_lblTableName, 0, 0);

            // 搜尋列
            var searchBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, BackColor = Color.Transparent,
                FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
                Padding = new Padding(4, 4, 4, 4)
            };
            rightPanel.Controls.Add(searchBar, 0, 1);

            searchBar.Controls.Add(new Label
            {
                Text = "搜尋：", AutoSize = true,
                ForeColor = Theme.TextSecondary, Margin = new Padding(4, 6, 0, 0)
            });

            _txtSearch = new TextBox
            {
                Width = 220, Height = 28, BackColor = Theme.BgInput,
                ForeColor = Theme.TextPrimary, BorderStyle = BorderStyle.FixedSingle,
                Font = Theme.FontCell9
            };
            _txtSearch.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { _page = 1; _ = LoadDataAsync(); }
            };
            searchBar.Controls.Add(_txtSearch);

            _btnSearch = MakeBtn("查詢", Theme.AccentBlue);
            _btnSearch.Click += (s, e) => { _page = 1; _ = LoadDataAsync(); };
            searchBar.Controls.Add(_btnSearch);

            _btnRefresh = MakeBtn("↻ 清除", Color.FromArgb(60, 65, 80));
            _btnRefresh.Width = 65;
            _btnRefresh.Margin = new Padding(8, 0, 0, 0);
            _btnRefresh.Click += (s, e) => { _page = 1; _txtSearch.Text = ""; _ = LoadDataAsync(); };
            searchBar.Controls.Add(_btnRefresh);

            searchBar.Controls.Add(new Label
            {
                Text = "每頁：", AutoSize = true,
                ForeColor = Theme.TextSecondary, Margin = new Padding(12, 6, 0, 0)
            });

            _cmbPageSize = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList, Width = 70,
                BackColor = Theme.BgInput, ForeColor = Theme.TextPrimary,
                FlatStyle = FlatStyle.Flat, Font = Theme.FontCell9
            };
            _cmbPageSize.Items.AddRange(new object[] { 20, 50, 100, 200, 500 });
            _cmbPageSize.SelectedIndex = 1;
            _cmbPageSize.SelectedIndexChanged += (s, e) =>
            {
                _pageSize = (int)_cmbPageSize.SelectedItem!;
                _page = 1;
                _ = LoadDataAsync();
            };
            searchBar.Controls.Add(_cmbPageSize);

            // DataGridView
            _dgvData = BuildDgv();
            _dgvData.Dock = DockStyle.Fill;
            _dgvData.ReadOnly = true;
            _dgvData.AllowUserToAddRows = false;
            _dgvData.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            rightPanel.Controls.Add(_dgvData, 0, 2);

            // 翻頁列
            var pageBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, BackColor = Theme.BgCard,
                FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
                Padding = new Padding(8, 4, 8, 4)
            };
            rightPanel.Controls.Add(pageBar, 0, 3);

            _btnPrev = MakeBtn("◀ 上一頁", Color.FromArgb(60, 65, 80));
            _btnPrev.Click += (s, e) => { if (_page > 1) { _page--; _ = LoadDataAsync(); } };
            pageBar.Controls.Add(_btnPrev);

            _lblPage = new Label
            {
                Text = "—", AutoSize = true,
                ForeColor = Theme.TextPrimary, Font = Theme.FontCell9,
                Margin = new Padding(10, 5, 10, 0)
            };
            pageBar.Controls.Add(_lblPage);

            _btnNext = MakeBtn("下一頁 ▶", Color.FromArgb(60, 65, 80));
            _btnNext.Click += (s, e) =>
            {
                if (_page * _pageSize < _totalRows) { _page++; _ = LoadDataAsync(); }
            };
            pageBar.Controls.Add(_btnNext);

            _lblStatus = new Label
            {
                Text = "", AutoSize = true,
                ForeColor = Theme.TextSecondary, Font = Theme.FontCell9,
                Margin = new Padding(20, 5, 0, 0)
            };
            pageBar.Controls.Add(_lblStatus);
        }

        // ══════════════════════════════════════════════════════
        // 資料載入
        // ══════════════════════════════════════════════════════
        private async Task LoadTablesAsync()
        {
            if (IsDisposed) return;
            if (!DatabaseManager.Instance.IsConnected) { _lblStatus.Text = "未連線"; return; }

            try
            {
                var tables = new List<string>();
                using var conn = DatabaseManager.Instance.GetConnection();
                await conn.OpenAsync();
                using var cmd = new MySqlCommand("SHOW TABLES", conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) tables.Add(r.GetString(0));

                if (IsDisposed) return;
                _allTables = tables;
                FilterTables();
                _lblStatus.Text = $"共 {tables.Count} 張表";
            }
            catch (Exception ex)
            {
                if (!IsDisposed) _lblStatus.Text = "載入失敗：" + ex.Message;
            }
        }

        private void FilterTables()
        {
            var kw = _txtTableSearch.Text.Trim().ToLower();
            var filtered = string.IsNullOrEmpty(kw)
                ? _allTables
                : _allTables.Where(t => t.ToLower().Contains(kw)).ToList();

            _lstTables.BeginUpdate();
            _lstTables.Items.Clear();
            foreach (var t in filtered) _lstTables.Items.Add(t);
            _lstTables.EndUpdate();
        }

        private async void LstTables_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_lstTables.SelectedItem is not string tbl) return;
            _currentTable = tbl;
            _page = 1;
            _txtSearch.Text = "";
            _searchText = "";
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            if (string.IsNullOrEmpty(_currentTable) || IsDisposed) return;
            if (!DatabaseManager.Instance.IsConnected) return;

            _searchText = _txtSearch.Text.Trim();
            _btnSearch.Enabled = false;
            _lblStatus.Text = "載入中...";
            _lblTableName.Text = $"📋 {_currentTable}  （讀取中...）";

            try
            {
                using var conn = DatabaseManager.Instance.GetConnection();
                await conn.OpenAsync();

                // 取得欄位資訊
                var columns = new List<string>();
                using (var desc = new MySqlCommand($"DESCRIBE `{_currentTable}`", conn))
                using (var rd = await desc.ExecuteReaderAsync())
                    while (await rd.ReadAsync()) columns.Add(rd.GetString(0));

                // WHERE 條件
                string where = "";
                if (!string.IsNullOrEmpty(_searchText))
                {
                    var parts = columns.Select(c => $"CAST(`{c}` AS CHAR) LIKE @kw");
                    where = "WHERE " + string.Join(" OR ", parts);
                }

                // 總筆數
                using (var cntCmd = new MySqlCommand($"SELECT COUNT(*) FROM `{_currentTable}` {where}", conn))
                {
                    if (!string.IsNullOrEmpty(_searchText))
                        cntCmd.Parameters.AddWithValue("@kw", $"%{_searchText}%");
                    _totalRows = Convert.ToInt32(await cntCmd.ExecuteScalarAsync());
                }

                // 取資料
                int offset = (_page - 1) * _pageSize;
                using var dataCmd = new MySqlCommand(
                    $"SELECT * FROM `{_currentTable}` {where} LIMIT {_pageSize} OFFSET {offset}", conn);
                if (!string.IsNullOrEmpty(_searchText))
                    dataCmd.Parameters.AddWithValue("@kw", $"%{_searchText}%");

                var dt = new DataTable();
                using var adapter = new MySqlDataAdapter(dataCmd);
                adapter.Fill(dt);

                if (IsDisposed) return;

                _dgvData.DataSource = null;
                _dgvData.DataSource = dt;

                // 自動欄寬，最大 250
                foreach (DataGridViewColumn col in _dgvData.Columns)
                {
                    col.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
                    col.MinimumWidth = 50;
                }
                foreach (DataGridViewColumn col in _dgvData.Columns)
                {
                    col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                    if (col.Width > 250) col.Width = 250;
                }

                int totalPages = (int)Math.Ceiling(_totalRows / (double)_pageSize);
                _lblPage.Text    = $"第 {_page} / {Math.Max(1, totalPages)} 頁";
                _lblStatus.Text  = $"共 {_totalRows:N0} 筆" +
                                   (string.IsNullOrEmpty(_searchText) ? "" : $"（搜尋：{_searchText}）");
                _lblTableName.Text = $"📋  {_currentTable}   欄位：{string.Join("  |  ", columns.Take(10))}{(columns.Count > 10 ? " ..." : "")}";
                _btnPrev.Enabled = _page > 1;
                _btnNext.Enabled = _page * _pageSize < _totalRows;
            }
            catch (Exception ex)
            {
                if (!IsDisposed) _lblStatus.Text = "錯誤：" + ex.Message;
            }
            finally
            {
                if (!IsDisposed) _btnSearch.Enabled = true;
            }
        }

        private static DataGridView BuildDgv()
        {
            var dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Theme.BgPage,
                BorderStyle = BorderStyle.None,
                GridColor = Color.FromArgb(55, 63, 80),
                RowHeadersVisible = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                ReadOnly = true,
                AllowUserToAddRows = false,
                ScrollBars = ScrollBars.Both,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(40, 47, 62),
                    ForeColor = Theme.TextSecondary,
                    Font = Theme.FontCell9Bold,
                    Padding = new Padding(4, 0, 4, 0)
                },
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing,
                RowTemplate = { Height = 26 }
            };
            dgv.EnableHeadersVisualStyles = false;
            dgv.DefaultCellStyle.BackColor = Theme.BgCard;
            dgv.DefaultCellStyle.ForeColor = Theme.TextPrimary;
            dgv.DefaultCellStyle.Font = Theme.FontCell9;
            dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(60, 80, 140);
            dgv.DefaultCellStyle.SelectionForeColor = Color.White;
            dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(48, 55, 72);
            return dgv;
        }

        private static Button MakeBtn(string text, Color bg) => new Button
        {
            Text = text, Height = 28, Width = 70,
            BackColor = bg, ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Font = Theme.FontCell9Bold,
            FlatAppearance = { BorderSize = 0 }
        };
    }
}
