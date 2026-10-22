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
    Factor, Clerk, Labourer, Woman, Postmaster,
    CreeChief, CreeHunter, CreeWoman, CreeDaughter,
    Sailor, Wife,
    NwcPartner, Voyageur,
    // The towns (World/Towns).
    Farmer, Merchant, Artisan, Soldier, Officer, Priest, Child,
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
    // Away with a brigade (design doc 12.1): not at the post, not on the
    // day's orders, and fed out of the canoes rather than out of the store.
    public bool Away;
    public string AwayWith = "";
    public World.Convoy Convoy;   // a hand of a brigade on the country's rivers (World/ConvoyView.cs): real only while someone is near, never saved

    // Seconds of being on his guard: a shot heard, a blow taken, a fight on.
    // While it runs down he cannot be taken unawares (design doc 15.6).
    public float Alerted;
    // Which ring of the simulation this person is in (design doc 18.2).
    public Core.Tier Tier = Core.Tier.Live;
    public Personality Personality = Personality.Random();
    public readonly Brain Brain = new();

    // Where they sleep, and whose stores they draw on.
    public Vector2I BedCell;
    public Vector2I WorkCell;
    // The household he lives from; it keeps the list of its own people.
    public Household HomeStores
    {
        get => _homeStores;
        set { if (_homeStores == value) return; _homeStores?.Members.Remove(this); _homeStores = value; value?.Members.Add(this); }
    }
    private Household _homeStores;
    public bool Sleeping;
    // Stopped to talk to the player; movement pauses.
    public bool Conversing;
    // Whom we're walking with, if anyone (FollowGoal).
    public Agent Following;
    // Held by the other company (design doc 9.1): walked home, kept a few
    // days, then turned loose. Their goods stay behind.
    public Economy.FortLedger HeldBy;
    public int HeldUntilDay = -1;
    private float _grudgeTimer;
    public bool IsFemale;

    // Kinship for the marriage system (design doc 8).
    public Npc Father;
    // A child of the player carries his name rather than a node reference.
    public string FatherName = "";
    public Agent Spouse;
    public readonly List<Npc> Children = new();

    // What this person knows (design doc 11.2), capped and fading.
    public readonly List<MemoryEntry> Memories = new();
    private float _gossipTimer;

    public void Remember(MemoryEntry e)
    {
        foreach (var m in Memories) if (m.Key == e.Key) return;
        // A made-up story meets someone who knows better (design doc P.10).
        if (e.Liar != null)
        {
            string why = Lies.SeesThrough(this, e, out bool quiet);
            if (why != null) { if (!quiet && Recalled.Add("exposed|" + e.Key)) Lies.Exposed(this, e, why); return; }
        }
        Memories.Add(e);
        // Personality filters (design doc 11.4): what you think of the actor.
        if (e.Actor != null && e.Actor != this && (e.Text.Contains(" robbed ") || e.Text.Contains(" killed ") || e.Text.Contains(" struck ")))
            ChangeRapport(e.Actor, -6f * e.Weight / e.Directness);
        Lies.Consequences(this, e);
        Friends.Heard(this, e);
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
            if (!Friends.WillRepeat(this, m, other)) break;   // a friend's misdeeds stay unsaid
            other.Remember(m.Retell(this));
            break;
        }
    }

    // Saying things out loud (design doc P.4, P.9): to whoever's beside you,
    // player or not. What's been raised with whom, and the dead mourned.
    public readonly HashSet<string> Recalled = new();
    public readonly Dictionary<string, int> Mourned = new();
    public readonly Dictionary<BarkKind, int> LastBarkDay = new();
    // At the household fire for the evening (HearthGoal), and things kept
    // from the dead: (what, whose it was).
    public World.Hearth AtHearth;
    public Vector2I? HearthSeat;
    private readonly Queue<string> _recentLines = new();
    private float _barkTimer = (float)GD.RandRange(6.0, 30.0);
    private string _reply; private float _replyIn = -1f;
    private float _friendTimer = (float)GD.RandRange(1.0, 3.0), _defendTimer;

    public void QueueReply(string text, float delay)
    {
        if (string.IsNullOrEmpty(text)) return;
        _reply = text; _replyIn = delay;
    }

    private void Chatter(float dt, GameCalendar cal)
    {
        if (_replyIn >= 0f)
        {
            _replyIn -= dt;
            if (_replyIn < 0f && _reply != null) { if (!Sleeping) Bark(_reply); _reply = null; }
        }
        _barkTimer -= dt;
        if (_barkTimer > 0f) return;
        _barkTimer = (float)GD.RandRange(25.0, 60.0) / Mathf.Lerp(0.5f, 1.5f, Personality.Talkative);
        if (AtHearth != null) _barkTimer *= 0.3f;   // the fire is where the talk is
        if (Sleeping || Conversing || Tier != Core.Tier.Live || cal.IsSkipping || HeldBy != null) return;
        if (Brain.Tier0Goal() is AttackGoal or FleeGoal) return;
        // One at a time: if someone beside you has just spoken, wait your turn.
        foreach (var a in Sim.AgentsWithin(GlobalPosition, Barks.HearRadius)) if (a != this && a.BarkedWithin(5f)) { _barkTimer = 3f; return; }
        var listener = Barks.ListenerFor(this);
        var line = Barks.Compose(this, listener);
        if (line.IsEmpty || _recentLines.Contains(line.Text)) return;
        _recentLines.Enqueue(line.Text); if (_recentLines.Count > 5) _recentLines.Dequeue();
        if (listener != null) FaceToward(listener.GlobalPosition);
        Bark(line.Text);
        Barks.Apply(this, listener, line);
    }

    // Fast-moving individual rapport with other agents (design doc 3.6).
    public readonly Dictionary<Agent, float> Rapport = new();
    // Worth of everything the player has given this person (bride-price etc.).
    public float GiftsFromPlayerMb;
    // How many times each person has bent our ear today: the first talk
    // warms, the fifth is a bore (design doc 3.6).
    public readonly Dictionary<Agent, int> TalkedToday = new();
    private int _talkDay = -1;
    public float Warm(Agent who, float amount)
    {
        int day = GameCalendar.Instance?.Day ?? 0;
        if (day != _talkDay) { _talkDay = day; TalkedToday.Clear(); }
        int n = TalkedToday.GetValueOrDefault(who);
        TalkedToday[who] = n + 1;
        float gain = n >= 4 ? 0f : amount / (1f + n);
        if (gain != 0f) ChangeRapport(who, gain);
        return gain;
    }

    // A standing order from a superior (design doc 3.9): the current task
    // chain to run when nothing more urgent is going on.
    public System.Func<Npc, Goal> AssignedTask;
    public string AssignedTaskName = "";
    public bool AssignedTaskIsHunt;
    // A company man's standing with his master (design doc 3.6): what the
    // year-end report says of him. Rises with work done, falls with refusals.
    public float Standing = 20f;
    // Something to tell the leader when next at the post (design doc 3.9 step 6).
    public string PendingReport; public string PendingReportSite; public Vector2I? PendingReportCell;

    private AnimatedSprite2D _sprite;
    private SpriteAnims _anims;
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
        if (_sprite != null) _anims = new SpriteAnims(_sprite);
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
        if (Net.Net.IsServer)
        {
            string sheet = Appearance.For(this);
            if (sheet == null && Appearance.IsChildSheet(FramesPath))
                sheet = IsFemale ? "res://Scenes/Npc/CountryWifeFrames.tres" : "res://Scenes/Npc/LabourerFrames.tres";
            // Real art needs no tint to tell it apart.
            if (sheet != null && sheet != FramesPath) { FramesPath = sheet; Tint = Colors.White; }
        }
        _tint = Tint;
        _sprite.SpriteFrames = GD.Load<SpriteFrames>(FramesPath);
        _sprite.Modulate = Tint;
        _sprite.Scale = Vector2.One * SpriteScale;
        // Frames stand on the bottom edge of their canvas, centred; canvases differ by sheet.
        var first = _sprite.SpriteFrames.GetFrameTexture("idle", 0);
        if (first != null) _sprite.Offset = new Vector2(-first.GetWidth() / 2f, -first.GetHeight() + 2f);
        _sprite.Play("idle");
        if (_label != null) _label.Text = DisplayName;
    }

    public override string CurrentAnimation => _anims?.Current ?? "idle";
    public override bool FlipH => _sprite?.FlipH ?? false;

    private Vector2 _netTarget; private bool _netHasTarget;
    public override void ApplySnapshot(Vector2 pos, string anim, bool flip)
    {
        _netTarget = pos; _netHasTarget = true;
        if (_anims != null && _anims.Has(anim)) _anims.Play(anim);
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

    // A person walks every frame, but thinks - his needs, what he notices,
    // what he does next - every frame only near the player, on his guard,
    // fighting, talking or led; further off every third frame, and out at
    // the edge of sight every sixth, each at his own turn, with the whole of
    // the time since. A town of eight hundred in sight is thought a few
    // score at a time.
    private double _thinkAcc;
    private int _thinkFrames = GD.RandRange(0, 2);
    private bool _canMove;
    private int ThinkEvery()
    {
        if (!Net.Net.IsServer || Alerted > 0f || Conversing || Following != null || HeldBy != null) return 1;
        if (Brain.Tier0Goal() is AttackGoal or FleeGoal) return 1;
        var p = Sim.Player;
        if (p == null || !IsInstanceValid(p)) return 3;
        float d2 = p.GlobalPosition.DistanceSquaredTo(GlobalPosition);
        return d2 < 1500f * 1500f ? 1 : d2 < 3000f * 3000f ? 3 : 6;
    }

    // A name over his head only within a few strides of the player: a whole
    // street's names at once is a smear nobody can read, and dear to draw.
    private const float NameRange = 700f;
    private void NameNearby()
    {
        if (_label == null) return;
        var p = Sim.Player;
        bool near = p != null && IsInstanceValid(p) && p.GlobalPosition.DistanceSquaredTo(GlobalPosition) < NameRange * NameRange;
        if (_label.Visible != near) _label.Visible = near;
    }

    public override void _PhysicsProcess(double delta)
    {
        ulong t0 = Time.GetTicksUsec();
        _thinkAcc += delta; _thinkFrames++;
        if (_thinkFrames >= ThinkEvery())
        {
            StepScale = _thinkFrames;
            double d = _thinkAcc;
            _thinkAcc = 0; _thinkFrames = 0;
            PhysicsStep(d);
            StepScale = 1f;
        }
        if (_canMove) Move((float)delta, GameCalendar.Instance?.IsSkipping ?? false);
        NameNearby();
        PerfUsec += Time.GetTicksUsec() - t0;
    }
    // Dev (--perf): time spent in every NPC's step.
    public static ulong PerfUsec;

    private void PhysicsStep(double delta)
    {
        _canMove = false;
        base._PhysicsProcess(delta);
        float dt = (float)delta;
        var cal = GameCalendar.Instance;
        if (cal == null) return;
        // A day that passes in a couple of seconds is a day nobody could have
        // walked anywhere in (design doc 18.2). The daily batch is already
        // resolving everyone's work in numbers at that speed, so a live body
        // re-running searches and paths every frame is doing it twice, for an
        // audience that cannot see him. He keeps his body and his position;
        // only the thinking stops until the clock is a man's again.
        if (cal.MotionScale > GameCalendar.LazyMotion || cal.IsSkipping)
        {
            if (Brain.Tier0Goal() == null) { StopMoving(); return; }
        }

        // On his guard, or not (design doc 15.6). A man at work with his back
        // to the bush can be taken; one who has heard a shot cannot.
        if (Alerted > 0f) Alerted -= dt;
        if (LastAttackedAgo < 20f || Brain.Tier0Goal() is AttackGoal or FleeGoal) Alerted = 25f;
        Unaware = Alerted <= 0f && !Conversing;

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

        float hours = cal.DeltaGameHours * StepScale;
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
        _steer = Vector2.Zero;   // a goal that steers says so again each step
        Brain.Tick(this, dt, hours);

        _canMove = true;   // the walk itself is every frame (_PhysicsProcess)
        ZIndex = Sleeping ? 1 : 0;   // a sleeper lies on the bunk, not under it
        Animate();
        Gossip(dt);
        Chatter(dt, cal);
        _friendTimer -= dt;
        if (_friendTimer <= 0f)
        {
            _friendTimer = 3f;
            if (!Sleeping && Tier == Core.Tier.Live && HeldBy == null) { Friends.Act(this); Lies.Consider(this); Favours.Consider(this); }   // talking to you is when he'd tell you
        }
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
    // A body sliding against the world (steering in a fight or a chase) is a
    // physics body; one walking a path that already goes round the walls is
    // only stepped, and costs the physics engine nothing. Nobody finds a
    // person by a physics query (Sim.AgentsWithin does that), so nothing is
    // lost. A dead man's body stays out of it for good.
    private bool _solid = true;
    private void Solid(bool on)
    {
        if (on == _solid || IsDead) return;
        _solid = on;
        CollisionLayer = on ? 2u : 0u;
        CollisionMask = on ? 5u : 0u;
    }

    private float _threatTimer = (float)GD.RandRange(0.0, 0.25);
    private Agent _threat;

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
        // A blood debt (design doc 7.4): the man who owes it, in sight, and a hunter's nerve.
        if (_grudgeTimer <= 0f)
        {
            _grudgeTimer = 2f;
            foreach (var g in Grudges.HeldBy(DisplayName))
            {
                foreach (var a in Sim.AgentsWithin(GlobalPosition, 450f))
                {
                    if (a.IsDead || a.Describe() != g.Against) continue;
                    var pursue = Grudges.Pursue(this, a);
                    if (pursue != null) { Sim.Notice($"{DisplayName} sees {a.Describe()} and remembers {g.Victim}."); if (a is Player.PlayerController gp) Core.Reveal.Lands(gp, $"{DisplayName} is coming for you over {g.Victim}.", $"killed {g.Victim}", 1.5f); Brain.Push(this, pursue); return; }
                }
            }
        }
        _grudgeTimer -= 1f / 60f;
        // A friend set upon: he comes in on their side (design doc P.8).
        _defendTimer -= 1f / 60f;
        if (_defendTimer <= 0f)
        {
            _defendTimer = 0.3f;
            var foe = Friends.FightFor(this);
            if (foe != null) { Brain.Push(this, new AttackGoal(foe)); return; }
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
        // Looked for four times a second, every frame once he is on his guard.
        _threatTimer -= 1f / 60f;
        if (_threatTimer <= 0f || Alerted > 0f) { _threatTimer = 0.25f; _threat = NearestHostile(220f); }
        var threat = _threat != null && IsInstanceValid(_threat) && !_threat.IsDead ? _threat : null;
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

    // A long way is worked out over a few frames (Pathfinder.Search): while
    // it is, he stands and thinks, and counts as having somewhere to go.
    private Pathfinder.Search _search;

    public bool SetDestination(Vector2I cell)
    {
        var paths = Sim.Paths;
        if (paths == null) return false;
        // Asked again for where he is already working out the way to: carry on.
        if (_search != null && _search.Destination == cell) return true;
        DropSearch();
        var start = Cell;
        MoveFailed = false;
        _repaths = 0;
        var s = paths.Begin(start, cell);
        if (!s.Done)
        {
            _search = s;
            _path.Clear(); _pathIndex = 0; _destination = cell;
            Arrived = false; _stuckTime = 0f;
            return true;
        }
        var path = s.Result;
        paths.Release(s);
        return TakePath(path, start, cell);
    }

    private bool TakePath(List<Vector2I> path, Vector2I start, Vector2I cell)
    {
        var paths = Sim.Paths;
        if (path.Count == 0)
        {
            // Already there, or unreachable.
            if (start == cell || (paths?.NearestWalkable(cell) is Vector2I nw && nw == start)) { Arrived = true; _path.Clear(); return true; }
            MoveFailed = true; Arrived = false; _path.Clear();
            return false;
        }
        _path = path; _pathIndex = 0; _destination = cell;
        Arrived = false; _stuckTime = 0f;
        return true;
    }

    private void DropSearch()
    {
        if (_search == null) return;
        Sim.Paths?.Release(_search);
        _search = null;
    }

    // Work on a way still being worked out. True once he has his answer
    // (a path, arrived, or failed); false while he is still thinking.
    private bool SettleSearch(bool now)
    {
        if (_search == null) return true;
        var paths = Sim.Paths;
        if (paths == null) { _search = null; return true; }
        if (now) paths.Complete(_search);
        else if (!paths.Continue(_search)) return false;
        var path = _search.Result; var cell = _search.Destination;
        DropSearch();
        TakePath(path, Cell, cell);
        return true;
    }

    // Walking a path just now (or working out the way to walk).
    public bool HasPath => _search != null || _pathIndex < _path.Count;

    public void StopMoving()
    {
        DropSearch();
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
        if (!HasPath || Arrived || (_destination != cell && _chaseRepath <= 0f))
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
        if (Sleeping || Conversing) { Velocity = Vector2.Zero; Solid(false); return; }
        float motion = GameCalendar.Instance?.MotionScale ?? 1f;

        // Still working out the way: a little more of it this frame. When
        // time is skipping the walk is resolved at once, so the way is too.
        if (_search != null && !SettleSearch(skipping || motion > GameCalendar.LazyMotion) && _steer == Vector2.Zero) { Velocity = Vector2.Zero; return; }

        if (_steer != Vector2.Zero)
        {
            IsSprinting = _steerRun;
            Velocity = _steer * EffectiveSpeed * motion * GroundFactor(out _);
            Solid(true);
            MoveAndSlide();
            return;
        }

        if (_path.Count == 0 || _pathIndex >= _path.Count)
        {
            Velocity = Vector2.Zero;
            Solid(false);
            if (_path.Count > 0) { Arrived = true; _path.Clear(); }
            return;
        }

        // Time is skipping (sleeping, waiting) or running so fast that
        // physics would tunnel: resolve the walk lazily.
        if (skipping || motion > GameCalendar.LazyMotion)
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
        float step = EffectiveSpeed * motion * (Mounted ? GroundFactor(out _) : 1f);
        // Don't overshoot the waypoint when time is running fast.
        if (step * dt > to.Length()) step = to.Length() / Mathf.Max(dt, 1e-4f);
        Velocity = to.Normalized() * step;
        // The path already goes round the walls and through the doors, so a
        // walker is stepped along it; sliding against the world is kept for
        // steering (a fight, a chase), where there is no path to trust.
        Solid(false);
        GlobalPosition += Velocity * dt;
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
        // In the saddle he sits: the horse does the walking.
        if (Mounted)
        {
            if (v.LengthSquared() >= 4f) { _sprite.FlipH = v.X < 0f; _facing = Mathf.Abs(v.X) > Mathf.Abs(v.Y) * 1.6f ? "walk_side" : v.Y < -0.3f * v.Length() ? "walk_back" : "walk"; }
            target = _facing == "walk_side" ? "idle_side" : _facing == "walk_back" ? "idle_back" : "idle";
            if (!_anims.Has(target)) target = "idle";
            _anims.Play(target);
            _sprite.SpeedScale = 1f;
            return;
        }
        if (v.LengthSquared() < 4f)
        {
            // Stood still, he stays facing the way he was going.
            target = _facing == "walk_side" ? "idle_side" : _facing == "walk_back" ? "idle_back" : "idle";
        }
        else
        {
            bool side = Mathf.Abs(v.X) > Mathf.Abs(v.Y) * 1.6f && _anims.Has("walk_side");
            target = side ? "walk_side" : (v.Y < -0.3f * v.Length() ? "walk_back" : "walk");
            _sprite.FlipH = v.X < 0f;
            _facing = target;
        }
        if (!_anims.Has(target)) target = "idle";
        _anims.Play(target);
        _sprite.SpeedScale = IsSprinting ? 1.6f : 1f;
    }

    private string _facing = "walk";

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
        // Fired on without warning: the house is shaken for a while, whatever
        // the numbers say (design doc 15.6).
        if (Alerted <= 0f && from != null && from is not Animal && !IsDead) Nerve.Shock(Faction, 0.3f);
        // A beaten rival is taken, not killed, when his own side isn't there to help.
        if (!IsDead && !Invulnerable && from is Npc captor && Economy.Rivalry.AreRivals(captor.Faction, Faction) && Needs.Health - amount <= 0f && HeldBy == null && !IsFemale)
        {
            var post = Economy.Rivalry.PostOf(captor.Faction);
            if (post != null && GD.Randf() < 0.85f)
            {
                Needs.Health = 10f;
                HeldBy = post; HeldUntilDay = (GameCalendar.Instance?.Day ?? 0) + 3;
                foreach (var (id, n) in new System.Collections.Generic.List<(ItemId, int)>(Inventory.All()))
                {
                    var k = Items.Items.Of(id).Kind;
                    if (k == ItemKind.Clothing || k == ItemKind.Food && n <= 1) continue;
                    Inventory.Remove(id, n); post.Stores.Add(id, n);
                }
                foreach (var m in new System.Collections.Generic.List<Message>(Mail)) { Mail.Remove(m); if (post.Leader != null && !post.Leader.IsDead) { post.Leader.Mail.Add(m); Core.Mail.Open(m, post.Leader); } }
                Brain.Clear(this);
                Brain.Push(this, new Goals.HeldPrisonerGoal(post));
                ForgetAttacker();
                Economy.Rivalry.Raise(6f, $"{DisplayName} taken prisoner");
                Sim.Notice($"{captor.DisplayName} has taken {DisplayName} prisoner.");
                Chronicle.Event($"{captor.DisplayName} took {DisplayName} prisoner.", GlobalPosition, captor, DisplayName, 2f);
                // Seeing a man of yours walked off is as bad for a party's
                // nerve as seeing him shot (design doc 15.6).
                Nerve.TheyBreak(this, captor);
                return;
            }
        }
        base.TakeDamage(amount, from);
        if (from == null || from == this || IsDead) return;
        if (from is Npc same && same.Faction == Faction) return;   // a brawl, not an attack on the house
        if (from is not Animal) Chronicle.Event($"{from.Describe()} struck {DisplayName}.", GlobalPosition, from, DisplayName, 1f);
        foreach (var a in Sim.AgentsWithin(GlobalPosition, 500f))
        {
            if (a is not Npc ally || ally == this || ally.IsDead || ally.Faction != Faction || ally == from) continue;
            ally.ChangeRapport(from, -10f);
            if (ally.Brain.Tier0Goal() is AttackGoal || ally.Brain.Tier0Goal() is FleeGoal) continue;
            // Numbers, a man giving orders, who is already hurt (design doc 15.6).
            if (Nerve.Stands(ally, from)) ally.Brain.Push(ally, new AttackGoal(from));
            else if (ally.Weapon == null || ally.Personality.Courage < 0.45f) ally.Brain.Push(ally, new FleeGoal(from, 6f));
        }
        if (from is Player.PlayerController attacker && Faction == Faction.HBC) attacker.Standing -= 5f;
        // Blood between the companies' servants is what the rivalry feeds on.
        if (Economy.Rivalry.AreRivals(from.Faction, Faction)) Economy.Rivalry.Raise(from.Raiding ? 2f : 6f, $"{from.Describe()} struck {DisplayName}");
    }

    // The men who carry muskets for the house; leaders, clerks and women don't turn out.
    public bool IsFighter => !IsFemale && Weapon != null && (Role == Role.Labourer || Role == Role.Voyageur || Role == Role.CreeHunter || Role == Role.Sailor || Role == Role.Soldier || Role == Role.Farmer);

    // Someone robbing our house is an enemy while they're at it - to the
    // armed men, and only if the house has the numbers (design doc 15.1).
    public override bool IsHostileTo(Agent other)
    {
        if (other is Player.PlayerController cap && cap.CaptiveOf != null) return false;   // a prisoner is nobody's quarry
        if (other is Npc heldMan && heldMan.HeldBy != null) return false;
        if (base.IsHostileTo(other)) return true;
        if (other.Raiding && other.RaidTarget == Faction && IsFighter) return Economy.Rivalry.HouseHasOdds(Faction, other.GlobalPosition);
        // Two nations at odds (design doc 7.2): the hunters of one see the other's as enemies in the bush.
        if (IsBandPerson && other.IsBandPerson && other.Faction != Faction && IsFighter && HomeStores is BandLedger mine && mine.AtOddsWithOtherBand && other is Npc on && on.IsFighter) return true;
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
            foreach (var n in Sim.Npcs) if (n.Role == Role.Clerk && n.HomeStores == HomeStores && !n.IsDead && (heir == null || n.Standing + n.Personality.Ambition * 10f > heir.Standing + heir.Personality.Ambition * 10f)) heir = n;
            if (heir == null) foreach (var n in Sim.Npcs) if (n.Role == Role.Labourer && n.HomeStores == HomeStores && !n.IsDead) { heir = n; break; }
            if (heir == null) { Sim.Notice("There is no one left to take charge of the post."); return; }
            heir.Role = Role.Factor;
            heir.DisplayName = "Mr. " + heir.DisplayName.Replace(" (clerk)", "") + " (acting Factor)";
            heir.ResetBehaviour(); heir.Brain.Clear(heir);
            heir.BedCell = BedCell; heir.WorkCell = WorkCell;
            if (HomeStores != null) HomeStores.Leader = heir;
            if (heir._label != null) heir._label.Text = heir.DisplayName;
            Sim.Notice($"{heir.DisplayName} takes charge of the post.");
        }
        else if (Role == Role.Postmaster)
        {
            // A post has lost its master: the men carry on under the steadiest
            // of them till the Bay sends someone (Delegation posts a clerk).
            // Whichever post it was - there are more than Oxford House now.
            Npc heir = null;
            foreach (var n in Sim.Npcs) if (n.Role == Role.Labourer && n.HomeStores == HomeStores && !n.IsDead && (heir == null || n.Standing > heir.Standing)) heir = n;
            string where = HomeStores == Sim.Outpost ? "Oxford House" : (HomeStores?.Title ?? "the post").Replace(" stores", "").Replace("the ", "");
            if (HomeStores != null) HomeStores.Leader = null;
            Sim.Notice($"{DisplayName} is dead. {char.ToUpper(where[0])}{where.Substring(1)} is without a master{(heir != null ? $"; {heir.DisplayName} keeps the men together" : "")}.");
        }
        else if (Role == Role.CreeChief)
        {
            Npc heir = null;
            foreach (var n in Sim.Npcs) if (n.Role == Role.CreeHunter && n.HomeStores == HomeStores && !n.IsDead && (heir == null || n.Personality.Ambition > heir.Personality.Ambition)) heir = n;
            if (heir == null) return;
            heir.Role = Role.CreeChief;
            if (HomeStores != null) HomeStores.Leader = heir;
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
    public void DieQuietly() { _quietDeath = true; Needs.Health = 0f; OnDeath(); }
    private bool _quietDeath;

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
        if (!_quietDeath) Losses.Record(this);
        // Say what took him: a death nobody can account for is no use to
        // anybody (design doc P.4).
        string cause = Killer != null ? $"killed by {Killer.Describe()}"
            : IsSick ? "of the fever"
            : HasScurvy ? "of the scurvy"
            : Needs.Warmth <= 1f ? "of the cold"
            : Needs.Hunger <= 1f ? "of hunger"
            : "of want";
        Sim.Notice($"{DisplayName} has died {cause}.");
        string how = Killer != null ? $"{DisplayName} was killed by {Killer.Describe()}." : $"{DisplayName} died {cause}.";
        Chronicle.Event(how, GlobalPosition, Killer, DisplayName, 2f);
        if (IsBandPerson && Killer != null) (HomeStores as BandLedger)?.OnMemberKilledBy(Killer);
        // Fights are won before the first shot (design doc 15.6): a man down is
        // where a party's nerve usually goes.
        if (Killer != null) Nerve.TheyBreak(this, Killer);
        // Blood debts (design doc 7.4): a killing makes one, a killer's death pays one, a holder's death passes one on.
        if (Killer != null) Grudges.OnKilled(this, Killer);
        foreach (var g in new List<Grudge>(Grudges.All)) if (!g.Settled && g.Against == DisplayName) { Grudges.Settle(g, true); Sim.Notice($"{DisplayName} is dead; the debt for {g.Victim} is paid in blood."); }
        Grudges.OnHolderDied(this);
        if (Spouse is Npc widow) widow.Spouse = null;
        Succession();
        // Bodies don't lie there forever.
        GetTree().CreateTimer(GameCalendar.Instance.SecondsPerGameHour * 48f).Timeout += () => { if (IsInstanceValid(this)) QueueFree(); };
    }
}
