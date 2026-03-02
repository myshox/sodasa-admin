using System;
using System.Drawing;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    /// <summary>
    /// 消費達成里程碑補發對話框：
    ///   模式 A — 同步遊戲（退 check，遊戲自動發到背包）
    ///   模式 B — 郵件發道具（直接寄出，同時標 check）
    /// </summary>
    public class CostClaimDialog : Form
    {
        public bool   UseMailMode { get; private set; }
        public int    ItemId      { get; private set; }
        public string ItemName    { get; private set; } = "";
        public int    ItemQty     { get; private set; }

        private RadioButton     _rbSync, _rbMail;
        private TableLayoutPanel _mailPanel;
        private NumericUpDown    _nudId, _nudQty;
        private TextBox          _txtName;

        public CostClaimDialog(string account, int milestoneIdx, long milestone)
        {
            Text            = $"補發里程碑 {milestoneIdx + 1}（{milestone:N0} 金幣）";
            Size            = new Size(480, 380);
            MinimumSize     = new Size(440, 360);
            BackColor       = Theme.BgPage;
            ForeColor       = Theme.TextPrimary;
            Font            = Theme.FontBody;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1,
                Padding = new Padding(20, 14, 20, 14), BackColor = Color.Transparent
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent,  100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            // 標題
            root.Controls.Add(new Label
            {
                Text = $"玩家 {account}　第 {milestoneIdx + 1} 里程碑補發",
                ForeColor = Color.FromArgb(180, 130, 255),
                Font = new Font(Theme.FontFamily, 11f, FontStyle.Bold),
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            // 模式 A
            _rbSync = new RadioButton
            {
                Text = "🔄  同步遊戲（退 check → 遊戲伺服器自動發道具到背包）【推薦】",
                ForeColor = Theme.TextPrimary, BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat, Font = Theme.FontBody,
                Dock = DockStyle.Fill, Checked = true
            };
            _rbSync.CheckedChanged += (s, e) => _mailPanel.Visible = !_rbSync.Checked;
            root.Controls.Add(_rbSync, 0, 1);

            // 模式 B
            _rbMail = new RadioButton
            {
                Text = "📬  郵件發道具（直接寄出，需輸入道具 ID）",
                ForeColor = Theme.TextMuted, BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat, Font = Theme.FontBody,
                Dock = DockStyle.Fill
            };
            _rbMail.CheckedChanged += (s, e) => { _mailPanel.Visible = _rbMail.Checked; _rbMail.ForeColor = _rbMail.Checked ? Theme.TextPrimary : Theme.TextMuted; };
            root.Controls.Add(_rbMail, 0, 2);

            // 郵件設定（預設隱藏）
            _mailPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2,
                BackColor = Color.FromArgb(28, 30, 50),
                Padding = new Padding(10, 6, 10, 6), Visible = false
            };
            _mailPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _mailPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110f));
            _mailPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _mailPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            _mailPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

            _mailPanel.Controls.Add(MakeLabel("道具 ID："), 0, 0);
            _nudId = new NumericUpDown { Minimum = 1, Maximum = 9_999_999, Value = 100104, BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary, Dock = DockStyle.Fill, Font = Theme.FontBody };
            var idHint = new Label { Text = "79MM=100103  綁定79MM=100104", ForeColor = Theme.TextMuted, Font = Theme.FontSmall, Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
            var idRow = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = System.Drawing.Color.Transparent, FlowDirection = FlowDirection.TopDown };
            idRow.Controls.Add(_nudId); idRow.Controls.Add(idHint);
            _mailPanel.Controls.Add(idRow, 1, 0);

            _mailPanel.Controls.Add(MakeLabel("道具名稱："), 0, 1);
            _txtName = new TextBox { Text = "綁定79MM", BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary, Dock = DockStyle.Fill, Font = Theme.FontBody };
            _mailPanel.Controls.Add(_txtName, 1, 1);

            _mailPanel.Controls.Add(MakeLabel("數量："), 2, 0);
            _nudQty = new NumericUpDown { Minimum = 1, Maximum = 99_999_999, Value = 1, Increment = 1, BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary, Dock = DockStyle.Fill, Font = Theme.FontBody, ThousandsSeparator = true };
            _mailPanel.Controls.Add(_nudQty, 2, 1);

            root.Controls.Add(_mailPanel, 0, 3);

            // 按鈕
            var btnRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
                BackColor = Color.Transparent, Padding = new Padding(0, 6, 0, 0)
            };
            var btnOk = Theme.MakePrimaryButton("✅ 確認補發", 120, 34);
            btnOk.Click += (s, e) =>
            {
                UseMailMode = _rbMail.Checked;
                ItemId      = (int)_nudId.Value;
                ItemName    = _txtName.Text.Trim();
                ItemQty     = (int)_nudQty.Value;
                DialogResult = DialogResult.OK;
            };
            var btnCancel = Theme.MakeGhostButton("取消", 80, 34);
            btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;
            btnRow.Controls.Add(btnOk);
            btnRow.Controls.Add(btnCancel);
            root.Controls.Add(btnRow, 0, 4);

            Controls.Add(root);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private Label MakeLabel(string text) => new Label
        {
            Text = text, ForeColor = Theme.TextSecondary, Font = Theme.FontSmall,
            AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top,
            Margin = new Padding(0, 8, 6, 0)
        };
    }
}
