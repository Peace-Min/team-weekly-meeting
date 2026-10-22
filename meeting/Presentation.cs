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
    private readonly Dictionary<WebView2, Action> _zoomWatchers = new();
    private readonly TextBlock _presentationEmpty = new() { FontFamily = new FontFamily("Malgun Gothic"), FontSize = 24,
        TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center, MaxWidth = 800, Margin = new Thickness(40) };

    private async Task AttachReportControls(WebView2 web)
    {
        var core = web.CoreWebView2;
        await core.AddScriptToExecuteOnDocumentCreatedAsync(PresentationInput);
        core.WebMessageReceived += (_, e) =>
        {
            if (_closing || !IsCompanyUrl(e.Source) || _people.SelectedIndex < 0
                || _reports[_people.SelectedIndex].Web != web) return;
            try
            {
                using var json = JsonDocument.Parse(e.WebMessageAsJson);
                string? kind=json.RootElement.GetProperty("kind").GetString();
                if(kind=="meeting-input")
                {
                    string? input=json.RootElement.GetProperty("action").GetString();
                    if(input is "drag" or "wheel" or "click")
                        AppLog.Write("browser.input",new{input,member=_reports[_people.SelectedIndex].Member.Id,
                            x=json.RootElement.GetProperty("x").GetDouble(),y=json.RootElement.GetProperty("y").GetDouble(),presentation=_presentation!=null});
                    return;
                }
                if (_presentation==null || kind != "meeting-presentation-key") return;
                string key = json.RootElement.GetProperty("key").GetString() ?? "";
                Dispatcher.BeginInvoke(new Action(() => HandlePresentationKey(key)));
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or System.Collections.Generic.KeyNotFoundException) { }
        };
        core.NavigationCompleted += async (_, e) =>
        { if (e.IsSuccess && _presentation != null) await SetPresentationFlagAsync(web, true); };
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
            await web.CoreWebView2.ExecuteScriptAsync("window.__meetingPresentation && window.__meetingPresentation(" + (enabled ? "true" : "false") + ")");
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
        surface.Children.Add(_pages); surface.Children.Add(_presentationEmpty);
        var window = new Window { Owner = this, Title = "팀 주간보고", Content = surface, WindowStyle = WindowStyle.None,
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
            surface.Children.Remove(_pages); surface.Children.Remove(_presentationEmpty);
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
        _presentationEmpty.Visibility = page.Ready ? Visibility.Hidden : Visibility.Visible;
        _presentationEmpty.Text = page.Member.Name + "\n\n" + page.Detail;
        _presentation.Title = page.Member.Name + " · 팀 주간보고";
        if (page.Ready) page.Web.Focus(); else _presentation.Focus();
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
            document.documentElement.style.removeProperty('cursor');
          }
          window.__meetingPresentation = enabled => {
            active=enabled;stopDrag();document.documentElement.classList.toggle('meeting-presenting',enabled);
            if(!document.getElementById('meeting-presentation-style')){
              const style=document.createElement('style');style.id='meeting-presentation-style';
              style.textContent='html.meeting-presenting{user-select:none;cursor:grab}'
                +'html.meeting-presenting #meeting-report-viewport input[type=button],'
                +'html.meeting-presenting #meeting-report-viewport input[type=submit],'
                +'html.meeting-presenting #meeting-report-viewport button{display:none!important}';
              document.head.append(style);
            }
          };
          document.addEventListener('keydown',e=>{
            if(!active || (e.ctrlKey || e.altKey || e.metaKey) && e.key!=='Escape')return;
            if(!['Escape','ArrowLeft','ArrowRight','ArrowUp','ArrowDown','PageUp','PageDown','Home','End'].includes(e.key))return;
            e.preventDefault();e.stopImmediatePropagation();
            if(!e.repeat)chrome.webview.postMessage({kind:'meeting-presentation-key',key:e.key});
          },true);
          document.addEventListener('pointerdown',e=>{
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
          document.addEventListener('click',e=>{inputLog('click');if(active){e.preventDefault();e.stopImmediatePropagation()}},true);
        })()
        """;
}
