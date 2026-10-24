// D20 Roller - single-file WinForms app. Build with build.bat (uses the C# compiler built into Windows).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("D20 Roller")]
[assembly: System.Reflection.AssemblyProduct("D20 Roller")]

class DiceForm : Form
{
    // Palette
    static readonly Color Orange      = Color.FromArgb(255, 122, 0);
    static readonly Color OrangeLight = Color.FromArgb(255, 170, 70);
    static readonly Color OrangeDark  = Color.FromArgb(205, 85, 0);
    static readonly Color Ink         = Color.FromArgb(34, 34, 34);
    static readonly Color Muted       = Color.FromArgb(160, 160, 160);
    static readonly Color Hairline    = Color.FromArgb(236, 236, 236);

    const int W = 400, H = 580;
    const float R = 118f;                 // die radius
    static readonly PointF DieCenter = new PointF(W / 2f, 238f);

    readonly RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
    readonly Timer timer = new Timer();
    readonly List<int> history = new List<int>();
    readonly float dpi;

    int value = 20, finalValue, tick;
    bool rolling, hasRolled, hover, pressed;
    float angle, scale = 1f;
    string status = "Ready when you are.";
    Color statusColor = Muted;
    readonly RectangleF btn = new RectangleF(110, 420, 180, 54);

    public DiceForm()
    {
        Text = "D20 Roller";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.White;
        DoubleBuffered = true;
        KeyPreview = true;

        // Scale for DPI, but never taller than the screen (minus room for the title bar)
        Rectangle work = Screen.FromPoint(Cursor.Position).WorkingArea;
        using (Graphics g = CreateGraphics()) dpi = g.DpiX / 96f;
        float maxScale = (work.Height - 60f * dpi) / H;
        if (dpi > maxScale) dpi = maxScale;
        ClientSize = new Size((int)(W * dpi), (int)(H * dpi));

        // Center after sizing (CenterScreen uses the size from before ClientSize was set)
        Location = new Point(work.X + (work.Width - Width) / 2, work.Y + Math.Max(0, (work.Height - Height) / 2));
        Icon = MakeIcon();

        timer.Interval = 28;
        timer.Tick += OnTick;
    }

    // ---------- Rolling ----------

    int RollD20()
    {
        // Unbiased 1..20 using rejection sampling
        byte[] b = new byte[1];
        do { rng.GetBytes(b); } while (b[0] >= 240);
        return b[0] % 20 + 1;
    }

    void Roll()
    {
        if (rolling) return;
        rolling = true;
        tick = 0;
        finalValue = RollD20();
        status = "Rolling...";
        statusColor = Muted;
        timer.Start();
    }

    void OnTick(object sender, EventArgs e)
    {
        const int total = 26;
        tick++;
        float t = tick / (float)total;
        float ease = 1f - (float)Math.Pow(1f - t, 3);

        angle = (1f - ease) * 360f * 1.5f;
        scale = 1f - 0.12f * (float)Math.Sin(t * Math.PI);

        // Number flickers fast at first, then slows down
        int every = tick < 12 ? 1 : tick < 20 ? 2 : 3;
        if (tick % every == 0)
        {
            int n;
            do { n = RollD20(); } while (n == value);
            value = n;
        }

        if (tick >= total)
        {
            timer.Stop();
            rolling = false;
            hasRolled = true;
            angle = 0; scale = 1f;
            value = finalValue;
            history.Insert(0, value);
            if (history.Count > 8) history.RemoveAt(8);

            if (value == 20)      { status = "NATURAL 20!"; statusColor = Orange; }
            else if (value == 1)  { status = "Critical fail."; statusColor = Ink; }
            else if (value >= 15) { status = "Nice roll."; statusColor = OrangeDark; }
            else                  { status = "You rolled " + value + "."; statusColor = Muted; }
        }
        Invalidate();
    }

    // ---------- Input ----------

    PointF Logical(Point p) { return new PointF(p.X / dpi, p.Y / dpi); }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        bool h = btn.Contains(Logical(e.Location));
        if (h != hover) { hover = h; Cursor = h ? Cursors.Hand : Cursors.Default; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e) { hover = pressed = false; Invalidate(); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        PointF p = Logical(e.Location);
        if (btn.Contains(p)) { pressed = true; Invalidate(); }
        else if (Dist(p, DieCenter) < R) Roll();   // clicking the die works too
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (pressed && btn.Contains(Logical(e.Location))) Roll();
        pressed = false;
        Invalidate();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys key)
    {
        if (key == Keys.Space || key == Keys.Enter) { Roll(); return true; }
        if (key == Keys.Escape) { Close(); return true; }
        return base.ProcessCmdKey(ref msg, key);
    }

