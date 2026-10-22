using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;

namespace TeamWeeklyMeeting;

internal sealed partial class MeetingWindow
{
    private int _fixtureRequests;
    private int _fixturePosts;
    private string _smokeLoginId = "alpha";

    private void InstallFixture(CoreWebView2 core)
    {
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += (_, e) =>
        {
            _fixtureRequests++;
            var uri = new Uri(e.Request.Uri);
            string body;
            if (uri.AbsolutePath == "/favicon.ico") body = "";
            else if (uri.AbsolutePath == "/pjm/login.htm" || !e.Request.Headers.Contains("Cookie")
                || !e.Request.Headers.GetHeader("Cookie").Contains("meeting-auth=1"))
            {
                body = """
                    <form name="form"><h1>테스트용 사내 로그인</h1><input name="id"><input type="password" name="pass">
                    <button type="button" onclick="document.cookie='meeting-auth=1;path=/;Secure';location.href='pjm.jsp'">로그인</button></form>
                    """;
            }
            else if (uri.AbsolutePath == "/pjm/pjm.jsp")
            {
                string id = uri.Query.Contains("id=beta") ? "beta" : uri.Query.Contains("id=empty") ? "empty"
                    : uri.Query.Contains("id=denied") ? "denied" : uri.Query.Contains("id=expired") ? "expired" : "alpha";
                if (id == "denied") body = "<h1>접근할 수 없습니다</h1>";
                else if (id == "expired") body = "<input type=password>";
                else
                {
                    string links = "<nav><a href='pjm.jsp?id=alpha'>테스트 가</a> <a href='pjm.jsp?id=beta'>테스트 나</a>"
                        + "<a href='https://example.org/pjm/pjm.jsp?id=external'>외부</a>"
                        + "<a href='pjm.jsp?id=alpha'>중복</a></nav>";
                    body = links + "<form><input type=hidden name=table_code value=report_tbl></form>";
                    if (id != "empty") body += "<table><tr><td><a href=\"javascript:go_view('"
                        + (id == "alpha" ? "7" : "8") + "')\">맨 위 보고서</a></td><td>2026-10-22</td></tr>"
                        + "<tr><td><a href=\"javascript:go_view('900')\">둘째 보고서</a></td><td>2026-10-15</td></tr></table>";
                }
            }
            else if (uri.AbsolutePath == "/pjm/pjm_view.jsp")
            {
                if (e.Request.Method != "POST") throw new InvalidOperationException("보고서는 기존 모듈처럼 POST로 열어야 합니다.");
                using var reader = new StreamReader(e.Request.Content, Encoding.UTF8, leaveOpen: true);
                string fields = reader.ReadToEnd();
                _fixturePosts++;
                bool alpha = fields.Contains("id=alpha");
                if (!fields.Contains("table_code=report_tbl") || !uri.Query.Contains(alpha ? "view_no=7" : "view_no=8"))
                    throw new InvalidOperationException("팀원 또는 최상단 보고서 선택 오류: " + fields + uri.Query);
                body = "<style>body{font-family:Malgun Gothic;padding:40px}td{padding:12px;vertical-align:top}"
                    + "table[data-fixture-report]{background:#e4ecef;width:560px}</style>"
                    + "<table width=810 cellspacing=0 cellpadding=0><tr><td colspan=4 id=site-top>사내 상단</td></tr>"
                    + "<tr><td width=29 rowspan=2 id=site-left>장식</td><td width=152 rowspan=2 id=site-menu>사내 인원 메뉴</td>"
                    + "<td width=600 height=35>주간보고</td><td width=29 rowspan=2 id=site-right>장식</td></tr>"
                    + "<tr><td width=600 valign=top><table width=560 data-fixture-report><tr><td>기간</td><td>2026-10-19 ~ 2026-10-23</td></tr>"
                    + "<tr><td>제목</td><td>10월 넷째 주 보고</td></tr><tr><td>진행사항</td><td>"
                    + string.Join("<p>팀별 진행사항 · 테스트 데이터</p>", Enumerable.Range(0, 70))
                    + "</td></tr><tr><td>차주계획</td><td>다음 주 계획</td></tr></table></td></tr></table>"
                    + "<script>window.__fixtureReportMarkup=document.querySelector('[data-fixture-report]').outerHTML;</script>";
            }
            else body = "<h1>알 수 없는 테스트 경로</h1>";
            var stream = new MemoryStream(Encoding.UTF8.GetBytes("<!doctype html><meta charset=utf-8>" + body));
            e.Response = _environment!.CreateWebResourceResponse(stream, 200, "OK", "Content-Type: text/html; charset=utf-8");
        };
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private const string FixtureDirectory = """
        {"found":true,"myUnit":"개발팀","units":[
          {"name":"회사","parent":null,"sortOrder":0},
          {"name":"개발팀","parent":"회사","sortOrder":1},
          {"name":"플랫폼파트","parent":"개발팀","sortOrder":2},
          {"name":"검증팀","parent":"회사","sortOrder":3}],
         "members":[
          {"loginId":"alpha","name":"김민준","title":"책임","orgUnit":"개발팀"},
          {"loginId":"denied","name":"접근 확인","orgUnit":"검증팀"},
          {"loginId":"expired","name":"세션 확인","orgUnit":"검증팀"},
          {"loginId":"beta","name":"이서연","title":"선임","orgUnit":"플랫폼파트"},
          {"loginId":"empty","name":"박지호","title":"연구원","orgUnit":"플랫폼파트"}]}
        """;

    private async Task SmokeTestAsync()
    {
        int checks = 0;
        void Check(bool condition, string message) { Require(condition, message); checks++; }
        void Choose(string team, params string[] ids)
        {
            _team.SelectedItem = team; _checked.Clear();
            foreach (string id in ids) _checked.Add(id);
            RenderAttendance();
        }
        var directory = MeetingDirectory.Parse(FixtureDirectory);
        Check(directory.Subtree("개발팀").SetEquals(new[] {"개발팀","플랫폼파트"}), "하위 조직 범위");
        var cyclic = new MeetingDirectory { Units = new[] { new OrgUnit("a","b",0), new OrgUnit("b","a",0) } };
        Check(cyclic.Subtree("a").Count == 2, "잘못된 조직 순환 방어");
        Check(!await LoggedInAsync(_login.CoreWebView2), "미로그인 세션 확인");
        Check(!_commands.Any(b => (string)b.Content == "사내 계정 로그인" || (string)b.Content == "조직도 새로고침"), "불필요한 로그인 및 새로고침 버튼 제거");
        Check(await SignInAsync(), "로그인 전용 창에서 인증 성공 후 자동 닫기");
        Check(await CheckSessionAsync(), "로그인 후 세션 확인");
        await LoadDirectoryAsync();
        Check(_loginId == "alpha", "로그인 아이디 연결, 비밀번호 수집 없음");
        Check((string?)_team.SelectedItem == "개발팀", "본인 팀 기본 선택");
        Check(Icon is System.Windows.Media.Imaging.BitmapSource iconSource && iconSource.PixelWidth>=256 && Application.Current.Resources.Contains("BrandOrange"),"작업표시줄 축소 방지용 고해상도 회사 아이콘 및 브랜드 스타일 적용");
        Check(_reportStartButton?.IsEnabled==false && _reportStartButton.ToolTip?.ToString()?.Contains("먼저 준비")==true,"보고서 준비 전 보고 시작 버튼 비활성화 및 안내");
        var firstRoot=(System.Windows.Controls.TreeViewItem)_attendance.Items[0];
        Check(firstRoot.Items[0] is System.Windows.Controls.CheckBox,"상위 조직 직속 인력을 하위 조직보다 먼저 표시");
        var hierarchy=new MeetingDirectory{MyUnit="기술개발총괄",Units=new[]{
            new OrgUnit("SW1팀","SW개발본부",1),new OrgUnit("SW개발본부","기술개발총괄",20),new OrgUnit("기술개발총괄","",10)},
            Members=new[]{new Member("박성봉","park","기술개발총괄","전무"),new Member("SW 책임자","swhead","SW개발본부","책임")}};
        SetDirectory(hierarchy);
        var tech=(System.Windows.Controls.TreeViewItem)_attendance.Items[0];
        Check(((System.Windows.Controls.CheckBox)tech.Items[0]).Tag as string=="park" && ((System.Windows.Controls.TreeViewItem)tech.Items[1]).Header as string=="SW개발본부",
            "박성봉 전무가 기술개발총괄 바로 아래, SW개발본부보다 먼저 표시");
        Check(hierarchy.OrderedUnits().Select(u=>u.Name).SequenceEqual(new[]{"기술개발총괄","SW개발본부","SW1팀"}),"팀 선택 목록도 부모 다음 하위 조직 순서");
        Check(TeamMembers().First().Id=="park","보고서 준비 및 발표 순서도 상위 조직 직속 인력 우선");
        SetDirectory(MeetingDirectory.Parse(FixtureDirectory));
        Check(TeamMembers().Length == 3 && SelectedAttendees().Length == 0, "하위 조직 포함, 참석자는 직접 체크");
        await CheckAllAsync(true);
        Check(SelectedAttendees().Length == 3, "팀 전체 참석 체크");
        await CheckAllAsync(false);
        Check(SelectedAttendees().Length == 0, "팀 전체 체크 해제");
        var root = (System.Windows.Controls.TreeViewItem)_attendance.Items[0];
        var alphaBox = root.Items.OfType<System.Windows.Controls.CheckBox>().Single();
        alphaBox.IsChecked = true;
        Check(SelectedAttendees().Single().Id == "alpha", "실제 참석자 체크박스 배선");
        _team.SelectedItem = "검증팀";
        Check(SelectedAttendees().Length == 0 && TeamMembers().Length == 2, "팀 전환 시 이전 참석자 제외");
        Choose("개발팀", "alpha", "beta", "empty");
        await PrepareAsync();
        Check(_reports.Count == 3 && _fixturePosts == 2, "참석자만 보고서 준비, 팀원마다 POST 한 번");
        Check(_reports[0].Detail.Contains("2026-10-19") && _reports[1].Detail.Contains("2026-10-19"), "쿠키 공유 및 본문 확인");
        Check(_reports[2].Detail.StartsWith("주간보고가 없습니다"), "보고서 없는 참석자 구분");
        Check(File.ReadAllText(SettingsPath).Contains("alpha"), "팀 및 오늘 참석자 저장");
        _people.SelectedIndex = 0; SelectPerson();
        var core = _reports[0].Web.CoreWebView2;
        Check(await core.ExecuteScriptAsync("document.querySelectorAll('#meeting-reader,#meeting-reader-style').length") == "0", "별도 본문 디자인 주입 없음");
        Check(await core.ExecuteScriptAsync("getComputedStyle(document.querySelector('[data-fixture-report]')).display") == "\"table\"", "사내 원본 표 표시");
        Check(await core.ExecuteScriptAsync("getComputedStyle(document.querySelector('[data-fixture-report]')).backgroundColor") == "\"rgb(228, 236, 239)\"", "원본 사이트 색상 유지");
        Check(await core.ExecuteScriptAsync("document.querySelector('[data-fixture-report]').outerHTML === window.__fixtureReportMarkup") == "true", "본문 표 원본 HTML 및 서식 보존");
        Check(await core.ExecuteScriptAsync("document.querySelector('#meeting-report-viewport').textContent.includes('주간보고')") == "true", "주간보고 제목 영역 포함");
        Check(await core.ExecuteScriptAsync("(()=>{const e=document.querySelector('#site-menu');return !e.getClientRects().length})()") == "true", "사내 좌측 메뉴 숨김");
        Check(await core.ExecuteScriptAsync("['site-top','site-left','site-right'].every(id=>!document.getElementById(id).getClientRects().length)") == "true", "사내 상단 및 좌우 장식 숨김");
        Check(await core.ExecuteScriptAsync("document.querySelector('[data-fixture-report]').getBoundingClientRect().right >= document.documentElement.clientWidth-48") == "true", "보고서가 뷰어 너비를 채워 큰 오른쪽 여백 제거");
        string originalMarkup = await core.ExecuteScriptAsync("document.documentElement.outerHTML");
        await ZoomAsync(10);
        Check(_reports.All(p => Math.Abs(p.Web.ZoomFactor - 1.1) < .001), "모든 보고서 네이티브 확대");
        await ZoomAsync(1000);
        await Task.Delay(100);
        Check(_zoomPercent == 500 && Math.Abs(_reports[0].Web.ZoomFactor-5) < .001, "네이티브 보기 배율 500% 지원");
        await ZoomAsync(-1000);
        Check(_zoomPercent == 25, "보기 배율 하한 25%");
        await ZoomAsync(100 - _zoomPercent);
        Check(await core.ExecuteScriptAsync("document.documentElement.outerHTML") == originalMarkup, "확대 축소 중 원본 HTML 유지");
        await core.ExecuteScriptAsync("window.scrollTo(0,600)");
        await Task.Delay(150);
        await MoveAsync(1); await MoveAsync(-1);
        Check(await core.ExecuteScriptAsync("window.scrollY") == "600", "발표자 전환 후 스크롤 유지");
        await DetachAsync(); await Task.Delay(150);
        Check(_reports[0].Detached != null && !_pages.Children.Contains(_reports[0].Web), "개별 창 분리");
        await MoveAsync(1);
        Check(_reports[0].Web.Visibility == Visibility.Visible, "분리 창 표시 유지");
        _reports[0].Detached!.Close();
        Check(_reports[0].Detached == null && _pages.Children.Contains(_reports[0].Web), "개별 창 닫기 후 복귀");
        _people.SelectedIndex = 0; SelectPerson();
        Check(await core.ExecuteScriptAsync("window.scrollY") == "600", "분리 및 복귀 후 스크롤 유지");
        await RefreshSelectedAsync();
        Check(await core.ExecuteScriptAsync("window.scrollY") == "0", "최신 보고서 재준비");
        Choose("회사","denied","beta"); await PrepareAsync();
        Check(_reports.Single(p=>p.Member.Id=="denied").Detail.StartsWith("준비 실패") && _reports.Single(p=>p.Member.Id=="beta").Detail.Contains("2026-10-19"), "한 명 실패해도 나머지 준비");
        Choose("회사","expired","beta"); await PrepareAsync();
        Check(_reports.Count == 0 && _status.Text.Contains("팀을 선택"), "세션 만료 시 자동 로그인 후 조직도 갱신");
        Choose("개발팀","alpha","beta"); await PrepareAsync();
        Check(_reports.Count == 2 && _pages.Children.Count == 3, "재준비 시 기존 페이지 정리");
        _checked.Clear(); LoadMeetingSettings();
        Check(_checked.SetEquals(new[]{"alpha","beta"}) && _savedTeam == "개발팀", "당일 참석자 및 팀 복원");
        SetDirectory(MeetingDirectory.Parse(FixtureDirectory));
        Check(SelectedAttendees().Length == 2, "조직도 새로고침 시 당일 참석자 유지");
        string settingsPath = SettingsPath;
        string currentSettings = File.ReadAllText(settingsPath);
        File.WriteAllText(settingsPath,JsonSerializer.Serialize(new MeetingSettings("개발팀","2000-01-01",new[]{"alpha"},100)));
        _checked.Clear(); LoadMeetingSettings();
        Check(_checked.Count == 0, "이전 날짜 참석자는 자동 체크하지 않음");
        File.WriteAllText(settingsPath,currentSettings); LoadMeetingSettings(); RenderAttendance();
        string settingsJson = File.ReadAllText(settingsPath);
        File.WriteAllText(settingsPath,"{\"LoginId\":\"alpha\",\"Team\":\"개발팀\",\"Date\":\"2000-01-01\",\"Attendees\":[],\"FontSize\":22}");
        LoadMeetingSettings();
        Check(_zoomPercent == 100, "이전 뷰어 설정은 원본 배율 100%로 전환");
        File.WriteAllText(settingsPath,settingsJson); LoadMeetingSettings(); RenderAttendance();
        string sharedFile=File.ReadAllText(SettingsPath);
        Check(!sharedFile.Contains("LoginId"),"공용 설정에 로그인 계정 정보를 저장하지 않음");
        string commonPath=SettingsPath;string previousActor=_loginId;_loginId="unrelated";
        LoadMeetingSettings();
        Check(SettingsPath==commonPath && _zoomPercent==100,"설정 파일을 계정별로 나누지 않음");
        _loginId=previousActor;RenderAttendance();
        _zoomInput.Text="175"; _stepInput.Text="7";
        await ApplyZoomInputsAsync();
        Check(_zoomPercent == 175 && _zoomStep == 7, "배율 및 증감 폭 직접 입력");
        await ZoomByStepAsync(1);
        Check(_zoomPercent == 182, "사용자 지정 증감 폭으로 확대");
        await ZoomByStepAsync(-1);
        Check(_zoomPercent == 175, "사용자 지정 증감 폭으로 축소");
        _reports[0].Web.ZoomFactor=1.33;await Task.Delay(150);
        Check(_zoomPercent==133 && JsonSerializer.Deserialize<MeetingSettings>(File.ReadAllText(SettingsPath))!.ZoomPercent==133,
            $"브라우저에서 바뀐 배율도 공용 설정에 반영 (앱={_zoomPercent}, 뷰={_reports[0].Web.ZoomFactor}, 저장={JsonSerializer.Deserialize<MeetingSettings>(File.ReadAllText(SettingsPath))!.ZoomPercent})");
        _zoomInput.Text="175";await ApplyZoomInputsAsync();
        _zoomInput.Text="501";
        bool invalid=false;try{await ApplyZoomInputsAsync();}catch(ArgumentException){invalid=true;}
        Check(invalid && _zoomPercent == 175, "범위 밖 배율은 기존 배율 유지");
        _zoomInput.Text="175";_stepInput.Text="0";invalid=false;
        try{await ApplyZoomInputsAsync();}catch(ArgumentException){invalid=true;}
        Check(invalid && _zoomStep == 7, "잘못된 증감 폭 거부");
        _stepInput.Text="7";
        Width=1180;Height=780; SaveMeetingSettings();
        string alphaSettings=SettingsPath;
        _smokeLoginId="beta";
        Check(await SignInAsync(), "다른 계정 로그인");await LoadDirectoryAsync();
        Check(_loginId=="beta" && _zoomPercent==175 && _zoomStep==7 && SelectedAttendees().Length==2, "로그인 계정과 관계없이 공용 설정 유지");
        Check(SettingsPath==alphaSettings && !Directory.Exists(Path.Combine(_dataDir,"accounts")), "계정별 설정 파일 생성 기능 제거");
        _zoomInput.Text="240";_stepInput.Text="15";await ApplyZoomInputsAsync();
        Choose("검증팀","denied");SaveMeetingSettings();
        _smokeLoginId="alpha";
        Check(await SignInAsync(), "원래 계정 재로그인");await LoadDirectoryAsync();
        Check(_zoomPercent==240 && _zoomStep==15 && (string?)_team.SelectedItem=="검증팀", "다른 계정으로 로그인해도 마지막 공용 배율·증감 폭·팀 유지");
        Check(Math.Abs(Width-1180)<1 && Math.Abs(Height-780)<1, "공용 창 크기 유지");
        Check(SelectedAttendees().Select(m=>m.Id).ToHashSet().SetEquals(new[]{"denied"}), "공용 당일 참석자 유지");
        Choose("개발팀","alpha","beta","empty");await PrepareAsync();
        await ZoomAsync(100-_zoomPercent);
        _people.SelectedIndex=0;SelectPerson();core=_reports[0].Web.CoreWebView2;
        await core.ExecuteScriptAsync("window.scrollTo(0,500);const b=document.createElement('button');b.id='fixture-action';b.textContent='수정';b.onclick=()=>window.actionTaken=true;document.querySelector('#meeting-report-viewport').append(b)");
        await StartPresentationAsync();await Task.Delay(250);
        Check(_presentation!=null && _presentation.WindowStyle==WindowStyle.None && _presentation.WindowState==WindowState.Maximized, "보고 모드 전체 화면 및 창 테두리 숨김");
        Check(!_viewer.Children.Contains(_pages) && ((System.Windows.Controls.Grid)_presentation!.Content).Children.Count==2, "보고 모드에 설정 레이아웃 없이 보고서만 표시");
        Check(await core.ExecuteScriptAsync("document.documentElement.classList.contains('meeting-presenting')")=="true", "보고 모드 브라우저 입력 활성화");
        Check(!core.Settings.AreBrowserAcceleratorKeysEnabled && !core.Settings.AreDefaultContextMenusEnabled, "보고 모드 중 브라우저 이동 단축키 및 메뉴 비활성화");
        Check(await core.ExecuteScriptAsync("getComputedStyle(document.querySelector('#fixture-action')).display")=="\"none\"", "보고 모드에서 사이트 편집 버튼 숨김");
        await core.ExecuteScriptAsync("document.querySelector('#fixture-action').click()");
        Check(await core.ExecuteScriptAsync("!!window.actionTaken")=="false", "보고 모드에서 사이트 클릭 실행 방지");
        await BrowserKeyAsync(core,"ArrowRight",39);await Task.Delay(150);
        Check(_people.SelectedIndex==1, "브라우저에 포커스가 있어도 오른쪽 방향키로 다음 발표자");
        await BrowserKeyAsync(_reports[1].Web.CoreWebView2,"ArrowLeft",37);await Task.Delay(150);
        Check(_people.SelectedIndex==0, "왼쪽 방향키로 이전 발표자");
        Check(await core.ExecuteScriptAsync("window.scrollY")=="500", "발표자 전환 시 보고서 위치 유지");
        await core.ExecuteScriptAsync("window.scrollTo(0,0)");
        await core.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent","{\"type\":\"mouseMoved\",\"x\":200,\"y\":350}");
        await core.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent","{\"type\":\"mousePressed\",\"x\":200,\"y\":350,\"button\":\"left\",\"buttons\":1,\"clickCount\":1}");
        await core.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent","{\"type\":\"mouseMoved\",\"x\":200,\"y\":150,\"buttons\":1}");
        await core.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent","{\"type\":\"mouseReleased\",\"x\":200,\"y\":150,\"button\":\"left\",\"buttons\":0,\"clickCount\":1}");
        await Task.Delay(150);
        double dragY=double.Parse(await core.ExecuteScriptAsync("window.scrollY"),System.Globalization.CultureInfo.InvariantCulture);
        Check(dragY>100, "실제 마우스 드래그로 본문 스크롤");
        await core.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent","{\"type\":\"mouseWheel\",\"x\":200,\"y\":200,\"deltaX\":0,\"deltaY\":250}");
        await Task.Delay(200);
        Check(double.Parse(await core.ExecuteScriptAsync("window.scrollY"),System.Globalization.CultureInfo.InvariantCulture)>dragY, "마우스 휠로 본문 스크롤");
        HandlePresentationKey("End");
        Check(_people.SelectedIndex==2 && _presentationEmpty.Visibility==Visibility.Visible && _pages.Visibility==Visibility.Hidden, "미작성 보고서는 사이트 메뉴 대신 해당 사람의 상태만 표시");
        HandlePresentationKey("ArrowRight");Check(_people.SelectedIndex==2, "마지막 발표자에서 다음 키는 목록 범위를 넘지 않음");
        HandlePresentationKey("Home");Check(_people.SelectedIndex==0,"Home으로 첫 발표자 이동");
        await core.ExecuteScriptAsync("window.scrollTo(0,0)");
        using(var preview=File.Create(Path.Combine(Path.GetTempPath(),"TeamWeeklyMeeting-presentation-preview.png")))
            await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,preview);
        await BrowserKeyAsync(core,"Escape",27);await Task.Delay(250);
        Check(_presentation==null && _viewer.Children.Contains(_pages), "브라우저 포커스에서 Esc로 보고 모드 종료 및 레이아웃 복원");
        Check(core.Settings.AreBrowserAcceleratorKeysEnabled && core.Settings.AreDefaultContextMenusEnabled, "보고 모드 종료 시 브라우저 단축키 및 메뉴 복원");
        Check(await core.ExecuteScriptAsync("document.documentElement.classList.contains('meeting-presenting')")=="false", "종료 후 드래그·키보드 가로채기 해제");
        await core.ExecuteScriptAsync("document.querySelector('#fixture-action').click()");
        Check(await core.ExecuteScriptAsync("!!window.actionTaken")=="true", "보고 모드 종료 후 사이트 직접 조작 복원");
        await StartPresentationAsync();await Task.Delay(100);HandlePresentationKey("Escape");await Task.Delay(150);
        Check(_presentation==null && _viewer.Children.Contains(_pages), "보고 모드 반복 진입 및 종료");
        bool supportFields=false;
        _ = Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,new Action(async()=>{
            var popup=OwnedWindows.Cast<Window>().Single(w=>w.Title=="문의 및 버그제보");
            await Task.Delay(120);popup.UpdateLayout();
            var elements=Descendants((DependencyObject)popup.Content).ToArray();
            supportFields=elements.OfType<System.Windows.Controls.TextBlock>().Any(t=>t.Text.Contains(SupportEmail))
                && elements.OfType<System.Windows.Controls.TextBox>().Any(t=>t.Text==AppLog.FilePath);
            var content=(FrameworkElement)popup.Content;
            var image=new RenderTargetBitmap((int)content.ActualWidth,(int)content.ActualHeight,96,96,PixelFormats.Pbgra32);
            image.Render(content);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
            using(var file=File.Create(Path.Combine(Path.GetTempPath(),"TeamWeeklyMeeting-support-preview.png")))encoder.Save(file);
            popup.Close();
        }));
        ShowSupport();
        Check(supportFields,"문의 버튼에 기존 캘린더 이메일 및 실제 로그 파일 경로 표시");
        AppLog.Write("qa.error",error:new InvalidOperationException("synthetic error for diagnostics"));
        string diagnostic=File.ReadAllText(AppLog.FilePath);
        var entries=diagnostic.Split('\n',StringSplitOptions.RemoveEmptyEntries).Select(line=>JsonDocument.Parse(line)).ToArray();
        foreach(string action in new[]{"app.start","auth.prompt","auth.success","team.select","directory.loaded","attendee.check","report.opened","presentation.start","presentation.key","browser.input","view.zoom","report.detach","report.reattach","support.open","qa.error"})
            Check(entries.Any(e=>e.RootElement.GetProperty("action").GetString()==action),"진단 로그 기록: "+action);
        Check(entries.Last().RootElement.GetProperty("error").GetProperty("type").GetString()=="System.InvalidOperationException","예외 종류와 원인 기록");
        Check(!diagnostic.Contains("meeting-auth") && !diagnostic.Contains("팀별 진행사항 · 테스트 데이터"),"쿠키 및 보고서 본문은 로그에 저장하지 않음");
        foreach(var entry in entries)entry.Dispose();
        Check(_fixtureRequests > 10, "모든 브라우저 요청 로컬 처리");
        _status.Text = "팀 선택 → 오늘 참석자 체크 → 보고서 준비";
        foreach (var command in _commands) command.IsEnabled = true;
        _team.IsEnabled = true; _attendance.IsEnabled = true; _people.IsEnabled = true;
        _zoomInput.IsEnabled=true;_stepInput.IsEnabled=true;
        await CapturePreviewAsync("TeamWeeklyMeeting-preview.png");
        Width = MinWidth; Height = MinHeight;
        await Task.Delay(200);
        Check(await _reports[0].Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-fixture-report]').getBoundingClientRect().right >= document.documentElement.clientWidth-48") == "true", "창 축소 후에도 본문 너비 자동 조절");
        await CapturePreviewAsync("TeamWeeklyMeeting-preview-small.png");
        File.WriteAllText(Path.Combine(Path.GetTempPath(),"TeamWeeklyMeeting-smoke-result.txt"),
            $"PASS: {checks} checks; {_fixtureRequests} intercepted requests; {_fixturePosts} report POSTs.\n");
        Close();
    }

    private static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Descendants(VisualTreeHelper.GetChild(root,i)))yield return child;
    }

    private static async Task BrowserKeyAsync(CoreWebView2 core,string key,int code)
    {
        await core.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent",JsonSerializer.Serialize(new{type="keyDown",key,windowsVirtualKeyCode=code}));
        await core.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent",JsonSerializer.Serialize(new{type="keyUp",key,windowsVirtualKeyCode=code}));
    }

    private async Task CapturePreviewAsync(string filename)
    {
        UpdateLayout();
        var visual = (Visual)Content;
        var bitmap = new RenderTargetBitmap((int)((FrameworkElement)Content).ActualWidth, (int)((FrameworkElement)Content).ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        using var browserImage = new MemoryStream();
        await _reports[0].Web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, browserImage);
        browserImage.Position = 0;
        var page = new BitmapImage();
        page.BeginInit(); page.CacheOption = BitmapCacheOption.OnLoad; page.StreamSource = browserImage; page.EndInit();
        Point point = _pages.TransformToAncestor(visual).Transform(new Point());

        var combined = new DrawingVisual();
        using (var drawing = combined.RenderOpen())
        {
            drawing.DrawRectangle(Background, null, new Rect(0, 0, ((FrameworkElement)Content).ActualWidth, ((FrameworkElement)Content).ActualHeight));
            drawing.DrawImage(bitmap, new Rect(0, 0, ((FrameworkElement)Content).ActualWidth, ((FrameworkElement)Content).ActualHeight));
            drawing.DrawImage(page, new Rect(point.X, point.Y, _pages.ActualWidth, _pages.ActualHeight));
        }
        var result = new RenderTargetBitmap((int)((FrameworkElement)Content).ActualWidth, (int)((FrameworkElement)Content).ActualHeight, 96, 96, PixelFormats.Pbgra32);
        result.Render(combined);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(result));
        using var output = File.Create(Path.Combine(Path.GetTempPath(), filename));
        encoder.Save(output);
    }
}
