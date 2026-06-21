using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MySqlConnector;

namespace SQ_Email_Tools
{
    public class IpScanForm : UserControl
    {
        // ── 控件 ─────────────────────────────────────────────
        private Button       _btnScan;
        private Button       _btnExport;
        private ComboBox     _cmbMinGroup;
        private CheckBox     _chkOnlineOnly;
        private Label        _lblStatus;
        private ListBox      _lstGroups;
        private DataGridView _dgvMembers;
        private Label        _lblGroupTitle;
        private TextBox      _txtIpFilter;

        // 手動查單一帳號
        private TextBox  _txtAccQuery;
        private Button   _btnAccQuery;
        private Label    _lblAccResult;

        // ── 狀態 ─────────────────────────────────────────────
        private List<IpGroupEntry>              _groups    = new();
        private IpGroupEntry?                   _selected  = null;
        private Dictionary<string, IpLabel>     _ipLabels  = new();  // IP 黑白名單快取

        public IpScanForm() => BuildUI();
        public void TriggerLoad() { }

        // ══════════════════════════════════════════════════════
        // UI 建置
        // ══════════════════════════════════════════════════════
        private void BuildUI()
        {
            Theme.ApplyHubForm(this);
            Dock      = DockStyle.Fill;
            BackColor = Theme.BgPage;
            Font      = Theme.FontBody;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
                BackColor = Color.Transparent
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); // 工具列
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); // 手動查詢列
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // 主體
            Controls.Add(root);

