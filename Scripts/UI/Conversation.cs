using System;
using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.Player;

namespace IsometricFurTradeGame.Scripts.UI;

// "Talk" (design doc 3.6): a topic menu built from what this particular
// person is, wants, and thinks of you. Runs on the server, where the
// people actually live; the client only shows the text and the choices.
public class Conversation
{
    private readonly PlayerController _player;
    private Npc _npc;
    private string _text = "";
    private bool _closed = true;
    private readonly List<(string label, Action action)> _options = new();

    public Conversation(PlayerController player) { _player = player; }

    public bool IsOpen => !_closed && (_stores != null || _pile != null || _canoe != null || _npc != null && GodotObject.IsInstanceValid(_npc));

    // The storeroom chest as a counterpart with no face (design doc 12.5).
    private Economy.FortLedger _stores;
    public const int StoresId = -2;
    public Godot.Collections.Dictionary StartStores(Economy.FortLedger fort)
    {
        if (_npc != null && GodotObject.IsInstanceValid(_npc)) _npc.Conversing = false;
        _npc = null; _stores = fort; _closed = false;
        ShowStores();
        return State();
    }

    // A woodpile: yours or somebody else's (design doc 16.3 in firewood).
    private Economy.Household _pile;
    public const int PileId = -4;
    public Godot.Collections.Dictionary StartWoodpile(Economy.Household pile)
    {
        if (_npc != null && GodotObject.IsInstanceValid(_npc)) _npc.Conversing = false;
        _npc = null; _stores = null; _pile = pile; _closed = false;
        ShowWoodpile();
        return State();
    }

    private void ShowWoodpile()
    {
        var pile = _pile; bool mine = pile == _player.HomePost;
        _text = $"{pile.Title}'s woodpile: {pile.Stores.Count(ItemId.Firewood)} firewood.{(mine ? "" : " Not yours. Anyone who sees you take from it will remember.")}";
        ClearOptions();
        int have = _player.Inventory.Count(ItemId.Firewood);
        if (have > 0) AddOption(mine ? $"Stack your {have} firewood" : $"Stack your {have} firewood on their pile (a gift)", () =>
        {
            _player.Inventory.Remove(ItemId.Firewood, have); pile.Stores.Add(ItemId.Firewood, have);
            if (mine) _player.Task.Progress(TaskKind.Firewood, have);
            else { foreach (var a in Sim.AgentsWithin(_player.GlobalPosition, 650f)) if (a is Npc w && w.HomeStores == pile) w.ChangeRapport(_player, 3f); Chronicle.Event($"{_player.DisplayName} brought wood to {pile.Title}'s pile.", _player.GlobalPosition, _player, weight: 0.5f); }
            _text = $"You stack {have} firewood ({pile.Stores.Count(ItemId.Firewood)} there now)."; ShowWoodpile();
        });
        AddOption(mine ? "Take 2 firewood" : "Take 2 firewood (theft)", () =>
        {
            int take = pile.Stores.TakeUpTo(ItemId.Firewood, 2);
            if (take == 0) { _text = "The pile is bare."; ShowWoodpile(); return; }
            _player.Inventory.Add(ItemId.Firewood, take);
            if (!mine)
            {
                Chronicle.Event($"{_player.DisplayName} took wood from {pile.Title}'s pile.", _player.GlobalPosition, _player, weight: 0.8f);
                foreach (var a in Sim.AgentsWithin(_player.GlobalPosition, 650f)) if (a is Npc w && w.HomeStores == pile) w.ChangeRapport(_player, -6f);
                if (pile is Economy.BandLedger b) b.StandingWithCompany = Mathf.Max(0f, b.StandingWithCompany - 1f);
            }
            _text = $"You take {take} firewood."; ShowWoodpile();
        });
        AddOption("Leave the pile", Close);
    }

    // A canoe drawn up on the bank (design doc 12): take her, or load her.
    private World.Canoe _canoe;
    public const int CanoeId = -5;
    public Godot.Collections.Dictionary StartCanoe(World.Canoe canoe)
    {
        if (_npc != null && GodotObject.IsInstanceValid(_npc)) _npc.Conversing = false;
        _npc = null; _stores = null; _pile = null; _canoe = canoe; _closed = false;
        ShowCanoe();
        return State();
    }

    private void ShowCanoe()
    {
        var c = _canoe;
        _text = $"A canoe on the bank. She carries {c.LoadLbs:0} of {World.Canoe.CapacityLbs:0} lb{(c.Overloaded ? " and rides dangerously low" : "")}.{(c.Cargo.SlotCount > 0 ? " In her: " + c.Cargo.Summary() : "")}";
        ClearOptions();
        if (_player.Aboard == null) AddOption("Push off", () => { Close(); Net.Net.I?.Act("canoe"); });
        AddOption("Load your goods and furs into her", () => { _text = _player.CanoeLoad(c, true); ShowCanoe(); });
        if (c.Cargo.SlotCount > 0) AddOption("Take everything out", () => { _text = _player.CanoeLoad(c, false); ShowCanoe(); });
        AddOption("Leave her", Close);
    }

    private void ShowStores()
    {
        var fort = _stores;
        _text = $"The storeroom. {fort.Stores.Summary()}";
        ClearOptions();
        AddOption("Stow your goods and furs", () => { int n = _player.StowInto(fort, false); _text = n > 0 ? $"You stow {n} lots." : "Nothing to stow: goods and furs go in here."; ShowStores(); });
        AddOption("Stow provisions for the mess (meat, fish, geese; you keep two meals)", () => { int n = _player.StowInto(fort, true); _text = n > 0 ? $"You hand in {n} of meat. The Factor will hear of it." : "Nothing to hand in."; ShowStores(); });
        bool trusted = _player.Rank == "clerk" || _player.Rank == "postmaster" || _player.Standing >= 15f;
        void Draw(ItemId id, int n, string why = null, bool ok = true)
        {
            if (!fort.Stores.Has(id)) return;
            string label = $"Draw {(n > 1 ? n + " " : "a ")}{Items.Items.Of(id).Name.ToLower()}{(n > 1 ? "" : "")} ({fort.Stores.Count(id)} in store)";
            if (!ok) { AddOption(label + $" - {why}", () => { _text = $"\"{why}\" You leave it."; ShowStores(); }); return; }
            AddOption(label, () => { int got = fort.Stores.TakeUpTo(id, n); _player.Inventory.Add(id, got); Chronicle.Event($"{_player.DisplayName} drew {Items.Items.Of(id).Name.ToLower()} from the stores.", _player.GlobalPosition, _player, weight: 0.3f); _text = $"You take {Items.Items.Of(id).Name.ToLower()} from the chest."; ShowStores(); });
        }
        Draw(ItemId.Musket, 1, "muskets are for men the Factor trusts (standing 15)", trusted && !_player.Inventory.Has(ItemId.Musket));
        Draw(ItemId.SteelTrap, 2);
        Draw(ItemId.Axe, 1, ok: !_player.Inventory.Has(ItemId.Axe));
        Draw(ItemId.Snowshoes, 1, ok: !_player.Inventory.Has(ItemId.Snowshoes));
        Draw(ItemId.PointBlanket, 1, "blankets are trade stock (standing 10)", _player.Standing >= 10f);
        Draw(ItemId.Kettle, 1, ok: !_player.Inventory.Has(ItemId.Kettle));
        Draw(ItemId.Tumpline, 1, ok: !_player.Inventory.Has(ItemId.Tumpline));
        Draw(ItemId.TradeGoods, 4, "trade goods are the clerk's to count out (rank clerk)", _player.Rank == "clerk" || _player.Rank == "postmaster");
        AddOption("Take a ration", () => { _text = fort.TakeRation(_player, 2) ? "You draw two days' ration." : "The shelves are bare."; ShowStores(); });
        AddOption("Close the chest", Close);
    }

    public Godot.Collections.Dictionary Start(Npc npc)
    {
        if (_npc != null && GodotObject.IsInstanceValid(_npc) && _npc != npc) _npc.Conversing = false;
        _npc = npc; _stores = null; _pile = null; _canoe = null;
        _closed = false;
        npc.Conversing = true;
        npc.StopMoving();
        npc.FaceToward(_player.GlobalPosition);
        if (npc.Role == Role.Factor && _player.Task.Kind == TaskKind.Report && !_player.Task.IsComplete)
        {
            _player.Task.Progress(TaskKind.Report, 1);
            _player.Standing += 3f;
            Chronicle.Event($"{_player.DisplayName} reported to {npc.DisplayName}.", npc.GlobalPosition, _player, weight: 0.4f);
        }
        ShowTopics(Greeting(npc));
        return State();
    }

    // The player picked option i (or -1 to walk away).
    public Godot.Collections.Dictionary Step(int npcId, int option)
    {
        if (_canoe != null && npcId == CanoeId && !_closed)
        {
            if (option < 0 || option >= _options.Count) { Close(); return State(); }
            _options[option].action();
            return State();
        }
        if (_pile != null && npcId == PileId && !_closed)
        {
            if (option < 0 || option >= _options.Count) { Close(); return State(); }
            _options[option].action();
            return State();
        }
        if (_stores != null && npcId == StoresId && !_closed)
        {
            if (option < 0 || option >= _options.Count) { Close(); return State(); }
            _options[option].action();
            return State();
        }
        if (_npc == null || !GodotObject.IsInstanceValid(_npc) || _npc.NetId != npcId || _closed) { Close(); return State(); }
        if (_npc.IsDead || _npc.GlobalPosition.DistanceTo(_player.GlobalPosition) > 220f) { Close(); return State(); }
        if (option < 0 || option >= _options.Count) { Close(); return State(); }
        _options[option].action();
        return State();
    }

    private void Close()
    {
        if (_npc != null && GodotObject.IsInstanceValid(_npc)) _npc.Conversing = false;
        _stores = null; _pile = null; _canoe = null;
        _closed = true;
    }

    public Godot.Collections.Dictionary State()
    {
        var opts = new Godot.Collections.Array();
        foreach (var (label, _) in _options) opts.Add(label);
        return new Godot.Collections.Dictionary
        {
            ["closed"] = _closed, ["npc"] = _canoe != null ? CanoeId : _pile != null ? PileId : _stores != null ? StoresId : _npc != null && GodotObject.IsInstanceValid(_npc) ? _npc.NetId : -1,
            ["title"] = _canoe != null ? "The canoe" : _pile != null ? "The woodpile" : _stores != null ? _stores.Title : _npc != null && GodotObject.IsInstanceValid(_npc) ? _npc.DisplayName : "", ["text"] = _text, ["options"] = opts,
        };
    }

