using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    public class BanForm : Form
    {
        private DataGridView _dgv;
        private TextBox      _searchBox;
        private Label        _statusLbl;
        private Button       _btnSearch;

        public BanForm()
        {
            Text          = "🔒 封號管理";
            Size          = new Size(1000, 640);
            MinimumSize   = new Size(760, 460);
            BackColor     = Theme.BgMid;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;

            BuildUI();
            _ = LoadBannedAsync();
        }

        private void BuildUI()
        {
            // ── Header ──
            var header = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme.BgDark };
            header.Controls.Add(new Label
            {
                Text      = "  🔒  封號管理  —  查詢封禁清單、搜尋玩家封/解封",
                ForeColor = Theme.AccentRed,
                Font      = Theme.FontBody,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(8, 0, 0, 0)
            });

            // ── 搜尋列 ──
            var searchPanel = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Theme.BgCard };
            searchPanel.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme.Border });
            searchPanel.Controls.Add(new Label
            {
                Text      = "輸入角色名稱或帳號搜尋玩家後操作封/解封",
                ForeColor = Theme.TextMuted, Font = new Font(Theme.FontFamily, 8.5f),
                AutoSize  = true, Location = new Point(42, 4)
            });
            var searchIcon = new Label
            {
                Text = "🔍", Font = new Font("Segoe UI Emoji", 14f),
                AutoSize = true, Location = new Point(12, 22)
            };
            _searchBox = new TextBox
            {
                BackColor       = Theme.BgPage, ForeColor = Theme.TextPrimary,
                BorderStyle     = BorderStyle.FixedSingle,
                Font            = new Font(Theme.FontFamily, 11f),
                PlaceholderText = "主帳號 / 角色名 / UID（主帳號可帶出全部子帳號，留空 = 顯示封禁清單）",
                Location        = new Point(42, 22), Height = 28,
                Anchor          = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _searchBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                    _ = string.IsNullOrWhiteSpace(_searchBox.Text) ? LoadBannedAsync() : SearchPlayerAsync(_searchBox.Text.Trim());
            };
            _btnSearch = Theme.MakePrimaryButton("搜尋", 80, 28);
            _btnSearch.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnSearch.Click += (s, e) =>
                _ = string.IsNullOrWhiteSpace(_searchBox.Text) ? LoadBannedAsync() : SearchPlayerAsync(_searchBox.Text.Trim());

            searchPanel.Controls.AddRange(new Control[] { searchIcon, _searchBox, _btnSearch });
            searchPanel.Resize += (s, e) =>
            {
                int pw = searchPanel.ClientSize.Width;
                _btnSearch.Left  = pw - 12 - _btnSearch.Width;
                _btnSearch.Top   = 22;
                _searchBox.Width = Math.Max(100, _btnSearch.Left - _searchBox.Left - 8);
            };

            // ── 狀態列 ──
            var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = Theme.BgDark };
            _statusLbl = new Label
            {
                Text = "載入中…", ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            };
            statusBar.Controls.Add(_statusLbl);

            // ── DataGridView ──
            _dgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(_dgv);
            _dgv.ReadOnly           = true;
            _dgv.RowTemplate.Height = 36;
            _dgv.CellDoubleClick   += DgvDoubleClick;

            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cStatus",   HeaderText = "狀態",     FillWeight = 50,  MinimumWidth = 55  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cCharName", HeaderText = "角色名稱", FillWeight = 110, MinimumWidth = 80  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cAccount",  HeaderText = "帳號",     FillWeight = 120, MinimumWidth = 80  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cExpire",   HeaderText = "解封時間", FillWeight = 130, MinimumWidth = 90  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "cAction", HeaderText = "操作（雙擊執行）",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 200
            });

            Controls.Add(_dgv);
            Controls.Add(statusBar);
            Controls.Add(searchPanel);
            Controls.Add(header);
        }

        // ── 載入封禁清單 ──────────────────────────────────────────
        private async Task LoadBannedAsync()
        {
            _btnSearch.Enabled = false;
            _statusLbl.Text    = "查詢中…";
            _dgv.Rows.Clear();
            try
            {
                var list = await DatabaseManager.Instance.GetAllBannedPlayersAsync();
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                foreach (var (acc, charName, endTs) in list)
                {
                    bool isPermanent = endTs == 0;
                    bool isActive    = isPermanent || endTs > now;
                    string expire    = isPermanent ? "永久" :
                                       DateTimeOffset.FromUnixTimeSeconds(endTs).LocalDateTime.ToString("yyyy/MM/dd HH:mm");
                    string status    = isActive ? "🔒 封禁中" : "⏰ 已到期";
                    int i = _dgv.Rows.Add(status, charName, acc, expire, "雙擊 → 解封");
                    _dgv.Rows[i].Tag = ("banned", acc);
                    _dgv.Rows[i].DefaultCellStyle.ForeColor = isActive ? Theme.AccentRed : Theme.TextMuted;
                }
                _statusLbl.Text = list.Count == 0 ? "目前沒有封禁帳號" : $"共 {list.Count} 筆封禁記錄";
            }
            catch (Exception ex) { _statusLbl.Text = "✗ " + ex.Message; }
            finally { _btnSearch.Enabled = true; }
        }

        // ── 搜尋玩家（顯示可封號操作）────────────────────────────
        private async Task SearchPlayerAsync(string query)
        {
            _btnSearch.Enabled = false;
            _statusLbl.Text    = "搜尋中…";
            _dgv.Rows.Clear();
            try
            {
                // 使用 PlayerPickerHelper：主帳號自動帶出子帳號選擇對話框（可複選）
                var picked = await PlayerPickerHelper.PickMultiAsync(this, query, multiMode: true);
                if (picked == null || picked.Count == 0) { _statusLbl.Text = ""; return; }

                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                foreach (var p in picked)
                {
                    var (isBanned, endTime) = await DatabaseManager.Instance.GetBanStatusAsync(p.Account);
                    string status  = isBanned ? "🔒 封禁中" : (p.IsOnline ? "🟢 在線" : "⚫ 離線");
                    string expire  = isBanned ? endTime : "—";
                    string action  = isBanned ? "雙擊 → 解封" : "雙擊 → 封號";
                    int i = _dgv.Rows.Add(status, p.OnlineName, p.Account, expire, action);
                    _dgv.Rows[i].Tag = (isBanned ? "banned" : "active", p.Account);
                    if (isBanned) _dgv.Rows[i].DefaultCellStyle.ForeColor = Theme.AccentRed;
                }
                _statusLbl.Text = $"已載入 {picked.Count} 位玩家";
            }
            catch (Exception ex) { _statusLbl.Text = "✗ " + ex.Message; }
            finally { _btnSearch.Enabled = true; }
        }

        // ── 雙擊：封/解封 ─────────────────────────────────────────
        private async void DgvDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (_dgv.Rows[e.RowIndex].Tag is not (string state, string account)) return;

            if (state == "banned")
            {
                // 解封
                var r = MessageBox.Show($"確定解封帳號 [{account}]？", "解封確認",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r != DialogResult.Yes) return;
                bool ok = await DatabaseManager.Instance.UnbanPlayerAsync(account);
                _statusLbl.Text = ok ? $"✓ 已解封 {account}" : "✗ 解封失敗";
                await (string.IsNullOrWhiteSpace(_searchBox.Text) ? LoadBannedAsync() : SearchPlayerAsync(_searchBox.Text.Trim()));
            }
            else
            {
                // 封號
                ShowBanDialog(account);
            }
        }

        private void ShowBanDialog(string account)
        {
            using var dlg = new Form
            {
                Text          = $"🔒 封號：{account}",
                Size          = new Size(430, 380),
                StartPosition = FormStartPosition.CenterParent,
                BackColor     = Theme.BgMid,
                ForeColor     = Theme.TextPrimary,
                Font          = Theme.FontBody,
                MinimumSize   = new Size(430, 380),
                MaximizeBox   = false
            };

            int y = 16;
            void AddLbl(string t) { dlg.Controls.Add(new Label { Text = t, AutoSize = true, Location = new Point(20, y + 4), ForeColor = Theme.TextSecondary }); }
            Control AddCtl(Control c) { c.Location = new Point(145, y); dlg.Controls.Add(c); return c; }

            AddLbl("封禁時長：");
            var cboDuration = new ComboBox
            {
                Width = 200, DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary, FlatStyle = FlatStyle.Flat
            };
            cboDuration.Items.AddRange(new object[] { "1 天", "3 天", "7 天", "14 天", "30 天", "🔴 永久封禁" });
            cboDuration.SelectedIndex = 0;
            AddCtl(cboDuration); y += 36;

            // ── 永久封禁警告區（初始隱藏）──────────────────────────
            var pnlPermWarn = new Panel
            {
                Location  = new Point(12, y),
                Size      = new Size(390, 88),
                BackColor = Color.FromArgb(50, 20, 20),
                Visible   = false
            };
            pnlPermWarn.Controls.Add(new Label
            {
                Text      = "⚠  永久封禁無法自動解除！請輸入角色帳號確認：",
                ForeColor = Color.FromArgb(255, 120, 80),
                Font      = new Font(Theme.FontFamily, 8.5f, FontStyle.Bold),
                AutoSize  = false, Size = new Size(370, 22),
                Location  = new Point(10, 8)
            });
            var txtConfirm = new TextBox
            {
                Size        = new Size(260, 26), Location = new Point(10, 36),
                BackColor   = Color.FromArgb(40, 15, 15),
                ForeColor   = Color.FromArgb(255, 140, 100),
                Font        = Theme.FontBody,
                PlaceholderText = $"輸入「{account}」確認"
            };
            var lblConfirmHint = new Label
            {
                Text      = "✗ 尚未輸入",
                ForeColor = Color.FromArgb(180, 80, 80),
                Font      = Theme.FontSmall, AutoSize = true,
                Location  = new Point(280, 40)
            };
            txtConfirm.TextChanged += (s, e) =>
            {
                bool ok = txtConfirm.Text.Trim() == account;
                lblConfirmHint.Text      = ok ? "✓ 已確認" : "✗ 帳號不符";
                lblConfirmHint.ForeColor = ok ? Theme.AccentGreen : Color.FromArgb(180, 80, 80);
            };
            pnlPermWarn.Controls.AddRange(new Control[] { txtConfirm, lblConfirmHint });
            dlg.Controls.Add(pnlPermWarn);
            y += 96;

            AddLbl("封禁原因：");
            var txtReason = new TextBox
            {
                Width = 200, BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary,
                PlaceholderText = "選填，預設：GM 封禁"
            };
            AddCtl(txtReason); y += 36;

            AddLbl("自訂到期：");
            var chkCustom = new CheckBox
            {
                Text = "啟用自訂日期", AutoSize = true,
                ForeColor = Theme.TextSecondary, FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent
            };
            AddCtl(chkCustom); y += 36;

            AddLbl("到期時間：");
            var dtp = new DateTimePicker
            {
                Width = 200, Value = DateTime.Now.AddDays(1),
                Enabled = false, BackColor = Theme.BgLight
            };
            AddCtl(dtp); y += 40;
            chkCustom.CheckedChanged += (s, e) =>
            {
                dtp.Enabled           = chkCustom.Checked;
                cboDuration.Enabled   = !chkCustom.Checked;
                pnlPermWarn.Visible   = false; // 自訂日期時隱藏永久警告
            };

            // 切換永久封禁時顯示/隱藏警告區
            cboDuration.SelectedIndexChanged += (s, e) =>
            {
                bool isPerm = cboDuration.SelectedIndex == 5;
                pnlPermWarn.Visible = isPerm && !chkCustom.Checked;
                if (isPerm) txtConfirm.Text = "";
            };

            var btnBan = Theme.MakePrimaryButton("確定封號", 110, 34);
            btnBan.BackColor = Theme.AccentRed;
            btnBan.Location  = new Point(90, y);
            var btnCancel = Theme.MakeSecondaryButton("取消", 80, 34);
            btnCancel.Location = new Point(210, y);
            btnCancel.Click   += (s, e) => dlg.Close();

            btnBan.Click += async (s, e) =>
            {
                bool isPerm = cboDuration.SelectedIndex == 5 && !chkCustom.Checked;

                // 永久封禁需輸入帳號確認
                if (isPerm && txtConfirm.Text.Trim() != account)
                {
                    MessageBox.Show($"永久封禁需正確輸入帳號「{account}」才能執行。",
                        "⚠ 永久封禁確認", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtConfirm.Focus();
                    return;
                }

                // 最終二次確認
                string durText   = isPerm ? "永久（不可自動解除）" : (cboDuration.SelectedItem?.ToString() ?? "");
                string reasonStr = txtReason.Text.Trim().Length > 0 ? txtReason.Text.Trim() : "GM 封禁";
                var confirm = MessageBox.Show(
                    $"確定要封禁帳號 [{account}]？\n\n" +
                    $"  封禁時長：{durText}\n" +
                    $"  封禁原因：{reasonStr}\n\n" +
                    (isPerm ? "⚠ 永久封禁執行後無法自動解除，需手動操作解封！\n\n" : "") +
                    "此操作將立即生效。",
                    isPerm ? "⚠ 永久封禁 — 最終確認" : "確認封號",
                    MessageBoxButtons.YesNo,
                    isPerm ? MessageBoxIcon.Stop : MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes) return;

                int endUnix = 0;
                if (chkCustom.Checked)
                {
                    endUnix = (int)new DateTimeOffset(dtp.Value, TimeZoneInfo.Local.GetUtcOffset(dtp.Value)).ToUnixTimeSeconds();
                }
                else
                {
                    int[] days = { 1, 3, 7, 14, 30, 0 };
                    int d = days[cboDuration.SelectedIndex];
                    endUnix = d == 0 ? 0 : (int)DateTimeOffset.UtcNow.AddDays(d).ToUnixTimeSeconds();
                }
                dlg.Close();
                bool ok = await DatabaseManager.Instance.BanPlayerAsync(account, endUnix, reasonStr);
                _statusLbl.Text = ok ? $"✓ 已封禁 {account}" : "✗ 封禁失敗";
                await (string.IsNullOrWhiteSpace(_searchBox.Text) ? LoadBannedAsync() : SearchPlayerAsync(_searchBox.Text.Trim()));
            };

            dlg.Controls.AddRange(new Control[] { btnBan, btnCancel });
            dlg.ShowDialog(this);
        }
    }
}
