using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;

namespace IsometricFurTradeGame.Scripts.Economy;

// Horses changing hands (design doc 12.3): bought at a camp for goods or for
// a gun - the trade that put guns into one nation's hands against another's
// (design doc 7.2) - given to a father at a marriage, lent from a post's
// horse guard to a servant of the house. The conversation offers these; the
// reckoning is here.
public static class HorseTrade
{
    public static bool CampHasHorses(Npc npc) => npc.IsBandPerson && npc.HomeStores is BandLedger b && b.Herd.Free >= 2 && (npc.Role is Role.CreeChief or Role.CreeHunter);

    public static bool PostLends(Npc npc, Player.PlayerController pc)
        => npc.HomeStores is FortLedger f && f.Herd.Free >= 2 && npc.Faction == pc.Faction && npc.Role is Role.Factor or Role.Postmaster or Role.NwcPartner;

    // A horse's price in trade goods, by how well he knows you.
    public static int Price(Npc npc, Player.PlayerController pc)
    {
        float r = npc.RapportWith(pc);
        float f = r >= 60f ? 0.8f : r >= 30f ? 1f : r >= 0f ? 1.3f : 1.8f;
        // A herd thinned by a hard winter or a raid: horses are dear.
        if (npc.HomeStores is { } h && h.Herd.Horses < 2 * Mathf.Max(1, (h as BandLedger)?.Mouths ?? 1) / 3) f *= 1.3f;
        return Mathf.RoundToInt(Herd.ValueMb * f);
    }

    // The things the player can do about horses with this man, as (label, action).
    public static List<(string Label, System.Func<string> Act)> Options(Npc npc, Player.PlayerController pc)
    {
        var list = new List<(string, System.Func<string>)>();
        var home = npc.HomeStores;
        if (home == null) return list;
        if (CampHasHorses(npc))
        {
            if (!pc.HasHorse)
            {
                int price = Price(npc, pc);
                list.Add(($"Trade for a horse ({price} trade goods)", () => Buy(npc, pc, price)));
                if (pc.Inventory.Has(ItemId.Musket)) list.Add(("Trade your musket for a horse", () => BuyWithGun(npc, pc)));
            }
            else if (pc.HorseLentBy == "")
            {
                list.Add(($"Give {pc.HorseName} to {npc.DisplayName} (a gift)", () => Give(npc, pc)));
                list.Add(($"Sell {pc.HorseName} ({Mathf.RoundToInt(Price(npc, pc) * 0.6f)} trade goods)", () => Sell(npc, pc)));
            }
        }
        if (PostLends(npc, pc) && !pc.HasHorse) list.Add(("Ask for a horse from the horse guard", () => Lend(npc, pc)));
        if (pc.HasHorse && pc.HorseLentBy != "" && home is FortLedger post && TitleOfPost(post) == pc.HorseLentBy) list.Add(($"Give {pc.HorseName} back to the horse guard", () => GiveBack(npc, pc)));
        return list;
    }

    private static string TitleOfPost(FortLedger f) => f is TradingPostLedger tp ? tp.Post.Name : f.Title;

    private static string Buy(Npc npc, Player.PlayerController pc, int price)
    {
        var herd = npc.HomeStores.Herd;
        if (herd.Free < 2) return "\"We have no horses to spare.\"";
        if (pc.Inventory.Count(ItemId.TradeGoods) < price) return $"\"{price} in goods.\" You have {pc.Inventory.Count(ItemId.TradeGoods)}.";
        pc.Inventory.Remove(ItemId.TradeGoods, price);
        npc.HomeStores.Stores.Add(ItemId.TradeGoods, price);
        Hand(npc, pc, herd);
        return $"{npc.DisplayName} has a boy bring in {pc.HorseName} from the herd, and takes your goods. The horse is yours. ({Core.Keybinds.KeyFor("mount")} to get up.)";
    }

