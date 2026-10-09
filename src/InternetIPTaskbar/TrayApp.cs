using System.Diagnostics;
using System.Drawing.Drawing2D;
using Microsoft.Win32;
using static InternetIPTaskbar.NativeMethods;

namespace InternetIPTaskbar;

/// <summary>Ties together the IP monitor, the overlay window and the tray icon.</summary>
internal sealed class TrayApp : ApplicationContext
{
    readonly Settings settings = Settings.Load();
    readonly IpMonitor monitor;
    readonly OverlayForm overlay;
    readonly NotifyIcon tray = new();
    readonly ContextMenuStrip menu = new();
    readonly System.Windows.Forms.Timer layoutTimer = new() { Interval = 1000 };
    readonly SynchronizationContext ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
    readonly CancellationTokenSource cts = new();

    Snapshot snap = Snapshot.Empty;
    Bitmap? flag;
    string? flagCountry;
    bool flagLoading;
    DateTime flagRetryAt = DateTime.MinValue;
    IntPtr trayIconHandle;
    bool disposed;

    public TrayApp()
    {
        Log.DebugEnabled = settings.DebugLog;
        Strings.Language = settings.Language;
        Log.Info($"START v{AppInfo.Version} interval={settings.IntervalSeconds}s placement={settings.Placement} alert={settings.AlertMode}");

        overlay = new OverlayForm(settings);
        monitor = new IpMonitor(settings);
        monitor.Changed += s => ui.Post(_ => OnSnapshot(s), null);

        tray.Visible = true;
        tray.ContextMenuStrip = menu;
        menu.Opening += (_, _) => { overlay.Paused = true; RebuildMenu(); };
        menu.Closed += (_, _) => { overlay.Paused = false; overlay.RefreshLayout(); };
        tray.DoubleClick += (_, _) => CopyToClipboard(snap.Ip, Strings.IpCopied);
        UpdateTray();

        overlay.Clicked += () =>
        {
            // without foreground status the menu would not close when the user clicks elsewhere
            SetForegroundWindow(overlay.Handle);
            // an empty ContextMenuStrip is silently not shown; the tray path fills it in Opening, so do it here
            RebuildMenu();
            menu.Show(Cursor.Position);
        };
        layoutTimer.Tick += (_, _) => overlay.RefreshLayout();
        overlay.Shown += (_, _) => overlay.RefreshLayout();
        overlay.Show();
        layoutTimer.Start();

        SystemEvents.DisplaySettingsChanged += OnSystemChanged;
        SystemEvents.UserPreferenceChanged += OnSystemChanged;
        ThreadExit += (_, _) => Cleanup();

        monitor.Start();
    }

    void OnSystemChanged(object? sender, EventArgs e) => ui.Post(_ =>
    {
        if (disposed) return;
        overlay.ApplyColors();
        overlay.RefreshLayout();
        overlay.Invalidate();
    }, null);

    // ───────── state ─────────

    void OnSnapshot(Snapshot s)
    {
        if (disposed) return;
        var prev = snap;
        snap = s;

        // a flag of a different country must not stay on screen: drop it at once and load the new one
        if (s.Country != flagCountry)
        {
            SetFlag(null, s.Country);
            flagRetryAt = DateTime.MinValue;
        }
        if (s.Country is not null && flag is null && !flagLoading && DateTime.UtcNow >= flagRetryAt)
            _ = LoadFlagAsync(s.Country);

        overlay.SetSnapshot(s);
        UpdateTray();

        if (s.Alert && !prev.Alert)
        {
            Log.Info($"ALERT: exit matches home ({settings.AlertMode}) ip={s.Ip} country={s.Country}");
            if (settings.AlertSound) HornSound.Play();
            tray.ShowBalloonTip(8000, Strings.AlertTitle,
                Strings.AlertBody(settings.AlertMode, s.Ip, s.Country), ToolTipIcon.Warning);
        }
        if (s.Ipv6Mismatch && !prev.Ipv6Mismatch)
        {
            Log.Info($"IPv6 mismatch: v4 {s.Ip} ({s.Country}) vs v6 {s.Ipv6} ({s.Ipv6Geo?.Country})");
            tray.ShowBalloonTip(8000, Strings.Ipv6LeakTitle,
                Strings.Ipv6LeakBody(s.Country, s.Ipv6Geo?.Country), ToolTipIcon.Warning);
        }
    }

    void SetFlag(Bitmap? bitmap, string? country)
    {
        overlay.SetFlag(bitmap);   // update the window first, then dispose the old image
        var old = flag;
        flag = bitmap;
        flagCountry = country;
        old?.Dispose();
    }

