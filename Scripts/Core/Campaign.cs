using System;
using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Core;

// How hard the campaign holds the world to its script (design doc 2.3).
public enum Adherence { Gentle, Steady, Firm, Strict }

// The campaign (design doc 2.3, 4.4): an overarching script of dated beats
// the director works toward - the Pemmican Proclamation, Seven Oaks, the
// merger - read from a JSON file the player can copy and rewrite.
//
//   none        no script: the world runs on its own pressures
//   historical  Campaigns/historical.json, the 1811-1821 arc
//   user:NAME   user://campaigns/NAME.json, the player's own
//
// Adherence sets how far the script may lean on the world:
//   Gentle  fixed facts (the blockade, a post's founding) and the director
//           favouring news that bears on what is due - nothing else
//   Steady  + each beat's pressure as its date nears (tension, London's orders)
//   Firm    + an event the world hasn't produced by twice its grace window
//           is made to happen
//   Strict  + made to happen as soon as its grace window runs out
//
// Gentle is design doc 2.2's director exactly: switch it off and the world
// is still valid. Firm and Strict break that on purpose - the player asked
// for history to keep its shape - and every forced beat is logged as forced.
// Whatever happens, nobody knows of it until a letter or a witness carries
// it to them (design doc 10): news is a sealed Message that has to arrive.
public static class Campaign
{
    public const string BuiltInDir = "res://Campaigns";
    public const string UserDir = "user://campaigns";

    public static string Id = "historical";
    public static Adherence Level = Adherence.Steady;
    public static string Title { get; private set; } = "";
    public static string Description { get; private set; } = "";
    public static int StartYear { get; private set; } = 1811;

    private class Beat
    {
        public string Id, Title, Kind = "event";
        public int Date = -1, Window = 60, LeadDays = 30, DelayDays;
        public string[] Requires = Array.Empty<string>(), When = Array.Empty<string>(), DoneWhen = Array.Empty<string>(), Keywords = Array.Empty<string>();
        public Godot.Collections.Array Pressure = new(), Happens = new();
    }

    private static readonly List<Beat> _beats = new();
    private static bool _postsByYear;
    private static readonly HashSet<string> _flags = new();
    private static readonly HashSet<string> _pressured = new();
    private static readonly HashSet<string> _done = new();
    private static readonly Dictionary<string, int> _readySince = new();
    private static readonly HashSet<string> _founded = new();
    private static readonly List<(int Day, Godot.Collections.Dictionary News)> _inTransit = new();
    public static readonly List<string> Log = new();
    private static int _lastDay = int.MinValue;
    private static bool _begun;

    // ---- choosing ------------------------------------------------------------

    // Everything a new game could follow, for the menu: (id, title, description).
    public static List<(string Id, string Title, string Description)> Available()
    {
        var list = new List<(string, string, string)> { ("none", "No script", "The world runs on its own pressures. Posts founded after the start date appear only if someone founds them.") };
        foreach (var (id, path) in Files())
        {
            var d = Read(path);
            if (d == null) continue;
            list.Add((id, Str(d, "title", id), Str(d, "description", "")));
        }
        return list;
    }

    private static IEnumerable<(string Id, string Path)> Files()
    {
        foreach (var f in DirAccess.GetFilesAt(BuiltInDir))
        {
            string name = f.Replace(".import", "").Replace(".remap", "");
            if (name.EndsWith(".json")) yield return (name[..^5], $"{BuiltInDir}/{name}");
        }
        if (DirAccess.DirExistsAbsolute(UserDir))
            foreach (var f in DirAccess.GetFilesAt(UserDir))
                if (f.EndsWith(".json")) yield return ("user:" + f[..^5], $"{UserDir}/{f}");
    }

    private static string PathOf(string id)
    {
        foreach (var (fid, path) in Files()) if (fid == id) return path;
        return null;
    }

