using System;
using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.Economy;

// One voice at Fort William (design doc 3.8.1): a wintering partner or the
// Montreal agents' man, with the shares he casts and the temper he argues with.
public class Councillor
{
    public string Name;
    public string District;
    public int Shares;
    public bool AgentFirm;          // McTavish, Frobisher & Co: capital and credit, not a district
    public float Ambition, Suspicion, Loyalty;   // loyalty here is to the concern as a whole
    public float Standing = 50f;    // how readily others' shares follow his
    public bool Present = true;
    public string Summary => $"{Name} ({District}, {Shares} share{(Shares == 1 ? "" : "s")}, standing {Standing:0})";
}

// One question before the council, with the ways it can go and what each
// choice does to the world if it carries.
public class CouncilItem
{
    public string Key;
    public string Question;
    public string[] Options;
    public Func<Councillor, int, float> Leaning;   // how much this councillor likes option i
    public Action<int> Apply;
    public int Decided = -1;
    public string Tally = "";
}

// The July council at Fort William (design doc 3.8, 3.8.1, 10.1.3): the
// generator behind the Nor'Westers' yearly mandate. Resolved off the map
// with the same kind of agents as everyone else; share-weighted votes;
// standing shifts whose shares follow whom. The agenda is whatever the
// attendees know about - an item exists only if someone carried it in.
public static class Council
{
    public static readonly List<Councillor> Members = new();
    public static readonly List<string> Minutes = new();
    public static int LastYear;

    // Letters the player sent up with the brigade (design doc 3.8.1: how non-partners get in).
    public static readonly List<(string item, int option, float weight, string from)> Letters = new();

    public static void Seed()
    {
        if (Members.Count > 0) return;
        Members.Add(new Councillor { Name = "William McGillivray", District = "Montreal (McTavish, McGillivrays & Co)", Shares = 6, AgentFirm = true, Ambition = 0.8f, Suspicion = 0.5f, Loyalty = 0.7f, Standing = 80f });
        Members.Add(new Councillor { Name = "Mr. Duncan Cameron", District = "the Bay (opposite York Factory)", Shares = 1, Ambition = 0.6f, Suspicion = 0.5f, Loyalty = 0.5f, Standing = 45f });
        Members.Add(new Councillor { Name = "John McDonald of Garth", District = "Red River", Shares = 2, Ambition = 0.7f, Suspicion = 0.6f, Loyalty = 0.6f, Standing = 60f });
        Members.Add(new Councillor { Name = "Alexander Henry the younger", District = "the Saskatchewan", Shares = 1, Ambition = 0.4f, Suspicion = 0.3f, Loyalty = 0.7f, Standing = 55f });
        Members.Add(new Councillor { Name = "Duncan McDougall", District = "Athabasca", Shares = 2, Ambition = 0.9f, Suspicion = 0.7f, Loyalty = 0.4f, Standing = 50f });
        Members.Add(new Councillor { Name = "Roderick McKenzie", District = "Lake Superior", Shares = 1, Ambition = 0.3f, Suspicion = 0.4f, Loyalty = 0.8f, Standing = 50f });
    }

    public static Councillor Local()
    {
        Seed();
        foreach (var c in Members) if (c.District.StartsWith("the Bay")) return c;
        return null;
    }

    // ---- the agenda, from what the room knows ---------------------------------

