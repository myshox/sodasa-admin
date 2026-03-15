using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    internal class GuildForm : UserControl
    {
        // ── 資料 ──────────────────────────────────────────────
        private List<FamilyInfo>   _families = new();
        private List<FamilyMember> _members  = new();
        private FamilyInfo?        _selected;

        // ── 控件 ──────────────────────────────────────────────
        private DataGridView _dgvFamily;
        private DataGridView _dgvMember;
        private Label        _lblFamilyStatus;
        private Label        _lblMemberStatus;
        private TextBox      _txtSearch;
        private Button       _btnRefresh;
        private Button       _btnDissolve;
        private Button       _btnKick;
        private Button       _btnTransfer;
        private Button       _btnAddMember;
        private Label        _lblFamilyName;
        private Panel        _detailPanel;

        public GuildForm()
        {
            BuildUI();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _ = LoadFamiliesAsync();
        }

        public void TriggerLoad() => _ = LoadFamiliesAsync();

        // ══════════════════════════════════════════════════════════
        // UI 建置
        // ══════════════════════════════════════════════════════════
        private void BuildUI()
        {
            Dock      = DockStyle.Fill;
            BackColor = Theme.BgPage;
            Font      = Theme.FontBody;

            // ── Root TableLayout: 標題列 + 內容區 ──
            var root = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 2,
                BackColor   = Color.Transparent,
                Padding     = new Padding(0)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            // ── 標題列 ──
            var header = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.FromArgb(28, 30, 38),
                Padding   = new Padding(16, 0, 12, 0)
            };
            root.Controls.Add(header, 0, 0);

            var lblTitle = new Label
            {
                Text      = "\u5BB6\u65CF\u7BA1\u7406",
                Font      = new Font(Theme.FontBody.FontFamily, 15, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize  = true,
                Anchor    = AnchorStyles.Left | AnchorStyles.Top
            };
            lblTitle.Location = new Point(16, 14);
            header.Controls.Add(lblTitle);

            // 搜尋
            _txtSearch = new TextBox
            {
                PlaceholderText = "\u641c\u5c0b\u5bb6\u65cf\u540d\u7a31...",
                Width     = 180,
                Height    = 28,
                BackColor = Theme.BgInput,
                ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Anchor    = AnchorStyles.Right | AnchorStyles.Top
            };
            _txtSearch.Location = new Point(header.Width - 350, 14);
            _txtSearch.Anchor   = AnchorStyles.Right | AnchorStyles.Top;
            _txtSearch.TextChanged += (s, e) => FilterFamilies();
            header.Controls.Add(_txtSearch);

            _btnRefresh = MakeBtn("\u91CD\u65B0\u8F09\u5165", Theme.AccentBlue);
            _btnRefresh.Anchor   = AnchorStyles.Right | AnchorStyles.Top;
            _btnRefresh.Width    = 90;
            _btnRefresh.Location = new Point(header.Width - 160, 13);
            _btnRefresh.Anchor   = AnchorStyles.Right | AnchorStyles.Top;
            _btnRefresh.Click   += async (s, e) => await LoadFamiliesAsync();
            header.Controls.Add(_btnRefresh);

            // ── 內容：左（家族列表）+ 右（成員列表）──
            var split = new SplitContainer
            {
                Dock        = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                BackColor   = Theme.BgPage,
                BorderStyle = BorderStyle.None
            };
            root.Controls.Add(split, 0, 1);
            split.Layout += (s, e) =>
            {
                if (split.Width > 100)
                    split.SplitterDistance = Math.Max(split.Panel1MinSize, Math.Min(split.Width * 2 / 5, split.Width - split.Panel2MinSize));
            };

            // ── 左：家族列表 Panel ──
            var leftPanel = new TableLayoutPanel
            {
                Dock      = DockStyle.Fill,
                ColumnCount = 1,
                RowCount  = 3,
                BackColor = Color.Transparent,
                Padding   = new Padding(8, 8, 4, 8)
            };
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            split.Panel1.Controls.Add(leftPanel);

            var lblLeft = new Label
            {
                Text      = "\u5BB6\u65CF\u5217\u8868",
                Font      = Theme.FontCell9Bold,
                ForeColor = Theme.TextSecondary,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            leftPanel.Controls.Add(lblLeft, 0, 0);

            _dgvFamily = BuildDgv();
            _dgvFamily.Columns.Add(new DataGridViewTextBoxColumn { Name = "C_FId",   HeaderText = "ID",    Width = 45,  ReadOnly = true });
            _dgvFamily.Columns.Add(new DataGridViewTextBoxColumn { Name = "C_FName", HeaderText = "\u5BB6\u65CF\u540D\u7A31", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            _dgvFamily.Columns.Add(new DataGridViewTextBoxColumn { Name = "C_FCnt",  HeaderText = "\u4EBA\u6578",  Width = 50,  ReadOnly = true });
            _dgvFamily.Columns.Add(new DataGridViewTextBoxColumn { Name = "C_FShop", HeaderText = "\u8CA2\u737B",  Width = 80,  ReadOnly = true });
            _dgvFamily.Columns.Add(new DataGridViewTextBoxColumn { Name = "C_FLast", HeaderText = "\u6700\u8FD1\u6D3B\u52D5", Width = 120, ReadOnly = true });
            _dgvFamily.SelectionChanged += DgvFamily_SelectionChanged;
            _dgvFamily.CellDoubleClick  += (s, e) => { if (e.RowIndex >= 0) DgvFamily_SelectionChanged(s, e); };
            leftPanel.Controls.Add(_dgvFamily, 0, 1);

            _lblFamilyStatus = new Label
            {
                Text      = "",
                ForeColor = Theme.TextSecondary,
                Font      = Theme.FontSmall,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            leftPanel.Controls.Add(_lblFamilyStatus, 0, 2);

            // ── 右：成員列表 Panel ──
            _detailPanel = new TableLayoutPanel
            {
                Dock      = DockStyle.Fill,
                ColumnCount = 1,
                RowCount  = 4,
                BackColor = Color.Transparent,
                Padding   = new Padding(4, 8, 8, 8)
            };
            var detailLayout = (TableLayoutPanel)_detailPanel;
            detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            detailLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            split.Panel2.Controls.Add(_detailPanel);

            // 標題（家族名）
            _lblFamilyName = new Label
            {
                Text      = "\u8ACB\u5148\u9078\u64C7\u5DE6\u4E2D\u4E00\u500B\u5BB6\u65CF",
                Font      = new Font(Theme.FontBody.FontFamily, 11, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            detailLayout.Controls.Add(_lblFamilyName, 0, 0);

            // 操作按鈕列
            var btnBar = new FlowLayoutPanel
            {
                Dock        = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor   = Color.Transparent,
                Padding     = new Padding(0),
                WrapContents = false
            };
            detailLayout.Controls.Add(btnBar, 0, 1);

            _btnDissolve = MakeBtn("\u89E3\u6563\u5BB6\u65CF", Color.FromArgb(220, 50, 47));
            _btnDissolve.Width   = 100;
            _btnDissolve.Enabled = false;
            _btnDissolve.Click  += BtnDissolve_Click;
            btnBar.Controls.Add(_btnDissolve);

            var sep = new Panel { Width = 8, Height = 1, BackColor = Color.Transparent };
            btnBar.Controls.Add(sep);

            _btnKick = MakeBtn("\u8E22\u9664\u9078\u4E2D\u6210\u54E1", Color.FromArgb(181, 94, 0));
            _btnKick.Width   = 120;
            _btnKick.Enabled = false;
            _btnKick.Click  += BtnKick_Click;
            btnBar.Controls.Add(_btnKick);

            var sep2 = new Panel { Width = 8, Height = 1, BackColor = Color.Transparent };
            btnBar.Controls.Add(sep2);

            _btnTransfer = MakeBtn("\u8F49\u79FB\u81F3\u5176\u4ED6\u5BB6\u65CF", Color.FromArgb(32, 128, 230));
            _btnTransfer.Width   = 130;
            _btnTransfer.Enabled = false;
            _btnTransfer.Click  += BtnTransfer_Click;
            btnBar.Controls.Add(_btnTransfer);

            var sep3 = new Panel { Width = 8, Height = 1, BackColor = Color.Transparent };
            btnBar.Controls.Add(sep3);

            var btnSetLeader = MakeBtn("\u8a2d\u70ba\u65cf\u9577", Color.FromArgb(100, 60, 180));
            btnSetLeader.Width   = 90;
            btnSetLeader.Enabled = false;
            btnSetLeader.Name    = "btnSetLeader";
            btnSetLeader.Click  += BtnSetLeader_Click;
            btnBar.Controls.Add(btnSetLeader);

            var sep4 = new Panel { Width = 8, Height = 1, BackColor = Color.Transparent };
            btnBar.Controls.Add(sep4);

            var btnSetElder = MakeBtn("\u8a2d\u70ba\u9577\u8001", Color.FromArgb(60, 110, 60));
            btnSetElder.Width   = 90;
            btnSetElder.Enabled = false;
            btnSetElder.Name    = "btnSetElder";
            btnSetElder.Click  += (s, e) => _ = SetRoleAsync(2);
            btnBar.Controls.Add(btnSetElder);

            var sep5 = new Panel { Width = 8, Height = 1, BackColor = Color.Transparent };
            btnBar.Controls.Add(sep5);

            var btnSetMember = MakeBtn("恢復成員", Color.FromArgb(70, 72, 80));
            btnSetMember.Width   = 90;
            btnSetMember.Enabled = false;
            btnSetMember.Name    = "btnSetMember";
            btnSetMember.Click  += (s, e) => _ = SetRoleAsync(0);
            btnBar.Controls.Add(btnSetMember);

            var sep6 = new Panel { Width = 16, Height = 1, BackColor = Color.Transparent };
            btnBar.Controls.Add(sep6);

            _btnAddMember = MakeBtn("＋ 新增成員", Color.FromArgb(10, 140, 80));
            _btnAddMember.Width   = 110;
            _btnAddMember.Enabled = false;
            _btnAddMember.Click  += BtnAddMember_Click;
            btnBar.Controls.Add(_btnAddMember);

            // 成員 DataGridView 右鍵選單
            var ctxMenu = new ContextMenuStrip { BackColor = Theme.BgCard, ForeColor = Theme.TextPrimary };
            var menuSetLeader = new ToolStripMenuItem("\u2654 \u8a2d\u70ba\u65cf\u9577") { BackColor = Theme.BgCard, ForeColor = Color.FromArgb(200, 160, 255) };
            var menuSetElder  = new ToolStripMenuItem("\u2605 \u8a2d\u70ba\u9577\u8001")  { BackColor = Theme.BgCard, ForeColor = Color.FromArgb(130, 210, 130) };
            var menuSetMember = new ToolStripMenuItem("\u25a1 \u6062\u5fa9\u6210\u54e1")  { BackColor = Theme.BgCard, ForeColor = Color.FromArgb(180, 180, 180) };
            var menuKick      = new ToolStripMenuItem("\u274c \u8e22\u9664\u6210\u54e1")  { BackColor = Theme.BgCard, ForeColor = Color.FromArgb(255, 120, 100) };
            var menuTransfer  = new ToolStripMenuItem("\u27a1 \u8f49\u79fb\u81f3\u5176\u4ed6\u5bb6\u65cf") { BackColor = Theme.BgCard, ForeColor = Color.FromArgb(100, 180, 255) };
            menuSetLeader.Click += BtnSetLeader_Click;
            menuSetElder.Click  += (s, e) => _ = SetRoleAsync(2);
            menuSetMember.Click += (s, e) => _ = SetRoleAsync(0);
            menuKick.Click      += BtnKick_Click;
            menuTransfer.Click  += BtnTransfer_Click;
            ctxMenu.Items.AddRange(new ToolStripItem[] { menuSetLeader, menuSetElder, menuSetMember, new ToolStripSeparator(), menuKick, new ToolStripSeparator(), menuTransfer });

            // 成員 DataGridView
            _dgvMember = BuildDgv();
            _dgvMember.ContextMenuStrip = ctxMenu;
            _dgvMember.Columns.Add(new DataGridViewTextBoxColumn { Name = "C_MRole",   HeaderText = "\u8077\u4f4d",   Width = 55,  ReadOnly = true });
            _dgvMember.Columns.Add(new DataGridViewTextBoxColumn { Name = "C_MOnline", HeaderText = "\u7DDA\u4E0A",   Width = 45,  ReadOnly = true });
            _dgvMember.Columns.Add(new DataGridViewTextBoxColumn { Name = "C_MName",   HeaderText = "\u89D2\u8272\u540D", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            _dgvMember.Columns.Add(new DataGridViewTextBoxColumn { Name = "C_MCdkey",  HeaderText = "\u5E33\u865F",   Width = 140, ReadOnly = true });
            _dgvMember.Columns.Add(new DataGridViewTextBoxColumn { Name = "C_MShop",   HeaderText = "\u8CA2\u737B",   Width = 80,  ReadOnly = true });
            _dgvMember.Columns.Add(new DataGridViewTextBoxColumn { Name = "C_MPay",    HeaderText = "\u7D2F\u8A08\u5132\u5024", Width = 75, ReadOnly = true });
            _dgvMember.Columns.Add(new DataGridViewTextBoxColumn { Name = "C_MJoin",   HeaderText = "\u52A0\u5165\u6642\u9593", Width = 118, ReadOnly = true });
            _dgvMember.SelectionChanged += (s, e) =>
            {
                bool hasSel  = _dgvMember.SelectedRows.Count > 0;
                bool oneOnly = _dgvMember.SelectedRows.Count == 1;
                _btnKick.Enabled     = hasSel && _selected != null;
                _btnTransfer.Enabled = _selected != null && _families.Count > 1;
                bool roleOk = oneOnly && _selected != null;
                foreach (string n in new[] { "btnSetLeader", "btnSetElder", "btnSetMember" })
                {
                    var b = btnBar.Controls[n] as Button;
                    if (b != null) b.Enabled = roleOk;
                }
                menuSetLeader.Enabled = roleOk;
                menuSetElder.Enabled  = roleOk;
                menuSetMember.Enabled = roleOk;
                menuKick.Enabled      = hasSel && _selected != null;
                menuTransfer.Enabled  = _selected != null && _families.Count > 1;
            };
            detailLayout.Controls.Add(_dgvMember, 0, 2);

            _lblMemberStatus = new Label
            {
                Text      = "",
                ForeColor = Theme.TextSecondary,
                Font      = Theme.FontSmall,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            detailLayout.Controls.Add(_lblMemberStatus, 0, 3);
        }

        // ══════════════════════════════════════════════════════════
        // 資料載入
        // ══════════════════════════════════════════════════════════
        private async Task LoadFamiliesAsync()
        {
            if (IsDisposed) return;
            if (!DatabaseManager.Instance.IsConnected)
            {
                if (_lblFamilyStatus != null)
                    _lblFamilyStatus.Text = "\u672a\u9023\u7dda\u8cc7\u6599\u5eab";
                return;
            }
            _btnRefresh.Enabled   = false;
            _lblFamilyStatus.Text = "\u8F09\u5165\u4E2D...";
            try
            {
                _families = await DatabaseManager.Instance.GetFamilyListAsync();
                if (IsDisposed) return;
                FilterFamilies();
                if (_families.Count == 0)
                    _lblFamilyStatus.Text = "\u76ee\u524d\u7121\u5bb6\u65cf\u8cc7\u6599\uff08zuzhanlog \u70ba\u7a7a\uff09";
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                    _lblFamilyStatus.Text = "\u8F09\u5165\u5931\u6557\uFF1A" + ex.Message;
            }
            finally { if (!IsDisposed) _btnRefresh.Enabled = true; }
        }

        private void FilterFamilies()
        {
            string kw = _txtSearch.Text.Trim().ToLower();
            var filtered = string.IsNullOrEmpty(kw)
                ? _families
                : _families.Where(f => f.FamilyName.ToLower().Contains(kw) || f.FamilyId.ToString().Contains(kw)).ToList();

            _dgvFamily.SuspendLayout();
            _dgvFamily.Rows.Clear();
            foreach (var f in filtered)
            {
                int i = _dgvFamily.Rows.Add();
                var row = _dgvFamily.Rows[i];
                row.Tag = f;
                row.Cells["C_FId"].Value   = f.FamilyId;
                row.Cells["C_FName"].Value = f.FamilyName;
                row.Cells["C_FCnt"].Value  = f.MemberCount;
                row.Cells["C_FShop"].Value = f.ShopContrib > 0 ? f.ShopContrib.ToString("N0") : "-";
                row.Cells["C_FLast"].Value = f.LastActive;
            }
            _dgvFamily.ResumeLayout();
            _lblFamilyStatus.Text = $"\u5171 {filtered.Count} \u500B\u5BB6\u65CF";
        }

        private async Task LoadMembersAsync(int familyId)
        {
            _lblMemberStatus.Text = "\u8F09\u5165\u4E2D...";
            _dgvMember.Rows.Clear();
            _btnKick.Enabled     = false;
            _btnTransfer.Enabled = false;
            try
            {
                _members = await DatabaseManager.Instance.GetFamilyMembersAsync(familyId);
                _dgvMember.SuspendLayout();
                foreach (var m in _members)
                {
                    int i = _dgvMember.Rows.Add();
                    var row = _dgvMember.Rows[i];
                    row.Tag = m;
                    row.Cells["C_MRole"].Value   = m.RoleLabel;
                    row.Cells["C_MOnline"].Value = m.IsOnline ? "\u2022 \u7DDA\u4E0A" : "\u96E2\u7DDA";
                    row.Cells["C_MName"].Value   = string.IsNullOrEmpty(m.CharName) ? m.OnlineName : m.CharName;
                    row.Cells["C_MCdkey"].Value  = m.Cdkey;
                    row.Cells["C_MShop"].Value   = m.ShopContrib > 0 ? m.ShopContrib.ToString("N0") : "-";
                    row.Cells["C_MPay"].Value    = m.PayTotal > 0 ? m.PayTotal.ToString("N0") : "-";
                    row.Cells["C_MJoin"].Value   = m.JoinTime;

                    if (m.Role == 1)
                        row.DefaultCellStyle.ForeColor = Color.FromArgb(220, 180, 255);  // 族長：紫色
                    else if (m.Role == 2)
                        row.DefaultCellStyle.ForeColor = Color.FromArgb(130, 210, 130);  // 長老：綠色
                    else if (m.IsOnline)
                        row.DefaultCellStyle.ForeColor = Color.FromArgb(80, 220, 100);   // 在線：亮綠
                }
                _dgvMember.ResumeLayout();
                _lblMemberStatus.Text = $"\u5171 {_members.Count} \u4EBA";
            }
            catch (Exception ex)
            {
                _lblMemberStatus.Text = "\u8F09\u5165\u5931\u6557\uFF1A" + ex.Message;
            }
        }

        // ══════════════════════════════════════════════════════════
        // 事件處理
        // ══════════════════════════════════════════════════════════
        private async void DgvFamily_SelectionChanged(object? sender, EventArgs e)
        {
            if (_dgvFamily.SelectedRows.Count == 0) return;
            var row = _dgvFamily.SelectedRows[0];
            if (row.Tag is not FamilyInfo fi) return;
            _selected             = fi;
            _lblFamilyName.Text   = $"「{fi.FamilyName}」  ID: {fi.FamilyId}  人數: {fi.MemberCount}";
            _btnDissolve.Enabled  = true;
            _btnTransfer.Enabled  = _families.Count > 1;
            _btnAddMember.Enabled = true;
            await LoadMembersAsync(fi.FamilyId);
        }

        private async void BtnSetLeader_Click(object? sender, EventArgs e)
        {
            if (_selected == null || _dgvMember.SelectedRows.Count != 1) return;
            var row = _dgvMember.SelectedRows[0];
            if (row.Tag is not FamilyMember m) return;

            string name = string.IsNullOrEmpty(m.CharName) ? m.Cdkey : m.CharName;
            if (m.Role == 1)
            {
                MessageBox.Show($"\u300c{name}\u300d \u5df2\u7d93\u662f\u65cf\u9577\u4e86\u3002", "\u63d0\u793a", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var ans = MessageBox.Show(
                $"\u78ba\u5b9a\u8981\u5c07\u300c{name}\u300d\u8a2d\u70ba\u5bb6\u65cf\u300c{_selected.FamilyName}\u300d\u7684\u65b0\u65cf\u9577\uff1f\n\u539f\u65cf\u9577\u5c07\u8b8a\u6210\u4e00\u822c\u6210\u54e1\u3002",
                "\u65cf\u9577\u8f49\u79fb", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (ans != DialogResult.Yes) return;

            var (ok, msg) = await DatabaseManager.Instance.SetFamilyRoleAsync(_selected.FamilyId, m.Cdkey, 1);
            MessageBox.Show(msg, ok ? "\u6210\u529f" : "\u5931\u6557",
                MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            if (ok) await LoadMembersAsync(_selected.FamilyId);
        }

        private async Task SetRoleAsync(int role)
        {
            if (_selected == null || _dgvMember.SelectedRows.Count != 1) return;
            var row = _dgvMember.SelectedRows[0];
            if (row.Tag is not FamilyMember m) return;

            string name      = string.IsNullOrEmpty(m.CharName) ? m.Cdkey : m.CharName;
            string roleLabel = role == 1 ? "\u65cf\u9577" : role == 2 ? "\u9577\u8001" : "\u4e00\u822c\u6210\u54e1";
            if (m.Role == role)
            {
                MessageBox.Show($"\u300c{name}\u300d \u5df2\u7d93\u662f{roleLabel}\u4e86\u3002", "\u63d0\u793a", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var ans = MessageBox.Show(
                $"\u78ba\u5b9a\u8981\u5c07\u300c{name}\u300d\u7684\u8077\u4f4d\u6539\u70ba\u300c{roleLabel}\u300d\uff1f",
                "\u66f4\u6539\u8077\u4f4d", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (ans != DialogResult.Yes) return;

            var (ok, msg) = await DatabaseManager.Instance.SetFamilyRoleAsync(_selected.FamilyId, m.Cdkey, role);
            MessageBox.Show(msg, ok ? "\u6210\u529f" : "\u5931\u6557",
                MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            if (ok) await LoadMembersAsync(_selected.FamilyId);
        }

        private async void BtnDissolve_Click(object? sender, EventArgs e)
        {
            if (_selected == null) return;
            var ans = MessageBox.Show(
                $"\u78BA\u5B9A\u8981\u89E3\u6563\u5BB6\u65CF\u300C{_selected.FamilyName}\u300D\uFF1F\n\u6B64\u64CD\u4F5C\u5C07\u522A\u9664\u6240\u6709\u6210\u54E1\u7684\u5BB6\u65CF\u6230\u8A18\u9304\uFF0C\u4E14\u4E0D\u53EF\u5FA9\u539F\u3002",
                "\u89E3\u6563\u5BB6\u65CF", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (ans != DialogResult.Yes) return;

            _btnDissolve.Enabled = false;
            var (ok, msg) = await DatabaseManager.Instance.DissolveFamilyAsync(_selected.FamilyId);
            MessageBox.Show(msg, ok ? "\u6210\u529F" : "\u5931\u6557",
                MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);

            if (ok) await LoadFamiliesAsync();
            _btnDissolve.Enabled = true;
        }

        private async void BtnKick_Click(object? sender, EventArgs e)
        {
            if (_selected == null || _dgvMember.SelectedRows.Count == 0) return;
            var members = _dgvMember.SelectedRows.Cast<DataGridViewRow>()
                .Where(r => r.Tag is FamilyMember)
                .Select(r => (FamilyMember)r.Tag!)
                .ToList();
            if (members.Count == 0) return;

            string names = string.Join(", ", members.Select(m => m.CharName.Length > 0 ? m.CharName : m.Cdkey));
            var ans = MessageBox.Show(
                $"\u78BA\u5B9A\u8981\u5F9E\u5BB6\u65CF\u300C{_selected.FamilyName}\u300D\u8E22\u9664\u9019\u4E9B\u6210\u54E1\uFF1F\n{names}",
                "\u8E22\u9664\u6210\u54E1", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (ans != DialogResult.Yes) return;

            _btnKick.Enabled = false;
            int success = 0, fail = 0;
            foreach (var m in members)
            {
                var (ok, _) = await DatabaseManager.Instance.KickFamilyMemberAsync(_selected.FamilyId, m.Cdkey);
                if (ok) success++; else fail++;
            }
            MessageBox.Show($"\u8E22\u9664\u5B8C\u6210\uFF1A{success} \u6210\u529F / {fail} \u5931\u6557",
                "\u8E22\u9664\u7D50\u679C", MessageBoxButtons.OK, MessageBoxIcon.Information);

            if (success > 0) await LoadMembersAsync(_selected.FamilyId);
            _btnKick.Enabled = _dgvMember.SelectedRows.Count > 0;
        }

        private async void BtnTransfer_Click(object? sender, EventArgs e)
        {
            if (_selected == null) return;

            // 若沒選成員 → 詢問是否轉移整個家族
            var members = _dgvMember.SelectedRows.Count > 0
                ? _dgvMember.SelectedRows.Cast<DataGridViewRow>()
                    .Where(r => r.Tag is FamilyMember)
                    .Select(r => (FamilyMember)r.Tag!)
                    .ToList()
                : new List<FamilyMember>();

            if (members.Count == 0)
            {
                // 沒選成員：詢問是否轉移全部
                var ans = MessageBox.Show(
                    $"\u60a8\u6c92\u6709\u9078\u64c7\u6210\u54e1\u3002\n\u662f\u5426\u8981\u5c07\u5bb6\u65cf\u300c{_selected.FamilyName}\u300d\u7684\u5168\u90e8 {_members.Count} \u4f4d\u6210\u54e1\u8f49\u79fb\u81f3\u5176\u4ed6\u5bb6\u65cf\uff1f",
                    "\u8f49\u79fb\u5168\u90e8\u6210\u54e1", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (ans != DialogResult.Yes) return;
                members = new List<FamilyMember>(_members);
            }

            if (members.Count == 0)
            {
                MessageBox.Show("\u6b64\u5bb6\u65cf\u6c92\u6709\u6210\u54e1\u53ef\u8f49\u79fb\u3002", "\u63d0\u793a", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 選擇目標家族
            var targets = _families.Where(f => f.FamilyId != _selected.FamilyId).ToList();
            if (targets.Count == 0)
            {
                MessageBox.Show("\u6c92\u6709\u5176\u4ed6\u5bb6\u65cf\u53ef\u4ee5\u8f49\u79fb\u3002\u8acb\u5148\u78ba\u4fdd\u5b58\u5728\u5169\u500b\u4ee5\u4e0a\u5bb6\u65cf\u3002", "\u63d0\u793a", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dlg = new TransferFamilyDialog(targets, members);
            if (dlg.ShowDialog() != DialogResult.OK) return;

            _btnTransfer.Enabled = false;
            int success = 0, fail = 0;
            var failMsgs = new System.Text.StringBuilder();
            foreach (var m in members)
            {
                var (ok, msg) = await DatabaseManager.Instance.TransferMemberAsync(m.Cdkey, dlg.TargetFamilyId, dlg.TargetFamilyName);
                if (ok) success++;
                else { fail++; failMsgs.AppendLine($"• {m.CharName}（{m.Cdkey}）: {msg}"); }
            }
            string resultMsg = $"轉移完成：{success} 成功 / {fail} 失敗";
            if (fail > 0) resultMsg += $"\n\n失敗原因：\n{failMsgs}";
            MessageBox.Show(resultMsg, "轉移結果", MessageBoxButtons.OK,
                fail > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

            if (success > 0)
            {
                await LoadFamiliesAsync();
                await LoadMembersAsync(_selected.FamilyId);
            }
            _btnTransfer.Enabled = _selected != null && _families.Count > 1;
        }

        // ══════════════════════════════════════════════════════════
        // 輔助
        // ══════════════════════════════════════════════════════════
        private DataGridView BuildDgv()
        {
            var dgv = new DataGridView
            {
                Dock                  = DockStyle.Fill,
                BackgroundColor       = Theme.BgCard,
                GridColor             = Color.FromArgb(50, 52, 60),
                BorderStyle           = BorderStyle.None,
                CellBorderStyle       = DataGridViewCellBorderStyle.SingleHorizontal,
                RowHeadersVisible     = false,
                ColumnHeadersVisible  = true,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect           = true,
                ReadOnly              = true,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.None,
                ScrollBars            = ScrollBars.Both,
                DefaultCellStyle      = new DataGridViewCellStyle
                {
                    BackColor    = Theme.BgCard,
                    ForeColor    = Theme.TextPrimary,
                    SelectionBackColor = Color.FromArgb(50, 100, 200),
                    SelectionForeColor = Color.White,
                    Font         = Theme.FontCell9,
                    Padding      = new Padding(4, 2, 4, 2)
                },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor    = Color.FromArgb(35, 37, 48),
                    ForeColor    = Theme.TextSecondary,
                    Font         = Theme.FontCell9Bold,
                    Alignment    = DataGridViewContentAlignment.MiddleLeft,
                    Padding      = new Padding(4, 0, 4, 0)
                },
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing,
                ColumnHeadersHeight = 28,
                RowTemplate         = { Height = 26 }
            };
            dgv.EnableHeadersVisualStyles = false;
            return dgv;
        }

        private async void BtnAddMember_Click(object? sender, EventArgs e)
        {
            if (_selected == null) return;
            using var dlg = new AddMemberDialog(_selected.FamilyId, _selected.FamilyName);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            var (cdkey, charName, role) = dlg.Result;
            var db = DatabaseManager.Instance;
            var (ok, msg) = await db.AddFamilyMemberAsync(_selected.FamilyId, _selected.FamilyName, cdkey, charName, role);
            if (ok)
            {
                _lblMemberStatus.Text = msg;
                await LoadMembersAsync(_selected.FamilyId);
                await LoadFamiliesAsync();
            }
            else
            {
                MessageBox.Show(msg, "新增失敗", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static Button MakeBtn(string text, Color bg)
        {
            return new Button
            {
                Text      = text,
                Height    = 30,
                BackColor = bg,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = Theme.FontCell9Bold,
                Cursor    = Cursors.Hand,
                FlatAppearance = { BorderSize = 0 }
            };
        }
    }

    // ══════════════════════════════════════════════════════════
    // 轉移家族選擇對話框
    // ══════════════════════════════════════════════════════════
    internal class TransferFamilyDialog : Form
    {
        public int    TargetFamilyId   { get; private set; }
        public string TargetFamilyName { get; private set; } = "";

        public TransferFamilyDialog(List<FamilyInfo> targets, List<FamilyMember> members)
        {
            Text            = "\u8F49\u79FB\u6210\u54E1\u5BB6\u65CF";
            Size            = new Size(360, 260);
            MinimumSize     = new Size(320, 240);
            BackColor       = Theme.BgPage;
            ForeColor       = Theme.TextPrimary;
            Font            = Theme.FontBody;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterParent;
            MaximizeBox     = false; MinimizeBox = false;

            string memberNames = string.Join(", ", members.Select(m => m.CharName.Length > 0 ? m.CharName : m.Cdkey));
            if (memberNames.Length > 40) memberNames = memberNames.Substring(0, 40) + "...";

            var lbl1 = new Label
            {
                Text      = "\u8981\u5c07\u4ee5\u4e0b\u6210\u54e1\u8f49\u79fb\u5230\uff1a",
                ForeColor = Theme.TextSecondary,
                AutoSize  = true,
                Location  = new Point(16, 14)
            };
            Controls.Add(lbl1);

            var lblNames = new Label
            {
                Text      = memberNames,
                ForeColor = Theme.TextPrimary,
                Font      = Theme.FontCell9Bold,
                AutoSize  = false,
                Size      = new Size(310, 36),
                Location  = new Point(16, 36)
            };
            Controls.Add(lblNames);

            var lbl2 = new Label
            {
                Text      = "\u76ee\u6a19\u5bb6\u65cf\uff1a",
                ForeColor = Theme.TextSecondary,
                AutoSize  = true,
                Location  = new Point(16, 84)
            };
            Controls.Add(lbl2);

            var cmb = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width         = 280,
                Location      = new Point(16, 104),
                BackColor     = Theme.BgInput,
                ForeColor     = Theme.TextPrimary,
                FlatStyle     = FlatStyle.Flat
            };
            foreach (var f in targets)
                cmb.Items.Add(new FamilyItem(f));
            if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
            Controls.Add(cmb);

            var btnOk = new Button
            {
                Text      = "\u78BA\u5B9A\u8F49\u79FB",
                Size      = new Size(100, 32),
                Location  = new Point(16, 160),
                BackColor = Color.FromArgb(32, 128, 230),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.OK
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) =>
            {
                if (cmb.SelectedItem is FamilyItem fi)
                {
                    TargetFamilyId   = fi.Info.FamilyId;
                    TargetFamilyName = fi.Info.FamilyName;
                }
            };
            Controls.Add(btnOk);

            var btnCancel = new Button
            {
                Text         = "\u53D6\u6D88",
                Size         = new Size(80, 32),
                Location     = new Point(124, 160),
                BackColor    = Color.FromArgb(70, 72, 80),
                ForeColor    = Color.White,
                FlatStyle    = FlatStyle.Flat,
                DialogResult = DialogResult.Cancel
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private class FamilyItem
        {
            public FamilyInfo Info { get; }
            public FamilyItem(FamilyInfo info) => Info = info;
            public override string ToString() => $"[{Info.FamilyId}] {Info.FamilyName}  ({Info.MemberCount}人)";
        }
    }

    /// <summary>手動新增成員對話框</summary>
    internal class AddMemberDialog : Form
    {
        public (string cdkey, string charName, int role) Result { get; private set; }

        public AddMemberDialog(int familyId, string familyName)
        {
            Text            = $"新增成員到「{familyName}」";
            Size            = new Size(400, 280);
            BackColor       = Theme.BgPage;
            ForeColor       = Theme.TextPrimary;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;

            var lblCdkey = new Label { Text = "玩家帳號 (cdkey)：", Location = new Point(16, 20), AutoSize = true, ForeColor = Theme.TextPrimary };
            Controls.Add(lblCdkey);

            var txtCdkey = new TextBox { Location = new Point(16, 42), Width = 340, BackColor = Theme.BgInput, ForeColor = Theme.TextPrimary, BorderStyle = BorderStyle.FixedSingle };
            Controls.Add(txtCdkey);

            var lblName = new Label { Text = "角色名稱（選填，留空會自動從 csalogin 取得）：", Location = new Point(16, 78), AutoSize = true, ForeColor = Theme.TextPrimary };
            Controls.Add(lblName);

            var txtName = new TextBox { Location = new Point(16, 100), Width = 340, BackColor = Theme.BgInput, ForeColor = Theme.TextPrimary, BorderStyle = BorderStyle.FixedSingle };
            Controls.Add(txtName);

            var lblRole = new Label { Text = "職位：", Location = new Point(16, 136), AutoSize = true, ForeColor = Theme.TextPrimary };
            Controls.Add(lblRole);

            var cmb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(16, 158), Width = 200, BackColor = Theme.BgInput, ForeColor = Theme.TextPrimary, FlatStyle = FlatStyle.Flat };
            cmb.Items.Add("成員");
            cmb.Items.Add("♔ 族長");
            cmb.Items.Add("★ 長老");
            cmb.SelectedIndex = 0;
            Controls.Add(cmb);

            var btnOk = new Button
            {
                Text         = "確定新增",
                Size         = new Size(100, 32),
                Location     = new Point(16, 200),
                BackColor    = Color.FromArgb(10, 140, 80),
                ForeColor    = Color.White,
                FlatStyle    = FlatStyle.Flat,
                DialogResult = DialogResult.OK
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) =>
            {
                var cdkey = txtCdkey.Text.Trim();
                if (string.IsNullOrEmpty(cdkey))
                {
                    MessageBox.Show("帳號不能為空！", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                    return;
                }
                int role = cmb.SelectedIndex; // 0=成員, 1=族長, 2=長老
                Result = (cdkey, txtName.Text.Trim(), role);
            };
            Controls.Add(btnOk);

            var btnCancel = new Button
            {
                Text         = "取消",
                Size         = new Size(80, 32),
                Location     = new Point(124, 200),
                BackColor    = Color.FromArgb(70, 72, 80),
                ForeColor    = Color.White,
                FlatStyle    = FlatStyle.Flat,
                DialogResult = DialogResult.Cancel
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }
}
