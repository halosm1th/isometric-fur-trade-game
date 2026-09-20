using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Ecology;

// One spawn site (design doc 16.4): a place with its own count, capacity,
// regrowth, and a breeding structure that overharvest destroys for good.
public class SpawnSite
{
    public string Name;
    public Species Species;
    public Vector2I Cell;
    public float RadiusPx;
    public float Count;          // abstract remainder + promoted living animals
    public int Capacity;
    public float BreedingCapacity = 1f;   // 0 = the lodge is gone
    public bool Scarcity;                  // over capacity: hungry animals range out
    public float FoodStock = 1f;          // local browse, 0..1

    public readonly List<Animal> Live = new();
    public readonly List<Trapline> Traplines = new();

    public Trapline LineOf(string owner)
    {
        foreach (var l in Traplines) if (l.Owner == owner) return l;
        return null;
    }

    public Trapline SetTraps(string owner, int n)
    {
        var line = LineOf(owner);
        if (line == null) { line = new Trapline { Owner = owner }; Traplines.Add(line); }
        line.Traps += n; line.HoursSinceSet = 0f;
        return line;
    }

    public void TickTraplines(float hours)
    {
        foreach (var l in Traplines) { l.HoursSinceCheck += hours; l.HoursSinceSet += hours; }
    }

    public Vector2 WorldPos => IsoGrid.ToWorld(Cell);
    public bool IsDead => BreedingCapacity <= 0f;
    public SpeciesDef Def => SpeciesInfo.Of(Species);

    public int LiveCount
    {
        get { Live.RemoveAll(a => a == null || !GodotObject.IsInstanceValid(a) || a.IsDead); return Live.Count; }
    }

    // Take animals out of the abstract count (a hunter working the site).
    public void Take(int n)
    {
        Count = Mathf.Max(0f, Count - n);
        CheckCollapse();
    }

    // A promoted animal died: the site loses one.
    public void OnLiveDeath(Animal a)
    {
        Live.Remove(a);
        Count = Mathf.Max(0f, Count - 1);
        CheckCollapse();
    }

    private void CheckCollapse()
    {
        if (!IsDead && Count < Capacity * Def.SustainFloor)
        {
            BreedingCapacity = 0f;
            Count = Mathf.Min(Count, 2f);
            Sim.Notice($"{Name} has been hunted out. It will not recover.");
        }
    }

    // One week of natural change.
    public void WeeklyTick(float preyPressure)
    {
        if (IsDead)
        {
            Count = Mathf.Max(0f, Count - 0.5f);
            return;
        }
        float r = Def.RegrowthPerYear / 52f;
        if (Def.Diet == Diet.Predator)
        {
            // Wolves follow the prey: grow when there is plenty, starve when not.
            r *= Mathf.Clamp(preyPressure * 2f - 0.6f, -1.5f, 1.5f);
            Count = Mathf.Max(1f, Count + r * Count);
        }
        else
        {
            Count += r * Count * (1f - Count / Capacity) + (Count < 2f ? 0.1f : 0f);
        }
        // Browse: eaten down when over capacity, regrows when under.
        float over = Count / Capacity;
        FoodStock = Mathf.Clamp(FoodStock + (over > 1f ? -(over - 1f) * 0.6f : 0.15f), 0f, 1f);
        bool wasScarce = Scarcity;
        Scarcity = FoodStock < 0.35f && over > 0.95f;
        if (Scarcity && !wasScarce) Sim.Notice($"{Name}: too many animals for the ground. They are hungry and ranging toward the post.");
        if (!Scarcity && wasScarce) Sim.Notice($"{Name} has settled again.");
    }

    public Animal NearestLiveAnimal(Vector2 pos, float radius)
    {
        Animal best = null; float bestD = radius;
        foreach (var a in Live)
        {
            if (a == null || !GodotObject.IsInstanceValid(a) || a.IsDead) continue;
            float d = a.GlobalPosition.DistanceTo(pos);
            if (d < bestD) { bestD = d; best = a; }
        }
        return best;
    }