    // The players' folder, made on first use with a copy of the historical
    // script to start from and a note on the format.
    public static string EnsureUserFolder()
    {
        DirAccess.MakeDirRecursiveAbsolute(UserDir);
        string copy = $"{UserDir}/my-campaign.json";
        if (!FileAccess.FileExists(copy))
        {
            var src = Read($"{BuiltInDir}/historical.json");
            if (src != null)
            {
                src["title"] = "My campaign (a copy of the historical script)";
                using var f = FileAccess.Open(copy, FileAccess.ModeFlags.Write);
                f?.StoreString(Json.Stringify(src, "  "));
            }
        }
        var readme = $"{UserDir}/README.txt";
        if (!FileAccess.FileExists(readme))
        {
            using var f = FileAccess.Open(readme, FileAccess.ModeFlags.Write);
            f?.StoreString(Readme);
        }
        return ProjectSettings.GlobalizePath(UserDir);
    }

    // ---- reading a script ------------------------------------------------------

    private static Godot.Collections.Dictionary Read(string path)
    {
        if (path.StartsWith("res://") && ResourceLoader.Exists(path))
        {
            var j = GD.Load<Json>(path);
            if (j?.Data.VariantType == Variant.Type.Dictionary) return j.Data.AsGodotDictionary();
        }
        if (!FileAccess.FileExists(path)) return null;
        var parsed = Json.ParseString(FileAccess.GetFileAsString(path));
        if (parsed.VariantType != Variant.Type.Dictionary) { GD.PushWarning($"[campaign] {path} is not a JSON object"); return null; }
        return parsed.AsGodotDictionary();
    }

    private static void LoadScript()
    {
        _beats.Clear(); _postsByYear = false; Title = "No script"; Description = "";
        if (Id == "none") return;
        var path = PathOf(Id);
        var d = path == null ? null : Read(path);
        if (d == null) { GD.PushWarning($"[campaign] no script '{Id}' - running with none"); Id = "none"; return; }
        Title = Str(d, "title", Id); Description = Str(d, "description", "");
        _postsByYear = d.TryGetValue("posts_by_founding_year", out var pby) && pby.AsBool();
        if (!d.TryGetValue("beats", out var bs)) return;
        foreach (var bv in bs.AsGodotArray())
        {
            var b = bv.AsGodotDictionary();
            var beat = new Beat
            {
                Id = Str(b, "id", $"beat{_beats.Count}"), Title = Str(b, "title", ""), Kind = Str(b, "kind", "event"),
                Date = b.TryGetValue("date", out var dt) ? DayOf(dt.AsString()) : -1,
                Window = b.TryGetValue("window_days", out var w) ? w.AsInt32() : 60,
                LeadDays = b.TryGetValue("lead_days", out var l) ? l.AsInt32() : 30,
                DelayDays = b.TryGetValue("delay_days", out var dl) ? dl.AsInt32() : 0,
                Requires = Strings(b, "requires"), When = Strings(b, "when"), DoneWhen = Strings(b, "done_when"), Keywords = Strings(b, "keywords"),
                Pressure = b.TryGetValue("pressure", out var p) ? p.AsGodotArray() : new(),
                Happens = b.TryGetValue("happens", out var h) ? h.AsGodotArray() : new(),
            };
            _beats.Add(beat);
        }
    }

    // ---- the game ---------------------------------------------------------------

