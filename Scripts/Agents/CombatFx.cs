using Godot;

namespace IsometricFurTradeGame.Scripts.Agents;

// The visual grammar of a fight: a shot is a line and a flash, a ball that
// goes wide kicks up dust, a blow that lands throws blood, and a man who
// falls leaves a pool on the ground behind him. Enough to read who fired at
// whom and whether it told.
//
// All of it is drawn where the blow is resolved, which on a server is the
// server; a client sees its own copy through the snapshot.
public static class CombatFx
{
    private const string Fx = "res://assets/sprites/fx/";
    private static readonly System.Collections.Generic.Dictionary<string, Texture2D[]> Cache = new();

    private static Texture2D[] Frames(string name, int n)
    {
        if (Cache.TryGetValue(name, out var have)) return have;
        var a = new Texture2D[n];
        for (int i = 0; i < n; i++) a[i] = GD.Load<Texture2D>($"{Fx}{name}_{i}.png");
        Cache[name] = a;
        return a;
    }

    // One run of an effect at a place in the world, and then it is gone.
    private static void Play(Node host, string name, int count, Vector2 at, float fps = 14f,
        float scale = 1f, bool flip = false, int z = 31, float fade = 0f)
    {
        var frames = Frames(name, count);
        if (frames.Length == 0 || frames[0] == null) return;
        var s = new Sprite2D
        {
            Texture = frames[0], GlobalPosition = at, ZIndex = z, ZAsRelative = false,
            Scale = new Vector2(flip ? -scale : scale, scale),
        };
        host.AddChild(s);
        var tree = host.GetTree();
        for (int i = 1; i < frames.Length; i++)
        {
            int k = i;
            tree.CreateTimer(k / fps).Timeout += () => { if (GodotObject.IsInstanceValid(s)) s.Texture = frames[k]; };
        }
        tree.CreateTimer(frames.Length / fps + fade).Timeout += () => { if (GodotObject.IsInstanceValid(s)) s.QueueFree(); };
    }

    private static Node Host(Agent a) => a?.GetTree()?.CurrentScene;

    // Powder in the pan, then at the muzzle: the flash and the great cloud
    // of white smoke a flintlock made, which is half of why a volley was
    // fired at close range and then the men went in with what they had.
    public static void Musket(Agent from, Vector2 toward)
    {
        var host = Host(from);
        if (host == null) return;
        bool left = toward.X < from.GlobalPosition.X;
        var muzzle = from.GlobalPosition + new Vector2(left ? -46f : 46f, -46f);
        Play(host, "pan_flash", 2, from.GlobalPosition + new Vector2(left ? -14f : 14f, -52f), 22f, 0.5f, left, 32);
        Play(host, "muzzle_flash", 3, muzzle, 11f, 0.75f, left, 32);
    }

    // A ball that went wide: dust off the ground where it struck.
    public static void Missed(Agent from, Vector2 at)
    {
        var host = Host(from);
        if (host != null) Play(host, "impact_dust", 3, at + new Vector2(GD.RandRange(-26, 26), GD.RandRange(-6, 10)), 9f, 0.7f, GD.Randf() < 0.5f, 6);
    }

    // A blade or a club: the arc of the stroke.
    public static void Swing(Agent from, Agent target)
    {
        var host = Host(from);
        if (host == null) return;
        bool left = target.GlobalPosition.X < from.GlobalPosition.X;
        Play(host, "slash", 4, (from.GlobalPosition + target.GlobalPosition) / 2f + new Vector2(0, -48f), 20f, 0.8f, left);
    }

    // A blow that told.
    public static void Blood(Agent who, float amount)
    {
        var host = Host(who);
        if (host == null || amount < 3f) return;
        Play(host, "blood_hit", 4, who.GlobalPosition + new Vector2(0, -44f), 16f, Mathf.Clamp(0.45f + amount / 70f, 0.5f, 1.1f), GD.Randf() < 0.5f);
    }

    // And the ground where he fell. This one stays: it is drawn under
    // everything and left there.
    public static void Pool(Agent who)
    {
        var host = Host(who);
        if (host == null) return;
        var frames = Frames("blood_pool", 3);
        if (frames.Length < 3 || frames[2] == null) return;
        var s = new Sprite2D
        {
            Texture = frames[0], GlobalPosition = who.GlobalPosition + new Vector2(0, -4f),
            ZIndex = 1, ZAsRelative = false, Scale = new Vector2(0.5f, 0.5f), Modulate = new Color(1, 1, 1, 0.85f),
        };
        host.AddChild(s);
        var tree = host.GetTree();
        tree.CreateTimer(0.5).Timeout += () => { if (GodotObject.IsInstanceValid(s)) { s.Texture = frames[1]; s.Scale = new Vector2(0.6f, 0.6f); } };
        tree.CreateTimer(1.6).Timeout += () => { if (GodotObject.IsInstanceValid(s)) { s.Texture = frames[2]; s.Scale = new Vector2(0.65f, 0.65f); } };
    }

    public static void Shot(Agent from, Vector2 to, bool musket)
    {
        var tree = from.GetTree();
        if (tree?.CurrentScene == null) return;
        var start = from.GlobalPosition + new Vector2(0, -50);
        var line = new Line2D
        {
            Width = musket ? 2f : 1.5f,
            DefaultColor = musket ? new Color(1f, 0.95f, 0.7f, 0.9f) : new Color(0.8f, 0.7f, 0.5f, 0.9f),
            ZIndex = 30, ZAsRelative = false,
        };
        line.AddPoint(start);
        line.AddPoint(to);
        tree.CurrentScene.AddChild(line);
        if (musket)
        {
            var flash = new PointLight2D
            {
                Position = start, Energy = 2.5f, Color = new Color(1f, 0.85f, 0.5f),
                Texture = FlashTexture(), TextureScale = 1.2f, ZIndex = 30, ZAsRelative = false,
            };
            tree.CurrentScene.AddChild(flash);
            tree.CreateTimer(0.08).Timeout += () => { if (GodotObject.IsInstanceValid(flash)) flash.QueueFree(); };
        }
        tree.CreateTimer(musket ? 0.1 : 0.16).Timeout += () => { if (GodotObject.IsInstanceValid(line)) line.QueueFree(); };
    }

    private static Texture2D _flash;
    private static Texture2D FlashTexture()
    {
        if (_flash != null) return _flash;
        var g = new Gradient();
        g.SetColor(0, new Color(1f, 1f, 1f, 1f));
        g.SetColor(1, new Color(0f, 0f, 0f, 0f));
        _flash = new GradientTexture2D { Gradient = g, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0f), Width = 128, Height = 128 };
        return _flash;
    }
}
