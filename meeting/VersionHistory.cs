using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
        var window=new Window{Owner=this,Icon=Icon,Title="버전 및 업데이트 내역",Width=650,Height=640,
            MinWidth=480,MinHeight=400,WindowStartupLocation=WindowStartupLocation.CenterOwner,
            FontFamily=FontFamily,FontSize=14,Background=Brushes.White,Foreground=(Brush)FindResource("Ink")};
        var layout=new DockPanel{Margin=new Thickness(26),Background=Brushes.White};
        var close=new Button{Content="닫기",HorizontalAlignment=HorizontalAlignment.Right,MinWidth=80,Margin=new Thickness(0,16,0,0)};
        close.Click+=(_,_)=>window.Close();DockPanel.SetDock(close,Dock.Bottom);layout.Children.Add(close);
        var title=new TextBlock{Text="팀 주간보고  v"+AppVersion,FontSize=22,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,22)};
        DockPanel.SetDock(title,Dock.Top);layout.Children.Add(title);
        var body=new StackPanel();
        foreach(var entry in ReadReleaseHistory())
        {
            body.Children.Add(new TextBlock{Text=entry.Heading,FontSize=16,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,10)});
            foreach(string note in entry.Notes)body.Children.Add(new TextBlock{Text="• "+note,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)});
            body.Children.Add(new Border{Height=1,Background=(Brush)FindResource("Line"),Margin=new Thickness(0,12,0,20)});
        }
        layout.Children.Add(new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Background=Brushes.White});
        window.Content=layout;return window;
    }

    private void ShowVersionHistory()
    {
        AppLog.Write("version.open",new{version=AppVersion});
        CreateVersionWindow().ShowDialog();
    }
}
