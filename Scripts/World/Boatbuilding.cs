using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.Player;
using NetHub = IsometricFurTradeGame.Scripts.Net.Net;

namespace IsometricFurTradeGame.Scripts.World;

// Making and mending boats, by a player's own hands (World/Boats says what a
// boat is; UI/HandWork plays each piece of work; World/Boatwrights is the
// camps' and posts' builders). Server side.
//
// The work goes: gather (bark, watap, gum, cedar - by season and country)
// -> lay a bed by the water -> each stage in turn, a game played for it,
// with whoever is helping playing their part by their own skill -> the last
// stage done she is a boat, and goes in the water to show her leaks.
// Mending is the same hands at the same games, on a hurt boat.
public static class Boatbuilding
{
    private sealed class Job
    {
        public int Token;
        public string Kind = "";          // stage, repair, watch, convoy
        public Canoe Boat;
        public BoatStage Stage;
        public string Repair = "";        // leaks, bark, ribs, gunwales, caulk, plank
        public readonly List<(Npc Who, float Score)> Helpers = new();
        public float Hours;
        public (ItemId Item, int Count)[] Uses = System.Array.Empty<(ItemId, int)>();
        public Convoy Convoy;
        public Npc Watched;
        public float WatchedSkill;
        public BoatGame Game;
    }

    private static readonly Dictionary<PlayerController, Job> _jobs = new();
    private static int _token = 1;

    public static void Reset() { _jobs.Clear(); _stripped.Clear(); _gummed.Clear(); }

    private static int Today => GameCalendar.Instance is { } c ? Campaign.DayOf(c.Year, c.Month, c.Day) : 0;

    // ---- gathering (the crooked knife's menu) -----------------------------

    private static readonly HashSet<Vector2I> _stripped = new();
    private static readonly Dictionary<Vector2I, int> _gummed = new();
    public static IEnumerable<Vector2I> Stripped => _stripped;
    public static void RestoreStripped(IEnumerable<Vector2I> cells) { foreach (var c in cells) _stripped.Add(c); }

    public static ScenePage GatherPage(PlayerController p)
    {
        var cal = GameCalendar.Instance;
        var page = new ScenePage { Title = "The bush", CanLeave = true };
        var birch = Boats.NearestTreeWhere(p.GlobalPosition, 170f, c => Boats.IsBirch(c) && !_stripped.Contains(c));
        var conifer = Boats.NearestTreeWhere(p.GlobalPosition, 170f, Boats.IsConifer);
        var any = Sim.Structures?.NearestTree(p.GlobalPosition, 170f);
        var lines = new List<string>();
        if (birch != null)
        {
            float season = Boats.BarkSeason(cal.Month);
            lines.Add(season >= 0.9f ? "A white birch, straight and clean-barked. The sap is up: the bark will come away in one sheet." : season >= 0.5f ? "A white birch. The bark will come, but not as freely as at midsummer." : "A white birch. Out of season the bark clings and tears; it would want warming over a fire to come away whole.");
            page.Add("Strip a sheet of bark from the birch", () => Done(p, Strip(p, birch.Value)));
        }
        else if (Boats.NearestTreeWhere(p.GlobalPosition, 170f, Boats.IsBirch) != null) lines.Add("The birch here has been stripped already.");
        if (conifer != null)
        {
            lines.Add(cal.IsFrozen ? "Spruce. Its roots are frozen into the ground till the thaw; its gum is hard and brittle, but it can be scraped." : "Spruce. Its long surface roots split into watap, and the scars on its trunk weep gum.");
            if (!cal.IsFrozen) page.Add("Dig up and split the roots for watap", () => Done(p, Roots(p, conifer.Value)));
            page.Add("Scrape the gum from the trunk", () => Done(p, Gum(p, conifer.Value)));
        }
        if (any != null)
        {
            bool cedar = Boats.CedarCountry(any.Value);
            lines.Add(cedar ? "There is cedar in this country: light, straight-grained, the best rib-stuff there is." : "No cedar here; ribs and gunwales would have to be split from spruce or tamarack, heavier and quicker to crack.");
            if (p.Inventory.Has(ItemId.Axe)) page.Add(cedar ? "Fell a cedar and split it for ribs and gunwales" : "Fell a spruce and split it for ribs and gunwales", () => Done(p, Split(p, any.Value)));
            else lines.Add("(You would need an axe to fell and split it.)");
        }
        if (lines.Count == 0) { lines.Add("No tree within reach. Birch bark, spruce roots and gum, and cedar for the ribs all come from the bush."); }
        // A bed here, by the water.
        foreach (BoatKind k in System.Enum.GetValues(typeof(BoatKind)))
        {
            var kk = k; var spec = Boats.Of(k);
            if (Why(p, k) != "") continue;
            string who = p.Skills.Crafting + 0.001f >= spec.MinSkill ? "" : $" - you'd need a master builder with you (crafting {spec.MinSkill:0.00})";
            page.Add($"Lay out a bed here for a {spec.Name} ({spec.CapacityLbs:0} lb, {spec.Seats} paddles){who}", () => Done(p, LayBed(p, kk)));
        }
        page.Text = string.Join("\n\n", lines);
        page.Add("Leave it", () => null);
        return page;
    }

    private static ScenePage Done(PlayerController p, string said)
    {
        p.Say(said);
        return null;
    }

