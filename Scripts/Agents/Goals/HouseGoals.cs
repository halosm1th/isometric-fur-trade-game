using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Agents.Goals;

// Keep the household's water up (design doc 17.2): in open water, kettles
// to the shore and back; in winter, melt snow at the fire. Water is not
// scarce - the cost is the walk and the fuel.
public class FetchWaterGoal : Goal
{
    private int _phase;
    public override string Label => "fetch water";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        var stores = npc.HomeStores;
        if (stores == null) return GoalStatus.Failed;
        var cal = GameCalendar.Instance;
        switch (_phase)
        {
            case 0:
                if (!npc.Inventory.Has(ItemId.Kettle))
                {
                    if (!stores.Stores.Has(ItemId.Kettle)) return GoalStatus.Failed;
                    _phase = 1;
                    return Delegate(new GoToGoal(stores.AccessCell, 100f));
                }
                _phase = 2;
                return GoalStatus.Running;
            case 1:
                if (!ChildFinished(out var r0)) return GoalStatus.Running;
                if (r0 != GoalStatus.Done || !stores.Stores.Remove(ItemId.Kettle)) return GoalStatus.Failed;
                npc.Inventory.Add(ItemId.Kettle);
                _phase = 2;
                return GoalStatus.Running;
            case 2:
                if (cal.IsFrozen)
                {
                    // Melt snow at the household fire.
                    var h = stores.LowestHearth(999f) ?? Sim.Structures?.NearestHearth(npc.GlobalPosition);
                    if (h == null) return GoalStatus.Failed;
                    _phase = 3;
                    return Delegate(new GoToGoal(npc.StandCellNear(h.Cell), 110f));
                }
                var cell = npc.NearestWaterCell(9000f);
                if (cell == null) return GoalStatus.Failed;
                _phase = 4;
                return Delegate(new GoToGoal(cell.Value, 30f));
            case 3:
            {
                if (!ChildFinished(out var r)) return GoalStatus.Running;
                if (r != GoalStatus.Done) return GoalStatus.Failed;
                var h = Sim.Structures?.HearthNear(npc.GlobalPosition, 200f);
                if (h == null || !h.IsBurning) return GoalStatus.Failed;
                h.FuelHours = Mathf.Max(0.1f, h.FuelHours - 0.6f);
                npc.Inventory.Add(ItemId.Water, 4);
                _phase = 5;
                return Delegate(new WaitGoal(0.5f));
            }
            case 4:
            {
                if (!ChildFinished(out var r)) return GoalStatus.Running;
                if (r != GoalStatus.Done) return GoalStatus.Failed;
                if (Sim.Terrain == null || !Sim.Terrain.IsWaterNear(npc.GlobalPosition)) return GoalStatus.Failed;
                npc.Inventory.Add(ItemId.Water, 6);
                npc.Needs.Thirst = 100f;
                _phase = 5;
                return Delegate(new WaitGoal(0.2f));
            }
            case 5:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                _phase = 6;
                return Delegate(new GoToGoal(stores.AccessCell, 100f));
            default:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                stores.Stores.Add(ItemId.Water, npc.Inventory.TakeUpTo(ItemId.Water, 99));
                if (npc.Inventory.Remove(ItemId.Kettle)) stores.Stores.Add(ItemId.Kettle);
                return GoalStatus.Done;
        }
    }
}

// Fishing (design doc 14.4): a line at the water's edge for a few hours,
// a handful of fish for the pot. Open water only.
public class FishGoal : Goal
{
    private int _phase;
    public override string Label => "fishing";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        var stores = npc.HomeStores;
        if (stores == null || GameCalendar.Instance.IsFrozen) return GoalStatus.Failed;
        switch (_phase)
        {
            case 0:
                var cell = npc.NearestWaterCell(9000f);
                if (cell == null) return GoalStatus.Failed;
                _phase = 1;
                return Delegate(new GoToGoal(cell.Value, 30f));
            case 1:
                if (!ChildFinished(out var r)) return GoalStatus.Running;
                if (r != GoalStatus.Done) return GoalStatus.Failed;
                _phase = 2;
                return Delegate(new WaitGoal(2.5f));
            case 2:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                int caught = GD.RandRange(0, 4);
                npc.Inventory.Add(ItemId.Fish, caught);
                _phase = 3;
                return Delegate(new GoToGoal(stores.AccessCell, 100f));
            default:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                stores.Stores.Add(ItemId.Fish, npc.Inventory.TakeUpTo(ItemId.Fish, 99));
                return GoalStatus.Done;
        }
    }
}

