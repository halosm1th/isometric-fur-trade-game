using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.Player;
using IsometricFurTradeGame.Scripts.World;
using NetHub = IsometricFurTradeGame.Scripts.Net.Net;

namespace IsometricFurTradeGame.Scripts.UI;

// Fishing with a line: a game of three parts.
//   1. Aim and throw. The fish show as dark shapes under the water near you;
//      put the ring on one (mouse, or the left stick) and throw (click / E /
//      A / RT). The hook has to land in the water close to a fish.
//   2. Wait. The fish turns, comes to the hook, noses it, and after a second
//      or two it takes it - the float goes under.
//   3. Play it. A needle swings along the bar; strike (Space / click / E / A)
//      while it is over the green, which is where the fish gives you line.
//      Enough good pulls and it is landed; too many bad ones, or too long
//      without a good one, and it throws the hook.
// Client side: what you land is sent to the host
// ("fish_catch"), who takes it from the fishery (World/Angling).
public partial class Fishing : Node2D
{
    public static Fishing Instance { get; private set; }
    public static bool IsOpen => Instance != null && IsInstanceValid(Instance) && Instance._state != St.Closed;

    private enum St { Closed, Aim, Fly, Wait, Fight, Landed }
    private St _state = St.Closed;
    private PlayerController _p;

    private const float CastRange = 400f;     // how far the line goes out
    private const float BiteRadius = 90f;     // about a tile: a fish that close comes to the hook
    private const float FlyTime = 0.55f;

    private sealed class Fish
    {
        public Vector2 Pos, Target, Heading = Vector2.Right;
        public float Speed = 18f, Retarget, Phase;
        public int Size = 1;           // 1 small, 2 good, 3 big
        public bool Coming, Fleeing;
    }
    private readonly List<Fish> _fish = new();
    private float _restock;

    private Vector2 _reticle, _bobber, _from;
    private float _t, _stateT;
    private Fish _biter;
    private float _latchIn = -1f;    // seconds until the fish takes the hook
    private float _nibble;           // a twitch of the float
    private readonly List<(Vector2 at, float age)> _ripples = new();
    private string _msg = ""; private float _msgFor;
    private bool _wasHeld = true;

    // The fight on the bar (all 0..1 along it).
    private float _needle, _needleDir = 1f, _needleSpeed, _zoneC, _zoneW, _zoneDrift, _zoneVel;
    private int _pulls, _need, _misses;
    private const int MaxMisses = 3;
    private float _sinceGood, _flash; private bool _flashGood;

    private CanvasLayer _layer;
    private Control _hud;

    // ---- open and close --------------------------------------------------------

    // Take the line out. `at` is where the pointer or the stick is aiming.
    public static string Begin(PlayerController p, Vector2? at, Vector2 aimDir)
    {
        if (p == null || p.IsDead) return "";
        if (!p.Inventory.Has(ItemId.FishingLine)) return "You have no fishing line.";
        if (GameCalendar.Instance != null && GameCalendar.Instance.IsFrozen) return "The water is frozen. You could jig through the ice if you had the patience - not yet.";
        if (Sim.Terrain == null || !Sim.Terrain.IsWaterNear(p.GlobalPosition, CastRange - 60f)) return "Take the line to the water's edge.";
        if (IsOpen) return "";
        var f = new Fishing { _p = p, Name = "Fishing" };
        p.AddChild(f);
        f._reticle = at ?? p.GlobalPosition + aimDir.Normalized() * 180f;
        f.ClampReticle();
        f.Enter(St.Aim);
        f.Say("The line is out. Put the ring on a fish and throw.");
        return "";
    }

