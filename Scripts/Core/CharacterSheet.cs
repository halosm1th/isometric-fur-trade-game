using System;
using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Items;

namespace IsometricFurTradeGame.Scripts.Core;

// Who the player is when the game begins (design doc 5.4): which road in -
// the Company's ship, the Nor'Westers' brigade, or born to one of the
// nations - a background on that road, a couple of traits, and what they
// mean to make of themselves. Not a second stat system: every choice sets
// the same skills, tongues, kit, standing, kin and ambitions any agent has.
// Chosen on the new-game page; travels to the host with the player's name.
public enum StartPath { HBC, NWC, Cree, Assiniboine }

public record BackgroundDef(string Title, string Blurb, Action<Player.PlayerController> Apply);
public record TraitDef(string Id, string Title, string Blurb, Action<Player.PlayerController> Apply);

public static class CharacterSheet
{
    public static StartPath Path = StartPath.HBC;
    public static int Background;
    public static readonly List<string> Traits = new();
    public static AmbitionKind Ambition = AmbitionKind.None;
    public const int MaxTraits = 2;

    public static string PathTitle(StartPath p) => p switch
    {
        StartPath.HBC => "The Hudson's Bay Company - off the ship at York Factory",
        StartPath.NWC => "The North West Company - up with the summer brigade",
        StartPath.Cree => "The Cree (Ininiwak) - born to the band by the Bay",
        StartPath.Assiniboine => "The Assiniboine (Nakoda) - born to the band upland",
        _ => "",
    };

    public static string PathBlurb(StartPath p) => p switch
    {
        StartPath.HBC => "You come ashore from the annual ship, a day out from the Factory, with a letter for the Factor and a contract for five years. Rich, slow, and bound by London's orders a year late.",
        StartPath.NWC => "You come up the river in a canot du nord with the brigade from Fort William, to winter at the partner's house across from the Bay men. Mobile, stretched thin, and ruled by the partners in council.",
        StartPath.Cree => "You are of the band that hunts the lowlands and trades at the Bay. Your family, your hunting grounds, and two companies who want your furs.",
        StartPath.Assiniboine => "You are of the band that winters upland and brings pemmican and furs down to the posts. Your kin, your horses' country far west, and traders who need what you have.",
        _ => "",
    };

    public static bool IsBand(StartPath p) => p is StartPath.Cree or StartPath.Assiniboine;

    // ---- backgrounds -----------------------------------------------------------------

    public static BackgroundDef[] BackgroundsFor(StartPath p) => p switch
    {
        StartPath.HBC => new BackgroundDef[]
        {
            new("Orkney labourer", "Signed on at Stromness for the wages. Strong, handy with an axe, and owed nothing by anyone.",
                pc => { pc.Skills.Woodcraft = 0.35f; pc.Inventory.Add(ItemId.Axe); }),
            new("Apprentice clerk", "A writer's place, a fair hand and the Committee's good opinion. The Factor will expect you at the books; the other clerks will see a rival.",
                pc => { pc.Skills.Trade = 0.4f; pc.Rank = "clerk"; pc.Standing = 30f; }),
            new("Country-born", "Son of a Company man and a Cree mother, raised about the Factory. You have the tongue and the country; London has never heard of you.",
                pc => { pc.Skills.Hunting = 0.4f; pc.CreeFluency = 0.7f; Kin(pc, Sim.Band, 1, 25f); }),
        },
        StartPath.NWC => new BackgroundDef[]
        {
            new("Voyageur", "A milieu from the St. Lawrence parishes, paddling for a wage. You can carry two pieces over a portage and sing the whole way.",
                pc => { pc.Skills.Woodcraft = 0.3f; pc.Skills.Hunting = 0.25f; pc.Inventory.Add(ItemId.Tumpline); }),
            new("Clerk from Montreal", "Five years' apprenticeship toward a partner's share, if you live and the partners like you.",
                pc => { pc.Skills.Trade = 0.4f; pc.Rank = "clerk"; pc.Standing = 30f; }),
            new("Freeman's son", "Metis, son of a freeman and a Cree mother. You know the country and the people better than the company does.",
                pc => { pc.Skills.Hunting = 0.45f; pc.CreeFluency = 0.8f; Kin(pc, Sim.Band, 1, 25f); }),
        },
        _ => new BackgroundDef[]
        {
            new("Hunter", "Young, a good shot, and not yet married. Your father's family hunts for the whole camp.",
                pc => { pc.Skills.Hunting = 0.5f; pc.Skills.Trapping = 0.35f; pc.Inventory.Add(ItemId.Bow); }),
            new("Trapper and go-between", "You run a line and you go down to the posts for the band. The traders know your face; you know their prices.",
                pc => { pc.Skills.Trapping = 0.45f; pc.Skills.Trade = 0.35f; pc.Inventory.Add(ItemId.SteelTrap, 2); }),
            new("Nephew of the leading man", "Your uncle leads the band. People listen to you because of him, and watch what you do with it.",
                pc => { pc.Skills.Hunting = 0.35f; var b = BandOf(pc); if (b?.Leader != null) Bind(pc, b.Leader, 45f); }),
        },
    };

