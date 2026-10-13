using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Player;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.UI;

// What the local player has seen of the country (design doc 10.8): the map
// is fogged except where he has been, and the ground round his own post that
// the men would have told him about. Kept per peer, for its own player.
public static class Exploration
{
    public const float CellPx = 1024f;          // a fog cell: about 25 km
    public const float SightPx = 2000f;         // what a walk reveals either side
    public static int Width => Mathf.CeilToInt(WorldMap.Columns * IsoGrid.TileW / CellPx);
    public static int Height => Mathf.CeilToInt(WorldMap.Rows * IsoGrid.TileH / 2f / CellPx);
    private static bool[] _seen;
    public static int Version { get; private set; }

    private static bool[] Seen => _seen ??= new bool[Width * Height];

    public static bool IsSeen(Vector2 world)
    {
        int x = Mathf.FloorToInt(world.X / CellPx), y = Mathf.FloorToInt(world.Y / CellPx);
        return x >= 0 && y >= 0 && x < Width && y < Height && Seen[y * Width + x];
    }
    public static bool IsSeenCell(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && Seen[y * Width + x];

    public static void Reveal(Vector2 world, float radius)
    {
        int r = Mathf.CeilToInt(radius / CellPx);
        int cx = Mathf.FloorToInt(world.X / CellPx), cy = Mathf.FloorToInt(world.Y / CellPx);
        for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (x < 0 || y < 0 || x >= Width || y >= Height) continue;
                var centre = new Vector2((x + 0.5f) * CellPx, (y + 0.5f) * CellPx);
                if (centre.DistanceTo(world) > radius + CellPx * 0.7f) continue;
                if (Seen[y * Width + x]) continue;
                Seen[y * Width + x] = true;
                Version++;
            }
    }

    public static void Reset() { _seen = null; Version++; }

    // The ground round a man's own post, the road from it and the far end of
    // the road: what anyone at York would have described to a new hand.
    public static void RevealHome(PlayerController p)
    {
        var home = p?.HomePost;
        var at = home != null ? IsoGrid.ToWorld(home.AccessCell) : p?.GlobalPosition ?? WorldLayout.FortCentre;
        Reveal(at, 7000f);
        if (home == Sim.Fort || home == Sim.Outpost)
        {
            foreach (var r in WorldLayout.RiverRoad) Reveal(r, 1800f);
            Reveal(IsoGrid.ToWorld(WorldLayout.OutpostTop), 3000f);
        }
    }

    public static Godot.Collections.Array Save()
    {
        var a = new Godot.Collections.Array();
        for (int i = 0; i < Seen.Length; i++) if (Seen[i]) a.Add(i);
        return a;
    }
    public static void Load(Godot.Collections.Array a)
    {
        _seen = new bool[Width * Height];
        foreach (var v in a) { int i = v.AsInt32(); if (i >= 0 && i < _seen.Length) _seen[i] = true; }
        Version++;
    }
}

// A point the player has marked on the map to make for.
public static class Waypoint
{
    public static Vector2? Target;
    public static void Set(Vector2 world) => Target = world;
    public static void Clear() => Target = null;
}

// Where the task in hand is to be done (design doc P.5: a clear next step).
public static class Objective
{
    public static (Vector2 At, string Label)? Of(PlayerController p)
    {
        if (p == null) return null;
        var t = p.Task;
        if (t == null || t.Kind == TaskKind.None || t.IsComplete && t.Kind != TaskKind.Report) return Report(p);
        var home = p.HomePost ?? Sim.Fort;
        switch (t.Kind)
        {
            case TaskKind.Report: return Report(p);
            case TaskKind.Firewood:
                if (p.Inventory.Has(Items.ItemId.Firewood)) return (IsoGrid.ToWorld(home?.WoodpileCell ?? WorldLayout.WoodpileCell), "the woodpile");
                var tree = Sim.Structures?.NearestTree(p.GlobalPosition, 4000f);
                return tree.HasValue ? (IsoGrid.ToWorld(tree.Value), "a tree to fell") : null;
            case TaskKind.CarryCargo:
                return Sim.Ship != null && Sim.Ship.IsDocked ? (WorldLayout.ShipBerth, "the ship") : (IsoGrid.ToWorld(WorldLayout.Warehouse.DoorCell), "the Depot");
            case TaskKind.DeliverLetter:
                return t.Done == 0 && Sim.Band?.Leader is { } chief && IsInstanceValid(chief) ? (chief.GlobalPosition, chief.DisplayName) : LeaderOf(home);
            case TaskKind.Raid:
                var rival = Economy.Rivalry.PostOf(Economy.Rivalry.RivalOf(p.Faction));
                return rival != null ? (IsoGrid.ToWorld(rival.AccessCell), rival.Title) : null;
            case TaskKind.Courier:
                return Sim.Outpost != null ? (IsoGrid.ToWorld(Sim.Outpost.AccessCell), "Oxford House") : null;
            case TaskKind.Brigade:
                return Convoys.Find(t.Convoy) is { } boats ? (boats.Where, boats.Name) : null;
            case TaskKind.Geese:
                foreach (var s in WorldLayout.Sites) if (s.Species == Ecology.Species.Goose) return (IsoGrid.ToWorld(s.Cell), s.Name);
                return null;
            case TaskKind.DriveOffAnimals:
                Agents.Animal best = null; float bd = 3000f;
                foreach (var a in Sim.Animals) if (a != null && IsInstanceValid(a) && !a.IsDead && a.GlobalPosition.DistanceTo(p.GlobalPosition) < bd) { bd = a.GlobalPosition.DistanceTo(p.GlobalPosition); best = a; }
                return best != null ? (best.GlobalPosition, "the animals") : null;
        }
        return null;
    }