    private static string BuyWithGun(Npc npc, Player.PlayerController pc)
    {
        var herd = npc.HomeStores.Herd;
        if (herd.Free < 2) return "\"We have no horses to spare.\"";
        pc.Inventory.Remove(ItemId.Musket);
        npc.Inventory.Add(ItemId.Musket);
        npc.ChangeRapport(pc, 6f);
        Hand(npc, pc, herd);
        Chronicle.Event($"{pc.DisplayName} traded a gun to {npc.DisplayName} for a horse.", pc.GlobalPosition, pc, npc.DisplayName, 1f);
        return $"{npc.DisplayName} turns the gun over in his hands for a long while, then sends for a horse. A gun for a horse: there is not a man on the plains who would call that a bad trade, and the men he rides against will learn of it.";
    }

    private static void Hand(Npc npc, Player.PlayerController pc, Herd herd)
    {
        herd.Lose(1);
        pc.GiveHorse(Plains.HorseNames[GD.RandRange(0, Plains.HorseNames.Length - 1)], Horses.CoatFor(npc.DisplayName + pc.DisplayName), herd.Condition);
    }

    // A horse given is a horse remembered: a father counts what a suitor has
    // brought him (Conversation.Propose reads GiftsFromPlayerMb).
    private static string Give(Npc npc, Player.PlayerController pc)
    {
        string name = pc.HorseName;
        pc.LoseHorse();
        npc.HomeStores.Herd.Horses++;
        npc.GiftsFromPlayerMb += Herd.ValueMb;
        npc.ChangeRapport(pc, 15f);
        Chronicle.Event($"{pc.DisplayName} gave {npc.DisplayName} a horse.", pc.GlobalPosition, pc, npc.DisplayName, 1f);
        return $"{npc.DisplayName} walks round {name} twice and looks in its mouth. He says nothing, which is thanks. A man who gives horses is a man people speak of.";
    }

    private static string Sell(Npc npc, Player.PlayerController pc)
    {
        int pay = Mathf.RoundToInt(Price(npc, pc) * 0.6f);
        if (npc.HomeStores.Stores.Count(ItemId.TradeGoods) < pay) return "\"We have nothing to give you for it.\"";
        string name = pc.HorseName;
        pc.LoseHorse();
        npc.HomeStores.Herd.Horses++;
        npc.HomeStores.Stores.Remove(ItemId.TradeGoods, pay);
        pc.Inventory.Add(ItemId.TradeGoods, pay);
        return $"{name} goes into {npc.DisplayName}'s herd. You get {pay} in goods for it.";
    }

    private static string Lend(Npc npc, Player.PlayerController pc)
    {
        if (pc.Standing < 15f) return "\"The horses are for the hunters and the express. When you have shown you can be trusted with one, ask again.\"";
        var f = (FortLedger)npc.HomeStores;
        f.Herd.Lose(1);
        pc.GiveHorse(Plains.HorseNames[GD.RandRange(0, Plains.HorseNames.Length - 1)], Horses.CoatFor(f.Title + pc.DisplayName), f.Herd.Condition, TitleOfPost(f));
        return $"\"Take {pc.HorseName}. It is the house's horse, mind - bring it back, or account for it.\"";
    }

    private static string GiveBack(Npc npc, Player.PlayerController pc)
    {
        var f = (FortLedger)npc.HomeStores;
        string name = pc.HorseName;
        bool thin = pc.HorseCondition < 30f;
        pc.LoseHorse();
        f.Herd.Horses++;
        if (thin) { pc.Standing = Mathf.Max(0f, pc.Standing - 2f); return $"{npc.DisplayName} looks at {name}'s ribs. \"You have ridden this horse half to death.\""; }
        return $"{name} goes back to the horse guard.";
    }

    // A lent horse not brought back is a theft from the house.
    public static void OnLentHorseLost(Player.PlayerController pc)
    {
        if (pc.HorseLentBy == "") return;
        pc.Standing = Mathf.Max(0f, pc.Standing - 4f);
        pc.Say($"That was {pc.HorseLentBy}'s horse. It will be charged against you.");
    }
}
