using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;

namespace TaskCalendarWidget;

// Read-only roster slice from task-calendar; no calendar write/session modules are shipped.
internal sealed class ProjectDb
{
    private readonly Action<string> _log;
    public ProjectDb(string dataDir, Action<string> log) { _log = log; }
    private const string ReadPreambleSql = "SET SESSION time_zone='+00:00'";
    private static string NotFoundJson() => JsonSerializer.Serialize(new { found = false });
    private static string BuildConnString()
    {
        DeployConfig.EnsureConfigured();
        return new MySqlConnectionStringBuilder {
            Server=DeployConfig.DbHost, Port=(uint)DeployConfig.DbPort,
            Database=DeployConfig.DbName, UserID=DeployConfig.DbUser,
            Password=DeployConfig.DbPassword, ConnectionTimeout=4,
            DefaultCommandTimeout=8, Pooling=false
        }.ConnectionString;
    }
    private sealed class OrgUnitRow
        {
            public string Name = "";
            public string Parent = "";     // 최상위는 빈 문자열(NULL) — payload 로 나갈 때 null 로 바뀐다
            public int SortOrder;
            //  ★ 관리자 편집 폼의 소속 드롭다운이 **번호로** 저장하기 때문에 필요하다(app_user.org_id).
            //    이름으로 보내면 개명 한 번에 소속이 통째로 어긋난다 — 완전 절단(01-schema-users.sql ★★)의 요지다.
            //    비관리자 회신에는 싣지 않는다(쓸 데가 없다).
            public int? OrgId;
        }

