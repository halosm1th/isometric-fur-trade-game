using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.Player;
using NetHub = IsometricFurTradeGame.Scripts.Net.Net;

namespace IsometricFurTradeGame.Scripts.Core;

// The end of the story (design doc 1, 4.3): when the companies are made one
// and the new order has weighed its men (Economy/Union), each player is told
// what he came to - the verdict, what he made of himself against what he
// meant to, the people bound to him, his last year as it will be told, and
// what the years after 1821 hold: Simpson's economies, the country wives
// set aside for European ones, the Southern Department still under the old
// order. Nothing here is invented; it is his own record, closed. He may
// then play on in the world as it is, or leave it (UI/EndScreen).
//
// Server; the screen itself is the player's own machine's. Dev: `--union`
// brings the union (and this) on the first morning; `--endtrace` prints it.
public static class Epilogue
{
    private static readonly bool Trace = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--endtrace") >= 0;

    public static void Show(PlayerController p, Union.Verdict? verdict, bool wasNorWester)
    {
        if (p == null || !GodotObject.IsInstanceValid(p)) return;
        var d = Compose(p, verdict, wasNorWester);
        if (Trace || DisplayServer.GetName() == "headless")
        {
            GD.Print($"[end] {d["title"]} - {p.DisplayName}");
            foreach (var v in d["sections"].AsGodotArray()) { var s = v.AsGodotDictionary(); GD.Print($"[end] == {s["heading"]}\n[end] {s["text"]}"); }
        }
        NetHub.I?.ShowEnd(p, d);
    }

    private static Godot.Collections.Dictionary Section(string heading, string text) => new() { ["heading"] = heading, ["text"] = text };

    public static Godot.Collections.Dictionary Compose(PlayerController p, Union.Verdict? verdict, bool wasNorWester)
    {
        var cal = GameCalendar.Instance;
        int year = cal?.Year ?? 1821;
        var sections = new Godot.Collections.Array();
        sections.Add(Section(verdict != null ? $"The reckoning of {year}" : "The companies made one", VerdictText(p, verdict, wasNorWester, year)));
        sections.Add(Section("What you made of yourself", Career(p, year)));
        string people = People(p);
        if (people != "") sections.Add(Section("Your people", people));
        if (!string.IsNullOrEmpty(p.LastYearStory)) sections.Add(Section("The last year, as it will be told", p.LastYearStory));
        sections.Add(Section("Afterwards", Afterwards(p, verdict, wasNorWester, year)));
        return new Godot.Collections.Dictionary
        {
            ["title"] = $"{p.DisplayName}, {year}",
            ["subtitle"] = verdict switch
            {
                Union.Verdict.GivenCharge => "Kept on, and given charge",
                Union.Verdict.KeptOn => "Kept on",
                Union.Verdict.CutLoose => "Let go",
                _ => "One company in the country",
            },
            ["sections"] = sections,
        };
    }

    private static string VerdictText(PlayerController p, Union.Verdict? v, bool wasNorWester, int year)
    {
        string nw = wasNorWester ? "The North West Company is gone into the Hudson's Bay Company, and a Nor'Wester starts behind: the Company keeps its own first. " : "The North West Company is gone into the Hudson's Bay Company. ";
        string early = year < 1821 ? $"It came in {year}, not 1821: the rivalry ruined them both, and the creditors would not wait. " : "";
        return v switch
        {
            Union.Verdict.GivenCharge => early + nw + "There were twice as many men as places, and the new Company weighed every one. You are kept on and given charge: a chief trader's place, and men under you - fewer than there were, and you will choose which of them go.",
            Union.Verdict.KeptOn => early + nw + "There were twice as many men as places, and the new Company weighed every one. You are kept on, as you were. Not every man was: you will see them go down the river with their pay and their families, and you will not be sorry it was not you.",
            Union.Verdict.CutLoose => early + nw + "There were twice as many men as places, and the new Company weighed every one. You are not one it keeps. You are off the books: a freeman now, in a country with one buyer for what you trap.",
            _ => early + nw + "The two houses your people played against each other for twenty years are one house now, and it pays a fifth less for furs. "
                + (p.IsBandPerson ? "What the old men said would happen has happened: there is no other trader to go to." : "You were never on either company's books, and the new one does not need you."),
        };
    }

    private static int ArrivedYear(PlayerController p, int now)
    {
        foreach (string e in p.Journal)
        {
            int colon = e.IndexOf(": ");
            if (colon <= 0) continue;
            var bits = e[..colon].Split(' ');
            if (bits.Length >= 3 && int.TryParse(bits[^1], out int y) && y > 1700 && y <= now) return y;
        }
        return now;
    }

