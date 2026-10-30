using Godot;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.UI;

// The weather you can see (design doc 14.2). The sky already decided what
// it was doing and already bit at a man's warmth; until now the only sign
// of it on screen was the light going down a little. Snow, rain, a
// whiteout, the morning fog off the river and the aurora on a hard clear
// winter night are drawn over the world here.
//
// Two layers of falling stuff, the near one larger and faster, so it has
// some depth to it; everything crossfades over a couple of seconds, so the
// sky changing is something you notice rather than something that blinks.
// Under a roof you still see it, faintly, through the door.
public partial class WeatherOverlay : Node
{
    public static WeatherOverlay Instance { get; private set; }

    private const string Dir = "res://assets/ui/weather/";
    private Sheet _snowLight, _snowHeavy, _blizzard, _rain, _fog;
    private Texture2D _aurora;
    private Layer _layer;
    private Aurora _sky1;

    // One animation: four frames and the size they are drawn at.
    private class Sheet
    {
        public Texture2D[] Frames = new Texture2D[4];
        public float Fps = 8f;
        public Texture2D At(float t) => Frames[Mathf.PosMod((int)(t * Fps), 4)];
    }

    public static void Install()
    {
        if (Instance != null) return;
        var root = (Engine.GetMainLoop() as SceneTree)?.Root;
        if (root == null) return;
        Instance = new WeatherOverlay { Name = "WeatherOverlay" };
        root.CallDeferred(Node.MethodName.AddChild, Instance);
    }

    public override void _Ready()
    {
        _snowLight = Load("snow_light");
        _snowHeavy = Load("snow_heavy");
        _blizzard = Load("blizzard");
        _rain = Load("rain", 12f);
        _fog = Load("fog", 2.5f);
        _aurora = GD.Load<Texture2D>(Dir + "aurora.png");
        // Layer 0 keeps it with the world rather than above the HUD; a high
        // z-index puts it over every sprite in it.
        var canvas = new CanvasLayer { Layer = 0 };
        AddChild(canvas);
        _layer = new Layer { Sky = this, ZIndex = 200, MouseFilter = Control.MouseFilterEnum.Ignore, TextureRepeat = CanvasItem.TextureRepeatEnum.Enabled };
        _layer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        canvas.AddChild(_layer);
        _sky1 = new Aurora
        {
            Sky = this, ZIndex = 199, MouseFilter = Control.MouseFilterEnum.Ignore,
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
        };
        _sky1.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        canvas.AddChild(_sky1);
    }

    private static Sheet Load(string name, float fps = 8f)
    {
        var s = new Sheet { Fps = fps };
        for (int i = 0; i < 4; i++) s.Frames[i] = GD.Load<Texture2D>($"{Dir}{name}_{i}.png");
        return s;
    }

    // What the sky is doing, eased so a change takes a moment.
    private float _snow, _rainy, _white, _mist, _sky;
    private float _t;

    private bool _forced;

    public override void _Process(double delta)
    {
        var cal = GameCalendar.Instance;
        if (cal == null || _layer == null) return;
        // Dev aid: `-- --sky=Blizzard` holds the sky there for a day, so the
        // weather can be looked at without waiting for it.
        if (!_forced)
        {
            _forced = true;
            foreach (string arg in OS.GetCmdlineUserArgs())
                if (arg.StartsWith("--sky=") && System.Enum.TryParse<SkyState>(arg.Substring(6), true, out var forced))
                { cal.Weather.Force(forced, 24f); GD.Print($"[dev] the sky is {forced}"); }
        }
        float dt = (float)delta;
        _t += dt;

        var state = cal.Weather.Current;
        // Nothing of the weather belongs on the front end: the menu has its
        // own painted evening.
        bool inWorld = Sim.Player != null && IsInstanceValid(Sim.Player);
        bool sheltered = inWorld && Sim.Player.IsSheltered;
        float inside = !inWorld ? 0f : sheltered ? 0.18f : 1f;

        float snow = state is SkyState.Snow ? 1f : state == SkyState.Blizzard ? 1f : 0f;
        float rain = state is SkyState.Rain ? 0.9f : state == SkyState.Storm ? 1f : 0f;
        float white = state == SkyState.Blizzard ? 0.55f : 0f;
        // River fog: an overcast morning, and the hour or two after dawn in
        // the autumn when the water is warmer than the air.
        bool morning = cal.Hour is >= 3.5f and <= 9f;
        float mist = morning && state is SkyState.Overcast or SkyState.Clear
            ? (cal.CurrentSeason == Season.FreezeUp || cal.CurrentSeason == Season.OpenWater ? 0.85f : 0.35f) : 0f;
        if (state is SkyState.Snow or SkyState.Blizzard or SkyState.Rain or SkyState.Storm) mist = 0f;
        // The aurora: a clear winter night, and the harder the cold the better.
        float sky = state == SkyState.Clear && cal.DaylightFactor < 0.12f
            && cal.CurrentSeason is Season.Winter or Season.FreezeUp ? 1f : 0f;

        float ease = Mathf.Min(1f, dt * 0.5f);
        _snow = Mathf.Lerp(_snow, snow * inside, ease);
        _rainy = Mathf.Lerp(_rainy, rain * inside, ease);
        _white = Mathf.Lerp(_white, white * inside, ease);
        _mist = Mathf.Lerp(_mist, mist * inside, ease);
        _sky = Mathf.Lerp(_sky, inWorld && !sheltered ? sky : 0f, ease);
        if (_snow + _rainy + _white + _mist > 0.002f) _layer.QueueRedraw();
        _sky1?.QueueRedraw();
    }

