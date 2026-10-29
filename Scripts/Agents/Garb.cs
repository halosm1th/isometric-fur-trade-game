using System.Collections.Generic;
using Godot;

namespace IsometricFurTradeGame.Scripts.Agents;

// What a man has on, within his sheet: one Orkneyman's coat is green, the
// next one's brown or grey; one voyageur's tuque and sash are red, another's
// blue; an old Cree's hair has gone grey. Each sheet names its garments by
// colour - a band of hue, saturation, value and height on the frame - and
// the dyes each may be turned to. A look is the sheet's path with the
// choices after a '#' ("LabourerFrames.tres#2.0"), so saves and the
// snapshot carry it as they carry any sheet; the choices are drawn by the
// garb shader, one shared material per look, so the sheet's frames are
// never copied. Picked from the person's name, so a man keeps his coat.
// Bands and dyes were tried on the frames first (no art changes): faces sit
// at hue 12-22, so the red cloth bands stop short of them.
public static class Garb
{
    // A garment: where its colour lies (hue, hue width, sat min/max, value
    // min/max, height on the frame min/max, all 0..1 but the hues in degrees)
    // and the dyes it may be: (hue turn, sat scale, sat add, value scale, value add).
    private sealed record Slot(float[] Band, float[][] Dyes, bool Hair = false);

    private static float[] B(float hue, float width, float smin = 0, float smax = 1, float vmin = 0, float vmax = 1, float ymin = 0, float ymax = 1) =>
        new[] { hue, width, smin, smax, vmin, vmax, ymin, ymax };
    private static float[] D(float turn, float smul = 1, float sadd = 0, float vmul = 1, float vadd = 0) => new[] { turn, smul, sadd, vmul, vadd };

    // Greys a nation's black hair (the part of the frame above the shoulders).
    private static Slot GreyHair(float hue, float width, float vmax = 0.4f) => new(B(hue, width, 0, 0.45f, 0.06f, vmax, 0, 0.5f), new[] { D(0, 0.3f, 0, 1.15f, 0.17f) }, true);

    // Greys a hatted man's beard: the hat shares its colours, so only the
    // part of the frame below the brim (standing frames; lying, it stays dark).
    // The voyageur's moustache is as dark as his outline and cannot be told apart.
    private static Slot GreyBeard(float ymin, float ymax, float vmin = 0.06f, float smax = 0.35f, float vmax = 0.32f) =>
        new(B(10, 60, 0, smax, vmin, vmax, ymin, ymax), new[] { D(0, 0.3f, 0, 1.2f, 0.2f) }, true);

