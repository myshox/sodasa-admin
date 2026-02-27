using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    // ══════════════════════════════════════════════════════════════
    // 累積充值修改對話框
    // ══════════════════════════════════════════════════════════════
    public class PayTotalDialog : Form
    {
        private NumericUpDown _nudValue;
        private Label         _lblCalc;      // 自動計算預覽
        private long          _tierGold = 0;  // 目前選取的套餐基礎金幣
        private long          _tierTwd  = 0;  // 目前選取的套餐台幣金額
        private int           _bonus    = 0;  // 回饋百分比 (0/5/10/15/20)
        private Button[]      _tierBtns;
        private Button[]      _bonusBtns;

        /// <summary>台幣金額（不含優惠贈金）：選套餐 = 套餐台幣，手動 = 輸入的台幣</summary>
        public long TwdAmount     => _tierTwd > 0 ? _tierTwd : (long)_nudValue.Value;
        /// <summary>實際發放金幣（含套餐加成 + 優惠%）</summary>
        public long NewValue
        {
            get
            {
                long baseGold = _tierGold > 0 ? _tierGold : TwdAmount * 100L;
                return (long)Math.Round(baseGold * (1 + _bonus / 100.0));
            }
        }
        public int  BonusPercent  => _bonus;
        /// <summary>true = GM 選擇清0累儲（不新增，而是重置進度）</summary>
        public bool IsResetRequest { get; private set; } = false;

        // (按鈕標籤, 金幣數量, 台幣金額)
        private static readonly (string Label, long Gold, int Twd)[] Tiers =
        {
            ("NT$100\n1萬金",    10_000,    100),
            ("NT$300\n3.2萬",   32_000,    300),
            ("NT$500\n5.5萬",   55_000,    500),
            ("NT$1K\n11.5萬",  115_000,  1_000),
            ("NT$3K\n36萬",    360_000,  3_000),
            ("NT$5K\n62.5萬",  625_000,  5_000),
            ("NT$10K\n130萬", 1_300_000, 10_000),
        };

        private static readonly int[] Bonuses = { 0, 5, 10, 15, 20 };

        public PayTotalDialog(string playerName, long currentValue)
        {
            var (vipLevel, vipEmoji, vipLabel, vipRate) = VipHelper.GetTier(currentValue);

            Text            = $"💰 給予儲值 — {playerName}";
            Size            = new Size(580, vipLevel > 0 ? 496 : 460);
            BackColor       = Theme.BgPage;
            ForeColor       = Theme.TextPrimary;
            Font            = Theme.FontBody;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;

            const int x = 16;
            int y = 14;

            // ── 目前累積儲值 ────────────────────────────────────────
            Controls.Add(new Label { Text = "目前累積儲值：", ForeColor = Theme.TextMuted, Font = Theme.FontSmall, AutoSize = true, Location = new Point(x, y + 3) });
            Controls.Add(new Label { Text = $"NT$ {currentValue:N0}", ForeColor = Color.FromArgb(255, 200, 80), Font = Theme.FontHeader, AutoSize = true, Location = new Point(x + 110, y) });
            y += 34;

            // ── VIP 橫幅（若玩家已達 VIP 資格）────────────────────
            if (vipLevel > 0)
            {
                Color bannerBg     = vipLevel == 2 ? Color.FromArgb(8, 28, 55) : Color.FromArgb(60, 44, 8);
                Color bannerAccent = vipLevel == 2 ? Color.FromArgb(100, 180, 255) : Color.FromArgb(255, 200, 60);
                var banner = new Panel
                {
                    Location  = new Point(x, y),
                    Size      = new Size(544, 30),
                    BackColor = bannerBg
                };
                banner.Controls.Add(new Label
                {
                    Text      = $"  {vipEmoji} {vipLabel}  —  後續儲值自動享有 +{vipRate * 100:0}% 金幣回饋（加成已預先選取）",
                    ForeColor = bannerAccent,
                    Font      = new Font(Theme.FontFamily, 9.5f, FontStyle.Bold),
                    Dock      = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                });
                Controls.Add(banner);
                y += 36;
            }
            y += 0;
            Controls.Add(new Panel { Location = new Point(x, y), Size = new Size(544, 1), BackColor = Theme.Border });
            y += 10;

            // ── STEP 1：選擇充值套餐 ─────────────────────────────────
            Controls.Add(new Label { Text = "STEP 1  選擇充值套餐：", ForeColor = Color.FromArgb(100, 180, 255), Font = new Font(Theme.FontFamily, 9f, FontStyle.Bold), AutoSize = true, Location = new Point(x, y) });
            Controls.Add(new Label { Text = "1台幣 = 100金幣（大額有加成）", ForeColor = Color.FromArgb(100, 190, 100), Font = Theme.FontSmall, AutoSize = true, Location = new Point(x + 200, y + 3) });
            y += 22;

            _tierBtns = new Button[Tiers.Length];
            int bx = x;
            for (int i = 0; i < Tiers.Length; i++)
            {
                var (label, gold, twd) = Tiers[i];
                var btn = new Button
                {
                    Text      = label,
                    BackColor = Theme.BgCard,
                    ForeColor = Color.FromArgb(200, 215, 255),
                    FlatStyle = FlatStyle.Flat,
                    Font      = new Font(Theme.FontFamily, 7.5f),
                    Size      = new Size(76, 54),
                    Location  = new Point(bx, y),
                    Cursor    = Cursors.Hand,
                    UseVisualStyleBackColor = false,
                    Tag       = (gold, twd)
                };
                btn.FlatAppearance.BorderColor = Color.FromArgb(40, 55, 90);
                btn.Click += (s, e) =>
                {
                    var (g, tw) = ((long Gold, int Twd))((Button)s).Tag;
                    _tierGold = g;
                    _tierTwd  = tw;
                    RefreshTierButtons();
                    RecalcAndUpdate();
                };
                Controls.Add(btn);
                _tierBtns[i] = btn;
                bx += 78;
            }
            y += 62;

            // ── STEP 2：選擇回饋加成 ─────────────────────────────────
            Controls.Add(new Panel { Location = new Point(x, y), Size = new Size(544, 1), BackColor = Theme.Border });
            y += 8;
            string step2Hint = vipLevel > 0
                ? $"STEP 2  VIP 回饋加成（{vipEmoji} 已自動套用，可手動調整）："
                : "STEP 2  選擇回饋加成（可選）：";
            Controls.Add(new Label { Text = step2Hint, ForeColor = Color.FromArgb(255, 180, 80), Font = new Font(Theme.FontFamily, 9f, FontStyle.Bold), AutoSize = true, Location = new Point(x, y) });
            y += 22;

            // VIP 預設加成百分比
            _bonus = VipHelper.BonusPercent(currentValue);

            _bonusBtns = new Button[Bonuses.Length];
            bx = x;
            for (int i = 0; i < Bonuses.Length; i++)
            {
                int pct = Bonuses[i];
                int idx = i;
                string label = pct == 0 ? "無加成\n+0%"
                             : vipLevel > 0 && pct == (int)(vipRate * 100)
                               ? $"+{pct}%\n⭐ VIP"
                               : $"+{pct}%\n回饋";
                var btn = new Button
                {
                    Text      = label,
                    BackColor = pct == 0 ? Color.FromArgb(22, 32, 50) : Color.FromArgb(35, 48, 22),
                    ForeColor = pct == 0 ? Color.FromArgb(160, 170, 200) : Color.FromArgb(150, 230, 120),
                    FlatStyle = FlatStyle.Flat,
                    Font      = new Font(Theme.FontFamily, 8f, FontStyle.Bold),
                    Size      = new Size(96, 44),
                    Location  = new Point(bx, y),
                    Cursor    = Cursors.Hand,
                    UseVisualStyleBackColor = false,
                    Tag       = pct
                };
                btn.FlatAppearance.BorderColor = Color.FromArgb(45, 65, 35);
                btn.Click += (s, e) =>
                {
                    _bonus = (int)((Button)s).Tag;
                    RefreshBonusButtons();
                    RecalcAndUpdate();
                };
                Controls.Add(btn);
                _bonusBtns[i] = btn;
                bx += 100;
            }
            y += 52;

            // VIP 預選後立即更新按鈕樣式
            Load += (s, e) => RefreshBonusButtons();

            // ── 自動計算預覽 ─────────────────────────────────────────
            Controls.Add(new Panel { Location = new Point(x, y), Size = new Size(544, 1), BackColor = Theme.Border });
            y += 8;
            _lblCalc = new Label
            {
                Text      = "請選擇套餐後自動計算",
                ForeColor = Color.FromArgb(80, 200, 255),
                Font      = new Font(Theme.FontFamily, 11f, FontStyle.Bold),
                AutoSize  = true,
                Location  = new Point(x, y)
            };
            Controls.Add(_lblCalc);
            y += 32;

            // ── 手動輸入台幣 ─────────────────────────────────────────
            Controls.Add(new Label { Text = "或手動輸入台幣（NT$）：", ForeColor = Theme.TextMuted, Font = Theme.FontSmall, AutoSize = true, Location = new Point(x, y + 5) });
            _nudValue = new NumericUpDown
            {
                Location  = new Point(x + 148, y),
                Width     = 130,
                Minimum   = 0,
                Maximum   = 999_999,
                Value     = 0,
                BackColor = Theme.BgInput,
                ForeColor = Theme.TextPrimary,
                Font      = Theme.FontBody,
                ThousandsSeparator = true
            };
            _nudValue.ValueChanged += (s, e) => { _tierGold = 0; _tierTwd = 0; RefreshTierButtons(); RecalcAndUpdate(); };
            Controls.Add(_nudValue);
            y += 44;

            // ── 確定 / 取消 ──────────────────────────────────────────
            var btnOk = Theme.MakeButton("✓ 確認給予", Theme.AccentGreen, Color.White, 120, 36);
            btnOk.Location = new Point(x + 270, y);
            btnOk.Click += (s, e) =>
            {
                long twd  = TwdAmount;
                long gold = NewValue;
                if (twd <= 0)
                {
                    MessageBox.Show("請先選擇套餐或輸入台幣金額。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                long baseGold = _tierGold > 0 ? _tierGold : twd * 100L;
                string goldLine = _bonus > 0
                    ? $"  金幣入帳：+{baseGold:N0}（套餐）＋ +{gold - baseGold:N0}（+{_bonus}%）＝ 共 {gold:N0} 元寶"
                    : $"  金幣入帳：+{gold:N0} 元寶";
                if (MessageBox.Show(
                    $"確認給予以下儲值？\n\n" +
                    $"  台幣金額：NT$ {twd:N0}（累積儲值進度 +NT${twd:N0}，優惠贈金不計入）\n" +
                    goldLine + "\n\n" +
                    "金幣將立即加入玩家帳戶，並更新累積充值記錄。",
                    "確認給予儲值", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
            };

            var btnCancel = Theme.MakeButton("✕ 取消", Theme.BgLight, Theme.TextSecondary, 90, 36);
            btnCancel.Location = new Point(x + 400, y);
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            // ── 清0累儲（危險操作，獨立在左側）───────────────────────
            var btnReset = Theme.MakeButton("🗑 清0累儲進度", Theme.AccentRed, Color.White, 130, 36);
            btnReset.Location = new Point(x, y);
            btnReset.Click += (s, e) =>
            {
                if (MessageBox.Show(
                    "⚠ 確定要將此玩家的累積充值進度歸零？\n\n" +
                    "  · paydata.point    → 0（當前循環進度清除）\n" +
                    "  · check / totalcheck → 0（已領取獎勵旗標清除）\n\n" +
                    "  ✅ 歷史總累儲（lifetime_total）保留不動\n\n" +
                    "此操作無法復原，請確認。",
                    "⚠ 清0累儲確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

                IsResetRequest = true;
                DialogResult   = DialogResult.OK;
                Close();
            };

            Controls.AddRange(new Control[] { btnReset, btnOk, btnCancel });

            // 預設選「無加成」
            _bonus = 0;
            RefreshBonusButtons();
        }

        private void RefreshTierButtons()
        {
            foreach (var btn in _tierBtns)
            {
                var (g, _) = ((long, int))btn.Tag;
                bool sel = g == _tierGold;
                btn.BackColor = sel ? Color.FromArgb(40, 90, 200) : Color.FromArgb(28, 38, 62);
                btn.ForeColor = sel ? Color.White : Color.FromArgb(200, 215, 255);
                btn.FlatAppearance.BorderColor = sel ? Color.FromArgb(80, 140, 255) : Color.FromArgb(40, 55, 90);
            }
        }

        private void RefreshBonusButtons()
        {
            foreach (var btn in _bonusBtns)
            {
                bool sel = (int)btn.Tag == _bonus;
                int pct = (int)btn.Tag;
                btn.BackColor = sel
                    ? (pct == 0 ? Color.FromArgb(50, 60, 85) : Color.FromArgb(40, 130, 30))
                    : (pct == 0 ? Color.FromArgb(22, 32, 50) : Color.FromArgb(35, 48, 22));
                btn.FlatAppearance.BorderColor = sel
                    ? (pct == 0 ? Color.FromArgb(120, 140, 200) : Color.FromArgb(80, 220, 60))
                    : Color.FromArgb(45, 65, 35);
            }
        }

        private void RecalcAndUpdate()
        {
            long twd = TwdAmount;
            if (twd <= 0)
            {
                _lblCalc.Text      = "請選擇套餐或輸入台幣金額";
                _lblCalc.ForeColor = Color.FromArgb(120, 130, 160);
                return;
            }

            // 套餐選取時更新台幣輸入框
            if (_tierTwd > 0 && (long)_nudValue.Value != _tierTwd)
                _nudValue.Value = Math.Min(_tierTwd, _nudValue.Maximum);

            long baseGold = _tierGold > 0 ? _tierGold : twd * 100L;
            long bonus    = (long)Math.Round(baseGold * _bonus / 100.0);
            long total    = Math.Min(baseGold + bonus, 99_999_999);

            if (_bonus > 0)
            {
                _lblCalc.Text =
                    $"💰 {baseGold:N0}  +  🎁 {bonus:N0}（+{_bonus}%）  =  ✅ {total:N0} 金幣  ｜  累積儲值 +NT${twd:N0}";
                _lblCalc.ForeColor = Color.FromArgb(100, 230, 120);
            }
            else
            {
                _lblCalc.Text      = $"💰 {baseGold:N0} 金幣（無加成）  ｜  累積儲值 +NT${twd:N0}";
                _lblCalc.ForeColor = Color.FromArgb(80, 200, 255);
            }
        }
    }

    public class PlayerProfileForm : Form
    {
        private readonly PlayerInfo _player;
        private PlayerDetail _detail;
        private Panel _bodyPanel;
        private Label _loadingLbl;
        // 還原功能：記錄上一次改名前的舊名稱
        private string _previousName = null;
        private Button _btnRestore   = null;

        public PlayerProfileForm(PlayerInfo player)
        {
            _player = player;
            InitUI();
            _ = LoadDetailAsync();
        }

        private void InitUI()
        {
            Text          = $"👤 用戶資料 — {_player.OnlineName}";
            Size          = new Size(700, 760);
            MinimumSize   = new Size(640, 600);
            BackColor = Theme.BgPage;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox   = true;
            StartPosition = FormStartPosition.CenterParent;

            // 標題列
            var hdr = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Theme.BgCard };
            var avatar = new Label
            {
                Text = _player.OnlineName.Length > 0 ? _player.OnlineName[0].ToString() : "?",
                Font = new Font(Theme.FontFamily, 22, FontStyle.Bold),
                ForeColor = Color.White, BackColor = Theme.AccentBlue,
                Size = new Size(48, 48), Location = new Point(12, 6),
                TextAlign = ContentAlignment.MiddleCenter
            };
            var nameL = new Label
            {
                Text = _player.OnlineName,
                ForeColor = Theme.TextPrimary, Font = Theme.FontTitle,
                AutoSize = true, Location = new Point(70, 8)
            };
            var accL = new Label
            {
                Text = _player.Account,
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                AutoSize = true, Location = new Point(72, 34)
            };
            hdr.Controls.AddRange(new Control[] { avatar, nameL, accL });

            // 在線狀態標籤
            var statusL = new Label
            {
                Text = _player.IsOnline ? "🟢 在線" : "⚫ 離線",
                ForeColor = _player.IsOnline ? Theme.AccentGreen : Theme.TextMuted,
                Font = Theme.FontBody, AutoSize = true, Location = new Point(72, 34 + 18)
            };

            // 按鈕列
            var btnRow = new Panel { Dock = DockStyle.Bottom, Height = 50, BackColor = Theme.BgCard };
            var btnSend     = Theme.MakeButton("✉ 發送道具",   Theme.AccentBlue,             Color.White,           118, 32);
            var btnGold     = Theme.MakeButton("💰 金幣水晶",  Color.FromArgb(160,110,20),    Color.White,            95, 32);
            var btnBan      = Theme.MakeButton("🚫 封禁管理",  Theme.AccentRed,               Color.White,            95, 32);
            var btnForceOff = Theme.MakeButton("⚡ 強制下線",  Color.FromArgb(100, 60, 10),   Color.FromArgb(255,200,80), 95, 32);
            var btnRename   = Theme.MakeButton("✏ 改  名",    Theme.AccentOrange,            Color.White,            85, 32);
            _btnRestore     = Theme.MakeButton("↩ 還原改名",  Color.FromArgb(60, 100, 60),   Color.FromArgb(160,240,160), 108, 32);
            var btnClose    = Theme.MakeButton("關  閉",      Theme.BgLight,                 Theme.TextSecondary,    75, 32);
            btnSend.Location     = new Point(12, 10);
            btnGold.Location     = new Point(138, 10);
            btnBan.Location      = new Point(241, 10);
            btnForceOff.Location = new Point(344, 10);
            btnRename.Location   = new Point(447, 10);
            _btnRestore.Location = new Point(540, 10);
            btnClose.Location    = new Point(657, 10);

            // 還原按鈕初始隱藏（改名後才顯示）
            _btnRestore.Visible = false;
            _btnRestore.Font    = Theme.FontSmall;

            btnSend.Click  += (s, e) => { Hide(); new SendForm(_player).ShowDialog(Owner); Show(); };
            btnGold.Click  += (s, e) => { new GoldDialog(_player).ShowDialog(this); };
            btnBan.Click   += (s, e) => { new BanDialog(_player).ShowDialog(this); };
            btnForceOff.Click += async (s, e) =>
            {
                if (_player?.IsOnline != true)
                {
                    MessageBox.Show("該玩家目前不在線，無需強制下線", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                var cf = MessageBox.Show($"確定強制將「{_player.OnlineName}」設為離線？\n（僅修改資料庫 Online 欄位，遊戲內的連線狀態由伺服器管理）",
                    "確認強制下線", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (cf != DialogResult.Yes) return;
                bool ok = await DatabaseManager.Instance.ForceOfflineAsync(_player.Account);
                if (ok)
                {
                    MessageBox.Show("✓ 已設為離線", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    _player.IsOnline    = false;
                    btnForceOff.Enabled = false;
                }
                else MessageBox.Show("操作失敗，請確認資料庫連線", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            btnRename.Click += async (s, e) =>
            {
                // 精確取得目前這個角色的名稱（而非主帳號名稱）
                string current = _detail?.OnlineName ?? _player.OnlineName;
                using var dlg = new EditNameDialog(current);
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                if (string.IsNullOrWhiteSpace(dlg.NewName) || dlg.NewName == current) return;

                // ── 檢查伺服器角色資料夾路徑是否已設定 ──────────────
                var srvSettings  = ServerSettings.Instance;
                bool hasRolePath = !string.IsNullOrWhiteSpace(srvSettings.RoleDataPath);
                if (!hasRolePath)
                {
                    var choice = MessageBox.Show(
                        $"⚠  警告：尚未設定「伺服器角色資料夾路徑」！\n\n" +
                        "石器私服的角色資料以角色名稱為資料夾名稱存放在伺服器磁碟上。\n" +
                        "只更新資料庫、不重命名磁碟資料夾，角色將無法進入遊戲！\n\n" +
                        "建議先到 GM 功能 → 「⚙ 伺服器設定」設定路徑，再執行改名。\n\n" +
                        "確定要僅更新資料庫（不重命名伺服器資料夾）嗎？",
                        "改名前重要提醒", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (choice != DialogResult.Yes) return;
                }

                try
                {
                    int matchCount = await DatabaseManager.Instance.CountByAccountAsync(_player.Account);
                    int dbId       = _detail?.CharDbId ?? _player.CharDbId;

                    string diagInfo = dbId > 0
                        ? $"使用 id={dbId} 精確定位"
                        : matchCount > 1
                            ? $"⚠ Name 有 {matchCount} 筆，使用 Name+MasterId+LIMIT 1"
                            : $"Name 唯一（共 {matchCount} 筆）";

                    // ── 1. 先更新資料庫 ──────────────────────────────
                    bool dbOk = await DatabaseManager.Instance.UpdatePlayerNameAsync(
                        _player.Account, current, dlg.NewName,
                        charDbId: dbId, masterId: _player.MasterId);

                    if (!dbOk)
                    {
                        MessageBox.Show("改名失敗：資料庫更新失敗，請確認連線。", "失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    _previousName       = current;
                    _btnRestore.Visible = true;
                    _player.OnlineName  = dlg.NewName;
                    if (_detail != null) _detail.OnlineName = dlg.NewName;

                    // ── 2. 若有設定路徑，同步重命名伺服器資料夾 ────────
                    string fileMsg = "";
                    if (hasRolePath)
                    {
                        var (fileOk, fileMsgRaw) = srvSettings.RenameRoleFolder(current, dlg.NewName);
                        fileMsg = fileOk
                            ? $"\n\n📁 伺服器資料夾：✓ 已同步重命名\n{fileMsgRaw}"
                            : $"\n\n📁 伺服器資料夾：✗ 重命名失敗\n{fileMsgRaw}\n\n請手動重命名後重啟伺服器。";
                    }
                    else
                    {
                        fileMsg = "\n\n📁 伺服器資料夾：未設定路徑，請手動重命名後重啟伺服器。";
                    }

                    MessageBox.Show(
                        $"✓ 資料庫已將「{current}」改名為「{dlg.NewName}」\n" +
                        $"🔍 定位方式：{diagInfo}{fileMsg}",
                        "改名完成", MessageBoxButtons.OK,
                        fileMsg.Contains("✓") ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

                    _ = LoadDetailAsync();
                }
                catch (Exception ex2) { MessageBox.Show("改名失敗：" + ex2.Message, "錯誤"); }
            };

            _btnRestore.Click += async (s, e) =>
            {
                if (string.IsNullOrEmpty(_previousName)) return;
                string current = _detail?.OnlineName ?? _player.OnlineName;
                if (MessageBox.Show(
                    $"確定要將「{current}」還原為原先的名稱「{_previousName}」？",
                    "↩ 還原改名確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
                try
                {
                    int dbId = _detail?.CharDbId ?? _player.CharDbId;
                    bool dbOk = await DatabaseManager.Instance.UpdatePlayerNameAsync(
                        _player.Account, current, _previousName,
                        charDbId: dbId, masterId: _player.MasterId);
                    if (!dbOk)
                    {
                        MessageBox.Show("還原失敗：資料庫更新失敗。", "失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    string restored     = _previousName;
                    _previousName       = null;
                    _btnRestore.Visible = false;
                    _player.OnlineName  = restored;
                    if (_detail != null) _detail.OnlineName = restored;

                    // 同步重命名伺服器資料夾（current → restored）
                    var srvSettings = ServerSettings.Instance;
                    string fileMsg  = "";
                    if (!string.IsNullOrWhiteSpace(srvSettings.RoleDataPath))
                    {
                        var (fileOk, fm) = srvSettings.RenameRoleFolder(current, restored);
                        fileMsg = fileOk ? $"\n📁 伺服器資料夾也已還原。" : $"\n📁 伺服器資料夾還原失敗：{fm}";
                    }

                    MessageBox.Show($"✓ 已還原為「{restored}」。{fileMsg}",
                        "還原成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    _ = LoadDetailAsync();
                }
                catch (Exception ex2) { MessageBox.Show("還原失敗：" + ex2.Message, "錯誤"); }
            };

            btnClose.Click += (s, e) => Close();
            btnRow.Controls.AddRange(new Control[] { btnSend, btnGold, btnBan, btnForceOff, btnRename, _btnRestore, btnClose });

            _loadingLbl = new Label
            {
                Text = "⏳ 載入中…", ForeColor = Theme.AccentOrange,
                Font = Theme.FontHeader, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };

            _bodyPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            _bodyPanel.Controls.Add(_loadingLbl);

            Controls.Add(btnRow);
            Controls.Add(_bodyPanel);
            Controls.Add(hdr);
        }

        private async Task LoadDetailAsync()
        {
            try
            {
                _detail = await DatabaseManager.Instance.GetPlayerDetailAsync(_player.Account);
                Invoke(new Action(BuildDetailUI));
            }
            catch (Exception ex)
            {
                Invoke(new Action(() => _loadingLbl.Text = "✗ 載入失敗：" + ex.Message));
            }
        }

        private void BuildDetailUI()
        {
            _bodyPanel.Controls.Clear();
            int y = 10, x = 16, colW = 300;
            int panelW = 640;

            void Section(string title, Color color)
            {
                var bar = new Panel { Location = new Point(x, y), Width = panelW, Height = 26, BackColor = Theme.BgCard };
                bar.Controls.Add(new Label { Text = title, ForeColor = color, Font = Theme.FontBody, AutoSize = true, Location = new Point(8, 4) });
                _bodyPanel.Controls.Add(bar);
                y += 30;
            }

            // 建立可選取 / 複製的唯讀文字框（外觀同 Label，但文字可拖選及 Ctrl+C）
            TextBox MakeValBox(string value, Color foreColor, int width, int locX, int locY)
            {
                var tb = new TextBox
                {
                    Text        = value,
                    ForeColor   = foreColor,
                    Font        = Theme.FontBody,
                    Width       = width,
                    Location    = new Point(locX, locY + 1),
                    ReadOnly    = true,
                    BorderStyle = BorderStyle.None,
                    BackColor   = Theme.BgMid,
                    TabStop     = false,
                    Cursor      = Cursors.IBeam
                };
                // Ctrl+A 全選
                tb.KeyDown += (s, e) =>
                {
                    if (e.Control && e.KeyCode == Keys.A) { tb.SelectAll(); e.Handled = true; }
                };
                // 雙擊複製全部內容
                tb.DoubleClick += (s, e) =>
                {
                    if (string.IsNullOrEmpty(tb.Text)) return;
                    Clipboard.SetText(tb.Text);
                    tb.SelectAll();
                    var tip = new ToolTip { InitialDelay = 0, AutoPopDelay = 1200 };
                    string preview = tb.Text.Length > 30 ? tb.Text[..30] + "…" : tb.Text;
                    tip.Show($"✓ 已複製：{preview}", tb, 0, -22, 1200);
                };
                // 右鍵複製
                var ctx = new ContextMenuStrip();
                var miCopy = new ToolStripMenuItem("📋  複製") { Font = Theme.FontSmall };
                miCopy.Click += (s, e) => { if (tb.Text.Length > 0) Clipboard.SetText(tb.Text); };
                ctx.Items.Add(miCopy);
                tb.ContextMenuStrip = ctx;
                return tb;
            }

            void Row(string label, string value, Color valueColor = default)
            {
                if (valueColor == default) valueColor = Theme.TextPrimary;
                var lbl = new Label { Text = label, ForeColor = Theme.TextMuted, Font = Theme.FontSmall, Width = 130, Location = new Point(x + 4, y + 2), TextAlign = ContentAlignment.MiddleRight };
                var val = MakeValBox(value, valueColor, 470, x + 140, y);
                _bodyPanel.Controls.AddRange(new Control[] { lbl, val });
                y += 24;
            }

            void RowDouble(string l1, string v1, string l2, string v2, Color c1 = default, Color c2 = default)
            {
                if (c1 == default) c1 = Theme.TextPrimary;
                if (c2 == default) c2 = Theme.TextPrimary;
                var la = new Label { Text = l1, ForeColor = Theme.TextMuted, Font = Theme.FontSmall, Width = 100, Location = new Point(x + 4, y + 2), TextAlign = ContentAlignment.MiddleRight };
                var va = MakeValBox(v1, c1, 180, x + 108, y);
                var lb = new Label { Text = l2, ForeColor = Theme.TextMuted, Font = Theme.FontSmall, Width = 100, Location = new Point(x + colW + 4, y + 2), TextAlign = ContentAlignment.MiddleRight };
                var vb = MakeValBox(v2, c2, 200, x + colW + 108, y);
                _bodyPanel.Controls.AddRange(new Control[] { la, va, lb, vb });
                y += 24;
            }

            // 帶「✏ 修改」按鈕的列
            void RowEditable(string label, string value, Color valueColor, Func<System.Threading.Tasks.Task> onEdit)
            {
                if (valueColor == default) valueColor = Theme.TextPrimary;
                var lbl = new Label { Text = label, ForeColor = Theme.TextMuted, Font = Theme.FontSmall, Width = 130, Location = new Point(x + 4, y + 2), TextAlign = ContentAlignment.MiddleRight };
                var val = MakeValBox(value, valueColor, 350, x + 140, y);
                var editBtn = Theme.MakeButton("✏ 修改", Color.FromArgb(130, 80, 20), Color.White, 76, 22);
                editBtn.Location = new Point(x + 500, y + 1);
                editBtn.Font = Theme.FontSmall;
                editBtn.Click += async (s, e) =>
                {
                    editBtn.Enabled = false;
                    try { await onEdit(); }
                    finally { if (!IsDisposed) editBtn.Enabled = true; }
                };
                _bodyPanel.Controls.AddRange(new Control[] { lbl, val, editBtn });
                y += 24;
            }

            // 四欄寵物素質列（兩對 label+value 擠一行）
            void RowPetStats(int hp, int atk, int def, int quick, double sum)
            {
                int px = x + 4, pw = 140;
                void Add(string lbl, string val, Color c) {
                    _bodyPanel.Controls.Add(new Label { Text = lbl, ForeColor = Theme.TextMuted, Font = Theme.FontSmall, Width = 54, Location = new Point(px, y + 2), TextAlign = ContentAlignment.MiddleRight });
                    px += 56;
                    _bodyPanel.Controls.Add(MakeValBox(val, c, pw - 56, px, y));
                    px += pw - 56;
                }
                Add("HP：",  $"{hp:N0}",   Color.FromArgb(80, 220, 120));
                Add("攻擊：", $"{atk:N0}",  Color.FromArgb(255, 150, 80));
                Add("防禦：", $"{def:N0}",  Color.FromArgb(100, 180, 255));
                Add("速度：", $"{quick:N0}", Color.FromArgb(200, 200, 80));
                y += 24;
                _bodyPanel.Controls.Add(new Label { Text = "評分/戰力：", ForeColor = Theme.TextMuted, Font = Theme.FontSmall, Width = 90, Location = new Point(x + 4, y + 2), TextAlign = ContentAlignment.MiddleRight });
                _bodyPanel.Controls.Add(new Label { Text = $"{sum:N2}", ForeColor = Color.FromArgb(255, 200, 50), Font = new Font(Theme.FontFamily, 11f, FontStyle.Bold), AutoSize = true, Location = new Point(x + 98, y - 2) });
                y += 26;
            }

            // ── 基本資訊 ──────────────────────────────────────────
            Section("👤  基本資訊", Theme.AccentBlue);
            Row("角色名稱：",  _detail.OnlineName, Theme.AccentOrange);
            Row("帳號 (cdkey)：", _detail.Account, Theme.TextSecondary);
            if (!string.IsNullOrEmpty(_detail.MasterName))
                Row("👑 主帳號：", _detail.MasterName, Color.FromArgb(180, 160, 255));
            RowDouble("在線狀態：", _detail.IsOnline ? "🟢 在線" : "⚫ 離線",
                      "禁言狀態：", _detail.IsMuted ? "🔇 禁言中" : "正常",
                      _detail.IsOnline ? Theme.AccentGreen : Theme.TextMuted,
                      _detail.IsMuted ? Theme.AccentRed : Theme.TextPrimary);
            RowDouble("伺服器：", $"{_detail.ServerName}（ID:{_detail.ServerId}）",
                      "群組：",   $"{_detail.GroupName}（ID:{_detail.GroupId}）");

            // 輩份（Belong）—— 欄位存在時才顯示
            if (_detail.Belong >= 0)
            {
                RowEditable("輩份（Belong）：",
                    _detail.Belong == 0 ? "0（未設定）" : $"{_detail.Belong}",
                    _detail.Belong > 0 ? Color.FromArgb(255, 195, 60) : Theme.TextMuted,
                    async () =>
                    {
                        using var inputDlg = new BelongDialog(_player.OnlineName, _detail.Belong);
                        if (inputDlg.ShowDialog(this) != DialogResult.OK) return;
                        bool ok = await DatabaseManager.Instance.UpdatePlayerBelongAsync(
                            _player.Account, inputDlg.NewBelong);
                        if (ok)
                        {
                            _detail.Belong = inputDlg.NewBelong;
                            MessageBox.Show($"✓ 輩份已設為 {inputDlg.NewBelong}。",
                                "修改成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            BuildDetailUI();
                        }
                        else
                            MessageBox.Show("修改輩份失敗（請確認資料庫是否有 Belong 欄位）。",
                                "失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    });
            }

            // ── 帳號狀態 ──────────────────────────────────────────
            y += 4;
            Section("🔒  帳號狀態", _detail.IsBanned ? Theme.AccentRed : Theme.AccentGreen);
            Row("封禁狀態：",
                _detail.IsBanned ? $"🔴 已封禁（到期：{_detail.BanEndTime}）" : "🟢 正常",
                _detail.IsBanned ? Theme.AccentRed : Theme.AccentGreen);

            // ── 貨幣 ──────────────────────────────────────────────
            y += 4;
            Section("💰  遊戲幣（元寶/金幣）  —  DB值，在線玩家重登後同步", Color.FromArgb(255, 200, 0));
            RowDouble("💰 元寶(金幣)：", $"{_detail.Gold:N0}",
                      "💎 水晶：",       $"{_detail.Crystal:N0}");
            RowDouble("充值點：", $"{_detail.PayPoint:N0}",
                      "R幣：",    $"{_detail.RmbPoint:N0}");

            // ── 累積充值（台幣）—— paydata.point 單位為 NT$，1循環 = NT$25,000 ────
            const long CYCLE = 25_000L;   // NT$25,000 / cycle（對應遊戲面板 0→25000）
            long  payPt      = _detail.PayTotal;         // NT$（paydata.point，遊戲直接讀取）
            long  lifetimePt = _detail.LifetimePayTotal; // NT$（永不歸零的歷史總額）
            long  cycle      = payPt / CYCLE;            // 已完成循環數
            long  inCycle    = payPt % CYCLE;            // 本循環已累積（NT$）
            long  remain     = CYCLE - inCycle;          // 距下一循環還差多少（NT$）
            int   pct        = (int)(inCycle * 100 / CYCLE);
            // 循環說明文字（與遊戲面板邏輯相同：遊戲顯示 inCycle / 25000）
            string cycleLabel = cycle == 0
                ? $"第 1 循環"
                : $"第 {cycle + 1} 循環（已完成 {cycle} 次）";

            y += 4;
            Section($"💳  累積充值（台幣）  —  {cycleLabel} · NT${inCycle:N0} / $25,000  ·  歷史總計 NT${payPt:N0}", Color.FromArgb(255, 200, 80));
            RowEditable("當前循環進度：",
                $"NT$ {inCycle:N0} / 25,000　（遊戲面板顯示值）　|　歷史總計 NT$ {payPt:N0}",
                Color.FromArgb(255, 200, 80), async () =>
            {
                using var dlg = new AdjustRechargeDialog(_player.OnlineName, _detail.PayTotal, _detail.LifetimePayTotal);
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                bool ok = await DatabaseManager.Instance.AdjustPayDataPointAsync(
                    _player.Account, dlg.TwdAmount, dlg.GoldAmount, dlg.GiveGold);
                if (ok)
                {
                    if (dlg.GiveGold) _detail.Gold += dlg.GoldAmount;
                    _detail.PayTotal         += dlg.TwdAmount;
                    _detail.LifetimePayTotal += dlg.TwdAmount;
                    string goldLine = dlg.GiveGold
                        ? $"✅ 金幣已入帳：+{dlg.GoldAmount:N0} 金幣（含套餐加成及優惠贈金）\n"
                        : "ℹ️ 本次不發放金幣（僅更新累儲進度）\n";
                    MessageBox.Show(
                        goldLine +
                        $"✅ 累積充值更新：+NT${dlg.TwdAmount:N0}（paydata.point 累加）\n" +
                        $"✅ 歷史總累儲同步更新（lifetime_total 累加）\n\n" +
                        "玩家重新登入後，遊戲內累積充值獎勵介面將顯示新數值。",
                        "操作成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    BuildDetailUI();
                }
                else
                {
                    MessageBox.Show("修改失敗，請確認資料庫連線。", "失敗",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            });

            // ── 操作按鈕列（修復循環 + 重置進度）────────────────────────
            {
                var btnPanel = new Panel
                {
                    Location  = new Point(x + 140, y),
                    Size      = new Size(480, 28),
                    BackColor = Color.Transparent
                };

                // 🔧 修復循環 check（不動 point，只補 check bits）
                var btnFix = Theme.MakeButton("🔧 修復循環顯示", Color.FromArgb(30, 90, 160), Color.White, 138, 24);
                btnFix.Font     = new Font(Theme.FontFamily, 8.5f, FontStyle.Bold);
                btnFix.Location = new Point(0, 2);
                btnFix.Click += async (s, e) =>
                {
                    long completedCycles = payPt / CYCLE;
                    if (MessageBox.Show(
                        $"🔧 根據目前 NT${payPt:N0} 自動計算並補齊循環旗標：\n\n" +
                        $"  已完成循環數 = {completedCycles}\n" +
                        $"  將把前 {completedCycles} 個循環的 check bits 全部設為「已領取」\n\n" +
                        "  ✅ paydata.point 不變\n" +
                        "  ✅ 遊戲面板將正確顯示當前循環進度\n\n" +
                        "確認執行？",
                        "🔧 修復循環顯示",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

                    bool ok = await DatabaseManager.Instance.FixPaydataCheckAsync(_player.Account);
                    if (ok)
                    {
                        MessageBox.Show(
                            $"✅ check 欄位已修復（前 {completedCycles} 循環全標記為已領取）\n" +
                            "玩家重新登入後遊戲面板將顯示正確循環進度。",
                            "修復成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        BuildDetailUI();
                    }
                    else
                        MessageBox.Show("⚠ 修復失敗（玩家可能無 paydata 記錄）。", "失敗", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                };

                // 🔄 重置（point + check → 0，保留 lifetime_total）
                var btnReset = Theme.MakeButton("🔄 重置進度（清0）", Theme.AccentRed, Color.White, 138, 24);
                btnReset.Font     = new Font(Theme.FontFamily, 8.5f, FontStyle.Bold);
                btnReset.Location = new Point(146, 2);
                btnReset.Click += async (s, e) =>
                {
                    if (MessageBox.Show(
                        $"⚠ 確定要將「{_player.OnlineName}」的累積充值進度歸零？\n\n" +
                        $"  · paydata.point → 0\n  · check / totalcheck → 0\n\n" +
                        $"  ✅ 歷史總累儲 NT${lifetimePt:N0} 保留不動\n\n此操作無法復原，請確認。",
                        "⚠ 重置確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

                    bool ok = await DatabaseManager.Instance.ResetPaydataProgressAsync(_player.Account);
                    if (ok)
                    {
                        _detail.PayTotal = 0;
                        MessageBox.Show("✅ 累儲進度已歸零。歷史總累儲保留不動。",
                            "操作成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        BuildDetailUI();
                    }
                    else
                        MessageBox.Show("⚠ 重置失敗（玩家可能無 paydata 記錄）。", "失敗", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                };

                var lblNote = new Label
                {
                    Text      = "← 修復：補 check bits；重置：清0 重來",
                    ForeColor = Theme.TextMuted,
                    Font      = Theme.FontSmall,
                    AutoSize  = true,
                    Location  = new Point(292, 6)
                };
                btnPanel.Controls.AddRange(new Control[] { btnFix, btnReset, lblNote });
                _bodyPanel.Controls.Add(btnPanel);
                y += 32;
            }

            // 🎁 發放循環獎勵按鈕（防呆：check==0 且 totalCheck>0 才顯示）
            if (_detail.ClaimReady)
            {
                var btnClaim = Theme.MakeButton(
                    $"🎁 發放第 {_detail.TotalCheck} 輪累積獎勵（{_player.OnlineName}）",
                    Color.FromArgb(180, 130, 20), Color.White, 400, 30);
                btnClaim.Font     = new Font(Theme.FontFamily, 9f, FontStyle.Bold);
                btnClaim.Location = new Point(x + 140, y);
                btnClaim.Click   += async (s, e) =>
                {
                    if (MessageBox.Show(
                        $"🎁 確定要發放「{_player.OnlineName}」第 {_detail.TotalCheck} 輪的累積獎勵？\n\n" +
                        "  · paydata.check 將設為 1（已領）\n" +
                        "  · 下次累積滿 NT$25,000 才能再次領獎\n\n" +
                        "確認執行？",
                        "🎁 發放累積獎勵",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

                    var (status, cycle) = await DatabaseManager.Instance.ClaimPaydataRewardAsync(_player.Account);
                    switch (status)
                    {
                        case "ok":
                            MessageBox.Show(
                                $"✅ 第 {cycle} 輪獎勵已發放，check 設為 1。\n玩家遊戲內領獎按鈕將消失。",
                                "發放成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            BuildDetailUI();
                            break;
                        case "already_claimed":
                            MessageBox.Show("⚠ 此輪獎勵已發放過，無法重複操作。", "已領取",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            break;
                        case "no_cycle":
                            MessageBox.Show("⚠ 尚未完成任何循環，無獎勵可發放。", "無獎勵",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            break;
                        default:
                            MessageBox.Show("⚠ 操作失敗，請確認資料庫連線。", "失敗",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                            break;
                    }
                };
                var lblClaim = new Label
                {
                    Text      = $"← check=0，第 {_detail.TotalCheck} 輪可領",
                    ForeColor = Color.FromArgb(255, 200, 80),
                    Font      = Theme.FontSmall,
                    AutoSize  = true,
                    Location  = new Point(x + 548, y + 7)
                };
                _bodyPanel.Controls.AddRange(new Control[] { btnClaim, lblClaim });
                y += 36;
            }
            else if (_detail.TotalCheck > 0)
            {
                // 已領過：顯示已完成狀態
                var lblDone = new Label
                {
                    Text      = $"✅ 第 {_detail.TotalCheck} 輪獎勵已發放（check=1）",
                    ForeColor = Color.FromArgb(80, 200, 120),
                    Font      = Theme.FontSmall,
                    AutoSize  = true,
                    Location  = new Point(x + 140, y)
                };
                _bodyPanel.Controls.Add(lblDone);
                y += 22;
            }

            // 歷史總累積儲值（永不歸零）
            {
                string lifetimeText = lifetimePt == payPt
                    ? $"NT$ {lifetimePt:N0}　（與當前累儲相同，尚無重置記錄）"
                    : $"NT$ {lifetimePt:N0}　（跨越 {lifetimePt / CYCLE} 個循環的歷史總額）";
                var lLifeKey = new Label
                {
                    Text      = "歷史總累儲（台幣）：",
                    ForeColor = Theme.TextMuted,
                    Font      = Theme.FontSmall,
                    Width     = 130,
                    Location  = new Point(x + 4, y + 2),
                    TextAlign = ContentAlignment.MiddleRight
                };
                var lLifeVal = new Label
                {
                    Text         = lifetimeText,
                    ForeColor    = Color.FromArgb(160, 230, 255),
                    Font         = Theme.FontSmall,
                    AutoSize     = false,
                    Width        = 350,
                    AutoEllipsis = true,
                    Location     = new Point(x + 140, y + 2)
                };
                var lLifeBadge = new Label
                {
                    Text      = "永不歸零",
                    ForeColor = Color.FromArgb(80, 220, 140),
                    BackColor = Theme.BgCard,
                    Font      = new Font(Theme.FontFamily, 7.5f, FontStyle.Bold),
                    AutoSize  = true,
                    Location  = new Point(x + 496, y + 2),
                    Padding   = new Padding(3, 1, 3, 1)
                };
                _bodyPanel.Controls.AddRange(new Control[] { lLifeKey, lLifeVal, lLifeBadge });
                y += 22;
            }

            // ── 循環進度條（大型，明顯顯示）──────────────────────────
            {
                // 主進度行
                var lbl = new Label
                {
                    Text      = "遊戲面板進度：",
                    ForeColor = Theme.TextMuted,
                    Font      = Theme.FontSmall,
                    Width     = 130,
                    Location  = new Point(x + 4, y + 2),
                    TextAlign = ContentAlignment.MiddleRight
                };

                // 進度數字（大字，遊戲面板顯示的就是這個）
                var valMain = new Label
                {
                    Text      = $"NT$ {inCycle:N0}  /  25,000",
                    ForeColor = pct >= 80 ? Color.FromArgb(80, 230, 140)
                              : pct >= 40 ? Color.FromArgb(255, 200, 80)
                              : Color.FromArgb(100, 180, 255),
                    Font      = new Font(Theme.FontFamily, 11f, FontStyle.Bold),
                    AutoSize  = true,
                    Location  = new Point(x + 140, y)
                };
                var valSub = new Label
                {
                    Text      = $"  還差 NT${remain:N0} 完成第 {cycle + 1} 循環　　歷史總計（paydata.point） = NT${payPt:N0}",
                    ForeColor = Theme.TextMuted,
                    Font      = Theme.FontSmall,
                    AutoSize  = true,
                    Location  = new Point(x + 140, y + 22)
                };
                _bodyPanel.Controls.AddRange(new Control[] { lbl, valMain, valSub });
                y += 40;

                // 進度條
                var barBg = new Panel
                {
                    Location  = new Point(x + 140, y),
                    Size      = new Size(420, 12),
                    BackColor = Theme.BgCard
                };
                int fillW = Math.Max(4, (int)(420 * pct / 100.0));
                var barFill = new Panel
                {
                    Location  = new Point(0, 0),
                    Size      = new Size(fillW, 12),
                    BackColor = pct >= 80 ? Color.FromArgb(50, 220, 120)
                              : pct >= 40 ? Color.FromArgb(255, 190, 60)
                              : Color.FromArgb(80, 140, 255)
                };
                barBg.Controls.Add(barFill);
                var barPct = new Label
                {
                    Text      = $"{pct}%",
                    ForeColor = Color.FromArgb(130, 150, 190),
                    Font      = Theme.FontSmall,
                    AutoSize  = true,
                    Location  = new Point(x + 566, y)
                };
                _bodyPanel.Controls.AddRange(new Control[] { barBg, barPct });
                y += 20;
            }

            // ── 寵物四圍素質 ──────────────────────────────────────
            y += 4;
            Section("🐾  寵物（capturepet）", Color.FromArgb(150, 220, 150));
            var tp = _detail.TopPet;
            RowDouble("持有數量：", $"{tp.Count} 隻",
                      "郵件總數：", $"{_detail.TotalMails} 封");
            Row("未領取郵件：", $"{_detail.UnreadMails} 封",
                _detail.UnreadMails > 0 ? Theme.AccentOrange : Theme.TextPrimary);
            if (tp.HasPet)
            {
                y += 2;
                Row("最強寵物：", $"#{tp.BestId}  {tp.BestName}  Lv.{tp.BestLv}  (由 {tp.BestAuthor} 捕捉)", Color.FromArgb(160, 240, 180));
                RowPetStats(tp.BestHp, tp.BestAttack, tp.BestDef, tp.BestQuick, tp.BestSum);
            }
            // 查看全部寵物 + GM 指令按鈕列（各佔一行，確保可見）
            {
                var btnPets = Theme.MakeButton("🐾 查看全部寵物清單", Color.FromArgb(30, 90, 50), Color.FromArgb(150, 240, 170), 180, 26);
                btnPets.Location = new Point(x + 140, y);
                btnPets.Font     = Theme.FontSmall;
                btnPets.Click   += async (s, e) =>
                {
                    btnPets.Enabled = false;
                    try
                    {
                        var pets = await DatabaseManager.Instance.GetPlayerPetsAsync(
                            _player.Account, _detail.OnlineName);
                        new PetListForm(_detail.OnlineName, _player.Account, pets).ShowDialog(this);
                    }
                    catch (Exception ex2) { MessageBox.Show("載入失敗：" + ex2.Message); }
                    finally { if (!IsDisposed) btnPets.Enabled = true; }
                };
                _bodyPanel.Controls.Add(btnPets);
                y += 30;

                var btnGmPet = Theme.MakeButton("🎮 GM 寵物指令", Color.FromArgb(60, 30, 100), Color.FromArgb(210, 160, 255), 160, 26);
                btnGmPet.Location = new Point(x + 140, y);
                btnGmPet.Font     = Theme.FontSmall;
                btnGmPet.Click   += (s, e) =>
                    new PetCommandDialog(_detail.Account, _detail.OnlineName).ShowDialog(this);
                _bodyPanel.Controls.Add(btnGmPet);
                y += 32;
            }

            // ── 時間記錄 ──────────────────────────────────────────
            y += 4;
            Section("📅  時間記錄", Color.FromArgb(100, 180, 255));
            RowDouble("注冊時間：", _detail.RegTime,
                      "最後登入：", _detail.LoginTime);
            // 注冊IP 旁加「查找同IP」按鈕
            {
                var lRegIp = new Label { Text = "注冊 IP：", ForeColor = Theme.TextMuted, Font = Theme.FontSmall, Width = 130, Location = new Point(x + 4, y + 2), TextAlign = ContentAlignment.MiddleRight };
                var vRegIp = new Label { Text = _detail.RegIP, ForeColor = Theme.TextPrimary, Font = Theme.FontBody, AutoSize = false, Width = 180, AutoEllipsis = true, Location = new Point(x + 140, y) };
                var lLastIp = new Label { Text = "最後 IP：", ForeColor = Theme.TextMuted, Font = Theme.FontSmall, Width = 100, Location = new Point(x + colW + 4, y + 2), TextAlign = ContentAlignment.MiddleRight };
                var vLastIp = new Label { Text = _detail.IP, ForeColor = Theme.TextPrimary, Font = Theme.FontBody, AutoSize = false, Width = 120, AutoEllipsis = true, Location = new Point(x + colW + 108, y) };
                var btnFindIp = Theme.MakeButton("🔍 查找同IP帳號", Color.FromArgb(20, 60, 120), Color.FromArgb(130, 180, 255), 120, 22);
                btnFindIp.Location = new Point(x + colW + 232, y + 1);
                btnFindIp.Font     = Theme.FontSmall;
                btnFindIp.Click   += async (s, e) =>
                {
                    string ip = _detail.IP;
                    if (string.IsNullOrWhiteSpace(ip)) { MessageBox.Show("IP 為空"); return; }
                    btnFindIp.Enabled = false;
                    try
                    {
                        var accs = await DatabaseManager.Instance.GetSameIpAccountsAsync(ip);
                        new SameIpMacForm($"相同 IP：{ip}", accs).ShowDialog(this);
                    }
                    catch (Exception ex2) { MessageBox.Show("查詢失敗：" + ex2.Message); }
                    finally { if (!IsDisposed) btnFindIp.Enabled = true; }
                };
                _bodyPanel.Controls.AddRange(new Control[] { lRegIp, vRegIp, lLastIp, vLastIp, btnFindIp });
                y += 26;
            }

            // ── 帳號資訊 ──────────────────────────────────────────
            y += 4;
            Section("🔧  帳號資訊", Theme.TextSecondary);
            Row("UID：", _detail.Uid);
            Row("QQ：",  _detail.QQ);

            // ── 密碼顯示（預設遮蔽，點「👁 顯示」才展開）─────────────
            {
                bool hasPass = !string.IsNullOrEmpty(_detail.Password);
                bool hasSafe = !string.IsNullOrEmpty(_detail.SafePassword);

                // 判斷是否為 MD5（32位十六進位）
                static bool IsMd5(string s) =>
                    s.Length == 32 && System.Text.RegularExpressions.Regex.IsMatch(s, @"^[0-9a-fA-F]{32}$");

                void AddPasswordRow(string labelText, string actual, string field)
                {
                    if (string.IsNullOrEmpty(actual)) return;
                    bool   revealed = false;
                    bool   isMd5    = IsMd5(actual);
                    const string mask = "●●●●●●●●";

                    var pwdL = new Label
                    {
                        Text      = labelText,
                        ForeColor = Theme.TextMuted,
                        Font      = Theme.FontSmall,
                        Width     = 124,
                        Location  = new Point(x + 4, y + 2),
                        TextAlign = ContentAlignment.MiddleRight
                    };

                    // 顯示框（顯示時若 MD5 加標註）
                    var pwdV = new TextBox
                    {
                        Text        = mask,
                        ForeColor   = Theme.TextMuted,
                        Font        = Theme.FontBody,
                        Width       = isMd5 ? 220 : 270,
                        Location    = new Point(x + 130, y + 1),
                        ReadOnly    = true,
                        BorderStyle = BorderStyle.None,
                        BackColor   = Theme.BgMid,
                        TabStop     = false,
                        Cursor      = Cursors.IBeam
                    };
                    pwdV.KeyDown += (s, e) =>
                    {
                        if (e.Control && e.KeyCode == Keys.A) { pwdV.SelectAll(); e.Handled = true; }
                    };

                    // MD5 標籤（只有雜湊才顯示）
                    var lblMd5 = new Label
                    {
                        Text      = isMd5 ? "MD5 加密" : "",
                        ForeColor = Color.FromArgb(120, 130, 160),
                        Font      = Theme.FontSmall,
                        AutoSize  = true,
                        Location  = new Point(x + 354, y + 4),
                        Visible   = isMd5
                    };

                    // 👁 顯示 / 🙈 隱藏 切換
                    var btnToggle = Theme.MakeButton("👁 顯示", Color.FromArgb(50, 40, 80), Color.FromArgb(190, 150, 255), 66, 22);
                    btnToggle.Location = new Point(x + 440, y + 1);
                    btnToggle.Font     = Theme.FontSmall;
                    btnToggle.Click   += (s, e) =>
                    {
                        revealed = !revealed;
                        if (revealed)
                        {
                            pwdV.Text      = actual;
                            pwdV.ForeColor = Color.FromArgb(255, 200, 80);
                            btnToggle.Text = "🙈 隱藏";
                            _ = GmLogger.Instance.LogAsync("查看玩家密碼",
                                _player.Account, $"欄位：{field}　角色：{_player.OnlineName}", true);
                        }
                        else
                        {
                            pwdV.Text      = mask;
                            pwdV.ForeColor = Theme.TextMuted;
                            btnToggle.Text = "👁 顯示";
                            pwdV.DeselectAll();
                        }
                    };

                    // 🔍 MD5 反查原文按鈕（只對 MD5 顯示）
                    if (isMd5)
                    {
                        var btnCrack = Theme.MakeButton("🔍 反查", Color.FromArgb(20, 50, 30), Color.FromArgb(80, 200, 120), 58, 22);
                        btnCrack.Location = new Point(x + 510, y + 1);
                        btnCrack.Font  = Theme.FontSmall;
                        btnCrack.Click += async (s, e) =>
                        {
                            btnCrack.Enabled = false;
                            btnCrack.Text    = "查詢中…";
                            try
                            {
                                string? plain = await Md5LookupAsync(actual);
                                if (!string.IsNullOrWhiteSpace(plain))
                                {
                                    pwdV.Text      = plain;
                                    pwdV.ForeColor = Color.FromArgb(80, 220, 120);
                                    revealed       = true;
                                    btnToggle.Text = "🙈 隱藏";
                                    MessageBox.Show(
                                        $"✅ 找到原始密碼！\n\n" +
                                        $"原文密碼：  {plain}\n" +
                                        $"MD5 雜湊：{actual}",
                                        "反查成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                    _ = GmLogger.Instance.LogAsync("MD5反查密碼",
                                        _player.Account, $"欄位：{field}　原文：{plain}", true);
                                }
                                else
                                {
                                    MessageBox.Show(
                                        "❌ 無法反查此密碼。\n\n" +
                                        "可能原因：\n" +
                                        "  · 玩家使用了較複雜的密碼（不在彩虹表內）\n" +
                                        "  · 網路無法連線到查詢服務\n\n" +
                                        "建議：直接使用「🔑 重設」按鈕幫玩家重設新密碼。",
                                        "查無結果", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show("查詢失敗：" + ex.Message, "錯誤");
                            }
                            finally
                            {
                                if (!IsDisposed) { btnCrack.Enabled = true; btnCrack.Text = "🔍 反查"; }
                            }
                        };
                        _bodyPanel.Controls.Add(btnCrack);
                    }

                    // 🔑 重設密碼按鈕
                    var btnReset = Theme.MakeButton("🔑 重設", Color.FromArgb(80, 30, 30), Color.FromArgb(255, 120, 120), 62, 22);
                    btnReset.Location = new Point(isMd5 ? x + 572 : x + 510, y + 1);
                    btnReset.Font     = Theme.FontSmall;
                    btnReset.Click   += async (s, e) =>
                    {
                        // 輸入新密碼對話框
                        string newPwd = "";
                        using (var dlg = new Form())
                        {
                            dlg.Text            = $"🔑 重設密碼 — {_player.OnlineName}";
                            dlg.Size            = new Size(400, 170);
                            dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                            dlg.StartPosition   = FormStartPosition.CenterParent;
                            dlg.MaximizeBox     = false;
                            dlg.BackColor       = Theme.BgPage;
                            var lbl = new Label { Text = isMd5 ? "輸入新明文密碼（自動轉 MD5 儲存）：" : "輸入新密碼：",
                                ForeColor = Theme.TextSecondary, Font = Theme.FontBody, AutoSize = true, Location = new Point(16, 16) };
                            var tb  = Theme.MakeTextBox(340); tb.Location = new Point(16, 42); tb.PasswordChar = '●';
                            var ok  = Theme.MakePrimaryButton("✓ 確認", 90, 32); ok.Location = new Point(190, 86);
                            var can = Theme.MakeButton("✕ 取消", Theme.BgLight, Theme.TextSecondary, 80, 32); can.Location = new Point(290, 86);
                            ok.Click  += (_, __) => { dlg.DialogResult = DialogResult.OK; };
                            can.Click += (_, __) => { dlg.DialogResult = DialogResult.Cancel; };
                            dlg.Controls.AddRange(new Control[] { lbl, tb, ok, can });
                            dlg.AcceptButton = ok;
                            dlg.CancelButton = can;
                            if (dlg.ShowDialog(this) != DialogResult.OK) return;
                            newPwd = tb.Text;
                        }
                        if (string.IsNullOrWhiteSpace(newPwd)) return;
                        if (newPwd.Length < 4)
                        {
                            MessageBox.Show("密碼至少需要 4 個字元", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                        btnReset.Enabled = false;
                        try
                        {
                            bool ok = await DatabaseManager.Instance.ResetPlayerPasswordAsync(
                                _player.Account, newPwd, field);
                            if (ok)
                            {
                                // 重新計算 MD5 以更新顯示
                                using var md5 = System.Security.Cryptography.MD5.Create();
                                string newHash = BitConverter.ToString(
                                    md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(newPwd)))
                                    .Replace("-", "").ToLower();
                                actual = newHash;  // 更新 closure
                                pwdV.Text      = mask;
                                pwdV.ForeColor = Theme.TextMuted;
                                revealed       = false;
                                btnToggle.Text = "👁 顯示";
                                MessageBox.Show(
                                    $"✅ 密碼已成功重設！\n\n" +
                                    $"新密碼（明文）：{newPwd}\n" +
                                    $"儲存格式（MD5）：{newHash}\n\n" +
                                    "玩家下次登入時使用新密碼即可。",
                                    "重設成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            else MessageBox.Show("重設失敗，請確認資料庫連線。", "失敗");
                        }
                        catch (Exception ex) { MessageBox.Show("錯誤：" + ex.Message); }
                        finally { if (!IsDisposed) btnReset.Enabled = true; }
                    };

                    _bodyPanel.Controls.AddRange(new Control[] { pwdL, pwdV, lblMd5, btnToggle, btnReset });
                    y += 24;
                }

                AddPasswordRow("登入密碼：",  _detail.Password,     "PassWord");
                AddPasswordRow("安全密碼：",  _detail.SafePassword, "SafePasswd");
            }

            // MAC 旁加「查找同MAC」按鈕
            {
                var macL = new Label { Text = "MAC：", ForeColor = Theme.TextMuted, Font = Theme.FontSmall, Width = 124, Location = new Point(x + 4, y + 2), TextAlign = ContentAlignment.MiddleRight };
                var macV = new Label { Text = _detail.MAC, ForeColor = Theme.TextMuted, Font = Theme.FontSmall, AutoSize = false, Width = 320, AutoEllipsis = true, Location = new Point(x + 130, y + 2) };
                var btnFindMac = Theme.MakeButton("🔍 查找同MAC帳號", Color.FromArgb(60, 30, 100), Color.FromArgb(190, 150, 255), 128, 22);
                btnFindMac.Location = new Point(x + 460, y + 1);
                btnFindMac.Font     = Theme.FontSmall;
                btnFindMac.Click   += async (s, e) =>
                {
                    string mac = _detail.MAC;
                    if (string.IsNullOrWhiteSpace(mac)) { MessageBox.Show("MAC 為空"); return; }
                    btnFindMac.Enabled = false;
                    try
                    {
                        var accs = await DatabaseManager.Instance.GetSameMacAccountsAsync(mac);
                        new SameIpMacForm($"相同 MAC：{mac}", accs).ShowDialog(this);
                    }
                    catch (Exception ex2) { MessageBox.Show("查詢失敗：" + ex2.Message); }
                    finally { if (!IsDisposed) btnFindMac.Enabled = true; }
                };
                _bodyPanel.Controls.AddRange(new Control[] { macL, macV, btnFindMac });
                y += 24;
            }

            y += 4;
            var noteBox = new Panel { Location = new Point(x, y), Width = panelW, Height = 36, BackColor = Theme.BgCard };
            noteBox.Controls.Add(new Label
            {
                Text      = "🔒 石幣 / 聲望 / 戰點 存於伺服器二進位角色檔案，無法從資料庫讀取",
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall, AutoSize = true, Location = new Point(8, 10)
            });
            _bodyPanel.Controls.Add(noteBox);
        }

        // ── MD5 反查（嘗試多個公開彩虹表服務）─────────────────────────
        private static readonly System.Net.Http.HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        private static async System.Threading.Tasks.Task<string?> Md5LookupAsync(string hash)
        {
            hash = hash.ToLower().Trim();

            // 服務 1：nitrxgen（回傳純文字）
            try
            {
                string r = await _httpClient.GetStringAsync($"https://www.nitrxgen.net/md5db/{hash}");
                if (!string.IsNullOrWhiteSpace(r) && r.Length < 64)
                    return r.Trim();
            }
            catch { }

            // 服務 2：md5.gromweb.com（回傳 JSON）
            try
            {
                var req = new System.Net.Http.HttpRequestMessage(
                    System.Net.Http.HttpMethod.Get,
                    $"https://md5.gromweb.com/query/{hash}");
                req.Headers.Add("Accept", "application/json");
                var resp = await _httpClient.SendAsync(req);
                if (resp.IsSuccessStatusCode)
                {
                    string body = await resp.Content.ReadAsStringAsync();
                    // 回應格式：{"string":"password","md5":"hash"}
                    var doc = System.Text.Json.JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("string", out var prop))
                    {
                        string val = prop.GetString() ?? "";
                        if (!string.IsNullOrWhiteSpace(val)) return val;
                    }
                }
            }
            catch { }

            // 服務 3：hashtoolkit（只試常見密碼本地）
            string[] commonPasswords = {
                "123456","password","123456789","12345678","12345","1234567",
                "1234567890","qwerty","abc123","111111","123123","admin",
                "letmein","welcome","monkey","dragon","master","123321",
                "666666","888888","000000","1q2w3e","pass","iloveyou",
                "sunshine","princess","football","shadow","superman"
            };
            foreach (var p in commonPasswords)
            {
                using var md5 = System.Security.Cryptography.MD5.Create();
                string h = BitConverter.ToString(
                    md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(p)))
                    .Replace("-", "").ToLower();
                if (h == hash) return p;
            }

            return null;
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 輩份設定對話框
    // ══════════════════════════════════════════════════════════════
    public class BelongDialog : Form
    {
        private NumericUpDown _nud;
        public int NewBelong => (int)_nud.Value;

        public BelongDialog(string playerName, int current)
        {
            Text            = $"🎖 設定輩份 — {playerName}";
            Size            = new Size(380, 185);
            BackColor       = Theme.BgPage;
            ForeColor       = Theme.TextPrimary;
            Font            = Theme.FontBody;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;

            const int x = 20;
            int y = 18;

            Controls.Add(new Label
            {
                Text      = "輩份值（Belong）：",
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                AutoSize  = true, Location = new Point(x, y + 4)
            });
            Controls.Add(new Label
            {
                Text      = $"目前：{current}",
                ForeColor = current > 0 ? Color.FromArgb(255, 200, 60) : Theme.TextMuted,
                Font      = Theme.FontBody, AutoSize = true, Location = new Point(x + 120, y)
            });
            y += 34;

            _nud = new NumericUpDown
            {
                Location  = new Point(x, y),
                Width     = 140,
                Minimum   = 0,
                Maximum   = 999,
                Value     = Math.Max(0, current),
                BackColor = Theme.BgInput,
                ForeColor = Theme.TextPrimary,
                Font      = Theme.FontBody
            };
            Controls.Add(_nud);
            Controls.Add(new Label
            {
                Text      = "（0 = 未設定）",
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                AutoSize  = true, Location = new Point(x + 150, y + 5)
            });
            y += 44;

            var btnOk = Theme.MakeButton("✓ 確認", Theme.AccentGreen, Color.White, 90, 32);
            btnOk.Location = new Point(x + 140, y);
            btnOk.Click   += (s, e) => { DialogResult = DialogResult.OK; Close(); };

            var btnCancel = Theme.MakeButton("✕ 取消", Theme.BgLight, Theme.TextSecondary, 80, 32);
            btnCancel.Location = new Point(x + 240, y);
            btnCancel.Click   += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            Controls.AddRange(new Control[] { btnOk, btnCancel });
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 調整累積充值對話框（雙軌制 + 強制選擇防呆）
    //
    //   ✦ paydata.point 單位：NT$（台幣），1循環 = NT$25,000
    //   ✦ 快選套餐自動依加成率換算金幣；手動輸入使用基礎率 ×100
    //   ✦ 操作類型強制二擇一，預設空白，送出前彈防呆確認視窗
    // ══════════════════════════════════════════════════════════════
    public class AdjustRechargeDialog : Form
    {
        private const long CYCLE = 25_000L;  // NT$25,000 / cycle

        private NumericUpDown _nudTwd;          // 台幣輸入
        private Label         _lblGoldCalc;     // 對應金幣預覽
        private RadioButton   _rbOnlyProgress;
        private RadioButton   _rbWithGold;
        private Label         _lblCycleAfter;
        private Panel         _barFillAfter;
        private Button[]      _tierBtns;
        private readonly long _currentTotal;   // 目前 paydata.point (NT$)
        private readonly long _lifetimeTotal;  // 歷史總累儲 (NT$)

        // 選取的套餐金幣（-1 = 手動輸入，用 ×100 基礎率）
        private long _selectedGold = -1;
        // 優惠加成 %（0/5/10/15/20）—— 只影響玩家實際入帳金幣，不影響累積儲值進度
        private int _bonusPct = 0;
        private Button[] _bonusBtns = Array.Empty<Button>();

        /// <summary>要加入 paydata.point 的台幣金額（不含優惠贈金）</summary>
        public long TwdAmount  => (long)_nudTwd.Value;
        /// <summary>要加入 VipPoint 的金幣（套餐金額 × (1 + bonus%)；累積儲值進度只計台幣，不含此贈金）</summary>
        public long GoldAmount => (long)Math.Round((_selectedGold >= 0 ? _selectedGold : TwdAmount * 100L) * (1 + _bonusPct / 100.0));
        public bool GiveGold   => _rbWithGold.Checked;

        // (顯示文字, 台幣, 金幣（含加成）)
        private static readonly (string Label, long Twd, long Gold)[] Tiers =
        {
            ("NT$100\n1萬金",     100,     10_000),
            ("NT$300\n3.2萬",     300,     32_000),
            ("NT$500\n5.5萬",     500,     55_000),
            ("NT$1K\n11.5萬",   1_000,    115_000),
            ("NT$3K\n36萬",     3_000,    360_000),
            ("NT$5K\n62.5萬",   5_000,    625_000),
            ("NT$10K\n130萬",  10_000,  1_300_000),
        };

        public AdjustRechargeDialog(string playerName, long currentPayTotal, long lifetimePayTotal)
        {
            _currentTotal  = currentPayTotal;
            _lifetimeTotal = lifetimePayTotal;
            Text           = $"💳 調整累積充值 — {playerName}";
            Size           = new Size(660, 650);
            BackColor = Theme.BgPage;
            ForeColor      = Theme.TextPrimary;
            Font           = Theme.FontBody;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox    = false;
            MinimizeBox    = false;
            StartPosition  = FormStartPosition.CenterParent;

            const int x = 18;
            const int W = 616;
            int y = 14;

            // ── 目前狀態資訊框 ─────────────────────────────────────
            long curCycle  = currentPayTotal / CYCLE;
            long curIn     = currentPayTotal % CYCLE;
            long curRemain = CYCLE - curIn;
            int  curPct    = (int)(curIn * 100 / CYCLE);

            var infoBox = new Panel { Location = new Point(x, y), Size = new Size(W, 96), BackColor = Theme.BgCard };
            infoBox.Controls.Add(new Label
            {
                Text      = "累積充值（台幣，費實際付款） — 1 台幣 = 100 元寶",
                ForeColor = Color.FromArgb(150, 165, 200), Font = Theme.FontSmall, AutoSize = true, Location = new Point(10, 6)
            });
            infoBox.Controls.Add(new Label
            {
                Text      = $"NT$ {currentPayTotal:N0}",
                ForeColor = Color.FromArgb(255, 200, 80),
                Font      = new Font(Theme.FontFamily, 12f, FontStyle.Bold),
                AutoSize  = true, Location = new Point(10, 22)
            });
            infoBox.Controls.Add(new Label
            {
                Text      = "← 遊戲內「累積充值獎勵」讀取此值",
                ForeColor = Color.FromArgb(110, 150, 220), Font = Theme.FontSmall, AutoSize = true, Location = new Point(165, 28)
            });
            bool sameLifetime = _lifetimeTotal == currentPayTotal;
            infoBox.Controls.Add(new Label
            {
                Text      = sameLifetime
                    ? $"歷史總累儲（永不歸零）：NT$ {_lifetimeTotal:N0}（尚無重置記錄）"
                    : $"歷史總累儲（永不歸零）：NT$ {_lifetimeTotal:N0}（跨越 {_lifetimeTotal / CYCLE} 個循環）",
                ForeColor = Color.FromArgb(100, 210, 255), Font = Theme.FontSmall, AutoSize = true, Location = new Point(10, 48)
            });
            string curCycStr = curCycle == 0
                ? $"第 1 循環 · 本循環 NT${curIn:N0} / $25,000 · 還差 NT${curRemain:N0}"
                : $"第 {curCycle + 1} 循環（完成 {curCycle} 次）· 本循環 NT${curIn:N0} / $25,000 · 還差 NT${curRemain:N0}";
            infoBox.Controls.Add(new Label
            {
                Text = curCycStr, ForeColor = Color.FromArgb(110, 175, 255), Font = Theme.FontSmall, AutoSize = true, Location = new Point(10, 66)
            });
            var barBg0 = new Panel { Location = new Point(10, 80), Size = new Size(W - 70, 8), BackColor = Theme.BgCard };
            barBg0.Controls.Add(new Panel
            {
                Location  = new Point(0, 0),
                Size      = new Size(Math.Max(2, (int)((W - 70) * curPct / 100.0)), 8),
                BackColor = curPct >= 80 ? Color.FromArgb(50, 220, 120) : curPct >= 40 ? Color.FromArgb(255, 190, 60) : Color.FromArgb(80, 140, 255)
            });
            infoBox.Controls.Add(barBg0);
            infoBox.Controls.Add(new Label { Text = $"{curPct}%", ForeColor = Color.FromArgb(80, 110, 160), Font = Theme.FontSmall, AutoSize = true, Location = new Point(W - 54, 78) });
            Controls.Add(infoBox);
            y += 104;

            // ── 快選套餐（以台幣為輸入，金幣自動套加成）─────────────
            Div(x, y, W); y += 10;
            Controls.Add(new Label
            {
                Text = "STEP 1  選擇充值套餐（台幣）— 金幣依加成率自動計算：",
                ForeColor = Color.FromArgb(100, 180, 255), Font = new Font(Theme.FontFamily, 9.5f, FontStyle.Bold),
                AutoSize = true, Location = new Point(x, y)
            });
            y += 22;

            _tierBtns = new Button[Tiers.Length];
            int bx = x;
            for (int i = 0; i < Tiers.Length; i++)
            {
                var (label, twd, gold) = Tiers[i];
                long captTwd = twd; long captGold = gold;
                var btn = new Button
                {
                    Text      = label,
                    BackColor = Theme.BgCard,
                    ForeColor = Color.FromArgb(200, 215, 255),
                    FlatStyle = FlatStyle.Flat,
                    Font      = new Font(Theme.FontFamily, 7.5f),
                    Size      = new Size(82, 52),
                    Location  = new Point(bx, y),
                    Cursor    = Cursors.Hand,
                    UseVisualStyleBackColor = false,
                    Tag       = (twd, gold)
                };
                btn.FlatAppearance.BorderColor = Color.FromArgb(40, 55, 90);
                btn.Click += (s, e) =>
                {
                    _nudTwd.Value   = Math.Min(captTwd, _nudTwd.Maximum);
                    _selectedGold   = captGold;
                    RefreshTierButtons(captTwd);
                    UpdateGoldPreview();
                    UpdateCycleAfter();
                };
                Controls.Add(btn);
                _tierBtns[i] = btn;
                bx += 84;
            }
            y += 58;

            // ── 優惠加成 ─────────────────────────────────────────
            Div(x, y, W); y += 10;
            Controls.Add(new Label
            {
                Text      = "STEP 2  選擇優惠加成%（贈金加成，累積儲值進度只計台幣，贈金不計入進度）：",
                ForeColor = Color.FromArgb(100, 220, 100), Font = Theme.FontSmall, AutoSize = true, Location = new Point(x, y + 2)
            });
            y += 22;

            int[] bonusPcts = { 0, 5, 10, 15, 20 };
            _bonusBtns = new Button[bonusPcts.Length];
            int bbx = x;
            for (int i = 0; i < bonusPcts.Length; i++)
            {
                int pct = bonusPcts[i];
                var bb = new Button
                {
                    Text      = pct == 0 ? "無加成" : $"+{pct}%",
                    Size      = new Size(80, 28),
                    Location  = new Point(bbx, y),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Theme.BgInput,
                    ForeColor = Theme.TextSecondary,
                    Font      = Theme.FontSmall,
                    Cursor    = Cursors.Hand,
                    UseVisualStyleBackColor = false,
                    Tag       = pct,
                };
                bb.FlatAppearance.BorderColor = Theme.Border;
                bb.Click += (s, e) =>
                {
                    _bonusPct = (int)((Button)s).Tag;
                    RefreshBonusBtns();
                    UpdateGoldPreview();
                };
                Controls.Add(bb);
                _bonusBtns[i] = bb;
                bbx += 84;
            }
            RefreshBonusBtns();
            y += 36;

            // ── 手動輸入台幣 ──────────────────────────────────────
            Div(x, y, W); y += 10;
            Controls.Add(new Label
            {
                Text = "STEP 2  或手動輸入台幣金額（金幣以基礎率 ×100 計算，無套餐加成）：",
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall, AutoSize = true, Location = new Point(x, y + 4)
            });
            y += 22;

            _nudTwd = new NumericUpDown
            {
                Location           = new Point(x, y),
                Width              = 160,
                Minimum            = 1,
                Maximum            = 99_999_999,
                Value              = 100,
                BackColor = Theme.BgInput,
                ForeColor          = Theme.TextPrimary,
                Font               = Theme.FontBody,
                ThousandsSeparator = true
            };
            _nudTwd.ValueChanged += (s, e) => { RefreshTierButtons(-1); _selectedGold = -1; UpdateGoldPreview(); UpdateCycleAfter(); };
            Controls.Add(_nudTwd);

            Controls.Add(new Label { Text = "NT$", ForeColor = Theme.TextMuted, Font = Theme.FontSmall, AutoSize = true, Location = new Point(x + 168, y + 5) });

            _lblGoldCalc = new Label
            {
                Text      = "→ 金幣：10,000（套餐加成）",
                ForeColor = Color.FromArgb(180, 240, 140),
                Font      = new Font(Theme.FontFamily, 9.5f, FontStyle.Bold),
                AutoSize  = true,
                Location  = new Point(x + 200, y + 3)
            };
            Controls.Add(_lblGoldCalc);
            y += 38;

            // ── 新增後循環預覽 ────────────────────────────────────
            Div(x, y, W); y += 10;
            Controls.Add(new Label
            {
                Text = "新增後累儲進度預覽：",
                ForeColor = Color.FromArgb(140, 220, 140), Font = new Font(Theme.FontFamily, 9.5f, FontStyle.Bold),
                AutoSize = true, Location = new Point(x, y)
            });
            y += 22;

            _lblCycleAfter = new Label
            {
                Text = "", ForeColor = Color.FromArgb(80, 200, 255),
                Font = new Font(Theme.FontFamily, 10f, FontStyle.Bold),
                AutoSize = true, Location = new Point(x, y)
            };
            Controls.Add(_lblCycleAfter);
            y += 22;

            var barBgAfter = new Panel { Location = new Point(x, y), Size = new Size(W - 52, 8), BackColor = Theme.BgCard };
            _barFillAfter  = new Panel { Location = new Point(0, 0), Size = new Size(4, 8), BackColor = Color.FromArgb(50, 220, 120) };
            barBgAfter.Controls.Add(_barFillAfter);
            Controls.Add(barBgAfter);
            y += 18;

            // ── 操作類型（強制選擇，預設空白）──────────────────────
            Div(x, y, W); y += 10;
            var opBox = new Panel { Location = new Point(x, y), Size = new Size(W, 90), BackColor = Theme.BgCard };
            opBox.Controls.Add(new Label
            {
                Text      = "⚠ STEP 3  操作類型（必填）— 請明確選擇，系統不設預設值：",
                ForeColor = Color.FromArgb(255, 200, 80), Font = new Font(Theme.FontFamily, 9.5f, FontStyle.Bold),
                AutoSize = true, Location = new Point(10, 8)
            });
            _rbOnlyProgress = new RadioButton
            {
                Text      = "🔘  【僅增加累儲進度】— 不發放金幣（補資料 / 賽季轉移用）",
                ForeColor = Color.FromArgb(160, 210, 255), Font = new Font(Theme.FontFamily, 9.5f),
                AutoSize = true, Location = new Point(10, 32), Checked = false, Cursor = Cursors.Hand
            };
            _rbWithGold = new RadioButton
            {
                Text      = "🟡  【增加累儲進度 ＋ 同步發放金幣】— 正常補單使用",
                ForeColor = Color.FromArgb(200, 240, 170), Font = new Font(Theme.FontFamily, 9.5f),
                AutoSize = true, Location = new Point(10, 60), Checked = false, Cursor = Cursors.Hand
            };
            opBox.Controls.AddRange(new Control[] { _rbOnlyProgress, _rbWithGold });
            Controls.Add(opBox);
            y += 98;

            // ── 確定 / 取消 ──────────────────────────────────────
            var btnOk = Theme.MakeButton("✓ 確認執行", Theme.AccentGreen, Color.White, 120, 36);
            btnOk.Location = new Point(x + 350, y);
            btnOk.Click += (s, e) =>
            {
                if (!_rbOnlyProgress.Checked && !_rbWithGold.Checked)
                {
                    MessageBox.Show("請選擇操作類型（STEP 3）。\n系統不設預設值，以防止誤操作。",
                        "⚠ 尚未選擇操作類型", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                long twd  = TwdAmount;
                long gold = GoldAmount;
                if (twd <= 0)
                {
                    MessageBox.Show("請輸入大於 0 的台幣金額。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                long newTot = _currentTotal + twd;
                long newCyc = newTot / CYCLE;
                long newIn  = newTot % CYCLE;
                long gained = newCyc - (_currentTotal / CYCLE);
                string cycNote = gained > 0
                    ? $"\n🎉 完成 {gained} 個循環，進入第 {newCyc + 1} 循環（本循環 NT${newIn:N0} / $25,000）"
                    : $"\n   累積後：第 {newCyc + 1} 循環，本循環 NT${newIn:N0} / $25,000";

                string modeTitle, modeDetail, icon;
                if (_rbOnlyProgress.Checked)
                {
                    modeTitle  = "【僅增加累儲進度】";
                    modeDetail = $"❌ 不會發放金幣\n✅ 累積充值進度 +NT${twd:N0}（只計台幣，不含贈金）{cycNote}\n✅ 歷史總累儲同步更新";
                    icon       = "⚠";
                }
                else
                {
                    long baseGold  = _selectedGold >= 0 ? _selectedGold : twd * 100L;
                    long bonusGold = gold - baseGold;
                    string goldBreakdown = _bonusPct > 0
                        ? $"+{baseGold:N0}（套餐）＋ +{bonusGold:N0}（+{_bonusPct}% 優惠）＝ 共 {gold:N0} 金幣"
                        : $"+{gold:N0} 金幣（{(_selectedGold >= 0 ? "套餐加成" : "基礎率 ×100")}）";
                    modeTitle  = "【增加累儲進度 ＋ 同步發放金幣】";
                    modeDetail = $"✅ 金幣入帳：{goldBreakdown}\n✅ 累積充值進度 +NT${twd:N0}（只計台幣，優惠贈金不納入）{cycNote}\n✅ 歷史總累儲同步更新";
                    icon       = "💰";
                }

                if (MessageBox.Show(
                    $"{icon} 提醒：您本次操作 {modeTitle}\n\n{modeDetail}\n\n請確認是否執行？",
                    "二次確認 — 請仔細核對操作模式",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
            };

            var btnCancel = Theme.MakeButton("✕ 取消", Theme.BgLight, Theme.TextSecondary, 90, 36);
            btnCancel.Location = new Point(x + 480, y);
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.AddRange(new Control[] { btnOk, btnCancel });

            // 初始化
            RefreshTierButtons(-1);
            UpdateGoldPreview();
            UpdateCycleAfter();
        }

        private void Div(int x, int y, int w) =>
            Controls.Add(new Panel { Location = new Point(x, y), Size = new Size(w, 1), BackColor = Theme.Border });

        private void RefreshTierButtons(long selectedTwd)
        {
            foreach (var btn in _tierBtns)
            {
                var (twd, _) = ((long, long))btn.Tag;
                bool sel = twd == selectedTwd;
                btn.BackColor = sel ? Color.FromArgb(40, 90, 200) : Color.FromArgb(28, 38, 62);
                btn.ForeColor = sel ? Color.White : Color.FromArgb(200, 215, 255);
                btn.FlatAppearance.BorderColor = sel ? Color.FromArgb(80, 140, 255) : Color.FromArgb(40, 55, 90);
            }
        }

        private void UpdateGoldPreview()
        {
            long twd       = (long)_nudTwd.Value;
            long baseGold  = _selectedGold >= 0 ? _selectedGold : twd * 100L;
            long bonusGold = (long)Math.Round(baseGold * _bonusPct / 100.0);
            long totalGold = baseGold + bonusGold;
            string rateNote = _selectedGold >= 0
                ? $"（套餐加成，{(double)baseGold / twd:F1}x/NT$）"
                : "（基礎率 ×100）";
            string bonusNote = _bonusPct > 0
                ? $"  ＋  {bonusGold:N0}（+{_bonusPct}% 優惠）  ＝  共 {totalGold:N0} 金幣"
                : "";
            _lblGoldCalc.Text      = $"→ 金幣：{baseGold:N0}　{rateNote}{bonusNote}";
            _lblGoldCalc.ForeColor = _bonusPct > 0
                ? Color.FromArgb(80, 230, 130)
                : (_selectedGold >= 0 ? Color.FromArgb(120, 240, 120) : Color.FromArgb(200, 200, 120));
        }

        private void RefreshBonusBtns()
        {
            foreach (var btn in _bonusBtns)
            {
                int pct = (int)btn.Tag;
                bool sel = pct == _bonusPct;
                btn.BackColor = sel ? (pct > 0 ? Color.FromArgb(25, 70, 35) : Theme.BgCard) : Theme.BgInput;
                btn.ForeColor = sel ? (pct > 0 ? Color.FromArgb(80, 220, 100) : Theme.TextPrimary) : Theme.TextSecondary;
                btn.FlatAppearance.BorderColor = sel
                    ? (pct > 0 ? Color.FromArgb(60, 180, 80) : Color.FromArgb(80, 100, 140))
                    : Theme.Border;
            }
        }

        private void UpdateCycleAfter()
        {
            long twd      = (long)_nudTwd.Value;
            long newTotal = _currentTotal + twd;
            long newCycle = newTotal / CYCLE;
            long newIn    = newTotal % CYCLE;
            long gained   = newCycle - (_currentTotal / CYCLE);
            int  pct      = (int)(newIn * 100 / CYCLE);

            _lblCycleAfter.Text = gained > 0
                ? $"🎉 完成 {gained} 個循環！  →  第 {newCycle + 1} 循環  |  本循環 NT${newIn:N0} / $25,000"
                : $"第 {newCycle + 1} 循環  |  本循環 NT${newIn:N0} / $25,000  |  還差 NT${CYCLE - newIn:N0}";
            _lblCycleAfter.ForeColor = gained > 0 ? Color.FromArgb(80, 220, 130) : Color.FromArgb(80, 200, 255);

            if (_barFillAfter?.Parent != null)
            {
                int barW = _barFillAfter.Parent.Width;
                _barFillAfter.Width     = Math.Max(4, (int)(barW * pct / 100.0));
                _barFillAfter.BackColor = pct >= 80 ? Color.FromArgb(50, 220, 120)
                                        : pct >= 40 ? Color.FromArgb(255, 190, 60)
                                        : Color.FromArgb(80, 140, 255);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 🐾 寵物清單視窗（含編輯 / 刪除 / 轉移）
    // ══════════════════════════════════════════════════════════════
    internal class PetListForm : Form
    {
        private readonly string                _playerName;
        private readonly string                _account;
        private System.Collections.Generic.List<PetInfo> _pets;
        private DataGridView                   _dgv    = null!;
        private Label                          _lblHdr = null!;

        public PetListForm(string playerName, string account,
                           System.Collections.Generic.List<PetInfo> pets)
        {
            _playerName   = playerName;
            _account      = account;
            _pets         = pets;
            Text          = $"🐾 寵物清單 — {playerName}";
            Size          = new Size(920, 560);
            MinimumSize   = new Size(720, 420);
            BackColor     = Theme.BgPage;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;

            BuildUI();
            PopulateGrid();
        }

        // ── UI ────────────────────────────────────────────────────
        private void BuildUI()
        {
            // 標題
            var hdr = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Theme.BgCard };
            _lblHdr = new Label
            {
                ForeColor = Color.FromArgb(150, 240, 170), Font = Theme.FontBody,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            };
            hdr.Controls.Add(_lblHdr);

            // DGV
            _dgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(_dgv);
            _dgv.ReadOnly              = true;
            _dgv.RowTemplate.Height    = 26;
            _dgv.ColumnHeadersHeight   = 28;
            _dgv.AllowUserToResizeRows = false;
            _dgv.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            _dgv.MultiSelect           = false;
            _dgv.CellDoubleClick      += (s, e) => { if (e.RowIndex >= 0) OpenEdit(e.RowIndex); };

            void Col(string name, string h, int w,
                     DataGridViewContentAlignment align = DataGridViewContentAlignment.MiddleLeft)
                => _dgv.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = name, HeaderText = h, Width = w,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    DefaultCellStyle = { Alignment = align }
                });

            Col("cId",   "寵物ID",  68, DataGridViewContentAlignment.MiddleCenter);
            Col("cName", "名稱",   140);
            Col("cType", "類型",    86);
            Col("cLv",   "等級",    52, DataGridViewContentAlignment.MiddleCenter);
            Col("cHp",   "HP",      88, DataGridViewContentAlignment.MiddleRight);
            Col("cAtk",  "攻擊",    72, DataGridViewContentAlignment.MiddleRight);
            Col("cDef",  "防禦",    72, DataGridViewContentAlignment.MiddleRight);
            Col("cSpd",  "速度",    64, DataGridViewContentAlignment.MiddleRight);
            Col("cSum",  "戰力",    80, DataGridViewContentAlignment.MiddleRight);
            Col("cAuth", "捕捉者", 106);
            Col("cStat", "狀態",    68, DataGridViewContentAlignment.MiddleCenter);

            // 右鍵選單
            var ctx = new ContextMenuStrip { BackColor = Theme.BgCard, ForeColor = Theme.TextPrimary };
            var miEdit     = ctx.Items.Add("✏️ 編輯四維");
            var miDelete   = ctx.Items.Add("🗑️ 刪除寵物");
            var miTransfer = ctx.Items.Add("🔄 轉移帳號");
            miEdit.Click     += (_, __) => { if (ContextRow() >= 0) OpenEdit(ContextRow()); };
            miDelete.Click   += async (_, __) => { if (ContextRow() >= 0) await DeletePetAsync(ContextRow()); };
            miTransfer.Click += async (_, __) => { if (ContextRow() >= 0) await TransferPetAsync(ContextRow()); };
            _dgv.ContextMenuStrip = ctx;

            // 頁尾工具列
            var foot = new Panel { Dock = DockStyle.Bottom, Height = 46, BackColor = Theme.BgCard };

            var btnEdit     = Theme.MakeButton("✏️ 編輯四維", Color.FromArgb(20, 80, 50), Color.FromArgb(130, 220, 130), 100, 30);
            var btnDelete   = Theme.MakeButton("🗑️ 刪除",    Color.FromArgb(90, 20, 20),  Color.FromArgb(255, 120, 100),  80, 30);
            var btnTransfer = Theme.MakeButton("🔄 轉移",    Color.FromArgb(20, 50, 100), Color.FromArgb(100, 180, 255),  80, 30);
            var btnDiag     = Theme.MakeButton("🔍 診斷",    Color.FromArgb(60, 50, 10),  Color.FromArgb(255, 210, 80),   72, 30);
            var btnClose    = Theme.MakeButton("關 閉",      Theme.BgLight,              Theme.TextSecondary,             72, 30);
            var lblTip      = new Label
            {
                Text      = "雙擊列可快速編輯",
                ForeColor = Theme.TextSecondary,
                Font      = Theme.FontSmall,
                AutoSize  = true
            };

            btnEdit.Location     = new Point(10, 8);
            btnDelete.Location   = new Point(116, 8);
            btnTransfer.Location = new Point(202, 8);
            btnDiag.Location     = new Point(288, 8);
            lblTip.Location      = new Point(368, 14);

            btnClose.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnClose.Location = new Point(foot.Width - 88, 8);

            btnEdit.Click     += (_, __) => { int r = SelectedRow(); if (r >= 0) OpenEdit(r); };
            btnDelete.Click   += async (_, __) => { int r = SelectedRow(); if (r >= 0) await DeletePetAsync(r); };
            btnTransfer.Click += async (_, __) => { int r = SelectedRow(); if (r >= 0) await TransferPetAsync(r); };
            btnDiag.Click     += async (_, __) => await RunDiagnosisAsync();
            btnClose.Click    += (_, __) => Close();

            foot.Controls.AddRange(new Control[]
                { btnEdit, btnDelete, btnTransfer, btnDiag, lblTip, btnClose });

            Controls.Add(_dgv);
            Controls.Add(foot);
            Controls.Add(hdr);
        }

        // ── 填充 Grid ─────────────────────────────────────────────
        private void PopulateGrid()
        {
            _dgv.Rows.Clear();
            foreach (var p in _pets)
            {
                int ri = _dgv.Rows.Add(p.Id, p.Name, p.Type, p.Lv,
                    $"{p.Hp:N0}", $"{p.Attack:N0}", $"{p.Def:N0}", $"{p.Quick:N0}",
                    $"{p.Sum:N2}", p.Author, p.StatusText);
                if (p.Check == 1)
                    _dgv.Rows[ri].DefaultCellStyle.ForeColor = Color.FromArgb(255, 210, 60);
            }
            _lblHdr.Text =
                $"🐾  {_playerName} 的寵物清單  —  共 {_pets.Count} 隻（依戰力排序，雙擊可編輯）";
        }

        // ── 輔助 ──────────────────────────────────────────────────
        private int SelectedRow()
        {
            if (_dgv.SelectedRows.Count == 0)
            {
                MessageBox.Show("請先選擇一隻寵物。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return -1;
            }
            return _dgv.SelectedRows[0].Index;
        }

        private int _contextRow = -1;
        private int ContextRow() => _contextRow;
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _dgv.MouseDown += (s, ev) =>
            {
                if (ev.Button == MouseButtons.Right)
                {
                    var hit = _dgv.HitTest(ev.X, ev.Y);
                    _contextRow = hit.RowIndex >= 0 ? hit.RowIndex : -1;
                    if (_contextRow >= 0) _dgv.Rows[_contextRow].Selected = true;
                }
            };
        }

        // ── 操作 ──────────────────────────────────────────────────
        private void OpenEdit(int rowIndex)
        {
            var pet = _pets[rowIndex];
            using var dlg = new PetEditDialog(pet);
            if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Result == null) return;

            var updated = dlg.Result;
            _ = SaveEditAsync(updated, rowIndex);
        }

        private async System.Threading.Tasks.Task SaveEditAsync(PetInfo updated, int rowIndex)
        {
            bool ok = await DatabaseManager.Instance.UpdatePetAsync(updated);
            if (!ok)
            {
                MessageBox.Show("儲存失敗，請稍後再試。", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            // 更新本地列表
            _pets[rowIndex] = updated;
            PopulateGrid();
            // 保持同一列選取
            if (rowIndex < _dgv.Rows.Count)
                _dgv.Rows[rowIndex].Selected = true;
            MessageBox.Show($"✅ 已儲存：{updated.Name}", "成功",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async System.Threading.Tasks.Task DeletePetAsync(int rowIndex)
        {
            var pet = _pets[rowIndex];
            var ans = MessageBox.Show(
                $"確定要刪除寵物「{pet.Name}」（ID:{pet.Id}）嗎？\n此操作無法還原！",
                "確認刪除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (ans != DialogResult.Yes) return;

            bool ok = await DatabaseManager.Instance.DeletePetAsync(pet.Unicode, pet.Name);
            if (!ok)
            {
                MessageBox.Show("刪除失敗，請稍後再試。", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            _pets.RemoveAt(rowIndex);
            PopulateGrid();
            MessageBox.Show($"🗑️ 已刪除：{pet.Name}", "成功",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async System.Threading.Tasks.Task TransferPetAsync(int rowIndex)
        {
            var pet = _pets[rowIndex];
            string target = Theme.ShowInputDialog(
                "寵物轉移",
                $"請輸入目標帳號（要將「{pet.Name}」轉移過去）：",
                "", this);
            if (string.IsNullOrWhiteSpace(target)) return;
            target = target.Trim();

            // 確認目標帳號存在
            var (exists, _) = await DatabaseManager.Instance.CheckAccountExistsAsync(target);
            if (!exists)
            {
                MessageBox.Show($"帳號「{target}」不存在！", "錯誤",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var ans = MessageBox.Show(
                $"確定要將「{pet.Name}」轉移給帳號「{target}」嗎？",
                "確認轉移", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (ans != DialogResult.Yes) return;

            bool ok = await DatabaseManager.Instance.TransferPetAsync(pet.Unicode, target, pet.Name);
            if (!ok)
            {
                MessageBox.Show("轉移失敗，請稍後再試。", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            // 從清單移除（已不屬於此帳號）
            _pets.RemoveAt(rowIndex);
            PopulateGrid();
            MessageBox.Show($"✅ 已轉移「{pet.Name}」→ {target}", "成功",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ── 🔍 cdkey 格式診斷 ─────────────────────────────────────
        private async System.Threading.Tasks.Task RunDiagnosisAsync()
        {
            var (dbName, dbOnline, dbUid, byId, sampleRows) =
                await DatabaseManager.Instance.DiagnosePetCdkeyAsync(_account, _playerName);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("── csalogin 資料 ──────────────────────────────────");
            sb.AppendLine($"  Name（登入帳號）  : [{dbName}]");
            sb.AppendLine($"  OnlineName（角色名）: [{dbOnline}]");
            sb.AppendLine($"  uid               : [{dbUid}]");
            sb.AppendLine();

            sb.AppendLine("── 用已知識別碼搜 capturepet（Name/OnlineName/uid）──");
            if (byId.Count == 0)
                sb.AppendLine("  ❌ 三者都找不到任何寵物");
            else
                foreach (var (cdkey, author, petName, petId) in byId)
                    sb.AppendLine($"  cdkey=[{cdkey}] author=[{author}] 寵物={petName}(ID:{petId})");

            sb.AppendLine();
            sb.AppendLine("── capturepet 最新 10 筆（看 cdkey 真實格式）──────");
            if (sampleRows.Count == 0)
                sb.AppendLine("  （表格為空或無法存取）");
            else
                foreach (var (cdkey, author, petName, petId) in sampleRows)
                    sb.AppendLine($"  cdkey=[{cdkey}] author=[{author}] 寵物={petName}(ID:{petId})");

            sb.AppendLine();
            sb.AppendLine("◆ 請將上方內容截圖或複製回報，即可確認 cdkey 格式。");

            using var dlg = new Form
            {
                Text          = "🔍 cdkey 診斷報告",
                Size          = new Size(640, 440),
                BackColor     = Theme.BgPage,
                ForeColor     = Theme.TextPrimary,
                Font          = Theme.FontSmall,
                StartPosition = FormStartPosition.CenterParent
            };
            var rtb = new RichTextBox
            {
                Dock        = DockStyle.Fill,
                Text        = sb.ToString(),
                BackColor   = Theme.BgLight,
                ForeColor   = Theme.TextPrimary,
                Font        = new Font("Consolas", 10f),
                ReadOnly    = true,
                BorderStyle = BorderStyle.None
            };
            var btnCopy = Theme.MakeButton("📋 複製全部", Theme.BgCard, Theme.TextPrimary, 100, 28);
            btnCopy.Dock   = DockStyle.Bottom;
            btnCopy.Click += (_, __) => Clipboard.SetText(sb.ToString());
            dlg.Controls.Add(rtb);
            dlg.Controls.Add(btnCopy);
            dlg.ShowDialog(this);
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 🔍 同 IP / 同 MAC 帳號查詢視窗
    // ══════════════════════════════════════════════════════════════
    internal class SameIpMacForm : Form
    {
        public SameIpMacForm(string title, System.Collections.Generic.List<PlayerInfo> accounts)
        {
            Text          = $"🔍 {title}（共 {accounts.Count} 個帳號）";
            Size          = new Size(680, 440);
            MinimumSize   = new Size(560, 320);
            BackColor     = Theme.BgPage;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;

            var hdr = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Theme.BgCard };
            hdr.Controls.Add(new Label
            {
                Text = accounts.Count == 0
                    ? $"🔍  {title}  —  沒有找到其他帳號"
                    : $"🔍  {title}  —  共 {accounts.Count} 個帳號（點擊開啟詳情）",
                ForeColor = Color.FromArgb(130, 180, 255), Font = Theme.FontBody,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            });

            var dgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(dgv);
            dgv.ReadOnly              = true;
            dgv.RowTemplate.Height    = 26;
            dgv.ColumnHeadersHeight   = 28;
            dgv.AllowUserToResizeRows = false;
            dgv.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgv.CursorChanged        += (s, e) => { };

            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cName", HeaderText = "角色名稱", Width = 130, SortMode = DataGridViewColumnSortMode.NotSortable });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cAcc",  HeaderText = "帳號 (cdkey)", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, SortMode = DataGridViewColumnSortMode.NotSortable });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cStat", HeaderText = "狀態", Width = 75, SortMode = DataGridViewColumnSortMode.NotSortable, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cTime", HeaderText = "最後登入", Width = 130, SortMode = DataGridViewColumnSortMode.NotSortable });

            foreach (var a in accounts)
            {
                int ri = dgv.Rows.Add(a.OnlineName, a.Account,
                    a.IsOnline ? "🟢 在線" : "⚫ 離線", a.LoginTime);
                dgv.Rows[ri].Tag = a;
                if (a.IsOnline)
                    dgv.Rows[ri].DefaultCellStyle.ForeColor = Color.FromArgb(80, 210, 140);
            }

            dgv.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0 || dgv.Rows[e.RowIndex].Tag is not PlayerInfo pi) return;
                new PlayerProfileForm(pi).ShowDialog(this);
            };

            var foot = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = Theme.BgCard };
            foot.Controls.Add(new Label
            {
                Text = "雙擊列可開啟玩家詳情", ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                AutoSize = true, Location = new Point(12, 14)
            });
            var btnClose = Theme.MakeButton("關 閉", Theme.BgLight, Theme.TextSecondary, 80, 30);
            btnClose.Location = new Point(foot.Width - 100, 7);
            btnClose.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnClose.Click   += (s, e) => Close();
            foot.Controls.Add(btnClose);

            Controls.Add(dgv);
            Controls.Add(foot);
            Controls.Add(hdr);
        }
    }
}