    private static string Strip(PlayerController p, Vector2I tree)
    {
        var cal = GameCalendar.Instance;
        float season = Boats.BarkSeason(cal.Month);
        bool warmed = season < 0.5f && Sim.Structures?.HearthNear(IsoGrid.ToWorld(tree), 500f) is { IsBurning: true };
        if (warmed) season = 0.55f;
        float knack = 0.4f + 0.6f * p.Skills.Crafting;
        int got = GD.Randf() < season * (0.6f + 0.4f * knack) ? (season >= 0.9f && GD.Randf() < knack ? 2 : 1) : 0;
        _stripped.Add(tree);
        p.Skills.Learn(ref p.Skills.Crafting, 0.004f, "gathering for the canoe");
        p.RequestSkip(1.5f, "");
        if (got == 0) return season < 0.5f ? "You score the bark top and bottom and work at it with the knife, but it splits and checks as it comes. Nothing worth sewing. Wait for the sap to rise - or warm it at a fire." : "The sheet tears halfway down. Not fit for a canoe.";
        p.Inventory.Add(ItemId.BirchBark, got);
        Chronicle.Event($"{p.DisplayName} stripped birch bark.", p.GlobalPosition, p, weight: 0.1f);
        return got > 1 ? "Long straight cuts top and bottom, a slit down the trunk, and the bark peels away under your hands in one great sheet - two rolls' worth." : warmed ? "Warmed at the fire, the bark comes away stiffly. A roll of bark." : "The bark comes away in a sheet. A roll of birch bark.";
    }

    private static string Roots(PlayerController p, Vector2I tree)
    {
        int got = 1 + (GD.Randf() < 0.4f + 0.5f * p.Skills.Crafting ? 2 : 1);
        p.Inventory.Add(ItemId.Watap, got);
        p.Skills.Learn(ref p.Skills.Crafting, 0.004f, "gathering for the canoe");
        p.RequestSkip(1.5f, "");
        return $"You follow the long roots out under the moss, pull them up, and split them with the knife and your teeth into supple strands. {Items.Items.Lot(ItemId.Watap, got)}.";
    }

    private static string Gum(PlayerController p, Vector2I tree)
    {
        if (_gummed.TryGetValue(tree, out int day) && Today - day < 30) return "This tree has been scraped clean lately. Find another.";
        _gummed[tree] = Today;
        var cal = GameCalendar.Instance;
        int got = cal.IsFrozen ? 1 : 1 + (GD.Randf() < 0.5f + 0.4f * p.Skills.Crafting ? 1 : 0);
        p.Inventory.Add(ItemId.SpruceGum, got);
        p.Skills.Learn(ref p.Skills.Crafting, 0.003f, "gathering for the canoe");
        p.RequestSkip(1f, "");
        return got > 1 ? "You scrape the weeping scars of the trunk into your hand: amber gum, enough to boil down into two cakes." : "You scrape what gum there is from the scars. One cake's worth, when it is boiled down with a little fat.";
    }

    private static string Split(PlayerController p, Vector2I tree)
    {
        bool cedar = Boats.CedarCountry(tree);
        if (!Sim.Structures.FellTreeAt(tree)) return "The axe glances off.";
        int got = cedar ? 3 : 2;
        if (GD.Randf() < p.Skills.Woodcraft) got++;
        p.Inventory.Add(ItemId.Cedar, got);
        p.Inventory.Add(ItemId.Firewood, 1);
        p.Skills.Learn(ref p.Skills.Woodcraft, 0.01f, "felling and splitting wood");
        p.Skills.Learn(ref p.Skills.Crafting, 0.003f, "gathering for the canoe");
        Sim.Noise(p.GlobalPosition, 800f, p, "fell");
        p.RequestSkip(2f, "");
        return cedar ? $"You fell the cedar and split it with wedges into thin, straight lengths: {Items.Items.Lot(ItemId.Cedar, got)}, and the rest for the fire." : $"You fell a spruce and split out what straight stuff it has: {Items.Items.Lot(ItemId.Cedar, got)}. Heavier than cedar, but it will serve.";
    }

    // ---- the bed ---------------------------------------------------------------

    public static string Why(PlayerController p, BoatKind kind)
    {
        var spec = Boats.Of(kind);
        if (p.Aboard != null || p.Carrying != null) return "Not from a canoe.";
        var cell = p.Cell;
        if (Sim.Terrain == null || Sim.Terrain.IsWaterOrIce(cell)) return "A canoe is built on dry ground, beside the water.";
        if (p.IsSheltered) return "Not indoors: the bed is made outside, on beaten earth by the water.";
        bool water = false;
        for (int dx = -6; dx <= 6 && !water; dx++) for (int dy = -12; dy <= 12 && !water; dy++) if (Sim.Terrain.IsWaterOrIce(new Vector2I(cell.X + dx, cell.Y + dy))) water = true;
        if (!water) return "Too far from the water: she has to be carried down to it when she is done. Make the bed nearer the shore.";
        foreach (var c in Sim.Spawner.Canoes) if (c.Building && !c.Wrecked && c.GlobalPosition.DistanceTo(p.GlobalPosition) < 220f) return "There is a canoe bed here already.";
        if (spec.Planked && !Posts.NearAny(p.GlobalPosition, 2500f) && p.GlobalPosition.DistanceTo(WorldLayout.FortCentre) > 3500f) return "A York boat is built at a post, where there are planks, a saw-pit and a smith.";
        return "";
    }