    // The aurora stands over the north: a band across the top of the view,
    // added to what is under it so it glows rather than greys, which is why
    // it is its own node - a blend mode belongs to a material in Godot 4.
    private partial class Aurora : Control
    {
        public WeatherOverlay Sky;

        public override void _Draw()
        {
            var s = Sky;
            if (s == null || s._aurora == null || s._sky <= 0.004f) return;
            float w = s._aurora.GetWidth(), h = s._aurora.GetHeight() * 0.85f;
            float x = -Mathf.PosMod(9f * s._t, w);
            for (int i = 0; i < 2; i++)
                DrawTextureRect(s._aurora, new Rect2(x + i * w, -h * 0.12f, w, h), false, new Color(1, 1, 1, 0.55f * s._sky));
        }
    }

    // The drawing itself, in screen space.
    private partial class Layer : Control
    {
        public WeatherOverlay Sky;

        public override void _Draw()
        {
            var s = Sky;
            if (s == null) return;
            var r = new Rect2(Vector2.Zero, Size);
            float t = s._t;

            // A whiteout is first of all a loss of the world: a veil of
            // white, and then the snow driving across it.
            if (s._white > 0.004f)
            {
                DrawRect(r, new Color(0.93f, 0.95f, 0.99f, 0.42f * s._white));
                Tile(s._blizzard.At(t), r, new Vector2(-900f, 260f) * t, 2.4f, new Color(1, 1, 1, 0.9f * s._white));
                Tile(s._blizzard.At(t + 0.17f), r, new Vector2(-1500f, 420f) * t, 3.6f, new Color(1, 1, 1, 0.7f * s._white));
            }

            if (s._snow > 0.004f)
            {
                var sheet = s._white > 0.1f ? s._snowHeavy : s._snowLight;
                float gust = s._white > 0.1f ? 420f : 70f;
                Tile(sheet.At(t), r, new Vector2(-gust, 120f) * t, 1.7f, new Color(1, 1, 1, 0.7f * s._snow));
                Tile(sheet.At(t + 0.21f), r, new Vector2(-gust * 1.7f, 240f) * t, 2.9f, new Color(1, 1, 1, 0.45f * s._snow));
            }

            if (s._rainy > 0.004f)
            {
                Tile(s._rain.At(t), r, new Vector2(-260f, 1500f) * t, 1.5f, new Color(0.85f, 0.9f, 1f, 0.55f * s._rainy));
                Tile(s._rain.At(t + 0.14f), r, new Vector2(-430f, 2400f) * t, 2.4f, new Color(0.85f, 0.9f, 1f, 0.4f * s._rainy));
            }

            // Fog lies in bands rather than falling: three of them, drifting
            // slowly across the lower half of the view. Its frames melt into
            // one another over seconds - cut from one to the next a few times
            // a second, the whole lower screen strobed.
            if (s._mist > 0.004f && s._fog.Frames[0] != null)
            {
                float phase = t * 0.12f;
                int a = Mathf.PosMod((int)phase, 4), b = (a + 1) % 4;
                float mix = phase - Mathf.Floor(phase);
                mix = mix * mix * (3f - 2f * mix);
                float h = Size.Y;
                for (int i = 0; i < 3; i++)
                {
                    float y = h * (0.42f + 0.17f * i);
                    float speed = 6f + 4f * i;
                    float alpha = s._mist * (0.32f - 0.07f * i);
                    foreach (var (f, k) in new[] { (s._fog.Frames[a], 1f - mix), (s._fog.Frames[b], mix) })
                    {
                        if (f == null || k < 0.01f) continue;
                        DrawTextureRect(f, new Rect2(-Mathf.PosMod(speed * t, f.GetWidth() * 2f), y, Size.X + f.GetWidth() * 2f, f.GetHeight() * (1.4f + 0.5f * i)),
                            true, new Color(1, 1, 1, alpha * k));
                    }
                }
            }

        }

        // One tiled pass of a frame, scrolled and scaled.
        private void Tile(Texture2D tex, Rect2 r, Vector2 scroll, float scale, Color tint)
        {
            if (tex == null) return;
            float w = tex.GetWidth() * scale, h = tex.GetHeight() * scale;
            var at = new Vector2(-Mathf.PosMod(-scroll.X, w), -Mathf.PosMod(-scroll.Y, h));
            DrawSetTransform(at, 0f, new Vector2(scale, scale));
            DrawTextureRect(tex, new Rect2(Vector2.Zero, (r.Size - at + new Vector2(w, h)) / scale), true, tint);
            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }
    }
}
