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

    // The fire itself (design doc 18.4). Until now a hearth was a light and
    // a tile: the flame is drawn here, over whatever ring of stones or
    // fireplace the tile shows, so it burns, dies down to embers, smokes and
    // throws the odd spark.
    private const string Fx = "res://assets/sprites/fx/";
    private static readonly System.Collections.Generic.Dictionary<string, Texture2D[]> Art = new();
    private Sprite2D _flame, _smoke, _sparks;
    private float _anim, _smokeAnim, _sparkIn = 4f;

    private static Texture2D[] Frames(string name, int n)
    {
        if (Art.TryGetValue(name, out var have)) return have;
        var a = new Texture2D[n];
        for (int i = 0; i < n; i++) a[i] = GD.Load<Texture2D>($"{Fx}{name}_{i}.png");
        Art[name] = a;
        return a;
    }

    // Each frame is cropped to its own size, so the anchor is set with it:
    // bottom centre, which is where the fire meets the ground.
    private static void Put(Sprite2D s, Texture2D t)
    {
        if (t == null || s.Texture == t) return;
        s.Texture = t;
        s.Offset = new Vector2(-t.GetWidth() / 2f, -t.GetHeight());
    }

    public override void _Ready()
    {
        // Additive lights use the texture's colour, so the edge must go to
        // black, not just transparent, or the whole square lights up.
        var grad = new Gradient();
        grad.SetColor(0, new Color(1f, 0.72f, 0.38f, 1f));
        grad.SetColor(1, new Color(0f, 0f, 0f, 0f));
        grad.AddPoint(0.35f, new Color(0.7f, 0.4f, 0.15f, 1f));
        var tex = new GradientTexture2D
        {
            Gradient = grad,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(0.5f, 0f),
            Width = 256, Height = 256,
        };
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

        // A fire indoors is in a fireplace and stands tall against the back
        // of it; one in the open is a low fire in a ring of stones.
        _smoke = new Sprite2D { Centered = false, Position = new Vector2(0, -34), Scale = Vector2.One * 0.15f, ZIndex = 1, Modulate = new Color(1, 1, 1, 0.3f) };
        AddChild(_smoke);
        _flame = new Sprite2D { Centered = false, Position = new Vector2(0, -16), Scale = Vector2.One * (Enclosed ? 0.22f : 0.19f), ZIndex = 0 };
        AddChild(_flame);
        _sparks = new Sprite2D { Centered = false, Position = new Vector2(0, -24), Scale = Vector2.One * 0.14f, ZIndex = 1, Visible = false };
        AddChild(_sparks);
    }
    private Sprite2D _glow;

    public override void _Process(double delta)
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

        _flicker += (float)delta * 9f;
        float pulse = 1f + 0.12f * Mathf.Sin(_flicker) * Mathf.Sin(_flicker * 0.37f);
        _light.Energy = Intensity * 2.2f * pulse;
        _light.Visible = IsBurning;
        // Firelight only shows once the day fades; in sun it just washes out.
        float dark = 1f - (cal?.DaylightFactor ?? 1f);
        _glow.Visible = IsBurning && dark > 0.02f;
        _glow.Modulate = new Color(1f, 0.6f, 0.3f, 0.28f * Intensity * pulse * dark);
        _light.Energy *= Mathf.Lerp(0.35f, 1f, dark);

        // The flame: six frames at a fire's own restless rate, or the four
        // of a bed of embers once the wood is nearly gone.
        bool embers = IsBurning && FuelHours < 1.5f;
        _flame.Visible = IsBurning;
        if (IsBurning)
        {
            _anim += (float)delta * (embers ? 4f : 10f);
            var set = embers ? Frames("embers", 4) : Frames(Enclosed ? "hearthfire" : "campfire", 6);
            Put(_flame, set[Mathf.PosMod((int)_anim, set.Length)]);
            _flame.Scale = Vector2.One * (embers ? 0.17f : Enclosed ? 0.22f : 0.19f) * Mathf.Lerp(0.82f, 1.05f, Intensity);
        }

        // Smoke rises off anything burning, and off a fire just gone out.
        bool smoking = IsBurning || FuelHours <= 0f && Lit && _smokeOut > 0f;
        if (IsBurning) _smokeOut = 14f;
        else if (_smokeOut > 0f) _smokeOut -= (float)delta;
        _smoke.Visible = smoking && !Enclosed;   // indoors it goes up the chimney
        if (_smoke.Visible)
        {
            _smokeAnim += (float)delta * 3.2f;
            var puffs = Frames("smoke", 6);
            Put(_smoke, puffs[Mathf.PosMod((int)_smokeAnim, puffs.Length)]);
            float strength = IsBurning ? 0.16f + 0.14f * Intensity : 0.26f;
            _smoke.Modulate = new Color(1, 1, 1, strength);
        }

        // And a well-fed fire throws sparks now and then.
        _sparkIn -= (float)delta;
        if (_sparkIn <= 0f)
        {
            _sparkIn = (float)GD.RandRange(2.5, 7.0);
            if (IsBurning && FuelHours > 3f)
            {
                var sp = Frames("sparks", 4);
                Put(_sparks, sp[GD.RandRange(0, sp.Length - 1)]);
                _sparks.Visible = true;
                GetTree().CreateTimer(0.35).Timeout += () => { if (IsInstanceValid(_sparks)) _sparks.Visible = false; };
            }
        }
    }
    private float _smokeOut;

    public float HeatAt(Vector2 worldPos)
    {
        if (!IsBurning) return 0f;
        float d = GlobalPosition.DistanceTo(worldPos);
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
