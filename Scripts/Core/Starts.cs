using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Core;

// How a new character comes into the world (design doc 5.5, P.1's first
// hour). Each road in has a short prologue - the last day of the crossing,
// the last carrying place below the post, a morning in the camp - with the
// people you arrive among, who are real and stay real: a face you can make a
// friend of, a temptation with a cost, and a rumour that names the men who
// matter. Then you're set down where you belong with your first instruction.
// Only for a character made on the new-game page; dev-arg starts are as before.
public class ScenePage
{
    public string Title = "", Text = "";
    public bool CanLeave;   // Esc closes it; a prologue is not walked away from
    public readonly List<(string label, Func<ScenePage> next)> Options = new();
    public ScenePage Add(string label, Func<ScenePage> next) { Options.Add((label, next)); return this; }
}

public static class Starts
{
    private static bool _worldDated;
    public static void Reset() => _worldDated = false;

    // The first line a player sees, by the road they came in on.
    public static string Intro(Player.PlayerController p) => p.StartPath switch
    {
        StartPath.NWC => "The North West Company's house, across from the Bay men. You are the partner's new man.",
        StartPath.Cree => "The Cree summer camp by the Bay men's house. These are your people.",
        StartPath.Assiniboine => "The Assiniboine camp. These are your people.",
        StartPath.American => "Fort Okanogan, on the upper Columbia. You are Mr. Astor's man, a long way from New York.",
        StartPath.Crown => $"{(p.GarrisonTown is { } t ? $"{t.FortName}, {t.Name}" : "Upper Canada")}. You are a private soldier of His Majesty's garrison.",
        _ => "York Factory, Hudson's Bay. You are the Company's new hand.",
    };

    // A garrison of that side for a soldier: the town (or fort) named, if it
    // stands and is held by that side; else the nearest that is (to the
    // place named, or to a point); else York's, the first in Upper Canada.
    public static Town GarrisonFor(string at, Faction side, Vector2? near = null)
    {
        bool Ours(Town t) => t != null && t.Standing && t.Garrison != null && t.Faction == side;
        if (!string.IsNullOrEmpty(at))
        {
            var named = Towns.Named(at) ?? Towns.All.Find(t => t.FortName == at);
            if (Ours(named)) return named;
            if (SpotOf(at) is Vector2 spot) near ??= spot;
        }
        if (near == null && Ours(Towns.Named("York"))) return Towns.Named("York");
        Town best = null; float bestD = float.MaxValue;
        foreach (var t in Towns.All)
        {
            if (!Ours(t)) continue;
            float d = near is Vector2 v ? t.Centre.DistanceTo(v) : 0f;
            if (d < bestD) { bestD = d; best = t; }
        }
        return best;
    }

    // Where a campaign's start_at is on the map, or null if nothing has that name.
    public static Vector2? SpotOf(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (Towns.All.Find(t => t.FortName == name) is { HasFort: true } ft)
            return IsoGrid.ToWorld(Sim.Paths?.NearestWalkable(IsoGrid.Cell(ft.FortTop, ft.FortWidth / 2, ft.FortDepth / 2), 12) ?? ft.FortTop);
        bool known = Posts.Named(name) != null || WorldMap.Place(name) != null || Towns.Named(name) != null || Camps.Named(name) != null || WorldMap.Landmarks().Any(l => l.Name == name);
        return known ? IsoGrid.ToWorld(TerrainPainter.NamedSpot(name)) : null;
    }

    // On the host, for a player new to this world, after the sheet is applied.
    public static void Begin(Player.PlayerController p, bool chosen, bool firstInWorld)
    {
        if (!chosen) return;
        var pop = p.GetTree().GetFirstNodeInGroup("population") as Population;
        // The first character of a new world sets the season he arrives in.
        bool dating = firstInWorld && !_worldDated;
        if (dating)
        {
            _worldDated = true;
            var cal = GameCalendar.Instance;
            int year = cal?.Year ?? 1811;
            // A campaign that names its first day has it on every path.
            if (Campaign.StartDate is var (cy, cm, cd) && !OS.GetCmdlineUserArgs().Any(x => x.StartsWith("--date="))) cal?.SetDate(cy, cm, cd, 7f);
            else switch (p.StartPath)
            {
                case StartPath.HBC: cal?.SetDate(year, 8, 20, 7f); break;           // the ship's day
                case StartPath.NWC: cal?.SetDate(year, 7, 12, 7f); break;           // the brigade's day
                case StartPath.American: cal?.SetDate(year, 9, 2, 7f); break;       // Mr. Stuart's party at the Okanogan
                case StartPath.Crown: cal?.SetDate(year, 6, 4, 7f); break;          // the King's birthday parade
                default: cal?.SetDate(year, 6, 10, 7f); break;   // the summer camp by the post
            }
            // The bands keep the season the date says: by the post from break-up to freeze-up.
            bool summer = (cal?.Month ?? 6) is >= 6 and <= 10;
            foreach (var b in new[] { Sim.Band, Sim.Assiniboine }) if (b != null) b.MoveCamp(summer ? b.SummerGround : b.WinterGround);
        }
        // A campaign that sets everyone down somewhere of its own (start_at)
        // has no use for the ship or the brigade: you are simply there. A
        // soldier still stands his parade, at the garrison nearest it.
        var at = SpotOf(Campaign.StartAt);
        if (Campaign.StartAt != "" && at == null) GD.PushWarning($"[start] the campaign's start_at '{Campaign.StartAt}' is nowhere on the map");
        ScenePage first = p.StartPath switch
        {
            StartPath.Crown => Parade(p, at),
            _ when at is Vector2 there && p.HomeCamp != Campaign.StartAt => SetDown(p, there),
            StartPath.HBC => Ship(p, pop, dating),
            StartPath.NWC => Brigade(p, pop, dating),
            StartPath.American => Columbia(p, pop),
            _ => Camp(p),
        };
        // Open the prologue a moment after the world settles.
        p.GetTree().CreateTimer(2.0).Timeout += () =>
        {
            if (!GodotObject.IsInstanceValid(p)) return;
            var state = p.Conversation.StartScene(first);
            Net.Net.I?.PushDialogue(p, state);
            Strand(p, 0);
        };
    }

