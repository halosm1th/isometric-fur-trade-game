using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Agents;

public enum Faction { HBC, Cree, ShipsCrew, Wild, NWC }

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

    // Networking: identity across peers, and whether this copy is a puppet
    // (rendered from the server's snapshots, no simulation of its own).
    public int NetId;
    public bool IsPuppet;

    public virtual string CurrentAnimation => "";
    public virtual bool FlipH => false;
    public virtual void ApplySnapshot(Vector2 pos, string anim, bool flip) { GlobalPosition = pos; }
    public virtual void PuppetDie() { }
    [Export] public Faction Faction = Faction.HBC;
    [Export] public float MoveSpeed = 140f;

    // Running is the voyageur's pace: much faster, and it eats you alive.
    [Export] public float SprintMultiplier = 1.7f;
    public bool IsSprinting { get; protected set; }
    // Crouched and quiet: slower, harder for animals to notice, and a
    // first stroke on something unaware hits hard.
    public bool IsSneaking { get; protected set; }
    public float StealthFactor => IsSneaking ? 0.4f : 1f;
    public bool Unaware = false;   // set by animals while grazing

    public bool IsDead => Needs.IsDead;
    public bool Invulnerable { get => Needs.Invulnerable; set => Needs.Invulnerable = value; }
    public bool IsSheltered { get; private set; }

    // 0..1 warmth reaching this agent from the nearest fire.
    public float HeatNear { get; private set; }
    public bool HeatEnclosed { get; private set; }

    // Drink (design doc 13.4): 0..100, wears off; slows you, spoils your aim,
    // loosens your tongue. Unarmed forces fists for a brawl.
    public float Intoxication;
    public bool Unarmed;
    public bool IsDrunk => Intoxication > 40f;

    // Fever (design doc 13.5). Days left; 0 = well. Recovered people don't catch it again that year.
    public float SickDaysLeft;
    public bool IsSick => SickDaysLeft > 0f;
    public bool Immune;
    public int Age = 25;

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
        // Leaving the server's world (a body cleared away, an animal
        // demoted) means leaving every client's too.
        if (Net.Net.IsServer && NetId != 0 && !(GetTree()?.CurrentScene == null)) Net.Net.I?.AnnounceDespawn(this);
        Sim.Unregister(this);
    }

    public override void _PhysicsProcess(double delta)
    {
        var cal = GameCalendar.Instance;
        if (cal == null) return;
        if (!Net.Net.IsServer) return;   // puppets and remote copies don't simulate needs
        float scaled = (float)delta * cal.MotionScale;
        HurtFlash = Mathf.Max(0f, HurtFlash - (float)delta);
        _attackCooldown = Mathf.Max(0f, _attackCooldown - scaled);
        LastAttackedAgo += scaled;
        if (IsDead) return;

        var structures = Sim.Structures;
        IsSheltered = structures?.IsSheltered(GlobalPosition) ?? false;
        bool enclosed = false;
        HeatNear = structures?.HeatAt(GlobalPosition, out enclosed) ?? 0f;
        HeatEnclosed = enclosed;

        bool moving = Velocity.LengthSquared() > 1f;
        Intoxication = Mathf.Max(0f, Intoxication - 8f * cal.DeltaGameHours);
        if (IsSick)
        {
            SickDaysLeft -= cal.DeltaGameHours / 24f;
            Needs.Health = Mathf.Max(0f, Needs.Health - 0.35f * cal.DeltaGameHours);   // ~8 a day
            Needs.Hunger = Mathf.Max(0f, Needs.Hunger - 0.4f * cal.DeltaGameHours);
            if (SickDaysLeft <= 0f) { Immune = true; Sim.Notice($"{Describe()} is over the fever."); }
        }
        Needs.Tick(
            cal.DeltaGameHours,
            activity: !moving ? 1f : (IsSprinting ? 2.4f : 1.5f),
            sheltered: IsSheltered,
            heat: HeatNear,
            clothingWarmth: Inventory.ClothingWarmth + InnateWarmth,
            season: cal.CurrentSeason,
            daylight: cal.DaylightFactor,
            fireEnclosed: HeatEnclosed);

        if (IsDead) OnDeath();
    }

    protected virtual void OnDeath()
    {
        if (_deathAnnounced) return;
        _deathAnnounced = true;
        Velocity = Vector2.Zero;
        EmitSignal(SignalName.Died);
        Net.Net.I?.AnnounceDeath(this);
    }

    // Back from the dead (a player respawning).
    public virtual void Revive()
    {
        _deathAnnounced = false;
        Needs.Health = 100f; Needs.Hunger = Needs.Thirst = Needs.Warmth = 100f;
        LastAttacker = null; LastAttackedAgo = 999f;
        CollisionLayer = 1; CollisionMask = 1;
        RemoveFromGroup("corpses");
    }

    // A voyageur carried two 90-lb pieces on a portage; anyone else slows
    // down well before that (design doc 12).
    public float LoadFactor => Mathf.Clamp(1f - (Inventory.TotalWeightLbs - 50f) / 160f, 0.35f, 1f);
    public float DrunkFactor => Intoxication > 40f ? Mathf.Lerp(1f, 0.6f, (Intoxication - 40f) / 60f) : 1f;
    public float SickFactor => IsSick ? 0.7f : 1f;
    public float EffectiveSpeed => MoveSpeed * Needs.CapabilityFactor * LoadFactor * DrunkFactor * SickFactor * (IsSprinting ? SprintMultiplier : IsSneaking ? 0.45f : 1f);

    public void CatchFever()
    {
        if (IsSick || Immune || IsDead) return;
        SickDaysLeft = (float)GD.RandRange(6.0, 11.0);
        Sim.Notice($"{Describe()} has come down with the fever.");
    }

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
        Intoxication = Mathf.Min(100f, Intoxication + def.Intoxication);
        return true;
    }

    // Water isn't scarce here; the cost is access (design doc 17.2). In
    // winter that means melting snow, which you pay for in warmth.
    public bool Drink()
    {
        // At a fire in winter you melt snow instead of walking to the shore;
        // it costs the fire a little fuel.
        if (GameCalendar.Instance.IsFrozen && HeatNear > 0.3f)
        {
            var hearth = Sim.Structures?.HearthNear(GlobalPosition, 400f);
            if (hearth != null && hearth.IsBurning)
            {
                hearth.FuelHours = Mathf.Max(0.1f, hearth.FuelHours - 0.3f);
                Needs.Thirst = 100f;
                return true;
            }
        }
        // Carried water first.
        if (Inventory.Has(ItemId.Water)) { Consume(ItemId.Water); return true; }
        // The household's barrel, if you're at it.
        if (this is Npc npc && npc.HomeStores != null && npc.HomeStores.Stores.Has(ItemId.Water) && GlobalPosition.DistanceTo(IsoGrid.ToWorld(npc.HomeStores.AccessCell)) < 140f)
        {
            npc.HomeStores.Stores.Remove(ItemId.Water); Needs.Thirst = 100f; return true;
        }
        if (this is Player.PlayerController pc && pc.HomePost != null && pc.HomePost.Stores.Has(ItemId.Water) && GlobalPosition.DistanceTo(IsoGrid.ToWorld(pc.HomePost.AccessCell)) < 140f)
        {
            pc.HomePost.Stores.Remove(ItemId.Water); Needs.Thirst = 100f; return true;
        }
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

    // The weapon in hand: a chosen one if still carried, else the best owned.
    public ItemId? PreferredWeapon;
    public ItemId? Weapon => Unarmed ? null : PreferredWeapon != null && Inventory.Has(PreferredWeapon.Value) ? PreferredWeapon : Inventory.BestWeapon();

    // Brief red flash when hit; subclasses with a sprite read this.
    public float HurtFlash { get; private set; }

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
    // 0 = ready, 1 = just fired.
    public float CooldownFraction => _lastCooldown <= 0f ? 0f : Mathf.Clamp(_attackCooldown / _lastCooldown, 0f, 1f);
    private float _lastCooldown;

    private HealthBar _healthBar;
    protected void ShowHealthBar()
    {
        if (_healthBar == null || !IsInstanceValid(_healthBar))
        {
            _healthBar = new HealthBar { Position = new Vector2(0, -130), ZIndex = 25, ZAsRelative = false };
            AddChild(_healthBar);
        }
        _healthBar.Show(this);
    }

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
        _attackCooldown = cooldown; _lastCooldown = cooldown;
        // Weak agents hit softer; ranged shots can miss. A sneak attack on
        // something that hasn't noticed you lands like a butcher's stroke.
        float mult = Needs.CapabilityFactor;
        if (IsSneaking && target.Unaware) mult *= 2.5f;
        if (range > 100f) CombatFx.Shot(this, target.GlobalPosition + new Vector2(0, -40), Weapon == ItemId.Musket);
        // Drink spoils the aim; fever weakens the arm.
        float missChance = (range > 100f ? 0.3f : 0f) + Intoxication / 250f;
        if (IsSick) mult *= 0.6f;
        if (GD.Randf() < missChance) { OnAttackMissed(target); return false; }
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
        HurtFlash = 0.18f;
        ShowHealthBar();
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
