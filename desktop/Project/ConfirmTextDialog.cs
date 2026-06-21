using System;
using System.Drawing;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    /// <summary>
    /// 強制輸入指定字串才能確認的對話框（用於高風險操作的最終確認）。
    /// 範例：清空全表、刪除使用者等不可復原的操作。
    /// </summary>
    public class ConfirmTextDialog : Form
    {
        private readonly string _expected;
        private readonly TextBox _txt;
        private readonly Button  _btnOk;
        private readonly Label   _lblHint;

        public ConfirmTextDialog(string title, string promptText, string expectedAnswer)
        {
            _expected = expectedAnswer ?? "";

            Text            = title;
            Size            = new Size(460, 230);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterParent;
            MaximizeBox     = false;
            MinimizeBox     = false;
            BackColor       = Theme.BgPage;
            ForeColor       = Theme.TextPrimary;
            Font            = Theme.FontBody;

            var lblIcon = new Label
            {
                Text      = "⚠️",
                Font      = new Font("Segoe UI Emoji", 28f),
                Location  = new Point(18, 18),
                Size      = new Size(48, 48),
                TextAlign = ContentAlignment.MiddleCenter
            };
            Controls.Add(lblIcon);

            var lblPrompt = new Label
            {
                Text     = promptText,
                ForeColor = Color.FromArgb(255, 200, 80),
                Font     = new Font(Theme.FontFamily, 9.5f, FontStyle.Bold),
                Location = new Point(76, 20),
                Size     = new Size(360, 60)
            };
            Controls.Add(lblPrompt);

            _txt = new TextBox
            {
                Location = new Point(20, 90),
                Size     = new Size(416, 28),
                Font     = new Font(Theme.FontFamily, 11f, FontStyle.Bold),
                BackColor = Theme.BgCard,
                ForeColor = Color.White
            };
            _txt.TextChanged += (s, e) => UpdateOkState();
            Controls.Add(_txt);

            _lblHint = new Label
            {
                Text     = $"必須完全輸入：「{_expected}」",
                ForeColor = Color.FromArgb(160, 160, 160),
                Font     = Theme.FontSmall,
                Location = new Point(22, 122),
                AutoSize = true
            };
            Controls.Add(_lblHint);

            _btnOk = Theme.MakeButton("✓ 確認執行", Color.FromArgb(200, 50, 50), Color.White, 110, 32);
            _btnOk.Location = new Point(218, 150);
            _btnOk.Enabled  = false;
            _btnOk.Click   += (s, e) => { DialogResult = DialogResult.OK; Close(); };
            Controls.Add(_btnOk);

            var btnCancel = Theme.MakeButton("✕ 取消", Color.FromArgb(80, 80, 80), Color.White, 90, 32);
            btnCancel.Location = new Point(338, 150);
            btnCancel.Click   += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnCancel);

            AcceptButton = _btnOk;
            CancelButton = btnCancel;
            _txt.Focus();
        }

        private void UpdateOkState()
        {
            bool match = string.Equals(_txt.Text.Trim(), _expected, StringComparison.Ordinal);
            _btnOk.Enabled = match;
            _lblHint.ForeColor = match
                ? Color.FromArgb(80, 220, 120)
                : Color.FromArgb(160, 160, 160);
            _lblHint.Text = match
                ? "✓ 字串正確，可確認執行"
                : $"必須完全輸入：「{_expected}」";
        }
    }
}
