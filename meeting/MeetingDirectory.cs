using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace TeamWeeklyMeeting;

internal sealed record Member(string Name, string Id, string Unit = "", string Title = "");
internal sealed record OrgUnit(string Name, string Parent, int Order);

internal sealed class MeetingDirectory
{
    internal OrgUnit[] Units = Array.Empty<OrgUnit>();
    internal Member[] Members = Array.Empty<Member>();
    internal string MyUnit = "";

    internal static MeetingDirectory Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.GetProperty("found").GetBoolean()) throw new InvalidOperationException("조직도를 불러오지 못했습니다.");
        string Text(JsonElement item, string key) => item.TryGetProperty(key, out var value) ? value.GetString() ?? "" : "";
        return new MeetingDirectory
        {
            MyUnit = Text(root, "myUnit"),
            Units = root.GetProperty("units").EnumerateArray().Select(u => new OrgUnit(Text(u, "name"), Text(u, "parent"),
                u.TryGetProperty("sortOrder", out var order) ? order.GetInt32() : 0)).OrderBy(u => u.Order).ThenBy(u => u.Name, StringComparer.Ordinal).ToArray(),
            Members = root.GetProperty("members").EnumerateArray().Select(m => new Member(Text(m, "name"), Text(m, "loginId"),
                Text(m, "orgUnit"), Text(m, "title"))).Where(m => m.Id.Length > 0).DistinctBy(m => m.Id).ToArray()
        };
    }

    internal HashSet<string> Subtree(string name)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Walk(string unit)
        {
            if (!seen.Add(unit)) return;
            foreach (var child in Units.Where(u => u.Parent == unit)) Walk(child.Name);
        }
        Walk(name);
        return seen;
    }

    internal OrgUnit[] OrderedUnits()
    {
        var output = new List<OrgUnit>(); var seen = new HashSet<string>(StringComparer.Ordinal);
        void Walk(OrgUnit unit)
        {
            if (!seen.Add(unit.Name)) return;
            output.Add(unit);
            foreach (var child in Units.Where(u => u.Parent == unit.Name)) Walk(child);
        }
        foreach (var root in Units.Where(u => !Units.Any(p => p.Name == u.Parent))) Walk(root);
        foreach (var remaining in Units) Walk(remaining);
        return output.ToArray();
    }
}