    // A new world (server): read the script, note the start year.
    public static void Begin()
    {
        if (_begun) return;
        _begun = true;
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--campaign=")) Id = arg.Substring(11);
            if (arg.StartsWith("--adherence=") && Enum.TryParse(arg.Substring(12), true, out Adherence a)) Level = a;
        }
        var cal = GameCalendar.Instance;
        StartYear = cal?.Year ?? 1811;
        LoadScript();
        // A later start: what was dated before it is history already. Its
        // flags hold, but nobody is sent news of it or pushed toward it.
        int today = cal == null ? 0 : DayOf(cal.Year, cal.Month, cal.Day);
        foreach (var b in _beats)
        {
            if (b.Date < 0 || b.Date >= today || !All(b.Requires, r => _done.Contains(r) || _flags.Contains(r))) continue;
            foreach (var av in b.Happens)
            {
                var a = av.AsGodotDictionary();
                string act = Str(a, "do", "");
                if (act == "flag") _flags.Add(Str(a, "name", b.Id));
                else if (act == "unflag") _flags.Remove(Str(a, "name", b.Id));
                else if (act == "found_post") _founded.Add(Str(a, "place", ""));
            }
            _done.Add(b.Id); _flags.Add(b.Id);
        }
        GD.Print($"[campaign] {Title} ({Level}); {_beats.Count} beats");
    }

    public static void Reset()
    {
        _begun = false;
        _flags.Clear(); _pressured.Clear(); _done.Clear(); _readySince.Clear(); _founded.Clear(); _inTransit.Clear(); Log.Clear();
        _lastDay = int.MinValue;
    }

    // Once a game day, on the server.
    public static void Tick()
    {
        var cal = GameCalendar.Instance;
        if (cal == null || !Net.Net.IsServer) return;
        if (!_begun) { if (SaveGame.HasPending) return; Begin(); }
        int today = DayOf(cal.Year, cal.Month, cal.Day);
        if (today == _lastDay) return;
        _lastDay = today;

        if (_postsByYear && cal.Month == 1 && cal.Day == 1)
            foreach (var p in WorldMap.Places)
                if (p.Kind == "post" && p.Year == cal.Year && p.Year > StartYear && _founded.Add(p.Name))
                { Note($"{p.Name} is founded ({p.Company})."); World.Posts.Sync(true); }

        foreach (var b in _beats)
        {
            if (_done.Contains(b.Id)) continue;
            if (!All(b.Requires, r => _done.Contains(r) || _flags.Contains(r)) || !All(b.When, Holds)) continue;
            if (!_readySince.ContainsKey(b.Id)) _readySince[b.Id] = today;
            // The world got there by itself.
            if (b.DoneWhen.Length > 0 && All(b.DoneWhen, Holds)) { Finish(b, false); continue; }
            int due = b.Date >= 0 ? b.Date : _readySince[b.Id] + b.DelayDays;
            if (today < due - b.LeadDays) continue;

            if (Level >= Adherence.Steady && _pressured.Add(b.Id)) Run(b, b.Pressure);
            if (today < due) continue;
            switch (b.Kind)
            {
                // Outside anyone's reach (design doc 4.4): the blockade, the
                // colony's founding, Selkirk's answer to a governor's killing.
                case "fixed" or "conditional": Run(b, b.Happens); Finish(b, false); break;
                case "pressure": if (Level == Adherence.Gentle || _pressured.Contains(b.Id)) Finish(b, false); break;
                default:
                    int grace = Mathf.Max(b.Window, 1);
                    bool force = Level == Adherence.Strict && today >= due + grace || Level == Adherence.Firm && today >= due + 2 * grace;
                    if (force) { Run(b, b.Happens); Finish(b, true); }
                    break;
            }
        }

        for (int i = _inTransit.Count - 1; i >= 0; i--)
        {
            if (_inTransit[i].Day > today) continue;
            var m = Letters.Build("news", _inTransit[i].News);
            var to = Mail.Addressee(m);
            if (to == null) continue;   // nobody there to take it yet; it waits
            Mail.Give(m, to);
            _inTransit.RemoveAt(i);
        }
    }

    private static void Finish(Beat b, bool forced)
    {
        _done.Add(b.Id);
        _flags.Add(b.Id);
        Note(forced ? $"[forced] {b.Title}" : $"{b.Title}");
    }

    private static void Note(string text)
    {
        var line = $"{GameCalendar.Instance?.DateString}: {text}";
        Log.Add(line);
        GD.Print($"[campaign] {line}");
    }

    // ---- actions ------------------------------------------------------------------

    private static void Run(Beat b, Godot.Collections.Array actions)
    {
        foreach (var av in actions)
        {
            var a = av.AsGodotDictionary();
            switch (Str(a, "do", ""))
            {
                case "flag": _flags.Add(Str(a, "name", b.Id)); break;
                case "unflag": _flags.Remove(Str(a, "name", b.Id)); break;
                case "tension": Economy.Rivalry.Raise((float)Num(a, "add", 0), Str(a, "why", b.Title)); break;
                case "london":
                    if (Sim.Fort != null)
                    {
                        Sim.Fort.LondonIncentiveNext = Mathf.Clamp(Sim.Fort.LondonIncentiveNext * (float)Num(a, "price", 1), 0.3f, 3f);
                        Sim.Fort.QuotaNextMb = Mathf.Clamp(Sim.Fort.QuotaNextMb * (float)Num(a, "quota", 1), 10f, 400f);
                    }
                    break;
                case "found_post": if (_founded.Add(Str(a, "place", ""))) { Note($"{Str(a, "place", "")} is founded."); World.Posts.Sync(true); } break;
                case "news":
                    var news = new Godot.Collections.Dictionary
                    {
                        ["from"] = Str(a, "from", "Montreal"), ["to"] = Str(a, "to", "the Factor at York Factory"),
                        ["subject"] = Str(a, "subject", b.Title), ["text"] = Str(a, "text", ""), ["company"] = Str(a, "company", "HBC"),
                        ["beat"] = b.Id,
                    };
                    _inTransit.Add((DayOf(GameCalendar.Instance.Year, GameCalendar.Instance.Month, GameCalendar.Instance.Day) + (int)Num(a, "days", 30), news));
                    break;
                // A beat acts on a brigade on the road (World/Convoys.cs): effect is "delay", "turn_back" or "lose".
                case "brigade": World.Convoys.Affect(Str(a, "run", ""), Str(a, "effect", "delay"), (int)Num(a, "days", 5)); break;
                default: GD.PushWarning($"[campaign] {b.Id}: unknown action '{Str(a, "do", "")}'"); break;
            }
        }
    }

    // When the news is read at the post it is a real, witnessed thing from
    // then on, and travels the way anything else does.
    public static void NewsRead(Agent reader, Godot.Collections.Dictionary news)
    {
        string text = $"{Str(news, "from", "")}: {Str(news, "subject", "")}. {Str(news, "text", "")}";
        _flags.Add("heard:" + Str(news, "beat", ""));
        Sim.Notice($"{reader?.Describe() ?? "Someone"} reads news from {text}");
        if (reader != null) Chronicle.Event($"news from {text}", reader.GlobalPosition, reader, subject: Str(news, "subject", ""), weight: 1.4f);
    }

    // ---- conditions ----------------------------------------------------------------

    public static void Flag(string name) { if (Net.Net.IsServer) _flags.Add(name); }
    public static void Unflag(string name) { if (Net.Net.IsServer) _flags.Remove(name); }
    public static bool Has(string name) => _flags.Contains(name);

    private static bool Holds(string c)
    {
        c = c.Trim();
        if (c.StartsWith("!")) return !Holds(c[1..]);
        int colon = c.IndexOf(':');
        string key = colon < 0 ? c : c[..colon], val = colon < 0 ? "" : c[(colon + 1)..];
        var cal = GameCalendar.Instance;
        int today = cal == null ? 0 : DayOf(cal.Year, cal.Month, cal.Day);
        switch (key)
        {
            case "flag": return _flags.Contains(val);
            case "beat": return _done.Contains(val);
            case "post": return PostExists(val);
            case "after": return today >= DayOf(val);
            case "before": return today < DayOf(val);
            case "dead": foreach (var n in Sim.Npcs) if (n.DisplayName == val) return n.IsDead; return false;
        }
        if (c.StartsWith("tension>=") && float.TryParse(c[9..], out float t1)) return Economy.Rivalry.Tension >= t1;
        if (c.StartsWith("tension<") && float.TryParse(c[8..], out float t2)) return Economy.Rivalry.Tension < t2;
        return _flags.Contains(c);
    }

    // Does a post stand yet (design doc 6.1: only once the timeline reaches it)?
    public static bool PostExists(string name)
    {
        // Given up before now (extras.json's post_closures): it isn't there.
        if (World.Posts.Named(name) is { Closed: > 0 } gone && gone.Closed <= (GameCalendar.Instance?.Year ?? StartYear)) return false;
        if (_founded.Contains(name)) return true;
        return WorldMap.Place(name) is { } p && p.Year <= StartYear;
    }

    // Words the director listens for while a beat is near (design doc 2.2's
    // "who talks about what"): news bearing on what is coming gets told first.
    public static IEnumerable<string> Keywords()
    {
        var cal = GameCalendar.Instance;
        if (cal == null) yield break;
        int today = DayOf(cal.Year, cal.Month, cal.Day);
        foreach (var b in _beats)
        {
            if (_done.Contains(b.Id) || b.Date < 0 || today < b.Date - 120 || today > b.Date + 2 * b.Window) continue;
            foreach (var k in b.Keywords) yield return k;
        }
    }

    public static string Describe() => Id == "none" ? "No campaign script." : $"{Title} ({Level}): {_done.Count}/{_beats.Count} beats past.";

    // ---- save ------------------------------------------------------------------------

    public static Godot.Collections.Dictionary Save()
    {
        var transit = new Godot.Collections.Array();
        foreach (var (day, news) in _inTransit) transit.Add(new Godot.Collections.Dictionary { ["day"] = day, ["news"] = news });
        return new Godot.Collections.Dictionary
        {
            ["id"] = Id, ["level"] = (int)Level, ["start"] = StartYear,
            ["flags"] = Arr(_flags), ["pressured"] = Arr(_pressured), ["done"] = Arr(_done), ["founded"] = Arr(_founded),
            ["transit"] = transit, ["log"] = Arr(Log),
        };
    }

    public static void Restore(Godot.Collections.Dictionary d)
    {
        Reset();
        if (d == null) { Begin(); return; }
        Id = Str(d, "id", "historical"); Level = (Adherence)(d.TryGetValue("level", out var l) ? l.AsInt32() : 1);
        _begun = true;
        LoadScript();
        StartYear = d.TryGetValue("start", out var s) ? s.AsInt32() : 1811;
        foreach (var (key, set) in new[] { ("flags", _flags), ("pressured", _pressured), ("done", _done), ("founded", _founded) })
            if (d.TryGetValue(key, out var v)) foreach (var x in v.AsGodotArray()) set.Add(x.AsString());
        if (d.TryGetValue("transit", out var tr))
            foreach (var x in tr.AsGodotArray()) { var e = x.AsGodotDictionary(); _inTransit.Add((e["day"].AsInt32(), e["news"].AsGodotDictionary())); }
        if (d.TryGetValue("log", out var lg)) foreach (var x in lg.AsGodotArray()) Log.Add(x.AsString());
        GD.Print($"[campaign] restored {Title} ({Level}): {_done.Count}/{_beats.Count} beats past, {_inTransit.Count} letters on the way");
    }

    // ---- helpers ------------------------------------------------------------------------

    private static readonly DateTime Epoch = new(1700, 1, 1);
    public static int DayOf(int y, int m, int d) => (new DateTime(Mathf.Clamp(y, 1700, 2100), Mathf.Clamp(m, 1, 12), 1).AddDays(Mathf.Max(0, d - 1)) - Epoch).Days;
    public static DateTime DateOf(int day) => Epoch.AddDays(day);
    public static int DayOf(string iso)
    {
        var p = iso.Split('-');
        int y = p.Length > 0 && int.TryParse(p[0], out int a) ? a : 1811, m = p.Length > 1 && int.TryParse(p[1], out int b) ? b : 1, d = p.Length > 2 && int.TryParse(p[2], out int c) ? c : 1;
        return DayOf(y, m, d);
    }
    private static bool All(string[] xs, Func<string, bool> f) { foreach (var x in xs) if (!f(x)) return false; return true; }
    private static string Str(Godot.Collections.Dictionary d, string k, string def) => d.TryGetValue(k, out var v) ? v.AsString() : def;
    private static double Num(Godot.Collections.Dictionary d, string k, double def) => d.TryGetValue(k, out var v) ? v.AsDouble() : def;
    private static string[] Strings(Godot.Collections.Dictionary d, string k)
    {
        if (!d.TryGetValue(k, out var v)) return Array.Empty<string>();
        if (v.VariantType == Variant.Type.String) return new[] { v.AsString() };
        var list = new List<string>();
        foreach (var x in v.AsGodotArray()) list.Add(x.AsString());
        return list.ToArray();
    }
    private static Godot.Collections.Array Arr(IEnumerable<string> xs) { var a = new Godot.Collections.Array(); foreach (var x in xs) a.Add(x); return a; }

    private const string Readme = @"Campaign scripts
