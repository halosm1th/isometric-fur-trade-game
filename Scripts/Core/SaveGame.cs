using System;
using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Ecology;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.Player;
using IsometricFurTradeGame.Scripts.World;
using GDict = Godot.Collections.Dictionary;
using GArr = Godot.Collections.Array;

namespace IsometricFurTradeGame.Scripts.Core;

// The world written down and read back (design doc 18.1: one continuous
// world). The host owns the save: the map regenerates from WorldLayout, and
// everything that has drifted from it - the date, the books, the people
// and what they know, the sites, the burnt and felled cells - is restored
// over the top. Players are matched by name, so a friend who joins later
// gets their own character back.
public static class SaveGame
{
    public const string Dir = "user://saves";
    public static string QuickPath => $"{Dir}/quicksave.json";

    // Set before the world scene (re)loads; consumed by Population.SpawnAll.
    public static GDict Pending;
    public static bool HasPending => Pending != null;
    public static string LastMessage = "";

    public static bool Exists(string path = null) => FileAccess.FileExists(path ?? QuickPath);

    public static string Summary(string path = null)
    {
        var d = Read(path ?? QuickPath);
        if (d == null) return "";
        var cal = d["calendar"].AsGodotDictionary();
        var players = d["players"].AsGodotArray();
        var names = new List<string>();
        foreach (var p in players) names.Add(p.AsGodotDictionary()["name"].AsString());
        return $"{cal["day"].AsInt32()} {MonthName(cal["month"].AsInt32())} {cal["year"].AsInt32()}, {cal["hour"].AsSingle():0}h - {string.Join(", ", names)}";
    }

    private static string MonthName(int m) => new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" }[Mathf.Clamp(m - 1, 0, 11)];

    // ---------------------------------------------------------------- save

