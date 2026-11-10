using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.World;
using NetHub = IsometricFurTradeGame.Scripts.Net.Net;

namespace IsometricFurTradeGame.Scripts.UI;

// The hands at work (World/Boats, World/Boatbuilding): each stage of making
// or mending a boat is a short game played with one button and the stick -
// drive the stakes true, keep the lashing taut, catch the awl's marks, bend
// the rib to the line and not past it, keep the gum hot and on the seam.
// How well it went is sent to the host, who puts it into the boat.
//
// Watching someone else at it is the same game played by their hands (their
// crafting skill), which is how a green hand learns. Client side; the host
// sends what to play (Net.Net.StartHandWork).
public partial class HandWork : CanvasLayer
{
    public static HandWork Instance { get; private set; }
    public static bool IsOpen => Instance != null && IsInstanceValid(Instance) && Instance._open;

    private bool _open;
    private Control _root;
    private HandWorkCanvas _canvas;
    internal Game _game;
    internal Godot.Collections.Dictionary _req;
    private float _resultFor;     // the result card's moment on screen
    private bool _sent;

    public override void _Ready()
    {
        Instance = this;
        Layer = 40;
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);
        _canvas = new HandWorkCanvas { Owner_ = this };
        _canvas.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _canvas.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(_canvas);
        // Dev: `--handwork=Sew` (any BoatGame) plays one on its own a few seconds in;
        // `--handwork=Sew:auto` lets a practised hand play it; a comma list plays
        // them one after another, each saved to user://hw_<game>_<n>.png midway
        // and at its result.
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--handwork="))
            {
                foreach (var one in arg.Substring(11).Split(',')) _devQueue.Enqueue(one);
                GetTree().CreateTimer(3.0).Timeout += DevNext;
            }
    }

    private readonly Queue<string> _devQueue = new();
    private bool _devRun; private int _devShots;
    private void DevNext()
    {
        if (_devQueue.Count == 0) { if (_devRun) GD.Print("[handwork] dev run done"); return; }
        var bits = _devQueue.Dequeue().Split(':');
        if (!System.Enum.TryParse(bits[0], out BoatGame g)) { DevNext(); return; }
        _devRun = true; _devShots = 0;
        Open(new Godot.Collections.Dictionary
        {
            ["game"] = (int)g, ["title"] = $"Dev: {g}", ["text"] = "A trial of the hand-work.", ["n"] = 0, ["diff"] = 0.4f,
            ["watch"] = bits.Length > 1 && bits[1] == "auto", ["skill"] = bits.Length > 2 && float.TryParse(bits[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float sk) ? sk : 0.7f, ["who"] = "Marie", ["token"] = -1,
            ["leaks"] = new Godot.Collections.Array { 0.2f, 0.5f, 0.45f, 0.3f, 0.8f, 0.7f }, ["planked"] = g is BoatGame.Nail or BoatGame.Caulk or BoatGame.Saw,
            ["helpers"] = new Godot.Collections.Array { "Marie", 0.8f },
        });
    }
    private void DevShot(string tag)
    {
        var path = ProjectSettings.GlobalizePath($"user://hw_{tag}.png");
        GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"[handwork] {tag}: {path}");
    }

    // ---- open and close ------------------------------------------------------

    public static void Open(Godot.Collections.Dictionary req)
    {
        var me = Instance;
        if (me == null || !IsInstanceValid(me)) return;
        me._req = req;
        var kind = (BoatGame)req["game"].AsInt32();
        float diff = req.TryGetValue("diff", out var dv) ? dv.AsSingle() : 0.4f;
        int n = req.TryGetValue("n", out var nv) ? nv.AsInt32() : 0;
        bool planked = req.TryGetValue("planked", out var pv) && pv.AsBool();
        var leaks = new List<Vector2>();
        if (req.TryGetValue("leaks", out var lv)) { var a = lv.AsGodotArray(); for (int i = 0; i + 1 < a.Count; i += 2) leaks.Add(new Vector2(a[i].AsSingle(), a[i + 1].AsSingle())); }
        me._game = kind switch
        {
            BoatGame.Stakes => new StakesGame(n > 0 ? n : 8, diff, planked),
            BoatGame.Lash => new LashGame(n > 0 ? n : 8, diff),
            BoatGame.Sew => new BeatGame(n > 0 ? n : 16, diff, BeatGame.Look.Sew),
            BoatGame.Saw => new BeatGame(n > 0 ? n : 14, diff, BeatGame.Look.Saw),
            BoatGame.Bend => new BendGame(n > 0 ? n : 6, diff, false),
            BoatGame.Caulk => new BendGame(n > 0 ? n : 7, diff, true),
            BoatGame.Gum => new GumGame(diff, planked, leaks),
            BoatGame.Nail => new NailGame(n > 0 ? n : 14, diff),
            BoatGame.Patch => new PatchGame(leaks, diff, planked),
            _ => new BeatGame(12, diff, BeatGame.Look.Sew),
        };
        me._game.Auto = req.TryGetValue("watch", out var w) && w.AsBool();
        me._game.Skill = req.TryGetValue("skill", out var sk) ? sk.AsSingle() : 0.5f;
        me._game.Planked = planked;
        me._resultFor = 0f; me._sent = false;
        me._open = true;
        me._root.Visible = true;
        me._canvas.QueueRedraw();
    }

    // Close without a word to the host (the dev test answers for the game itself).
    public static void Dismiss()
    {
        var me = Instance;
        if (me == null || !IsInstanceValid(me)) return;
        me._sent = true; me._open = false; me._root.Visible = false; me._game = null;
    }

    private void Finish(bool abandoned)
    {
        if (_sent) return;
        _sent = true;
        int token = _req != null && _req.TryGetValue("token", out var t) ? t.AsInt32() : -1;
        if (token >= 0)
        {
            var misses = new Godot.Collections.Array();
            foreach (var m in _game.Misses) misses.Add(m);
            var fills = new Godot.Collections.Array();
            foreach (var f in _game.Fills) fills.Add(f);
            NetHub.I?.Act("handwork", new Godot.Collections.Array { token, abandoned ? -1f : _game.Score, misses, fills });
        }
        if (_devRun) GD.Print($"[handwork] {(_req != null ? ((BoatGame)_req["game"].AsInt32()).ToString() : "?")} score {(_game != null ? _game.Score : -1f):0.00} misses {_game?.Misses.Count}");
        _open = false;
        _root.Visible = false;
        _game = null;
        if (_devRun) GetTree().CreateTimer(0.6).Timeout += DevNext;
    }

    public override void _Input(InputEvent e)
    {
        if (!_open) return;
        if (e.IsActionPressed("ui_cancel") || e.IsActionPressed("pause_menu"))
        {
            // Walking away mid-work: nothing is spent, nothing is done. A
            // finished game's card goes at once.
            if (_game != null && _game.Done) Finish(false); else Finish(true);
            GetViewport().SetInputAsHandled();
            return;
        }
        if (e is InputEventKey or InputEventJoypadButton or InputEventMouseButton) GetViewport().SetInputAsHandled();
    }

    // The one button: E / A / the left mouse button / Space.
    private bool _wasHeld;
    public override void _Process(double delta)
    {
        if (!_open || _game == null) return;
        float dt = (float)delta;
        bool held = Input.IsActionPressed("interact") || Input.IsActionPressed("ui_accept") || Input.IsMouseButtonPressed(MouseButton.Left);
        var inp = new In
        {
            Held = held, Pressed = held && !_wasHeld, Released = !held && _wasHeld,
            Axis = Input.GetVector("move_left", "move_right", "move_up", "move_down"),
        };
        _wasHeld = held;
        var r = _canvas.Board;
        if (r.HasArea())
        {
            var mp = _canvas.GetLocalMousePosition();
            inp.Mouse = new Vector2((mp.X - r.Position.X) / r.Size.X, (mp.Y - r.Position.Y) / r.Size.Y);
            inp.MouseMoved = Input.GetLastMouseVelocity().LengthSquared() > 4f;
        }
        if (_devRun && _req != null)
        {
            string tag = ((BoatGame)_req["game"].AsInt32()).ToString();
            if (_devShots == 0 && _game.Time > 4f) { _devShots = 1; DevShot(tag + "_mid"); }
            if (_devShots == 1 && _game.Done && _resultFor > 0.3f) { _devShots = 2; DevShot(tag + "_end"); }
        }
        if (!_game.Done)
        {
            if (_game.Auto) inp = _game.AutoInput(dt);
            _game.Time += dt;
            _game.Step(dt, inp);
        }
        else
        {
            _resultFor += dt;
            if (_resultFor > (_game.Auto ? 2.2f : 2.6f) || (_resultFor > 0.6f && inp.Pressed && !_game.Auto)) Finish(false);
        }
        _canvas.QueueRedraw();
    }

    // ---- drawing ---------------------------------------------------------------

    // ---- the games ---------------------------------------------------------

    public struct In
    {
        public bool Pressed, Held, Released, MouseMoved;
        public Vector2 Axis, Mouse;
    }

    public abstract class Game
    {
        public float Time, Score;
        public bool Done, Auto, Planked;
        public float Skill = 0.5f;
        public string Status = "";
        public abstract string Help { get; }
        // Where along her (0 bow .. 1 stern) the work went wrong.
        public readonly List<float> Misses = new();
        // For the patch game: how far each leak was filled (0..1).
        public readonly List<float> Fills = new();
        protected readonly RandomNumberGenerator Rng = new();
        protected Game() { Rng.Randomize(); }
        public abstract void Step(float dt, In inp);
        public abstract void Draw(HandWorkCanvas c, Rect2 r, Font font);
        // How a practised (or green) hand plays it.
        public abstract In AutoInput(float dt);
        protected float Wobble(float scale) => Rng.Randfn(0f, 1f) * scale * (1.15f - Skill);
        protected bool AutoHeld;
        protected In Hold(bool h) { var i = new In { Held = h, Pressed = h && !AutoHeld, Released = !h && AutoHeld }; AutoHeld = h; return i; }

        // The canoe from the side, for the games drawn on her: a long low
        // curve, upswept at both ends.
        protected static Vector2[] HullOutline(Rect2 r, bool planked)
        {
            var pts = new List<Vector2>();
            float cx = r.Position.X + r.Size.X / 2f, top = r.Position.Y + r.Size.Y * 0.35f, len = r.Size.X * 0.42f, depth = r.Size.Y * 0.32f;
            int k = 28;
            for (int i = 0; i <= k; i++)
            {
                float t = (float)i / k, x = cx - len + 2f * len * t;
                float sheer = planked ? 0.12f * Mathf.Pow(Mathf.Abs(2f * t - 1f), 3f) : 0.35f * Mathf.Pow(Mathf.Abs(2f * t - 1f), 4f);
                pts.Add(new Vector2(x, top - sheer * depth * 2f));
            }
            for (int i = k; i >= 0; i--)
            {
                float t = (float)i / k, x = cx - len + 2f * len * t;
                float belly = Mathf.Sin(t * Mathf.Pi);
                pts.Add(new Vector2(x, top + depth * Mathf.Pow(belly, planked ? 0.5f : 0.8f)));
            }
            return pts.ToArray();
        }
        protected static void DrawHull(HandWorkCanvas c, Rect2 r, bool planked, float fill = 1f)
        {
            var pts = HullOutline(r, planked);
            int k = pts.Length / 2;
            var col = planked ? new Color(0.62f, 0.45f, 0.28f, fill) : new Color(0.93f, 0.9f, 0.8f, fill);
            // Strip by strip, sheer to keel: one polygon of the whole curve
            // does not triangulate.
            for (int i = 0; i + 1 < k; i++)
                c.DrawColoredPolygon(new[] { pts[i], pts[i + 1], pts[pts.Length - 2 - i], pts[pts.Length - 1 - i] }, col);
            var loop = new Vector2[pts.Length + 1]; pts.CopyTo(loop, 0); loop[^1] = pts[0];
            c.DrawPolyline(loop, new Color(0.25f, 0.16f, 0.08f), 3f);
        }
        protected static Vector2 AlongHull(Rect2 r, float t, float v)
        {
            float cx = r.Position.X + r.Size.X / 2f, top = r.Position.Y + r.Size.Y * 0.35f, len = r.Size.X * 0.42f, depth = r.Size.Y * 0.32f;
            float belly = Mathf.Sin(t * Mathf.Pi);
            return new Vector2(cx - len + 2f * len * t, top + depth * belly * v);
        }
        protected static void Bar(HandWorkCanvas c, Rect2 at, float v, Color col, string label, Font font)
        {
            c.DrawRect(at, new Color(0.3f, 0.22f, 0.12f, 0.4f));
            c.DrawRect(new Rect2(at.Position, new Vector2(at.Size.X * Mathf.Clamp(v, 0f, 1f), at.Size.Y)), col);
            c.DrawRect(at, new Color(0.25f, 0.16f, 0.08f), false, 1.5f);
            if (label != "") c.DrawString(font, at.Position + new Vector2(0, -4), label, HorizontalAlignment.Left, -1, 12, new Color(0.25f, 0.16f, 0.08f));
        }
    }

    // Stake out the bark (or set up the frames): a stake rides along the
    // bed's side; drive it on the mark. Both sides, bow to stern.
    private sealed class StakesGame : Game
    {
        private readonly float[] _marks; private readonly float[] _driven; private readonly float[] _acc;
        private int _i; private float _x, _dir = 1f; private readonly float _speed;
        private float _autoErr; private bool _autoArmed;
        public override string Help => Planked ? "Press E / A when the frame is on its mark." : "Press E / A when the stake is over its mark.";
        public StakesGame(int n, float diff, bool planked)
        {
            n = Mathf.Clamp(n, 4, 16); if (n % 2 == 1) n++;
            _marks = new float[n]; _driven = new float[n]; _acc = new float[n];
            for (int i = 0; i < n; i++) { int k = i / 2; _marks[i] = Mathf.Lerp(0.12f, 0.88f, (k + 0.5f + Rng.RandfRange(-0.25f, 0.25f)) / (n / 2)); _driven[i] = -1f; }
            _speed = 0.45f + 0.5f * diff; _x = 0.05f; Planked = planked;
            NewAutoErr();
        }
        private void NewAutoErr() { _autoErr = Wobble(0.06f); _autoArmed = true; }
        public override void Step(float dt, In inp)
        {
            _x += _dir * _speed * dt * (1f + 0.04f * _i);
            if (_x > 0.97f) { _x = 0.97f; _dir = -1f; }
            if (_x < 0.03f) { _x = 0.03f; _dir = 1f; }
            if (inp.Pressed)
            {
                float err = Mathf.Abs(_x - _marks[_i]);
                _acc[_i] = Mathf.Clamp(1f - err / 0.12f, 0f, 1f);
                _driven[_i] = _x;
                Status = _acc[_i] > 0.85f ? "True." : _acc[_i] > 0.5f ? "Near enough." : "Off the line - she'll be crooked there.";
                if (_acc[_i] < 0.4f) Misses.Add(_marks[_i]);
                _i++; NewAutoErr();
                if (_i >= _marks.Length) { float s = 0f; foreach (var a in _acc) s += a; Score = s / _acc.Length; Done = true; }
            }
        }
        public override In AutoInput(float dt)
        {
            if (_i >= _marks.Length) return default;
            float target = _marks[_i] + _autoErr;
            bool hit = _autoArmed && Mathf.Abs(_x - target) < _speed * dt * 1.5f;
            if (hit) _autoArmed = false;
            return new In { Pressed = hit };
        }
        public override void Draw(HandWorkCanvas c, Rect2 r, Font font)
        {
            // From above: the bark sheet on its bed, the canoe's lines on it.
            var bed = new Rect2(r.Position + new Vector2(20, 30), r.Size - new Vector2(40, 60));
            c.DrawRect(bed, new Color(0.55f, 0.42f, 0.28f));
            var sheet = new Rect2(bed.Position + new Vector2(20, 18), bed.Size - new Vector2(40, 36));
            c.DrawRect(sheet, Planked ? new Color(0.68f, 0.5f, 0.3f) : new Color(0.94f, 0.92f, 0.85f));
            float cy = sheet.GetCenter().Y, half = sheet.Size.Y * 0.38f;
            Vector2 P(float t, int side) => new(sheet.Position.X + sheet.Size.X * t, cy + side * half * Mathf.Pow(Mathf.Sin(Mathf.Pi * t), 0.6f));
            for (int side = -1; side <= 1; side += 2)
            {
                var line = new Vector2[41];
                for (int k = 0; k <= 40; k++) line[k] = P(k / 40f, side);
                c.DrawPolyline(line, new Color(0.3f, 0.2f, 0.1f, 0.6f), 2f);
            }
            for (int i = 0; i < _marks.Length; i++)
            {
                int side = i % 2 == 0 ? -1 : 1;
                var m = P(_marks[i], side) + new Vector2(0, side * 14f);
                c.DrawColoredPolygon(new[] { m + new Vector2(-6, side * 8), m + new Vector2(6, side * 8), m }, i == _i ? new Color(0.75f, 0.2f, 0.1f) : new Color(0.4f, 0.3f, 0.2f, 0.6f));
                if (_driven[i] >= 0f) c.DrawCircle(P(_driven[i], side) + new Vector2(0, side * 10f), 6f, _acc[i] > 0.6f ? new Color(0.3f, 0.2f, 0.1f) : new Color(0.6f, 0.15f, 0.1f));
            }
            if (_i < _marks.Length)
            {
                int side = _i % 2 == 0 ? -1 : 1;
                var at = P(_x, side) + new Vector2(0, side * 10f);
                c.DrawCircle(at, 9f, new Color(0.95f, 0.7f, 0.2f));
                c.DrawLine(at, at + new Vector2(0, side * 26f), new Color(0.3f, 0.2f, 0.1f), 3f);
            }
            c.DrawString(font, r.Position + new Vector2(12, 20), $"{(Planked ? "Frames" : "Stakes")} {Mathf.Min(_i, _marks.Length)}/{_marks.Length}", HorizontalAlignment.Left, -1, 14, new Color(0.25f, 0.16f, 0.08f));
        }
    }

    // Lash the gunwales: hold to draw the watap tight, let go to ease it.
    // Wraps only bite while the tension is in the band; too tight and it parts.
    private sealed class LashGame : Game
    {
        private readonly int _wraps; private float _done, _t = 0.3f, _inBand, _band; private int _snaps;
        private readonly float _width;
        public override string Help => "Hold E / A to pull the lashing tight; let go to ease it. Keep it in the green.";
        public LashGame(int wraps, float diff) { _wraps = Mathf.Clamp(wraps, 4, 16); _width = 0.24f - 0.1f * diff; }
        private float Centre => 0.55f + 0.18f * Mathf.Sin(Time * 0.9f) + 0.05f * Mathf.Sin(Time * 2.3f);
        public override void Step(float dt, In inp)
        {
            _t = Mathf.Clamp(_t + (inp.Held ? 0.85f : -0.6f) * dt, 0f, 1f);
            float c = Centre;
            bool inBand = Mathf.Abs(_t - c) < _width / 2f;
            if (inBand) { _done += dt * 0.95f; _inBand += dt; }
            if (_t >= 0.995f)
            {
                _snaps++; _t = 0.15f; _done = Mathf.Max(0f, _done - 1f);
                Misses.Add(Mathf.Clamp(_done / _wraps, 0f, 1f));
                Status = "The watap parts! Start that wrap again.";
            }
            else if (!inBand) Status = _t > c ? "Too tight..." : "Slack...";
            else Status = "";
            if (_done >= _wraps)
            {
                float eff = Mathf.Clamp(_wraps / 0.95f / Mathf.Max(0.1f, Time), 0f, 1f);
                Score = Mathf.Clamp(0.55f * eff + 0.45f * (1f - _snaps / 4f), 0f, 1f);
                Done = true;
            }
        }
        public override In AutoInput(float dt)
        {
            float c = Centre + Wobble(0.05f);
            return Hold(_t < c - _width * 0.15f);
        }
        public override void Draw(HandWorkCanvas c, Rect2 r, Font font)
        {
            // The gunwale strip across the board, the wraps along it.
            var strip = new Rect2(r.Position + new Vector2(30, r.Size.Y * 0.45f), new Vector2(r.Size.X - 140, 22));
            c.DrawRect(strip, new Color(0.72f, 0.48f, 0.3f));
            c.DrawRect(new Rect2(strip.Position + new Vector2(0, 26), new Vector2(strip.Size.X, 30)), new Color(0.94f, 0.92f, 0.85f));
            int whole = Mathf.FloorToInt(_done);
            for (int i = 0; i < _wraps; i++)
            {
                float x = strip.Position.X + 12 + (strip.Size.X - 24) * i / Mathf.Max(1, _wraps - 1);
                var col = i < whole ? new Color(0.45f, 0.3f, 0.15f) : i == whole ? new Color(0.45f, 0.3f, 0.15f, Mathf.Clamp(_done - whole, 0.1f, 1f)) : new Color(0.4f, 0.3f, 0.2f, 0.15f);
                for (int k = 0; k < 4; k++) c.DrawLine(new Vector2(x + k * 3, strip.Position.Y - 4), new Vector2(x + k * 3 + 6, strip.End.Y + 30), col, 2f);
            }
            // The tension gauge.
            var gauge = new Rect2(new Vector2(r.End.X - 80, r.Position.Y + 20), new Vector2(34, r.Size.Y - 40));
            c.DrawRect(gauge, new Color(0.3f, 0.22f, 0.12f, 0.35f));
            float cc = Centre;
            float y0 = gauge.End.Y - gauge.Size.Y * (cc + _width / 2f), y1 = gauge.End.Y - gauge.Size.Y * (cc - _width / 2f);
            c.DrawRect(new Rect2(gauge.Position.X, y0, gauge.Size.X, y1 - y0), new Color(0.35f, 0.65f, 0.25f, 0.8f));
            c.DrawRect(new Rect2(gauge.Position.X, gauge.Position.Y, gauge.Size.X, 6), new Color(0.75f, 0.15f, 0.1f));
            float ty = gauge.End.Y - gauge.Size.Y * _t;
            c.DrawLine(new Vector2(gauge.Position.X - 8, ty), new Vector2(gauge.End.X + 8, ty), new Color(0.15f, 0.1f, 0.05f), 4f);
            c.DrawRect(gauge, new Color(0.25f, 0.16f, 0.08f), false, 2f);
            c.DrawString(font, r.Position + new Vector2(12, 20), $"Wraps {Mathf.Min(whole, _wraps)}/{_wraps}   Parted: {_snaps}", HorizontalAlignment.Left, -1, 14, new Color(0.25f, 0.16f, 0.08f));
        }
    }

    // Sew the seams (or pull the pit saw): marks come along the seam to the
    // awl; press as each one is under it.
    private sealed class BeatGame : Game
    {
        public enum Look { Sew, Saw }
        private readonly Look _look;
        private readonly float[] _at; private readonly int[] _hit;   // 0 waiting, 1 perfect, 2 good, 3 rough, -1 missed
        private readonly float _speed; private const float Awl = 0.22f;
        private readonly float[] _autoErr;
        public override string Help => _look == Look.Saw ? "Press E / A as each stroke reaches the line - keep time with the man below." : "Press E / A as each mark reaches the awl.";
        public BeatGame(int n, float diff, Look look)
        {
            _look = look; n = Mathf.Clamp(n, 6, 30);
            _at = new float[n]; _hit = new int[n]; _autoErr = new float[n];
            float x = 1.05f;
            for (int i = 0; i < n; i++) { _at[i] = x; x += Rng.RandfRange(0.09f, 0.16f) * (look == Look.Saw ? 1.2f : 1f); _autoErr[i] = Wobble(0.035f); }
            _speed = 0.2f + 0.16f * diff;
        }
        private float Pos(int i) => _at[i] - Time * _speed * (1f + 0.25f * Time / 30f);
        public override void Step(float dt, In inp)
        {
            int next = -1;
            for (int i = 0; i < _at.Length; i++) if (_hit[i] == 0) { next = i; break; }
            if (next < 0) { Finish(); return; }
            if (Pos(next) < Awl - 0.07f) { _hit[next] = -1; Misses.Add((float)next / _at.Length); Status = _look == Look.Saw ? "Out of time - the saw binds." : "Missed one: a loose stitch."; return; }
            if (inp.Pressed)
            {
                float err = Mathf.Abs(Pos(next) - Awl);
                if (err < 0.07f)
                {
                    _hit[next] = err < 0.016f ? 1 : err < 0.04f ? 2 : 3;
                    Status = _hit[next] == 1 ? "Clean." : _hit[next] == 2 ? "Good." : "Rough.";
                }
                else Status = "Too soon.";
            }
        }
        private void Finish()
        {
            float s = 0f;
            foreach (var h in _hit) s += h switch { 1 => 1f, 2 => 0.78f, 3 => 0.45f, _ => 0f };
            Score = s / _hit.Length; Done = true;
        }
        public override In AutoInput(float dt)
        {
            for (int i = 0; i < _at.Length; i++)
                if (_hit[i] == 0)
                {
                    float p = Pos(i) - _autoErr[i];
                    return new In { Pressed = p <= Awl && p > Awl - 0.05f && Rng.Randf() > (1f - Skill) * 0.04f };
                }
            return default;
        }
        public override void Draw(HandWorkCanvas c, Rect2 r, Font font)
        {
            float y = r.Position.Y + r.Size.Y * 0.5f;
            if (_look == Look.Sew)
            {
                c.DrawRect(new Rect2(r.Position.X, y - 50, r.Size.X, 50), new Color(0.94f, 0.92f, 0.85f));
                c.DrawRect(new Rect2(r.Position.X, y, r.Size.X, 50), new Color(0.9f, 0.87f, 0.78f));
                c.DrawLine(new Vector2(r.Position.X, y), new Vector2(r.End.X, y), new Color(0.4f, 0.3f, 0.2f), 2f);
            }
            else
            {
                c.DrawRect(new Rect2(r.Position.X, y - 30, r.Size.X, 60), new Color(0.7f, 0.5f, 0.3f));
                c.DrawLine(new Vector2(r.Position.X, y), new Vector2(r.End.X, y), new Color(0.35f, 0.2f, 0.1f), 2f);
            }
            for (int i = 0; i < _at.Length; i++)
            {
                float x = r.Position.X + r.Size.X * Pos(i);
                if (x < r.Position.X - 10 || x > r.End.X + 10) continue;
                var col = _hit[i] switch { 0 => new Color(0.25f, 0.15f, 0.05f), 1 => new Color(0.25f, 0.5f, 0.15f), 2 => new Color(0.4f, 0.5f, 0.15f), 3 => new Color(0.6f, 0.45f, 0.1f), _ => new Color(0.7f, 0.15f, 0.1f) };
                if (_look == Look.Sew) { c.DrawLine(new Vector2(x - 6, y - 14), new Vector2(x + 6, y + 14), col, 3f); c.DrawLine(new Vector2(x + 6, y - 14), new Vector2(x - 6, y + 14), col, _hit[i] > 0 ? 3f : 1f); }
                else c.DrawRect(new Rect2(x - 4, y - 22, 8, 44), col);
            }
            float ax = r.Position.X + r.Size.X * Awl;
            c.DrawLine(new Vector2(ax, y - 80), new Vector2(ax, y + 80), new Color(0.75f, 0.2f, 0.1f, 0.7f), 2f);
            if (_look == Look.Sew) c.DrawColoredPolygon(new[] { new Vector2(ax - 5, y - 90), new Vector2(ax + 5, y - 90), new Vector2(ax, y - 58) }, new Color(0.45f, 0.45f, 0.5f));
            else
            {
                float s = Mathf.Sin(Time * 6f) * 30f;
                c.DrawRect(new Rect2(ax - 3, y - 90 + s, 6, 120), new Color(0.5f, 0.5f, 0.55f));
            }
            int done = 0; foreach (var h in _hit) if (h != 0) done++;
            c.DrawString(font, r.Position + new Vector2(12, 20), $"{(_look == Look.Saw ? "Strokes" : "Stitches")} {done}/{_at.Length}", HorizontalAlignment.Left, -1, 14, new Color(0.25f, 0.16f, 0.08f));
        }
    }

    // Bend in the ribs (or drive the oakum): hold to bend, let go in the
    // green. Past the red line the rib cracks (the seam splits).
    private sealed class BendGame : Game
    {
        private readonly bool _caulk;
        private readonly int _n; private int _i; private float _g; private bool _bending;
        private float _lo, _w; private readonly float _diff; private readonly List<float> _acc = new();
        private float _autoAt; private int _cracked;
        public override string Help => _caulk ? "Hold E / A to drive the oakum home; let go when it's in the green." : "Hold E / A to bend the rib; let go in the green, before it cracks.";
        public BendGame(int n, float diff, bool caulk) { _n = Mathf.Clamp(n, 3, 12); _diff = diff; _caulk = caulk; Next(); }
        private void Next()
        {
            _g = 0f; _bending = false;
            _w = Mathf.Max(0.05f, 0.14f - 0.06f * _diff - 0.004f * _i);
            _lo = Rng.RandfRange(0.45f, 0.82f - _w);
            _autoAt = _lo + _w / 2f + Wobble(0.06f);
        }
        private float Rate => 0.42f + 0.25f * _diff + 0.03f * _i;
        public override void Step(float dt, In inp)
        {
            if (!_bending && inp.Pressed) _bending = true;
            if (_bending && inp.Held) _g += Rate * dt;
            if (_bending && (_g >= 0.95f || inp.Released || !inp.Held))
            {
                float a;
                if (_g >= 0.95f) { a = 0f; _cracked++; Status = _caulk ? "The seam splits." : "Crack! The rib breaks."; Misses.Add((_i + 0.5f) / _n); }
                else if (_g >= _lo && _g <= _lo + _w) { a = 1f - 0.3f * Mathf.Abs(_g - (_lo + _w / 2f)) / (_w / 2f); Status = "Just so."; }
                else { float d = _g < _lo ? _lo - _g : _g - _lo - _w; a = Mathf.Max(0f, 1f - d / 0.25f) * 0.55f; Status = _g < _lo ? (_caulk ? "Not driven home." : "Not bent enough: she'll be flat there.") : (_caulk ? "Driven too hard." : "Over-bent: strained."); if (a < 0.25f) Misses.Add((_i + 0.5f) / _n); }
                _acc.Add(a); _i++;
                if (_i >= _n) { float s = 0f; foreach (var x in _acc) s += x; Score = s / _n; Done = true; }
                else Next();
            }
        }
        public override In AutoInput(float dt) => Hold(_g < _autoAt);
        public override void Draw(HandWorkCanvas c, Rect2 r, Font font)
        {
            var mid = new Vector2(r.Position.X + r.Size.X * 0.4f, r.Position.Y + r.Size.Y * 0.62f);
            if (_caulk)
            {
                // Two planks and the seam between, the oakum going in.
                c.DrawRect(new Rect2(r.Position.X + 30, mid.Y - 70, r.Size.X * 0.6f, 60), new Color(0.65f, 0.47f, 0.28f));
                c.DrawRect(new Rect2(r.Position.X + 30, mid.Y - 4, r.Size.X * 0.6f, 60), new Color(0.6f, 0.43f, 0.25f));
                c.DrawRect(new Rect2(r.Position.X + 30, mid.Y - 10, r.Size.X * 0.6f * Mathf.Clamp(_g / 0.8f, 0f, 1f), 6), new Color(0.45f, 0.35f, 0.18f));
            }
            else
            {
                // The rib: a strip of cedar bowing as it is bent.
                float bend = _g;
                var pts = new Vector2[25];
                for (int k = 0; k <= 24; k++) { float t = k / 24f - 0.5f; pts[k] = mid + new Vector2(t * 300f, -bend * 160f * (0.25f - t * t) * 4f); }
                c.DrawPolyline(pts, _g >= 0.95f ? new Color(0.6f, 0.15f, 0.1f) : new Color(0.72f, 0.48f, 0.3f), 12f);
            }
            var gauge = new Rect2(new Vector2(r.End.X - 90, r.Position.Y + 20), new Vector2(34, r.Size.Y - 40));
            c.DrawRect(gauge, new Color(0.3f, 0.22f, 0.12f, 0.35f));
            float y0 = gauge.End.Y - gauge.Size.Y * (_lo + _w), y1 = gauge.End.Y - gauge.Size.Y * _lo;
            c.DrawRect(new Rect2(gauge.Position.X, y0, gauge.Size.X, y1 - y0), new Color(0.35f, 0.65f, 0.25f, 0.85f));
            float ry = gauge.End.Y - gauge.Size.Y * 0.95f;
            c.DrawLine(new Vector2(gauge.Position.X, ry), new Vector2(gauge.End.X, ry), new Color(0.75f, 0.15f, 0.1f), 3f);
            float gy = gauge.End.Y - gauge.Size.Y * Mathf.Min(_g, 1f);
            c.DrawLine(new Vector2(gauge.Position.X - 8, gy), new Vector2(gauge.End.X + 8, gy), new Color(0.15f, 0.1f, 0.05f), 4f);
            c.DrawRect(gauge, new Color(0.25f, 0.16f, 0.08f), false, 2f);
            c.DrawString(font, r.Position + new Vector2(12, 20), $"{(_caulk ? "Seams" : "Ribs")} {Mathf.Min(_i + 1, _n)}/{_n}   {(_caulk ? "Split" : "Cracked")}: {_cracked}", HorizontalAlignment.Left, -1, 14, new Color(0.25f, 0.16f, 0.08f));
        }
    }

    // Gum the seams (or pay them with pitch): the hot stick runs along the
    // seam by itself; keep it on the line with up and down, and keep the gum
    // hot - tap E / A to blow on the embers.
    private sealed class GumGame : Game
    {
        private const int Segs = 48;
        private readonly bool[] _covered = new bool[Segs * 2];
        private float _x, _y = 0.5f, _heat = 1f, _phase; private int _pass;
        private readonly float _speed, _tol, _amp;
        private readonly float[] _weak;   // weak stitches (from the sewing) shown along the seam
        private float _autoLag;
        public override string Help => Planked ? "Up / down keeps the pitch on the seam; tap E / A to keep it hot." : "Up / down (or the mouse) keeps the gum on the seam; tap E / A to keep it hot.";
        public GumGame(float diff, bool planked, List<Vector2> weak)
        {
            Planked = planked;
            _speed = 0.11f + 0.07f * diff; _tol = 0.07f - 0.025f * diff; _amp = 0.08f + 0.1f * diff; _phase = Rng.Randf() * 6f;
            _weak = new float[weak.Count]; for (int i = 0; i < weak.Count; i++) _weak[i] = weak[i].X;
            _autoLag = 0f;
        }
        private float Seam(float x) => 0.5f + _amp * Mathf.Sin(x * 9f + _phase + _pass * 2f) + _amp * 0.4f * Mathf.Sin(x * 23f + _phase);
        public override void Step(float dt, In inp)
        {
            _x += _speed * dt;
            float move = inp.Axis.Y;
            if (inp.MouseMoved && inp.Mouse.Y > 0f && inp.Mouse.Y < 1f) _y = Mathf.MoveToward(_y, inp.Mouse.Y, 1.6f * dt);
            else _y = Mathf.Clamp(_y + move * 0.75f * dt, 0.05f, 0.95f);
            _heat = Mathf.Max(0f, _heat - 0.14f * dt);
            if (inp.Pressed) _heat = Mathf.Min(1f, _heat + 0.38f);
            int seg = Mathf.Clamp((int)(_x * Segs), 0, Segs - 1);
            bool on = Mathf.Abs(_y - Seam(_x)) < _tol;
            if (on && _heat > 0.15f) _covered[_pass * Segs + seg] = true;
            Status = _heat <= 0.15f ? "The gum has gone cold - blow on the embers!" : !on ? "Off the seam." : "";
            if (_x >= 1f)
            {
                _x = 0f; _pass++;
                if (_pass >= 2)
                {
                    int got = 0; for (int i = 0; i < _covered.Length; i++) if (_covered[i]) got++;
                    Score = (float)got / _covered.Length;
                    // A run of bare seam is a leak waiting.
                    for (int p = 0; p < 2; p++)
                        for (int i = 0; i < Segs; i += 4)
                        {
                            int bare = 0; for (int k = i; k < i + 4 && k < Segs; k++) if (!_covered[p * Segs + k]) bare++;
                            if (bare >= 3) Misses.Add((i + 2f) / Segs);
                        }
                    Done = true;
                }
            }
        }
        public override In AutoInput(float dt)
        {
            _autoLag += dt;
            float target = Seam(_x + 0.02f) + Wobble(0.03f);
            float dy = target - _y;
            var a = new Vector2(0f, Mathf.Clamp(dy * (6f + 8f * Skill), -1f, 1f));
            bool blow = _heat < 0.35f + Wobble(0.1f) && _autoLag > 0.25f;
            if (blow) _autoLag = 0f;
            return new In { Axis = a, Pressed = blow };
        }
        public override void Draw(HandWorkCanvas c, Rect2 r, Font font)
        {
            var area = new Rect2(r.Position + new Vector2(20, 30), r.Size - new Vector2(110, 50));
            c.DrawRect(area, Planked ? new Color(0.6f, 0.43f, 0.26f) : new Color(0.94f, 0.92f, 0.85f));
            c.DrawString(font, r.Position + new Vector2(12, 20), $"Seam {Mathf.Min(_pass + 1, 2)}/2", HorizontalAlignment.Left, -1, 14, new Color(0.25f, 0.16f, 0.08f));
            Vector2 P(float x, float y) => area.Position + new Vector2(area.Size.X * x, area.Size.Y * y);
            for (int i = 0; i < Segs; i++)
            {
                float x0 = (float)i / Segs, x1 = (i + 1f) / Segs;
                bool cov = _pass < 2 && _covered[Mathf.Min(_pass, 1) * Segs + i];
                c.DrawLine(P(x0, Seam(x0)), P(x1, Seam(x1)), cov ? new Color(0.35f, 0.18f, 0.05f) : new Color(0.4f, 0.3f, 0.2f, 0.5f), cov ? 8f : 2f);
            }
            foreach (var w in _weak) c.DrawCircle(P(w, Seam(w)), 5f, new Color(0.7f, 0.2f, 0.1f, 0.6f));
            c.DrawCircle(P(_x, _y), 9f, _heat > 0.15f ? new Color(0.95f, 0.55f, 0.15f) : new Color(0.4f, 0.35f, 0.3f));
            c.DrawLine(P(_x, _y), P(_x, _y) + new Vector2(18, -40), new Color(0.3f, 0.2f, 0.1f), 4f);
            Bar(c, new Rect2(new Vector2(r.End.X - 70, r.Position.Y + 40), new Vector2(40, 16)), _heat, _heat > 0.35f ? new Color(0.9f, 0.45f, 0.1f) : new Color(0.6f, 0.2f, 0.1f), "Heat", font);
        }
    }

    // Plank and nail: the hammer swings up and down; strike at the top of
    // the swing. A weak blow bends the nail.
    private sealed class NailGame : Game
    {
        private readonly int _n; private int _i; private readonly List<float> _acc = new();
        private readonly float _rate; private float _autoAt;
        public override string Help => "Press E / A at the top of the swing.";
        public NailGame(int n, float diff) { _n = Mathf.Clamp(n, 6, 24); _rate = 2.2f + 1.6f * diff; _autoAt = 0.93f + Wobble(0.05f); }
        private float Swing => 0.5f - 0.5f * Mathf.Cos(Time * _rate * (1f + 0.02f * _i));
        private float _last;
        public override void Step(float dt, In inp)
        {
            if (inp.Pressed)
            {
                float a = Mathf.Clamp((Swing - 0.55f) / 0.4f, 0f, 1f);
                _acc.Add(a);
                Status = a > 0.85f ? "Home in one." : a > 0.5f ? "Drove it." : "Bent the nail.";
                if (a < 0.35f) Misses.Add((_i + 0.5f) / _n);
                _i++; _autoAt = 0.93f + Wobble(0.05f);
                if (_i >= _n) { float s = 0f; foreach (var x in _acc) s += x; Score = s / _n; Done = true; }
            }
            _last = Swing;
        }
        public override In AutoInput(float dt)
        {
            float s = Swing;
            return new In { Pressed = s >= Mathf.Min(_autoAt, 0.995f) && s > _last };
        }
        public override void Draw(HandWorkCanvas c, Rect2 r, Font font)
        {
            var plank = new Rect2(r.Position + new Vector2(30, r.Size.Y * 0.55f), new Vector2(r.Size.X - 160, 50));
            c.DrawRect(plank, new Color(0.68f, 0.5f, 0.3f));
            c.DrawRect(plank, new Color(0.35f, 0.24f, 0.12f), false, 2f);
            for (int i = 0; i < _n; i++)
            {
                float x = plank.Position.X + 14 + (plank.Size.X - 28) * i / Mathf.Max(1, _n - 1);
                var col = i < _acc.Count ? (_acc[i] > 0.35f ? new Color(0.25f, 0.25f, 0.28f) : new Color(0.6f, 0.2f, 0.15f)) : new Color(0.3f, 0.3f, 0.3f, 0.2f);
                c.DrawCircle(new Vector2(x, plank.GetCenter().Y), i < _acc.Count ? 5f : 3f, col);
            }
            var gauge = new Rect2(new Vector2(r.End.X - 90, r.Position.Y + 20), new Vector2(34, r.Size.Y - 40));
            c.DrawRect(gauge, new Color(0.3f, 0.22f, 0.12f, 0.35f));
            c.DrawRect(new Rect2(gauge.Position.X, gauge.Position.Y, gauge.Size.X, gauge.Size.Y * 0.12f), new Color(0.35f, 0.65f, 0.25f, 0.85f));
            float hy = gauge.End.Y - gauge.Size.Y * Swing;
            c.DrawRect(new Rect2(gauge.Position.X - 14, hy - 8, gauge.Size.X + 28, 16), new Color(0.35f, 0.35f, 0.4f));
            c.DrawRect(gauge, new Color(0.25f, 0.16f, 0.08f), false, 2f);
            c.DrawString(font, r.Position + new Vector2(12, 20), $"Nails {_i}/{_n}", HorizontalAlignment.Left, -1, 14, new Color(0.25f, 0.16f, 0.08f));
        }
    }

    // Mending: the leaks show where the water comes in. Move the hot gum
    // (stick, keys or mouse) onto one and hold E / A to work it in; when the
    // gum cools, hold it over the fire to heat it again.
    private sealed class PatchGame : Game
    {
        private readonly List<Vector2> _leaks = new();   // x along, y size
        private readonly float[] _fill; private readonly float[] _yAt;
        private Vector2 _cur = new(0.08f, 0.85f); private float _heat = 1f; private readonly float _limit;
        private int _autoTarget = -1;
        public override string Help => "Move onto a leak and hold E / A to gum it; hold over the fire (lower left) to heat the gum.";
        public PatchGame(List<Vector2> leaks, float diff, bool planked)
        {
            Planked = planked;
            if (leaks.Count == 0) leaks.Add(new Vector2(0.5f, 0.3f));
            _leaks.AddRange(leaks);
            _fill = new float[_leaks.Count]; _yAt = new float[_leaks.Count];
            for (int i = 0; i < _leaks.Count; i++) { _yAt[i] = 0.3f + 0.55f * Mathf.Abs(Mathf.Sin(_leaks[i].X * 37.1f)); Fills.Add(0f); }
            _limit = 14f + 5f * _leaks.Count - 4f * diff;
        }
        // Where a leak sits on the hull as drawn (HullOutline over the whole board).
        private Vector2 LeakAt(int i)
        {
            float t = 0.12f + 0.76f * _leaks[i].X;
            return new Vector2(0.08f + 0.84f * t, 0.35f + 0.32f * Mathf.Sin(t * Mathf.Pi) * _yAt[i] + 0.02f);
        }
        private static readonly Vector2 Fire = new(0.06f, 0.88f);
        public override void Step(float dt, In inp)
        {
            if (inp.MouseMoved && inp.Mouse.X is > 0f and < 1f && inp.Mouse.Y is > 0f and < 1f) _cur = _cur.MoveToward(inp.Mouse, 1.4f * dt);
            else _cur = (_cur + inp.Axis * 0.6f * dt).Clamp(Vector2.Zero, Vector2.One);
            bool atFire = _cur.DistanceTo(Fire) < 0.08f;
            if (inp.Held && atFire) _heat = Mathf.Min(1f, _heat + 0.7f * dt);
            else _heat = Mathf.Max(0f, _heat - (inp.Held ? 0.16f : 0.05f) * dt);
            Status = _heat < 0.15f ? "The gum is cold. Back to the fire with it." : "";
            if (inp.Held && !atFire && _heat >= 0.15f)
                for (int i = 0; i < _leaks.Count; i++)
                    if (_cur.DistanceTo(LeakAt(i)) < 0.045f + 0.03f * _leaks[i].Y && _fill[i] < 1f)
                    {
                        _fill[i] = Mathf.Min(1f, _fill[i] + dt * 0.75f / (0.5f + _leaks[i].Y));
                        Fills[i] = _fill[i];
                    }
            bool all = true; foreach (var f in _fill) if (f < 1f) all = false;
            if (all || Time >= _limit)
            {
                float s = 0f; foreach (var f in _fill) s += f;
                Score = s / _fill.Length;
                Done = true;
            }
        }
        public override In AutoInput(float dt)
        {
            float speed = 0.5f + 0.5f * Skill;
            if (_heat < 0.2f + Wobble(0.05f) || _autoTarget == -2)
            {
                _autoTarget = _heat < 0.95f ? -2 : -1;
                var to = Fire - _cur;
                return new In { Axis = to.Length() > 0.03f ? to.Normalized() * speed : Vector2.Zero, Held = to.Length() < 0.05f };
            }
            if (_autoTarget < 0 || _fill[_autoTarget] >= 1f)
            {
                _autoTarget = -1; float best = 9f;
                for (int i = 0; i < _fill.Length; i++) if (_fill[i] < 1f && _cur.DistanceTo(LeakAt(i)) < best) { best = _cur.DistanceTo(LeakAt(i)); _autoTarget = i; }
                if (_autoTarget < 0) return default;
            }
            var d = LeakAt(_autoTarget) + new Vector2(Wobble(0.01f), Wobble(0.01f)) - _cur;
            return new In { Axis = d.Length() > 0.02f ? d.Normalized() * speed : Vector2.Zero, Held = d.Length() < 0.04f };
        }
        public override void Draw(HandWorkCanvas c, Rect2 r, Font font)
        {
            DrawHull(c, r, Planked);
            Vector2 P(Vector2 v) => r.Position + new Vector2(r.Size.X * v.X, r.Size.Y * v.Y);
            for (int i = 0; i < _leaks.Count; i++)
            {
                var at = P(LeakAt(i)); float rad = 7f + 9f * _leaks[i].Y;
                c.DrawCircle(at, rad, new Color(0.2f, 0.35f, 0.55f, 0.75f * (1f - _fill[i])));
                c.DrawCircle(at, rad * _fill[i], new Color(0.35f, 0.18f, 0.05f));
                if (_fill[i] >= 1f) c.DrawArc(at, rad, 0f, Mathf.Tau, 16, new Color(0.2f, 0.45f, 0.15f), 2f);
            }
            var fire = P(Fire);
            c.DrawCircle(fire, 16f, new Color(0.35f, 0.2f, 0.1f));
            c.DrawCircle(fire + new Vector2(0, -6), 9f + 2f * Mathf.Sin(Time * 9f), new Color(0.95f, 0.5f, 0.1f));
            var cur = P(_cur);
            c.DrawCircle(cur, 8f, _heat > 0.15f ? new Color(0.95f, 0.6f, 0.15f) : new Color(0.4f, 0.35f, 0.3f));
            c.DrawLine(cur, cur + new Vector2(16, -36), new Color(0.3f, 0.2f, 0.1f), 4f);
            Bar(c, new Rect2(new Vector2(r.End.X - 140, r.Position.Y + 20), new Vector2(120, 12)), _heat, new Color(0.9f, 0.45f, 0.1f), "Heat", font);
            Bar(c, new Rect2(new Vector2(r.End.X - 140, r.Position.Y + 50), new Vector2(120, 12)), 1f - Time / _limit, new Color(0.4f, 0.55f, 0.7f), "Daylight", font);
            int done = 0; foreach (var f in _fill) if (f >= 1f) done++;
            c.DrawString(font, r.Position + new Vector2(12, 20), $"Leaks stopped {done}/{_fill.Length}", HorizontalAlignment.Left, -1, 14, new Color(0.25f, 0.16f, 0.08f));
        }
    }
}
