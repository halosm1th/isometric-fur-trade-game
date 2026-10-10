using System;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.Items;

namespace IsometricFurTradeGame.Scripts.World;

public enum HomePost { Fort, CreeBand, Nwc, Outpost, Assiniboine }

// One person of the starting cast, placed on the map in the editor. The
// marker stands on his bed; its "Work" child is where he spends the day.
// It draws him in the editor and nothing at runtime: the server's
// Population reads every marker and spawns the real man from it, so
// multiplayer and save/load go through the same path as before.
[Tool, GlobalClass]
public partial class NpcSpawn : Node2D
{
    public const string Group = "npc_spawn";

    [Export] public string DisplayName = "New man";
    [Export] public Role Role = Role.Labourer;
    [Export] public Faction Faction = Faction.HBC;
    [Export] public HomePost Home = HomePost.Fort;
    // The household's head (the Factor, the chief, the postmaster).
    [Export] public bool Leader;
    [Export] public SpriteFrames Frames;
    [Export] public Color Tint = Colors.White;
    [Export(PropertyHint.Range, "0.4,1.2,0.01")] public float SpriteScale = 0.8f;
    [Export] public bool Female;
    // 0 = a likely age for the role.
    [Export(PropertyHint.Range, "0,80")] public int Age;
    // What he carries: "Musket, Capote, Pemmican:2" (ItemId names).
    [Export(PropertyHint.MultilineText)] public string Inventory = "";
    // Set on either side of a marriage; the other side is filled in.
    [Export] public NpcSpawn Spouse;
    [Export] public NpcSpawn Father;

    public Vector2I BedCell => IsoGrid.FromWorld(GlobalPosition);
    public Vector2I WorkCell => GetNodeOrNull<Node2D>("Work") is { } w ? IsoGrid.FromWorld(w.GlobalPosition) : BedCell;

    public override void _EnterTree() => AddToGroup(Group);

    public override void _Ready()
    {
        if (!Engine.IsEditorHint()) { Visible = false; return; }
        // A new marker gets a Work point to drag about.
        if (GetNodeOrNull("Work") == null)
        {
            var w = new Marker2D { Name = "Work", Position = new Vector2(64, 32) };
            AddChild(w);
            w.Owner = GetTree().EditedSceneRoot;
        }
    }

    public override void _Process(double delta)
    {
        if (!Engine.IsEditorHint()) { SetProcess(false); return; }
        // Snap to the cell grid so what you place is where he stands.
        GlobalPosition = IsoGrid.ToWorld(IsoGrid.FromWorld(GlobalPosition));
        if (GetNodeOrNull<Node2D>("Work") is { } w) w.GlobalPosition = IsoGrid.ToWorld(IsoGrid.FromWorld(w.GlobalPosition));
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Engine.IsEditorHint()) return;
        DrawCell(Vector2.Zero, new Color(1f, 0.85f, 0.3f, 0.35f));
        if (GetNodeOrNull<Node2D>("Work") is { } w)
        {
            DrawDashedLine(Vector2.Zero, w.Position, new Color(1f, 0.85f, 0.3f, 0.8f), 2f, 8f);
            DrawCell(w.Position, new Color(0.4f, 0.8f, 1f, 0.35f));
        }
        if (Spouse != null && IsInstanceValid(Spouse)) DrawLine(Vector2.Zero, ToLocal(Spouse.GlobalPosition), new Color(1f, 0.5f, 0.6f, 0.6f), 2f);
        if (Father != null && IsInstanceValid(Father)) DrawLine(Vector2.Zero, ToLocal(Father.GlobalPosition), new Color(0.6f, 0.9f, 0.6f, 0.6f), 2f);

        var tex = Frames != null && Frames.HasAnimation("idle") && Frames.GetFrameCount("idle") > 0 ? Frames.GetFrameTexture("idle", 0) : null;
        if (tex != null)
        {
            // Same offset as Npc.tscn's sprite.
            var size = tex.GetSize() * SpriteScale;
            DrawTextureRect(tex, new Rect2(new Vector2(-75, -128) * SpriteScale, size), false, Tint);
        }
        else DrawCircle(new Vector2(0, -40), 18f, Tint);
        var font = ThemeDB.FallbackFont;
        string label = Leader ? DisplayName + " *" : DisplayName;
        DrawString(font, new Vector2(-120, 22), label, HorizontalAlignment.Center, 240, 14, Colors.White);
    }

    private void DrawCell(Vector2 c, Color col)
    {
        float hw = IsoGrid.TileW / 2f, hh = IsoGrid.TileH / 2f;
        DrawColoredPolygon(new[] { c + new Vector2(0, -hh), c + new Vector2(hw, 0), c + new Vector2(0, hh), c + new Vector2(-hw, 0) }, col);
    }

    public Household Household() => Home switch
    {
        HomePost.Fort => Sim.Fort,
        HomePost.CreeBand => Sim.Band,
        HomePost.Nwc => Sim.Nwc,
        HomePost.Outpost => Sim.Outpost,
        HomePost.Assiniboine => Sim.Assiniboine,
        _ => null,
    };

    // A band's people are placed at its winter camp; if the band has
    // already moved, they go where it is.
    public Vector2I Shifted(Vector2I cell, Household home)
    {
        if (home is not BandLedger band || band.Camp == band.WinterGround) return cell;
        var d = IsoGrid.ToWorld(band.Camp) - IsoGrid.ToWorld(band.WinterGround);
        return IsoGrid.FromWorld(IsoGrid.ToWorld(cell) + d);
    }

    public void FillInventory(Agent a)
    {
        foreach (var part in Inventory.Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var bits = part.Split(':');
            int n = bits.Length > 1 && int.TryParse(bits[1].Trim(), out int k) ? k : 1;
            if (Enum.TryParse(bits[0].Trim(), true, out ItemId id)) a.Inventory.Add(id, n);
            else GD.PushWarning($"{Name}: no item called '{bits[0].Trim()}'");
        }
    }
}