// Water on the flames. Tier 0: your house is burning.
public class FightFireGoal : Goal
{
    public override int Tier => 0;
    private int _phase;
    private float _giveUp = 90f;
    public override string Label => "fight the fire";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        var fire = World.Fire.I; var stores = npc.HomeStores;
        if (fire == null || stores == null) return GoalStatus.Failed;
        _giveUp -= dt;
        if (_giveUp <= 0f) return GoalStatus.Failed;
        var target = fire.NearestBurning(IsoGrid.ToWorld(stores.AccessCell), 900f);
        if (target == null) return GoalStatus.Done;
        switch (_phase)
        {
            case 0:
                if (npc.Inventory.Has(ItemId.Water)) { _phase = 2; return GoalStatus.Running; }
                if (!stores.Stores.Has(ItemId.Water)) return GoalStatus.Failed;
                _phase = 1;
                return Delegate(new GoToGoal(stores.AccessCell, 100f));
            case 1:
                if (!ChildFinished(out var r)) return GoalStatus.Running;
                if (r != GoalStatus.Done) return GoalStatus.Failed;
                npc.Inventory.Add(ItemId.Water, stores.Stores.TakeUpTo(ItemId.Water, 3));
                if (!npc.Inventory.Has(ItemId.Water)) return GoalStatus.Failed;
                _phase = 2;
                return GoalStatus.Running;
            case 2:
                _phase = 3;
                return Delegate(new GoToGoal(npc.StandCellNear(target.Value), 110f));
            default:
            {
                if (!ChildFinished(out _)) return GoalStatus.Running;
                var near = fire.NearestBurning(npc.GlobalPosition, 150f);
                if (near != null && npc.Inventory.Remove(ItemId.Water)) fire.Douse(near.Value);
                _phase = npc.Inventory.Has(ItemId.Water) ? 2 : 0;
                return GoalStatus.Running;
            }
        }
    }
}

// Get away from a point (fire, mostly).
public class FleePointGoal : Goal
{
    public override int Tier => 0;
    private readonly Vector2 _from;
    private float _left;
    public FleePointGoal(Vector2 from, float seconds = 4f) { _from = from; _left = seconds; }
    public override string Label => "run from the fire";
    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        _left -= dt;
        if (_left <= 0f) { npc.StopMoving(); return GoalStatus.Done; }
        npc.SteerDirect(npc.GlobalPosition - _from, true);
        return GoalStatus.Running;
    }
    public override void OnExit(Npc npc) => npc.StopMoving();
}

// An evening with the rum (design doc 13.4). Company men and Cree hunters
// alike: take a dram from the cache, drink it, talk. Drunk men quarrel.
public class CarouseGoal : Goal
{
    private int _phase;
    private float _left = 2.5f;
    public override string Label => "drinking";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        var stores = npc.HomeStores;
        if (stores == null) return GoalStatus.Failed;
        switch (_phase)
        {
            case 0:
                if (!stores.Stores.Has(ItemId.Rum) && !stores.Stores.Has(ItemId.Ale)) return GoalStatus.Failed;
                _phase = 1;
                return Delegate(new GoToGoal(stores.AccessCell, 100f));
            case 1:
            {
                if (!ChildFinished(out var r)) return GoalStatus.Running;
                if (r != GoalStatus.Done) return GoalStatus.Failed;
                var drink = stores.Stores.Has(ItemId.Rum) ? ItemId.Rum : ItemId.Ale;
                if (!stores.Stores.Remove(drink)) return GoalStatus.Failed;
                npc.Inventory.Add(drink);
                npc.Consume(drink);
                npc.PlayAnimation("drink");
                _phase = 2;
                return GoalStatus.Running;
            }
            default:
                _left -= hours;
                if (_left <= 0f || GameCalendar.Instance.Hour >= 23f) return GoalStatus.Done;
                // Another dram now and then; the drunker, the likelier a quarrel.
                if (GD.Randf() < hours * 0.5f && stores.Stores.Has(ItemId.Rum) && npc.Intoxication < 80f && npc.GlobalPosition.DistanceTo(IsoGrid.ToWorld(stores.AccessCell)) < 160f)
                {
                    stores.Stores.Remove(ItemId.Rum); npc.Inventory.Add(ItemId.Rum); npc.Consume(ItemId.Rum); npc.PlayAnimation("drink");
                }
                if (npc.Intoxication > 55f && GD.Randf() < hours * 0.25f * npc.Personality.Courage)
                {
                    foreach (var a in Sim.AgentsWithin(npc.GlobalPosition, 160f))
                    {
                        if (a is Npc other && other != npc && !other.IsDead && other.Intoxication > 30f && !other.IsFemale)
                        {
                            Chronicle.Event($"{npc.DisplayName} and {other.DisplayName} came to blows over the rum.", npc.GlobalPosition, npc, other.DisplayName, 1f);
                            Sim.Notice($"{npc.DisplayName} and {other.DisplayName} are brawling, drunk.");
                            return Delegate(new BrawlGoal(other));
                        }
                    }
                }
                return GoalStatus.Running;
        }
    }
}

