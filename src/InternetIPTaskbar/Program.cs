namespace InternetIPTaskbar;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        // The name carries no version number: otherwise two copies could run side by side after an update.
        using var mutex = new Mutex(true, "InternetIPTaskbar.SingleInstance", out bool first);
        if (!first) return;

        Application.ThreadException += (_, e) => Log.Info($"UNHANDLED (UI) {e.Exception}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Info($"UNHANDLED {e.ExceptionObject}");

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}
