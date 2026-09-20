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

    [Export] public float SecondsPerGameHour = 20f;
    [Export] public float FastForwardMultiplier = 40f;
    [Export] public bool Paused = false;

    public Season CurrentSeason { get; private set; }

    // Game hours that elapsed during the last frame; agents tick off this.
    public float DeltaGameHours { get; private set; }

    private int _startYear, _startMonth, _startDay;
    private float _startHour;

    // (month, day, name). Fire once per year when the date is reached.
    private static readonly (int m, int d, string name)[] AnnualEvents =
    {
        (6, 5, "Breakup"),
        (6, 15, "SummerBisonHunt"),
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
        }

        (_startYear, _startMonth, _startDay, _startHour) = (Year, Month, Day, Hour);
        CurrentSeason = SeasonFor(Month);
        _lastHour = Mathf.FloorToInt(Hour);
    }

    public void Reset()
    {
        (Year, Month, Day, Hour) = (_startYear, _startMonth, _startDay, _startHour);
        CurrentSeason = SeasonFor(Month);
        _lastHour = Mathf.FloorToInt(Hour);
    }

    public override void _Process(double delta)
    {
        if (Paused) { DeltaGameHours = 0f; return; }

        float speed = Input.IsActionPressed("time_fast") ? FastForwardMultiplier : 1f;
        DeltaGameHours = (float)delta / SecondsPerGameHour * speed;
        Hour += DeltaGameHours;

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
    public float DaylightFactor
    {
        get
        {
            var (rise, set) = Daylight[Month - 1];
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
