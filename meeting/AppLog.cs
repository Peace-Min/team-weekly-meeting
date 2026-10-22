using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace TeamWeeklyMeeting;

internal static class AppLog
{
    private static readonly object Gate = new();
    private static readonly string Run = Guid.NewGuid().ToString("N");
    internal static string FilePath { get; private set; } = "";
    internal static string DirectoryPath => Path.GetDirectoryName(FilePath) ?? "";
    internal static string Failure { get; private set; } = "";
    internal static void Initialize(string dataDir)
    {
        FilePath = Path.Combine(dataDir, "logs", "meeting.log");
        Write("app.start", new { version = typeof(AppLog).Assembly.GetName().Version?.ToString(), build=typeof(AppLog).Assembly.ManifestModule.ModuleVersionId,
            pid = Environment.ProcessId, os = Environment.OSVersion.VersionString, runtime = Environment.Version.ToString() });
    }
    internal static void Write(string action, object? data = null, Exception? error = null)
    {
        if (FilePath.Length == 0) return;
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                // Current file plus four archives, each at most approximately 5 MiB.
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length >= 5 * 1024 * 1024)
                {
                    for (int i = 4; i >= 1; i--)
                    {
                        string source = i == 1 ? FilePath : FilePath + "." + (i - 1);
                        if (File.Exists(source)) File.Move(source, FilePath + "." + i, true);
                    }
                }
                string line = JsonSerializer.Serialize(new { at = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9)),
                    run = Run, action, data, error = error == null ? null : new { type = error.GetType().FullName, error.Message, error.StackTrace } });
                File.AppendAllText(FilePath, line + Environment.NewLine, new UTF8Encoding(false));
                Failure = "";
            }
            catch (Exception ex) { Failure = ex.Message; Debug.WriteLine("Meeting log: " + ex.Message); }
        }
    }
}
