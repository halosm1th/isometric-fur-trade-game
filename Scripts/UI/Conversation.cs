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

    public bool IsOpen => !_closed && (_stores != null || _npc != null && GodotObject.IsInstanceValid(_npc));

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
        _npc = npc; _stores = null;
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
        _stores = null;
        _closed = true;
    }

    public Godot.Collections.Dictionary State()
    {
        var opts = new Godot.Collections.Array();
        foreach (var (label, _) in _options) opts.Add(label);
        return new Godot.Collections.Dictionary
        {
            ["closed"] = _closed, ["npc"] = _stores != null ? StoresId : _npc != null && GodotObject.IsInstanceValid(_npc) ? _npc.NetId : -1,
            ["title"] = _stores != null ? _stores.Title : _npc != null && GodotObject.IsInstanceValid(_npc) ? _npc.DisplayName : "", ["text"] = _text, ["options"] = opts,
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
        string busy = npc.Brain.Top != null ? $" They were {npc.Brain.Top.Label}." : "";
        string debt = "";
        foreach (var g in Grudges.HeldBy(npc.DisplayName)) if (g.Against == _player.DisplayName) { debt = $" There is blood between you: {g.Victim}. {(npc.IsFighter ? "Their hand is on their knife." : "They will hear what you offer for it, or nothing.")}"; break; }
        if (debt == "" && _player.Spouse is Npc wife && npc.Faction == Faction.Cree)
            foreach (var g in Grudges.HeldBy(npc.DisplayName)) { debt = $" Your wife's people have a debt unanswered: {g.Against} killed {g.Victim}. As kin, it is yours to help settle."; break; }
        string tongue = npc.Faction == Faction.Cree && _player.EffectiveFluency < 0.4f ? (_player.HasInterpreter ? "" : " You have little of their tongue; you get by with signs and a few words.") : "";
        string state = npc.IsSick ? " They are grey with fever - keep your distance." : npc.HasScurvy ? " Their gums are black with scurvy." : npc.IsDrunk ? " They are drunk." : npc.Needs.Hunger < 35 ? " They look hungry." : npc.Needs.Warmth < 35 ? " They are shivering." : npc.Needs.Health < 50 ? " They are hurt." : "";
        if (npc.Faction == Faction.Cree && !_player.HasInterpreter) _player.CreeFluency = Mathf.Min(1f, _player.CreeFluency + 0.02f);   // every talk teaches a little
        return $"{npc.DisplayName}, {role}, greets you{(mood == "" ? "" : " " + mood)}.{busy}{state}{debt}{tongue}";
    }

    private void ShowTopics(string text)
    {
        _text = text;
        ClearOptions();
        var npc = _npc;
        bool leader = npc.Role == Role.Factor || npc.Role == Role.NwcPartner;
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
            bool kin = _player.Spouse is Npc && npc.Faction == Faction.Cree;
            if (mine || kin) AddOption($"Offer restitution for {g.Victim} ({g.DebtMb:0} trade goods{(mine ? "" : $", on {g.Against}'s behalf")})", () => ShowTopics(Restitution(npc, g)));
        }

        AddOption("Talk...", () => ShowTalk(npc));
        if (ownLeader || (ownMan && canCommand) || npc.Following == _player || CanAskAlong(npc)) AddOption("Work and orders...", () => ShowWork(npc));
        if (npc.Role == Role.Clerk || npc.Role == Role.Factor || npc.Role == Role.CreeChief || npc.Role == Role.NwcPartner)
            AddOption("Trade", () => ShowTrade(npc));
        AddOption("Give a gift", () => ShowGive(npc));
        if (npc.Needs.Hunger < 45f && _player.Inventory.FirstOfKind(ItemKind.Food) is ItemId food)
            AddOption($"Offer them something to eat ({Items.Items.Of(food).Name})", () => ShowTopics(OfferFood(npc, food)));
        if (npc.Faction == Faction.Cree && (npc.Role == Role.CreeChief || npc.Role == Role.CreeHunter)) AddOption("Family and marriage...", () => ShowFamily(npc));
        if (leader && (Economy.Rivalry.AreRivals(npc.Faction, _player.Faction) || ownLeader && Sim.Nwc != null)) AddOption("The companies...", () => ShowCompanies(npc));
        if (npc.Faction != Faction.HBC || npc.Role == Role.Labourer)
            AddOption("Threaten them", () => ShowTopics(Threaten(npc)));
        AddOption("Leave", Close);
    }

    private bool CanAskAlong(Npc npc) => !npc.IsFemale && npc.Role != Role.Factor && npc.Role != Role.NwcPartner && npc.Role != Role.Clerk && npc.Role != Role.Sailor;

    private void ShowTalk(Npc npc)
    {
        _text = $"{npc.DisplayName} waits to hear what you want to know.{Bored(npc)}";
        ClearOptions();
        bool canTalk = npc.Faction != Faction.Cree || _player.EffectiveFluency >= 0.3f;   // design doc 10.6
        AddOption("How do things stand?", () => { _text = AskNews(npc); ShowTalk(npc); });
        AddOption("Tell me about yourself", () => { _text = AskAboutThem(npc); ShowTalk(npc); });
        AddOption("What's the talk? (rumours)", () => { _text = AskGossip(npc); ShowTalk(npc); });
        if ((npc.Faction == Faction.Cree && npc.Role is Role.CreeChief or Role.CreeHunter) || npc.Role is Role.Labourer or Role.Voyageur or Role.NwcPartner)
            AddOption("Where is the game?", () => { _text = AskHunting(npc); ShowTalk(npc); });
        if (npc.Role == Role.Factor && Sim.Fort != null)
        {
            int news = 0;
            foreach (var sname in _player.KnownSites) if (!Sim.Fort.Knows(sname)) news++;
            if (news > 0) AddOption($"Tell him where the game is ({news} places)", () => { _text = TellSites(npc); ShowTalk(npc); });
        }
        if ((npc.Role == Role.Factor || npc.Role == Role.NwcPartner) && npc.Faction == _player.Faction)
            AddOption(npc.Role == Role.Factor ? "What does London want of us?" : "What do the partners want of us?", () => { _text = AskDirective(npc); ShowTalk(npc); });
        if (npc.Role == Role.NwcPartner && Sim.Nwc != null && Sim.Nwc.DirectiveReadDay > -100 && npc.Faction == _player.Faction)
            AddOption("What did the council decide?", () => { _text = $"\"{Sim.Nwc.DirectiveText}\""; ShowTalk(npc); });
        if (npc.Faction == Faction.Cree && !canTalk) _text += " (You have only signs and a few words with them.)";
        AddOption("Back", () => ShowTopics(Greeting(npc)));
    }

    private void ShowWork(Npc npc)
    {
        _text = npc.Role == Role.Factor || npc.Role == Role.NwcPartner ? "\"Yes?\"" : $"{npc.DisplayName} straightens up.";
        ClearOptions();
        bool leader = npc.Role == Role.Factor || npc.Role == Role.NwcPartner;
        bool ownLeader = leader && npc.Faction == _player.Faction;
        bool ownMan = npc.Faction == _player.Faction && (npc.Role == Role.Labourer || npc.Role == Role.Voyageur);
        bool canCommand = _player.Rank == "clerk" || _player.Rank == "postmaster" || _player.Standing >= 35f;
        if (npc.Role == Role.Factor) AddOption("Ask for work", () => { _text = AskForWork(npc); ShowWork(npc); });
        if (ownLeader)
        {
            if (_player.Task.Kind != TaskKind.None && !_player.Task.IsComplete && _player.Task.Kind != TaskKind.Report)
            {
                AddOption("Decline today's task", () => { _text = DeclineTask(npc); ShowWork(npc); });
                AddOption("Ask for a different task", () => { _text = CounterTask(npc); ShowWork(npc); });
            }
            AddOption("Volunteer for something...", () => ShowVolunteer(npc));
            if (npc.Role == Role.Factor && Sim.Nwc != null) AddOption("\"I'll be across the river a few days.\"", () => { _text = Authorize(npc); ShowWork(npc); });
        }
        if (ownMan && canCommand) AddOption("Give orders...", () => ShowOrders(npc));
        if (npc.Following == _player) AddOption("\"That will do - carry on.\"", () => { npc.Following = null; _text = $"{npc.DisplayName} nods and goes back to their own business."; ShowWork(npc); });
        else if (CanAskAlong(npc) && !(ownMan && canCommand)) AddOption("Ask them to come with you", () => { _text = AskAlong(npc); ShowWork(npc); });
        AddOption("Back", () => ShowTopics(Greeting(npc)));
    }

    private void ShowFamily(Npc npc)
    {
        bool canTalk = _player.EffectiveFluency >= 0.3f;
        _text = canTalk ? $"{npc.DisplayName} listens." : NoWords(npc);
        ClearOptions();
        if (canTalk)
        {
            AddOption("Ask about marriage in the custom of the country", () => { _text = AskMarriage(npc); ShowFamily(npc); });
            foreach (var d in npc.Children)
                if (d.Role == Role.CreeDaughter && d.Spouse == null && !d.IsDead)
                    AddOption($"Ask for {d.DisplayName}'s hand", () => { _text = Propose(npc, d); ShowFamily(npc); });
        }
        AddOption("Back", () => ShowTopics(Greeting(npc)));
    }

    private void ShowCompanies(Npc npc)
    {
        _text = Economy.Rivalry.OpenHostility ? "\"Between the companies it is open hostility. Speak carefully.\"" : $"\"The companies: {Economy.Rivalry.Describe()}.\"";
        ClearOptions();
        bool ownLeader = npc.Faction == _player.Faction;
        // Papers for sale (design doc 10.3).
        foreach (var m in new List<Message>(_player.Mail))
            if (!m.Read && Economy.Rivalry.AreRivals(m.Company, npc.Faction))
                AddOption($"Sell them the {m.Kind} from {m.From} ({PacketPrice(m)} trade goods)", () => { _text = SellPacket(npc, m); ShowCompanies(npc); });
        if (ownLeader && Economy.Rivalry.Tension >= 40f && Economy.Rivalry.PostOf(Economy.Rivalry.RivalOf(npc.Faction)) != null)
            AddOption(npc.Faction == Faction.HBC ? "Propose seizing the Nor'Westers' stores" : "Propose seizing the Company's stores", () => { _text = ProposeRaid(npc); ShowCompanies(npc); });
        if (!ownLeader && Economy.Rivalry.Tension >= 35f)
            AddOption("Propose a truce between the companies", () => { _text = ProposeTruce(npc); ShowCompanies(npc); });
        if (npc.Role == Role.NwcPartner && _player.Faction != Faction.NWC)
            AddOption("Ask about signing on with the North West Company", () => { _text = OfferNwc(npc); ShowCompanies(npc); });
        if (npc.Role == Role.NwcPartner && _player.Faction != Faction.NWC && _player.WantsToDefect)
            AddOption("Sign on. Leave the Company's service.", () => { _text = Defect(npc); ShowCompanies(npc); });
        if (npc.Role == Role.Factor && _player.Faction == Faction.NWC)
            AddOption("Ask to be taken back into the Company's service", () => { _text = Return(npc); ShowCompanies(npc); });
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
        else if (npc.Faction == Faction.Cree)
        {
            foreach (var n in Sim.Npcs) if (n.Faction == Faction.Cree && !n.IsDead) mouths++;
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
            else if (other.Spouse == null && !other.IsFemale && other.Role != Role.Factor && other.Role != Role.Sailor) lines.Add($"\"{other.DisplayName} wants a wife, if you ask me.\"");
        }
        // What they actually know, secondhand or not (design doc 10.2).
        bool garbled = npc.Faction == Faction.Cree && _player.EffectiveFluency < 0.5f;
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

    private string NoWords(Npc npc) => $"You have not the words for it, and {npc.DisplayName} has not yours. Signs will buy meat; they will not carry this. Learn more of the tongue, or bring someone who has it - your wife, or a woman of the post.";

    private string Restitution(Npc holder, Grudge g)
    {
        int cost = Mathf.CeilToInt(g.DebtMb);
        if (_player.Inventory.Count(ItemId.TradeGoods) < cost) return $"\"{cost} pieces of goods, laid before the family. You have not got it.\" (you carry {_player.Inventory.Count(ItemId.TradeGoods)})";
        _player.Inventory.Remove(ItemId.TradeGoods, cost);
        holder.HomeStores?.Stores.Add(ItemId.TradeGoods, cost);
        Grudges.Settle(g, false);
        holder.ChangeRapport(_player, 25f);
        if (Sim.Band != null) Sim.Band.StandingWithCompany = Mathf.Min(100f, Sim.Band.StandingWithCompany + 12f);
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
        if (npc.Faction == Faction.Cree) Sim.Band?.OnInsult();
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
    private void ShowTrade(Npc npc)
    {
        var fort = Sim.Fort; var band = Sim.Band;
        var post = npc.Faction == Faction.NWC ? (Economy.FortLedger)Sim.Nwc : Sim.Fort;
        if (npc.Faction != Faction.Cree)
        {
            fort = post;
            if (fort == null) { ShowTopics("\"Nothing to trade.\""); return; }
            if (fort.Company != _player.Faction) { ShowTopics(_player.Faction == Faction.NWC ? "\"We do not serve Nor'Westers at this counter. Go back across the river.\"" : "\"The Company's servant wants to trade with us? Sign on first, or take your furs to your own Factor.\""); return; }
        }
        Inventory counter = npc.Faction == Faction.Cree ? band?.Stores : fort?.Stores;
        if (counter == null) { ShowTopics("\"Nothing to trade.\""); return; }
        bool crude = npc.Faction == Faction.Cree && _player.EffectiveFluency < 0.5f;   // design doc 10.6: signs and fingers
        _text = npc.Faction == Faction.Cree
            ? (crude ? "Without the words, it is fingers and pointing: the band trades meat and hides for goods, and you pay a piece more for the misunderstanding." : "The band will trade meat and hides for goods, one for one by worth.")
            : $"The Company sells from the stores at cost and buys furs at {fort.PeltPrice:0.00} MB in goods per MB.";
        ClearOptions();

        // Sell furs / buy with trade goods.
        foreach (var (id, n) in new List<(ItemId, int)>(_player.Inventory.All()))
        {
            var def = Items.Items.Of(id);
            if (npc.Faction != Faction.Cree && def.Kind == ItemKind.Fur)
            {
                int pay = Mathf.Max(1, Mathf.RoundToInt(def.ValueMb * fort.PeltPrice));
                if (counter.Count(ItemId.TradeGoods) >= pay)
                    AddOption($"Sell {def.Name} for {pay} trade goods", () =>
                    {
                        _player.Inventory.Remove(id); counter.Add(id); counter.Remove(ItemId.TradeGoods, pay); _player.Inventory.Add(ItemId.TradeGoods, pay);
                        fort.FursTakenMb += def.ValueMb; fort.GoodsPaidMb += pay;
                        ShowTrade(npc);
                    });
            }
            if (_options.Count >= 4) break;
        }
        // Buy from the counter with trade goods (or furs at the camp).
        int shown = 0, skipped = 0; bool more = false;
        foreach (var (id, n) in new List<(ItemId, int)>(counter.All()))
        {
            var def = Items.Items.Of(id);
            if (def.Kind == ItemKind.Fur && npc.Faction != Faction.Cree) continue;
            if (id == ItemId.TradeGoods) continue;
            if (shown >= 4) { more = true; break; }
            if (skipped < _tradePage * 4) { skipped++; continue; }
            int cost = Mathf.Max(1, Mathf.CeilToInt(def.ValueMb * (def.Kind == ItemKind.Food ? fort.FoodPriceMultiplier : 1f))) + (crude ? 1 : 0);
            ItemId currency = ItemId.TradeGoods;
            if (_player.Inventory.Count(currency) >= cost)
            {
                AddOption($"Buy {def.Name} ({n} in store) for {cost} trade goods", () =>
                {
                    counter.Remove(id); _player.Inventory.Add(id); _player.Inventory.Remove(currency, cost); counter.Add(currency, cost);
                    ShowTrade(npc);
                });
                shown++;
            }
        }
        if (_options.Count == 0 && _tradePage == 0) _text += "\n\nYou have nothing they want, or they nothing you can pay for. Trade goods are the coin here.";
        if (more) AddOption("More of the stores...", () => { _tradePage++; ShowTrade(npc); });
        else if (_tradePage > 0) AddOption("Back to the first shelf", () => { _tradePage = 0; ShowTrade(npc); });
        AddOption("Back", () => { _tradePage = 0; ShowTopics(Greeting(npc)); });
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
                if (id == ItemId.Rum || id == ItemId.Ale) { gain *= 2f; npc.Inventory.Remove(id); npc.Consume(id); npc.PlayAnimation("drink"); if (npc.Faction == Faction.Cree) Sim.Band?.OnRum(1); }
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
        _player.CreeFluency = Mathf.Min(1f, _player.CreeFluency + 0.3f);
        daughter.Role = Role.Wife;
        daughter.HomeStores = Sim.Fort;
        daughter.BedCell = World.IsoGrid.Cell(World.WorldLayout.Depot.Top, 4, 3);
        daughter.WorkCell = World.WorldLayout.Depot.Centre;
        daughter.Brain.Clear(daughter);
        daughter.ResetBehaviour();
        foreach (var n in Sim.Npcs) if (n.Faction == Faction.Cree) n.ChangeRapport(_player, 10f);
        Sim.Notice($"{daughter.DisplayName} has married {_player.DisplayName} in the custom of the country and moves into the post.");
        return $"\"So be it.\" {daughter.DisplayName} will come to live at the post. Her family is now your family.";
    }
}
