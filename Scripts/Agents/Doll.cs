using System.Collections.Generic;
using System.Linq;
using Godot;
using IsometricFurTradeGame.Scripts.Items;

namespace IsometricFurTradeGame.Scripts.Agents;

// The paper-doll man. Instead of one painted sheet per role, the body and
// each thing he wears or carries are drawn as their own layer, every frame
// lined up (tools/sprites/doll renders them from one rigged figure), and a
// man's look is the layers stacked: what he is dressed in, what he has put
// on over it, the gun if he carries one. A look is written as a frames
// path "doll:layer,layer,...", so saves and the snapshot carry it like any
// sheet; each different look is stacked once and shared by everyone who
// wears it, so it costs no more to draw than a painted sheet.
public static class Doll
{
    public const string Prefix = "doll:";
    private const string Dir = "res://assets/sprites/doll/";

    // Things in the pack that show when worn, and the layer each draws.
    private static readonly (ItemId Item, string Layer)[] Worn = { (ItemId.Capote, "capote") };

    // What a voyageur is dressed in under anything he puts on.
    private static readonly string[] Voyageur = { "body", "face", "hair", "moccasins", "trousers", "shirt", "breechcloth", "sash", "tuque" };

    private static string[] _order;
    private static Godot.Collections.Dictionary _anims;
    private static readonly Dictionary<string, SpriteFrames> _looks = new();
    private static readonly Dictionary<string, Image> _layers = new();

    public static bool Is(string path) => path != null && path.StartsWith(Prefix, System.StringComparison.Ordinal);

    // The look for this man, from his dress, what he wears and his weapon.
    public static string For(Npc n)
    {
        var layers = new List<string>(Voyageur);
        foreach (var (item, layer) in Worn)
            if (n.Inventory.IsWorn(item)) layers.Add(layer);
        if (n.Weapon == ItemId.Musket) layers.Add("gun");
        // The paddle, the load, the cup: each shows only in its own work.
        layers.Add("tools");
        return Prefix + string.Join(",", layers);
    }

    // A frames path, doll or painted sheet.
    public static SpriteFrames Load(string path) => Is(path) ? Look(path) : GD.Load<SpriteFrames>(Appearance.Resolve(Garb.Base(path)));

    private static void Manifest()
    {
        if (_order != null) return;
        using var f = FileAccess.Open(Dir + "doll.json", FileAccess.ModeFlags.Read);
        var d = Json.ParseString(f.GetAsText()).AsGodotDictionary();
        _order = d["order"].AsStringArray();
        _anims = d["anims"].AsGodotDictionary();
    }

    private static SpriteFrames Look(string path)
    {
        Manifest();
        var wanted = new HashSet<string>(path[Prefix.Length..].Split(','));
        var layers = _order.Where(wanted.Contains).ToArray();
        string key = string.Join(",", layers);
        if (_looks.TryGetValue(key, out var have)) return have;

        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");
        foreach (var (anim, v) in _anims)
        {
            var a = v.AsGodotDictionary();
            string name = anim.AsString();
            frames.AddAnimation(name);
            frames.SetAnimationSpeed(name, a["fps"].AsDouble());
            frames.SetAnimationLoop(name, a["loop"].AsBool());
            for (int i = 0; i < a["frames"].AsInt32(); i++)
            {
                Image img = null;
                foreach (var layer in layers)
                {
                    var src = Layer($"{layer}/{name}_{i}");
                    if (src == null) continue;
                    if (img == null) img = Image.CreateEmpty(src.GetWidth(), src.GetHeight(), false, Image.Format.Rgba8);
                    img.BlendRect(src, new Rect2I(Vector2I.Zero, src.GetSize()), Vector2I.Zero);
                }
                if (img != null) frames.AddFrame(name, ImageTexture.CreateFromImage(img));
            }
        }
        return _looks[key] = frames;
    }

    private static Image Layer(string stem)
    {
        if (_layers.TryGetValue(stem, out var img)) return img;
        var tex = GD.Load<Texture2D>($"{Dir}{stem}.png");
        img = tex?.GetImage();
        if (img != null)
        {
            if (img.IsCompressed()) img.Decompress();
            img.Convert(Image.Format.Rgba8);
        }
        return _layers[stem] = img;
    }
}
