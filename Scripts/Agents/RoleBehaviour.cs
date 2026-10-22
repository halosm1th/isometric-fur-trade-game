using Godot;
using IsometricFurTradeGame.Scripts.Agents.Goals;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Ecology;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Agents;

// What an agent of a given role does with an empty goal stack. This is the
// "seasonal playbook" of design doc 3.9 in its simplest form: a few rules
// keyed off the hour, the season, and the state of the household.
public abstract class RoleBehaviour
{
    public static RoleBehaviour For(Role role) => role switch
    {
        Role.Factor or Role.NwcPartner or Role.Postmaster => new FactorBehaviour(),
        Role.Voyageur => new LabourerBehaviour(),
        Role.Clerk => new ClerkBehaviour(),
        Role.Labourer => new LabourerBehaviour(),
        Role.Woman or Role.Wife => new HouseholdBehaviour(),
        Role.CreeChief => new CreeChiefBehaviour(),
        Role.CreeHunter => new CreeHunterBehaviour(),
        Role.CreeWoman or Role.CreeDaughter => new CreeWomanBehaviour(),
        Role.Sailor => new SailorBehaviour(),
        Role.Farmer => new FarmerBehaviour(),
        Role.Merchant or Role.Artisan or Role.Priest or Role.Officer => new TownsmanBehaviour(),
        Role.Soldier => new SoldierBehaviour(),
        Role.Child => new ChildBehaviour(),
        _ => new ClerkBehaviour(),
    };

    protected static GameCalendar Cal => GameCalendar.Instance;
    protected static bool IsNight => Cal.Hour >= 21f || Cal.Hour < 6f;
    // Is anybody in this house going too long without anything fresh? That,
    // not the fisher's own belly, is what the scurvy turns on.
    protected static bool HouseWantsFresh(Economy.Household home, float days = 12f)
    {
        if (home == null) return false;
        foreach (var n in Sim.Npcs) if (!n.IsDead && n.HomeStores == home && n.DaysSinceFresh > days) return true;
        return false;
    }

    protected static bool IsWorkHours => Cal.Hour >= 7f && Cal.Hour < 18f && !FoulWeather;
    // The weather where the man deciding is, not over the player's head.
    private static Vector2 _here;
    protected static bool FoulWeather => Cal.Weather.FoulAt(_here);

    public virtual Goal NextGoal(Npc npc)
    {
        _here = npc.GlobalPosition;
        if (npc.PendingReport != null && !IsNight)
        {
            var g = new ReportGoal(npc.PendingReport, npc.PendingReportSite, npc.PendingReportCell);
            npc.PendingReport = null; npc.PendingReportSite = null; npc.PendingReportCell = null;
            return g;
        }
        if (IsNight) return new SleepGoal();
        var dram = EveningDrink(npc);
        if (dram != null) return dram;
        var fire = EveningAtFire(npc);
        if (fire != null) return fire;
        // Hangover: a man still drunk at first light does no work worth the name.
        if (npc.Intoxication > 35f && IsWorkHours) return new WanderGoal(npc.BedCell, 1, 0.8f);
        if (npc.AssignedTask != null && IsWorkHours && !(npc.AssignedTaskIsHunt && Cal.Hour >= 12f))
        {
            var g = npc.AssignedTask(npc);
            if (g != null) return g;
        }
        // A motion before the town or the council: canvassing for it, or the meeting (Core/Laws).
        if (Core.Laws.Errand(npc) is { } motion) return motion;
        return Work(npc) ?? new WanderGoal(npc.WorkCell, 2, 1f);
    }

    protected abstract Goal Work(Npc npc);

    // Trapping (design doc 16.1): check a ripe line, else set traps at the
    // thickest known lodge.
    public static Goal TraplineWork(Npc npc, Economy.Household home, EcologyManager eco, bool wantFurs)
    {
        if (home == null || eco == null) return null;
        string owner = home.Title;
        SpawnSite ripe = null;
        foreach (var s in eco.Sites)
        {
            var line = s.LineOf(owner);
            if (line != null && line.Traps > 0 && line.ReadyToCheck && (ripe == null || line.Caught > ripe.LineOf(owner).Caught)) ripe = s;
        }
        if (ripe != null) return new CheckTraplineGoal(ripe);
        if (!wantFurs) return null;
        int month = Cal.Month;
        if (month >= 6 && month <= 9) return null;   // summer pelts: not worth the traps (design doc 14.4)
        if (!npc.Inventory.Has(ItemId.SteelTrap))
        {
            int n = home.Stores.TakeUpTo(ItemId.SteelTrap, 4);
            if (n == 0) return null;
            npc.Inventory.Add(ItemId.SteelTrap, n);
        }
        var target = eco.BestSite(Species.Beaver, home);
        return target != null ? new SetTraplineGoal(target) : null;
    }

