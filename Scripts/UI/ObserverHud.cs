using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.UI;

// The observer's few words at the top of the screen (Core/Observer): the
// keys, what the camera is following, and what is on the move in the
// country. The body's HUD - needs, the hotbar - is put away; the notices and
// the calendar stay, and they are the running news of the country.
public partial class ObserverHud : Control
{
    private Label _label;
    private float _timer;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _label = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, MouseFilter = MouseFilterEnum.Ignore };
        _label.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
        _label.OffsetTop = -70; _label.OffsetBottom = -12;
        _label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f));
        _label.AddThemeConstantOverride("shadow_offset_x", 1); _label.AddThemeConstantOverride("shadow_offset_y", 1);
        AddChild(_label);
        foreach (var name in new[] { "Needs", "Hotbar" })
            if (GetParent()?.GetNodeOrNull<CanvasItem>(name) is { } hud) hud.Visible = false;
        // The player's own objective and waypoint pointers: there is no player.
        foreach (var n in GetTree().GetNodesInGroup("wayfinding")) if (n is CanvasItem ci) ci.Visible = false;
        HideWayfinding(GetTree().CurrentScene);
    }

    private static void HideWayfinding(Node n)
    {
        if (n == null) return;
        if (n is WayfindingHud w) w.Visible = false;
        foreach (var c in n.GetChildren()) HideWayfinding(c);
    }

    public override void _Process(double delta)
    {
        _timer -= (float)delta;
        if (_timer > 0f) return;
        _timer = 0.25f;
        int convoys = 0; foreach (var c in Convoys.All) if (c.Now != ConvoyState.Arrived) convoys++;
        int wars = 0; foreach (var w in War.Parties) if (w.Phase != WarPhase.Over) wars++;
        int raids = 0; foreach (var _ in Plains.LiveRaids()) raids++;
        string follow = Observer.FollowName != "" ? $"\nFollowing: {Observer.FollowName}" : "";
        _label.Text = "Watching the world   ·   WASD fly (Shift faster)   ·   wheel or + / - zoom   ·   Tab the next thing happening   ·   Home York   ·   [ ] the clock, Space stops it   ·   M the map\n"
            + $"{convoys} brigades on the rivers   ·   {TradeTrips.All.Count} trading parties out   ·   {wars} war parties   ·   {raids} raids   ·   {Ecology.BuffaloRun.Active.Count} buffalo runs   ·   {Tiers.LiveCount} people in sight, {Tiers.BackgroundCount} beyond{follow}";
    }
}
