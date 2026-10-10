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
    private float _searched;
    private Vector2I _believed;
    public HuntSiteGoal(SpawnSite site, float workHours = 5f) { _site = site; _workHours = workHours; }
    public override string Label => _phase == 5 ? $"look for {_site.Name}" : $"hunt at {_site.Name}";

    private bool HerdHere(Npc npc) => _site.WorldPos.DistanceTo(npc.GlobalPosition) < _site.RadiusPx * 1.3f;

    // A hunter after furs leaves the musket slung: a ball ruins the pelt.
    private ItemId? _weaponBefore;
    public override void OnEnter(Npc npc)
    {
        _weaponBefore = npc.PreferredWeapon;
        if ((_site.Species == Species.Beaver || _site.Species == Species.Wolf) && npc.Inventory.Has(ItemId.Bow)) npc.PreferredWeapon = ItemId.Bow;
    }
    public override void OnExit(Npc npc) { npc.PreferredWeapon = _weaponBefore; npc.StopMoving(); }

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        switch (_phase)
        {
            case 0:
                _phase = 1;
                // You walk to where you believe the animals are (design doc 16.5).
                _believed = npc.HomeStores?.BelievedCell(_site) ?? _site.Cell;
                return Delegate(new GoToGoal(_believed, _site.RadiusPx * 0.35f));
            case 1:
                if (!ChildFinished(out var r)) return GoalStatus.Running;
                if (r != GoalStatus.Done) return GoalStatus.Failed;
                if (_site.IsMobile && !HerdHere(npc))
                {
                    // Nothing here but tracks. Cast about for the rest of the morning.
                    Chronicle.Event($"{npc.DisplayName} found the ground at {_site.Name}'s old place empty; the herd had moved on.", npc.GlobalPosition, npc, _site.Name, 0.6f);
                    _phase = 5;
                    return GoalStatus.Running;
                }
                _phase = 2;
                return GoalStatus.Running;
            case 5:
            {
                _searched += hours;
                if (HerdHere(npc))
                {
                    npc.HomeStores?.Believe(_site.Name, _site.Cell);
                    Chronicle.Event($"{npc.DisplayName} found the caribou again, {Direction(_believed, _site.Cell)} of the old ground.", npc.GlobalPosition, npc, _site.Name, 1f);
                    _phase = 2;
                    return GoalStatus.Running;
                }
                if (_searched > 4f || GameCalendar.Instance.Hour >= 15f || npc.Needs.Thirst < 40f || npc.Needs.Hunger < 30f)
                {
                    _phase = 3;
                    if (npc.HomeStores != null) npc.HomeStores.SiteLostDay[_site.Name] = GameCalendar.Instance.Day;
                    Sim.Notice($"{npc.DisplayName} comes home empty-handed: {_site.Name} has moved and nobody knows where.");
                    if (npc.HomeStores == null) return GoalStatus.Done;
                    return Delegate(new GoToGoal(npc.HomeStores.AccessCell, 100f));
                }
                if (npc.Arrived || npc.MoveFailed)
                {
                    // Herds follow the seasons; a hunter guesses along the same lines.
                    int month = GameCalendar.Instance.Month;
                    var c = npc.Cell;
                    Vector2I guess;
                    if (npc.HomeStores != null && npc.HomeStores.KnowsTheCountry && _site.Route != null)
                    {
                        // Those who know the country know where the caribou go
                        // in this month; they head that way, casting about.
                        var ground = _site.Route[Mathf.Clamp(month - 1, 0, _site.Route.Length - 1)];
                        guess = new Vector2I((c.X + ground.X) / 2 + GD.RandRange(-4, 4), (c.Y + ground.Y) / 2 + GD.RandRange(-6, 6));
                    }
                    else
                    {
                        int bias = month >= 4 && month <= 8 ? -1 : 1;
                        guess = new Vector2I(c.X + GD.RandRange(-5, 5), c.Y + GD.RandRange(0, 14) * bias + GD.RandRange(-4, 4));
                    }
                    npc.SetDestination(new Vector2I(Mathf.Clamp(guess.X, 2, WorldLayout.MapWidth - 3), Mathf.Clamp(guess.Y, 50, WorldLayout.MapHeight - 4)));
                }
                return GoalStatus.Running;
            }
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
                // Another nation's hunters on the same ground (design doc 7.2, 16.5): words, and the standing between the bands slips.
                if (npc.IsBandPerson && npc.HomeStores is Economy.BandLedger myBand && _tryTimer <= 0f)
                {
                    foreach (var a in Sim.AgentsWithin(npc.GlobalPosition, 500f))
                    {
                        if (a is not Npc o || !o.IsBandPerson || o.Faction == npc.Faction || o.IsDead) continue;
                        if (GD.Randf() < 0.04f)
                        {
                            myBand.StandingWithOtherBand = Mathf.Max(0f, myBand.StandingWithOtherBand - 2f);
                            if (myBand.OtherBand != null) myBand.OtherBand.StandingWithOtherBand = Mathf.Max(0f, myBand.OtherBand.StandingWithOtherBand - 2f);
                            Chronicle.Event($"there were hard words between {npc.DisplayName} and {o.DisplayName} over the game at {_site.Name}.", npc.GlobalPosition, npc, o.DisplayName, 1f);
                            if (myBand.AtOddsWithOtherBand) Sim.Notice($"The Cree and the Assiniboine are at odds over {_site.Name}. Their hunters will not pass each other quietly now.");
                        }
                        break;
                    }
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
                    string party = npc.HomeStores?.Title ?? npc.DisplayName;
                    float crowd = _site.CrowdFactor(party, GameCalendar.Instance.Day);
                    _site.ClaimForToday(party, GameCalendar.Instance.Day);
                    // Chance per attempt scales with how thick the game is.
                    float chance = Mathf.Clamp(_site.Count / (float)_site.Capacity, 0.05f, 1f) * 0.22f * (0.6f + npc.Skills.Hunting * 0.8f);
                    npc.Skills.Learn(ref npc.Skills.Hunting, 0.004f);
                    if (GD.Randf() < chance * crowd && _site.Count > 0)
                    {
                        _site.Take(SpeciesInfo.PerKill(_site.Species, npc.Weapon == ItemId.Musket));
                        SpeciesInfo.Yield(_site.Species, npc.Inventory, npc.Weapon == ItemId.Musket);
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
                npc.PendingReport = _kills > 0 ? $"took {_kills} at {_site.Name}, the ground {(_site.Count > _site.Capacity * 0.5f ? "thick with game" : "thin")}" : $"nothing to be had at {_site.Name}";
                npc.PendingReportSite = _site.Name; npc.PendingReportCell = _site.Cell;
                return GoalStatus.Done;
        }
    }

    private static string Direction(Vector2I from, Vector2I to)
    {
        var d = IsoGrid.ToWorld(to) - IsoGrid.ToWorld(from);
        return Mathf.Abs(d.Y) > Mathf.Abs(d.X) ? (d.Y < 0 ? "north" : "south") : (d.X < 0 ? "west" : "east");
    }

    private static void DepositTake(Npc npc)
    {
        var store = npc.HomeStores;
        if (store == null) return;
        foreach (var id in new[] { ItemId.Venison, ItemId.Goose, ItemId.DriedMeat, ItemId.Hare, ItemId.BeaverPelt, ItemId.ThinPelt, ItemId.ShotPelt, ItemId.ShotHide, ItemId.CaribouHide, ItemId.WolfPelt })
        {
            int n = npc.Inventory.Count(id);
            int keep = Items.Items.Of(id).Kind == ItemKind.Food ? 1 : 0;
            if (n > keep) { npc.Inventory.Remove(id, n - keep); store.Stores.Add(id, n - keep); }
        }
    }
}

