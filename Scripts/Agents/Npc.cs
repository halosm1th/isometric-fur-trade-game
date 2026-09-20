using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents.Goals;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Agents;

public enum Role
{
    Factor, Clerk, Labourer, Woman,
    CreeChief, CreeHunter, CreeWoman, CreeDaughter,
    Sailor, Wife,
}

// Four traits that decide what an agent does with what it knows and wants
// (design doc 11.4). 0..1.
public struct Personality
{
    public float Loyalty, Talkative, Suspicion, Ambition, Courage;
    public static Personality Random() => new()
    {
        Loyalty = GD.Randf(), Talkative = GD.Randf(), Suspicion = GD.Randf(), Ambition = GD.Randf(), Courage = GD.Randf(),
    };
}

// A person run by a goal stack instead of a keyboard. Same needs, same
// inventory, same verbs as the player.
public partial class Npc : Agent
{
    public Role Role;
    public Personality Personality = Personality.Random();
    public readonly Brain Brain = new();

    // Where they sleep, and whose stores they draw on.
    public Vector2I BedCell;
    public Vector2I WorkCell;
    public Household HomeStores;
    public bool Sleeping;
    public bool IsFemale;

    // Kinship for the marriage system (design doc 8).
    public Npc Father;
    public Agent Spouse;
    public readonly List<Npc> Children = new();

    // Fast-moving individual rapport with other agents (design doc 3.6).
    public readonly Dictionary<Agent, float> Rapport = new();

    // A standing order from a superior (design doc 3.9): the current task
    // chain to run when nothing more urgent is going on.
    public System.Func<Npc, Goal> AssignedTask;
    public string AssignedTaskName = "";
    public bool AssignedTaskIsHunt;

    private AnimatedSprite2D _sprite;
    private Label _label;
    private RoleBehaviour _behaviour;

    // ---- movement --------------------------------------------------------
    private List<Vector2I> _path = new();
    private int _pathIndex;
    private Vector2I _destination;
    private float _stuckTime;
    private int _repaths;
    public bool Arrived { get; private set; } = true;
    public bool MoveFailed { get; private set; }
    private Vector2 _steer;
    private bool _steerRun;
    private float _sinceEatAnim;

    public string GoalDescription => Brain.Describe();

    public override void _Ready()
    {
        _sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        _label = GetNodeOrNull<Label>("NameLabel");
        if (_label != null) _label.Text = DisplayName;
        _behaviour = RoleBehaviour.For(Role);
        MotionMode = MotionModeEnum.Floating;
    }

    public void ResetBehaviour() => _behaviour = RoleBehaviour.For(Role);

    public void SetAppearance(SpriteFrames frames, Color tint, float scale)
    {
        if (_sprite == null) return;
        _sprite.SpriteFrames = frames;
        _sprite.Modulate = tint;
        _sprite.Scale = Vector2.One * scale;
        _sprite.Play("idle");
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        float dt = (float)delta;
        var cal = GameCalendar.Instance;
        if (cal == null) return;

        if (IsDead)
        {
            Velocity = Vector2.Zero;
            return;
        }

        float hours = cal.DeltaGameHours;
        _sinceEatAnim += dt;

        Sleeping = false;   // SleepGoal re-asserts this every tick it's lying down
        ConsiderInterrupts();
        if (Brain.IsEmpty)
        {
            // A plan that just fell through gets a pause before the next
            // try, so an unreachable goal can't be re-planned every frame.
            if (Brain.LastResult == GoalStatus.Failed && Brain.ConsecutiveFailures > 0)
            {
                Brain.Push(this, new WaitGoal(Mathf.Min(2f, 0.4f * Brain.ConsecutiveFailures)));
            }
            else
            {
                var next = _behaviour?.NextGoal(this);
                if (next != null) Brain.Push(this, next);
            }
        }
        Brain.Tick(this, dt, hours);

        Move(dt, cal.IsSkipping);
        Animate();
    }

    // Things you do in passing, no plan needed.
    private void Opportunities()
    {
        if (Needs.Thirst < 70f) Drink();
        if (Needs.Hunger < 55f && Inventory.FirstOfKind(ItemKind.Food) != null && _sinceEatAnim > 3f)
        {
            if (Eat()) PlayAnimation("eat");
        }
    }

