using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Economy;

// A band or a homeguard family of the wider country (World/Camps.cs). It is
// a BandLedger so its people behave as band people do - the chief, the
// hunters, the women at the camp - but it stands outside the York
// country's books: not in Sim.Bands, not courted by York's counter, its
// hunt and its trade resolved weekly in numbers (design doc 18.2).
public partial class CountryBand : BandLedger
{
    public CountryCamp Country;
    private float _hoursToWeek = 24f * 7f;

    public override void _Ready()
    {
        Title = Country.IsFamily ? $"{Country.Name}'s cache" : $"the {Country.Name} cache";
        AccessCell = IsoGrid.Cell(Camp, 1, 1);
        WoodpileCell = IsoGrid.Cell(Camp, 2, -2);
        HearthCells.Add(Camp);
        // They trade at the posts of their own country, not at York: the
        // York counters' reckonings never reach them.
        StandingWithCompany = 0f; StandingWithNwc = 0f;
        // Nor do they know the York hunting grounds: their own country's game
        // is in the weekly sum below.
        KnowsTheCountry = false;
        Stores.Add(ItemId.DriedMeat, 30);
        Stores.Add(ItemId.Pemmican, 10);
        Stores.Add(ItemId.Firewood, 20);
        Stores.Add(ItemId.Water, 10);
        Stores.Add(ItemId.BeaverPelt, Country.IsFamily ? 2 : 8);
        Horses.Seed(this);
    }

    public override void _Process(double delta)
    {
        var cal = GameCalendar.Instance;
        if (cal == null || !Net.Net.IsServer) return;
        _hoursToWeek -= cal.DeltaGameHours;
        if (_hoursToWeek > 0f) return;
        _hoursToWeek += 24f * 7f;
        int mouths = Mouths;
        if (mouths == 0) return;
        // A week of the hunt, the fishery and the snares, dried and put by:
        // lean in late winter, fat in the autumn.
        int lean = cal.Month is 2 or 3 or 4 ? 4 : cal.Month is 9 or 10 ? 8 : 6;
        Stores.Add(ItemId.DriedMeat, mouths * lean);
        // Part of the week's take is eaten fresh off the hunt and the nets.
        // Without it the camp has nothing but dried meat and the whole country
        // dies of the scurvy over a winter (design doc 13.5).
        Stores.Add(ItemId.Venison, mouths * 2);
        Stores.Add(ItemId.Fish, mouths);
        // The women keep the woodpile (or the chip pile, on the plains) up to
        // a week and more of the lodges' fires: the daily reckoning burns a
        // piece a person a day, and a camp that ran short froze in November.
        int wood = mouths * 10;
        if (Stores.Count(ItemId.Firewood) < wood) Stores.Add(ItemId.Firewood, wood - Stores.Count(ItemId.Firewood));
        if (cal.Month is >= 10 or <= 4)
        {
            // Steel traps take more beaver than deadfalls, and wear for it (Economy/Wants).
            // A week's trapping by the camp's trappers - about one in six of
            // its people - each with a deadfall line or two and, with steel
            // traps, more.
            int trappers = Mathf.Max(1, mouths / 6);
            float traps = Mathf.Clamp(Stores.Count(ItemId.SteelTrap) / (float)Mathf.Max(1, trappers * 4), 0f, 1f);
            int pelts = 0;
            for (int i = 0; i < trappers; i++) pelts += (GD.Randf() < 0.35f ? 1 : 0) + (GD.Randf() < traps * 0.5f ? 1 : 0);
            Stores.Add(ItemId.BeaverPelt, pelts);
            Wants.OnTrapping(this, pelts);
        }
        // The week's hunting for the pot (Economy/Wants): a few beasts, the hides to dress.
        Wants.OnHunt(this, Mathf.Max(1, mouths / 6), Mathf.Max(1, mouths / 8));
    }
}