    private static readonly Dictionary<string, Slot[]> Sheets = new()
    {
        // The Orkneyman's green coat; his breeches.
        ["LabourerFrames.tres"] = new Slot[] {
            new(B(175, 35, 0.08f), new[] { D(-150, 1.3f, 0, 1.1f), D(0, 0.1f, 0, 1.25f), D(40, 1.4f), D(180, 1.6f, 0, 1.05f) }),
            new(B(225, 25, 0.08f, ymin: 0.6f), new[] { D(160), D(0, 0.2f, 0, 1.1f) }),
            GreyBeard(0.36f, 0.52f, 0.06f, 0.45f, 0.34f) },
        // The tuque, sash and trousers; the gingham shirt.
        ["VoyageurFrames.tres"] = new Slot[] {
            new(B(356, 14, 0.5f), new[] { D(220, 1, 0, 0.85f), D(130, 0.8f, 0, 0.85f), D(30, 0.6f, 0, 0.75f), D(0, 0.25f, 0, 0.55f) }),
            new(B(205, 25, 0.12f), new[] { D(160), D(-80), D(180, 0.5f, 0, 0.9f) }) },
        // The jacket; the canvas trousers, tarred or slops-blue. (His red
        // waistcoat shares his ruddy face's hues, so it stays red.)
        ["SailorFrames.tres"] = new Slot[] {
            new(B(220, 25, 0.15f), new[] { D(170, 0.8f, 0, 1.1f), D(0, 0.15f, 0, 1.2f), D(-80, 0.8f) }),
            new(B(40, 40, 0, 0.3f, 0.55f, 1, 0.65f), new[] { D(180, 1, 0.2f, 0.75f), D(0, 1, 0.2f, 0.8f), D(0, 0.3f, 0, 0.55f) }) },
        ["ClerkFrames.tres"] = new Slot[] {
            new(B(220, 25, 0.15f), new[] { D(170, 0.8f, 0, 1.1f), D(-80, 0.8f), D(0, 0.2f, 0, 0.75f), D(80, 0.8f) }) },
        // The calico dress; the plaid shawl.
        ["CountryWifeFrames.tres"] = new Slot[] {
            new(B(210, 28, 0.15f), new[] { D(150), D(-80, 0.9f), D(60, 0.9f, 0, 0.9f), D(180, 0.6f, 0, 0.8f) }),
            new(B(356, 12, 0.5f), new[] { D(220, 1, 0, 0.9f), D(120, 0.8f, 0, 0.9f), D(45, 1, 0, 1.1f) }) },
        // The greatcoat (grey, below the head): an officer's blue or green.
        ["FactorFrames.tres"] = new Slot[] {
            new(B(25, 40, 0, 0.25f, 0.15f, 0.7f, 0.3f), new[] { D(195, 1, 0.25f, 0.75f), D(100, 1, 0.2f, 0.75f) }) },
        // A Nor'Wester's red coat.
        ["RivalFrames.tres"] = new Slot[] {
            new(B(345, 22, 0.45f), new[] { D(-120, 0.8f, 0, 0.9f), D(140, 0.7f, 0, 0.9f), D(40, 0.6f, 0, 0.8f), D(0, 0.15f, 0, 0.6f) }),
            GreyBeard(0.27f, 0.42f, 0.04f, 0.4f) },
        ["NwcPartnerFrames.tres"] = new Slot[] {
            new(B(120, 30, 0.2f), new[] { D(100), D(-90, 0.7f, 0, 0.9f), D(-130) }) },
        // The boy's white capote, dyed; the girl's red dress.
        ["ChildBoyFrames.tres"] = new Slot[] {
            new(B(40, 40, 0, 0.3f, 0.6f), new[] { D(170, 1, 0.35f, 0.85f), D(-40, 1, 0.4f, 0.8f), D(80, 1, 0.3f, 0.8f) }) },
        ["ChildGirlFrames.tres"] = new Slot[] {
            new(B(358, 12, 0.5f), new[] { D(220, 1, 0, 0.9f), D(120, 0.8f, 0, 0.9f), D(45, 1, 0, 1.1f) }) },
        // The breechcloth's trade stroud: scarlet, blue, green or black.
        ["CreeFrames.tres"] = new Slot[] {
            new(B(356, 10, 0.6f), new[] { D(220, 1, 0, 0.8f), D(120, 0.8f, 0, 0.8f), D(0, 0.2f, 0, 0.5f) }),
            GreyHair(260, 60) },
        ["CreeWomanFrames.tres"] = new Slot[] { GreyHair(300, 60) },
        // The captain's coat, a present from the Company: scarlet, blue or green.
        // His hair is grey already.
        ["CreeCaptainFrames.tres"] = new Slot[] {
            new(B(356, 12, 0.65f), new[] { D(220, 1, 0, 0.8f), D(120, 0.8f, 0, 0.8f) }) },
        ["BlackfootFrames.tres"] = new Slot[] { GreyHair(300, 60) },
        ["DakotaFrames.tres"] = new Slot[] { GreyHair(300, 60) },
        ["DeneFrames.tres"] = new Slot[] { GreyHair(240, 60) },
        ["MandanFrames.tres"] = new Slot[] { GreyHair(320, 50) },
        ["AssiniboineFrames.tres"] = new Slot[] { GreyHair(0, 180, 0.3f) },
    };

    // From this age black hair, or a beard below a hat, shows grey.
    public const int Grey = 50;

    public static string Base(string path)
    {
        if (path == null) return null;
        int i = path.IndexOf('#');
        return i < 0 ? path : path[..i];
    }

    private static Slot[] SlotsFor(string path) =>
        path != null && Sheets.TryGetValue(path[(path.LastIndexOf('/') + 1)..], out var s) ? s : null;

    // The sheet with this person's garments chosen: the same each time for
    // the same name, the hair grey once they are old.
    public static string Dress(Npc n, string path)
    {
        path = Base(path);
        var slots = SlotsFor(path);
        if (slots == null) return path;
        uint h = 2166136261;
        foreach (char c in n.DisplayName ?? "") h = (h ^ c) * 16777619;
        var picks = new string[slots.Length];
        bool any = false;
        for (int i = 0; i < slots.Length; i++)
        {
            int pick = slots[i].Hair ? (n.Age >= Grey ? 1 : 0) : (int)((h >> (i * 7)) % (uint)(slots[i].Dyes.Length + 1));
            picks[i] = pick.ToString();
            any |= pick > 0;
        }
        return any ? path + "#" + string.Join(".", picks) : path;
    }

