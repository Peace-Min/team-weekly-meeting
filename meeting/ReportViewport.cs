namespace TeamWeeklyMeeting;

internal static class ReportViewport
{
    // Move the original content cells, preserving their styles, links and form handlers.
    internal const string Crop = """
        (() => {
          if (document.getElementById('meeting-report-viewport')) return true;
          const label = [...document.querySelectorAll('td')].find(td =>
            (td.querySelector('div')?.innerText || td.innerText || '').replace(/\s+/g,' ').trim() === '진행사항');
          if (!label) return false;
          const report = label.closest('table');
          const region = report.closest('td[width="600"][valign="top"]');
          const viewport = document.createElement('div'); viewport.id='meeting-report-viewport';
          if (region) {
            const outer = region.closest('table');
            const title = [...outer.querySelectorAll('td[width="600"][height="35"]')]
              .find(td => td.closest('table') === outer);
            const frame = document.createElement('table');
            frame.setAttribute('width','600'); frame.setAttribute('border','0');
            frame.setAttribute('cellpadding','0'); frame.setAttribute('cellspacing','0');
            const tbody = document.createElement('tbody'); frame.append(tbody);
            for (const cell of [title,region].filter(Boolean)) {
              const row=document.createElement('tr'); row.append(cell); tbody.append(row);
            }
            viewport.append(frame);
          } else viewport.append(report);
          const style=document.createElement('style'); style.id='meeting-report-crop-style';
          style.textContent='body{margin:0!important;padding:0!important;background:#fff!important}'
            +'body > :not(#meeting-report-viewport){display:none!important}'
            +'#meeting-report-viewport{display:block;margin:0;padding:0;width:100%;box-sizing:border-box}'
            +'#meeting-report-viewport > table{margin:0!important}'
            +'#meeting-report-viewport td[height="35"] > img[width="600"]{display:block;width:100%!important;height:35px!important}'
            +'#meeting-report-viewport > table,#meeting-report-viewport table[width="600"],'
            +'#meeting-report-viewport table[width="560"]{width:100%!important;max-width:100%!important}'
            +'#meeting-report-viewport td[width="600"],#meeting-report-viewport td[width="560"]{width:auto!important}';
          document.head.append(style); document.body.append(viewport);
          window.scrollTo(0,0);
          return true;
        })()
        """;
}