================

Each .json file here is a campaign the main menu offers under New game.
my-campaign.json starts as a copy of the historical script: rename it, edit
it, or start fresh. The game reads this folder when the menu opens.

{
  ""title"": ""shown in the menu"",
  ""description"": ""one or two sentences"",
  ""posts_by_founding_year"": true,     // posts in the atlas appear on 1 Jan of their year
  ""beats"": [ { ... }, ... ]
}

A beat:
  id            unique; once it has happened it is also a flag of that name
  title         what the log calls it
  kind          fixed       - happens on its date at every adherence
                conditional - happens (delay_days after) once requires/when hold, at every adherence
                event       - the world may produce it itself (done_when); at Firm/Strict it is
                              made to happen once its grace window has run out
                pressure    - only its pressure actions, at Steady and up
  date          YYYY-MM-DD (optional for conditional)
  window_days   grace before an event is enforced (Strict: after one window, Firm: two)
  lead_days     how long before the date its pressure starts (default 30)
  requires      [beat ids or flags] that must already hold
  when          [conditions] that must hold
  done_when     [conditions] meaning the world got there itself
  keywords      [words] the director listens for while the beat is near:
                news mentioning them is told first
  pressure      [actions] run once, at Steady and up, from lead_days before the date
  happens       [actions] run when it happens (fixed, conditional, or enforced)

