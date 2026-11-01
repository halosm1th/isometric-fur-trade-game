using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Economy;

namespace IsometricFurTradeGame.Scripts.UI;

// Agreements, talked over (Economy/Accords): what this chief's people are
// bound to, and terms of your own put on your company's behalf - peace, the
// camp's furs to your company alone, an alliance. (A truce between the
// houses is put to the other master in "The companies...".) He weighs it by
// what his people think of your company, what they think of you, and his
// own temper; the Factor hears whose word made it.
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
        AddOption("Back", () => ShowTopics(Greeting(npc)));
    }

    private static string CompanyName(Faction f) => f == Faction.NWC ? "the North West Company" : f == Faction.American ? "Mr. Astor's company" : "the Company";

    private void Propose(Npc npc, BandLedger b, FortLedger mine, Term[] terms, int days, float bonus)
    {
        float standing = b.StandingWith(mine.Company);
        float chance = 0.15f + (standing - 40f) / 80f + npc.RapportWith(_player) / 200f + _player.Standing / 100f;
        if (System.Array.IndexOf(terms, Term.Exclusive) >= 0) chance -= 0.15f;   // it shuts a door
        if (System.Array.IndexOf(terms, Term.Alliance) >= 0) chance -= standing < 65f ? 0.4f : 0.1f;
        if (System.Array.IndexOf(terms, Term.Peace) >= 0 && terms.Length == 1) chance += Plains.Feud(b, mine) > 3f ? -0.2f : 0.2f;
        string no = standing < 40f ? "\"Your people have not dealt fairly with us. Words are cheap.\"" : "He smokes a long while. \"The council would not have it. Not yet.\"";
        chance = Mathf.Clamp(chance, 0.05f, 0.9f);
        if (GD.Randf() > chance)
        {
            npc.ChangeRapport(_player, -1f);
            ShowAccords(npc, no);
            return;
        }
        var ac = Accords.Make(b, mine, terms, days, $"{_player.DisplayName} and {npc.DisplayName}", bonus);
        _player.Standing += System.Array.IndexOf(terms, Term.Exclusive) >= 0 ? 3f : 2f;
        npc.ChangeRapport(_player, 4f);
        _player.Journal.Add($"{Core.GameCalendar.Instance?.DateString}: You made an accord with {npc.DisplayName}: {Accords.Describe(ac)}.");
        ShowAccords(npc, "\"It is agreed.\" He gives you his hand on it. Word will go round that it was your doing, and so will the blame, if it is broken.");
    }
}