    public string Status()
    {
        string state = IsDead ? "DEAD" : Scarcity ? "scarcity" : "ok";
        string traps = "";
        foreach (var l in Traplines) traps += $" [{l.Owner}: {l.Traps} traps, {l.Caught} caught]";
        return $"{Name}: {Count:0}/{Capacity} ({LiveCount} live) browse {FoodStock:0.00} [{state}]{traps}";
    }
}

// Runs the sites: weekly growth, promotion of animals into live agents near
// the player (or near the post when they're hungry), demotion when far.
public partial class EcologyManager : Node2D
{
    [Export] public PackedScene AnimalScene;
    [Export] public float PromoteRadius = 2600f;
    [Export] public float DemoteRadius = 3600f;

    public readonly List<SpawnSite> Sites = new();
    private float _hoursToWeek = 24f * 7f;
    private float _promoteTimer;

    public override void _Ready()
    {
        Sim.Ecology = this;
        foreach (var d in WorldLayout.Sites)
        {
            Sites.Add(new SpawnSite { Name = d.Name, Species = d.Species, Cell = d.Cell, Count = d.Count, Capacity = d.Capacity, RadiusPx = d.RadiusPx });
        }
    }

    public SpawnSite BestSite(Species s, Economy.Household knownBy = null)
    {
        SpawnSite best = null;
        foreach (var site in Sites)
        {
            if (site.Species != s || site.IsDead || site.Count < 1f) continue;
            if (knownBy != null && !knownBy.Knows(site.Name)) continue;
            if (best == null || site.Count > best.Count) best = site;
        }
        return best;
    }

    // Anyone passing close to a site learns where it is for their household.
    public void Scout(Agents.Npc npc)
    {
        if (npc.HomeStores == null) return;
        foreach (var site in Sites)
        {
            if (npc.HomeStores.Knows(site.Name)) continue;
            if (site.WorldPos.DistanceTo(npc.GlobalPosition) < site.RadiusPx * 0.6f) npc.HomeStores.Learn(site.Name);
        }
    }

    public float PreyPressure
    {
        get
        {
            float have = 0f, cap = 0f;
            foreach (var s in Sites) if (s.Def.Diet == Diet.Prey) { have += s.Count; cap += s.Capacity; }
            return cap > 0f ? have / cap : 0f;
        }
    }

    private bool _scenarioDone;
    private int _lastTrapDay = -1;

    public SpawnSite SiteNear(Vector2 pos, Species? species = null)
    {
        foreach (var s in Sites)
        {
            if (species != null && s.Species != species) continue;
            if (s.WorldPos.DistanceTo(pos) <= s.RadiusPx) return s;
        }
        return null;
    }

    public override void _Process(double delta)
    {
        var cal = GameCalendar.Instance;
        if (cal == null || !Net.Net.IsServer) return;
        if (!_scenarioDone)
        {
            _scenarioDone = true;
            // Dev aid: `-- --wolves` drops a hungry pack outside the gate at start.
            foreach (string arg in OS.GetCmdlineUserArgs())
            {
                if (arg == "--wolves") SpawnPackNear(Species.Wolf, IsoGrid.ToWorld(WorldLayout.GateOutside) + new Vector2(350, 250), 4, true);
                if (arg == "--scarcity") foreach (var s in Sites) { s.FoodStock = 0.1f; s.Count = s.Capacity * 1.2f; s.Scarcity = true; }
            }
        }
        foreach (var s in Sites) s.TickTraplines(cal.DeltaGameHours);
        // Traps work at night.
        int day = cal.Day;
        if (day != _lastTrapDay && cal.Hour >= 5f)
        {
            _lastTrapDay = day;
            foreach (var s in Sites) TraplineRules.NightlyCatch(s);
        }
        _hoursToWeek -= cal.DeltaGameHours;
        if (_hoursToWeek <= 0f)
        {
            _hoursToWeek += 24f * 7f;
            float pressure = PreyPressure;
            foreach (var s in Sites) s.WeeklyTick(pressure);
        }

        _promoteTimer -= (float)delta;
        if (_promoteTimer <= 0f)
        {
            _promoteTimer = 1.5f;
            Promote();
            foreach (var n in Sim.Npcs) if (!n.IsDead) Scout(n);
        }
    }

