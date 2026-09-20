using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Ecology;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Agents.Goals;

// Go work a wildlife site (design doc 16.4): walk out, draw the site down
// for a few hours (killing any live animal that's handy), carry the take
// back to the household's cache.
public class HuntSiteGoal : Goal
{
    private readonly SpawnSite _site;
    private readonly float _workHours;
    private int _phase;
    private float _worked;
    private float _tryTimer;
    private int _kills;
    public HuntSiteGoal(SpawnSite site, float workHours = 5f) { _site = site; _workHours = workHours; }
    public override string Label => $"hunt at {_site.Name}";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        switch (_phase)
        {
            case 0:
                _phase = 1;
                return Delegate(new GoToGoal(_site.Cell, _site.RadiusPx * 0.35f));
            case 1:
                if (!ChildFinished(out var r)) return GoalStatus.Running;
                if (r != GoalStatus.Done) return GoalStatus.Failed;
                _phase = 2;
                return GoalStatus.Running;
            case 2:
                _worked += hours;
                _tryTimer -= hours;
                // Weather closing in or the light going: head home with what you have.
                var sky = GameCalendar.Instance.Weather.Current;
                if (sky == SkyState.Blizzard || sky == SkyState.Storm || GameCalendar.Instance.Hour >= 16f || npc.Needs.Thirst < 40f || npc.Needs.Hunger < 30f)
                {
                    _phase = 3;
                    if (npc.HomeStores == null) return GoalStatus.Done;
                    return Delegate(new GoToGoal(npc.HomeStores.AccessCell, 100f));
                }
                // A live animal within reach is fair game.
                var prey = _site.NearestLiveAnimal(npc.GlobalPosition, 500f);
                if (prey != null && !prey.IsDead && npc.Weapon != null)
                {
                    _kills++;   // counted on the way in; the AttackGoal butchers it
                    return Delegate(new AttackGoal(prey));
                }
                if (_tryTimer <= 0f)
                {
                    _tryTimer = 0.75f;
                    // Chance per attempt scales with how thick the game is.
                    float chance = Mathf.Clamp(_site.Count / (float)_site.Capacity, 0.05f, 1f) * 0.35f;
                    if (GD.Randf() < chance && _site.Count > 0)
                    {
                        _site.Take(1);
                        SpeciesInfo.Yield(_site.Species, npc.Inventory);
                        _kills++;
                    }
                    else
                    {
                        // Walk a little within the site while looking.
                        var c = _site.Cell;
                        npc.SetDestination(new Vector2I(c.X + GD.RandRange(-4, 4), c.Y + GD.RandRange(-8, 8)));
                    }
                }
                // One caribou is a load; small game you keep working.
                int enough = _site.Species == Species.Caribou ? 1 : _site.Species == Species.Beaver ? 2 : 4;
                bool loaded = npc.Inventory.TotalWeightLbs > 90f || _kills >= enough;
                if (_worked >= _workHours || loaded || _site.Count <= 0)
                {
                    _phase = 3;
                    if (npc.HomeStores == null) return GoalStatus.Done;
                    return Delegate(new GoToGoal(npc.HomeStores.AccessCell, 100f));
                }
                return GoalStatus.Running;
            default:
                if (!ChildFinished(out var r2)) return GoalStatus.Running;
                DepositTake(npc);
                return GoalStatus.Done;
        }
    }

    private static void DepositTake(Npc npc)
    {
        var store = npc.HomeStores;
        if (store == null) return;
        foreach (var id in new[] { ItemId.Venison, ItemId.Goose, ItemId.DriedMeat, ItemId.BeaverPelt, ItemId.ThinPelt, ItemId.CaribouHide, ItemId.WolfPelt })
        {
            int n = npc.Inventory.Count(id);
            int keep = Items.Items.Of(id).Kind == ItemKind.Food ? 1 : 0;
            if (n > keep) { npc.Inventory.Remove(id, n - keep); store.Stores.Add(id, n - keep); }
        }
    }
}