    public static bool Save(string path = null)
    {
        if (!Net.Net.IsServer) { LastMessage = "Only the host can save."; return false; }
        path ??= QuickPath;
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(Dir);
            var d = Capture();
            using var f = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            if (f == null) { LastMessage = $"Could not write {path}: {FileAccess.GetOpenError()}"; return false; }
            f.StoreString(Json.Stringify(d, "  "));
            LastMessage = $"Saved: {ProjectSettings.GlobalizePath(path)}";
            Sim.Notice("The world is written down.");
            return true;
        }
        catch (Exception e)
        {
            LastMessage = $"Save failed: {e.Message}";
            GD.PushError(e.ToString());
            return false;
        }
    }

    private static GDict Capture()
    {
        var cal = GameCalendar.Instance;
        var d = new GDict
        {
            ["version"] = 1,
            ["calendar"] = new GDict { ["year"] = cal.Year, ["month"] = cal.Month, ["day"] = cal.Day, ["hour"] = cal.Hour, ["sky"] = (int)cal.Weather.Current, ["skyHours"] = cal.Weather.HoursLeft },
            ["tension"] = Rivalry.Tension,
            ["camp"] = new GArr { WorldLayout.CampCentre.X, WorldLayout.CampCentre.Y },
            ["abroad"] = MailOf(Mail.Abroad),
            ["council"] = CouncilState(),
            ["grudges"] = GrudgeState(),
            ["fort"] = Ledger(Sim.Fort),
            ["nwc"] = Ledger(Sim.Nwc),
            ["band"] = Band(Sim.Band),
            ["sites"] = Sites(),
            ["hearths"] = Hearths(),
            ["cells"] = Sim.Structures?.ChangedCells() ?? new GArr(),
            ["items"] = Sim.Spawner?.GoneItems() ?? new GArr(),
            ["canoes"] = Canoes(),
            ["ship"] = Ship(),
            ["npcs"] = Npcs(),
            ["players"] = Players(),
        };
        return d;
    }

    private static GDict Inv(Inventory inv)
    {
        var d = new GDict();
        if (inv == null) return d;
        foreach (var (id, n) in inv.All()) d[id.ToString()] = n;
        return d;
    }

    private static GDict Ledger(FortLedger f)
    {
        if (f == null) return new GDict();
        var d = Household(f);
        d["peltPrice"] = f.PeltPrice; d["incentiveNext"] = f.LondonIncentiveNext; d["quota"] = f.QuotaMb; d["quotaNext"] = f.QuotaNextMb;
        d["fursTaken"] = f.FursTakenMb; d["lostToRival"] = f.LostToRivalMb; d["goodsPaid"] = f.GoodsPaidMb; d["year"] = f.Year;
        d["lastRaided"] = f.LastRaidedDay; d["lastRaid"] = f.LastRaidDay; d["lotsLost"] = f.LotsLostToRaids;
        d["directive"] = f.DirectiveText; d["directiveDay"] = f.DirectiveReadDay; d["directiveYear"] = f.DirectiveYear; d["skimmed"] = f.SkimmedMb;
        return d;
    }

    private static GDict Household(Household h)
    {
        var d = new GDict { ["stores"] = Inv(h.Stores) };
        var known = new GArr(); foreach (var s in h.KnownSites) known.Add(s);
        d["known"] = known;
        var belief = new GDict(); foreach (var kv in h.SiteBelief) belief[kv.Key] = new GArr { kv.Value.X, kv.Value.Y };
        d["belief"] = belief;
        return d;
    }

    private static GDict Band(BandLedger b)
    {
        if (b == null) return new GDict();
        var d = Household(b);
        d["standing"] = b.StandingWithCompany; d["standingNwc"] = b.StandingWithNwc; d["debt"] = b.DebtMb; d["debtNwc"] = b.DebtNwcMb;
        d["rumTaste"] = b.RumTaste; d["invited"] = b.InvitedToTrade;
        return d;
    }

    private static GArr Sites()
    {
        var a = new GArr();
        if (Sim.Ecology == null) return a;
        foreach (var s in Sim.Ecology.Sites)
        {
            var lines = new GArr();
            foreach (var l in s.Traplines) lines.Add(new GDict { ["owner"] = l.Owner, ["traps"] = l.Traps, ["caught"] = l.Caught, ["check"] = l.HoursSinceCheck, ["set"] = l.HoursSinceSet, ["robbed"] = l.Robbed });
            a.Add(new GDict
            {
                ["name"] = s.Name, ["count"] = s.Count, ["breeding"] = s.BreedingCapacity, ["scarcity"] = s.Scarcity, ["food"] = s.FoodStock,
                ["cx"] = s.Cell.X, ["cy"] = s.Cell.Y, ["lines"] = lines,
            });
        }
        return a;
    }

    private static GArr Hearths()
    {
        var a = new GArr();
        if (Sim.Structures == null) return a;
        foreach (var h in Sim.Structures.AllHearths) a.Add(new GArr { h.Cell.X, h.Cell.Y, h.FuelHours });
        return a;
    }

    private static GArr Canoes()
    {
        var a = new GArr();
        if (Sim.Spawner == null) return a;
        foreach (var c in Sim.Spawner.Canoes) a.Add(new GArr { c.Index, c.GlobalPosition.X, c.GlobalPosition.Y });
        return a;
    }

    private static GDict Ship()
    {
        var s = Sim.Ship;
        if (s == null) return new GDict();
        return new GDict { ["state"] = (int)s.Current, ["x"] = s.Position.X, ["y"] = s.Position.Y, ["cargo"] = Inv(s.Cargo), ["hold"] = Inv(s.Hold), ["dockedHours"] = s.DockedHours, ["ordersRead"] = s.OrdersRead };
    }

    private static GDict AgentCommon(Agent a)
    {
        return new GDict
        {
            ["name"] = a.DisplayName, ["faction"] = (int)a.Faction, ["x"] = a.GlobalPosition.X, ["y"] = a.GlobalPosition.Y,
            ["hunger"] = a.Needs.Hunger, ["thirst"] = a.Needs.Thirst, ["warmth"] = a.Needs.Warmth, ["health"] = a.Needs.Health,
            ["inv"] = Inv(a.Inventory), ["age"] = a.Age, ["sick"] = a.SickDaysLeft, ["immune"] = a.Immune, ["drunk"] = a.Intoxication,
            ["weapon"] = a.PreferredWeapon?.ToString() ?? "", ["dead"] = a.IsDead, ["fresh"] = a.DaysSinceFresh, ["mail"] = MailOf(a.Mail),
        };
    }

    private static GDict CouncilState()
    {
        Council.Seed();
        var members = new GArr();
        foreach (var c in Council.Members) members.Add(new GDict { ["name"] = c.Name, ["shares"] = c.Shares, ["standing"] = c.Standing });
        var minutes = new GArr(); foreach (var l in Council.Minutes) minutes.Add(l);
        var letters = new GArr(); foreach (var (item, opt, w, from) in Council.Letters) letters.Add(new GArr { item, opt, w, from });
        var nwc = Sim.Nwc;
        return new GDict { ["members"] = members, ["minutes"] = minutes, ["letters"] = letters, ["year"] = Council.LastYear,
            ["rum"] = nwc?.RumShareSetting ?? 0.35f, ["outfit"] = nwc?.OutfitPieces ?? 90, ["discretion"] = nwc?.PriceAtDiscretion ?? false, ["raids"] = nwc?.RaidsSanctioned ?? false, ["knowsPlan"] = Sim.Fort?.KnowsRivalPlan ?? false };
    }
    private static void LoadCouncil(GDict d)
    {
        if (d.Count == 0) return;
        Council.Seed();
        foreach (var mv in d["members"].AsGodotArray()) { var md = mv.AsGodotDictionary(); var c = Council.Members.Find(x => x.Name == S(md, "name")); if (c != null) { c.Shares = I(md, "shares", c.Shares); c.Standing = F(md, "standing", c.Standing); } }
        Council.Minutes.Clear(); foreach (var l in d["minutes"].AsGodotArray()) Council.Minutes.Add(l.AsString());
        Council.Letters.Clear(); foreach (var lv in d["letters"].AsGodotArray()) { var a = lv.AsGodotArray(); Council.Letters.Add((a[0].AsString(), a[1].AsInt32(), a[2].AsSingle(), a[3].AsString())); }
        Council.LastYear = I(d, "year");
        if (Sim.Nwc != null) { Sim.Nwc.RumShareSetting = F(d, "rum", 0.35f); Sim.Nwc.OutfitPieces = I(d, "outfit", 90); Sim.Nwc.PriceAtDiscretion = B(d, "discretion"); Sim.Nwc.RaidsSanctioned = B(d, "raids"); }
        if (Sim.Fort != null) Sim.Fort.KnowsRivalPlan = B(d, "knowsPlan");
    }
    private static GArr GrudgeState()
    {
        var a = new GArr();
        foreach (var g in Grudges.All) a.Add(new GDict { ["holder"] = g.Holder, ["against"] = g.Against, ["victim"] = g.Victim, ["day"] = g.Day, ["debt"] = g.DebtMb, ["settled"] = g.Settled, ["blood"] = g.Blood });
        return a;
    }

    private static GArr MailOf(List<Message> mail)
    {
        var a = new GArr();
        foreach (var m in mail) a.Add(MailEntry(m));
        return a;
    }
    private static GDict MailEntry(Message m) => new() { ["kind"] = m.Kind, ["from"] = m.From, ["to"] = m.To, ["subject"] = m.Subject, ["text"] = m.Text, ["company"] = (int)m.Company, ["day"] = m.DayWritten, ["read"] = m.Read, ["transit"] = m.TransitHoursLeft, ["payload"] = m.Payload };
    private static Message MailFrom(GDict d)
    {
        var m = Letters.Build(S(d, "kind"), d["payload"].AsGodotDictionary());
        m.From = S(d, "from", m.From); m.To = S(d, "to", m.To); m.Subject = S(d, "subject", m.Subject); if (S(d, "text") != "") m.Text = S(d, "text");
        m.Company = (Faction)I(d, "company"); m.DayWritten = I(d, "day"); m.Read = B(d, "read"); m.TransitHoursLeft = F(d, "transit");
        return m;
    }

    private static GArr Npcs()
    {
        var a = new GArr();
        foreach (var n in Sim.Npcs)
        {
            if (!GodotObject.IsInstanceValid(n)) continue;
            var d = AgentCommon(n);
            d["role"] = (int)n.Role; d["female"] = n.IsFemale;
            d["bed"] = new GArr { n.BedCell.X, n.BedCell.Y }; d["work"] = new GArr { n.WorkCell.X, n.WorkCell.Y };
            d["home"] = n.HomeStores == Sim.Fort ? "fort" : n.HomeStores == Sim.Nwc ? "nwc" : n.HomeStores == Sim.Band ? "band" : "";
            d["frames"] = n.FramesPath; d["tint"] = n.Tint.ToHtml(); d["scale"] = n.SpriteScale;
            var p = n.Personality;
            d["personality"] = new GArr { p.Loyalty, p.Talkative, p.Suspicion, p.Ambition, p.Courage };
            d["spouse"] = n.Spouse?.DisplayName ?? ""; d["father"] = n.Father?.DisplayName ?? "";
            var kids = new GArr(); foreach (var c in n.Children) kids.Add(c.DisplayName);
            d["children"] = kids;
            d["gifts"] = n.GiftsFromPlayerMb;
            var rapport = new GDict(); foreach (var kv in n.Rapport) if (kv.Key != null && GodotObject.IsInstanceValid(kv.Key)) rapport[kv.Key.DisplayName] = kv.Value;
            d["rapport"] = rapport;
            var mem = new GArr();
            foreach (var m in n.Memories) mem.Add(new GDict { ["text"] = m.Text, ["subject"] = m.Subject ?? "", ["actor"] = m.Actor != null && GodotObject.IsInstanceValid(m.Actor) ? m.Actor.DisplayName : "", ["day"] = m.Day, ["directness"] = m.Directness, ["weight"] = m.Weight, ["key"] = m.Key ?? "" });
            d["memories"] = mem;
            d["task"] = n.AssignedTaskName ?? "";
            a.Add(d);
        }
        return a;
    }

    private static GArr Players()
    {
        var a = new GArr();
        foreach (var p in Sim.Players())
        {
            var d = AgentCommon(p);
            d["standing"] = p.Standing; d["rank"] = p.Rank; d["spouse"] = p.Spouse?.DisplayName ?? ""; d["tongue"] = p.CreeFluency; d["authorized"] = p.AuthorizedAcrossUntilDay;
            d["task"] = new GArr { (int)p.Task.Kind, p.Task.Need, p.Task.Done, p.Task.DayAssigned }; d["taskIntent"] = p.Task.Intent; d["taskDeadline"] = p.Task.Deadline;
            var known = new GArr(); foreach (var s in p.KnownSites) known.Add(s);
            d["knownSites"] = known;
            var belief = new GDict(); foreach (var kv in p.SiteBelief) belief[kv.Key] = new GArr { kv.Value.X, kv.Value.Y };
            d["belief"] = belief;
            var journal = new GArr(); foreach (var j in p.Journal) journal.Add(j);
            d["journal"] = journal;
            var knownEvents = new GArr(); foreach (var k in p.Known) knownEvents.Add(k);
            d["known"] = knownEvents;
            a.Add(d);
        }
        return a;
    }

    // ---------------------------------------------------------------- load

    public static GDict Read(string path)
    {
        if (!FileAccess.FileExists(path)) return null;
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (f == null) return null;
        var v = Json.ParseString(f.GetAsText());
        return v.VariantType == Variant.Type.Dictionary ? v.AsGodotDictionary() : null;
    }

    // Arrange for the world to come back as saved: remember the data, then
    // (re)load the world scene. Population.SpawnAll does the rest.
    public static bool BeginLoad(SceneTree tree, string path = null)
    {
        if (!Net.Net.IsServer) { LastMessage = "Only the host can load."; return false; }
        var d = Read(path ?? QuickPath);
        if (d == null) { LastMessage = "No save to load."; return false; }
        Pending = d;
        Net.Net.I?.AnnounceReload();
        tree.CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://Scenes/World/YorkFactoryExterior.tscn");
        return true;
    }

    private static Vector2 Pos(GDict d) => new(d["x"].AsSingle(), d["y"].AsSingle());
    private static Vector2I CellOf(GArr a) => new(a[0].AsInt32(), a[1].AsInt32());
    private static float F(GDict d, string k, float def = 0f) => d.ContainsKey(k) ? d[k].AsSingle() : def;
    private static int I(GDict d, string k, int def = 0) => d.ContainsKey(k) ? d[k].AsInt32() : def;
    private static string S(GDict d, string k, string def = "") => d.ContainsKey(k) ? d[k].AsString() : def;
    private static bool B(GDict d, string k, bool def = false) => d.ContainsKey(k) ? d[k].AsBool() : def;

    private static void LoadInv(Inventory inv, GDict d)
    {
        inv.Clear();
        foreach (var kv in d) if (Enum.TryParse(kv.Key.AsString(), out ItemId id)) inv.Add(id, kv.Value.AsInt32());
    }

    private static void LoadHousehold(Household h, GDict d)
    {
        if (h == null || d.Count == 0) return;
        LoadInv(h.Stores, d["stores"].AsGodotDictionary());
        h.KnownSites.Clear();
        foreach (var s in d["known"].AsGodotArray()) h.KnownSites.Add(s.AsString());
        h.SiteBelief.Clear();
        foreach (var kv in d["belief"].AsGodotDictionary()) h.SiteBelief[kv.Key.AsString()] = CellOf(kv.Value.AsGodotArray());
    }

    private static void LoadLedger(FortLedger f, GDict d)
    {
        if (f == null || d.Count == 0) return;
        LoadHousehold(f, d);
        f.PeltPrice = F(d, "peltPrice", 1f); f.LondonIncentiveNext = F(d, "incentiveNext", 1f); f.QuotaMb = F(d, "quota", 60f); f.QuotaNextMb = F(d, "quotaNext", 60f);
        f.FursTakenMb = F(d, "fursTaken"); f.LostToRivalMb = F(d, "lostToRival"); f.GoodsPaidMb = F(d, "goodsPaid"); f.Year = I(d, "year", 1811);
        f.LastRaidedDay = I(d, "lastRaided", -999); f.LastRaidDay = I(d, "lastRaid", -999); f.LotsLostToRaids = I(d, "lotsLost");
        if (d.ContainsKey("directive")) { f.DirectiveText = S(d, "directive"); f.DirectiveReadDay = I(d, "directiveDay", -999); f.DirectiveYear = I(d, "directiveYear"); f.SkimmedMb = F(d, "skimmed"); }
    }

    private static void LoadAgentCommon(Agent a, GDict d)
    {
        a.GlobalPosition = Pos(d);
        a.Needs.Hunger = F(d, "hunger", 100f); a.Needs.Thirst = F(d, "thirst", 100f); a.Needs.Warmth = F(d, "warmth", 100f); a.Needs.Health = F(d, "health", 100f);
        LoadInv(a.Inventory, d["inv"].AsGodotDictionary());
        a.Age = I(d, "age", 25); a.SickDaysLeft = F(d, "sick"); a.Immune = B(d, "immune"); a.Intoxication = F(d, "drunk"); a.DaysSinceFresh = F(d, "fresh");
        a.Faction = (Faction)I(d, "faction");
        string w = S(d, "weapon");
        a.PreferredWeapon = w != "" && Enum.TryParse(w, out ItemId wid) ? wid : null;
        a.Mail.Clear();
        if (d.ContainsKey("mail")) foreach (var mv in d["mail"].AsGodotArray()) a.Mail.Add(MailFrom(mv.AsGodotDictionary()));
    }

    // Called by Population.SpawnAll instead of the default cast. Spawns the
    // people as saved and restores everything else around them.
    public static void Restore(Population pop)
    {
        var d = Pending;
        Pending = null;
        if (d == null) return;
        try
        {
            var cal = GameCalendar.Instance;
            var c = d["calendar"].AsGodotDictionary();
            cal.SetDate(I(c, "year", 1811), I(c, "month", 1), I(c, "day", 1), F(c, "hour", 9f));
            cal.Weather.Force((SkyState)I(c, "sky"), F(c, "skyHours", 6f));
            Rivalry.Tension = F(d, "tension", 20f);
            if (d.ContainsKey("camp")) Sim.Band?.MoveCamp(CellOf(d["camp"].AsGodotArray()));
            Mail.Abroad.Clear();
            if (d.ContainsKey("abroad")) foreach (var mv in d["abroad"].AsGodotArray()) Mail.Abroad.Add(MailFrom(mv.AsGodotDictionary()));
            if (d.ContainsKey("council")) LoadCouncil(d["council"].AsGodotDictionary());
            Grudges.All.Clear();
            if (d.ContainsKey("grudges")) foreach (var gv in d["grudges"].AsGodotArray()) { var gd = gv.AsGodotDictionary(); Grudges.All.Add(new Grudge { Holder = S(gd, "holder"), Against = S(gd, "against"), Victim = S(gd, "victim"), Day = I(gd, "day"), DebtMb = F(gd, "debt"), Settled = B(gd, "settled"), Blood = B(gd, "blood") }); }

            LoadLedger(Sim.Fort, d["fort"].AsGodotDictionary());
            LoadLedger(Sim.Nwc, d["nwc"].AsGodotDictionary());
            var bd = d["band"].AsGodotDictionary();
            if (Sim.Band != null && bd.Count > 0)
            {
                LoadHousehold(Sim.Band, bd);
                Sim.Band.StandingWithCompany = F(bd, "standing", 50f); Sim.Band.StandingWithNwc = F(bd, "standingNwc", 50f);
                Sim.Band.DebtMb = F(bd, "debt"); Sim.Band.DebtNwcMb = F(bd, "debtNwc"); Sim.Band.RumTaste = F(bd, "rumTaste"); Sim.Band.InvitedToTrade = B(bd, "invited");
            }

            if (Sim.Ecology != null)
            {
                foreach (var sv in d["sites"].AsGodotArray())
                {
                    var sd = sv.AsGodotDictionary();
                    var site = Sim.Ecology.Sites.Find(s => s.Name == S(sd, "name"));
                    if (site == null) continue;
                    site.Count = F(sd, "count"); site.BreedingCapacity = F(sd, "breeding", 1f); site.Scarcity = B(sd, "scarcity"); site.FoodStock = F(sd, "food", 1f);
                    site.Cell = new Vector2I(I(sd, "cx", site.Cell.X), I(sd, "cy", site.Cell.Y));
                    site.Traplines.Clear();
                    foreach (var lv in sd["lines"].AsGodotArray())
                    {
                        var ld = lv.AsGodotDictionary();
                        site.Traplines.Add(new Trapline { Owner = S(ld, "owner"), Traps = I(ld, "traps"), Caught = I(ld, "caught"), HoursSinceCheck = F(ld, "check"), HoursSinceSet = F(ld, "set"), Robbed = I(ld, "robbed") });
                    }
                }
            }

            Sim.Structures?.RestoreCells(d["cells"].AsGodotArray());
            foreach (var hv in d["hearths"].AsGodotArray())
            {
                var h = hv.AsGodotArray();
                Sim.Structures?.SetHearthFuel(new Vector2I(h[0].AsInt32(), h[1].AsInt32()), h[2].AsSingle());
            }
            Sim.Spawner?.RestoreGone(d["items"].AsGodotArray());
            if (Sim.Spawner != null)
            {
                foreach (var cv in d["canoes"].AsGodotArray())
                {
                    var ca = cv.AsGodotArray();
                    int idx = ca[0].AsInt32();
                    foreach (var canoe in Sim.Spawner.Canoes) if (canoe.Index == idx) canoe.SetState(new Vector2(ca[1].AsSingle(), ca[2].AsSingle()), 0);
                }
            }

            // People, then the ties between them.
            var byName = new Dictionary<string, Npc>();
            var pending = new List<(Npc npc, GDict d)>();
            foreach (var nv in d["npcs"].AsGodotArray())
            {
                var nd = nv.AsGodotDictionary();
                Household home = S(nd, "home") switch { "fort" => Sim.Fort, "nwc" => Sim.Nwc, "band" => Sim.Band, _ => null };
                var frames = GD.Load<SpriteFrames>(S(nd, "frames", "res://Scenes/Player/PlayerFrames.tres"));
                var npc = pop.Spawn(S(nd, "name"), (Role)I(nd, "role"), (Faction)I(nd, "faction"), CellOf(nd["bed"].AsGodotArray()), CellOf(nd["work"].AsGodotArray()), home, frames, Color.FromHtml(S(nd, "tint", "ffffff")), F(nd, "scale", 0.8f), B(nd, "female"));
                LoadAgentCommon(npc, nd);
                var p = nd["personality"].AsGodotArray();
                npc.Personality = new Personality { Loyalty = p[0].AsSingle(), Talkative = p[1].AsSingle(), Suspicion = p[2].AsSingle(), Ambition = p[3].AsSingle(), Courage = p[4].AsSingle() };
                npc.GiftsFromPlayerMb = F(nd, "gifts");
                npc.AssignedTaskName = S(nd, "task");
                foreach (var mv in nd["memories"].AsGodotArray())
                {
                    var md = mv.AsGodotDictionary();
                    npc.Memories.Add(new MemoryEntry { Text = S(md, "text"), Subject = S(md, "subject"), Day = F(md, "day"), Directness = I(md, "directness", 1), Weight = F(md, "weight", 1f), Key = S(md, "key") });
                }
                if (B(nd, "dead")) npc.DieQuietly();
                byName[npc.DisplayName] = npc;
                pending.Add((npc, nd));
            }
            foreach (var (npc, nd) in pending)
            {
                string spouse = S(nd, "spouse");
                if (spouse != "" && byName.TryGetValue(spouse, out var sp)) npc.Spouse = sp;
                string father = S(nd, "father");
                if (father != "" && byName.TryGetValue(father, out var fa)) npc.Father = fa;
                foreach (var kv in nd["children"].AsGodotArray()) if (byName.TryGetValue(kv.AsString(), out var kid) && !npc.Children.Contains(kid)) npc.Children.Add(kid);
                foreach (var kv in nd["rapport"].AsGodotDictionary()) if (byName.TryGetValue(kv.Key.AsString(), out var other)) npc.Rapport[other] = kv.Value.AsSingle();
                // Rapport with players is re-attached when each player spawns.
                var savedRapport = new Dictionary<string, float>();
                foreach (var kv in nd["rapport"].AsGodotDictionary()) if (!byName.ContainsKey(kv.Key.AsString())) savedRapport[kv.Key.AsString()] = kv.Value.AsSingle();
                if (savedRapport.Count > 0) _npcPlayerRapport[npc] = savedRapport;
            }

            var sh = d["ship"].AsGodotDictionary();
            if (Sim.Ship != null && sh.Count > 0)
            {
                var cargo = new Inventory(); LoadInv(cargo, sh["cargo"].AsGodotDictionary());
                var hold = new Inventory(); LoadInv(hold, sh["hold"].AsGodotDictionary());
                Sim.Ship.Restore((Ship.State)I(sh, "state"), Pos(sh), cargo, hold, F(sh, "dockedHours"), B(sh, "ordersRead"));
                foreach (var n in Sim.Npcs) if (n.Role == Role.Sailor && !n.IsDead) Sim.Ship.Crew.Add(n);
            }

            _savedPlayers.Clear();
            foreach (var pv in d["players"].AsGodotArray()) { var pd = pv.AsGodotDictionary(); _savedPlayers[S(pd, "name")] = pd; }
            _npcByName = byName;
            LastMessage = "The world is as it was.";
            Sim.Notice($"Loaded: {cal.DateString}. {Sim.Npcs.Count} souls.");
        }
        catch (Exception e)
        {
            LastMessage = $"Load failed: {e.Message}";
            GD.PushError(e.ToString());
        }
    }

    private static readonly Dictionary<string, GDict> _savedPlayers = new();
    private static readonly Dictionary<Npc, Dictionary<string, float>> _npcPlayerRapport = new();
    private static Dictionary<string, Npc> _npcByName = new();

    // A player has spawned (now, or joining later): give them their character back.
    public static void ApplyPlayer(PlayerController p)
    {
        if (p == null || !_savedPlayers.TryGetValue(p.DisplayName, out var d)) return;
        LoadAgentCommon(p, d);
        p.Standing = F(d, "standing", 10f); p.Rank = S(d, "rank", "labourer"); p.CreeFluency = F(d, "tongue", 0.1f); p.AuthorizedAcrossUntilDay = I(d, "authorized", -1);
        var t = d["task"].AsGodotArray();
        p.Task.Kind = (TaskKind)t[0].AsInt32(); p.Task.Need = t[1].AsInt32(); p.Task.Done = t[2].AsInt32(); p.Task.DayAssigned = t[3].AsInt32();
        p.Task.Intent = S(d, "taskIntent"); p.Task.Deadline = I(d, "taskDeadline");
        p.KnownSites.Clear(); foreach (var s in d["knownSites"].AsGodotArray()) p.KnownSites.Add(s.AsString());
        p.SiteBelief.Clear(); foreach (var kv in d["belief"].AsGodotDictionary()) p.SiteBelief[kv.Key.AsString()] = CellOf(kv.Value.AsGodotArray());
        p.Journal.Clear(); foreach (var j in d["journal"].AsGodotArray()) p.Journal.Add(j.AsString());
        p.Known.Clear(); foreach (var k in d["known"].AsGodotArray()) p.Known.Add(k.AsString());
        string spouse = S(d, "spouse");
        if (spouse != "" && _npcByName.TryGetValue(spouse, out var sp)) { p.Spouse = sp; sp.Spouse = p; }
        foreach (var kv in _npcPlayerRapport) if (kv.Value.TryGetValue(p.DisplayName, out float r)) kv.Key.Rapport[p] = r;
        if (B(d, "dead")) p.Needs.Health = 0f;
        p.Say("You take up where you left off.");
    }
}
