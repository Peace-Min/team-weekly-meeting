using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;

namespace TeamWeeklyMeeting;

internal sealed partial class MeetingWindow
{
    private readonly ComboBox _team = new() { MinHeight = 34, Margin = new Thickness(0, 6, 6, 8), FontSize = 14 };
    private readonly TreeView _attendance = new() { Height = 215, BorderThickness = new Thickness(0), Background = System.Windows.Media.Brushes.Transparent };
    private readonly TextBlock _attendeeCount = new() { Margin = new Thickness(0, 1, 0, 1) };
    private MeetingDirectory _directory = new();
    private readonly HashSet<string> _checked = new(StringComparer.Ordinal);
    private string _loginId = "", _pendingLoginId = "", _savedTeam = "";
    private bool _settingDirectory;

    private void BindTeamSelection()
    {
        _team.SelectionChanged += (_, _) =>
        {
            if (_settingDirectory) return;
            string team = _team.SelectedItem as string ?? "";
            if (team != _savedTeam) { _checked.Clear(); _savedTeam = team; }
            AppLog.Write("team.select",new{team});
            RenderAttendance();PersistSettings();
        };
    }

    private async Task TrackLoginIdAsync(CoreWebView2 core)
    {
        core.WebMessageReceived += (_, e) =>
        {
            if (!IsCompanyUrl(e.Source)) return;
            try
            {
                using var message = JsonDocument.Parse(e.WebMessageAsJson);
                if (message.RootElement.GetProperty("kind").GetString() != "meeting-login") return;
                string id = message.RootElement.GetProperty("id").GetString() ?? "";
                if (System.Text.RegularExpressions.Regex.IsMatch(id, @"^[A-Za-z0-9_.@-]{1,100}$")) _pendingLoginId = id;
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException) { }
        };
        await core.AddScriptToExecuteOnDocumentCreatedAsync("""
            (() => {
              function capture() {
                if (!document.querySelector('input[type=password]')) return;
                const id = document.querySelector('input[name=id]');
                if (id && id.value) chrome.webview.postMessage({kind:'meeting-login',id:id.value});
              }
              for (const event of ['input','change','click','submit','keydown']) document.addEventListener(event, capture, true);
            })()
            """);
    }

    private async Task LoadDirectoryAsync()
    {
        if (!await CheckSessionAsync()) return;
        await RefreshDirectoryForAccountAsync();
    }

    private async Task RefreshDirectoryForAccountAsync()
    {
        ClearReports();
        if (_pendingLoginId.Length > 0 && _pendingLoginId != _loginId)
        {
            _loginId = _pendingLoginId;

        }
        string json = _smoke ? FixtureDirectory : await TaskCalendarWidget.MeetingIntegration.LoadDirectoryAsync(_loginId, message=>AppLog.Write("directory.db",new{message}));
        SetDirectory(MeetingDirectory.Parse(json));
        SaveMeetingSettings();
        _pendingLoginId = "";
        _login.Visibility = Visibility.Hidden;
        _heading.Text = "오늘 참석자를 체크하세요";
        _details.Text = "선택한 팀의 참석자 보고서를 한 번에 준비합니다.";
        _status.Text = "팀을 선택한 뒤 오늘 참석하는 사람을 체크하세요.";
    }

    private void SetDirectory(MeetingDirectory directory)
    {
        _directory = directory;
        AppLog.Write("directory.loaded",new{directory.MyUnit,directory.Units,directory.Members});
        string selected = directory.Units.Any(u => u.Name == _savedTeam) ? _savedTeam
            : directory.Units.Any(u => u.Name == directory.MyUnit) ? directory.MyUnit : directory.Units.FirstOrDefault()?.Name ?? "";
        _savedTeam = selected;
        _settingDirectory = true;
        try
        {
            _team.ItemsSource = directory.OrderedUnits().Select(u => u.Name).ToArray();
            _team.SelectedItem = selected;
        }
        finally { _settingDirectory = false; }
        _checked.IntersectWith(directory.Members.Select(m => m.Id));
        RenderAttendance();
    }

    private void RenderAttendance()
    {
        _attendance.Items.Clear();
        string team = _team.SelectedItem as string ?? "";
        if (team.Length == 0) { _attendeeCount.Text = "조직도를 불러온 뒤 팀을 선택하세요."; return; }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        TreeViewItem Build(string name)
        {
            var node = new TreeViewItem { Header = name, IsExpanded = true, FontSize = 14, Margin = new Thickness(0, 3, 0, 3) };
            if (!seen.Add(name)) return node;
            foreach (var member in _directory.Members.Where(m => m.Unit == name))
            {
                var box = new CheckBox { Content = member.Name + (member.Title.Length == 0 ? "" : "  " + member.Title),
                    Tag = member.Id, IsChecked = _checked.Contains(member.Id), Margin = new Thickness(0, 1, 0, 1), FontWeight = FontWeights.Normal };
                box.Checked += (_, _) => { AppLog.Write("attendee.check",new{member.Id,member.Name}); _checked.Add(member.Id); UpdateAttendeeCount(); PersistSettings(); };
                box.Unchecked += (_, _) => { AppLog.Write("attendee.uncheck",new{member.Id,member.Name}); _checked.Remove(member.Id); UpdateAttendeeCount(); PersistSettings(); };
                node.Items.Add(box);
            }
            foreach (var unit in _directory.Units.Where(u => u.Parent == name)) node.Items.Add(Build(unit.Name));
            return node;
        }
        _attendance.Items.Add(Build(team));
        UpdateAttendeeCount();
    }

    private Member[] TeamMembers()
    {
        string team = _team.SelectedItem as string ?? "";
        if (team.Length == 0) return Array.Empty<Member>();
        var units = _directory.Subtree(team);
        return _directory.OrderedUnits().Where(u=>units.Contains(u.Name))
            .SelectMany(u=>_directory.Members.Where(m=>m.Unit==u.Name)).ToArray();
    }
    private Member[] SelectedAttendees() => TeamMembers().Where(m => _checked.Contains(m.Id)).ToArray();
    private void UpdateAttendeeCount() => _attendeeCount.Text = $"참석 {SelectedAttendees().Length}명 / 팀 {TeamMembers().Length}명";
    private Task CheckAllAsync(bool value)
    {
        AppLog.Write("attendee.check-all",new{value,team=_team.SelectedItem});
        foreach (var member in TeamMembers()) { if (value) _checked.Add(member.Id); else _checked.Remove(member.Id); }
        RenderAttendance();
        PersistSettings();
        return Task.CompletedTask;
    }

}