    public static string LayBed(PlayerController p, BoatKind kind)
    {
        string why = Why(p, kind);
        if (why != "") return why;
        var c = Sim.Spawner.PlaceCanoe(-1, p.GlobalPosition + new Vector2(0, 30), kind);
        c.Stage = 0;
        for (int i = 0; i < 4; i++) c.Parts[i] = 0f;
        c.Shape = 0f; c.Quality = 0f;
        c.Owner = p.DisplayName; c.Builders = p.DisplayName;
        c.BuiltAt = PlaceName(c.GlobalPosition);
        c.Refresh();
        Chronicle.Event($"{p.DisplayName} laid out a canoe bed.", p.GlobalPosition, p, weight: 0.3f);
        p.Journal.Add($"{GameCalendar.Instance?.DateString}: Laid out a bed for a {Boats.Of(kind).Name} {c.BuiltAt}.");
        p.RequestSkip(1f, "");
        return $"You clear and beat flat a bed of earth by the water for a {Boats.Of(kind).Name}. Now the work: {string.Join(", ", StageList(kind))}.";
    }

    private static IEnumerable<string> StageList(BoatKind kind) { foreach (var s in Boats.Of(kind).Stages) yield return Boats.StageName(s).ToLower(); }

    public static string PlaceName(Vector2 at)
    {
        string best = ""; float bestD = 6000f;
        foreach (var post in Posts.All) { if (!post.Standing) continue; float d = post.Centre.DistanceTo(at); if (d < bestD) { bestD = d; best = post.Name; } }
        foreach (var camp in Camps.All) { float d = IsoGrid.ToWorld(camp.Centre).DistanceTo(at); if (d < bestD) { bestD = d; best = camp.IsFamily ? camp.Name + "'s" : camp.Name; } }
        if (at.DistanceTo(WorldLayout.FortCentre) < bestD) { best = "York Factory"; }
        return best == "" ? "in the bush" : $"near {best}";
    }

    // ---- helpers ------------------------------------------------------------------

    // Who is at your side: friends following you, and the builders of the
    // camp or post this bed belongs to, if they are here.
    public static List<Npc> HelpersAt(PlayerController p, Canoe c)
    {
        var list = new List<Npc>();
        foreach (var a in Sim.AgentsWithin(c.GlobalPosition, 520f))
        {
            if (a is not Npc n || n.IsDead || n.HeldBy != null || n.Conversing) continue;
            bool mine = n.Following == p;
            bool theirs = c.Yard != "" && Boatwrights.IsBuilderFor(n, c);
            if (mine || theirs) list.Add(n);
            if (list.Count >= 6) break;
        }
        return list;
    }

    // ---- stages ----------------------------------------------------------------------

    public static (ItemId Item, int Count)[] NeedsFor(Canoe c, BoatStage s)
    {
        var spec = c.Spec;
        int i = System.Array.IndexOf(spec.Stages, s);
        return i < 0 ? System.Array.Empty<(ItemId, int)>() : spec.Needs[i];
    }

    // What is missing for the next stage, in words; empty when all is ready.
    public static string Lacks(PlayerController p, Canoe c, List<Npc> helpers)
    {
        if (c.NextStage is not BoatStage s) return "She is finished.";
        var lack = new List<string>();
        foreach (var (item, n) in NeedsFor(c, s))
        {
            int have = c.Stock.Count(item) + p.Inventory.Count(item);
            if (have < n) lack.Add(Items.Items.Lot(item, n - have));
        }
        if (Boats.ToolFor(s) is { } tool && !p.Inventory.Has(tool.Tool) && !helpers.Exists(h => h.Inventory.Has(tool.Tool) || h.IsBandPerson))
            lack.Add($"{tool.Why}");
        var cal = GameCalendar.Instance;
        if (Boats.NeedsThaw(s) && cal.IsFrozen && !c.Spec.Planked) lack.Add("the thaw (watap and gum can't be worked frozen)");
        if (Boats.NeedsFire(s) && !(Sim.Structures?.HearthNear(c.GlobalPosition, 520f) is { IsBurning: true })) lack.Add("a fire burning near the bed");
        float lead = p.Skills.Crafting; foreach (var h in helpers) lead = Mathf.Max(lead, h.Skills.Crafting);
        if (lead + 0.001f < c.Spec.MinSkill) lack.Add($"a builder who knows a {c.Spec.Name} (crafting {c.Spec.MinSkill:0.00}; you have {p.Skills.Crafting:0.00})");
        if (GameCalendar.Instance.Weather.FoulAt(c.GlobalPosition)) lack.Add("a dry day");
        return string.Join("; ", lack);
    }

    private static float Difficulty(Canoe c) => c.Kind switch { BoatKind.Small => 0.25f, BoatKind.North => 0.45f, BoatKind.Master => 0.65f, _ => 0.5f };
    private static int Count(Canoe c, BoatGame g)
    {
        int size = c.Kind switch { BoatKind.Small => 0, BoatKind.North => 1, BoatKind.Master => 2, _ => 1 };
        return g switch
        {
            BoatGame.Stakes => new[] { 8, 10, 12 }[size],
            BoatGame.Lash => new[] { 7, 10, 13 }[size],
            BoatGame.Sew => new[] { 16, 22, 28 }[size],
            BoatGame.Bend => new[] { 5, 7, 9 }[size],
            BoatGame.Nail => new[] { 12, 16, 20 }[size],
            BoatGame.Caulk => new[] { 6, 8, 10 }[size],
            BoatGame.Saw => new[] { 12, 14, 16 }[size],
            _ => 0,
        };
    }