    // ---- the ship ---------------------------------------------------------------------

    // The month by name, for a start that doesn't fall on its usual day.
    private static string Month => (GameCalendar.Instance?.Month ?? 8) switch
    {
        1 => "January", 2 => "February", 3 => "March", 4 => "April", 5 => "May", 6 => "June",
        7 => "July", 8 => "August", 9 => "September", 10 => "October", 11 => "November", _ => "December",
    };

    private static ScenePage Ship(Player.PlayerController p, Population pop, bool dating)
    {
        // The ship lies off the coast with you aboard and comes in on the
        // morning tide, when you turn in (Arrive). A man
        // joining a world already running comes when it is: if the ship isn't
        // in the roads, he came round from Churchill in the Company's sloop.
        if (dating && Sim.Ship != null && Sim.Ship.Current == World.Ship.State.Away) Sim.Ship.StandOffshore();
        bool bySloop = Sim.Ship == null || Sim.Ship.Current is not (World.Ship.State.Arriving or World.Ship.State.Docked);
        var land = IsoGrid.ToWorld(WorldLayout.WharfEnd);
        // Aboard her, out in the roads, for as long as the prologue lasts.
        var deck = Sim.Ship != null ? Sim.Ship.Position + new Vector2(0, 30) : land + new Vector2(600, -260);
        p.SetStartPosition(deck);
        // Two lads who signed on with you at Stromness.
        var mates = new List<Npc>();
        string[] names = { "Hugh Rendall", "Andrew Flett" };
        var mens = WorldLayout.MensHouse;
        for (int i = 0; i < names.Length && pop != null && Sim.Fort != null; i++)
        {
            var m = pop.Spawn(names[i], Role.Labourer, Faction.HBC, IsoGrid.Cell(mens.Top, 1 + i, 4), IsoGrid.Cell(WorldLayout.FortTop, 10 + i, 9), Sim.Fort, GD.Load<SpriteFrames>("res://Scenes/Npc/LabourerFrames.tres"), Colors.White);
            m.Inventory.Add(ItemId.Capote); m.Inventory.Add(ItemId.Knife); m.Inventory.Add(ItemId.Bread, 2);
            m.Age = GD.RandRange(17, 23);
            m.GlobalPosition = Dry(land + new Vector2(10 + i * 30, 60));
            m.Brain.Push(m, new Agents.Goals.ReportForDutyGoal());
            Shared(m, p, $"{m.DisplayName} and {p.DisplayName} crossed from Stromness together on the Company's ship.");
            mates.Add(m);
        }
        if (mates.Count == names.Length) Campaign.Flag("ship_mates");   // the broached cask (company-servant.camp) is theirs
        p.AssignTask(Player.TaskKind.Report, 1, "you are the Company's new hand");
        string factor = Clean(Sim.Fort?.Leader?.DisplayName) ?? "the Factor";
        string rival = RivalClerk(Sim.Fort);
        string partner = Clean(Sim.Nwc?.Leader?.DisplayName) ?? "the Canadian partner";
        string sailor = "Davy Gunn";
        string a = mates.Count > 0 ? Agents.Barks.CallName(mates[0]) : "Hugh", b = mates.Count > 1 ? Agents.Barks.CallName(mates[1]) : "Andrew";
        var seen = new HashSet<string>();

        ScenePage hub = null;
        ScenePage Hub()
        {
            hub = new ScenePage { Title = bySloop ? "The last day aboard the sloop" : "The last day of the crossing", Text = seen.Count == 0
                ? (bySloop
                    ? $"Hudson's Bay, {Month}. The Company's sloop from Churchill runs down the coast toward the mouth of the Hayes with you, {a} and {b} aboard - three hands Churchill could spare. A sailor, {sailor}, has been watching you since breakfast. The master is at the tiller with his glass."
                    : $"Hudson's Bay, {Month}. Six weeks out of Stromness the Company's ship beats down the low grey coast toward the mouth of the Hayes. {a} and {b}, who signed on with you, lean on the rail beside you. A sailor, {sailor}, has been watching you since breakfast. The mate is on the quarterdeck with his glass.")
                : "The coast is closer. The afternoon wears on." };
            if (!seen.Contains("mates")) hub.Add($"Talk with {a} and {b}", () => { seen.Add("mates"); return Mates(); });
            if (!seen.Contains("sailor")) hub.Add($"See what {sailor} wants", () => { seen.Add("sailor"); return Sailor(); });
            if (!seen.Contains("mate")) hub.Add(bySloop ? "Ask the master about the Factory" : "Ask the mate about the Factory", () => { seen.Add("mate"); return Mate(); });
            if (!seen.Contains("churchill")) hub.Add(bySloop ? "Talk with the Churchill people aboard" : "Watch the ship call at Churchill", () => { seen.Add("churchill"); return Churchill(); });
            hub.Add(bySloop ? "Turn in. The sloop will be in by morning." : "Turn in. The ship will be in by morning.", () => Arrive());
            return hub;
        }
        ScenePage Mates() => new ScenePage { Title = "At the rail", Text = $"{a} talks of his mother's croft on Sanday and the five years ahead of him. {b} says little, but he shares his tobacco. \"We'll stick together over there,\" {a} says. \"Whatever they set us to.\"" }
            .Add("\"We will.\" Shake on it.", () => { foreach (var m in mates) { m.ChangeRapport(p, 15f); Shared(m, p, $"{m.DisplayName} and {p.DisplayName} swore on the ship to stand by each other."); } Note(p, $"You swore with {a} and {b} to stand by each other."); return Hub(); })
            .Add("Keep your own counsel.", () => Hub());
        ScenePage Sailor() => new ScenePage { Title = sailor, Text = $"\"I've brandy of my own below,\" {sailor} says low. \"The Indians pay three beaver a keg and the Company need never know. Two pieces of your goods for each of my kegs - that's a fortune for a lad like you.\"" }
            .Add("Buy two kegs (4 pieces of goods)", () =>
            {
                if (p.Inventory.Count(ItemId.TradeGoods) < 4) return new ScenePage { Title = sailor, Text = "\"Not enough goods? Then you're not the man I took you for.\" He spits over the side." }.Add("Back", Hub);
                p.Inventory.Remove(ItemId.TradeGoods, 4); p.Inventory.Add(ItemId.Rum, 6);
                Note(p, $"You bought brandy from {sailor} aboard ship, to trade on your own account.");
                // The crew know. Whoever of them lands may say so.
                Remembered(p, $"{p.DisplayName} traded on his own account aboard the ship.", mates, 1.2f);
                return new ScenePage { Title = sailor, Text = "\"Keep it dry and keep it quiet.\" You stow six drams' worth among your things. If the Factor ever hears you've been trading on your own account, it will be in his report." }.Add("Back", Hub);
            })
            .Add("No.", () => Hub())
            .Add("Tell the mate.", () => { p.Standing += 3f; Note(p, $"You told the mate {sailor} was trading brandy on his own account. The Company will think well of it; the crew will not."); return new ScenePage { Title = "The mate", Text = $"The mate takes it down. {sailor} looks at you across the deck for the rest of the day." }.Add("Back", Hub); });
        ScenePage Mate() => new ScenePage { Title = bySloop ? "The master" : "The mate", Text = $"\"{factor}'s the Factor - an old Bay man, hard but fair, and he'll want you the hour you land. Watch {rival}: he wants the next place going and he'll not thank a new man for crowding him. And keep off the Canadians across the river - {partner}'s people. There's been bad blood since the winter.\"" }
            .Add("Thank him", () => { Note(p, $"The mate's advice: report to {factor} at once; {rival} wants the next place going; keep clear of {partner}'s Nor'Westers."); return Hub(); });
        // Churchill (the owner's opening: the ship calls there before York).
        // Prince of Wales Fort was taken by the French in 1782 without a shot
        // and its walls blown where they could be; the Company lives since in
        // a wooden factory up the river. The river's Cree name is Missinipi.
        string clerk = "James Spence";
        var atChurchill = new HashSet<string>();
        ScenePage Churchill()
        {
            var page = new ScenePage
            {
                Title = "Churchill",
                Text = atChurchill.Count > 0 ? "The boat from Churchill rides alongside while the last of the outfit goes over the side."
                    : bySloop ? $"The sloop came out of the Churchill three days ago. Besides {a}, {b} and you she carries {clerk}, a clerk of six winters at Churchill who is bound for York and then the Saskatchewan, the Churchill packet, and the man who piloted her over the bar, a Cree of the Churchill homeguard."
                    : $"The ship stands in to the mouth of the Churchill River to land the Churchill outfit before she runs down the coast to York. On the point, the great stone walls of Prince of Wales Fort stand empty: the French took it in 1782 without a shot fired and blew what they could. The Company's people live now in the wooden factory five miles up the river. A boat comes off with the Churchill packet, a clerk going down to York, and a Cree pilot at the steering oar.",
            };
            if (!atChurchill.Contains("clerk")) page.Add($"Talk with {clerk}, the Churchill clerk", () => { atChurchill.Add("clerk"); return ChurchillClerk(); });
            if (!atChurchill.Contains("pilot")) page.Add("Sit with the pilot", () => { atChurchill.Add("pilot"); return Pilot(); });
            if (!atChurchill.Contains("packet") && Sim.Fort?.Leader is Npc master) page.Add($"Offer to carry the Churchill packet to {factor}", () => { atChurchill.Add("packet"); return Packet(master); });
            page.Add("Back to the rail", Hub);
            return page;
        }
        ScenePage ChurchillClerk() => new ScenePage { Title = clerk, Text = $"{clerk} has been six winters at Churchill and is glad to be leaving it. \"They'll send you inland, a strong lad like you. If it's the Saskatchewan, it'll be Edmonton House - and the Nor'Westers' Fort Augustus stands so near it you could throw a stone from one gate to the other. The Cree of the Beaver Hills trade with whichever house treats them better that year, and the Blackfoot come in when they choose and not before. And the Canadians - friendly over a dram, and they'll have your best hunter away the next morning.\"" }
            .Add("Ask what a new man should know", () => { Note(p, $"{clerk}, a Churchill clerk: inland it will likely be Edmonton House, beside the Nor'Westers' Fort Augustus; the Beaver Hills Cree trade with whichever house treats them better; the Blackfoot come when they choose."); return new ScenePage { Title = clerk, Text = "\"Learn the tongue. Keep your word to the Indians and your powder dry. Never let the master see you idle, and never let the Canadians see you drunk.\" He grins. \"The rest you'll learn the hard way, like the rest of us.\"" }.Add("Back", Churchill); })
            .Add("Back", Churchill);
        ScenePage Pilot() => new ScenePage { Title = "The pilot", Text = "The pilot is a Cree man of the Churchill homeguard, who has brought the Company's boats over the bar since before you were born. He says little in English. When you point at the river he says \"Missinipi\" - the big water - and waits until you can say it back." }
            .Add("Say it back until he nods", () => { p.CreeFluency = Mathf.Min(1f, p.CreeFluency + 0.03f); Note(p, "A Cree pilot at Churchill taught you the river's name: Missinipi, the big water."); return new ScenePage { Title = "The pilot", Text = "He nods at last, and gives you two more words - for goose, and for tobacco - which you mean to remember." }.Add("Back", Churchill); })
            .Add("Back", Churchill);
        ScenePage Packet(Npc master)
        {
            p.Mail.Add(new Message { Kind = "letter", From = "the master at Churchill", To = master.DisplayName, Company = Faction.HBC, Subject = "the Churchill packet", Text = "Churchill's returns and its wants for the year, and a word for the new man who carried this: he was handy in the boat." });
            Note(p, $"You carry the Churchill packet for {factor}.");
            return new ScenePage { Title = clerk, Text = $"\"Good lad.\" {clerk} hands you an oilskin packet tied with tape. \"Into {factor}'s own hand, mind. Not the clerk's.\"" }.Add("Back", Churchill);
        }

        ScenePage Arrive()
        {
            if (!bySloop) Sim.Ship?.ComeIn();
            return ArrivePage();
        }
        ScenePage ArrivePage() => new ScenePage { Title = "York Factory", Text = $"Morning. The {(bySloop ? "sloop" : "ship")} comes up the Hayes on the tide and makes fast at the Factory's wharf. Palisade, flag, smoke from the chimneys, and every soul in the place come down to the shore. Your first order is in your pocket: report to {factor}." }
            .Add("Step ashore", () =>
            {
                // Now he is ashore: on the planks, by his own act.
                Place(p, land + new Vector2(-30, 40));
                Note(p, $"You came ashore at York Factory with {a} and {b}. Report to {factor}.");
                return null;
            });
        return Hub();
    }