    private static List<CouncilItem> Agenda(NwcLedger nwc, Npc partner, float shipped, float quota)
    {
        var items = new List<CouncilItem>();
        var local = Local();
        float returnsRatio = quota > 0f ? shipped / quota : 1f;
        float bayPrice = Sim.Fort?.PeltPrice ?? 1f;

        // The price against the Bay: the agents want margin, the winterers want furs.
        items.Add(new CouncilItem
        {
            Key = "price", Question = "The standard against the Company at the Bay",
            Options = new[] { "hold the price", "undercut the Company by a fifth", "let the Bay post set its own price" },
            Leaning = (c, i) => i switch
            {
                0 => c.AgentFirm ? 1f : 0.3f + (returnsRatio > 0.9f ? 0.4f : 0f),
                1 => (c.AgentFirm ? -0.3f : 0.4f) + (returnsRatio < 0.7f ? 0.5f : 0f) + c.Ambition * 0.3f,
                _ => c == local ? 0.9f : 0.1f - c.Suspicion * 0.3f,
            },
            Apply = i =>
            {
                if (i == 0) nwc.PeltPrice = Mathf.Clamp(nwc.PeltPrice, 0.5f, 3f);
                else if (i == 1) nwc.PeltPrice = Mathf.Clamp(Mathf.Max(nwc.PeltPrice, bayPrice * 1.2f), 0.5f, 3f);
                else nwc.PriceAtDiscretion = true;
            },
        });

        // The outfit: the biggest zero-sum item (design doc 3.8.1).
        items.Add(new CouncilItem
        {
            Key = "outfit", Question = "The outfit for the Bay district",
            Options = new[] { "a lean outfit (60 pieces)", "the usual (90 pieces)", "a full outfit (130 pieces)" },
            Leaning = (c, i) => i switch
            {
                0 => (c.AgentFirm ? 0.5f : 0.4f) + (returnsRatio < 0.5f ? 0.5f : 0f) - (c == local ? 1f : 0f),
                1 => 0.6f,
                _ => (c == local ? 1.2f : -0.2f) + (returnsRatio > 1.1f ? 0.6f : 0f) - c.Ambition * 0.2f,   // every other district wants those pieces
            },
            Apply = i => nwc.OutfitPieces = i == 0 ? 60 : i == 1 ? 90 : 130,
        });

        // Rum: the winterers know what it buys; the agents know what it costs.
        items.Add(new CouncilItem
        {
            Key = "rum", Question = "Liquor in the trade",
            Options = new[] { "restraint - a dram, no more", "plenty of rum: it is what brings the furs" },
            Leaning = (c, i) => i == 0 ? c.Loyalty * 0.5f + (c.AgentFirm ? 0.3f : 0f) : 0.4f + c.Ambition * 0.4f + (returnsRatio < 0.8f ? 0.3f : 0f),
            Apply = i => nwc.RumShareSetting = i == 0 ? 0.2f : 0.4f,
        });

        // The rivalry: only on the table if the partner carried it in (design doc 3.8.1: the agenda comes from the world).
        bool knowsOfBlows = false;
        if (partner != null) foreach (var m in partner.Memories) if (m.Text.Contains("seize") || m.Text.Contains("broke into") || m.Text.Contains("struck")) knowsOfBlows = true;
        if (knowsOfBlows || Rivalry.Tension >= 50f)
        {
            items.Add(new CouncilItem
            {
                Key = "bay", Question = "The Company's post at the Bay",
                Options = new[] { "keep the peace and take their trade", "press them hard - their stores are fair game" },
                Leaning = (c, i) => i == 0 ? c.Loyalty * 0.4f + (1f - c.Ambition) * 0.4f + (c.AgentFirm ? 0.4f : 0f) : c.Ambition * 0.6f + c.Suspicion * 0.3f + (Rivalry.Tension > 70f ? 0.3f : 0f),
                Apply = i => { if (i == 0) Rivalry.Raise(-20f, "the council orders peace at the Bay"); else { Rivalry.Raise(15f, "the council orders the Bay pressed"); nwc.RaidsSanctioned = true; } },
            });
        }

        // The partner's share: his returns against the plan.
        items.Add(new CouncilItem
        {
            Key = "share", Question = $"{local?.Name}'s conduct of the Bay district",
            Options = new[] { "censure: the returns were poor", "confirm him in his post", "a second share, in recognition of the returns" },
            Leaning = (c, i) => i switch
            {
                0 => (returnsRatio < 0.6f ? 0.9f : -0.5f) + c.Suspicion * 0.2f - (c == local ? 2f : 0f),
                1 => 0.5f,
                _ => (returnsRatio > 1.2f ? 0.9f : -0.6f) - c.Ambition * 0.2f + (c == local ? 1f : 0f),
            },
            Apply = i =>
            {
                if (local == null) return;
                if (i == 0) { local.Standing = Mathf.Max(10f, local.Standing - 15f); if (partner != null) partner.ChangeRapport(partner, 0f); }
                else if (i == 2) { local.Shares++; local.Standing = Mathf.Min(100f, local.Standing + 15f); }
                else local.Standing = Mathf.Min(100f, local.Standing + 3f);
            },
        });
        return items;
    }

    // ---- deliberation (design doc 3.8.1) ---------------------------------------

