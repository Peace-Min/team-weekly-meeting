using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using static TeamWeeklyMeeting.NetcusText;

namespace TeamWeeklyMeeting;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        bool smoke = args.Contains("--smoke-test");
        using var mutex = new Mutex(true, "TeamWeeklyMeeting" + (smoke ? "Smoke" : ""), out bool first);
        if (!first) { MessageBox.Show("팀 주간보고 프로그램이 이미 실행 중입니다."); return; }
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        app.DispatcherUnhandledException += (_, e) => AppLog.Write("app.dispatcher-failed", error:e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => AppLog.Write("app.unhandled", error:e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => AppLog.Write("app.task-failed", error:e.Exception);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => AppLog.Write("app.process-exit");
        if (smoke)
        {
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "TeamWeeklyMeeting-smoke-result.txt"), "RUNNING\n");
            app.DispatcherUnhandledException += (_, e) =>
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "TeamWeeklyMeeting-smoke-result.txt"), "FAIL\n" + e.Exception);
                e.Handled = true;
                app.Shutdown(1);
            };
        }
        try { app.Run(new MeetingWindow(smoke)); }
        catch(Exception ex){ AppLog.Write("app.fatal", error:ex); Environment.ExitCode=1; throw; }
    }
}

internal sealed partial class MeetingWindow : Window
{
    internal const string Home = "https://www.netcus.com/pjm/pjm.jsp";
    private readonly bool _smoke;
    private readonly string _dataDir;
    private readonly TextBlock _status = new() { Text = "로그인한 뒤 팀원을 선택하세요.", TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _heading = new() { Text = "사내 근태관리 로그인", FontSize = 18, FontWeight = FontWeights.SemiBold };
    private readonly TextBlock _details = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray };
    private readonly ListBox _people = new() { FontSize = 15, Margin = new Thickness(0, 8, 0, 8) };
    private readonly Grid _pages = new() { Background = Brushes.White };
    private readonly List<ReportPage> _reports = new();
    private readonly List<Button> _commands = new();
    private readonly WebView2 _login = new();
    private readonly DockPanel _viewer = new();
    private CoreWebView2Environment? _environment;
    private bool _busy, _closing;

    private sealed class ReportPage
    {
        internal readonly Member Member;
        internal readonly WebView2 Web = new() { Visibility = Visibility.Hidden };
        internal string Detail = "준비 중…";
        internal Window? Detached;
        internal bool Ready;
        internal ReportPage(Member member) { Member = member; }
    }

    internal MeetingWindow(bool smoke)
    {
        _smoke = smoke;
        _dataDir = smoke ? Path.Combine(Path.GetTempPath(), "TeamWeeklyMeeting-smoke-" + Guid.NewGuid().ToString("N"))
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TeamWeeklyMeeting");
        AppLog.Initialize(_dataDir);
        _auth=new AuthStore(Path.Combine(_dataDir,"autologin.dat"));
        ApplyBranding();
        Title = "넷커스터마이즈 · 팀 주간보고 v"+AppVersion;
        Width = 1320; Height = 900; MinWidth = 1000; MinHeight = 720;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new FontFamily("Malgun Gothic"); FontSize = 13;
        Foreground = new SolidColorBrush(Color.FromRgb(40,43,48));
        Background = new SolidColorBrush(Color.FromRgb(243,244,246));
        var root = new DockPanel(); Content=root;
        var header=BuildBrandHeader();DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        _status.FontSize=12;_status.Foreground=new SolidColorBrush(Color.FromRgb(94,101,111));
        var footer=new Border{Padding=new Thickness(20,8,20,8),Child=_status,Background=Brushes.White};
        DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        var side=new Border{Child=BuildSidebar(),Background=Brushes.White,BorderBrush=(Brush)FindResource("Line"),BorderThickness=new Thickness(0,0,1,0)};
        DockPanel.SetDock(side,Dock.Left);root.Children.Add(side);
        _people.SelectionChanged += (_, _) => SelectPerson();
        var main=_viewer;main.Margin=new Thickness(20,16,20,12);root.Children.Add(main);
        var toolbar=BuildViewerToolbar();DockPanel.SetDock(toolbar,Dock.Top);main.Children.Add(toolbar);
        var zoom=new WrapPanel{Margin=new Thickness(0,0,0,14)};AddZoomInputs(zoom);
        DockPanel.SetDock(zoom,Dock.Top);main.Children.Add(zoom);
        var caption=new StackPanel{Margin=new Thickness(0,0,0,12)};
        caption.Children.Add(_heading);caption.Children.Add(_details);DockPanel.SetDock(caption,Dock.Top);main.Children.Add(caption);
        main.Children.Add(_pages);_pages.Children.Add(_login);
        Loaded += async (_, _) => await RunAsync(InitializeAsync);
        Closing += (_, e) =>
        {
            if (!_closing && _busy && !_smoke && MessageBox.Show("보고서를 준비 중입니다. 종료할까요?", Title,
                    MessageBoxButton.YesNo) != MessageBoxResult.Yes) { e.Cancel = true; return; }
            _closing = true;
            AppLog.Write("app.close", Snapshot());
            PersistSettings();
            _presentation?.Close();
            ClearReports(); _login.Dispose();
        };
    }