    async Task LoadFlagAsync(string country)
    {
        flagLoading = true;
        try
        {
            var bmp = await FlagStore.LoadAsync(country, cts.Token); // continues on the UI thread
            if (disposed || country != snap.Country) { bmp?.Dispose(); return; } // the country changed meanwhile
            if (bmp is null) { flagRetryAt = DateTime.UtcNow.AddSeconds(60); return; }
            SetFlag(bmp, country);
            UpdateTray();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Log.Info($"FLAG error {ex.GetType().Name}: {ex.Message}"); flagRetryAt = DateTime.UtcNow.AddSeconds(60); }
        finally { flagLoading = false; }
    }

    // ───────── tray icon and tooltip ─────────

    void UpdateTray()
    {
        string text = snap.State switch
        {
            NetState.Offline => Strings.Offline,
            NetState.Starting => Strings.Detecting,
            _ => $"{snap.Ip}{(snap.Country is null ? "" : " · " + snap.Country)}"
                 + (snap.Geo?.Isp is { } isp ? "\n" + isp : "")
        };
        if (snap.State == NetState.Stale) text += "\n" + Strings.UpdateFailedTip;
        if (snap.Alert) text = "⚠ " + text;
        tray.Text = text.Length > 120 ? text[..120] : text; // NotifyIcon.Text is limited to 127 characters

        var newHandle = DrawTrayIcon(out var icon);
        tray.Icon = icon;
        if (trayIconHandle != IntPtr.Zero) DestroyIcon(trayIconHandle);
        trayIconHandle = newHandle;
    }

    IntPtr DrawTrayIcon(out Icon icon)
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            var rect = new Rectangle(2, 6, 28, 20);

