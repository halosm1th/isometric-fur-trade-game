using System;
using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents.Goals;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Ecology;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Agents;

// One task the post needs done today (design doc 3.9 step 1): a chain, a
// place, a quantity, how many men it's worth, what it takes, and how much
// the leader minds who gets it.
public class PostTask
{
    public string Name;
    public Func<Npc, Goal> Make;
    public int Men;                 // how many to put on it
    public float Urgency;           // bigger first
    public bool Hunt;               // out of the post, back by evening
    public bool NeedsMusket;
    public bool KeyTask;            // a suspicious leader gives these only to men he trusts
    public SpawnSite Site;          // where, if anywhere
    public Player.TaskKind PlayerKind = Player.TaskKind.None;
    public int PlayerNeed = 1;
    public string Intent;           // what the leader says the point is
}

// A man's standing order as the leader remembers giving it, for the
// overdue-report check (design doc 3.9 step 6).
public class Assignment
{
    public Npc Man; public string Task; public int Day; public SpawnSite Site; public bool ReportedBack;
}

// The leader turning his orders into work (design doc 3.9). Generic: a
// Factor with London's directive, a partner with the council's mandate.
// Runs on the morning beat, and again when an event calls for it.
public static class Delegation
{
    private static GameCalendar Cal => GameCalendar.Instance;

    public static Npc LeaderOf(Npc n)
    {
        foreach (var l in Sim.Npcs)
        {
            if (l.IsDead || l.Faction != n.Faction) continue;
            if (n.Faction == Faction.HBC && l.Role == Role.Factor) return l;
            if (n.Faction == Faction.NWC && l.Role == Role.NwcPartner) return l;
            if (n.Faction == Faction.Cree && l.Role == Role.CreeChief) return l;
        }
        return null;
    }

    // ---- step 1: what needs doing --------------------------------------------