    private string Greeting(Npc npc)
    {
        float r = npc.RapportWith(_player);
        string mood = r > 40 ? "warmly" : r > 10 ? "civilly" : r < -20 ? "coldly" : "";
        string role = npc.Role switch
        {
            Role.Factor => "the Factor, master of this post",
            Role.Clerk => "a clerk of the Company",
            Role.Postmaster => "postmaster of Oxford House, the Company's inland post",
            Role.Labourer => "a Company labourer",
            Role.Woman => "a woman of the post",
            Role.CreeChief => "leading man of the band camped by the river",
            Role.CreeHunter => "a hunter of the band",
            Role.CreeWoman => "a woman of the band",
            Role.CreeDaughter => npc.Father != null ? $"{npc.Father.DisplayName}'s daughter" : "a young woman of the band",
            Role.Sailor => "one of the ship's people",
            Role.NwcPartner => "wintering partner of the North West Company, master of the post across the river",
            Role.Voyageur => "a voyageur of the North West Company",
            Role.Wife => "your wife",
            _ => "",
        };
        string busy = npc.Tier == Core.Tier.Background ? " They have been about their own work." : npc.Brain.Top != null ? $" They were {npc.Brain.Top.Label}." : "";
        string debt = "";
        foreach (var g in Grudges.HeldBy(npc.DisplayName)) if (g.Against == _player.DisplayName) { debt = $" There is blood between you: {g.Victim}. {(npc.IsFighter ? "Their hand is on their knife." : "They will hear what you offer for it, or nothing.")}"; break; }
        if (debt == "" && _player.Spouse is Npc wife && npc.IsBandPerson)
            foreach (var g in Grudges.HeldBy(npc.DisplayName)) { debt = $" Your wife's people have a debt unanswered: {g.Against} killed {g.Victim}. As kin, it is yours to help settle."; break; }
        string tongue = npc.IsBandPerson && _player.FluencyWith(npc) < 0.4f ? (_player.HasInterpreterFor(npc) ? "" : " You have little of their tongue; you get by with signs and a few words.") : "";
        string state = npc.IsSick ? " They are grey with fever - keep your distance." : npc.HasScurvy ? " Their gums are black with scurvy." : npc.IsDrunk ? " They are drunk." : npc.Needs.Hunger < 35 ? " They look hungry." : npc.Needs.Warmth < 35 ? " They are shivering." : npc.Needs.Health < 50 ? " They are hurt." : "";
        // Two more things a face shows (design doc 3.5): a man on his guard,
        // and one who has just buried somebody of his house.
        if (state == "" && npc.Alerted > 0f && npc.Weapon != null) state = " He has not let go of his musket; something has him on his guard.";
        if (state == "") foreach (var l in Losses.All) if (l.Home == npc.HomeStores && l.Fresh) { state = $" His house is still short of {l.Name}; it is in his face."; break; }
        if (npc.IsBandPerson && !_player.HasInterpreterFor(npc)) _player.Learn(npc, 0.02f);   // every talk teaches a little
        return $"{npc.DisplayName}, {role}, greets you{(mood == "" ? "" : " " + mood)}.{busy}{state}{debt}{tongue}";
    }