    // Tier 0 preempts everything (design doc 3.1). Fighting comes first;
    // then whichever need is worst, which may bump a less urgent need's goal.
    private void ConsiderInterrupts()
    {
        Opportunities();
        var current = Brain.Tier0Goal();
        if (current is AttackGoal || current is FleeGoal) return;

        // Being attacked: fight back if there's a chance, else run.
        if (LastAttackedAgo < 2f && LastAttacker != null && IsInstanceValid(LastAttacker) && !LastAttacker.IsDead)
        {
            bool armed = Weapon != null;
            bool brave = Personality.Courage > 0.35f || armed;
            if (brave && Needs.Health > 30f) Brain.Push(this, new AttackGoal(LastAttacker));
            else Brain.Push(this, new FleeGoal(LastAttacker, 8f));
            return;
        }
        // A predator closing in: armed men stand, others run for the walls.
        var threat = NearestHostile(220f);
        if (threat != null)
        {
            if (Weapon != null && Personality.Courage > 0.2f) Brain.Push(this, new AttackGoal(threat));
            else Brain.Push(this, new FleeGoal(threat, 5f));
            return;
        }

        // Worst need first. Warmth bites fastest so it's weighted.
        float w = Needs.Warmth - 5f, th = Needs.Thirst, h = Needs.Hunger + 5f;
        float worst = Mathf.Min(w, Mathf.Min(th, h));
        if (worst >= 30f) return;
        Goal wanted = worst == w ? new WarmUpGoal() : worst == th ? new DrinkGoal() : new EatGoal();
        if (current != null)
        {
            if (current.GetType() == wanted.GetType()) return;
            // Only bump the running need-goal if this one is clearly worse.
            float currentValue = current is WarmUpGoal ? w : current is DrinkGoal ? th : h;
            if (worst > currentValue - 12f) return;
            Brain.PopTier0(this);
        }
        Brain.Push(this, wanted);
    }

    public Agent NearestHostile(float radius)
    {
        Agent best = null; float bestD = radius;
        foreach (var a in Sim.AgentsWithin(GlobalPosition, radius))
        {
            if (!IsHostileTo(a)) continue;
            if (a is Animal an && !an.IsDangerous) continue;
            float d = GlobalPosition.DistanceTo(a.GlobalPosition);
            if (d < bestD) { bestD = d; best = a; }
        }
        return best;
    }

    // ---- movement API used by goals --------------------------------------------

    public bool SetDestination(Vector2I cell)
    {
        var paths = Sim.Paths;
        if (paths == null) return false;
        var start = Cell;
        var path = paths.FindPath(start, cell);
        MoveFailed = false;
        _repaths = 0;
        if (path.Count == 0)
        {
            // Already there, or unreachable.
            if (start == cell || (paths.NearestWalkable(cell) is Vector2I nw && nw == start)) { Arrived = true; _path.Clear(); return true; }
            MoveFailed = true; Arrived = false;
            return false;
        }
        _path = path; _pathIndex = 0; _destination = cell;
        Arrived = false; _stuckTime = 0f;
        return true;
    }

    public void StopMoving()
    {
        _path.Clear();
        _steer = Vector2.Zero;
        Velocity = Vector2.Zero;
    }

    // Direct steering (fleeing, chasing) without a path.
    public void SteerDirect(Vector2 dir, bool run)
    {
        _steer = dir.Normalized();
        _steerRun = run;
    }

    private float _chaseRepath;

    public void ChaseToward(Vector2 target, float dt)
    {
        // Path when far, straight steer when close. Moving targets only
        // get a fresh path twice a second.
        float d = GlobalPosition.DistanceTo(target);
        if (d < 160f) { SteerDirect(target - GlobalPosition, true); return; }
        _chaseRepath -= dt;
        var cell = IsoGrid.FromWorld(target);
        if (_path.Count == 0 || Arrived || (_destination != cell && _chaseRepath <= 0f))
        {
            _chaseRepath = 0.5f;
            SetDestination(cell);
        }
        _steer = Vector2.Zero;
    }

    public void FaceToward(Vector2 target)
    {
        if (_sprite != null) _sprite.FlipH = target.X < GlobalPosition.X;
    }

