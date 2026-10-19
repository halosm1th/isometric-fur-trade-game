using Godot;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.World;

// A fire is a simulated object, not a tile flag (design doc 18.4): it holds
// fuel that burns down in game-hours and radiates heat that falls off with
// distance. StructurePainter spawns one of these on every hearth tile.
public partial class Hearth : Node2D
{
    // Game-hours of fuel left. A fresh hearth is lit but needs tending.
    [Export] public float FuelHours = 6f;
    [Export] public float MaxFuelHours = 18f;

    // Heat is full within InnerRadius, gone at OuterRadius (pixels).
    [Export] public float InnerRadius = 90f;
    [Export] public float OuterRadius = 330f;

    public Vector2I Cell;

    // Where it stands in the world, noted when it is placed (fires don't
    // move): read every frame by every body asking how warm it is, and
    // GlobalPosition is a call into the engine each time.
    public Vector2 WorldPos;

    // No fire warms further than this (StructurePainter looks this far round
    // a body for fires).
    public const float MaxReach = 330f;

    // A hearth in a building, or the camp's lodge fires: its heat isn't
    // lost to the wind the way an open fire's is.
    public bool Enclosed = true;

    // Wood laid but no spark yet: a fire someone has to light.
    public bool Lit = true;
    public bool IsBurning => Lit && FuelHours > 0f;

    // 0..1 - how much of the fire's warmth reaches this point.
    public float Intensity => IsBurning ? Mathf.Clamp(0.4f + FuelHours / MaxFuelHours, 0.4f, 1f) : 0f;

    private PointLight2D _light;
    private float _flicker;

    public override void _Ready()
    {
        var tex = Glow;
        _light = new PointLight2D
        {
            Texture = tex,
            TextureScale = OuterRadius / 128f,
            Color = new Color(1f, 0.78f, 0.5f),
            Position = new Vector2(0, -28),
            BlendMode = Light2D.BlendModeEnum.Add,
        };
        AddChild(_light);

        // The light only brightens what's already lit, so at night it reads
        // faintly; this additive glow puts firelight on the floor regardless.
        _glow = new Sprite2D
        {
            Texture = tex,
            Position = new Vector2(0, -6),
            Scale = Vector2.One * (OuterRadius * 0.55f / 128f),
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
        };
        AddChild(_glow);
        SetProcess(false);
    }
    private Sprite2D _glow;

    // Called by StructurePainter for every hearth in one loop, not by the
    // engine for each: with a fire in every post and camp in the country,
    // the engine's call into each one every frame was itself a cost.
    public void Tick(double delta)
    {
        var cal = GameCalendar.Instance;
        if (cal != null && Net.Net.IsServer)
        {
            if (Lit) FuelHours = Mathf.Max(0f, FuelHours - cal.DeltaGameHours);
            // Stuffed past sense, a hearth throws sparks into the room (design doc 18.4).
            if (FuelHours > 15f && Fire.I != null && GD.Randf() < cal.DeltaGameHours * 0.02f)
            {
                var floor = IsoGrid.DownLeft(Cell);
                if (Fire.I.Ignite(floor, "A spark from an overloaded hearth has caught the floor!")) Sim.Notice("FIRE! Water, quick!");
            }
        }

        // The light is only worked for fires you could see: every hearth in
        // the country flickered every frame, a few hundred miles off.
        if (!InView(WorldPos))
        {
            if (_light.Visible) { _light.Visible = false; _glow.Visible = false; }
            return;
        }

        _flicker += (float)delta * 9f;
        float pulse = 1f + 0.12f * Mathf.Sin(_flicker) * Mathf.Sin(_flicker * 0.37f);
        _light.Energy = Intensity * 2.2f * pulse;
        _light.Visible = IsBurning;
        // Firelight only shows once the day fades; in sun it just washes out.
        float dark = 1f - (cal?.DaylightFactor ?? 1f);
        _glow.Visible = IsBurning && dark > 0.02f;
        _glow.Modulate = new Color(1f, 0.6f, 0.3f, 0.28f * Intensity * pulse * dark);
        _light.Energy *= Mathf.Lerp(0.35f, 1f, dark);
    }

    // What the camera shows this frame, grown by a fire's reach. Set once a
    // frame by StructurePainter, which every hearth hangs off (a parent is
    // processed before its children), instead of each fire asking.
    public static Rect2 View = new(-1e9f, -1e9f, 2e9f, 2e9f);
    private static bool InView(Vector2 at) => View.HasPoint(at);

    // One glow for every fire. Each hearth used to make its own 256 x 256
    // texture - with a fire in every post and camp in the country that was
    // hundreds of megabytes of video memory, and twice that while a save was
    // loading (the old world and the new), which ran small cards out.
    private static GradientTexture2D _glowTex;
    private static GradientTexture2D Glow
    {
        get
        {
            if (_glowTex != null) return _glowTex;
            // Additive lights use the texture's colour, so the edge must go to
            // black, not just transparent, or the whole square lights up.
            var grad = new Gradient();
            grad.SetColor(0, new Color(1f, 0.72f, 0.38f, 1f));
            grad.SetColor(1, new Color(0f, 0f, 0f, 0f));
            grad.AddPoint(0.35f, new Color(0.7f, 0.4f, 0.15f, 1f));
            _glowTex = new GradientTexture2D
            {
                Gradient = grad,
                Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f),
                FillTo = new Vector2(0.5f, 0f),
                Width = 256, Height = 256,
            };
            return _glowTex;
        }
    }

    public float HeatAt(Vector2 worldPos)
    {
        if (!IsBurning) return 0f;
        float d = WorldPos.DistanceTo(worldPos);
        float falloff = 1f - Mathf.Clamp((d - InnerRadius) / (OuterRadius - InnerRadius), 0f, 1f);
        return falloff * Intensity;
    }

    // What you see when you look at the fire.
    public string Describe()
    {
        if (!IsBurning) return "The fire is out. Cold ash.";
        if (FuelHours < 1.5f) return $"The fire is down to embers - {FuelHours:0.#} h left. It wants wood.";
        if (FuelHours < 5f) return $"The fire is burning low - about {FuelHours:0.#} h of fuel.";
        return $"The fire is burning well - about {FuelHours:0.#} h of fuel.";
    }

    public void Feed(float hours) => FuelHours = Mathf.Min(MaxFuelHours, FuelHours + hours);
    public void Light() { Lit = true; Sim.Noise(GlobalPosition, 150f, null, "flint"); }
}