    // ---- the brigade ------------------------------------------------------------------

    private static ScenePage Brigade(Player.PlayerController p, Population pop, bool dating)
    {
        var gate = IsoGrid.ToWorld(WorldLayout.NwcGateCell);
        // In the canoe on the river until he steps out of it.
        p.SetStartPosition(gate + new Vector2(340, 300));
        var mates = new List<Npc>();
        string[] names = { "Michel Allary", "Jean Paul" };
        var house = WorldLayout.NwcHouse;
        for (int i = 0; i < names.Length && pop != null && Sim.Nwc != null; i++)
        {
            var v = pop.Spawn(names[i], Role.Voyageur, Faction.NWC, IsoGrid.Cell(house.Top, 1 + i, 4), IsoGrid.Cell(WorldLayout.NwcTop, 6, 6), Sim.Nwc, GD.Load<SpriteFrames>("res://Scenes/Npc/RivalFrames.tres"), new Color(0.85f, 0.75f, 0.75f));
            v.Inventory.Add(ItemId.Capote); v.Inventory.Add(ItemId.Knife); v.Inventory.Add(ItemId.Pemmican, 2);
            v.GlobalPosition = gate + new Vector2(90 + i * 30, 110);
            v.Brain.Push(v, new Agents.Goals.ReportForDutyGoal());
            Shared(v, p, $"{v.DisplayName} and {p.DisplayName} came up from Fort William in the same canoe.");
            mates.Add(v);
        }
        p.AssignTask(Player.TaskKind.Report, 1, $"you are the North West Company's new man");
        string partner = Clean(Sim.Nwc?.Leader?.DisplayName) ?? "the partner";
        string factor = Clean(Sim.Fort?.Leader?.DisplayName) ?? "the Factor";
        string chief = Clean(Sim.Band?.Leader?.DisplayName) ?? "the leading man";
        string a = mates.Count > 0 ? Agents.Barks.CallName(mates[0]) : "Michel", b = mates.Count > 1 ? Agents.Barks.CallName(mates[1]) : "Jean";
        var seen = new HashSet<string>();
        ScenePage Hub()
        {
            var hub = new ScenePage { Title = "Above the last carrying place", Text = seen.Count == 0
                ? $"{Month}. {(dating || GameCalendar.Instance?.Month == 7 ? "The brigade" : "A light canoe")} from Fort William is camped above the last portage, a day's paddle from {partner}'s house. The canoes are turned over for the night; the kettle is on. {a} is mending a paddle. {b} has the cards out. The guide is broaching the keg for a regale."
                : "The fire burns down. The river talks to itself in the dark." };
            if (!seen.Contains("mates")) hub.Add($"Sit with {a}", () => { seen.Add("mates"); return Mates(); });
            if (!seen.Contains("cards")) hub.Add($"Play cards with {b}", () => { seen.Add("cards"); return Cards(); });
            if (!seen.Contains("guide")) hub.Add("Ask the guide about the post", () => { seen.Add("guide"); return Guide(); });
            hub.Add("Sleep. You'll be at the post tomorrow.", () => Arrive());
            return hub;
        }
        ScenePage Mates() => new ScenePage { Title = a, Text = $"{a} shows you how to wrap a cracked blade with spruce root. \"Two winters I've done for the Company. A third and I'll have enough for a farm at Sorel - if the partners pay what they promise.\"" }
            .Add("Help him with the paddle", () => { foreach (var m in mates) m.ChangeRapport(p, 12f); Shared(mates.Count > 0 ? mates[0] : null, p, $"{a} taught {p.DisplayName} to mend a paddle on the brigade."); Note(p, $"You sat with {a} on the brigade; he means to buy a farm at Sorel."); return Hub(); })
            .Add("Back", Hub);
        ScenePage Cards() => new ScenePage { Title = "Cards", Text = $"\"A game, bourgeois?\" {b} grins. \"Three pieces a hand. The winner buys the next regale.\"" }
            .Add("Stake three pieces", () =>
            {
                if (p.Inventory.Count(ItemId.TradeGoods) < 3) return new ScenePage { Title = "Cards", Text = "You haven't three pieces to your name. He laughs and deals you out." }.Add("Back", Hub);
                bool win = GD.Randf() < 0.45f + p.Skills.Trade * 0.2f;
                if (win) { p.Inventory.Add(ItemId.TradeGoods, 3); if (mates.Count > 1) mates[1].ChangeRapport(p, -3f); Note(p, $"You won three pieces from {b} at cards."); }
                else { p.Inventory.Remove(ItemId.TradeGoods, 3); if (mates.Count > 1) { mates[1].ChangeRapport(p, 8f); mates[1].Inventory.Add(ItemId.TradeGoods, 3); } Note(p, $"You lost three pieces to {b} at cards."); }
                return new ScenePage { Title = "Cards", Text = win ? $"You take the pot. {b} laughs, but not much." : $"{b} sweeps the pot. \"You're a good loser, bourgeois. I like a good loser.\"" }.Add("Back", Hub);
            })
            .Add("Not tonight", Hub);
        ScenePage Guide() => new ScenePage { Title = "The guide", Text = $"\"{partner} winters at the house across from the Bay men. He'll want you the moment we land. The Bay men are under {factor}, and they have the band of {chief} trading with them more than with us. The partners at Fort William want that turned round this year.\"" }
            .Add("Thank him", () => { Note(p, $"The guide: report to {partner} on landing; the partners want {chief}'s band turned from {factor}'s Bay men to us this year."); return Hub(); });
        ScenePage Arrive() => new ScenePage { Title = $"{partner}'s house", Text = $"Noon. The brigade sweeps round the last bend singing, and the canoes run up on the landing below the North West Company's house. Across the river the Bay men's flag. Report to {partner}." }
            .Add("Step ashore", () => { Place(p, gate + new Vector2(60, 90)); Note(p, $"You came up with the brigade to {partner}'s house, with {a} and {b}. Report to {partner}."); return null; });
        return Hub();
    }

