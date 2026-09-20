using Godot;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.Agents;

// Tier 0 subsistence (design doc 3.1 / 17): hunger, thirst, warmth, all
// 0..100 where 100 is fully satisfied. These drain in game-hours, so the
// same code runs an NPC ticked once a day as the player ticked every frame.
public class Needs
{
    public float Hunger = 100f;
    public float Thirst = 100f;
    public float Warmth = 100f;
    public float Health = 100f;

    // Points per game hour.
    private const float HungerBase = 1.4f;      // ~3 days to empty
    private const float ThirstBase = 2.8f;      // ~1.5 days
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

    public void Tick(float gameHours, float activity, bool sheltered, float heat, float clothingWarmth, Season season, float daylight)
    {
        if (gameHours <= 0f || IsDead) return;

        float hungerRate = HungerBase * activity * (season == Season.Winter ? 1.3f : 1f);
        Hunger = Mathf.Max(0f, Hunger - hungerRate * gameHours);
        Thirst = Mathf.Max(0f, Thirst - ThirstBase * gameHours);

        // Shelter blocks wind; only a fire produces warmth. Real shelter is
        // both together (design doc 17.3 / 18.4).
        float exposure = Climate.ExposurePerHour(season, daylight) * (sheltered ? ShelterWindFactor : 1f);
        float loss = Mathf.Max(0f, exposure - clothingWarmth);
        float gain = heat * HearthRecovery * (sheltered ? 1f : OpenFireFactor);
        Warmth = Mathf.Clamp(Warmth + (gain - loss) * gameHours, 0f, 100f);

        float damage = 0f;
        if (Warmth <= 0f) damage += 6f;
        if (Thirst <= 0f) damage += 5f;
        if (Hunger <= 0f) damage += 3f;
        Health = Mathf.Clamp(Health - damage * gameHours, 0f, 100f);
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
    public static float ExposurePerHour(Season season, float daylight)
    {
        (float day, float night) = season switch
        {
            Season.Winter => (5f, 10f),
            Season.OpenWater => (0f, 2f),
            _ => (1.5f, 4f),
        };
        return Mathf.Lerp(night, day, daylight);
    }
}
