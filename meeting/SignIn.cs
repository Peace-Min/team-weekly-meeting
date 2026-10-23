using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
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
        AppLog.Write("auth.prompt");
        _pendingLoginId = "";
        var core = _login.CoreWebView2;
        _pages.Children.Remove(_login);
        _login.Visibility = Visibility.Visible;
        var dialog = new Window { Owner = this, Icon=Icon, Title = "사내 계정 로그인", Width = 860, Height = 680,
            MinWidth = 600, MinHeight = 460, Content = _login, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        bool accepted = false, checking = false;
        async Task CheckAsync()
        {
            if (checking || accepted || _closing || !dialog.IsVisible) return;
            checking = true;
            try
            {
                string state = await core.ExecuteScriptAsync("""
                    (() => {
                      if(document.querySelector('input[type=password]'))return null;
                      if(!document.querySelector('a[href*=go_view],a[href*="pjm.jsp"],input[name=table_code][value=report_tbl],textarea[name=content]'))return null;
                      return {id:document.querySelector('input[name=id]')?.value || ''};
                    })()
                    """);
                if (state == "null") return;
                if (_pendingLoginId.Length == 0)
                {
                    using var parsed = JsonDocument.Parse(state);
                    string id = parsed.RootElement.GetProperty("id").GetString() ?? "";
                    if (Regex.IsMatch(id, @"^[A-Za-z0-9_.@-]{1,100}$")) _pendingLoginId = id;
                }
                if (_pendingLoginId.Length == 0) return;
                if (!dialog.IsVisible) return;
                dialog.DialogResult = true;
                accepted = true;
                AppLog.Write("auth.success",new{id=_pendingLoginId});
            }
            catch (Exception ex) when (!_closing)
            {
                AppLog.Write("auth.verify-failed",error:ex);
                _status.Text = "로그인 확인 실패: " + ex.Message;
            }
            catch (Exception) when (_closing) { }
            finally { checking = false; }
        }
        async void Completed(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess) { await core.ExecuteScriptAsync(LoginLayout); await CheckAsync(); }
        }
        try
        {
            // No other account's saved session is used to choose the roster identity.
            await NavigateAsync(core, () => core.Navigate("https://www.netcus.com/pjm/login.htm"));
            await core.ExecuteScriptAsync(LoginLayout);
            core.NavigationCompleted += Completed;
            dialog.ContentRendered += async (_, _) =>
            {
                await CheckAsync();
                if (!_smoke || accepted) return;
                Require(await core.ExecuteScriptAsync("(()=>{const p=document.querySelector('input[type=password]'),l=p.closest('td').previousElementSibling;return getComputedStyle(l).whiteSpace==='nowrap'&&l.scrollWidth<=l.clientWidth&&p.getBoundingClientRect().width===140})()") == "true","로그인 비밀번호 라벨 한 줄 표시 및 입력 폭 확보");
                using(var preview=System.IO.File.Create(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"TeamWeeklyMeeting-login-preview.png")))await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,preview);
                await core.ExecuteScriptAsync("document.querySelector('input[name=id]').value=" + JsonSerializer.Serialize(_smokeLoginId)
                    + ";document.querySelector('input[name=id]').dispatchEvent(new Event('input',{bubbles:true}));document.querySelector('button').click()");
            };
            dialog.ShowDialog();
            if(!accepted)AppLog.Write("auth.cancel");
            return accepted;
        }
        finally
        {
            core.NavigationCompleted -= Completed;
            dialog.Content = null;
            _login.Visibility = Visibility.Hidden;
            _pages.Children.Add(_login);
        }
    }
}