    // ---- up the Columbia ----------------------------------------------------------------

    // The Pacific Fur Company's first house inland (design doc 7.3): David
    // Stuart's party went up the Columbia from Astoria in the summer of 1811
    // and built Fort Okanogan where the Okanogan comes in. Two of the men
    // with him were Ovide de Montigny, a Canadian, and Naukane, one of the
    // Hawaiians the Tonquin signed on at the islands; both stay at the post.
    private static ScenePage Columbia(Player.PlayerController p, Population pop)
    {
        var post = Posts.Named("Fort Okanogan");
        var gate = post != null ? IsoGrid.ToWorld(post.GateOutside) : p.GlobalPosition;
        p.SetStartPosition(Dry(gate + new Vector2(-260, 220)));
        var mates = new List<Npc>();
        if (pop != null && post?.Ledger != null)
        {
            var frames = GD.Load<SpriteFrames>(Agents.Appearance.VoyageurSheet);
            string[] names = { "Ovide de Montigny", "Naukane" };
            for (int i = 0; i < names.Length; i++)
            {
                var v = pop.Spawn(names[i], Role.Labourer, Faction.American, IsoGrid.Cell(post.House.Top, 3 + i, 3), post.WoodpileCell, post.Ledger, frames, Posts.AmericanTint);
                v.Inventory.Add(ItemId.Capote); v.Inventory.Add(ItemId.Knife); v.Inventory.Add(ItemId.Pemmican, 2);
                v.GlobalPosition = gate + new Vector2(-200 + i * 40, 250);
                Shared(v, p, $"{v.DisplayName} and {p.DisplayName} came up the Columbia from Astoria in Mr. Stuart's canoes.");
                mates.Add(v);
            }
        }
        p.AssignTask(Player.TaskKind.Report, 1, "you are Mr. Astor's new man at Fort Okanogan");
        string master = Clean(post?.Ledger?.Leader?.DisplayName) ?? "Mr. Stuart";
        string a = mates.Count > 0 ? Agents.Barks.CallName(mates[0]) : "Montigny", b = mates.Count > 1 ? mates[1].DisplayName : "Naukane";
        var seen = new HashSet<string>();
        ScenePage Hub()
        {
            var hub = new ScenePage { Title = "Below the Okanogan", Text = seen.Count == 0
                ? $"{Month}. Two months up the Columbia from Astoria: the Cascades, the long portage at the Dalles, the falls where the river people fish from their scaffolds. The canoes are drawn up for the night under the dry hills. {master} is writing up his journal by the fire. {a} is smoking. {b} is singing something from the islands, low, to himself."
                : "The fire burns down. Somewhere up the river a dog barks; there are lodges near." };
            if (!seen.Contains("hawaiian")) hub.Add($"Sit with {b}", () => { seen.Add("hawaiian"); return Islander(); });
            if (!seen.Contains("canadian")) hub.Add($"Ask {a} about the North men", () => { seen.Add("canadian"); return Canadian(); });
            if (!seen.Contains("master")) hub.Add($"Ask {master} what the post is for", () => { seen.Add("master"); return Master(); });
            hub.Add("Sleep. The Okanogan is a morning's paddle.", () => Arrive());
            return hub;
        }
        ScenePage Islander() => new ScenePage { Title = b, Text = $"{b} came aboard the Tonquin at Oahu. \"The ship went north with Captain Thorn and did not come back,\" he says. \"The Indians at Clayoquot say she blew up. Every man. I was lucky - Mr. McDougall kept me at Astoria to cut wood.\" He looks at the river. \"Cold country. But the salmon are good.\"" }
            .Add("Tell him you'll look out for each other", () => { if (mates.Count > 1) mates[1].ChangeRapport(p, 12f); Note(p, $"{b}, from Oahu, came out on the Tonquin before she was lost; you promised to look out for each other."); return Hub(); })
            .Add("Back", Hub);
        ScenePage Canadian() => new ScenePage { Title = a, Text = $"\"The North men?\" {a} spits into the fire. \"Mr. Thompson came down this river in July, past this very spot, with a Union Jack and a paper claiming it all for King George. He breakfasted with us at Astoria, très poli. And they have a house already at Spokane, east of here, with Finan McDonald in it. They were here first, and they mean us to know it.\"" }
            .Add("\"Then we trade better than they do.\"", () => { if (mates.Count > 0) mates[0].ChangeRapport(p, 6f); Note(p, $"{a}: the Nor'Westers have Spokane House east of Okanogan, and David Thompson went down the Columbia in July claiming it for the King."); return Hub(); })
            .Add("Back", Hub);
        ScenePage Master() => new ScenePage { Title = master, Text = $"{master} closes his book. \"Mr. Astor means to have the whole trade of this coast: furs to Canton, tea and silk to New York, and a ship every year to supply us. Astoria is the depot; this is the first post inland. The Okanagan people - the Syilx - live up and down this river. If they trade with us, the house stands. If they take their furs to the North men at Spokane, it doesn't.\"" }
            .Add("Thank him", () => { Note(p, $"{master}: Fort Okanogan is the Pacific Fur Company's first post inland; win the Syilx trade or lose it to the Nor'Westers at Spokane House."); return Hub(); });
        ScenePage Arrive() => new ScenePage { Title = "Fort Okanogan", Text = $"Morning. The canoes turn up the mouth of the Okanogan to the flat where the men have cut the first logs for the house. Smoke from the Syilx lodges across the river. Report to {master}." }
            .Add("Step ashore", () => { Place(p, gate + new Vector2(-60, 90)); Note(p, $"You came up the Columbia to Fort Okanogan with {master}, {a} and {b}. Report to {master}."); return null; });
        return Hub();
    }

