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
        Role.Factor or Role.NwcPartner => new FactorBehaviour(),
        Role.Voyageur => new LabourerBehaviour(),
        Role.Clerk => new ClerkBehaviour(),
        Role.Labourer => new LabourerBehaviour(),
        Role.Woman or Role.Wife => new HouseholdBehaviour(),
        Role.CreeChief => new CreeChiefBehaviour(),
        Role.CreeHunter => new CreeHunterBehaviour(),
        Role.CreeWoman or Role.CreeDaughter => new CreeWomanBehaviour(),
        Role.Sailor => new SailorBehaviour(),
        _ => new ClerkBehaviour(),
    };

    protected static GameCalendar Cal => GameCalendar.Instance;
    protected static bool IsNight => Cal.Hour >= 21f || Cal.Hour < 6f;
    protected static bool IsWorkHours => Cal.Hour >= 7f && Cal.Hour < 18f && !FoulWeather;
    protected static bool FoulWeather => Cal.Weather.Current == SkyState.Blizzard || Cal.Weather.Current == SkyState.Storm;

    public virtual Goal NextGoal(Npc npc)
    {
        if (npc.PendingReport != null && !IsNight)
        {
            var g = new ReportGoal(npc.PendingReport, npc.PendingReportSite, npc.PendingReportCell);
            npc.PendingReport = null; npc.PendingReportSite = null; npc.PendingReportCell = null;
            return g;
        }
        if (IsNight) return new SleepGoal();
        var dram = EveningDrink(npc);
        if (dram != null) return dram;
        // Hangover: a man still drunk at first light does no work worth the name.
        if (npc.Intoxication > 35f && IsWorkHours) return new WanderGoal(npc.BedCell, 1, 0.8f);
        if (npc.AssignedTask != null && IsWorkHours && !(npc.AssignedTaskIsHunt && Cal.Hour >= 12f))
        {
            var g = npc.AssignedTask(npc);
            if (g != null) return g;
        }
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
        float inclination = 0.3f + npc.Personality.Talkative * 0.4f - npc.Personality.Loyalty * 0.3f;
        return GD.Randf() < inclination * 0.5f ? new CarouseGoal() : null;
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
    protected override Goal Work(Npc npc)
    {
        var fire = FireDuty(npc, 3f);
        if (fire != null) return fire;
        return new WanderGoal(WorldLayout.Depot.Centre, 1, 1.5f);
    }
}

public class LabourerBehaviour : RoleBehaviour
{
    protected override Goal Work(Npc npc)
    {
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
        if (IsWorkHours && !Cal.IsFrozen && npc.HomeStores != null && npc.HomeStores.FoodDays(10) < 14f && GD.Randf() < 0.5f) return new FishGoal();
        if (IsWorkHours && npc.HomeStores != null && npc.HomeStores.Stores.Count(ItemId.Venison) >= 3) return new DryMeatGoal();
        if (npc.Needs.Thirst < 60f) return new DrinkGoal();
        return new WanderGoal(npc.WorkCell, 2, 1.2f);
    }
}

public class CreeChiefBehaviour : RoleBehaviour
{
    protected override Goal Work(Npc npc)
    {
        var band = Sim.Band;
        if (band == null) return null;
        var fire = FireDuty(npc, 3f);
        if (fire != null) return fire;
        // The chief carries the furs to the post himself when there's a
        // worthwhile load, or when the camp is running out of food.
        int mouths = 0;
        foreach (var n in Sim.Npcs) if (n.Faction == Faction.Cree && !n.IsDead) mouths++;
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
            float score = post.PeltPrice * (0.5f + post.BandStanding / 100f) / (1f + walk / 4000f);
            if (post.Stores.Has(ItemId.Rum)) score *= 1f + band.RumTaste / 200f;
            if (post.BandDebt > 0f) score *= 1.15f;   // you go back where you owe
            if (score > bestScore) { bestScore = score; best = post; }
        }
        if (best != null && IsWorkHours && (fursMb >= 8f || band.InvitedToTrade && fursMb > 0f || (hungry && (fursMb > 0f || band.Stores.Has(ItemId.TradeGoods) || best.BandStanding > 40f))))
            return new TradeTripGoal(hungry, best);
        return new WanderGoal(WorldLayout.CampCentre, 3, 1.5f);
    }
}

public class CreeHunterBehaviour : RoleBehaviour
{
    protected override Goal Work(Npc npc)
    {
        var band = Sim.Band; var eco = Sim.Ecology; var fort = Sim.Fort;
        if (band == null || eco == null) return null;
        if (!IsWorkHours) return new WanderGoal(WorldLayout.CampCentre, 3, 1f);
        var fire = FireDuty(npc, 3f);
        if (fire != null) return fire;

        int mouths = 0;
        foreach (var n in Sim.Npcs) if (n.Faction == Faction.Cree && !n.IsDead) mouths++;
        bool needMeat = band.FoodDays(mouths) < 6f;

        // How hard the band hunts for trade follows the price the post pays
        // (design doc 13/16.4): this is the London incentive at work.
        float incentive = fort?.PeltPrice ?? 1f;
        bool furHunt = GD.Randf() < Mathf.Clamp(0.25f * incentive, 0.05f, 0.95f);

        // Hunts start in the morning; a long walk out in the afternoon means
        // a night in the open.
        if (Cal.Hour >= 12f) return new WanderGoal(WorldLayout.CampCentre, 3, 1f);
        // The trapline comes first: check any line that's had a night, or set
        // one if there are traps in the cache and the price makes it worth it.
        var trapWork = TraplineWork(npc, band, eco, furHunt);
        if (trapWork != null) return trapWork;
        SpawnSite site = needMeat ? eco.BestSite(Species.Caribou, band) ?? eco.BestSite(Species.Goose, band) : null;
        // Laying in stock for the cache when it's under three weeks; otherwise the day goes on other things.
        if (site == null && band.FoodDays(mouths) < 21f && GD.Randf() < 0.3f) site = eco.BestSite(Species.Caribou, band);
        if (site == null && Cal.CurrentSeason != Season.Winter && GD.Randf() < 0.3f) return new FishGoal();
        if (site == null && GD.Randf() < 0.35f) return new ForageGoal(4f);
        if (site != null) return new HuntSiteGoal(site, 5f);
        return new WanderGoal(WorldLayout.CampCentre, 3, 1f);
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
        if (IsWorkHours && npc.HomeStores != null && npc.HomeStores.Stores.Count(ItemId.Venison) >= 3) return new DryMeatGoal();
        if (npc.HomeStores != null && npc.HomeStores.Stores.Count(ItemId.Firewood) < 12 && IsWorkHours) return new ChopWoodGoal();
        return new WanderGoal(WorldLayout.CampCentre, 3, 1.2f);
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
