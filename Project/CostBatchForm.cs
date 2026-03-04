using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    /// <summary>消費達成獎勵 — 全服批量管理（全選、批量重置）</summary>
    public class CostBatchForm : Form
    {
        private static readonly long[] Milestones = DatabaseManager.CostMilestones;

        private DataGridView _dgv;
        private Button   _btnLoadAll, _btnLoadOnline, _btnRefresh;
        private Button   _btnSelectAll, _btnDeselectAll, _btnSelectPending;
        private Button   _btnReset, _btnFullReset;
        private Label    _lblStatus;
        private TextBox  _txtFilter;

        // data
        private List<CostRow> _allRows   = new();
        private List<CostRow> _displayed = new();

        public CostBatchForm()
        {
            BuildUI();
        }

        // ── UI ────────────────────────────────────────────────────
        private void BuildUI()
        {
            Text        = "👥 消費達成獎勵 — 全服批量管理";
            BackColor   = Theme.BgPage;
            ForeColor   = Theme.TextPrimary;
            Font        = Theme.FontBody;
            MinimumSize = new Size(960, 620);
            Size        = new Size(1100, 720);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4,
                BackColor = Color.Transparent, Padding = new Padding(14, 10, 14, 10)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));  // 工具列
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));  // 篩選 + 批量按鈕
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // DataGridView
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));  // 狀態列
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            // ── Row 0：工具列 ──────────────────────────────────────
            var toolRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, BackColor = Color.Transparent,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false, AutoSize = false,
                Padding = new Padding(0, 4, 0, 4)
            };
            _btnLoadAll    = Theme.MakePrimaryButton("🌐 載入全服",   110, 32);
            _btnLoadOnline = Theme.MakeButton("🟢 載入線上玩家", Color.FromArgb(20, 60, 30), Color.FromArgb(80, 200, 100), 130, 32);
            _btnRefresh    = Theme.MakeButton("🔄 重新整理",    Color.FromArgb(30, 30, 50), Theme.TextMuted, 100, 32);

            _btnLoadAll.Margin    = new Padding(0, 0, 6, 0);
            _btnLoadOnline.Margin = new Padding(0, 0, 6, 0);
            _btnRefresh.Margin    = new Padding(0, 0, 0, 0);

            _btnLoadAll.Click    += (s, e) => _ = LoadAsync(false);
            _btnLoadOnline.Click += (s, e) => _ = LoadAsync(true);
            _btnRefresh.Click    += (s, e) => _ = LoadAsync(_lastOnlineOnly);

            toolRow.Controls.Add(_btnLoadAll);
            toolRow.Controls.Add(_btnLoadOnline);
            toolRow.Controls.Add(_btnRefresh);
            root.Controls.Add(toolRow, 0, 0);

            // ── Row 1：篩選 + 批量操作 ─────────────────────────────
            var filterRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 1,
                BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 4)
            };
            filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            filterRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _txtFilter = new TextBox
            {
                Dock = DockStyle.Fill, BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary,
                Font = Theme.FontBody, BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "篩選角色名 / CDKEY / 主帳號…"
            };
            _txtFilter.TextChanged += (s, e) => ApplyFilter();
            filterRow.Controls.Add(_txtFilter, 0, 0);

            _btnSelectAll     = Theme.MakeButton("☑ 全選",    Color.FromArgb(25,35,60), Color.FromArgb(100,150,255), 80, 32);
            _btnDeselectAll   = Theme.MakeButton("☐ 取消全選", Color.FromArgb(25,35,60), Theme.TextMuted, 90, 32);
            _btnSelectPending = Theme.MakeButton("🎁 選待補發", Color.FromArgb(40,35,10), Color.FromArgb(255,200,60), 95, 32);
            _btnReset         = Theme.MakeButton("🔄 批量重置已領", Color.FromArgb(60,50,10), Color.FromArgb(255,200,60), 130, 32);
            _btnFullReset     = Theme.MakeButton("🗑 批量完全重置", Color.FromArgb(100,20,20), Color.FromArgb(255,120,120), 130, 32);

            foreach (var b in new[] { _btnSelectAll, _btnDeselectAll, _btnSelectPending })
                b.Margin = new Padding(6, 0, 0, 0);
            _btnReset.Margin = _btnFullReset.Margin = new Padding(8, 0, 0, 0);

            _btnSelectAll.Click     += (s, e) => SetAllChecked(true);
            _btnDeselectAll.Click   += (s, e) => SetAllChecked(false);
            _btnSelectPending.Click += (s, e) => SelectPending();
            _btnReset.Click         += (s, e) => _ = DoBatchResetAsync(false);
            _btnFullReset.Click     += (s, e) => _ = DoBatchResetAsync(true);

            new ToolTip().SetToolTip(_btnReset,     "點數保留，check=0 → 玩家可立即重領");
            new ToolTip().SetToolTip(_btnFullReset, "point=0, check=0 → 玩家必須重新消費才能領取");

            filterRow.Controls.Add(_btnSelectAll,     1, 0);
            filterRow.Controls.Add(_btnDeselectAll,   2, 0);
            filterRow.Controls.Add(_btnSelectPending, 3, 0);
            filterRow.Controls.Add(_btnReset,         4, 0);
            filterRow.Controls.Add(_btnFullReset,     5, 0);
            root.Controls.Add(filterRow, 0, 1);

            // ── Row 2：DataGridView ────────────────────────────────
            _dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Theme.BgCard,
                GridColor       = Theme.BgMid,
                ForeColor       = Theme.TextPrimary,
                Font            = Theme.FontSmall,
                BorderStyle     = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                RowHeadersVisible      = false,
                AllowUserToAddRows     = false,
                AllowUserToDeleteRows  = false,
                AllowUserToResizeRows  = false,
                SelectionMode          = DataGridViewSelectionMode.FullRowSelect,
                ReadOnly               = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight    = 32,
                RowTemplate            = { Height = 28 },
                AutoSizeColumnsMode    = DataGridViewAutoSizeColumnsMode.None,
                ScrollBars             = ScrollBars.Both
            };
            _dgv.ColumnHeadersDefaultCellStyle.BackColor   = Theme.BgSidebar;
            _dgv.ColumnHeadersDefaultCellStyle.ForeColor   = Theme.TextMuted;
            _dgv.ColumnHeadersDefaultCellStyle.Font        = Theme.FontSmall;
            _dgv.DefaultCellStyle.BackColor                 = Theme.BgCard;
            _dgv.DefaultCellStyle.ForeColor                 = Theme.TextPrimary;
            _dgv.AlternatingRowsDefaultCellStyle.BackColor  = Theme.BgLight;

            // 欄位定義
            _dgv.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "colCheck", HeaderText = "☐", Width = 36,
                ReadOnly = false, Resizable = DataGridViewTriState.False
            });
            AddCol("colName",    "角色名",   160, true);
            AddCol("colCdkey",   "CDKEY",    140, false);
            AddCol("colMaster",  "主帳號",   130, false);
            AddCol("colPoint",   "累計金幣", 100, false);
            AddCol("colClaimed", "已領/5",   60,  false);
            AddCol("colMiles",   "里程碑",   110, false);
            AddCol("colOnline",  "狀態",     55,  false);

            _dgv.CellFormatting += DgvCellFormatting;
            _dgv.CellValueChanged += (s, e) =>
            {
                if (e.ColumnIndex == 0 && e.RowIndex >= 0)
                    UpdateSelStatus();
            };
            _dgv.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_dgv.IsCurrentCellDirty) _dgv.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            root.Controls.Add(_dgv, 0, 2);

            // ── Row 3：狀態列 ──────────────────────────────────────
            _lblStatus = new Label
            {
                Dock = DockStyle.Fill, ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                TextAlign = ContentAlignment.MiddleLeft, Text = "請點「載入全服」或「載入線上玩家」開始"
            };
            root.Controls.Add(_lblStatus, 0, 3);

            Controls.Add(root);
        }

        private void AddCol(string name, string header, int width, bool bold)
        {
            var col = new DataGridViewTextBoxColumn
            {
                Name = name, HeaderText = header, Width = width,
                ReadOnly = true, Resizable = DataGridViewTriState.True,
                SortMode = DataGridViewColumnSortMode.Automatic
            };
            if (bold) col.DefaultCellStyle.Font = new Font(Theme.FontFamily, Theme.FontBody.Size, FontStyle.Bold);
            _dgv.Columns.Add(col);
        }

        // ── 載入 ──────────────────────────────────────────────────
        private bool _lastOnlineOnly = false;

        private async Task LoadAsync(bool onlineOnly)
        {
            _lastOnlineOnly = onlineOnly;
            SetBusy(true);
            _lblStatus.Text = onlineOnly ? "載入線上玩家中…" : "載入全服資料中…";
            try
            {
                var raw = await DatabaseManager.Instance.GetAllCostDataListAsync(onlineOnly);
                _allRows = raw.Select(r => new CostRow
                {
                    Uid           = r.uid,
                    OnlineName    = r.onlineName,
                    MasterAccount = r.masterAccount,
                    IsOnline      = r.isOnline,
                    Point         = r.point,
                    Check         = r.check
                }).ToList();
                ApplyFilter();
                _lblStatus.Text = $"共 {_allRows.Count} 筆（{(onlineOnly ? "線上玩家" : "全服")}）";
            }
            catch (Exception ex) { _lblStatus.Text = "✗ " + ex.Message; }
            finally { SetBusy(false); }
        }

        private void ApplyFilter()
        {
            string kw = _txtFilter.Text.Trim().ToLower();
            _displayed = string.IsNullOrEmpty(kw)
                ? _allRows.ToList()
                : _allRows.Where(r =>
                    r.OnlineName.ToLower().Contains(kw) ||
                    r.Uid.ToLower().Contains(kw) ||
                    r.MasterAccount.ToLower().Contains(kw)).ToList();
            RefreshGrid();
        }

        private void RefreshGrid()
        {
            _dgv.Rows.Clear();
            foreach (var r in _displayed)
            {
                string miles = "";
                for (int i = 0; i < Milestones.Length; i++)
                {
                    bool reached = r.Point >= Milestones[i];
                    bool claimed = (r.Check & (1 << i)) != 0;
                    miles += claimed ? "✅" : reached ? "🎁" : "⬜";
                }
                int idx = _dgv.Rows.Add(
                    r.Selected,
                    string.IsNullOrEmpty(r.OnlineName) ? "（無名）" : r.OnlineName,
                    r.Uid,
                    r.MasterAccount,
                    $"{r.Point:N0}",
                    $"{System.Numerics.BitOperations.PopCount((uint)(r.Check >= 0 ? r.Check : 0))}/5",
                    miles,
                    r.IsOnline ? "● 線上" : "");
                _dgv.Rows[idx].Tag = r;
            }
            UpdateSelStatus();
        }

        private void DgvCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (_dgv.Rows[e.RowIndex].Tag is not CostRow r) return;
            var col = _dgv.Columns[e.ColumnIndex].Name;

            if (r.IsOnline && col == "colOnline")
            { e.CellStyle.ForeColor = Color.FromArgb(22, 200, 120); e.FormattingApplied = true; }
            else if (col == "colPoint")
            { e.CellStyle.ForeColor = Color.FromArgb(180, 130, 255); e.CellStyle.Font = new Font(Theme.FontFamily, Theme.FontSmall.Size, FontStyle.Bold); e.FormattingApplied = true; }
            else if (col == "colCdkey")
            { e.CellStyle.ForeColor = Color.FromArgb(96, 165, 250); e.FormattingApplied = true; }
            else if (col == "colMaster")
            { e.CellStyle.ForeColor = Theme.TextMuted; e.FormattingApplied = true; }
        }

        // ── 批量選取 ──────────────────────────────────────────────
        private void SetAllChecked(bool val)
        {
            foreach (var r in _displayed) r.Selected = val;
            foreach (DataGridViewRow row in _dgv.Rows)
                if (row.Tag is CostRow r) row.Cells["colCheck"].Value = r.Selected;
            UpdateSelStatus();
        }

        private void SelectPending()
        {
            foreach (var r in _displayed)
            {
                bool hasPending = false;
                for (int i = 0; i < Milestones.Length; i++)
                    if (r.Point >= Milestones[i] && (r.Check & (1 << i)) == 0) { hasPending = true; break; }
                r.Selected = hasPending;
            }
            foreach (DataGridViewRow row in _dgv.Rows)
                if (row.Tag is CostRow r) row.Cells["colCheck"].Value = r.Selected;
            UpdateSelStatus();
        }

        private void UpdateSelStatus()
        {
            // 同步 grid checkbox → CostRow.Selected
            foreach (DataGridViewRow row in _dgv.Rows)
            {
                if (row.Tag is CostRow r)
                    r.Selected = Convert.ToBoolean(row.Cells["colCheck"].Value);
            }
            int sel = _displayed.Count(r => r.Selected);
            _lblStatus.Text = $"已選 {sel} / {_displayed.Count} 筆（全服共 {_allRows.Count} 筆）";
        }

        // ── 批量重置 ──────────────────────────────────────────────
        private async Task DoBatchResetAsync(bool fullReset)
        {
            UpdateSelStatus();
            var targets = _displayed.Where(r => r.Selected).ToList();
            if (targets.Count == 0)
            {
                MessageBox.Show("請先勾選要操作的玩家。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string kind = fullReset
                ? "🗑 完全重置（point=0, check=0，玩家須重新消費才能領取）"
                : "🔄 重置已領狀態（point 保留，玩家可立即重領）";
            string names = string.Join("、", targets.Take(5).Select(r => r.OnlineName.Length > 0 ? r.OnlineName : r.Uid));
            if (targets.Count > 5) names += $" … 共 {targets.Count} 人";
            if (MessageBox.Show(
                $"{kind}\n\n對象：{names}\n\n⚠ 此操作無法復原！確定執行？",
                "確認批量操作", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            SetBusy(true);
            _lblStatus.Text = "執行中…";
            int success = 0, fail = 0;
            var prog = new Progress<(int done, string uid, bool ok)>(p =>
            {
                _lblStatus.Text = $"執行中 {p.done}/{targets.Count}  UID：{p.uid}  {(p.ok ? "✓" : "✗")}";
            });

            await Task.Run(async () =>
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    var t = targets[i];
                    bool ok = fullReset
                        ? await DatabaseManager.Instance.FullResetCostDataAsync(t.Uid)
                        : await DatabaseManager.Instance.ResetCostDataAsync(t.Uid);
                    if (ok) success++; else fail++;
                    ((IProgress<(int, string, bool)>)prog).Report((i + 1, t.Uid, ok));
                }
            });

            SetBusy(false);
            MessageBox.Show(
                $"批量操作完成！\n\n✅ 成功：{success} 筆\n✗ 失敗：{fail} 筆",
                "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await LoadAsync(_lastOnlineOnly);
        }

        private void SetBusy(bool busy)
        {
            _btnLoadAll.Enabled = _btnLoadOnline.Enabled = _btnRefresh.Enabled = !busy;
            _btnReset.Enabled   = _btnFullReset.Enabled                        = !busy;
        }

        // ── Row model ─────────────────────────────────────────────
        private class CostRow
        {
            public string Uid           { get; set; } = "";
            public string OnlineName    { get; set; } = "";
            public string MasterAccount { get; set; } = "";
            public bool   IsOnline      { get; set; }
            public long   Point         { get; set; }
            public int    Check         { get; set; }
            public bool   Selected      { get; set; }
        }
    }
}
