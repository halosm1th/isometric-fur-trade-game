using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Ecology;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Agents;

// A promoted animal: a real agent with needs and a small state machine.
// Prey grazes and runs; predators (and any animal starving under a site
// in scarcity) go looking for food, which near a post means people and
// their stores.
public partial class Animal : Agent
{
    public Species Species;
    public SpawnSite Site;
    public bool Hungry;

    private enum Mode { Graze, Flee, Hunt, Feed }
    private Mode _mode = Mode.Graze;
    private Vector2 _target;
    private float _modeTimer;
    private Agent _quarry;
    private AnimatedSprite2D _sprite;
    private Label _label;
    private float _hurtFlash;
    private bool _butchered;

    public SpeciesDef Def => SpeciesInfo.Of(Species);
    public bool IsPredator => Def.Diet == Diet.Predator;
    // Will it go for people? Predators always; prey only when starving.
    public bool IsDangerous => !IsDead && (IsPredator || Hungry);

    public override void _Ready()
    {
        Faction = Faction.Wild;
        var def = Def;
        DisplayName = def.Name;
        MoveSpeed = def.Speed;
        Needs.Health = def.Health;
        NaturalDamage = def.BiteDamage;
        NaturalRange = 55f;
        NaturalCooldown = 1.3f;
        InnateWarmth = 20f;   // fur: the cold doesn't touch them in this model
        Needs.Hunger = Hungry ? 25f : 80f;
        MotionMode = MotionModeEnum.Floating;

        _sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        _label = GetNodeOrNull<Label>("NameLabel");
        if (_sprite != null)
        {
            _sprite.SpriteFrames = GD.Load<SpriteFrames>(def.Frames);
            _sprite.Scale = Vector2.One * (Species switch { Species.Caribou => 2.2f, Species.Wolf => 1.5f, Species.Goose => 1.1f, _ => 1.0f });
            _sprite.Offset = Species switch { Species.Caribou => new Vector2(0, -19), Species.Wolf => new Vector2(0, -26), _ => new Vector2(0, -16) };
            _sprite.Play(Anim("idle", Vector2.Down));
        }
        if (_label != null) _label.Text = def.Name;
        _target = GlobalPosition;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        var cal = GameCalendar.Instance;
        if (cal == null) return;
        float dt = (float)delta;
        _hurtFlash = Mathf.Max(0f, _hurtFlash - dt);
        if (_sprite != null) _sprite.Modulate = _hurtFlash > 0f ? new Color(1f, 0.5f, 0.5f) : Colors.White;
        if (IsDead) { Velocity = Vector2.Zero; return; }

        // Grazers eat as they go unless the ground is eaten out; predators
        // only eat what they kill, and hunger drives the hunt.
        if (!IsPredator && !(Site?.Scarcity ?? false)) Needs.Hunger = 100f;
        if (IsPredator && Needs.Hunger < 40f) Hungry = true;
        if (Site != null && Site.Scarcity) Hungry = true;
        Needs.Thirst = 100f;

        float motion = Mathf.Min(cal.MotionScale, 8f);
        _modeTimer -= dt * motion;
        Think(dt);
        Act(dt, motion);
        Animate();
    }

    private void Think(float dt)
    {
        // Threat check for prey: any human close by.
        if (!IsPredator && !Hungry || (IsPredator && Needs.Health < Def.Health * 0.3f))
        {
            var human = NearestHuman(260f);
            if (human != null) { _mode = Mode.Flee; _quarry = human; _modeTimer = 3f; return; }
        }
        if (LastAttackedAgo < 1f && LastAttacker != null)
        {
            if (IsDangerous && Needs.Health > Def.Health * 0.3f) { _mode = Mode.Hunt; _quarry = LastAttacker; _modeTimer = 8f; return; }
            _mode = Mode.Flee; _quarry = LastAttacker; _modeTimer = 4f; return;
        }

        if (_mode == Mode.Flee && _modeTimer > 0f) return;

        if (IsDangerous)
        {
            // Look for food: a person, or (for wolves) prey animals.
            if (_quarry == null || !IsInstanceValid(_quarry) || _quarry.IsDead || _modeTimer <= 0f)
            {
                _quarry = FindQuarry();
                _modeTimer = 6f;
            }
            if (_quarry != null) { _mode = Mode.Hunt; return; }
            // Nothing in sight: drift toward the post where the food is.
            if (_modeTimer <= 0f || _mode != Mode.Graze)
            {
                var fort = IsoGrid.ToWorld(IsoGrid.Cell(WorldLayout.FortTop, WorldLayout.FortWidth / 2, WorldLayout.FortDepth / 2));
                _target = GlobalPosition.Lerp(fort, 0.25f) + new Vector2((float)GD.RandRange(-200, 200), (float)GD.RandRange(-120, 120));
                _mode = Mode.Graze; _modeTimer = 5f;
            }
            return;
        }

        // Peaceful: wander around the home site.
        if (_modeTimer <= 0f)
        {
            _modeTimer = (float)GD.RandRange(3.0, 8.0);
            var home = Site?.WorldPos ?? GlobalPosition;
            float r = Site?.RadiusPx ?? 300f;
            _target = home + new Vector2((float)GD.RandRange(-r, r), (float)GD.RandRange(-r * 0.5f, r * 0.5f));
            _mode = Mode.Graze;
        }
    }

