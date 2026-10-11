using Godot;
using System.Collections.Generic;

namespace IsometricFurTradeGame.Scripts.Core;

public enum Season
{
    Winter,     // Nov-Apr: bay frozen, overland by snowshoe and sled
    Breakup,    // May-Jun: ice goes out, geese arrive, rivers open
    OpenWater,  // Jul-Aug: the supply ship can reach the bay
    FreezeUp,   // Sep-Oct: ship must leave; last brigades in
}

// Autoloaded game clock. Everything time-dependent (light, weather, the
// annual ship, the hunts) hangs off this. Dates are on Hudson Bay terms:
// York Factory is at 57 N, so daylight swings from ~6 hours in December to
// ~18 in June, and the sea is only open for about two months a year.
public partial class GameCalendar : Node
{
    public static GameCalendar Instance { get; private set; }

    [Signal] public delegate void HourPassedEventHandler(int hour);
    [Signal] public delegate void DayPassedEventHandler(int year, int month, int day);
    [Signal] public delegate void SeasonChangedEventHandler(int season);
    [Signal] public delegate void CalendarEventEventHandler(string eventName);

    [Export] public int Year = 1811;
    [Export] public int Month = 1;
    [Export] public int Day = 1;
    [Export] public float Hour = 9f;

    // A year should take four to six hours of play, not a day (design doc
    // P.3), and the compression is uneven: the trading season and the
    // reckoning run close to live, deep winter nights go by in a breath.
    // A day in twelve minutes of play (the user's pick, Oct 2026).
    [Export] public float SecondsPerGameHour = 30f;
    [Export] public bool UnevenPacing = true;
    public float PacingFactor { get; private set; } = 1f;
    public string ChapterName => CurrentSeason switch
    {
        Season.FreezeUp => "Freeze-up - the postings are decided",
        Season.Winter => "Winter - hold the men together",
        Season.Breakup => "Breakup - the year's trade is won here",
        _ => Month is 7 or 8 ? "The ship - the reckoning" : "Open water",
    };

    // How dense this hour is: what's happening, and who is near enough to care.
    private float Pacing()
    {
        if (!UnevenPacing) return 1f;
        float f = 1f;
        // The reckoning and the trade run slow and close.
        if (Sim.Ship != null && Sim.Ship.IsDocked) f *= 2.2f;
        if (CurrentSeason == Season.Breakup) f *= 1.5f;
        if (Month is 7 or 8) f *= 1.3f;
        // Deep winter, and the small hours, go by fast - unless something is happening.
        bool night = Hour < 5f || Hour >= 21f;
        if (night) f *= CurrentSeason == Season.Winter ? 0.3f : 0.45f;
        else if (CurrentSeason == Season.Winter) f *= 0.65f;
        if (Weather.Current is SkyState.Blizzard or SkyState.Storm) f *= 0.8f;
        // Company slows the clock: a crowd round you is a scene.
        var pc = Sim.Player;
        if (pc != null && GodotObject.IsInstanceValid(pc))
        {
            int near = 0;
            foreach (var a in Sim.AgentsWithin(pc.GlobalPosition, 420f)) if (a != pc) near++;
            if (near >= 2) f *= 1.5f; else if (near == 1) f *= 1.2f;
            if (pc.LastAttackedAgo < 8f) f *= 2.5f;
        }
        return Mathf.Clamp(f, 0.25f, 3f);
    }
    [Export] public float FastForwardMultiplier = 40f;
    [Export] public bool Paused = false;

    public Season CurrentSeason { get; private set; }
    public readonly Weather Weather = new();

    // Game hours that elapsed during the last frame; agents tick off this.
    public float DeltaGameHours { get; private set; }

    // Hours still to burn through at skip speed (sleeping, waiting).
    public float SkipRemaining { get; private set; }
    public bool IsSkipping => SkipRemaining > 0f;
    private const float SkipSpeed = 400f;
    public const float TravelBoost = 6f;
    // Simulation stays sane if no frame ever covers more than this.
    private const float MaxDeltaHours = 0.25f;

    // How much faster than the baseline clock (10 s per game hour) time is
    // running right now. NPCs and animals scale their movement by this so a
    // sped-up world still resolves their days (design doc 18.2's lazy
    // resolution, done crudely). 1 at normal speed.
    public float MotionScale { get; private set; } = 1f;
    private const float BaselineSecondsPerHour = 10f;

    // Fractional day-of-year, useful for anything seasonal.
    public float DayOfYear
    {
        get { float d = Day; for (int m = 0; m < Month - 1; m++) d += DaysInMonth[m]; return d + Hour / 24f; }
    }

