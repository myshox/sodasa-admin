using System;
using System.Drawing;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    /// <summary>一鍵清除遊戲內郵件（全服 / 在線玩家 / 指定帳號）</summary>
    public class MailClearForm : Form
    {
        private RadioButton _rbAll, _rbOnline, _rbSingle;
        private TextBox     _txtAccount;
        private CheckBox    _chkUnclaimedOnly;
        private Button      _btnClear;
        private Label       _statusLbl;

        public MailClearForm()
        {
            BackColor       = Theme.BgPage;
            ForeColor       = Theme.TextPrimary;
            Font            = Theme.FontBody;
            FormBorderStyle = FormBorderStyle.None;
            InitUI();
        }

        private void InitUI()
        {
            int y = 24;
            const int x = 24;

            // ── 標題 ──
            var lTitle = new Label
            {
                Text      = "🗑  清除遊戲內郵件",
                Font      = new Font(Theme.FontFamily, 14, FontStyle.Bold),
                ForeColor = Theme.AccentRed,
                AutoSize  = true,
                Location  = new Point(x, y)
            };
            y += 42;

            // ── 目標選擇 ──
            var grpTarget = new GroupBox
            {
                Text      = "  目標範圍",
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                Location  = new Point(x, y),
                Size      = new Size(560, 100),
                BackColor = Theme.BgCard
            };

            _rbOnline = new RadioButton
            {
                Text      = "🟢 僅在線玩家",
                Location  = new Point(160, 24),
                AutoSize  = true,
                ForeColor = Theme.TextPrimary,
                Checked   = true
            };
            _rbAll = new RadioButton
            {
                Text      = "🌐 全部玩家",
                Location  = new Point(16, 24),
                AutoSize  = true,
                ForeColor = Theme.TextPrimary
            };
            _rbSingle = new RadioButton
            {
                Text      = "📋 指定帳號",
                Location  = new Point(320, 24),
                AutoSize  = true,
                ForeColor = Theme.TextPrimary
            };

            _txtAccount = new TextBox
            {
                Location        = new Point(16, 58),
                Size            = new Size(524, 24),
                BackColor       = Theme.BgInput,
                ForeColor       = Theme.TextPrimary,
                BorderStyle     = BorderStyle.FixedSingle,
                Font            = Theme.FontBody,
                PlaceholderText = "輸入玩家帳號（選擇「指定帳號」時生效）",
                Enabled         = false
            };
            _rbSingle.CheckedChanged += (s, e) => _txtAccount.Enabled = _rbSingle.Checked;

            grpTarget.Controls.AddRange(new Control[] { _rbAll, _rbOnline, _rbSingle, _txtAccount });
            y += 110;

            // ── 選項 ──
            _chkUnclaimedOnly = new CheckBox
            {
                Text      = "只清除未領取郵件（check=0，保留已領取的歷史記錄）",
                Location  = new Point(x, y),
                AutoSize  = true,
                ForeColor = Theme.TextPrimary,
                Checked   = true
            };
            y += 32;

            // ── 警告 ──
            var lWarn = new Label
            {
                Text      = "⚠  此操作為軟刪除（deleamill=1），玩家信箱會立即清空，操作不可逆，請謹慎使用。",
                ForeColor = Theme.AccentOrange,
                Font      = Theme.FontSmall,
                Location  = new Point(x, y),
                AutoSize  = true
            };
            y += 32;

            // ── 執行按鈕 ──
            _btnClear = Theme.MakeButton("🗑  執行清除", Theme.AccentRed, Color.White, 160, 38);
            _btnClear.Location = new Point(x, y);
            _btnClear.Font     = new Font(Theme.FontFamily, 11, FontStyle.Bold);
            _btnClear.Click   += async (s, e) => await DoClearAsync();
            y += 52;

            // ── 結果顯示 ──
            _statusLbl = new Label
            {
                Location  = new Point(x, y),
                Size      = new Size(620, 60),
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontBody,
                AutoSize  = false
            };

            Controls.AddRange(new Control[]
            {
                lTitle, grpTarget, _chkUnclaimedOnly, lWarn, _btnClear, _statusLbl
            });
        }

        private async System.Threading.Tasks.Task DoClearAsync()
        {
            bool   unclaimedOnly = _chkUnclaimedOnly.Checked;
            bool   onlineOnly    = _rbOnline.Checked;
            string account       = _rbSingle.Checked ? _txtAccount.Text.Trim() : "";

            if (_rbSingle.Checked && string.IsNullOrWhiteSpace(account))
            {
                MessageBox.Show("請輸入玩家帳號", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string scopeLabel = _rbAll.Checked    ? "全部玩家"
                              : _rbOnline.Checked  ? "在線玩家"
                                                   : $"玩家「{account}」";
            string typeLabel  = unclaimedOnly ? "未領取郵件" : "全部郵件";

            if (MessageBox.Show(
                    $"確定清除「{scopeLabel}」的{typeLabel}？\n此操作不可逆！",
                    "確認清除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            _btnClear.Enabled    = false;
            _statusLbl.ForeColor = Theme.AccentOrange;
            _statusLbl.Text      = "處理中，請稍候…";

            try
            {
                int count = await DatabaseManager.Instance.ClearPlayerMailAsync(account, unclaimedOnly, onlineOnly);
                _statusLbl.ForeColor = Theme.AccentGreen;
                _statusLbl.Text      = $"✓ 清除完成！共清除 {count} 封郵件\n目標：{scopeLabel}  類型：{typeLabel}";
            }
            catch (Exception ex)
            {
                _statusLbl.ForeColor = Theme.AccentRed;
                _statusLbl.Text      = "✗ 清除失敗：" + ex.Message;
            }
            finally
            {
                if (!IsDisposed) _btnClear.Enabled = true;
            }
        }
    }
}
