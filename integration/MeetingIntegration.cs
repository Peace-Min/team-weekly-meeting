using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace TaskCalendarWidget;

// The companion uses the same roster query and version gate as the calendar.
public static class MeetingIntegration
{
    public static string RowExtractJs => NetcusText.RowExtractJs;
    public static string CellExtractJs => NetcusText.CellExtractJs;
    public static string GoViewJs(string number, string id) => NetcusText.GoViewJs(number, id);

    public static async Task<string> LoadDirectoryAsync(string loginId, Action<string>? log = null)
    {
        if (string.IsNullOrWhiteSpace(loginId))
            throw new InvalidOperationException("본인 계정으로 로그인한 뒤 조직도를 불러오세요.");
        var db = new ProjectDb(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TeamWeeklyMeeting"), log ?? (_ => { }));
        string? json = await db.LoadMembersJsonAsync(loginId);
        if (json == null) throw new IOException("조직도를 불러오지 못했습니다. 로컬 DB 접속 설정과 DB 구조 버전을 확인하세요. 자세한 원인은 로그에 기록됩니다.");
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("found", out var found) || !found.GetBoolean())
            throw new InvalidOperationException("캘린더에 등록된 본인 계정으로 로그인하세요.");
        return json;
    }
}
