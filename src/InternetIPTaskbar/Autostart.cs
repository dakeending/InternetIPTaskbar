using Microsoft.Win32;

namespace InternetIPTaskbar;

/// <summary>
/// Start with Windows via HKCU\...\Run (the installer writes the same value).
/// A "disabled" mark set in Task Manager (StartupApproved) is honoured and cleared when we enable autostart.
/// </summary>
internal static class Autostart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    const string ValueName = "InternetIPTaskbar";

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var run = Registry.CurrentUser.OpenSubKey(RunKey);
                return run?.GetValue(ValueName) is string && !DisabledInTaskManager();
            }
            catch { return false; }
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using (var run = Registry.CurrentUser.CreateSubKey(RunKey))
        {
            if (enabled)
            {
                var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot determine the path of the executable");
                run.SetValue(ValueName, $"\"{exe}\"");
            }
            else run.DeleteValue(ValueName, false);
        }

        // a "disabled" mark set in Task Manager would otherwise override our enabling
        using (var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, true))
            approved?.DeleteValue(ValueName, false);

        Log.Info($"AUTOSTART {(enabled ? "enabled" : "disabled")}");
    }

    static bool DisabledInTaskManager()
    {
        try
        {
            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey);
            return approved?.GetValue(ValueName) is byte[] { Length: > 0 } b && (b[0] & 1) == 1;
        }
        catch { return false; }
    }
}