    private void ShowTopics(string text)
    {
        _text = text;
        ClearOptions();
        var npc = _npc;
        bool leader = npc.Role == Role.Factor || npc.Role == Role.NwcPartner || npc.Role == Role.Postmaster;
        bool ownLeader = leader && npc.Faction == _player.Faction;
        bool ownMan = npc.Faction == _player.Faction && (npc.Role == Role.Labourer || npc.Role == Role.Voyageur);
        bool canCommand = _player.Rank == "clerk" || _player.Rank == "postmaster" || _player.Standing >= 35f;

        // What's pressing comes first: a letter to hand over, a debt to settle.
        if (npc.Role == Role.CreeChief && (_player.Task.Kind == TaskKind.DeliverLetter && !_player.Task.IsComplete && _player.Task.Done == 0 || HasMailFor(npc)))
            AddOption("Hand him the Factor's letter", () => ShowTopics(DeliverLetter(npc)));
        foreach (var m in new List<Message>(_player.Mail))
        {
            if (m.Read) continue;
            bool addressed = m.To == npc.DisplayName || m.To == Mail.RoleName(npc);
            if (addressed && npc.Role != Role.CreeChief) AddOption($"Hand over the {m.Kind} from {m.From}", () => ShowTopics(HandOver(npc, m)));
        }
        foreach (var g in new List<Grudge>(Grudges.HeldBy(npc.DisplayName)))
        {
            bool mine = g.Against == _player.DisplayName;
            bool kin = _player.Spouse is Npc && npc.IsBandPerson;
            if (mine || kin) AddOption($"Offer restitution for {g.Victim} ({g.DebtMb:0} trade goods{(mine ? "" : $", on {g.Against}'s behalf")})", () => ShowTopics(Restitution(npc, g)));
        }

        // A dead man's effects for sale among the men this evening (design doc P.8).
        if (ownLeader && Losses.OpenSaleAt(npc.HomeStores) is Loss sale)
            AddOption($"Bid at the sale of {sale.Name}'s effects...", () => ShowSale(npc, sale));
        // An evening at the fire: tell them something you saw.
        if (npc.AtHearth != null && _player.GlobalPosition.DistanceTo(npc.AtHearth.GlobalPosition) < 300f && StoriesToTell(npc.AtHearth).Count > 0)
            AddOption("Tell a story at the fire...", () => ShowStories(npc));
        AddOption("Talk...", () => ShowTalk(npc));
        if (ownLeader || (ownMan && canCommand) || npc.Following == _player || CanAskAlong(npc)) AddOption("Work and orders...", () => ShowWork(npc));
        if (npc.Role == Role.Clerk || npc.Role == Role.Factor || npc.Role == Role.CreeChief || npc.Role == Role.NwcPartner || npc.Role == Role.Postmaster)
            AddOption("Trade", () => ShowTrade(npc));
        if (npc.IsFemale && _player.Needs.Health < 70f && !_player.IsDead)
            AddOption("Ask her to see to your wounds", () => ShowTopics(Treat(npc)));
        AddOption("Give a gift", () => ShowGive(npc));
        if (npc.Needs.Hunger < 45f && _player.Inventory.FirstOfKind(ItemKind.Food) is ItemId food)
            AddOption($"Offer them something to eat ({Items.Items.Of(food).Name})", () => ShowTopics(OfferFood(npc, food)));
        if (npc.IsBandPerson && (npc.Role == Role.CreeChief || npc.Role == Role.CreeHunter)) AddOption("Family and marriage...", () => ShowFamily(npc));
        if (leader && (Economy.Rivalry.AreRivals(npc.Faction, _player.Faction) || ownLeader && Sim.Nwc != null)) AddOption("The companies...", () => ShowCompanies(npc));
        // Held by the other side (design doc 9.1): buy your way out, or give your word.
        if (leader && _player.CaptiveOf != null && Economy.Rivalry.PostOf(npc.Faction) == _player.CaptiveOf)
        {
            AddOption("Offer a ransom for your freedom", () => ShowTopics(Ransom(npc)));
            AddOption("Give your parole: you'll carry no arms against them this year", () => ShowTopics(Parole(npc)));
        }
        if (ownLeader && _player.ParoleUntilDay > GameCalendar.Instance.Day)
            AddOption("Confess you gave your parole to the other side", () => ShowTopics(Confess(npc)));
        if (npc.Faction != Faction.HBC || npc.Role == Role.Labourer)
            AddOption("Threaten them", () => ShowTopics(Threaten(npc)));
        AddOption("Leave", Close);
    }

    private bool CanAskAlong(Npc npc) => !npc.IsFemale && npc.Role != Role.Factor && npc.Role != Role.NwcPartner && npc.Role != Role.Clerk && npc.Role != Role.Sailor;

    private void ShowTalk(Npc npc, string said = null)
    {
        _text = said ?? $"{npc.DisplayName} waits to hear what you want to know.{Bored(npc)}";
        ClearOptions();
        bool canTalk = !npc.IsBandPerson || _player.FluencyWith(npc) >= 0.3f;   // design doc 10.6
        AddOption("How do things stand?", () => ShowTalk(npc, AskNews(npc)));
        AddOption("Tell me about yourself", () => ShowTalk(npc, AskAboutThem(npc)));
        AddOption("What's the talk? (rumours)", () => ShowTalk(npc, AskGossip(npc)));
        if ((npc.IsBandPerson && npc.Role is Role.CreeChief or Role.CreeHunter) || npc.Role is Role.Labourer or Role.Voyageur or Role.NwcPartner)
            AddOption("Where is the game?", () => ShowTalk(npc, AskHunting(npc)));
        if (npc.Role == Role.Factor && Sim.Fort != null)
        {
            int news = 0;
            foreach (var sname in _player.KnownSites) if (!Sim.Fort.Knows(sname)) news++;
            if (news > 0) AddOption($"Tell him where the game is ({news} places)", () => ShowTalk(npc, TellSites(npc)));
        }
        if ((npc.Role == Role.Factor || npc.Role == Role.NwcPartner || npc.Role == Role.Postmaster) && npc.Faction == _player.Faction)
            AddOption(npc.Role == Role.Factor ? "What does London want of us?" : "What do the partners want of us?", () => ShowTalk(npc, AskDirective(npc)));
        if (npc.Role == Role.CreeChief && npc.HomeStores is Economy.BandLedger cb && cb.OtherBand != null && cb.StandingWithOtherBand < 60f && canTalk)
            AddOption($"Speak for peace with {cb.OtherBand.Name}", () => ShowTalk(npc, BrokerPeace(npc, cb)));
        if (npc.Role == Role.NwcPartner && Sim.Nwc != null && Sim.Nwc.DirectiveReadDay > -100 && npc.Faction == _player.Faction)
            AddOption("What did the council decide?", () => ShowTalk(npc, $"\"{Sim.Nwc.DirectiveText}\""));
        if (npc.IsBandPerson && !canTalk) _text += " (You have only signs and a few words with them.)";
        AddOption("Back", () => ShowTopics(Greeting(npc)));
    }

    // ---- the sale of a dead man's effects (design doc P.8) -----------------------

    private void ShowSale(Npc npc, Loss sale, string said = null)
    {
        _text = said ?? $"{npc.DisplayName} holds up what {sale.Name} left, one thing at a time. You have {_player.Inventory.Count(ItemId.TradeGoods)} pieces of goods.";
        ClearOptions();
        if (sale.AuctionOpen)
            for (int i = 0; i < sale.Lots.Count; i++)
            {
                int lot = i;
                var (id, n) = sale.Lots[i];
                AddOption($"His {Items.Items.Of(id).Name.ToLower()}{(n > 1 ? $" x{n}" : "")} - {Losses.PriceOf(id, n)} pieces", () => ShowSale(npc, sale, Losses.Buy(_player, sale, lot)));
            }
        AddOption("Back", () => ShowTopics(Greeting(npc)));
    }

    // ---- telling at the fire ---------------------------------------------------------

    // Things you saw yourself that someone sitting at this fire hasn't heard.
    private List<MemoryEntry> StoriesToTell(World.Hearth fire)
    {
        var listeners = new List<Npc>();
        foreach (var a in Sim.AgentsWithin(fire.GlobalPosition, 260f)) if (a is Npc n && n.AtHearth == fire) listeners.Add(n);
        var seen = new HashSet<string>();
        var stories = new List<MemoryEntry>();
        foreach (var n in Sim.Npcs)
            foreach (var m in n.Memories)
            {
                if (m.Directness != 1 || m.Weight < 0.8f || !m.Witnessed(_player) || !seen.Add(m.Key)) continue;
                bool news = false;
                foreach (var l in listeners) { bool knows = false; foreach (var k in l.Memories) if (k.Key == m.Key) { knows = true; break; } if (!knows) { news = true; break; } }
                if (news) stories.Add(m);
            }
        float now = GameCalendar.Instance?.DayOfYear ?? 0f;
        stories.Sort((a, b) => Mathf.PosMod(now - a.Day, 365f).CompareTo(Mathf.PosMod(now - b.Day, 365f)));
        if (stories.Count > 4) stories.RemoveRange(4, stories.Count - 4);
        return stories;
    }

    private void ShowStories(Npc npc)
    {
        _text = "Faces turn to you in the firelight. What will you tell them?";
        ClearOptions();
        foreach (var m in StoriesToTell(npc.AtHearth))
        {
            var story = m;
            string label = story.Text.Length > 70 ? story.Text.Substring(0, 67) + "..." : story.Text;
            AddOption(label, () => ShowTopics(TellAtFire(npc, story)));
        }
        AddOption("Back", () => ShowTopics(Greeting(npc)));
    }

    // You pass it on, firsthand: everyone at the fire who hadn't heard now
    // has it from you, and an evening's story warms them to the teller.
    private string TellAtFire(Npc npc, MemoryEntry m)
    {
        var fire = npc.AtHearth;
        if (fire == null) return "The fire has broken up for the night.";
        int told = 0;
        Npc answer = null;
        foreach (var a in Sim.AgentsWithin(fire.GlobalPosition, 260f))
        {
            if (a is not Npc n || n.AtHearth != fire) continue;
            bool knows = false; foreach (var k in n.Memories) if (k.Key == m.Key) { knows = true; break; }
            n.ChangeRapport(_player, 1f + n.Personality.Talkative);
            if (knows) continue;
            n.Remember(m.Copy());
            told++; answer ??= n;
        }
        _player.Bark(Barks.Personalise(m.Text, _player, null));
        answer?.QueueReply(answer.Personality.Suspicion > 0.65f ? "Is that the truth of it?" : "God save us. I'd not heard that.", 2.5f);
        return told == 0 ? "They had all heard it already, but they listen to you tell it." : $"You tell them how it was. {told} of them hadn't heard it; now they have it from a man who was there.";
    }

    private void ShowWork(Npc npc, string said = null)
    {
        _text = said ?? (npc.Role == Role.Factor || npc.Role == Role.NwcPartner || npc.Role == Role.Postmaster ? "\"Yes?\"" : $"{npc.DisplayName} straightens up.");
        ClearOptions();
        bool leader = npc.Role == Role.Factor || npc.Role == Role.NwcPartner || npc.Role == Role.Postmaster;
        bool ownLeader = leader && npc.Faction == _player.Faction;
        bool ownMan = npc.Faction == _player.Faction && (npc.Role == Role.Labourer || npc.Role == Role.Voyageur);
        bool canCommand = _player.Rank == "clerk" || _player.Rank == "postmaster" || _player.Standing >= 35f;
        if (npc.Role == Role.Factor) AddOption("Ask for work", () => ShowWork(npc, AskForWork(npc)));
        if (npc.Role == Role.Postmaster && _player.PostedInland) AddOption("Ask to be sent back down to the Bay", () => { _player.PostedInland = false; ShowWork(npc, "\"Go, then. Tell Mr. Auld we want more goods and fewer letters.\""); });
        if (npc.Role == Role.Postmaster && !_player.PostedInland && _player.Faction == Faction.HBC) AddOption("Ask to serve here at Oxford House", () => { _player.PostedInland = true; ShowWork(npc, "\"A man who can walk the road is worth having. You're on my books.\""); });
        if (ownLeader)
        {
            if (_player.Task.Kind != TaskKind.None && !_player.Task.IsComplete && _player.Task.Kind != TaskKind.Report)
                AddOption("About this task...", () => ShowBargain(npc));
            AddOption("Volunteer for something...", () => ShowVolunteer(npc));
            if (npc.Role == Role.Factor && Sim.Nwc != null) AddOption("\"I'll be across the river a few days.\"", () => ShowWork(npc, Authorize(npc)));
        }
        if (ownMan && canCommand) AddOption("Give orders...", () => ShowOrders(npc));
        if (npc.Following == _player) AddOption("\"That will do - carry on.\"", () => { npc.Following = null; ShowWork(npc, $"{npc.DisplayName} nods and goes back to their own business."); });
        else if (CanAskAlong(npc) && !(ownMan && canCommand)) AddOption("Ask them to come with you", () => ShowWork(npc, AskAlong(npc)));
        AddOption("Back", () => ShowTopics(Greeting(npc)));
    }

    private void ShowFamily(Npc npc, string said = null)
    {
        bool canTalk = _player.FluencyWith(npc) >= 0.3f;
        _text = said ?? (canTalk ? $"{npc.DisplayName} listens." : NoWords(npc));
        ClearOptions();
        if (canTalk)
        {
            AddOption("Ask about marriage in the custom of the country", () => ShowFamily(npc, AskMarriage(npc)));
            foreach (var d in npc.Children)
                if (d.Role == Role.CreeDaughter && d.Spouse == null && !d.IsDead)
                    AddOption($"Ask for {d.DisplayName}'s hand", () => ShowFamily(npc, Propose(npc, d)));
        }
        AddOption("Back", () => ShowTopics(Greeting(npc)));
    }

    private void ShowCompanies(Npc npc, string said = null)
    {
        _text = said ?? (Economy.Rivalry.OpenHostility ? "\"Between the companies it is open hostility. Speak carefully.\"" : $"\"The companies: {Economy.Rivalry.Describe()}.\"");
        ClearOptions();
        bool ownLeader = npc.Faction == _player.Faction;
        // Papers for sale (design doc 10.3).
        foreach (var m in new List<Message>(_player.Mail))
            if (!m.Read && Economy.Rivalry.AreRivals(m.Company, npc.Faction))
                AddOption($"Sell them the {m.Kind} from {m.From} ({PacketPrice(m)} trade goods)", () => ShowCompanies(npc, SellPacket(npc, m)));
        if (ownLeader && Economy.Rivalry.Tension >= 40f && Economy.Rivalry.PostOf(Economy.Rivalry.RivalOf(npc.Faction)) != null)
            AddOption(npc.Faction == Faction.HBC ? "Propose seizing the Nor'Westers' stores" : "Propose seizing the Company's stores", () => ShowCompanies(npc, ProposeRaid(npc)));
        if (!ownLeader && Economy.Rivalry.Tension >= 35f)
            AddOption("Propose a truce between the companies", () => ShowCompanies(npc, ProposeTruce(npc)));
        if (npc.Role == Role.NwcPartner && _player.Faction != Faction.NWC)
            AddOption("Ask about signing on with the North West Company", () => ShowCompanies(npc, OfferNwc(npc)));
        if (npc.Role == Role.NwcPartner && _player.Faction != Faction.NWC && _player.WantsToDefect)
            AddOption("Sign on. Leave the Company's service.", () => ShowCompanies(npc, Defect(npc)));
        if (npc.Role == Role.Factor && _player.Faction == Faction.NWC)
            AddOption("Ask to be taken back into the Company's service", () => ShowCompanies(npc, Return(npc)));
        if (npc.Role == Role.NwcPartner && _player.Faction == Faction.NWC && GameCalendar.Instance.Month < 7 && _player.Standing >= 20f)
            AddOption("Write to the partners at Fort William...", () => ShowCouncilLetter(npc));
        AddOption("Back", () => ShowTopics(Greeting(npc)));
    }

    private void ClearOptions() => _options.Clear();

    private void AddOption(string label, Action action) => _options.Add((label, action));

    // ---- topics ---------------------------------------------------------

    private string AskNews(Npc npc)
    {
        var cal = GameCalendar.Instance; var fort = Sim.Fort; var band = Sim.Band;
        string s = "";
        int mouths = 0;
        if (npc.Faction == Faction.HBC || npc.Faction == Faction.ShipsCrew)
        {
            foreach (var n in Sim.Npcs) if (n.Faction == Faction.HBC && !n.IsDead) mouths++;
            float days = fort?.FoodDays(mouths + 1) ?? 0f;
            s += days < 20 ? "\"The stores are thin. We'll be on short rations before the geese come.\" " : "\"We're provisioned well enough.\" ";
            if (Sim.Ship != null && Sim.Ship.IsDocked) s += "\"The ship's in - every man to the landing.\" ";
            else if (cal.Month >= 6 && cal.Month <= 8) s += "\"We look for the ship any day now.\" ";
            if (fort != null) s += $"\"We hold {fort.FursInStoreMb:0} Made Beaver in furs against London's {fort.QuotaMb:0}.\" ";
            if (npc.Role == Role.Labourer && npc.AssignedTaskName != "") s += $"\"Mr. Auld has me set to {npc.AssignedTaskName}.\" ";
        }
        else if (npc.Faction == Faction.NWC)
        {
            foreach (var n in Sim.Npcs) if (n.Faction == Faction.NWC && !n.IsDead) mouths++;
            float days = Sim.Nwc?.FoodDays(mouths + 1) ?? 0f;
            s += days < 20 ? "\"Provisions are short till the brigade comes.\" " : "\"We eat well enough.\" ";
        }
        if (npc.Faction == Faction.HBC || npc.Faction == Faction.NWC)
        {
            string r = Economy.Rivalry.Describe();
            s += Economy.Rivalry.OpenHostility ? $"\"Between us and the other side it is {r}. Keep your musket by you.\" "
                : Economy.Rivalry.Tension >= 40f ? $"\"There is {r} with the other company. It will come to blows if it goes on.\" " : "";
        }
        else if (npc.IsBandPerson)
        {
            band = npc.HomeStores as Economy.BandLedger ?? band;
            mouths = band?.Mouths ?? 0;
            float days = band?.FoodDays(mouths) ?? 0f;
            s += days < 5 ? "\"The camp is hungry. The hunters go out every day and come back light.\" " : "\"There is meat in the camp.\" ";
            if (fort != null) s += fort.PeltPrice >= 1.3f ? "\"The Company pays well for beaver this year. Everyone is on the trapline.\" "
                : fort.PeltPrice <= 0.7f ? "\"The Company's price is poor. It is hardly worth the walk to the post.\" " : "\"The Company's price is fair.\" ";
        }
        if (npc.Personality.Talkative > 0.6f)
        {
            foreach (var other in Sim.Npcs)
            {
                if (other.IsDead) { s += $"\"You heard {other.DisplayName} is dead?\" "; break; }
            }
        }
        npc.Warm(_player, 1f);
        return s == "" ? "\"Nothing to tell.\"" : s;
    }

    // A message takes effect when it's read (design doc 10.1), and it has
    // to be carried there by someone.
    private bool HasMailFor(Npc npc)
    {
        foreach (var m in _player.Mail) if (!m.Read && (m.To == npc.DisplayName || m.To == Mail.RoleName(npc))) return true;
        return false;
    }

    private string HandOver(Npc npc, Message m)
    {
        _player.Mail.Remove(m);
        if (m.Kind == "directive" || m.Kind == "mandate") Mail.Give(m, npc); else { npc.Mail.Add(m); Mail.Open(m, npc); }
        npc.ChangeRapport(_player, 3f);
        if (npc.Faction == _player.Faction) _player.Standing += 2f;
        Chronicle.Event($"{_player.DisplayName} handed {npc.DisplayName} a {m.Kind} from {m.From}.", npc.GlobalPosition, _player, weight: 0.6f);
        return $"{npc.DisplayName} takes the {m.Kind}, breaks the seal, and reads it there and then." + (m.Kind == "directive" ? " His face gives nothing away." : "");
    }

    private static int PacketPrice(Message m) => m.Kind == "directive" || m.Kind == "mandate" ? 8 : 3;

    // Sold paper is read paper (design doc 10.3): whoever opens it is
    // affected. Then a hidden roll decides whether word gets back.
    private string SellPacket(Npc buyer, Message m)
    {
        var counter = buyer.HomeStores?.Stores;
        int price = PacketPrice(m);
        if (counter == null || counter.Count(ItemId.TradeGoods) < price) return "\"I'd take it, but the shelves are bare. Come back when the outfit's in.\"";
        counter.Remove(ItemId.TradeGoods, price); _player.Inventory.Add(ItemId.TradeGoods, price);
        _player.Mail.Remove(m);
        Mail.Open(m, buyer);
        buyer.ChangeRapport(_player, 6f);
        Chronicle.Event($"{_player.DisplayName} sold {buyer.DisplayName} a {m.Kind} meant for the other company.", buyer.GlobalPosition, _player, weight: 2.5f);
        if (m.Company == Faction.HBC && GD.Randf() < 0.4f)
        {
            // Somebody talks. It takes days to reach the Factor.
            Mail.Send(Letters.Rumour(_player.DisplayName, $"{_player.DisplayName} sold the Company's {m.Kind} to the Nor'Westers.", 20f), 24f * GD.RandRange(3, 9));
        }
        return $"\"Well now.\" {buyer.DisplayName} counts out {price} trade goods and reads the {m.Kind} on the spot. Whether anyone ever hears of this is not up to you.";
    }

    private string AskDirective(Npc leader)
    {
        var fort = leader.HomeStores as Economy.FortLedger;
        if (fort == null) return "\"...\"";
        string when = fort.DirectiveReadDay < -100 ? "" : $" (read {GameCalendar.Instance.Day - fort.DirectiveReadDay} days ago)";
        string progress = $" We hold {fort.FursInStoreMb:0} MB against {fort.QuotaMb:0}; the ship is {DaysToShip()} days off.";
        return $"\"{fort.DirectiveText}\"{when}{progress}" + (fort.Assignments.Count > 0 ? $" The men are set as you heard this morning." : "");
    }

    private static int DaysToShip()
    {
        int doy = Mathf.RoundToInt(GameCalendar.Instance.DayOfYear);
        int ship = 31 + 28 + 31 + 30 + 31 + 30 + 31 + 20;
        return ship >= doy ? ship - doy : 365 - doy + ship;
    }

    // Declining costs standing; a leader with a temper costs more (design doc 3.9).
    private string DeclineTask(Npc leader)
    {
        float cost = 3f + leader.Personality.Suspicion * 3f;
        _player.Standing -= cost;
        _player.TasksRefused++;
        leader.ChangeRapport(_player, -5f);
        string was = _player.Task.Describe();
        _player.Task.Kind = TaskKind.None;
        Chronicle.Event($"{_player.DisplayName} declined the Factor's order.", leader.GlobalPosition, _player, weight: 0.8f);
        return leader.Personality.Suspicion > 0.6f
            ? $"\"You'll not? Then you'll not eat at my table with the same face.\" He writes something in his book. (standing -{cost:0})"
            : $"\"Suit yourself. There are men here who do as they're bid.\" (standing -{cost:0})";
    }

    private string CounterTask(Npc leader)
    {
        var fort = leader.HomeStores as Economy.FortLedger;
        if (fort == null) return "\"...\"";
        var kind = _player.Task.Kind;
        _player.Standing -= 1f;
        var options = new List<(TaskKind, int, string)> { (TaskKind.Firewood, 4, "the woodpile"), (TaskKind.CarryCargo, 8, "the ship"), (TaskKind.DriveOffAnimals, 1, "the beasts about the post"), (TaskKind.DeliverLetter, 2, "the band's furs") };
        options.RemoveAll(o => o.Item1 == kind || (o.Item1 == TaskKind.CarryCargo && !(Sim.Ship?.IsDocked ?? false)) || (o.Item1 == TaskKind.DeliverLetter && (Sim.Band?.InvitedToTrade ?? true)));
        if (options.Count == 0) return "\"There is nothing else. Do what you were set.\"";
        var pick = options[0];
        _player.AssignTask(pick.Item1, pick.Item2, pick.Item3);
        if (pick.Item1 == TaskKind.DeliverLetter) Delegation.WriteLetterToChief(leader, _player);
        return $"\"Very well.\" He thinks a moment. \"{_player.Task.Describe()}.\"";
    }

    private void ShowVolunteer(Npc leader)
    {
        _text = "\"What is it you fancy?\"";
        ClearOptions();
        var fort = leader.HomeStores as Economy.FortLedger; var eco = Sim.Ecology;
        AddOption("\"Let me carry your letter to the band.\"", () => { _player.AssignTask(TaskKind.DeliverLetter, 2, "the band's furs"); Delegation.WriteLetterToChief(leader, _player); _player.Standing += 1f; ShowTopics("\"Good. Tell the chief the Company's counter is open and his people are welcome.\" He writes a few lines, seals them, and hands you the letter."); });
        if (fort != null && eco != null)
        {
            var site = eco.BestSite(Ecology.Species.Caribou, fort) ?? eco.BestSite(Ecology.Species.Goose, fort);
            if (site != null) AddOption($"\"Let me hunt at {site.Name}.\"", () => { _player.AssignTask(TaskKind.Firewood, 0, $"hunt at {site.Name} - bring meat to the stores"); _player.Standing += 1f; ShowTopics($"\"Bring back meat and I'll remember it.\" {(fort.Stores.Remove(ItemId.Musket) ? "He hands you a musket from the stores." : "")}"); if (!_player.Inventory.Has(ItemId.Musket) && _player.Task.Intent.StartsWith("hunt")) _player.Inventory.Add(ItemId.Musket); });
        }
        if (leader.Role == Role.Factor && Sim.Outpost != null && !_player.PostedInland)
        {
            AddOption("\"Send me up to Oxford House with the outfit.\"", () =>
            {
                if (_player.Standing < 12f) { ShowTopics("\"Inland? A man I've known a fortnight? Prove yourself here first.\" (standing 12)"); return; }
                int goods = fort.Stores.TakeUpTo(ItemId.TradeGoods, 10); _player.Inventory.Add(ItemId.TradeGoods, goods);
                int food = fort.Stores.TakeUpTo(ItemId.Pemmican, 4); _player.Inventory.Add(ItemId.Pemmican, food);
                _player.AssignTask(TaskKind.Courier, goods, "the inland outfit");
                _player.PostedInland = true; _player.Standing += 2f;
                var orders = new Message { Kind = "orders", From = leader.DisplayName, To = "the postmaster at Oxford House", Company = Faction.HBC, Subject = "a new hand and the outfit", Text = $"{leader.DisplayName} sends {_player.DisplayName} to serve at Oxford House with {goods} pieces of the outfit. Pay {fort.PeltPrice:0.00}." };
                orders.OnRead = r => { if (Sim.Outpost != null) { Sim.Outpost.PeltPrice = fort.PeltPrice; } Sim.Notice($"{r?.Describe() ?? "The postmaster"} reads the Factor's line: {_player.DisplayName} is his now."); };
                _player.Mail.Add(orders);
                ShowTopics($"\"Very well. {goods} pieces and a letter for Mr. Sutherland. The road head is below the gate, on the bank - a day's march. You are posted to Oxford House till I say otherwise; he gives your orders now.\"");
            });
        }
        if (Economy.Rivalry.Tension >= 40f && Economy.Rivalry.PostOf(Economy.Rivalry.RivalOf(leader.Faction)) != null)
            AddOption("\"Let me stand guard at the stores.\"", () => { _player.AssignTask(TaskKind.Firewood, 0, "guard the storeroom"); _player.Standing += 1f; ShowTopics("\"Keep your eyes open, then.\""); });
        AddOption("Back", () => ShowTopics(Greeting(leader)));
    }

    private string DeliverLetter(Npc chief)
    {
        var band = Sim.Band;
        _player.Task.Progress(TaskKind.DeliverLetter, 1);
        if (band == null) return "\"...\"";
        // The letter itself, if you're carrying it (design doc 10.1.1); else the word of mouth version.
        Message letter = null;
        foreach (var m in _player.Mail) if (!m.Read && (m.To == chief.DisplayName || m.To == Mail.RoleName(chief))) { letter = m; break; }
        if (letter != null) { _player.Mail.Remove(letter); Mail.Open(letter, chief); }
        else { band.InvitedToTrade = true; band.StandingWithCompany = Mathf.Min(100f, band.StandingWithCompany + 6f); }
        // His answer goes back in your pack: a string of beads and his words for the Factor.
        var reply = Letters.Reply(chief.DisplayName, band.WillTrade ? "the band will come when the furs are ready, and the price had better be good" : "the band remembers how the Company has dealt with them and will see");
        _player.Mail.Add(reply);
        chief.ChangeRapport(_player, 4f);
        Chronicle.Event($"{_player.DisplayName} brought the Factor's letter to {chief.DisplayName}.", chief.GlobalPosition, _player, weight: 0.6f);
        return band.WillTrade
            ? "\"He wants our beaver. Tell him we will come when the furs are ready, and that his price had better be good.\" He gives you a string of beads for the Factor."
            : "\"Tell your Factor we remember how his people have dealt with us. We will see.\"";
    }

    // Changing sides (design doc 1, 5): the partner will take a Company man
    // who knows the Bay; the Company will remember it.
    private string OfferNwc(Npc partner)
    {
        _player.WantsToDefect = true;
        return "\"A man who knows the Bay post and its people is worth something to us. Winter with us: better pay, a share of the trade if you earn it, and nobody in London reading your letters. Your Factor will call it desertion. That is his word for it.\" Choose \"Sign on\" if you mean it.";
    }

    private string Defect(Npc partner)
    {
        _player.ChangeSides(Faction.NWC, partner);
        return "\"Good. Baptiste will show you the bunks.\" You are a Nor'Wester now. The Company's men will not forget it.";
    }

    private string Return(Npc factor)
    {
        if (factor.RapportWith(_player) < 20f) return "\"You went over to the Nor'Westers and now you want your place back? Bring me something worth having first.\" (rapport " + factor.RapportWith(_player).ToString("0") + ", needs 20)";
        _player.ChangeSides(Faction.HBC, factor);
        return "\"Very well. You start at the bottom, and I will be watching.\"";
    }

    private string AskAboutThem(Npc npc)
    {
        var p = npc.Personality;
        string temper = p.Courage > 0.7f ? "a bold" : p.Courage < 0.3f ? "a cautious" : "a steady";
        string tongue = p.Talkative > 0.6f ? "talkative" : p.Talkative < 0.3f ? "close-mouthed" : "civil";
        string want = p.Ambition > 0.65f ? "means to rise" : p.Loyalty > 0.65f ? "is loyal to their own" : "wants a quiet life";
        string s = npc.Role switch
        {
            Role.Factor => "\"Thirty years in the Company's service, and this post is my charge. London wants furs; I want the men fed and the returns respectable.\"",
            Role.Clerk => "\"I keep the books and the counter. A clerk who does well may have a post of his own one day.\"",
            Role.Labourer => "\"Orkneyman, five-year contract. Cut wood, haul cargo, shoot geese, freeze. The pay goes home to my mother.\"",
            Role.Woman => "\"My man is one of the servants here. I keep the house and the fires; without us the post would go cold.\"",
            Role.CreeChief => "\"This is our country. The Company sits on the shore; we bring what it wants and take what we need.\"",
            Role.CreeHunter => "\"I hunt for the camp. Beaver for the trade, caribou for the pot.\"",
            Role.CreeWoman => "\"I dress the hides and keep the lodge. The pemmican is women's work, and so is most of the rest.\"",
            Role.CreeDaughter => "\"My father decides whom I marry. But he listens to me.\"",
            Role.Sailor => "\"Out of Gravesend in June, home by November if the ice lets us. I do not envy you the winter.\"",
            Role.Wife => "\"I have a foot in both worlds now. Do not forget my family.\"",
            _ => "",
        };
        string family = "";
        if (npc.Spouse is Npc sp) family += $" Married to {sp.DisplayName}.";
        if (npc.Children.Count > 0) family += $" Children: {string.Join(", ", npc.Children.ConvertAll(c => c.DisplayName))}.";
        if (npc.Father != null) family += $" {npc.Father.DisplayName}'s child.";
        npc.Warm(_player, 1.5f);
        return $"{s} About {npc.Age} years old. You judge them {temper}, {tongue} sort who {want}.{family} (rapport {npc.RapportWith(_player):0})";
    }

    private string AskGossip(Npc npc)
    {
        var lines = new List<string>();
        foreach (var other in Sim.Npcs)
        {
            if (other == npc || other.IsDead || other.Faction != npc.Faction) continue;
            if (other.Needs.Health < 60) lines.Add($"\"{other.DisplayName} is hurt.\"");
            else if (other.Needs.Hunger < 35) lines.Add($"\"{other.DisplayName} has not eaten properly in days.\"");
            else if (other.Role == Role.Labourer && other.AssignedTaskName != "") lines.Add($"\"{other.DisplayName} is set to {other.AssignedTaskName}.\"");
            else if (other.Role == Role.Clerk) lines.Add(other.Standing > _player.Standing ? $"\"{other.DisplayName} stands well with the Factor - better than you, if you want it plain.\"" : $"\"{other.DisplayName} has not made much of himself this year.\"");
            else if (other.Spouse == null && !other.IsFemale && other.Role != Role.Factor && other.Role != Role.Sailor) lines.Add($"\"{other.DisplayName} wants a wife, if you ask me.\"");
        }
        // What they actually know, secondhand or not (design doc 10.2).
        bool garbled = npc.IsBandPerson && _player.FluencyWith(npc) < 0.5f;
        foreach (var m in npc.Memories)
        {
            string hedge = m.Directness == 1 ? "\"I saw it myself: " : m.Directness == 2 ? "\"I was told " : "\"They say ";
            // Without a shared tongue even a direct account can come through bent (design doc 10.6).
            var heard = garbled && GD.Randf() < 0.35f ? m.Retell(npc) : m;
            lines.Add($"{hedge}{heard.Text}\"" + (heard != m ? " (as best you follow it)" : ""));
        }
        if (lines.Count == 0) return "\"Nothing worth telling.\"";
        int n = npc.Personality.Talkative > 0.6f ? 4 : npc.Personality.Talkative > 0.3f ? 2 : 1;
        var picked = new List<string>();
        for (int i = 0; i < n && lines.Count > 0; i++) { int k = (int)(GD.Randi() % (uint)lines.Count); picked.Add(lines[k]); lines.RemoveAt(k); }
        npc.Warm(_player, 0.5f);
        return string.Join(" ", picked) + (npc.Personality.Talkative < 0.3f ? " They say no more." : "");
    }

    // Orders to one of the Company's men. He weighs your rank against his
    // own loyalty to the Factor; a sulky man does it slowly or not at all.
    private void ShowOrders(Npc man)
    {
        _text = $"{man.DisplayName} waits to hear it. (set to: {(man.AssignedTaskName == "" ? "nothing" : man.AssignedTaskName)})";
        ClearOptions();
        var fort = _player.HomePost; var eco = Sim.Ecology;
        AddOption("\"Cut wood.\"", () => ShowTopics(Order(man, "cut wood", _ => new Agents.Goals.ChopWoodGoal())));
        AddOption("\"Snare small game.\"", () => ShowTopics(Order(man, "snare small game", _ => new Agents.Goals.ForageGoal(5f), hunt: true)));
        if (eco != null && fort != null)
        {
            var site = eco.BestSite(Ecology.Species.Goose, fort) ?? eco.BestSite(Ecology.Species.Caribou, fort) ?? eco.BestSite(Ecology.Species.Beaver, fort);
            if (site != null) AddOption($"\"Hunt at {site.Name}.\"", () => ShowTopics(Order(man, $"hunt at {site.Name}", _ => new Agents.Goals.HuntSiteGoal(site, 6f), hunt: true)));
            if (fort.Stores.Has(ItemId.SteelTrap) || man.Inventory.Has(ItemId.SteelTrap))
                AddOption("\"Work the trapline.\"", () => ShowTopics(Order(man, "work the trapline", n => RoleBehaviour.TraplineWork(n, fort, eco, true) ?? new Agents.Goals.ChopWoodGoal())));
        }
        if (fort != null)
        {
            var door = fort.Company == Faction.HBC ? World.WorldLayout.Warehouse.DoorCell : World.WorldLayout.NwcHouse.DoorCell;
            AddOption("\"Stand guard at the stores.\"", () => ShowTopics(Order(man, "stand guard at the stores", _ => new Agents.Goals.GuardGoal(door, 4f), arm: true)));
        }
        AddOption("\"Come with me.\"", () => ShowTopics(Order(man, "go with " + _player.DisplayName, _ => new Agents.Goals.FollowGoal(_player, 10f), follow: true)));
        if (man.AssignedTaskName != "") AddOption("\"Carry on as the Factor set you.\"", () => { man.AssignedTask = null; man.AssignedTaskName = ""; man.Following = null; ShowTopics($"\"Aye.\" {man.DisplayName} goes back to the morning's orders."); });
        AddOption("Back", () => ShowTopics(Greeting(man)));
    }

    private string Order(Npc man, string name, Func<Npc, Agents.Goals.Goal> task, bool hunt = false, bool arm = false, bool follow = false)
    {
        // Will he take it from you? Loyalty here is loyalty to the Factor's
        // chain of command; your rank and his opinion of you carry it.
        float will = (_player.Rank == "postmaster" ? 0.6f : _player.Rank == "clerk" ? 0.4f : 0.2f) + man.RapportWith(_player) / 100f + (1f - man.Personality.Loyalty) * 0.2f + (man.Personality.Ambition > 0.6f ? -0.1f : 0.1f);
        if (man.IsDrunk) return $"{man.DisplayName} is too drunk to take an order from anyone.";
        if (will < 0.4f)
        {
            man.ChangeRapport(_player, -2f);
            return $"\"I take my orders from {(man.Faction == Faction.NWC ? "Mr. Cameron" : "Mr. Auld")}, not from you.\" {man.DisplayName} does not move.";
        }
        if ((arm || hunt) && !man.Inventory.Has(ItemId.Musket) && _player.HomePost != null && _player.HomePost.Stores.Remove(ItemId.Musket)) man.Inventory.Add(ItemId.Musket);
        man.AssignedTask = follow ? null : task;
        man.AssignedTaskName = name;
        man.AssignedTaskIsHunt = hunt;
        man.Brain.Clear(man);
        man.Brain.Push(man, task(man));
        man.ChangeRapport(_player, 1f);
        Chronicle.Event($"{_player.DisplayName} set {man.DisplayName} to {name}.", man.GlobalPosition, _player, weight: 0.4f);
        return $"\"Aye, sir.\" {man.DisplayName} goes to {name}.";
    }

    // A friend asked along for the day.
    private string AskAlong(Npc npc)
    {
        float r = npc.RapportWith(_player);
        bool kin = npc.Spouse == _player || (_player.Spouse != null && (npc.Children.Contains(_player.Spouse as Npc) || npc == (_player.Spouse as Npc)?.Father));
        if (npc.Faction != _player.Faction && !kin && r < 20f) return $"{npc.DisplayName} shakes their head. They do not know you well enough to walk into the bush with you. (rapport {r:0}, needs 20)";
        if (npc.Faction == _player.Faction && r < 5f && !kin) return $"\"I have my own work.\" (rapport {r:0}, needs 5)";
        if (npc.Needs.Hunger < 30f || npc.Needs.Warmth < 30f) return $"{npc.DisplayName} is in no state to go anywhere.";
        npc.Brain.Clear(npc);
        npc.Brain.Push(npc, new Agents.Goals.FollowGoal(_player, 8f));
        npc.ChangeRapport(_player, 2f);
        return $"{npc.DisplayName} picks up {(npc.Weapon != null ? "their " + Items.Items.Of(npc.Weapon.Value).Name.ToLower() : "their things")} and falls in beside you. They will follow till evening, or till you tell them to stop.";
    }

    private static string Bored(Npc npc) => npc.TalkedToday.GetValueOrDefault(Sim.Player as Agent) >= 4 ? $" {npc.DisplayName} has had enough of your questions for one day." : "";

    // A go-between with standing in both camps (design doc 7.2, 10.1.5's Metis niche).
    private string BrokerPeace(Npc chief, Economy.BandLedger band)
    {
        float r = chief.RapportWith(_player);
        var other = band.OtherBand;
        if (r < 15f) return $"\"Who are you to speak for {other.Name}?\" (rapport {r:0}, needs 15)";
        if (_player.Inventory.Count(ItemId.TradeGoods) < 3) return "\"Words are wind. A pipe and tobacco between the camps would be something.\" (3 trade goods)";
        _player.Inventory.Remove(ItemId.TradeGoods, 3); other.Stores.Add(ItemId.TradeGoods, 3);
        band.StandingWithOtherBand = Mathf.Min(100f, band.StandingWithOtherBand + 15f);
        other.StandingWithOtherBand = Mathf.Min(100f, other.StandingWithOtherBand + 15f);
        chief.ChangeRapport(_player, 5f); _player.Standing += 2f;
        Chronicle.Event($"{_player.DisplayName} carried tobacco between {band.Name} and {other.Name}.", chief.GlobalPosition, _player, weight: 1.2f);
        return $"\"Take this to their fire, then, and say it comes from us.\" The standing between the bands eases ({band.StandingWithOtherBand:0}).";
    }

    // Dressing a wound (design doc 17.5): clean water, a strip of blanket, a steady hand. Once a day.
    private int _treatedDay = -1;
    private string Treat(Npc her)
    {
        int day = GameCalendar.Instance.Day;
        if (_treatedDay == day) return $"{her.DisplayName} has already seen to you today. \"Rest it.\"";
        if (!_player.Inventory.Has(ItemId.Water) && !(her.HomeStores?.Stores.Has(ItemId.Water) ?? false)) return "\"There is no clean water. Fetch some and I will see to it.\"";
        if (_player.Inventory.Has(ItemId.Water)) _player.Inventory.Remove(ItemId.Water); else her.HomeStores.Stores.Remove(ItemId.Water);
        _treatedDay = day;
        float before = _player.Needs.Health;
        _player.Needs.Health = Mathf.Min(100f, _player.Needs.Health + 25f);
        her.ChangeRapport(_player, 2f);
        return $"{her.DisplayName} washes the wound and binds it with a strip of blanket. ({before:0} → {_player.Needs.Health:0})";
    }

    // Ransom and parole (design doc 9.1): a prisoner is worth more loose.
    private string Ransom(Npc captor)
    {
        int price = 12;
        if (_player.Inventory.Count(ItemId.TradeGoods) < price) return $"\"Twelve pieces of goods and you may walk out. You have {_player.Inventory.Count(ItemId.TradeGoods)}.\"";
        _player.Inventory.Remove(ItemId.TradeGoods, price);
        captor.HomeStores?.Stores.Add(ItemId.TradeGoods, price);
        var post = _player.CaptiveOf; _player.CaptiveOf = null; _player.CaptiveUntilDay = -1;
        captor.ChangeRapport(_player, 4f);
        Chronicle.Event($"{_player.DisplayName} bought his freedom from {captor.DisplayName} for {price} pieces.", captor.GlobalPosition, _player, weight: 1.2f);
        return $"\"A man who pays is a man of sense.\" {captor.DisplayName} counts the goods and waves you at the gate. ({post.Title} keeps your kit.)";
    }

    private string Parole(Npc captor)
    {
        float r = captor.RapportWith(_player);
        if (captor.Personality.Suspicion > 0.65f && r < 10f) return "\"Your word? I have seen what your word is worth.\"";
        _player.CaptiveOf = null; _player.CaptiveUntilDay = -1;
        _player.ParoleUntilDay = GameCalendar.Instance.Day + 120;
        _player.ParoleTo = captor.Faction;
        captor.ChangeRapport(_player, 8f);
        Chronicle.Event($"{_player.DisplayName} gave his parole to {captor.DisplayName}.", captor.GlobalPosition, _player, weight: 1.5f);
        return "\"Then go. No arms against us till the ice goes out - and I will hear of it if you break it.\" You are free, and bound by your word for four months.";
    }

    private string Confess(Npc leader)
    {
        _player.Standing -= 6f;
        _player.ParoleConfessed = true;
        leader.ChangeRapport(_player, -4f);
        return leader.Personality.Suspicion > 0.6f
            ? "\"You gave the Canadians your word? Then you are their man as much as mine.\" He writes it down. (standing -6, but it will not surprise him later.)"
            : "\"Better I hear it from you than from them.\" (standing -6, and no worse when it comes out.)";
    }

    // Accept, decline, or argue the thing out (design doc 3.9 step 5, P.2).
    // What he wears depends on rank, regard, what you have done for him,
    // who speaks for you and what he has heard - and he says which.
    private void ShowBargain(Npc leader, string said = null)
    {
        _text = said ?? $"\"{_player.Task.Describe()}\" {leader.DisplayName} waits.";
        ClearOptions();
        AddOption("\"It will be done.\"", () => { _player.Task.Accepted = true; leader.ChangeRapport(_player, 1f); ShowBargain(leader, "\"Good man.\" He turns back to his book."); });
        AddOption("Ask for more time", () => ShowBargain(leader, AskMoreTime(leader)));
        AddOption("Ask for a man to go with you", () => ShowBargain(leader, AskForAMan(leader)));
        AddOption("Ask for something in hand for it", () => ShowBargain(leader, AskForPay(leader)));
        AddOption("Ask leave to do it your own way", () => ShowBargain(leader, AskOwnWay(leader)));
        AddOption("Ask for a different task", () => ShowBargain(leader, CounterTask(leader)));
        AddOption("Decline it", () => ShowBargain(leader, DeclineTask(leader)));
        AddOption("Back", () => ShowWork(leader));
    }

    private string AskMoreTime(Npc leader)
    {
        var v = Negotiation.Weigh(_player, leader, 0.5f, "returns");
        if (!v.Granted) { leader.ChangeRapport(_player, -1f); return $"\"It wants doing today, not when it suits you.\" {v.Because()}"; }
        _player.Task.Deadline += 2;
        return $"\"Two days, then. No more.\" {v.Because()}";
    }

    private string AskForAMan(Npc leader)
    {
        var v = Negotiation.Weigh(_player, leader, 1.0f);
        if (!v.Granted) { return $"\"Every man here has his own work.\" {v.Because()}"; }
        Npc best = null;
        foreach (var n in Sim.Npcs)
        {
            if (n.HomeStores != leader.HomeStores || n.IsDead || n.Role is not (Role.Labourer or Role.Voyageur) || n.Tier != Core.Tier.Live) continue;
            if (best == null || n.RapportWith(_player) > best.RapportWith(_player)) best = n;
        }
        if (best == null) return "\"There is nobody about to send with you.\"";
        best.Brain.Clear(best);
        best.Brain.Push(best, new Agents.Goals.FollowGoal(_player, 10f));
        best.ChangeRapport(_player, 2f);
        return $"\"{best.DisplayName} - go with him.\" {v.Because()}";
    }

    private string AskForPay(Npc leader)
    {
        var fort = leader.HomeStores as Economy.FortLedger;
        if (fort == null) return "\"...\"";
        var v = Negotiation.Weigh(_player, leader, 1.2f);
        if (!v.Granted) { leader.ChangeRapport(_player, -2f); return $"\"You are paid by the year like every other man.\" {v.Because()}"; }
        int goods = fort.Stores.TakeUpTo(ItemId.TradeGoods, 4);
        _player.Inventory.Add(ItemId.TradeGoods, goods);
        if (!_player.Inventory.Has(ItemId.Rum) && fort.Stores.Remove(ItemId.Rum)) _player.Inventory.Add(ItemId.Rum);
        return $"\"Against your account, then.\" He counts out {goods} pieces. {v.Because()}";
    }

    // Leave to fill the order your own way (design doc 3.9's "fulfilling an
    // order is a choice of method"): the task becomes an open one.
    private string AskOwnWay(Npc leader)
    {
        var v = Negotiation.Weigh(_player, leader, 1.4f, "returns");
        if (!v.Granted) { leader.ChangeRapport(_player, -1f); return $"\"You will do it as I set it.\" {v.Because()}"; }
        _player.Task.OwnWay = true;
        _player.Task.Intent += " - your own way";
        return $"\"I care how much comes in, not how.\" {v.Because()} (Bring in the worth of it by any road you like.)";
    }

    private string NoWords(Npc npc) => $"You have not the words for it, and {npc.DisplayName} has not yours. Signs will buy meat; they will not carry this. Learn more of the tongue, or bring someone who has it - your wife, or a woman of the post.";

    private string Restitution(Npc holder, Grudge g)
    {
        int cost = Mathf.CeilToInt(g.DebtMb);
        if (_player.Inventory.Count(ItemId.TradeGoods) < cost) return $"\"{cost} pieces of goods, laid before the family. You have not got it.\" (you carry {_player.Inventory.Count(ItemId.TradeGoods)})";
        _player.Inventory.Remove(ItemId.TradeGoods, cost);
        holder.HomeStores?.Stores.Add(ItemId.TradeGoods, cost);
        Grudges.Settle(g, false);
        holder.ChangeRapport(_player, 25f);
        if (holder.HomeStores is Economy.BandLedger hb) hb.StandingWithCompany = Mathf.Min(100f, hb.StandingWithCompany + 12f);
        Chronicle.Event($"{_player.DisplayName} paid {cost} pieces to {holder.DisplayName}'s people for {g.Victim}.", holder.GlobalPosition, _player, weight: 1.5f);
        return $"{holder.DisplayName} takes the goods and says the name of {g.Victim} once more. \"It is covered. There is no more between us for this.\"";
    }

    private void ShowCouncilLetter(Npc partner)
    {
        _text = "\"The canoes go in a month. What would you have the partners hear from the Bay?\"";
        ClearOptions();
        float w = Mathf.Clamp(_player.Standing / 25f, 0.3f, 2f);
        void Letter(string item, int opt, string label) => AddOption(label, () => { Economy.Council.Letters.Add((item, opt, w, _player.DisplayName)); _player.Standing += 1f; ShowTopics($"He seals your letter with his own. \"It will be read, for what a clerk's word is worth. Yours weighs about {w:0.0} of a share.\""); });
        Letter("price", 1, "Urge them to undercut the Company's price");
        Letter("price", 0, "Urge them to hold the price and keep the margin");
        Letter("outfit", 2, "Ask for a full outfit for this district");
        Letter("bay", 0, "Urge peace with the Company's post");
        Letter("bay", 1, "Urge them to press the Company hard");
        Letter("share", 2, $"Speak for {partner.DisplayName}'s second share");
        AddOption("Back", () => ShowTopics(Greeting(partner)));
    }

    private string Authorize(Npc factor)
    {
        int day = GameCalendar.Instance.Day;
        _player.AuthorizedAcrossUntilDay = day + 5;
        if (factor.Personality.Suspicion > 0.6f) { factor.ChangeRapport(_player, -2f); return "\"Are you. And what business has a Company servant over there?\" He lets it pass, but he has written it down. For five days, being seen at the Nor'Westers' is your business and not a story."; }
        return "\"See what they are paying while you are at it.\" For five days, being seen across the river is no story against you.";
    }

    private string ProposeRaid(Npc leader)
    {
        var from = Economy.Rivalry.PostOf(leader.Faction); var target = Economy.Rivalry.PostOf(Economy.Rivalry.RivalOf(leader.Faction));
        if (from == null || target == null) return "\"There is nobody to seize anything from.\"";
        int today = GameCalendar.Instance.Day;
        if (today - from.LastRaidDay <= 2) return "\"The men have only just come back. Let them eat first.\"";
        float will = leader.Personality.Ambition * 0.6f + leader.Personality.Courage * 0.3f + Economy.Rivalry.Tension / 200f + (_player.Standing > 30f ? 0.15f : 0f) - leader.Personality.Loyalty * 0.2f;
        if (from.RaidedRecently(today)) will += 0.3f;
        if (will < 0.55f)
        {
            leader.ChangeRapport(_player, -3f);
            return leader.Faction == Faction.HBC
                ? "\"And have London read that its Factor turned bandit? No. We hold the counter and we hold our tempers. If they come to us, that is another matter.\""
                : "\"The partners at Fort William want furs, not a war on the Bay with our name on it. Not yet.\"";
        }
        var sent = Economy.Rivalry.OrderRaid(leader, from, target, 3);
        if (sent.Count == 0) return "\"I have no men to spare for it today.\"";
        _player.AssignTask(TaskKind.Raid, 4);
        leader.ChangeRapport(_player, 4f);
        return $"\"Very well. {string.Join(", ", sent.ConvertAll(n => n.DisplayName))} go with you - muskets, and no shooting unless they shoot first. Bring back what you can carry.\"";
    }

    private string ProposeTruce(Npc leader)
    {
        float t = Economy.Rivalry.Tension;
        float will = (1f - leader.Personality.Ambition) * 0.4f + leader.RapportWith(_player) / 100f + (leader.Needs.Health < 60f ? 0.2f : 0f) + (leader.Personality.Suspicion < 0.4f ? 0.15f : 0f);
        var own = Economy.Rivalry.PostOf(leader.Faction);
        if (own != null && own.RaidedRecently(GameCalendar.Instance.Day)) will -= 0.3f;
        if (will < 0.45f)
        {
            leader.ChangeRapport(_player, -4f);
            return "\"A truce? After what your people have done? Go back and tell your master we remember.\"";
        }
        Economy.Rivalry.Raise(-35f, "a truce agreed");
        leader.ChangeRapport(_player, 10f);
        Chronicle.Event($"{_player.DisplayName} and {leader.DisplayName} agreed a truce between the companies.", leader.GlobalPosition, _player, weight: 1.5f);
        return $"\"Neither of us profits from this. Tell your master the {(leader.Faction == Faction.NWC ? "North West Company" : "Company")} will keep to its own side of the river if he keeps to his.\" The rivalry stands at {Economy.Rivalry.Describe()} now ({t:0} to {Economy.Rivalry.Tension:0}).";
    }

    private string AskMarriage(Npc npc)
    {
        var daughters = new List<Npc>();
        foreach (var c in npc.Children) if (c.Role == Role.CreeDaughter && c.Spouse == null && !c.IsDead) daughters.Add(c);
        string s = "\"A man who wants a wife from us shows two things: that he is a friend, and that he can provide. He brings gifts to her father - blankets, a gun, cloth, kettles - and he comes back more than once. Then he asks. Your Company allows a man one wife; we do not hold you to that, but your Factor will.\"";
        s += $" Your standing with {npc.DisplayName}: rapport {npc.RapportWith(_player):0} (needs 30), gifts {npc.GiftsFromPlayerMb:0.#} MB (needs 8).";
        if (daughters.Count > 0) s += $" Unmarried daughters: {string.Join(", ", daughters.ConvertAll(d => d.DisplayName))}. Ask for her hand from this menu when you are ready.";
        else s += " They have no unmarried daughter.";
        return s;
    }

    private string OfferFood(Npc npc, ItemId food)
    {
        if (!_player.Inventory.Remove(food)) return "You have nothing to give.";
        npc.Inventory.Add(food);
        npc.Consume(food);
        npc.ChangeRapport(_player, 8f);
        return $"{npc.DisplayName} eats the {Items.Items.Of(food).Name.ToLower()} on the spot and thanks you. A hungry person remembers who fed them. (rapport {npc.RapportWith(_player):0})";
    }

    private string Threaten(Npc npc)
    {
        bool armed = _player.Weapon != null;
        float fear = (armed ? 0.5f : 0.2f) - npc.Personality.Courage * 0.5f + (npc.Weapon == null ? 0.2f : 0f);
        npc.ChangeRapport(_player, -15f);
        if (npc.IsBandPerson) (npc.HomeStores as Economy.BandLedger)?.OnInsult($"threatened {npc.DisplayName}");
        if (fear > 0.25f)
        {
            // Cowed: hands over something small and remembers it.
            var slot = npc.Inventory.Slot(0);
            if (slot is var (id, _) && slot != null && Items.Items.Of(id).Kind != ItemKind.Weapon) { npc.Inventory.Remove(id); _player.Inventory.Add(id); Close(); Sim.Notice($"{npc.DisplayName} hands over a {Items.Items.Of(id).Name.ToLower()} and backs away."); return ""; }
            Close();
            return "";
        }
        Close();
        npc.Brain.Push(npc, new Agents.Goals.AttackGoal(_player));
        Sim.Notice($"{npc.DisplayName} is not having it. Fists up.");
        return "";
    }

    private string AskHunting(Npc npc)
    {
        var eco = Sim.Ecology;
        if (eco == null) return "\"...\"";
        string s = "";
        var home = npc.HomeStores;
        foreach (var site in eco.Sites)
        {
            if (home != null && !home.Knows(site.Name)) continue;   // they can only tell what their house knows
            if (site.IsDead) s += $"\"{site.Name} is finished - hunted out. Nothing comes back there.\" ";
            else if (site.Scarcity) s += $"\"{site.Name} is crowded and hungry; the animals are wandering toward your post.\" ";
            else if (site.Count < site.Capacity * 0.4f) s += $"\"{site.Name} is getting thin.\" ";
        }
        // Knowledge is currency (design doc 10): the sites are told, not shown,
        // and only to someone the hunter has some regard for.
        npc.Warm(_player, 1f);
        if (npc.RapportWith(_player) >= 15f)
        {
            int learned = 0;
            // They tell you where they believe things are - for a herd, where
            // their people last saw it, which may be stale (design doc 16.5).
            foreach (var site in eco.Sites)
            {
                if (home != null && !home.Knows(site.Name)) continue;
                if (_player.KnownSites.Add(site.Name)) learned++;
                var believed = home != null ? home.BelievedCell(site) : site.Cell;
                if (!_player.SiteBelief.TryGetValue(site.Name, out var mine) || mine != believed) { _player.SiteBelief[site.Name] = believed; if (site.IsMobile) s += $" \"The caribou were last seen {(believed.Y < 130 ? "north, toward the coast" : "south, in the bush")}.\""; }
            }
            if (learned > 0) s += $" {(npc.IsFemale ? "She" : "He")} describes where the game is: {learned} places you did not know.{(npc.Faction == Faction.HBC ? "" : " The Factor would want to hear it.")}";
        }
        else s += $" {(npc.IsFemale ? "She" : "He")} says nothing of where the game is. You are not yet a friend.";
        return s == "" ? "\"The country is in good heart. Game everywhere it should be.\"" : s;
    }

    private string AskForWork(Npc factor)
    {
        var ship = Sim.Ship;
        var threats = 0;
        foreach (var a in Sim.Animals) if (!a.IsDead && a.IsDangerous) threats++;
        // Asking gets you today's order on the spot if you haven't one.
        if (_player.Task.Kind == TaskKind.None || _player.Task.IsComplete)
        {
            if (threats > 0) _player.AssignTask(TaskKind.DriveOffAnimals, 1);
            else if (ship != null && ship.IsDocked && ship.Cargo.SlotCount > 0) _player.AssignTask(TaskKind.CarryCargo, 8);
            else _player.AssignTask(TaskKind.Firewood, 4);
        }
        if (ship != null && ship.IsDocked) return "\"Work? The ship is in, man. Get down to the landing and carry.\"";
        if (threats > 0)
        {
            if (!_player.Inventory.Has(ItemId.Musket) && Sim.Fort != null && Sim.Fort.Stores.Remove(ItemId.Musket)) { _player.Inventory.Add(ItemId.Musket); return "\"There are beasts about the post.\" He hands you a musket from the stores. \"See them off.\""; }
            return "\"There are beasts about the post. See them off.\"";
        }
        return "\"Keep the fires up and the woodpile high. When the ship comes you'll have work enough.\"";
    }

    private string TellSites(Npc factor)
    {
        int told = 0;
        foreach (var sname in _player.KnownSites) if (Sim.Fort.Learn(sname, _player.SiteBelief.TryGetValue(sname, out var at) ? at : null)) told++;
        _player.Standing += 3f * told;
        factor.ChangeRapport(_player, 4f * told);
        return told > 0 ? $"\"Good man. That is worth knowing.\" He notes it in the journal. (standing +{3 * told})" : "\"You have told me nothing new.\"";
    }

    private int _tradePage;
    private Economy.Barter _barter;

    // The barter (design doc 3.6.1). Not a shop: a session with a man who has
    // his own position, his own patience, and a price he means to keep.
    private void ShowTrade(Npc npc, string said = null)
    {
        var fort = Sim.Fort; var band = npc.HomeStores as Economy.BandLedger ?? Sim.Band;
        var post = npc.Faction == Faction.NWC ? (Economy.FortLedger)Sim.Nwc : npc.HomeStores as Economy.FortLedger ?? Sim.Fort;
        if (!npc.IsBandPerson)
        {
            fort = post;
            if (fort == null) { ShowTopics("\"Nothing to trade.\""); return; }
            if (fort.Company != _player.Faction) { ShowTopics(_player.Faction == Faction.NWC ? "\"We do not serve Nor'Westers at this counter. Go back across the river.\"" : "\"The Company's servant wants to trade with us? Sign on first, or take your furs to your own Factor.\""); return; }
        }
        Inventory counter = npc.IsBandPerson ? band?.Stores : fort?.Stores;
        if (counter == null) { ShowTopics("\"Nothing to trade.\""); return; }
        if (!Economy.Barter.WillSitDown(npc, out float sulk))
        {
            _barter = null;
            ShowTopics($"He will not sit down to it again today - you pushed him past his patience, and he has {sulk:0} hours of being asked nothing at all coming to him.");
            return;
        }
        if (_barter == null || _barter.With != npc) _barter = new Economy.Barter(_player, npc, counter, npc.IsBandPerson ? null : fort, npc.IsBandPerson ? band : null);
        var bart = _barter;
        if (bart.Walked) { _barter = null; ShowTopics(said ?? "He is gone."); return; }
        bool crude = bart.Crude;

        var sb = new System.Text.StringBuilder();
        if (said != null) { sb.AppendLine(said); sb.AppendLine(); }
        sb.AppendLine(npc.IsBandPerson
            ? (crude ? "Without the words it is fingers and pointing, and you pay a piece more for the misunderstanding." : "Meat and hides against the Company's goods, and the price is what the two of you make it.")
            : $"The Company's counter: furs at {fort.PeltPrice:0.00} MB in goods per MB, the stores at cost.");
        int told = 0;
        foreach (string t in bart.Tells) { if (told++ >= 3) break; sb.AppendLine("  - " + t); }
        sb.AppendLine();
        sb.AppendLine($"{bart.Standing()} {bart.Mood()}");
        _text = sb.ToString();
        ClearOptions();

        // Sell out of your own pack.
        foreach (var (id, n) in new List<(ItemId, int)>(_player.Inventory.All()))
        {
            var def = Items.Items.Of(id);
            bool wanted = npc.IsBandPerson
                ? def.Kind is ItemKind.Goods or ItemKind.Tool or ItemKind.Weapon or ItemKind.Clothing
                : def.Kind == ItemKind.Fur;
            if (!wanted) continue;
            if (npc.IsBandPerson && id == ItemId.Musket && bart.HeldBackGuns) continue;
            // The whole lot goes over the counter at once; that is where the
            // haggling shows (design doc 3.6.1).
            int lot = n;
            ItemId paidIn = npc.IsBandPerson ? ItemId.BeaverPelt : ItemId.TradeGoods;
            while (lot > 1 && counter.Count(paidIn) < bart.SellPrice(id, lot)) lot--;
            int pay = bart.SellPrice(id, lot);
            if (counter.Count(paidIn) < pay) continue;
            var sold = id; int sellN = lot;
            AddOption($"Sell {Items.Items.Lot(id, sellN)} for {pay} {(paidIn == ItemId.TradeGoods ? "trade goods" : "pelts")}", () =>
            {
                _player.Inventory.Remove(sold, sellN); counter.Add(sold, sellN);
                counter.Remove(paidIn, pay); _player.Inventory.Add(paidIn, pay);
                if (!npc.IsBandPerson) { fort.FursTakenMb += def.ValueMb * sellN; fort.GoodsPaidMb += pay; if (def.Kind == ItemKind.Fur && fort.Company == _player.Faction) _player.StoodGuardOverTheQuota = true; }
                if (npc.IsBandPerson && sold == ItemId.Musket && band != null && band.AtOddsWithOtherBand)
                {
                    band.StandingWithCompany = Mathf.Min(100f, band.StandingWithCompany + 6f);
                    Core.Reveal.Lands(_player, $"{band.Name} have a gun of your selling, and they are at war.", "you sold a musket at the camp", 1.3f);
                }
                _player.Skills.Learn(ref _player.Skills.Trade, 0.012f);
                ShowTrade(npc, "Done, and the goods change hands.");
            });
            if (_options.Count >= 3) break;
        }

        // Buy off his counter.
        int shown = 0, skipped = 0; bool more = false;
        foreach (var (id, n) in new List<(ItemId, int)>(counter.All()))
        {
            var def = Items.Items.Of(id);
            if (def.Kind == ItemKind.Fur && !npc.IsBandPerson) continue;
            if (id == ItemId.TradeGoods) continue;
            if (shown >= 3) { more = true; break; }
            if (skipped < _tradePage * 3) { skipped++; continue; }
            int want = def.Kind is ItemKind.Food or ItemKind.Fuel ? Mathf.Min(n, 5) : 1;
            while (want > 1 && _player.Inventory.Count(ItemId.TradeGoods) < bart.BuyPrice(id, want)) want--;
            int cost = bart.BuyPrice(id, want);
            if (_player.Inventory.Count(ItemId.TradeGoods) < cost) continue;
            var bought = id; int buyN = want;
            AddOption($"Buy {Items.Items.Lot(id, buyN)} ({n} by him) for {cost} trade goods", () =>
            {
                counter.Remove(bought, buyN); _player.Inventory.Add(bought, buyN);
                _player.Inventory.Remove(ItemId.TradeGoods, cost); counter.Add(ItemId.TradeGoods, cost);
                _player.Skills.Learn(ref _player.Skills.Trade, 0.012f);
                ShowTrade(npc, "Done, and the goods change hands.");
            });
            shown++;
        }

        // The rounds themselves.
        if (!bart.GiftGiven) AddOption(npc.IsBandPerson ? "Open with a gift and a pipe..." : "Put something in his hand first...", () => ShowGiftOpening(npc));
        AddOption("Press him for a better price", () => ShowTrade(npc, bart.Press()));
        if (bart.Presses > 0) AddOption("Allow that he has a point", () => ShowTrade(npc, bart.Relent()));
        if (npc.IsBandPerson && band != null && band.AtOddsWithOtherBand && _player.Inventory.Has(ItemId.Musket) && !bart.HeldBackGuns)
            AddOption("Tell him the muskets are not for trade", () => ShowTrade(npc, bart.HoldBackGuns()));
        if (_options.Count == 0 && _tradePage == 0) _text += "\n\nYou have nothing he wants, nor he anything you can pay for. Trade goods are the coin here.";
        if (more) AddOption("More of his stores...", () => { _tradePage++; ShowTrade(npc); });
        else if (_tradePage > 0) AddOption("Back to the first shelf", () => { _tradePage = 0; ShowTrade(npc); });
        AddOption("Break off", () => { _tradePage = 0; _barter = null; ShowTopics(Greeting(npc)); });
    }

    // The gift that opens a trade: what you can spare, worth first.
    private void ShowGiftOpening(Npc npc)
    {
        _text = npc.IsBandPerson
            ? "Nothing is traded until something is given. What do you put in his hands?"
            : "A small thing across the counter does no harm. What?";
        ClearOptions();
        int shown = 0;
        foreach (var (id, n) in new List<(ItemId, int)>(_player.Inventory.All()))
        {
            var def = Items.Items.Of(id);
            if (def.Kind is not (ItemKind.Drink or ItemKind.Food or ItemKind.Goods or ItemKind.Tool or ItemKind.Clothing)) continue;
            if (shown++ >= 5) break;
            var give = id;
            AddOption($"Give {def.Name.ToLower()} ({def.ValueMb:0.0#} MB)", () => ShowTrade(npc, _barter?.Gift(give)));
        }
        if (shown == 0) _text += "\n\nYou have nothing to give that he would take as a gift.";
        AddOption("Go straight to the goods", () => ShowTrade(npc, null));
    }

    private void ShowGive(Npc npc)
    {
        _text = "A gift, freely given, is remembered.";
        ClearOptions();
        int shown = 0;
        foreach (var (id, n) in new List<(ItemId, int)>(_player.Inventory.All()))
        {
            var def = Items.Items.Of(id);
            AddOption($"Give {def.Name} (worth {def.ValueMb:0.#} MB)", () =>
            {
                _player.Inventory.Remove(id); npc.Inventory.Add(id);
                npc.GiftsFromPlayerMb += def.ValueMb;
                float gain = 4f + def.ValueMb * 6f;
                // Drink is the quick way to a man's good opinion, and he drinks it now.
                if (id == ItemId.Rum || id == ItemId.Ale) { gain *= 2f; npc.Inventory.Remove(id); npc.Consume(id); npc.PlayAnimation("drink"); if (npc.IsBandPerson) (npc.HomeStores as Economy.BandLedger)?.OnRum(1); }
                npc.ChangeRapport(_player, gain);
                ShowTopics($"{npc.DisplayName} accepts the {def.Name.ToLower()}. (rapport {npc.RapportWith(_player):0})");
            });
            if (++shown >= 7) break;
        }
        AddOption("Back", () => ShowTopics(Greeting(npc)));
    }

    // Marriage (design doc 8): standing with the father, a bride-price in
    // goods already given, and the HBC rule of one wife.
    private string Propose(Npc father, Npc daughter)
    {
        if (_player.Spouse != null) return "\"You have a wife already. The Company allows a man one.\"";
        float rapport = father.RapportWith(_player);
        float gifts = father.GiftsFromPlayerMb;
        if (rapport < 30f) return $"{father.DisplayName} hears you out and says nothing. You are a stranger to him yet. (rapport {rapport:0}, needs 30)";
        if (gifts < 8f) return $"\"A daughter is not given for nothing.\" He looks at what you have brought him. (gifts worth {gifts:0.#} MB, wants 8)";
        daughter.Spouse = _player;
        _player.Spouse = daughter;
        _player.MarriedOnDay = GameCalendar.Instance.Day;
        _player.Learn(daughter, 0.3f);
        daughter.Role = Role.Wife;
        daughter.HomeStores = Sim.Fort;
        daughter.BedCell = World.IsoGrid.Cell(World.WorldLayout.Depot.Top, 4, 3);
        daughter.WorkCell = World.WorldLayout.Depot.Centre;
        daughter.Brain.Clear(daughter);
        daughter.ResetBehaviour();
        foreach (var n in Sim.Npcs) if (n.HomeStores == father.HomeStores) n.ChangeRapport(_player, 10f);
        Sim.Notice($"{daughter.DisplayName} has married {_player.DisplayName} in the custom of the country and moves into the post.");
        return $"\"So be it.\" {daughter.DisplayName} will come to live at the post. Her family is now your family.";
    }
}
