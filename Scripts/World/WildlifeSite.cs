using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Ecology;

namespace IsometricFurTradeGame.Scripts.World;

// A wildlife site (design doc 16.4) placed in the editor: a lodge, a den,
// a marsh, or a herd. The simulation works on the site's count, not on
// single beasts - animals are promoted out of it when someone comes near -
// so this is what you place, not the animals. A herd's Marker2D children,
// in order, are where it goes each month (Jan..Dec).
[Tool, GlobalClass]
public partial class WildlifeSite : Node2D
{
    public const string Group = "wildlife_site";

    [Export] public string SiteName = "Beaver lodge";
    [Export] public Species Species = Species.Beaver;
    [Export] public int Count = 40;
    [Export] public int Capacity = 60;
    [Export(PropertyHint.Range, "100,2000,10")] public float RadiusPx = 350f;

    public Vector2I Cell => IsoGrid.FromWorld(GlobalPosition);

    public Vector2I[] Route()
    {
        var r = new List<Vector2I>();
        foreach (var c in GetChildren()) if (c is Marker2D m) r.Add(IsoGrid.FromWorld(m.GlobalPosition));
        return r.Count > 0 ? r.ToArray() : null;
    }

    public SiteDef ToDef() => new(SiteName, Species, Cell, Count, Capacity, RadiusPx, Route());

    // Every site in the loaded scene, in tree order.
    public static SiteDef[] Collect()
    {
        var list = new List<SiteDef>();
        if (Engine.GetMainLoop() is SceneTree tree)
            foreach (var n in tree.GetNodesInGroup(Group))
                if (n is WildlifeSite s) list.Add(s.ToDef());
        return list.ToArray();
    }

    public override void _EnterTree() => AddToGroup(Group);

    public override void _Ready()
    {
        if (!Engine.IsEditorHint()) Visible = false;
    }

    public override void _Process(double delta)
    {
        if (!Engine.IsEditorHint()) { SetProcess(false); return; }
        GlobalPosition = IsoGrid.ToWorld(IsoGrid.FromWorld(GlobalPosition));
        foreach (var c in GetChildren()) if (c is Marker2D m) m.GlobalPosition = IsoGrid.ToWorld(IsoGrid.FromWorld(m.GlobalPosition));
        QueueRedraw();
    }

    private static readonly string[] Months = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

    public override void _Draw()
    {
        if (!Engine.IsEditorHint()) return;
        var col = Species switch
        {
            Species.Beaver => new Color(0.65f, 0.45f, 0.25f),
            Species.Goose => new Color(0.85f, 0.85f, 0.95f),
            Species.Caribou => new Color(0.75f, 0.6f, 0.4f),
            Species.Buffalo => new Color(0.45f, 0.3f, 0.2f),
            Species.Elk => new Color(0.6f, 0.42f, 0.28f),
            Species.Wolf => new Color(0.55f, 0.55f, 0.6f),
            Species.Hare => new Color(0.8f, 0.75f, 0.65f),
            Species.Ptarmigan => new Color(0.95f, 0.95f, 0.9f),
            _ => new Color(0.6f, 0.8f, 0.6f),
        };
        DrawCircle(Vector2.Zero, RadiusPx, new Color(col, 0.12f));
        DrawArc(Vector2.Zero, RadiusPx, 0f, Mathf.Tau, 64, new Color(col, 0.8f), 3f);
        DrawCircle(Vector2.Zero, 14f, col);
        var font = ThemeDB.FallbackFont;
        DrawString(font, new Vector2(-200, -24), $"{SiteName}  ({Count}/{Capacity})", HorizontalAlignment.Center, 400, 22, Colors.White);

        var pts = new List<Vector2>();
        foreach (var c in GetChildren()) if (c is Marker2D m) pts.Add(m.Position);
        for (int i = 0; i < pts.Count; i++)
        {
            if (i + 1 < pts.Count) DrawLine(pts[i], pts[i + 1], new Color(col, 0.9f), 3f);
            DrawCircle(pts[i], 9f, col);
            DrawString(font, pts[i] + new Vector2(12, -8), i < Months.Length ? Months[i] : $"{i + 1}", HorizontalAlignment.Left, -1, 18, Colors.White);
        }
    }
}