    private static List<PostTask> Objectives(Npc leader, FortLedger fort, List<Npc> men)
    {
        var tasks = new List<PostTask>();
        var ship = fort.Company == Faction.HBC ? Sim.Ship : null;
        var eco = Sim.Ecology;
        var fortCentre = IsoGrid.ToWorld(fort.AccessCell);
        int today = Cal.Day;

        // Tier 0 of the post: beasts at the gate preempt everything.
        int threats = 0;
        foreach (var a in Sim.Animals) if (!a.IsDead && a.IsDangerous && a.GlobalPosition.DistanceTo(fortCentre) < 1800f) threats++;
        if (threats > 0) tasks.Add(new PostTask { Name = "drive off the animals", Make = _ => new HuntThreatGoal(), Men = Math.Min(men.Count, threats + 1), Urgency = 100f, NeedsMusket = true, PlayerKind = Player.TaskKind.DriveOffAnimals, Intent = "the beasts about the post" });

        // The ship: everything else waits.
        if (ship != null && ship.IsDocked)
        {
            bool unload = ship.Cargo.SlotCount > 0;
            tasks.Add(new PostTask { Name = unload ? "unload the ship" : "load the furs", Make = _ => new CarryCargoGoal(unload), Men = 99, Urgency = 90f, PlayerKind = unload ? Player.TaskKind.CarryCargo : Player.TaskKind.None, PlayerNeed = 8, Intent = "the ship must sail before the ice" });
            return tasks;
        }

        // The rivalry: a guard after a robbery or with tempers high.
        var rival = Rivalry.PostOf(Rivalry.RivalOf(fort.Company));
        if (rival != null && (fort.RaidedRecently(today) || Rivalry.Tension >= 60f))
        {
            var door = fort.Company == Faction.HBC ? WorldLayout.Warehouse.DoorCell : WorldLayout.NwcHouse.DoorCell;
            tasks.Add(new PostTask { Name = "stand guard at the stores", Make = _ => new GuardGoal(door, 4f), Men = 1, Urgency = 60f, NeedsMusket = true, KeyTask = true, Intent = "the other side has been at our stores" });
        }

        // Provisions (design doc 13.1): days of food, and the scurvy count.
        int mouths = 0, scorbutic = 0;
        foreach (var n in Sim.Npcs) if (n.Faction == fort.Company && !n.IsDead) { mouths++; if (n.DaysSinceFresh > 28f) scorbutic++; }
        float foodDays = fort.FoodDays(mouths + 1);
        bool gooseSeason = (Cal.Month == 9 || Cal.Month == 10 || Cal.Month == 5) && eco?.BestSite(Species.Goose, fort) != null;
        bool foodShort = foodDays < 30f || scorbutic >= 3;
        if (gooseSeason || foodShort)
        {
            var site = eco?.BestSite(gooseSeason ? Species.Goose : Species.Caribou, fort);
            if (site == null) { var lodge = eco?.BestSite(Species.Beaver, fort); if (lodge != null && lodge.Count > lodge.Capacity * 0.6f) site = lodge; }
            float urgency = foodDays < 10f ? 85f : scorbutic >= 3 ? 70f : gooseSeason ? 55f : 45f;
            int want = foodDays < 10f ? 3 : 2;
            if (site != null)
                tasks.Add(new PostTask { Name = $"hunt at {site.Name}", Make = _ => new HuntSiteGoal(site, 6f), Men = site.Species == Species.Beaver ? 1 : want, Urgency = urgency, Hunt = true, NeedsMusket = true, KeyTask = true, Site = site, Intent = foodShort ? $"we have {foodDays:0} days of food" : "the geese are in" });
            else
                tasks.Add(new PostTask { Name = "snare small game", Make = _ => new ForageGoal(5f), Men = want, Urgency = urgency - 5f, Hunt = true, Intent = scorbutic >= 3 ? "the men want fresh meat" : $"we have {foodDays:0} days of food" });
        }

        // Returns (design doc 3.9 step 1): the directive's quota by the
        // ship's deadline, at the pace the calendar leaves us.
        if (fort.Stores.Has(ItemId.SteelTrap) && Cal.CurrentSeason == Season.Winter && eco?.BestSite(Species.Beaver, fort) != null)
        {
            float behind = fort.QuotaMb - fort.FursInStoreMb;
            int daysLeft = Math.Max(1, DaysUntilShip());
            float needPerDay = behind / daysLeft;
            int want = behind <= 0f ? 0 : needPerDay > 0.5f ? 2 : 1;
            if (want > 0) tasks.Add(new PostTask { Name = "work the trapline", Make = n => RoleBehaviour.TraplineWork(n, fort, eco, true) ?? new ChopWoodGoal(), Men = want, Urgency = 40f + Mathf.Min(30f, needPerDay * 30f), KeyTask = true, Hunt = true, Intent = $"{behind:0} MB short of London's {fort.QuotaMb:0}" });
        }

        // Wood: the post burns through it.
        int wood = fort.Stores.Count(ItemId.Firewood);
        tasks.Add(new PostTask { Name = "cut wood", Make = _ => new ChopWoodGoal(), Men = wood < 15 ? 3 : wood < 40 ? 2 : 1, Urgency = wood < 15 ? 65f : 20f, PlayerKind = Player.TaskKind.Firewood, PlayerNeed = 4, Intent = wood < 15 ? "the woodpile is bare" : "the woodpile" });
        return tasks;
    }

    private static int DaysUntilShip()
    {
        var cal = Cal;
        int doy = Mathf.RoundToInt(cal.DayOfYear);
        int ship = 31 + 28 + 31 + 30 + 31 + 30 + 31 + 20;   // 20 August
        return ship >= doy ? ship - doy : 365 - doy + ship;
    }

    // ---- step 3: who gets what -------------------------------------------------

    // How much this leader trusts this man with something that matters.
    public static float Trust(Npc leader, Npc man) => Mathf.Clamp(0.4f + man.Personality.Loyalty * 0.4f + leader.RapportWith(man) / 100f, 0f, 1f);

    private static float Fitness(Npc leader, Npc man, PostTask t)
    {
        float f = 1f;
        if (t.NeedsMusket && man.Inventory.Has(ItemId.Musket)) f += 0.5f;
        if (t.Hunt) f += man.Personality.Courage * 0.3f;
        if (t.Site != null && man.HomeStores != null && man.HomeStores.SiteBelief.ContainsKey(t.Site.Name)) f += 0.3f;   // knows the ground (design doc 16.4)
        if (t.KeyTask) f += Trust(leader, man) * 0.8f;
        if (man.IsSick || man.HasScurvy) f -= 0.6f;
        if (man.Intoxication > 30f) f -= 0.5f;
        if (man.Needs.Health < 60f) f -= 0.4f;
        // Personality of the leader (design doc 3.9 step 3): the suspicious
        // keep key tasks for men they trust; the ambitious keep a rival close.
        if (t.KeyTask && leader.Personality.Suspicion > 0.6f && Trust(leader, man) < 0.55f) f -= 1.5f;
        if (leader.Personality.Ambition > 0.6f && man.Personality.Ambition > 0.7f && t.Hunt) f -= 0.8f;
        return f;
    }

