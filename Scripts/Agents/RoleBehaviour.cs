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
        Role.Factor => new FactorBehaviour(),
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
        if (IsNight) return new SleepGoal();
        if (npc.AssignedTask != null && IsWorkHours && !(npc.AssignedTaskIsHunt && Cal.Hour >= 12f))
        {
            var g = npc.AssignedTask(npc);
            if (g != null) return g;
        }
        return Work(npc) ?? new WanderGoal(npc.WorkCell, 2, 1f);
    }

    protected abstract Goal Work(Npc npc);

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
        return new WanderGoal(WorldLayout.Depot.Centre, 1, 1.5f);
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
        if (!IsWorkHours) return new WanderGoal(WorldLayout.MensHouse.Centre, 2, 1f);
        var fire = FireDuty(npc, 3f);
        if (fire != null) return fire;
        // Nothing assigned: keep the woodpile up.
        if (npc.HomeStores != null && npc.HomeStores.Stores.Count(ItemId.Firewood) < 30) return new ChopWoodGoal();
        return new WanderGoal(IsoGrid.Cell(WorldLayout.FortTop, 10, 8), 3, 1f);
    }
}

public class HouseholdBehaviour : RoleBehaviour
{
    protected override Goal Work(Npc npc)
    {
        var fire = FireDuty(npc, 5f);
        if (fire != null) return fire;
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
        if (band.WillTrade && IsWorkHours && (fursMb >= 8f || (hungry && (fursMb > 0f || band.Stores.Has(ItemId.TradeGoods)))))
            return new TradeTripGoal(hungry);
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
        SpawnSite site = needMeat ? eco.BestSite(Species.Caribou, band) ?? eco.BestSite(Species.Goose, band) : null;
        if (site == null && furHunt) site = eco.BestSite(Species.Beaver, band);
        if (site == null && GD.Randf() < 0.3f) site = eco.BestSite(Species.Caribou, band);
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

// The Factor turning orders into work (design doc 3.9).
public static class Delegation
{
    public static void AssignDailyTasks(Npc factor)
    {
        var fort = Sim.Fort; var ship = Sim.Ship;
        if (fort == null) return;
        var men = new System.Collections.Generic.List<Npc>();
        foreach (var n in Sim.Npcs) if (n.Faction == Faction.HBC && n.Role == Role.Labourer && !n.IsDead) men.Add(n);
        if (men.Count == 0) return;

        var fortCentre = IsoGrid.ToWorld(IsoGrid.Cell(WorldLayout.FortTop, WorldLayout.FortWidth / 2, WorldLayout.FortDepth / 2));
        int threats = 0;
        foreach (var a in Sim.Animals) if (!a.IsDead && a.IsDangerous && a.GlobalPosition.DistanceTo(fortCentre) < 1800f) threats++;

        bool shipIn = ship != null && ship.IsDocked;
        bool gooseSeason = (Cal().Month == 9 || Cal().Month == 10 || Cal().Month == 5) && Sim.Ecology?.BestSite(Species.Goose, fort) != null;
        int mouths = 0;
        foreach (var n in Sim.Npcs) if (n.Faction == Faction.HBC && !n.IsDead) mouths++;
        bool foodShort = fort.FoodDays(mouths + 1) < 30f;

        string summary = "";
        for (int i = 0; i < men.Count; i++)
        {
            var m = men[i];
            m.AssignedTaskIsHunt = false;
            if (threats > 0 && i < Mathf.Min(men.Count, threats + 1))
            {
                if (!m.Inventory.Has(ItemId.Musket) && fort.Stores.Remove(ItemId.Musket)) m.Inventory.Add(ItemId.Musket);
                m.AssignedTask = _ => new HuntThreatGoal(); m.AssignedTaskName = "drive off the animals";
            }
            else if (shipIn)
            {
                bool unload = ship.Cargo.SlotCount > 0;
                m.AssignedTask = _ => new CarryCargoGoal(unload); m.AssignedTaskName = unload ? "unload the ship" : "load the furs";
            }
            else if ((gooseSeason || foodShort) && i % 2 == 0)
            {
                var site = Sim.Ecology?.BestSite(gooseSeason ? Species.Goose : Species.Caribou, fort) ?? Sim.Ecology?.BestSite(Species.Beaver, fort);
                if (site != null)
                {
                    if (!m.Inventory.Has(ItemId.Musket) && fort.Stores.Remove(ItemId.Musket)) m.Inventory.Add(ItemId.Musket);
                    m.AssignedTask = _ => new HuntSiteGoal(site, 6f); m.AssignedTaskName = $"hunt at {site.Name}"; m.AssignedTaskIsHunt = true;
                }
                else { m.AssignedTask = null; m.AssignedTaskName = "cut wood"; }
            }
            else
            {
                m.AssignedTask = null; m.AssignedTaskName = "cut wood";
            }
            summary += $"{m.DisplayName}: {m.AssignedTaskName}; ";
        }
        Sim.Notice($"{factor.DisplayName} gives the morning's orders. {summary}");

        // The new hand gets his orders like everyone else - if he's at the
        // post to hear them (design doc 10: nothing travels by telepathy).
        if (Sim.Player is Player.PlayerController pc && !pc.IsDead)
        {
            if (pc.GlobalPosition.DistanceTo(factor.GlobalPosition) > 900f)
            {
                pc.Standing -= 1f;
                Sim.Notice("You were not at the post when the morning's orders were given. Mr. Auld noticed.");
                return;
            }
            if (threats > 0) pc.AssignTask(Player.TaskKind.DriveOffAnimals, 1);
            else if (shipIn && ship.Cargo.SlotCount > 0) pc.AssignTask(Player.TaskKind.CarryCargo, 8);
            else pc.AssignTask(Player.TaskKind.Firewood, 4);
        }
    }

    private static GameCalendar Cal() => GameCalendar.Instance;
}
