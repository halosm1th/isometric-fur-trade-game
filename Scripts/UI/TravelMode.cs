using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Player;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.UI;

// Travel mode (design doc 18.3) across the whole country: set a waypoint on
// the map and press G. The camera pulls back, the party goes by at about
// twenty-five miles a day of game time, and the clock runs with the miles.
// It drops back to ordinary play wherever something needs you: anything
// you do yourself, people or beasts coming into sight, a post or camp
// ahead, foul weather, and nightfall when you are hungry or cold. Every
// stop says why. (The river road's own travel stays as it was.)
public partial class TravelMode : Control
{
    public static bool Active { get; private set; }
    private const float SpeedPx = 900f;              // how fast the token moves on screen
    private const float KmPerGameHour = 1.05f;       // ~25 km a day, rests included
    private const float StopNearPx = 1800f;           // a post or camp this close is "ahead"

    private PlayerController _player;
    private List<Vector2> _route;
    private int _leg;
    private string _dest = "";
    private bool _planning;
    private float _checkTimer;
    private readonly HashSet<GodotObject> _alreadyNear = new();
    private readonly HashSet<string> _placesAtStart = new();
    private Vector2 _savedZoom = Vector2.One;
    private int _lastHour = -1;
    private Vector2 _start;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        if (!InputMap.HasAction("set_out"))
        {
            InputMap.AddAction("set_out");
            InputMap.ActionAddEvent("set_out", new InputEventKey { PhysicalKeycode = Key.G });
        }
        _player = Sim.Player as PlayerController;
        Sim.LocalPlayerChanged += p => _player = p as PlayerController;
        // Dev aid: `-- --travelto=Norway House` marks that place and sets out for it.
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--travelto="))
            {
                string name = arg.Substring(11);
                GetTree().CreateTimer(4.0).Timeout += () =>
                {
                    Vector2? at = Posts.Named(name) is { } post ? post.Centre : WorldMap.Place(name) is { } pl ? IsoGrid.ToWorld(pl.Cell) : null;
                    if (at == null) { GD.Print($"[travel] no place called {name}"); return; }
                    Waypoint.Set(at.Value); SetOut();
                };
            }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!e.IsActionPressed("set_out") || (ChatBox.Instance?.IsTyping ?? false)) return;
        GetViewport().SetInputAsHandled();
        if (Active || _planning) { Stop("You stop."); return; }
        SetOut();
    }

    public async void SetOut()
    {
        if (_player == null || _player.IsDead) return;
        if (Waypoint.Target is not { } target) { Say("Mark a waypoint on the map (M, then click) to set out for it."); return; }
        _planning = true;
        Say("You look over the way ahead...");
        bool frozen = Sim.Terrain?.IsFrozen ?? false;
        var route = await Routes.PlanAsync(_player.GlobalPosition, target, frozen);
        _planning = false;
        if (route == null || route.Count < 2) { Say(frozen ? "There's no way there that you can see." : "There's no way there on foot that you can see - open water lies between. Wait for the freeze-up, or go by canoe."); return; }
        _route = route; _leg = 1; _start = _player.GlobalPosition;
        _dest = NearestPlaceName(target) ?? "the waypoint";
        _alreadyNear.Clear(); _placesAtStart.Clear();
        foreach (var n in Sim.Npcs) if (n.GlobalPosition.DistanceTo(_player.GlobalPosition) < 1400f) _alreadyNear.Add(n);
        foreach (var a in Sim.Animals) if (a.GlobalPosition.DistanceTo(_player.GlobalPosition) < 1400f) _alreadyNear.Add(a);
        foreach (var (name, at) in Places()) if (at.DistanceTo(_player.GlobalPosition) < StopNearPx * 1.5f) _placesAtStart.Add(name);
        var cam = _player.GetNodeOrNull<Camera2D>("Camera2D");
        if (cam != null) { _savedZoom = cam.Zoom; cam.Zoom = _savedZoom * 0.55f; }
        Active = true;
        float km = 0f; for (int i = 1; i < route.Count; i++) km += route[i - 1].DistanceTo(route[i]) / WorldMap.PxPerKm;
        Say($"You set out for {_dest}: about {km:0} km, {km / (KmPerGameHour * 24f):0.#} days. G or any step of your own stops.");
    }

    private void Stop(string why)
    {
        if (!Active && !_planning) return;
        Active = false; _planning = false; _route = null;
        if (Net.Net.IsServer) GameCalendar.Instance?.CancelSkip();
        var cam = _player?.GetNodeOrNull<Camera2D>("Camera2D");
        if (cam != null) cam.Zoom = _savedZoom;
        if (why != "") Say(why);
    }

    private void Say(string text) { GD.Print($"[travel] {GameCalendar.Instance?.DateString} {text}"); _player?.EmitSignal(PlayerController.SignalName.Notice, text); }

    public override void _Process(double delta)
    {
        QueueRedraw();
        if (!Active) return;
        if (_player == null || !IsInstanceValid(_player) || _player.IsDead) { Stop(""); return; }
        bool typing = ChatBox.Instance?.IsTyping ?? false;
        if (!typing && Input.GetVector("move_left", "move_right", "move_up", "move_down") != Vector2.Zero) { Stop("You stop and look about you."); return; }
        var cal = GameCalendar.Instance;

        // The march goes with the clock: each frame you cover the ground the
        // hours that actually passed would carry you (the server keeps the
        // clock racing while you go; a joined player's march can't hurry the
        // shared day, so his goes at a fixed pace on screen).
        float moved;
        if (Net.Net.IsServer && cal != null)
        {
            if (cal.SkipRemaining < 0.5f) cal.SkipHours(0.5f - cal.SkipRemaining);
            moved = cal.DeltaGameHours * KmPerGameHour * WorldMap.PxPerKm;
        }
        else moved = SpeedPx * (float)delta;
        float budget = moved;
        while (budget > 0f && _leg < _route.Count)
        {
            var to = _route[_leg];
            var d = to - _player.GlobalPosition;
            float len = d.Length();
            if (len <= budget) { _player.GlobalPosition = to; budget -= len; _leg++; }
            else { _player.GlobalPosition += d / len * budget; budget = 0f; }
        }
        if (_leg >= _route.Count) { Stop($"You have come to {_dest}."); Waypoint.Clear(); return; }

        // Eat on the march when hungry, if there is anything in the pack.
        if (_player.Needs.Hunger < 45f) _player.Eat();

        _checkTimer -= (float)delta;
        if (_checkTimer > 0f) return;
        _checkTimer = 0.2f;
        if (cal != null && cal.Weather.Current is SkyState.Blizzard or SkyState.Storm) { Stop("The weather closes in. You can't go on in this - find shelter or make a fire."); return; }
        int hour = Mathf.FloorToInt(cal?.Hour ?? 12f);
        if (hour == 20 && _lastHour != 20 && (_player.Needs.Hunger < 35f || _player.Needs.Warmth < 45f))
        { _lastHour = hour; Stop(_player.Needs.Warmth < 45f ? "Night comes on and you are cold. Make camp and a fire before going on." : "Night comes on and you are hungry. Eat, or hunt, before going on."); return; }
        _lastHour = hour;
        // Nobody near home is news: the first stretch out of a post is full of its own people.
        if (_player.GlobalPosition.DistanceTo(_start) < 3000f) return;
        foreach (var n in Sim.Npcs)
        {
            if (n.IsDead || _alreadyNear.Contains(n) || n.Tier == Tier.Background) continue;
            if (n.GlobalPosition.DistanceTo(_player.GlobalPosition) < 1100f) { Stop($"You come upon {n.Describe()}."); return; }
        }
        foreach (var a in Sim.Animals)
        {
            // Wolves stop you; deer and geese only if you came out for them.
            if (a.IsDead || _alreadyNear.Contains(a) || a.Species != Ecology.Species.Wolf) continue;
            if (a.GlobalPosition.DistanceTo(_player.GlobalPosition) < 1000f) { Stop("Wolves ahead."); return; }
        }
        foreach (var (name, at) in Places())
            if (!_placesAtStart.Contains(name) && at.DistanceTo(_player.GlobalPosition) < StopNearPx) { Stop($"{name} is ahead."); return; }
    }

    private static IEnumerable<(string, Vector2)> Places()
    {
        yield return ("York Factory", WorldLayout.FortCentre);
        yield return ("Oxford House", IsoGrid.ToWorld(WorldLayout.OutpostTop));
        yield return ("The Nor'Westers' house", IsoGrid.ToWorld(WorldLayout.NwcTop));
        foreach (var p in Posts.Standing()) yield return (p.Name, p.Centre);
        foreach (var c in Camps.All) if (!c.IsFamily) yield return ($"A camp of the {Camps.NationName(c.Nation)}", IsoGrid.ToWorld(c.Centre));
    }

    private static string NearestPlaceName(Vector2 at)
    {
        string best = null; float bd = 2500f;
        foreach (var (name, p) in Places()) { float d = p.DistanceTo(at); if (d < bd) { bd = d; best = name; } }
        return best;
    }

    public override void _Draw()
    {
        if (!Active && !_planning) return;
        var font = ThemeDB.FallbackFont;
        var size = GetViewportRect().Size;
        string text = _planning ? "Looking over the way ahead..." : $"Travelling to {_dest}  -  {GameCalendar.Instance?.DateString}  -  G or any step stops";
        DrawString(font, new Vector2(1, 81), text, HorizontalAlignment.Center, size.X, 17, new Color(0, 0, 0, 0.7f));
        DrawString(font, new Vector2(0, 80), text, HorizontalAlignment.Center, size.X, 17, new Color(1f, 0.92f, 0.75f));
    }
}
