using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;

namespace TeamWeeklyMeeting;

internal sealed partial class MeetingWindow
{
    private const string LoginLayout = """
        (()=>{
          const password=document.querySelector('input[type=password]');
          const table=password?.closest('table');if(!table)return;
          table.style.width='240px';
          for(const input of table.querySelectorAll('input:not([type=hidden])')){
            const cell=input.closest('td'),label=cell?.previousElementSibling;
            if(label){label.style.whiteSpace='nowrap';label.style.width='90px'}
            if(cell)cell.style.width='150px';
            input.style.width='140px';input.style.boxSizing='border-box';
          }
        })()
        """;

    private async Task<bool> SignInAsync()
    {
        AppLog.Write("auth.prompt");ResetAccountView();await ClearSessionAsync();
        var core=_login.CoreWebView2;
        _pages.Children.Remove(_login);_login.Visibility=Visibility.Visible;
        var layout=new DockPanel();
        var options=new StackPanel{Margin=new Thickness(22,12,22,14)};
        var remember=new CheckBox{Content="자동로그인",IsChecked=_auth.Exists};
        var warning=new TextBlock{Text="공용 PC에서는 체크하지 마세요. 다음 실행부터 이 계정으로 자동 로그인됩니다.",FontSize=12,TextWrapping=TextWrapping.Wrap,Foreground=System.Windows.Media.Brushes.DimGray,Margin=new Thickness(0,4,0,0)};
        options.Children.Add(remember);options.Children.Add(warning);DockPanel.SetDock(options,Dock.Bottom);layout.Children.Add(options);
        var notice=new TextBlock{Text=_authNotice,TextWrapping=TextWrapping.Wrap,Foreground=System.Windows.Media.Brushes.SaddleBrown,Margin=new Thickness(22,12,22,0)};
        DockPanel.SetDock(notice,Dock.Top);layout.Children.Add(notice);_authNotice="";
        var retry=new Button{Content="로그인 페이지 다시 연결",HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(22,8,22,0),Visibility=Visibility.Collapsed};
        DockPanel.SetDock(retry,Dock.Top);layout.Children.Add(retry);layout.Children.Add(_login);
        var dialog=new Window{Owner=this,Icon=Icon,Title="사내 계정 로그인",Width=860,Height=740,MinWidth=600,MinHeight=500,Content=layout,WindowStartupLocation=WindowStartupLocation.CenterOwner,FontFamily=FontFamily,FontSize=13,Background=System.Windows.Media.Brushes.White};
        bool accepted=false,checking=false,smokeDriven=false;
        _captureLoginSecrets=remember.IsChecked==true;
        async Task ApplyPageAsync()
        {
            await core.ExecuteScriptAsync(LoginLayout);await core.ExecuteScriptAsync(CaptureLogin);
            await core.ExecuteScriptAsync("window.__meetingRemember="+(_captureLoginSecrets?"true":"false"));
        }
        async Task OpenPageAsync()
        {
            retry.IsEnabled=false;
            try{await NavigateAsync(core,()=>core.Navigate("https://www.netcus.com/pjm/login.htm"));await ApplyPageAsync();retry.Visibility=Visibility.Collapsed;}
            catch(Exception ex)when(!_closing){notice.Text="로그인 페이지에 연결하지 못했습니다. 사내망 연결을 확인한 뒤 다시 연결하세요.";retry.Visibility=Visibility.Visible;AppLog.Write("auth.manual-connect-failed",new{type=ex.GetType().Name});}
            finally{retry.IsEnabled=true;}
        }
        retry.Click+=async(_,_)=>await OpenPageAsync();
        remember.Checked+=async(_,_)=>{_captureLoginSecrets=true;await ApplyPageAsync();};
        remember.Unchecked+=async(_,_)=>{
            _captureLoginSecrets=false;_pendingCredential=null;_autoLoginAttempted=true;
            try{_auth.Delete();notice.Text="자동로그인을 해제하고 저장 정보를 삭제했습니다.";AppLog.Write("auth.remember-disabled");}
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){notice.Text="저장 정보를 삭제하지 못했습니다. 다음 실행에서 자동로그인될 수 있습니다.";AppLog.Write("auth.storage-failed",new{operation="delete",type=ex.GetType().Name});}
            await ApplyPageAsync();
        };
        async Task CheckAsync()
        {
            if(checking||accepted||_closing||!dialog.IsVisible)return;
            checking=true;
            try{
                string actor=await AuthenticatedIdAsync();if(actor.Length==0)return;
                if(_pendingLoginId.Length>0&&actor!=_pendingLoginId){notice.Text="입력한 계정과 인증된 계정이 다릅니다. 다시 로그인해 주세요.";return;}
                _pendingLoginId=actor;
                try{
                    if(remember.IsChecked!=true)_auth.Delete();
                    else if(_pendingCredential?.Id==actor){_auth.Save(_pendingCredential);AppLog.Write("auth.remember-saved");}
                    else {
                        _auth.Delete();_autoLoginAttempted=true;AppLog.Write("auth.remember-unavailable");
                        if(!_smoke)MessageBox.Show("로그인은 성공했지만 자동로그인 정보를 확인하지 못해 기존 저장 정보를 삭제했습니다. 다음 실행에서는 직접 로그인해 주세요.","자동로그인",MessageBoxButton.OK,MessageBoxImage.Warning);
                    }
                }catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException){
                    _autoLoginAttempted=true;bool retained=false;
                    try{_auth.Delete();}catch(Exception removal)when(removal is IOException or UnauthorizedAccessException){retained=true;}
                    AppLog.Write("auth.storage-failed",new{operation="save",type=ex.GetType().Name});
                    if(!_smoke)MessageBox.Show(retained?"로그인은 성공했지만 저장 정보를 갱신·삭제하지 못했습니다. 다음 실행에서 이전 저장 계정으로 자동로그인될 수 있으므로 파일 권한을 확인하세요.":"로그인은 성공했지만 자동로그인 정보를 저장하지 못했습니다. 다음 실행에서는 직접 로그인해 주세요.","자동로그인",MessageBoxButton.OK,MessageBoxImage.Warning);
                }
                accepted=true;AppLog.Write("auth.success",new{id=actor});dialog.DialogResult=true;
            }catch(Exception ex)when(!_closing){AppLog.Write("auth.verify-failed",new{type=ex.GetType().Name});notice.Text="로그인 상태를 확인하지 못했습니다. 다시 로그인해 주세요.";}
            finally{checking=false;}
        }
        async void Completed(object? sender,CoreWebView2NavigationCompletedEventArgs e)
        {try{if(e.IsSuccess){await ApplyPageAsync();await CheckAsync();}else{notice.Text="서버 연결을 확인하지 못했습니다. 다시 연결해 주세요.";retry.Visibility=Visibility.Visible;}}catch(Exception)when(_closing||!dialog.IsVisible){}}
        core.NavigationCompleted+=Completed;
        try{
            await OpenPageAsync();
            using var blockedStore=_smokeBlockSave?new FileStream(_auth.PathName,FileMode.Open,FileAccess.Read,FileShare.None):null;
            dialog.ContentRendered+=async(_,_)=>{
                await CheckAsync();if(!_smoke||accepted||!dialog.IsVisible||smokeDriven)return;
                smokeDriven=true;
                if(_smokeDisableRemember){remember.IsChecked=false;await Task.Delay(100);}
                if(_smokeCancelLogin){dialog.Close();return;}
                Require(await core.ExecuteScriptAsync("(()=>{const p=document.querySelector('input[type=password]'),l=p.closest('td').previousElementSibling;return getComputedStyle(l).whiteSpace==='nowrap'&&l.scrollWidth<=l.clientWidth&&p.getBoundingClientRect().width===140})()") == "true","로그인 비밀번호 라벨 한 줄 표시 및 입력 폭 확보");
                if(_smokeRemember)remember.IsChecked=true;
                if(_smokeSuppressCapture)await core.ExecuteScriptAsync("window.__meetingRemember=false");
                using(var preview=File.Create(Path.Combine(Path.GetTempPath(),"TeamWeeklyMeeting-login-preview.png")))await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,preview);
                string submit="document.querySelector('input[name=id]').value="+JsonSerializer.Serialize(_smokeLoginId)+";document.querySelector('input[type=password]').value="+JsonSerializer.Serialize(SmokePassword)+";goLogin()";
                if(_smokeRejectFirst){
                    byte[] original=File.ReadAllBytes(_auth.PathName);_fixtureAuthMode="rejected";
                    await NavigateAsync(core,()=>_ = core.ExecuteScriptAsync(submit.Replace(SmokePassword,SmokePassword+"-wrong")));
                    Require(System.Linq.Enumerable.SequenceEqual(original,File.ReadAllBytes(_auth.PathName)),"인증 실패 입력으로 기존 저장 정보를 덮어쓰지 않음");
                    _fixtureAuthMode="success";await OpenPageAsync();
                }
                await core.ExecuteScriptAsync(submit);
            };
            dialog.ShowDialog();if(!accepted)AppLog.Write("auth.cancel");return accepted;
        }finally{
            _captureLoginSecrets=false;_pendingCredential=null;core.NavigationCompleted-=Completed;
            core.Stop();layout.Children.Remove(_login);dialog.Content=null;_login.Visibility=Visibility.Hidden;_pages.Children.Add(_login);
        }
    }
}
