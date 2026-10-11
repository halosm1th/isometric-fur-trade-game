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
    public bool OneShot;            // a journey, not a day's standing work: done once, then back to the roster
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
        if (n.HomeStores?.Leader != null && GodotObject.IsInstanceValid(n.HomeStores.Leader) && !n.HomeStores.Leader.IsDead) return n.HomeStores.Leader;
        foreach (var l in Sim.Npcs)
        {
            if (l.IsDead || l.Faction != n.Faction) continue;
            if (n.Faction == Faction.HBC && l.Role == Role.Factor) return l;
            if (n.Faction == Faction.NWC && l.Role == Role.NwcPartner) return l;
            if (n.IsBandPerson && l.Role == Role.CreeChief && l.HomeStores == n.HomeStores) return l;
        }
        return null;
    }

    // ---- step 1: what needs doing --------------------------------------------

    private static List<PostTask> Objectives(Npc leader, FortLedger fort, List<Npc> men)
    {
        var tasks = new List<PostTask>();
        var ship = fort == Sim.Fort ? Sim.Ship : null;   // only the Bay post has a ship
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
        var rival = fort is OutpostLedger ? null : Rivalry.PostOf(Rivalry.RivalOf(fort.Company));
        if (rival != null && (fort.RaidedRecently(today) || Rivalry.Tension >= 60f))
        {
            var door = fort.Company == Faction.HBC ? WorldLayout.Warehouse.DoorCell : WorldLayout.NwcHouse.DoorCell;
            tasks.Add(new PostTask { Name = "stand guard at the stores", Make = _ => new GuardGoal(door, 4f), Men = 1, Urgency = 60f, NeedsMusket = true, KeyTask = true, Intent = "the other side has been at our stores" });
        }

        // Provisions (design doc 13.1): days of food, and the scurvy count.
        int mouths = 0, scorbutic = 0;
        foreach (var n in Sim.Npcs) if (n.HomeStores == fort && !n.IsDead) { mouths++; if (n.DaysSinceFresh > 18f) scorbutic++; }
        float foodDays = fort.FoodDays(mouths + 1);
        bool gooseSeason = World.GooseHunt.Season && eco?.BestSite(Species.Goose, fort) != null;
        bool foodShort = foodDays < 30f || scorbutic >= 3;
        // The autumn flight: the whole establishment goes to the marsh while
        // it lasts, because what is not salted now is not eaten in February.
        if (World.GooseHunt.Autumn && eco?.BestSite(Species.Goose, fort) is SpawnSite marsh)
        {
            int casks = World.GooseHunt.CasksThisSeason(fort), want = World.GooseHunt.Wanted(fort);
            if (casks < want)
                tasks.Add(new PostTask
                {
                    Name = $"shoot geese at {marsh.Name}",
                    Make = _ => new HuntSiteGoal(marsh, 7f),
                    Men = Math.Max(2, mouths / 3),
                    Urgency = casks < want / 2 ? 95f : 75f,
                    Hunt = true, NeedsMusket = true, KeyTask = true, Site = marsh,
                    PlayerKind = Player.TaskKind.Geese, PlayerNeed = 12,
                    Intent = $"{casks} casks salted of {want}, and {World.GooseHunt.DaysLeft} days of the flight left",
                });
        }
        // Birds coming in faster than the women can put them down: the men
        // leave the marsh and get them into casks, or they rot on the floor.
        if (World.GooseHunt.Season && fort.Stores.Count(ItemId.Goose) >= 40)
            tasks.Add(new PostTask { Name = "salt the geese down", Make = _ => new SaltGeeseGoal(), Men = fort.Stores.Count(ItemId.Goose) >= 120 ? 3 : 1, Urgency = 80f, KeyTask = true, Intent = $"{fort.Stores.Count(ItemId.Goose)} birds on the floor and the flight still on" });
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

        // The inland post's duties (design doc 10.1.2's second leg): once a
        // month a man carries the furs and a report down to the Bay.
        if (fort is OutpostLedger op && Sim.Fort != null && today - op.LastReportSentDay >= 30 && Cal.Hour < 10f && !FoulWeather())
        {
            var opRef = op;
            tasks.Add(new PostTask
            {
                Name = "carry the furs and the report down to York Factory", Men = 1, Urgency = 58f, KeyTask = true, Hunt = true, OneShot = true, Intent = "the Factor must hear how we do",
                Make = n =>
                {
                    opRef.LastReportSentDay = Cal.Day;
                    int fit = 0; foreach (var x in Sim.Npcs) if (x.HomeStores == opRef && !x.IsDead && !x.IsSick) fit++;
                    int mouths2 = 0; foreach (var x in Sim.Npcs) if (x.HomeStores == opRef && !x.IsDead) mouths2++;
                    string news = opRef.LostToRivalMb > 0f ? $"{opRef.LostToRivalMb:0} MB went to the Canadians for want of goods." : "The inland bands are trading.";
                    if (opRef.Leader == null || opRef.Leader.IsDead) news = "The post has no master.";
                    return new CourierGoal(opRef, Sim.Fort, Letters.OutpostReport(leader.DisplayName, opRef.FursInStoreMb, opRef.FoodDays(mouths2), fit, news), opRef.FursInStoreMb > 0f);
                },
            });
        }
        // The Bay post: an inland report overdue is itself news (design doc 3.9 step 6).
        if (fort == Sim.Fort && Sim.Outpost != null)
        {
            // Word has come that Oxford House has no master: the clerk with the
            // best name goes up to take it (design doc 3.7 - the posting is the prize).
            bool masterless = Sim.Outpost.Leader == null || !GodotObject.IsInstanceValid(Sim.Outpost.Leader) || Sim.Outpost.Leader.IsDead;
            if (masterless && fort.KnowsOutpostMasterless && today - fort.LastPostingDay > 20) PostAClerk(leader, fort);
            if (fort.LastOutpostReportDay < -100) fort.LastOutpostReportDay = today;   // as of the start, they've just heard
            int silence = today - fort.LastOutpostReportDay;
            if (silence > 45 && men.Count >= 3)
            {
                var pmaster = Sim.Outpost.Leader;
                var where = Sim.Outpost.AccessCell;
                tasks.Add(new PostTask { Name = "go up to Oxford House and see how they do", Men = 2, Urgency = 62f, KeyTask = true, Hunt = true, NeedsMusket = true, OneShot = true, Intent = $"no word from inland in {silence} days", Make = _ => new LookForGoal(pmaster, where) });
                fort.LastOutpostReportDay = today - 20;   // one party at a time; try again in a few weeks if nothing
            }
        }

        // The Nor'Westers on the river road (design doc 10.3): with tempers up,
        // two men lie in wait a day's march out for the Company's inland packet.
        if (fort is NwcLedger nwcFort && Sim.Outpost != null && (Rivalry.Tension >= 55f || nwcFort.RaidsSanctioned) && men.Count >= 3 && today - nwcFort.LastAmbushDay > 12 && GD.Randf() < 0.3f + leader.Personality.Ambition * 0.3f)
        {
            var spot = IsoGrid.FromWorld(WorldLayout.RiverRoad[4]);
            var nf = nwcFort;
            tasks.Add(new PostTask { Name = "lie up by the river road for the Company's packet", Men = 2, Urgency = 57f, KeyTask = true, Hunt = true, NeedsMusket = true, OneShot = true, Intent = "what the Bay sends inland is worth having", Make = _ => { nf.LastAmbushDay = Cal.Day; return new AmbushGoal(spot, 30f); } });
        }

        // One of ours held across the river (design doc 9.1): a party goes to fetch him.
        if (rival != null && men.Count >= 2)
        {
            Npc held = null;
            foreach (var n in Sim.Npcs) if (n.HomeStores == fort && n.HeldBy == rival && !n.IsDead) { held = n; break; }
            if (held != null)
                tasks.Add(new PostTask { Name = $"fetch {held.DisplayName} back from {rival.Title}", Men = 2, Urgency = 72f, KeyTask = true, NeedsMusket = true, OneShot = true, Intent = "they are holding one of ours", Make = _ => new RescueGoal(held, rival) });
        }

        // Wood: the post burns through it.
        int wood = fort.Stores.Count(ItemId.Firewood);
        tasks.Add(new PostTask { Name = "cut wood", Make = _ => new ChopWoodGoal(), Men = wood < 15 ? 3 : wood < 40 ? 2 : 1, Urgency = wood < 15 ? 65f : 20f, PlayerKind = Player.TaskKind.Firewood, PlayerNeed = 4, Intent = wood < 15 ? "the woodpile is bare" : "the woodpile" });
        return tasks;
    }

    // The posting (design doc 3.7): the clerks compete for it on standing;
    // the loser stays at the Bay and remembers.
    public static void PostAClerk(Npc factor, FortLedger fort)
    {
        var op = Sim.Outpost; if (op == null) return;
        Npc best = null, runnerUp = null;
        foreach (var n in Sim.Npcs)
        {
            if (n.Role != Role.Clerk || n.HomeStores != fort || n.IsDead || n.IsSick) continue;
            float score = n.Standing + Trust(factor, n) * 20f + n.Personality.Ambition * 5f;
            if (best == null || score > best.Standing + Trust(factor, best) * 20f + best.Personality.Ambition * 5f) { runnerUp = best; best = n; } else if (runnerUp == null) runnerUp = n;
        }
        if (best == null) { Sim.Notice($"{factor.DisplayName} has no clerk to send to Oxford House."); fort.LastPostingDay = Cal.Day; return; }
        fort.LastPostingDay = Cal.Day;
        best.Role = Role.Postmaster;
        best.DisplayName = "Mr. " + best.DisplayName.Replace(" (clerk)", "") + " (postmaster)";
        best.HomeStores = op; op.Leader = best;
        best.BedCell = IsoGrid.Cell(WorldLayout.OutpostHouse.Top, 3, 1); best.WorkCell = IsoGrid.Cell(WorldLayout.OutpostHouse.Top, 2, 2);
        best.ResetBehaviour(); best.Brain.Clear(best);
        if (!best.Inventory.Has(ItemId.Musket) && fort.Stores.Remove(ItemId.Musket)) best.Inventory.Add(ItemId.Musket);
        best.Inventory.Add(ItemId.Pemmican, fort.Stores.TakeUpTo(ItemId.Pemmican, 3));
        var orders = new Message { Kind = "orders", From = factor.DisplayName, To = "the postmaster at Oxford House", Company = Faction.HBC, Subject = "the charge of Oxford House", Text = $"{best.DisplayName} is to take charge of Oxford House, hold the post, and send the furs down before the ship. Pay {fort.PeltPrice:0.00}." };
        orders.OnRead = r => { op.PeltPrice = fort.PeltPrice; op.DirectiveText = orders.Text; op.DirectiveReadDay = Cal.Day; };
        best.Mail.Add(orders);
        best.Brain.Push(best, new RoadGoal(op.AccessCell));
        fort.KnowsOutpostMasterless = false;
        Sim.Notice($"{factor.DisplayName} names {best.DisplayName} to the charge of Oxford House and sends him up the river road.");
        // If that was the seat the player was after, he hears who got it and why.
        Rivals.SeatFilled(best, "the charge of Oxford House");
        Chronicle.Event($"{factor.DisplayName} gave {best.DisplayName} the charge of Oxford House.", factor.GlobalPosition, factor, best.DisplayName, 1.5f);
        if (runnerUp != null) { runnerUp.ChangeRapport(best, -15f); runnerUp.ChangeRapport(factor, -5f); Chronicle.Event($"{runnerUp.DisplayName} was passed over for Oxford House.", runnerUp.GlobalPosition, runnerUp, weight: 0.8f); }
        // The players in the running hear how they placed.
        foreach (var pc in Sim.Players()) if (pc.HomePost == fort && !pc.IsDead) pc.Say(pc.Standing > best.Standing ? $"{best.DisplayName} has Oxford House. Your standing was higher; the Factor wanted a clerk who can keep books." : $"{best.DisplayName} has been given Oxford House. Standing {pc.Standing:0} against his {best.Standing:0}.");
    }

    private static bool FoulWeather() => Cal.Weather.Current == SkyState.Blizzard || Cal.Weather.Current == SkyState.Storm;

    // After the ship: the fall outfit goes inland with two men (design doc 3.9 step 2).
    public static void OrderSupply(Npc factor)
    {
        var fort = factor?.HomeStores as FortLedger; var op = Sim.Outpost;
        if (fort == null || op == null || fort != Sim.Fort) return;
        var men = new List<Npc>();
        foreach (var n in Sim.Npcs) if (n.HomeStores == fort && n.Role == Role.Labourer && !n.IsDead && !n.IsSick) men.Add(n);
        if (men.Count < 3) { Sim.Notice($"{factor.DisplayName} has not the men to send the outfit inland this year."); return; }
        men.Sort((a, b) => Trust(factor, b).CompareTo(Trust(factor, a)));
        var cargo = new List<(ItemId, int)> { (ItemId.TradeGoods, 20), (ItemId.Pemmican, 10), (ItemId.Rum, 3), (ItemId.SteelTrap, 4) };
        var cargo2 = new List<(ItemId, int)> { (ItemId.TradeGoods, 15), (ItemId.DriedGoose, 12), (ItemId.Musket, 1), (ItemId.PointBlanket, 4) };
        var letter = new Message { Kind = "orders", From = factor.DisplayName, To = "the postmaster at Oxford House", Company = Faction.HBC, Subject = "the winter's orders", Text = $"Hold the post; pay {fort.PeltPrice:0.00}; send the furs down before the ice goes out. London wants {fort.QuotaMb:0} MB from this district." };
        letter.OnRead = r => { op.PeltPrice = fort.PeltPrice; op.DirectiveText = letter.Text; op.DirectiveReadDay = Cal.Day; Sim.Notice($"{r?.Describe() ?? "The postmaster"} reads the Factor's orders: pay {op.PeltPrice:0.00} inland."); };
        for (int i = 0; i < 2; i++)
        {
            var m = men[i];
            m.AssignedTask = null; m.AssignedTaskName = "carry the outfit up to Oxford House"; m.AssignedTaskIsHunt = false;
            m.Brain.Clear(m);
            m.Brain.Push(m, new CourierGoal(fort, op, i == 0 ? letter : null, false, i == 0 ? cargo : cargo2));
        }
        fort.LastSupplyDay = Cal.Day;
        Sim.Notice($"{factor.DisplayName}: \"{men[0].DisplayName}, {men[1].DisplayName} - the outfit for Oxford House. Take the river road and mind the packet.\"");
        Chronicle.Event($"{factor.DisplayName} sent the fall outfit inland with {men[0].DisplayName} and {men[1].DisplayName}.", factor.GlobalPosition, factor, weight: 0.8f);
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
        foreach (var n in Sim.Npcs) if (n.HomeStores == fort && (n.Role == Role.Labourer || n.Role == Role.Voyageur) && !n.IsDead && n != leader && n.HeldBy == null && n.Tier == Core.Tier.Live) men.Add(n);
        if (men.Count == 0) return;
        int today = Cal.Day;

        CheckOverdue(leader, fort, men);

        // Raids are decided here too (design doc 7.1); the men sent are spoken for.
        var rival = fort is OutpostLedger ? null : Rivalry.PostOf(Rivalry.RivalOf(fort.Company));
        bool shipIn = fort == Sim.Fort && Sim.Ship != null && Sim.Ship.IsDocked;
        int raidersSent = 0;
        bool sanctioned = fort is NwcLedger nl && nl.RaidsSanctioned && Rivalry.Tension >= 45f;
        if (rival != null && (Rivalry.OpenHostility || sanctioned) && men.Count >= 2 && today - fort.LastRaidDay > 4 && !shipIn)
        {
            int mouths = 0; foreach (var n in Sim.Npcs) if (n.HomeStores == fort && !n.IsDead) mouths++;
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
            if (pc.IsDead || pc.HomePost != fort) continue;
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
            m.Standing = Mathf.Max(0f, m.Standing - 3f);
            refused.Add($"{m.DisplayName} says he'll not {t.Name} today{(m.Personality.Ambition > 0.6f ? " - it's beneath him" : m.Needs.Hunger < 40f ? " - not on an empty belly" : "")}. {leader.DisplayName} marks it.");
            Chronicle.Event($"{m.DisplayName} refused {leader.DisplayName}'s order to {t.Name}.", m.GlobalPosition, m, weight: 0.8f);
            summary.Append($"{m.DisplayName}: refuses; ");
            return;
        }
        if (t.NeedsMusket && !m.Inventory.Has(ItemId.Musket) && fort.Stores.Remove(ItemId.Musket)) m.Inventory.Add(ItemId.Musket);
        var make = t.Make;
        if (t.OneShot) { var once = make; make = n => { n.AssignedTask = null; n.AssignedTaskName = ""; return once(n); }; }
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
            { pc.AssignTask(Player.TaskKind.DeliverLetter, 2, "the band's furs"); WriteLetterToChief(leader, pc); return; }
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
        man.Standing = Mathf.Min(100f, man.Standing + (report.StartsWith("took") || report.StartsWith("snared") || report.StartsWith("back from") ? 1.5f : 0.5f));
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
