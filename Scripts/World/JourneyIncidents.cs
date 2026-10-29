using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.Player;
using NetHub = IsometricFurTradeGame.Scripts.Net.Net;

namespace IsometricFurTradeGame.Scripts.World;

// A thousand miles in the boats is a story, not a wait (design doc P.5).
// When a player is riding with a brigade, the road now and then puts a
// choice to him: run the rapid or carry round it, what to do when the food
// runs short, whether to race the other house's canoes, whose load to take
// when a man goes down on the portage. Each choice moves the brigade (days,
// pieces, food) and the player (standing, the men's regard, his own back).
//
// Server only; called from Convoys' day on the road.
public static class JourneyIncidents
{
    private static readonly Dictionary<int, int> _lastDay = new();   // convoy id -> day of its last incident
    private static readonly Dictionary<int, HashSet<string>> _had = new();   // which kinds each crossing has had

    public static void Reset() { _lastDay.Clear(); _had.Clear(); }

    private static int Today()
    {
        var cal = GameCalendar.Instance;
        return cal == null ? 0 : Campaign.DayOf(cal.Year, cal.Month, cal.Day);
    }

    // Once a day for each convoy with someone aboard. `atPortage` is the
    // carrying place she came to today, if any.
    public static void Day(Convoy c, RoadEvent atPortage)
    {
        if (!NetHub.IsServer || c == null) return;
        var riders = new List<PlayerController>();
        foreach (var kv in Convoys.Riders) if (kv.Value == c && GodotObject.IsInstanceValid(kv.Key) && !kv.Key.IsDead && !kv.Key.Conversation.IsOpen) riders.Add(kv.Key);
        if (riders.Count == 0) return;
        int today = Today();
        if (_lastDay.TryGetValue(c.Id, out int last) && today - last < 2) return;
        if (!_had.TryGetValue(c.Id, out var had)) _had[c.Id] = had = new HashSet<string>();

        ScenePage page = null; string kind = null;
        if (atPortage != null && !had.Contains("hurt") && GD.Randf() < 0.45f) { kind = "hurt"; page = Hurt(c, atPortage, riders[0]); }
        else if (c.ShortDays >= 2 && !had.Contains("hungry")) { kind = "hungry"; page = Hungry(c, riders[0]); }
        else if (c.Moving && !had.Contains("rapid") && NextPortage(c) is { } rapid && GD.Randf() < 0.5f) { kind = "rapid"; page = Rapid(c, rapid, riders[0]); }
        else if (c.Moving && !had.Contains("race") && Rivalry.AreRivals(c.Company, c.Company == Faction.HBC ? Faction.NWC : Faction.HBC) && GD.Randf() < 0.35f) { kind = "race"; page = Race(c, riders[0]); }
        if (page == null) return;
        had.Add(kind);
        _lastDay[c.Id] = today;
        foreach (var p in riders)
        {
            if (p == Sim.Player) GameCalendar.Instance?.CancelSkip();
            NetHub.I?.PushDialogue(p, p.Conversation.StartScene(page));
        }
    }

    private static RoadEvent NextPortage(Convoy c)
    {
        if (c.NextEvent >= c.Path.Events.Count) return null;
        var e = c.Path.Events[c.NextEvent];
        if (e.Call || e.At - c.Along > 60f * WorldMap.PxPerKm) return null;
        // White water that might be run; not a height of land or a falls.
        return e.Name.Contains("Rapid") || !e.Name.Contains("Portage") && !e.Name.Contains("Falls") ? e : null;
    }

    private static ScenePage Page(Convoy c, string text) => new() { Title = char.ToUpperInvariant(c.Name[0]) + c.Name[1..], Text = text, CanLeave = false };
    private static ScenePage Done(Convoy c, string text) => Page(c, text).Add("Go on", () => null);

    private static void Men(Convoy c, PlayerController p, float rapport)
    {
        foreach (var n in c.Men) if (n != null && GodotObject.IsInstanceValid(n) && !n.IsDead) n.ChangeRapport(p, rapport);
    }

    private static string AMan(Convoy c)
    {
        foreach (var n in c.Men) if (n != null && GodotObject.IsInstanceValid(n) && !n.IsDead) return n.DisplayName;
        string[] names = c.Company == Faction.NWC ? new[] { "Basile Roy", "Amable Grignon", "Olivier Cote" } : new[] { "Magnus Isbister", "Peter Sabiston", "Nicol Heddle" };
        return names[GD.RandRange(0, names.Length - 1)];
    }

    private static void Note(PlayerController p, string line) => p.Journal.Add($"{GameCalendar.Instance?.DateString}: {line}");

    // ---- the rapid ------------------------------------------------------------------