    private Agent FindQuarry()
    {
        Agent best = null; float bestD = IsPredator ? 900f : 700f;
        foreach (var a in Sim.AgentsWithin(GlobalPosition, bestD))
        {
            if (a == this || a.IsDead) continue;
            bool human = a is not Animal;
            bool preyAnimal = a is Animal an && !an.IsPredator && IsPredator;
            if (!human && !preyAnimal) continue;
            float d = GlobalPosition.DistanceTo(a.GlobalPosition);
            // Wolves prefer prey animals to armed men.
            if (human && a.Weapon != null) d *= 1.6f;
            if (d < bestD) { bestD = d; best = a; }
        }
        return best;
    }

    private Agent NearestHuman(float radius)
    {
        Agent best = null; float bestD = radius;
        foreach (var a in Sim.AgentsWithin(GlobalPosition, radius))
        {
            if (a is Animal || a.IsDead) continue;
            float d = GlobalPosition.DistanceTo(a.GlobalPosition);
            if (d < bestD) { bestD = d; best = a; }
        }
        return best;
    }

    private void Act(float dt, float motion)
    {
        switch (_mode)
        {
            case Mode.Flee:
                if (_quarry == null || !IsInstanceValid(_quarry)) { _mode = Mode.Graze; break; }
                IsSprinting = true;
                Velocity = (GlobalPosition - _quarry.GlobalPosition).Normalized() * EffectiveSpeed * motion;
                MoveAndSlide();
                if (_modeTimer <= 0f) { _mode = Mode.Graze; _modeTimer = 0f; }
                return;
            case Mode.Hunt:
                if (_quarry == null || !IsInstanceValid(_quarry) || _quarry.IsDead)
                {
                    if (_quarry != null && IsInstanceValid(_quarry) && _quarry.IsDead) Feed(_quarry);
                    _quarry = null; _mode = Mode.Graze; break;
                }
                var to = _quarry.GlobalPosition - GlobalPosition;
                if (to.Length() <= NaturalRange + 10f)
                {
                    Velocity = Vector2.Zero;
                    if (Attack(_quarry) && _sprite != null) _sprite.Play(Anim("attack", to));
                    return;
                }
                IsSprinting = true;
                Velocity = to.Normalized() * EffectiveSpeed * motion;
                MoveAndSlide();
                return;
        }
        // Graze: amble toward the target.
        IsSprinting = false;
        var d = _target - GlobalPosition;
        if (d.Length() < 20f) { Velocity = Vector2.Zero; return; }
        Velocity = d.Normalized() * Mathf.Min(EffectiveSpeed * 0.45f * motion, d.Length() / Mathf.Max(dt, 1e-4f));
        MoveAndSlide();
        if (GetSlideCollisionCount() > 0) _modeTimer = 0f;
    }

    private void Feed(Agent kill)
    {
        Needs.Hunger = 100f;
        Hungry = false;
        if (Site != null && Site.Scarcity) Hungry = true;
    }

    protected override void OnAttacked(Agent target)
    {
        if (target.IsDead) Feed(target);
    }

    public override void TakeDamage(float amount, Agent from)
    {
        base.TakeDamage(amount, from);
        _hurtFlash = 0.15f;
    }

    private string Anim(string kind, Vector2 dir)
    {
        if (_sprite == null) return kind;
        var frames = _sprite.SpriteFrames;
        if (frames.HasAnimation(kind)) return kind;   // simple two-frame species
        string d = (dir.X >= 0f ? (dir.Y >= 0f ? "se" : "ne") : (dir.Y >= 0f ? "sw" : "nw"));
        string name = $"{kind}_{d}";
        return frames.HasAnimation(name) ? name : $"idle_{d}";
    }

    private void Animate()
    {
        if (_sprite == null) return;
        if (_sprite.Animation.ToString().StartsWith("attack") && _sprite.IsPlaying()) return;
        var v = Velocity;
        bool moving = v.LengthSquared() > 4f;
        string name = Anim(moving ? "walk" : "idle", moving ? v : _lastDir);
        if (moving) _lastDir = v;
        if (_sprite.SpriteFrames.HasAnimation("walk") && !_sprite.SpriteFrames.HasAnimation("walk_se"))
        {
            _sprite.FlipH = _lastDir.X < 0f;
        }
        if (_sprite.Animation != name) _sprite.Play(name);
        _sprite.SpeedScale = IsSprinting ? 1.5f : 1f;
    }
    private Vector2 _lastDir = Vector2.Down;

    protected override void OnDeath()
    {
        base.OnDeath();
        Velocity = Vector2.Zero;
        Site?.OnLiveDeath(this);
        if (_sprite != null)
        {
            string death = Anim("death", _lastDir);
            if (death.StartsWith("death")) _sprite.Play(death);
            else { _sprite.Rotation = Mathf.Pi / 2f; _sprite.Modulate = new Color(0.7f, 0.7f, 0.7f); }
        }
        if (_label != null) _label.Text = $"{Def.Name} (dead)";
        CollisionLayer = 0; CollisionMask = 0;
        AddToGroup("corpses");
        Sim.Unregister(this);
        GetTree().CreateTimer(GameCalendar.Instance.SecondsPerGameHour * 36f).Timeout += () => { if (IsInstanceValid(this)) QueueFree(); };
    }

    // Butcher the carcass: meat and a pelt into the given pack.
    public bool Butcher(Inventory into)
    {
        if (!IsDead || _butchered) return false;
        _butchered = true;
        SpeciesInfo.Yield(Species, into);
        if (_label != null) _label.Text = $"{Def.Name} (butchered)";
        return true;
    }
}