    // The barrel needs filling (design doc 17.2).
    protected static Goal WaterDuty(Npc npc, int threshold)
    {
        var s = npc.HomeStores;
        if (s == null || s.Stores.Count(ItemId.Water) >= threshold) return null;
        if (!s.Stores.Has(ItemId.Kettle) && !npc.Inventory.Has(ItemId.Kettle)) return null;
        return new FetchWaterGoal();
    }

    // An evening dram if there's drink in the cache and the man's that sort.
    protected static Goal EveningDrink(Npc npc)
    {
        var s = npc.HomeStores;
        if (s == null || npc.IsFemale) return null;
        if (Cal.Hour < 19f || Cal.Hour >= 22f) return null;
        if (!s.Stores.Has(ItemId.Rum) && !s.Stores.Has(ItemId.Ale)) return null;
        if (Core.Laws.Dry(s)) return null;   // the town or the camp has voted it out
        float inclination = 0.3f + npc.Personality.Talkative * 0.4f - npc.Personality.Loyalty * 0.3f;
        return GD.Randf() < inclination * 0.5f ? new CarouseGoal() : null;
    }

    // After the day's work the household drifts to its fire (design doc P.8):
    // the sociable nearly always, the solitary now and then. A camp's fire
    // is in the open, so a blizzard keeps the band in their lodges.
    protected static Goal EveningAtFire(Npc npc)
    {
        if (npc.HomeStores == null || Cal.Hour < 18f || Cal.Hour >= 21f) return null;
        if (npc.Role is Role.Sailor || npc.HeldBy != null || npc.Following != null) return null;
        if (npc.IsBandPerson && FoulWeather) return null;
        if (GD.Randf() > 0.35f + npc.Personality.Talkative * 0.55f) return null;
        return HearthGoal.HomeHearth(npc) != null ? new HearthGoal() : null;
    }

    // Fires need wood; whoever notices first deals with it.
    protected static Goal FireDuty(Npc npc, float threshold)
    {
        var s = npc.HomeStores;
        if (s == null) return null;
        if (s.LowestHearth(threshold) == null) return null;
        if (s.Stores.Has(ItemId.Firewood) || npc.Inventory.Has(ItemId.Firewood)) return new TendFiresGoal();
        return new ChopWoodGoal();
    }
}

public class FactorBehaviour : RoleBehaviour
{
    private int _lastAssignmentDay = -1;

    protected override Goal Work(Npc npc)
    {
        var cal = Cal;
        // Morning assignments (design doc 3.9): once a day, look at the
        // post's situation and hand out the day's tasks.
        if (cal.Hour >= 7f && _lastAssignmentDay != cal.Day)
        {
            _lastAssignmentDay = cal.Day;
            Delegation.AssignDailyTasks(npc);
            return new WaitGoal(0.3f);
        }
        return new WanderGoal(npc.WorkCell, 1, 1.5f);
    }
}

public class ClerkBehaviour : RoleBehaviour
{
    private int _lastWhisperDay = -1;
    protected override Goal Work(Npc npc)
    {
        // Career competition (design doc 3.7): an ambitious clerk who has heard
        // something against a rising new hand makes sure the Factor hears it too.
        if (npc.Personality.Ambition > 0.6f && _lastWhisperDay != Cal.Day && Cal.Hour >= 9f && Cal.Hour < 10f)
        {
            _lastWhisperDay = Cal.Day;
            var leader = Delegation.LeaderOf(npc);
            if (leader != null && leader != npc && GD.Randf() < 0.25f)
            {
                foreach (var pc in Sim.Players())
                {
                    if (pc.IsDead || pc.HomePost != npc.HomeStores || pc.Standing < npc.Standing) continue;
                    MemoryEntry worst = null;
                    foreach (var m in npc.Memories) if (m.Subject == pc.DisplayName && m.Weight >= 0.8f && (worst == null || m.Weight > worst.Weight)) worst = m;
                    if (worst == null) continue;
                    leader.Remember(worst.Retell(npc));
                    pc.Standing -= 2f;
                    Chronicle.Event($"{npc.DisplayName} had a quiet word with {leader.DisplayName} about {pc.DisplayName}.", npc.GlobalPosition, npc, pc.DisplayName, 0.7f);
                    break;
                }
            }
        }
        var fire = FireDuty(npc, 3f);
        if (fire != null) return fire;
        // About his own counter, wherever his post is. This used to be York's
        // depot by name, which sent a clerk at Edmonton House walking two
        // thousand miles to the Bay - the reason the planned posts had to give
        // their clerks a labourer's behaviour instead of a clerk's.
        return new WanderGoal(npc.HomeStores != null ? npc.HomeStores.AccessCell : npc.WorkCell, 1, 1.5f);
    }
}

