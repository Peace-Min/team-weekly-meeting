using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TeamWeeklyMeeting;

internal sealed partial class MeetingWindow
{
    private int _zoomPercent = 100, _zoomStep = 10;
    private bool _applyingZoom;
    private readonly TextBox _zoomInput = new() { Width = 52, Text = "100", ToolTip = "보기 배율 25~500%" };
    private readonly TextBox _stepInput = new() { Width = 42, Text = "10", ToolTip = "확대·축소 증감 폭 (%)" };
    private sealed record MeetingSettings(string Team, string Date, string[] Attendees,
        int ZoomPercent = 100, int ZoomStep = 10, double Width = 1320, double Height = 900, bool Maximized = false);
    private static string MeetingDate => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Korea Standard Time").ToString("yyyy-MM-dd");
    private string SettingsPath => Path.Combine(_dataDir,"meeting.json");

    private void LoadMeetingSettings()
    {
        _savedTeam="";_checked.Clear();_zoomPercent=100;_zoomStep=10;
        WindowState=WindowState.Normal;Width=1320;Height=900;
        string path=SettingsPath;
        try
        {
            // Import the latest previous preference once; subsequent settings are shared.
            string old=Path.Combine(_dataDir,"accounts");
            if(!File.Exists(path) && Directory.Exists(old))
                path=new DirectoryInfo(old).EnumerateFiles("*.json").OrderByDescending(f=>f.LastWriteTimeUtc).FirstOrDefault()?.FullName ?? path;
            if(File.Exists(path))
            {
                var saved=JsonSerializer.Deserialize<MeetingSettings>(File.ReadAllText(path));
                if(saved!=null)
                {
                    _savedTeam=saved.Team;_zoomPercent=Math.Clamp(saved.ZoomPercent,25,500);_zoomStep=Math.Clamp(saved.ZoomStep,1,475);
                    if(saved.Date==MeetingDate && saved.Attendees!=null)foreach(string id in saved.Attendees)_checked.Add(id);
                    Width=double.IsFinite(saved.Width)?Math.Clamp(saved.Width,MinWidth,Math.Max(MinWidth,SystemParameters.WorkArea.Width)):1320;
                    Height=double.IsFinite(saved.Height)?Math.Clamp(saved.Height,MinHeight,Math.Max(MinHeight,SystemParameters.WorkArea.Height)):900;
                    if(saved.Maximized)WindowState=WindowState.Maximized;
                    AppLog.Write("settings.loaded",new{shared=true,imported=path!=SettingsPath,team=_savedTeam,zoom=_zoomPercent,step=_zoomStep});
                }
            }
        }
        catch(Exception ex)when(ex is IOException or JsonException or UnauthorizedAccessException)
        {AppLog.Write("settings.load-failed",error:ex);_status.Text="설정을 읽지 못했습니다: "+ex.Message;}
        SyncZoomInputs();
    }

    private void SaveMeetingSettings()
    {
        Directory.CreateDirectory(_dataDir);
        var saved=new MeetingSettings(_team.SelectedItem as string??"",MeetingDate,SelectedAttendees().Select(m=>m.Id).ToArray(),
            _zoomPercent,_zoomStep,WindowState==WindowState.Normal?Width:RestoreBounds.Width,
            WindowState==WindowState.Normal?Height:RestoreBounds.Height,WindowState==WindowState.Maximized);
        File.WriteAllText(SettingsPath+".tmp",JsonSerializer.Serialize(saved));File.Move(SettingsPath+".tmp",SettingsPath,true);
    }
    private void PersistSettings()
    {
        try{SaveMeetingSettings();}
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException)
        {AppLog.Write("settings.save-failed",error:ex);_status.Text="설정 저장 실패: "+ex.Message;}
    }

    private void AddZoomInputs(WrapPanel toolbar)
    {
        foreach(var input in new[]{_zoomInput,_stepInput})
        {
            input.Margin=new Thickness(0,0,4,0);
            input.KeyDown+=async(_,e)=>{if(e.Key==Key.Enter){e.Handled=true;await RunAsync(ApplyZoomInputsAsync,"배율 직접 적용");}};
        }
        var zoom=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,16,6)};
        var step=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,6)};
        TextBlock Label(string text)=>new(){Text=text,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,6,0)};
        zoom.Children.Add(Label("배율"));zoom.Children.Add(_zoomInput);zoom.Children.Add(Label("%"));
        zoom.Children.Add(Command("축소",()=>ZoomByStepAsync(-1)));zoom.Children.Add(Command("확대",()=>ZoomByStepAsync(1)));
        zoom.Children.Add(Command("100%",()=>ZoomAsync(100-_zoomPercent)));
        step.Children.Add(Label("증감"));step.Children.Add(_stepInput);step.Children.Add(Label("%"));
        step.Children.Add(Command("적용",ApplyZoomInputsAsync));toolbar.Children.Add(zoom);toolbar.Children.Add(step);
    }

    private static int ReadNumber(TextBox input, int min, int max, string label)
    {
        if (!int.TryParse(input.Text.Trim().TrimEnd('%'), NumberStyles.None, CultureInfo.InvariantCulture, out int value)
            || value < min || value > max) throw new ArgumentException($"{label}은 {min}~{max} 사이 정수로 입력하세요.");
        return value;
    }
    private void SyncZoomInputs()
    { _zoomInput.Text = _zoomPercent.ToString(CultureInfo.InvariantCulture); _stepInput.Text = _zoomStep.ToString(CultureInfo.InvariantCulture); }
    private Task ApplyZoomInputsAsync()
    {
        int zoom = ReadNumber(_zoomInput, 25, 500, "배율");
        int step = ReadNumber(_stepInput, 1, 475, "증감 폭");
        _zoomStep = step;
        return ZoomAsync(zoom - _zoomPercent);
    }
    private Task ZoomByStepAsync(int direction)
    {
        _zoomStep = ReadNumber(_stepInput, 1, 475, "증감 폭");
        return ZoomAsync(direction * _zoomStep);
    }
    private async Task ZoomAsync(int delta)
    {
        int before=_zoomPercent;
        _zoomPercent = (int)Math.Clamp((long)_zoomPercent + delta, 25, 500);
        _applyingZoom = true;
        try { foreach (var page in _reports) page.Web.ZoomFactor = _zoomPercent / 100.0; }
        finally { _applyingZoom = false; }
        if(_presentation!=null)foreach(var page in _reports)
            await page.Web.CoreWebView2.ExecuteScriptAsync("window.__meetingAnnotationScale && window.__meetingAnnotationScale("+(100.0/_zoomPercent).ToString(CultureInfo.InvariantCulture)+")");
        AppLog.Write("view.zoom",new{before,after=_zoomPercent,step=_zoomStep});
        SyncZoomInputs();
        _status.Text = $"보기 배율 {_zoomPercent}% · 증감 {_zoomStep}%";
        PersistSettings();
    }
}