    private static ScenePage Rapid(Convoy c, RoadEvent e, PlayerController p)
    {
        var page = Page(c, $"You hear {e.Name} long before you see it: a low roar, and then white water across the whole river. {c.Guide} stands up in the stern and looks at it a long time. \"Loaded as we are, we should carry round. But the men say she'll run, and it would save us a day on the portage.\" He looks at you. \"You're the Company's man. What do you say?\"");
        page.Add("\"Run it.\"", () =>
        {
            float risk = 0.25f + (c.Lots > c.Run.Craft.LotsPerBoat * c.Boats * 0.8f ? 0.15f : 0f);
            // She runs it instead of carrying: no portage days when she gets there.
            e = SkipPortage(c, e);
            if (GD.Randf() < risk)
            {
                int wet = 0;
                foreach (var (id, n) in new List<(ItemId, int)>(c.Cargo.All())) { int take = n / 10; if (take > 0) { c.Cargo.Remove(id, take); wet += take; } }
                p.Needs.Health = Mathf.Max(10f, p.Needs.Health - 12f); p.Needs.Warmth = Mathf.Max(5f, p.Needs.Warmth - 30f);
                p.Standing -= 1f;
                Note(p, $"You told {c.Guide} to run {e?.Name ?? "the rapid"}. A boat struck and {wet} pieces were lost.");
                return Done(c, $"Halfway down, the second boat strikes a rock with a crack like a musket. She swings broadside and fills. Men are in the water, you among them, and for a long cold minute you do not know which way is up. Everyone comes out alive, but {wet} pieces of the outfit are gone down the river, and {c.Guide} does not look at you for the rest of the day.");
            }
            Men(c, p, 5f); p.Standing += 1f;
            Note(p, $"You ran {e?.Name ?? "the rapid"} with {c.Name} and saved a day.");
            return Done(c, "\"Hold on, then!\" The boats go into it nose first. Spray, a lurch, the bowsman screaming at the rocks, and then you are through, soaked and laughing in the slack water below. The men are pleased with themselves, and with you. A day saved.");
        });
        page.Add("\"Carry round. The pieces matter more than a day.\"", () =>
        {
            Men(c, p, -1f); p.Standing += 0.5f;
            return Done(c, $"{c.Guide} nods slowly. \"Aye. The Factor would say the same.\" The men grumble, but they will do it. Ninety pounds on each man's back over the portage trail.");
        });
        page.Add("\"You're the guide. You decide.\"", () =>
        {
            if (GD.Randf() < 0.5f) { e = SkipPortage(c, e); return Done(c, $"{c.Guide} grins. \"Then we run it.\" And they do, cleanly. He knows his river."); }
            return Done(c, $"\"Then we carry.\" {c.Guide} steers for the landing above the white water.");
        });
        return page;
    }

    // The boats run the rapid: the portage at it costs nothing when they reach it.
    private static RoadEvent SkipPortage(Convoy c, RoadEvent e)
    {
        if (e != null) c.RunsRapid = e.Name;
        return e;
    }

    // ---- the food runs short ------------------------------------------------------------

    private static ScenePage Hungry(Convoy c, PlayerController p)
    {
        int food = 0; foreach (var (id, n) in p.Inventory.All()) if (Items.Items.Of(id).Kind == ItemKind.Food) food += n;
        var page = Page(c, $"The bags are nearly empty. For two days now the men have been paddling on a handful of pemmican each, and this morning {c.Guide} cut the ration again. Somebody muttered about turning back, and nobody told him to hold his tongue.");
        if (food > 0)
            page.Add($"Share out what you carry ({food})", () =>
            {
                foreach (var (id, n) in new List<(ItemId, int)>(p.Inventory.All()))
                    if (Items.Items.Of(id).Kind == ItemKind.Food) { p.Inventory.Remove(id, n); c.Provisions.Add(id, n); }
                c.ShortDays = 0;
                Men(c, p, 10f); p.Standing += 1.5f;
                Note(p, $"You shared out your own food among the men of {c.Name} when they were hungry.");
                return Done(c, "You open your pack in front of everyone and hand round what's in it. It is not much spread over so many, but the men won't forget that the new hand gave his own dinner away.");
            });
        if (p.Inventory.Has(ItemId.Tobacco) || p.Inventory.Has(ItemId.TradeGoods))
            page.Add("Put in at the next camp and trade for fish", () =>
            {
                var pay = p.Inventory.Has(ItemId.Tobacco) ? ItemId.Tobacco : ItemId.TradeGoods;
                p.Inventory.Remove(pay); c.Provisions.Add(ItemId.Fish, 4 + c.Crew / 6); c.ShortDays = 0;
                Men(c, p, 6f); p.Standing += 1f;
                Note(p, $"You bought fish for {c.Name} at a camp on the river with your own {Items.Items.Of(pay).Name}.");
                return Done(c, $"There is smoke on a point ahead. You trade your {Items.Items.Of(pay).Name} with the family there for a pile of whitefish, and that night for once everyone in the brigade goes to sleep full.");
            });
        page.Add("Set nets and lose a day", () =>
        {
            c.Now = ConvoyState.Held; c.DayLeft = 1; c.At = "on the bank, fishing"; c.CampAt = Convoys.LandNear(c.Path.At(c.Along)); c.HasCamp = true; c.Step = 0f;
            c.Provisions.Add(ItemId.Fish, 2 + c.Crew / 8); c.ShortDays = Mathf.Max(0, c.ShortDays - 2);
            return Done(c, $"{c.Guide} puts in at a sandbar and the nets go out. A day lost, but a few fish in the bags.");
        });
        page.Add("\"Tighten your belts. We go on.\"", () =>
        {
            Men(c, p, -6f);
            if (GD.Randf() < 0.4f) { c.Crew = Mathf.Max(c.Run.Boats * 2, c.Crew - 1); return Done(c, "The brigade goes on hungry. In the morning one man's place is empty and his blanket is gone. He has taken his chances on foot."); }
            return Done(c, "The brigade goes on hungry. Nobody says anything to you, and that is worse than if they did.");
        });
        return page;
    }

