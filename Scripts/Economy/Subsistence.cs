using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Ecology;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Economy;

// A household's week of getting its own living where nobody is watching:
// the camps' hunt, fishery and snares, an inland post's nets (CountryBand,
// TradingPostLedger). It is the household's own people's work and the
// country's own game (design doc 2, 16.4): as much as its able hands at
// home can bring in, scaled by what the grounds of its own country still
// hold, and taken off those grounds. People up close count too: their goals
// hunt and fetch, but the camp's drying, pemmican and picking are this. A country hunted out gives thin fare
// (the snares and the fishery never quite fail), and the grounds recover
// as the ecology lets them.
public static class Subsistence
{
    private const float Unmapped = 0.6f;   // a country with no grounds laid out: about as before, a little lean
    private const float Floor = 0.5f;      // snares, the fishery and the hare flats: lean, but never nothing

    // Able hands at home.
    public static int Hands(Household h)
    {
        int n = 0;
        foreach (var m in h.Members)
            if (GodotObject.IsInstanceValid(m) && !m.IsDead && !m.Away && m.OffMap == "" && m.Age >= 14 && !m.IsSick) n++;
        return n;
    }

    // The share of a full week's living the hands can bring in: half the
    // household being able hands is a full complement.
    public static float HandsShare(Household h, int mouths) => mouths <= 0 ? 0f : Mathf.Clamp(Hands(h) / (mouths * 0.5f), 0f, 1f);

    // The grounds of a household's own country that feed it through the
    // year: not the geese, which are away half of it (World/GooseHunt has them).
    private static bool Feeds(SpawnSite s, Vector2 at) =>
        !s.IsDead && s.Species is not (Species.Beaver or Species.Wolf or Species.Goose)
        && IsoGrid.ToWorld(s.Cell).DistanceTo(at) <= WorldLayout.HomeCountryPx;

    // What the household's own country still holds of food game, 0.2..1.
    // `ground` stands for the country's grounds when the take is made.
    public static float Game(Household h, out Household ground)
    {
        ground = h;
        var eco = Sim.Ecology;
        if (eco == null) return Unmapped;
        var at = IsoGrid.ToWorld(h.AccessCell);
        float have = 0f, cap = 0f;
        foreach (var s in eco.Sites) if (Feeds(s, at)) { have += s.Count; cap += s.Capacity; }
        return cap <= 0f ? Unmapped : Mathf.Clamp(have / cap, Floor, 1f);
    }

    // Meat to a beast, in pieces, by kind: a buffalo is a great deal more than a hare.
    private static float PiecesPerBeast(Species s) => s switch
    {
        Species.Buffalo => 70f, Species.Elk => 40f, Species.Caribou => 18f, _ => 1.5f,
    };

    // Off the grounds of the household's country, each in proportion to what
    // it holds: the pieces brought in, as beasts of that ground's kind.
    public static void Take(Household h, int pieces)
    {
        var eco = Sim.Ecology;
        if (h == null || eco == null || pieces <= 0) return;
        var at = IsoGrid.ToWorld(h.AccessCell);
        float have = 0f;
        foreach (var s in eco.Sites) if (Feeds(s, at)) have += s.Count;
        if (have <= 0f) return;
        foreach (var s in eco.Sites)
        {
            if (!Feeds(s, at) || s.Count <= 0f) continue;
            float beasts = pieces * (s.Count / have) / PiecesPerBeast(s.Species);
            int whole = Mathf.FloorToInt(beasts) + (GD.Randf() < beasts - Mathf.Floor(beasts) ? 1 : 0);
            if (whole > 0) s.Take(whole);
        }
    }

    // A week of trapping by `trappers`, each with a line or two (more with
    // steel traps), off the beaver lodges of the household's own country:
    // no lodge near, no beaver.
    public static int Pelts(Household h, int trappers, float traps)
    {
        var eco = Sim.Ecology;
        if (eco == null) return 0;
        var at = IsoGrid.ToWorld(h.AccessCell);
        SpawnSite lodge = null;
        foreach (var s in eco.Sites)
        {
            if (s.Species != Species.Beaver || s.IsDead || s.Count < 1f) continue;
            if (IsoGrid.ToWorld(s.Cell).DistanceTo(at) > WorldLayout.HomeCountryPx) continue;
            if (lodge == null || s.Count > lodge.Count) lodge = s;
        }
        if (lodge == null) return 0;
        float stock = Mathf.Clamp(lodge.Count / Mathf.Max(1f, lodge.Capacity), 0.1f, 1f);
        int pelts = 0;
        for (int i = 0; i < trappers; i++) pelts += (GD.Randf() < 0.35f * stock ? 1 : 0) + (GD.Randf() < traps * 0.5f * stock ? 1 : 0);
        pelts = Mathf.Min(pelts, Mathf.FloorToInt(lodge.Count));
        lodge.Take(pelts);
        return pelts;
    }
}
