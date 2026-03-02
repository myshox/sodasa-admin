using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    // ══════════════════════════════════════════════════════════════════════
    //  PlayerPickerDialog
    //  當輸入主帳號名稱且底下有多個角色時，顯示此對話框讓 GM 選擇一個角色。
    // ══════════════════════════════════════════════════════════════════════
    public class PlayerPickerDialog : Form
    {
        public PlayerInfo?       SelectedPlayer  { get; private set; }
        public List<PlayerInfo>  SelectedPlayers { get; private set; } = new();

        private readonly List<PlayerInfo> _players;
        private readonly bool             _multiMode;
        private DataGridView _dgv;
        private Button _btnOk, _btnCancel;

        /// <param name="multiMode">true = 多選模式（用於「全部加入收件人」），false = 單選</param>
        public PlayerPickerDialog(string masterName, List<PlayerInfo> players, bool multiMode = false)
        {
            _players   = players;
            _multiMode = multiMode;

            Text            = $"👥 選擇角色 — 主帳號：{masterName}（共 {players.Count} 個角色）";
            Size            = new Size(680, 460);
            MinimumSize     = new Size(520, 320);
            BackColor       = Theme.BgPage;
            ForeColor       = Theme.TextPrimary;
            Font            = Theme.FontBody;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;

            BuildUI();
        }

        private void BuildUI()
        {
            string hintText = _multiMode
                ? "  主帳號旗下所有角色如下，可多選（Ctrl/Shift）或點「全選」，加入收件人："
                : "  此主帳號底下有多個角色，請選擇要操作的角色（雙擊 或 選取後按確定）：";

            var lblHint = new Label
            {
                Text      = hintText,
                ForeColor = Color.FromArgb(120, 180, 255),
                Font      = Theme.FontSmall,
                Dock      = DockStyle.Top,
                Height    = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.FromArgb(20, 40, 80),
            };

            // DataGridView
            _dgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(_dgv);
            _dgv.RowTemplate.Height = 34;
            _dgv.ReadOnly           = true;
            _dgv.SelectionMode      = DataGridViewSelectionMode.FullRowSelect;
            _dgv.MultiSelect        = _multiMode;

            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cStatus",  HeaderText = "狀態",        Width = 70 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cName",    HeaderText = "角色名稱",    Width = 140 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cAccount", HeaderText = "UID (cdkey)", Width = 150 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cPay",     HeaderText = "💳 儲值 NT$", Width = 110 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cLogin",   HeaderText = "最後登入",    FillWeight = 100 });
            _dgv.Columns["cLogin"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            foreach (var p in _players)
            {
                int i = _dgv.Rows.Add(
                    p.IsOnline ? "🟢 在線" : "⚫ 離線",
                    p.OnlineName,
                    p.Account,
                    p.PayTotal.ToString("N0"),
                    p.LoginTime);
                _dgv.Rows[i].Tag = p;
                if (p.IsOnline)
                    _dgv.Rows[i].DefaultCellStyle.ForeColor = Color.FromArgb(80, 220, 140);
            }

            // 雙擊：單選模式直接確認，多選模式切換選取
            _dgv.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                if (!_multiMode)
                {
                    SelectedPlayer  = _dgv.Rows[e.RowIndex].Tag as PlayerInfo;
                    SelectedPlayers = new List<PlayerInfo> { SelectedPlayer };
                    DialogResult    = DialogResult.OK;
                }
            };

            // 底部按鈕列
            var btnRow = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = Theme.BgCard };
            btnRow.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Theme.Border });

            int bx = 12;

            _btnOk = Theme.MakeButton(_multiMode ? "✔ 加入選取角色" : "✔ 確定選擇", Theme.AccentBlue, Color.White, _multiMode ? 130 : 110, 32);
            _btnOk.Location = new Point(bx, 10);
            _btnOk.Click   += (s, e) =>
            {
                if (_dgv.SelectedRows.Count == 0) { MessageBox.Show("請先選擇角色", "提示"); return; }
                if (_multiMode)
                {
                    SelectedPlayers.Clear();
                    foreach (DataGridViewRow r in _dgv.SelectedRows)
                        if (r.Tag is PlayerInfo p) SelectedPlayers.Add(p);
                    SelectedPlayer = SelectedPlayers.Count > 0 ? SelectedPlayers[0] : null;
                }
                else
                {
                    SelectedPlayer  = _dgv.SelectedRows[0].Tag as PlayerInfo;
                    SelectedPlayers = new List<PlayerInfo> { SelectedPlayer };
                }
                DialogResult = DialogResult.OK;
            };
            btnRow.Controls.Add(_btnOk);
            bx += _btnOk.Width + 8;

            // 多選模式：加「全選」按鈕
            if (_multiMode)
            {
                var btnAll = Theme.MakeButton("⬛ 全選", Color.FromArgb(20, 60, 20), Color.FromArgb(86, 196, 118), 80, 32);
                btnAll.Location = new Point(bx, 10);
                btnAll.Click   += (s, e) => { _dgv.SelectAll(); };
                btnRow.Controls.Add(btnAll);
                bx += 88;

                var btnNone = Theme.MakeButton("⬜ 取消全選", Theme.BgLight, Theme.TextSecondary, 90, 32);
                btnNone.Location = new Point(bx, 10);
                btnNone.Click   += (s, e) => { _dgv.ClearSelection(); };
                btnRow.Controls.Add(btnNone);
                bx += 98;
            }

            _btnCancel = Theme.MakeButton("✕ 取消", Theme.BgLight, Theme.TextSecondary, 80, 32);
            _btnCancel.Location = new Point(bx, 10);
            _btnCancel.Click   += (s, e) => DialogResult = DialogResult.Cancel;
            btnRow.Controls.Add(_btnCancel);

            Controls.AddRange(new Control[] { _dgv, lblHint, btnRow });
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  PlayerPickerHelper
    //  共用靜態方法：輸入任何識別值，找出對應角色（支援主帳號展開）。
    //  用法：
    //    var player = await PlayerPickerHelper.PickAsync(this, inputText);
    //    if (player == null) return;   // 使用者取消 or 找不到
    //    // 繼續使用 player.Account
    // ══════════════════════════════════════════════════════════════════════
    public static class PlayerPickerHelper
    {
        /// <summary>
        /// 選擇模式：找到多個角色時彈出多選框（可勾選多個），回傳第一個選定的玩家。
        /// 若需取全部選定清單，請直接呼叫 PickMultiAsync。
        /// </summary>
        public static async Task<PlayerInfo?> PickAsync(Form parent, string query, string title = "選擇角色")
        {
            var list = await PickMultiAsync(parent, query, multiMode: true);
            return list?.Count > 0 ? list[0] : null;
        }

        /// <summary>
        /// 多選模式：找到主帳號下多個角色時，顯示多選框，可全選或部分選。
        /// - 找到 0 筆 → 顯示錯誤，回傳 null
        /// - 找到 1 筆 → 直接回傳含 1 筆的 List
        /// - 找到多筆 → 顯示 PlayerPickerDialog（multiMode），由 GM 選取，回傳選定清單
        /// </summary>
        public static async Task<List<PlayerInfo>?> PickMultiAsync(Form parent, string query, bool multiMode = true)
        {
            if (string.IsNullOrWhiteSpace(query)) return null;

            List<PlayerInfo> players;
            try
            {
                players = await DatabaseManager.Instance.SearchPlayersAsync(query.Trim(), 50);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"查詢失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }

            if (players.Count == 0)
            {
                MessageBox.Show($"找不到玩家「{query}」\n請確認主帳號、角色名稱或 UID 是否正確。",
                    "找不到玩家", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }

            if (players.Count == 1)
                return players;

            // 多個角色 → 讓 GM 選擇
            using var dlg = new PlayerPickerDialog(query.Trim(), players, multiMode);
            if (dlg.ShowDialog(parent) == DialogResult.OK)
                return dlg.SelectedPlayers;

            return null;
        }
    }
}
