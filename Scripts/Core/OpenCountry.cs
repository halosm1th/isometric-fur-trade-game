using System.Collections.Generic;
using Godot;

namespace IsometricFurTradeGame.Scripts.Core;

// The open country costs its days. Bodies and buildings are life-size, but
// between the posts the map is 40 px to the km, so a man walking at the pace
// that suits a fort covers some 70 km in a game hour - a brigade's two days
// in an hour's stroll. Out past the posts, camps and towns, then, the clock
// runs by the distance walked: a km for every 1/KmPerHour of an hour, the
// same km the brigades are paddling. Nothing jumps - you still walk every
// step, and the world goes by faster round you while you do (no teleporting).
//
// Server only, like the clock. With several players the one making the most
// ground sets the pace, as the shared day must.
public static class OpenCountry
{
    public const float KmPerHour = 5f;            // on foot in the bush, going steadily
    private const float SettledPx = 2400f;        // this near a post's walls is still the post
    private const float TownPx = 1200f;
    private const float JumpPx = 600f;            // more in a frame than legs can do: a load, a ride, a respawn
    // Under GameCalendar.LazyMotion, so the people and beasts you meet out
    // there still walk and think rather than being resolved in a jump.
    private const float MaxClockSpeed = GameCalendar.LazyMotion - 1f;

    public static bool Charging { get; private set; }

    private static readonly Dictionary<ulong, Vector2> _last = new();
    private static readonly Dictionary<ulong, bool> _wild = new();
    private static float _checkIn;
    private static bool _wasLogged;
    private static float _sinceStep = 99f, _owed;

    // Game hours the walking this frame has earned, 0 if nobody is out in the wilds.
    public static float Hours(double delta, float secondsPerHour)
    {
        if (!World.WorldMap.Loaded || delta <= 0) { Charging = false; return 0f; }
        _checkIn -= (float)delta;
        bool recheck = _checkIn <= 0f;
        if (recheck) _checkIn = 0.5f;

        float km = 0f;
        foreach (var p in Sim.Players())
        {
            ulong id = p.GetInstanceId();
            var pos = p.GlobalPosition;
            bool had = _last.TryGetValue(id, out var was);
            _last[id] = pos;
            if (!had || p.IsDead || p.TravellingServer || p.RideId != 0) continue;
            if (recheck || !_wild.ContainsKey(id)) _wild[id] = Wild(pos);
            if (!_wild[id]) continue;
            float moved = pos.DistanceTo(was);
            if (moved > JumpPx) continue;
            km = Mathf.Max(km, moved / World.WorldMap.PxPerKm);
        }
        // A physics tick's step lands in one render frame; what the cap holds
        // back is paid over the next frames (but never banked for long).
        _owed = Mathf.Min(_owed + km / KmPerHour, 0.5f);
        float hours = Mathf.Min(_owed, (float)delta / secondsPerHour * MaxClockSpeed);
        _owed -= hours;
        // Bodies move on the physics tick, so some frames see no step at all:
        // "charging" holds for a second after the last one.
        bool wasCharging = Charging;
        _sinceStep = hours > (float)delta / secondsPerHour ? 0f : _sinceStep + (float)delta;
        Charging = _sinceStep < 1f;
        if (Charging != wasCharging && (_wasLogged || Charging))
        {
            _wasLogged = true;
            GD.Print(Charging ? $"[country] out in the open country: the clock goes by the miles ({GameCalendar.Instance?.DateString} {GameCalendar.Instance?.TimeString})" : "[country] back in settled ground, or standing: the usual clock");
        }
        return hours;
    }

    private static bool Wild(Vector2 at) =>
        !World.Posts.NearAny(at, SettledPx) && !World.Camps.NearAny(at) && !World.Towns.NearAny(at, TownPx);

    public static void Reset() { _last.Clear(); _wild.Clear(); Charging = false; _owed = 0f; }
}
