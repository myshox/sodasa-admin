using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    public class BanDialog : Form
    {
        private readonly PlayerInfo _player;
        private Label      _statusLbl;
        private ComboBox   _cbDuration;
        private TextBox    _txtReason;
        private Button     _btnBan, _btnUnban, _btnClose;
        private Label      _banInfoLbl;

        public BanDialog(PlayerInfo player)
        {
            _player = player;
            InitUI();
            _ = LoadBanStatusAsync();
        }

        private void InitUI()
        {
            Text            = $"🚫 封禁管理 — {_player.OnlineName}";
            Size            = new Size(480, 360);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition   = FormStartPosition.CenterParent;
            BackColor       = Theme.BgMid;
            ForeColor       = Theme.TextPrimary;
            Font            = Theme.FontBody;

            int y = 20, x = 24;

            // 玩家資訊
            var hdr = new Panel { Location = new Point(0, 0), Size = new Size(480, 52), BackColor = Theme.BgDark };
            hdr.Controls.Add(new Label
            {
                Text = $"👤 {_player.OnlineName}（{_player.Account}）  {_player.OnlineText}",
                ForeColor = _player.IsOnline ? Theme.AccentGreen : Theme.TextSecondary,
                Font = Theme.FontHeader, AutoSize = true, Location = new Point(16, 16)
            });
            Controls.Add(hdr);
            y = 68;

            // 封禁狀態顯示
            _banInfoLbl = new Label
            {
                Text = "查詢中…", ForeColor = Theme.AccentOrange,
                Font = Theme.FontHeader, AutoSize = true, Location = new Point(x, y)
            };
            Controls.Add(_banInfoLbl);
            y += 36;

            // 封禁時長
            var lbl1 = Theme.MakeLabel("封禁時長：", Theme.TextSecondary);
            lbl1.Location = new Point(x, y + 4); Controls.Add(lbl1);

            _cbDuration = new ComboBox
            {
                Location = new Point(x + 80, y), Width = 200,
                BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary,
                DropDownStyle = ComboBoxStyle.DropDownList, Font = Theme.FontBody
            };
            _cbDuration.Items.AddRange(new object[]
            {
                "1 小時", "6 小時", "12 小時", "1 天", "3 天",
                "7 天", "30 天", "90 天", "永久封禁"
            });
            _cbDuration.SelectedIndex = 3; // 預設 1 天
            Controls.Add(_cbDuration);
            y += 40;

            // 封禁原因
            var lbl2 = Theme.MakeLabel("封禁原因：", Theme.TextSecondary);
            lbl2.Location = new Point(x, y + 4); Controls.Add(lbl2);

            _txtReason = Theme.MakeTextBox(280);
            _txtReason.Location = new Point(x + 80, y);
            _txtReason.PlaceholderText = "請輸入原因（選填）";
            Controls.Add(_txtReason);
            y += 40;

            // 操作按鈕
            _btnBan = Theme.MakeButton("🚫 執行封禁", Theme.AccentRed, Color.White, 130, 34);
            _btnBan.Location = new Point(x, y);
            _btnBan.Click += BtnBan_Click;
            Controls.Add(_btnBan);

            _btnUnban = Theme.MakeButton("✅ 解除封禁", Theme.AccentGreen, Color.White, 130, 34);
            _btnUnban.Location = new Point(x + 140, y);
            _btnUnban.Click += BtnUnban_Click;
            Controls.Add(_btnUnban);

            _btnClose = Theme.MakeButton("關  閉", Theme.BgLight, Theme.TextSecondary, 80, 34);
            _btnClose.Location = new Point(x + 290, y);
            _btnClose.Click += (s, e) => Close();
            Controls.Add(_btnClose);
            y += 52;

            _statusLbl = new Label
            {
                Text = "", ForeColor = Theme.TextSecondary, Font = Theme.FontSmall,
                AutoSize = true, Location = new Point(x, y)
            };
            Controls.Add(_statusLbl);
        }

        private async Task LoadBanStatusAsync()
        {
            try
            {
                var (banned, endTime) = await DatabaseManager.Instance.GetBanStatusAsync(_player.Account);
                if (InvokeRequired) { Invoke(new Action(() => UpdateBanInfo(banned, endTime))); }
                else UpdateBanInfo(banned, endTime);
            }
            catch { }
        }

        private void UpdateBanInfo(bool banned, string endTime)
        {
            if (banned)
            {
                _banInfoLbl.Text = $"🔴 已封禁  到期：{endTime}";
                _banInfoLbl.ForeColor = Theme.AccentRed;
            }
            else
            {
                _banInfoLbl.Text = "🟢 帳號正常（未封禁）";
                _banInfoLbl.ForeColor = Theme.AccentGreen;
            }
        }

        private async void BtnBan_Click(object sender, EventArgs e)
        {
            if (!Confirm($"確定要封禁「{_player.OnlineName}」嗎？")) return;

            int endUnix = CalcEndTime(_cbDuration.SelectedIndex);
            _btnBan.Enabled = false;
            _statusLbl.Text = "處理中…";
            try
            {
                bool ok = await DatabaseManager.Instance.BanPlayerAsync(
                    _player.Account, endUnix, _txtReason.Text.Trim());
                if (ok)
                {
                    _statusLbl.Text      = "✓ 封禁成功！";
                    _statusLbl.ForeColor = Theme.AccentGreen;
                    await LoadBanStatusAsync();
                }
                else { _statusLbl.Text = "✗ 封禁失敗"; _statusLbl.ForeColor = Theme.AccentRed; }
            }
            catch (Exception ex) { _statusLbl.Text = "✗ " + ex.Message; _statusLbl.ForeColor = Theme.AccentRed; }
            finally { _btnBan.Enabled = true; }
        }

        private async void BtnUnban_Click(object sender, EventArgs e)
        {
            if (!Confirm($"確定要解除「{_player.OnlineName}」的封禁？")) return;
            _btnUnban.Enabled = false;
            _statusLbl.Text = "處理中…";
            try
            {
                bool ok = await DatabaseManager.Instance.UnbanPlayerAsync(_player.Account);
                if (ok)
                {
                    _statusLbl.Text = "✓ 解除封禁成功！"; _statusLbl.ForeColor = Theme.AccentGreen;
                    await LoadBanStatusAsync();
                }
                else { _statusLbl.Text = "✗ 解除失敗（可能未被封禁）"; _statusLbl.ForeColor = Theme.AccentRed; }
            }
            catch (Exception ex) { _statusLbl.Text = "✗ " + ex.Message; _statusLbl.ForeColor = Theme.AccentRed; }
            finally { _btnUnban.Enabled = true; }
        }

        private static int CalcEndTime(int idx)
        {
            DateTime dt = idx switch
            {
                0 => DateTime.Now.AddHours(1),
                1 => DateTime.Now.AddHours(6),
                2 => DateTime.Now.AddHours(12),
                3 => DateTime.Now.AddDays(1),
                4 => DateTime.Now.AddDays(3),
                5 => DateTime.Now.AddDays(7),
                6 => DateTime.Now.AddDays(30),
                7 => DateTime.Now.AddDays(90),
                _ => DateTime.Now.AddYears(100) // 永久
            };
            return (int)new DateTimeOffset(dt).ToUnixTimeSeconds();
        }

        private bool Confirm(string msg) =>
            MessageBox.Show(msg, "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
            == DialogResult.Yes;
    }
}
