using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace TeamWeeklyMeeting;

internal sealed partial class MeetingWindow
{
    internal const string SupportEmail = "phmin@netcus.com";
    private Window CreateSupportWindow()
    {
        var window = new Window { Owner = this, Icon = Icon, Title = "문의 및 버그제보", Width = 620, Height = 550,
            MinWidth = 520, MinHeight = 450, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            FontFamily = FontFamily, FontSize = 14, Foreground=(System.Windows.Media.Brush)FindResource("Ink"), Background = System.Windows.Media.Brushes.White };
        var body = new StackPanel { Margin = new Thickness(26) };
        void Text(string value, bool bold = false)
        { body.Children.Add(new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12), FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal }); }
        Text("문의 및 버그제보", true);
        Text("팀 주간보고 v"+AppVersion);
        Text("불편한 점이나 개선 의견을 이메일 또는 랜메신저로 보내 주세요.");
        Text(SupportEmail + "\n랜메신저 민평화", true);
        var mail = new Button { Content = "메일 주소 복사", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 20) };
        mail.Click += (_, _) => CopySupportText(SupportEmail, "메일 주소"); body.Children.Add(mail);
        Text("무엇을 하다가 생겼는지, 기대한 동작과 실제 결과, 발생 시각을 함께 알려 주세요.");
        Text("진단 기록", true);
        body.Children.Add(new TextBox { Text = AppLog.FilePath, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        var actions = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
        var folder = new Button { Content = "로그 폴더 열기", Margin = new Thickness(0, 0, 8, 0) };
        folder.Click += (_, _) =>
        {
            try
            {
                AppLog.Write("support.open-log-folder");
                var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
                start.ArgumentList.Add(AppLog.DirectoryPath); Process.Start(start);
            }
            catch(Exception ex){AppLog.Write("support.open-log-folder-failed",error:ex);MessageBox.Show(window,"폴더를 열지 못했습니다. 표시된 경로를 직접 복사해 주세요.",window.Title);}
        }; actions.Children.Add(folder);
        var path = new Button { Content = "로그 경로 복사" };
        path.Click += (_, _) => CopySupportText(AppLog.FilePath, "로그 경로"); actions.Children.Add(path); body.Children.Add(actions);
        Text("meeting.log와 번호가 붙은 이전 로그도 함께 보내 주세요. 기록에는 사용자·조직명과 접속 정보가 포함될 수 있으므로 사내에서만 공유하세요.");
        if (AppLog.Failure.Length > 0) Text("로그 저장 오류: " + AppLog.Failure);
        var close = new Button { Content = "닫기", HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 80 };
        close.Click += (_, _) => window.Close(); body.Children.Add(close);
        window.Content = new ScrollViewer { Background=System.Windows.Media.Brushes.White, Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        return window;
    }
    private void ShowSupport()
    { AppLog.Write("support.open"); CreateSupportWindow().ShowDialog(); }
    private void CopySupportText(string text, string label)
    {
        try { Clipboard.SetText(text); _status.Text = label + "를 복사했습니다."; AppLog.Write("support.copy", new { label }); }
        catch (Exception ex) { AppLog.Write("support.copy-failed", new { label }, ex); MessageBox.Show("복사하지 못했습니다. 표시된 내용을 직접 선택해 복사해 주세요.", Title); }
    }
}