    private static (Vector2, string)? Report(PlayerController p)
    {
        if (p.Task?.Kind != TaskKind.Report || p.Task.IsComplete) return null;
        return LeaderOf(p.HomePost ?? Sim.Fort);
    }

    private static (Vector2, string)? LeaderOf(Economy.FortLedger home)
    {
        var l = home?.Leader;
        return l != null && IsInstanceValid(l) && !l.IsDead ? (l.GlobalPosition, l.DisplayName) : null;
    }

    private static bool IsInstanceValid(GodotObject o) => GodotObject.IsInstanceValid(o);
}

// On the screen: the waypoint and the task's target, as a marker where they
// can be seen or an arrow at the edge pointing the way, with the distance.
public partial class WayfindingHud : Control
{
    private float _revealTimer;
    private PlayerController _player;
    private bool _homeRevealed;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        _player = Sim.Player as PlayerController;
        Sim.LocalPlayerChanged += p => _player = p as PlayerController;
    }

    public override void _Process(double delta)
    {
        _revealTimer -= (float)delta;
        if (_revealTimer <= 0f && _player != null && IsInstanceValid(_player))
        {
            _revealTimer = 0.5f;
            if (!_homeRevealed)
            {
                _homeRevealed = true; Exploration.RevealHome(_player);
                // Dev aid: `-- --revealmap` lifts the fog off the whole country.
                if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--revealmap") >= 0)
                    for (float y = 0; y < Exploration.Height * Exploration.CellPx; y += Exploration.CellPx)
                        for (float x = 0; x < Exploration.Width * Exploration.CellPx; x += Exploration.CellPx) Exploration.Reveal(new Vector2(x, y), 10f);
            }
            Exploration.Reveal(_player.GlobalPosition, Exploration.SightPx);
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_player == null || !IsInstanceValid(_player) || MapPanel.IsOpen) return;
        if (Waypoint.Target is { } w) Mark(w, "waypoint", new Color(1f, 0.82f, 0.3f));
        if (Objective.Of(_player) is { } o) Mark(o.At, o.Label, new Color(0.55f, 0.8f, 1f));
    }

    private void Mark(Vector2 world, string label, Color col)
    {
        var xf = GetViewport().GetCanvasTransform();
        var screen = xf * world;
        var me = xf * _player.GlobalPosition;
        var size = GetViewportRect().Size;
        float km = _player.GlobalPosition.DistanceTo(world) / WorldMap.PxPerKm;
        string dist = km >= 1f ? $"{km:0} km" : $"{km * 1000f:0} m";
        var font = ThemeDB.FallbackFont;
        var shadow = new Color(0, 0, 0, 0.7f);
        var inset = new Rect2(new Vector2(48, 48), size - new Vector2(96, 140));
        if (inset.HasPoint(screen))
        {
            // In sight: a diamond over the spot.
            var p = screen + new Vector2(0, -60);
            DrawColoredPolygon(new[] { p + new Vector2(0, -9), p + new Vector2(7, 0), p + new Vector2(0, 9), p + new Vector2(-7, 0) }, col);
            DrawString(font, p + new Vector2(-100, -14) + Vector2.One, label, HorizontalAlignment.Center, 200, 13, shadow);
            DrawString(font, p + new Vector2(-100, -14), label, HorizontalAlignment.Center, 200, 13, col);
            return;
        }
        // Out of sight: an arrow at the edge of the screen, on the line from you to it.
        var dir = (screen - me).Normalized();
        var centre = size / 2f;
        float t = float.MaxValue;
        if (dir.X != 0) t = Mathf.Min(t, ((dir.X > 0 ? inset.End.X : inset.Position.X) - centre.X) / dir.X);
        if (dir.Y != 0) t = Mathf.Min(t, ((dir.Y > 0 ? inset.End.Y : inset.Position.Y) - centre.Y) / dir.Y);
        var tip = centre + dir * t;
        var side = dir.Orthogonal() * 10f;
        DrawColoredPolygon(new[] { tip, tip - dir * 22f + side, tip - dir * 22f - side }, col);
        var text = $"{label} - {dist}";
        var at = tip - dir * 40f + new Vector2(-110, 5);
        DrawString(font, at + Vector2.One, text, HorizontalAlignment.Center, 220, 13, shadow);
        DrawString(font, at, text, HorizontalAlignment.Center, 220, 13, col);
    }
}
