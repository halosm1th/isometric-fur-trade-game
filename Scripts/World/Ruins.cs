using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.Player;
using NetHub = IsometricFurTradeGame.Scripts.Net.Net;

namespace IsometricFurTradeGame.Scripts.World;

// A stone fort nobody keeps (design doc 18.1: the country remembers):
// a square curtain wall with a bastion at each corner, roofless houses
// inside, guns on the bastions, the wall broken here and there and
// breached in places. Laid out from world.json "ruins" (top, size,
// bastion; the curtain's cells run i down-right and j down-left from top).
public class Ruin
{
    public string Name, Sub, Rock, RockName;
    public Vector2I Top, RockCell;
    public int Size = 12, Bastion = 4;
    // Every cell inside the walls, as (i, j) offsets from Top.
    public readonly HashSet<Vector2I> Inside = new();
    // The roofless houses: (i, j, width, depth), door in the front wall.
    public readonly List<(int I, int J, int W, int D)> Houses = new();
    public Vector2I Cell(int i, int j) => IsoGrid.Cell(Top, i, j);
    public Vector2 Centre => IsoGrid.ToWorld(Cell(Size / 2, Size / 2));
    public int GateI => Size / 2 - 1;
    public Vector2I GateOutside => IsoGrid.DownLeft(IsoGrid.DownLeft(Cell(GateI, Size - 1)));
    public float Reach => (Size + Bastion) * 0.75f * IsoGrid.TileW;
}

public static class Ruins
{
    public static readonly List<Ruin> All = Load();

    private static List<Ruin> Load()
    {
        var list = new List<Ruin>();
        foreach (var v in WorldMap.LayoutArray("ruins"))
        {
            var d = v.AsGodotDictionary();
            var t = d["top"].AsGodotArray();
            var r = new Ruin
            {
                Name = d["name"].AsString(), Sub = d.TryGetValue("sub", out var s) ? s.AsString() : "",
                Top = new Vector2I(t[0].AsInt32(), t[1].AsInt32()),
                Size = d["size"].AsInt32(), Bastion = d["bastion"].AsInt32(),
            };
            if (d.TryGetValue("rock", out var rv))
            {
                var rd = rv.AsGodotDictionary(); var rc = rd["cell"].AsGodotArray();
                r.RockCell = new Vector2I(rc[0].AsInt32(), rc[1].AsInt32()); r.RockName = rd["name"].AsString(); r.Rock = rd["text"].AsString();
            }
            for (int i = 0; i < r.Size; i++) for (int j = 0; j < r.Size; j++) r.Inside.Add(new Vector2I(i, j));
            // A bastion is a square centred on each corner of the curtain.
            int b = r.Bastion, lo = -(b / 2);
            foreach (int ci in new[] { 0, r.Size - 1 })
                foreach (int cj in new[] { 0, r.Size - 1 })
                    for (int i = 0; i < b; i++) for (int j = 0; j < b; j++)
                        r.Inside.Add(new Vector2I(ci + lo + i + (ci > 0 ? 1 : 0), cj + lo + j + (cj > 0 ? 1 : 0)));
            if (d.TryGetValue("houses", out var hv))
                foreach (var h in hv.AsGodotArray()) { var a = h.AsGodotArray(); r.Houses.Add((a[0].AsInt32(), a[1].AsInt32(), a[2].AsInt32(), a[3].AsInt32())); }
            list.Add(r);
        }
        return list;
    }

    // The bush doesn't grow on the old fort's ground.
    public static bool NearAny(Vector2 at, float margin)
    {
        foreach (var r in All) if (r.Centre.DistanceTo(at) < r.Reach + margin) return true;
        return false;
    }