    // ---- traits ----------------------------------------------------------------------

    public static readonly TraitDef[] AllTraits =
    {
        new("eye", "Keen eye", "Hunting comes easy to you.", pc => pc.Skills.Hunting = Mathf.Min(1f, pc.Skills.Hunting + 0.2f)),
        new("hands", "Patient hands", "Traps and snares, set right the first time.", pc => pc.Skills.Trapping = Mathf.Min(1f, pc.Skills.Trapping + 0.2f)),
        new("tongue", "Silver tongue", "A better bargain, and people like to hear you talk.", pc => pc.Skills.Trade = Mathf.Min(1f, pc.Skills.Trade + 0.2f)),
        new("bush", "Bushwise", "Wood, fire, snow and weather. You carry snowshoes.", pc => { pc.Skills.Woodcraft = Mathf.Min(1f, pc.Skills.Woodcraft + 0.2f); pc.Inventory.Add(ItemId.Snowshoes); }),
        new("hardy", "Hardy", "You were raised cold. A point blanket and dried meat in your pack.", pc => { pc.Inventory.Add(ItemId.PointBlanket); pc.Inventory.Add(ItemId.DriedMeat, 3); }),
        new("tongues", "Ear for tongues", "Languages come to you - the next people's as well as your own.", pc => { pc.CreeFluency = Mathf.Min(1f, pc.CreeFluency + 0.3f); pc.AssiniboineFluency = Mathf.Min(1f, pc.AssiniboineFluency + 0.3f); }),
        new("bottle", "A bottle in the pack", "Three drams of rum. Friends at first, and a habit later.", pc => pc.Inventory.Add(ItemId.Rum, 3)),
        new("purse", "Something put by", "Twelve more pieces of goods than you ought to have, and no one to ask where from.", pc => pc.Inventory.Add(ItemId.TradeGoods, 12)),
    };

    // ---- the sheet on the wire ---------------------------------------------------------

    public static string Encode() => $"{(int)Path}|{Background}|{string.Join(",", Traits)}|{(int)Ambition}";