    public static string BeginStage(PlayerController p, Canoe c)
    {
        if (c == null || !c.Building || c.NextStage is not BoatStage s) return "There is nothing to do on her.";
        if (_jobs.ContainsKey(p)) _jobs.Remove(p);
        var helpers = HelpersAt(p, c);
        string lacks = Lacks(p, c, helpers);
        if (lacks != "") return $"Not yet. Wanted: {lacks}.";
        var job = new Job { Token = _token++, Kind = "stage", Boat = c, Stage = s, Uses = NeedsFor(c, s), Game = Boats.GameFor(s) };
        int stageIdx = System.Array.IndexOf(c.Spec.Stages, s);
        foreach (var h in helpers) job.Helpers.Add((h, Boats.AiScore(h.Skills.Crafting)));
        job.Hours = c.Spec.Hours[stageIdx] / (1f + 0.7f * helpers.Count);
        _jobs[p] = job;
        var weak = new Godot.Collections.Array();
        if (job.Game == BoatGame.Gum) foreach (var l in c.Leaks) { weak.Add(l.X); weak.Add(l.Y); }
        Send(p, job, Boats.StageName(s), StageHelp(s, c), weak, -1f, "");
        return "";
    }

    private static string StageHelp(BoatStage s, Canoe c) => s switch
    {
        BoatStage.Bed => "The bark is laid white side down on the bed and the stakes driven round it to the canoe's shape. Where a stake is off, she'll be crooked.",
        BoatStage.Gunwales => "The gunwales are set in and lashed through the bark with watap, wrap after wrap. Too slack and they work loose; too hard and the root parts.",
        BoatStage.Sew => "The bark panels are sewn together with watap through holes made with the awl. A missed stitch is a weak place - and a leak, if the gum doesn't cover it.",
        BoatStage.Ribs => "The cedar ribs, steamed over the fire, are bent and driven in under the sheathing. Too little and she is flat; too much and the rib cracks.",
        BoatStage.Gum => $"Spruce gum and grease boiled down, laid hot along every seam. Weak stitches show red: cover them well. ({c.Leaks.Count} weak places from the sewing)",
        BoatStage.Keel => "The keel is sawn out at the pit, one man above and one below. Keep time with the man in the pit.",
        BoatStage.Frame => "The frames are set up along the keel, each on its mark. A frame off its line and she'll never sail true.",
        BoatStage.Plank => "The planks are hung and clench-nailed, strake after strake. Strike at the top of the swing.",
        BoatStage.Caulk => "Oakum is driven into every seam with the caulking iron, hard enough to hold, not so hard it splits the seam.",
        BoatStage.Pitch => "Hot pitch is paid over the caulked seams. Keep it on the seam, and keep the kettle hot.",
        _ => "",
    };

    private static void Send(PlayerController p, Job job, string title, string text, Godot.Collections.Array leaks, float watchSkill, string who)
    {
        var helpers = new Godot.Collections.Array();
        foreach (var (h, sc) in job.Helpers) { helpers.Add(h.DisplayName); helpers.Add(sc); }
        var req = new Godot.Collections.Dictionary
        {
            ["game"] = (int)job.Game, ["title"] = title, ["text"] = text, ["token"] = job.Token,
            ["n"] = job.Boat != null ? Count(job.Boat, job.Game) : 8, ["diff"] = job.Boat != null ? Difficulty(job.Boat) : 0.4f,
            ["planked"] = job.Boat?.Spec.Planked ?? false, ["leaks"] = leaks, ["helpers"] = helpers,
            ["watch"] = watchSkill >= 0f, ["skill"] = watchSkill >= 0f ? watchSkill : p.Skills.Crafting, ["who"] = who,
        };
        NetHub.I?.StartHandWork(p, req);
    }

    // The game is over: put it into the boat (or the convoy, or the watcher).
    public static string Result(PlayerController p, int token, float score, Godot.Collections.Array misses, Godot.Collections.Array fills)
    {
        if (!_jobs.TryGetValue(p, out var job) || job.Token != token) return "";
        _jobs.Remove(p);
        var missList = new List<float>(); foreach (var m in misses) missList.Add(m.AsSingle());
        var fillList = new List<float>(); foreach (var f in fills) fillList.Add(f.AsSingle());
        if (job.Kind == "watch") return Watched(p, job, score);
        if (score < 0f) return "You leave off. Nothing is spent; the work waits for you.";
        if (job.Kind == "convoy") return ConvoyPatched(p, job, score, fillList);
        var c = job.Boat;
        if (c == null || !GodotObject.IsInstanceValid(c) || c.Wrecked) return "";
        // What it took, from the stock laid by first and then the pack.
        foreach (var (item, n) in job.Uses)
        {
            int fromStock = c.Stock.TakeUpTo(item, n);
            if (fromStock < n) p.Inventory.Remove(item, Mathf.Min(n - fromStock, p.Inventory.Count(item)));
        }
        float mine = Boats.Blend(score, p.Skills.Crafting);
        float q = mine, w = 1f;
        foreach (var (_, hs) in job.Helpers) { q += hs * 0.6f; w += 0.6f; }
        q /= w;
        string said = job.Kind == "stage" ? ApplyStage(p, c, job, q, mine, missList) : ApplyRepair(p, c, job, q, mine, missList, fillList);
        // Everyone at it learns, the green most.
        float lesson = 0.006f + 0.012f * (1f - p.Skills.Crafting) * (0.4f + score);
        p.Skills.Learn(ref p.Skills.Crafting, lesson, job.Kind == "stage" ? "building a boat" : "mending a boat");
        foreach (var (h, _) in job.Helpers)
        {
            h.Skills.Learn(ref h.Skills.Crafting, 0.004f);
            h.ChangeRapport(p, 1.5f);
        }
        p.RequestSkip(job.Hours, "");
        c.Refresh();
        NetHub.I?.PushDialogue(p, p.Conversation.StartCanoe(c, said));
        return "";
    }