    public async Task<string?> LoadMembersJsonAsync(string loginId, bool includeInactive = false, bool flatOrder = false)
        {
            string id = (loginId ?? "").Trim();
            if (id.Length == 0) return NotFoundJson();
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                await using var conn = await OpenReadAsync(cts.Token);

                // ① 나 자신 — 일정 열람 범위와 소속을 여기서 정한다.
                //   ★ name/title 은 읽지 않는다: 명부에 전원이 담기므로 본인 행도 그 안에 들어 있다.
                //   ★ edit_role 은 '직원 관리 화면을 그릴 것인가' 판정용이다(위 ★). 표시용이 아니다.
                string scope = "", myUnit = "", myRole = "";
                int myActive = 0;
                bool found = false;
                //   ★ 소속 이름은 org_unit 을 JOIN 해서 만든다 — myUnit 은 이름 기준 트리 순회(③)의 시작점이다.
                await using (var cmd = new MySqlCommand(
                    "SELECT o.name AS org_unit, u.view_scope, u.edit_role, u.is_active " +
                    "FROM app_user u LEFT JOIN org_unit o ON o.org_id = u.org_id " +
                    "WHERE u.login_id=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);   // 값은 반드시 파라미터 바인딩(문자열 연결 금지)
                    await using var rd = await cmd.ExecuteReaderAsync(cts.Token);
                    if (await rd.ReadAsync(cts.Token))
                    {
                        found    = true;
                        myUnit   = Str(rd, "org_unit");
                        scope    = Str(rd, "view_scope");
                        myRole   = Str(rd, "edit_role");
                        myActive = IntOrNull(rd, "is_active") ?? 0;
                    }
                }
                if (!found)
                {
                    _log("DB 구성원 조회: 미등록(" + id + ")");
                    return NotFoundJson();
                }
                // ★ is_active=0 을 여기서 막지 않는다 — 이건 '읽기'다. 회수의 목적은 편집 차단이지
                //   조직도 열람 차단이 아니고, 막으면 비활성 계정은 자기 상태를 확인할 화면조차 잃는다.
                //   실제 차단은 쓰기 관문(OpenWriteAsync) 한 곳에서만 한다.

                // 관리자인가 — 활성 admin 만이다. 비활성 admin 에게 편집 컨트롤을 그려 봐야
                //   저장이 관문(OpenAdminAsync)에서 전부 거부된다: 화면과 관문이 같은 답을 해야 한다.
                bool isAdmin = myActive != 0 && string.Equals(myRole, "admin", StringComparison.Ordinal);
                bool withInactive = isAdmin && includeInactive;   // 퇴사자 보기는 관리자 전용

                // ② 조직 트리 — 열람 범위와 무관하게 항상 전 조직이다(scope 로 건너뛰지 않는다).
                //   조직도는 사내망에 이미 공개된 정보고, 트리가 없으면 '내 위에 무엇이 있는지'조차 볼 수 없다.
                //   ★ 부모 '이름'은 자기 JOIN 으로 만든다(정본은 parent_id 뿐이다). 최상위는 parent_id IS NULL →
                //     LEFT JOIN 이 NULL 을 돌려주고 Str() 이 ""로 바꾼다 → payload 에서 다시 null 이 된다(종전과 동일).
                //   ★ ORDER BY 대상은 예전 그대로 org_unit 자신의 sort_order·name 이다(화면 순서 불변).
                var units = new List<OrgUnitRow>();
                await using (var cmd = new MySqlCommand(
                    "SELECT t.name, p.name AS parent, t.sort_order, t.org_id " +
                    "FROM org_unit t LEFT JOIN org_unit p ON p.org_id = t.parent_id " +
                    "WHERE t.is_active=1 ORDER BY t.sort_order, t.name", conn))
                {
                    await using var rd = await cmd.ExecuteReaderAsync(cts.Token);
                    while (await rd.ReadAsync(cts.Token))
                    {
                        string n = Str(rd, "name");
                        if (n.Length == 0) continue;
                        units.Add(new OrgUnitRow { Name = n, Parent = Str(rd, "parent"), SortOrder = IntOrNull(rd, "sort_order") ?? 0, OrgId = IntOrNull(rd, "org_id") });
                    }
                }

                // ③ 일정 열람 가능 유닛 집합 — 명부의 경계가 아니라 '누구의 일정을 열 수 있는가'다.
                //   self(그리고 알 수 없는 값)는 빈 집합 → 명부는 전원 그대로 나가되 아무도 누를 수 없다.
                var allowed = new HashSet<string>(StringComparer.Ordinal);
                switch (scope)
                {
                    case "all":
                        foreach (var u in units) allowed.Add(u.Name);
                        break;
                    case "unit_tree":
                        ExpandUnitTree(units, myUnit, allowed);
                        break;
                    default:
                        break;   // self(그리고 알 수 없는 값) — 빈 집합.
                }

                // ④ 구성원 — 항상 전원(is_active=1). 유닛 필터(IN 절)를 두지 않는다:
                //   명부는 통제 대상이 아니고, 필터를 되살리면 self 인 사람은 다시 자기 한 줄만 보게 된다.
                //   ★ 본인도 이 목록에 그대로 들어 있다(따로 담지 않는다 — 두 경로가 되면 한쪽이 낡는다).
                var members = new List<Dictionary<string, object?>>();
                //   ★ 소속 이름은 JOIN 으로 만든다. 정렬 첫 키도 그 이름이다.
                //   ★ 2026-09-10 — 정렬에 u.sort_order 가 끼었다(USER-ADMIN §5.3):
                //       ORDER BY o.name, u.sort_order IS NULL, u.sort_order, u.name
                //     소속 → 순번(NULL 은 맨 뒤) → 이름. **직급 서열을 여기 끼우지 않는다** —
                //     전사 서열이 직급을 이미 담고 있고, 끼우면 관리자가 정한 순서를 직급이 뒤엎는다.
                //     화면은 이 순서를 **그대로** 그린다(renderMembers 에 sort 가 없다) — 두 곳이면 갈린다.
                //     소속 없는 사람(NULL)이 앞에 오는 것은 종전과 같다(MySQL 은 ASC 에서 NULL 이 먼저다).
                //   ★ 2026-09-10 — 「구성원 편집」은 flatOrder=true 로 부른다(사용자 결정: 편집은 팀과 무관하게 서열로).
                //       ORDER BY u.sort_order IS NULL, u.sort_order, u.name
                //     서열의 주인은 편집 화면이고 전사 단일 서열이라 소속을 첫 키로 두면 그 서열이 팀에 갇힌다.
                //     보기 화면은 종전대로 소속 → 순번 → 이름. 정렬은 여기 두 리터럴뿐이다 — 화면은 어느 쪽도 재정렬하지 않는다.
                //   ★ WHERE 는 두 갈래다. 퇴사자 포함은 관리자 전용이고, 그 판정은 이미 위에서
                //     이 연결로 읽은 edit_role 이 했다(withInactive). 웹이 보낸 플래그만으로는 열리지 않는다.
                //   ★ 직급 이름도 JOIN 으로 만든다(app_user 에는 title_id 뿐). 별칭 title 은 종전 payload 키 그대로다.
                string rosterSql =
                    "SELECT u.user_id, u.login_id, u.name, t.name AS title, u.title_id, u.sort_order, u.org_id, " +
                    "o.name AS org_unit, u.view_scope, u.edit_role, u.is_active " +
                    "FROM app_user u LEFT JOIN org_unit o ON o.org_id = u.org_id " +
                    "LEFT JOIN title_code t ON t.title_id = u.title_id " +
                    (withInactive ? "" : "WHERE u.is_active=1 ") +
                    (flatOrder ? "ORDER BY u.sort_order IS NULL, u.sort_order, u.name"
                               : "ORDER BY o.name, u.sort_order IS NULL, u.sort_order, u.name");
                await using (var cmd = new MySqlCommand(rosterSql, conn))
                {
                    await using var rd = await cmd.ExecuteReaderAsync(cts.Token);
                    while (await rd.ReadAsync(cts.Token))
                    {
                        string ou = Str(rd, "org_unit");
                        var row = new Dictionary<string, object?>
                        {
                            ["loginId"] = Str(rd, "login_id"),
                            ["name"]    = Str(rd, "name"),
                            ["title"]   = Str(rd, "title"),
                            ["orgUnit"] = ou,
                            // 이 한 값이 곧 열람 범위다 — 화면은 이걸 보고 행을 누를 수 있게 할지 정한다.
                            ["canViewSchedule"] = allowed.Contains(ou),
                        };
                        // ★ 관리자에게만 더 싣는다. 비관리자 회신은 위 5키에서 한 글자도 늘지 않는다.
                        if (isAdmin)
                        {
                            row["userId"]    = IntOrNull(rd, "user_id");
                            row["orgId"]     = IntOrNull(rd, "org_id");     // 소속 없으면 null(드롭다운의 '(없음)')
                            row["titleId"]   = IntOrNull(rd, "title_id");   // 직급 드롭다운 값(이름이 아니라 번호)
                            row["viewScope"] = Str(rd, "view_scope");
                            row["editRole"]  = Str(rd, "edit_role");
                            row["isActive"]  = (IntOrNull(rd, "is_active") ?? 0) != 0;
                            row["sortOrder"] = IntOrNull(rd, "sort_order");
                        }
                        members.Add(row);
                    }
                }

                // 직급 목록 — 편집 폼의 드롭다운 소스. 관리자일 때만 읽는다(비관리자에게는 쓸 데가 없다).
                //   ★ 활성만·직급 서열순이다. 저장은 활성 직급 번호만 받는다(UpsertUserAsync) —
                //     드롭다운을 이 목록으로 채우면 '고를 수 없는 값'과 '저장할 수 없는 값'이 같아진다.
                //   ★ [{id, name}] 다(CODE-TABLE-IDS §4). 드롭다운 값은 번호, 글자는 이름이다.
                List<Dictionary<string, object?>>? titles = null;
                if (isAdmin)
                {
                    titles = new List<Dictionary<string, object?>>();
                    await using var cmd = new MySqlCommand(
                        "SELECT title_id, name FROM title_code WHERE is_active=1 ORDER BY sort_order, name", conn);
                    await using var rd = await cmd.ExecuteReaderAsync(cts.Token);
                    while (await rd.ReadAsync(cts.Token))
                    {
                        string n = Str(rd, "name");
                        if (n.Length > 0) titles.Add(new Dictionary<string, object?> { ["id"] = IntOrNull(rd, "title_id"), ["name"] = n });
                    }
                }

                var unitPayload = new List<Dictionary<string, object?>>(units.Count);
                foreach (var u in units)
                {
                    var un = new Dictionary<string, object?>
                    {
                        ["name"]      = u.Name,
                        ["parent"]    = u.Parent.Length == 0 ? null : u.Parent,   // 최상위는 null(웹이 루트로 읽는다)
                        ["sortOrder"] = u.SortOrder,
                        // ★ allowed 는 싣지 않는다 — 트리는 전부 활성이라 노드에 붙일 범위 개념이 없다.
                    };
                    if (isAdmin) un["orgId"] = u.OrgId;   // 편집 폼의 소속 드롭다운 값(이름이 아니라 번호)
                    unitPayload.Add(un);
                }
                _log("DB 구성원 조회: " + id + " (" + scope + "/" + (myActive != 0 ? "활성" : "비활성") +
                     (isAdmin ? "/관리자" : "") + ") 유닛 " + unitPayload.Count + "건 · 일정 열람 가능 유닛 " +
                     allowed.Count + "건 · 구성원 " + members.Count + "명");
                var payload = new Dictionary<string, object?>
                {
                    ["found"]   = true,
                    ["scope"]   = scope,
                    ["myUnit"]  = myUnit,
                    ["units"]   = unitPayload,
                    ["members"] = members,
                };
                // ★ 관리자에게만 더 싣는다 — 비관리자 회신은 위 5키 그대로다(키 순서까지 종전과 같다).
                if (isAdmin)
                {
                    payload["admin"] = true;
                    payload["titles"] = titles;                 // 활성 직급(드롭다운 소스)
                    payload["includeInactive"] = withInactive;  // 퇴사자 보기 상태 — 화면 토글이 이 값으로 자기를 맞춘다
                }
                return JsonSerializer.Serialize(payload);
            }
            catch (Exception ex) { DbErrors.Observe(ex, _log); _log("DB 구성원 조회 실패(" + id + "): " + Short(ex)); return null; }
        }

