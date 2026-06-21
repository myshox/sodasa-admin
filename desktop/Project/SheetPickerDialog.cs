using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    /// <summary>
    /// Excel 多分頁選擇器：
    ///   - 點某分頁 → SelectedSheet = "分頁名"
    ///   - 點「全部合併」 → SelectedSheet = "*"
    ///   - 取消 → DialogResult.Cancel
    /// </summary>
    public sealed class SheetPickerDialog : Form
    {
        public string SelectedSheet { get; private set; }

        public SheetPickerDialog(string[] sheetNames)
        {
            Text            = "選擇 Excel 分頁";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterParent;
            MinimizeBox     = false;
            MaximizeBox     = false;
            ClientSize      = new Size(380, 60 + Math.Min(sheetNames.Length, 8) * 30 + 60);
            BackColor       = Theme.BgCard;
            ForeColor       = Theme.TextPrimary;
            Font            = Theme.FontBody;

            var lbl = new Label
            {
                Text     = $"檔案內有 {sheetNames.Length} 個分頁，請選擇要讀哪個：",
                Location = new Point(12, 10),
                Size     = new Size(360, 22),
                ForeColor= Theme.TextSecondary,
            };
            Controls.Add(lbl);

            var lb = new ListBox
            {
                Location      = new Point(12, 36),
                Size          = new Size(356, Math.Min(sheetNames.Length, 8) * 22 + 4),
                IntegralHeight= false,
                BorderStyle   = BorderStyle.FixedSingle,
                BackColor     = Theme.BgInput,
                ForeColor     = Theme.TextPrimary,
            };
            foreach (var s in sheetNames) lb.Items.Add(s);
            if (lb.Items.Count > 0) lb.SelectedIndex = 0;
            lb.DoubleClick += (s, e) => { if (lb.SelectedItem != null) { SelectedSheet = lb.SelectedItem.ToString(); DialogResult = DialogResult.OK; Close(); } };
            Controls.Add(lb);

            int by = 36 + lb.Height + 12;

            var btnOk = Theme.MakeButton("讀取此分頁", Theme.AccentBlue, Color.White, 110, 28);
            btnOk.Location = new Point(12, by);
            btnOk.Click += (s, e) =>
            {
                if (lb.SelectedItem == null) return;
                SelectedSheet = lb.SelectedItem.ToString();
                DialogResult  = DialogResult.OK;
                Close();
            };
            Controls.Add(btnOk);

            var btnAll = Theme.MakeButton("全部合併", Theme.AccentGreen, Color.White, 110, 28);
            btnAll.Location = new Point(132, by);
            btnAll.Click += (s, e) => { SelectedSheet = "*"; DialogResult = DialogResult.OK; Close(); };
            Controls.Add(btnAll);

            var btnCancel = Theme.MakeButton("取消", Theme.AccentRed, Color.White, 110, 28);
            btnCancel.Location = new Point(252, by);
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            ClientSize = new Size(380, by + 28 + 12);
        }

        /// <summary>
        /// 若 path 是 Excel 且分頁 ≥ 2，跳出選單；否則回傳 null（讓呼叫端用預設）。
        /// 使用者按取消 → 回傳 ""（呼叫端應視為「中止」）
        /// </summary>
        public static string AskIfMultiSheet(IWin32Window owner, string path)
        {
            string[] sheets;
            try { sheets = ItemListImporter.GetXlsxSheetNames(path); }
            catch { return null; }
            if (sheets == null || sheets.Length < 2) return null;

            using var dlg = new SheetPickerDialog(sheets);
            return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.SelectedSheet : "";
        }
    }
}
