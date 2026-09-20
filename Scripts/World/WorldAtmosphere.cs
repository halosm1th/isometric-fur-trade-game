using Godot;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.World;

// Drives the scene's CanvasModulate from the calendar: a day/night cycle
// whose length follows the season, warm light at dawn and dusk, and a
// colder, dimmer grade through the winter months.
public partial class WorldAtmosphere : CanvasModulate
{
    [Export] public Color Day = new(0.86f, 0.90f, 0.97f);
    [Export] public Color Night = new(0.22f, 0.27f, 0.45f);
    [Export] public Color Twilight = new(0.98f, 0.70f, 0.52f);
    [Export] public Color WinterTint = new(0.86f, 0.92f, 1.04f);
    [Export] public Color SummerTint = new(1.02f, 1.0f, 0.94f);

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint()) return;
        var cal = GameCalendar.Instance;
        if (cal == null) return;

        float light = cal.DaylightFactor;
        Color c = Night.Lerp(Day, light);

        // Warm bump peaking mid-twilight, gone in full day or full night.
        float warmth = 1f - Mathf.Abs(light * 2f - 1f);
        c = c.Lerp(Twilight * c.V + c * (1 - c.V), warmth * 0.55f);

        Color season = cal.CurrentSeason switch
        {
            Season.Winter => WinterTint,
            Season.OpenWater => SummerTint,
            _ => Colors.White,
        };
        float sky = cal.Weather.LightMultiplier;
        Color target = new(c.R * season.R * sky, c.G * season.G * sky, c.B * season.B * Mathf.Lerp(1f, sky, 0.7f), 1f);

        Color = Color.Lerp(target, (float)delta * 2f);
    }
}
