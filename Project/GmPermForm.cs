using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    /// <summary>
    /// GM 權限管理：設定玩家的 NeiCe（GM 內測標記）和 GroupId（群組）
    /// </summary>
    public class GmPermForm : Form
    {
        private DataGridView _dgv;
        private TextBox      _searchBox;
        private Label        _statusLbl;
        private Button       _btnSearch;

        public GmPermForm()
        {
            Text          = "🔑 GM 權限管理";
            Size          = new Size(1000, 640);
            MinimumSize   = new Size(760, 460);
            BackColor     = Theme.BgMid;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;

            BuildUI();
            _ = LoadAsync("");
        }

        private void BuildUI()
        {
            // ── Header ──
            var header = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme.BgDark };
            header.Controls.Add(new Label
            {
                Text      = "  🔑  GM 權限管理  —  設定玩家的 GM 標記（NeiCe）和群組 ID",
                ForeColor = Theme.AccentPurple,
                Font      = Theme.FontBody,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(8, 0, 0, 0)
            });

            // ── 提示列 ──
            var infoBar = new Panel { Dock = DockStyle.Top, Height = 24, BackColor = Color.FromArgb(30, 14, 50) };
            infoBar.Controls.Add(new Label
            {
                Text      = "  💡 NeiCe=1 為 GM 標記  |  GroupId 預設 0=一般玩家  |  雙擊列 = 編輯",
                ForeColor = Color.FromArgb(180, 120, 255), Font = Theme.FontSmall,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft
            });

            // ── 搜尋列 ──
            var searchPanel = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Theme.BgCard };
            searchPanel.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme.Border });
            searchPanel.Controls.Add(new Label
            {
                Text = "搜尋角色名稱或帳號（留空 = 顯示所有 GM 標記玩家）",
                ForeColor = Theme.TextMuted, Font = new Font(Theme.FontFamily, 8.5f),
                AutoSize = true, Location = new Point(42, 4)
            });
            var searchIcon = new Label { Text = "🔍", Font = new Font("Segoe UI Emoji", 14f), AutoSize = true, Location = new Point(12, 22) };
            _searchBox = new TextBox
            {
                BackColor = Theme.BgPage, ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle, Font = new Font(Theme.FontFamily, 11f),
                PlaceholderText = "主帳號 / 角色名 / UID（主帳號可帶出全部子帳號，留空 = 列出所有 GM 玩家）",
                Location = new Point(42, 22), Height = 28,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _searchBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) _ = LoadAsync(_searchBox.Text.Trim()); };
            _btnSearch = Theme.MakePrimaryButton("查詢", 80, 28);
            _btnSearch.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnSearch.Click += (s, e) => _ = LoadAsync(_searchBox.Text.Trim());
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
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0)
            };
            statusBar.Controls.Add(_statusLbl);

            // ── DataGridView ──
            _dgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(_dgv);
            _dgv.ReadOnly           = true;
            _dgv.RowTemplate.Height = 36;
            _dgv.CellDoubleClick   += DgvDoubleClick;

            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cOnline",   HeaderText = "在線",     Width = 58  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cCharName", HeaderText = "角色名稱", Width = 130 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cAccount",  HeaderText = "帳號",     Width = 130 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cNeiCe",    HeaderText = "GM 標記",  Width = 80  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cGroupId",  HeaderText = "群組 ID",  Width = 80  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "cPermLevel", HeaderText = "權限等級",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 150
            });

            Controls.Add(_dgv);
            Controls.Add(statusBar);
            Controls.Add(searchPanel);
            Controls.Add(infoBar);
            Controls.Add(header);
        }

        private async Task LoadAsync(string query)
        {
            _btnSearch.Enabled = false;
            _statusLbl.Text    = "查詢中…";
            _dgv.Rows.Clear();
            try
            {
                List<GameGmInfo> list;
                if (string.IsNullOrWhiteSpace(query))
                {
                    // 留空：直接列出所有 GM 標記玩家
                    list = await DatabaseManager.Instance.GetAllPlayersGmInfoAsync("");
                    list = list.FindAll(p => p.NeiCe == 1 || p.GroupId != 0);
                }
                else
                {
                    // 有關鍵字：使用 PlayerPickerHelper，主帳號可帶出多角色（可複選）
                    var picked = await PlayerPickerHelper.PickMultiAsync(this, query, multiMode: true);
                    if (picked == null || picked.Count == 0) { _statusLbl.Text = ""; return; }
                    // 取得這些角色的 GM 資訊
                    list = new List<GameGmInfo>();
                    foreach (var p in picked)
                    {
                        var gmList = await DatabaseManager.Instance.GetAllPlayersGmInfoAsync(p.Account);
                        list.AddRange(gmList.FindAll(g => g.Account == p.Account));
                        if (!gmList.Any(g => g.Account == p.Account))
                            list.Add(new GameGmInfo { Account = p.Account, OnlineName = p.OnlineName, IsOnline = p.IsOnline });
                    }
                }

                foreach (var p in list)
                {
                    string permText = p.NeiCe == 1 ? "⭐ GM（最高）" :
                                      p.GroupId > 0 ? $"群組 {p.GroupId}" : "一般玩家";
                    int i = _dgv.Rows.Add(
                        p.IsOnline ? "🟢" : "⚫",
                        p.OnlineName, p.Account,
                        p.NeiCe == 1 ? "✅ 是" : "—",
                        p.GroupId,
                        permText);
                    _dgv.Rows[i].Tag = p;
                    if (p.NeiCe == 1)
                        _dgv.Rows[i].DefaultCellStyle.ForeColor = Theme.AccentOrange;
                    else if (p.GroupId > 0)
                        _dgv.Rows[i].DefaultCellStyle.ForeColor = Theme.AccentPurple;
                }
                string hint = string.IsNullOrWhiteSpace(query) ? "GM/特殊權限玩家" : "搜尋結果";
                _statusLbl.Text = $"{hint}：共 {list.Count} 筆";
            }
            catch (Exception ex) { _statusLbl.Text = "✗ " + ex.Message; }
            finally { _btnSearch.Enabled = true; }
        }

        private void DgvDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (_dgv.Rows[e.RowIndex].Tag is not GameGmInfo gm) return;
            ShowEditDialog(gm);
        }

        private void ShowEditDialog(GameGmInfo gm)
        {
            using var dlg = new Form
            {
                Text          = $"編輯權限：{gm.OnlineName} ({gm.Account})",
                Size          = new Size(380, 260),
                StartPosition = FormStartPosition.CenterParent,
                BackColor     = Theme.BgMid,
                ForeColor     = Theme.TextPrimary,
                Font          = Theme.FontBody
            };

            int y = 20, x = 20;
            void AddRow(string label, Control ctrl)
            {
                dlg.Controls.Add(new Label { Text = label, Location = new Point(x, y + 3), ForeColor = Theme.TextSecondary, AutoSize = true });
                ctrl.Location = new Point(x + 110, y);
                dlg.Controls.Add(ctrl);
                y += 42;
            }

            var chkNeiCe = new CheckBox
            {
                Text = "啟用 GM 標記（NeiCe = 1）",
                Checked = gm.NeiCe == 1,
                ForeColor = Theme.TextPrimary, AutoSize = true,
                FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent
            };
            AddRow("GM 標記：", chkNeiCe);

            var nudGroup = new NumericUpDown
            {
                Minimum = 0, Maximum = 9999, Value = gm.GroupId,
                Width = 120, BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary
            };
            AddRow("群組 ID：", nudGroup);

            dlg.Controls.Add(new Label
            {
                Text = "（0 = 一般，GroupId 由遊戲伺服器定義）",
                Location = new Point(x, y), ForeColor = Theme.TextMuted,
                Font = Theme.FontSmall, AutoSize = true
            });
            y += 30;

            var btnSave = Theme.MakePrimaryButton("儲存", 90, 32);
            btnSave.Location = new Point(x, y);
            btnSave.Click += async (s, e) =>
            {
                int newNeiCe = chkNeiCe.Checked ? 1 : 0;
                int newGroup = (int)nudGroup.Value;
                dlg.Close();
                try
                {
                    bool ok = await DatabaseManager.Instance.SetPlayerPermAsync(gm.Account, newNeiCe, newGroup);
                    _statusLbl.Text = ok ? $"✓ 已更新 {gm.OnlineName} 的權限" : "✗ 更新失敗";
                }
                catch (Exception ex) { _statusLbl.Text = "✗ " + ex.Message; }
                await LoadAsync(_searchBox.Text.Trim());
            };
            var btnCancel = Theme.MakeSecondaryButton("取消", 80, 32);
            btnCancel.Location = new Point(x + 100, y);
            btnCancel.Click += (s, e) => dlg.Close();

            dlg.Controls.AddRange(new Control[] { btnSave, btnCancel });
            dlg.ShowDialog(this);
        }
    }

}