// Walk out to a lodge and set the traps you carry (design doc 16.1).
public class SetTraplineGoal : Goal
{
    private readonly SpawnSite _site;
    private int _phase;
    public SetTraplineGoal(SpawnSite site) { _site = site; }
    public override string Label => $"set traps at {_site.Name}";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        switch (_phase)
        {
            case 0:
                if (!npc.Inventory.Has(ItemId.SteelTrap)) return GoalStatus.Failed;
                _phase = 1;
                return Delegate(new GoToGoal(_site.Cell, _site.RadiusPx * 0.4f));
            case 1:
                if (!ChildFinished(out var r)) return GoalStatus.Running;
                if (r != GoalStatus.Done) return GoalStatus.Failed;
                _phase = 2;
                return Delegate(new WaitGoal(0.8f));
            default:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                int n = npc.Inventory.TakeUpTo(ItemId.SteelTrap, 6);
                if (n == 0) return GoalStatus.Failed;
                _site.SetTraps(npc.HomeStores?.Title ?? npc.DisplayName, n);
                Chronicle.Event($"{npc.DisplayName} set a trapline at {_site.Name}.", npc.GlobalPosition, npc, weight: 0.5f);
                return GoalStatus.Done;
        }
    }
}

// Go round the line: take the catch (pelts and meat), leave the traps, and
// carry it home. Passing another party's traps is a temptation (16.3).
public class CheckTraplineGoal : Goal
{
    private readonly SpawnSite _site;
    private int _phase;
    public CheckTraplineGoal(SpawnSite site) { _site = site; }
    public override string Label => $"check the traps at {_site.Name}";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        string owner = npc.HomeStores?.Title ?? npc.DisplayName;
        switch (_phase)
        {
            case 0:
                _phase = 1;
                return Delegate(new GoToGoal(_site.Cell, _site.RadiusPx * 0.4f));
            case 1:
                if (!ChildFinished(out var r)) return GoalStatus.Running;
                if (r != GoalStatus.Done) return GoalStatus.Failed;
                _phase = 2;
                return Delegate(new WaitGoal(0.6f));
            case 2:
            {
                if (!ChildFinished(out _)) return GoalStatus.Running;
                var line = _site.LineOf(owner);
                int got = 0;
                if (line != null)
                {
                    got = line.Caught; line.Caught = 0; line.HoursSinceCheck = 0f;
                    if (line.Robbed > 0) { Chronicle.Event($"{npc.DisplayName} found the traps at {_site.Name} robbed.", npc.GlobalPosition, npc, weight: 1.2f); Sim.Notice($"{npc.DisplayName}: someone has been at our traps at {_site.Name}."); line.Robbed = 0; }
                }
                // Others' lines: the less honest take what's in them.
                int poached = TraplineRules.Poach(_site, owner, Mathf.Clamp(0.6f - npc.Personality.Loyalty, 0f, 0.6f));
                if (poached > 0) Chronicle.Event($"{npc.DisplayName} took beaver from another party's traps at {_site.Name}.", npc.GlobalPosition, npc, weight: 1.5f);
                for (int i = 0; i < got + poached; i++) SpeciesInfo.Yield(Species.Beaver, npc.Inventory);
                if (got + poached > 0) Sim.Notice($"{npc.DisplayName} lifted {got + poached} beaver from the traps at {_site.Name}.");
                _phase = 3;
                if (npc.HomeStores == null) return GoalStatus.Done;
                return Delegate(new GoToGoal(npc.HomeStores.AccessCell, 100f));
            }
            default:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                var store = npc.HomeStores;
                if (store != null)
                {
                    foreach (var id in new[] { ItemId.BeaverPelt, ItemId.ThinPelt, ItemId.Venison })
                    {
                        int n = npc.Inventory.Count(id); int keep = id == ItemId.Venison ? 1 : 0;
                        if (n > keep) { npc.Inventory.Remove(id, n - keep); store.Stores.Add(id, n - keep); }
                    }
                }
                return GoalStatus.Done;
        }
    }
}

