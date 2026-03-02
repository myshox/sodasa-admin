using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
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
            BackColor     = Theme.BgMid;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;

            BuildUI();
            LoadLogFiles();
        }

        private void BuildUI()
        {
            // ── Header ──
            var header = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme.BgDark };
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
            var toolPanel = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Theme.BgCard };
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
            _cboDates.SelectedIndexChanged += (s, e) => LoadSelectedDate();

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
            _dgv.RowTemplate.Height = 32;

            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cTime",   HeaderText = "時間",   Width = 80  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cOp",     HeaderText = "操作員", Width = 80  });
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

        // ── 載入日誌檔列表 ──────────────────────────────────────
        private void LoadLogFiles()
        {
            _cboDates.Items.Clear();
            _cboDates.Items.Add("今日（即時）");
            var files = GmLogger.Instance.GetLogFiles();
            foreach (var f in files)
            {
                string dateStr = Path.GetFileNameWithoutExtension(f);
                _cboDates.Items.Add(dateStr);
            }
            _cboDates.SelectedIndex = 0;
        }

        // ── 載入選定日期的日誌 ──────────────────────────────────
        private void LoadSelectedDate()
        {
            _dgv.Rows.Clear();
            if (_cboDates.SelectedIndex == 0)
            {
                // 今日即時記錄
                var entries = GmLogger.Instance.RecentLogs;
                foreach (var e in entries.Reverse<GmLogEntry>())
                {
                    int i = _dgv.Rows.Add(
                        e.Time.ToString("HH:mm:ss"),
                        e.Operator,
                        e.Success ? "✓" : "✗",
                        e.Action, e.Target, e.Detail);
                    _dgv.Rows[i].DefaultCellStyle.ForeColor = e.Success ? Theme.TextPrimary : Theme.AccentRed;
                }
                _statusLbl.Text = $"今日共 {entries.Count} 筆操作記錄";
            }
            else
            {
                // 讀取歷史 log 檔
                string date = _cboDates.SelectedItem?.ToString() ?? "";
                string content = GmLogger.Instance.ReadLogFile(date + ".log");
                var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines.Reverse())
                {
                    int i = _dgv.Rows.Add(ParseLogLine(line));
                    if (line.Contains(" ✗ ")) _dgv.Rows[i].DefaultCellStyle.ForeColor = Theme.AccentRed;
                }
                _statusLbl.Text = $"{date} 共 {lines.Length} 筆記錄";
            }
            ApplyFilter();
        }

        private static string[] ParseLogLine(string line)
        {
            // 格式：[HH:mm:ss] [操作員] ✓/✗ 操作 | 目標 | 詳情
            try
            {
                string time = line.Length > 10 ? line.Substring(1, 8) : "";
                string rest = line.Length > 12 ? line.Substring(10).Trim() : line;
                string op   = "", result = "", action = "", target = "", detail = "";
                if (rest.StartsWith("["))
                {
                    int end = rest.IndexOf(']');
                    if (end > 0) { op = rest.Substring(1, end - 1); rest = rest.Substring(end + 1).Trim(); }
                }
                result = rest.StartsWith("✓") ? "✓" : rest.StartsWith("✗") ? "✗" : "";
                if (result.Length > 0) rest = rest.Substring(1).Trim();
                var parts = rest.Split('|');
                action = parts.Length > 0 ? parts[0].Trim() : "";
                target = parts.Length > 1 ? parts[1].Trim() : "";
                detail = parts.Length > 2 ? parts[2].Trim() : "";
                return new[] { time, op, result, action, target, detail };
            }
            catch { return new[] { "", "", "", line, "", "" }; }
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
