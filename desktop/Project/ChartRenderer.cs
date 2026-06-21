using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    // ═══════════════════════════════════════════════════════════════
    // ChartRenderer — 純 GDI+ 圖表繪製工具（無需額外套件）
    // ═══════════════════════════════════════════════════════════════
    public static class ChartRenderer
    {
        // ── 共用色板 ──────────────────────────────────────────────
        public static readonly Color[] Palette = {
            Color.FromArgb(64,  158, 255),  // 0 藍
            Color.FromArgb(103, 194, 58),   // 1 綠
            Color.FromArgb(230, 162, 60),   // 2 橙
            Color.FromArgb(245, 108, 108),  // 3 紅
            Color.FromArgb(150, 100, 240),  // 4 紫
            Color.FromArgb(83,  223, 196),  // 5 青
            Color.FromArgb(255, 206, 86),   // 6 黃
            Color.FromArgb(255, 99,  132),  // 7 粉紅
        };

        private static readonly Color GridColor   = Color.FromArgb(60,  65,  80);
        private static readonly Color AxisColor   = Color.FromArgb(100, 106, 125);
        private static readonly Color LabelColor  = Color.FromArgb(160, 165, 185);
        private static readonly Font  SmFont      = new Font("Microsoft JhengHei UI", 8f);
        private static readonly Font  TitleFont   = new Font("Microsoft JhengHei UI", 10f, FontStyle.Bold);
        private static readonly Font  ValFont     = new Font("Microsoft JhengHei UI", 7.5f);

        // ════════════════════════════════════════════════════════
        // 1. 垂直長條圖
        // ════════════════════════════════════════════════════════
        public static void DrawBarChart(
            Graphics g, Rectangle bounds,
            double[]  values,
            string[]  xLabels,
            Color     barColor,
            string    title      = "",
            string    yUnit      = "",
            bool      showValue  = true,
            double    forcedMax  = 0)
        {
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            if (values == null || values.Length == 0) { DrawEmpty(g, bounds, "無資料"); return; }

            const int padL = 56, padR = 16, padT = 36, padB = 44;
            Rectangle plot = new Rectangle(bounds.X + padL, bounds.Y + padT,
                                           bounds.Width - padL - padR,
                                           bounds.Height - padT - padB);

            double max = forcedMax > 0 ? forcedMax : (values.Max() * 1.15);
            if (max == 0) max = 1;

            // 標題
            if (!string.IsNullOrEmpty(title))
                g.DrawString(title, TitleFont, new SolidBrush(Color.FromArgb(200, 210, 230)),
                    bounds.X + padL, bounds.Y + 8);

            // 橫格線 & Y 軸標籤
            int gridLines = 5;
            using var gridPen   = new Pen(GridColor);
            using var labelBrush = new SolidBrush(LabelColor);
            using var whiteBrush = new SolidBrush(Color.White);
            using var sfNear    = new StringFormat { Alignment = StringAlignment.Near };
            using var sfCenter  = new StringFormat { Alignment = StringAlignment.Center };

            for (int i = 0; i <= gridLines; i++)
            {
                double v = max / gridLines * i;
                int    y = plot.Bottom - (int)(plot.Height * i / gridLines);
                g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
                string lbl = v >= 10000 ? $"{v / 10000:0.#}萬" : v >= 1000 ? $"{v / 1000:0.#}k" : $"{v:0}";
                if (!string.IsNullOrEmpty(yUnit)) lbl += yUnit;
                g.DrawString(lbl, SmFont, labelBrush, bounds.X + 2, y - 8, sfNear);
            }

            // 長條
            int n      = values.Length;
            float gap  = Math.Max(1, plot.Width * 0.08f / n);
            float barW = (plot.Width - gap * (n + 1)) / n;
            barW = Math.Max(barW, 2);

            using var topLinePen = new Pen(Color.FromArgb(240, barColor), 1.5f);
            for (int i = 0; i < n; i++)
            {
                float x = plot.Left + gap * (i + 1) + barW * i;
                float h = (float)(plot.Height * values[i] / max);
                float y = plot.Bottom - h;

                // 漸層長條（h<=1 時跳過避免 GDI+ OutOfMemoryException）
                if (h > 1)
                {
                    using var brush = new LinearGradientBrush(
                        new PointF(x, y), new PointF(x, plot.Bottom + 1),
                        Color.FromArgb(210, barColor), Color.FromArgb(140, barColor));
                    g.FillRectangle(brush, x, y, barW, h);

                    // 頂邊高亮線
                    g.DrawLine(topLinePen, x, y, x + barW, y);
                }

                // 數值標籤
                if (showValue && h > 14)
                {
                    string val = values[i] >= 10000 ? $"{values[i] / 10000:0.#}萬"
                               : values[i] >= 1000  ? $"{values[i] / 1000:0.#}k"
                               : $"{values[i]:0}";
                    g.DrawString(val, ValFont, whiteBrush, x + barW / 2, y - 14, sfCenter);
                }

                // X 軸標籤
                if (xLabels != null && i < xLabels.Length)
                    g.DrawString(xLabels[i], SmFont, labelBrush, x + barW / 2, plot.Bottom + 4, sfCenter);
            }

            // X / Y 軸線
            using var axisPen = new Pen(AxisColor, 1);
            g.DrawLine(axisPen, plot.Left, plot.Top, plot.Left, plot.Bottom);
            g.DrawLine(axisPen, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
        }

        // ════════════════════════════════════════════════════════
        // 2. 折線/面積圖（支援多條線）
        // ════════════════════════════════════════════════════════
        public class LineSeries
        {
            public string   Label  { get; set; }
            public double[] Values { get; set; }
            public Color    Color  { get; set; }
            public bool     FillArea { get; set; } = false;
        }

        public static void DrawLineChart(
            Graphics      g,
            Rectangle     bounds,
            LineSeries[]  series,
            string[]      xLabels,
            string        title   = "",
            string        yUnit   = "",
            double        forcedMax = 0)
        {
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            if (series == null || series.Length == 0 || series[0].Values.Length == 0)
            { DrawEmpty(g, bounds, "無資料"); return; }

            const int padL = 60, padR = 20, padT = 40, padB = 50;
            Rectangle plot = new Rectangle(bounds.X + padL, bounds.Y + padT,
                                           bounds.Width - padL - padR,
                                           bounds.Height - padT - padB);

            double max = forcedMax > 0 ? forcedMax
                       : series.SelectMany(s => s.Values).DefaultIfEmpty(0).Max() * 1.15;
            if (max == 0) max = 1;

            // 標題
            using var titleBrush2 = new SolidBrush(Color.FromArgb(200, 210, 230));
            if (!string.IsNullOrEmpty(title))
                g.DrawString(title, TitleFont, titleBrush2, bounds.X + padL, bounds.Y + 8);

            // 格線
            using var gridPen2    = new Pen(GridColor);
            using var labelBrush2 = new SolidBrush(LabelColor);
            using var sfNear2     = new StringFormat { Alignment = StringAlignment.Near };
            using var sfCenter2   = new StringFormat { Alignment = StringAlignment.Center };
            using var axisPen2    = new Pen(AxisColor);

            for (int i = 0; i <= 5; i++)
            {
                double v = max / 5 * i;
                int    y = plot.Bottom - (int)(plot.Height * i / 5);
                g.DrawLine(gridPen2, plot.Left, y, plot.Right, y);
                string lbl = v >= 10000 ? $"{v / 10000:0.#}萬"
                           : v >= 1000  ? $"{v / 1000:0.#}k"
                           : $"{v:0}";
                if (!string.IsNullOrEmpty(yUnit)) lbl += yUnit;
                g.DrawString(lbl, SmFont, labelBrush2, bounds.X + 2, y - 8, sfNear2);
            }

            // 每條線
            foreach (var s in series)
            {
                if (s.Values == null || s.Values.Length < 2) continue;
                int n = s.Values.Length;

                PointF Pt(int i) => new PointF(
                    plot.Left  + (float)plot.Width  * i / (n - 1),
                    plot.Bottom - (float)(plot.Height * s.Values[i] / max));

                var pts = Enumerable.Range(0, n).Select(i => Pt(i)).ToArray();

                // 填充區域
                if (s.FillArea)
                {
                    var fillPts = pts.Concat(new[] {
                        new PointF(pts[^1].X, plot.Bottom),
                        new PointF(pts[0].X,  plot.Bottom)
                    }).ToArray();
                    using var fillBrush = new SolidBrush(Color.FromArgb(30, s.Color));
                    g.FillPolygon(fillBrush, fillPts);
                }

                // 折線
                using var pen = new Pen(s.Color, 2.2f) { LineJoin = LineJoin.Round };
                g.DrawLines(pen, pts);

                // 資料點
                using var dotBrush  = new SolidBrush(s.Color);
                using var dotBrush2 = new SolidBrush(Color.FromArgb(30, 30, 40));
                foreach (var pt in pts)
                {
                    g.FillEllipse(dotBrush,  pt.X - 3,   pt.Y - 3,   6, 6);
                    g.FillEllipse(dotBrush2, pt.X - 1.5f, pt.Y - 1.5f, 3, 3);
                }
            }

            // X 標籤（每隔 N 個顯示）
            if (xLabels != null)
            {
                int n    = xLabels.Length;
                int step = Math.Max(1, n / 10);
                for (int i = 0; i < n; i += step)
                {
                    float x = plot.Left + (float)plot.Width * i / Math.Max(n - 1, 1);
                    g.DrawString(xLabels[i], SmFont, labelBrush2, x, plot.Bottom + 4, sfCenter2);
                }
            }

            // 軸線
            g.DrawLine(axisPen2, plot.Left, plot.Top,    plot.Left,  plot.Bottom);
            g.DrawLine(axisPen2, plot.Left, plot.Bottom, plot.Right, plot.Bottom);

            // 圖例
            if (series.Length > 1)
            {
                int lx = bounds.X + padL + 8, ly = bounds.Y + padT + 4;
                foreach (var s in series)
                {
                    using var legendBrush = new SolidBrush(s.Color);
                    g.FillRectangle(legendBrush, lx, ly + 2, 16, 8);
                    g.DrawString(s.Label, SmFont, labelBrush2, lx + 20, ly);
                    lx += (int)g.MeasureString(s.Label, SmFont).Width + 44;
                }
            }
        }

        // ════════════════════════════════════════════════════════
        // 3. 圓餅圖 / 甜甜圈圖
        // ════════════════════════════════════════════════════════
        public static void DrawPieChart(
            Graphics  g,
            Rectangle bounds,
            double[]  values,
            string[]  labels,
            Color[]   colors     = null,
            string    title      = "",
            bool      donut      = true)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (values == null || values.Length == 0 || values.Sum() == 0)
            { DrawEmpty(g, bounds, "無資料"); return; }

            using var titleBrush3 = new SolidBrush(Color.FromArgb(200, 210, 230));
            if (!string.IsNullOrEmpty(title))
                g.DrawString(title, TitleFont, titleBrush3, bounds.X + 8, bounds.Y + 8);

            if (colors == null) colors = Palette;

            // 圓餅區域（左側）
            int   size   = Math.Min(bounds.Width * 6 / 10, bounds.Height - 60);
            float cx     = bounds.X + size / 2f + 20;
            float cy     = bounds.Y + bounds.Height / 2f + 10;
            var   pieRect = new RectangleF(cx - size / 2f, cy - size / 2f, size, size);

            double total = values.Sum();
            float  start = -90f;
            using var separatorPen = new Pen(Color.FromArgb(30, 30, 40), 1.5f);
            for (int i = 0; i < values.Length; i++)
            {
                float sweep = (float)(values[i] / total * 360);
                if (sweep < 0.1f) { start += sweep; continue; }
                using var pieBrush = new SolidBrush(colors[i % colors.Length]);
                g.FillPie(pieBrush,     pieRect.X, pieRect.Y, pieRect.Width, pieRect.Height, start, sweep);
                g.DrawPie(separatorPen, pieRect.X, pieRect.Y, pieRect.Width, pieRect.Height, start, sweep);
                start += sweep;
            }

            // 甜甜圈中心
            if (donut)
            {
                float di = size * 0.45f;
                using var donutBrush = new SolidBrush(Color.FromArgb(50, 52, 65));
                using var whiteBrush3 = new SolidBrush(Color.White);
                g.FillEllipse(donutBrush, cx - di / 2f, cy - di / 2f, di, di);
                using var sfCenter3 = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString($"總計\n{FormatVal(total)}", TitleFont, whiteBrush3,
                    new RectangleF(cx - di / 2f, cy - di / 2f, di, di), sfCenter3);
            }

            // 右側圖例
            float lx = bounds.X + size + 40;
            float ly = bounds.Y + 50;
            using var labelBrush3 = new SolidBrush(LabelColor);
            for (int i = 0; i < values.Length && i < labels.Length; i++)
            {
                using var legendBrush3 = new SolidBrush(colors[i % colors.Length]);
                g.FillRectangle(legendBrush3, lx, ly + 2, 14, 14);
                double pct = values[i] / total * 100;
                g.DrawString($"{labels[i]}  {FormatVal(values[i])}  ({pct:0.#}%)",
                    SmFont, labelBrush3, lx + 20, ly);
                ly += 26;
            }
        }

        // ════════════════════════════════════════════════════════
        // 4. 24 小時熱力條（單行 heatmap）
        // ════════════════════════════════════════════════════════
        public static void DrawHourHeatBar(
            Graphics  g,
            Rectangle bounds,
            int[]     hourCounts,   // 長度 24
            string    title = "")
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (hourCounts == null || hourCounts.Length < 24) { DrawEmpty(g, bounds, "無資料"); return; }

            using var titleBrush4 = new SolidBrush(Color.FromArgb(200, 210, 230));
            if (!string.IsNullOrEmpty(title))
                g.DrawString(title, TitleFont, titleBrush4, bounds.X + 8, bounds.Y + 8);

            int max4 = hourCounts.Max();
            if (max4 == 0) max4 = 1;

            int padL4 = 10, padT4 = 36, padB4 = 28;
            float cellW = (bounds.Width - padL4 * 2) / 24f;
            float cellH = bounds.Height - padT4 - padB4 - 8;

            using var sfHeat   = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var sfHour   = new StringFormat { Alignment = StringAlignment.Center };
            using var labelBrush4 = new SolidBrush(LabelColor);

            // 熱力格 + 小時標籤
            for (int h = 0; h < 24; h++)
            {
                float x = bounds.X + padL4 + h * cellW;
                float intensity = (float)hourCounts[h] / max4;

                Color heat;
                if (intensity < 0.5f)
                    heat = InterpolateColor(Color.FromArgb(26, 58, 107), Color.FromArgb(255, 165, 0), intensity * 2);
                else
                    heat = InterpolateColor(Color.FromArgb(255, 165, 0), Color.FromArgb(255, 60, 60), (intensity - 0.5f) * 2);

                using (var heatBrush = new SolidBrush(heat))
                    g.FillRectangle(heatBrush, x + 1, bounds.Y + padT4, cellW - 2, cellH);

                if (cellH > 22)
                {
                    using var valBrush4 = new SolidBrush(intensity > 0.4f ? Color.Black : Color.FromArgb(160, 170, 190));
                    g.DrawString(hourCounts[h] > 0 ? hourCounts[h].ToString() : "",
                        ValFont, valBrush4, new RectangleF(x, bounds.Y + padT4, cellW, cellH), sfHeat);
                }

                g.DrawString(h == 0 ? "00" : h.ToString(),
                    SmFont, labelBrush4, x + cellW / 2, bounds.Y + padT4 + cellH + 4, sfHour);
            }

            // 色條說明
            string[] hints = { "少", "", "多" };
            Color[]  hcs   = { Color.FromArgb(26,58,107), Color.FromArgb(255,165,0), Color.FromArgb(255,60,60) };
            float bx4 = bounds.X + padL4;
            float by4 = bounds.Y + bounds.Height - 14;
            for (int i = 0; i < 3; i++)
            {
                using var hintBrush = new SolidBrush(hcs[i]);
                g.FillRectangle(hintBrush, bx4 + i * 14, by4, 12, 10);
                g.DrawString(hints[i], SmFont, labelBrush4, bx4 + i * 14 + 14, by4 - 2);
            }
        }

        // ════════════════════════════════════════════════════════
        // 5. 水平長條排行圖
        // ════════════════════════════════════════════════════════
        public static void DrawHorizontalBars(
            Graphics  g,
            Rectangle bounds,
            double[]  values,
            string[]  labels,
            Color     barColor,
            string    title  = "",
            string    xUnit  = "")
        {
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            if (values == null || values.Length == 0) { DrawEmpty(g, bounds, "無資料"); return; }

            if (!string.IsNullOrEmpty(title))
                g.DrawString(title, TitleFont, new SolidBrush(Color.FromArgb(200, 210, 230)),
                    bounds.X + 8, bounds.Y + 8);

            const int padL = 140, padR = 80, padT = 36, padB = 12;
            int n    = Math.Min(values.Length, 10);
            double max = values.Take(n).Max();
            if (max == 0) max = 1;

            float rowH = (float)(bounds.Height - padT - padB) / n;

            for (int i = 0; i < n; i++)
            {
                float y  = bounds.Y + padT + i * rowH + 3;
                float bh = Math.Max(rowH - 8, 4);
                float bw = (float)(bounds.Width - padL - padR) * (float)(values[i] / max);

                // 背景槽
                g.FillRectangle(new SolidBrush(Color.FromArgb(40, 42, 55)),
                    bounds.X + padL, y, bounds.Width - padL - padR, bh);
                // 長條
                if (bw > 0)
                {
                    using var br = new LinearGradientBrush(
                        new PointF(bounds.X + padL, y), new PointF(bounds.X + padL + bw, y),
                        Color.FromArgb(180, barColor), barColor);
                    g.FillRectangle(br, bounds.X + padL, y, bw, bh);
                }

                // 左側標籤
                var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                string lbl = labels != null && i < labels.Length ? labels[i] : i.ToString();
                g.DrawString(lbl, SmFont, new SolidBrush(LabelColor),
                    bounds.X + padL - 4, y + bh / 2, sf);

                // 數值
                string val = FormatVal(values[i]);
                if (!string.IsNullOrEmpty(xUnit)) val += " " + xUnit;
                g.DrawString(val, SmFont, new SolidBrush(Color.FromArgb(200, 210, 230)),
                    bounds.X + padL + bw + 6, y + bh / 2 - 8);
            }
        }

        // ════════════════════════════════════════════════════════
        // 輔助
        // ════════════════════════════════════════════════════════
        private static void DrawEmpty(Graphics g, Rectangle b, string msg)
        {
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(msg, TitleFont, new SolidBrush(Color.FromArgb(80, 85, 100)),
                new RectangleF(b.X, b.Y, b.Width, b.Height), sf);
        }

        private static Color InterpolateColor(Color a, Color b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        public static string FormatVal(double v) =>
            v >= 10000000 ? $"{v / 10000000:0.##}千萬" :
            v >= 10000    ? $"{v / 10000:0.##}萬" :
            v >= 1000     ? $"{v / 1000:0.##}k" :
            $"{v:0.##}";

        // ── 建立可繪圖的 Panel（在 Paint 時呼叫 renderer）───────
        public static Panel MakeChartPanel(Color bgColor = default, Action<Graphics, Rectangle> painter = null)
        {
            if (bgColor == default) bgColor = Color.FromArgb(36, 37, 50);
            var p = new Panel { BackColor = bgColor, Dock = DockStyle.Fill };
            if (painter != null)
                p.Paint += (s, e) => painter(e.Graphics, p.ClientRectangle);
            return p;
        }
    }
}