    // A stage's work into the hull, whoever did it (the player, or a yard's
    // builders by their skill).
    public static void ApplyStageWork(Canoe c, BoatStage s, float q, List<float> misses)
    {
        int idx = System.Array.IndexOf(c.Spec.Stages, s);
        c.StageScores[Mathf.Clamp(idx, 0, 4)] = q;
        switch (s)
        {
            case BoatStage.Bed: case BoatStage.Keel:
                c.Shape = q; c.Mend(HullPart.Skin, 0.65f + 0.35f * q); break;
            case BoatStage.Frame:
                c.Shape = (c.Shape + q) / 2f; c.Mend(HullPart.Ribs, 0.55f + 0.45f * q); break;
            case BoatStage.Gunwales:
                c.Mend(HullPart.Gunwales, 0.55f + 0.45f * q); break;
            case BoatStage.Sew:
                c.Mend(HullPart.Seams, 0.3f + 0.3f * q);
                foreach (var m in misses) c.Hole(m, 0.15f);
                break;
            case BoatStage.Plank:
                c.Mend(HullPart.Skin, Mathf.Max(c.Part(HullPart.Skin), 0.6f + 0.4f * q));
                c.Mend(HullPart.Gunwales, 0.6f + 0.4f * q);   // the gunwale is the top strake
                foreach (var m in misses) c.Hole(m, 0.2f);
                break;
            case BoatStage.Ribs:
                c.Mend(HullPart.Ribs, 0.5f + 0.5f * q); c.Shape = (c.Shape * 2f + q) / 3f; break;
            case BoatStage.Gum: case BoatStage.Caulk: case BoatStage.Pitch:
            {
                c.Mend(HullPart.Seams, 0.5f + 0.5f * q);
                // Good gum covers weak stitches; bare seam is a new leak.
                for (int i = c.Leaks.Count - 1; i >= 0; i--) if (GD.Randf() < 0.35f + 0.6f * q) c.Leaks.RemoveAt(i);
                foreach (var m in misses) c.Hole(m, 0.25f);
                break;
            }
        }
        c.Stage++;
        if (!c.Launched) return;
        float sum = 0f; for (int i = 0; i < c.Spec.Stages.Length; i++) sum += c.StageScores[i];
        c.Quality = sum / c.Spec.Stages.Length;
        c.BuiltDay = Today;
        c.Bilge = 0f;
    }

    private static string ApplyStage(PlayerController p, Canoe c, Job job, float q, float mine, List<float> misses)
    {
        var s = job.Stage;
        ApplyStageWork(c, s, q, misses);
        c.WorkHours = 0f;
        var helpers = new List<string>(); foreach (var (h, _) in job.Helpers) if (!c.Builders.Contains(h.DisplayName)) helpers.Add(h.DisplayName);
        if (helpers.Count > 0) c.Builders = c.Builders + ", " + string.Join(", ", helpers);
        string how = $"{Boats.StageName(s)}: {Boats.Grade(q)} work{(job.Helpers.Count > 0 ? $" (your own {Boats.Grade(mine)}, with {job.Helpers.Count} beside you)" : "")}.";
        if (!c.Launched) return $"{how} Next: {Boats.StageName(c.NextStage.Value).ToLower()}.";
        // Done. A yard's boat that you put the last of the work into is
        // still theirs (or whoever paid for it).
        if (c.Yard != "" && Boatwrights.YardOf(c) is { } yard) Boatwrights.Finished(yard, c);
        else if (c.CommissionedBy == "" && c.Owner == "") c.Owner = p.DisplayName;
        Chronicle.Event($"{p.DisplayName} finished a {c.Spec.Name}.", c.GlobalPosition, p, weight: 0.8f);
        p.Journal.Add($"{GameCalendar.Instance?.DateString}: Finished a {c.Spec.Name} {c.BuiltAt}: {Boats.Grade(c.Quality)} work. Built by {c.Builders}.");
        foreach (var a in Sim.AgentsWithin(c.GlobalPosition, 700f)) if (a is Npc n && !n.IsDead && n.IsBandPerson) n.ChangeRapport(p, 2f);
        return $"{how}\n\nShe is done: a {c.Spec.Name}, {Boats.Grade(c.Quality)} work. {(c.Leaks.Count > 0 ? $"Put in the water she weeps at {c.Leaks.Count} place{(c.Leaks.Count == 1 ? "" : "s")} - gum them." : "Put in the water she is tight: not a drop comes in.")}";
    }

    // ---- mending -------------------------------------------------------------------

    public sealed record Mend(string Key, string Label, HullPart Part, BoatGame Game, (ItemId, int)[] Uses, float Hours);

    public static List<Mend> Mends(Canoe c)
    {
        var list = new List<Mend>();
        if (!c.Launched || c.Wrecked) return list;
        bool pl = c.Spec.Planked;
        int gum = Mathf.Max(1, Mathf.CeilToInt(c.Leaks.Count / 3f));
        if (c.Leaks.Count > 0 || c.Part(HullPart.Seams) < 0.8f)
            list.Add(pl ? new Mend("caulk", $"Caulk and pitch her seams ({c.Leaks.Count} leaks)", HullPart.Seams, BoatGame.Patch, new[] { (ItemId.Oakum, 1), (ItemId.Pitch, 1) }, 2f)
                        : new Mend("leaks", $"Gum her leaks ({c.Leaks.Count}; {gum} gum)", HullPart.Seams, BoatGame.Patch, new[] { (ItemId.SpruceGum, gum) }, 1f + 0.3f * c.Leaks.Count));
        if (c.Part(HullPart.Skin) < 0.8f)
            list.Add(pl ? new Mend("plank", "Put in a new plank", HullPart.Skin, BoatGame.Nail, new[] { (ItemId.Plank, 1), (ItemId.Nails, 1) }, 3f)
                        : new Mend("bark", "Sew a bark patch over the worst of her", HullPart.Skin, BoatGame.Sew, new[] { (ItemId.BirchBark, 1), (ItemId.Watap, 1), (ItemId.SpruceGum, 1) }, 2.5f));
        if (c.Part(HullPart.Ribs) < 0.8f)
            list.Add(new Mend("ribs", pl ? "Fit new timbers where the frames are cracked" : "Replace the cracked ribs", HullPart.Ribs, BoatGame.Bend, new[] { (ItemId.Cedar, 1) }, 3f));
        if (c.Part(HullPart.Gunwales) < 0.8f)
            list.Add(new Mend("gunwales", pl ? "Make good the gunwale" : "Re-lash the gunwales", HullPart.Gunwales, BoatGame.Lash, pl ? new[] { (ItemId.Cedar, 1), (ItemId.Nails, 1) } : new[] { (ItemId.Watap, 2) }, 2f));
        return list;
    }

