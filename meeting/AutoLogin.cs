using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;

namespace TeamWeeklyMeeting;

internal sealed partial class MeetingWindow
{
    private AuthStore _auth=null!;
    private bool _autoLoginAttempted, _captureLoginSecrets;
    private LoginCredential? _pendingCredential;
    private string _authNotice="";
    private readonly TextBlock _accountText=new(){VerticalAlignment=VerticalAlignment.Center,FontSize=12,Foreground=Brushes.DimGray,Margin=new Thickness(12,0,8,0)};
    private Button? _accountButton;

    private const string AuthenticatedActor = """
        (()=>{
          if(document.querySelector('input[type=password]')||!document.querySelector('#submenu1'))return '';
          return document.querySelector('input[name=id]')?.value||'';
        })()
        """;

    // Capture before the site's RSA function masks the password, only after explicit opt-in.
    private const string CaptureLogin = """
        (()=>{
          if(window.__meetingLoginCaptureInstalled)return;
          window.__meetingLoginCaptureInstalled=true;
          let captured=false;
          function capture(){
            if(captured||!window.__meetingRemember)return;
            const id=document.querySelector('input[name=id]')?.value||'';
            const password=document.querySelector('input[type=password]')?.value||'';
            if(id&&password){captured=true;chrome.webview.postMessage({kind:'meeting-credential',id,password});}
          }
          if(typeof window.goLogin==='function'){
            const original=window.goLogin;
            window.goLogin=function(...args){captured=false;capture();return original.apply(this,args)};
          }
          document.addEventListener('submit',capture,true);
        })()
        """;

    private async Task<string> AuthenticatedIdAsync(CancellationToken cancellationToken=default)
    {
        var core=_login.CoreWebView2;
        if(!IsCompanyUrl(core.Source))return "";
        return JsonSerializer.Deserialize<string>(await core.ExecuteScriptAsync(AuthenticatedActor).WaitAsync(cancellationToken))??"";
    }

    private void UpdateAccountHeader()
    {
        _accountText.Text=_loginId.Length==0?"로그인되지 않음":_loginId;
        if(_accountButton!=null)_accountButton.Content=_loginId.Length==0?"로그인":"로그아웃";
    }

    private void ResetAccountView()
    {
        ClearReports();_checked.Clear();_savedTeam="";_directory=new MeetingDirectory();
        _settingDirectory=true;
        try{_team.ItemsSource=null;_attendance.Items.Clear();}finally{_settingDirectory=false;}
        _loginId="";_pendingLoginId="";_pendingCredential=null;_captureLoginSecrets=false;
        _heading.Text="사내 근태관리 로그인";_details.Text="";UpdateAttendeeCount();UpdateAccountHeader();PersistSettings();
    }