    // What stands on each edge of the fort's outline: 0 = sound wall,
    // 1 = knocked down to half its height, 2 = breached (a gap of rubble).
    // Edges 0..3 are N (down-left), E (up-left), S (up-right), W (down-right),
    // as StructurePainter's wall variants. The gate is open; the rest is
    // fixed by the cell, so every peer paints the same ruin.
    public static IEnumerable<(Vector2I Cell, int Edge, int State)> Walls(Ruin r)
    {
        foreach (var c in r.Inside)
        {
            var across = new[] { new Vector2I(c.X, c.Y + 1), new Vector2I(c.X - 1, c.Y), new Vector2I(c.X, c.Y - 1), new Vector2I(c.X + 1, c.Y) };
            for (int e = 0; e < 4; e++)
            {
                if (r.Inside.Contains(across[e])) continue;
                if (e == 0 && c.Y == r.Size - 1 && (c.X == r.GateI || c.X == r.GateI + 1)) continue;
                yield return (c, e, State(r, c, e));
            }
        }
        foreach (var (hi, hj, w, d) in r.Houses)
            for (int i = 0; i < w; i++)
                for (int j = 0; j < d; j++)
                {
                    var c = new Vector2I(hi + i, hj + j);
                    // The houses burned and were blown: most of their walls are down.
                    // The doorway is in the middle of the front wall.
                    if (j == d - 1 && i != w / 2) yield return (c, 0, State(r, c, 0, 0.55f));
                    if (i == 0) yield return (c, 1, State(r, c, 1, 0.55f));
                    if (j == 0) yield return (c, 2, State(r, c, 2, 0.55f));
                    if (i == w - 1) yield return (c, 3, State(r, c, 3, 0.55f));
                }
    }

    private static int State(Ruin r, Vector2I c, int edge, float broken = 0.3f)
    {
        float h = IsoGrid.Hash(r.Cell(c.X, c.Y), 211 + edge);
        // The breaches run two cells wide: decide them by pairs along the wall.
        float pair = IsoGrid.Hash(r.Cell(edge % 2 == 0 ? c.X / 2 : c.X, edge % 2 == 0 ? c.Y : c.Y / 2), 223 + edge);
        return pair < 0.08f ? 2 : h < broken ? 1 : 0;
    }

    // The guns on the bastions (the French spiked them and left them), and
    // where the rubble lies.
    public static IEnumerable<Vector2I> Guns(Ruin r)
    {
        int b = r.Bastion, lo = -(b / 2), hi = r.Size - 1 + b / 2;
        yield return r.Cell(lo + 1, lo); yield return r.Cell(lo, lo + 1);
        yield return r.Cell(hi - 1, lo); yield return r.Cell(hi, lo + 1);
        yield return r.Cell(lo + 1, hi); yield return r.Cell(lo, hi - 1);
        yield return r.Cell(hi - 1, hi); yield return r.Cell(hi, hi - 1);
    }

    public static IEnumerable<Vector2I> Rubble(Ruin r)
    {
        foreach (var (c, e, state) in Walls(r))
        {
            if (state != 2) continue;
            yield return r.Cell(c.X, c.Y);
            var o = e switch { 0 => new Vector2I(c.X, c.Y + 1), 1 => new Vector2I(c.X - 1, c.Y), 2 => new Vector2I(c.X, c.Y - 1), _ => new Vector2I(c.X + 1, c.Y) };
            yield return r.Cell(o.X, o.Y);
        }
        foreach (var c in r.Inside)
            if (IsoGrid.Hash(r.Cell(c.X, c.Y), 229) < 0.05f) yield return r.Cell(c.X, c.Y);
    }

    // The fort's dead, outside the back wall.
    public static IEnumerable<Vector2I> Graves(Ruin r)
    {
        for (int i = r.Size / 2 - 2; i <= r.Size / 2 + 1; i += 2) yield return r.Cell(i, -2);
    }

    // ---- walking in -----------------------------------------------------------------

    private static float _look;

    // Server, from Population: the first time someone comes inside the walls,
    // or to the rock at the cove, the place is put to him as a page.
    public static void Tick(float delta)
    {
        if (!NetHub.IsServer || All.Count == 0) return;
        _look -= delta;
        if (_look > 0f) return;
        _look = 1f;
        foreach (var p in Sim.Players())
        {
            if (p == null || !GodotObject.IsInstanceValid(p) || p.IsDead || p.Conversation.IsOpen) continue;
            foreach (var r in All)
            {
                if (r.RockName != null && !p.Known.Contains("ruin:" + r.RockName) && p.GlobalPosition.DistanceTo(IsoGrid.ToWorld(r.RockCell)) < 170f)
                {
                    p.Known.Add("ruin:" + r.RockName);
                    NetHub.I?.PushDialogue(p, p.Conversation.StartScene(RockPage(p, r)));
                    break;
                }
                if (!p.Known.Contains("ruin:" + r.Name) && Within(r, p.GlobalPosition))
                {
                    p.Known.Add("ruin:" + r.Name);
                    NetHub.I?.PushDialogue(p, p.Conversation.StartScene(FortPage(p, r)));
                    break;
                }
            }
        }
    }

