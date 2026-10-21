using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;

namespace IsometricFurTradeGame.Scripts.Core;

// What matters now (design doc P.4): three lines in the corner of the
// screen so a player in a deep world always knows where they stand - the
// most pressing thing, the next step on what they've set out to do, and
// the freshest thing they've heard that touches them. Built on the host
// from what the player can actually know, mirrored to the owner.
public static class Focus
{
    public static string For(Player.PlayerController p)
    {
        if (p == null || p.IsDead) return "";
        return $"Now: {Pressing(p)}\nAim: {Aim(p)}\nHeard: {Heard(p)}";
    }

    private static string Pressing(Player.PlayerController p)
    {
        var cal = GameCalendar.Instance;
        int today = (int)(cal?.DayOfYear ?? 0f);
        var n = p.Needs;
        if (p.CaptiveOf != null) return $"you are held at {p.CaptiveOf.Title} - offer a ransom, give your parole, or walk out and be an escaper.";
        if (n.Health < 35f) return "you are badly hurt - eat, keep warm, and find a woman of the post to dress it.";
        if (n.Warmth < 25f) return "you are freezing - get to a fire, or make one.";
        if (n.Hunger < 25f) return "you are starving - eat, draw a ration at the stores, or ask a friend.";
        if (n.Thirst < 25f) return $"you are parched - drink ({Keybinds.KeyFor("drink")}).";
        foreach (var g in Grudges.All)
        {
            if (g.Settled || g.Against != p.DisplayName) continue;
            var holder = Lies.Find(g.Holder);
            if (holder != null && !holder.IsDead && holder.GlobalPosition.DistanceTo(p.GlobalPosition) < 900f)
                return $"{g.Holder} is close by, and holds you to account for {g.Victim}.";
        }
        if (p.LastWarning != "" && Mathf.PosMod(today - p.LastWarningDay, 365) <= 2) return $"a friend's warning - {p.LastWarning}";
        if (p.HomePost != null && Losses.OpenSaleAt(p.HomePost) is Loss sale) return $"{sale.Name}'s effects are being sold tonight - talk to {Losses.Auctioneer(sale)?.DisplayName ?? "the master"} to bid.";
        if (Quests.Focus(p) is string quest) return quest;
        if (p.Task.Kind != Player.TaskKind.None && !p.Task.IsComplete) return p.Task.Describe();
        if (p.HomePost != null)
        {
            int mouths = 0;
            foreach (var m in Sim.Npcs) if (!m.IsDead && m.HomeStores == p.HomePost) mouths++;
            float days = p.HomePost.FoodDays(mouths + 1);
            if (days < 10f) return $"the post has about {days:0} days of food in the stores.";
        }
        return "nothing presses. A good day to hunt, trade, or sit at the fire.";
    }

    private static string Aim(Player.PlayerController p)
    {
        foreach (var a in p.Ambitions) if (!a.Abandoned) return a.Line(p);
        return $"none declared - open your book ({Keybinds.KeyFor("journal")}) and choose what you mean to make of yourself.";
    }

    // The most relevant of the last dozen things in the journal, by the
    // director's measure of what touches this player; else the newest.
    private static string Heard(Player.PlayerController p)
    {
        if (p.Journal.Count == 0) return "nothing yet.";
        var recent = new List<string>();
        for (int i = p.Journal.Count - 1; i >= 0 && recent.Count < 12; i--) recent.Add(p.Journal[i]);
        string best = recent[0];
        float bestScore = Director.Relevance(best, p);
        foreach (var r in recent) { float s = Director.Relevance(r, p); if (s > bestScore) { best = r; bestScore = s; } }
        return best.Length > 140 ? best.Substring(0, 137) + "..." : best;
    }
}