            if (snap.State == NetState.Offline)
            {
                using var b = new SolidBrush(Color.FromArgb(110, 110, 110));
                g.FillRectangle(b, rect);
                DrawCenteredText(g, "×", rect, 14);
            }
            else if (flag is not null && snap.Country == flagCountry)
            {
                g.DrawImage(flag, rect);
            }
            else
            {
                using var b = new SolidBrush(Color.FromArgb(60, 90, 140));
                g.FillRectangle(b, rect);
                DrawCenteredText(g, snap.Country ?? "IP", rect, snap.Country is null ? 11 : 10);
            }
            if (snap.Alert)
            {
                using var alertBorder = new Pen(Color.FromArgb(255, 145, 0), 3);
                g.DrawRectangle(alertBorder, rect);
            }
            else
            {
                // a thin crisp white outline around the flag (no anti-aliasing for a sharp 1 px line)
                g.SmoothingMode = SmoothingMode.None;
                using var border = new Pen(Color.White, 1);
                g.DrawRectangle(border, rect.X - 1, rect.Y - 1, rect.Width + 1, rect.Height + 1);
            }
        }
        var handle = bmp.GetHicon();
        icon = Icon.FromHandle(handle); // does not own the handle; it is released in UpdateTray
        return handle;
    }

    static void DrawCenteredText(Graphics g, string text, Rectangle rect, float size)
    {
        using var font = new Font("Segoe UI", size, FontStyle.Bold, GraphicsUnit.Pixel);
        using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, Brushes.White, rect, fmt);
    }

    // ───────── menu ─────────

    void RebuildMenu()
    {
        // Dispose() removes an item from the collection, so iterate over a copy
        foreach (var item in menu.Items.Cast<ToolStripItem>().ToArray()) item.Dispose();
        menu.Items.Clear();

        Add(menu, snap.State switch
        {
            NetState.Offline => Strings.Offline,
            NetState.Starting => Strings.Detecting,
            _ => $"{snap.Ip}{(snap.Country is null ? "" : "  " + snap.Country)}"
        }, enabled: false, bold: true);
        if (snap.Ipv6 is not null)
            Add(menu, $"IPv6: {snap.Ipv6}{(snap.Ipv6Geo is null ? "" : "  " + snap.Ipv6Geo.Country)}", enabled: false);
        if (snap.Ipv6Mismatch)
            Add(menu, Strings.Ipv6MismatchMenu, enabled: false);
        if (snap.State == NetState.Stale)
            Add(menu, Strings.UpdateFailedMenu, enabled: false);
        menu.Items.Add(new ToolStripSeparator());

        Add(menu, Strings.CopyIp, () => CopyToClipboard(snap.Ip, Strings.IpCopied), enabled: snap.Ip is not null);
        if (snap.Ipv6 is not null)
            Add(menu, Strings.CopyIpv6, () => CopyToClipboard(snap.Ipv6, Strings.Ipv6Copied));
        Add(menu, Strings.CheckNow, () => monitor.CheckNow());
        menu.Items.Add(new ToolStripSeparator());

        var prefs = new ToolStripMenuItem(Strings.Settings);

        var interval = new ToolStripMenuItem(Strings.Interval);
        foreach (var sec in Settings.Intervals)
        {
            Add(interval, Strings.Seconds(sec), () => { settings.IntervalSeconds = sec; settings.Save(); monitor.CheckNow("interval"); },
                checkedState: settings.IntervalSeconds == sec);
        }
        prefs.DropDownItems.Add(interval);

        var placement = new ToolStripMenuItem(Strings.Placement);
        Add(placement, Strings.PlacementNearTray, () => SetPlacement(Placement.NearTray), checkedState: settings.Placement == Placement.NearTray);
        Add(placement, Strings.PlacementCenter, () => SetPlacement(Placement.Center), checkedState: settings.Placement == Placement.Center);
        prefs.DropDownItems.Add(placement);

        var guard = new ToolStripMenuItem(Strings.VpnGuard);
        Add(guard, Strings.GuardOff, () => SetAlert(AlertMode.Off), checkedState: settings.AlertMode == AlertMode.Off);
        // three independent choices: watch the exact IP, its /24 range, or the country
        Add(guard, Strings.GuardByIp(settings.HomeIp), () => SetAlert(AlertMode.ByIp), checkedState: settings.AlertMode == AlertMode.ByIp);
        Add(guard, Strings.GuardByRange(settings.HomeRange), () => SetAlert(AlertMode.ByRange), checkedState: settings.AlertMode == AlertMode.ByRange);
        Add(guard, Strings.GuardByCountry(settings.HomeCountry), () => SetAlert(AlertMode.ByCountry), checkedState: settings.AlertMode == AlertMode.ByCountry);
        guard.DropDownItems.Add(new ToolStripSeparator());
        Add(guard, Strings.RememberIp, () => RememberHome(AlertMode.ByIp), enabled: snap.Ip is not null);
        Add(guard, Strings.RememberRange, () => RememberHome(AlertMode.ByRange), enabled: IpRange.FromIp(snap.Ip) is not null);
        Add(guard, Strings.RememberCountry, () => RememberHome(AlertMode.ByCountry), enabled: snap.Country is not null);
        Add(guard, Strings.ForgetHome, () =>
        {
            settings.HomeIp = settings.HomeRange = settings.HomeCountry = null;
            settings.AlertMode = AlertMode.Off;
            settings.Save();
            monitor.Reevaluate();
        });
        prefs.DropDownItems.Add(guard);

        var language = new ToolStripMenuItem(Strings.LanguageMenu);
        Add(language, Strings.LanguageAuto, () => SetLanguage(UiLanguage.Auto), checkedState: settings.Language == UiLanguage.Auto);
        Add(language, Strings.LanguageEnglish, () => SetLanguage(UiLanguage.English), checkedState: settings.Language == UiLanguage.English);
        Add(language, Strings.LanguageRussian, () => SetLanguage(UiLanguage.Russian), checkedState: settings.Language == UiLanguage.Russian);
        Add(language, Strings.LanguageChinese, () => SetLanguage(UiLanguage.ChineseHK), checkedState: settings.Language == UiLanguage.ChineseHK);
        prefs.DropDownItems.Add(language);

        prefs.DropDownItems.Add(new ToolStripSeparator());
        Add(prefs, Strings.Horn, () =>
        {
            settings.AlertSound = !settings.AlertSound;
            settings.Save();
            if (settings.AlertSound) HornSound.Play(); // preview when switched on
        }, checkedState: settings.AlertSound);
        Add(prefs, Strings.CheckIpv6, () => { settings.CheckIpv6 = !settings.CheckIpv6; settings.Save(); monitor.CheckNow("settings"); },
            checkedState: settings.CheckIpv6);
        Add(prefs, Strings.StartWithWindows, ToggleAutostart, checkedState: Autostart.IsEnabled);
        Add(prefs, Strings.VerboseLog, () =>
        {
            settings.DebugLog = !settings.DebugLog;
            Log.DebugEnabled = settings.DebugLog;
            settings.Save();
        }, checkedState: settings.DebugLog);
        prefs.DropDownItems.Add(new ToolStripSeparator());
        Add(prefs, Strings.UninstallItem, Uninstall, enabled: UninstallerPath is not null);
        menu.Items.Add(prefs);
        menu.Items.Add(new ToolStripSeparator());

        Add(menu, Strings.OpenLog, () => Process.Start(new ProcessStartInfo("notepad.exe", Log.FilePath) { UseShellExecute = true }));
        menu.Items.Add(new ToolStripSeparator());
        Add(menu, Strings.Exit(AppInfo.Version), ExitThread);
    }

    static ToolStripMenuItem Add(ToolStrip parent, string text, Action? onClick = null, bool enabled = true, bool checkedState = false, bool bold = false)
        => Add(parent.Items, text, onClick, enabled, checkedState, bold);

    static ToolStripMenuItem Add(ToolStripMenuItem parent, string text, Action? onClick = null, bool enabled = true, bool checkedState = false, bool bold = false)
        => Add(parent.DropDownItems, text, onClick, enabled, checkedState, bold);

    static ToolStripMenuItem Add(ToolStripItemCollection items, string text, Action? onClick, bool enabled, bool checkedState, bool bold)
    {
        var item = new ToolStripMenuItem(text) { Enabled = enabled, Checked = checkedState };
        if (bold) item.Font = new Font(item.Font, FontStyle.Bold);
        if (onClick is not null) item.Click += (_, _) => onClick();
        items.Add(item);
        return item;
    }

    /// <summary>The Inno Setup uninstaller next to the exe; null when the program is not running from an installation.</summary>
    static string? UninstallerPath
    {
        get
        {
            var dir = Path.GetDirectoryName(Environment.ProcessPath);
            var path = dir is null ? null : Path.Combine(dir, "unins000.exe");
            return path is not null && File.Exists(path) ? path : null;
        }
    }

    void Uninstall()
    {
        if (UninstallerPath is not { } path) return;
        try
        {
            Log.Info("UNINSTALL requested");
            // the uninstaller asks for confirmation itself and closes this program only after the user agrees
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Info($"UNINSTALL failed: {ex.GetType().Name}: {ex.Message}");
            tray.ShowBalloonTip(5000, Strings.UninstallTitle, Strings.UninstallFailed(ex.Message), ToolTipIcon.Error);
        }
    }

    void ToggleAutostart()
    {
        try { Autostart.SetEnabled(!Autostart.IsEnabled); }
        catch (Exception ex)
        {
            Log.Info($"AUTOSTART failed: {ex.GetType().Name}: {ex.Message}");
            tray.ShowBalloonTip(5000, Strings.AutostartTitle, Strings.AutostartFailed(ex.Message), ToolTipIcon.Error);
        }
    }

    void SetPlacement(Placement p)
    {
        settings.Placement = p;
        settings.Save();
        overlay.RefreshLayout();
    }

    void SetAlert(AlertMode mode)
    {
        if (mode == AlertMode.ByIp && string.IsNullOrEmpty(settings.HomeIp)
            || mode == AlertMode.ByRange && string.IsNullOrEmpty(settings.HomeRange)
            || mode == AlertMode.ByCountry && string.IsNullOrEmpty(settings.HomeCountry))
        {
            tray.ShowBalloonTip(5000, Strings.HomeNotSetTitle, Strings.HomeNotSetBody, ToolTipIcon.Info);
            return;
        }
        settings.AlertMode = mode;
        settings.Save();
        monitor.Reevaluate();
    }

    /// <summary>Remembers only the value for <paramref name="mode"/> (IP, its /24 range, or the country) and switches the alert to that mode.</summary>
    void RememberHome(AlertMode mode)
    {
        string? value;
        switch (mode)
        {
            case AlertMode.ByIp: value = settings.HomeIp = snap.Ip; break;
            case AlertMode.ByRange: value = settings.HomeRange = IpRange.FromIp(snap.Ip); break;
            case AlertMode.ByCountry: value = settings.HomeCountry = snap.Country; break;
            default: return;
        }
        if (string.IsNullOrEmpty(value)) return;
        settings.AlertMode = mode;
        settings.Save();
        Log.Info($"HOME remembered: mode={mode}" + (mode == AlertMode.ByIp ? " (IP saved to settings.json)" : $" value={value}"));
        monitor.Reevaluate();
        tray.ShowBalloonTip(5000, Strings.HomeSavedTitle, Strings.HomeSavedBody(mode, value), ToolTipIcon.Info);
    }

    void SetLanguage(UiLanguage language)
    {
        settings.Language = language;
        settings.Save();
        Strings.Language = language;
        UpdateTray();           // tooltip and icon text
        overlay.RefreshLayout();   // "no network" text changes width
        overlay.Invalidate();
    }

    void CopyToClipboard(string? value, string message)
    {
        if (string.IsNullOrEmpty(value)) return;
        try
        {
            Clipboard.SetText(value);
            tray.ShowBalloonTip(1500, message, value, ToolTipIcon.Info);
        }
        catch (Exception ex) { Log.Info($"CLIPBOARD failed: {ex.Message}"); }
    }

    // ───────── shutdown ─────────

    void Cleanup()
    {
        if (disposed) return;
        disposed = true;
        SystemEvents.DisplaySettingsChanged -= OnSystemChanged;
        SystemEvents.UserPreferenceChanged -= OnSystemChanged;
        layoutTimer.Stop();
        cts.Cancel();
        monitor.Dispose();
        tray.Visible = false;
        tray.Dispose();
        menu.Dispose();
        overlay.Close();
        overlay.Dispose();
        flag?.Dispose();
        if (trayIconHandle != IntPtr.Zero) DestroyIcon(trayIconHandle);
        Log.Info("EXIT");
    }
}
