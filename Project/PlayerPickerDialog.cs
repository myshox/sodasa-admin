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
        public PlayerInfo? SelectedPlayer { get; private set; }

        private readonly List<PlayerInfo> _players;
        private DataGridView _dgv;
        private Button _btnOk, _btnCancel;

        public PlayerPickerDialog(string masterName, List<PlayerInfo> players)
        {
            _players = players;

            Text            = $"👥 選擇角色 — 主帳號：{masterName}（共 {players.Count} 個角色）";
            Size            = new Size(640, 420);
            MinimumSize     = new Size(500, 300);
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
            // 標題提示
            var lblHint = new Label
            {
                Text      = "  此主帳號底下有多個角色，請選擇要操作的角色（雙擊 或 選取後按確定）：",
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
            _dgv.RowTemplate.Height  = 34;
            _dgv.ReadOnly            = true;
            _dgv.SelectionMode       = DataGridViewSelectionMode.FullRowSelect;
            _dgv.MultiSelect         = false;

            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cStatus",  HeaderText = "狀態",       Width = 60 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cName",    HeaderText = "角色名稱",   Width = 130 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cAccount", HeaderText = "UID (cdkey)", Width = 140 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cPay",     HeaderText = "💳 儲值 NT$", Width = 110 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cLogin",   HeaderText = "最後登入",   FillWeight = 100 });
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

            // 雙擊直接確認
            _dgv.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                SelectedPlayer = _dgv.Rows[e.RowIndex].Tag as PlayerInfo;
                DialogResult   = DialogResult.OK;
            };

            // 底部按鈕列
            var btnRow = new Panel { Dock = DockStyle.Bottom, Height = 50, BackColor = Theme.BgCard };
            btnRow.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Theme.Border });

            _btnOk = Theme.MakeButton("✔ 確定選擇", Theme.AccentBlue, Color.White, 110, 32);
            _btnOk.Location = new Point(12, 10);
            _btnOk.Click   += (s, e) =>
            {
                if (_dgv.SelectedRows.Count == 0) { MessageBox.Show("請先選擇一個角色", "提示"); return; }
                SelectedPlayer = _dgv.SelectedRows[0].Tag as PlayerInfo;
                DialogResult   = DialogResult.OK;
            };

            _btnCancel = Theme.MakeButton("✕ 取消", Theme.BgLight, Theme.TextSecondary, 80, 32);
            _btnCancel.Location = new Point(130, 10);
            _btnCancel.Click   += (s, e) => DialogResult = DialogResult.Cancel;

            btnRow.Controls.AddRange(new Control[] { _btnOk, _btnCancel });

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
        /// 搜尋玩家（支援主帳號展開）。
        /// - 找到 0 筆 → 顯示錯誤提示，回傳 null
        /// - 找到 1 筆 → 直接回傳
        /// - 找到多筆 → 顯示 PlayerPickerDialog，由 GM 選擇，回傳選定者
        /// </summary>
        public static async Task<PlayerInfo?> PickAsync(Form parent, string query, string title = "選擇角色")
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
                MessageBox.Show($"找不到玩家「{query}」\n請確認帳號、角色名稱或主帳號是否正確。",
                    "找不到玩家", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }

            if (players.Count == 1)
                return players[0];

            // 多個角色 → 讓 GM 選擇
            using var dlg = new PlayerPickerDialog(query.Trim(), players);
            if (dlg.ShowDialog(parent) == DialogResult.OK)
                return dlg.SelectedPlayer;

            return null;
        }
    }
}