    // Dev: `--fish` takes the line out a few seconds in; `--fish=auto` also
    // plays it (aims at the nearest fish, strikes on the green most times)
    // and saves user://fish_<part>.png at each part.
    private bool _auto; private readonly HashSet<string> _shot = new();
    public static void DevStart(PlayerController p, bool auto)
    {
        if (!p.Inventory.Has(ItemId.FishingLine)) p.Inventory.Add(ItemId.FishingLine, 1);
        string why = Begin(p, null, Vector2.Down);
        GD.Print($"[fish] dev start: {(IsOpen ? "line out" : why)}");
        if (IsOpen) Instance._auto = auto;
    }
    private void DevShot(string tag)
    {
        if (!_auto || !_shot.Add(tag)) return;
        var path = ProjectSettings.GlobalizePath($"user://fish_{tag}.png");
        GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"[fish] {tag}: {path}");
    }
    private bool AutoPress()
    {
        switch (_state)
        {
            case St.Aim:
            {
                Fish near = null; float bd = CastRange;
                foreach (var f in _fish) if (!f.Fleeing && f.Pos.DistanceTo(_p.GlobalPosition) < bd) { bd = f.Pos.DistanceTo(_p.GlobalPosition); near = f; }
                if (near != null) _reticle = _reticle.Lerp(near.Pos, 0.08f);
                if (_stateT > 1.2f) DevShot("aim");
                return _stateT > 1.6f && near != null && _reticle.DistanceTo(near.Pos) < 20f;
            }
            case St.Wait: if (_latchIn > 0f) DevShot("wait"); return false;
            case St.Fight:
                if (_stateT > 1.2f) DevShot("fight");
                return _stateT > 0.5f && Mathf.Abs(_needle - _zoneC) < _zoneW * 0.3f && GD.Randf() < 0.2f;
            case St.Landed: if (_stateT > 0.6f) DevShot("landed"); return false;
        }
        return false;
    }

    public override void _Ready()
    {
        Instance = this;
        TopLevel = true; GlobalPosition = Vector2.Zero;
        ZIndex = 60; ZAsRelative = false;
        _layer = new CanvasLayer { Layer = 35 };
        AddChild(_layer);
        _hud = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _hud.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _hud.Draw += DrawHud;
        _layer.AddChild(_hud);
        StockFish(4 + (int)(GD.Randi() % 3));
    }

    public override void _ExitTree() { if (Instance == this) Instance = null; }

    private void Close()
    {
        _state = St.Closed;
        QueueFree();
    }

    private void Enter(St s) { _state = s; _stateT = 0f; _wasHeld = true; }
    private void Say(string text, float secs = 3f) { _msg = text; _msgFor = secs; }

    // ---- the fish ------------------------------------------------------------------

    private static bool Water(Vector2 at) => Sim.Terrain != null && Sim.Terrain.IsWater(IsoGrid.FromWorld(at));

    private void StockFish(int n)
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        for (int tries = 0; tries < n * 30 && n > 0; tries++)
        {
            var at = _p.GlobalPosition + Vector2.FromAngle(rng.RandfRange(0f, Mathf.Tau)) * rng.RandfRange(90f, CastRange + 40f);
            if (!Water(at)) continue;
            float r = rng.Randf();
            var f = new Fish { Pos = at, Target = at, Phase = rng.Randf() * 10f, Size = r < 0.55f ? 1 : r < 0.88f ? 2 : 3 };
            f.Speed = 22f - 3f * f.Size;
            _fish.Add(f); n--;
        }
    }

    private void SwimFish(float dt)
    {
        for (int i = _fish.Count - 1; i >= 0; i--)
        {
            var f = _fish[i];
            f.Phase += dt;
            if (f.Fleeing)
            {
                f.Pos += f.Heading * 90f * dt;
                if (f.Pos.DistanceTo(_p.GlobalPosition) > CastRange + 220f || !Water(f.Pos)) _fish.RemoveAt(i);
                continue;
            }
            if (f.Coming && _state == St.Fight) continue;    // on the line: the bar moves it
            if (f.Coming) f.Target = _bobber + f.Heading * -6f;
            else
            {
                f.Retarget -= dt;
                if (f.Retarget <= 0f || f.Pos.DistanceTo(f.Target) < 6f)
                {
                    f.Retarget = (float)GD.RandRange(2.0, 5.0);
                    var to = f.Pos + Vector2.FromAngle((float)GD.RandRange(0, Mathf.Tau)) * (float)GD.RandRange(30, 110);
                    if (Water(to)) f.Target = to;
                }
            }
            var off = f.Target - f.Pos;
            if (off.Length() > 2f)
            {
                var want = off.Normalized();
                f.Heading = f.Heading.Lerp(want, Mathf.Min(1f, dt * 3f)).Normalized();
                float sp = f.Coming ? 38f : f.Speed;
                var next = f.Pos + f.Heading * Mathf.Min(sp * dt, off.Length());
                if (Water(next)) f.Pos = next; else f.Retarget = 0f;
            }
        }
        // More come in, now and then, while the line is out.
        _restock -= dt;
        if (_restock <= 0f) { _restock = 8f; if (_fish.Count < 4) StockFish(1); }
    }

    // ---- input ----------------------------------------------------------------------

    private static bool Button() =>
        Input.IsActionPressed("interact") || Input.IsActionPressed("ui_accept") || Input.IsActionPressed("use_ahead")
        || Input.IsActionPressed("pause_clock") || (Input.IsMouseButtonPressed(MouseButton.Left) && !Pad.Synthetic);

    public override void _Input(InputEvent e)
    {
        if (_state == St.Closed) return;
        if (e.IsActionPressed("ui_cancel") || e.IsActionPressed("pause_menu") || e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
        {
            GetViewport().SetInputAsHandled();
            if (_state == St.Fight) { LoseFish("You let the line go slack. The fish is off."); return; }
            _p.Say("You wind in the line.");
            Close();
            return;
        }
        if (e is InputEventMouseMotion && !Pad.Synthetic && _state == St.Aim) { _reticle = _p.GetGlobalMousePosition(); ClampReticle(); }
        // The strike and the throw are read in _Process; the world doesn't
        // also get the click or the key.
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } || e.IsAction("interact") || e.IsAction("ui_accept")
            || e.IsAction("use_ahead") || e.IsAction("pause_clock"))
            GetViewport().SetInputAsHandled();
    }

    private void ClampReticle()
    {
        var off = _reticle - _p.GlobalPosition;
        if (off.Length() > CastRange) _reticle = _p.GlobalPosition + off.Normalized() * CastRange;
    }

    // ---- the parts -------------------------------------------------------------------

    public override void _Process(double delta)
    {
        if (_state == St.Closed) return;
        float dt = (float)delta;
        if (!IsInstanceValid(_p) || _p.IsDead || (DialogueMenu.Instance?.IsOpen ?? false) || PauseMenu.IsOpen
            || (GameCalendar.Instance != null && GameCalendar.Instance.IsFrozen)) { Close(); return; }
        _stateT += dt;
        _msgFor -= dt;
        bool held = _auto ? AutoPress() : Button();
        bool pressed = held && !_wasHeld;
        _wasHeld = held;

        SwimFish(dt);
        for (int i = _ripples.Count - 1; i >= 0; i--) { var r = _ripples[i]; r.age += dt; if (r.age > 1.4f) _ripples.RemoveAt(i); else _ripples[i] = r; }

        switch (_state)
        {
            case St.Aim:
            {
                var stick = Input.GetVector("move_left", "move_right", "move_up", "move_down");
                if (stick.LengthSquared() > 0.04f) { _reticle += stick * 420f * dt; ClampReticle(); }
                if (pressed && _stateT > 0.15f)
                {
                    _from = RodTip(); _bobber = _reticle; _t = 0f;
                    Enter(St.Fly);
                }
                break;
            }
            case St.Fly:
                _t += dt;
                if (_t >= FlyTime) Landed();
                break;
            case St.Wait:
                Waiting(dt, pressed);
                break;
            case St.Fight:
                Fight(dt, pressed);
                break;
            case St.Landed:
                if (_stateT > 1.4f || (pressed && _stateT > 0.4f)) Enter(St.Aim);
                break;
        }
        QueueRedraw();
        _hud.QueueRedraw();
    }

    private Vector2 RodTip() => _p.GlobalPosition + new Vector2(_bobber.X >= _p.GlobalPosition.X || _state == St.Aim && _reticle.X >= _p.GlobalPosition.X ? 14f : -14f, -44f);

    private void Landed()
    {
        _ripples.Add((_bobber, 0f));
        if (!Water(_bobber)) { Say("The hook lands on the bank. Throw it into the water."); Enter(St.Aim); return; }
        // The nearest fish within a tile of the hook comes to it.
        _biter = null; float best = BiteRadius;
        foreach (var f in _fish)
        {
            if (f.Fleeing) continue;
            float d = f.Pos.DistanceTo(_bobber);
            if (d < best) { best = d; _biter = f; }
        }
        if (_biter != null) _biter.Coming = true;
        _latchIn = -1f; _nibble = 0f;
        Enter(St.Wait);
    }

    private void Waiting(float dt, bool pressed)
    {
        if (pressed && _stateT > 0.25f)
        {
            // Striking at nothing: the hook comes back.
            if (_biter != null) { _biter.Coming = false; _biter.Retarget = 0f; }
            _biter = null;
            Say(_latchIn > 0f ? "Too soon - you pull the hook out of its mouth." : "You draw the line in.");
            Enter(St.Aim);
            return;
        }
        if (_biter == null)
        {
            if (_stateT > 4f) { Say("Nothing comes to the hook. Throw where you see the fish."); Enter(St.Aim); }
            return;
        }
        if (_latchIn < 0f)
        {
            if (_biter.Pos.DistanceTo(_bobber) < 14f) _latchIn = (float)GD.RandRange(1.0, 2.6);   // a sec or two of nosing it
            else if (_stateT > 7f) { _biter.Coming = false; _biter = null; Say("It turns away."); Enter(St.Aim); }
            return;
        }
        _latchIn -= dt;
        _nibble = Mathf.Max(0f, _nibble - dt * 4f);
        if (GD.Randf() < dt * 2.2f) _nibble = 1f;      // a twitch
        if (_latchIn <= 0f) StartFight();
    }

    private void StartFight()
    {
        int s = _biter.Size;
        float hand = _p.Skills.Woodcraft;
        _need = 2 + s;
        _pulls = 0; _misses = 0; _sinceGood = 0f;
        _needle = 0f; _needleDir = 1f;
        _needleSpeed = 0.85f + 0.3f * s;                       // bars per second
        _zoneW = Mathf.Clamp(0.24f - 0.035f * s + 0.12f * hand, 0.1f, 0.34f);
        _zoneDrift = 0.08f + 0.07f * s;
        _zoneC = (float)GD.RandRange(0.3, 0.7); _zoneVel = 1f;
        _ripples.Add((_bobber, 0f)); _ripples.Add((_bobber, 0.25f));
        Say("It takes the hook! Strike when the needle is on the green.", 2.5f);
        Enter(St.Fight);
        _wasHeld = true;
    }

    private void Fight(float dt, bool pressed)
    {
        _sinceGood += dt;
        if (_auto) _wasHeld = false;
        _flash = Mathf.Max(0f, _flash - dt * 2.5f);
        _needle += _needleDir * _needleSpeed * dt;
        if (_needle > 1f) { _needle = 2f - _needle; _needleDir = -1f; }
        if (_needle < 0f) { _needle = -_needle; _needleDir = 1f; }
        // The fish runs this way and that: the green goes with it.
        _zoneC += _zoneVel * _zoneDrift * dt;
        float half = _zoneW / 2f;
        if (_zoneC > 1f - half) { _zoneC = 1f - half; _zoneVel = -1f; }
        if (_zoneC < half) { _zoneC = half; _zoneVel = 1f; }
        if (GD.Randf() < dt * 0.6f) _zoneVel = -_zoneVel;
        // The fish on the line thrashes by the float.
        _biter.Pos = _bobber + new Vector2(Mathf.Sin(_stateT * 9f) * 5f, Mathf.Cos(_stateT * 7f) * 2.5f);

        if (pressed && _stateT > 0.3f)
        {
            if (Mathf.Abs(_needle - _zoneC) <= half)
            {
                _pulls++; _sinceGood = 0f; _flash = 1f; _flashGood = true;
                // Line in: the float comes toward you a share of the way.
                _bobber = _bobber.Lerp(_p.GlobalPosition + new Vector2(0, 10), 1f / (_need - _pulls + 1.5f));
                _ripples.Add((_bobber, 0f));
                _zoneC = (float)GD.RandRange(half, 1f - half);       // it turns: find the green again
                if (_pulls >= _need) { Land(); return; }
            }
            else
            {
                _misses++; _flash = 1f; _flashGood = false;
                _bobber = _bobber.Lerp(_bobber + (_bobber - _p.GlobalPosition).Normalized() * 40f, 1f);
                if (!Water(_bobber)) _bobber = _bobber.Lerp(_p.GlobalPosition, 0.2f);
                if (_misses >= MaxMisses) { LoseFish("The line goes slack - it has thrown the hook."); return; }
            }
        }
        if (_sinceGood > 6f) LoseFish("You gave it too long: it shakes the hook loose.");
    }

    private void Land()
    {
        var f = _biter;
        _fish.Remove(f); _biter = null;
        NetHub.I?.Act("fish_catch", new Godot.Collections.Array { f.Size, _bobber.X, _bobber.Y });
        Say(f.Size switch { 3 => "Landed - a big one!", 2 => "Landed - a good fish.", _ => "Landed." }, 2f);
        _restock = 3f;
        Enter(St.Landed);
    }

    private void LoseFish(string why)
    {
        if (_biter != null) { _biter.Coming = false; _biter.Fleeing = true; _biter.Heading = (_biter.Pos - _p.GlobalPosition).Normalized(); }
        _biter = null;
        Say(why);
        Enter(St.Aim);
    }

    // ---- drawing: the water (world space) ----------------------------------------------

    private static readonly Vector2[] Body = MakeEllipse(1f, 0.38f, 14);
    private static Vector2[] MakeEllipse(float rx, float ry, int n)
    {
        var pts = new Vector2[n];
        for (int i = 0; i < n; i++) { float a = Mathf.Tau * i / n; pts[i] = new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry); }
        return pts;
    }

    public override void _Draw()
    {
        if (_state == St.Closed) return;
        var shade = new Color(0.04f, 0.08f, 0.12f, 0.42f);
        foreach (var f in _fish)
        {
            float len = 12f + 6f * f.Size;
            float wag = Mathf.Sin(f.Phase * (f.Coming ? 14f : 6f)) * 0.25f;
            DrawSetTransform(f.Pos, f.Heading.Angle(), new Vector2(1f, 0.6f));    // flattened: seen through the water
            var body = new Vector2[Body.Length];
            for (int i = 0; i < Body.Length; i++) body[i] = Body[i] * len;
            DrawColoredPolygon(body, shade);
            var tailRoot = new Vector2(-len * 0.9f, 0f);
            var tail = new[] { tailRoot, tailRoot + new Vector2(-len * 0.55f, len * (0.4f + wag)), tailRoot + new Vector2(-len * 0.55f, -len * (0.4f - wag)) };
            DrawColoredPolygon(tail, shade);
        }
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);

        foreach (var (at, age) in _ripples)
        {
            float r = 6f + age * 26f;
            DrawSetTransform(at, 0f, new Vector2(1f, 0.5f));
            DrawArc(Vector2.Zero, r, 0f, Mathf.Tau, 24, new Color(0.9f, 0.95f, 1f, 0.55f * (1f - age / 1.4f)), 1.5f);
        }
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);

        if (_state == St.Aim)
        {
            bool wet = Water(_reticle);
            bool fishy = false;
            foreach (var f in _fish) if (!f.Fleeing && f.Pos.DistanceTo(_reticle) < BiteRadius) { fishy = true; break; }
            var c = !wet ? new Color(0.85f, 0.3f, 0.25f, 0.85f) : fishy ? new Color(0.35f, 0.95f, 0.45f, 0.95f) : new Color(0.95f, 0.95f, 0.85f, 0.8f);
            float pulse = fishy ? 1f + 0.12f * Mathf.Sin(_stateT * 8f) : 1f;
            DrawSetTransform(_reticle, 0f, new Vector2(1f, 0.5f));
            DrawArc(Vector2.Zero, BiteRadius * pulse, 0f, Mathf.Tau, 40, c with { A = c.A * 0.35f }, 1.5f);
            DrawArc(Vector2.Zero, 12f, 0f, Mathf.Tau, 20, c, 2.5f);
            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
            // The line's reach, faintly, from where you stand.
            DrawDashedLine(RodTip(), _reticle, c with { A = 0.35f }, 1.2f, 8f);
            return;
        }

        if (_state == St.Landed) return;
        // The line and the float.
        Vector2 bob = _bobber;
        float lift = 0f;
        if (_state == St.Fly)
        {
            float k = Mathf.Clamp(_t / FlyTime, 0f, 1f);
            bob = _from.Lerp(_bobber, k);
            lift = 4f * k * (1f - k) * 110f;
            bob.Y -= lift;
        }
        var tip = RodTip();
        var mid = (tip + bob) / 2f + new Vector2(0, _state == St.Fight ? -6f : 14f);   // slack sags, a fighting line is taut
        var pts = new Vector2[9];
        for (int i = 0; i < pts.Length; i++)
        {
            float k = i / (pts.Length - 1f);
            pts[i] = tip * (1 - k) * (1 - k) + mid * 2 * k * (1 - k) + bob * k * k;
        }
        DrawPolyline(pts, new Color(0.92f, 0.9f, 0.82f, 0.85f), 1.2f, true);
        if (_state == St.Fight) { DrawCircle(bob + new Vector2(0, 3f), 3f, new Color(0.8f, 0.15f, 0.1f, 0.6f)); return; }   // under
        float dip = _state == St.Wait ? Mathf.Sin(_stateT * 2.5f) * 1.2f + _nibble * 4f : 0f;
        var b = bob + new Vector2(0, dip);
        DrawCircle(b, 4.5f, new Color(0.85f, 0.15f, 0.1f));
        DrawCircle(b + new Vector2(0, -3.5f), 3f, new Color(0.96f, 0.95f, 0.9f));
    }

    // ---- drawing: the bar and the words (screen space) -----------------------------------

    private void DrawHud()
    {
        if (_state == St.Closed) return;
        var font = ThemeDB.FallbackFont;
        var vp = _hud.GetViewportRect().Size;
        float w = Mathf.Min(460f, vp.X - 40f), h = 26f;
        var bar = new Rect2((vp.X - w) / 2f, vp.Y - 190f, w, h);

        string hint = _state switch
        {
            St.Aim => $"Aim at a fish (mouse or stick) - {Keybinds.KeyFor("interact")} or click to throw - Esc to wind in",
            St.Wait => _latchIn > 0f ? "It's nosing the hook... wait for it to take it" : _biter != null ? "A fish turns toward the hook..." : "Waiting...",
            St.Fight => $"{Keybinds.KeyFor("pause_clock")} / click / {Keybinds.KeyFor("interact")} when the needle is on the green",
            _ => "",
        };

        if (_state == St.Fight)
        {
            var back = bar.Grow(6f);
            _hud.DrawRect(back, new Color(0.08f, 0.07f, 0.06f, 0.85f));
            _hud.DrawRect(bar, new Color(0.18f, 0.24f, 0.3f));
            var zone = new Rect2(bar.Position.X + (_zoneC - _zoneW / 2f) * w, bar.Position.Y, _zoneW * w, h);
            _hud.DrawRect(zone, new Color(0.3f, 0.75f, 0.35f));
            if (_flash > 0f) _hud.DrawRect(bar, (_flashGood ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.3f, 0.25f)) with { A = 0.45f * _flash });
            float nx = bar.Position.X + _needle * w;
            _hud.DrawRect(new Rect2(nx - 2f, bar.Position.Y - 8f, 4f, h + 16f), new Color(0.97f, 0.93f, 0.8f));
            // Pulls landed, and the strikes against you.
            for (int i = 0; i < _need; i++)
            {
                var c = new Vector2(bar.Position.X + 10f + i * 18f, bar.Position.Y - 22f);
                _hud.DrawCircle(c, 6f, i < _pulls ? new Color(0.4f, 0.9f, 0.45f) : new Color(0.3f, 0.3f, 0.3f, 0.8f));
            }
            for (int i = 0; i < MaxMisses; i++)
            {
                var c = new Vector2(bar.End.X - 10f - i * 18f, bar.Position.Y - 22f);
                _hud.DrawCircle(c, 6f, i < _misses ? new Color(0.95f, 0.3f, 0.25f) : new Color(0.3f, 0.3f, 0.3f, 0.8f));
            }
            // How long the fish will stand for no good pull.
            float left = Mathf.Clamp(1f - _sinceGood / 6f, 0f, 1f);
            _hud.DrawRect(new Rect2(bar.Position.X, bar.End.Y + 6f, w * left, 3f), new Color(0.95f, 0.8f, 0.3f, 0.8f));
        }

        float y = bar.End.Y + 34f;
        if (_msgFor > 0f && _msg != "") Text(font, _msg, y - 2f, vp.X, 17, new Color(1f, 0.95f, 0.8f));
        if (hint != "") Text(font, hint, y + 22f, vp.X, 14, new Color(0.85f, 0.85f, 0.85f, 0.9f));
    }

    private void Text(Font font, string s, float y, float vw, int size, Color c)
    {
        var sz = font.GetStringSize(s, HorizontalAlignment.Left, -1, size);
        var at = new Vector2((vw - sz.X) / 2f, y);
        _hud.DrawRect(new Rect2(at + new Vector2(-8f, -size - 2f), sz + new Vector2(16f, 8f)), new Color(0f, 0f, 0f, 0.5f));
        _hud.DrawString(font, at, s, HorizontalAlignment.Left, -1, size, c);
    }
}