    // ---------- Painting ----------

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.ScaleTransform(dpi, dpi);

        DrawBackground(g);
        DrawHeader(g);
        DrawDie(g);
        DrawStatus(g);
        DrawButton(g);
        DrawHistory(g);
        DrawFooter(g);
    }

    void DrawBackground(Graphics g)
    {
        using (var bg = new LinearGradientBrush(new Rectangle(0, 0, W, H), Color.White, Color.FromArgb(255, 246, 236), 90f))
            g.FillRectangle(bg, 0, 0, W, H);

        // Soft glow behind the die
        float gr = R * 1.55f;
        using (var path = new GraphicsPath())
        {
            path.AddEllipse(DieCenter.X - gr, DieCenter.Y - gr, gr * 2, gr * 2);
            using (var pb = new PathGradientBrush(path))
            {
                int a = (hasRolled && value == 20 && !rolling) ? 120 : 55;
                pb.CenterColor = Color.FromArgb(a, OrangeLight);
                pb.SurroundColors = new[] { Color.FromArgb(0, OrangeLight) };
                g.FillPath(pb, path);
            }
        }
    }

    void DrawHeader(Graphics g)
    {
        // Glossy top strip
        using (var b = new LinearGradientBrush(new RectangleF(0, 0, W, 8), OrangeLight, OrangeDark, 90f))
            g.FillRectangle(b, 0, 0, W, 8);
        using (var b = new SolidBrush(Color.FromArgb(90, Color.White)))
            g.FillRectangle(b, 0, 0, W, 3);

        using (var f = new Font("Segoe UI", 20f, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var b = new SolidBrush(Ink))
            g.DrawString("d20", f, b, 26, 26);
        using (var f = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Pixel))
        using (var b = new SolidBrush(Muted))
            g.DrawString("R O L L E R", f, b, 72, 35);

        // Little range pill on the right
        RectangleF pill = new RectangleF(W - 96, 30, 70, 20);
        using (var p = RoundRect(pill, 10))
        using (var b = new SolidBrush(Color.FromArgb(255, 238, 222)))
            g.FillPath(b, p);
        using (var b = new SolidBrush(Orange))
            g.FillEllipse(b, pill.X + 9, pill.Y + 7, 6, 6);
        using (var f = new Font("Segoe UI", 10.5f, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var b = new SolidBrush(OrangeDark))
            g.DrawString("1 - 20", f, b, pill.X + 19, pill.Y + 3);

        using (var p = new Pen(Hairline))
            g.DrawLine(p, 26, 72, W - 26, 72);
    }

    void DrawDie(Graphics g)
    {
        GraphicsState state = g.Save();
        g.TranslateTransform(DieCenter.X, DieCenter.Y);

        // Drop shadow (stays flat on the "table")
        using (var path = new GraphicsPath())
        {
            path.AddEllipse(-R * 0.8f, R * 0.92f, R * 1.6f, R * 0.26f);
            using (var pb = new PathGradientBrush(path))
            {
                pb.CenterColor = Color.FromArgb(rolling ? 30 : 55, 120, 60, 0);
                pb.SurroundColors = new[] { Color.FromArgb(0, 120, 60, 0) };
                g.FillPath(pb, path);
            }
        }

        g.RotateTransform(angle);
        g.ScaleTransform(scale, scale);

        PointF[] h = new PointF[6];
        for (int i = 0; i < 6; i++) h[i] = Polar(R, -90 + 60 * i);
        float r = R * 0.56f;
        PointF t0 = Polar(r, -90), t1 = Polar(r, 30), t2 = Polar(r, 150);

        bool crit = hasRolled && !rolling && value == 1;
        Color baseC = crit ? Color.FromArgb(150, 150, 150) : Orange;

        // Faces, shaded as if lit from the upper left
        Face(g, baseC, 0.30f, h[5], h[0], t0);
        Face(g, baseC, 0.12f, h[0], h[1], t0);
        Face(g, baseC, 0.18f, h[5], t0, t2);
        Face(g, baseC, -0.05f, h[1], t1, t0);
        Face(g, baseC, 0.05f, h[4], h[5], t2);
        Face(g, baseC, -0.18f, h[1], h[2], t1);
        Face(g, baseC, -0.10f, h[4], t2, h[3]);
        Face(g, baseC, -0.28f, h[2], h[3], t1);
        Face(g, baseC, -0.16f, t2, t1, h[3]);

        // Center face gets a gradient
        using (var lg = new LinearGradientBrush(new PointF(0, -r), new PointF(0, r * 0.5f),
                   Shade(baseC, 0.28f), Shade(baseC, 0.02f)))
            g.FillPolygon(lg, new[] { t0, t1, t2 });

        // Facet lines
        using (var p = new Pen(Color.FromArgb(150, Color.White), 1.6f))
        {
            p.LineJoin = LineJoin.Round;
            g.DrawPolygon(p, new[] { t0, t1, t2 });
            g.DrawLine(p, t0, h[0]); g.DrawLine(p, t0, h[5]); g.DrawLine(p, t0, h[1]);
            g.DrawLine(p, t1, h[1]); g.DrawLine(p, t1, h[2]); g.DrawLine(p, t1, h[3]);
            g.DrawLine(p, t2, h[5]); g.DrawLine(p, t2, h[4]); g.DrawLine(p, t2, h[3]);
        }

        // Gloss highlight, clipped to the die (the Y2K bit)
        using (var hex = new GraphicsPath())
        {
            hex.AddPolygon(h);
            Region old = g.Clip;
            g.SetClip(hex, CombineMode.Intersect);
            using (var gloss = new GraphicsPath())
            {
                gloss.AddEllipse(-R * 1.25f, -R * 1.55f, R * 2.2f, R * 1.65f);
                using (var gb = new LinearGradientBrush(new PointF(0, -R), new PointF(0, R * 0.1f),
                           Color.FromArgb(95, Color.White), Color.FromArgb(0, Color.White)))
                    g.FillPath(gb, gloss);
            }
            g.Clip = old;

            using (var p = new Pen(crit ? Color.FromArgb(110, 110, 110) : OrangeDark, 2.5f))
            {
                p.LineJoin = LineJoin.Round;
                g.DrawPath(p, hex);
            }
        }

        // Number
        string s = value.ToString();
        using (var f = new Font("Segoe UI", 38f, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            RectangleF box = new RectangleF(-60, -26, 120, 60);
            using (var sh = new SolidBrush(Color.FromArgb(70, 120, 50, 0)))
                g.DrawString(s, f, sh, new RectangleF(box.X + 1.5f, box.Y + 2f, box.Width, box.Height), sf);
            using (var b = new SolidBrush(Color.White))
                g.DrawString(s, f, b, box, sf);
        }

        g.Restore(state);
    }

    void DrawStatus(Graphics g)
    {
        bool big = hasRolled && !rolling && (value == 20 || value == 1);
        using (var f = new Font("Segoe UI", big ? 19f : 15f, big ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
        using (var b = new SolidBrush(statusColor))
        using (var sf = new StringFormat { Alignment = StringAlignment.Center })
            g.DrawString(status, f, b, new RectangleF(0, 380, W, 30), sf);
    }

    void DrawButton(Graphics g)
    {
        RectangleF rc = btn;
        if (pressed) rc.Offset(0, 1.5f);

        // Shadow
        if (!pressed)
            using (var p = RoundRect(new RectangleF(rc.X + 2, rc.Y + 4, rc.Width - 4, rc.Height), rc.Height / 2))
            using (var b = new SolidBrush(Color.FromArgb(45, OrangeDark)))
                g.FillPath(b, p);

        Color top = hover ? Color.FromArgb(255, 186, 100) : OrangeLight;
        Color bot = hover ? Color.FromArgb(255, 128, 10) : Color.FromArgb(240, 108, 0);
        if (rolling) { top = Color.FromArgb(255, 200, 150); bot = Color.FromArgb(250, 160, 90); }

        using (var path = RoundRect(rc, rc.Height / 2))
        {
            using (var b = new LinearGradientBrush(rc, top, bot, 90f))
                g.FillPath(b, path);

            // Aqua-style gloss on the top half
            RectangleF gl = new RectangleF(rc.X + 6, rc.Y + 3, rc.Width - 12, rc.Height * 0.46f);
            using (var gp = RoundRect(gl, gl.Height / 2))
            using (var gb = new LinearGradientBrush(gl, Color.FromArgb(170, Color.White), Color.FromArgb(25, Color.White), 90f))
                g.FillPath(gb, gp);

            using (var p = new Pen(OrangeDark, 1.2f))
                g.DrawPath(p, path);
        }

        using (var f = new Font("Segoe UI", 16f, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            string label = rolling ? "ROLLING" : "ROLL";
            RectangleF tr = new RectangleF(rc.X, rc.Y + 1, rc.Width, rc.Height);
            using (var sh = new SolidBrush(Color.FromArgb(80, 150, 60, 0)))
                g.DrawString(label, f, sh, new RectangleF(tr.X, tr.Y + 1, tr.Width, tr.Height), sf);
            using (var b = new SolidBrush(Color.White))
                g.DrawString(label, f, b, tr, sf);
        }
    }

    void DrawHistory(Graphics g)
    {
        using (var f = new Font("Segoe UI", 10.5f, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var b = new SolidBrush(Muted))
            g.DrawString("LAST ROLLS", f, b, 26, 500);

        using (var p = new Pen(Hairline))
            g.DrawLine(p, 100, 507, W - 26, 507);

        if (history.Count == 0)
        {
            using (var f = new Font("Segoe UI", 12f, FontStyle.Italic, GraphicsUnit.Pixel))
            using (var b = new SolidBrush(Color.FromArgb(195, 195, 195)))
                g.DrawString("nothing yet", f, b, 26, 522);
            return;
        }

        float x = 26;
        using (var f = new Font("Segoe UI", 12f, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            for (int i = 0; i < history.Count; i++)
            {
                int v = history[i];
                RectangleF chip = new RectangleF(x, 520, 36, 24);
                bool first = i == 0;
                using (var path = RoundRect(chip, 12))
                {
                    Color fill = v == 20 ? Orange : first ? Color.FromArgb(255, 236, 216) : Color.White;
                    using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                    using (var p = new Pen(v == 20 ? OrangeDark : first ? OrangeLight : Hairline)) g.DrawPath(p, path);
                }
                Color tc = v == 20 ? Color.White : first ? OrangeDark : Color.FromArgb(110, 110, 110);
                using (var b = new SolidBrush(tc))
                    g.DrawString(v.ToString(), f, b, chip, sf);
                x += 43;
            }
        }
    }

    void DrawFooter(Graphics g)
    {
        using (var f = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Pixel))
        using (var b = new SolidBrush(Color.FromArgb(185, 185, 185)))
        using (var sf = new StringFormat { Alignment = StringAlignment.Center })
            g.DrawString("SPACE / ENTER to roll   ·   ESC to quit", f, b, new RectangleF(0, H - 22, W, 16), sf);
    }

    // ---------- Helpers ----------

    static void Face(Graphics g, Color c, float shade, params PointF[] pts)
    {
        using (var b = new SolidBrush(Shade(c, shade))) g.FillPolygon(b, pts);
    }

    static Color Shade(Color c, float amt)
    {
        if (amt >= 0)
            return Color.FromArgb(c.A,
                (int)(c.R + (255 - c.R) * amt), (int)(c.G + (255 - c.G) * amt), (int)(c.B + (255 - c.B) * amt));
        float k = 1 + amt;
        return Color.FromArgb(c.A, (int)(c.R * k), (int)(c.G * k), (int)(c.B * k));
    }

    static PointF Polar(float radius, float deg)
    {
        double a = deg * Math.PI / 180.0;
        return new PointF((float)(radius * Math.Cos(a)), (float)(radius * Math.Sin(a)));
    }

    static float Dist(PointF a, PointF b)
    {
        float dx = a.X - b.X, dy = a.Y - b.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    static Icon MakeIcon()
    {
        using (var bmp = new Bitmap(32, 32))
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                PointF[] h = new PointF[6];
                for (int i = 0; i < 6; i++)
                {
                    PointF p = Polar(15, -90 + 60 * i);
                    h[i] = new PointF(p.X + 16, p.Y + 16);
                }
                using (var b = new SolidBrush(Orange)) g.FillPolygon(b, h);
                PointF a = Polar(8, -90), c = Polar(8, 30), d = Polar(8, 150);
                using (var b = new SolidBrush(OrangeLight))
                    g.FillPolygon(b, new[] { new PointF(a.X + 16, a.Y + 16), new PointF(c.X + 16, c.Y + 16), new PointF(d.X + 16, d.Y + 16) });
            }
            return Icon.FromHandle(bmp.GetHicon());
        }
    }

    // ---------- Entry ----------

    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

    [STAThread]
    static void Main()
    {
        try { SetProcessDPIAware(); } catch { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new DiceForm());
    }
}