    // ---- the camp ---------------------------------------------------------------------

    private static ScenePage Camp(Player.PlayerController p)
    {
        var band = CharacterSheet.BandOf(p);
        var centre = band != null ? band.AccessCell : WorldLayout.CampCentre;
        Place(p, IsoGrid.ToWorld(centre) + new Vector2(50, 40));
        Npc elder = null, cousin = null;
        foreach (var n in Sim.Npcs) if (!n.IsDead && !n.IsFemale && p.KinNames.Contains(n.DisplayName)) { if (elder == null) elder = n; else if (cousin == null) cousin = n; }
        if (cousin == null) foreach (var n in Sim.Npcs) if (!n.IsDead && n != elder && p.KinNames.Contains(n.DisplayName)) { cousin = n; break; }
        elder ??= band?.Leader;
        string e = elder != null ? Agents.Barks.CallName(elder) : "your uncle", c = cousin != null ? Agents.Barks.CallName(cousin) : "your cousin";
        string factor = Clean(Sim.Fort?.Leader?.DisplayName) ?? "the Bay men's master";
        string partner = Clean(Sim.Nwc?.Leader?.DisplayName) ?? "the Canadian";
        string people = World.Camps.NationName(p.Faction);
        // By the Bay, or out in the country with its own traders (World/Camps).
        var camp = World.Camps.Named(p.HomeCamp);
        string post = camp != null && !string.IsNullOrEmpty(camp.Post) ? camp.Post : "the nearest post";
        string where = camp != null ? $"stand at {camp.Name}, far from the Bay" : WorldLayout.IsSummerCamp ? "stand on the trail by the Bay men's house for the trading" : "stand in the winter camp in the bush";
        string toPost = camp != null ? $"down to {post}" : "down to the Bay house";
        string kegMan = camp != null ? $"A trader's man from {post}" : "A Canadian from across the river";
        if (camp != null) { factor = $"The master at {post}"; partner = $"The master at {post}"; }
        string arrive = camp != null ? $"The day is yours. {char.ToUpper(post[0]) + post.Substring(1)} is days away; your own country is all around you." : "The day is yours. The Bay men's house is a short walk down the trail; the Canadians' across the river.";

        var seen = new HashSet<string>();
        ScenePage Hub()
        {
            var hub = new ScenePage { Title = "Morning in the camp", Text = seen.Count == 0
                ? $"{Month}. The {people} lodges {where}. Smoke, dogs, children at the river. {e} is sitting outside his lodge with the winter's furs bundled at his feet. {c} is restringing a bow. {kegMan} has come into the camp with a keg."
                : "The camp goes about its morning." };
            if (!seen.Contains("elder")) hub.Add($"Sit with {e}", () => { seen.Add("elder"); return Elder(); });
            if (!seen.Contains("cousin")) hub.Add($"Go with {c} to look at the snares", () => { seen.Add("cousin"); return Cousin(); });
            if (!seen.Contains("keg")) hub.Add(camp != null ? "See what the trader's man wants" : "See what the Canadian wants", () => { seen.Add("keg"); return Keg(); });
            hub.Add("Go about the day", () => Arrive());
            return hub;
        }
        ScenePage Elder() => new ScenePage { Title = e, Text = $"\"Take these {toPost},\" {e} says, pushing the bundle toward you. \"Four good beaver. Bring back powder and a kettle. {factor} will try to give you less than they're worth, and {(camp != null ? "their men" : "the Canadians")} will try to give you rum. Don't let either of them make a fool of you.\"" }
            .Add("Take the furs", () =>
            {
                p.Inventory.Add(ItemId.BeaverPelt, 4);
                if (elder != null) { elder.ChangeRapport(p, 5f); Favours.Ask(elder, p, FavourKind.TradeForKin, 8, toPost); }
                Note(p, $"{e} gave you four beaver to take {toPost} for powder and a kettle.");
                return Hub();
            });
        ScenePage Cousin() => new ScenePage { Title = c, Text = $"Two hares in the snares and a third torn loose by a fox. {c} talks about a girl in the other band and whether her father would ever look at him." }
            .Add("Promise to speak for him", () => { if (cousin != null) { cousin.ChangeRapport(p, 10f); Shared(cousin, p, $"{cousin.DisplayName} and {p.DisplayName} checked the snares together and talked of marriage."); } Note(p, $"You promised {c} you'd speak for him to the girl's father."); return Hub(); })
            .Add("Tease him", () => { if (cousin != null) cousin.ChangeRapport(p, 3f); return Hub(); });
        ScenePage Keg() => new ScenePage { Title = "The Canadian", Text = $"\"{partner} sends his compliments,\" the Canadian says, and pours a cup from the keg. \"There's more where this came from for a man who brings his furs across the river instead of to the Bay house.\"" }
            .Add("Drink with him", () => { p.Inventory.Add(ItemId.Rum, 1); p.Consume(ItemId.Rum); Note(p, $"A Nor'Wester came into the camp with rum from {partner}, for whoever takes their furs across the river."); return Hub(); })
            .Add("Send him away", () => { if (elder != null) elder.ChangeRapport(p, 4f); Note(p, $"You sent {partner}'s man and his keg out of the camp."); return Hub(); });
        ScenePage Arrive() => new ScenePage { Title = "The camp", Text = arrive }
            .Add("Go", () => null);
        return Hub();
    }

