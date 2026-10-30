using Godot;

namespace IsometricFurTradeGame.Scripts.UI;

// The loading screen's painting, alive: the flag stirs on its staff, the
// camp fires flicker, the stars come out, and the ship stands in from the
// Bay's horizon to her anchorage as the world loads (Progress 0..1). The
// ship is cut from the menu painting by tools/sprites/loading_art.py, which
// paints the sky back behind her.
public partial class LoadingArt : Control
{
    public float Progress;

    // In the painting's pixels (2752 x 1536).
    private static readonly Rect2 FlagArea = new(1966, 790, 136, 135);   // the flag, and sky enough round it to wave into
    private const float FlagTop = 812, FlagBottom = 903;
    private static readonly Vector2 ShipCorner = new(2195, 650);       // loading_art.py's SHIP_BOX at anchor
    private const float ShipWaterline = 295;                             // down from her corner
    private static readonly Vector2 Horizon = new(2700, 878);            // where she comes up over the sea
    private static readonly Vector2[] Fires = { new(2465, 1092), new(2602, 1108) };
    private static readonly Vector2[] Stars = { new(2546, 66), new(2084, 134), new(2274, 150), new(2506, 230), new(2674, 352), new(2230, 430), new(1686, 320), new(1900, 114) };

    private const string FlagCode = @"
shader_type canvas_item;
uniform vec4 area;          // x, y, w, h in the painting's pixels
uniform vec2 rows;          // the flag's top and bottom
uniform vec2 size;          // the painting
void fragment() {
    vec2 p = UV * size;
    vec2 q = (p - area.xy) / area.zw;
    if (q.x > 0.0 && q.x < 1.0 && q.y > 0.0 && q.y < 1.0) {
        // Fastened at the staff, freest at the fly; fading to nothing at the edges of the sky round it.
        float u = q.x;
        float reach = u * (1.0 - smoothstep(0.8, 1.0, u));
        float v = clamp(min(p.y - area.y, area.y + area.w - p.y) / max(1.0, min(rows.x - area.y, area.y + area.w - rows.y)), 0.0, 1.0);
        float dy = sin(u * 9.0 - TIME * 4.2) * 6.0 * reach * v;
        // Whole steps, to keep it pixel art; up and down only, so the cross doesn't tear.
        vec2 d = vec2(0.0, floor(dy / 2.0 + 0.5) * 2.0);
        COLOR = texture(TEXTURE, (p + d) / size);
    } else {
        COLOR = texture(TEXTURE, UV);
    }
}";

    private Texture2D _art;
    private TextureRect _ship;
    private readonly TextureRect[] _fires = new TextureRect[Fires.Length];
    private readonly TextureRect[] _stars = new TextureRect[Stars.Length];
    private TextureRect _lantern;
    private float _t;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _art = GD.Load<Texture2D>("res://assets/ui/loading_background.jpg");
        var flag = new ShaderMaterial { Shader = new Shader { Code = FlagCode } };
        flag.SetShaderParameter("area", new Vector4(FlagArea.Position.X, FlagArea.Position.Y, FlagArea.Size.X, FlagArea.Size.Y));
        flag.SetShaderParameter("rows", new Vector2(FlagTop, FlagBottom));
        flag.SetShaderParameter("size", _art.GetSize());
        Material = flag;

        var add = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        var glow = Glow(new Color(1f, 0.55f, 0.2f));
        for (int i = 0; i < Fires.Length; i++) _fires[i] = Light(glow, add);
        var star = Glow(new Color(0.85f, 0.9f, 1f));
        for (int i = 0; i < Stars.Length; i++) _stars[i] = Light(star, add);
        _ship = new TextureRect { Texture = GD.Load<Texture2D>("res://assets/ui/loading_ship.png"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_ship);
        _lantern = Light(Glow(new Color(1f, 0.75f, 0.35f)), add);
    }

    private static GradientTexture2D Glow(Color c)
    {
        var g = new Gradient();
        g.SetColor(0, c);
        g.SetColor(1, new Color(c, 0f));
        return new GradientTexture2D { Gradient = g, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0f), Width = 64, Height = 64 };
    }

    private TextureRect Light(Texture2D tex, Material add)
    {
        var r = new TextureRect { Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Material = add, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(r);
        return r;
    }

    // The painting covers the screen, cropped to fit: its scale and corner.
    private (float, Vector2) Cover()
    {
        var a = _art.GetSize();
        float k = Mathf.Max(Size.X / a.X, Size.Y / a.Y);
        return (k, (Size - a * k) / 2f);
    }

    private static void Place(Control c, Vector2 centre, float radius, float k, Vector2 o)
    {
        c.Size = Vector2.One * radius * 2f * k;
        c.Position = o + centre * k - c.Size / 2f;
    }

    public override void _Process(double delta)
    {
        _t += Mathf.Min((float)delta, 0.1f);
        var (k, o) = Cover();
        for (int i = 0; i < _fires.Length; i++)
        {
            float f = 0.75f + 0.15f * Mathf.Sin(_t * 11f + i * 2.1f) + 0.1f * Mathf.Sin(_t * 23f + i * 5.3f);
            Place(_fires[i], Fires[i] + new Vector2(0, -8), 46f * f, k, o);
            _fires[i].Modulate = new Color(1, 1, 1, 0.55f * f);
        }
        for (int i = 0; i < _stars.Length; i++)
        {
            float tw = Mathf.Max(0f, Mathf.Sin(_t * (0.9f + i * 0.23f) + i * 1.7f));
            Place(_stars[i], Stars[i], 9f, k, o);
            _stars[i].Modulate = new Color(1, 1, 1, 0.15f + 0.6f * tw * tw);
        }
        // Up over the horizon small and far, nearer and larger as she stands in.
        float e = 1f - Mathf.Pow(1f - Mathf.Clamp(Progress, 0f, 1f), 2f);
        float s = Mathf.Lerp(0.22f, 1f, e);
        var tex = _ship.Texture.GetSize();
        var anchor = ShipCorner + new Vector2(tex.X / 2f, ShipWaterline);
        var at = Horizon.Lerp(anchor, e);
        var corner = at - new Vector2(tex.X / 2f, ShipWaterline) * s;
        // A slow roll on the swell.
        corner.Y += Mathf.Sin(_t * 1.6f) * 2f * s;
        _ship.Position = o + corner * k;
        _ship.Size = tex * s * k;
        _ship.Modulate = new Color(1, 1, 1, Mathf.Clamp(Progress * 12f, 0f, 1f));
        // Her stern lantern, lit in the dusk.
        Place(_lantern, corner + new Vector2(318, 262) * s, 14f * s, k, o);
        _lantern.Modulate = new Color(1, 1, 1, _ship.Modulate.A * (0.6f + 0.15f * Mathf.Sin(_t * 7f)));
        QueueRedraw();
    }

    public override void _Draw()
    {
        var (k, o) = Cover();
        DrawTextureRect(_art, new Rect2(o, _art.GetSize() * k), false);
    }
}