    private static float DistanceToNearestPlayer(Vector2 p)
    {
        float best = float.MaxValue;
        foreach (var pl in Sim.Players()) if (!pl.IsDead) best = Mathf.Min(best, pl.GlobalPosition.DistanceTo(p));
        return best;
    }

    private void Promote()
    {
        if (AnimalScene == null) return;
        var fortCentre = IsoGrid.ToWorld(IsoGrid.Cell(WorldLayout.FortTop, WorldLayout.FortWidth / 2, WorldLayout.FortDepth / 2));

        foreach (var site in Sites)
        {
            int live = site.LiveCount;
            float dPlayer = DistanceToNearestPlayer(site.WorldPos);
            bool near = dPlayer < PromoteRadius;
            int want = near || site.Scarcity ? Mathf.Min(MaxLive(site), Mathf.FloorToInt(site.Count)) : 0;

            if (live < want)
            {
                // Hungry animals appear on the post's side of the site.
                var origin = site.WorldPos;
                if (site.Scarcity) origin = origin.Lerp(fortCentre, 0.35f);
                var a = AnimalScene.Instantiate<Animal>();
                a.Species = site.Species;
                a.Site = site;
                a.Position = origin + new Vector2((float)GD.RandRange(-1.0, 1.0), (float)GD.RandRange(-0.5, 0.5)) * site.RadiusPx * 0.6f;
                a.Hungry = site.Scarcity;
                AddChild(a);
                a.Name = $"animal_{a.NetId}";
                Net.Net.I?.Announce(a);
                site.Live.Add(a);
            }
            else if (live > 0 && !near && !site.Scarcity && dPlayer > DemoteRadius)
            {
                // Far away and quiet: back into the count.
                foreach (var a in new List<Animal>(site.Live))
                {
                    if (a == null || !IsInstanceValid(a) || a.IsDead) continue;
                    if (DistanceToNearestPlayer(a.GlobalPosition) > DemoteRadius)
                    {
                        site.Live.Remove(a);
                        Net.Net.I?.AnnounceDespawn(a);
                        a.QueueFree();
                    }
                }
            }
        }
    }

    // Debug/test: drop a group of animals somewhere, tied to the nearest
    // site of that species so the bookkeeping still holds.
    public void SpawnPackNear(Species species, Vector2 pos, int n, bool hungry)
    {
        if (AnimalScene == null) return;
        SpawnSite site = null;
        foreach (var s in Sites) if (s.Species == species) { site = s; break; }
        for (int i = 0; i < n; i++)
        {
            var a = AnimalScene.Instantiate<Animal>();
            a.Species = species; a.Site = site; a.Hungry = hungry;
            a.Position = pos + new Vector2((float)GD.RandRange(-120, 120), (float)GD.RandRange(-60, 60));
            AddChild(a);
            a.Name = $"animal_{a.NetId}";
            Net.Net.I?.Announce(a);
            if (site != null) { site.Live.Add(a); site.Count += 1; }
        }
    }

    private static int MaxLive(SpawnSite s) => s.Species switch
    {
        Species.Caribou => 7,
        Species.Goose => 12,
        Species.Beaver => 3,
        Species.Wolf => 5,
        _ => 4,
    };

    public string Report()
    {
        var lines = new List<string>();
        foreach (var s in Sites) lines.Add(s.Status());
        return string.Join("\n", lines);
    }
}