    // Dev: `-- --lineup` stands every dyed sheet beside the player, one row
    // a sheet: as drawn, then each garment's dyes one at a time.
    public static void Lineup(Node2D parent, Vector2 at)
    {
        int row = 0;
        foreach (var (file, slots) in Sheets)
        {
            string path = "res://Scenes/Npc/" + file;
            var looks = new List<string> { path };
            for (int i = 0; i < slots.Length; i++)
                for (int j = 1; j <= slots[i].Dyes.Length; j++)
                {
                    var picks = new string[slots.Length];
                    for (int k = 0; k < picks.Length; k++) picks[k] = k == i ? j.ToString() : "0";
                    looks.Add(path + "#" + string.Join(".", picks));
                }
            var origin = at + new Vector2(row / 7 * 620, row % 7 * 105);
            for (int c = 0; c < looks.Count && c < 9; c++)
            {
                var s = new AnimatedSprite2D { SpriteFrames = Doll.Load(looks[c]), Material = Material(looks[c]), Centered = false, Scale = Vector2.One * 0.6f };
                var first = s.SpriteFrames.GetFrameTexture("idle", 0);
                if (first != null) s.Offset = new Vector2(-first.GetWidth() / 2f, -first.GetHeight());

                s.ZIndex = 50;
                parent.AddChild(s);
                s.GlobalPosition = origin + new Vector2(c * 66, 0);
                s.Play(s.SpriteFrames.HasAnimation("walk") ? "walk" : "idle");
            }
            row++;
        }
    }

    private static Shader _shader;
    private static readonly Dictionary<string, ShaderMaterial> _materials = new();
    private const int Max = 4;

    // The material that dyes this look, shared by everyone wearing it; null
    // for a sheet as drawn.
    public static ShaderMaterial Material(string path)
    {
        int at = path?.IndexOf('#') ?? -1;
        if (at < 0) return null;
        if (_materials.TryGetValue(path, out var have)) return have;
        var slots = SlotsFor(path[..at]);
        var picks = path[(at + 1)..].Split('.');
        var band = new Vector4[Max]; var range = new Vector4[Max]; var dye = new Vector4[Max]; var add = new float[Max];
        int count = 0;
        for (int i = 0; slots != null && i < slots.Length && i < picks.Length && count < Max; i++)
        {
            if (!int.TryParse(picks[i], out int p) || p < 1 || p > slots[i].Dyes.Length) continue;
            var b = slots[i].Band; var d = slots[i].Dyes[p - 1];
            band[count] = new Vector4(b[0], b[1], b[2], b[3]);
            range[count] = new Vector4(b[4], b[5], b[6], b[7]);
            dye[count] = new Vector4(d[0], d[1], d[2], d[3]);
            add[count] = d[4];
            count++;
        }
        ShaderMaterial m = null;
        if (count > 0)
        {
            _shader ??= new Shader { Code = Code };
            m = new ShaderMaterial { Shader = _shader };
            m.SetShaderParameter("band", band);
            m.SetShaderParameter("range", range);
            m.SetShaderParameter("dye", dye);
            m.SetShaderParameter("vadd", add);
            m.SetShaderParameter("layers", count);
        }
        return _materials[path] = m;
    }

    private const string Code = @"
shader_type canvas_item;
uniform vec4 band[4];    // hue, hue width (degrees), sat min, sat max
uniform vec4 range[4];   // value min, max, height on the frame min, max
uniform vec4 dye[4];     // hue turn (degrees), sat scale, sat add, value scale
uniform float vadd[4];
uniform int layers = 0;

vec3 to_hsv(vec3 c) {
	vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
	vec4 p = mix(vec4(c.bg, K.wz), vec4(c.gb, K.xy), step(c.b, c.g));
	vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));
	float d = q.x - min(q.w, q.y);
	return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + 1e-10)), d / (q.x + 1e-10), q.x);
}

vec3 to_rgb(vec3 c) {
	vec3 p = abs(fract(c.xxx + vec3(1.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0);
	return c.z * mix(vec3(1.0), clamp(p - 1.0, 0.0, 1.0), c.y);
}

// The modulate (a tint, the hurt flash, the dead's grey), without the
// texture that the fragment's COLOR already holds.
varying vec4 tint;
void vertex() { tint = COLOR; }

void fragment() {
	vec4 t = texture(TEXTURE, UV);
	vec3 hsv = to_hsv(t.rgb);
	vec3 col = t.rgb;
	for (int i = 0; i < layers; i++) {
		float d = abs(mod(hsv.x * 360.0 - band[i].x + 180.0, 360.0) - 180.0);
		float w = band[i].y >= 180.0 ? 1.0 : clamp(1.0 - (d - band[i].y) / 6.0, 0.0, 1.0);
		w *= step(band[i].z, hsv.y) * step(hsv.y, band[i].w) * step(range[i].x, hsv.z) * step(hsv.z, range[i].y)
			* step(range[i].z, UV.y) * step(UV.y, range[i].w);
		vec3 n = to_rgb(vec3(fract(hsv.x + dye[i].x / 360.0), clamp(hsv.y * dye[i].y + dye[i].z, 0.0, 1.0), clamp(hsv.z * dye[i].w + vadd[i], 0.0, 1.0)));
		col = mix(col, n, w);
	}
	COLOR = vec4(col, t.a) * tint;
}
";
}