    public static string BeginMend(PlayerController p, Canoe c, string key)
    {
        if (c == null || !c.Launched) return "";
        if (!c.IsFree && p.Aboard != c) return "Someone is in her.";
        if (p.Aboard == c) return "Bring her ashore first: you can't mend a canoe you're sitting in.";
        var m = Mends(c).Find(x => x.Key == key);
        if (m == null) return "She doesn't need that.";
        foreach (var (item, n) in m.Uses)
            if (c.Stock.Count(item) + p.Inventory.Count(item) < n) return $"You need {Items.Items.Lot(item, n)} for that.";
        bool hot = Sim.Structures?.HearthNear(c.GlobalPosition, 520f) is { IsBurning: true };
        if (m.Game == BoatGame.Patch && !hot)
        {
            // A small fire on the bank to melt the gum: one stick of wood and a spark.
            if (!p.Inventory.Has(ItemId.Firewood) || !p.Inventory.Has(ItemId.Flint)) return "The gum has to be melted: you need a fire on the bank (or firewood and flint to make one).";
            p.Inventory.Remove(ItemId.Firewood);
        }
        if (m.Key is "ribs" && !p.Inventory.Has(ItemId.CrookedKnife)) return "You'd want a crooked knife to shave the new ribs.";
        if (m.Key is "bark" && !p.Inventory.Has(ItemId.Awl)) return "You need an awl to sew a patch.";
        var job = new Job { Token = _token++, Kind = "repair", Boat = c, Repair = key, Uses = m.Uses, Hours = m.Hours, Game = m.Game };
        foreach (var h in HelpersAt(p, c)) job.Helpers.Add((h, Boats.AiScore(h.Skills.Crafting)));
        _jobs[p] = job;
        var leaks = new Godot.Collections.Array();
        foreach (var l in c.Leaks) { leaks.Add(l.X); leaks.Add(l.Y); }
        string text = key switch
        {
            "leaks" => "Turn her over on the bank. Where the water came in, warm the gum and work it into the seam and the stitch-holes.",
            "caulk" => "Rake out the rotten oakum, drive in new, and pay hot pitch over it.",
            "bark" => "A patch of bark sewn over the torn place with watap, then gummed.",
            "plank" => "The split plank out and a new one hung and clenched.",
            "ribs" => "Out with the cracked ribs, new cedar shaved, steamed and bent in.",
            _ => "The lashing renewed where it has worked loose.",
        };
        Send(p, job, m.Label, text, leaks, -1f, "");
        return "";
    }

    private static string ApplyRepair(PlayerController p, Canoe c, Job job, float q, float mine, List<float> misses, List<float> fills)
    {
        int before = c.Leaks.Count;
        switch (job.Repair)
        {
            case "leaks": case "caulk":
            {
                // Each leak gummed home is gone; half-done ones are smaller.
                for (int i = c.Leaks.Count - 1; i >= 0; i--)
                {
                    float f = i < fills.Count ? fills[i] : 0f;
                    if (f >= 0.999f) c.Leaks.RemoveAt(i);
                    else if (f > 0f) c.Leaks[i] = new Vector2(c.Leaks[i].X, c.Leaks[i].Y * (1f - f));
                }
                c.Mend(HullPart.Seams, Mathf.Min(0.95f, c.Part(HullPart.Seams) + 0.25f * q));
                return $"{(job.Repair == "caulk" ? "Caulked and pitched" : "Gummed")}: {before - c.Leaks.Count} of {before} leaks stopped. {(c.Leaks.Count == 0 ? "She's tight." : $"{c.Leaks.Count} still weep.")}";
            }
            case "bark": case "plank":
                c.Mend(HullPart.Skin, Mathf.Min(0.95f, c.Part(HullPart.Skin) + c.Spec.SkinPerPatch * (0.4f + q)));
                foreach (var m in misses) c.Hole(m, 0.1f);
                for (int i = c.Leaks.Count - 1; i >= 0 && i >= c.Leaks.Count - 2; i--) if (c.Leaks[i].Y > 0.3f && GD.Randf() < q) c.Leaks.RemoveAt(i);
                return $"{(job.Repair == "plank" ? "A new plank in" : "Patched")}: her {Boats.PartName(HullPart.Skin, c.Spec.Planked).ToLower()} is {Boats.Condition(c.Part(HullPart.Skin))} now ({Boats.Grade(q)} work).";
            case "ribs":
                c.Mend(HullPart.Ribs, Mathf.Min(0.95f, c.Part(HullPart.Ribs) + 0.3f * (0.4f + q)));
                return $"New {(c.Spec.Planked ? "timbers" : "ribs")} in: {Boats.Condition(c.Part(HullPart.Ribs))} ({Boats.Grade(q)} work).";
            default:
                c.Mend(HullPart.Gunwales, Mathf.Min(0.95f, c.Part(HullPart.Gunwales) + 0.35f * (0.4f + q)));
                return $"The gunwales are {Boats.Condition(c.Part(HullPart.Gunwales))} again ({Boats.Grade(q)} work).";
        }
    }