            // ── 工具列 ────────────────────────────────────
            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, BackColor = Theme.BgCard,
                FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
                Padding = new Padding(10, 8, 10, 6)
            };
            root.Controls.Add(toolbar, 0, 0);

            toolbar.Controls.Add(MakeLabel("最少帳號數："));

            _cmbMinGroup = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList, Width = 65,
                BackColor = Theme.BgInput, ForeColor = Theme.TextPrimary,
                FlatStyle = FlatStyle.Flat, Font = Theme.FontCell9
            };
            _cmbMinGroup.Items.AddRange(new object[] { 2, 3, 5, 10 });
            _cmbMinGroup.SelectedIndex = 0;
            toolbar.Controls.Add(_cmbMinGroup);

            _chkOnlineOnly = new CheckBox
            {
                Text = "只顯示有在線的群組", AutoSize = true,
                ForeColor = Theme.TextSecondary, Font = Theme.FontCell9,
                Margin = new Padding(10, 5, 0, 0)
            };
            _chkOnlineOnly.CheckedChanged += (s, e) => ApplyGroupFilter();
            toolbar.Controls.Add(_chkOnlineOnly);

            _btnScan = MakeBtn("🔍 掃描", Theme.AccentBlue, 90);
            _btnScan.Margin = new Padding(14, 0, 0, 0);
            _btnScan.Click += (s, e) => _ = RunScanAsync();
            toolbar.Controls.Add(_btnScan);

            _btnExport = MakeBtn("📥 匯出 CSV", Color.FromArgb(55, 62, 80), 105);
            _btnExport.Enabled = false;
            _btnExport.Click += BtnExport_Click;
            toolbar.Controls.Add(_btnExport);

            _lblStatus = new Label
            {
                AutoSize = true, ForeColor = Theme.TextSecondary, Font = Theme.FontCell9,
                Margin = new Padding(14, 6, 0, 0)
            };
            toolbar.Controls.Add(_lblStatus);

            // ── 手動查單一帳號列 ──────────────────────────
            var accBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, BackColor = Color.FromArgb(40, 47, 60),
                FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
                Padding = new Padding(10, 5, 10, 5)
            };
            root.Controls.Add(accBar, 0, 1);

            accBar.Controls.Add(MakeLabel("查詢單一帳號："));

            _txtAccQuery = new TextBox
            {
                Width = 200, Height = 26, BackColor = Theme.BgInput,
                ForeColor = Theme.TextPrimary, BorderStyle = BorderStyle.FixedSingle,
                Font = Theme.FontCell9
            };
            _txtAccQuery.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) _ = QuerySingleAsync(); };
            accBar.Controls.Add(_txtAccQuery);

            _btnAccQuery = MakeBtn("查詢", Theme.AccentBlue, 60);
            _btnAccQuery.Click += (s, e) => _ = QuerySingleAsync();
            accBar.Controls.Add(_btnAccQuery);

            _lblAccResult = new Label
            {
                AutoSize = true, ForeColor = Theme.TextSecondary, Font = Theme.FontCell9,
                Margin = new Padding(10, 6, 0, 0)
            };
            accBar.Controls.Add(_lblAccResult);

            // ── 主體：左側群組清單 + 右側成員表 ──────────
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill, Orientation = Orientation.Vertical,
                SplitterWidth = 4, BackColor = Theme.BgPage
            };
            split.SplitterDistance = 260;
            root.Controls.Add(split, 0, 2);

            // 左側
            var leftLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
                BackColor = Color.Transparent
            };
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            split.Panel1.Controls.Add(leftLayout);

            var leftHeader = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, BackColor = Theme.BgCard,
                FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
                Padding = new Padding(8, 4, 4, 4)
            };
            leftLayout.Controls.Add(leftHeader, 0, 0);
            leftHeader.Controls.Add(MakeLabel("🔴 共用IP群組"));

            _txtIpFilter = new TextBox
            {
                Width = 100, Height = 22, BackColor = Theme.BgInput,
                ForeColor = Theme.TextPrimary, BorderStyle = BorderStyle.FixedSingle,
                Font = Theme.FontCell9, Margin = new Padding(6, 2, 0, 0)
            };
            _txtIpFilter.TextChanged += (s, e) => ApplyGroupFilter();
            leftHeader.Controls.Add(_txtIpFilter);

            _lstGroups = new ListBox
            {
                Dock = DockStyle.Fill, BackColor = Theme.BgCard,
                ForeColor = Theme.TextPrimary, Font = Theme.FontCell9,
                BorderStyle = BorderStyle.None, IntegralHeight = false,
                DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 26
            };
            _lstGroups.DrawItem    += LstGroups_DrawItem;
            _lstGroups.SelectedIndexChanged += LstGroups_SelectedIndexChanged;
            leftLayout.Controls.Add(_lstGroups, 0, 1);

            // 右側
            var rightLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
                BackColor = Color.Transparent
            };
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            split.Panel2.Controls.Add(rightLayout);

            // 右側標題列：標題 + 「批量封禁」按鈕
            var rightHeader = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgCard };
            _lblGroupTitle = new Label
            {
                Text = "← 選擇左側群組查看成員",
                ForeColor = Theme.TextSecondary, Font = Theme.FontCell9Bold,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                Location = new Point(0, 0), Height = 30, Width = 300
            };

            var _btnBanGroup = MakeBtn("🚫 封禁整群", Color.FromArgb(120, 20, 20), 110);
            _btnBanGroup.Enabled = false;
            _btnBanGroup.Height  = 24;
            _btnBanGroup.Anchor  = AnchorStyles.Right | AnchorStyles.Top;
            _btnBanGroup.Click  += async (s, e) =>
            {
                if (_selected == null) return;
                var allAccs = _selected.Members.Select(m => m.Account).ToList();
                if (allAccs.Count == 0) return;

                int banDays = 0; string banReason = "";
                using (var dlg = new Form())
                {
                    dlg.Text = $"🚫 批量封禁 — IP：{_selected.Ip}";
                    dlg.Size = new Size(420, 260);
                    dlg.BackColor = Theme.BgPage; dlg.ForeColor = Theme.TextPrimary;
                    dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                    dlg.StartPosition = FormStartPosition.CenterParent;
                    dlg.MaximizeBox = false;

                    var lbl1 = new Label { Text = $"將封禁以下 {allAccs.Count} 個帳號：", ForeColor = Theme.AccentRed, Font = Theme.FontCell9Bold, AutoSize = true, Location = new Point(16, 12) };
                    var lblList = new Label { Text = string.Join("  ", allAccs.Take(10)) + (allAccs.Count > 10 ? $"…等{allAccs.Count}人" : ""), ForeColor = Theme.TextMuted, Font = Theme.FontCell9, AutoSize = false, Width = 380, Height = 36, Location = new Point(16, 34) };
                    var lbl2 = new Label { Text = "封禁天數（0 = 永久）：", ForeColor = Theme.TextSecondary, Font = Theme.FontCell9, AutoSize = true, Location = new Point(16, 78) };
                    var tbDays = new TextBox { Text = "7", Width = 80, Location = new Point(160, 75), BackColor = Theme.BgInput, ForeColor = Theme.TextPrimary, BorderStyle = BorderStyle.FixedSingle };
                    var lbl3 = new Label { Text = "封禁原因（選填）：", ForeColor = Theme.TextSecondary, Font = Theme.FontCell9, AutoSize = true, Location = new Point(16, 108) };
                    var tbReason = new TextBox { Text = "工作室", Width = 360, Location = new Point(16, 126), BackColor = Theme.BgInput, ForeColor = Theme.TextPrimary, BorderStyle = BorderStyle.FixedSingle };
                    var btnOk  = Theme.MakePrimaryButton("🚫 確認封禁", 100, 30); btnOk.Location  = new Point(196, 170);
                    var btnCan = Theme.MakeButton("取消", Theme.BgLight, Theme.TextSecondary, 80, 30); btnCan.Location = new Point(308, 170);
                    btnOk.Click  += (_, __) => dlg.DialogResult = DialogResult.OK;
                    btnCan.Click += (_, __) => dlg.DialogResult = DialogResult.Cancel;
                    dlg.Controls.AddRange(new Control[] { lbl1, lblList, lbl2, tbDays, lbl3, tbReason, btnOk, btnCan });
                    dlg.AcceptButton = btnOk; dlg.CancelButton = btnCan;
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    banDays   = int.TryParse(tbDays.Text, out var d) ? d : 7;
                    banReason = tbReason.Text;
                }

                _btnBanGroup.Enabled = false;
                int ok = 0, fail = 0;
                foreach (var acc in allAccs)
                {
                    try
                    {
                        int endUnix = banDays == 0 ? 0 : (int)DateTimeOffset.Now.AddDays(banDays).ToUnixTimeSeconds();
                        if (await DatabaseManager.Instance.BanPlayerAsync(acc, endUnix, banReason)) ok++; else fail++;
                    }
                    catch { fail++; }
                }
                string dur = banDays == 0 ? "永久" : $"{banDays} 天";
                MessageBox.Show($"批量封禁完成（{dur}）\n成功：{ok} 人\n失敗：{fail} 人", "封禁結果", MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (!IsDisposed) _btnBanGroup.Enabled = true;
                _ = RunScanAsync();
            };

            var _btnUnbanGroup = MakeBtn("✅ 解封整群", Color.FromArgb(20, 80, 30), 110);
            _btnUnbanGroup.Enabled = false;
            _btnUnbanGroup.Height  = 24;
            _btnUnbanGroup.Anchor  = AnchorStyles.Right | AnchorStyles.Top;
            _btnUnbanGroup.Click  += async (s, e) =>
            {
                if (_selected == null) return;
                var allAccs = _selected.Members.Select(m => m.Account).ToList();
                if (allAccs.Count == 0) return;
                if (MessageBox.Show($"確定解封此群組所有 {allAccs.Count} 個帳號？\nIP：{_selected.Ip}",
                    "確認解封", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
                _btnUnbanGroup.Enabled = false;
                int ok = 0, fail = 0;
                foreach (var acc in allAccs)
                {
                    try { if (await DatabaseManager.Instance.UnbanPlayerAsync(acc)) ok++; else fail++; }
                    catch { fail++; }
                }
                MessageBox.Show($"批量解封完成\n成功：{ok} 人\n失敗（本來未封禁）：{fail} 人", "解封結果", MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (!IsDisposed) _btnUnbanGroup.Enabled = true;
                _ = RunScanAsync();
            };

            var _btnExportGroup = MakeBtn("📥 匯出此群組", Color.FromArgb(30, 60, 90), 110);
            _btnExportGroup.Enabled = false;
            _btnExportGroup.Height  = 24;
            _btnExportGroup.Anchor  = AnchorStyles.Right | AnchorStyles.Top;
            _btnExportGroup.Click  += (s, e) => ExportSelectedGroup();

            _lstGroups.SelectedIndexChanged += (s, e) =>
            {
                _btnBanGroup.Enabled    = _selected?.Members.Count > 0;
                _btnUnbanGroup.Enabled  = _selected?.Members.Count > 0;
                _btnExportGroup.Enabled = _selected?.Members.Count > 0;
                _btnBanGroup.Text = _selected != null
                    ? $"🚫 封禁整群 ({_selected.Members.Count} 人)"
                    : "🚫 封禁整群";
                _btnUnbanGroup.Text = _selected != null
                    ? $"✅ 解封整群 ({_selected.Members.Count} 人)"
                    : "✅ 解封整群";
                _btnExportGroup.Text = _selected != null
                    ? $"📥 匯出此群組 ({_selected.Members.Count})"
                    : "📥 匯出此群組";
            };
            rightHeader.Controls.Add(_lblGroupTitle);
            rightHeader.Controls.Add(_btnBanGroup);
            rightHeader.Controls.Add(_btnUnbanGroup);
            rightHeader.Controls.Add(_btnExportGroup);
            rightHeader.Resize += (s, e) =>
            {
                _btnBanGroup.Location    = new Point(rightHeader.Width - _btnBanGroup.Width - 6, 3);
                _btnUnbanGroup.Location  = new Point(rightHeader.Width - _btnBanGroup.Width - _btnUnbanGroup.Width - 12, 3);
                _btnExportGroup.Location = new Point(rightHeader.Width - _btnBanGroup.Width - _btnUnbanGroup.Width - _btnExportGroup.Width - 18, 3);
                _lblGroupTitle.Width     = rightHeader.Width - _btnBanGroup.Width - _btnUnbanGroup.Width - _btnExportGroup.Width - 24;
            };
            rightLayout.Controls.Add(rightHeader, 0, 0);

            _dgvMembers = BuildDgv();

            // 右鍵選單
            var ctxMenu = new ContextMenuStrip { BackColor = Theme.BgCard, ForeColor = Theme.TextPrimary, Font = Theme.FontCell9 };

            var copyCell = new ToolStripMenuItem("複製儲存格") { ForeColor = Theme.TextPrimary };
            copyCell.Click += (s, e) => CopyDgvSelection();
            ctxMenu.Items.Add(copyCell);

            var copyRow = new ToolStripMenuItem("複製整列") { ForeColor = Theme.TextPrimary };
            copyRow.Click += (s, e) => CopyDgvRows();
            ctxMenu.Items.Add(copyRow);

            var copyAll = new ToolStripMenuItem("複製全部（含標題）") { ForeColor = Theme.TextPrimary };
            copyAll.Click += (s, e) => CopyDgvAll();
            ctxMenu.Items.Add(copyAll);

            ctxMenu.Items.Add(new ToolStripSeparator());

            var kickItem = new ToolStripMenuItem("⚡ 強制下線此帳號（不封號）") { ForeColor = Color.FromArgb(255, 160, 50) };            kickItem.Click += async (s, e) =>
            {
                var cell = _dgvMembers.CurrentCell;
                if (cell == null || cell.RowIndex < 0) return;
                string account = _dgvMembers.Rows[cell.RowIndex].Cells[0].Value?.ToString() ?? "";
                string charName = _dgvMembers.Rows[cell.RowIndex].Cells[1].Value?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(account)) return;
                if (MessageBox.Show($"確定強制下線「{charName}」（{account}）？\n（不封號，只踢出線上）",
                    "確認", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
                try
                {
                    bool ok = await DatabaseManager.Instance.ForceOfflineAsync(account);
                    MessageBox.Show(ok ? $"✅ 已強制下線「{charName}」" : "操作失敗，請確認資料庫連線。");
                }
                catch (Exception ex2) { MessageBox.Show("錯誤：" + ex2.Message); }
            };
            ctxMenu.Items.Add(kickItem);

            ctxMenu.Items.Add(new ToolStripSeparator());

            var queryOwnerItem = new ToolStripMenuItem("🔎 查詢此 IP 的原始主人") { ForeColor = Color.FromArgb(248, 185, 90) };
            queryOwnerItem.Click += (s, e) => QueryIpOwnerFromContext();
            ctxMenu.Items.Add(queryOwnerItem);

            ctxMenu.Items.Add(new ToolStripSeparator());

            // ── IP 黑白名單標記 ──────────────────────────────────────
            var labelStudio = new ToolStripMenuItem("🔴 標記為工作室 IP") { ForeColor = Color.FromArgb(255, 100, 100) };
            var labelWhite  = new ToolStripMenuItem("🟢 標記為白名單 IP（正常共用）") { ForeColor = Color.FromArgb(100, 220, 120) };
            var labelClear  = new ToolStripMenuItem("🔘 移除標記") { ForeColor = Theme.TextMuted };

            string GetCurrentRowIp()
            {
                var cell = _dgvMembers.CurrentCell;
                if (cell == null || cell.RowIndex < 0) return "";
                // 優先取 loginIp 欄（col 4），若為空取 regIp（col 5）
                return _dgvMembers.Rows[cell.RowIndex].Cells[4].Value?.ToString()?.Trim() ?? "";
            }

            labelStudio.Click += async (s, e) =>
            {
                string ip = GetCurrentRowIp();
                if (string.IsNullOrEmpty(ip)) return;
                string note = ShowInputBox($"標記為工作室 IP：{ip}\n備註（選填）：", "標記工作室", "工作室");
                await DatabaseManager.Instance.SetIpLabelAsync(ip, IpLabelType.Studio, note);
                _ipLabels = await DatabaseManager.Instance.GetIpLabelsAsync();
                _lstGroups.Invalidate();
            };
            labelWhite.Click += async (s, e) =>
            {
                string ip = GetCurrentRowIp();
                if (string.IsNullOrEmpty(ip)) return;
                string note = ShowInputBox($"標記為白名單 IP：{ip}\n備註（選填，例如：網咖）：", "標記白名單", "網咖");
                await DatabaseManager.Instance.SetIpLabelAsync(ip, IpLabelType.Whitelist, note);
                _ipLabels = await DatabaseManager.Instance.GetIpLabelsAsync();
                _lstGroups.Invalidate();
            };
            labelClear.Click += async (s, e) =>
            {
                string ip = GetCurrentRowIp();
                if (string.IsNullOrEmpty(ip)) return;
                await DatabaseManager.Instance.RemoveIpLabelAsync(ip);
                _ipLabels = await DatabaseManager.Instance.GetIpLabelsAsync();
                _lstGroups.Invalidate();
            };
            ctxMenu.Items.Add(labelStudio);
            ctxMenu.Items.Add(labelWhite);
            ctxMenu.Items.Add(labelClear);

            // 開啟右鍵選單前，動態決定各項目是否啟用
            ctxMenu.Opening += (s, e) =>
            {
                var cell = _dgvMembers.CurrentCell;
                bool hasRow = cell != null && cell.RowIndex >= 0;

                queryOwnerItem.Enabled = cell != null && cell.RowIndex >= 0
                    && (cell.ColumnIndex == 4 || cell.ColumnIndex == 5)
                    && !string.IsNullOrWhiteSpace(cell.Value?.ToString());
                queryOwnerItem.Text = queryOwnerItem.Enabled
                    ? $"🔎 查 IP 原始主人：{cell!.Value}"
                    : "🔎 查 IP 原始主人（請先點選IP欄位）";

                // 標記選項：取目前列的 loginIp
                string rowIp = "";
                if (hasRow) rowIp = _dgvMembers.Rows[cell!.RowIndex].Cells[4].Value?.ToString()?.Trim() ?? "";
                bool hasIp = !string.IsNullOrEmpty(rowIp);
                labelStudio.Enabled = hasIp;
                labelWhite.Enabled  = hasIp;
                labelClear.Enabled  = hasIp && _ipLabels.ContainsKey(rowIp);
                if (hasIp)
                {
                    _ipLabels.TryGetValue(rowIp, out var lbl);
                    string current = lbl == null ? "" : lbl.Label == IpLabelType.Studio ? "（目前：工作室）" : "（目前：白名單）";
                    labelStudio.Text = $"🔴 標記工作室 IP：{rowIp} {current}";
                    labelWhite.Text  = $"🟢 標記白名單 IP：{rowIp} {current}";
                    labelClear.Text  = $"🔘 移除標記：{rowIp}";
                }
                else
                {
                    labelStudio.Text = "🔴 標記為工作室 IP";
                    labelWhite.Text  = "🟢 標記為白名單 IP";
                    labelClear.Text  = "🔘 移除標記";
                }
            };

            _dgvMembers.ContextMenuStrip = ctxMenu;

            // Ctrl+C 複製選取儲存格（覆蓋預設行為，避免複製到表頭）
            _dgvMembers.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.C)
                {
                    CopyDgvSelection();
                    e.Handled = true;
                }
            };

            rightLayout.Controls.Add(_dgvMembers, 0, 1);
        }

        // ══════════════════════════════════════════════════════
        // 掃描
        // ══════════════════════════════════════════════════════
        private async Task RunScanAsync()
        {
            _btnScan.Enabled = false;
            _btnExport.Enabled = false;
            _lblStatus.Text = "⏳ 掃描中...";
            _lblStatus.ForeColor = Theme.TextSecondary;
            _lstGroups.Items.Clear();
            _groups.Clear();
            _selected = null;
            ClearMemberDgv();

            try
            {
                // 確保 ip_labels 表存在，並載入標記快取
                await DatabaseManager.Instance.EnsureIpLabelsTableAsync();
                _ipLabels = await DatabaseManager.Instance.GetIpLabelsAsync();

                int minGroup = (int)_cmbMinGroup.SelectedItem!;
                _groups = await DatabaseManager.Instance.GetIpGroupsAsync(minGroup);
                if (IsDisposed) return;

                RefreshGroupList();

                int total = _groups.Sum(g => g.Members.Count);
                if (_groups.Count == 0)
                {
                    _lblStatus.Text      = "✅ 未發現共用IP群組";
                    _lblStatus.ForeColor = Color.FromArgb(22, 185, 122);
                }
                else
                {
                    int studioCount = _groups.Count(g => _ipLabels.ContainsKey(g.Ip) && _ipLabels[g.Ip].Label == IpLabelType.Studio);
                    _lblStatus.Text      = $"發現 {_groups.Count} 個群組，涉及 {total} 個帳號" +
                                           (studioCount > 0 ? $"  🔴 已標記工作室 {studioCount} 個" : "");
                    _lblStatus.ForeColor = Color.FromArgb(248, 113, 113);
                    _btnExport.Enabled   = true;
                }
            }
            catch (Exception ex)
            {
                if (!IsDisposed) { _lblStatus.Text = "掃描失敗：" + ex.Message; _lblStatus.ForeColor = Color.FromArgb(248, 113, 113); }
            }
            finally
            {
                if (!IsDisposed) _btnScan.Enabled = true;
            }
        }

        // ══════════════════════════════════════════════════════
        // 手動查單一帳號
        // ══════════════════════════════════════════════════════
        private async Task QuerySingleAsync()
        {
            var acc = _txtAccQuery.Text.Trim();
            if (string.IsNullOrEmpty(acc)) return;

            _btnAccQuery.Enabled = false;
            _lblAccResult.Text   = "查詢中...";
            _lblAccResult.ForeColor = Theme.TextSecondary;

            try
            {
                var result = await DatabaseManager.Instance.GetSharedIpForAccountAsync(acc);
                if (IsDisposed) return;

                if (result == null)
                {
                    _lblAccResult.Text      = $"找不到帳號「{acc}」";
                    _lblAccResult.ForeColor = Color.FromArgb(248, 113, 113);
                    ClearMemberDgv();
                    return;
                }

                // 建立臨時群組顯示
                var tempGroup = new IpGroupEntry
                {
                    Ip          = $"{result.LoginIp} / {result.RegIp}",
                    OnlineCount = result.IsOnline ? 1 : 0,
                    TotalCount  = result.SharedMembers.Count + 1,
                    Members     = new List<IpGroupMember> { new IpGroupMember {
                        Account = result.Account, CharName = result.CharName,
                        MasterName = result.MasterName, LoginIp = result.LoginIp,
                        RegIp = result.RegIp, IsOnline = result.IsOnline
                    }}.Concat(result.SharedMembers).ToList()
                };
                ShowGroupInDgv(tempGroup);

                int sharedCount = result.SharedMembers.Count;
                _lblAccResult.Text = sharedCount == 0
                    ? "✅ 無共用IP帳號"
                    : $"⚠ 發現 {sharedCount} 個共用IP帳號";
                _lblAccResult.ForeColor = sharedCount == 0 ? Color.FromArgb(22, 185, 122) : Color.FromArgb(248, 113, 113);
            }
            catch (Exception ex)
            {
                if (!IsDisposed) { _lblAccResult.Text = "查詢失敗：" + ex.Message; _lblAccResult.ForeColor = Color.FromArgb(248, 113, 113); }
            }
            finally
            {
                if (!IsDisposed) _btnAccQuery.Enabled = true;
            }
        }

        // ══════════════════════════════════════════════════════
        // 群組清單
        // ══════════════════════════════════════════════════════
        private void RefreshGroupList()
        {
            var kw      = _txtIpFilter.Text.Trim().ToLower();
            bool onlyOn = _chkOnlineOnly.Checked;
            _lstGroups.Items.Clear();
            foreach (var g in _groups)
            {
                if (!string.IsNullOrEmpty(kw) && !g.Ip.ToLower().Contains(kw)) continue;
                if (onlyOn && g.OnlineCount == 0) continue;
                _lstGroups.Items.Add(g);
            }
        }

        private void ApplyGroupFilter() => RefreshGroupList();

        private void LstGroups_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_lstGroups.SelectedItem is IpGroupEntry g)
            {
                _selected = g;
                ShowGroupInDgv(g);
            }
        }

        private void LstGroups_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _lstGroups.Items.Count) return;
            var g = (IpGroupEntry)_lstGroups.Items[e.Index];
            bool selected  = (e.State & DrawItemState.Selected) != 0;
            bool hasOnline = g.OnlineCount > 0;

            // 依黑白名單決定背景色
            _ipLabels.TryGetValue(g.Ip, out var label);
            Color bgColor;
            if (selected)
                bgColor = Color.FromArgb(60, 80, 140);
            else if (label?.Label == IpLabelType.Studio)
                bgColor = Color.FromArgb(70, 20, 20);      // 工作室：深紅
            else if (label?.Label == IpLabelType.Whitelist)
                bgColor = Color.FromArgb(20, 55, 30);      // 白名單：深綠
            else if (hasOnline)
                bgColor = Color.FromArgb(55, 28, 28);
            else
                bgColor = Theme.BgCard;
            e.Graphics.FillRectangle(new SolidBrush(bgColor), e.Bounds);

            // 標記圖示
            string badge = label?.Label == IpLabelType.Studio ? "🔴 " :
                           label?.Label == IpLabelType.Whitelist ? "🟢 " : "";

            // IP 文字
            var ipColor = label?.Label == IpLabelType.Studio    ? Color.FromArgb(255, 100, 100) :
                          label?.Label == IpLabelType.Whitelist  ? Color.FromArgb(100, 220, 120) :
                          hasOnline ? Color.FromArgb(248, 113, 113) : Theme.AccentBlue;
            e.Graphics.DrawString(badge + g.Ip, new Font("Consolas", 9, FontStyle.Bold),
                new SolidBrush(ipColor), e.Bounds.X + 6, e.Bounds.Y + 5);

            // 帳號數
            string info = $"  {g.TotalCount}帳";
            if (g.OnlineCount > 0) info += $" 🟢{g.OnlineCount}";
            e.Graphics.DrawString(info, Theme.FontCell9, new SolidBrush(Theme.TextSecondary),
                e.Bounds.X + 148, e.Bounds.Y + 5);
        }

        // ══════════════════════════════════════════════════════
        // 成員 DataGridView
        // ══════════════════════════════════════════════════════
        private void ShowGroupInDgv(IpGroupEntry g)
        {
            _lblGroupTitle.Text = $"IP：{g.Ip}   共 {g.TotalCount} 個帳號  {(g.OnlineCount > 0 ? $"（{g.OnlineCount} 在線）" : "")}";
            _dgvMembers.Rows.Clear();

            foreach (var m in g.Members)
            {
                int idx = _dgvMembers.Rows.Add(
                    m.IsOnline ? "在線" : "離線",
                    m.Account, m.CharName, m.MasterName,
                    m.LoginIp, m.RegIp
                );
                if (m.IsOnline)
                    _dgvMembers.Rows[idx].DefaultCellStyle.ForeColor = Color.FromArgb(22, 185, 122);
            }
        }

        private void ClearMemberDgv()
        {
            _dgvMembers.Rows.Clear();
            _lblGroupTitle.Text = "← 選擇左側群組查看成員";
        }

        // ══════════════════════════════════════════════════════
        // 複製 / 匯出
        // ══════════════════════════════════════════════════════
        private void CopyDgvSelection()
        {
            if (_dgvMembers.SelectedCells.Count == 0) return;
            var sb = new StringBuilder();
            int lastRow = -1;
            var cells = _dgvMembers.SelectedCells.Cast<DataGridViewCell>()
                .OrderBy(c => c.RowIndex).ThenBy(c => c.ColumnIndex).ToList();
            foreach (var cell in cells)
            {
                if (cell.RowIndex != lastRow && lastRow != -1) sb.AppendLine();
                else if (lastRow != -1) sb.Append('\t');
                sb.Append(cell.Value ?? "");
                lastRow = cell.RowIndex;
            }
            Clipboard.SetText(sb.ToString());
        }

        private void CopyDgvRows()
        {
            if (_dgvMembers.SelectedRows.Count == 0) return;
            var sb = new StringBuilder();
            sb.AppendLine(string.Join("\t", _dgvMembers.Columns.Cast<DataGridViewColumn>().Select(c => c.HeaderText)));
            foreach (DataGridViewRow row in _dgvMembers.SelectedRows)
                sb.AppendLine(string.Join("\t", row.Cells.Cast<DataGridViewCell>().Select(c => c.Value ?? "")));
            Clipboard.SetText(sb.ToString());
        }

        private void CopyDgvAll()
        {
            if (_dgvMembers.Rows.Count == 0) return;
            var sb = new StringBuilder();
            sb.AppendLine(string.Join("\t", _dgvMembers.Columns.Cast<DataGridViewColumn>().Select(c => c.HeaderText)));
            foreach (DataGridViewRow row in _dgvMembers.Rows)
                if (!row.IsNewRow)
                    sb.AppendLine(string.Join("\t", row.Cells.Cast<DataGridViewCell>().Select(c => c.Value ?? "")));
            Clipboard.SetText(sb.ToString());
        }

        private void DgvMembers_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            // 欄位 4 = 登入IP, 5 = 註冊IP
            if (e.ColumnIndex != 4 && e.ColumnIndex != 5) return;
            var ip = _dgvMembers.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString();
            if (!string.IsNullOrWhiteSpace(ip))
                _ = ShowIpOwnerAsync(ip);
        }

        private void QueryIpOwnerFromContext()
        {
            if (_dgvMembers.CurrentCell == null) return;
            int col = _dgvMembers.CurrentCell.ColumnIndex;
            int row = _dgvMembers.CurrentCell.RowIndex;
            if (row < 0) return;
            // 優先取登入IP，否則取任何有值的IP欄
            string? ip = null;
            if (col == 4 || col == 5)
                ip = _dgvMembers.Rows[row].Cells[col].Value?.ToString();
            ip ??= _dgvMembers.Rows[row].Cells[4].Value?.ToString();
            ip ??= _dgvMembers.Rows[row].Cells[5].Value?.ToString();
            if (!string.IsNullOrWhiteSpace(ip))
                _ = ShowIpOwnerAsync(ip);
        }

        private async Task ShowIpOwnerAsync(string ip)
        {
            var result = await DatabaseManager.Instance.GetIpOwnerAsync(ip);

            if (IsDisposed) return;

            if (result == null)
            {
                MessageBox.Show($"找不到使用過 {ip} 的帳號紀錄。", "查無資料",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var onlineTag = result.IsOnline ? "🟢 在線" : "⚫ 離線";
            var msg = new StringBuilder();
            msg.AppendLine($"IP：{ip}");
            msg.AppendLine();
            msg.AppendLine($"── 最早使用此IP的帳號（原始主人）──");
            msg.AppendLine();
            msg.AppendLine($"  帳號：    {result.Account}");
            msg.AppendLine($"  角色名：  {result.CharName}");
            msg.AppendLine($"  主帳號：  {result.MasterName}");
            msg.AppendLine($"  狀態：    {onlineTag}");
            msg.AppendLine($"  最早時間：{result.RegTime}");
            msg.AppendLine($"  命中方式：{result.MatchType}");
            msg.AppendLine();
            msg.AppendLine($"  登入IP：  {result.LoginIp}");
            msg.AppendLine($"  註冊IP：  {result.RegIp}");

            var dlg = new Form
            {
                Text = $"IP 原始主人查詢 — {ip}",
                Width = 420, Height = 300,
                BackColor = Theme.BgCard,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontBody,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false, MinimizeBox = false
            };

            var txtResult = new TextBox
            {
                Multiline = true, ReadOnly = true, WordWrap = false,
                Dock = DockStyle.Fill, BackColor = Theme.BgCard,
                ForeColor = Theme.TextPrimary, Font = new Font("Consolas", 10),
                BorderStyle = BorderStyle.None, ScrollBars = ScrollBars.Both,
                Text = msg.ToString()
            };
            dlg.Controls.Add(txtResult);

            var btnCopy = new Button
            {
                Text = "📋 複製", Dock = DockStyle.Bottom, Height = 32,
                BackColor = Theme.AccentBlue, ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, Font = Theme.FontCell9Bold
            };
            btnCopy.FlatAppearance.BorderSize = 0;
            btnCopy.Click += (s, e) => { Clipboard.SetText(msg.ToString()); btnCopy.Text = "✅ 已複製"; };
            dlg.Controls.Add(btnCopy);

            dlg.ShowDialog(FindForm());
        }

        private void BtnExport_Click(object? sender, EventArgs e)
        {
            if (_groups.Count == 0) return;
            using var dlg = new SaveFileDialog
            {
                Filter = "CSV 檔案|*.csv",
                FileName = $"重複IP全部_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                DefaultExt = "csv"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("群組IP,在線數,總帳號數,帳號,角色名,主帳號,登入IP,註冊IP,狀態,工作室標記");
                foreach (var g in _groups)
                {
                    _ipLabels.TryGetValue(g.Ip, out var lbl);
                    string tag = lbl == null ? "" : lbl.Label == IpLabelType.Studio ? "工作室" : "白名單";
                    foreach (var m in g.Members)
                        sb.AppendLine($"{CsvEsc(g.Ip)},{g.OnlineCount},{g.TotalCount},{CsvEsc(m.Account)},{CsvEsc(m.CharName)},{CsvEsc(m.MasterName)},{CsvEsc(m.LoginIp)},{CsvEsc(m.RegIp)},{(m.IsOnline ? "在線" : "離線")},{tag}");
                    sb.AppendLine(); // 群組間空行
                }
                File.WriteAllText(dlg.FileName, "\uFEFF" + sb, Encoding.UTF8);
                MessageBox.Show($"已匯出 {_groups.Count} 個群組到：\n{dlg.FileName}", "匯出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show("匯出失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void ExportSelectedGroup()
        {
            if (_selected == null) { MessageBox.Show("請先選擇左側群組"); return; }
            using var dlg = new SaveFileDialog
            {
                Filter = "CSV 檔案|*.csv",
                FileName = $"IP_{_selected.Ip.Replace(".", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                DefaultExt = "csv"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"# IP：{_selected.Ip}  共 {_selected.TotalCount} 帳號  在線 {_selected.OnlineCount} 人");
                sb.AppendLine("帳號,角色名,主帳號,登入IP,註冊IP,狀態");
                foreach (var m in _selected.Members)
                    sb.AppendLine($"{CsvEsc(m.Account)},{CsvEsc(m.CharName)},{CsvEsc(m.MasterName)},{CsvEsc(m.LoginIp)},{CsvEsc(m.RegIp)},{(m.IsOnline ? "在線" : "離線")}");
                File.WriteAllText(dlg.FileName, "\uFEFF" + sb, Encoding.UTF8);
                MessageBox.Show($"已匯出 {_selected.Members.Count} 筆到：\n{dlg.FileName}", "匯出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show("匯出失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private static string CsvEsc(string v)
            => v.Contains(',') || v.Contains('"') || v.Contains('\n')
               ? $"\"{v.Replace("\"", "\"\"")}\"" : v;

        // ══════════════════════════════════════════════════════
        // 工廠方法
        // ══════════════════════════════════════════════════════
        private static DataGridView BuildDgv()
        {
            var dgv = new DataGridView
            {
                Dock = DockStyle.Fill, BackgroundColor = Theme.BgPage,
                BorderStyle = BorderStyle.None, GridColor = Color.FromArgb(55, 63, 80),
                RowHeadersVisible = false, AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                MultiSelect = true, ReadOnly = true, AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText
            };
            dgv.EnableHeadersVisualStyles = false;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(40, 47, 62);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Theme.TextSecondary;
            dgv.ColumnHeadersDefaultCellStyle.Font      = Theme.FontCell9Bold;
            dgv.DefaultCellStyle.BackColor              = Theme.BgCard;
            dgv.DefaultCellStyle.ForeColor              = Theme.TextPrimary;
            dgv.DefaultCellStyle.Font                   = Theme.FontCell9;
            dgv.DefaultCellStyle.SelectionBackColor     = Color.FromArgb(60, 80, 140);
            dgv.DefaultCellStyle.SelectionForeColor     = Color.White;
            dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(48, 55, 72);
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "狀態",    FillWeight = 40,  MinimumWidth = 45,  Name = "status",   ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "帳號",    FillWeight = 120, MinimumWidth = 80,  Name = "account",  ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "角色名",  FillWeight = 100, MinimumWidth = 70,  Name = "charName", ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "主帳號",  FillWeight = 100, MinimumWidth = 70,  Name = "master",   ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "登入 IP", FillWeight = 110, MinimumWidth = 80,  Name = "loginIp",  ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "註冊 IP", FillWeight = 110, MinimumWidth = 80,  Name = "regIp",    ReadOnly = true });
            return dgv;
        }

        private static Button MakeBtn(string text, Color bg, int w) => new Button
        {
            Text = text, Width = w, Height = 30, BackColor = bg, ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Font = Theme.FontCell9Bold,
            FlatAppearance = { BorderSize = 0 }
        };

        private static Label MakeLabel(string text) => new Label
        {
            Text = text, AutoSize = true, ForeColor = Theme.TextSecondary,
            Font = Theme.FontCell9, Margin = new Padding(0, 6, 4, 0)
        };

        private static string ShowInputBox(string prompt, string title, string defaultValue = "")
        {
            using var dlg = new Form();
            dlg.Text = title; dlg.Size = new Size(380, 150);
            dlg.BackColor = Theme.BgPage; dlg.ForeColor = Theme.TextPrimary;
            dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
            dlg.StartPosition = FormStartPosition.CenterParent;
            dlg.MaximizeBox = false;
            var lbl = new Label { Text = prompt, ForeColor = Theme.TextSecondary, Font = Theme.FontCell9, AutoSize = true, Location = new Point(12, 12) };
            var tb  = new TextBox { Text = defaultValue, Width = 340, Location = new Point(12, 42), BackColor = Theme.BgInput, ForeColor = Theme.TextPrimary, BorderStyle = BorderStyle.FixedSingle };
            var ok  = Theme.MakePrimaryButton("確認", 80, 28); ok.Location = new Point(196, 74);
            var can = Theme.MakeButton("取消", Theme.BgLight, Theme.TextSecondary, 70, 28); can.Location = new Point(284, 74);
            ok.Click  += (_, __) => dlg.DialogResult = DialogResult.OK;
            can.Click += (_, __) => dlg.DialogResult = DialogResult.Cancel;
            dlg.Controls.AddRange(new Control[] { lbl, tb, ok, can });
            dlg.AcceptButton = ok; dlg.CancelButton = can;
            return dlg.ShowDialog() == DialogResult.OK ? tb.Text : defaultValue;
        }
    }
}
