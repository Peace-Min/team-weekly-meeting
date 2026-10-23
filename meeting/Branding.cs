using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TeamWeeklyMeeting;

internal sealed partial class MeetingWindow
{
    private Button? _reportStartButton;
    private void UpdateReportStartButton()
    {
        if(_reportStartButton==null)return;
        _reportStartButton.IsEnabled=!_busy && _environment!=null && _reports.Count>0;
        _reportStartButton.ToolTip=_reports.Count==0?"참석자 보고서를 먼저 준비하세요":"보고서만 전체 화면으로 표시 · Esc로 종료";
    }
    private void ApplyBranding()
    {
        if (!Application.Current.Resources.Contains("BrandOrange"))
            Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary {
                Source = new Uri("pack://application:,,,/TeamWeeklyMeeting;component/BrandTheme.xaml") });
        // WPF centers the ICO's first 16px frame inside its 32px taskbar icon.
        // A full-size source lets WPF scale to each native icon size instead.
        Icon = new BitmapImage(new Uri("pack://application:,,,/TeamWeeklyMeeting;component/Assets/company-app-icon.png"));
    }

    private Border BuildBrandHeader()
    {
        var logo = new BitmapImage(new Uri("pack://application:,,,/TeamWeeklyMeeting;component/Assets/company-mark.png"));
        var header = new DockPanel { LastChildFill = true };
        var contact = new Button { Content = "문의 및 버그제보", Style = (Style)FindResource("HeaderButton"),
            VerticalAlignment = VerticalAlignment.Center, ToolTip = "phmin@netcus.com · 랜메신저 민평화" };
        contact.Click += (_, _) => ShowSupport();
        DockPanel.SetDock(contact, Dock.Right); header.Children.Add(contact);
        var history=new Button{Content="v"+AppVersion+" · 업데이트 내역",Style=(Style)FindResource("HeaderButton"),
            VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,10,0)};
        history.Click+=(_,_)=>ShowVersionHistory();DockPanel.SetDock(history,Dock.Right);header.Children.Add(history);
        var mark = new Image { Source = logo,
            Width = 48, Height = 48, Margin = new Thickness(0, 0, 14, 0), Stretch = Stretch.Uniform };
        DockPanel.SetDock(mark, Dock.Left); header.Children.Add(mark);
        var brand = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        brand.Children.Add(new TextBlock { Text = "넷커스터마이즈", Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32)), FontSize = 18, FontWeight = FontWeights.SemiBold });
        brand.Children.Add(new TextBlock { Text = "팀 주간보고", Foreground = new SolidColorBrush(Color.FromRgb(96, 96, 96)),
            FontSize = 12, Margin = new Thickness(0, 4, 0, 0) });
        header.Children.Add(brand);
        return new Border { Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(226, 229, 233)),
            BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(22, 12, 24, 12), Child = header };
    }

    private Grid BuildSidebar()
    {
        var grid = new Grid { Width = 280, Margin = new Thickness(18, 18, 18, 12) };
        foreach (var height in new[] { GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto,
            GridLength.Auto, new GridLength(.65, GridUnitType.Star) }) grid.RowDefinitions.Add(new RowDefinition { Height = height });
        grid.RowDefinitions[1].MinHeight = 150; grid.RowDefinitions[4].MinHeight = 85;
        var top = new StackPanel();
        top.Children.Add(new TextBlock { Text = "참석할 팀", FontWeight = FontWeights.SemiBold });
        top.Children.Add(_team);
        top.Children.Add(new TextBlock { Text = "오늘 참석자", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 8) });
        grid.Children.Add(top);
        _attendance.Height = double.NaN; Grid.SetRow(_attendance, 1); grid.Children.Add(_attendance);
        var actions = new StackPanel { Margin = new Thickness(0, 8, 0, 12) };
        var choices = new WrapPanel();
        choices.Children.Add(Command("전체 체크", () => CheckAllAsync(true)));
        choices.Children.Add(Command("전체 해제", () => CheckAllAsync(false)));
        actions.Children.Add(choices); actions.Children.Add(_attendeeCount);
        actions.Children.Add(Command("참석자 보고서 준비", PrepareAsync));
        Grid.SetRow(actions, 2); grid.Children.Add(actions);
        var label = new TextBlock { Text = "발표 순서", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) };
        Grid.SetRow(label, 3); grid.Children.Add(label);
        Grid.SetRow(_people, 4); grid.Children.Add(_people);
        BindTeamSelection();
        return grid;
    }

    private WrapPanel BuildViewerToolbar()
    {
        var toolbar = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        void Group(params Button[] buttons)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 14, 0) };
            foreach (var button in buttons) row.Children.Add(button);
            toolbar.Children.Add(row);
        }
        Group(Command("이전", () => MoveAsync(-1)), Command("다음", () => MoveAsync(1)));
        var refresh=Command("현재 보고서 갱신",RefreshSelectedAsync);
        refresh.ToolTip="현재 선택한 발표자의 최신 주간보고를 다시 엽니다.";
        Group(refresh, Command("창 분리", DetachAsync));
        _reportStartButton=Command("보고 시작",StartPresentationAsync);
        toolbar.Children.Add(_reportStartButton);UpdateReportStartButton();
        return toolbar;
    }
}
