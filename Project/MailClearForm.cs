using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    /// <summary>清除遊戲內郵件 + 維護工具（修正舊郵件 buff3）</summary>
    public class MailClearForm : Form
    {
        private RadioButton _rbAll, _rbOnline, _rbSingle;
        private TextBox     _txtAccount;
        private CheckBox    _chkUnclaimedOnly;
        private Button      _btnClear;
        private Label       _statusLbl;

        // 維護工具區
        private Button _btnFixBuff3;
        private Button _btnFixAll;
        private Button _btnQuickClearUnclaimed;
        private Button _btnQuickClearAll;
        private Label  _fixStatusLbl;
        private Label  _descCountLbl;

        public MailClearForm()
        {
            BackColor       = Theme.BgPage;
            ForeColor       = Theme.TextPrimary;
            Font            = Theme.FontBody;
            FormBorderStyle = FormBorderStyle.None;
            AutoScroll      = true;
            InitUI();
        }

        private void InitUI()
        {
            int y = 24;
            const int x = 24;
            const int W = 620;

            // ── 標題 ──
            Controls.Add(new Label
            {
                Text      = "🗑  清除遊戲內郵件",
                Font      = new Font(Theme.FontFamily, 14, FontStyle.Bold),
                ForeColor = Theme.AccentRed,
                AutoSize  = true,
                Location  = new Point(x, y)
            });
            y += 42;

            // ── 目標選擇 ──
            var grpTarget = new GroupBox
            {
                Text      = "  目標範圍",
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                Location  = new Point(x, y),
                Size      = new Size(W, 100),
                BackColor = Theme.BgCard
            };

            _rbOnline = new RadioButton { Text = "🟢 僅在線玩家", Location = new Point(160, 24), AutoSize = true, ForeColor = Theme.TextPrimary, Checked = true, FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent };
            _rbAll    = new RadioButton { Text = "🌐 全部玩家",   Location = new Point(16,  24), AutoSize = true, ForeColor = Theme.TextPrimary, FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent };
            _rbSingle = new RadioButton { Text = "📋 指定帳號",   Location = new Point(320, 24), AutoSize = true, ForeColor = Theme.TextPrimary, FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent };
            _txtAccount = new TextBox
            {
                Location = new Point(16, 58), Size = new Size(584, 24),
                BackColor = Theme.BgInput, ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle, Font = Theme.FontBody,
                PlaceholderText = "輸入玩家帳號（選擇「指定帳號」時生效）", Enabled = false
            };
            _rbSingle.CheckedChanged += (s, e) => _txtAccount.Enabled = _rbSingle.Checked;
            grpTarget.Controls.AddRange(new Control[] { _rbAll, _rbOnline, _rbSingle, _txtAccount });
            Controls.Add(grpTarget);
            y += 110;

            // ── 選項 ──
            _chkUnclaimedOnly = new CheckBox
            {
                Text = "只清除未領取郵件（check=0，保留已領取的歷史記錄）",
                Location = new Point(x, y), AutoSize = true,
                ForeColor = Theme.TextPrimary, Checked = true,
                FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent
            };
            Controls.Add(_chkUnclaimedOnly);
            y += 32;

            // ── 警告 ──
            Controls.Add(new Label
            {
                Text      = "⚠  此操作為軟刪除（deleamill=1），玩家信箱會立即清空，操作不可逆，請謹慎使用。",
                ForeColor = Theme.AccentOrange, Font = Theme.FontSmall,
                Location  = new Point(x, y), AutoSize = true
            });
            y += 32;

            // ── 執行清除按鈕 ──
            _btnClear = Theme.MakeButton("🗑  執行清除", Theme.AccentRed, Color.White, 160, 38);
            _btnClear.Location = new Point(x, y);
            _btnClear.Font     = new Font(Theme.FontFamily, 11, FontStyle.Bold);
            _btnClear.Click   += async (s, e) => await DoClearAsync();
            Controls.Add(_btnClear);
            y += 52;

            // ── 清除結果 ──
            _statusLbl = new Label
            {
                Location = new Point(x, y), Size = new Size(W, 50),
                ForeColor = Theme.TextMuted, Font = Theme.FontBody, AutoSize = false
            };
            Controls.Add(_statusLbl);
            y += 58;

            // ════════════════════════════════════════════════════════
            // 🔧 維護工具區
            // ════════════════════════════════════════════════════════
            var grpFix = new Panel
            {
                Location  = new Point(x, y),
                Size      = new Size(W, 240),
                BackColor = Color.FromArgb(245, 101, 101, 15)
            };
            grpFix.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 2, BackColor = Color.FromArgb(180, 60, 60) });

            int fy = 14;
            grpFix.Controls.Add(new Label
            {
                Text = "🔧  維護工具", Font = new Font(Theme.FontFamily, 10f, FontStyle.Bold),
                ForeColor = Theme.AccentRed, AutoSize = true, Location = new Point(12, fy)
            });
            fy += 28;

            // 道具描述數量提示
            int descCount = GameDataManager.Instance.ItemCount + GameDataManager.Instance.PetCount;
            _descCountLbl = new Label
            {
                Text      = descCount > 0
                    ? $"✓ 已載入 {descCount} 種道具描述，可完整修復"
                    : "⚠ 請先在「⚙ 資料設定」載入 items.xlsx 以確保所有道具都能修復",
                ForeColor = descCount > 0 ? Theme.AccentGreen : Theme.AccentOrange,
                Font      = Theme.FontSmall, AutoSize = true, Location = new Point(14, fy)
            };
            grpFix.Controls.Add(_descCountLbl);
            fy += 22;

            // 修正按鈕列
            var fixRow = new FlowLayoutPanel
            {
                Location = new Point(12, fy), Size = new Size(W - 24, 38),
                FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent, WrapContents = false
            };
            _btnFixBuff3 = Theme.MakeButton("修正全服舊郵件 buff3（讓舊道具可領取）", Color.FromArgb(80, 30, 30), Color.FromArgb(245, 101, 101), 280, 34);
            _btnFixBuff3.Font    = Theme.FontSmall;
            _btnFixBuff3.Margin  = new Padding(0, 2, 8, 0);
            _btnFixBuff3.Click  += async (s, e) => await DoFixBuff3Async("");
            fixRow.Controls.Add(_btnFixBuff3);
            grpFix.Controls.Add(fixRow);
            fy += 44;

            // 修正結果顯示
            _fixStatusLbl = new Label
            {
                Location = new Point(14, fy), Size = new Size(W - 28, 36),
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall, AutoSize = false
            };
            grpFix.Controls.Add(_fixStatusLbl);
            fy += 42;

            // 分隔線
            grpFix.Controls.Add(new Panel
            {
                Location = new Point(0, fy), Size = new Size(W, 1),
                BackColor = Color.FromArgb(80, 40, 40)
            });
            fy += 10;

            grpFix.Controls.Add(new Label
            {
                Text = "🗑 一鍵清除全服遊戲內郵件（軟刪除，玩家信箱會清空）",
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                AutoSize = true, Location = new Point(14, fy)
            });
            fy += 22;

            var clearRow = new FlowLayoutPanel
            {
                Location = new Point(12, fy), Size = new Size(W - 24, 38),
                FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent
            };
            _btnQuickClearUnclaimed = Theme.MakeButton("清除全服未領取郵件", Color.FromArgb(60, 50, 10), Theme.AccentOrange, 170, 32);
            _btnQuickClearUnclaimed.Font   = Theme.FontSmall;
            _btnQuickClearUnclaimed.Margin = new Padding(0, 2, 8, 0);
            _btnQuickClearUnclaimed.Click += async (s, e) => await QuickClearAsync(true);

            _btnQuickClearAll = Theme.MakeButton("清除全服所有郵件", Color.FromArgb(70, 20, 20), Theme.AccentRed, 160, 32);
            _btnQuickClearAll.Font   = Theme.FontSmall;
            _btnQuickClearAll.Margin = new Padding(0, 2, 0, 0);
            _btnQuickClearAll.Click += async (s, e) => await QuickClearAsync(false);

            clearRow.Controls.Add(_btnQuickClearUnclaimed);
            clearRow.Controls.Add(_btnQuickClearAll);
            grpFix.Controls.Add(clearRow);
            fy += 44;

            grpFix.Height = fy + 10;
            Controls.Add(grpFix);
        }

        // ── 清除（原有功能）──────────────────────────────────────────
        private async Task DoClearAsync()
        {
            bool   unclaimedOnly = _chkUnclaimedOnly.Checked;
            bool   onlineOnly    = _rbOnline.Checked;
            string account       = _rbSingle.Checked ? _txtAccount.Text.Trim() : "";

            if (_rbSingle.Checked && string.IsNullOrWhiteSpace(account))
            { MessageBox.Show("請輸入玩家帳號", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            string scopeLabel = _rbAll.Checked ? "全部玩家" : _rbOnline.Checked ? "在線玩家" : $"玩家「{account}」";
            string typeLabel  = unclaimedOnly ? "未領取郵件" : "全部郵件";

            if (MessageBox.Show($"確定清除「{scopeLabel}」的{typeLabel}？\n此操作不可逆！",
                    "確認清除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

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
            finally { if (!IsDisposed) _btnClear.Enabled = true; }
        }

        // ── 快速全服清除（維護工具區按鈕）────────────────────────────
        private async Task QuickClearAsync(bool unclaimedOnly)
        {
            string label = unclaimedOnly ? "未領取郵件" : "全部郵件";
            if (MessageBox.Show($"確定清除全服所有玩家的{label}？\n此操作不可逆，請謹慎操作！",
                    "確認清除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            _btnQuickClearUnclaimed.Enabled = false;
            _btnQuickClearAll.Enabled       = false;
            _fixStatusLbl.ForeColor         = Theme.AccentOrange;
            _fixStatusLbl.Text              = "清除中，請稍候…";
            try
            {
                int count = await DatabaseManager.Instance.ClearPlayerMailAsync("", unclaimedOnly);
                _fixStatusLbl.ForeColor = Theme.AccentGreen;
                _fixStatusLbl.Text      = $"✓ 已清除全服 {label} {count} 封";
            }
            catch (Exception ex)
            {
                _fixStatusLbl.ForeColor = Theme.AccentRed;
                _fixStatusLbl.Text      = "✗ 清除失敗：" + ex.Message;
            }
            finally
            {
                if (!IsDisposed)
                {
                    _btnQuickClearUnclaimed.Enabled = true;
                    _btnQuickClearAll.Enabled       = true;
                }
            }
        }

        // ── 修正舊郵件 buff3 ──────────────────────────────────────────
        private async Task DoFixBuff3Async(string account)
        {
            var gm       = GameDataManager.Instance;
            int descCount = gm.ItemCount + gm.PetCount;
            string descInfo = descCount > 0
                ? $"\n已載入 {descCount} 種道具描述，將逐一比對回填"
                : "\n⚠ 尚未載入 items.xlsx，只能用資料庫內既有記錄回填（可能不完整）";
            string scope = string.IsNullOrWhiteSpace(account) ? "全伺服器" : $"玩家 {account}";

            if (MessageBox.Show($"確定要修正{scope}所有 buff3 為空的舊郵件？{descInfo}",
                    "確認修正", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            _btnFixBuff3.Enabled            = false;
            _btnFixBuff3.Text               = "處理中…";
            _fixStatusLbl.ForeColor         = Theme.AccentOrange;
            _fixStatusLbl.Text              = "修正中，請稍候…";

            try
            {
                // 組合道具描述清單
                IEnumerable<(int, string)>? descs = null;
                if (descCount > 0)
                {
                    var list = new List<(int, string)>();
                    list.AddRange(gm.GetAllItems().Where(i => !string.IsNullOrEmpty(i.Description)).Select(i => (i.Id, i.Description)));
                    list.AddRange(gm.GetAllPets() .Where(p => !string.IsNullOrEmpty(p.Description)).Select(p => (p.Id, p.Description)));
                    descs = list;
                }

                var (titleFixed, buff3Fixed, totalEmpty) = await DatabaseManager.Instance.FixOldMailsAsync(account, descs);
                _fixStatusLbl.ForeColor = Theme.AccentGreen;
                _fixStatusLbl.Text =
                    $"✓ 修正完成（{scope}）\n" +
                    $"• 標題修正：{titleFixed} 筆\n" +
                    $"• buff3 回填：{buff3Fixed} 筆\n" +
                    $"• 掃描空 buff3 筆數：{totalEmpty} 筆";
            }
            catch (Exception ex)
            {
                _fixStatusLbl.ForeColor = Theme.AccentRed;
                _fixStatusLbl.Text      = "✗ 修正失敗：" + ex.Message;
            }
            finally
            {
                if (!IsDisposed)
                {
                    _btnFixBuff3.Enabled = true;
                    _btnFixBuff3.Text    = "修正全服舊郵件 buff3（讓舊道具可領取）";
                }
            }
        }
    }
}
