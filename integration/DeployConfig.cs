using System;
using System.IO;
using System.Text.Json;

namespace TaskCalendarWidget;

// Deployment secrets belong to the user's profile, never the public source or installer.
internal static class DeployConfig
{
    internal static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TeamWeeklyMeeting", "connection.json");

    private sealed record Settings(string Host = "localhost", int Port = 3306,
        string Database = "taskcalendar", string User = "", string Password = "");

    private static Settings Read()
    {
        if (!File.Exists(ConfigPath)) return new Settings();
        try
        {
            return JsonSerializer.Deserialize<Settings>(File.ReadAllText(ConfigPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new Settings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException("DB 연결 설정 파일을 읽을 수 없습니다: " + ConfigPath);
        }
    }

    private static string Value(string variable, Func<Settings, string> read) =>
        Environment.GetEnvironmentVariable(variable) ?? read(Read());

    public static string DbHost => Value("TEAM_MEETING_DB_HOST", s => s.Host);
    public static int DbPort => int.TryParse(Environment.GetEnvironmentVariable("TEAM_MEETING_DB_PORT"), out int port) ? port : Read().Port;
    public static string DbName => Value("TEAM_MEETING_DB_NAME", s => s.Database);
    public static string DbUser => Value("TEAM_MEETING_DB_USER", s => s.User);
    public static string DbPassword => Value("TEAM_MEETING_DB_PASSWORD", s => s.Password);

    public static void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(DbHost) || string.IsNullOrWhiteSpace(DbName) || string.IsNullOrWhiteSpace(DbUser))
            throw new InvalidOperationException("DB 접속 설정이 필요합니다. connection.example.json을 참고해 " + ConfigPath + "을 작성한 뒤 앱을 다시 실행하세요.");
        if (DbPort < 1 || DbPort > 65535) throw new InvalidOperationException("DB 포트는 1~65535로 설정하세요.");
    }
}
