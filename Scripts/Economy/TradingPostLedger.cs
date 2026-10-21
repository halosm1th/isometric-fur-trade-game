using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Economy;

// The books of one of the wider country's posts (World/Posts.cs). Same
// ledger as York Factory and Oxford House, but its country is not drawn:
// the nets, the post's own hunters and the bands that trade there come and
// go as a weekly sum (design doc 18.2's background tier), so a master and
// two men a thousand miles off neither starve nor walk to York for meat.
public partial class TradingPostLedger : FortLedger
{
    public TradingPost Post;
    private float _hoursToWeek = 24f * 7f;
    // What its country last came in wanting and did not find (Economy/StandardOfTrade).
    public System.Collections.Generic.List<string> ShortOf = new();

    public override Agents.Faction Company => Post?.Faction ?? Agents.Faction.HBC;

    public override void _Ready()
    {
        Title = $"{Post.Name} stores";
        AccessCell = Post.AccessCell;
        WoodpileCell = Post.WoodpileCell;
        HearthCells.Add(Post.HearthCell);
        PeltPrice = 1.0f;
        QuotaMb = 25f;
        Year = GameCalendar.Instance?.Year ?? 1811;
        Stores.Add(ItemId.Pemmican, 25);
        Stores.Add(ItemId.DriedMeat, 20);
        Stores.Add(ItemId.Firewood, 25);
        // The goods of last year's outfit, part traded (Economy/Outfits).
        Outfits.Seed(this);
        Stores.Add(ItemId.Water, 10);
        Stores.Add(ItemId.Axe, 1);
        Stores.Add(ItemId.FishingLine, 2);
        Stores.Add(ItemId.BeaverPelt, 4);
        KnowsTheCountry = true;
        Horses.Seed(this);
        DirectiveText = "Hold the post, keep the Indians in your interest, and send the returns out with the brigade.";
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        var cal = GameCalendar.Instance;
        if (cal == null || !Net.Net.IsServer) return;
        _hoursToWeek -= cal.DeltaGameHours;
        if (_hoursToWeek > 0f) return;
        _hoursToWeek += 24f * 7f;

        int mouths = 0;
        foreach (var n in Sim.Npcs) if (n.HomeStores == this && !n.IsDead) mouths++;
        if (mouths == 0) return;
        // A week of nets and snares, and what the post's hunters bring in,
        // dried and put by (fresh it would spoil on the books before anyone
        // ate it): less in the hard months, made up by what the bands sell.
        bool winter = cal.IsFrozen;
        Stores.Add(ItemId.DriedMeat, mouths * (winter ? 5 : 7));
        if (winter) Stores.Add(ItemId.Pemmican, mouths);
        if (Stores.Count(ItemId.Firewood) < 20) Stores.Add(ItemId.Firewood, 6);
        // The trade itself is the country's camps coming in with their furs
        // (Economy/StandardOfTrade); the goods come up, and the furs go down,
        // with the post's own feeder brigade (Economy/Outfits, World/Convoys).
    }
}
