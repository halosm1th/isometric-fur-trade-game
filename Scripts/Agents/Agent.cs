using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Agents;

// New nations go on the end: saves store the number.
// Free: a man of no company and no garrison - a deserter on his own (PlayerController.Desert), or a freeman.
public enum Faction { HBC, Cree, ShipsCrew, Wild, NWC, Assiniboine, Ojibwe, Dene, Blackfoot, Sioux, Plateau, Coast, Missouri, Metis, American, Haudenosaunee, SaukFox, Crown, Free }

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
    // What a body has learned to do (design doc 3.10): seeded, improved by doing.
    public readonly Skills Skills = new();
    // Letters and packets carried (design doc 10.1.1). Not items: each is one particular paper.
    public readonly System.Collections.Generic.List<Message> Mail = new();
    // Things kept from the dead (design doc P.8): what, and whose it was.
    public readonly System.Collections.Generic.List<(ItemId id, string from)> Keepsakes = new();

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

    // Scurvy (design doc 13.5): the winter disease of posts living on
    // dried and salted rations. Fresh meat and fish keep it off; six weeks
    // without and it sets in, and then it slowly kills.
    public float DaysSinceFresh;
    public bool HasScurvy => DaysSinceFresh > 42f;
    private bool _scurvyTold;

    // Fixed clothing not carried as items (animals' hides, sailors' slops).
    public float InnateWarmth = 0f;

    // Set while breaking into the other company's stores: the house treats
    // you as an enemy for as long as it lasts (design doc 15.1).
    public bool Raiding;
    public Faction RaidTarget;
    public float RaidTimer;

    // Who hurt us last; drives fear and retaliation.
    public Agent LastAttacker { get; private set; }
    public void ForgetAttacker() { LastAttacker = null; LastAttackedAgo = 999f; }
    public float LastAttackedAgo { get; private set; } = 999f;
    // Whose blow it was that killed us, if a blow did; a man hit an hour ago
    // who then starves did not die of the blow.
    public Agent Killer { get; private set; }

    private bool _deathAnnounced;
    private float _stepTimer;
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

    private float _envTimer = (float)GD.RandRange(0.0, 0.25), _exposure, _weatherExposure = 1f, _clothing;
    private bool _rainedOn;
    // How many frames' worth of time this step covers (1 for anyone who steps every frame).
    protected float StepScale = 1f;

    // Physics steps thirty times a second and is drawn in between (physics
    // interpolation): a body that has jumped - a teleport, a fast-travel leg,
    // a person put at his day's place - is drawn where it landed, not
    // sliding there across the country.
    private Vector2 _lastStepAt = new(float.NaN, float.NaN);
    protected void NoSlideOnJump()
    {
        var at = GlobalPosition;
        if (!float.IsNaN(_lastStepAt.X) && at.DistanceSquaredTo(_lastStepAt) > 300f * 300f) ResetPhysicsInterpolation();
        _lastStepAt = at;
    }

    public override void _PhysicsProcess(double delta)
    {
        NoSlideOnJump();
        var cal = GameCalendar.Instance;
        if (cal == null) return;
        if (!Net.Net.IsServer) return;   // puppets and remote copies don't simulate needs
        float scaled = (float)delta * cal.MotionScale;
        // The game hours since this body last stepped (an NPC away from the
        // player steps every few frames: Npc.ThinkEvery).
        float hoursStep = cal.DeltaGameHours * StepScale;
        HurtFlash = Mathf.Max(0f, HurtFlash - (float)delta);
        _attackCooldown = Mathf.Max(0f, _attackCooldown - scaled);
        LastAttackedAgo += scaled;
        if (RaidTimer > 0f) { RaidTimer -= scaled; if (RaidTimer <= 0f) Raiding = false; }
        // Footfalls carry (design doc 16.1): running far, walking some, crouching hardly at all.
        if (this is not Animal && Velocity.LengthSquared() > 4f)
        {
            _stepTimer -= (float)delta;
            if (_stepTimer <= 0f)
            {
                _stepTimer = IsSprinting ? 0.35f : 0.55f;
                float r = IsSprinting ? 420f : IsSneaking ? 45f : 190f;
                Sim.Noise(GlobalPosition, r, this, "step");
            }
        }
        if (IsDead) return;

        // What the place he stands in is like - under a roof, by a fire, the
        // country's cold and the day's weather, what he has on - changes over
        // seconds, not frames: looked at four times a second, each person at
        // his own moment, so a town full of people does not all ask at once.
        _envTimer -= (float)delta;
        if (_envTimer <= 0f)
        {
            _envTimer = 0.25f;
            var structures = Sim.Structures;
            IsSheltered = structures?.IsSheltered(GlobalPosition) ?? false;
            bool enclosed = false;
            HeatNear = structures?.HeatAt(GlobalPosition, out enclosed) ?? 0f;
            HeatEnclosed = enclosed;
            _exposure = Climate.ExposureAt(GlobalPosition, cal.DaylightFactor, cal.DayOfYear);
            _weatherExposure = cal.Weather.ExposureMultiplierAt(GlobalPosition);
            _rainedOn = cal.Weather.At(GlobalPosition) is SkyState.Rain or SkyState.Storm;
            if (this is Npc) DressForWeather();
            _clothing = Inventory.ClothingWarmth + InnateWarmth;
        }

        bool moving = Velocity.LengthSquared() > 1f;
        Intoxication = Mathf.Max(0f, Intoxication - 8f * hoursStep);
        DaysSinceFresh += hoursStep / 24f;
        if (HasScurvy)
        {
            if (!_scurvyTold) { _scurvyTold = true; Sim.Notice($"{Describe()} has the scurvy: gums bleeding, old wounds opening. Fresh meat is the cure."); }
            Needs.Health = Mathf.Max(0f, Needs.Health - 0.12f * hoursStep);   // ~3 a day
        }
        if (IsSick)
        {
            SickDaysLeft -= hoursStep / 24f;
            Needs.Health = Mathf.Max(0f, Needs.Health - 0.35f * hoursStep);   // ~8 a day
            Needs.Hunger = Mathf.Max(0f, Needs.Hunger - 0.4f * hoursStep);
            if (SickDaysLeft <= 0f) { Immune = true; Sim.Notice($"{Describe()} is over the fever."); }
        }
        Needs.Tick(
            hoursStep,
            activity: !moving ? 1f : (IsSprinting ? 2.4f : IsOverloaded ? 2.0f : 1.5f),
            sheltered: IsSheltered,
            heat: HeatNear,
            clothingWarmth: _clothing,
            season: cal.CurrentSeason,
            daylight: cal.DaylightFactor,
            fireEnclosed: HeatEnclosed,
            localExposure: _exposure,
            localWeather: _weatherExposure,
            rainedOn: _rainedOn);

        if (IsDead) OnDeath();
    }

    // A man puts his coat or robe on when the cold bites and takes it off
    // when it is warm again (the player dresses himself). The gap between
    // the two keeps him from fussing with it at every change of the wind;
    // a warm rain is still warm.
    private void DressForWeather()
    {
        if (_exposure < 0f) return;
        float cold = _exposure * _weatherExposure;
        if (cold > 0.2f && cold < 0.6f) return;
        foreach (var id in Wraps)
        {
            if (!Inventory.Has(id)) continue;
            if (cold >= 0.6f) Inventory.Unworn.Remove(id);
            else Inventory.Unworn.Add(id);
        }
    }

    private static readonly ItemId[] Wraps = { ItemId.Capote, ItemId.PointBlanket, ItemId.HideCoat, ItemId.BuffaloRobe };

    protected virtual void OnDeath()
    {
        if (_deathAnnounced) return;
        _deathAnnounced = true;
        Velocity = Vector2.Zero;
        // What is left on the ground where he fell.
        if (Killer != null) CombatFx.Pool(this);
        if (this is not Animal) Sim.Noise(GlobalPosition, 220f, this, "fall");
        EmitSignal(SignalName.Died);
        Net.Net.I?.AnnounceDeath(this);
    }

    // Back from the dead (a player respawning).
    public virtual void Revive()
    {
        _deathAnnounced = false;
        Needs.Health = 100f; Needs.Hunger = Needs.Thirst = Needs.Warmth = 100f;
        LastAttacker = null; LastAttackedAgo = 999f; Killer = null;
        CollisionLayer = 1; CollisionMask = 1;
        RemoveFromGroup("corpses");
    }

    // A voyageur carried two 90-lb pieces on a portage; anyone else slows
    // down well before that (design doc 12).
    public virtual float BaseCarryLbs => 50f;
    public float CarryCapacityLbs => BaseCarryLbs + Inventory.CarryBonusLbs;
    public bool IsOverloaded => Inventory.TotalWeightLbs > CarryCapacityLbs;
    public float LoadFactor => Mathf.Clamp(1f - (Inventory.TotalWeightLbs - CarryCapacityLbs) / 160f, 0.35f, 1f);
    public float DrunkFactor => Intoxication > 40f ? Mathf.Lerp(1f, 0.6f, (Intoxication - 40f) / 60f) : 1f;
    public float SickFactor => IsSick ? 0.7f : HasScurvy ? 0.8f : 1f;
    // On horseback (design doc 12.3): the coat of the horse under him, or -1
    // afoot. The pace that buys is the ground's to say (TerrainPainter.RideFactorAt):
    // three times a walker over open grass, worse than one in the muskeg.
    public int MountCoat = -1;
    public bool Mounted => MountCoat >= 0;
    // The horse's own shape (a thin horse is a slow one).
    public virtual float RidePace => 1f;
    public virtual void SetMounted(int coat)
    {
        if (coat == MountCoat) return;
        MountCoat = coat;
        GetNodeOrNull<Mount>("Mount")?.QueueFree();
        if (coat >= 0) { var m = new Mount { Name = "Mount", Coat = coat }; AddChild(m); }
    }
    // The ground's pace for this body, afoot or mounted.
    public float GroundFactor(out bool slippery)
    {
        slippery = false;
        if (Sim.Terrain == null) return 1f;
        return Mounted ? Sim.Terrain.RideFactorAt(GlobalPosition, out slippery) * RidePace : Sim.Terrain.SpeedFactorAt(GlobalPosition, Inventory.IsWorn(ItemId.Snowshoes), out slippery);
    }
    // What goes over the wire: the animation, and the horse if there is one.
    public string NetAnim => Mounted ? $"{CurrentAnimation}|h{MountCoat}" : CurrentAnimation;
    public static string StripMount(string anim) { int bar = anim.IndexOf("|h"); return bar >= 0 ? anim.Substring(0, bar) : anim; }
    public void ApplyNetSnapshot(Vector2 pos, string anim, bool flip)
    {
        int bar = anim.IndexOf("|h");
        int coat = bar >= 0 && int.TryParse(anim.Substring(bar + 2), out int c) ? c : -1;
        if (coat != MountCoat) SetMounted(coat);
        ApplySnapshot(pos, bar >= 0 ? anim.Substring(0, bar) : anim, flip);
    }

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
        // Fresh meat, and the things that actually answered the scurvy in this
        // country: berries in their season and spruce beer out of it, and the
        // settlers' potatoes from the cellar.
        if (id == ItemId.Venison || id == ItemId.Goose || id == ItemId.Fish || id == ItemId.Hare || id == ItemId.Ptarmigan || id == ItemId.Berries || id == ItemId.SpruceBeer || id == ItemId.Roots)
        {
            if (HasScurvy) Sim.Notice($"{Describe()} is mending: {(id == ItemId.Berries ? "berries at last" : id == ItemId.SpruceBeer ? "spruce beer at last" : id == ItemId.Roots ? "potatoes at last" : "fresh meat at last")}.");
            DaysSinceFresh = 0f; _scurvyTold = false;
        }
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

    // Powder and ball (design doc 12.5): a horn and a bag are about twenty
    // charges; a musket shot spends one. An empty horn is filled from the
    // pack, or for a man of a house from its store; a man with no powder
    // left can only club with his gun.
    public const int ChargesPerHorn = 20;
    public int Charges;
    public bool HasCharge() => Charges > 0 || Inventory.Has(ItemId.Powder) || this is Npc { HomeStores: { } h } && h.Stores.Has(ItemId.Powder);
    public bool UseCharge()
    {
        if (Charges <= 0)
        {
            if (Inventory.Remove(ItemId.Powder)) Charges = ChargesPerHorn;
            else if (this is Npc { HomeStores: { } h } && h.Stores.Remove(ItemId.Powder)) Charges = ChargesPerHorn;
            else return false;
        }
        Charges--;
        return true;
    }

    public (float damage, float range, float cooldown) AttackProfile()
    {
        var w = Weapon;
        if (w == ItemId.Musket && !HasCharge()) return (18f, 75f, 1.4f);   // the butt of an empty gun
        if (w != null)
        {
            var d = Items.Items.Of(w.Value);
            return (d.Damage, d.Range, d.Cooldown);
        }
        return (NaturalDamage > 0f ? NaturalDamage : 8f, NaturalRange, NaturalCooldown);
    }

    public bool CanAttack => _attackCooldown <= 0f && !IsDead;
    protected void _attackCooldownPublic(float s) { _attackCooldown = s; _lastCooldown = s; }
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

    // Something said out loud (design doc P.4, P.9): a bubble here, and the
    // same line on every client near enough to see it.
    public void Bark(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        LastBarkMs = Time.GetTicksMsec();
        if (Net.Net.IsServer) GD.Print($"[bark] {Describe()}: {text}");
        ShowBark(text);
        if (Net.Net.IsServer) Net.Net.I?.BroadcastBark(this, text);
    }

    public ulong LastBarkMs;
    public bool BarkedWithin(float seconds) => LastBarkMs > 0 && Time.GetTicksMsec() - LastBarkMs < seconds * 1000f;
    private SpeechBubble _bubble;
    public void ShowBark(string text)
    {
        // Their own tongue to someone who hasn't learned it (design doc 10.6).
        if (IsBandPerson && Sim.Player is Player.PlayerController me && me != this && me.FluencyWith(this) < 0.3f)
            text = $"({Languages.Name(Faction)} - you catch a word or two)";
        if (_bubble == null || !IsInstanceValid(_bubble))
        {
            _bubble = new SpeechBubble { Position = new Vector2(0, -140), ZIndex = 30, ZAsRelative = false };
            AddChild(_bubble);
        }
        _bubble.Say(text);
    }

    public bool InAttackRange(Agent target)
    {
        var (_, range, _) = AttackProfile();
        if (GlobalPosition.DistanceTo(target.GlobalPosition) > range + 20f) return false;
        // A man behind a wall isn't in range of a gun: close in round it.
        return range <= 100f || Sim.Structures?.ShotBlockedAt(GlobalPosition, target.GlobalPosition) == null;
    }

    // The last shot went into a wall rather than at the mark.
    public bool LastShotWalled { get; private set; }

    // Swing/shoot at the target if in range and ready. Returns true on a hit.
    public bool Attack(Agent target)
    {
        if (target == null || !IsInstanceValid(target) || target.IsDead || !CanAttack) return false;
        var (damage, range, cooldown) = AttackProfile();
        if (GlobalPosition.DistanceTo(target.GlobalPosition) > range + 20f) return false;
        _attackCooldown = cooldown; _lastCooldown = cooldown;
        LastShotWalled = false;
        // Walls, palisades and shut doors stop a ball (or an arrow): it
        // goes into the timber, and the mark behind it is untouched.
        if (range > 100f && Sim.Structures?.ShotBlockedAt(GlobalPosition, target.GlobalPosition) is Vector2 wall)
        {
            bool shot = Weapon == ItemId.Musket && UseCharge();
            CombatFx.Shot(this, wall + new Vector2(0, -40), shot);
            if (shot) { CombatFx.Musket(this, wall); Sim.Noise(GlobalPosition, 1500f, this, "shot"); }
            CombatFx.Missed(this, wall);
            LastShotWalled = true;
            OnAttackMissed(target);
            return false;
        }
        // Weak agents hit softer; ranged shots can miss. A sneak attack on
        // something that hasn't noticed you lands like a butcher's stroke.
        float mult = Needs.CapabilityFactor;
        if (IsSneaking && target.Unaware) mult *= 2.5f;
        bool fired = Weapon == ItemId.Musket && range > 100f && UseCharge();
        if (range > 100f) CombatFx.Shot(this, target.GlobalPosition + new Vector2(0, -40), fired);
        if (fired) CombatFx.Musket(this, target.GlobalPosition);
        else if (range <= 100f) CombatFx.Swing(this, target);
        if (fired) Sim.Noise(GlobalPosition, 1500f, this, "shot");
        else if (range > 100f) Sim.Noise(GlobalPosition, 200f, this, "step");
        // Hold your fire until the range is right (design doc 15.6): a smooth
        // bore at the edge of its reach mostly throws the ball away, and at
        // ten paces it is a butcher's tool. Drink spoils the aim; fever
        // weakens the arm.
        float missChance = Intoxication / 250f;
        if (range > 100f)
        {
            float reach = Mathf.Clamp(GlobalPosition.DistanceTo(target.GlobalPosition) / range, 0f, 1f);
            missChance += (0.08f + 0.62f * reach * reach) * (1f - Skills.Hunting * 0.55f);
            if (target.IsSprinting) missChance += 0.1f;
            if (IsSneaking && target.Unaware) missChance *= 0.4f;   // a steady rest and all the time in the world
        }
        if (range > 100f) Skills.Learn(ref Skills.Hunting, 0.01f, this is Player.PlayerController ? "shooting at range" : null);
        if (IsSick) mult *= 0.6f;
        if (GD.Randf() < missChance) { CombatFx.Missed(this, target.GlobalPosition); OnAttackMissed(target); return false; }
        target.TakeDamage(damage * mult, this);
        // A blow landing is heard (and seen) by whoever is near.
        if (range <= 100f && this is not Animal) Sim.Noise(target.GlobalPosition, 160f, this, "hit");
        else if (this is Animal { Species: Ecology.Species.Wolf }) Sim.Noise(target.GlobalPosition, 300f, this, "bite");
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
        CombatFx.Blood(this, amount);
        ShowHealthBar();
        LastAttacker = from;
        LastAttackedAgo = 0f;
        EmitSignal(SignalName.Damaged, amount);
        if (IsDead) { Killer = from; OnDeath(); }
    }

    public virtual bool IsHostileTo(Agent other)
    {
        if (other == null || other == this) return false;
        if (Faction == Faction.Wild || other.Faction == Faction.Wild) return true;
        return false;
    }

    public string Describe() => string.IsNullOrEmpty(DisplayName) ? Name : DisplayName;
    // One of the nations (design doc 7.2), as against a company man or a sailor.
    public bool IsBandPerson => Languages.IsNation(Faction);
}
