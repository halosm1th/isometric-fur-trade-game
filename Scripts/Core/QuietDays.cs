using System.Collections.Generic;
using Godot;

namespace IsometricFurTradeGame.Scripts.Core;

// Quiet days (design doc P.3: a year in hours, not days of play). The day is
// twelve minutes when you are playing it; when you leave the controls alone
// the days go by at speed, and they stop going by the moment something
// happens: news, a reveal, a quest step, the calendar's events, a blow, an
// empty belly, the weather closing in. Everyone alive has to be leaving it
// alone, as with sleep (one shared clock).
//
// The owner's machine notices the idle hands (PlayerController.QuietWatch)
// and asks; the server runs the clock and calls the stops.
public static class QuietDays
{
    public const float IdleSeconds = 20f;   // real seconds with hands off before the days begin to pass
    public const float Speed = 80f;         // a day in about nine seconds at the usual clock

    public static bool Running { get; private set; }
    private static readonly Dictionary<Player.PlayerController, bool> _wasFoul = new();

    // Each frame on the server, from the calendar: may the days pass now?
    public static bool Tick()
    {
        bool was = Running;
        Running = false;
        int alive = 0;
        foreach (var p in Sim.Players())
        {
            if (p.IsDead) continue;
            alive++;
            if (!p.WantsQuiet) { Why("not asked yet"); return false; }
            string why = Busy(p);
            Why(why);
            if (why != null) { Stop(p, why); return false; }
        }
        Running = alive > 0;
        if (Running && !was) GD.Print($"[quiet] the days begin to pass ({GameCalendar.Instance.DateString})");
        if (Running && !was) foreach (var p in Sim.Players()) _wasFoul[p] = GameCalendar.Instance.Weather.FoulAt(p.GlobalPosition);
        return Running;
    }

    // Something that needs the player: the reason, or null if the day is quiet.
    // An empty reason stops it without a word (travel mode keeps its own clock).
    private static string Busy(Player.PlayerController p)
    {
        var cal = GameCalendar.Instance;
        if (p.TravellingServer) return "";
        if (p.LastAttackedAgo < 8f && p.LastAttacker != null && GodotObject.IsInstanceValid(p.LastAttacker)) return $"{p.LastAttacker.Describe()} is at you.";
        if (p.Needs.Health < 50f) return "You are hurt.";
        if (p.Needs.Hunger < 30f) return "You are hungry.";
        if (p.Needs.Thirst < 30f) return "You are thirsty.";
        if (p.Needs.Warmth < 35f) return "You are cold.";
        if (Sim.Ship != null && Sim.Ship.IsDocked) return "The ship is in. These days are lived, not passed.";
        bool foul = cal.Weather.FoulAt(p.GlobalPosition);
        bool wasFoul = _wasFoul.GetValueOrDefault(p);
        _wasFoul[p] = foul;
        if (foul && !wasFoul) return "The weather closes in.";
        return null;
    }

    // Something happened: whoever was letting the days go takes them up
    // again. null means everyone (the calendar's events, the headlines).
    // `cause` is for the log only.
    public static void Interrupt(Player.PlayerController who = null, string why = null, string cause = null)
    {
        if (!Net.Net.IsServer) return;
        if (Running && cause != null) GD.Print($"[quiet] stopped: {cause}");
        foreach (var p in Sim.Players())
            if ((who == null || p == who) && p.WantsQuiet) Stop(p, why);
    }

    private static void Stop(Player.PlayerController p, string why)
    {
        bool running = Running;
        p.WantsQuiet = false;
        Running = false;
        if (running && !string.IsNullOrEmpty(why)) p.Say($"{why} The quiet days end.");
    }

    // Dev (`--quiet`, which also lets a headless run go quiet): what is
    // holding the quiet days back, when it changes.
    private static readonly bool _log = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--quiet") >= 0;
    private static string _lastWhy = "-";
    private static void Why(string why)
    {
        if (!_log || why == _lastWhy) return;
        _lastWhy = why;
        Dev($"{(why == null ? "quiet" : why == "" ? "travelling" : why)}");
    }
    public static void Dev(string s)
    {
        if (_log) GD.Print($"[quiet] {GameCalendar.Instance?.DateString} {GameCalendar.Instance?.Hour:0.0}h: {s}");
    }

    public static void Reset() { Running = false; _wasFoul.Clear(); }

    public static string Status => Running ? "The days go quietly by. Touch any key to take them up." : "";
}
