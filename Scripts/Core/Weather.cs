using System.Collections.Generic;
using Godot;

namespace IsometricFurTradeGame.Scripts.Core;

public enum SkyState { Clear, Overcast, Snow, Blizzard, Rain, Storm }

// Weather (design doc 14.2): a sky state that scales exposure and dims the
// light. A blizzard is the thing that kills a man caught between the woodpile
// and the door.
//
// The country is too big for one sky. It is cut into regions about two
// hundred kilometres across, and each rolls its own weather from its own
// climate (Agents/Climate): snow where it is cold enough to snow and rain
// where it is not, the dry plains clearer than the wet woods and the Pacific
// slope, and a front that comes in on the west wind - a region often takes
// the weather its western neighbour had. `Current` is the sky over the local
// player (or York, with nobody about), so the light, the sound and the HUD
// keep asking what they always asked; anything happening somewhere in
// particular asks `At(position)`.
public class Weather
{
    public const float RegionPx = 8000f;

    private class Region { public SkyState Sky; public float Hours; }
    private readonly Dictionary<Vector2I, Region> _regions = new();
    private readonly List<Vector2I> _due = new();

    // Where "here" is: the local player's position, set every frame.
    public Vector2 Focus;
    public bool HasFocus;

    public SkyState Current => Of(KeyOf(Focus)).Sky;
    public float HoursLeft => Of(KeyOf(Focus)).Hours;

    public SkyState At(Vector2 world) => Of(KeyOf(world)).Sky;
    public bool FoulAt(Vector2 world) => At(world) is SkyState.Blizzard or SkyState.Storm;

    public static Vector2I KeyOf(Vector2 world) => new(Mathf.FloorToInt(world.X / RegionPx), Mathf.FloorToInt(world.Y / RegionPx));

    private Region Of(Vector2I key)
    {
        if (_regions.TryGetValue(key, out var r)) return r;
        r = new Region();
        _regions[key] = r;
        RollRegion(key, r, GameCalendar.Instance?.CurrentSeason ?? Season.Winter, false);
        return r;
    }

    // How much harder the cold bites.
    public float ExposureMultiplier => MultiplierOf(Current);
    public float ExposureMultiplierAt(Vector2 world) => MultiplierOf(At(world));
    public static float MultiplierOf(SkyState s) => s switch
    {
        SkyState.Blizzard => 2.2f,
        SkyState.Storm => 1.7f,
        SkyState.Snow => 1.3f,
        SkyState.Rain => 1.4f,   // wet clothes (design doc 17.3)
        SkyState.Overcast => 1.05f,
        _ => 1f,
    };

    // Light and visibility.
    public float LightMultiplier => Current switch
    {
        SkyState.Blizzard => 0.55f,
        SkyState.Storm => 0.6f,
        SkyState.Snow => 0.8f,
        SkyState.Rain => 0.8f,
        SkyState.Overcast => 0.9f,
        _ => 1f,
    };

    public bool Wet => Current == SkyState.Rain || Current == SkyState.Storm;

    public void Tick(float hours, Season season)
    {
        // West to east, so a front rolled this frame in the west is there to
        // be taken up by its eastern neighbour.
        _due.Clear();
        foreach (var kv in _regions) { kv.Value.Hours -= hours; if (kv.Value.Hours <= 0f) _due.Add(kv.Key); }
        if (_due.Count == 0) return;
        _due.Sort((a, b) => a.X.CompareTo(b.X));
        foreach (var k in _due) RollRegion(k, _regions[k], season, true);
    }

    // A fresh sky everywhere (the start of a game).
    public void Roll(Season season)
    {
        foreach (var kv in _regions) RollRegion(kv.Key, kv.Value, season, false);
        Of(KeyOf(Focus));
    }

    private void RollRegion(Vector2I key, Region r, Season season, bool announce)
    {
        var was = r.Sky;
        var centre = new Vector2((key.X + 0.5f) * RegionPx, (key.Y + 0.5f) * RegionPx);
        float doy = GameCalendar.Instance?.DayOfYear ?? 15f;
        float mean = Agents.Climate.MeanAt(centre, doy, out float lat, out float lon);
        float g = GD.Randf();
        // The west wind brings the neighbour's weather (snow turning to rain
        // if it comes into warmer country, and the other way about).
        if (_regions.TryGetValue(key + new Vector2I(-1, 0), out var west) && g < 0.4f)
            r.Sky = Convert(west.Sky, mean);
        else if (mean < -100f)
            r.Sky = Fallback(season);
        else
            r.Sky = Convert(RollFor(mean, lat, lon), mean);
        r.Hours = r.Sky switch
        {
            SkyState.Blizzard => (float)GD.RandRange(6.0, 18.0),
            SkyState.Storm => (float)GD.RandRange(4.0, 12.0),
            _ => (float)GD.RandRange(6.0, 30.0),
        };
        if (announce && r.Sky == SkyState.Blizzard && was != SkyState.Blizzard && r.Hours >= 8f && Net.Net.IsServer)
            foreach (var h in Agents.Chronicle.Households())
                if (KeyOf(World.IsoGrid.ToWorld(h.AccessCell)) == key) Agents.Chronicle.Shared(h, "a blizzard shut everyone in by the fire", 1.1f);
    }

