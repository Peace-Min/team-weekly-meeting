using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TeamWeeklyMeeting;

internal sealed record ReleaseEntry(string Heading, string[] Notes);

internal sealed partial class MeetingWindow
{
    internal static string AppVersion => typeof(MeetingWindow).Assembly.GetName().Version?.ToString(3) ?? "";
    internal static ReleaseEntry[] ReadReleaseHistory()
    {
        using var stream=typeof(MeetingWindow).Assembly.GetManifestResourceStream("TeamWeeklyMeeting.Changelog.md")
            ?? throw new InvalidOperationException("업데이트 내역을 읽을 수 없습니다.");
        using var reader=new StreamReader(stream);
        var entries=new List<ReleaseEntry>();var notes=new List<string>();string heading="";
        foreach(string line in reader.ReadToEnd().Split('\n'))
        {
            if(line.StartsWith("## "))
            {
                if(heading.Length>0)entries.Add(new ReleaseEntry(heading,notes.ToArray()));
                heading=line[3..].Trim();notes.Clear();
            }
            else if(line.StartsWith("- "))notes.Add(line[2..].Trim());
        }
        if(heading.Length>0)entries.Add(new ReleaseEntry(heading,notes.ToArray()));
        return entries.ToArray();
    }

    private Window CreateVersionWindow()
    {
        var muted=new SolidColorBrush(Color.FromRgb(110,116,125));
        var window=new Window{Owner=this,Icon=Icon,Title="버전 정보",Width=560,Height=420,
            MinWidth=480,MinHeight=380,WindowStartupLocation=WindowStartupLocation.CenterOwner,
            FontFamily=FontFamily,FontSize=13,Background=Brushes.White,Foreground=(Brush)FindResource("Ink")};
        var layout=new DockPanel{Margin=new Thickness(24),Background=Brushes.White};
        var footer=new DockPanel{Margin=new Thickness(0,16,0,0)};
        var close=new Button{Content="닫기",MinWidth=72,IsCancel=true};
        close.Click+=(_,_)=>window.Close();DockPanel.SetDock(close,Dock.Right);footer.Children.Add(close);
        footer.Children.Add(new TextBlock{Text="설치된 버전의 변경 내역",Foreground=muted,FontSize=11,VerticalAlignment=VerticalAlignment.Center});
        DockPanel.SetDock(footer,Dock.Bottom);layout.Children.Add(footer);
        var header=new DockPanel{Margin=new Thickness(0,0,0,20)};
        var icon=new Image{Source=(ImageSource)Icon,Width=40,Height=40,Margin=new Thickness(0,0,12,0)};
        DockPanel.SetDock(icon,Dock.Left);header.Children.Add(icon);
        var identity=new StackPanel();
        identity.Children.Add(new TextBlock{Text="팀 주간보고",FontSize=18,FontWeight=FontWeights.SemiBold});
        identity.Children.Add(new TextBlock{Text="현재 버전  v"+AppVersion,Foreground=muted,FontSize=12,Margin=new Thickness(0,3,0,0)});
        header.Children.Add(identity);DockPanel.SetDock(header,Dock.Top);layout.Children.Add(header);
        var body=new StackPanel();
        var entries=ReadReleaseHistory();
        for(int i=0;i<entries.Length;i++)
        {
            var entry=entries[i];var parts=entry.Heading.Split('·',2);
            var heading=new DockPanel{LastChildFill=false};
            heading.Children.Add(new TextBlock{Text="v"+parts[0].Trim(),FontSize=14,FontWeight=FontWeights.SemiBold,MinWidth=76});
            heading.Children.Add(new TextBlock{Text=parts.Length>1?parts[1].Trim():"",Foreground=muted,FontSize=11,VerticalAlignment=VerticalAlignment.Center});
            if(i==0)heading.Children.Add(new Border{Background=(Brush)FindResource("BrandSoft"),CornerRadius=new CornerRadius(3),Padding=new Thickness(6,2,6,2),Margin=new Thickness(10,0,0,0),Child=new TextBlock{Text="현재",FontSize=10,Foreground=new SolidColorBrush(Color.FromRgb(159,73,0))}});
            var notes=new StackPanel{Margin=new Thickness(0,10,0,10)};
            foreach(string note in entry.Notes)
            {
                var row=new DockPanel{Margin=new Thickness(0,0,0,7)};
                var dot=new TextBlock{Text="•",Foreground=(Brush)FindResource("BrandOrange"),Width=14};DockPanel.SetDock(dot,Dock.Left);row.Children.Add(dot);
                row.Children.Add(new TextBlock{Text=note,TextWrapping=TextWrapping.Wrap,LineHeight=20});notes.Children.Add(row);
            }
            var section=new Expander{Header=heading,Content=notes,IsExpanded=i==0,HorizontalContentAlignment=HorizontalAlignment.Stretch,Padding=new Thickness(0,9,0,5)};
            body.Children.Add(section);
            body.Children.Add(new Border{Height=1,Background=(Brush)FindResource("Line")});
        }
        layout.Children.Add(new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        window.Content=layout;return window;
    }

    private void ShowVersionHistory()
    {
        AppLog.Write("version.open",new{version=AppVersion});
        CreateVersionWindow().ShowDialog();
    }
}
