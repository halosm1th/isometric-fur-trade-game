using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Agents;

public enum Faction { HBC, Cree, ShipsCrew, Wild }

// Every person (and animal) in the world is one of these - the player
// included (design doc 11.1). Needs, inventory, health and the shared action
// vocabulary live here; what drives the agent (input, or a goal stack) is
// the subclass's job.
public partial class Agent : CharacterBody2D
{
    [Signal] public delegate void DiedEventHandler();
    [Signal] public delegate void DamagedEventHandler(float amount);

    public readonly Needs Needs = new();
    public readonly Inventory Inventory = new();

    [Export] public string DisplayName = "";
    [Export] public Faction Faction = Faction.HBC;
    [Export] public float MoveSpeed = 140f;

    // Running is the voyageur's pace: much faster, and it eats you alive.
    [Export] public float SprintMultiplier = 1.7f;
    public bool IsSprinting { get; protected set; }

    public bool IsDead => Needs.IsDead;
    public bool Invulnerable { get => Needs.Invulnerable; set => Needs.Invulnerable = value; }
    public bool IsSheltered { get; private set; }

    // 0..1 warmth reaching this agent from the nearest fire.
    public float HeatNear { get; private set; }

    // Fixed clothing not carried as items (animals' hides, sailors' slops).
    public float InnateWarmth = 0f;

    // Who hurt us last; drives fear and retaliation.
    public Agent LastAttacker { get; private set; }
    public float LastAttackedAgo { get; private set; } = 999f;

    private bool _deathAnnounced;
    private float _attackCooldown;

    public Vector2I Cell => IsoGrid.FromWorld(GlobalPosition);

    public override void _EnterTree()
    {
        Sim.Register(this);
    }

    public override void _ExitTree()
    {
        Sim.Unregister(this);
    }

    public override void _PhysicsProcess(double delta)
    {
        var cal = GameCalendar.Instance;
        if (cal == null) return;
        _attackCooldown = Mathf.Max(0f, _attackCooldown - (float)delta);
        LastAttackedAgo += (float)delta;
        if (IsDead) return;

        var structures = Sim.Structures;
        IsSheltered = structures?.IsSheltered(GlobalPosition) ?? false;
        HeatNear = structures?.HeatAt(GlobalPosition) ?? 0f;

        bool moving = Velocity.LengthSquared() > 1f;
        Needs.Tick(
            cal.DeltaGameHours,
            activity: !moving ? 1f : (IsSprinting ? 2.4f : 1.5f),
            sheltered: IsSheltered,
            heat: HeatNear,
            clothingWarmth: Inventory.ClothingWarmth + InnateWarmth,
            season: cal.CurrentSeason,
            daylight: cal.DaylightFactor);

        if (IsDead) OnDeath();
    }

    protected virtual void OnDeath()
    {
        if (_deathAnnounced) return;
        _deathAnnounced = true;
        Velocity = Vector2.Zero;
        EmitSignal(SignalName.Died);
    }

    public float EffectiveSpeed => MoveSpeed * Needs.CapabilityFactor * (IsSprinting ? SprintMultiplier : 1f);

    // ---- shared action vocabulary (design doc 3.3) ----

    // Eat the first food carried.
    public bool Eat()
    {
        foreach (var (id, _) in Inventory.All())
        {
            if (Items.Items.Of(id).Kind == ItemKind.Food && Consume(id)) return true;
        }
        return false;
    }

    // Eat or drink one specific item. Ale is a small meal and a warm glow.
    public bool Consume(ItemId id)
    {
        var def = Items.Items.Of(id);
        if (def.Kind != ItemKind.Food && def.Kind != ItemKind.Drink) return false;
        if (!Inventory.Remove(id)) return false;
        Needs.Hunger = Mathf.Min(100f, Needs.Hunger + def.Nutrition);
        Needs.Thirst = Mathf.Min(100f, Needs.Thirst + def.Thirst);
        if (def.Kind == ItemKind.Drink) Needs.Warmth = Mathf.Min(100f, Needs.Warmth + def.Warmth);
        return true;
    }

    // Water isn't scarce here; the cost is access (design doc 17.2). In
    // winter that means melting snow, which you pay for in warmth.
    public bool Drink()
    {
        var terrain = Sim.Terrain;
        if (terrain == null || !terrain.IsWaterNear(GlobalPosition)) return false;

        Needs.Thirst = 100f;
        if (GameCalendar.Instance.IsFrozen)
        {
            Needs.Warmth = Mathf.Max(0f, Needs.Warmth - 8f);
        }
        return true;
    }

    // Put a piece of fuel on the nearest fire, if there is one in reach.
    public bool FeedFire(ItemId id)
    {
        var def = Items.Items.Of(id);
        if (def.Kind != ItemKind.Fuel) return false;
        var hearth = Sim.Structures?.HearthNear(GlobalPosition);
        if (hearth == null || !Inventory.Remove(id)) return false;
        hearth.Feed(def.FuelHours);
        return true;
    }

    // ---- combat (design doc 15) ----

    // Innate attack for animals; humans use what they carry.
    public float NaturalDamage = 0f;
    public float NaturalRange = 60f;
    public float NaturalCooldown = 1.2f;

    public ItemId? Weapon => Inventory.BestWeapon();

    public (float damage, float range, float cooldown) AttackProfile()
    {
        var w = Weapon;
        if (w != null)
        {
            var d = Items.Items.Of(w.Value);
            return (d.Damage, d.Range, d.Cooldown);
        }
        return (NaturalDamage > 0f ? NaturalDamage : 8f, NaturalRange, NaturalCooldown);
    }

    public bool CanAttack => _attackCooldown <= 0f && !IsDead;

    public bool InAttackRange(Agent target)
    {
        var (_, range, _) = AttackProfile();
        return GlobalPosition.DistanceTo(target.GlobalPosition) <= range + 20f;
    }

    // Swing/shoot at the target if in range and ready. Returns true on a hit.
    public bool Attack(Agent target)
    {
        if (target == null || !IsInstanceValid(target) || target.IsDead || !CanAttack) return false;
        var (damage, range, cooldown) = AttackProfile();
        if (GlobalPosition.DistanceTo(target.GlobalPosition) > range + 20f) return false;
        _attackCooldown = cooldown;
        // Weak agents hit softer; ranged shots can miss.
        float mult = Needs.CapabilityFactor;
        if (range > 100f && GD.Randf() > 0.7f) { OnAttackMissed(target); return false; }
        target.TakeDamage(damage * mult, this);
        OnAttacked(target);
        return true;
    }

    protected virtual void OnAttacked(Agent target) { }
    protected virtual void OnAttackMissed(Agent target) { }

    public virtual void TakeDamage(float amount, Agent from)
    {
        if (IsDead || Invulnerable) return;
        Needs.Health = Mathf.Max(0f, Needs.Health - amount);
        LastAttacker = from;
        LastAttackedAgo = 0f;
        EmitSignal(SignalName.Damaged, amount);
        if (IsDead) OnDeath();
    }

    public bool IsHostileTo(Agent other)
    {
        if (other == null || other == this) return false;
        if (Faction == Faction.Wild || other.Faction == Faction.Wild) return true;
        return false;
    }

    public string Describe() => string.IsNullOrEmpty(DisplayName) ? Name : DisplayName;
}
