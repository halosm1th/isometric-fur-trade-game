using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.Items;

namespace IsometricFurTradeGame.Scripts.Agents;

public enum AmbitionKind { None, RiseAtThePost, MarryIn, PrivateTrade, DriveOffTheRival, KeepTheBandWhole, FoundAPost }

// What the player is doing with the year, as against what the year is
// doing to them (design doc P.2). An ambition is an ordinary Tier 1 goal -
// the same object an ambitious clerk carries - except the player chose it
// and the journal keeps it in the open. They pull against orders on
// purpose: the quota wants the lodge trapped out, the marriage wants the
// band's country spared.
public class Ambition
{
    public AmbitionKind Kind;
    public int DeclaredDay;
    public int DeclaredYear;
    public bool Abandoned;
    public float Best;            // high-water mark, so a setback still reads as progress made

    public string Title => Kind switch
    {
        AmbitionKind.RiseAtThePost => "Rise at the post",
        AmbitionKind.MarryIn => "Marry into a family of the country",
        AmbitionKind.PrivateTrade => "Grow rich on private trade",
        AmbitionKind.DriveOffTheRival => "Drive the other company off this river",
        AmbitionKind.KeepTheBandWhole => "Keep the band fed and whole",
        AmbitionKind.FoundAPost => "Found a post of your own",
        _ => "",
    };

    public string Pledge => Kind switch
    {
        AmbitionKind.RiseAtThePost => "to be master of a post before the Company is done with me",
        AmbitionKind.MarryIn => "to marry in the custom of the country and be of two peoples",
        AmbitionKind.PrivateTrade => "to come out of this country with something of my own",
        AmbitionKind.DriveOffTheRival => "to see the Canadians off this river",
        AmbitionKind.KeepTheBandWhole => "that the band shall not starve for the Company's price",
        AmbitionKind.FoundAPost => "to set up a post of my own inland",
        _ => "",
    };

    // 0..1 against what would count as having done it.
    public float Progress(Player.PlayerController p)
    {
        float v = Kind switch
        {
            AmbitionKind.RiseAtThePost => p.Rank == "postmaster" ? 1f : p.Rank == "clerk" ? 0.6f : Mathf.Clamp(p.Standing / 60f, 0f, 0.55f),
            AmbitionKind.MarryIn => p.Spouse != null ? 1f : Mathf.Clamp(BestCourtship(p), 0f, 0.9f),
            AmbitionKind.PrivateTrade => Mathf.Clamp(p.PrivateWorthMb / 120f, 0f, 1f),
            AmbitionKind.DriveOffTheRival => Sim.Nwc == null ? 1f : Mathf.Clamp(1f - Sim.Nwc.Stores.Count(ItemId.TradeGoods) / 90f, 0f, 1f) * 0.6f + Mathf.Clamp(Rivalry.Tension / 100f, 0f, 1f) * 0.4f,
            AmbitionKind.KeepTheBandWhole => BandHealth(),
            AmbitionKind.FoundAPost => 0f,   // not yet buildable; the doc's 13.8 work
            _ => 0f,
        };
        Best = Mathf.Max(Best, v);
        return v;
    }

    // How far along the courtship is: the father's regard and the gifts given.
    private static float BestCourtship(Player.PlayerController p)
    {
        float best = 0f;
        foreach (var n in Sim.Npcs)
        {
            if (n.IsDead || n.Children.Count == 0) continue;
            bool hasDaughter = false;
            foreach (var c in n.Children) if (c.Role == Role.CreeDaughter && c.Spouse == null && !c.IsDead) hasDaughter = true;
            if (!hasDaughter) continue;
            float r = Mathf.Clamp(n.RapportWith(p) / 30f, 0f, 1f) * 0.5f + Mathf.Clamp(n.GiftsFromPlayerMb / 8f, 0f, 1f) * 0.5f;
            best = Mathf.Max(best, r);
        }
        return best;
    }

    private static float BandHealth()
    {
        var band = Sim.Band;
        if (band == null) return 0f;
        int mouths = band.Mouths;
        float food = Mathf.Clamp(band.FoodDays(mouths) / 30f, 0f, 1f);
        float standing = Mathf.Clamp(band.StandingWithCompany / 100f, 0f, 1f);
        int alive = band.Alive();
        float whole = Mathf.Clamp(alive / 11f, 0f, 1f);
        return food * 0.4f + standing * 0.3f + whole * 0.3f;
    }

    // What it looks like in the journal: a line the player can act on.
    public string Line(Player.PlayerController p)
    {
        float v = Progress(p);
        string state = Abandoned ? "abandoned" : v >= 1f ? "done" : $"{v * 100f:0}%";
        string next = Kind switch
        {
            AmbitionKind.RiseAtThePost => p.Rank == "postmaster" ? "you have a post" : p.Rank == "clerk" ? "a post wants a master; be the clerk the Factor names" : $"standing {p.Standing:0} - a clerk's place comes at 35",
            AmbitionKind.MarryIn => p.Spouse != null ? $"married to {p.Spouse.Describe()}" : "gifts to her father, and keep coming back",
            AmbitionKind.PrivateTrade => $"{p.PrivateWorthMb:0} MB of your own, counting what you carry and what you've cached",
            AmbitionKind.DriveOffTheRival => Sim.Nwc != null ? $"their shelves hold {Sim.Nwc.Stores.Count(ItemId.TradeGoods)} pieces; the rivalry stands at {Rivalry.Tension:0}" : "they are gone",
            AmbitionKind.KeepTheBandWhole => Sim.Band != null ? $"the band has {Sim.Band.FoodDays(Sim.Band.Mouths):0} days of food, standing {Sim.Band.StandingWithCompany:0}" : "",
            AmbitionKind.FoundAPost => "not yet: you would need the Company's leave and men",
            _ => "",
        };
        return $"{Title} - {state}. {next}";
    }
}

public static class Ambitions
{
    public static readonly AmbitionKind[] Offered =
    {
        AmbitionKind.RiseAtThePost, AmbitionKind.MarryIn, AmbitionKind.PrivateTrade,
        AmbitionKind.DriveOffTheRival, AmbitionKind.KeepTheBandWhole,
    };

    public static string Blurb(AmbitionKind k) => k switch
    {
        AmbitionKind.RiseAtThePost => "Clerk, then master of a post. Your rivals are running the same race.",
        AmbitionKind.MarryIn => "A wife of the country, and her people's quarrels for your own.",
        AmbitionKind.PrivateTrade => "Furs and goods of your own, however you come by them. The books have to balance.",
        AmbitionKind.DriveOffTheRival => "The Nor'Westers off this river. Escalation nobody fully controls.",
        AmbitionKind.KeepTheBandWhole => "The band fed and whole - and London's quota pulls the other way.",
        _ => "",
    };
}