    // ---- the parade -------------------------------------------------------------------

    // A private soldier of a Crown garrison on the Lakes frontier (design doc
    // 6.1's towns). The King's birthday, 4 June, was a holiday in every
    // garrison: the troops paraded in their best and fired a feu de joie.
    // Desertion across the water was the garrisons' standing trouble - the
    // Americans offered a recruit land, a hundred and sixty acres - and a man
    // brought back was flogged before his regiment.
    private static ScenePage Parade(Player.PlayerController p, Vector2? at)
    {
        var town = p.GarrisonTown;
        var g = town?.Garrison;
        Vector2 parade = town != null && town.HasFort ? Dry(IsoGrid.ToWorld(IsoGrid.Cell(town.FortTop, town.FortWidth / 2, town.FortDepth / 2))) : at ?? p.GlobalPosition;
        p.SetStartPosition(parade);
        // The men of his mess, and the sergeant: the garrison's own people, placed in the editor.
        var mates = new List<Npc>();
        Npc sergeant = null;
        if (g != null)
            foreach (var n in g.Members)
            {
                if (!GodotObject.IsInstanceValid(n) || n.IsDead || n.Away || n.Role != Role.Soldier) continue;
                if (sergeant == null && n.DisplayName.StartsWith("Sergeant")) sergeant = n;
                else if (mates.Count < 2 && n.DisplayName.StartsWith("Private")) mates.Add(n);
            }
        foreach (var m in mates) Shared(m, p, $"{m.DisplayName} and {p.DisplayName} have shared a mess in the barracks since the winter.");
        string officer = Clean(g?.Leader?.DisplayName) ?? "the commanding officer";
        string sgt = Clean(sergeant?.DisplayName) ?? "the sergeant";
        string a = mates.Count > 0 ? Agents.Barks.CallName(mates[0]) : "Doyle", b = mates.Count > 1 ? Agents.Barks.CallName(mates[1]) : "Kelly";
        string fort = town?.FortName ?? "the fort", place = town?.Name ?? "Upper Canada";
        string over = town?.Name switch
        {
            "Niagara" or "Queenston" => "Fort Niagara across the river",
            "Amherstburg" => "the Michigan shore",
            "St. Joseph Island" => "Michilimackinac",
            _ => "the American shore across the lake",
        };
        var cal = GameCalendar.Instance;
        bool birthday = cal != null && cal.Month == 6 && cal.Day == 4;
        string occasion = birthday ? "the King's birthday" : "the general muster";
        var seen = new HashSet<string>();
        ScenePage Hub()
        {
            var hub = new ScenePage { Title = birthday ? "The King's birthday" : "On the parade", Text = seen.Count == 0
                ? $"{Month}. {fort}, {place}. The garrison is drawn up on the parade in its best coats for {occasion}: pipeclay and polish, powder served out for the feu de joie, the colours uncased. {a} and {b} stand either side of you in the rear rank. {Cap(sgt)} walks the front of the company with his pike. Beyond the palisade the townspeople have come out to watch, and one of them is watching you."
                : "The company stands easy. The sun is high and the pipeclay itches." };
            if (!seen.Contains("mess")) hub.Add($"Stand easy with {a} and {b}", () => { seen.Add("mess"); return Mess(); });
            if (!seen.Contains("sergeant")) hub.Add($"Listen to {sgt}", () => { seen.Add("sergeant"); return Sergeant(); });
            if (!seen.Contains("stranger")) hub.Add("See who is watching you", () => { seen.Add("stranger"); return Stranger(); });
            hub.Add("Fall in for the feu de joie", () => Dismissed());
            return hub;
        }
        ScenePage Mess() => new ScenePage { Title = "The rear rank", Text = $"\"A shilling a day,\" {a} says out of the side of his mouth, \"and sixpence of it stopped for the rations before you see it, and the rest for the shoes and the washing.\" {b} nods at {over}. \"Over there they give a man land for enlisting. A hundred and sixty acres. A man could be a farmer.\"" }
            .Add("\"Hold your tongues, the pair of you.\"", () => { foreach (var m in mates) m.ChangeRapport(p, 4f); return Hub(); })
            .Add("\"And how would a man get over there?\"", () =>
            {
                Remembered(p, $"{p.DisplayName} asked how a man would get across to the Americans.", mates, 1.3f);
                Note(p, $"{a} and {b} grumbled about the stoppages; the Americans at {over} give a recruit a hundred and sixty acres. You asked how a man would get over.");
                return new ScenePage { Title = "The rear rank", Text = $"{b} looks at you sideways. \"Not out loud, you wouldn't.\"" }.Add("Back", Hub);
            });
        ScenePage Sergeant() => new ScenePage { Title = Cap(sgt), Text = $"\"{officer} will inspect after the firing, and God help the man with a dirty lock. Any man drunk tonight is for the triangle in the morning. And mark this, the lot of you: the last man who tried for {over} was brought back in irons and flogged till the drummers were tired. Desert, and we will fetch you.\"" }
            .Add("\"Sergeant.\"", () => { Note(p, $"{Cap(sgt)}: {officer} inspects after the firing; a man who deserts is fetched back and flogged."); return Hub(); });
        ScenePage Stranger() => new ScenePage { Title = "By the gate", Text = $"A man in a farmer's coat by the gate catches your eye and drifts over when the sergeant's back is turned. \"There's a boat goes over from below {place} on dark nights,\" he says, low. \"Two dollars, and a coat that isn't red. Ask at the landing for Abner.\" He is gone into the crowd before you can answer." }
            .Add("Remember the name", () => { Campaign.Flag("start:knows-the-boatman"); Note(p, $"A stranger at the gate of {fort}: a boat goes over to {over} from below {place} on dark nights. Ask at the landing for Abner."); return Hub(); })
            .Add($"Tell {sgt}", () => { p.Standing += 3f; Note(p, $"You told {sgt} of a man at the gate who offered a boat across to {over}."); return new ScenePage { Title = Cap(sgt), Text = "He looks at the crowd a long time. \"Good lad. I'll remember that.\"" }.Add("Back", Hub); })
            .Add("Forget it", () => Hub());
        ScenePage Dismissed() => new ScenePage { Title = fort, Text = $"The line fires by files, a ripple of smoke from one end of the company to the other and back, three times, and three cheers for the King. {officer} rides down the ranks. Then the parade is dismissed. You are on the strength of {fort}: a bed in the barracks, the garrison's rations, and {officer} over all of you." }
            .Add("Fall out", () =>
            {
                bool elsewhere = at is Vector2 there && there.DistanceTo(parade) > 1500f;
                Place(p, elsewhere ? at.Value : parade + new Vector2(40, 30));
                Note(p, $"You paraded with the garrison of {fort} for {occasion}.{(elsewhere ? $" You are sent to {Campaign.StartAt}." : "")}");
                return null;
            });
        return Hub();
    }

