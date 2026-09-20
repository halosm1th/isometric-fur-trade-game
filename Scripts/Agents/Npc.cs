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
    NwcPartner, Voyageur,
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
    // Stopped to talk to the player; movement pauses.
    public bool Conversing;
    // Whom we're walking with, if anyone (FollowGoal).
    public Agent Following;
    public bool IsFemale;

    // Kinship for the marriage system (design doc 8).
    public Npc Father;
    public Agent Spouse;
    public readonly List<Npc> Children = new();

    // What this person knows (design doc 11.2), capped and fading.
    public readonly List<MemoryEntry> Memories = new();
    private float _gossipTimer;

    public void Remember(MemoryEntry e)
    {
        foreach (var m in Memories) if (m.Key == e.Key) return;
        Memories.Add(e);
        // Personality filters (design doc 11.4): what you think of the actor.
        if (e.Actor != null && e.Actor != this && e.Weight >= 1f && e.Text.Contains(" robbed ") || e.Text.Contains(" killed ") || e.Text.Contains(" struck "))
        {
            if (e.Actor != null && e.Actor != this) ChangeRapport(e.Actor, -6f * e.Weight / e.Directness);
        }
        if (Memories.Count > 24) Memories.RemoveAt(0);
    }

    // Standing about: swap a story with whoever's next to you (10.4).
    private void Gossip(float dt)
    {
        _gossipTimer -= dt;
        if (_gossipTimer > 0f || Memories.Count == 0) return;
        _gossipTimer = (float)GD.RandRange(4.0, 9.0) / Mathf.Max(0.2f, Personality.Talkative);
        foreach (var a in Sim.AgentsWithin(GlobalPosition, 140f))
        {
            if (a is not Npc other || other == this || other.IsDead) continue;
            var m = Memories[(int)(GD.Randi() % (uint)Memories.Count)];
            if (m.Directness >= 4) continue;   // too garbled to pass on
            other.Remember(m.Copy());
            break;
        }
    }

    // Fast-moving individual rapport with other agents (design doc 3.6).
    public readonly Dictionary<Agent, float> Rapport = new();
    // Worth of everything the player has given this person (bride-price etc.).
    public float GiftsFromPlayerMb;

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

    private Color _tint = Colors.White;
    public string FramesPath = "res://Scenes/Player/PlayerFrames.tres";
    public Color Tint = Colors.White;
    public float SpriteScale = 0.8f;

    public void SetAppearance(SpriteFrames frames, Color tint, float scale)
    {
        FramesPath = frames.ResourcePath; Tint = tint; SpriteScale = scale;
        ApplyAppearance();
    }

    // Puppets rebuild their look from the replicated description.
    public void ApplyAppearance()
    {
        if (_sprite == null) return;
        _tint = Tint;
        _sprite.SpriteFrames = GD.Load<SpriteFrames>(FramesPath);
        _sprite.Modulate = Tint;
        _sprite.Scale = Vector2.One * SpriteScale;
        _sprite.Play("idle");
        if (_label != null) _label.Text = DisplayName;
    }

    public override string CurrentAnimation => _sprite?.Animation ?? "idle";
    public override bool FlipH => _sprite?.FlipH ?? false;

    private Vector2 _netTarget; private bool _netHasTarget;
    public override void ApplySnapshot(Vector2 pos, string anim, bool flip)
    {
        _netTarget = pos; _netHasTarget = true;
        if (_sprite != null && _sprite.SpriteFrames != null && _sprite.SpriteFrames.HasAnimation(anim) && _sprite.Animation != anim) _sprite.Play(anim);
        if (_sprite != null) _sprite.FlipH = flip;
    }

    public override void PuppetDie()
    {
        if (_sprite != null)
        {
            if (_sprite.SpriteFrames.HasAnimation("death")) _sprite.Play("death");
            else { _sprite.Rotation = Mathf.Pi / 2f; _sprite.Position += new Vector2(30, -10); }
            _sprite.Modulate = new Color(0.7f, 0.7f, 0.72f, 0.95f);
        }
        if (_label != null) _label.Text = $"{DisplayName} (dead)";
        Needs.Health = 0f;
        CollisionLayer = 0; CollisionMask = 0;
        AddToGroup("corpses");
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        float dt = (float)delta;
        var cal = GameCalendar.Instance;
        if (cal == null) return;

        // A client's copy just glides toward the last snapshot.
        if (!Net.Net.IsServer)
        {
            if (_netHasTarget) GlobalPosition = GlobalPosition.Lerp(_netTarget, Mathf.Clamp(dt * 12f, 0f, 1f));
            return;
        }

        if (IsDead)
        {
            Velocity = Vector2.Zero;
            return;
        }

        float hours = cal.DeltaGameHours;
        _sinceEatAnim += dt;

        Sleeping = false;   // SleepGoal re-asserts this every tick it's lying down
        Unaware = Brain.Tier0Goal() == null;   // busy with the day's work, not on guard
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
        Gossip(dt);
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
        // A man badly hurt in a fight with other men breaks off, unless
        // he's the reckless sort. Animals you can't outrun.
        if (current is AttackGoal ag && ag.Target is not Animal && Needs.Health < 50f && Personality.Courage < 0.75f && LastAttacker != null && IsInstanceValid(LastAttacker))
        {
            Brain.PopTier0(this);
            Brain.Push(this, new FleeGoal(LastAttacker, 10f));
            return;
        }
        if (current is AttackGoal || current is FleeGoal) return;
        var top = Brain.Top;
        if (top is FleePointGoal || top is FightFireGoal || top is BrawlGoal) return;

        // Being attacked: fight back if there's a chance, else run.
        if (LastAttackedAgo < 2f && LastAttacker != null && IsInstanceValid(LastAttacker) && !LastAttacker.IsDead)
        {
            // A quarrel among your own people stays a quarrel: fists, not muskets.
            if (LastAttacker is Npc kin && kin.Faction == Faction) { Brain.Push(this, new BrawlGoal(kin)); return; }
            bool armed = Weapon != null;
            bool brave = Personality.Courage > 0.35f || armed;
            if (brave && Needs.Health > 30f) Brain.Push(this, new AttackGoal(LastAttacker));
            else Brain.Push(this, new FleeGoal(LastAttacker, 8f));
            return;
        }
        // Walking with someone: their fight is your fight.
        if (Following != null && IsInstanceValid(Following) && !Following.IsDead && Following.LastAttackedAgo < 2f && Following.LastAttacker != null && IsInstanceValid(Following.LastAttacker) && !Following.LastAttacker.IsDead && Following.LastAttacker != this)
        {
            if (Weapon != null && Personality.Courage > 0.15f) { Brain.Push(this, new AttackGoal(Following.LastAttacker)); return; }
        }
        // Fire: run from flames at your feet; men of the house go for water.
        var fire = World.Fire.I;
        if (fire != null && fire.Count > 0)
        {
            var near = fire.NearestBurning(GlobalPosition, 110f);
            if (near != null) { Brain.Push(this, new FleePointGoal(IsoGrid.ToWorld(near.Value), 3f)); return; }
            if (HomeStores != null && !IsFemale && !Brain.Has<FightFireGoal>() && fire.NearestBurning(IsoGrid.ToWorld(HomeStores.AccessCell), 700f) != null)
            {
                Brain.Push(this, new FightFireGoal()); return;
            }
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
        if (Sleeping || Conversing) { Velocity = Vector2.Zero; return; }
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

        // Time is skipping (sleeping, waiting) or running so fast that
        // physics would tunnel: resolve the walk lazily.
        if (skipping || motion > 8f)
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
        if (HurtFlash > 0f) _sprite.Modulate = new Color(1f, 0.45f, 0.45f); else if (_sprite.Modulate.G < 0.5f) _sprite.Modulate = _tint;
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

    // Getting hit raises the alarm: armed friends nearby come running,
    // and everyone of your people thinks less of the attacker.
    public override void TakeDamage(float amount, Agent from)
    {
        base.TakeDamage(amount, from);
        if (from == null || from == this || IsDead) return;
        if (from is Npc same && same.Faction == Faction) return;   // a brawl, not an attack on the house
        if (from is not Animal) Chronicle.Event($"{from.Describe()} struck {DisplayName}.", GlobalPosition, from, DisplayName, 1f);
        foreach (var a in Sim.AgentsWithin(GlobalPosition, 500f))
        {
            if (a is not Npc ally || ally == this || ally.IsDead || ally.Faction != Faction || ally == from) continue;
            ally.ChangeRapport(from, -10f);
            if (ally.Brain.Tier0Goal() is AttackGoal || ally.Brain.Tier0Goal() is FleeGoal) continue;
            if (ally.Weapon != null && ally.Personality.Courage > 0.25f) ally.Brain.Push(ally, new AttackGoal(from));
            else if (ally.Personality.Courage < 0.3f) ally.Brain.Push(ally, new FleeGoal(from, 6f));
        }
        if (from is Player.PlayerController attacker && Faction == Faction.HBC) attacker.Standing -= 5f;
        // Blood between the companies' servants is what the rivalry feeds on.
        if (Economy.Rivalry.AreRivals(from.Faction, Faction)) Economy.Rivalry.Raise(from.Raiding ? 2f : 6f, $"{from.Describe()} struck {DisplayName}");
    }

    // The men who carry muskets for the house; leaders, clerks and women don't turn out.
    public bool IsFighter => !IsFemale && Weapon != null && (Role == Role.Labourer || Role == Role.Voyageur || Role == Role.CreeHunter || Role == Role.Sailor);

    // Someone robbing our house is an enemy while they're at it - to the
    // armed men, and only if the house has the numbers (design doc 15.1).
    public override bool IsHostileTo(Agent other)
    {
        if (base.IsHostileTo(other)) return true;
        if (other.Raiding && other.RaidTarget == Faction && IsFighter) return Economy.Rivalry.HouseHasOdds(Faction, other.GlobalPosition);
        return false;
    }

    // A voyageur carried two ninety-pound pieces over a portage.
    public override float BaseCarryLbs => Role == Role.Voyageur ? 110f : Role == Role.Labourer || Role == Role.CreeHunter ? 65f : IsFemale ? 45f : 50f;

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

    // A body restored from a save: dead where it lay, no alarm.
    public void DieQuietly() { Needs.Health = 0f; OnDeath(); }

    protected override void OnDeath()
    {
        base.OnDeath();
        Brain.Clear(this);
        StopMoving();
        if (_sprite != null)
        {
            if (_sprite.SpriteFrames.HasAnimation("death"))
            {
                _sprite.Play("death");
            }
            else
            {
                _sprite.Play("idle");
                _sprite.Rotation = Mathf.Pi / 2f;
                _sprite.Position += new Vector2(30, -10);
            }
            _sprite.Modulate = new Color(0.7f, 0.7f, 0.72f, 0.95f);
        }
        if (_label != null) _label.Text = $"{DisplayName} (dead)";
        CollisionLayer = 0; CollisionMask = 0;
        AddToGroup("corpses");
        Sim.Unregister(this);
        Sim.Notice($"{DisplayName} has died.");
        string how = LastAttacker != null && LastAttackedAgo < 30f ? $"{DisplayName} was killed by {LastAttacker.Describe()}." : $"{DisplayName} died of want.";
        Chronicle.Event(how, GlobalPosition, LastAttacker != null && LastAttackedAgo < 30f ? LastAttacker : null, DisplayName, 2f);
        if (Faction == Faction.Cree && LastAttacker != null && LastAttackedAgo < 30f) Sim.Band?.OnMemberKilledBy(LastAttacker);
        if (Spouse is Npc widow) widow.Spouse = null;
        Succession();
        // Bodies don't lie there forever.
        GetTree().CreateTimer(GameCalendar.Instance.SecondsPerGameHour * 48f).Timeout += () => { if (IsInstanceValid(this)) QueueFree(); };
    }
}
