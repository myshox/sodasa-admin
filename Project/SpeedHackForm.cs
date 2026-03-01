using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    /// <summary>加速外掛偵測 — 統計 speedlog 並提供一鍵批量封禁</summary>
    public class SpeedHackForm : Form
    {
        private NumericUpDown   _nudMin;
        private Button          _btnLoad;
        private Label           _summaryLbl;
        private DataGridView    _dgv;
        private ComboBox        _cmbDuration;
        private Button          _btnBanSelected, _btnSelectAll, _btnSelectNone;
        private Label           _statusLbl;
        private List<SpeedHackEntry> _allData = new();

        public SpeedHackForm()
        {
            BackColor       = Theme.BgPage;
            ForeColor       = Theme.TextPrimary;
            Font            = Theme.FontBody;
            FormBorderStyle = FormBorderStyle.None;
            InitUI();
        }

        private void InitUI()
        {
            // ── 頂部工具列 ───────────────────────────────────────────
            var toolbar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 48,
                BackColor = Theme.BgCard,
                Padding   = new Padding(10, 8, 10, 0),
            };

            var lTitle = new Label
            {
                Text      = "⚡  加速外掛偵測",
                Font      = new Font(Theme.FontFamily, 13, FontStyle.Bold),
                ForeColor = Theme.AccentOrange,
                AutoSize  = true,
                Location  = new Point(10, 12)
            };

            var lMin = new Label
            {
                Text      = "最低次數：",
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                AutoSize  = true,
                Location  = new Point(220, 16)
            };

            _nudMin = new NumericUpDown
            {
                Minimum   = 1,
                Maximum   = 999999,
                Value     = 10,
                Location  = new Point(300, 12),
                Size      = new Size(70, 24),
                BackColor = Theme.BgInput,
                ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Font      = Theme.FontBody,
            };

            _btnLoad = Theme.MakeButton("🔍 查詢", Theme.AccentBlue, Color.White, 80, 28);
            _btnLoad.Location = new Point(380, 10);
            _btnLoad.Font     = Theme.FontBody;
            _btnLoad.Click   += async (s, e) => await LoadDataAsync();

            _summaryLbl = new Label
            {
                Text      = "",
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                AutoSize  = true,
                Location  = new Point(480, 16)
            };

            toolbar.Controls.AddRange(new Control[] { lTitle, lMin, _nudMin, _btnLoad, _summaryLbl });

            // ── 批量操作列 ────────────────────────────────────────────
            var actionBar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 44,
                BackColor = Color.FromArgb(28, 28, 35),
                Padding   = new Padding(10, 7, 10, 0),
            };

            _btnSelectAll = Theme.MakeButton("全選未封", Theme.AccentBlue, Color.White, 84, 28);
            _btnSelectAll.Font     = Theme.FontSmall;
            _btnSelectAll.Location = new Point(10, 8);
            _btnSelectAll.Click   += (s, e) => SelectAll(true);

            _btnSelectNone = Theme.MakeButton("清除選取", Color.FromArgb(60, 60, 70), Color.White, 84, 28);
            _btnSelectNone.Font     = Theme.FontSmall;
            _btnSelectNone.Location = new Point(102, 8);
            _btnSelectNone.Click   += (s, e) => SelectAll(false);

            var lDur = new Label
            {
                Text      = "封禁時間：",
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                AutoSize  = true,
                Location  = new Point(200, 14)
            };

            _cmbDuration = new ComboBox
            {
                DropDownStyle   = ComboBoxStyle.DropDownList,
                BackColor       = Theme.BgInput,
                ForeColor       = Theme.TextPrimary,
                Font            = Theme.FontBody,
                Location        = new Point(272, 10),
                Width           = 110,
                FlatStyle       = FlatStyle.Flat,
            };
            _cmbDuration.Items.AddRange(new object[] { "永久", "1 天", "3 天", "7 天", "30 天" });
            _cmbDuration.SelectedIndex = 0;

            _btnBanSelected = Theme.MakeButton("🚫 封禁選取", Theme.AccentRed, Color.White, 120, 28);
            _btnBanSelected.Font     = new Font(Theme.FontFamily, 9, FontStyle.Bold);
            _btnBanSelected.Location = new Point(396, 8);
            _btnBanSelected.Enabled  = false;
            _btnBanSelected.Click   += async (s, e) => await BanSelectedAsync();

            _statusLbl = new Label
            {
                Text      = "",
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                AutoSize  = true,
                Location  = new Point(530, 14)
            };

            actionBar.Controls.AddRange(new Control[]
            {
                _btnSelectAll, _btnSelectNone, lDur, _cmbDuration, _btnBanSelected, _statusLbl
            });

            // ── DataGridView ──────────────────────────────────────────
            _dgv = new DataGridView
            {
                Dock                  = DockStyle.Fill,
                BackgroundColor       = Theme.BgPage,
                GridColor             = Theme.Border,
                DefaultCellStyle      = { BackColor = Theme.BgCard, ForeColor = Theme.TextPrimary, SelectionBackColor = Color.FromArgb(50, 74, 158, 255), SelectionForeColor = Color.White, Font = Theme.FontBody },
                ColumnHeadersDefaultCellStyle = { BackColor = Theme.BgInput, ForeColor = Theme.TextMuted, Font = Theme.FontSmall, Padding = new Padding(4) },
                EnableHeadersVisualStyles = false,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible     = false,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                ReadOnly              = false,
                BorderStyle           = BorderStyle.None,
                RowTemplate           = { Height = 28 },
                ScrollBars            = ScrollBars.Both,
            };
            _dgv.CellValueChanged  += DgvCellChanged;
            _dgv.CurrentCellDirtyStateChanged += (s, e) => { if (_dgv.IsCurrentCellDirty) _dgv.CommitEdit(DataGridViewDataErrorContexts.Commit); };

            Controls.AddRange(new Control[] { _dgv, actionBar, toolbar });
        }

        // ── 載入資料 ─────────────────────────────────────────────────
        private async Task LoadDataAsync()
        {
            _btnLoad.Enabled    = false;
            _summaryLbl.Text    = "載入中…";
            _summaryLbl.ForeColor = Theme.AccentOrange;
            _statusLbl.Text     = "";
            _dgv.Columns.Clear();
            _dgv.Rows.Clear();
            _allData.Clear();

            try
            {
                _allData = await DatabaseManager.Instance.GetSpeedHackPlayersAsync((long)_nudMin.Value);

                // 建立欄位
                _dgv.Columns.Add(new DataGridViewCheckBoxColumn
                {
                    Name = "sel", HeaderText = "選取", Width = 50,
                    ReadOnly = false,
                    TrueValue = true, FalseValue = false,
                });
                _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "status",   HeaderText = "狀態",         ReadOnly = true, Width = 80  });
                _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "charName",  HeaderText = "角色名稱",     ReadOnly = true, FillWeight = 120 });
                _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "account",   HeaderText = "帳號",         ReadOnly = true, FillWeight = 120 });
                _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "totalCnt",  HeaderText = "異常總次數",   ReadOnly = true, Width = 100, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });
                _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "records",   HeaderText = "紀錄筆數",     ReadOnly = true, Width = 80,  DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });
                _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "lastTime",  HeaderText = "最後偵測時間", ReadOnly = true, Width = 140 });

                foreach (var p in _allData)
                {
                    int idx = _dgv.Rows.Add(
                        false,                                       // 選取
                        p.IsBanned ? "🔒 已封" : p.IsOnline ? "🟢 在線" : "⚫ 離線",
                        p.CharName,
                        p.Account,
                        p.TotalCnt.ToString("N0"),
                        p.Records,
                        p.LastTime
                    );
                    var row = _dgv.Rows[idx];
                    if (p.IsBanned)
                    {
                        row.DefaultCellStyle.ForeColor = Theme.TextMuted;
                        ((DataGridViewCheckBoxCell)row.Cells["sel"]).ReadOnly = true;
                    }
                    else
                    {
                        Color c = p.TotalCnt > 1000 ? Theme.AccentRed
                                : p.TotalCnt > 100  ? Theme.AccentOrange
                                                    : Theme.TextPrimary;
                        row.Cells["totalCnt"].Style.ForeColor = c;
                        row.Cells["totalCnt"].Style.Font      = new Font(Theme.FontFamily, 9, FontStyle.Bold);
                    }
                }

                int banned  = _allData.Count(p => p.IsBanned);
                int pending = _allData.Count - banned;
                _summaryLbl.Text      = $"共 {_allData.Count} 人 | 已封 {banned} | 待處理 {pending}";
                _summaryLbl.ForeColor = Theme.TextPrimary;
            }
            catch (Exception ex)
            {
                _summaryLbl.Text      = "載入失敗：" + ex.Message;
                _summaryLbl.ForeColor = Theme.AccentRed;
            }
            finally { _btnLoad.Enabled = true; }
        }

        private void DgvCellChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex != _dgv.Columns["sel"]?.Index) return;
            int selCount = _dgv.Rows.Cast<DataGridViewRow>()
                .Count(r => r.Cells["sel"].Value is true);
            _btnBanSelected.Enabled = selCount > 0;
            _btnBanSelected.Text    = selCount > 0 ? $"🚫 封禁選取（{selCount}）" : "🚫 封禁選取";
        }

        private void SelectAll(bool check)
        {
            foreach (DataGridViewRow row in _dgv.Rows)
            {
                if (row.Cells["sel"].ReadOnly) continue;
                row.Cells["sel"].Value = check;
            }
            _dgv.CommitEdit(DataGridViewDataErrorContexts.Commit);
            _dgv.RefreshEdit();
            int selCount = _dgv.Rows.Cast<DataGridViewRow>().Count(r => r.Cells["sel"].Value is true);
            _btnBanSelected.Enabled = selCount > 0;
            _btnBanSelected.Text    = selCount > 0 ? $"🚫 封禁選取（{selCount}）" : "🚫 封禁選取";
        }

        private async Task BanSelectedAsync()
        {
            var toban = _dgv.Rows.Cast<DataGridViewRow>()
                .Where(r => r.Cells["sel"].Value is true)
                .Select(r => r.Cells["account"].Value?.ToString() ?? "")
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .ToList();

            if (toban.Count == 0) return;

            string durText = _cmbDuration.SelectedItem?.ToString() ?? "永久";
            if (MessageBox.Show(
                    $"確定封禁以下 {toban.Count} 位玩家？\n封禁時間：{durText}\n\n{string.Join("\n", toban.Take(10))}{(toban.Count > 10 ? $"\n…（共 {toban.Count} 位）" : "")}",
                    "確認批量封禁", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            int days = durText switch
            {
                "1 天"  =>  1,
                "3 天"  =>  3,
                "7 天"  =>  7,
                "30 天" => 30,
                _       =>  0
            };

            _btnBanSelected.Enabled = false;
            _statusLbl.ForeColor    = Theme.AccentOrange;
            _statusLbl.Text         = "封禁中…";

            try
            {
                var results = await DatabaseManager.Instance.BatchBanAsync(toban, days);
                int success = results.Count(r => r.ok);
                int fail    = results.Count - success;

                _statusLbl.ForeColor = fail == 0 ? Theme.AccentGreen : Theme.AccentOrange;
                _statusLbl.Text      = $"✓ 成功 {success}，失敗 {fail}";

                // 更新 UI 狀態
                foreach (DataGridViewRow row in _dgv.Rows)
                {
                    string acc = row.Cells["account"].Value?.ToString() ?? "";
                    if (toban.Contains(acc) && results.FirstOrDefault(r => r.account == acc).ok)
                    {
                        row.Cells["sel"].Value    = false;
                        row.Cells["sel"].ReadOnly = true;
                        row.Cells["status"].Value = "🔒 已封";
                        row.DefaultCellStyle.ForeColor = Theme.TextMuted;
                    }
                }
                _btnBanSelected.Text = "🚫 封禁選取";
            }
            catch (Exception ex)
            {
                _statusLbl.ForeColor = Theme.AccentRed;
                _statusLbl.Text      = "封禁失敗：" + ex.Message;
                _btnBanSelected.Enabled = true;
            }
        }
    }
}
