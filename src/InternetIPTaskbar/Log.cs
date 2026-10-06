namespace InternetIPTaskbar;

/// <summary>File log with rotation. Info is always written, Debug only when the flag is on.</summary>
internal static class Log
{
    const long MaxBytes = 1_000_000;
    const int Files = 3; // app.log, app.1.log, app.2.log

    static readonly object Gate = new();
    static readonly string Dir = Path.Combine(AppInfo.DataDir, "logs");

    public static string FilePath { get; } = Path.Combine(Dir, "app.log");
    public static bool DebugEnabled { get; set; }

    public static void Info(string message) => Write("INFO", message);

    public static void Debug(string message)
    {
        if (DebugEnabled) Write("DBG ", message);
    }

    static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Dir);
                var info = new FileInfo(FilePath);
                if (info.Exists && info.Length > MaxBytes) Rotate();
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}{Environment.NewLine}");
            }
        }
        catch { }
    }

    static void Rotate()
    {
        string Name(int i) => i == 0 ? FilePath : Path.Combine(Dir, $"app.{i}.log");
        File.Delete(Name(Files - 1));
        for (int i = Files - 2; i >= 0; i--)
        {
            if (File.Exists(Name(i))) File.Move(Name(i), Name(i + 1), true);
        }
    }
}