    // Skip forward. Used by the wait key and the debug menu.
    public void SkipHours(float hours) => SkipRemaining += Mathf.Max(0f, hours);
    // Travel mode (UI/TravelMode.cs) stops: whatever it asked for and has not had is forgotten.
    public void CancelSkip() => SkipRemaining = 0f;

    public void SkipUntilHour(float hour)
    {
        float diff = hour - Hour;
        if (diff <= 0.01f) diff += 24f;
        SkipHours(diff);
    }

    // Debug: jump straight to a date. Fires SeasonChanged if needed but not
    // the events in between - it's a teleport, not a simulation.
    public void SetDate(int year, int month, int day, float hour)
    {
        Year = year; Month = Mathf.Clamp(month, 1, 12);
        Day = Mathf.Clamp(day, 1, DaysInMonth[Month - 1]); Hour = Mathf.Clamp(hour, 0f, 23.99f);
        _lastHour = Mathf.FloorToInt(Hour);
        var season = SeasonFor(Month);
        if (season != CurrentSeason)
        {
            CurrentSeason = season;
            EmitSignal(SignalName.SeasonChanged, (int)season);
        }
        EmitSignal(SignalName.DayPassed, Year, Month, Day);
    }

    private int _startYear, _startMonth, _startDay;
    private float _startHour;

    // (month, day, name). Fire once per year when the date is reached.
    private static readonly (int m, int d, string name)[] AnnualEvents =
    {
        (6, 5, "Breakup"),
        (6, 15, "SummerBisonHunt"),
        (7, 12, "Brigade"),
        (8, 20, "ShipArrives"),
        (9, 15, "ShipDeparts"),
        (10, 15, "FallBisonHunt"),
        (11, 10, "FreezeUp"),
    };

    private static readonly int[] DaysInMonth = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

    public static readonly string[] MonthNames =
    {
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December",
    };

    // Approximate local sunrise / sunset by month at 57 N.
    private static readonly (float rise, float set)[] Daylight =
    {
        (8.5f, 15.5f), (7.8f, 16.8f), (6.7f, 18.0f), (5.5f, 19.3f),
        (4.3f, 20.5f), (3.7f, 21.3f), (4.0f, 21.0f), (5.0f, 19.8f),
        (6.2f, 18.5f), (7.3f, 17.2f), (8.3f, 16.0f), (9.0f, 15.2f),
    };

    private int _lastHour;

    public override void _Ready()
    {
        Instance = this;

        // Dev aid: `godot -- --hour=20` starts the clock at that hour.
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--hour=") && float.TryParse(arg.Substring(7), out float h)) Hour = h;
            if (arg.StartsWith("--speed=") && float.TryParse(arg.Substring(8), out float s)) SecondsPerGameHour = s;
            // --date=1811-08-19
            if (arg.StartsWith("--date="))
            {
                var parts = arg.Substring(7).Split('-');
                if (parts.Length == 3 && int.TryParse(parts[0], out int y) && int.TryParse(parts[1], out int m) && int.TryParse(parts[2], out int d))
                {
                    Year = y; Month = Mathf.Clamp(m, 1, 12); Day = Mathf.Clamp(d, 1, DaysInMonth[Month - 1]);
                }
            }
        }

