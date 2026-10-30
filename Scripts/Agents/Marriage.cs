using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Agents;

// Marriage in the custom of the country (design doc 8). How a man marries is
// his side's custom, not one rule for everyone:
// - the Company's way (8.1, 8.2): one wife, and she comes into his post;
// - the Nor'West way: he marries into her family and she stays among her
//   people, where he is kin and comes and goes;
// - born to a band, she comes to his lodge.
// Only the Company and the King hold a man to one wife. Going over to the
// Company with more than one means choosing, and the ones left behind are
// not forgotten (8.3, 8.4).
public static class Marriage
{
    public enum Way { Post, NorWest, Lodge }

    public static Way WayOf(Player.PlayerController p)
    {
        if (CharacterSheet.IsBand(p.StartPath) && p.Faction != Faction.NWC && p.Faction != Faction.HBC) return Way.Lodge;
        if (p.Faction is Faction.HBC or Faction.American or Faction.Crown && p.HomePost != null) return Way.Post;
        return Way.NorWest;
    }

    public static bool OneWifeOnly(Player.PlayerController p) => p.Faction is Faction.HBC or Faction.Crown;

    // Every living wife, the first married first.
    public static List<Npc> Wives(Player.PlayerController p)
    {
        var all = new List<Npc>();
        if (p.Spouse != null && GodotObject.IsInstanceValid(p.Spouse) && !p.Spouse.IsDead) all.Add(p.Spouse);
        foreach (var w in p.OtherWives) if (GodotObject.IsInstanceValid(w) && !w.IsDead && !all.Contains(w)) all.Add(w);
        return all;
    }

    public static bool IsWife(Player.PlayerController p, Npc n) => n != null && (p.Spouse == n || p.OtherWives.Contains(n));

    // What the father says when asked how it is done; it depends on whose man you are.
    public static string Custom(Player.PlayerController p) => WayOf(p) switch
    {
        Way.Post when p.Faction == Faction.HBC => "Your Company allows a man one wife, and she goes to live in your post; we do not hold you to one, but your Factor will.",
        Way.Post => "She would go to live at your post, among strangers.",
        Way.NorWest => "A Nor'Wester who marries one of ours is one of ours. She stays with her people, and you are of her family when you come to us. We do not count a man's wives; feed them all.",
        _ => "She would come to your lodge. A good hunter may keep more than one wife, if he can feed them.",
    };

    public static string Propose(Player.PlayerController p, Npc father, Npc daughter)
    {
        var wives = Wives(p);
        if (wives.Count > 0 && OneWifeOnly(p))
            return p.Faction == Faction.HBC ? "\"You have a wife already. Your Company allows a man one.\"" : "\"You have a wife already, and your King allows a man one.\"";
        float rapport = father.RapportWith(p);
        float gifts = father.GiftsFromPlayerMb;
        // A second wife costs what the first did, and her father looks to how the first is kept.
        float wantRapport = 30f + 10f * wives.Count, wantGifts = 8f * (1 + wives.Count);
        if (rapport < wantRapport) return $"{father.DisplayName} hears you out and says nothing. You are a stranger to him yet. (rapport {rapport:0}, needs {wantRapport:0})";
        if (gifts < wantGifts) return $"\"A daughter is not given for nothing.\" He looks at what you have brought him. (gifts worth {gifts:0.#} MB, wants {wantGifts:0})";

        daughter.Spouse = p;
        if (p.Spouse == null || !GodotObject.IsInstanceValid(p.Spouse) || p.Spouse.IsDead) p.Spouse = daughter;
        else p.OtherWives.Add(daughter);
        if (wives.Count == 0) p.MarriedOnDay = GameCalendar.Instance.Day;
        p.Learn(daughter, 0.3f);
        daughter.Role = Role.Wife;
        var way = WayOf(p);
        string where = MoveIn(p, daughter, way);
        // Married into the family the Nor'West way, a man is kin to the whole camp, not a visitor to it.
        float kin = way == Way.NorWest ? 20f : 10f;
        foreach (var n in Sim.Npcs) if (n.HomeStores == father.HomeStores) n.ChangeRapport(p, kin);
        if (way == Way.NorWest && father.HomeStores is BandLedger band) band.StandingWithNwc = Mathf.Min(100f, band.StandingWithNwc + 10f);
        Sim.Notice($"{daughter.DisplayName} has married {p.DisplayName} in the custom of the country{where}.");
        return way switch
        {
            Way.NorWest => $"\"So be it.\" {daughter.DisplayName} stays with her people, and you are of her family now. Come back to her often.",
            Way.Lodge => $"\"So be it.\" {daughter.DisplayName} will come to your lodge. Her family is now your family.",
            _ => $"\"So be it.\" {daughter.DisplayName} will come to live at the post. Her family is now your family.",
        };
    }

