using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;

namespace IsometricFurTradeGame.Scripts.Agents;

// What you know as a weapon (design doc P.10's other half). Lies are what
// you make up; leverage is what you saw. A misdeed the player witnessed
// first hand - the witness lists say so - can be put to the man privately:
// "I saw what you did." He pays for your silence (goods, a word with his
// master, an end to his stories about you) and resents it; or, if he has
// the nerve and the thing would not ruin him, he gets his version in first
// and tells his master you've been threatening him - which is true.
public static class Leverage
{
    // What this man did that the player saw with their own eyes.
    public static List<MemoryEntry> Holds(Player.PlayerController p, Npc n)
    {
        var found = new List<MemoryEntry>();
        var keys = new HashSet<string>();
        foreach (var x in Sim.Npcs)
            foreach (var m in x.Memories)
                if (m.Actor == n && m.Liar == null && m.Directness == 1 && m.Witnessed(p) && Lies.IsMisdeed(m.Text) && !m.Text.Contains("telling lies") && keys.Add(m.Key)) found.Add(m);
        if (found.Count > 3) found.RemoveRange(0, found.Count - 3);
        return found;
    }

    // How badly it would hurt him if his master heard: a killing or a
    // robbery is ruin; a blow or a quarrel is a bad week.
    private static float Gravity(MemoryEntry m) =>
        m.Text.Contains(" killed ") ? 3f : m.Text.Contains(" robbed ") || m.Text.Contains("broke into") || m.Text.Contains("stole ") || m.Text.Contains("on his own account") || m.Text.Contains("talking with their master") ? 2f : 1f;

    private static readonly HashSet<string> _pressed = new();
    public static bool Pressed(Agent p, MemoryEntry m) => _pressed.Contains(p.DisplayName + "|" + m.Key);
    public static void Reset() => _pressed.Clear();

    // The player puts it to him and names a price.
    public static string Press(Player.PlayerController p, Npc n, MemoryEntry m, string price)
    {
        _pressed.Add(p.DisplayName + "|" + m.Key);
        float g = Gravity(m);
        float defiance = n.Personality.Courage + n.Personality.Suspicion * 0.5f - g * 0.35f + (n.RapportWith(p) < -20f ? 0.2f : 0f);
        string deed = Barks.YouForm(m.Text, n.DisplayName).TrimEnd('.');
        n.ChangeRapport(p, -15f);
        if (defiance > 0.8f)
        {
            // He goes first: to his master, with the truth about you.
            var lead = n.HomeStores?.Leader;
            string to = lead != null && lead != n ? lead.DisplayName : "anyone who'll listen";
            if (lead != null && lead != n)
                lead.Remember(new MemoryEntry { Text = $"{p.DisplayName} has been threatening {n.DisplayName} to get his way.", Actor = p, Subject = p.DisplayName, Day = GameCalendar.Instance?.DayOfYear ?? 0f, Directness = 2, Weight = 1.4f, Key = $"threat{m.Key}{p.DisplayName}", Witnesses = new List<string> { n.DisplayName } });
            n.ChangeRapport(p, -15f);
            GD.Print($"[leverage] {n.DisplayName} defied {p.DisplayName} and went to {to}");
            return $"\"Tell who you like.\" {n.DisplayName} looks you in the eye. \"And I'll tell {to} what you just tried.\" He means it.";
        }
        n.Memories.Add(new MemoryEntry { Text = $"{p.DisplayName} has a hold over {n.DisplayName}.", Actor = p, Subject = p.DisplayName, Day = GameCalendar.Instance?.DayOfYear ?? 0f, Directness = 1, Weight = 0.9f, Key = $"hold{m.Key}", Witnesses = new List<string> { n.DisplayName, p.DisplayName } });
        GD.Print($"[leverage] {n.DisplayName} pays {p.DisplayName}: {price}");
        switch (price)
        {
            case "goods":
                foreach (var id in new[] { ItemId.TradeGoods, ItemId.BeaverPelt, ItemId.Rum, ItemId.Pemmican })
                {
                    if (!n.Inventory.Has(id)) continue;
                    int k = Mathf.Min((int)(2 + g * 2), n.Inventory.Count(id));
                    n.Inventory.Remove(id, k); p.Inventory.Add(id, k);
                    return $"He goes white, then counts out {k} {Items.Items.Of(id).Name.ToLower()} into your hand. \"That's the end of it,\" he says. It won't be, and you both know it.";
                }
                return "\"I've nothing,\" he says - and turns out his pockets to prove it. He'll remember you did this.";
            case "speak":
                var lead = n.HomeStores?.Leader;
                if (lead == null || lead == n) return "\"There's nobody over me to speak to.\" But he'll remember you asked.";
                lead.ChangeRapport(p, 8f);
                if (lead.Faction == p.Faction) p.Standing += 3f;
                return $"\"I'll put in a word with {lead.DisplayName}.\" He does. It sticks in his throat.";
            case "stop":
                int lies = 0;
                foreach (var x in Sim.Npcs) lies += x.Memories.RemoveAll(e => e.Liar == n.DisplayName && e.Accused == p.DisplayName);
                n.ChangeRapport(p, 0f);
                return lies > 0 ? $"\"All right. Not another word about you.\" He goes round taking back what he said; {lies} of them let it go." : "\"I've said nothing against you.\" Perhaps that's even true.";
        }
        return "";
    }

    // Short form for a menu: "struck Nancy Isbister".
    public static string Label(MemoryEntry m, Npc n) => Barks.YouForm(m.Text, n.DisplayName).TrimEnd('.');
}