// Fists only, a few blows, then it's over.
public class BrawlGoal : Goal
{
    public override int Tier => 0;
    private readonly Agent _target;
    private float _left = 12f;
    private int _blows;
    public BrawlGoal(Agent target) { _target = target; }
    public override string Label => $"brawl with {_target?.Describe()}";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        _left -= dt;
        if (_target == null || !GodotObject.IsInstanceValid(_target) || _target.IsDead || _left <= 0f || _blows >= 3 || _target.Needs.Health < 40f) { npc.StopMoving(); return GoalStatus.Done; }
        if (npc.GlobalPosition.DistanceTo(_target.GlobalPosition) < 70f)
        {
            npc.StopMoving();
            var savedWeapon = npc.PreferredWeapon;
            npc.Unarmed = true;
            if (npc.Attack(_target)) _blows++;
            npc.Unarmed = false;
            return GoalStatus.Running;
        }
        npc.ChaseToward(_target.GlobalPosition, dt);
        return GoalStatus.Running;
    }
    public override void OnExit(Npc npc) => npc.StopMoving();
}

// An evening at the fire (design doc P.8, 17.6): the household gathers at
// its own hearth after the day's work - stories, songs, a word for the dead -
// until bedtime. Talk comes thicker here (Npc.Chatter, Barks.HearthLine).
public class HearthGoal : Goal
{
    private int _phase;
    private Hearth _hearth;
    public override string Label => "sitting by the fire";
    public override void OnExit(Npc npc) { npc.AtHearth = null; }

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        switch (_phase)
        {
            case 0:
                _hearth = HomeHearth(npc);
                if (_hearth == null) return GoalStatus.Failed;
                _phase = 1;
                return Delegate(new GoToGoal(Seat(npc, _hearth), 30f));
            case 1:
                if (!ChildFinished(out var r)) return GoalStatus.Running;
                if (r != GoalStatus.Done) return GoalStatus.Failed;
                npc.AtHearth = _hearth;
                npc.StopMoving();
                npc.FaceToward(_hearth.GlobalPosition);
                _phase = 2;
                return GoalStatus.Running;
            default:
                float h = GameCalendar.Instance.Hour;
                if (h >= 21.5f || h < 12f || _hearth == null || !GodotObject.IsInstanceValid(_hearth)) return GoalStatus.Done;
                // An evening spent at the same fire draws people together.
                foreach (var pc in Sim.Players()) if (pc.GlobalPosition.DistanceTo(_hearth.GlobalPosition) < 260f) npc.ChangeRapport(pc, hours * 0.8f);
                return GoalStatus.Running;
        }
    }

    // A place of his own round the fire: a free cell up to two steps out
    // from the hearth without going through a wall, as far from the others
    // already sitting as can be had.
    private static Vector2I Seat(Npc npc, Hearth hearth)
    {
        var paths = Sim.Paths; var st = Sim.Structures;
        var ring = new System.Collections.Generic.List<Vector2I>();
        var seen = new System.Collections.Generic.HashSet<Vector2I> { hearth.Cell };
        var frontier = new System.Collections.Generic.List<Vector2I> { hearth.Cell };
        for (int step = 0; step < 2; step++)
        {
            var next = new System.Collections.Generic.List<Vector2I>();
            foreach (var c in frontier)
                foreach (var n in IsoGrid.EdgeNeighbours(c))
                {
                    if (!seen.Add(n) || paths != null && !paths.IsWalkable(n) || st != null && st.EdgeBlocked(c, n, out _)) continue;
                    next.Add(n); ring.Add(n);
                }
            frontier = next;
        }
        if (ring.Count == 0) return npc.StandCellNear(hearth.Cell);
        Vector2I best = ring[0]; float bestScore = float.MinValue;
        foreach (var c in ring)
        {
            var w = IsoGrid.ToWorld(c);
            float crowd = 0f;
            foreach (var a in Sim.AgentsWithin(w, 90f)) if (a != npc) crowd += 1f;
            float score = -crowd * 10f + GD.Randf();
            if (score > bestScore) { bestScore = score; best = c; }
        }
        return best;
    }

    // The burning hearth of his own household nearest where he sleeps.
    public static Hearth HomeHearth(Npc npc)
    {
        var s = Sim.Structures;
        if (s == null || npc.HomeStores == null) return null;
        var bed = IsoGrid.ToWorld(npc.BedCell);
        Hearth best = null; float bestD = 1400f;
        foreach (var h in s.AllHearths)
        {
            if (!h.IsBurning || !npc.HomeStores.HearthCells.Contains(h.Cell)) continue;
            float d = h.GlobalPosition.DistanceTo(bed);
            if (d < bestD) { bestD = d; best = h; }
        }
        return best;
    }
}
