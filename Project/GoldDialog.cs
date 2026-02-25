using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    /// <summary>
    /// 貨幣管理器
    ///   可修改：金幣（VipPoint）、水晶（PetPoint）
    ///   唯讀說明：石幣、聲望、戰點（存於伺服器二進位角色檔案）
    /// </summary>
    public class GoldDialog : Form
    {
        private readonly PlayerInfo _player;
        private PlayerCurrencies _cur;

        // ── 金幣 ────────────────────────────────────────
        private Label         _goldLbl;
        private NumericUpDown _nudGold;
        private Button        _btnSetGold;
        private Label         _goldStatusLbl;

        // ── 水晶 ────────────────────────────────────────
        private Label         _crystalLbl;
        private NumericUpDown _nudCrystal;
        private Button        _btnSetCrystal;
        private Label         _crystalStatusLbl;

        public GoldDialog(PlayerInfo player)
        {
            _player = player;
            InitUI();
            _ = LoadAsync();
        }

        private void InitUI()
        {
            Text          = $"💰 貨幣管理 — {_player.OnlineName}";
            Size          = new Size(520, 520);
            MinimumSize   = new Size(480, 480);
            BackColor = Theme.BgPage;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox   = false;
            StartPosition = FormStartPosition.CenterParent;

            int y = 14, x = 18, w = 460;

            // ── 玩家資訊列 ──────────────────────────────
            var hdr = new Panel { Location = new Point(0, 0), Width = 520, Height = 44, BackColor = Theme.BgCard };
            hdr.Controls.Add(new Label
            {
                Text = $"👤  {_player.OnlineName}（{_player.Account}）  {_player.OnlineText}",
                ForeColor = _player.IsOnline ? Theme.AccentGreen : Theme.TextSecondary,
                Font = Theme.FontHeader, AutoSize = true, Location = new Point(14, 11)
            });
            Controls.Add(hdr);
            y = 54;

            if (_player.IsOnline)
            {
                var warn = new Panel { Location = new Point(x, y), Width = w, Height = 32, BackColor = Theme.BgCard };
                warn.Controls.Add(new Label
                {
                    Text = "⚠ 玩家在線中 — 金幣/水晶修改後，玩家需重新登入才能看到新數值",
                    ForeColor = Theme.AccentOrange, Font = Theme.FontSmall,
                    AutoSize = true, Location = new Point(8, 8)
                });
                Controls.Add(warn);
                y += 38;
            }

            // ═══════════════════════════════════════════
            // 1. 元寶 / 金幣（VipPoint）
            // ═══════════════════════════════════════════
            y = AddSectionHeader("💰 元寶 / 金幣  ( csalogin.VipPoint )  ≠  台幣", Color.FromArgb(255, 200, 0), y, x, w);

            _goldLbl = new Label
            {
                Text = "讀取中…", ForeColor = Color.FromArgb(80, 210, 255),
                Font = new Font(Theme.FontHeader.FontFamily, 18, FontStyle.Bold),
                AutoSize = true, Location = new Point(x, y)
            };
            Controls.Add(_goldLbl);
            y += 38;

            // 增加按鈕列
            y = AddQuickButtons("金幣", y, x, true);

            var setRow = new Panel { Location = new Point(x, y), Width = w, Height = 36 };
            var setLbl = Theme.MakeLabel("設定為：", Theme.TextSecondary); setLbl.Location = new Point(0, 8);
            _nudGold = new NumericUpDown
            {
                Location = new Point(70, 4), Width = 160, Minimum = 0, Maximum = 999999999,
                BackColor = Theme.BgInput, ForeColor = Theme.TextPrimary, Font = Theme.FontBody,
                DecimalPlaces = 0, ThousandsSeparator = true, Increment = 100
            };
            _btnSetGold = Theme.MakeButton("✔ 設定", Theme.AccentBlue, Color.White, 86, 28);
            _btnSetGold.Location = new Point(240, 4);
            _btnSetGold.Click += async (s, e) => await ApplyCurrencyAsync("gold");

            var btnZeroGold = Theme.MakeButton("🗑 清0金幣", Theme.AccentRed, Color.White, 88, 28);
            btnZeroGold.Location = new Point(334, 4);
            btnZeroGold.Click += async (s, e) =>
            {
                if (MessageBox.Show(
                    $"⚠ 確定要將「{_player.OnlineName}」的金幣清除為 0？\n\n此操作無法復原。",
                    "清0確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                await DoSet("gold", 0);
            };

            _goldStatusLbl = new Label { Text = "", ForeColor = Theme.AccentGreen, Font = Theme.FontSmall, AutoSize = true, Location = new Point(430, 10) };
            setRow.Controls.AddRange(new Control[] { setLbl, _nudGold, _btnSetGold, btnZeroGold, _goldStatusLbl });
            Controls.Add(setRow);
            y += 42;

            // ═══════════════════════════════════════════
            // 2. 水晶（PetPoint）
            // ═══════════════════════════════════════════
            y += 6;
            y = AddSectionHeader("💎 水晶  ( csalogin.PetPoint )", Color.FromArgb(100, 200, 255), y, x, w);

            _crystalLbl = new Label
            {
                Text = "讀取中…", ForeColor = Color.FromArgb(100, 200, 255),
                Font = new Font(Theme.FontHeader.FontFamily, 18, FontStyle.Bold),
                AutoSize = true, Location = new Point(x, y)
            };
            Controls.Add(_crystalLbl);
            y += 38;

            y = AddQuickButtons("水晶", y, x, false);

            var setRow2 = new Panel { Location = new Point(x, y), Width = w, Height = 36 };
            var setLbl2 = Theme.MakeLabel("設定為：", Theme.TextSecondary); setLbl2.Location = new Point(0, 8);
            _nudCrystal = new NumericUpDown
            {
                Location = new Point(70, 4), Width = 160, Minimum = 0, Maximum = 999999999,
                BackColor = Theme.BgInput, ForeColor = Theme.TextPrimary, Font = Theme.FontBody,
                DecimalPlaces = 0, ThousandsSeparator = true, Increment = 100
            };
            _btnSetCrystal = Theme.MakeButton("✔ 設定", Theme.AccentBlue, Color.White, 86, 28);
            _btnSetCrystal.Location = new Point(240, 4);
            _btnSetCrystal.Click += async (s, e) => await ApplyCurrencyAsync("crystal");

            var btnZeroCrystal = Theme.MakeButton("🗑 清0水晶", Theme.AccentRed, Color.White, 88, 28);
            btnZeroCrystal.Location = new Point(334, 4);
            btnZeroCrystal.Click += async (s, e) =>
            {
                if (MessageBox.Show(
                    $"⚠ 確定要將「{_player.OnlineName}」的水晶清除為 0？\n\n此操作無法復原。",
                    "清0確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                await DoSet("crystal", 0);
            };

            _crystalStatusLbl = new Label { Text = "", ForeColor = Theme.AccentGreen, Font = Theme.FontSmall, AutoSize = true, Location = new Point(430, 10) };
            setRow2.Controls.AddRange(new Control[] { setLbl2, _nudCrystal, _btnSetCrystal, btnZeroCrystal, _crystalStatusLbl });
            Controls.Add(setRow2);
            y += 42;

            // ═══════════════════════════════════════════
            // 3. 唯讀說明（石幣/聲望/戰點）
            // ═══════════════════════════════════════════
            y += 6;
            var readOnlyBox = new Panel
            {
                Location = new Point(x, y), Width = w, Height = 76,
                BackColor = Theme.BgCard
            };
            readOnlyBox.Controls.Add(new Label
            {
                Text = "🔒  以下貨幣存於伺服器二進位角色檔案，無法透過資料庫讀取或修改：\n\n" +
                       "    🪙 石幣  |  ⭐ 聲望  |  ⚔ 戰點\n\n" +
                       "    需透過遊戲內 GM 指令或修改伺服器角色存檔才能調整",
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                AutoSize = true, Location = new Point(10, 8)
            });
            Controls.Add(readOnlyBox);
        }

        private int AddSectionHeader(string title, Color color, int y, int x, int w)
        {
            var bar = new Panel { Location = new Point(x, y), Width = w, Height = 26, BackColor = Theme.BgCard };
            bar.Controls.Add(new Label { Text = title, ForeColor = color, Font = Theme.FontBody, AutoSize = true, Location = new Point(6, 4) });
            Controls.Add(bar);
            return y + 30;
        }

        private int AddQuickButtons(string type, int y, int x, bool isGold)
        {
            var btnRow = new Panel { Location = new Point(x, y), Width = 468, Height = 30 };
            btnRow.Controls.Add(new Label { Text = "快速增加：", ForeColor = Theme.TextMuted, Font = Theme.FontSmall, Location = new Point(0, 8), AutoSize = true });
            int bx = 72;
            // 使用精簡標籤避免截斷：+百、+千、+萬、+十萬、+百萬
            foreach (var (label, amt) in new[] { ("+100", 100L), ("+1千", 1000L), ("+1萬", 10000L), ("+10萬", 100000L), ("+100萬", 1000000L) })
            {
                long capturedAmt = amt;
                bool capturedGold = isGold;
                var b = Theme.MakeButton(label, Theme.BgLight, Theme.TextSecondary, 72, 24);
                b.Font = Theme.FontSmall; b.Location = new Point(bx, 3); bx += 76;
                b.Click += async (s, e) =>
                {
                    if (capturedGold) await ApplyDeltaAsync("gold",    capturedAmt);
                    else              await ApplyDeltaAsync("crystal", capturedAmt);
                };
                btnRow.Controls.Add(b);
            }
            Controls.Add(btnRow);
            return y + 34;
        }

        // ════════════════════════════════════════════════
        // 資料讀取
        // ════════════════════════════════════════════════
        private async Task LoadAsync()
        {
            try
            {
                _cur = await DatabaseManager.Instance.GetCurrenciesAsync(_player.Account);
                Invoke(new Action(UpdateDisplay));
            }
            catch (Exception ex)
            {
                Invoke(new Action(() =>
                {
                    _goldLbl.Text = "讀取失敗";
                    _goldLbl.ForeColor = Theme.AccentRed;
                    _crystalLbl.Text = ex.Message;
                }));
            }
        }

        private void UpdateDisplay()
        {
            if (_cur == null) return;
            _goldLbl.Text    = _cur.Gold.ToString("N0") + " 元寶(金幣)";
            _crystalLbl.Text = _cur.Crystal.ToString("N0") + " 水晶";
            _nudGold.Value    = (decimal)Math.Min(_nudGold.Maximum, _cur.Gold);
            _nudCrystal.Value = (decimal)Math.Min(_nudCrystal.Maximum, _cur.Crystal);
        }

        // ════════════════════════════════════════════════
        // 設定操作
        // ════════════════════════════════════════════════
        private async Task ApplyCurrencyAsync(string type)
        {
            long newVal = type == "gold" ? (long)_nudGold.Value : (long)_nudCrystal.Value;
            await DoSet(type, newVal);
        }

        private async Task ApplyDeltaAsync(string type, long delta)
        {
            if (_cur == null) await LoadAsync();
            long current = type == "gold" ? _cur.Gold : _cur.Crystal;
            long newVal  = Math.Max(0, current + delta);
            await DoSet(type, newVal);
        }

        private async Task DoSet(string type, long newVal)
        {
            SetButtons(false);
            try
            {
                bool ok;
                if (type == "gold")
                {
                    var (o, _) = await DatabaseManager.Instance.SetGoldAsync(_player.Account, newVal);
                    ok = o;
                    if (ok) { _cur.Gold = newVal; _goldLbl.Text = newVal.ToString("N0") + " 元寶(金幣)"; _nudGold.Value = (decimal)Math.Min(_nudGold.Maximum, newVal); }
                    _goldStatusLbl.Text = ok ? "✓ 已更新" : "✗ 失敗";
                    _goldStatusLbl.ForeColor = ok ? Theme.AccentGreen : Theme.AccentRed;
                }
                else
                {
                    var (o, _) = await DatabaseManager.Instance.SetCrystalAsync(_player.Account, newVal);
                    ok = o;
                    if (ok) { _cur.Crystal = newVal; _crystalLbl.Text = newVal.ToString("N0") + " 水晶"; _nudCrystal.Value = (decimal)Math.Min(_nudCrystal.Maximum, newVal); }
                    _crystalStatusLbl.Text = ok ? "✓ 已更新" : "✗ 失敗";
                    _crystalStatusLbl.ForeColor = ok ? Theme.AccentGreen : Theme.AccentRed;
                }
                // 3秒後清除狀態文字
                await Task.Delay(3000);
                if (type == "gold")    _goldStatusLbl.Text = "";
                else _crystalStatusLbl.Text = "";
            }
            catch (Exception ex)
            {
                _goldStatusLbl.Text = "✗ " + ex.Message;
                _goldStatusLbl.ForeColor = Theme.AccentRed;
            }
            finally { SetButtons(true); }
        }

        private void SetButtons(bool enabled)
        {
            if (InvokeRequired) { Invoke(new Action(() => SetButtons(enabled))); return; }
            _btnSetGold.Enabled    = enabled;
            _btnSetCrystal.Enabled = enabled;
        }
    }
}
