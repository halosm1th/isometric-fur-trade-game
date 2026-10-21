using Godot;
using IsometricFurTradeGame.Scripts.Core;
using NetHub = IsometricFurTradeGame.Scripts.Net.Net;

namespace IsometricFurTradeGame.Scripts.Player;

// The observer (Core/Observer): the local player as a ghost. No body to
// see, no needs, nothing to hit, nobody's witness - Sim.AgentsWithin leaves
// a ghost out, so no camp sees it and no raider is caught by it - but still
// the point the live simulation, the terrain and the weather gather round, so
// wherever the camera goes the country is alive in full. It flies at the
// same speed across the screen at any zoom.
public partial class PlayerController
{
    public bool Ghost { get; private set; }
    private float _observerZoom = 0.5f;
    public const float MinZoom = 0.012f, MaxZoom = 1.6f;

    private void BecomeGhost()
    {
        Ghost = true;
        // Dev: `--observezoom=0.05` starts the camera that far out.
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--observezoom=") && float.TryParse(arg.Substring(14), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z)) _observerZoom = Mathf.Clamp(z, MinZoom, MaxZoom);
        Needs.GodMode = true;
        Invulnerable = true;
        CollisionLayer = 0; CollisionMask = 0;
        if (_sprite != null) _sprite.Visible = false;
        foreach (var child in GetChildren())
            if (child is CanvasItem ci && child is not Camera2D) ci.Visible = false;
        var cam = GetNodeOrNull<Camera2D>("Camera2D");
        if (cam != null) { cam.Zoom = Vector2.One * _observerZoom; cam.PositionSmoothingEnabled = false; }
        // The country as the map shows it, and what is moving on it, under the
        // camera when it is far out; and the observer's own few words.
        var root = GetTree().CurrentScene;
        root?.CallDeferred(Node.MethodName.AddChild, new UI.ObserverView { Name = "ObserverView" });
        root?.GetNodeOrNull<CanvasLayer>("HUD")?.CallDeferred(Node.MethodName.AddChild, new UI.ObserverHud { Name = "ObserverHud" });
    }

    private void ObserverMove(float dt)
    {
        var cam = GetNodeOrNull<Camera2D>("Camera2D");
        bool typing = UI.ChatBox.Instance != null && UI.ChatBox.Instance.IsTyping;
        var input = typing ? Vector2.Zero : Input.GetVector("move_left", "move_right", "move_up", "move_down");
        if (input != Vector2.Zero)
        {
            Observer.Follow = null; Observer.FollowName = "";
            float speed = 900f * (Input.IsActionPressed("sprint") ? 3.5f : 1f) / Mathf.Max(0.01f, _observerZoom);
            GlobalPosition += input * speed * dt;
        }
        else if (Observer.Follow != null)
        {
            var at = Observer.Follow();
            if (at is Vector2 p) GlobalPosition = GlobalPosition.Lerp(p, Mathf.Min(1f, dt * 4f));
            else { Observer.Follow = null; Observer.FollowName = ""; }
        }
        var lo = Vector2.Zero; var hi = new Vector2(World.WorldMap.Columns * 128f, World.WorldMap.Rows * 32f);
        GlobalPosition = GlobalPosition.Clamp(lo, hi);
        if (cam != null && !cam.Zoom.IsEqualApprox(Vector2.One * _observerZoom)) cam.Zoom = cam.Zoom.Lerp(Vector2.One * _observerZoom, Mathf.Min(1f, dt * 10f));
        Velocity = Vector2.Zero;
    }

    // Returns true when the event was the observer's own.
    private bool ObserverInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp) { _observerZoom = Mathf.Min(MaxZoom, _observerZoom * 1.18f); return true; }
            if (mb.ButtonIndex == MouseButton.WheelDown) { _observerZoom = Mathf.Max(MinZoom, _observerZoom / 1.18f); return true; }
        }
        if (e is InputEventKey k && k.Pressed && !k.Echo)
        {
            if (k.PhysicalKeycode is Key.Equal or Key.KpAdd) { _observerZoom = Mathf.Min(MaxZoom, _observerZoom * 1.5f); return true; }
            if (k.PhysicalKeycode is Key.Minus or Key.KpSubtract) { _observerZoom = Mathf.Max(MinZoom, _observerZoom / 1.5f); return true; }
            if (k.PhysicalKeycode == Key.Tab)
            {
                // The next thing happening in the country.
                var all = Observer.Happenings();
                if (all.Count == 0) { Say("Nothing is moving in the country just now."); return true; }
                int i = (all.FindIndex(h => h.Name == Observer.FollowName) + 1) % all.Count;
                Observer.FollowName = all[i].Name; Observer.Follow = all[i].Where;
                if (Observer.Follow() is Vector2 p) GlobalPosition = p;
                if (_observerZoom < 0.2f) _observerZoom = 0.35f;
                return true;
            }
            if (k.PhysicalKeycode == Key.Home) { GlobalPosition = World.IsoGrid.ToWorld(World.WorldLayout.GateOutside); Observer.Follow = null; Observer.FollowName = ""; return true; }
        }
        return false;
    }

    public float ObserverZoom => _observerZoom;
}