    private void Move(float dt, bool skipping)
    {
        IsSprinting = false;
        if (Sleeping) { Velocity = Vector2.Zero; return; }
        float motion = GameCalendar.Instance?.MotionScale ?? 1f;

        if (_steer != Vector2.Zero)
        {
            IsSprinting = _steerRun;
            Velocity = _steer * EffectiveSpeed * motion;
            MoveAndSlide();
            _steer = Vector2.Zero;
            return;
        }

        if (_path.Count == 0 || _pathIndex >= _path.Count)
        {
            Velocity = Vector2.Zero;
            if (_path.Count > 0) { Arrived = true; _path.Clear(); }
            return;
        }

        // Time is skipping (sleeping, waiting): resolve the walk lazily.
        if (skipping)
        {
            GlobalPosition = IsoGrid.ToWorld(_path[^1]);
            _path.Clear(); Arrived = true; Velocity = Vector2.Zero;
            return;
        }

        var next = _path[_pathIndex];
        var target = IsoGrid.ToWorld(next);
        var to = target - GlobalPosition;
        if (to.Length() < 14f)
        {
            _pathIndex++;
            if (_pathIndex >= _path.Count) { Arrived = true; _path.Clear(); Velocity = Vector2.Zero; }
            return;
        }

        // Doors open as you reach them.
        var here = Cell;
        if (Sim.Structures != null && Sim.Structures.EdgeBlocked(here, next, out bool isDoor) == false && isDoor)
        {
            Sim.Structures.OpenDoorAt(here);
            Sim.Structures.OpenDoorAt(next);
        }

        var before = GlobalPosition;
        float step = EffectiveSpeed * motion;
        // Don't overshoot the waypoint when time is running fast.
        if (step * dt > to.Length()) step = to.Length() / Mathf.Max(dt, 1e-4f);
        Velocity = to.Normalized() * step;
        MoveAndSlide();
        if (GlobalPosition.DistanceTo(before) < step * dt * 0.2f)
        {
            _stuckTime += dt;
            if (_stuckTime > 1.5f)
            {
                _stuckTime = 0f;
                if (++_repaths > 3) { MoveFailed = true; _path.Clear(); return; }
                var p = Sim.Paths.FindPath(Cell, _destination);
                if (p.Count == 0) { MoveFailed = true; _path.Clear(); }
                else { _path = p; _pathIndex = 0; }
            }
        }
        else _stuckTime = 0f;
    }

    private void Animate()
    {
        if (_sprite == null) return;
        if (_sinceEatAnim < 0.9f) return; // let one-shot anims play
        var v = Velocity;
        string target;
        if (v.LengthSquared() < 4f) target = "idle";
        else
        {
            bool side = Mathf.Abs(v.X) > Mathf.Abs(v.Y) * 1.6f && _sprite.SpriteFrames.HasAnimation("walk_side");
            target = side ? "walk_side" : (v.Y < -0.3f * v.Length() ? "walk_back" : "walk");
            _sprite.FlipH = v.X < 0f;
        }
        if (!_sprite.SpriteFrames.HasAnimation(target)) target = "idle";
        if (_sprite.Animation != target) _sprite.Play(target);
        _sprite.SpeedScale = IsSprinting ? 1.6f : 1f;
    }

    public void PlayAnimation(string anim)
    {
        if (_sprite == null || !_sprite.SpriteFrames.HasAnimation(anim)) return;
        _sprite.Play(anim);
        _sinceEatAnim = 0f;
    }

    // ---- helpers for goals ---------------------------------------------------

    // Nearest burning fire, preferring the household's own; a cold hearth
    // is acceptable if we can light it (carrying wood, or the woodpile has some).
    public Hearth PreferredHearth()
    {
        var s = Sim.Structures;
        if (s == null) return null;
        bool canLight = Inventory.Has(ItemId.Firewood) || (HomeStores != null && HomeStores.Stores.Has(ItemId.Firewood));
        Hearth best = null; float bestScore = float.MaxValue;
        foreach (var h in s.AllHearths)
        {
            if (!h.IsBurning && !canLight) continue;
            bool mine = HomeStores == null || HomeStores.HearthCells.Contains(h.Cell);
            float score = h.GlobalPosition.DistanceTo(GlobalPosition) * (mine ? 1f : 2.5f) + (h.IsBurning ? 0f : 400f);
            if (score < bestScore) { bestScore = score; best = h; }
        }
        return best;
    }

    // A walkable cell beside a prop cell.
    public Vector2I StandCellNear(Vector2I cell)
    {
        var paths = Sim.Paths;
        Vector2I best = cell; float bestD = float.MaxValue;
        foreach (var n in IsoGrid.EdgeNeighbours(cell))
        {
            if (paths != null && !paths.IsWalkable(n)) continue;
            if (Sim.Structures != null && Sim.Structures.EdgeBlocked(cell, n, out _)) continue;
            float d = IsoGrid.ToWorld(n).DistanceTo(GlobalPosition);
            if (d < bestD) { bestD = d; best = n; }
        }
        return best;
    }

