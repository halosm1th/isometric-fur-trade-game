using Godot;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.Agents;

// Tier 0 subsistence (design doc 3.1 / 17): hunger, thirst, warmth, all
// 0..100 where 100 is fully satisfied. These drain in game-hours, so the
// same code runs an NPC ticked once a day as the player ticked every frame.
// Four skills, 0..1 (design doc 3.10). Nobody trains; you get better by
// doing the thing, a hair at a time.
public class Skills
{
    public float Hunting = 0.15f, Trapping = 0.15f, Trade = 0.15f, Woodcraft = 0.15f;
    public void Learn(ref float skill, float amount) => skill = Mathf.Min(1f, skill + amount);
    public string Describe(float s) => s >= 0.75f ? "master" : s >= 0.5f ? "practised" : s >= 0.3f ? "passable" : "green";
    public void Seed(float hunting, float trapping, float trade, float woodcraft) { Hunting = hunting; Trapping = trapping; Trade = trade; Woodcraft = woodcraft; }
}

public class Needs
{
    public float Hunger = 100f;
    public float Thirst = 100f;
    public float Warmth = 100f;
    public float Health = 100f;

    // Points per game hour.
    private const float HungerBase = 1.1f;      // ~4 days to empty
    private const float ThirstBase = 1.8f;      // ~2.5 days
    // Warmth regained per hour standing right at a full fire, indoors.
    private const float HearthRecovery = 14f;
    // How much of the wind walls and a roof keep off you (design doc 18.1):
    // an unheated building is still cold, just less lethal.
    private const float ShelterWindFactor = 0.35f;
    // An open fire in the wind loses most of its heat to the air.
    private const float OpenFireFactor = 0.4f;

    // Below this a need starts costing capability before it costs health -
    // the "visible spiral" rather than sudden death.
    public const float Weak = 25f;

    public bool IsDead => Health <= 0f;

    public bool Invulnerable;

    // Test console: the bars stop moving altogether, which is what you want
    // when you are trying to look at something else.
    public bool GodMode;

    public void Tick(float gameHours, float activity, bool sheltered, float heat, float clothingWarmth, Season season, float daylight, bool fireEnclosed = false, float localExposure = -1f)
    {
        if (gameHours <= 0f || IsDead) return;
        if (GodMode)
        {
            Hunger = Thirst = Warmth = 100f;
            Health = 100f;
            return;
        }

        float hungerRate = HungerBase * activity * (season == Season.Winter ? 1.3f : 1f);
        Hunger = Mathf.Max(0f, Hunger - hungerRate * gameHours);
        Thirst = Mathf.Max(0f, Thirst - ThirstBase * gameHours);

        // Shelter blocks wind; only a fire produces warmth. Real shelter is
        // both together (design doc 17.3 / 18.4).
        float weather = GameCalendar.Instance?.Weather.ExposureMultiplier ?? 1f;
        float exposure = (localExposure >= 0f ? localExposure : Climate.ExposurePerHour(season, daylight)) * (sheltered ? ShelterWindFactor : weather);
        float loss = Mathf.Max(0f, exposure - clothingWarmth);
        float gain = heat * HearthRecovery * (sheltered || fireEnclosed ? 1f : OpenFireFactor);
        Warmth = Mathf.Clamp(Warmth + (gain - loss) * gameHours, 0f, 100f);

        float damage = 0f;
        if (Warmth <= 0f) damage += 3f;
        if (Thirst <= 0f) damage += 5f;
        if (Hunger <= 0f) damage += 3f;
        if (!Invulnerable) Health = Mathf.Clamp(Health - damage * gameHours, 0f, 100f);
        // Wounds knit (design doc 17.5): a point an hour when fed, watered and
        // warm, half that on the move; nothing while you're starving or freezing.
        if (damage == 0f && Hunger > 55f && Thirst > 45f && Warmth > 50f)
            Health = Mathf.Min(100f, Health + (activity > 1.2f ? 0.5f : 1f) * gameHours);
    }

    // 1.0 at full capability, falling toward 0.5 as any need runs low.
    public float CapabilityFactor
    {
        get
        {
            float worst = Mathf.Min(Hunger, Mathf.Min(Thirst, Warmth));
            if (worst >= Weak) return 1f;
            return Mathf.Lerp(0.5f, 1f, worst / Weak);
        }
    }
}

// How hard the cold bites outdoors, in warmth points per hour before
// clothing. Hudson Bay winter nights are lethal without shelter; summer
// days cost nothing.
public static class Climate
{
    // The cold where you stand (design doc 6, 17.3): the Bay coast is not the
    // plains. A mean temperature for the day of the year from the latitude -
    // York's January -26 and July +15, the Red River's -17 and +20 - milder
    // west of the mountains, the nights colder than the days; and exposure
    // grows as it falls below about 12 degrees. A July night on the
    // Saskatchewan costs a man nothing; one at York in May still bites.
    public static float ExposureAt(Vector2 world, float daylight, float dayOfYear)
    {
        if (!World.WorldMap.Loaded) return -1f;
        var cell = World.IsoGrid.FromWorld(world);
        var key = new Vector2I(cell.X >> 5, cell.Y >> 5);
        if (!_latCache.TryGetValue(key, out var ll)) { var g = World.WorldMap.LonLatOf(World.IsoGrid.ToWorld(new Vector2I(key.X << 5, key.Y << 5))); ll = new Vector2((float)g.Lon, (float)g.Lat); _latCache[key] = ll; }
        float lat = ll.Y, lon = ll.X;
        float jan = -26f + (57f - lat) * 1.3f, jul = 15f + (57f - lat) * 0.7f;
        if (lon < -118f) { jan += 12f; jul -= 1f; }   // the Pacific side: wet, mild winters
        float mean = (jan + jul) / 2f - (jul - jan) / 2f * Mathf.Cos(2f * Mathf.Pi * (dayOfYear - 15f) / 365f);
        float t = Mathf.Lerp(mean - 5f, mean + 8f, daylight);
        if (Probe) GD.Print($"[climate] lat {lat:0.0} lon {lon:0.0} day {dayOfYear:0}: mean {mean:0} C, now {t:0} C, exposure {Mathf.Max(0f, (12f - t) / 7f):0.00}/h");
        return Mathf.Max(0f, (12f - t) / 7f);
    }
    private static readonly System.Collections.Generic.Dictionary<Vector2I, Vector2> _latCache = new();
    public static bool Probe;

    public static float ExposurePerHour(Season season, float daylight)
    {
        (float day, float night) = season switch
        {
            Season.Winter => (3f, 6f),
            Season.OpenWater => (0f, 1f),
            _ => (1f, 2.5f),
        };
        return Mathf.Lerp(night, day, daylight);
    }
}
