using System.Drawing.Drawing2D;
using static InternetIPTaskbar.NativeMethods;

namespace InternetIPTaskbar;

/// <summary>
/// A small "flag + IP" window above the taskbar. It never takes focus; a click opens the tray menu.
/// It hides itself for full-screen applications and while an auto-hidden taskbar is collapsed.
/// The plate is green when everything is fine, grey while there is no network, and bright orange on an alert
/// (blinking at 3 Hz for the first 10 seconds, then steady).
/// </summary>
internal sealed class OverlayForm : Form
{
    // plate colours
    static readonly Color Green = Color.FromArgb(21, 128, 61), GreenDimText = Color.FromArgb(200, 232, 212);
    static readonly Color Grey = Color.FromArgb(96, 104, 116);
    static readonly Color Orange = Color.FromArgb(255, 145, 0), OrangeText = Color.FromArgb(25, 25, 25);
    static readonly Color OrangeOff = Color.FromArgb(92, 46, 6); // the "dark" half of a blink

    public const double BlinkHz = 3;
    public const double BlinkSeconds = 10;

    readonly Settings settings;
    readonly Font font = new("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point);
    readonly Font badgeFont = new("Segoe UI", 7f, FontStyle.Bold, GraphicsUnit.Point);
    readonly System.Windows.Forms.Timer blinkTimer = new() { Interval = 40 };

    Snapshot snap = Snapshot.Empty;
    Bitmap? flag; // owned by TrayApp
    DateTime blinkStart;
    bool blinkOn = true;

    public OverlayForm(Settings settings)
    {
        this.settings = settings;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Opacity = .96;
        Size = new Size(120, 28);
        SetStyle(ControlStyles.Selectable, false);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
            | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        UpdateStyles();
        blinkTimer.Tick += (_, _) => OnBlinkTick();
        ApplyColors();
    }

    /// <summary>While true the window is neither raised nor moved (otherwise it would cover an open tray menu).</summary>
    public bool Paused { get; set; }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    /// <summary>A mouse click (left or right) on the window; opens the menu like the tray icon does.</summary>
    public event Action? Clicked;

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button is MouseButtons.Left or MouseButtons.Right) Clicked?.Invoke();
    }

    public void SetSnapshot(Snapshot s)
    {
        bool wasAlert = snap.Alert;
        bool visualChange = s.State != snap.State || s.Alert != snap.Alert || s.Ip != snap.Ip || s.Country != snap.Country;
        snap = s;

        if (s.Alert && !wasAlert) StartBlink();
        else if (!s.Alert) StopBlink();

        if (!visualChange) return;
        ApplyColors();
        RefreshLayout();
        Invalidate();
    }

    /// <summary>The bitmap is owned by the caller; null means show the country code instead of a picture.</summary>
    public void SetFlag(Bitmap? bitmap)
    {
        flag = bitmap;
        Invalidate();
    }

    // ───────── colours and blinking ─────────

    void StartBlink()
    {
        blinkStart = DateTime.UtcNow;
        blinkOn = true;
        blinkTimer.Start();
    }

    void StopBlink()
    {
        blinkTimer.Stop();
        blinkOn = true;
    }

    void OnBlinkTick()
    {
        var elapsed = (DateTime.UtcNow - blinkStart).TotalSeconds;
        if (elapsed >= BlinkSeconds)
        {
            // blinking is over: the plate stays bright orange while the alert lasts
            StopBlink();
            ApplyColors();
            Invalidate();
            return;
        }
        bool on = BlinkPhaseOn(elapsed);
        if (on == blinkOn) return;
        blinkOn = on;
        ApplyColors();
        Invalidate();
    }

    /// <summary>True during the bright half of a blink cycle; <paramref name="elapsedSeconds"/> counts from the start of the alert.</summary>
    internal static bool BlinkPhaseOn(double elapsedSeconds) => (int)(elapsedSeconds * BlinkHz * 2) % 2 == 0;

    (Color Back, Color Text) PlateColors()
    {
        if (snap.Alert) return blinkOn ? (Orange, OrangeText) : (OrangeOff, Color.White);
        return snap.State switch
        {
            NetState.Ok => (Green, Color.White),
            NetState.Stale => (Green, GreenDimText), // one failed request: keep the plate, dim the text
            _ => (Grey, Color.White)                 // starting or no network
        };
    }

    public void ApplyColors() => BackColor = PlateColors().Back;

    // ───────── layout and painting ─────────

    float Dpi => DeviceDpi / 96f;

    string Caption => snap.State switch
    {
        NetState.Offline => Strings.OverlayOffline,
        NetState.Starting => "…",
        _ => snap.Ip ?? "…"
    };

    readonly record struct Metrics(int Pad, int Gap, int BadgeW, int BadgeH, Size TextSize)
    {
        public int Width => Pad * 2 + (BadgeW > 0 ? BadgeW + Gap : 0) + TextSize.Width;
    }

    Metrics Measure()
    {
        float s = Dpi;
        var textSize = TextRenderer.MeasureText(Caption, font, new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        bool hasBadge = snap.State != NetState.Offline && snap.Country is not null;
        return new Metrics((int)(8 * s), (int)(6 * s), hasBadge ? (int)(20 * s) : 0, (int)(14 * s), textSize);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var (back, text) = PlateColors();
        g.Clear(back);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var lay = Measure();
        int x = lay.Pad;
        if (lay.BadgeW > 0)
        {
            var rect = new Rectangle(x, (Height - lay.BadgeH) / 2, lay.BadgeW, lay.BadgeH);
            DrawBadge(g, rect);
            x += lay.BadgeW + lay.Gap;
        }
        TextRenderer.DrawText(g, Caption, font, new Point(x, (Height - lay.TextSize.Height) / 2), text,
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }

    void DrawBadge(Graphics g, Rectangle rect)
    {
        if (flag is not null)
        {
            g.DrawImage(flag, rect);
        }
        else
        {
            // the flag is not loaded yet or unavailable: show the country code
            using var fill = new SolidBrush(Color.FromArgb(110, 128, 128, 128));
            g.FillRectangle(fill, rect);
            TextRenderer.DrawText(g, snap.Country ?? "", badgeFont, rect, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }

        // a thin white outline around the flag keeps it distinct from the plate (crisp 1 px line: no anti-aliasing)
        var smoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.None;
        using var pen = new Pen(Color.White, 1f);
        g.DrawRectangle(pen, rect.X - 1, rect.Y - 1, rect.Width + 1, rect.Height + 1);
        g.SmoothingMode = smoothing;
    }

    /// <summary>Recomputes size and position and shows or hides the window. Call on the UI thread.</summary>
    public void RefreshLayout()
    {
        if (!IsHandleCreated || IsDisposed || Paused) return;

        var tb = Taskbar.Query();
        if (tb is not { Visible: true } info || FullscreenDetector.IsActive(Handle, info.Monitor))
        {
            SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_HIDEWINDOW);
            return;
        }

        float s = Dpi;
        var size = new Size(Measure().Width, (int)(28 * s));
        var target = new Rectangle(ComputeLocation(info, size, s), size);
        if (Bounds != target) Bounds = target;

        // the taskbar raises itself above topmost windows when clicked, so put our window back on top
        SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOOWNERZORDER);
    }

    Point ComputeLocation(TaskbarInfo tb, Size size, float s)
    {
        int margin = (int)(6 * s);
        bool notifyOk = !tb.Notify.IsEmpty && tb.Bounds.Contains(tb.Notify);
        bool center = settings.Placement == Placement.Center;

        if (tb.Horizontal)
        {
            int y = tb.Bounds.Top + (tb.Bounds.Height - size.Height) / 2;
            int x = center
                ? tb.Monitor.Left + (tb.Monitor.Width - size.Width) / 2
                : (notifyOk ? tb.Notify.Left : tb.Bounds.Right - (int)(160 * s)) - size.Width - margin;
            return new Point(Math.Max(tb.Bounds.Left, x), y);
        }

        // a vertical taskbar is too narrow: place the window next to it, above the notification area
        int vx = tb.Edge == TaskbarEdge.Left ? tb.Bounds.Right : tb.Bounds.Left - size.Width;
        int vy = center
            ? tb.Monitor.Top + (tb.Monitor.Height - size.Height) / 2
            : (notifyOk ? tb.Notify.Top : tb.Bounds.Bottom - (int)(120 * s)) - size.Height - margin;
        return new Point(vx, Math.Max(tb.Monitor.Top, vy));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            blinkTimer.Dispose();
            font.Dispose();
            badgeFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