    private static string Career(PlayerController p, int year)
    {
        int arrived = ArrivedYear(p, year);
        int years = Mathf.Max(0, year - arrived);
        var bits = new List<string>();
        bits.Add(years == 0 ? "You came into the country this year." : $"{years} year{(years == 1 ? "" : "s")} in the country, since {arrived}.");
        if (p.StartPath is not (StartPath.Cree or StartPath.Assiniboine))   // a man on someone's books, then or now
        {
            string standing = p.Standing >= 60f ? "high" : p.Standing >= 30f ? "good" : p.Standing >= 10f ? "middling" : "low";
            bits.Add($"You end as {Article(p.Rank)} {p.Rank}, your standing {standing} ({p.Standing:0}). {p.TasksDone} orders done, {p.TasksRefused} refused.");
        }
        foreach (var a in p.Ambitions)
        {
            string said = $"\"{char.ToUpper(a.Pledge[0])}{a.Pledge[1..]},\" you said";
            if (a.Abandoned) { bits.Add($"{said}, and gave it up."); continue; }
            string how = a.Best >= 1f ? "and you did it" : a.Best >= 0.6f ? "and you came close" : a.Best >= 0.25f ? "and you got some way towards it" : "and it came to little";
            bits.Add($"{said}, {how}.");
        }
        if (p.Deserter) bits.Add("You deserted, and it is in the book.");
        return string.Join(" ", bits);
    }

    private static string Article(string w) => w.Length > 0 && "aeiou".Contains(char.ToLowerInvariant(w[0])) ? "an" : "a";

    private static string People(PlayerController p)
    {
        var bits = new List<string>();
        if (p.Spouse != null && GodotObject.IsInstanceValid(p.Spouse))
            bits.Add(p.Spouse.IsDead ? $"Your wife, {p.Spouse.DisplayName}, is dead." : $"Your wife is {p.Spouse.DisplayName}.");
        foreach (var w in p.OtherWives) if (GodotObject.IsInstanceValid(w) && !w.IsDead) bits.Add($"{w.DisplayName} is your wife too, by the custom of the country.");
        int kids = 0;
        foreach (var n in Sim.Npcs) if (!n.IsDead && n.FatherName == p.DisplayName) kids++;
        if (kids > 0) bits.Add($"{kids} child{(kids == 1 ? "" : "ren")} of yours living.");
        var friends = new List<string>();
        var foes = new List<string>();
        foreach (var n in Sim.Npcs)
        {
            if (n.IsDead || !GodotObject.IsInstanceValid(n)) continue;
            if (friends.Count < 4 && Friends.IsFriend(n, p)) friends.Add(n.DisplayName);
            else if (foes.Count < 3 && n.RapportWith(p) <= -40f) foes.Add(n.DisplayName);
        }
        if (friends.Count > 0) bits.Add($"Friends who would stand by you: {string.Join(", ", friends)}.");
        if (Rivals.Current?.Him is { IsDead: false } r) bits.Add($"{r.DisplayName}, who wanted {Rivals.Current.Seat}, is still in the country.");
        if (foes.Count > 0) bits.Add($"And men who would see you ruined: {string.Join(", ", foes)}.");
        return string.Join(" ", bits);
    }

    private static string Afterwards(PlayerController p, Union.Verdict? v, bool wasNorWester, int year)
    {
        var paras = new List<string>();
        paras.Add($"{Union.Closed.Count} posts were given up at the union and half their men let go. In the years after, the new governor of the Northern Department, George Simpson, cut deeper: fewer posts, fewer men, smaller brigades, the York boat in place of the canoe, the price of a beaver set once for the whole country. The rivalry that kept the posts open and the prices high was over.");
        paras.Add(v switch
        {
            Union.Verdict.GivenCharge => "You are one of his officers now. He will ask you for economies and expect them, and the men you let go will remember who read out their names.",
            Union.Verdict.KeptOn => "You are one of his servants. The wages are cut, the work is the same, and there is no other house across the river to threaten to go to.",
            Union.Verdict.CutLoose => "Many of the men let go stayed in the country rather than go home: to Red River, to the Métis hunt, to a wife's people. A freeman traps and hunts for himself, and sells to the only buyer there is, at the only price there is.",
            _ => p.IsBandPerson ? "A camp that had played one house against the other had no one left to play. The posts near the old rival houses closed, the gifts grew smaller, and the debt each fall was the Company's to give or refuse." : "The country has one master in trade now, and everyone in it reckons with that.",
        });
        bool countryFamily = p.Spouse is { } s && GodotObject.IsInstanceValid(s) && s.IsBandPerson || p.OtherWives.Count > 0;
        if (countryFamily)
            paras.Add("Simpson took country wives of his own, Elizabeth Sinclair and then Margaret Taylor, and then in 1830 went home to marry his English cousin and brought her out to Red River. The officers followed the fashion: European wives, and the country families set aside or provided for at a distance. What you do with yours is your own choice. The Company's fashion is turning against them.");
        if (wasNorWester)
            paras.Add("Fort William empties. The great hall where the partners voted their shares goes quiet, and the best of the wintering partners become chief factors and chief traders under the Deed Poll, with shares in the Company's profits instead of their own.");
        paras.Add("Not all of it changed at once. William Williams, the senior governor, took the Southern Department and ran it from Moose Factory under the old ways until 1826, when the two departments came under Simpson. The country learned of the new order the way it learned of everything, late, and a piece at a time.");
        paras.Add("You can play on in the world as it is now, or leave it here.");
        return string.Join("\n\n", paras);
    }
}