    // Set the player up from a sheet, on the host. Empty or bad = the old
    // default (an HBC labourer), so dev args and older clients still work.
    public static void Apply(Player.PlayerController pc, string sheet)
    {
        var parts = (sheet ?? "").Split('|');
        var path = StartPath.HBC; int bg = 0; var traits = new List<string>(); var amb = AmbitionKind.None;
        if (parts.Length >= 4)
        {
            if (int.TryParse(parts[0], out int pi) && Enum.IsDefined(typeof(StartPath), pi)) path = (StartPath)pi;
            int.TryParse(parts[1], out bg);
            foreach (var t in parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries)) if (traits.Count < MaxTraits) traits.Add(t);
            if (int.TryParse(parts[3], out int ai) && Enum.IsDefined(typeof(AmbitionKind), ai)) amb = (AmbitionKind)ai;
        }
        pc.StartPath = path;
        pc.Faction = path switch { StartPath.NWC => Faction.NWC, StartPath.Cree => Faction.Cree, StartPath.Assiniboine => Faction.Assiniboine, _ => Faction.HBC };
        if (path == StartPath.Cree) pc.CreeFluency = 1f;
        if (path == StartPath.Assiniboine) { pc.AssiniboineFluency = 1f; pc.CreeFluency = Mathf.Max(pc.CreeFluency, 0.3f); }
        if (IsBand(path))
        {
            // Born to it: a family in the camp, your own kit, nothing of the Company's.
            pc.Inventory.Remove(ItemId.Capote); pc.Inventory.Remove(ItemId.Tumpline);
            pc.Inventory.Add(ItemId.HideCoat); pc.Inventory.Add(ItemId.Snowshoes); pc.Inventory.Add(ItemId.DriedMeat, 2);
            pc.Rank = "of the band"; pc.Standing = 0f;
            Kin(pc, BandOf(pc), 3, 50f);
        }
        var bgs = BackgroundsFor(path);
        bg = Mathf.Clamp(bg, 0, bgs.Length - 1);
        pc.BackgroundTitle = bgs[bg].Title;
        bgs[bg].Apply(pc);
        foreach (var t in traits) foreach (var def in AllTraits) if (def.Id == t) def.Apply(pc);
        if (amb != AmbitionKind.None)
        {
            var cal = GameCalendar.Instance;
            pc.Ambitions.Add(new Ambition { Kind = amb, DeclaredDay = cal?.Day ?? 0, DeclaredYear = cal?.Year ?? 1811 });
            pc.AmbitionsDeclared = true;
        }
        GD.Print($"[sheet] {pc.DisplayName}: {path}, {pc.BackgroundTitle}, traits {string.Join("/", traits)}, ambition {amb}");
    }

    // ---- kin -----------------------------------------------------------------------------

    // Who a player counts as family (design doc 8): their regard starts high
    // and they share a past - a memory of growing up together - so they act
    // as friends would (Friends.Bond) from the first day.
    private static readonly Dictionary<string, HashSet<string>> _kin = new();
    public static bool IsKin(Agent player, Agent other) => player != null && other != null && _kin.TryGetValue(player.DisplayName, out var k) && k.Contains(other.DisplayName);
    public static IEnumerable<string> KinOf(Agent player) => player != null && _kin.TryGetValue(player.DisplayName, out var k) ? k : Array.Empty<string>();
    public static void ResetKin() => _kin.Clear();

    // After a load: the kin list comes back with the player; rebuild the index.
    public static void RestoreKin(Player.PlayerController pc)
    {
        if (pc.KinNames.Count == 0) return;
        if (!_kin.TryGetValue(pc.DisplayName, out var k)) _kin[pc.DisplayName] = k = new HashSet<string>();
        foreach (var n in pc.KinNames) k.Add(n);
    }

    public static Economy.BandLedger BandOf(Player.PlayerController pc) => pc.Faction == Faction.Assiniboine ? Sim.Assiniboine : Sim.Band;

    private static void Kin(Player.PlayerController pc, Economy.Household home, int count, float regard)
    {
        if (home == null) return;
        int n = 0;
        foreach (var npc in Sim.Npcs)
        {
            if (npc.IsDead || npc.HomeStores != home || npc == home.Leader && count < 3) continue;
            Bind(pc, npc, regard);
            if (++n >= count) break;
        }
    }

    public static void Bind(Player.PlayerController pc, Npc npc, float regard)
    {
        if (!_kin.TryGetValue(pc.DisplayName, out var k)) _kin[pc.DisplayName] = k = new HashSet<string>();
        k.Add(npc.DisplayName);
        npc.Rapport[pc] = Mathf.Max(npc.RapportWith(pc), regard);
        pc.KinNames.Add(npc.DisplayName);
        // A shared past: two firsthand memories, so the bond is real from the start.
        for (int i = 0; i < 2; i++)
            npc.Memories.Add(new MemoryEntry
            {
                Text = i == 0 ? $"{npc.DisplayName} and {pc.DisplayName} wintered together as family." : $"{npc.DisplayName} taught {pc.DisplayName} the country.",
                Subject = pc.DisplayName, Day = 1f, Directness = 1, Weight = 1.1f, Key = $"kin{i}{npc.DisplayName}{pc.DisplayName}",
                Witnesses = new List<string> { npc.DisplayName, pc.DisplayName },
            });
    }
}