    public Vector2I? NearestWaterCell(float maxDist)
    {
        var t = Sim.Terrain; var paths = Sim.Paths;
        if (t == null) return null;
        // Scan outward in rings from our cell for a walkable cell next to water.
        var c = Cell;
        int maxR = Mathf.CeilToInt(maxDist / 64f);
        Vector2I? best = null; float bestD = float.MaxValue;
        for (int r = 1; r < maxR; r += 1)
        {
            void Consider(Vector2I n)
            {
                if (!paths.IsWalkable(n) || !t.IsWaterNear(IsoGrid.ToWorld(n))) return;
                float d = IsoGrid.ToWorld(n).DistanceTo(GlobalPosition);
                if (d < bestD) { bestD = d; best = n; }
            }
            for (int dx = -r; dx <= r; dx += Mathf.Max(1, r / 4))
            {
                Consider(new Vector2I(c.X + dx, c.Y - 2 * r));
                Consider(new Vector2I(c.X + dx, c.Y + 2 * r));
            }
            for (int dy = -2 * r; dy <= 2 * r; dy += Mathf.Max(1, r / 2))
            {
                Consider(new Vector2I(c.X - r, c.Y + dy));
                Consider(new Vector2I(c.X + r, c.Y + dy));
            }
            // Once something is found, finish this ring and one more, then stop.
            if (best != null && r > 2 && bestD < r * 64f) break;
        }
        return best;
    }

    public float RapportWith(Agent a) => Rapport.GetValueOrDefault(a, 0f);
    public void ChangeRapport(Agent a, float delta) => Rapport[a] = Mathf.Clamp(RapportWith(a) + delta, -100f, 100f);

    // Somebody steps into a dead leader's place (design doc 9.3.3): the
    // most ambitious clerk becomes Factor; the band's senior hunter, chief.
    private void Succession()
    {
        if (Role == Role.Factor)
        {
            Npc heir = null;
            foreach (var n in Sim.Npcs) if (n.Role == Role.Clerk && !n.IsDead && (heir == null || n.Personality.Ambition > heir.Personality.Ambition)) heir = n;
            if (heir == null) foreach (var n in Sim.Npcs) if (n.Role == Role.Labourer && !n.IsDead) { heir = n; break; }
            if (heir == null) { Sim.Notice("There is no one left to take charge of the post."); return; }
            heir.Role = Role.Factor;
            heir.DisplayName = "Mr. " + heir.DisplayName.Replace(" (clerk)", "") + " (acting Factor)";
            heir.ResetBehaviour(); heir.Brain.Clear(heir);
            heir.BedCell = BedCell; heir.WorkCell = WorkCell;
            if (heir._label != null) heir._label.Text = heir.DisplayName;
            Sim.Notice($"{heir.DisplayName} takes charge of the post.");
        }
        else if (Role == Role.CreeChief)
        {
            Npc heir = null;
            foreach (var n in Sim.Npcs) if (n.Role == Role.CreeHunter && !n.IsDead && (heir == null || n.Personality.Ambition > heir.Personality.Ambition)) heir = n;
            if (heir == null) return;
            heir.Role = Role.CreeChief;
            heir.DisplayName += " (chief)";
            heir.ResetBehaviour(); heir.Brain.Clear(heir);
            foreach (var child in Children) { child.Father = heir; heir.Children.Add(child); }
            if (heir._label != null) heir._label.Text = heir.DisplayName;
            Sim.Notice($"{heir.DisplayName} now leads the band.");
        }
        int hbcAlive = 0;
        foreach (var n in Sim.Npcs) if (n.Faction == Faction.HBC && !n.IsDead && !n.IsFemale) hbcAlive++;
        if (hbcAlive == 0 && Faction == Faction.HBC) Sim.Notice("York Factory has no men left. The post has fallen.");
    }

    // ---- death ------------------------------------------------------------

    protected override void OnDeath()
    {
        base.OnDeath();
        Brain.Clear(this);
        StopMoving();
        if (_sprite != null)
        {
            _sprite.Play("idle");
            _sprite.Rotation = Mathf.Pi / 2f;
            _sprite.Modulate = new Color(0.6f, 0.6f, 0.65f, 0.95f);
            _sprite.Position += new Vector2(30, -10);
        }
        if (_label != null) _label.Text = $"{DisplayName} (dead)";
        CollisionLayer = 0; CollisionMask = 0;
        AddToGroup("corpses");
        Sim.Unregister(this);
        Sim.Notice($"{DisplayName} has died.");
        if (Spouse is Npc widow) widow.Spouse = null;
        Succession();
        // Bodies don't lie there forever.
        GetTree().CreateTimer(GameCalendar.Instance.SecondsPerGameHour * 48f).Timeout += () => { if (IsInstanceValid(this)) QueueFree(); };
    }
}
