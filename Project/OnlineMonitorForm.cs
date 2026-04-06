using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    public class OnlineMonitorForm : Form
    {
        private DataGridView _dgv;
        private Label        _statusLbl, _countLbl;
        private Button       _btnRefresh;
        private System.Windows.Forms.Timer _timer;
        private bool _loading;

        public OnlineMonitorForm()
        {
            Text          = "🟢 線上玩家監控";
            Size          = new Size(1000, 640);
            MinimumSize   = new Size(720, 460);
            BackColor     = Theme.BgMid;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;

            BuildUI();
            _ = RefreshAsync();
            StartAutoRefresh();
        }

        private void BuildUI()
        {
            // ── Header ──
            var header = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme.BgDark };
            _btnRefresh = Theme.MakePrimaryButton("🔄 立即刷新", 110, 28);
            _btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnRefresh.Click += (s, e) => _ = RefreshAsync();
            header.Controls.Add(new Label
            {
                Text      = "  🟢  線上玩家監控  —  即時顯示目前在線玩家（每30秒自動刷新）",
                ForeColor = Theme.AccentGreen,
                Font      = Theme.FontBody,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(8, 0, 0, 0)
            });
            header.Controls.Add(_btnRefresh);
            header.Resize += (s, e) =>
            {
                _btnRefresh.Left = header.Width - _btnRefresh.Width - 12;
                _btnRefresh.Top  = 8;
            };

            // ── 提示列 ──
            var infoBar = new Panel { Dock = DockStyle.Top, Height = 24, BackColor = Color.FromArgb(14, 50, 14) };
            infoBar.Controls.Add(new Label
            {
                Text      = "  💡 雙擊列 = 開啟玩家詳情   |   顯示上限：全服在線玩家",
                ForeColor = Color.FromArgb(100, 220, 100), Font = Theme.FontSmall,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft
            });

            // ── 狀態列 ──
            var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = Theme.BgDark };
            _countLbl  = new Label { Text = "🟢 0 人在線", ForeColor = Theme.AccentGreen, Font = Theme.FontSmall, AutoSize = true, Location = new Point(12, 7) };
            _statusLbl = new Label { Text = "", ForeColor = Theme.TextMuted, Font = Theme.FontSmall, AutoSize = true, Location = new Point(110, 7) };
            statusBar.Controls.AddRange(new Control[] { _countLbl, _statusLbl });

            // ── DataGridView ──
            _dgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(_dgv);
            _dgv.ReadOnly           = true;
            _dgv.RowTemplate.Height = 36;
            _dgv.CellDoubleClick   += OpenPlayerProfile;

            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cOnline",     HeaderText = "狀態",      FillWeight = 50,  MinimumWidth = 55  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cCharName",   HeaderText = "角色名稱",  FillWeight = 120, MinimumWidth = 80  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cAccount",    HeaderText = "帳號",      FillWeight = 120, MinimumWidth = 80  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cMaster",     HeaderText = "主帳號",    FillWeight = 100, MinimumWidth = 70  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cServerId",   HeaderText = "伺服器",    FillWeight = 60,  MinimumWidth = 50  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cLoginTime",  HeaderText = "登入時間",  FillWeight = 120, MinimumWidth = 90  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "cPayTotal", HeaderText = "累積充值", FillWeight = 80, MinimumWidth = 70,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            Theme.AddNumericAwareSort(_dgv, "cPayTotal");

            Controls.Add(_dgv);
            Controls.Add(statusBar);
            Controls.Add(infoBar);
            Controls.Add(header);
        }

        // ── 刷新在線列表 ─────────────────────────────────────────
        private async Task RefreshAsync()
        {
            if (_loading) return;
            _loading = true;
            _btnRefresh.Enabled = false;
            _statusLbl.Text     = "刷新中…";
            try
            {
                var players = await DatabaseManager.Instance.GetOnlinePlayersAsync();
                _dgv.Rows.Clear();
                foreach (var p in players)
                {
                    int i = _dgv.Rows.Add(
                        "🟢 在線",
                        p.OnlineName,
                        p.Account,
                        string.IsNullOrEmpty(p.MasterName) ? "—" : p.MasterName,
                        p.ServerId,
                        p.LoginTime,
                        p.PayTotal > 0 ? $"NT${p.PayTotal:N0}" : "—");
                    _dgv.Rows[i].Tag = p;
                    _dgv.Rows[i].DefaultCellStyle.ForeColor = Theme.AccentGreen;
                }
                _countLbl.Text  = $"🟢 {players.Count} 人在線";
                _statusLbl.Text = $"上次刷新：{DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex) { _statusLbl.Text = "✗ " + ex.Message; }
            finally { _loading = false; _btnRefresh.Enabled = true; }
        }

        // ── 自動刷新（30秒）─────────────────────────────────────
        private void StartAutoRefresh()
        {
            _timer = new System.Windows.Forms.Timer { Interval = 30_000 };
            _timer.Tick += (s, e) => _ = RefreshAsync();
            _timer.Start();
            FormClosed += (s, e) => _timer.Stop();
        }

        // ── 雙擊開啟玩家詳情 ─────────────────────────────────────
        private void OpenPlayerProfile(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (_dgv.Rows[e.RowIndex].Tag is PlayerInfo p)
                new PlayerProfileForm(p).ShowDialog(this);
        }
    }
}
