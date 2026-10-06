namespace InternetIPTaskbar;

internal static class AppInfo
{
    public static readonly string Version =
        typeof(AppInfo).Assembly.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "0.0.0";

    public static string UserAgent => $"InternetIPTaskbar/{Version}";

    public static readonly string DataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InternetIPTaskbar");
}