public class LabourerBehaviour : RoleBehaviour
{
    private int _actingDay = -1;
    protected override Goal Work(Npc npc)
    {
        // A town's labourer - most of them a farmer's or a tradesman's grown son (Agents/TownLife).
        if (npc.HomeStores is World.TownHousehold townHome) return TownLife.Labourer(npc, townHome) ?? new WanderGoal(townHome.AccessCell, 1, 1f);
        // A post with no master (design doc 3.9): the steadiest man gives the orders till one comes.
        if (npc.HomeStores is Economy.FortLedger f && (f.Leader == null || !GodotObject.IsInstanceValid(f.Leader) || f.Leader.IsDead) && Cal.Hour >= 7f && _actingDay != Cal.Day)
        {
            Npc steadiest = null;
            foreach (var n in Sim.Npcs) if (n.HomeStores == f && n.Role == Role.Labourer && !n.IsDead && (steadiest == null || n.Standing > steadiest.Standing)) steadiest = n;
            if (steadiest == npc) { _actingDay = Cal.Day; Delegation.AssignDailyTasks(npc); return new WaitGoal(0.3f); }
        }
        if (!IsWorkHours) return new WanderGoal(npc.BedCell, 2, 1f);
        var fire = FireDuty(npc, 3f);
        if (fire != null) return fire;
        var water = WaterDuty(npc, 6);
        if (water != null) return water;
        // Nothing assigned: keep the woodpile up.
        if (npc.HomeStores != null && npc.HomeStores.Stores.Count(ItemId.Firewood) < 30) return new ChopWoodGoal();
        return new WanderGoal(npc.WorkCell, 3, 1f);
    }
}

public class HouseholdBehaviour : RoleBehaviour
{
    protected override Goal Work(Npc npc)
    {
        var fire = FireDuty(npc, 5f);
        if (fire != null) return fire;
        var water = WaterDuty(npc, 12);
        if (water != null) return water;
        // A town house's day is its own: the market, the quern and the oven,
        // the wash and the garden (Agents/TownLife).
        if (npc.HomeStores is World.TownHousehold townHome) return TownLife.Woman(npc, townHome) ?? new WanderGoal(townHome.AccessCell, 1, 1f);
        // A town house buys its bread at the market (World/Towns); its women
        // go down to the river with a line only when the house is nearly bare.
        // The post's women fish for the mess against a fortnight for ten.
        bool town = npc.HomeStores is World.TownHousehold;
        float shortOf = town ? npc.HomeStores.FoodDays(Mathf.Max(1, npc.HomeStores.Alive())) : npc.HomeStores?.FoodDays(10) ?? 99f;
        if (IsWorkHours && !Cal.IsFrozen && npc.HomeStores != null && shortOf < (town ? 3f : 14f) && GD.Randf() < 0.5f) return new FishGoal();
        if (IsWorkHours && Cal.IsFrozen && npc.HomeStores != null && (HouseWantsFresh(npc.HomeStores) || npc.HomeStores.FoodDays(10) < 20f) && GD.Randf() < 0.6f) return new FishGoal(true);
        // While the flight is on, salting comes before everything else: fresh
        // geese spoil and casks do not (design doc 14.1).
        if (IsWorkHours && npc.HomeStores != null && npc.HomeStores.Stores.Count(ItemId.Goose) >= 8) return new SaltGeeseGoal();
        if (IsWorkHours && npc.HomeStores != null && npc.HomeStores.Stores.Count(ItemId.Venison) >= 3) return new DryMeatGoal();
        if (npc.Needs.Thirst < 60f) return new DrinkGoal();
        return new WanderGoal(npc.WorkCell, 2, 1.2f);
    }
}