    public static Message Convene(NwcLedger nwc, Npc partner, float shipped, float quota)
    {
        Seed();
        var cal = GameCalendar.Instance;
        LastYear = cal?.Year ?? 0;
        Minutes.Clear();
        var local = Local();
        // Who made it this year: a distant partner misses one in five.
        foreach (var c in Members) c.Present = c.AgentFirm || c == local || GD.Randf() > 0.2f;
        var items = Agenda(nwc, partner, shipped, quota);
        Minutes.Add($"Fort William, July {LastYear}. Present: {string.Join("; ", Members.FindAll(c => c.Present).ConvertAll(c => c.Name))}.");
        if (partner != null && partner.IsDead) Minutes.Add("The Bay partner is dead; his district is spoken for by the agents.");

        foreach (var item in items)
        {
            var weight = new float[item.Options.Length];
            // Each voice: his own leaning, shifted by whoever in the room he listens to.
            foreach (var c in Members)
            {
                if (!c.Present) continue;
                int pick = 0; float best = float.MinValue;
                for (int i = 0; i < item.Options.Length; i++)
                {
                    float l = item.Leaning(c, i);
                    // Standing (design doc 3.8.1): the loudest man in the room pulls the undecided.
                    foreach (var other in Members) if (other != c && other.Present && other.Standing > c.Standing + 15f) l += 0.15f * (item.Leaning(other, i) > 0.5f ? 1f : 0f) * (other.Standing - c.Standing) / 100f;
                    if (l > best) { best = l; pick = i; }
                }
                weight[pick] += c.Shares;
            }
            // Letters from the district (the player's testimony) carry a little.
            foreach (var (k, opt, w, from) in Letters) if (k == item.Key && opt >= 0 && opt < weight.Length) { weight[opt] += w; Minutes.Add($"  A letter from {from} was read on the question of {item.Question.ToLower()}."); }
            int decided = 0; for (int i = 1; i < weight.Length; i++) if (weight[i] > weight[decided]) decided = i;
            item.Decided = decided;
            var parts = new List<string>(); for (int i = 0; i < weight.Length; i++) parts.Add($"{item.Options[i]} {weight[i]:0}");
            item.Tally = string.Join(" / ", parts);
            Minutes.Add($"{item.Question}: {item.Options[decided]} ({item.Tally} shares).");
            item.Apply(decided);
        }
        Letters.Clear();

        // Dissent (design doc 3.8.1): the partner who lost the price vote may defy it.
        string dissent = "";
        var priceItem = items.Find(i => i.Key == "price");
        if (partner != null && !partner.IsDead && priceItem != null && local != null && priceItem.Leaning(local, priceItem.Decided) < 0.4f && partner.Personality.Ambition > 0.6f && partner.Personality.Loyalty < 0.5f)
        {
            nwc.PriceAtDiscretion = true;
            dissent = $" {partner.DisplayName} left the room saying he would set the price the country required, whatever the minutes said.";
            local.Standing = Mathf.Max(10f, local.Standing - 8f);
        }

        // The minutes are a message like any other (design doc 3.8.1): carried, lost, intercepted.
        var m = new Message
        {
            Kind = "mandate", From = "the partners at Fort William", To = "the wintering partner", Company = Faction.NWC,
            Subject = $"the minutes of the council of {LastYear}", Text = string.Join(" ", Minutes) + dissent, DayWritten = cal?.Day ?? 0,
        };
        m.Payload["shipped"] = shipped; m.Payload["quota"] = quota; m.Payload["minutes"] = m.Text;
        m.OnRead = reader =>
        {
            nwc.DirectiveText = m.Text;
            nwc.DirectiveReadDay = cal?.Day ?? 0;
            Sim.Notice($"{reader?.Describe() ?? "The partner"} reads the council's minutes: the Nor'Westers will pay {nwc.PeltPrice:0.00}{(nwc.PriceAtDiscretion ? " (or what he judges right)" : "")}, with {nwc.OutfitPieces} pieces landed.");
        };
        m.OnIntercepted = reader =>
        {
            if (reader == null || reader.Faction != Faction.HBC) return;
            Sim.Notice($"{reader.Describe()} reads the Nor'Westers' minutes: {nwc.DirectiveSummary()}. The Factor knows their whole year's plan.");
            Rivalry.Raise(6f, "the Nor'Westers' minutes read at the Bay");
            if (Sim.Fort != null) Sim.Fort.KnowsRivalPlan = true;
        };
        Sim.Notice($"At Fort William the partners have sat: {items.Count} questions decided. The minutes come north with the brigade.");
        return m;
    }

    public static string Report()
    {
        Seed();
        var sb = new System.Text.StringBuilder();
        foreach (var c in Members) sb.AppendLine(c.Summary);
        if (Minutes.Count > 0) { sb.AppendLine($"Last council ({LastYear}):"); foreach (var l in Minutes) sb.AppendLine("  " + l); }
        if (Letters.Count > 0) sb.AppendLine($"Letters going up with the brigade: {Letters.Count}");
        return sb.ToString();
    }
}