// Cross to the other company's post and carry off its stores (design doc
// 7.1, 15.1). Whoever meets you at the door decides whether it's a theft or
// a fight; the party fights only the armed men of the house.
public class RaidGoal : Goal
{
    private readonly Economy.FortLedger _target;
    private int _phase;
    public RaidGoal(Economy.FortLedger target) { _target = target; }
    public override string Label => $"seize {_target?.Title}";

    public override void OnEnter(Npc npc) { npc.Raiding = true; npc.RaidTarget = _target.Company; npc.RaidTimer = 0f; }

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        switch (_phase)
        {
            case 0:
                _phase = 1;
                return Delegate(new GoToGoal(_target.AccessCell, 130f));
            case 1:
                if (!ChildFinished(out var r)) return GoalStatus.Running;
                if (r != GoalStatus.Done) return GoalStatus.Failed;
                // Muskets at the door and the odds against you: most men think
                // better of it. That is what a guard is for.
                if (Economy.Rivalry.HouseHasOdds(_target.Company, npc.GlobalPosition) && npc.Personality.Courage < 0.65f)
                {
                    Sim.Notice($"{npc.DisplayName} turned back at the door of {_target.Title}: too many muskets about.");
                    Chronicle.Event($"{npc.DisplayName} came for {_target.Title} and turned back at the sight of the guard.", npc.GlobalPosition, npc, weight: 1f);
                    Economy.Rivalry.Raise(3f, "a party turned back at the door");
                    npc.Raiding = false;   // going home empty-handed; not a target any more
                    _phase = 2;
                    return Delegate(new GoToGoal(npc.HomeStores?.AccessCell ?? npc.BedCell, 110f));
                }
                Economy.Rivalry.Plunder(npc, _target, 6);
                npc.RaidTimer = 0f;   // the goal keeps the flag; the timer is for players
                _phase = 2;
                return Delegate(new GoToGoal(npc.HomeStores?.AccessCell ?? npc.BedCell, 110f));
            default:
                if (!ChildFinished(out var r2)) return GoalStatus.Running;
                if (npc.HomeStores != null)
                {
                    int lots = 0;
                    foreach (var (id, n) in new System.Collections.Generic.List<(ItemId, int)>(npc.Inventory.All()))
                    {
                        var kind = Items.Items.Of(id).Kind;
                        if (kind == ItemKind.Weapon || kind == ItemKind.Clothing || kind == ItemKind.Tool) continue;
                        if (kind == ItemKind.Food && n <= 1) continue;
                        int give = kind == ItemKind.Food ? n - 1 : n;
                        npc.Inventory.Remove(id, give); npc.HomeStores.Stores.Add(id, give); lots += give;
                    }
                    if (lots > 0) Sim.Notice($"{npc.DisplayName} is back with {lots} lots of the {(_target.Company == Faction.NWC ? "Nor'Westers'" : "Company's")} goods.");
                }
                return GoalStatus.Done;
        }
    }