public class CreeChiefBehaviour : RoleBehaviour
{
    protected override Goal Work(Npc npc)
    {
        var band = npc.HomeStores as Economy.BandLedger;
        if (band == null) return null;
        var fire = FireDuty(npc, 3f);
        if (fire != null) return fire;
        // The chief carries the furs to the post himself when there's a
        // worthwhile load, or when the camp is running out of food.
        int mouths = band.Mouths;
        bool hungry = band.FoodDays(mouths) < 4f;
        float fursMb = 0f;
        foreach (var (id, n) in band.Stores.All()) if (Items.Items.Of(id).Kind == ItemKind.Fur) fursMb += Items.Items.Of(id).ValueMb * n;
        // Two counters bid for the furs (design doc 7.1): price, how the band
        // feels about each company, and the walk. The Nor'Westers are across
        // the river - only the ford or the ice gets you there.
        Economy.FortLedger best = null; float bestScore = 0f;
        foreach (var post in Sim.Posts())
        {
            if (!band.WillTradeWith(post) || post.Stores.Count(ItemId.TradeGoods) < 5) continue;
            float walk = IsoGrid.ToWorld(post.AccessCell).DistanceTo(IsoGrid.ToWorld(band.AccessCell));
            float score = post.PeltPrice * (0.5f + post.StandingOf(band) / 100f) / (1f + walk / 4000f);
            if (post.Stores.Has(ItemId.Rum)) score *= 1f + band.RumTaste / 200f;
            if (post.DebtOf(band) > 0f) score *= 1.15f;   // you go back where you owe
            if (score > bestScore) { bestScore = score; best = post; }
        }
        // The Assiniboine also come in when they have pemmican to sell (design doc 13.1).
        bool provisions = !band.IsCree && !hungry && band.Stores.Count(ItemId.Pemmican) >= mouths * 6 + 12;
        if (best != null && IsWorkHours && (fursMb >= 8f || provisions || band.InvitedToTrade && fursMb > 0f || (hungry && (fursMb > 0f || band.Stores.Has(ItemId.TradeGoods) || best.StandingOf(band) > 40f))))
            return new TradeTripGoal(hungry, best, provisions);
        return new WanderGoal(band.Camp, 3, 1.5f);
    }
}

public class CreeHunterBehaviour : RoleBehaviour
{
    protected override Goal Work(Npc npc)
    {
        var band = npc.HomeStores as Economy.BandLedger; var eco = Sim.Ecology; var fort = Sim.Fort;
        if (band == null || eco == null) return null;
        if (!IsWorkHours) return new WanderGoal(band.Camp, 3, 1f);
        var fire = FireDuty(npc, 3f);
        if (fire != null) return fire;

        int mouths = band.Mouths;
        bool needMeat = band.FoodDays(mouths) < 6f;

        // How hard the band hunts for trade follows the price the post pays
        // (design doc 13/16.4): this is the London incentive at work.
        float incentive = fort?.PeltPrice ?? 1f;
        bool furHunt = GD.Randf() < Mathf.Clamp(0.25f * incentive, 0.05f, 0.95f);

        // Hunts start in the morning; a long walk out in the afternoon means
        // a night in the open.
        if (Cal.Hour >= 12f) return new WanderGoal(band.Camp, 3, 1f);
        // The trapline comes first: check any line that's had a night, or set
        // one if there are traps in the cache and the price makes it worth it.
        var trapWork = TraplineWork(npc, band, eco, furHunt);
        if (trapWork != null) return trapWork;
        SpawnSite site = needMeat ? eco.BestBigGame(band) ?? eco.BestSite(Species.Goose, band) : null;
        // Laying in stock for the cache when it's under three weeks; otherwise the day goes on other things.
        if (site == null && band.FoodDays(mouths) < 21f && GD.Randf() < 0.3f) site = eco.BestBigGame(band);
        if (site == null && Cal.CurrentSeason != Season.Winter && GD.Randf() < 0.3f) return new FishGoal();
        if (site == null && GD.Randf() < 0.35f) return new ForageGoal(4f);
        if (site != null) return new HuntSiteGoal(site, 5f);
        return new WanderGoal(band.Camp, 3, 1f);
    }
}

