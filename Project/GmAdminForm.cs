using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    public class GmAdminForm : Form
    {
        private DataGridView _dgv;
        private Label        _statusLbl;

        public GmAdminForm()
        {
            InitUI();
            _ = LoadAsync();
        }

        private void InitUI()
        {
            Text          = "👥 GM 帳號管理";
            Size          = new Size(700, 520);
            MinimumSize   = new Size(600, 420);
            BackColor = Theme.BgPage;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;

            // 標題列
            var hdr = new Panel { Dock = DockStyle.Top, Height = Theme.ToolbarHeight, BackColor = Theme.BgDialogHeader };
            hdr.Controls.Add(new Label
            {
                Text = "👥  GM 帳號管理  —  管理可登入此工具的管理員帳號",
                ForeColor = Theme.AccentBlue, Font = Theme.FontHeader,
                AutoSize = true, Location = new Point(14, 12)
            });
            // 說明
            var infoBar = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = Theme.BgCard };
            infoBar.Controls.Add(new Label
            {
                Text = "⚠ 密碼以 MD5 雜湊儲存。帳號「admin」不能刪除。新增 GM 帳號後對方可登入本工具。",
                ForeColor = Color.FromArgb(120, 210, 100), Font = Theme.FontSmall,
                AutoSize = true, Location = new Point(10, 8)
            });

            // DataGrid
            _dgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(_dgv);
            _dgv.ReadOnly = true;
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cId",       HeaderText = "ID",   Width = 50 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cUsername",  HeaderText = "帳號", Width = 130 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cNickname",  HeaderText = "暱稱", Width = 130 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cStatus",    HeaderText = "狀態", Width = 80 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cCreated",   HeaderText = "建立時間", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _dgv.Columns.Add(MakeBtnCol("cToggle",  "啟用/停用", Theme.AccentOrange));
            _dgv.Columns.Add(MakeBtnCol("cPwd",     "重設密碼",  Color.FromArgb(40, 100, 150)));
            _dgv.Columns.Add(MakeBtnCol("cDelete",  "刪除",      Theme.AccentRed));
            _dgv.CellClick += Dgv_CellClick;

            // 底部工具列
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = Theme.BgCard };
            var btnAdd = Theme.MakeButton("➕ 新增 GM 帳號", Theme.AccentGreen, Color.White, 140, 32);
            btnAdd.Location = new Point(12, 11);
            btnAdd.Click += BtnAdd_Click;
            var btnRefresh = Theme.MakeButton("🔄 重新整理", Theme.BgLight, Theme.TextSecondary, 100, 32);
            btnRefresh.Location = new Point(160, 11);
            btnRefresh.Click += async (s, e) => await LoadAsync();
            _statusLbl = new Label { Text = "", ForeColor = Theme.AccentGreen, Font = Theme.FontSmall, AutoSize = true, Location = new Point(270, 19) };
            var btnClose = Theme.MakeButton("關  閉", Theme.BgLight, Theme.TextSecondary, 80, 32);
            btnClose.Location = new Point(590, 11);
            btnClose.Click += (s, e) => Close();
            bottom.Controls.AddRange(new Control[] { btnAdd, btnRefresh, _statusLbl, btnClose });

            // Fill 先加（背景），Bottom 次之，Top 最後加（前景），確保 DockStyle 正確分配空間
            Controls.Add(_dgv);
            Controls.Add(bottom);
            Controls.Add(infoBar);
            Controls.Add(hdr);
        }

        private DataGridViewButtonColumn MakeBtnCol(string name, string text, Color bg) =>
            new DataGridViewButtonColumn
            {
                Name = name, HeaderText = "", Width = 76, FlatStyle = FlatStyle.Flat,
                UseColumnTextForButtonValue = true, Text = text,
                DefaultCellStyle = { BackColor = bg, ForeColor = Color.White, SelectionBackColor = bg, SelectionForeColor = Color.White, Font = Theme.FontSmall }
            };

        private async Task LoadAsync()
        {
            try
            {
                var users = await DatabaseManager.Instance.GetAdminUsersAsync();
                if (InvokeRequired) { Invoke(new Action(() => FillGrid(users))); return; }
                FillGrid(users);
            }
            catch (Exception ex)
            {
                if (InvokeRequired) Invoke(new Action(() => _statusLbl.Text = "✗ " + ex.Message));
                else _statusLbl.Text = "✗ " + ex.Message;
            }
        }

        private void FillGrid(System.Collections.Generic.List<AdminUser> users)
        {
            _dgv.Rows.Clear();
            foreach (var u in users)
            {
                int i = _dgv.Rows.Add(u.Id, u.Username, u.Nickname,
                    u.IsEnabled ? "✅ 啟用" : "⛔ 停用", u.CreatedAt,
                    u.IsEnabled ? "停用" : "啟用", "重設密碼", u.Username == "admin" ? "—" : "刪除");
                _dgv.Rows[i].Tag = u;
                if (!u.IsEnabled)
                    _dgv.Rows[i].DefaultCellStyle.ForeColor = Theme.TextMuted;
            }
        }

        private async void Dgv_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var user = _dgv.Rows[e.RowIndex].Tag as AdminUser;
            if (user == null) return;
            string col = _dgv.Columns[e.ColumnIndex].Name;

            switch (col)
            {
                case "cToggle":
                    bool newStatus = !user.IsEnabled;
                    if (MessageBox.Show($"確定要{(newStatus ? "啟用" : "停用")}帳號「{user.Username}」？",
                        "確認", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                    bool ok = await DatabaseManager.Instance.ToggleAdminStatusAsync(user.Id, newStatus);
                    SetStatus(ok ? $"✓ 已{(newStatus ? "啟用" : "停用")}「{user.Username}」" : "✗ 操作失敗", ok);
                    if (ok) await LoadAsync();
                    break;

                case "cPwd":
                    using (var dlg = new NewPasswordDialog(user.Username))
                    {
                        if (dlg.ShowDialog(this) != DialogResult.OK) return;
                        if (string.IsNullOrWhiteSpace(dlg.NewPassword)) { MessageBox.Show("密碼不能為空"); return; }
                        bool ok2 = await DatabaseManager.Instance.ResetAdminPasswordAsync(user.Id, dlg.NewPassword);
                        SetStatus(ok2 ? $"✓ 已重設「{user.Username}」的密碼" : "✗ 重設失敗", ok2);
                    }
                    break;

                case "cDelete":
                    if (user.Username == "admin") { MessageBox.Show("admin 帳號不能刪除"); return; }
                    if (MessageBox.Show($"確定刪除 GM 帳號「{user.Username}」？", "確認",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                    bool ok3 = await DatabaseManager.Instance.DeleteAdminUserAsync(user.Id);
                    SetStatus(ok3 ? $"✓ 已刪除「{user.Username}」" : "✗ 刪除失敗", ok3);
                    if (ok3) await LoadAsync();
                    break;
            }
        }

        private async void BtnAdd_Click(object sender, EventArgs e)
        {
            using var dlg = new AddAdminDialog();
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            if (string.IsNullOrWhiteSpace(dlg.Username) || string.IsNullOrWhiteSpace(dlg.Password))
            { MessageBox.Show("帳號和密碼不能為空"); return; }
            bool ok = await DatabaseManager.Instance.AddAdminUserAsync(dlg.Username, dlg.Password, dlg.Nickname);
            SetStatus(ok ? $"✓ 已新增 GM 帳號「{dlg.Username}」" : "✗ 新增失敗（帳號可能重複）", ok);
            if (ok) await LoadAsync();
        }

        private void SetStatus(string msg, bool ok)
        {
            _statusLbl.Text = msg;
            _statusLbl.ForeColor = ok ? Theme.AccentGreen : Theme.AccentRed;
        }
    }

    // ── 新增 GM 帳號對話框 ────────────────────────────────
    public class AddAdminDialog : Form
    {
        private TextBox _user, _pwd, _nick;
        public string Username => _user.Text.Trim();
        public string Password => _pwd.Text;
        public string Nickname => _nick.Text.Trim();

        public AddAdminDialog()
        {
            Text = "新增 GM 帳號"; Size = new Size(400, 240);
            BackColor = Theme.BgPage; ForeColor = Theme.TextPrimary;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            int y = 16;
            void AddRow(string lbl, out TextBox box, bool pwd = false)
            {
                Controls.Add(new Label { Text = lbl, ForeColor = Theme.TextSecondary, Font = Theme.FontSmall, AutoSize = true, Location = new Point(20, y + 4) });
                box = Theme.MakeTextBox(240); box.Location = new Point(100, y);
                if (pwd) box.PasswordChar = '●';
                Controls.Add(box); y += 36;
            }
            AddRow("帳號：",  out _user);
            AddRow("密碼：",  out _pwd, true);
            AddRow("暱稱：",  out _nick);

            var ok = Theme.MakeButton("確 定", Theme.AccentBlue, Color.White, 100, 32);
            ok.Location = new Point(160, y + 10);
            ok.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
            Controls.Add(ok);
            var cancel = Theme.MakeButton("取 消", Theme.BgLight, Theme.TextSecondary, 80, 32);
            cancel.Location = new Point(270, y + 10);
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);
        }
    }

    // ── 重設密碼對話框 ────────────────────────────────────
    public class NewPasswordDialog : Form
    {
        private TextBox _pwd, _pwd2;
        public string NewPassword => _pwd.Text;

        public NewPasswordDialog(string username)
        {
            Text = $"重設密碼 — {username}"; Size = new Size(380, 190);
            BackColor = Theme.BgPage; ForeColor = Theme.TextPrimary;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            int y = 16;
            Controls.Add(new Label { Text = "新密碼：", ForeColor = Theme.TextSecondary, Font = Theme.FontSmall, AutoSize = true, Location = new Point(20, y + 4) });
            _pwd = Theme.MakeTextBox(230); _pwd.Location = new Point(90, y); _pwd.PasswordChar = '●'; Controls.Add(_pwd); y += 36;
            Controls.Add(new Label { Text = "確認密碼：", ForeColor = Theme.TextSecondary, Font = Theme.FontSmall, AutoSize = true, Location = new Point(20, y + 4) });
            _pwd2 = Theme.MakeTextBox(230); _pwd2.Location = new Point(90, y); _pwd2.PasswordChar = '●'; Controls.Add(_pwd2); y += 36;

            var ok = Theme.MakeButton("確 定", Theme.AccentBlue, Color.White, 100, 32);
            ok.Location = new Point(140, y + 8);
            ok.Click += (s, e) =>
            {
                if (_pwd.Text != _pwd2.Text) { MessageBox.Show("兩次密碼不一致"); return; }
                if (_pwd.Text.Length < 4) { MessageBox.Show("密碼至少4位"); return; }
                DialogResult = DialogResult.OK; Close();
            };
            Controls.Add(ok);
            var cancel = Theme.MakeButton("取 消", Theme.BgLight, Theme.TextSecondary, 80, 32);
            cancel.Location = new Point(250, y + 8);
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);
        }
    }
}