    // A campaign's start_at: set down there, on the first day, with no prologue.
    private static ScenePage SetDown(Player.PlayerController p, Vector2 there)
    {
        string where = Campaign.StartAt;
        p.SetStartPosition(Dry(there));
        // On a company's road, a house of his own company that keeps books is his post from the first day.
        if (!CharacterSheet.IsBand(p.StartPath) && Posts.Named(where) is { } post && post.Faction == p.Faction && Convoys.Ledger(where) != null) p.PostedTo = where;
        string who = p.StartPath switch
        {
            StartPath.HBC => "a servant of the Hudson's Bay Company",
            StartPath.NWC => "a man of the North West Company",
            StartPath.American => "Mr. Astor's man",
            _ => $"one of the {Camps.NationName(p.Faction)}",
        };
        return new ScenePage { Title = where, Text = $"{Month} {GameCalendar.Instance?.Year}. {where}. You are {who}, and this is where your story begins." }
            .Add("Begin", () => { Place(p, there); Note(p, $"You began at {where}, {who}."); return null; });
    }

    private static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

    // ---- helpers ------------------------------------------------------------------------

    // Put a man down on ground he can stand on. The map has been redrawn
    // twice since these starts were written, and a hand-picked pixel offset
    // from the wharf was dropping the Company's new hand into the Hayes: he
    // began in the river, where he could not move at all.
    private static void Place(Player.PlayerController p, Vector2 at)
    {
        p.SetStartPosition(Dry(at));
    }