    private Button Command(string label, Func<Task> action)
    {
        var button = new Button { Content = label, Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(0, 4, 6, 0), HorizontalContentAlignment = HorizontalAlignment.Center };
        if(label == "보고 시작")button.Style=(Style)FindResource("PrimaryButton");
        button.Click += async (_, _) => await RunAsync(action,label);
        _commands.Add(button);
        return button;
    }

    private async Task RunAsync(Func<Task> action,string? label=null)
    {
        if (_busy || _closing) return;
        _busy = true;
        string operation=label??action.Method.Name;
        var timer=System.Diagnostics.Stopwatch.StartNew();
        AppLog.Write("action.begin",new{operation,state=Snapshot()});
        foreach (var command in _commands) command.IsEnabled = false;
        _team.IsEnabled = false; _attendance.IsEnabled = false; _people.IsEnabled = false;
        _zoomInput.IsEnabled = false; _stepInput.IsEnabled = false;
        try { await action(); }
        catch (Exception ex) when (!_closing)
        {
            AppLog.Write("action.failed",new{operation,elapsedMs=timer.ElapsedMilliseconds,state=Snapshot()},ex);
            _status.Text = "진행하지 못했습니다: " + ex.Message;
            if (!_smoke) MessageBox.Show(ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            else
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "TeamWeeklyMeeting-smoke-result.txt"), "FAIL\n" + ex);
                Environment.ExitCode = 1;
                Close();
            }
        }
        catch (Exception) when (_closing) { }
        finally
        {
            AppLog.Write("action.end",new{operation,elapsedMs=timer.ElapsedMilliseconds,state=Snapshot()});
            _busy = false;
            if (!_closing)
            {
                foreach (var command in _commands) command.IsEnabled = _environment != null;
                UpdateReportStartButton();
                _team.IsEnabled = true; _attendance.IsEnabled = true; _people.IsEnabled = true;
                _zoomInput.IsEnabled = true; _stepInput.IsEnabled = true;
            }
        }
    }

    private async Task InitializeAsync()
    {
        Directory.CreateDirectory(_dataDir);
        LoadMeetingSettings();
        _environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(_dataDir, "WebView2"));
        await InitializeWebAsync(_login);
        if (_smoke)
        {
            await NavigateAsync(_login.CoreWebView2, () => _login.CoreWebView2.Navigate(Home));
            await SmokeTestAsync();
        }
        else if (await AuthenticateAsync()) await LoadDirectoryAsync();
        else { _closing = true; Close(); }
    }

    private async Task InitializeWebAsync(WebView2 web)
    {
        await web.EnsureCoreWebView2Async(_environment);
        if (web != _login) web.ZoomFactor = _zoomPercent / 100.0;
        if (web != _login) await AttachReportControls(web);
        var core = web.CoreWebView2;
        core.Profile.IsPasswordAutosaveEnabled=false;core.Profile.IsGeneralAutofillEnabled=false;
        core.NavigationCompleted += (_, e) => AppLog.Write("browser.navigation-end",new{role=web==_login?"login":"report",url=LogUrl(core.Source),e.NavigationId,e.IsSuccess,httpStatus=e.HttpStatusCode,error=e.WebErrorStatus.ToString()});
        core.ProcessFailed += (_, e) => AppLog.Write("browser.process-failed",new{kind=e.ProcessFailedKind.ToString(),state=Snapshot()});
        if (web == _login) await TrackLoginIdAsync(core);
        core.Settings.AreDefaultScriptDialogsEnabled = true;
        if (web != _login)
        {
            core.NavigationCompleted += async (_, e) =>
            {
                if (!e.IsSuccess || _closing || !Uri.TryCreate(core.Source, UriKind.Absolute, out var source)
                    || source.AbsolutePath != "/pjm/pjm_view.jsp") return;
                try { await core.ExecuteScriptAsync(ReportViewport.Crop); }
                catch (Exception ex) { if (!_closing) _status.Text = "보고서 영역 표시 실패: " + ex.Message; }
            };
        }
        // Prevent a link from launching an unshared browser session.
        core.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            if (IsCompanyUrl(e.Uri)) core.Navigate(e.Uri);
        };
        core.NavigationStarting += (_, e) =>
        {
            AppLog.Write("browser.navigation-start",new{role=web==_login?"login":"report",url=LogUrl(e.Uri),e.NavigationId});
            if (!IsCompanyUrl(e.Uri) && e.Uri != "about:blank") e.Cancel = true;
        };
        if (_smoke) InstallFixture(core);
    }

    private static bool IsCompanyUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && (uri.Host == "www.netcus.com" || uri.Host == "netcus.com");

    // Navigation handlers always leave on the UI thread, including timeout and script failure.
    private async Task NavigateAsync(CoreWebView2 core, Action start, CancellationToken cancellationToken=default)
    {
        var completion = new TaskCompletionSource<bool>();
        ulong? navigationId = null;
        void Starting(object? s, CoreWebView2NavigationStartingEventArgs e) => navigationId ??= e.NavigationId;
        void Completed(object? s, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.NavigationId == navigationId) completion.TrySetResult(e.IsSuccess && e.HttpStatusCode<400);
        }
        core.NavigationStarting += Starting;
        core.NavigationCompleted += Completed;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();start();
            bool success = await completion.Task.WaitAsync(TimeSpan.FromSeconds(20),cancellationToken);
            if (_closing) throw new OperationCanceledException();
            if (!success) throw new IOException("사내 페이지를 열지 못했습니다. 사내망 연결을 확인하세요.");
        }
        catch (TimeoutException) { core.Stop(); throw new IOException("페이지 응답이 20초를 넘었습니다. 다시 준비해 주세요."); }
        catch (OperationCanceledException){core.Stop();throw;}
        finally
        {
            core.NavigationStarting -= Starting;
            core.NavigationCompleted -= Completed;
        }
    }

    private async Task NavigateScriptAsync(CoreWebView2 core, string script)
    {
        Task<string>? execution = null;
        await NavigateAsync(core, () => execution = core.ExecuteScriptAsync(script));
        if (execution != null && await execution != "\"ok\"") throw new IOException("보고서 페이지 이동에 실패했습니다.");
    }

    private async Task<bool> LoggedInAsync(CoreWebView2 core)
    {
        return await core.ExecuteScriptAsync("!!document.querySelector('input[type=password]')") != "true";
    }

    private async Task<bool> CheckSessionAsync()
    {
        await NavigateAsync(_login.CoreWebView2, () => _login.CoreWebView2.Navigate(Home));
        if (!await LoggedInAsync(_login.CoreWebView2))
        {
            ResetAccountView();
            if (await AuthenticateAsync()) await RefreshDirectoryForAccountAsync();
            return false;
        }
        bool recognized = await _login.CoreWebView2.ExecuteScriptAsync(
            "!!document.querySelector('a[href*=go_view],a[href*=\"pjm.jsp?id=\"],input[name=table_code][value=report_tbl]')") == "true";
        if (!recognized) throw new IOException("사내 주간보고 목록을 확인하지 못했습니다. 로그인 화면과 접근 권한을 확인하세요.");
        return true;
    }

    private async Task PrepareAsync()
    {
        Member[] members = SelectedAttendees();
        AppLog.Write("reports.prepare",new{team=_team.SelectedItem,members=members.Select(m=>new{m.Id,m.Name,m.Unit})});
        if (members.Length == 0) throw new InvalidOperationException("조직도에서 오늘 참석하는 사람을 체크하세요.");
        if (!await CheckSessionAsync()) return;
        SaveMeetingSettings();
        ClearReports();
        int failures = 0;
        foreach (Member member in members)
        {
            if (_closing) return;
            var page = new ReportPage(member);
            _reports.Add(page); _pages.Children.Add(page.Web);
            _people.Items.Add(member.Name);
            _status.Text = $"{_reports.Count}/{members.Length} · {member.Name} 보고서 준비 중…";
            _people.SelectedIndex = _reports.Count - 1;
            try
            {
                await InitializeWebAsync(page.Web);
                await OpenLatestAsync(page);
            }
            catch (Exception ex) when (!_closing)
            {
                AppLog.Write("report.failed",new{member.Id,member.Name},ex);
                page.Detail = "준비 실패: " + ex.Message; failures++;
                _people.Items[_reports.Count - 1] = member.Name + " · 확인 필요";
            }
            if (_closing) return;
            if (page.Detail.StartsWith("로그인이 필요"))
            {
                _status.Text = "세션이 만료되어 준비를 중단했습니다. 로그인 후 전체 보고서를 다시 준비하세요.";
                ResetAccountView();
                if (await AuthenticateAsync()) await RefreshDirectoryForAccountAsync();
                return;
            }
        }
        _people.SelectedIndex = 0; SelectPerson();
        _status.Text = $"{members.Length}명 준비 완료" + (failures > 0 ? $" · {failures}명 확인 필요" : "")
            + " · 발표자를 누르면 각 페이지의 스크롤 위치가 유지됩니다.";
    }

    private async Task OpenLatestAsync(ReportPage page)
    {
        page.Ready = false;
        var core = page.Web.CoreWebView2;
        await NavigateAsync(core, () => core.Navigate(Home + "?id=" + Uri.EscapeDataString(page.Member.Id)));
        if (!await LoggedInAsync(core)) { page.Detail = "로그인이 필요합니다. 로그인 후 다시 준비하세요."; return; }
        string json = JsonSerializer.Deserialize<string>(await core.ExecuteScriptAsync(RowExtractJs)) ?? "[]";
        using var rows = JsonDocument.Parse(json);
        if (rows.RootElement.GetArrayLength() == 0)
        {
            bool list = await core.ExecuteScriptAsync("!!document.querySelector('form input[name=table_code][value=report_tbl]')") == "true";
            page.Detail = list ? "주간보고가 없습니다. 개인 목록을 열어 두었습니다."
                : "목록을 확인하지 못했습니다. 페이지와 접근 권한을 확인하세요.";
            if (!list) throw new IOException(page.Detail);
            return;
        }
        // Match the video: use the first visible report, never guess from a date or post number.
        string number = rows.RootElement[0].GetProperty("viewNo").GetString() ?? "";
        await NavigateScriptAsync(core, GoViewJs(number, page.Member.Id));
        if (!await LoggedInAsync(core)) { page.Detail = "로그인이 필요합니다. 로그인 후 다시 준비하세요."; return; }
        string cellJson = JsonSerializer.Deserialize<string>(await core.ExecuteScriptAsync(CellExtractJs)) ?? "{}";
        using var cell = JsonDocument.Parse(cellJson);
        string period = cell.RootElement.GetProperty("period").GetString() ?? "";
        string title = cell.RootElement.GetProperty("title").GetString() ?? "";
        if (period.Length == 0 || title.Length == 0) throw new IOException("보고서 본문을 확인하지 못했습니다. 페이지와 접근 권한을 확인하세요.");
        page.Detail = period + " · " + title;
        AppLog.Write("report.opened",new{page.Member.Id,page.Member.Name,period,number});
        if (await core.ExecuteScriptAsync(ReportViewport.Crop) != "true")
            throw new IOException("보고서 본문 영역을 찾지 못했습니다.");
        await core.ExecuteScriptAsync("window.scrollTo(0,0)");
        page.Ready = true;
    }

    private void SelectPerson()
    {
        int index = _people.SelectedIndex;
        if (index < 0 || index >= _reports.Count) return;
        _login.Visibility = Visibility.Hidden;
        for (int i = 0; i < _reports.Count; i++)
            _reports[i].Web.Visibility = i == index || _reports[i].Detached != null ? Visibility.Visible : Visibility.Hidden;
        var selected = _reports[index];
        AppLog.Write("presenter.select",new{index,selected.Member.Id,selected.Member.Name,presentation=_presentation!=null});
        _heading.Text = $"{index + 1} / {_reports.Count} · {selected.Member.Name}";
        _details.Text = selected.Detail;
        UpdatePresentationPage();
        if (selected.Detached != null)
        {
            selected.Web.Visibility = Visibility.Visible;
            if (selected.Detached.WindowState == WindowState.Minimized) selected.Detached.WindowState = WindowState.Normal;
            selected.Detached.Activate();
        }
    }

    private Task MoveAsync(int delta)
    {
        if (_reports.Count > 0) _people.SelectedIndex = Math.Clamp(_people.SelectedIndex + delta, 0, _reports.Count - 1);
        return Task.CompletedTask;
    }

    private async Task RefreshSelectedAsync()
    {
        if (_people.SelectedIndex < 0) return;
        var page = _reports[_people.SelectedIndex];
        try
        {
            await OpenLatestAsync(page);
            _people.Items[_people.SelectedIndex] = page.Member.Name;
            _status.Text = page.Member.Name + " · " + page.Detail;
        }
        catch (Exception ex) { page.Detail = "준비 실패: " + ex.Message; throw; }
        finally { SelectPerson(); }
    }

    private Task DetachAsync()
    {
        if (_people.SelectedIndex < 0) return Task.CompletedTask;
        var page = _reports[_people.SelectedIndex];
        if (page.Detached != null) { page.Detached.Activate(); return Task.CompletedTask; }
        AppLog.Write("report.detach",new{page.Member.Id,page.Member.Name});
        _pages.Children.Remove(page.Web);
        var window = new Window { Owner = this, Title = page.Member.Name + " · 주간보고", Width = 1000, Height = 850,
            Content = page.Web, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        page.Detached = window; page.Web.Visibility = Visibility.Visible;
        window.Closed += (_, _) =>
        {
            AppLog.Write("report.reattach",new{page.Member.Id,page.Member.Name});
            window.Content = null; page.Detached = null;
            if (!_closing && _reports.Contains(page)) { _pages.Children.Add(page.Web); SelectPerson(); }
        };
        window.Show();
        _status.Text = "개별 창을 닫으면 원래 발표 화면으로 돌아옵니다.";
        return Task.CompletedTask;
    }

    private object Snapshot() => new { actor=_loginId,team=_team.SelectedItem as string,attendees=_checked.Count,
        reports=_reports.Count,index=_people.SelectedIndex,zoom=_zoomPercent,step=_zoomStep,presentation=_presentation!=null };
    private static string LogUrl(string url)=>Uri.TryCreate(url,UriKind.Absolute,out var parsed)?parsed.GetLeftPart(UriPartial.Path):"invalid";

    private void ClearReports()
    {
        _presentation?.Close();
        var old = _reports.ToArray(); _reports.Clear(); _people.Items.Clear();
        UpdateReportStartButton();
        foreach (var page in old)
        {
            page.Detached?.Close(); _pages.Children.Remove(page.Web);
            if (_zoomWatchers.Remove(page.Web, out var detachZoom)) detachZoom();
            page.Web.Dispose();
        }
        _login.Visibility = Visibility.Visible;
    }
}