    public static bool Within(Ruin r, Vector2 at)
    {
        if (r.Centre.DistanceTo(at) > r.Reach) return false;
        var c = IsoGrid.FromWorld(at);
        foreach (var i in r.Inside) if (r.Cell(i.X, i.Y) == c) return true;
        return false;
    }

    private static void Note(PlayerController p, string line) => p.Journal.Add($"{GameCalendar.Instance?.DateString}: {line}");

    // Prince of Wales Fort: forty years building (1731-1771), given up to La
    // Pérouse's squadron on 8 August 1782 by Samuel Hearne with some thirty-nine
    // men and no soldiers; the French spiked the guns, burned the houses and
    // mined the walls, which mostly held.
    private static ScenePage FortPage(PlayerController p, Ruin r)
    {
        var seen = new HashSet<string>();
        ScenePage Hub()
        {
            var page = new ScenePage
            {
                Title = r.Name, CanLeave = true,
                Text = seen.Count > 0 ? "The wind comes in off the Bay over the broken parapet."
                    : "Grey stone, laid course on course, the walls as thick at the foot as a house is long. The Company was forty years building this, and gave it up in an afternoon: in August of 1782 three French ships came in, and Mr. Hearne, with some forty men and not a soldier among them, opened the gate. The French spiked the guns, burned what would burn and tried to blow the walls. The houses went. The walls mostly held.",
            };
            if (seen.Add("_")) Note(p, $"Walked into the ruin of {r.Name}, the Company's stone fort at the Churchill mouth, empty since the French took it in 1782.");
            if (!seen.Contains("rampart")) page.Add("Climb a bastion", () => { seen.Add("rampart"); return Rampart(); });
            if (!seen.Contains("guns")) page.Add("Look at the guns", () => { seen.Add("guns"); return Guns(); });
            if (!seen.Contains("house")) page.Add("Search the burned house", () => { seen.Add("house"); return House(); });
            page.Add("Leave it to the wind", () => null);
            return page;
        }
        ScenePage Rampart()
        {
            if (Sim.Player == p) UI.Exploration.Reveal(r.Centre, 9000f);
            Note(p, $"From the bastion of {r.Name}: the Bay to the north, the river mouth below, and the wooden factory up the far shore.");
            return new ScenePage { Title = "The bastion", CanLeave = true, Text = "The steps are half gone but the rampart is broad enough to drive a cart along. From the top you can see a long way: the Bay to the north, grey to the edge of the world; the river mouth below with the tide running; the Company's wooden factory up the far shore, with smoke going up from it; and inland, nothing but rock and moss and stunted spruce." }.Add("Back", Hub);
        }
        ScenePage Guns() => new ScenePage { Title = "The guns", CanLeave = true, Text = "They lie where the French left them, some on their rotted carriages, some tumbled on the stone: big iron guns for a fort that never fired them. Each touch-hole has an iron spike hammered into it, and on some the trunnions are knocked off. Somebody has cut initials into the nearest one." }
            .Add("Back", Hub);
        ScenePage House()
        {
            p.Inventory.Add(ItemId.Flint, 2);
            Note(p, $"Found two gunflints in the ash of the house at {r.Name}.");
            return new ScenePage { Title = "The burned house", CanLeave = true, Text = "Inside the shell there is a floor of ash and fallen stone, a chimney standing alone against the sky, and broken bottle glass everywhere. Raking along the foot of the wall you turn up two good gunflints that the French missed." }
                .Add("Back", Hub);
        }
        return Hub();
    }

    // Sloop Cove, where the sloops lay up for the winter: the men cut their
    // names in the rock, Samuel Hearne's among them ("Sl. Hearne, July ye 1 1767").
    private static ScenePage RockPage(PlayerController p, Ruin r)
    {
        Note(p, $"At {r.RockName}, read the names cut in the rock, among them Samuel Hearne's, 1767.");
        return new ScenePage { Title = r.RockName, CanLeave = true, Text = r.Rock }
            .Add("Cut your own name beside them", () =>
            {
                Note(p, $"Cut your name into the rock at {r.RockName}, under the old ones.");
                return new ScenePage { Title = r.RockName, CanLeave = true, Text = "It takes the best part of an hour with the point of a knife, and the letters are not as straight as Mr. Hearne's. But they will be there long after you are gone." }.Add("Leave", () => null);
            })
            .Add("Leave", () => null);
    }
}