    // The sky a region of this mean temperature tends to have. The plains
    // between the Saskatchewan and the Missouri are dry country; the Pacific
    // slope is wet.
    private static SkyState RollFor(float mean, float lat, float lon)
    {
        float r = GD.Randf();
        bool dry = lon > -115f && lon < -96f && lat < 54f;
        bool wet = lon < -120f;
        if (dry) r = Mathf.Min(0.999f, r * 0.8f);
        if (wet) r = Mathf.Min(0.999f, 0.15f + r * 0.85f);
        if (mean < -8f) return r < 0.45f ? SkyState.Clear : r < 0.7f ? SkyState.Overcast : r < 0.9f ? SkyState.Snow : SkyState.Blizzard;
        if (mean < 2f) return r < 0.35f ? SkyState.Clear : r < 0.65f ? SkyState.Overcast : r < 0.8f ? SkyState.Rain : r < 0.93f ? SkyState.Snow : SkyState.Blizzard;
        if (mean < 10f) return r < 0.4f ? SkyState.Clear : r < 0.7f ? SkyState.Overcast : r < 0.88f ? SkyState.Rain : SkyState.Storm;
        return r < 0.5f ? SkyState.Clear : r < 0.75f ? SkyState.Overcast : r < 0.93f ? SkyState.Rain : SkyState.Storm;
    }

    // Precipitation takes the form the air allows.
    private static SkyState Convert(SkyState s, float mean)
    {
        if (mean >= 3f && s == SkyState.Snow) return SkyState.Rain;
        if (mean >= 3f && s == SkyState.Blizzard) return SkyState.Storm;
        if (mean < -3f && s == SkyState.Rain) return SkyState.Snow;
        if (mean < -3f && s == SkyState.Storm) return SkyState.Blizzard;
        return s;
    }

    // Before the map is loaded: the old one-sky roll by season.
    private static SkyState Fallback(Season season)
    {
        float r = GD.Randf();
        return season switch
        {
            Season.Winter => r < 0.45f ? SkyState.Clear : r < 0.7f ? SkyState.Overcast : r < 0.9f ? SkyState.Snow : SkyState.Blizzard,
            Season.OpenWater => r < 0.5f ? SkyState.Clear : r < 0.75f ? SkyState.Overcast : r < 0.93f ? SkyState.Rain : SkyState.Storm,
            _ => r < 0.35f ? SkyState.Clear : r < 0.65f ? SkyState.Overcast : r < 0.8f ? SkyState.Rain : r < 0.93f ? SkyState.Snow : SkyState.Blizzard,
        };
    }

    // The sky over "here" (the debug buttons, a joined player hearing from the server).
    public void Force(SkyState sky, float hours) { var r = Of(KeyOf(Focus)); r.Sky = sky; r.Hours = hours; }

    public Godot.Collections.Array Save()
    {
        var a = new Godot.Collections.Array();
        foreach (var kv in _regions) a.Add(new Godot.Collections.Array { kv.Key.X, kv.Key.Y, (int)kv.Value.Sky, kv.Value.Hours });
        return a;
    }

    public void Load(Godot.Collections.Array a)
    {
        if (a == null) return;
        _regions.Clear();
        foreach (var v in a)
        {
            var e = v.AsGodotArray();
            _regions[new Vector2I(e[0].AsInt32(), e[1].AsInt32())] = new Region { Sky = (SkyState)e[2].AsInt32(), Hours = e[3].AsSingle() };
        }
    }

    public void Clear() => _regions.Clear();

    public string Describe() => Current switch
    {
        SkyState.Clear => "clear",
        SkyState.Overcast => "overcast",
        SkyState.Snow => "snowing",
        SkyState.Blizzard => "BLIZZARD",
        SkyState.Rain => "rain",
        SkyState.Storm => "gale",
        _ => "",
    };
}
