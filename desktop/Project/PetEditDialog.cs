using System;
using System.Drawing;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    // ══════════════════════════════════════════════════════════════════
    //  寵物四維編輯對話框
    // ══════════════════════════════════════════════════════════════════
    internal class PetEditDialog : Form
    {
        // ── 欄位 ──────────────────────────────────────────────────────
        private readonly PetInfo _original;

        private TextBox         _txtName  = null!;
        private NumericUpDown   _nudLv    = null!;
        private NumericUpDown   _nudHp    = null!;
        private NumericUpDown   _nudAtk   = null!;
        private NumericUpDown   _nudDef   = null!;
        private NumericUpDown   _nudSpd   = null!;
        private ComboBox        _cmbCheck = null!;
        private Label           _lblSum   = null!;

        /// <summary>儲存後由此讀取修改結果。</summary>
        public PetInfo? Result { get; private set; }

        // ── 建構子 ────────────────────────────────────────────────────
        public PetEditDialog(PetInfo pet)
        {
            _original     = pet;
            Text          = $"✏️ 編輯寵物 — {pet.Name}  (ID: {pet.Id})";
            Size          = new Size(420, 460);
            MinimumSize   = new Size(380, 420);
            MaximizeBox   = false;
            BackColor     = Theme.BgPage;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;

            BuildUI(pet);
        }

        // ── UI 建構 ───────────────────────────────────────────────────
        private void BuildUI(PetInfo pet)
        {
            // ── 標題列 ──────────────────────────────────────────────
            var hdr = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = Theme.BgCard };
            hdr.Controls.Add(new Label
            {
                Text      = $"🐾  {pet.Name}  ▸  {pet.Type}  ▸  Lv.{pet.Lv}",
                ForeColor = Color.FromArgb(150, 240, 170),
                Font      = Theme.FontBody,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(14, 0, 0, 0)
            });

            // ── 主體表格 ─────────────────────────────────────────────
            var body = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 9,
                Padding     = new Padding(18, 12, 18, 6),
                BackColor   = Theme.BgPage
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90f));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            for (int i = 0; i < 9; i++)
                body.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));

            Label MkLbl(string t) => new Label
            {
                Text      = t,
                ForeColor = Theme.TextSecondary,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Font      = Theme.FontBody
            };

            NumericUpDown MkNud(int val, int max)
            {
                var n = new NumericUpDown
                {
                    Minimum      = 0,
                    Maximum      = max,
                    Value        = Math.Max(0, Math.Min(val, max)),
                    Dock         = DockStyle.Fill,
                    BackColor    = Theme.BgLight,
                    ForeColor    = Theme.TextPrimary,
                    Font         = Theme.FontBody,
                    ThousandsSeparator = true
                };
                n.ValueChanged += (_, __) => UpdateSum();
                return n;
            }

            // 名稱
            _txtName = new TextBox
            {
                Text      = pet.Name,
                Dock      = DockStyle.Fill,
                BackColor = Theme.BgLight,
                ForeColor = Theme.TextPrimary,
                Font      = Theme.FontBody,
                MaxLength = 24
            };

            // NUD
            _nudLv  = MkNud(pet.Lv,     200);
            _nudHp  = MkNud(pet.Hp,  999999);
            _nudAtk = MkNud(pet.Attack, 99999);
            _nudDef = MkNud(pet.Def,   99999);
            _nudSpd = MkNud(pet.Quick, 99999);

            // 狀態
            _cmbCheck = new ComboBox
            {
                Dock          = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor     = Theme.BgLight,
                ForeColor     = Theme.TextPrimary,
                Font          = Theme.FontBody
            };
            _cmbCheck.Items.AddRange(new object[] { "0 — 揹包中", "1 — 出戰中" });
            _cmbCheck.SelectedIndex = pet.Check == 1 ? 1 : 0;

            // 綜合戰力（唯讀）
            _lblSum = new Label
            {
                Dock      = DockStyle.Fill,
                ForeColor = Color.FromArgb(255, 215, 60),
                Font      = new Font(Theme.FontBody.FontFamily, 11f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            UpdateSum();

            int row = 0;
            void AddRow(string label, Control ctrl)
            {
                body.Controls.Add(MkLbl(label), 0, row);
                body.Controls.Add(ctrl, 1, row);
                row++;
            }

            AddRow("名稱",     _txtName);
            AddRow("等級",     _nudLv);
            AddRow("HP",       _nudHp);
            AddRow("攻擊",     _nudAtk);
            AddRow("防禦",     _nudDef);
            AddRow("速度",     _nudSpd);
            AddRow("狀態",     _cmbCheck);
            AddRow("綜合戰力", _lblSum);

            // ── 提示行 ───────────────────────────────────────────────
            var lblHint = new Label
            {
                Text      = "⚠  修改後需玩家重新登入才能看到變化",
                ForeColor = Color.FromArgb(255, 180, 60),
                Font      = Theme.FontSmall,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            body.Controls.Add(new Label(), 0, 8);   // left blank
            body.Controls.Add(lblHint, 1, 8);

            // ── 頁尾按鈕 ─────────────────────────────────────────────
            var foot = new Panel { Dock = DockStyle.Bottom, Height = 50, BackColor = Theme.BgCard };

            var btnSave   = Theme.MakeButton("💾 儲存", Color.FromArgb(20, 90, 40), Color.FromArgb(120, 220, 120), 90, 32);
            var btnCancel = Theme.MakeButton("取消",    Theme.BgLight,              Theme.TextSecondary,           72, 32);

            btnSave.Location   = new Point(foot.Width - 176, 9);
            btnCancel.Location = new Point(foot.Width - 80,  9);
            btnSave.Anchor     = btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            btnSave.Click += (_, __) =>
            {
                if (string.IsNullOrWhiteSpace(_txtName.Text))
                {
                    MessageBox.Show("寵物名稱不可為空！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                double newSum = (double)_nudHp.Value * 0.5 +
                                ((double)_nudAtk.Value + (double)_nudDef.Value + (double)_nudSpd.Value) * 0.5;
                Result = new PetInfo
                {
                    Unicode = _original.Unicode,
                    Id      = _original.Id,
                    Name    = _txtName.Text.Trim(),
                    Type    = _original.Type,
                    Lv      = (int)_nudLv.Value,
                    Hp      = (int)_nudHp.Value,
                    Attack  = (int)_nudAtk.Value,
                    Def     = (int)_nudDef.Value,
                    Quick   = (int)_nudSpd.Value,
                    Sum     = newSum,
                    Author  = _original.Author,
                    Cdkey   = _original.Cdkey,
                    Check   = _cmbCheck.SelectedIndex == 1 ? 1 : 0
                };
                DialogResult = DialogResult.OK;
                Close();
            };

            btnCancel.Click += (_, __) => { DialogResult = DialogResult.Cancel; Close(); };

            foot.Controls.Add(btnSave);
            foot.Controls.Add(btnCancel);

            Controls.Add(body);
            Controls.Add(foot);
            Controls.Add(hdr);
        }

        // ── 戰力即時計算 ──────────────────────────────────────────────
        private void UpdateSum()
        {
            if (_nudHp == null) return;
            double s = (double)_nudHp.Value  * 0.5 +
                       ((double)_nudAtk.Value + (double)_nudDef.Value + (double)_nudSpd.Value) * 0.5;
            _lblSum.Text = $"{s:N2}";
        }
    }
}