    private static void ExpandUnitTree(List<OrgUnitRow> units, string myUnit, HashSet<string> allowed)
        {
            string root = (myUnit ?? "").Trim();
            if (root.Length == 0) return;

            var children = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var u in units)
            {
                if (u.Parent.Length == 0) continue;
                if (!children.TryGetValue(u.Parent, out var lst)) { lst = new List<string>(); children[u.Parent] = lst; }
                lst.Add(u.Name);
            }

            var queue = new Queue<string>();
            allowed.Add(root);           // 내 소속은 org_unit 에 없더라도(비활성 유닛 등) 항상 내 범위다
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                string cur = queue.Dequeue();
                if (!children.TryGetValue(cur, out var kids)) continue;
                foreach (var k in kids)
                {
                    if (allowed.Add(k)) queue.Enqueue(k);   // 방문 집합 가드
                }
            }
        }

    private static async Task ApplyPreambleAsync(MySqlConnection conn, string sql, CancellationToken ct)
        {
            try
            {
                await using var pre = conn.CreateCommand();
                pre.CommandText = sql;
                await pre.ExecuteNonQueryAsync(ct);
            }
            catch { await conn.DisposeAsync(); throw; }
        }

    private static async Task<MySqlConnection> OpenReadAsync(CancellationToken ct)
        {
            //  연결은 버전 관문(DbGate)이 연다. 장애 주입, 열기 실패 정리, 구조 번호 판정이 모두 그 안에 있다.
            var conn = await DbGate.OpenAsync(BuildConnString(), ct);
            await ApplyPreambleAsync(conn, ReadPreambleSql, ct);   // 프리앰블은 연 연결마다 — 예외를 두면 그 자리가 규약 밖이 된다
            return conn;
        }

    private static string Str(DbDataReader rd, string col)
        {
            int i = rd.GetOrdinal(col);
            return rd.IsDBNull(i) ? "" : (rd.GetValue(i)?.ToString() ?? "");
        }

    private static int? IntOrNull(DbDataReader rd, string col)
        {
            int i = rd.GetOrdinal(col);
            if (rd.IsDBNull(i)) return null;
            try { return Convert.ToInt32(rd.GetValue(i)); } catch { return null; }
        }

    private static string Short(Exception ex)
        {
            string m = ex.Message ?? ex.GetType().Name;
            m = m.Replace("\r", " ").Replace("\n", " ").Trim();
            return m.Length > 120 ? m.Substring(0, 120) + "…" : m;
        }
}
