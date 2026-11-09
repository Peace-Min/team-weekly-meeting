using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Wpf;

namespace TeamWeeklyMeeting;

internal sealed partial class MeetingWindow
{
    private Window? _presentation;
    private StackPanel? _presentationFallback;
    private Button[] _fallbackNavigation = Array.Empty<Button>();
    private readonly Dictionary<WebView2, Action> _zoomWatchers = new();
    private readonly TextBlock _presentationEmpty = new() { FontFamily = new FontFamily("Malgun Gothic"), FontSize = 24,
        TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center, MaxWidth = 800, Margin = new Thickness(40), Focusable = true };

    private async Task AttachReportControls(WebView2 web)
    {
        var core = web.CoreWebView2;
        await core.AddScriptToExecuteOnDocumentCreatedAsync(PresentationInput);
        await core.AddScriptToExecuteOnDocumentCreatedAsync(AnnotationInput);
        core.WebMessageReceived += (_, e) =>
        {
            if (_closing || !IsCompanyUrl(e.Source) || _people.SelectedIndex < 0 || _people.SelectedIndex >= _reports.Count
                || _reports[_people.SelectedIndex].Web != web) return;
            try
            {
                using var json = JsonDocument.Parse(e.WebMessageAsJson);
                string? kind=json.RootElement.GetProperty("kind").GetString();
                if(kind=="meeting-annotation")
                {
                    string? action=json.RootElement.GetProperty("action").GetString();
                    if(_presentation!=null && action is "mode" or "stroke" or "erase" or "undo" or "clear")
                        AppLog.Write("presentation.annotation",new{action,mode=json.RootElement.GetProperty("mode").GetString(),count=json.RootElement.GetProperty("count").GetInt32(),member=_reports[_people.SelectedIndex].Member.Id});
                    return;
                }
                if(kind=="meeting-input")
                {
                    string? input=json.RootElement.GetProperty("action").GetString();
                    if(input is "drag" or "wheel" or "click")
                        AppLog.Write("browser.input",new{input,member=_reports[_people.SelectedIndex].Member.Id,
                            x=json.RootElement.GetProperty("x").GetDouble(),y=json.RootElement.GetProperty("y").GetDouble(),presentation=_presentation!=null});
                    return;
                }
                if (_presentation == null) return;
                if (kind == "meeting-presentation-step")
                {
                    if (json.RootElement.GetProperty("direction").GetString() is string direction)
                    {
                        if (direction == "previous") MovePresentationByMouse(-1);
                        if (direction == "next") MovePresentationByMouse(1);
                    }
                    return;
                }
                if (kind != "meeting-presentation-key") return;
                string key = json.RootElement.GetProperty("key").GetString() ?? "";
                Dispatcher.BeginInvoke(new Action(() => HandlePresentationKey(key)));
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or System.Collections.Generic.KeyNotFoundException) { }
        };
        void QueueZoom()
        {
            if (_applyingZoom || _closing) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_closing || !_reports.Any(p => p.Web == web)) return;
                int percent = Math.Clamp((int)Math.Round(web.ZoomFactor * 100), 25, 500);
                if (percent != _zoomPercent) _ = ZoomAsync(percent - _zoomPercent);
            }));
        }
        web.ZoomFactorChanged += (_, _) => QueueZoom();
        var descriptor = DependencyPropertyDescriptor.FromProperty(WebView2.ZoomFactorProperty, typeof(WebView2));
        EventHandler changed = (_, _) => QueueZoom();
        descriptor.AddValueChanged(web, changed);
        _zoomWatchers[web] = () => descriptor.RemoveValueChanged(web, changed);
    }

    private async Task SetPresentationFlagAsync(WebView2 web, bool enabled)
    {
        try
        {
            web.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = !enabled;
            web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = !enabled;
            web.CoreWebView2.Settings.IsZoomControlEnabled = !enabled;
            int index = _reports.FindIndex(page => page.Web == web);
            string labels = enabled && index >= 0
                ? "," + JsonSerializer.Serialize(PresentationLabel(index - 1)) + "," + JsonSerializer.Serialize(PresentationLabel(index + 1))
                : "";
            await web.CoreWebView2.ExecuteScriptAsync("window.__meetingPresentation && window.__meetingPresentation(" + (enabled ? "true" : "false") + labels + ")");
            await web.CoreWebView2.ExecuteScriptAsync("window.__meetingAnnotations && window.__meetingAnnotations(" + (enabled ? "true" : "false") + "," + (1/web.ZoomFactor).ToString(System.Globalization.CultureInfo.InvariantCulture) + ")");
        }
        catch (Exception ex) { AppLog.Write("presentation.input-failed",error:ex); if (!_closing) _status.Text = "보고 모드 입력 설정 실패: " + ex.Message; }
    }

    private async Task StartPresentationAsync()
    {
        AppLog.Write("presentation.start",Snapshot());
        if (_reports.Count == 0) throw new InvalidOperationException("참석자 보고서를 먼저 준비하세요.");
        if (_presentation != null) { _presentation.Activate(); return; }
        foreach (var page in _reports) page.Detached?.Close();
        _viewer.Children.Remove(_pages);
        var surface = new Grid { Background = Brushes.White };
        var fallback = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        fallback.Children.Add(_presentationEmpty);
        var navigation = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        _fallbackNavigation = new[] { FallbackButton("이전", -1), FallbackButton("다음", 1) };
        foreach (var button in _fallbackNavigation) navigation.Children.Add(button);
        fallback.Children.Add(navigation);
        _presentationFallback = fallback;
        surface.Children.Add(_pages); surface.Children.Add(fallback);
        var window = new Window { Owner = this, Icon=Icon, Title = "팀 주간보고", Content = surface, WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize, WindowState = WindowState.Maximized, Background = Brushes.White };
        _presentation = window;
        window.PreviewKeyDown += (_, e) =>
        {
            // Focused browser keys are captured inside the document.
            if (_reports.Any(p => p.Web.IsKeyboardFocusWithin)) return;
            if (HandlePresentationKey(e.Key.ToString())) e.Handled = true;
        };
        window.Closed += async (_, _) =>
        {
            AppLog.Write("presentation.end",Snapshot());
            _presentation = null;
            surface.Children.Remove(_pages); fallback.Children.Remove(_presentationEmpty);
            _presentationFallback = null; _fallbackNavigation = Array.Empty<Button>();
            _pages.Visibility = Visibility.Visible; _viewer.Children.Add(_pages);
            foreach (var page in _reports.ToArray()) await SetPresentationFlagAsync(page.Web, false);
            if (!_closing) { Activate(); SelectPerson(); }
        };
        if (_people.SelectedIndex < 0) _people.SelectedIndex = 0;
        foreach (var page in _reports)
        {
            page.Ready = page.Web.CoreWebView2 != null
                && await page.Web.CoreWebView2.ExecuteScriptAsync("!!document.getElementById('meeting-report-viewport')") == "true";
            await SetPresentationFlagAsync(page.Web, true);
        }
        window.Show(); UpdatePresentationPage();
        _status.Text = "보고 모드 · ←/→ 발표자 이동 · 드래그/휠 스크롤 · Esc 종료";
    }

    private void UpdatePresentationPage()
    {
        if (_presentation == null || _people.SelectedIndex < 0) return;
        var page = _reports[_people.SelectedIndex];
        _pages.Visibility = page.Ready ? Visibility.Visible : Visibility.Hidden;
        _presentationEmpty.Visibility = _presentationFallback!.Visibility = page.Ready ? Visibility.Hidden : Visibility.Visible;
        for (int i = 0; i < _fallbackNavigation.Length; i++)
        {
            var label = PresentationLabel(_people.SelectedIndex + (i == 0 ? -1 : 1));
            _fallbackNavigation[i].Visibility = label == null ? Visibility.Hidden : Visibility.Visible;
            ((TextBlock)((StackPanel)_fallbackNavigation[i].Content).Children[1]).Text = label ?? "";
        }
        _presentationEmpty.Text = page.Member.Name + "\n\n" + page.Detail;
        _presentation.Title = page.Member.Name + " · 팀 주간보고";
        if (page.Ready) page.Web.Focus(); else _presentationEmpty.Focus();
    }

    private string? PresentationLabel(int index)
    {
        if (index < 0 || index >= _reports.Count) return null;
        var member = _reports[index].Member;
        return member.DisplayLabel.Length > 0 ? member.DisplayLabel : member.Name;
    }

    private Button FallbackButton(string heading, int step)
    {
        var text = new StackPanel { Width = 134 };
        text.Children.Add(new TextBlock { Text = heading, FontSize = 15, FontWeight = FontWeights.Bold });
        text.Children.Add(new TextBlock { FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("Ink"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
        var button = new Button { Content = text, Width = 160, MinHeight = 44, Margin = new Thickness(6, 0, 6, 0) };
        if (step > 0) { button.Background = (Brush)FindResource("BrandSoft"); button.BorderBrush = (Brush)FindResource("BrandOrange"); }
        button.Click += (_, _) => MovePresentationByMouse(step);
        return button;
    }

    private void MovePresentationByMouse(int step)
    {
        if (_presentation == null || _closing || _people.SelectedIndex < 0) return;
        int from = _people.SelectedIndex, target = from + step;
        if (target < 0 || target >= _reports.Count) return;
        AppLog.Write("presentation.navigate", new { from, target });
        _people.SelectedIndex = target;
    }

    private bool HandlePresentationKey(string key)
    {
        if (_presentation == null || _closing) return false;
        AppLog.Write("presentation.key",new{key,index=_people.SelectedIndex});
        if (key is "Escape" or "Esc") { _presentation.Close(); return true; }
        int index = _people.SelectedIndex;
        int next = key switch
        {
            "ArrowLeft" or "Left" or "ArrowUp" or "Up" or "PageUp" => index - 1,
            "ArrowRight" or "Right" or "ArrowDown" or "Down" or "PageDown" => index + 1,
            "Home" => 0, "End" => _reports.Count - 1, _ => -999
        };
        if (next == -999) return false;
        _people.SelectedIndex = Math.Clamp(next, 0, _reports.Count - 1);
        return true;
    }

    private const string PresentationInput = """
        (() => {
          let active=false, drag=null;
          const inputLog=action=>chrome.webview.postMessage({kind:'meeting-input',action,x:scrollX,y:scrollY});
          let wheelTimer;
          document.addEventListener('wheel',()=>{clearTimeout(wheelTimer);wheelTimer=setTimeout(()=>inputLog('wheel'),200)},{passive:true});
          function stopDrag(){
            const previous=drag;drag=null;
            if(previous){inputLog('drag');try{document.documentElement.releasePointerCapture(previous.id)}catch{}}
            if(!window.__meetingDrawingActive?.())document.documentElement.style.removeProperty('cursor');
          }
          window.__meetingPresentation = (enabled, previous, next) => {
            active=enabled;stopDrag();document.documentElement.classList.toggle('meeting-presenting',enabled);
            if(!document.getElementById('meeting-presentation-style')){
              const style=document.createElement('style');style.id='meeting-presentation-style';
              style.textContent='html.meeting-presenting{user-select:none;cursor:grab}'
                +'html.meeting-presenting #meeting-report-viewport input[type=button],'
                +'html.meeting-presenting #meeting-report-viewport input[type=submit],'
                +'html.meeting-presenting #meeting-report-viewport button:not([data-meeting-tool]){display:none!important}'
                +'#meeting-presenter-footer{display:grid;grid-template-columns:repeat(2,minmax(0,160px));justify-content:center;gap:12px;zoom:var(--meeting-ui-scale,1);margin-top:24px;padding:13px 13px var(--meeting-footer-clearance,80px);border-top:1px solid #d9dde2}'
                +'#meeting-presenter-footer button{min-height:44px;padding:6px 12px;border:1px solid #c5cdd4;border-radius:6px;text-align:left;background:#fff;color:#282b30;cursor:pointer;font:15px/1.4 "Malgun Gothic",sans-serif;overflow-wrap:anywhere}'
                +'#meeting-presenter-footer button:last-child{background:#fff0e3;border-color:#b86831;color:#873800}'
                +'#meeting-presenter-footer button:hover{background:#ffe0c2}'
                +'#meeting-presenter-footer button:focus-visible{outline:2px solid #873800;outline-offset:2px}'
                +'#meeting-presenter-footer strong,#meeting-presenter-footer small{display:block}'
                +'#meeting-presenter-footer small{margin-top:3px;color:#282b30;font-size:16px;font-weight:600;white-space:pre-wrap}';
              document.head.append(style);
            }
            document.getElementById('meeting-presenter-footer')?.remove();
            const viewport=document.getElementById('meeting-report-viewport');
            if(!enabled||!viewport||(!previous&&!next))return;
            viewport.tabIndex=-1;
            const footer=document.createElement('nav');
            footer.id='meeting-presenter-footer';footer.dataset.meetingTools='';
            footer.setAttribute('aria-label','발표자 이동');
            for(const [heading,label,direction] of [['이전',previous,'previous'],['다음',next,'next']]){
              const button=document.createElement('button');
              button.type='button';button.dataset.meetingTool='';button.dataset.direction=direction;
              if(!label)button.style.visibility='hidden';
              const title=document.createElement('strong'),subtitle=document.createElement('small');
              title.textContent=heading;subtitle.textContent=label||'';
              button.append(title,subtitle);
              button.addEventListener('click',()=>{
                if(!active||!label)return;
                viewport.focus({preventScroll:true});
                chrome.webview.postMessage({kind:'meeting-presentation-step',direction});
              });
              footer.append(button);
            }
            viewport.append(footer);
          };
          document.addEventListener('keydown',e=>{
            if(!active || (e.ctrlKey || e.altKey || e.metaKey) && e.key!=='Escape')return;
            if(!['Escape','ArrowLeft','ArrowRight','ArrowUp','ArrowDown','PageUp','PageDown','Home','End'].includes(e.key))return;
            e.preventDefault();e.stopImmediatePropagation();
            if(!e.repeat)chrome.webview.postMessage({kind:'meeting-presentation-key',key:e.key});
          },true);
          document.addEventListener('pointerdown',e=>{
            if(e.target.closest('[data-meeting-tools]') || window.__meetingDrawingActive?.())return;
            if(!active || e.button!==0)return;
            e.preventDefault();e.stopImmediatePropagation();drag={id:e.pointerId,x:e.clientX,y:e.clientY};
            try{document.documentElement.setPointerCapture(e.pointerId)}catch{}
            document.documentElement.style.cursor='grabbing';
          },true);
          document.addEventListener('pointermove',e=>{
            if(!active || !drag || e.pointerId!==drag.id)return;
            e.preventDefault();window.scrollBy(drag.x-e.clientX,drag.y-e.clientY);
            drag.x=e.clientX;drag.y=e.clientY;
          },true);
          for(const name of ['pointerup','pointercancel','lostpointercapture'])document.addEventListener(name,stopDrag,true);
          document.addEventListener('click',e=>{if(e.target.closest('[data-meeting-tools]'))return;inputLog('click');if(active){e.preventDefault();e.stopImmediatePropagation()}},true);
        })()
        """;
}