    public override void OnExit(Npc npc) { npc.Raiding = false; npc.RaidTimer = 0f; npc.StopMoving(); if (npc.AssignedTaskName.StartsWith("seize ")) npc.AssignedTaskName = "back from the other post"; }
}

// Stand by the stores with a musket after a robbery (design doc 15.1). The
// interrupt logic does the rest when a raider shows up.
public class GuardGoal : Goal
{
    private readonly Vector2I _post;
    private float _left;
    private float _shift;
    public GuardGoal(Vector2I post, float hours) { _post = post; _left = hours; }
    public override string Label => "stand guard";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        _left -= hours; _shift -= hours;
        if (_left <= 0f) return GoalStatus.Done;
        if (npc.Needs.Thirst < 35f || npc.Needs.Hunger < 30f || npc.Needs.Warmth < 30f) return GoalStatus.Done;
        if (_shift <= 0f)
        {
            _shift = 0.5f;
            npc.SetDestination(new Vector2I(_post.X + GD.RandRange(-1, 1), _post.Y + GD.RandRange(-2, 2)));
        }
        return GoalStatus.Running;
    }
    public override void OnExit(Npc npc) => npc.StopMoving();
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
                _site.SetTraps(npc.HomeStores?.Title ?? npc.DisplayName, n, npc.Skills.Trapping);
                npc.Skills.Learn(ref npc.Skills.Trapping, 0.01f);
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
    private readonly bool _sellProvisions;
    public TradeTripGoal(bool buyFood, Economy.FortLedger post = null, bool sellProvisions = false) { _buyFood = buyFood; _post = post; _sellProvisions = sellProvisions; }
    public override string Label => (_buyFood ? "trade for provisions at " : "trade furs at ") + (_post?.Title ?? "the post");

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        var band = npc.HomeStores as Economy.BandLedger ?? Sim.Band; var fort = _post ?? Sim.Fort;
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
                foreach (var id in new[] { ItemId.BeaverPelt, ItemId.ThinPelt, ItemId.ShotPelt, ItemId.ShotHide, ItemId.CaribouHide, ItemId.WolfPelt })
                    npc.Inventory.Add(id, band.Stores.TakeUpTo(id, 30));
                npc.Inventory.Add(ItemId.TradeGoods, band.Stores.TakeUpTo(ItemId.TradeGoods, _buyFood ? 10 : 0));
                if (_sellProvisions) npc.Inventory.Add(ItemId.Pemmican, band.Stores.TakeUpTo(ItemId.Pemmican, System.Math.Max(0, band.Stores.Count(ItemId.Pemmican) - (band.Mouths * 6 + 6))));   // the plains trade, keeping the camp's own
                if (npc.Inventory.CountOfKind(ItemKind.Fur) == 0 && !npc.Inventory.Has(ItemId.TradeGoods) && npc.Inventory.Count(ItemId.Pemmican) < 4) return GoalStatus.Failed;
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
                float paid = fort.BuyFurs(npc.Inventory, npc.Inventory, band);
                int bought = 0, soldFood = 0;
                if (_buyFood) bought = fort.SellFood(npc.Inventory, npc.Inventory.Count(ItemId.TradeGoods), band);
                if (_sellProvisions && npc.Inventory.Count(ItemId.Pemmican) >= 4) { int m = 0; foreach (var x in Sim.Npcs) if (x.HomeStores == fort && !x.IsDead) m++; soldFood = fort.BuyProvisions(npc.Inventory, npc.Inventory, m); }
                Sim.Notice($"{npc.DisplayName} traded at {fort.Title}: {paid:0} MB in goods{(bought > 0 ? $", bought {bought} of food" : "")}{(soldFood > 0 ? $", sold {soldFood} pemmican" : "")}{(fort.DebtOf(band) > 0 ? $" (owes {fort.DebtOf(band):0.#} MB)" : "")}.");
                Chronicle.Event($"{npc.DisplayName} traded furs at {fort.Title}.", npc.GlobalPosition, npc, weight: 0.4f);
                if (fort is Economy.NwcLedger && Sim.Fort != null) { Sim.Fort.LostToRivalMb += paid; Economy.Rivalry.Raise(paid * 0.25f, "furs going to the Nor'Westers"); }
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
                    foreach (var id in new[] { ItemId.BeaverPelt, ItemId.ThinPelt, ItemId.ShotPelt, ItemId.ShotHide, ItemId.CaribouHide, ItemId.WolfPelt })
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

// Small game in the bush (design doc 14.4): snaring hares and knocking
// over ptarmigan along the edge of the woods. Abstract abundance - it
// doesn't draw a site down - but it's thin fare and a long walk.
public class ForageGoal : Goal
{
    private readonly float _workHours;
    private float _worked, _tryTimer;
    private int _phase;
    private int _got;
    public ForageGoal(float workHours = 4f) { _workHours = workHours; }
    public override string Label => "hunt small game";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        switch (_phase)
        {
            case 0:
            {
                _phase = 1;
                // Walk to the nearest edge of the bush.
                var tree = Sim.Structures?.NearestTree(npc.GlobalPosition, 3500f);
                if (tree == null) return GoalStatus.Failed;
                return Delegate(new GoToGoal(tree.Value, 160f));
            }
            case 1:
                if (!ChildFinished(out var r)) return GoalStatus.Running;
                if (r != GoalStatus.Done) return GoalStatus.Failed;
                _phase = 2;
                return GoalStatus.Running;
            case 2:
            {
                _worked += hours; _tryTimer -= hours;
                var sky = GameCalendar.Instance.Weather.Current;
                if (_worked >= _workHours || _got >= 4 || sky == SkyState.Blizzard || sky == SkyState.Storm || GameCalendar.Instance.Hour >= 16f || npc.Needs.Thirst < 40f || npc.Needs.Warmth < 35f)
                {
                    _phase = 3;
                    if (npc.HomeStores == null) return GoalStatus.Done;
                    return Delegate(new GoToGoal(npc.HomeStores.AccessCell, 100f));
                }
                if (_tryTimer <= 0f)
                {
                    _tryTimer = 0.75f;
                    float chance = GameCalendar.Instance.CurrentSeason == Season.Winter ? 0.3f : 0.4f;
                    if (GD.Randf() < chance) { npc.Inventory.Add(ItemId.Hare); _got++; }
                    var c = npc.Cell;
                    npc.SetDestination(new Vector2I(c.X + GD.RandRange(-3, 3), c.Y + GD.RandRange(-6, 6)));
                }
                return GoalStatus.Running;
            }
            default:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                if (npc.HomeStores != null)
                {
                    int n = npc.Inventory.Count(ItemId.Hare);
                    if (n > 1) { npc.Inventory.Remove(ItemId.Hare, n - 1); npc.HomeStores.Stores.Add(ItemId.Hare, n - 1); }
                }
                npc.PendingReport = _got > 0 ? $"snared {_got} hares" : "the snares were empty";
                return GoalStatus.Done;
        }
    }
}

// Pound dried meat with fat into pemmican (design doc 13.1): four dried
// meat and a hide's worth of fat make three that keep for years.
public class MakePemmicanGoal : Goal
{
    private int _phase;
    public override string Label => "make pemmican";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        var stores = npc.HomeStores;
        if (stores == null) return GoalStatus.Failed;
        switch (_phase)
        {
            case 0:
                if (stores.Stores.Count(ItemId.DriedMeat) < 4) return GoalStatus.Failed;
                _phase = 1;
                return Delegate(new GoToGoal(stores.AccessCell, 100f));
            case 1:
            {
                if (!ChildFinished(out var r)) return GoalStatus.Running;
                if (r != GoalStatus.Done) return GoalStatus.Failed;
                int took = stores.Stores.TakeUpTo(ItemId.DriedMeat, 8);
                if (took < 4) { stores.Stores.Add(ItemId.DriedMeat, took); return GoalStatus.Failed; }
                npc.Inventory.Add(ItemId.DriedMeat, took);
                var hearth = stores.LowestHearth(999f) ?? Sim.Structures?.NearestHearth(npc.GlobalPosition);
                if (hearth == null) return GoalStatus.Failed;
                _phase = 2;
                return Delegate(new GoToGoal(npc.StandCellNear(hearth.Cell), 110f));
            }
            case 2:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                _phase = 3;
                return Delegate(new WaitGoal(1.5f));
            default:
            {
                if (!ChildFinished(out _)) return GoalStatus.Running;
                int dried = npc.Inventory.Count(ItemId.DriedMeat);
                int made = dried * 3 / 4;
                npc.Inventory.Remove(ItemId.DriedMeat, dried);
                stores.Stores.Add(ItemId.Pemmican, made);
                Chronicle.Event($"{npc.DisplayName} made {made} pemmican.", npc.GlobalPosition, npc, weight: 0.2f);
                return GoalStatus.Done;
            }
        }
    }
}

// Salt the geese down (design doc 13.1, 14.1): the autumn flight is what a
// post lives on until spring, but only what goes into a cask keeps. Eight
// birds to a cask, an hour at the stores, and the cask is the winter.
public class SaltGeeseGoal : Goal
{
    private int _phase;
    public override string Label => "salt the geese";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        var stores = npc.HomeStores;
        if (stores == null) return GoalStatus.Failed;
        switch (_phase)
        {
            case 0:
                if (stores.Stores.Count(ItemId.Goose) < 8) return GoalStatus.Failed;
                _phase = 1;
                return Delegate(new GoToGoal(stores.AccessCell, 100f));
            case 1:
            {
                if (!ChildFinished(out var r)) return GoalStatus.Running;
                if (r != GoalStatus.Done) return GoalStatus.Failed;
                _phase = 2;
                return Delegate(new WaitGoal(1.4f));
            }
            default:
            {
                if (!ChildFinished(out _)) return GoalStatus.Running;
                int birds = stores.Stores.TakeUpTo(ItemId.Goose, 48);
                int casks = birds / 8;
                stores.Stores.Add(ItemId.Goose, birds - casks * 8);
                if (casks <= 0) return GoalStatus.Failed;
                stores.Stores.Add(ItemId.SaltGeese, casks);
                World.GooseHunt.Salted(stores, casks);
                Chronicle.Event($"{npc.DisplayName} salted down {casks * 8} geese - {casks} cask{(casks == 1 ? "" : "s")} for the winter.", npc.GlobalPosition, npc, weight: 0.4f);
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
