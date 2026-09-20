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

    public bool IsOpen => !_closed && _npc != null && GodotObject.IsInstanceValid(_npc);

    public Godot.Collections.Dictionary Start(Npc npc)
    {
        if (_npc != null && GodotObject.IsInstanceValid(_npc) && _npc != npc) _npc.Conversing = false;
        _npc = npc;
        _closed = false;
        npc.Conversing = true;
        npc.StopMoving();
        npc.FaceToward(_player.GlobalPosition);
        ShowTopics(Greeting(npc));
        return State();
    }

    // The player picked option i (or -1 to walk away).
    public Godot.Collections.Dictionary Step(int npcId, int option)
    {
        if (_npc == null || !GodotObject.IsInstanceValid(_npc) || _npc.NetId != npcId || _closed) { Close(); return State(); }
        if (_npc.IsDead || _npc.GlobalPosition.DistanceTo(_player.GlobalPosition) > 220f) { Close(); return State(); }
        if (option < 0 || option >= _options.Count) { Close(); return State(); }
        _options[option].action();
        return State();
    }

    private void Close()
    {
        if (_npc != null && GodotObject.IsInstanceValid(_npc)) _npc.Conversing = false;
        _closed = true;
    }

    public Godot.Collections.Dictionary State()
    {
        var opts = new Godot.Collections.Array();
        foreach (var (label, _) in _options) opts.Add(label);
        return new Godot.Collections.Dictionary
        {
            ["closed"] = _closed, ["npc"] = _npc != null && GodotObject.IsInstanceValid(_npc) ? _npc.NetId : -1,
            ["title"] = _npc != null && GodotObject.IsInstanceValid(_npc) ? _npc.DisplayName : "", ["text"] = _text, ["options"] = opts,
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
        string state = npc.IsSick ? " They are grey with fever - keep your distance." : npc.IsDrunk ? " They are drunk." : npc.Needs.Hunger < 35 ? " They look hungry." : npc.Needs.Warmth < 35 ? " They are shivering." : npc.Needs.Health < 50 ? " They are hurt." : "";
        return $"{npc.DisplayName}, {role}, greets you{(mood == "" ? "" : " " + mood)}.{busy}{state}";
    }

    private void ShowTopics(string text)
    {
        _text = text;
        ClearOptions();
        var npc = _npc;

        if (npc.Role == Role.CreeChief && _player.Task.Kind == TaskKind.DeliverLetter && !_player.Task.IsComplete)
            AddOption("Hand him the Factor's letter", () => ShowTopics(DeliverLetter(npc)));
        AddOption("Ask how things stand", () => ShowTopics(AskNews(npc)));
        AddOption("Ask about them", () => ShowTopics(AskAboutThem(npc)));
        AddOption("Ask about the others", () => ShowTopics(AskGossip(npc)));
        if (npc.Faction == Faction.Cree && (npc.Role == Role.CreeChief || npc.Role == Role.CreeHunter))
            AddOption("Ask about marriage in the custom of the country", () => ShowTopics(AskMarriage(npc)));
        if (npc.Needs.Hunger < 45f && _player.Inventory.FirstOfKind(ItemKind.Food) is ItemId food)
            AddOption($"Offer them something to eat ({Items.Items.Of(food).Name})", () => ShowTopics(OfferFood(npc, food)));
        if (npc.Faction != Faction.HBC || npc.Role == Role.Labourer)
            AddOption("Threaten them", () => ShowTopics(Threaten(npc)));
        if (npc.Faction == Faction.Cree && npc.Role is Role.CreeChief or Role.CreeHunter)
            AddOption("Ask about the hunting", () => ShowTopics(AskHunting(npc)));
        if (npc.Role == Role.Factor) AddOption("Ask for work", () => ShowTopics(AskForWork(npc)));
        if (npc.Role == Role.Factor && Sim.Fort != null)
        {
            int news = 0;
            foreach (var sname in _player.KnownSites) if (!Sim.Fort.Knows(sname)) news++;
            if (news > 0) AddOption($"Tell him where the game is ({news} places)", () => ShowTopics(TellSites(npc)));
        }
        if (npc.Role == Role.Clerk || npc.Role == Role.Factor || npc.Role == Role.CreeChief || npc.Role == Role.NwcPartner)
            AddOption("Trade", () => ShowTrade(npc));
        if (npc.Role == Role.NwcPartner && _player.Faction != Faction.NWC)
            AddOption("Ask about signing on with the North West Company", () => ShowTopics(OfferNwc(npc)));
        if (npc.Role == Role.NwcPartner && _player.Faction != Faction.NWC && _player.WantsToDefect)
            AddOption("Sign on. Leave the Company's service.", () => ShowTopics(Defect(npc)));
        if (npc.Role == Role.Factor && _player.Faction == Faction.NWC)
            AddOption("Ask to be taken back into the Company's service", () => ShowTopics(Return(npc)));
        AddOption("Give a gift", () => ShowGive(npc));
        if (npc.Role == Role.CreeChief || (npc.Role == Role.CreeHunter && npc.Children.Count > 0))
        {
            foreach (var d in npc.Children)
                if (d.Role == Role.CreeDaughter && d.Spouse == null && !d.IsDead)
                    AddOption($"Ask for {d.DisplayName}'s hand", () => ShowTopics(Propose(npc, d)));
        }
        AddOption("Leave", Close);
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
        npc.ChangeRapport(_player, 1f);
        return s == "" ? "\"Nothing to tell.\"" : s;
    }

    // A message takes effect when it's read (design doc 10.1), and it has
    // to be carried there by someone.
    private string DeliverLetter(Npc chief)
    {
        var band = Sim.Band;
        _player.Task.Progress(TaskKind.DeliverLetter, 1);
        if (band == null) return "\"...\"";
        band.InvitedToTrade = true;
        band.StandingWithCompany = Mathf.Min(100f, band.StandingWithCompany + 6f);
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
        npc.ChangeRapport(_player, 1.5f);
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
        foreach (var m in npc.Memories)
        {
            string hedge = m.Directness == 1 ? "\"I saw it myself: " : m.Directness == 2 ? "\"I was told " : "\"They say ";
            lines.Add($"{hedge}{m.Text}\"");
        }
        if (lines.Count == 0) return "\"Nothing worth telling.\"";
        int n = npc.Personality.Talkative > 0.6f ? 4 : npc.Personality.Talkative > 0.3f ? 2 : 1;
        var picked = new List<string>();
        for (int i = 0; i < n && lines.Count > 0; i++) { int k = (int)(GD.Randi() % (uint)lines.Count); picked.Add(lines[k]); lines.RemoveAt(k); }
        npc.ChangeRapport(_player, 0.5f);
        return string.Join(" ", picked) + (npc.Personality.Talkative < 0.3f ? " They say no more." : "");
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
        foreach (var site in eco.Sites)
        {
            if (site.IsDead) s += $"\"{site.Name} is finished - hunted out. Nothing comes back there.\" ";
            else if (site.Scarcity) s += $"\"{site.Name} is crowded and hungry; the animals are wandering toward your post.\" ";
            else if (site.Count < site.Capacity * 0.4f) s += $"\"{site.Name} is getting thin.\" ";
        }
        // Knowledge is currency (design doc 10): the sites are told, not shown,
        // and only to someone the hunter has some regard for.
        npc.ChangeRapport(_player, 1f);
        if (npc.RapportWith(_player) >= 15f)
        {
            int learned = 0;
            foreach (var site in eco.Sites) if (_player.KnownSites.Add(site.Name)) learned++;
            if (learned > 0) s += $" He describes where the game is: {learned} places you did not know. The Factor would want to hear it.";
        }
        else s += " He says nothing of where the game is. You are not yet a friend.";
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
        foreach (var sname in _player.KnownSites) if (Sim.Fort.Learn(sname)) told++;
        _player.Standing += 3f * told;
        factor.ChangeRapport(_player, 4f * told);
        return told > 0 ? $"\"Good man. That is worth knowing.\" He notes it in the journal. (standing +{3 * told})" : "\"You have told me nothing new.\"";
    }

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
        _text = npc.Faction == Faction.Cree
            ? "The band will trade meat and hides for goods, one for one by worth."
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
        int shown = 0;
        foreach (var (id, n) in new List<(ItemId, int)>(counter.All()))
        {
            var def = Items.Items.Of(id);
            if (def.Kind == ItemKind.Fur && npc.Faction != Faction.Cree) continue;
            if (id == ItemId.TradeGoods) continue;
            int cost = Mathf.Max(1, Mathf.CeilToInt(def.ValueMb * (def.Kind == ItemKind.Food ? fort.FoodPriceMultiplier : 1f)));
            ItemId currency = ItemId.TradeGoods;
            if (_player.Inventory.Count(currency) >= cost)
            {
                AddOption($"Buy {def.Name} ({n} in store) for {cost} trade goods", () =>
                {
                    counter.Remove(id); _player.Inventory.Add(id); _player.Inventory.Remove(currency, cost); counter.Add(currency, cost);
                    ShowTrade(npc);
                });
                if (++shown >= 4) break;
            }
        }
        if (_options.Count == 0) _text += "\n\nYou have nothing they want, or they nothing you can pay for. Trade goods are the coin here.";
        AddOption("Back", () => ShowTopics(Greeting(npc)));
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