    // ---- step 5: does he do it? ------------------------------------------------

    public enum Compliance { Willing, Grudging, Refused }
    public static Compliance Decide(Npc leader, Npc man, PostTask t)
    {
        float p = 0.55f + man.Personality.Loyalty * 0.4f - man.Personality.Ambition * 0.15f + leader.RapportWith(man) / 200f;
        if (t.Hunt && man.Personality.Ambition > 0.6f) p += 0.15f;        // credit to be had
        if (t.Urgency >= 85f) p += 0.3f;                                  // the ship, or wolves: nobody argues
        if (t.Name == "cut wood" && man.Personality.Ambition > 0.6f) p -= 0.15f;
        if (man.Needs.Hunger < 40f || man.Needs.Warmth < 40f) p -= 0.2f;  // his own needs come first
        if (man.HasScurvy || man.IsSick) p -= 0.2f;
        float r = GD.Randf();
        if (r < p) return Compliance.Willing;
        if (r < p + 0.25f) return Compliance.Grudging;
        return Compliance.Refused;
    }

    // ---- the morning beat --------------------------------------------------------

    public static void AssignDailyTasks(Npc leader)
    {
        var fort = leader.HomeStores as FortLedger;
        if (fort == null) return;
        var men = new List<Npc>();
        foreach (var n in Sim.Npcs) if (n.Faction == fort.Company && (n.Role == Role.Labourer || n.Role == Role.Voyageur) && !n.IsDead) men.Add(n);
        if (men.Count == 0) return;
        int today = Cal.Day;

        CheckOverdue(leader, fort, men);

        // Raids are decided here too (design doc 7.1); the men sent are spoken for.
        var rival = Rivalry.PostOf(Rivalry.RivalOf(fort.Company));
        bool shipIn = fort.Company == Faction.HBC && Sim.Ship != null && Sim.Ship.IsDocked;
        int raidersSent = 0;
        if (rival != null && Rivalry.OpenHostility && men.Count >= 2 && today - fort.LastRaidDay > 4 && !shipIn)
        {
            int mouths = 0; foreach (var n in Sim.Npcs) if (n.Faction == fort.Company && !n.IsDead) mouths++;
            bool worthIt = fort.FoodDays(mouths + 1) < 30f || fort.Stores.Count(ItemId.TradeGoods) < 15 || Rivalry.Tension >= 90f || fort.RaidedRecently(today);
            if (worthIt && GD.Randf() < 0.35f + leader.Personality.Ambition * 0.4f)
                raidersSent = Rivalry.OrderRaid(leader, fort, rival, Math.Min(3, men.Count - 1)).Count;
        }

        var tasks = Objectives(leader, fort, men);
        tasks.Sort((a, b) => b.Urgency.CompareTo(a.Urgency));
        var free = new List<Npc>();
        foreach (var m in men) if (!(m.AssignedTaskName.StartsWith("seize ") && raidersSent > 0) && !m.Brain.Has<LookForGoal>()) free.Add(m);

        fort.Assignments.RemoveAll(a => a.Day < today - 3 || a.Man == null || !GodotObject.IsInstanceValid(a.Man) || a.Man.IsDead);
        var summary = new System.Text.StringBuilder();
        var refused = new List<string>();
        foreach (var t in tasks)
        {
            int slots = Math.Min(t.Men, free.Count);
            for (int k = 0; k < slots; k++)
            {
                if (free.Count == 0) break;
                Npc best = null; float bestF = float.MinValue;
                foreach (var m in free) { float f = Fitness(leader, m, t); if (f > bestF) { bestF = f; best = m; } }
                if (best == null || bestF < -0.5f) break;
                free.Remove(best);
                Give(leader, fort, best, t, summary, refused);
            }
            if (free.Count == 0) break;
        }
        // Anyone left keeps the woodpile up.
        foreach (var m in free) { m.AssignedTask = null; m.AssignedTaskName = "cut wood"; m.AssignedTaskIsHunt = false; summary.Append($"{m.DisplayName}: cut wood; "); }

        Sim.Notice($"{leader.DisplayName} gives the morning's orders. {summary}");
        foreach (var r in refused) Sim.Notice(r);
        if (fort.LostToRivalMb > 0f && fort.Company == Faction.HBC) Sim.Notice($"{leader.DisplayName}: \"{fort.LostToRivalMb:0} Made Beaver walked past us to the Nor'Westers this season. London will want to know why.\"");
        if (fort.LotsLostToRaids > 0 && fort.RaidedRecently(today)) Sim.Notice($"{leader.DisplayName}: \"{fort.LotsLostToRaids} lots gone out of our stores to the other side. That will be answered.\"");

        // The players get theirs the same way - if they're here to hear it.
        foreach (var pc in Sim.Players())
        {
            if (pc.IsDead || pc.Faction != fort.Company) continue;
            if (pc.GlobalPosition.DistanceTo(leader.GlobalPosition) > 900f)
            {
                pc.Standing -= 1f;
                pc.Say($"You were not at the post when the morning's orders were given. {leader.DisplayName} noticed.");
                continue;
            }
            if (pc.Task.Kind == Player.TaskKind.Report && !pc.Task.IsComplete) continue;   // report first
            AssignPlayer(leader, fort, pc, tasks, raidersSent > 0);
        }
    }