public class CreeWomanBehaviour : RoleBehaviour
{
    protected override Goal Work(Npc npc)
    {
        var fire = FireDuty(npc, 5f);
        if (fire != null) return fire;
        var water = WaterDuty(npc, 10);
        if (water != null) return water;
        if (IsWorkHours && !Cal.IsFrozen && npc.HomeStores != null && npc.HomeStores.FoodDays(11) < 8f && GD.Randf() < 0.5f) return new FishGoal();
        if (IsWorkHours && Cal.IsFrozen && npc.HomeStores != null && (HouseWantsFresh(npc.HomeStores) || npc.HomeStores.FoodDays(11) < 14f) && GD.Randf() < 0.6f) return new FishGoal(true);
        if (IsWorkHours && npc.HomeStores != null && npc.HomeStores.Stores.Count(ItemId.Venison) >= 3) return new DryMeatGoal();
        if (IsWorkHours && npc.HomeStores != null && npc.HomeStores.Stores.Count(ItemId.Goose) >= 8) return new SaltGeeseGoal();
        if (IsWorkHours && npc.HomeStores != null && npc.HomeStores.Stores.Count(ItemId.DriedMeat) >= 8 && GD.Randf() < 0.5f) return new MakePemmicanGoal();
        if (npc.HomeStores != null && npc.HomeStores.Stores.Count(ItemId.Firewood) < 12 && IsWorkHours) return new ChopWoodGoal();
        return new WanderGoal((npc.HomeStores as Economy.BandLedger)?.Camp ?? WorldLayout.CampCentre, 3, 1.2f);
    }
}

public class SailorBehaviour : RoleBehaviour
{
    public override Goal NextGoal(Npc npc)
    {
        var ship = Sim.Ship;
        if (ship == null || !ship.IsDocked) return new WanderGoal(WorldLayout.WharfEnd, 1, 1f);
        if (IsNight) return new WanderGoal(WorldLayout.WharfEnd, 1, 2f);
        if (ship.Cargo.SlotCount > 0) return new CarryCargoGoal(true);
        if (Sim.Fort != null && Sim.Fort.FursInStoreMb > 0f && ship.LoadingOpen) return new CarryCargoGoal(false);
        return new WanderGoal(WorldLayout.WharfEnd, 2, 1f);
    }
    protected override Goal Work(Npc npc) => null;
}

// The towns (World/Towns). A farmer of the cote is in his fields from the
// spring ploughing to the harvest, and in the bush for his winter's wood.
public class FarmerBehaviour : RoleBehaviour
{
    protected override Goal Work(Npc npc)
    {
        var home = npc.HomeStores;
        // The year's work on his own fields, step by step (World/Fields, Agents/TownLife).
        if (home is World.TownHousehold farm && TownLife.Farmer(npc, farm) is { } job) return job;
        return new WanderGoal(home?.AccessCell ?? npc.BedCell, 2, 1f);
    }
}

// The merchant at his counter, the smith at his forge, the priest at his
// church, the officer at his orderly room: the day is spent at the work.
public class TownsmanBehaviour : RoleBehaviour
{
    protected override Goal Work(Npc npc)
    {
        var home = npc.HomeStores;
        if (home is World.TownHousehold th && TownLife.Townsman(npc, th) is { } job) return job;
        return new WanderGoal(home?.AccessCell ?? npc.BedCell, 1, 1.5f);
    }
}

// A soldier of the garrison: his turn as sentry or at drill, and the fatigue
// parties that bring in the fort's wood.
public class SoldierBehaviour : RoleBehaviour
{
    protected override Goal Work(Npc npc)
    {
        var home = npc.HomeStores;
        if (!IsWorkHours) return new WanderGoal(home?.AccessCell ?? npc.BedCell, 1, 1f);
        if (home != null && home.Stores.Count(ItemId.Firewood) < 30 && GD.Randf() < 0.35f) return new ChopWoodGoal();
        // A third of the garrison at drill or on sentry in any hour, turn and
        // turn about; the rest about the barracks.
        bool onDuty = World.IsoGrid.Hash(new Vector2I(Cal.Day * 24 + (int)Cal.Hour, npc.NetId), 79) < 0.34f;
        // A sentry's post is outside the palisade; everyone else's is on the square.
        if (home is World.TownHousehold th && th.Town.HasFort)
        {
            var fort = new BuildingDef(BuildingKind.Barracks, "", th.Town.FortTop, th.Town.FortWidth, th.Town.FortDepth, 0);
            if (TownLife.Soldier(npc, th, !fort.Contains(npc.WorkCell), onDuty) is { } job) return job;
        }
        return onDuty ? new WanderGoal(npc.WorkCell, 1, 0.5f) : new WanderGoal(home?.AccessCell ?? npc.BedCell, 3, 1.2f);
    }
}

// A child: about the house and the street, never far.
public class ChildBehaviour : RoleBehaviour
{
    protected override Goal Work(Npc npc) =>
        (npc.HomeStores is World.TownHousehold th ? TownLife.Child(npc, th) : null) ?? new WanderGoal(npc.HomeStores?.AccessCell ?? npc.BedCell, 4, 1.3f);
}
