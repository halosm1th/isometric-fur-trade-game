using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Economy;

namespace IsometricFurTradeGame.Scripts.UI;

// Agreements, talked over (Economy/Accords): what this chief's people are
// bound to, and terms of your own put on your company's behalf - peace, the
// camp's furs to your company alone, an alliance - and restitution for blood
// they hold against you. (A truce between the houses is put to the other
// master in "The companies...".) He puts it to the council: each man there
// weighs it by what his people think of your company, what he thinks of you,
// and his own temper; the Factor hears whose word made it.
public partial class Conversation
{
    // A chief gives his camp's word.
    private static BandLedger AccordHouse(Npc npc) => npc.HomeStores is BandLedger bl && npc.Role == Role.Chief ? bl : null;

    private bool CanTreat(Npc npc) => AccordHouse(npc) != null && _player.HomePost != null && _player.Faction is Faction.HBC or Faction.NWC;

    private void ShowAccords(Npc npc, string said = null)
    {
        var b = AccordHouse(npc);
        var mine = _player.HomePost;
        if (b == null || mine == null) { ShowTopics(Greeting(npc)); return; }
        var lines = new System.Text.StringBuilder();
        if (said != null) lines.Append(said).Append("\n\n");
        var bound = new List<Accord>(Accords.Of(b));
        string nm = Accords.Name(b); nm = char.ToUpper(nm[0]) + nm[1..];
        if (bound.Count == 0) lines.Append($"{nm} is bound by no accord.");
        else { lines.Append($"What {Accords.Name(b)} has given its word to:"); foreach (var ac in bound) lines.Append("\n  - ").Append(Accords.Describe(ac)); }
        _text = lines.ToString();
        ClearOptions();
        if (!Accords.AtPeace(b, mine) && (Plains.Feud(b, mine) > 0.5f || Plains.Feud(mine, b) > 0.5f || b.StandingWith(mine.Company) < 40f))
            AddOption($"Offer peace between your people and {Accords.Name(mine)}", () => Propose(npc, b, mine, new[] { Term.Peace }, 365, 0f));
        if (Accords.ExclusiveOf(b) == null)
            AddOption($"Ask them to bring their furs to {CompanyName(mine.Company)} alone, for better pay", () => Propose(npc, b, mine, new[] { Term.Exclusive }, 730, 0.15f));
        if (Accords.Between(b, mine, Term.Alliance) == null)
            AddOption("Offer an alliance: each to stand by the other", () => Propose(npc, b, mine, new[] { Term.Alliance, Term.Peace }, 730, 0f));
        var mineOwed = new List<Agents.Grudge>();
        float owed = Accords.Owed(b, mine, mineOwed, _player.DisplayName);
        if (owed >= 1f)
            AddOption($"Offer restitution for the blood they hold against you ({Mathf.CeilToInt(owed)} trade goods)", () => Restitution(npc, b, mine, owed, mineOwed));
        AddOption("Back", () => ShowTopics(Greeting(npc)));
    }

    private static string CompanyName(Faction f) => f == Faction.NWC ? "the North West Company" : f == Faction.American ? "Mr. Astor's company" : "the Company";

    private void Propose(Npc npc, BandLedger b, FortLedger mine, Term[] terms, int days, float bonus)
    {
        // The council sits on the hardest of the terms.
        var term = System.Array.IndexOf(terms, Term.Alliance) >= 0 ? Term.Alliance : terms[0];
        var vote = Accords.Council(b, term, mine, _player);
        string tally = $"{vote.For:0} of {vote.All:0} voices";
        if (!vote.Yes)
        {
            npc.ChangeRapport(_player, -1f);
            ShowAccords(npc, b.StandingWith(mine.Company) < 40f
                ? $"The council sits, and the answer is no: {tally} for it. \"Your people have not dealt fairly with us. Words are cheap.\""
                : $"The council sits a long while over it. {tally} for it, and it wants two in three. \"Not yet,\" he tells you.");
            return;
        }
        var ac = Accords.Make(b, mine, terms, days, $"{_player.DisplayName} and {npc.DisplayName}", bonus);
        _player.Standing += System.Array.IndexOf(terms, Term.Exclusive) >= 0 ? 3f : 2f;
        npc.ChangeRapport(_player, 4f);
        _player.Journal.Add($"{Core.GameCalendar.Instance?.DateString}: You made an accord with {npc.DisplayName}: {Accords.Describe(ac)}.");
        ShowAccords(npc, $"The council sits, and {tally} are for it. \"It is agreed.\" He gives you his hand on it. Word will go round that it was your doing, and so will the blame, if it is broken.");
    }

    // Goods for the blood they hold against you (design doc 7.4): if the council takes them, it is laid down.
    private void Restitution(Npc npc, BandLedger b, FortLedger mine, float owed, List<Agents.Grudge> debts)
    {
        int goods = Mathf.CeilToInt(owed);
        if (_player.Inventory.Count(Items.ItemId.TradeGoods) < goods)
        {
            ShowAccords(npc, $"You would need {goods} trade goods in your pack to make it right. You have {_player.Inventory.Count(Items.ItemId.TradeGoods)}.");
            return;
        }
        var vote = Accords.Council(b, Term.Settlement, mine, _player);
        if (!vote.Yes)
        {
            npc.ChangeRapport(_player, -2f);
            ShowAccords(npc, $"The council hears you out. {vote.For:0} of {vote.All:0} voices would take the goods; the rest want something else from you. \"Not goods. Not yet.\"");
            return;
        }
        var ac = Accords.Settle(b, mine, _player.Inventory, owed, debts, $"{_player.DisplayName} and {npc.DisplayName}");
        npc.ChangeRapport(_player, 6f);
        _player.Journal.Add($"{Core.GameCalendar.Instance?.DateString}: You paid {goods} trade goods to {Accords.Name(b)} for the blood between you, and it is laid down.");
        ShowAccords(npc, "The goods are laid out before the lodge and counted. The dead man's kin take them, and the eldest of them gives you his hand. It is finished.");
    }
}