// Carry the band's furs to the post and trade them; buy food if the camp
// is short. Design doc 3.6: trade is two agents' goals meeting.
public class TradeTripGoal : Goal
{
    private int _phase;
    private readonly bool _buyFood;
    private readonly Economy.FortLedger _post;
    public TradeTripGoal(bool buyFood, Economy.FortLedger post = null) { _buyFood = buyFood; _post = post; }
    public override string Label => (_buyFood ? "trade for provisions at " : "trade furs at ") + (_post?.Title ?? "the post");

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        var band = Sim.Band; var fort = _post ?? Sim.Fort;
        if (band == null || fort == null) return GoalStatus.Failed;
        switch (_phase)
        {
            case 0:
                // Load up at the cache.
                _phase = 1;
                return Delegate(new GoToGoal(band.AccessCell, 100f));
            case 1:
                if (!ChildFinished(out var r)) return GoalStatus.Running;
                if (r != GoalStatus.Done) return GoalStatus.Failed;
                foreach (var id in new[] { ItemId.BeaverPelt, ItemId.ThinPelt, ItemId.CaribouHide, ItemId.WolfPelt })
                    npc.Inventory.Add(id, band.Stores.TakeUpTo(id, 30));
                npc.Inventory.Add(ItemId.TradeGoods, band.Stores.TakeUpTo(ItemId.TradeGoods, _buyFood ? 10 : 0));
                if (npc.Inventory.CountOfKind(ItemKind.Fur) == 0 && !npc.Inventory.Has(ItemId.TradeGoods)) return GoalStatus.Failed;
                _phase = 2;
                return Delegate(new GoToGoal(fort.AccessCell, 120f));
            case 2:
                if (!ChildFinished(out var r2)) return GoalStatus.Running;
                if (r2 != GoalStatus.Done) { _phase = 4; return Delegate(new GoToGoal(band.AccessCell, 100f)); }
                _phase = 3;
                return Delegate(new WaitGoal(0.5f));
            case 3:
            {
                if (!ChildFinished(out _)) return GoalStatus.Running;
                float paid = fort.BuyFurs(npc.Inventory, npc.Inventory);
                int bought = 0;
                if (_buyFood) bought = fort.SellFood(npc.Inventory, npc.Inventory.Count(ItemId.TradeGoods));
                Sim.Notice($"{npc.DisplayName} traded at {fort.Title}: {paid:0} MB in goods{(bought > 0 ? $", bought {bought} of food" : "")}{(fort.BandDebt > 0 ? $" (owes {fort.BandDebt:0.#} MB)" : "")}.");
                Chronicle.Event($"{npc.DisplayName} traded furs at {fort.Title}.", npc.GlobalPosition, npc, weight: 0.4f);
                if (fort is Economy.NwcLedger && Sim.Fort != null) Sim.Fort.LostToRivalMb += paid;
                band.InvitedToTrade = false;
                // A clerk who dealt fairly earns a little standing with the band.
                _phase = 4;
                return Delegate(new GoToGoal(band.AccessCell, 100f));
            }
            default:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                foreach (var (id, n) in new System.Collections.Generic.List<(ItemId, int)>(npc.Inventory.All()))
                {
                    if (id == ItemId.Bow || id == ItemId.Knife || id == ItemId.Tomahawk || id == ItemId.Musket || id == ItemId.HideCoat) continue;
                    if (Items.Items.Of(id).Kind == ItemKind.Food && n <= 1) continue;
                    npc.Inventory.Remove(id, n);
                    band.Stores.Add(id, n);
                }
                return GoalStatus.Done;
        }
    }
}

// Move goods between the ship and the warehouse, one armful at a time.
public class CarryCargoGoal : Goal
{
    private readonly bool _unload;   // true: ship -> warehouse; false: furs warehouse -> ship
    private int _phase;
    public CarryCargoGoal(bool unload) { _unload = unload; }
    public override string Label => _unload ? "unload the ship" : "load furs aboard";

    private static Vector2I ShipCell => WorldLayout.WharfEnd;
    private static Vector2I WarehouseCell => IsoGrid.Cell(WorldLayout.Warehouse.Top, 4, 3);

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        var ship = Sim.Ship; var fort = Sim.Fort;
        if (ship == null || fort == null || !ship.IsDocked) return GoalStatus.Failed;
        var from = _unload ? ShipCell : WarehouseCell;
        var to = _unload ? WarehouseCell : ShipCell;
        switch (_phase)
        {
            case 0:
                _phase = 1;
                return Delegate(new GoToGoal(from, 90f));
            case 1:
            {
                if (!ChildFinished(out var r)) return GoalStatus.Running;
                if (r != GoalStatus.Done) return GoalStatus.Failed;
                int carried = 0;
                if (_unload)
                {
                    foreach (var (id, n) in new System.Collections.Generic.List<(ItemId, int)>(ship.Cargo.All()))
                    {
                        int take = Mathf.Min(n, 4 - carried);
                        if (take <= 0) break;
                        ship.Cargo.Remove(id, take); npc.Inventory.Add(id, take); carried += take;
                    }
                }
                else
                {
                    foreach (var id in new[] { ItemId.BeaverPelt, ItemId.ThinPelt, ItemId.CaribouHide, ItemId.WolfPelt })
                    {
                        int take = fort.Stores.TakeUpTo(id, 6 - carried);
                        npc.Inventory.Add(id, take); carried += take;
                        if (carried >= 6) break;
                    }
                }
                if (carried == 0) return GoalStatus.Done; // nothing left to move
                _phase = 2;
                return Delegate(new GoToGoal(to, 90f));
            }
            default:
            {
                if (!ChildFinished(out var r)) return GoalStatus.Running;
                foreach (var (id, n) in new System.Collections.Generic.List<(ItemId, int)>(npc.Inventory.All()))
                {
                    var kind = Items.Items.Of(id).Kind;
                    if (kind == ItemKind.Weapon || kind == ItemKind.Clothing) continue;
                    if (kind == ItemKind.Food && n <= 2) continue;
                    npc.Inventory.Remove(id, n);
                    if (_unload) fort.Stores.Add(id, n); else ship.Hold.Add(id, n);
                }
                return GoalStatus.Done;
            }
        }
    }
}

// Dry fresh meat at the household's fire (design doc 3.6's pemmican chain,
// first step): 3 fresh -> 2 dried, an hour's work, and it keeps.
public class DryMeatGoal : Goal
{
    private int _phase;
    public override string Label => "dry meat";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        var stores = npc.HomeStores;
        if (stores == null) return GoalStatus.Failed;
        switch (_phase)
        {
            case 0:
                if (stores.Stores.Count(ItemId.Venison) < 3) return GoalStatus.Failed;
                _phase = 1;
                return Delegate(new GoToGoal(stores.AccessCell, 100f));
            case 1:
                if (!ChildFinished(out var r)) return GoalStatus.Running;
                if (r != GoalStatus.Done) return GoalStatus.Failed;
                if (stores.Stores.TakeUpTo(ItemId.Venison, 6) is int took && took >= 3) npc.Inventory.Add(ItemId.Venison, took);
                else return GoalStatus.Failed;
                var hearth = stores.LowestHearth(999f) ?? Sim.Structures?.NearestHearth(npc.GlobalPosition);
                if (hearth == null) return GoalStatus.Failed;
                _phase = 2;
                return Delegate(new GoToGoal(npc.StandCellNear(hearth.Cell), 110f));
            case 2:
                if (!ChildFinished(out var r2)) return GoalStatus.Running;
                _phase = 3;
                return Delegate(new WaitGoal(1.2f));
            default:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                int fresh = npc.Inventory.Count(ItemId.Venison);
                int dried = fresh * 2 / 3;
                npc.Inventory.Remove(ItemId.Venison, fresh);
                npc.Inventory.Add(ItemId.DriedMeat, dried);
                // Back to the cache.
                stores.Stores.Add(ItemId.DriedMeat, npc.Inventory.TakeUpTo(ItemId.DriedMeat, dried));
                return GoalStatus.Done;
        }
    }
}

// Armed men sent out to kill the animals pressing on the post.
public class HuntThreatGoal : Goal
{
    private float _left = 6f;
    public override string Label => "drive off the animals";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        _left -= hours;
        var fortCentre = IsoGrid.ToWorld(IsoGrid.Cell(WorldLayout.FortTop, WorldLayout.FortWidth / 2, WorldLayout.FortDepth / 2));
        Animal target = null; float best = 1900f;
        foreach (var a in Sim.Animals)
        {
            if (a.IsDead || !a.IsDangerous) continue;
            float d = a.GlobalPosition.DistanceTo(fortCentre);
            if (d < best) { best = d; target = a; }
        }
        ChildFinished(out _);
        if (_left <= 0f || target == null) return GoalStatus.Done;
        return Delegate(new AttackGoal(target));
    }
}
