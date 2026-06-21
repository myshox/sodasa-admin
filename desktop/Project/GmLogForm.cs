using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    public class GmLogForm : Form
    {
        private DataGridView _dgv;
        private TextBox      _searchBox;
        private ComboBox     _cboDates;
        private Label        _statusLbl;

        public GmLogForm()
        {
            Text          = "📋 GM 操作日誌";
            Size          = new Size(1100, 660);
            MinimumSize   = new Size(800, 480);
            Theme.ApplyHubForm(this);

            StartPosition = FormStartPosition.CenterParent;

            BuildUI();
            _ = LoadLogDatesAsync();

            // 有新操作時自動刷新目前檢視（讓網頁/其他 GM 的操作也即時出現）
            GmLogger.Instance.LogUpdated += OnLogUpdated;
            FormClosed += (s, e) => GmLogger.Instance.LogUpdated -= OnLogUpdated;
        }

        private void OnLogUpdated()
        {
            if (IsDisposed) return;
            try { BeginInvoke(new Action(() => _ = LoadSelectedDateAsync())); } catch { }
        }

        private void BuildUI()
        {
            // ── Header ──
            var header = new Panel { Dock = DockStyle.Top, Height = Theme.ToolbarHeight, BackColor = Theme.BgDialogHeader };
            header.Controls.Add(new Label
            {
                Text      = "  📋  GM 操作日誌  —  查詢所有 GM 的操作記錄",
                ForeColor = Theme.AccentPurple,
                Font      = Theme.FontBody,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(8, 0, 0, 0)
            });

            // ── 工具列 ──
            var toolPanel = new Panel { Dock = DockStyle.Top, Height = Theme.ToolbarHeight, BackColor = Theme.BgCard };
            toolPanel.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme.Border });
            toolPanel.Controls.Add(new Label
            {
                Text = "選擇日期查看歷史記錄，或在搜尋框篩選本頁記錄",
                ForeColor = Theme.TextMuted, Font = new Font(Theme.FontFamily, 8.5f),
                AutoSize = true, Location = new Point(12, 4)
            });

            _cboDates = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor     = Theme.BgPage,
                ForeColor     = Theme.TextPrimary,
                Font          = new Font(Theme.FontFamily, 10.5f),
                Location      = new Point(12, 22),
                Width         = 160,
                Height        = 28
            };
            _cboDates.SelectedIndexChanged += (s, e) => _ = LoadSelectedDateAsync();

            var lblSearch = new Label
            {
                Text = "🔍", Font = new Font("Segoe UI Emoji", 14f),
                AutoSize = true, Location = new Point(182, 22)
            };

            _searchBox = new TextBox
            {
                BackColor       = Theme.BgPage, ForeColor = Theme.TextPrimary,
                BorderStyle     = BorderStyle.FixedSingle,
                Font            = new Font(Theme.FontFamily, 11f),
                PlaceholderText = "篩選操作、目標或詳情…",
                Location        = new Point(212, 22), Height = 28,
                Anchor          = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _searchBox.TextChanged += (s, e) => ApplyFilter();
            _searchBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    _ = LoadSelectedDateAsync();   // Enter：跨全部歷史於資料庫端搜尋
                }
            };

            var btnExport = Theme.MakeButton("💾 匯出", Theme.AccentGreen, Color.White, 90, 28);
            btnExport.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnExport.Click += ExportLog;

            toolPanel.Controls.AddRange(new Control[] { _cboDates, lblSearch, _searchBox, btnExport });
            toolPanel.Resize += (s, e) =>
            {
                int pw = toolPanel.ClientSize.Width;
                btnExport.Left  = pw - 12 - btnExport.Width;
                btnExport.Top   = 22;
                _searchBox.Width = Math.Max(100, btnExport.Left - _searchBox.Left - 8);
            };

            // ── 狀態列 ──
            var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = Theme.BgDark };
            _statusLbl = new Label
            {
                Text = "", ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            };
            statusBar.Controls.Add(_statusLbl);

            // ── DataGridView ──
            _dgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(_dgv);
            _dgv.ReadOnly           = true;
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cTime",   HeaderText = "時間",   Width = 140 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cOp",     HeaderText = "操作員", Width = 80  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cSrc",    HeaderText = "來源",   Width = 56  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cResult", HeaderText = "結果",   Width = 54  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cAction", HeaderText = "操作",   Width = 110 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cTarget", HeaderText = "目標",   Width = 120 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "cDetail", HeaderText = "詳情",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 200
            });

            Controls.Add(_dgv);
            Controls.Add(statusBar);
            Controls.Add(toolPanel);
            Controls.Add(header);
        }

        // ── 載入有紀錄的日期清單（從共用資料庫）───────────────────
        private async Task LoadLogDatesAsync()
        {
            string prev = _cboDates.SelectedItem?.ToString();
            var dates = await DatabaseManager.Instance.GetGmLogDatesAsync();

            _cboDates.SelectedIndexChanged -= OnDateChanged;
            _cboDates.Items.Clear();
            _cboDates.Items.Add("全部（最近）");
            foreach (var d in dates) _cboDates.Items.Add(d);

            int idx = prev != null ? _cboDates.Items.IndexOf(prev) : -1;
            _cboDates.SelectedIndex = idx >= 0 ? idx : 0;
            _cboDates.SelectedIndexChanged += OnDateChanged;

            await LoadSelectedDateAsync();
        }

        private void OnDateChanged(object sender, EventArgs e) => _ = LoadSelectedDateAsync();

        // ── 載入選定日期的紀錄（從共用資料庫）─────────────────────
        private async Task LoadSelectedDateAsync()
        {
            if (!DatabaseManager.Instance.IsConnected)
            {
                _dgv.Rows.Clear();
                _statusLbl.Text = "尚未連線資料庫，無法載入操作紀錄";
                return;
            }

            string date = _cboDates.SelectedIndex <= 0 ? "" : (_cboDates.SelectedItem?.ToString() ?? "");
            string keyword = _searchBox.Text.Trim();

            var (rows, total) = await DatabaseManager.Instance.GetGmLogsAsync(date, keyword, 0, 1000);
            if (IsDisposed) return;

            _dgv.Rows.Clear();
            foreach (var e in rows)
            {
                int i = _dgv.Rows.Add(
                    e.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                    e.Operator,
                    e.Source == "web" ? "網頁" : "工具",
                    e.Success ? "✓" : "✗",
                    e.Action, e.Target, e.Detail);
                _dgv.Rows[i].DefaultCellStyle.ForeColor = e.Success ? Theme.TextPrimary : Theme.AccentRed;
            }

            string scope = string.IsNullOrEmpty(date) ? "全部" : date;
            string filterNote = string.IsNullOrEmpty(keyword) ? "" : $"（關鍵字「{keyword}」）";
            _statusLbl.Text = total > rows.Count
                ? $"{scope}{filterNote} 共 {total} 筆，顯示最新 {rows.Count} 筆"
                : $"{scope}{filterNote} 共 {rows.Count} 筆記錄";
        }

        private void ApplyFilter()
        {
            string q = _searchBox.Text.Trim().ToLower();
            foreach (DataGridViewRow row in _dgv.Rows)
            {
                if (row.IsNewRow) continue;
                bool visible = string.IsNullOrEmpty(q) ||
                    row.Cells.Cast<DataGridViewCell>().Any(c => c.Value?.ToString()?.ToLower().Contains(q) == true);
                row.Visible = visible;
            }
            int shown = _dgv.Rows.Cast<DataGridViewRow>().Count(r => !r.IsNewRow && r.Visible);
            if (!string.IsNullOrEmpty(q))
                _statusLbl.Text = $"篩選後顯示 {shown} 筆";
        }

        private void ExportLog(object sender, EventArgs e)
        {
            using var sfd = new SaveFileDialog
            {
                Filter   = "文字檔 (*.txt)|*.txt",
                FileName = $"GM日誌_{DateTime.Today:yyyy-MM-dd}.txt"
            };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;
            var lines = new System.Text.StringBuilder();
            foreach (DataGridViewRow row in _dgv.Rows)
            {
                if (row.IsNewRow || !row.Visible) continue;
                lines.AppendLine(string.Join("\t", row.Cells.Cast<DataGridViewCell>()
                    .Select(c => c.Value?.ToString() ?? "")));
            }
            System.IO.File.WriteAllText(sfd.FileName, lines.ToString(), System.Text.Encoding.UTF8);
            _statusLbl.Text = "✓ 已匯出";
        }
    }
}
