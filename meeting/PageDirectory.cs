using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace TeamWeeklyMeeting;

internal sealed partial class MeetingWindow
{
    // Read the weekly-report menu, not the duplicated attendance menu or report form.
    internal const string DirectoryExtractFunction = """
        ((doc, loginId) => {
          if (doc.querySelector('input[type=password]')) return {found:false,reason:'login-required'};
          const menu = doc.querySelector('#submenu1');
          if (!menu) return {found:false,reason:'weekly-menu-missing'};
          const clean = value => (value || '').replace(/\s+/g,' ').trim();
          const units = [], members = [], unitNames = new Set(), ids = new Set();
          let section = '', major = '';
          for (const row of menu.querySelectorAll('tr')) {
            const heading = Array.from(row.cells).find(cell => cell.colSpan > 1 && cell.querySelector('b,strong') && !cell.querySelector('a[href]'));
            if (heading) {
              const name = clean(heading.querySelector('b,strong').textContent);
              if (!name) continue;
              const color = (heading.getAttribute('bgcolor') || '').toLowerCase();
              const subgroup = color === '#efefef';
              const parent = subgroup ? major : '';
              if (!subgroup) major = name;
              section = name;
              if (!unitNames.has(name)) {
                unitNames.add(name);
                units.push({name,parent:parent || null,sortOrder:units.length});
              }
              continue;
            }
            if (!section) continue;
            for (const link of row.querySelectorAll('a[href]')) {
              let url;
              try { url = new URL(link.getAttribute('href'), 'https://www.netcus.com/pjm/pjm.jsp'); }
              catch { continue; }
              if (!['www.netcus.com','netcus.com'].includes(url.hostname) || !['https:','http:'].includes(url.protocol) || url.pathname !== '/pjm/pjm.jsp') continue;
              const id = url.searchParams.get('id') || '';
              if (!/^[A-Za-z0-9_.@-]{1,100}$/.test(id) || ids.has(id)) continue;
              const label = clean(link.textContent);
              if (!label) continue;
              const parts = /^(.*?)\s+(\S+)$/.exec(label);
              ids.add(id);
              members.push({loginId:id,name:parts ? parts[1] : label,title:parts ? parts[2] : '',orgUnit:section});
            }
          }
          return {found:units.length > 0 && members.length > 0,
            myUnit:members.find(member => member.loginId === loginId)?.orgUnit || '',units,members};
        })
        """;

    private async Task<MeetingDirectory> ReadPageDirectoryAsync()
    {
        var core = _login.CoreWebView2;
        if (!IsCompanyUrl(core.Source)) throw new InvalidOperationException("사내 페이지에서만 조직도를 읽을 수 있습니다.");
        string json = await core.ExecuteScriptAsync(DirectoryExtractFunction + "(document," + JsonSerializer.Serialize(_loginId) + ")");
        using var parsed = JsonDocument.Parse(json);
        if (!parsed.RootElement.GetProperty("found").GetBoolean())
        {
            string reason = parsed.RootElement.TryGetProperty("reason", out var value) ? value.GetString() ?? "empty-menu" : "empty-menu";
            AppLog.Write("directory.page-failed", new { reason });
            throw new InvalidOperationException("로그인한 페이지의 주간보고 조직 메뉴를 읽지 못했습니다. 로그인 상태와 페이지 구성을 확인하세요.");
        }
        var directory = MeetingDirectory.Parse(json);
        AppLog.Write("directory.page", new { units=directory.Units.Length, members=directory.Members.Length });
        return directory;
    }
}
