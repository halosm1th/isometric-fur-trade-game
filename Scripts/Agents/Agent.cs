using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Agents;

// Every person in the world is one of these - the player included (design
// doc 11.1). Needs, inventory, and the shared action vocabulary live here;
// what drives the agent (input, or a goal stack) is the subclass's job.
public partial class Agent : CharacterBody2D
{
    [Signal] public delegate void DiedEventHandler();

    public readonly Needs Needs = new();
    public readonly Inventory Inventory = new();

    [Export] public float MoveSpeed = 140f;

    // Running is the voyageur's pace: much faster, and it eats you alive.
    [Export] public float SprintMultiplier = 1.7f;
    public bool IsSprinting { get; protected set; }

    public bool IsDead => Needs.IsDead;
    public bool IsSheltered { get; private set; }

    // 0..1 warmth reaching this agent from the nearest fire.
    public float HeatNear { get; private set; }

    private bool _deathAnnounced;

    public override void _PhysicsProcess(double delta)
    {
        var cal = GameCalendar.Instance;
        if (cal == null || IsDead) return;

        var structures = GetTree().GetFirstNodeInGroup("structures") as StructurePainter;
        IsSheltered = structures?.IsSheltered(Position) ?? false;
        HeatNear = structures?.HeatAt(GlobalPosition) ?? 0f;

        bool moving = Velocity.LengthSquared() > 1f;
        Needs.Tick(
            cal.DeltaGameHours,
            activity: !moving ? 1f : (IsSprinting ? 2.4f : 1.5f),
            sheltered: IsSheltered,
            heat: HeatNear,
            clothingWarmth: Inventory.ClothingWarmth,
            season: cal.CurrentSeason,
            daylight: cal.DaylightFactor);

        if (IsDead && !_deathAnnounced)
        {
            _deathAnnounced = true;
            Velocity = Vector2.Zero;
            EmitSignal(SignalName.Died);
        }
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
        Needs.Warmth = Mathf.Min(100f, Needs.Warmth + def.Warmth * (def.Kind == ItemKind.Drink ? 1f : 0f));
        return true;
    }

    // Put a piece of fuel on the nearest fire, if there is one in reach.
    public bool FeedFire(ItemId id)
    {
        var def = Items.Items.Of(id);
        if (def.Kind != ItemKind.Fuel) return false;
        var structures = GetTree().GetFirstNodeInGroup("structures") as StructurePainter;
        var hearth = structures?.HearthNear(GlobalPosition);
        if (hearth == null || !Inventory.Remove(id)) return false;
        hearth.Feed(def.FuelHours);
        return true;
    }

    // Water isn't scarce here; the cost is access (design doc 17.2). In
    // winter that means melting snow, which you pay for in warmth.
    public bool Drink()
    {
        var terrain = GetTree().GetFirstNodeInGroup("terrain") as TerrainPainter;
        if (terrain == null || !terrain.IsWaterNear(Position)) return false;

        Needs.Thirst = 100f;
        if (GameCalendar.Instance.IsFrozen)
        {
            Needs.Warmth = Mathf.Max(0f, Needs.Warmth - 8f);
        }
        return true;
    }
}
