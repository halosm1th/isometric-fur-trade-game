using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Agents.Goals;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Economy;

// The ball game of the Lakes and the woods - baaga'adowe to the Anishinaabe,
// tewaarathon to the Haudenosaunee, takapsicapi to the Dakota; "the little
// brother of war". In the summer a camp sends word to a neighbour it is not
// at odds with, and some days later their young men meet on the flat ground
// by the host's camp, the whole of both camps watching and betting what they
// have - kettles, guns, blankets - on their own side. A game played hard and
// fair lays an old quarrel down a little; men are hurt at it; the winners go
// home richer and the country hears of it.
//
// Near a player the game is played: the players run the field, the camps
// stand round it. Out of sight it is settled in numbers, by the players'
// strength and skill and luck.
public static class Lacrosse
{
    private sealed class Game
    {
        public CountryBand Host, Guest;
        public int Day;
        public Vector2I Field;
        public bool Playing;
        public int EndsHour;
        public readonly List<Npc> Players = new();
    }
    private static readonly List<Game> _games = new();
    private static readonly Dictionary<CountryBand, int> _lastGame = new();

    public static void Reset() { _games.Clear(); _lastGame.Clear(); _fields.Clear(); _dev = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--lacrosse") >= 0; }

    // Dev aid: `-- --lacrosse` - a game at the camp that plays nearest the
    // player, today, against its nearest neighbour.
    private static bool _dev;
    private static void DevGame(GameCalendar cal)
    {
        _dev = false;
        var p = Sim.Player;
        if (p == null) return;
        CountryCamp host = null; float best = float.MaxValue;
        foreach (var c in Camps.All) if (Plays(c) && IsoGrid.ToWorld(c.Ledger.Camp).DistanceTo(p.GlobalPosition) is var d && d < best) { best = d; host = c; }
        if (host == null) return;
        CountryCamp guest = null; best = float.MaxValue;
        foreach (var c in Camps.All) if (c != host && Plays(c) && IsoGrid.ToWorld(c.Ledger.Camp).DistanceTo(IsoGrid.ToWorld(host.Ledger.Camp)) is var d && d < best) { best = d; guest = c; }
        if (guest == null) return;
        var field = FieldAt(host.Ledger.Camp);
        var g = new Game { Host = host.Ledger, Guest = guest.Ledger, Day = Today(cal), Field = field };
        _games.Add(g);
        GD.Print($"[dev] {NameOf(host)}: {host.Name} against {guest.Name} at {field}");
        Start(g, cal);
    }

    // Who plays it, and what they call it.
    public static bool Plays(CountryCamp c) => !c.IsFamily && c.Ledger != null &&
        (c.Nation is Faction.Ojibwe or Faction.Haudenosaunee or Faction.SaukFox or Faction.Sioux || c.Name.StartsWith("Ho-Chunk"));
    public static string NameOf(CountryCamp c) =>
        c.Nation == Faction.Haudenosaunee && !c.Name.StartsWith("Wyandot") ? "tewaarathon"
        : c.Nation == Faction.Sioux ? "takapsicapi"
        : c.Nation == Faction.Ojibwe && !c.Name.StartsWith("Menominee") ? "baaga'adowe"
        : "the ball game";

    // Once a week in the summer: a camp sends word to a neighbour.
    public static void Week(GameCalendar cal)
    {
        if (cal.Month is < 6 or > 8) return;
        int today = Today(cal);
        foreach (var host in Camps.All)
        {
            if (!Plays(host) || GD.Randf() > 0.2f) continue;
            if (_lastGame.TryGetValue(host.Ledger, out int last) && today - last < 30) continue;
            if (_games.Exists(g => g.Host == host.Ledger || g.Guest == host.Ledger)) continue;
            CountryCamp guest = null; float best = 9000f;
            var at = IsoGrid.ToWorld(host.Ledger.Camp);
            foreach (var c in Camps.All)
            {
                if (c == host || !Plays(c) || _games.Exists(g => g.Host == c.Ledger || g.Guest == c.Ledger)) continue;
                if (Plains.Feud(host.Ledger, c.Ledger) >= 4f || Plains.Feud(c.Ledger, host.Ledger) >= 4f) continue;   // not with people you are at war with
                float d = IsoGrid.ToWorld(c.Ledger.Camp).DistanceTo(at);
                if (d < best) { best = d; guest = c; }
            }
            if (guest == null) continue;
            var field = FieldAt(host.Ledger.Camp);
            _games.Add(new Game { Host = host.Ledger, Guest = guest.Ledger, Day = today + GD.RandRange(3, 8), Field = field });
            _lastGame[host.Ledger] = today; _lastGame[guest.Ledger] = today;
            Chronicle.Event($"{host.Name} have sent tobacco to {guest.Name}: there is to be {NameOf(host)} at their camp.", at, null, host.Name, 0.6f);
            if (Plains.Log) GD.Print($"[lacrosse] {host.Name} invite {guest.Name} to {NameOf(host)}, in {_games[^1].Day - today} days");
        }
    }

    // Every day: a game whose day has come is played, or settled.
    public static void Day(GameCalendar cal)
    {
        int today = Today(cal);
        foreach (var g in new List<Game>(_games))
        {
            if (g.Playing) { if (cal.Hour >= g.EndsHour || today > g.Day) Finish(g, true); continue; }
            if (today < g.Day) continue;
            if (NearPlayer(g.Host.Camp)) Start(g, cal);
            else Finish(g, false);
        }
    }

    // The playing ground: the nearest open, dry stretch round the camp - a
    // meadow, a beach, the burnt-over ground - with no timber on it for a
    // few cells every way; beside the camp if the bush is thick to the water.
    private static readonly Dictionary<Vector2I, Vector2I> _fields = new();
    private static Vector2I FieldAt(Vector2I camp)
    {
        if (_fields.TryGetValue(camp, out var known)) return known;
        bool Open(Vector2I c) => !WorldMap.IsWater(c) && WorldLayout.ForestDensity(c) <= 0f;
        Vector2I best = camp; bool found = false;
        for (int r = 4; r <= 28 && !found; r += 2)
            for (int dx = -r; dx <= r && !found; dx += 2)
                foreach (int dy in new[] { -r, r })
                {
                    var c = IsoGrid.Cell(camp, dx, dy);
                    int open = 0;
                    for (int i = -3; i <= 3; i++) for (int j = -3; j <= 3; j++) if (Open(IsoGrid.Cell(c, i, j))) open++;
                    if (open >= 42) { best = c; found = true; break; }
                }
        if (!found) best = IsoGrid.Cell(camp, 6, 2);
        best = Sim.Paths?.NearestWalkable(best, 8) ?? best;
        _fields[camp] = best;
        return best;
    }

    private static int Today(GameCalendar cal) => cal.Year * 400 + (int)cal.DayOfYear;

    // A game being played finishes when its hours are run (Core/Tiers, every frame: a few games at most).
    public static void Tick(GameCalendar cal)
    {
        if (_dev && Sim.Player != null && cal.Hour >= 10f) DevGame(cal);
        foreach (var g in new List<Game>(_games)) if (g.Playing && cal.Hour >= g.EndsHour) Finish(g, true);
    }

    private static Vector2 Nearest(Vector2I cell)
    {
        var at = IsoGrid.ToWorld(cell); var best = new Vector2(float.MaxValue, float.MaxValue);
        foreach (var p in Sim.Players()) if (p.GlobalPosition.DistanceTo(at) < best.DistanceTo(at)) best = p.GlobalPosition;
        return best;
    }
    private static bool NearPlayer(Vector2I cell) => Nearest(cell).DistanceTo(IsoGrid.ToWorld(cell)) < Tiers.LiveRadius;

    // The young men of both camps to the field, everyone else round it.
    private static void Start(Game g, GameCalendar cal)
    {
        g.Playing = true;
        g.EndsHour = Mathf.Min(19, (int)cal.Hour + 4);
        foreach (var side in new[] { g.Host, g.Guest })
            foreach (var n in side.Members)
            {
                if (!GodotObject.IsInstanceValid(n) || n.IsDead || n.Away) continue;
                bool player = !n.IsFemale && n.Age is >= 15 and <= 40;
                if (player) g.Players.Add(n);
                n.Brain.Clear(n);
                // Promoted to the live world by the tiers if he isn't already; a
                // man of the guest camp walks over.
                n.Brain.Push(n, player ? new WanderGoal(g.Field, 5, 4f) : new WanderGoal(g.Field, 10, 4f));
            }
        Sim.Headline($"{g.Host.Country?.Name} and {g.Guest.Country?.Name} are playing {NameOf(g.Host.Country)} by {g.Host.Country?.Name}'s camp.");
    }

    // Who won, what changed hands, who was hurt; and the country hears.
    private static void Finish(Game g, bool played)
    {
        _games.Remove(g);
        var host = g.Host.Country; var guest = g.Guest.Country;
        if (host == null || guest == null) return;
        float Strength(CountryBand b)
        {
            float s = 0f;
            foreach (var n in b.Members)
                if (GodotObject.IsInstanceValid(n) && !n.IsDead && !n.Away && !n.IsFemale && n.Age is >= 15 and <= 40)
                    s += 1f + n.Skills.Hunting + n.Needs.Health / 200f;
            return s * (0.7f + GD.Randf() * 0.6f);
        }
        float hs = Strength(g.Host) * 1.08f, gs = Strength(g.Guest);   // the host knows its ground
        var (won, lost) = hs >= gs ? (g.Host, g.Guest) : (g.Guest, g.Host);
        var (wonC, lostC) = hs >= gs ? (host, guest) : (guest, host);

        // The bets: the best of what the losers put up goes home with the winners.
        var taken = new List<string>();
        foreach (var id in new[] { ItemId.Kettle, ItemId.Musket, ItemId.PointBlanket, ItemId.Knife, ItemId.Tobacco, ItemId.Stroud, ItemId.Beads, ItemId.HideCoat })
        {
            if (taken.Count >= 3) break;
            int n = Mathf.Min(lost.Stores.Count(id), id is ItemId.Tobacco or ItemId.Beads or ItemId.Stroud ? 4 : 1);
            if (n <= 0 || GD.Randf() < 0.35f) continue;
            lost.Stores.Remove(id, n); won.Stores.Add(id, n);
            string lot = Items.Items.Lot(id, n);
            taken.Add(n > 1 ? lot : ("aeiou".Contains(lot[0]) ? "an " : "a ") + lot);
        }

        // Hard knocks: a man or two hurt, now and then badly.
        string hurt = "";
        foreach (var side in new[] { g.Host, g.Guest })
            foreach (var n in side.Members)
            {
                if (!GodotObject.IsInstanceValid(n) || n.IsDead || n.Away || n.IsFemale || n.Age is < 15 or > 40 || GD.Randf() > 0.06f) continue;
                n.Needs.Health = Mathf.Max(10f, n.Needs.Health - (float)GD.RandRange(15.0, 45.0));
                if (hurt == "") hurt = $" {n.DisplayName} of {side.Country?.Name} was carried off the field.";
            }

        // A game played well lays an old quarrel down a little.
        foreach (var (a, b) in new[] { (g.Host, g.Guest), (g.Guest, g.Host) })
        {
            float f = Plains.Feud(a, b);
            if (f <= 1f) Plains.CoolFeud(a, b); else Plains.HeatFeud(a, b, -1f);
        }

        string stakes = taken.Count > 0 ? $" and took home {string.Join(", ", taken)}" : "";
        string text = $"{wonC.Name} beat {lostC.Name} at {NameOf(host)}{stakes}.{hurt}";
        Chronicle.Event(text, IsoGrid.ToWorld(g.Field), null, wonC.Name, played ? 1.4f : 0.9f);
        if (Plains.Log) GD.Print($"[lacrosse] {(played ? "played" : "settled")}: {text}");
        if (played) Sim.Headline(text);
        foreach (var n in g.Players) if (GodotObject.IsInstanceValid(n) && !n.IsDead) n.Brain.Clear(n);
    }
}
