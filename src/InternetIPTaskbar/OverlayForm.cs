using System.Drawing.Drawing2D;
using Microsoft.Win32;
using static InternetIPTaskbar.NativeMethods;

namespace InternetIPTaskbar;

/// <summary>
/// A small "flag + IP" window above the taskbar. It never takes focus; a click opens the tray menu.
/// It hides itself for full-screen applications and while an auto-hidden taskbar is collapsed.
/// </summary>
internal sealed class OverlayForm : Form
{
    static readonly Color DarkBack = Color.FromArgb(28, 28, 28), DarkText = Color.White, DarkDim = Color.FromArgb(150, 150, 150);
    static readonly Color LightBack = Color.FromArgb(243, 243, 243), LightText = Color.FromArgb(20, 20, 20), LightDim = Color.FromArgb(115, 115, 115);
    static readonly Color AlertBack = Color.FromArgb(176, 32, 32);

    readonly Settings settings;
    readonly Font font = new("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point);
    readonly Font badgeFont = new("Segoe UI", 7f, FontStyle.Bold, GraphicsUnit.Point);

    Snapshot snap = Snapshot.Empty;
    Bitmap? flag; // owned by TrayApp
    bool lightTheme;

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
        ApplyTheme();
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
        bool visualChange = s.State != snap.State || s.Alert != snap.Alert || s.Ip != snap.Ip || s.Country != snap.Country;
        snap = s;
        if (!visualChange) return;
        ApplyTheme();
        RefreshLayout();
        Invalidate();
    }

    /// <summary>The bitmap is owned by the caller; null means show the country code instead of a picture.</summary>
    public void SetFlag(Bitmap? bitmap)
    {
        flag = bitmap;
        Invalidate();
    }

    public void ApplyTheme()
    {
        lightTheme = IsLightTaskbar();
        BackColor = snap.Alert ? AlertBack : lightTheme ? LightBack : DarkBack;
    }

    static bool IsLightTaskbar()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is int v && v == 1;
        }
        catch { return false; }
    }

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
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var lay = Measure();
        Color text = snap.Alert ? Color.White
            : snap.State is NetState.Stale or NetState.Offline ? (lightTheme ? LightDim : DarkDim)
            : lightTheme ? LightText : DarkText;

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
            using var pen = new Pen(Color.FromArgb(90, 128, 128, 128));
            g.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
            return;
        }
        // the flag is not loaded yet or unavailable: show the country code
        using var fill = new SolidBrush(Color.FromArgb(110, 128, 128, 128));
        g.FillRectangle(fill, rect);
        TextRenderer.DrawText(g, snap.Country ?? "", badgeFont, rect, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
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
            font.Dispose();
            badgeFont.Dispose();
        }
        base.Dispose(disposing);
    }
}