        (_startYear, _startMonth, _startDay, _startHour) = (Year, Month, Day, Hour);
        CurrentSeason = SeasonFor(Month);
        Weather.Roll(CurrentSeason);
        _lastHour = Mathf.FloorToInt(Hour);
    }

    public void Reset()
    {
        (Year, Month, Day, Hour) = (_startYear, _startMonth, _startDay, _startHour);
        CurrentSeason = SeasonFor(Month);
        _lastHour = Mathf.FloorToInt(Hour);
    }

    // A client takes the server's clock and only free-runs between syncs.
    private bool _clientMode;
    public void ClientSync(int year, int month, int day, float hour, SkyState sky)
    {
        _clientMode = true;
        bool dayChanged = day != Day || month != Month || year != Year;
        Year = year; Month = month; Day = day;
        // Ease onto the server's hour unless we're far off.
        Hour = Mathf.Abs(hour - Hour) > 0.5f ? hour : Mathf.Lerp(Hour, hour, 0.5f);
        var season = SeasonFor(Month);
        if (season != CurrentSeason) { CurrentSeason = season; EmitSignal(SignalName.SeasonChanged, (int)season); }
        if (dayChanged) EmitSignal(SignalName.DayPassed, Year, Month, Day);
        if (Weather.Current != sky) Weather.Force(sky, 6f);
        SkipRemaining = 0f;
    }

    public override void _Process(double delta)
    {
        if (Paused) { DeltaGameHours = 0f; MotionScale = 1f; return; }
        if (_clientMode)
        {
            // Free-run the hour hand between syncs; no events fire here.
            DeltaGameHours = (float)delta / SecondsPerGameHour;
            Hour += DeltaGameHours;
            if (Hour >= 24f) Hour -= 24f;
            MotionScale = 1f;
            return;
        }

        PacingFactor = Pacing();
        float speed = (Input.IsActionPressed("time_fast") ? FastForwardMultiplier : 1f) / PacingFactor;
        // Someone on the river road: the day goes by at a march for everyone (design doc 18.3, 21.3).
        bool travelling = false;
        foreach (var p in Sim.Players()) if (p.TravellingServer && !p.IsDead) travelling = true;
        if (travelling) speed = Mathf.Max(speed, TravelBoost);
        if (SkipRemaining > 0f) speed = SkipSpeed;
        DeltaGameHours = Mathf.Min(MaxDeltaHours, (float)delta / SecondsPerGameHour * speed);
        if (SkipRemaining > 0f)
        {
            DeltaGameHours = Mathf.Min(DeltaGameHours, SkipRemaining);
            SkipRemaining -= DeltaGameHours;
        }
        Hour += DeltaGameHours;
        Weather.Tick(DeltaGameHours, CurrentSeason);
        MotionScale = delta > 0 ? Mathf.Max(1f, DeltaGameHours / ((float)delta / BaselineSecondsPerHour)) : 1f;

        int h = Mathf.FloorToInt(Hour);
        if (h != _lastHour)
        {
            _lastHour = h % 24;
            EmitSignal(SignalName.HourPassed, _lastHour);
        }

        while (Hour >= 24f)
        {
            Hour -= 24f;
            AdvanceDay();
        }
    }

    private void AdvanceDay()
    {
        Day++;
        if (Day > DaysInMonth[Month - 1])
        {
            Day = 1;
            Month++;
            if (Month > 12)
            {
                Month = 1;
                Year++;
            }
            var season = SeasonFor(Month);
            if (season != CurrentSeason)
            {
                CurrentSeason = season;
                EmitSignal(SignalName.SeasonChanged, (int)season);
            }
        }

        EmitSignal(SignalName.DayPassed, Year, Month, Day);

        foreach (var (m, d, name) in AnnualEvents)
        {
            if (m == Month && d == Day)
            {
                EmitSignal(SignalName.CalendarEvent, name);
            }
        }
    }

    public static Season SeasonFor(int month) => month switch
    {
        5 or 6 => Season.Breakup,
        7 or 8 => Season.OpenWater,
        9 or 10 => Season.FreezeUp,
        _ => Season.Winter,
    };

    public bool IsFrozen => CurrentSeason == Season.Winter;

    // 0 = full night, 1 = full day, with ~1.5h of twilight either side.
    // Where on the earth we are (design doc 14.3): York Factory sits at 57 N.
    // Day length comes from latitude and the date; then a gameplay thumb on
    // the scale keeps the day a little longer than the night except in the
    // dead of a northern winter.
    [Export] public float LatitudeDeg = 57f;
    public float DayLengthHours
    {
        get
        {
            float doy = DayOfYear;
            float decl = 23.44f * Mathf.Sin(Mathf.Tau * (doy - 81f) / 365f);
            float lat = Mathf.DegToRad(LatitudeDeg), d = Mathf.DegToRad(decl);
            float x = Mathf.Clamp(-Mathf.Tan(lat) * Mathf.Tan(d), -1f, 1f);
            float astro = 2f * Mathf.RadToDeg(Mathf.Acos(x)) / 15f;
            float biased = Mathf.Lerp(astro, 13f, 0.4f);
            bool deadOfWinter = LatitudeDeg > 55f && (Month == 12 || Month == 1);
            return Mathf.Clamp(biased, deadOfWinter ? 9.5f : 12.6f, 20f);
        }
    }
    public float Sunrise => 12.3f - DayLengthHours / 2f;
    public float Sunset => 12.3f + DayLengthHours / 2f;

    public float DaylightFactor
    {
        get
        {
            float rise = Sunrise, set = Sunset;
            const float twilight = 1.5f;
            if (Hour < rise - twilight || Hour > set + twilight) return 0f;
            if (Hour < rise) return (Hour - (rise - twilight)) / twilight;
            if (Hour > set) return 1f - (Hour - set) / twilight;
            return 1f;
        }
    }

    public string DateString =>
        $"{Day} {MonthNames[Month - 1]} {Year}";

    public string TimeString
    {
        get
        {
            int h = Mathf.FloorToInt(Hour);
            int m = Mathf.FloorToInt((Hour - h) * 60f);
            return $"{h:00}:{m:00}";
        }
    }

    public string SeasonString => CurrentSeason switch
    {
        Season.Winter => "Winter - bay frozen",
        Season.Breakup => "Breakup",
        Season.OpenWater => "Open water",
        Season.FreezeUp => "Freeze-up",
        _ => "",
    };
}