    private static void Give(Npc leader, FortLedger fort, Npc m, PostTask t, System.Text.StringBuilder summary, List<string> refused)
    {
        var verdict = Decide(leader, m, t);
        if (verdict == Compliance.Refused)
        {
            m.AssignedTask = null; m.AssignedTaskName = ""; m.AssignedTaskIsHunt = false;
            leader.ChangeRapport(m, -8f);
            m.ChangeRapport(leader, -3f);
            refused.Add($"{m.DisplayName} says he'll not {t.Name} today{(m.Personality.Ambition > 0.6f ? " - it's beneath him" : m.Needs.Hunger < 40f ? " - not on an empty belly" : "")}. {leader.DisplayName} marks it.");
            Chronicle.Event($"{m.DisplayName} refused {leader.DisplayName}'s order to {t.Name}.", m.GlobalPosition, m, weight: 0.8f);
            summary.Append($"{m.DisplayName}: refuses; ");
            return;
        }
        if (t.NeedsMusket && !m.Inventory.Has(ItemId.Musket) && fort.Stores.Remove(ItemId.Musket)) m.Inventory.Add(ItemId.Musket);
        var make = t.Make;
        var siteFor = t.Site;
        m.AssignedTaskIsHunt = t.Hunt;
        m.AssignedTaskName = t.Name;
        if (verdict == Compliance.Grudging)
        {
            // Takes his time about it: an hour's dawdle first.
            m.AssignedTask = n => { n.AssignedTask = make; return new WanderGoal(n.BedCell, 2, 1f); };
            summary.Append($"{m.DisplayName}: {t.Name} (grumbling); ");
        }
        else
        {
            m.AssignedTask = make;
            summary.Append($"{m.DisplayName}: {t.Name}; ");
        }
        fort.Assignments.Add(new Assignment { Man = m, Task = t.Name, Day = Cal.Day, Site = siteFor });
    }

    // One task for the player: intent and a count, with a deadline (design doc 3.9).
    private static void AssignPlayer(Npc leader, FortLedger fort, Player.PlayerController pc, List<PostTask> tasks, bool raid)
    {
        var band = Sim.Band;
        if (raid) { pc.AssignTask(Player.TaskKind.Raid, 4, "the other side's stores"); return; }
        foreach (var t in tasks)
        {
            if (t.PlayerKind == Player.TaskKind.None) continue;
            if (t.PlayerKind == Player.TaskKind.Firewood && band != null && !band.InvitedToTrade && band.WillTrade && fort.FursInStoreMb < fort.QuotaMb && fort.Company == Faction.HBC && GD.Randf() < 0.4f)
            { pc.AssignTask(Player.TaskKind.DeliverLetter, 1, "the band's furs"); WriteLetterToChief(leader, pc); return; }
            pc.AssignTask(t.PlayerKind, t.PlayerNeed, t.Intent);
            return;
        }
        pc.AssignTask(Player.TaskKind.Firewood, 4, "the woodpile");
    }

    // The Factor's letter to the chief is a real object in your pack (design doc 10.1.1).
    public static void WriteLetterToChief(Npc leader, Agent carrier)
    {
        var m = Letters.LetterToChief(leader.DisplayName);
        m.DayWritten = Cal.Day;
        carrier.Mail.Add(m);
    }

    // ---- step 6: reports, and the overdue --------------------------------------

    public static void Receive(Npc leader, Npc man, string report, string site, Vector2I? siteCell)
    {
        leader.Remember(new MemoryEntry { Text = $"{man.DisplayName} reported: {report}.", Subject = man.DisplayName, Actor = man, Day = Cal.DayOfYear, Directness = 2, Weight = 0.6f, Key = $"rep{Cal.Day}{man.NetId}" });
        var fort = leader.HomeStores as FortLedger;
        if (fort != null)
        {
            foreach (var a in fort.Assignments) if (a.Man == man && !a.ReportedBack) a.ReportedBack = true;
            if (site != null && siteCell != null) fort.Believe(site, siteCell.Value);
        }
        leader.ChangeRapport(man, 1f);
        if (GD.Randf() < 0.35f) Sim.Notice($"{man.DisplayName} to {leader.DisplayName}: \"{report}.\"");
    }

    // A man sent out yesterday who hasn't been seen since: send someone to look.
    private static void CheckOverdue(Npc leader, FortLedger fort, List<Npc> men)
    {
        int today = Cal.Day;
        var post = IsoGrid.ToWorld(fort.AccessCell);
        foreach (var a in new List<Assignment>(fort.Assignments))
        {
            if (a.ReportedBack || a.Site == null || a.Day >= today) continue;
            var m = a.Man;
            if (m == null || !GodotObject.IsInstanceValid(m)) continue;
            bool home = !m.IsDead && m.GlobalPosition.DistanceTo(post) < 1200f;
            if (home) { a.ReportedBack = true; continue; }
            // Dead or far: the leader only knows he's not back.
            a.ReportedBack = true;   // one search per assignment
            Npc searcher = null; float best = float.MinValue;
            foreach (var s in men) { if (s == m || s.IsDead) continue; float f = Trust(leader, s) + (s.Inventory.Has(ItemId.Musket) ? 0.3f : 0f); if (f > best) { best = f; searcher = s; } }
            if (searcher == null) continue;
            if (!searcher.Inventory.Has(ItemId.Musket) && fort.Stores.Remove(ItemId.Musket)) searcher.Inventory.Add(ItemId.Musket);
            searcher.AssignedTask = null; searcher.AssignedTaskName = $"look for {m.DisplayName}";
            searcher.Brain.Clear(searcher);
            searcher.Brain.Push(searcher, new LookForGoal(m, fort.BelievedCell(a.Site)));
            Sim.Notice($"{leader.DisplayName}: \"{m.DisplayName} went out to {a.Task} and has not come back. {searcher.DisplayName} - go and look.\"");
            Chronicle.Event($"{m.DisplayName} did not come back from {a.Site.Name}.", leader.GlobalPosition, subject: m.DisplayName, weight: 1f);
        }
    }

    // ---- a new hand (design doc 5.5) ---------------------------------------------

    public static void ReportedForDuty(Npc leader, Npc man)
    {
        var fort = leader.HomeStores as FortLedger;
        if (fort == null) return;
        var tasks = Objectives(leader, fort, new List<Npc> { man });
        tasks.Sort((a, b) => b.Urgency.CompareTo(a.Urgency));
        var t = tasks.Count > 0 ? tasks[0] : null;
        if (t == null || !RoleBehaviourHours()) { man.AssignedTask = null; man.AssignedTaskName = "cut wood"; Sim.Notice($"{leader.DisplayName} looks the new hand over. \"{man.DisplayName}. Find a bed in the men's house; there's wood wants cutting.\""); return; }
        var sb = new System.Text.StringBuilder(); var refused = new List<string>();
        Give(leader, fort, man, t, sb, refused);
        Sim.Notice($"{leader.DisplayName} to the new hand: \"{man.DisplayName}, is it? You'll {t.Name} - {t.Intent}.\"" + (refused.Count > 0 ? " " + refused[0] : ""));
    }

    private static bool RoleBehaviourHours() => Cal.Hour >= 7f && Cal.Hour < 17f;
}