    // Where a wife lives, the way her husband married. Returns a few words for the notice.
    public static string MoveIn(Player.PlayerController p, Npc wife, Way way)
    {
        Household home = way switch
        {
            Way.Post => p.HomePost,
            Way.Lodge => CharacterSheet.BandOf(p),
            _ => null,
        };
        if (home == null) return " and stays with her people";   // the Nor'West way: her home is where it was
        wife.HomeStores = home;
        if (home is BandLedger lodge && new List<Vector2I>(lodge.Tipis()) is { Count: > 0 } tipis)
        {
            wife.BedCell = tipis[(int)(GD.Randi() % (uint)tipis.Count)];
            wife.WorkCell = IsoGrid.Cell(lodge.Camp, 1, 2);
        }
        else
        {
            var hearth = home.HearthCells.Count > 0 ? home.HearthCells[0] : home.AccessCell;
            var bed = IsoGrid.Cell(hearth, 3, 2);
            wife.BedCell = Sim.Paths?.NearestWalkable(bed) ?? bed;
            wife.WorkCell = home.AccessCell;
        }
        wife.Brain.Clear(wife);
        wife.ResetBehaviour();
        return way == Way.Lodge ? " and comes to his lodge" : $" and moves into {home.Title.Replace(" stores", "")}";
    }

    // He has gone over to the Company (a desertion, or the union of 1821)
    // with more than one wife (8.3, 8.4). The first wife goes with him into
    // the post, and that is costly; the others go home, and their families
    // remember it. A wife left behind marries again, and her new husband is
    // a man with his own side.
    public static void IntoTheCompany(Player.PlayerController p)
    {
        var wives = Wives(p);
        if (wives.Count == 0) return;
        var kept = wives[0];
        p.Spouse = kept;
        p.OtherWives.Clear();
        for (int i = 1; i < wives.Count; i++) LeaveBehind(p, wives[i]);
        // Bringing a wife of the country onto the Company's books is a harder
        // road than leaving it (8.3): the Factor wants it regular, and it costs him standing.
        if (kept.HomeStores is not FortLedger && p.HomePost != null)
        {
            MoveIn(p, kept, Way.Post);
            p.Standing = Mathf.Max(0f, p.Standing - 5f);
            if (kept.Father != null && !kept.Father.IsDead) kept.Father.ChangeRapport(p, -5f);
            p.Say($"{kept.DisplayName} comes with you into the Company's post. Her family let her go, and not gladly.");
        }
        if (wives.Count > 1)
            p.Journal.Add($"{GameCalendar.Instance?.DateString}: The Company allows a man one wife. {kept.DisplayName} goes with you; {string.Join(" and ", wives.GetRange(1, wives.Count - 1).ConvertAll(w => w.DisplayName))} went home to their people.");
    }

    public static void LeaveBehind(Player.PlayerController p, Npc wife)
    {
        if (p.Spouse == wife) p.Spouse = null;
        p.OtherWives.Remove(wife);
        wife.Spouse = null;
        var band = wife.Father?.HomeStores as BandLedger ?? (wife.HomeStores as BandLedger) ?? Sim.Band;
        wife.Role = Role.BandDaughter;
        if (band != null)
        {
            wife.HomeStores = band;
            var tipis = new List<Vector2I>(band.Tipis());
            if (tipis.Count > 0) wife.BedCell = tipis[0];
            wife.WorkCell = IsoGrid.Cell(band.Camp, 1, 2);
            band.StandingWithCompany = Mathf.Max(0f, band.StandingWithCompany - 15f);
            band.OnInsult();
        }
        wife.ResetBehaviour(); wife.Brain.Clear(wife);
        // The judgement of her family and her people, and a debt owed for her.
        foreach (var n in Sim.Npcs) if (n.HomeStores == band) n.ChangeRapport(p, -20f);
        wife.ChangeRapport(p, -50f);
        if (wife.Father != null && !wife.Father.IsDead && !Grudges.HasOpen(wife.Father.DisplayName, p.DisplayName))
            Grudges.All.Add(new Grudge { Holder = wife.Father.DisplayName, Against = p.DisplayName, Victim = wife.DisplayName, Day = GameCalendar.Instance?.Day ?? 0, DebtMb = 12f });
        Chronicle.Event($"{p.DisplayName} put away {wife.DisplayName}, and she went home to her people.", wife.GlobalPosition, p, wife.DisplayName, 2f);
        Remarry(p, wife);
    }

    // She finds another husband, and he has a side of his own: a Nor'Wester
    // or a man of her own people, whoever is near and unmarried.
    private static void Remarry(Player.PlayerController p, Npc wife)
    {
        Npc best = null; float bestD = float.MaxValue;
        foreach (var n in Sim.Npcs)
        {
            if (n.IsDead || n.IsFemale || n.Spouse != null || n.Age < 18) continue;
            if (n.Faction != Faction.NWC && n.Faction != wife.Faction) continue;
            if (n.Faction == p.Faction) continue;
            float d = n.GlobalPosition.DistanceTo(wife.GlobalPosition);
            if (d < bestD) { bestD = d; best = n; }
        }
        if (best == null) return;
        best.Spouse = wife; wife.Spouse = best;
        wife.Role = best.IsBandPerson ? Role.BandWoman : Role.Wife;
        best.ChangeRapport(p, -15f);
        if (!best.IsBandPerson && best.HomeStores != null) MoveInWith(wife, best);
        Sim.Notice($"{wife.DisplayName} has married {best.DisplayName}.");
    }

    private static void MoveInWith(Npc wife, Npc husband)
    {
        wife.HomeStores = husband.HomeStores;
        wife.BedCell = husband.BedCell;
        wife.WorkCell = husband.WorkCell;
        wife.ResetBehaviour(); wife.Brain.Clear(wife);
    }
}
