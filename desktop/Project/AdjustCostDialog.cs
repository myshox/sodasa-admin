using System;
using System.Drawing;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    /// <summary>消費達成進度調整對話框（對應 costdata.point）</summary>
    public class AdjustCostDialog : Form
    {
        public long AddPoint { get; private set; }

        private NumericUpDown _nudAmount;

        public AdjustCostDialog(string charName, long currentPoint, string account)
        {
            Text          = "調整消費達成點數";
            Size          = new Size(420, 280);
            MinimumSize   = new Size(380, 260);
            BackColor     = Theme.BgPage;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox   = false;
            MinimizeBox   = false;
            StartPosition = FormStartPosition.CenterParent;

            var layout = new TableLayoutPanel
            {
                Dock    = DockStyle.Fill,
                Padding = new Padding(20, 16, 20, 16),
                RowCount = 5, ColumnCount = 1,
                BackColor = Color.Transparent
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));  // 標題
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));  // 目前點數
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));  // 里程碑提示
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50f));  // 輸入
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,  100f)); // 按鈕
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            layout.Controls.Add(new Label
            {
                Text      = $"⚙  調整「{charName}」消費達成進度",
                ForeColor = Color.FromArgb(180, 130, 255),
                Font      = new Font(Theme.FontFamily, 11f, FontStyle.Bold),
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            layout.Controls.Add(new Label
            {
                Text      = $"目前累計消費點數：{currentPoint:N0} 金幣",
                ForeColor = Theme.TextSecondary,
                Font      = Theme.FontSmall,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 1);

            string milestonesHint = "里程碑：3,000 / 5,000 / 10,000 / 50,000 / 100,000 金幣";
            layout.Controls.Add(new Label
            {
                Text      = milestonesHint,
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 2);

            // 輸入區
            var inputPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
                BackColor = Color.Transparent
            };
            inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            inputPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            inputPanel.Controls.Add(new Label
            {
                Text = "增加點數：", ForeColor = Theme.TextSecondary,
                Font = Theme.FontBody, AutoSize = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Margin = new Padding(0, 12, 8, 0)
            }, 0, 0);

            _nudAmount = new NumericUpDown
            {
                Minimum   = 1,
                Maximum   = 10_000_000,
                Value     = 1000,
                Increment = 1000,
                BackColor = Theme.BgLight,
                ForeColor = Theme.TextPrimary,
                Font      = Theme.FontBody,
                Dock      = DockStyle.Fill,
                Margin    = new Padding(0, 8, 0, 0),
                ThousandsSeparator = true
            };
            inputPanel.Controls.Add(_nudAmount, 1, 0);
            layout.Controls.Add(inputPanel, 0, 3);

            // 按鈕
            var btnPanel = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                BackColor     = Color.Transparent,
                Padding       = new Padding(0, 6, 0, 0)
            };
            var btnOk = Theme.MakePrimaryButton("✅ 確認增加", 120, 34);
            btnOk.Click += (s, e) =>
            {
                AddPoint = (long)_nudAmount.Value;
                DialogResult = DialogResult.OK;
            };
            var btnCancel = Theme.MakeGhostButton("取消", 80, 34);
            btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;
            btnPanel.Controls.Add(btnOk);
            btnPanel.Controls.Add(btnCancel);
            layout.Controls.Add(btnPanel, 0, 4);

            Controls.Add(layout);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }
}
