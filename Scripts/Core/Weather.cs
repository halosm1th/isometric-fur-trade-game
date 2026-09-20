using Godot;

namespace IsometricFurTradeGame.Scripts.Core;

public enum SkyState { Clear, Overcast, Snow, Blizzard, Rain, Storm }

// Weather (design doc 14.2), simplest useful form: a sky state rolled a few
// times a day from the season, that scales exposure and dims the light.
// A blizzard is the thing that kills a man caught between the woodpile and
// the door.
public class Weather
{
    public SkyState Current { get; private set; } = SkyState.Clear;
    public float HoursLeft { get; private set; }

    // How much harder the cold bites.
    public float ExposureMultiplier => Current switch
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
        HoursLeft -= hours;
        if (HoursLeft > 0f) return;
        Roll(season);
    }

    public void Roll(Season season)
    {
        float r = GD.Randf();
        Current = season switch
        {
            Season.Winter => r < 0.45f ? SkyState.Clear : r < 0.7f ? SkyState.Overcast : r < 0.9f ? SkyState.Snow : SkyState.Blizzard,
            Season.Breakup => r < 0.4f ? SkyState.Clear : r < 0.7f ? SkyState.Overcast : r < 0.85f ? SkyState.Rain : r < 0.95f ? SkyState.Snow : SkyState.Storm,
            Season.OpenWater => r < 0.5f ? SkyState.Clear : r < 0.75f ? SkyState.Overcast : r < 0.93f ? SkyState.Rain : SkyState.Storm,
            _ => r < 0.35f ? SkyState.Clear : r < 0.65f ? SkyState.Overcast : r < 0.8f ? SkyState.Rain : r < 0.93f ? SkyState.Snow : SkyState.Blizzard,
        };
        HoursLeft = Current switch
        {
            SkyState.Blizzard => (float)GD.RandRange(6.0, 18.0),
            SkyState.Storm => (float)GD.RandRange(4.0, 12.0),
            _ => (float)GD.RandRange(6.0, 30.0),
        };
    }

    public void Force(SkyState sky, float hours) { Current = sky; HoursLeft = hours; }

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
