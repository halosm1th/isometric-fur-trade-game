using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Core;

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

    // The day's scheduling is pure (Core/CampaignRunner.cs): this file feeds it the real world.
    private static readonly CampaignRunner _runner = new();
    private static CampaignState _st => _runner.State;
    private static readonly GodotWorld _world = new();
    private static HashSet<string> _flags => _st.Flags;
    private static HashSet<string> _done => _st.Done;
    private static HashSet<string> _pressured => _runner.Pressured;
    private static Dictionary<string, int> _shift => _runner.Shift;
    private static Dictionary<string, int> _cooldown => _runner.Cooldown;
    private static readonly HashSet<string> _founded = new();
    private static readonly List<(int Day, Godot.Collections.Dictionary News)> _inTransit = new();
    public static readonly List<string> Log = new();
    private static int _lastDay = int.MinValue;
    private static bool _begun;
    // Whether what the script dates before today has been made history yet.
    // It waits for the first player, whose starting path sets the first day.
    private static bool _settled;

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
        _runner.Load(new CampaignScript()); Title = "No script"; Description = "";
        if (Id == "none") return;
        var path = PathOf(Id);
        var d = path == null ? null : Read(path);
        if (d == null) { GD.PushWarning($"[campaign] no script '{Id}' - running with none"); Id = "none"; return; }
        var script = CampaignScript.Parse(ToDict(d), Id);
        Title = script.Title; Description = script.Description;
        _runner.Load(script);
    }

    // ---- the script as plain data -------------------------------------------------

    private static object ToTree(Variant v) => v.VariantType switch
    {
        Variant.Type.Dictionary => ToDict(v.AsGodotDictionary()),
        Variant.Type.Array => ToList(v.AsGodotArray()),
        Variant.Type.String or Variant.Type.StringName => v.AsString(),
        Variant.Type.Bool => v.AsBool(),
        Variant.Type.Int or Variant.Type.Float => v.AsDouble(),
        _ => null,
    };
    private static Dictionary<string, object> ToDict(Godot.Collections.Dictionary d)
    {
        var r = new Dictionary<string, object>();
        foreach (var kv in d) r[kv.Key.AsString()] = ToTree(kv.Value);
        return r;
    }
    private static List<object> ToList(Godot.Collections.Array a)
    {
        var r = new List<object>();
        foreach (var v in a) r.Add(ToTree(v));
        return r;
    }
    // ---- the game ---------------------------------------------------------------

    // A new world (server): read the script, note the start year. Which beats
    // are already history waits for the first day of play (Settle).
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
        GD.Print($"[campaign] {Title} ({Level}); {_runner.Script.Beats.Count} beats");
    }

    // The world's first day of play: the first player is in and their starting
    // path has dated the world (Starts.Begin, in the same call that adds him),
    // so what the script dates before today is history now - its flags and
    // posts hold, nobody is sent news of it. On a dedicated server this waits
    // for the first player to join.
    private static void Settle()
    {
        _settled = true;
        var cal = GameCalendar.Instance;
        int today = cal == null ? 0 : DayOf(cal.Year, cal.Month, cal.Day);
        int posts = _founded.Count;
        // A dedicated server's clock may have run into a new year before anyone came.
        if (_runner.Script.PostsByFoundingYear && cal != null) for (int y = StartYear + 1; y <= cal.Year; y++) _world.NewYear(y);
        _runner.Level = Level;
        _runner.Begin(today, _world);
        if (_founded.Count != posts) World.Posts.Sync(true);
        GD.Print($"[campaign] history to {cal?.DateString}: {_runner.History.Count} beats past");
    }

    public static void Reset()
    {
        _begun = false; _settled = false;
        _runner.Reset();
        _founded.Clear(); _inTransit.Clear(); Log.Clear(); JournalText = ""; _journalStale = true;
        _lastDay = int.MinValue;
    }

    // Once a game day, on the server.
    public static void Tick()
    {
        var cal = GameCalendar.Instance;
        if (cal == null || !Net.Net.IsServer) return;
        if (!_begun) { if (SaveGame.HasPending) return; Begin(); }
        if (!_settled) { if (!Sim.Players().Any()) return; Settle(); }
        int today = DayOf(cal.Year, cal.Month, cal.Day);
        if (today == _lastDay) return;
        _lastDay = today;
        _runner.Level = Level;
        _runner.Tick(today, _world);
        string jt = BuildJournalText(today);
        if (jt != JournalText || _journalStale) { JournalText = jt; _journalStale = false; Net.Net.I?.BroadcastJournalText(jt); }

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

    private static void Note(string text)
    {
        var line = $"{GameCalendar.Instance?.DateString}: {text}";
        Log.Add(line);
        GD.Print($"[campaign] {line}");
    }

    // ---- actions ------------------------------------------------------------------

    private static void Run(List<object> actions) => CampaignLogic.Run(actions, _st, _world);

    // What the game does for the actions the language does not own, and the
    // facts and numbers it answers conditions with.
    private sealed class GodotWorld : ICampaignHost
    {
        public void Note(string text) => Campaign.Note(text);
        public void Founded(string place) => _founded.Add(place);
        public void NewYear(int year)
        {
            foreach (var p in WorldMap.Places)
                if (p.Kind == "post" && p.Year == year && p.Year > StartYear && _founded.Add(p.Name))
                { Campaign.Note($"{p.Name} is founded ({p.Company})."); World.Posts.Sync(true); }
        }

        public double Metric(string name, string[] a)
        {
            string arg = a.Length > 0 ? a[0] : "";
            switch (name)
            {
                case "tension": return Economy.Rivalry.Tension;
                case "standing": return (Sim.Player as Player.PlayerController)?.Standing ?? 0;
                case "stock": return a.Length > 1 && Convoys.Ledger(arg) is { } led && Enum.TryParse<ItemId>(a[1], out var id) ? led.Stores.Count(id) : double.NaN;
                case "furs": return Convoys.Ledger(arg)?.FursInStoreMb ?? double.NaN;
                case "convoys_active": { int n = 0; foreach (var c in Convoys.All) if (c.Now != ConvoyState.Arrived) n++; return n; }
                case "convoy_days_late": return Convoys.Find(arg) is { } cv ? Math.Max(0, _st.Today - cv.DueDay) : 0;
                case "convoy_days_out": return Convoys.Find(arg)?.DaysOut ?? 0;
            }
            GD.PushWarning($"[campaign] unknown number '{name}'");
            return double.NaN;
        }

        public bool Predicate(string name, string[] a)
        {
            string arg = a.Length > 0 ? a[0] : "";
            switch (name)
            {
                case "post": return PostExists(arg);
                case "dead": foreach (var n in Sim.Npcs) if (n.DisplayName == arg) return n.IsDead; return false;
                case "player_faction": return Sim.Players().Any(p => p.Faction.ToString().Equals(arg, StringComparison.OrdinalIgnoreCase));
                case "season": return GameCalendar.Instance?.CurrentSeason.ToString().Equals(arg, StringComparison.OrdinalIgnoreCase) ?? false;
                case "convoy_wintering": return Convoys.Find(arg)?.Now == ConvoyState.Wintering;
            }
            GD.PushWarning($"[campaign] unknown condition '{name}'");
            return false;
        }

        private static string S(Dictionary<string, object> a, string k, string def = "") => a.TryGetValue(k, out var v) ? CampaignLogic.Str(v) : def;
        private static double N(Dictionary<string, object> a, string k, double def = 0) => a.TryGetValue(k, out var v) ? CampaignLogic.Num(v) : def;

        public void Effect(string name, Dictionary<string, object> a)
        {
            string beat = S(a, "_beat");
            switch (name)
            {
                case "tension": Economy.Rivalry.Raise((float)N(a, "add"), S(a, "why", beat)); break;
                case "london":
                    if (Sim.Fort != null)
                    {
                        Sim.Fort.LondonIncentiveNext = Mathf.Clamp(Sim.Fort.LondonIncentiveNext * (float)N(a, "price", 1), 0.3f, 3f);
                        Sim.Fort.QuotaNextMb = Mathf.Clamp(Sim.Fort.QuotaNextMb * (float)N(a, "quota", 1), 10f, 400f);
                    }
                    break;
                case "found_post": if (_founded.Add(S(a, "place"))) { Note($"{S(a, "place")} is founded."); World.Posts.Sync(true); } break;
                case "news":
                    var news = new Godot.Collections.Dictionary
                    {
                        ["from"] = S(a, "from", "Montreal"), ["to"] = S(a, "to", "the Factor at York Factory"),
                        ["subject"] = S(a, "subject", beat), ["text"] = S(a, "text"), ["company"] = S(a, "company", "HBC"),
                        ["beat"] = beat,
                    };
                    _inTransit.Add((_st.Today + (int)N(a, "days", 30), news));
                    break;
                // A beat acts on a brigade on the road (World/Convoys.cs): effect is
                // "delay", "turn_back", "lose", "cancel" (the cargo goes home) or "launch" (a leg now; leg = its number).
                case "brigade": Convoys.Affect(S(a, "run"), S(a, "effect", "delay"), (int)N(a, "days", 5), (int)N(a, "leg", 0)); break;
                // A post's shelves: add (or take, if negative) so many of an item.
                case "stock":
                    if (Convoys.Ledger(S(a, "post")) is { } led && Enum.TryParse<ItemId>(S(a, "item"), out var id))
                    {
                        int n = (int)N(a, "add");
                        if (n > 0) led.Stores.Add(id, n); else if (n < 0) led.Stores.TakeUpTo(id, -n);
                    }
                    else GD.PushWarning($"[campaign] {beat}: stock needs a standing post and an item, got '{S(a, "post")}' '{S(a, "item")}'");
                    break;
                // A line for the players of one company (or all): their journal, their screen, and the headline if asked.
                case "say":
                    string text = S(a, "text"), to = S(a, "to", "all");
                    foreach (var p in Sim.Players())
                    {
                        if (to != "all" && !p.Faction.ToString().Equals(to, StringComparison.OrdinalIgnoreCase)) continue;
                        p.Journal.Add($"{GameCalendar.Instance?.DateString}: {text}"); p.Say(text);
                    }
                    if (a.TryGetValue("headline", out var hl) && hl is true) Sim.Headline(text);
                    break;
                default: GD.PushWarning($"[campaign] {beat}: unknown action '{name}'"); break;
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

    // Something the player did that the story should know of (truce, raid,
    // escort, courier): a flag and a count a script can ask about, and a line
    // in the journal's account of what you have done.
    public static void Deed(string kind) { if (Net.Net.IsServer) { _st.Deed(kind); _journalStale = true; } }

    // ---- what is stirring (the journal) ---------------------------------------------

    private static bool _journalStale = true;
    public static string JournalText { get; private set; } = "";

    private static readonly (string kind, string line)[] DeedWords =
    {
        ("truce", "brokered a truce between the companies"), ("raid", "gone out on a raid"), ("escort", "ridden a brigade to its landing"), ("courier", "carried a packet through"),
    };

    // The journal's top: the rumours of what is coming, then what you have done.
    // Built on the server each day and sent to the others.
    private static string BuildJournalText(int today)
    {
        var sb = new System.Text.StringBuilder();
        var stirs = _runner.Stirring(today);
        if (stirs.Count > 0)
        {
            sb.AppendLine("[color=#dcb][b]What is stirring[/b][/color]");
            foreach (var s in stirs.OrderBy(x => x.DaysToDue))
            {
                string when = s.DaysToDue <= 0 ? "any day now" : s.DaysToDue <= 14 ? "within a fortnight" : s.DaysToDue <= 45 ? "within weeks" : s.DaysToDue <= 120 ? "in the coming months" : "before long";
                sb.AppendLine($"   {s.Rumour} [color=#998]({when})[/color]");
            }
            sb.AppendLine();
        }
        var done = new List<string>();
        foreach (var (kind, line) in DeedWords)
        {
            int n = (int)_st.Vars.GetValueOrDefault("deed:" + kind);
            if (n > 0) done.Add(n == 1 ? line : $"{line} ({n} times)");
        }
        if (done.Count > 0)
        {
            sb.AppendLine("[color=#dcb][b]What you have done[/b][/color]");
            sb.AppendLine("   You have " + string.Join(", ", done) + ".");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public static void ApplyJournalText(string text) { JournalText = text; }

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
        return cal == null ? Array.Empty<string>() : _runner.Keywords(DayOf(cal.Year, cal.Month, cal.Day));
    }

    public static string Describe() => Id == "none" ? "No campaign script." : $"{Title} ({Level}): {_done.Count}/{_runner.Script.Beats.Count} beats past.";

    // ---- save ------------------------------------------------------------------------

    private static Godot.Collections.Dictionary Vars()
    {
        var d = new Godot.Collections.Dictionary();
        foreach (var kv in _st.Vars) d[kv.Key] = kv.Value;
        return d;
    }
    private static Godot.Collections.Dictionary Ints(Dictionary<string, int> src)
    {
        var d = new Godot.Collections.Dictionary();
        foreach (var kv in src) d[kv.Key] = kv.Value;
        return d;
    }
    // Pending timers: their actions are plain data, so they go out as JSON text.
    private static Godot.Collections.Array Timers()
    {
        var a = new Godot.Collections.Array();
        foreach (var t in _st.Timers) a.Add(new Godot.Collections.Dictionary { ["day"] = t.Day, ["json"] = Json.Stringify(ToVariant(t.Actions)) });
        return a;
    }
    private static Variant ToVariant(object o)
    {
        switch (o)
        {
            case null: return default;
            case string s: return s;
            case bool b: return b;
            case double d: return d;
            case List<object> l: { var a = new Godot.Collections.Array(); foreach (var x in l) a.Add(ToVariant(x)); return a; }
            case Dictionary<string, object> m: { var d = new Godot.Collections.Dictionary(); foreach (var kv in m) d[kv.Key] = ToVariant(kv.Value); return d; }
        }
        return default;
    }

    public static Godot.Collections.Dictionary Save()
    {
        var transit = new Godot.Collections.Array();
        foreach (var (day, news) in _inTransit) transit.Add(new Godot.Collections.Dictionary { ["day"] = day, ["news"] = news });
        return new Godot.Collections.Dictionary
        {
            ["id"] = Id, ["level"] = (int)Level, ["start"] = StartYear, ["settled"] = _settled,
            ["flags"] = Arr(_flags), ["pressured"] = Arr(_pressured), ["done"] = Arr(_done), ["founded"] = Arr(_founded),
            ["transit"] = transit, ["log"] = Arr(Log),
            ["vars"] = Vars(), ["doneDay"] = Ints(_st.DoneDay), ["shift"] = Ints(_shift), ["cooldown"] = Ints(_cooldown),
            ["timers"] = Timers(), ["seed"] = (long)_st.Seed, ["forced"] = Arr(_st.Forced),
            ["awakened"] = Arr(_runner.Awakened), ["cancelled"] = Arr(_runner.Cancelled), ["readySince"] = Ints(_runner.ReadySince),
        };
    }

    public static void Restore(Godot.Collections.Dictionary d)
    {
        Reset();
        if (d == null) { Begin(); return; }
        Id = Str(d, "id", "historical"); Level = (Adherence)(d.TryGetValue("level", out var l) ? l.AsInt32() : 1);
        _begun = true;
        _settled = !d.TryGetValue("settled", out var st) || st.AsBool();   // older saves: decided at world start
        LoadScript();
        StartYear = d.TryGetValue("start", out var s) ? s.AsInt32() : 1811;
        foreach (var (key, set) in new[] { ("flags", _flags), ("pressured", _pressured), ("done", _done), ("founded", _founded) })
            if (d.TryGetValue(key, out var v)) foreach (var x in v.AsGodotArray()) set.Add(x.AsString());
        if (d.TryGetValue("transit", out var tr))
            foreach (var x in tr.AsGodotArray()) { var e = x.AsGodotDictionary(); _inTransit.Add((e["day"].AsInt32(), e["news"].AsGodotDictionary())); }
        if (d.TryGetValue("log", out var lg)) foreach (var x in lg.AsGodotArray()) Log.Add(x.AsString());
        if (d.TryGetValue("vars", out var vs)) foreach (var kv in vs.AsGodotDictionary()) _st.Vars[kv.Key.AsString()] = kv.Value.AsDouble();
        foreach (var (key, into) in new[] { ("doneDay", _st.DoneDay), ("shift", _shift), ("cooldown", _cooldown) })
            if (d.TryGetValue(key, out var iv)) foreach (var kv in iv.AsGodotDictionary()) into[kv.Key.AsString()] = kv.Value.AsInt32();
        if (d.TryGetValue("seed", out var sd)) _st.Seed = (uint)sd.AsInt64();
        if (d.TryGetValue("forced", out var fc)) foreach (var x in fc.AsGodotArray()) _st.Forced.Add(x.AsString());
        foreach (var (key, into) in new[] { ("awakened", _runner.Awakened), ("cancelled", _runner.Cancelled) })
            if (d.TryGetValue(key, out var av)) foreach (var x in av.AsGodotArray()) into.Add(x.AsString());
        if (d.TryGetValue("readySince", out var rs)) foreach (var kv in rs.AsGodotDictionary()) _runner.ReadySince[kv.Key.AsString()] = kv.Value.AsInt32();
        if (d.TryGetValue("timers", out var tm))
            foreach (var x in tm.AsGodotArray())
            {
                var t = x.AsGodotDictionary();
                var actions = ToTree(Json.ParseString(t["json"].AsString())) as List<object> ?? new List<object>();
                _st.Timers.Add(new CampaignTimer { Day = t["day"].AsInt32(), Actions = actions });
            }
        GD.Print($"[campaign] restored {Title} ({Level}): {_done.Count}/{_runner.Script.Beats.Count} beats past, {_inTransit.Count} letters on the way");
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

More than flags (see tools/CampaignTool/README.md: a Pascal-like language for writing scripts, and a tool
that checks them and plays them forward without the game):
  Conditions also take { ""all"": [..] }, { ""any"": [..] }, { ""not"": c }, a fact
  { ""p"": ""convoy_late"", ""a"": [""the York boats""] } or a number
  { ""m"": ""stock"", ""a"": [""York Factory"", ""Rum""], ""op"": ""<"", ""v"": 10 }; in a string,
  ""stock(York Factory, Rum) < 10"", ""var(mood) >= 3"", ""month:6-8"", ""chance:30"", ""season:Winter"".
  A beat can also have repeat_days (it happens again that much later).
  Actions also:  { ""do"": ""set"", ""var"": ""mood"", ""to"": 2 }   { ""do"": ""add"", ""var"": ""mood"", ""by"": 1 }
    { ""do"": ""if"", ""cond"": c, ""then"": [..], ""else"": [..] }
    { ""do"": ""random"", ""options"": [ { ""weight"": 30, ""do"": [..] }, .. ] }
    { ""do"": ""after"", ""days"": 10, ""then"": [..] }      { ""do"": ""trigger"", ""beat"": ""id"" }
    { ""do"": ""brigade"", ""run"": ""the York boats"", ""effect"": ""delay|turn_back|lose|cancel|launch"" }
    { ""do"": ""stock"", ""post"": ""York Factory"", ""item"": ""Rum"", ""add"": -10 }
    { ""do"": ""say"", ""text"": ""..."", ""to"": ""HBC"", ""headline"": true }

Adherence (chosen with the campaign):
  Gentle  fixed and conditional beats; the director favours related news
  Steady  + pressure
  Firm    + overdue events made to happen after twice their window
  Strict  + made to happen when their window runs out
";
}