Conditions: flag:NAME  !flag:NAME  beat:ID  post:NAME  after:YYYY-MM-DD
            before:YYYY-MM-DD  dead:Person Name  tension>=N  tension<N

Actions:
  { ""do"": ""flag"", ""name"": ""x"" }                       { ""do"": ""unflag"", ""name"": ""x"" }
  { ""do"": ""tension"", ""add"": 15, ""why"": ""..."" }       rivalry between the companies (0-100)
  { ""do"": ""london"", ""price"": 0.8, ""quota"": 0.9 }       multiplies next year's orders from London
  { ""do"": ""found_post"", ""place"": ""Fort William"" }      a post name from the world map
  { ""do"": ""news"", ""from"": ""Montreal"", ""to"": ""the Factor at York Factory"",
    ""subject"": ""..."", ""text"": ""..."", ""days"": 60, ""company"": ""HBC"" }
      a sealed letter that reaches the addressee after `days` and is only
      known once read - nobody knows what they weren't told.
      Addressees: a person's name, or the Factor at York Factory, the
      postmaster at Oxford House, the wintering partner, the chief of the band.

Adherence (chosen with the campaign):
  Gentle  fixed and conditional beats; the director favours related news
  Steady  + pressure
  Firm    + overdue events made to happen after twice their window
  Strict  + made to happen when their window runs out
";
}