    // ---- a race with the other house --------------------------------------------------

    private static ScenePage Race(Convoy c, PlayerController p)
    {
        bool nwc = c.Company == Faction.NWC;
        string them = nwc ? "the Company's York boats" : "a Nor'Wester canoe";
        string their = nwc ? "a Bay man's" : "voyageurs'";
        var page = Page(c, $"Paddles flash behind you: {them}, coming up fast, eight men singing a {their} paddling song. They are going to pass. The men's backs stiffen. Somebody shouts across the water, and the answer that comes back cannot be printed.");
        page.Add("\"Race them!\"", () =>
        {
            bool won = GD.Randf() < 0.5f + (c.Crew >= c.Run.Boats * c.Run.Men ? 0.1f : -0.1f) - (c.ShortDays > 0 ? 0.15f : 0f);
            c.Along = Mathf.Min(c.Path.Length - 1f, c.Along + 25f * WorldMap.PxPerKm);
            p.Needs.Hunger = Mathf.Max(0f, p.Needs.Hunger - 15f);
            if (won)
            {
                Men(c, p, 8f); p.Standing += 1f;
                Rivalry.Raise(1f, $"{c.Name} raced the other house's men on the river");
                Note(p, $"{c.Name} raced the other house's men on the river and beat them.");
                return Done(c, "You put your back into it with the rest. For an hour the two crews are bow to bow, and then the other boat falls away, its steersman swearing. The men howl with joy. Twenty-five miles gone today, and every one of them earned.");
            }
            Men(c, p, 3f);
            return Done(c, "You put your back into it with the rest, but they are fresher and lighter, and they pull away laughing. Still, twenty-five miles gone today. The men are tired, sore and in a good mood.");
        });
        page.Add("\"Let them go. We have pieces to bring in.\"", () =>
        {
            Men(c, p, -3f); p.Standing += 0.5f;
            return Done(c, $"{them} slide past to jeers. The men paddle on in a sulk, but {c.Guide} gives you a small nod.");
        });
        return page;
    }

    // ---- a man hurt on the portage -----------------------------------------------------

    private static ScenePage Hurt(Convoy c, RoadEvent e, PlayerController p)
    {
        string man = AMan(c);
        var page = Page(c, $"Halfway over {e.Name}, with two pieces on his back, {man} puts a foot wrong on the wet rock and goes down with a cry. His ankle is already swelling. There are still his pieces to get over, and nobody has a spare back.");
        page.Add("Take his pieces as well as your own", () =>
        {
            p.Needs.Hunger = Mathf.Max(0f, p.Needs.Hunger - 20f);
            p.Needs.Health = Mathf.Max(20f, p.Needs.Health - 6f);
            Men(c, p, 8f); p.Standing += 1.5f;
            p.Skills.Learn(ref p.Skills.Woodcraft, 0.02f, $"the carrying place at {e.Name}");
            Note(p, $"You carried {man}'s pieces over {e.Name} after he hurt his ankle.");
            return Done(c, $"Three trips over the portage instead of two. By the last one your legs are shaking and the tumpline has rubbed your forehead raw. {man} limps beside you the whole way, swearing he'll pay you back. The guide writes your name in his book.");
        });
        page.Add("Help him over and leave a piece behind", () =>
        {
            foreach (var (id, n) in c.Cargo.All()) { c.Cargo.Remove(id, 1); break; }
            Men(c, p, 3f);
            return Done(c, $"You get {man} over with an arm round your shoulders. One piece stays behind under a spruce at the upper end, for someone to fetch another year. It probably never will be.");
        });
        page.Add("\"Somebody else can see to him.\"", () =>
        {
            Men(c, p, -5f);
            return Done(c, $"You shoulder your own load and go on. Behind you two of the others get {man} up between them. Later nobody offers you the pipe.");
        });
        return page;
    }
}