    // ---- watching a master -------------------------------------------------------------

    // Watch someone at a boat: their hands play it (their skill), and you
    // learn from it what you can.
    public static string BeginWatch(PlayerController p, Npc n, Canoe c)
    {
        if (n == null || c == null) return "";
        var s = c.NextStage ?? (c.Spec.Planked ? BoatStage.Caulk : BoatStage.Gum);
        var job = new Job { Token = _token++, Kind = "watch", Boat = c, Stage = s, Watched = n, WatchedSkill = n.Skills.Crafting, Game = Boats.GameFor(s), Hours = 1.5f };
        _jobs[p] = job;
        var weak = new Godot.Collections.Array();
        foreach (var l in c.Leaks) { weak.Add(l.X); weak.Add(l.Y); }
        Send(p, job, $"{n.DisplayName}: {Boats.StageName(s).ToLower()}", StageHelp(s, c), weak, n.Skills.Crafting, n.DisplayName);
        return "";
    }

    private static string Watched(PlayerController p, Job job, float score)
    {
        // A master shows more than a green hand does; you learn up to a little past them.
        float gap = Mathf.Max(0f, job.WatchedSkill + 0.1f - p.Skills.Crafting);
        float lesson = Mathf.Min(0.012f, gap * 0.04f);
        p.Skills.Learn(ref p.Skills.Crafting, lesson, "watching a builder at work");
        if (job.Watched != null && GodotObject.IsInstanceValid(job.Watched)) job.Watched.ChangeRapport(p, 1f);
        p.RequestSkip(job.Hours, "");
        if (job.Boat != null && GodotObject.IsInstanceValid(job.Boat)) NetHub.I?.PushDialogue(p, p.Conversation.StartCanoe(job.Boat, lesson > 0.001f ? $"You watch {job.Watched?.DisplayName} at it and see how it is done." : $"You watch {job.Watched?.DisplayName}, but there's little there you didn't know."));
        return "";
    }

    // ---- the brigade's leaking canoe (World/JourneyIncidents) -----------------------------

    public static void BeginConvoyPatch(PlayerController p, Convoy c)
    {
        var job = new Job { Token = _token++, Kind = "convoy", Convoy = c, Game = BoatGame.Patch, Hours = 2f };
        _jobs[p] = job;
        var leaks = new Godot.Collections.Array();
        int n = 3 + (int)((1f - c.Hull) * 6f);
        for (int i = 0; i < n; i++) { leaks.Add(GD.Randf() * 0.9f + 0.05f); leaks.Add(0.2f + GD.Randf() * 0.6f); }
        var req = new Godot.Collections.Dictionary
        {
            ["game"] = (int)BoatGame.Patch, ["title"] = "Gum the leaking canoe", ["token"] = job.Token, ["diff"] = 0.45f, ["leaks"] = leaks,
            ["text"] = "The canoe is unloaded and turned over on the bank. The gum pot is on the fire. Stop what leaks you can before the light goes.",
            ["planked"] = c.Run.Craft.Name == "york boat", ["skill"] = p.Skills.Crafting, ["watch"] = false, ["who"] = "",
        };
        NetHub.I?.StartHandWork(p, req);
    }

    private static string ConvoyPatched(PlayerController p, Job job, float score, List<float> fills)
    {
        var c = job.Convoy;
        if (c == null) return "";
        c.Hull = Mathf.Min(1f, c.Hull + 0.35f * Boats.Blend(score, p.Skills.Crafting));
        p.Skills.Learn(ref p.Skills.Crafting, 0.008f, "mending a boat");
        p.Standing += score * 1.5f;
        Chronicle.Event($"{p.DisplayName} gummed a leaking canoe of {c.Name}.", p.GlobalPosition, p, weight: 0.3f);
        return score >= 0.75f ? $"The canoe is tight again, and the guide says so before the men. ({c.Name}'s boats: {Boats.Condition(c.Hull)})" : score >= 0.4f ? $"She'll do, with a man bailing. ({c.Name}'s boats: {Boats.Condition(c.Hull)})" : "You make a poor job of it; the bowman goes over your work after you with his own gum.";
    }

    // ---- naming ---------------------------------------------------------------------

    public static readonly string[] Names =
    {
        "the Swallow", "the Loon", "the Otter", "the Kingfisher", "the Good Hope", "the Sainte-Anne", "the Marten", "the Grey Goose",
        "the Northern Light", "the Lucky Strike", "the Beaver", "the Thunderbird", "the White Birch", "the Fisher", "the Mink", "the Prince Rupert",
    };

    // ---- dev ---------------------------------------------------------------------

