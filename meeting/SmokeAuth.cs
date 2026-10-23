using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TeamWeeklyMeeting;

internal sealed partial class MeetingWindow
{
    private async Task SmokeAutoLoginAsync(Action<bool,string> check)
    {
        var results=new List<string>();
        void Verify(bool value,string name){results.Add((value?"PASS: ":"FAIL: ")+name);check(value,name);}
        var core=_login.CoreWebView2;
        var good=new LoginCredential{Id="alpha",Password=SmokePassword};
        async Task ResetAttempt(string mode="success"){
            _fixtureAuthMode=mode;_autoLoginAttempted=false;_authNotice="";_pendingLoginId="";
            await ClearSessionAsync();
        }
        try{
            Verify(!_auth.Exists,"기본 상태에서는 인증정보를 저장하지 않음");
            int posts=_fixtureAuthPosts;
            Verify(!await TryAutoLoginAsync()&&_fixtureAuthPosts==posts,"저장 정보가 없으면 자동 인증 요청 없음");
            await core.ExecuteScriptAsync("localStorage.setItem('qa-session','fixture')");
            await LogoutAsync(false);
            Verify(_reports.Count==0&&_checked.Count==0&&_directory.Members.Length==0&&_team.Items.Count==0&&_loginId=="","로그아웃 시 조직·참석자·보고서·계정 화면 정리");
            Verify(!_auth.Exists&&(await core.CookieManager.GetCookiesAsync("https://www.netcus.com")).Count==0,"로그아웃 시 저장 정보와 앱 쿠키 삭제");
            await NavigateAsync(core,()=>core.Navigate("https://www.netcus.com/pjm/login.htm"));
            Verify(await core.ExecuteScriptAsync("localStorage.getItem('qa-session')===null")=="true","로그아웃 시 사이트 로컬 저장소 삭제");
            Verify(!core.Profile.IsPasswordAutosaveEnabled&&!core.Profile.IsGeneralAutofillEnabled,"WebView2 자체 비밀번호 저장과 자동 완성 비활성화");
            _smokeCancelLoading=true;
            Verify(!await SignInAsync()&&!_auth.Exists,"로그인 페이지 준비 중 창을 닫아도 취소 처리하고 저장하지 않음");
            _smokeCancelLoading=false;
            _smokeRemember=true;
            bool capturedAtRedirect=false;
            void ObserveRedirect(object? sender,Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e){
                if(core.Source.Contains("/pjm/loginRSA.jsp")&&e.IsSuccess)capturedAtRedirect=_pendingCredential?.Password==SmokePassword;
            }
            core.NavigationCompleted+=ObserveRedirect;
            Verify(await SignInAsync(),"체크박스 선택 후 수동 로그인 성공");await LoadDirectoryAsync();
            core.NavigationCompleted-=ObserveRedirect;
            Verify(capturedAtRedirect,"RSA 중간 페이지에서도 마스킹 전 입력 유지, 리다이렉트 후 인증");
            Verify(_auth.Read()?.Password==SmokePassword&&_auth.Read()?.Id=="alpha","RSA 마스킹 전 원래 비밀번호를 인증 성공 후 저장");
            string encrypted=Encoding.UTF8.GetString(File.ReadAllBytes(_auth.PathName));
            Verify(!encrypted.Contains(SmokePassword)&&!encrypted.Contains("alpha"),"파일에 계정과 비밀번호 평문 없음");
            _autoLoginAttempted=false;posts=_fixtureAuthPosts;
            Verify(await TryAutoLoginAsync()&&_fixtureAuthPosts==posts,"유효한 본인 세션은 비밀번호 제출 없이 재사용");
            Verify(!await TryAutoLoginAsync()&&_fixtureAuthPosts==posts,"실행당 자동 인증 최대 1회 제한");
            await ResetAttempt();posts=_fixtureAuthPosts;
            Verify(await TryAutoLoginAsync()&&_fixtureAuthPosts==posts+1&&_pendingLoginId=="alpha","세션 만료 후 저장 정보로 자동 인증 1회 성공");
            await RefreshDirectoryForAccountAsync();
            Verify(_loginId=="alpha"&&_directory.Members.Length==5&&_accountButton?.Content?.ToString()=="로그아웃","자동 인증 후 페이지 조직도 및 계정 UI 갱신");
            foreach(string mode in new[]{"rejected","locked","disabled","alert","server","network","structure","mismatch","not-submitted"}){
                await ResetAttempt(mode);byte[] before=File.ReadAllBytes(_auth.PathName);posts=_fixtureAuthPosts;
                var attemptTimer=System.Diagnostics.Stopwatch.StartNew();
                Verify(!await TryAutoLoginAsync(),mode+": 자동 인증 실패를 성공으로 처리하지 않음");
                if(mode=="not-submitted")Verify(attemptTimer.Elapsed.TotalSeconds<5&&_fixtureAuthPosts==posts&&_authNotice.Contains("제출"),"로그인 함수가 제출하지 않으면 20초 대기 없이 수동 전환");
                Verify(_pendingLoginId==""&&_authNotice.Length>0&&File.ReadAllBytes(_auth.PathName).SequenceEqual(before),mode+": 안내 제공·저장 정보 유지·미인증 계정 제거");
                int after=_fixtureAuthPosts;
                Verify(!await TryAutoLoginAsync()&&_fixtureAuthPosts==after&&after-posts<=1,mode+": 자동 반복 없음");
            }
            await ResetAttempt();
            void BlockRequest(object? sender,Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs e){if(e.Uri.Contains("/pjm/loginRSA.jsp"))e.Cancel=true;}
            core.NavigationStarting+=BlockRequest;
            try{
                Verify(!await TryAutoLoginAsync()&&_authNotice.Contains("서버"),"실제 WebView2 탐색 차단 오류를 인증 거부와 구분하여 수동 전환");
            }finally{
                core.NavigationStarting-=BlockRequest;
            }
            await ResetAttempt("timeout");var timer=System.Diagnostics.Stopwatch.StartNew();
            Verify(!await TryAutoLoginAsync()&&timer.Elapsed.TotalSeconds is >=19 and <27&&_authNotice.Contains("20초"),"20초 전체 제한 후 자동 시도 중단");
            await ResetAttempt();
            File.WriteAllBytes(_auth.PathName,new byte[]{1,2,3,4});
            Verify(!await TryAutoLoginAsync()&&_authNotice.Contains("읽을 수"),"손상된 암호화 파일은 수동 로그인으로 전환");
            await ResetAttempt();
            File.WriteAllBytes(_auth.PathName,ProtectedData.Protect("invalid-json"u8.ToArray(),"NETCUS.TeamWeeklyMeeting.AutoLogin.v1"u8.ToArray(),DataProtectionScope.CurrentUser));
            Verify(!await TryAutoLoginAsync(),"복호화 후 잘못된 데이터도 거부");
            await ResetAttempt();
            File.WriteAllBytes(_auth.PathName,ProtectedData.Protect("{\"Id\":null,\"Password\":null}"u8.ToArray(),"NETCUS.TeamWeeklyMeeting.AutoLogin.v1"u8.ToArray(),DataProtectionScope.CurrentUser));
            Verify(!await TryAutoLoginAsync(),"복호화된 계정·비밀번호가 null이면 안전하게 거부");
            await ResetAttempt();
            File.WriteAllBytes(_auth.PathName,ProtectedData.Protect("another-context"u8.ToArray(),"different-key-context"u8.ToArray(),DataProtectionScope.CurrentUser));
            Verify(!await TryAutoLoginAsync(),"다른 보호 컨텍스트의 파일을 복호화하지 못하면 수동 전환");
            _auth.Save(good);
            using(var locked=new FileStream(_auth.PathName,FileMode.Open,FileAccess.Read,FileShare.None)){
                _autoLoginAttempted=false;
                Verify(!await TryAutoLoginAsync(),"인증 파일 읽기 잠금 시 앱 종료 없이 수동 전환");
                bool refused=false;try{_auth.Save(good);}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){refused=true;}
                Verify(refused,"인증 파일 쓰기 실패를 성공으로 처리하지 않음");
            }
            var blocker=Path.Combine(_dataDir,"qa-storage-blocker");File.WriteAllText(blocker,"fixture");
            bool writeFailed=false;try{new AuthStore(Path.Combine(blocker,"autologin.dat")).Save(good);}catch(IOException){writeFailed=true;}
            Verify(writeFailed,"저장 경로를 만들 수 없으면 저장 실패 처리");
            _smokeBlockSave=true;byte[] savedBefore=File.ReadAllBytes(_auth.PathName);
            Verify(await SignInAsync()&&File.ReadAllBytes(_auth.PathName).SequenceEqual(savedBefore),"자동로그인 저장 실패 시 수동 인증 성공은 유지하고 기존 정보 보존");
            _smokeBlockSave=false;await LoadDirectoryAsync();
            _smokeRejectFirst=true;
            Verify(await SignInAsync(),"수동 인증 실패 입력은 저장하지 않고 성공한 재입력만 저장");
            _smokeRejectFirst=false;await LoadDirectoryAsync();
            Verify(_auth.Read()?.Password==SmokePassword,"실패한 비밀번호가 저장되지 않음");
            _smokeCancelLogin=true;_smokeDisableRemember=true;_smokeRemember=false;
            Verify(!await SignInAsync()&&!_auth.Exists,"자동로그인 해제 즉시 인증정보 삭제, 취소 시 저장하지 않음");
            _smokeCancelLogin=false;_smokeDisableRemember=false;
            _autoLoginAttempted=false;posts=_fixtureAuthPosts;
            Verify(!await TryAutoLoginAsync()&&_fixtureAuthPosts==posts,"해제 후 다음 시작에서도 자동 요청 없음");
            _smokeLoginId="beta";_smokeRemember=true;
            Verify(await SignInAsync(),"다른 계정으로 새 로그인");await LoadDirectoryAsync();
            Verify(_loginId=="beta"&&_checked.Count==0&&_reports.Count==0&&_team.SelectedItem?.ToString()=="플랫폼파트"&&_auth.Read()?.Id=="beta","계정 전환 시 이전 참석자·보고서 제거 및 새 계정 저장");
            byte[] previousCredential=File.ReadAllBytes(_auth.PathName);_smokeSuppressCapture=true;
            Verify(await SignInAsync()&&!_auth.Exists,"인증은 성공했지만 저장 입력을 확인하지 못한 경우 기존 자동로그인 정보 제거");
            _smokeSuppressCapture=false;await LoadDirectoryAsync();File.WriteAllBytes(_auth.PathName,previousCredential);
            using(var locked=new FileStream(_auth.PathName,FileMode.Open,FileAccess.Read,FileShare.None)){
                bool refused=false;try{await LogoutAsync(false);}catch(IOException ex){refused=ex.Message.Contains("다음 실행");}
                Verify(refused&&_loginId==""&&_directory.Members.Length==0&&(await core.CookieManager.GetCookiesAsync("https://www.netcus.com")).Count==0,"인증정보 삭제 실패도 세션·화면은 정리하고 자동로그인 잔존 경고");
            }
            await LogoutAsync(false);_autoLoginAttempted=false;posts=_fixtureAuthPosts;
            Verify(!await TryAutoLoginAsync()&&!_auth.Exists&&_fixtureAuthPosts==posts,"로그아웃 후 재실행 조건에서도 자동 인증 없음");
            _smokeCancelLogin=true;
            await LogoutAsync();
            _smokeCancelLogin=false;
            string logs=string.Join("\n",Directory.GetFiles(AppLog.DirectoryPath,"meeting.log*").Select(File.ReadAllText));
            string logoutLog=logs[logs.LastIndexOf("auth.logout-start",StringComparison.Ordinal)..];
            Verify(logoutLog.Split("auth.session-cleared").Length==2,"로그아웃 후 재로그인 창에서 세션 삭제는 정확히 한 번");
            int shown=logs.IndexOf("auth.window-shown",logs.IndexOf("auth.cancel",StringComparison.Ordinal),StringComparison.Ordinal);
            Verify(shown>=0&&logs.IndexOf("auth.session-cleared",shown,StringComparison.Ordinal)<logs.IndexOf("auth.page-ready",shown,StringComparison.Ordinal),"로그인 창을 세션 삭제·페이지 로딩보다 먼저 표시");
            Verify(logs.Split("auth.remember-unavailable").Length==2,"입력 수집을 강제 차단한 한 경우 외에는 저장 정보 삭제 경고 없음");
            Verify(!logs.Contains(SmokePassword)&&!logs.Contains("Password_Enc")&&!logs.Contains("meeting-auth="),"진단 로그에 비밀번호·RSA 필드·쿠키 없음");
            Verify(logs.Contains("auth.auto-success")&&logs.Contains("auth.auto-failed")&&logs.Contains("auth.logout-complete"),"비밀 없이 인증 성공·실패·로그아웃 진단 기록");
        }finally{
            _smokeRemember=false;_smokeCancelLogin=false;_smokeCancelLoading=false;_smokeDisableRemember=false;_smokeRejectFirst=false;_smokeBlockSave=false;_smokeSuppressCapture=false;_fixtureAuthMode="success";
            File.WriteAllLines(Path.Combine(Path.GetTempPath(),"TeamWeeklyMeeting-auth-qa.txt"),results);
        }
    }
}
