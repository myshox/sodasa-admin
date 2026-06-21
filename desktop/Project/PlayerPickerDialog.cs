using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    /// <summary>
    /// 主帳號下多角色時的選擇視窗：標題層級、留白、表格外框與頁尾按鈕與全站 Theme 一致。
    /// </summary>
    public class PlayerPickerDialog : Form
    {
        public PlayerInfo?      SelectedPlayer  { get; private set; }
        public List<PlayerInfo> SelectedPlayers { get; private set; } = new();

        private readonly string            _masterName;
        private readonly List<PlayerInfo> _players;
        private readonly bool              _multiMode;
        private DataGridView _dgv;
        private Button       _btnOk, _btnCancel;

        /// <param name="multiMode">true = 多選（寄信收件人等）；false = 單選（按確定僅允許一列）</param>
        public PlayerPickerDialog(string masterName, List<PlayerInfo> players, bool multiMode = false)
        {
            _masterName = masterName;
            _players    = players;
            _multiMode  = multiMode;

            Text            = "選擇角色";
            Size            = new Size(880, 540);
            MinimumSize     = new Size(640, 400);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;

            Theme.ApplyDialogShell(this);
            BuildUI();
        }

        private void BuildUI()
        {
            string hintText = _multiMode
                ? "雙擊列可快速確認；複選請按住 Ctrl 或 Shift 再點列，然後按「加入選取角色」。"
                : "雙擊列可快速確認；若只需一個角色，請選取一列後按「確定」。（複選多列後按確定會提示改為單選）";

            // ── 頂部：標題帶 + 左側色條 ─────────────────────────────
            var header = new Panel { Dock = DockStyle.Top, Height = 124, BackColor = Theme.BgDialogHeader };
            var accentBar = new Panel
            {
                Dock      = DockStyle.Left,
                Width     = 4,
                BackColor = Theme.AccentLineSubtle
            };
            var headerInner = new Panel
            {
                Dock      = DockStyle.Fill,
                Padding   = new Padding(Theme.UiPadLg, 18, Theme.UiPadLg, 14),
                BackColor = Theme.BgDialogHeader
            };

            var lblTitle = new Label
            {
                Text      = "選擇角色",
                Font      = Theme.FontPageTitle,
                ForeColor = Theme.TextPrimary,
                AutoSize  = true,
                Location  = new Point(0, 0)
            };
            var lblMeta = new Label
            {
                Text      = $"主帳號 「{_masterName}」 · 共 {_players.Count} 個角色",
                Font      = Theme.FontSmall,
                ForeColor = Theme.TextMuted,
                AutoSize  = true,
                Location  = new Point(0, 36)
            };
            var lblHint = new Label
            {
                Text      = hintText,
                Font      = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize  = false,
                Location  = new Point(0, 62),
                Height    = 44,
                TextAlign = ContentAlignment.TopLeft
            };
            headerInner.Controls.AddRange(new Control[] { lblTitle, lblMeta, lblHint });
            headerInner.Resize += (_, _) => { lblHint.Width = Math.Max(200, headerInner.ClientSize.Width); };

            header.Controls.Add(accentBar);
            header.Controls.Add(headerInner);

            // ── 底部：按鈕列（主操作在左、取消固定右下）──────────────
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 68, BackColor = Theme.BgDark };
            footer.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Theme.Border });

            const int btnH = 36;
            _btnOk = Theme.MakeButton(_multiMode ? "加入選取角色" : "確定", Theme.AccentBlue, Color.White,
                _multiMode ? 140 : 100, btnH);
            _btnOk.Click += (_, _) =>
            {
                if (_dgv.SelectedRows.Count == 0)
                {
                    MessageBox.Show("請先選擇角色。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (_multiMode)
                {
                    SelectedPlayers.Clear();
                    foreach (DataGridViewRow r in _dgv.SelectedRows)
                        if (r.Tag is PlayerInfo p) SelectedPlayers.Add(p);
                    SelectedPlayer = SelectedPlayers.Count > 0 ? SelectedPlayers[0] : null;
                }
                else
                {
                    if (_dgv.SelectedRows.Count > 1)
                    {
                        MessageBox.Show(
                            "此步驟一次只能操作一個角色。請只保留一列選取，或點表格空白處後再單點一列。",
                            "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    SelectedPlayer  = _dgv.SelectedRows[0].Tag as PlayerInfo;
                    SelectedPlayers = SelectedPlayer != null ? new List<PlayerInfo> { SelectedPlayer } : new List<PlayerInfo>();
                }
                DialogResult = DialogResult.OK;
            };

            _btnCancel = Theme.MakeGhostButton("取消", 92, btnH);
            _btnCancel.Click += (_, _) => DialogResult = DialogResult.Cancel;

            var leftActions = new FlowLayoutPanel
            {
                FlowDirection     = FlowDirection.LeftToRight,
                WrapContents      = false,
                AutoSize          = true,
                AutoSizeMode      = AutoSizeMode.GrowAndShrink,
                BackColor         = Color.Transparent,
                Padding           = new Padding(0),
                Margin            = new Padding(0),
                Location          = new Point(Theme.UiPadMd, 16)
            };
            leftActions.Controls.Add(_btnOk);

            if (_multiMode)
            {
                var btnAll = Theme.MakeButton("全選", Theme.BgMid, Theme.AccentGreen, 72, btnH);
                btnAll.Margin = new Padding(10, 0, 0, 0);
                btnAll.Click += (_, _) => _dgv.SelectAll();
                leftActions.Controls.Add(btnAll);

                var btnNone = Theme.MakeGhostButton("取消全選", 96, btnH);
                btnNone.Margin = new Padding(8, 0, 0, 0);
                btnNone.Click += (_, _) => _dgv.ClearSelection();
                leftActions.Controls.Add(btnNone);
            }

            footer.Controls.Add(leftActions);
            footer.Controls.Add(_btnCancel);
            footer.Resize += (_, _) =>
            {
                _btnCancel.Location = new Point(footer.ClientSize.Width - _btnCancel.Width - Theme.UiPadMd, 16);
            };
            _btnCancel.Location = new Point(footer.ClientSize.Width - _btnCancel.Width - Theme.UiPadMd, 16);
            _btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            // ── 中央：表格外框 + 列表 ───────────────────────────────
            var body = new Panel
            {
                Dock    = DockStyle.Fill,
                Padding = new Padding(Theme.UiPadMd, 8, Theme.UiPadMd, Theme.UiPadMd),
                BackColor = Theme.BgPage
            };
            var frame = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Theme.Border,
                Padding   = new Padding(1)
            };
            var sheet = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Theme.BgInset
            };

            _dgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridViewDialog(_dgv);
            _dgv.ReadOnly      = true;
            _dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _dgv.MultiSelect   = true;

            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cStatus", HeaderText = "狀態", Width = 72 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cName", HeaderText = "角色名稱", Width = 130 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cAccount", HeaderText = "UID（cdkey）", Width = 240 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cPay", HeaderText = "儲值 NT$", Width = 100 });
            var colLogin = new DataGridViewTextBoxColumn { Name = "cLogin", HeaderText = "最後登入", FillWeight = 100, MinimumWidth = 180 };
            colLogin.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _dgv.Columns.Add(colLogin);

            static Color OnlineTint() => Color.FromArgb(34, 48, 42);

            foreach (var p in _players)
            {
                int i = _dgv.Rows.Add(
                    p.IsOnline ? "在線" : "離線",
                    p.OnlineName,
                    p.Account,
                    p.PayTotal.ToString("N0"),
                    p.LoginTime);
                _dgv.Rows[i].Tag = p;
                if (p.IsOnline)
                {
                    var st = _dgv.Rows[i].DefaultCellStyle;
                    st.BackColor               = OnlineTint();
                    st.ForeColor               = Theme.TextPrimary;
                    st.SelectionBackColor      = Color.FromArgb(30, 110, 75);
                    st.SelectionForeColor      = Color.White;
                    _dgv.Rows[i].Cells["cStatus"].Style.ForeColor = Theme.AccentGreen;
                }
            }

            _dgv.CellDoubleClick += (_, e) =>
            {
                if (e.RowIndex < 0) return;
                if (_dgv.Rows[e.RowIndex].Tag is not PlayerInfo p) return;
                SelectedPlayer  = p;
                SelectedPlayers = new List<PlayerInfo> { p };
                DialogResult    = DialogResult.OK;
            };

            sheet.Controls.Add(_dgv);
            frame.Controls.Add(sheet);
            body.Controls.Add(frame);

            Controls.Add(body);
            Controls.Add(footer);
            Controls.Add(header);

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;
        }
    }

    public static class PlayerPickerHelper
    {
        /// <summary>
        /// 單選：多角色時開啟選擇框；確定時僅允許一列。
        /// </summary>
        public static async Task<PlayerInfo?> PickAsync(Form parent, string query, string title = "選擇角色")
        {
            var list = await PickMultiAsync(parent, query, multiMode: false);
            return list?.Count > 0 ? list[0] : null;
        }

        /// <summary>
        /// 多選：多角色時開啟選擇框，可全選／部分選。
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

            using var dlg = new PlayerPickerDialog(query.Trim(), players, multiMode);
            if (dlg.ShowDialog(parent) == DialogResult.OK)
                return dlg.SelectedPlayers;

            return null;
        }
    }
}
