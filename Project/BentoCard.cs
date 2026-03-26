using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    // ══════════════════════════════════════════════════════════════════
    // BentoCard — 圓角卡片基礎元件
    // ══════════════════════════════════════════════════════════════════
    public class BentoCard : Panel
    {
        private int   _radius = 16;
        private Color _cardBg = Color.FromArgb(16, 20, 36);

        public int   CornerRadius { get => _radius; set { _radius = value; Invalidate(); RebuildRegion(); } }
        public Color CardColor    { get => _cardBg; set { _cardBg = value; Invalidate(); } }

        public BentoCard()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            Padding   = new Padding(16);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode      = SmoothingMode.AntiAlias;
            g.CompositingQuality = CompositingQuality.HighQuality;
            using var path  = MakePath(new Rectangle(0, 0, Width - 1, Height - 1), _radius);
            using var brush = new SolidBrush(_cardBg);
            g.FillPath(brush, path);
            base.OnPaint(e);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { /* 不繪製背景，避免白色閃爍 */ }

        protected override void OnResize(EventArgs e)     { base.OnResize(e); RebuildRegion(); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); RebuildRegion(); }

        private void RebuildRegion()
        {
            if (Width > 0 && Height > 0)
            {
                using var path = MakePath(new Rectangle(0, 0, Width, Height), _radius);
                Region = new Region(path);
            }
        }

        public static GraphicsPath MakePath(Rectangle b, int r)
        {
            r = Math.Max(1, Math.Min(r, Math.Min(b.Width / 2, b.Height / 2)));
            var p = new GraphicsPath();
            p.AddArc(b.X,             b.Y,              r * 2, r * 2, 180, 90);
            p.AddArc(b.Right - r * 2, b.Y,              r * 2, r * 2, 270, 90);
            p.AddArc(b.Right - r * 2, b.Bottom - r * 2, r * 2, r * 2,   0, 90);
            p.AddArc(b.X,             b.Bottom - r * 2, r * 2, r * 2,  90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // BentoStatCard — 統計數字卡片（icon + 數字 + 標籤）
    // ══════════════════════════════════════════════════════════════════
    public class BentoStatCard : BentoCard
    {
        private readonly Label _lblValue;
        private readonly Label _lblTitle;
        private readonly Label _lblSub;
        private readonly Panel _accentBar;
        private readonly Color _accent;

        public string Value    { get => _lblValue.Text; set => _lblValue.Text = value; }
        public string SubLabel { get => _lblSub.Text;   set => _lblSub.Text   = value; }

        public BentoStatCard(string icon, string title, Color accent, Color cardBg)
        {
            _accent   = accent;
            CardColor = cardBg;
            Padding   = new Padding(0);

            // 頂部 accent 條（圓角卡片內嵌）
            _accentBar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 5,
                BackColor = accent
            };
            Controls.Add(_accentBar);

            // 內容 TableLayoutPanel
            var tbl = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                RowCount    = 4,
                ColumnCount = 1,
                BackColor   = Color.Transparent,
                Padding     = new Padding(18, 14, 18, 14)
            };
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));  // icon
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // value
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 18f));  // title
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 16f));  // sub
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            var lblIcon = new Label
            {
                Text      = icon,
                ForeColor = accent,
                Font      = new Font("Segoe UI Emoji", 13f),
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _lblValue = new Label
            {
                Text      = "—",
                ForeColor = Color.FromArgb(235, 240, 255),
                Font      = new Font(Theme.FontFamily, 24f, FontStyle.Bold),
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            var lblTitle = new Label
            {
                Text      = title,
                ForeColor = Color.FromArgb(100, 118, 160),
                Font      = new Font(Theme.FontFamily, 10f),
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft,
                BackColor = Color.Transparent
            };
            _lblTitle = lblTitle;

            _lblSub = new Label
            {
                Text      = "",
                ForeColor = accent,
                Font      = new Font(Theme.FontFamily, 9f),
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            tbl.Controls.Add(lblIcon,   0, 0);
            tbl.Controls.Add(_lblValue, 0, 1);
            tbl.Controls.Add(lblTitle,  0, 2);
            tbl.Controls.Add(_lblSub,   0, 3);
            Controls.Add(tbl);
        }

        // 滑鼠 hover 效果
        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            CardColor = Color.FromArgb(
                Math.Min(255, _cardBg.R + 6),
                Math.Min(255, _cardBg.G + 6),
                Math.Min(255, _cardBg.B + 8));
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            CardColor = _cardBgOrigin;
        }

        private Color _cardBgOrigin;

        public new Color CardColor
        {
            get => base.CardColor;
            set { base.CardColor = value; _cardBgOrigin = value; }
        }

        private Color _cardBg => base.CardColor;
    }
}