    // `--boattest=Small` (any BoatKind): a few seconds in, the host lays a bed
    // where he stands, is given the stuff, tools and a fire, and plays every
    // stage at 0.8 through the same calls a player's games make; then the boat
    // is run on the rocks and mended. Each step is printed.
    public static void DevTest(PlayerController p)
    {
        string arg = System.Array.Find(OS.GetCmdlineUserArgs(), a => a.StartsWith("--boattest="));
        if (arg == null) return;
        var argBits = arg.Substring(11).Split(':');
        if (!System.Enum.TryParse(argBits[0], out BoatKind kind)) return;
        int stopAt = argBits.Length > 1 && int.TryParse(argBits[1], out int st) ? st : 99;
        p.GetTree().CreateTimer(6.0).Timeout += () =>
        {
            void Say(string s) => GD.Print("[boattest] " + s);
            var spec = Boats.Of(kind);
            foreach (var stage in spec.Needs) foreach (var (item, n) in stage) p.Inventory.Add(item, n);
            foreach (var t in new[] { ItemId.Axe, ItemId.CrookedKnife, ItemId.Awl, ItemId.Kettle, ItemId.Flint }) if (!p.Inventory.Has(t)) p.Inventory.Add(t);
            p.Inventory.Add(ItemId.SpruceGum, 6); p.Inventory.Add(ItemId.BirchBark, 2); p.Inventory.Add(ItemId.Watap, 4); p.Inventory.Add(ItemId.Cedar, 2);
            p.Inventory.Add(ItemId.Oakum, 2); p.Inventory.Add(ItemId.Pitch, 2); p.Inventory.Add(ItemId.Plank, 2); p.Inventory.Add(ItemId.Nails, 2);
            p.Skills.Crafting = Mathf.Max(p.Skills.Crafting, spec.MinSkill);
            Say($"why not here: '{Why(p, kind)}'");
            Say(LayBed(p, kind));
            Canoe c = null; foreach (var x in Sim.Spawner.Canoes) if (x.Building && x.Owner == p.DisplayName) c = x;
            if (c == null) { Say("no bed"); return; }
            var fire = Sim.Structures?.BuildCampfire(IsoGrid.FromWorld(c.GlobalPosition) + new Vector2I(1, 2));
            if (fire != null) { fire.Feed(20f); fire.Light(); } else Say("no fire could be laid");
            int guard = 0;
            while (c.Building && guard++ < 8 && c.Stage < stopAt)
            {
                var s = c.NextStage.Value;
                string r = BeginStage(p, c);
                if (r != "") { Say($"{s}: refused: {r}"); return; }
                var job = _jobs[p];
                Result(p, job.Token, 0.8f, new Godot.Collections.Array { 0.3f }, new Godot.Collections.Array());
                Say($"{s}: done; stage {c.Stage}/{spec.Stages.Length}, shape {c.Shape:0.00}, parts {c.Parts[0]:0.00}/{c.Parts[1]:0.00}/{c.Parts[2]:0.00}/{c.Parts[3]:0.00}, leaks {c.Leaks.Count}");
                GameCalendar.Instance.CancelSkip(); p.WantsSkipHours = 0f;
            }
            UI.HandWork.Dismiss();
            if (c.Building)
            {
                Say($"stopped on the bed at stage {c.Stage}");
                NetHub.I?.PushDialogue(p, p.Conversation.Step(UI.Conversation.CanoeId, -1));
                // `--boattestsave`: save the world now (the quicksave), boats and all.
                if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--boattestsave") >= 0)
                {
                    foreach (var b in Sim.Spawner.Canoes) Say($"before save: {b.Index}:{b.Kind}/{b.Stage}/{b.Owner}/{b.Leaks.Count} leaks/stock {b.Stock.SlotCount}");
                    SaveGame.Save(); Say("saved: " + SaveGame.LastMessage);
                }
                return;
            }
            Say($"launched={c.Launched} quality {c.Quality:0.00} ({Boats.Grade(c.Quality)}), capacity {c.CapacityLbs:0}, speed {c.SpeedFactor:0.00}, leak rate {c.LeakRate:0.0000}, owner {c.Owner}");
            // On the rocks.
            c.Wear(HullPart.Skin, 0.4f); c.Wear(HullPart.Ribs, 0.3f); c.Hole(0.2f, 0.4f); c.Hole(0.7f, 0.3f); c.Wear(HullPart.Seams, 0.4f);
            Say($"after the rocks: {c.Parts[0]:0.00}/{c.Parts[1]:0.00}/{c.Parts[2]:0.00}/{c.Parts[3]:0.00}, leaks {c.Leaks.Count}, rate {c.LeakRate:0.0000}, speed {c.SpeedFactor:0.00}");
            foreach (var m in Mends(c))
            {
                string r = BeginMend(p, c, m.Key);
                if (r != "") { Say($"mend {m.Key}: refused: {r}"); continue; }
                var job = _jobs[p];
                var fills = new Godot.Collections.Array(); foreach (var _ in c.Leaks) fills.Add(1f);
                Result(p, job.Token, 0.85f, new Godot.Collections.Array(), fills);
                GameCalendar.Instance.CancelSkip(); p.WantsSkipHours = 0f;
                Say($"mend {m.Key}: {c.Parts[0]:0.00}/{c.Parts[1]:0.00}/{c.Parts[2]:0.00}/{c.Parts[3]:0.00}, leaks {c.Leaks.Count}");
            }
            var packed = c.Pack(); var copy = new Canoe(); copy.Unpack(packed);
            Say($"pack/unpack: kind {copy.Kind}, stage {copy.Stage}, quality {copy.Quality:0.00}, leaks {copy.Leaks.Count}, owner {copy.Owner}");
            Say($"menu: {p.Conversation.StartCanoe(c)["text"]}");
            copy.Free();
            UI.HandWork.Dismiss();
            // Leave her a little the worse, for the boats page.
            c.Hole(0.4f, 0.3f); c.Wear(HullPart.Gunwales, 0.35f); c.Bilge = 0.25f; c.BoatName = "the Swallow";
            NetHub.I?.PushDialogue(p, p.Conversation.Step(UI.Conversation.CanoeId, -1));
        };
    }
}