    // Never leave a man standing on water he cannot walk on: if the prologue
    // has closed and he is still afloat, put him ashore.
    private static void Strand(Player.PlayerController p, int tries)
    {
        if (!GodotObject.IsInstanceValid(p) || tries > 30) return;
        p.GetTree().CreateTimer(4.0).Timeout += () =>
        {
            if (!GodotObject.IsInstanceValid(p)) return;
            bool afloat = Sim.Paths != null && !Sim.Paths.IsWalkable(p.Cell);
            if (afloat && !p.Conversation.IsOpen && p.Aboard == null)
            {
                p.SetStartPosition(Dry(p.GlobalPosition));
                p.Say("You step off onto the planks.");
                return;
            }
            if (afloat || p.Conversation.IsOpen) Strand(p, tries + 1);
        };
    }

    // The nearest cell that is neither water nor blocked, as a world point.
    private static Vector2 Dry(Vector2 at)
    {
        var cell = IsoGrid.FromWorld(at);
        bool wet = Sim.Terrain != null && Sim.Terrain.IsWaterOrIce(cell);
        if (!wet && (Sim.Paths == null || Sim.Paths.IsWalkable(cell))) return at;
        var found = Sim.Paths?.NearestWalkable(cell, 10);
        if (found == null) return at;
        // NearestWalkable does not care about water; keep looking if it is wet.
        var c = found.Value;
        if (Sim.Terrain != null && Sim.Terrain.IsWaterOrIce(c))
            for (int r = 1; r <= 12 && Sim.Terrain.IsWaterOrIce(c); r++)
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -2 * r; dy <= 2 * r; dy++)
                    {
                        var n = new Vector2I(cell.X + dx, cell.Y + dy);
                        if (Sim.Terrain.IsWaterOrIce(n)) continue;
                        if (Sim.Paths != null && !Sim.Paths.IsWalkable(n)) continue;
                        c = n; r = 99; dx = r; dy = 2 * r; break;
                    }
        return IsoGrid.ToWorld(c);
    }

    // A firsthand memory two people share from before the game - the stuff
    // the friend system (and the bond) is made of.
    private static void Shared(Npc n, Agent p, string text)
    {
        if (n == null) return;
        n.Memories.Add(new MemoryEntry { Text = text, Subject = p.DisplayName, Day = Mathf.PosMod((GameCalendar.Instance?.DayOfYear ?? 0f) - 1f, 365f), Directness = 1, Weight = 1.2f, Key = $"start{n.DisplayName}{text.GetHashCode()}", Witnesses = new List<string> { n.DisplayName, p.DisplayName } });
        n.ChangeRapport(p, 12f);
    }

    // Something the people around you saw you do before you landed.
    private static void Remembered(Agent p, string text, List<Npc> by, float weight)
    {
        var e = new MemoryEntry { Text = text, Actor = p, Subject = p.DisplayName, Day = Mathf.PosMod((GameCalendar.Instance?.DayOfYear ?? 0f) - 1f, 365f), Directness = 1, Weight = weight, Key = $"start{text.GetHashCode()}", Witnesses = new List<string> { p.DisplayName } };
        foreach (var n in by) { e.Witnesses.Add(n.DisplayName); n.Remember(e); }
    }

    private static void Note(Player.PlayerController p, string line) => p.Journal.Add($"{GameCalendar.Instance?.DateString}: {line}");

    // "Alex Kennedy (clerk)" reads as "Alex Kennedy" in a sentence.
    private static string Clean(string name) => name == null ? null : System.Text.RegularExpressions.Regex.Replace(name, @"\s*\([^)]*\)", "");

    private static string RivalClerk(Economy.Household h)
    {
        Npc best = null;
        foreach (var n in Sim.Npcs) if (!n.IsDead && n.HomeStores == h && n.Role == Role.Clerk && (best == null || n.Personality.Ambition > best.Personality.Ambition)) best = n;
        return Clean(best?.DisplayName) ?? "the senior clerk";
    }
}
