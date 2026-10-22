using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents.Goals;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;

namespace IsometricFurTradeGame.Scripts.Agents;

// An unanswered wrong (design doc 7.4): not a number on a reputation
// sheet but a debt owed to particular people, who pursue it. Settled with
// goods to the kin, or in blood, or passed down when the holder dies.
public class Grudge
{
    public string Holder;      // who carries it
    public string Against;     // who owes
    public string Victim;
    public int Day;
    public float DebtMb;       // what would make it right
    public bool Settled;
    public bool Blood;         // answered in kind
    public string Summary => $"{Holder} holds {Against} to account for {Victim} ({(Settled ? (Blood ? "answered in blood" : "settled") : $"{DebtMb:0} MB owed")})";
}

public static class Grudges
{
    public static readonly List<Grudge> All = new();

    public static IEnumerable<Grudge> HeldBy(string holder) { foreach (var g in All) if (g.Holder == holder && !g.Settled) yield return g; }
    public static IEnumerable<Grudge> Against(string who) { foreach (var g in All) if (g.Against == who && !g.Settled) yield return g; }
    public static bool HasOpen(string holder, string against) { foreach (var g in All) if (g.Holder == holder && g.Against == against && !g.Settled) return true; return false; }

    // Someone has been killed. Their kin - spouse, father, children, and
    // the band's leading man - take up the debt. Company men's deaths are
    // the house's business (the rivalry), not a family's.
    public static void OnKilled(Npc victim, Agent killer)
    {
        if (victim == null || killer == null || killer == victim) return;
        if (killer is Animal) return;
        if (!victim.IsBandPerson) return;
        float debt = victim.Role == Role.Chief ? 24f : victim.IsFemale ? 14f : 16f;
        var kin = new List<Npc>();
        if (victim.Spouse is Npc sp && !sp.IsDead) kin.Add(sp);
        if (victim.Father != null && !victim.Father.IsDead) kin.Add(victim.Father);
        foreach (var c in victim.Children) if (!c.IsDead) kin.Add(c);
        // The band is one family for this: the leading man and the hunters all carry it.
        foreach (var n in Sim.Npcs) if ((n.Role == Role.Chief || n.Role == Role.Hunter) && n.HomeStores == victim.HomeStores && !n.IsDead && !kin.Contains(n) && n != victim) kin.Add(n);
        int day = GameCalendar.Instance?.Day ?? 0;
        foreach (var k in kin)
        {
            if (HasOpen(k.DisplayName, killer.DisplayName)) continue;
            All.Add(new Grudge { Holder = k.DisplayName, Against = killer.DisplayName, Victim = victim.DisplayName, Day = day, DebtMb = debt });
            k.ChangeRapport(killer, -60f);
        }
        if (kin.Count > 0)
        {
            Sim.Notice($"{victim.DisplayName}'s people hold {killer.Describe()} to account. It will be made right with goods, or with blood.");
            Chronicle.Event($"{killer.Describe()} killed {victim.DisplayName}; the family will want it answered.", victim.GlobalPosition, killer, victim.DisplayName, 3f);
        }
    }

    // The holder died: the debt passes to the next of the victim's kin who is still alive.
    public static void OnHolderDied(Npc holder)
    {
        foreach (var g in new List<Grudge>(All))
        {
            if (g.Holder != holder.DisplayName || g.Settled) continue;
            Npc heir = null;
            foreach (var n in Sim.Npcs) if (!n.IsDead && n.HomeStores == holder.HomeStores && n != holder && (n.Role == Role.Chief || n.Role == Role.Hunter)) { heir = n; break; }
            if (heir == null) { g.Settled = true; continue; }
            g.Holder = heir.DisplayName;
            Sim.Notice($"The debt for {g.Victim} passes to {heir.DisplayName}.");
        }
    }

    public static void Settle(Grudge g, bool blood)
    {
        g.Settled = true; g.Blood = blood;
        // Settling one kinsman's claim settles the family's.
        foreach (var o in All) if (o.Victim == g.Victim && o.Against == g.Against) { o.Settled = true; o.Blood = blood; }
    }

    // What the holder does about it when the debtor is in front of him.
    public static Goal Pursue(Npc holder, Agent debtor)
    {
        if (!holder.IsFighter || holder.Personality.Courage <= 0.45f || holder.Needs.Health <= 50f) return null;
        // A man who means to settle a death will settle it; whether today is
        // the day is the director's to nudge (design doc 2.2, 7.4). Saying no
        // only means not this afternoon.
        if (debtor is Player.PlayerController && !Core.Director.MayActNow("blood", 1.5f, holder.GlobalPosition)) return null;
        if (debtor is Player.PlayerController) Core.Director.Saw("blood", 1.5f);
        return new AttackGoal(debtor);
    }
}