    private async Task ClearSessionAsync(CancellationToken cancellationToken=default)
    {
        var core=_login.CoreWebView2;
        core.Stop();await NavigateAsync(core,()=>core.Navigate("about:blank"),cancellationToken);
        core.CookieManager.DeleteAllCookies();
        await core.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.AllProfile).WaitAsync(cancellationToken);
        core.Profile.IsPasswordAutosaveEnabled=false;core.Profile.IsGeneralAutofillEnabled=false;
    }

    private async Task<bool> AuthenticateAsync(bool allowAutomatic=true)
    {
        if(allowAutomatic && await TryAutoLoginAsync())return true;
        return await SignInAsync();
    }

    private async Task<bool> TryAutoLoginAsync()
    {
        if(_autoLoginAttempted){AppLog.Write("auth.auto-skipped",new{reason="attempt-limit"});return false;}
        if(!_auth.Exists){AppLog.Write("auth.auto-skipped",new{reason="not-configured"});return false;}
        _autoLoginAttempted=true;
        LoginCredential? saved=null;
        try{saved=_auth.Read();}
        catch(Exception ex)when(ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException or JsonException)
        {
            _authNotice="저장된 로그인 정보를 읽을 수 없습니다. 직접 로그인해 주세요.";
            AppLog.Write("auth.auto-failed",new{reason="storage",type=ex.GetType().Name});return false;
        }
        if(saved==null)return false;
        AppLog.Write("auth.auto-start");
        var core=_login.CoreWebView2;
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        bool previousDialogs=core.Settings.AreDefaultScriptDialogsEnabled,alertShown=false;
        void AutoDialog(object? sender,CoreWebView2ScriptDialogOpeningEventArgs e){alertShown=true;e.Accept();}
        core.Settings.AreDefaultScriptDialogsEnabled=false;core.ScriptDialogOpening+=AutoDialog;
        string failure="connection";bool authenticated=false;
        try
        {
            await NavigateAsync(core,()=>core.Navigate(Home),deadline.Token);
            string actor=await AuthenticatedIdAsync(deadline.Token);
            if(actor==saved.Id){_pendingLoginId=actor;authenticated=true;AppLog.Write("auth.auto-success",new{mode="session"});return true;}
            await ClearSessionAsync(deadline.Token);
            await NavigateAsync(core,()=>core.Navigate("https://www.netcus.com/pjm/login.htm"),deadline.Token);
            if(await core.ExecuteScriptAsync("!!document.querySelector('input[type=password]') && typeof goLogin==='function'").WaitAsync(deadline.Token)!="true")
            {failure="page-structure";return false;}
            _pendingLoginId=saved.Id;
            failure="rejected";
            await NavigateAsync(core,()=>{
                var submission=core.ExecuteScriptAsync("document.querySelector('input[name=id]').value="+JsonSerializer.Serialize(saved.Id)+";document.querySelector('input[type=password]').value="+JsonSerializer.Serialize(saved.Password)+";goLogin()");
                // Observe failures without forwarding a credential-bearing operation to the global logger.
                _=submission.ContinueWith(task=>{_ = task.Exception;},TaskContinuationOptions.OnlyOnFaulted);
            },deadline.Token);
            actor=await AuthenticatedIdAsync(deadline.Token);
            if(actor!=saved.Id){failure=actor.Length>0?"account-mismatch":"rejected";return false;}
            authenticated=true;AppLog.Write("auth.auto-success",new{mode="credentials"});return true;
        }
        catch(IOException){failure="connection-or-timeout";return false;}
        catch(OperationCanceledException)when(!_closing){failure=alertShown?"rejected":"timeout";return false;}
        catch(Exception ex)when(!_closing){failure="browser";AppLog.Write("auth.auto-error",new{type=ex.GetType().Name});return false;}
        finally
        {
            saved=null;
            core.ScriptDialogOpening-=AutoDialog;core.Settings.AreDefaultScriptDialogsEnabled=previousDialogs;
            if(!authenticated)
            {
                _authNotice=failure switch{
                    "rejected"=>"자동로그인이 거부되었습니다. 비밀번호 변경 또는 계정 상태를 확인하고 직접 로그인해 주세요.",
                    "account-mismatch"=>"로그인 계정이 일치하지 않습니다. 직접 로그인해 주세요.",
                    "page-structure"=>"로그인 페이지 구성이 달라 자동로그인을 중단했습니다. 직접 로그인해 주세요.",
                    "timeout"=>"자동로그인 응답이 20초를 넘어 중단했습니다. 직접 다시 로그인해 주세요.",
                    _=>"서버 응답 또는 사내망 연결을 확인하지 못했습니다. 직접 다시 로그인해 주세요."};
                AppLog.Write("auth.auto-failed",new{reason=failure});
                _pendingLoginId="";
            }
        }
    }

    private async Task AccountActionAsync()
    {
        if(_loginId.Length>0){await LogoutAsync();return;}
        if(await AuthenticateAsync(false))await LoadDirectoryAsync();
    }

    private async Task LogoutAsync(bool showLogin=true)
    {
        AppLog.Write("auth.logout-start");_autoLoginAttempted=true;ResetAccountView();
        Exception? deletionFailure=null;
        try{_auth.Delete();}
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){deletionFailure=ex;}
        await ClearSessionAsync();
        if(deletionFailure!=null){
            AppLog.Write("auth.storage-failed",new{operation="delete",type=deletionFailure.GetType().Name});
            throw new IOException("화면과 세션은 정리했지만 자동로그인 정보를 삭제하지 못했습니다. 다음 실행에서 자동로그인될 수 있으므로 저장 파일 권한을 확인하세요.");
        }
        _authNotice="로그아웃했습니다. 자동로그인 정보와 로그인 세션을 삭제했습니다.";
        _status.Text=_authNotice;AppLog.Write("auth.logout-complete");
        if(showLogin && await AuthenticateAsync(false))await LoadDirectoryAsync();
    }
}
