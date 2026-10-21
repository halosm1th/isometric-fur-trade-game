using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;

namespace IsometricFurTradeGame.Scripts.UI;

// Getting to know someone (design doc 3.6, 11.4, P.8). The rest of the
// conversation is business - news, trade, orders, favours. This is the
// talk people have when there is no business: where they come from, who
// they have, what they want, what eats at them, what they make of the
// people around them, and what they make of you. Every answer is read off
// state the person already has (personality, rapport, kin, grudges,
// memories, the house's leader, hunger and fever) - nothing is a canned
// script beyond the turn of phrase. How much they open up rests on trust:
// their rapport with you, how talkative they are, how suspicious.
// They remember what you have talked about (Acquaintance below), so a
// second asking gets "as I told you", and the next greeting knows you.
public partial class Conversation
{
    // How far this person will open up to you: rapport, plus a talker's
    // looseness, less a suspicious man's guard. Roughly -40..120.
    private float Trust(Npc npc) => npc.RapportWith(_player) + npc.Personality.Talkative * 20f - npc.Personality.Suspicion * 20f
        + (_player.Spouse != null && npc.IsBandPerson && _player.KinNames.Contains(npc.DisplayName) ? 30f : 0f);

    private void ShowAcquaint(Npc npc, string said = null)
    {
        float trust = Trust(npc);
        var known = Acquaintance.Topics(_player, npc);
        _text = said ?? (known.Count == 0
            ? $"{npc.DisplayName} looks at you, waiting to see what kind of talk this is.{Bored(npc)}"
            : $"{npc.DisplayName} settles in; you have talked before.{Bored(npc)}");
        ClearOptions();
        AddOption(Asked(known, "home", "Where do you come from?"), () => ShowAcquaint(npc, AskHome(npc)));
        AddOption(Asked(known, "family", "Do you have people?"), () => ShowAcquaint(npc, AskFamily(npc)));
        if (trust >= 5f) AddOption(Asked(known, "want", "What do you want out of all this?"), () => ShowAcquaint(npc, AskWant(npc)));
        if (trust >= 20f) AddOption(Asked(known, "trouble", "What's troubling you?"), () => ShowConfidence(npc, AskTrouble(npc)));
        // A word you promised someone else to put in with this person.
        foreach (var asker in PromisedWordWith(npc)) { var a = asker; AddOption($"Put in a word for {a.DisplayName}", () => ShowAcquaint(npc, PutInWord(npc, a))); }
        if (npc.HomeStores?.Leader is Npc master && master != npc && trust >= 15f)
            AddOption($"What do you make of {master.DisplayName}?", () => ShowAcquaint(npc, AskOpinion(npc, master)));
        if (Acquaintances(npc).Count > 0) AddOption("What do you make of...", () => ShowAskAbout(npc));
        AddOption("What do you make of me?", () => ShowAcquaint(npc, AskOfMe(npc)));
        if (_player.Inventory.Count(ItemId.Tobacco) > 0) AddOption(known.Exists(k => k.StartsWith("pipe-day:")) ? "Share a pipe with them again (1 tobacco)" : "Share a pipe with them (1 tobacco)", () => ShowAcquaint(npc, SharePipe(npc)));
        AddOption("Tell them about yourself", () => ShowAcquaint(npc, TellOfSelf(npc)));
        AddOption("Back", () => ShowTalk(npc));
    }

    private static string Asked(List<string> known, string topic, string label) => known.Contains(topic) ? label + " (asked)" : label;

    // A topic asked again is answered shortly and warms nobody.
    private bool Again(Npc npc, string topic, out string prefix)
    {
        bool again = Acquaintance.Has(_player, npc, topic);
        prefix = again ? $"{npc.DisplayName} has told you this before. " : "";
        if (!again) Acquaintance.Add(_player, npc, topic);
        return again;
    }

    // A stable per-person choice from a list (no save needed: the same man
    // always comes from the same parish). FNV-1a, not string.GetHashCode,
    // which changes between runs.
    private static T Pick<T>(Npc npc, string salt, T[] from)
    {
        uint h = 2166136261;
        foreach (char c in npc.DisplayName + salt) { h ^= c; h *= 16777619; }
        return from[h % (uint)from.Length];
    }

    // ---- the topics ------------------------------------------------------------------

    private static readonly string[] Orkney = { "Stromness", "Birsay", "Harray", "South Ronaldsay", "Sandwick", "Firth", "Orphir", "Stenness", "Evie", "Hoy" };
    private static readonly string[] Parishes = { "Sorel", "Berthier", "Maskinongé", "Rivière-du-Loup", "Trois-Rivières", "Lachine", "Saint-Ours", "Yamachiche" };
    private static readonly string[] Highlands = { "Inverness", "Glengarry", "Strathglass", "Kintail", "Badenoch" };
    private static readonly string[] Missouri = { "St. Louis", "Ste. Genevieve", "Kaskaskia", "Kentucky", "Cahokia" };

    private string AskHome(Npc npc)
    {
        bool again = Again(npc, "home", out string pre);
        string s;
        if (npc.IsBandPerson)
        {
            string country = npc.HomeStores is Economy.CountryBand cb ? cb.Country.Name : npc.HomeStores?.Title?.Replace(" cache", "") ?? "this country";
            s = npc.Personality.Talkative > 0.5f
                ? $"\"Here. {Cap(country)}. My grandmother's grandmother was born a day's walk from where you stand, and she knew every creek of it by its own name.\""
                : $"\"Here. This is our country.\" A wave of the hand takes in all of it.";
        }
        else s = npc.Faction switch
        {
            Faction.NWC when npc.Role == Role.NwcPartner => $"\"{Pick(npc, "h", Highlands)}, by way of Montreal. The partners are mostly Highland men; we look after our own.\"",
            Faction.NWC => $"\"{Pick(npc, "p", Parishes)}, on the river below Montreal. My father farmed and my brother farms. I paddle.\"",
            Faction.American => $"\"{Pick(npc, "m", Missouri)}. Came up the river on a keelboat; I'll go down on one, God willing, with something to show for it.\"",
            Faction.ShipsCrew => "\"Wapping, near enough. The ship's my parish.\"",
            _ when npc.Role is Role.Woman or Role.Wife => "\"From the Homeguard camp, by the river. My mother's people trade here and have since before the old fort burned.\"",
            _ when npc.Role is Role.Factor or Role.Clerk or Role.Postmaster => $"\"{Pick(npc, "k", new[] { "Kirkwall", "Edinburgh", "Stromness", "London", "Aberdeen" })}. The Company takes its clerks young; I have been out here longer than I was ever there.\"",
            _ => $"\"{Pick(npc, "o", Orkney)}, in Orkney. Peat and sheep and the sea. You can see the whole parish from the hill behind our house.\"",
        };
        if (!again) npc.Warm(_player, 1.5f + npc.Personality.Talkative);
        return pre + s;
    }

    private string AskFamily(Npc npc)
    {
        bool again = Again(npc, "family", out string pre);
        var parts = new List<string>();
        if (npc.Spouse is Npc sp) parts.Add(sp.IsDead ? $"\"I had a wife - {sp.DisplayName}. She is buried here.\"" : $"\"{sp.DisplayName} is my {(npc.IsFemale ? "husband" : "wife")}.\"");
        var kids = npc.Children.FindAll(c => !c.IsDead);
        if (kids.Count > 0) parts.Add($"\"{(kids.Count == 1 ? "A child" : $"{kids.Count} children")}: {string.Join(", ", kids.ConvertAll(c => c.DisplayName))}.\"{(npc.Personality.Talkative > 0.4f ? " They brighten saying it." : "")}");
        if (npc.Father is Npc fa) parts.Add(fa.IsDead ? $"\"My father was {fa.DisplayName}. He is dead.\"" : $"\"{fa.DisplayName} is my father.\"");
        if (npc.Role is Role.Woman or Role.Wife && !npc.IsBandPerson)
            parts.Add("\"My mother's people are the Homeguard, camped by the river. Half the post is my cousin, one way or another.\"");
        else if (!npc.IsBandPerson && npc.Spouse == null && kids.Count == 0)
            parts.Add(npc.Age < 30
                ? $"\"A mother and {Pick(npc, "sib", new[] { "two sisters", "a brother at the fishing", "a young brother", "a sister in service" })} at home. The pay goes to them, most of it.\""
                : $"\"Nobody, out here. There was a girl at home once; she will have married by now.\"");
        if (npc.IsBandPerson && parts.Count == 0) parts.Add("\"The whole camp is my family. Ask who is not.\"");
        if (!again) npc.Warm(_player, 2f);
        return pre + string.Join(" ", parts);
    }

    private string AskWant(Npc npc)
    {
        bool again = Again(npc, "want", out string pre);
        var p = npc.Personality;
        string s = npc.Role switch
        {
            Role.Labourer when p.Ambition > 0.6f => "\"To be steersman of a boat, then a postmaster. Men have done it. Not many.\"",
            Role.Labourer when npc.Faction == Faction.American => "\"A stake. Enough beaver to buy a farm on the Missouri bottoms and never see a trap again.\"",
            Role.Labourer => $"\"My time out, and home with enough put by to {Pick(npc, "w", new[] { "take a lease on a farm", "marry", "buy a share in a boat", "keep my mother" })}. That is all any of us want.\"",
            Role.Voyageur when p.Ambition > 0.6f => "\"To be a guide. A guide is paid twice what I am, and nobody tells him where to put his paddle.\"",
            Role.Voyageur => "\"To go home rich and spend it all at Lachine in a week. Then come back.\" He laughs; he means it.",
            Role.Clerk => p.Ambition > 0.5f ? "\"A post of my own. Then a district. They say the Committee reads every report; I mean them to read mine.\"" : "\"To get through my years and go home with a good character from the Factor.\"",
            Role.Factor or Role.Postmaster or Role.NwcPartner => p.Ambition > 0.5f ? "\"Returns that make London sit up. A man is remembered for his returns.\"" : "\"To keep my people alive through the winter and send down an honest account. The rest is God's.\"",
            Role.CreeChief => "\"That my people eat, and that the traders remember who brought them their furs.\"",
            Role.CreeHunter => p.Courage > 0.6f ? "\"To be spoken of. A man who brings meat in a hard winter is spoken of for a long time.\"" : "\"Meat in the lodge and peace with our neighbours.\"",
            Role.Woman or Role.Wife or Role.CreeWoman => "\"Children who live. Ask any woman.\"",
            Role.CreeDaughter => "\"A good husband - one who will stay.\" A look; it is not quite a joke.",
            Role.Sailor => "\"The Thames by November and my wages in my hand.\"",
            _ => "\"A quiet life.\"",
        };
        if (!again) npc.Warm(_player, 2f);
        return pre + s;
    }

    // What actually weighs on them, read off their state. A confidence: only
    // asked of people who trust you, and it binds them a little more to you.
    private string AskTrouble(Npc npc)
    {
        bool again = Again(npc, "trouble", out string pre);
        var cares = new List<string>();
        if (npc.IsSick) cares.Add("\"The fever. I am not over it, whatever I tell the master.\"");
        if (npc.HasScurvy) cares.Add("\"My gums bleed and my old cuts open. I want something green.\"");
        if (npc.Needs.Hunger < 40f) cares.Add("\"I am hungry. We are all hungry, but I am hungry.\"");
        foreach (var l in Losses.All) if (l.Home == npc.HomeStores && l.Fresh) { cares.Add($"\"{l.Name}. I keep looking for him at the fire.\""); break; }
        foreach (var g in Grudges.HeldBy(npc.DisplayName)) { cares.Add($"\"{g.Against} killed {g.Victim}, and nothing has been paid for it.\" Their face goes hard."); break; }
        foreach (var g in Grudges.Against(npc.DisplayName)) { cares.Add($"\"{g.Holder} has not forgotten {g.Victim}. I sleep with my knife.\""); break; }
        if (npc.Spouse is Npc sp && sp.IsDead) cares.Add($"\"I miss {sp.DisplayName}.\"");
        if (npc.HomeStores?.Leader is Npc master && master != npc && npc.RapportWith(master) < -15f) cares.Add($"\"{master.DisplayName} has it in for me. I do not know what I did.\"");
        if (cares.Count == 0 && npc.Personality.Ambition > 0.6f) cares.Add("\"That I will be here twenty years and still be what I am now.\"");
        if (cares.Count == 0 && !npc.IsBandPerson) cares.Add("\"Home. Some nights I can smell the peat smoke, I swear it.\"");
        if (cares.Count == 0) cares.Add("\"Nothing I would put on you.\"");
        if (!again) { npc.Warm(_player, 3f); npc.ChangeRapport(_player, 2f); }
        return pre + string.Join(" ", cares.GetRange(0, Mathf.Min(cares.Count, npc.Personality.Talkative > 0.5f ? 3 : 2)));
    }

    // ---- what you do with a confidence ---------------------------------------------
    // Being told something is the start of an obligation (design doc P.8):
    // you can help, counsel, keep it close - or carry it to the master.
    private void ShowConfidence(Npc npc, string said)
    {
        _text = said;
        ClearOptions();
        var master = npc.HomeStores?.Leader as Npc;
        bool sore = master != null && master != npc && npc.RapportWith(master) < -15f;
        if (sore && !Acquaintance.Has(_player, npc, "promise:" + master.DisplayName))
            AddOption($"\"I'll put in a word for you with {master.DisplayName}.\"", () =>
            {
                Acquaintance.Add(_player, npc, "promise:" + master.DisplayName);
                npc.ChangeRapport(_player, 3f);
                _player.Journal.Add($"{GameCalendar.Instance?.DateString}: You promised {npc.DisplayName} you would speak for them to {master.DisplayName}.");
                ShowAcquaint(npc, $"\"Would you? I'd not forget it.\" {npc.DisplayName} looks at you differently. (Speak to {master.DisplayName} - Get to know them - to keep your word.)");
            });
        foreach (var g in Grudges.HeldBy(npc.DisplayName))
        {
            var gg = g;
            AddOption($"Counsel them to let {gg.Victim} rest", () => ShowAcquaint(npc, Counsel(npc, gg)));
            break;
        }
        if (npc.Needs.Hunger < 40f && _player.Inventory.FirstOfKind(ItemKind.Food) is ItemId food)
            AddOption($"Give them something to eat ({Items.Items.Of(food).Name})", () => ShowAcquaint(npc, OfferFood(npc, food)));
        AddOption("\"It stays between us.\"", () => { npc.Warm(_player, 1f); ShowAcquaint(npc, $"{npc.DisplayName} nods. It is the kind of thing people remember."); });
        // Only something the master would want to know is worth carrying to him.
        bool worth = sore || npc.IsSick || Grudges.HeldBy(npc.DisplayName).GetEnumerator().MoveNext();
        if (worth && master != null && master != npc && npc.Faction == _player.Faction)
            AddOption($"(Say nothing - and carry it to {master.DisplayName} later)", () => ShowAcquaint(npc, Inform(npc, master)));
        AddOption("Back", () => ShowAcquaint(npc));
    }

    // People who confided a grievance against this man, and whom you promised to speak for.
    private List<Npc> PromisedWordWith(Npc master)
    {
        var list = new List<Npc>();
        foreach (var o in Sim.Npcs)
            if (!o.IsDead && o != master && o.HomeStores?.Leader == master && Acquaintance.Has(_player, o, "promise:" + master.DisplayName) && !Acquaintance.Has(_player, o, "kept:" + master.DisplayName)) list.Add(o);
        return list;
    }

    // Keeping your word: the master hears you out as far as he rates you, and
    // the man you spoke for hears of it either way - you did what you said.
    private string PutInWord(Npc master, Npc asker)
    {
        Acquaintance.Add(_player, asker, "kept:" + master.DisplayName);
        asker.ChangeRapport(_player, 8f);
        float weight = master.RapportWith(_player) + _player.Standing * 0.5f;
        _player.Journal.Add($"{GameCalendar.Instance?.DateString}: You kept your word and spoke to {master.DisplayName} for {asker.DisplayName}.");
        if (weight >= 20f)
        {
            master.ChangeRapport(asker, 12f);
            return $"\"{asker.DisplayName}? I had not thought of it that way.\" {master.DisplayName} rubs his jaw. \"Very well. I'll look at him again.\" You have done {asker.DisplayName} a real turn.";
        }
        master.ChangeRapport(_player, -2f);
        return $"\"And who asked you?\" {master.DisplayName} hears you out, coldly; you have not the standing to move him. But {asker.DisplayName} will hear that you tried.";
    }

    // Talking someone back from a feud (design doc 15.4): a cautious, loyal
    // man may take the debt down; a bold one resents being told.
    private string Counsel(Npc npc, Grudge g)
    {
        if (Acquaintance.Has(_player, npc, "counselled")) return $"\"You have said your piece about {g.Victim}.\"";
        Acquaintance.Add(_player, npc, "counselled");
        float chance = 0.25f + (1f - npc.Personality.Courage) * 0.35f + Mathf.Clamp(npc.RapportWith(_player), 0f, 60f) / 150f;
        if (GD.Randf() < chance)
        {
            g.DebtMb = Mathf.Max(1f, Mathf.Round(g.DebtMb * 0.6f));
            npc.ChangeRapport(_player, 2f);
            return $"A long silence. \"Maybe. Maybe it does not have to be blood.\" They would take less to settle it now: {g.DebtMb:0} MB.";
        }
        npc.ChangeRapport(_player, -4f);
        return $"\"It was not your {g.Victim}.\" That was the wrong thing to say.";
    }

    // Selling a confidence to the master: it buys a little of his favour, and
    // costs the teller everything if it comes back to him - which, at a post,
    // it usually does.
    private string Inform(Npc npc, Npc master)
    {
        master.ChangeRapport(_player, 3f);
        master.ChangeRapport(npc, -6f);
        _player.Journal.Add($"{GameCalendar.Instance?.DateString}: You told {master.DisplayName} what {npc.DisplayName} said in confidence.");
        if (GD.Randf() < 0.3f + master.Personality.Talkative * 0.5f)
        {
            npc.ChangeRapport(_player, -30f);
            Sim.Notice($"{npc.DisplayName} has found out who carried their words to {master.DisplayName}.");
            return $"Later you hear that {master.DisplayName} let something slip, and {npc.DisplayName} knows exactly who told. They will not forget it.";
        }
        return $"You keep your face straight. {master.DisplayName} will hear it from you tonight; {npc.DisplayName} need never know.";
    }

    // The people this person would have an opinion of: their own house, and
    // anyone they feel strongly about.
    private List<Npc> Acquaintances(Npc npc)
    {
        var list = new List<Npc>();
        foreach (var (a, r) in npc.Rapport)
            if (a is Npc o && o != npc && !o.IsDead && Mathf.Abs(r) >= 10f) list.Add(o);
        foreach (var o in Sim.Npcs)
        {
            if (list.Count >= 10) break;
            if (o != npc && !o.IsDead && o.HomeStores == npc.HomeStores && npc.HomeStores != null && !list.Contains(o) && o.Tier != Tier.Background) list.Add(o);
        }
        list.Sort((a, b) => Mathf.Abs(npc.RapportWith(b)).CompareTo(Mathf.Abs(npc.RapportWith(a))));
        return list.Count > 8 ? list.GetRange(0, 8) : list;
    }

    private void ShowAskAbout(Npc npc)
    {
        _text = $"Whom do you want {npc.DisplayName}'s opinion of?";
        ClearOptions();
        foreach (var o in Acquaintances(npc)) { var oo = o; AddOption(oo.DisplayName, () => ShowAcquaint(npc, AskOpinion(npc, oo))); }
        AddOption("Never mind", () => ShowAcquaint(npc));
    }

    // What one person makes of another: their rapport, any blood between
    // them, kin, and what they saw the other do. A guarded man says less
    // the less he trusts you; nobody runs down their master to a stranger.
    private string AskOpinion(Npc npc, Npc other)
    {
        float trust = Trust(npc);
        float r = npc.RapportWith(other);
        bool master = npc.HomeStores?.Leader == other;
        if (master && trust < 30f) return r >= 0f ? $"\"{other.DisplayName} is the master. A fair one.\"" : $"\"{other.DisplayName} is the master.\" That is all you will get.";
        if (trust < 0f) return $"\"I keep my opinions to myself.\"";
        string kin = npc.Spouse == other ? "my wife" : other.Spouse == npc ? "my husband" : other.Father == npc ? "my child" : npc.Father == other ? "my father" : "";
        string s = kin != "" ? $"\"{other.DisplayName}? That's {kin}.\" " : "";
        s += r >= 45f ? $"\"There is nobody here I'd rather have at my back.\""
            : r >= 20f ? $"\"A good sort. We get on.\""
            : r > -10f && master ? "\"Hard, but fair, as masters go. You know where you are with him.\""
            : r > -10f ? $"\"{(other.IsFemale ? "She" : "He")} keeps to {(other.IsFemale ? "herself" : "himself")}, mostly. I've no complaint.\""
            : r > -40f ? $"\"I don't trust {(other.IsFemale ? "her" : "him")}, and I'll say so to {(other.IsFemale ? "her" : "his")} face.\""
            : $"\"If {(other.IsFemale ? "she" : "he")} went through the ice I would not reach for a pole.\"";
        if (Grudges.HasOpen(npc.DisplayName, other.DisplayName)) s += " There is blood unpaid between them.";
        foreach (var m in npc.Memories)
            if (m.Actor == other && m.Directness == 1 && m.Weight >= 0.5f) { s += $" \"I saw it myself: {m.Text}\""; break; }
        if (master && r < -10f) s += " They glance about before they say it.";
        npc.Warm(_player, trust > 40f ? 1.5f : 0.5f);
        return s;
    }

    // An honest read of you, so far as they will give it. Talkers say it
    // straight; a suspicious, close man gives you a word and a look.
    private string AskOfMe(Npc npc)
    {
        float r = npc.RapportWith(_player);
        var said = StoriesAboutMe(npc);
        string s = r >= 50f ? "\"You're one of us now. I'd stand by you.\""
            : r >= 20f ? "\"You'll do. You pull your weight.\""
            : r > -10f ? (npc.Personality.Suspicion > 0.6f ? "\"I don't know you yet.\" They hold your eye a moment too long." : "\"Too soon to say. Ask me in the spring.\"")
            : r > -40f ? "\"Since you ask: I don't care for you.\""
            : "\"You know what I think of you.\" They turn half away.";
        if (said.Count > 0 && npc.Personality.Talkative > 0.35f) s += $" \"And I have heard things. {said[0].Text}\"";
        if (Grudges.HasOpen(npc.DisplayName, _player.DisplayName)) s += " \"And you owe me blood.\"";
        return s;
    }

    // Tobacco is how talk begins in this country (design doc 3.6.1): shared,
    // it warms anyone, and a band person most of all. A pipe opens a man up -
    // he tells you one thing he has been keeping.
    private string SharePipe(Npc npc)
    {
        // Once a day: a second pipe is only smoke.
        string today = "pipe-day:" + (GameCalendar.Instance is { } cal ? cal.Year * 400 + (int)cal.DayOfYear : 0);
        if (Acquaintance.Has(_player, npc, today)) return $"\"We've had our pipe today.\" {npc.DisplayName} pats a full belly of smoke.";
        Acquaintance.ForgetPrefix(_player, npc, "pipe-day:");
        Acquaintance.Add(_player, npc, today);
        _player.Inventory.Remove(ItemId.Tobacco);
        bool band = npc.IsBandPerson;
        float gain = (band ? 6f : 3f) + npc.Personality.Talkative * 3f;
        npc.ChangeRapport(_player, gain);
        Acquaintance.Add(_player, npc, "pipe");
        string s = band
            ? $"You fill the pipe and offer it stem first. {npc.DisplayName} smokes, points it to the four directions, and hands it back. Something in the air between you eases."
            : $"You fill a pipe each from your twist. {npc.DisplayName} draws on it, lets the smoke go, and talks more easily.";
        // One thing they had kept back: the weightiest memory they have not yet raised with you.
        MemoryEntry best = null;
        foreach (var m in npc.Memories) if (m.Directness <= 2 && (best == null || m.Weight > best.Weight) && !Acquaintance.Has(_player, npc, "told:" + m.Key)) best = m;
        if (best != null && Trust(npc) >= 10f)
        {
            Acquaintance.Add(_player, npc, "told:" + best.Key);
            s += $" \"{(best.Directness == 1 ? "I'll tell you something I saw" : "I'll tell you what I heard")}: {best.Text}\"";
            _player.Journal.Add($"{GameCalendar.Instance?.DateString}: Over a pipe, {npc.DisplayName} told you: {best.Text}");
        }
        return s;
    }

    // You talk, they listen - and how they take it depends on who they are
    // and who you are: a man from the same road warms to you; a suspicious
    // one wonders why you're telling him.
    private string TellOfSelf(Npc npc)
    {
        bool again = Again(npc, "heard-me", out _);
        if (again) return $"{npc.DisplayName} has heard your story. \"You told me. {(npc.Personality.Talkative > 0.5f ? "Tell it again if you like - it was a good one." : "")}\"";
        bool sameRoad = npc.Faction == _player.Faction;
        string bg = _player.BackgroundTitle != "" ? _player.BackgroundTitle.ToLower() : "a man come out to the country";
        string s = $"You tell {npc.DisplayName} who you are: {bg}{(_player.TraitTitles.Count > 0 ? ", " + string.Join(" and ", _player.TraitTitles).ToLower() : "")}.";
        if (npc.Personality.Suspicion > 0.65f && !sameRoad) { s += " They listen without a word and you are not sure you have done yourself any good."; npc.Warm(_player, 0.5f); }
        else if (sameRoad) { s += " \"Then you know how it is.\" A nod, as between people who do."; npc.Warm(_player, 3f); }
        else { s += $" {npc.DisplayName} asks a question or two. It is something."; npc.Warm(_player, 1.5f); }
        return s;
    }

    private static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

    // A line for the greeting: someone you have really talked with knows you.
    private string AcquaintedLine(Npc npc)
    {
        var known = Acquaintance.Topics(_player, npc);
        if (known.Count == 0) return "";
        if (known.Contains("pipe") && npc.RapportWith(_player) > 20f) return " They make room for you, as for someone they have smoked with.";
        if (known.Contains("trouble") && npc.RapportWith(_player) > 15f) return " A look passes between you: you are someone they have told things to.";
        return known.Count >= 2 && npc.RapportWith(_player) > 10f ? " They know you by now." : "";
    }
}

// What each player has talked about with each person (design doc P.8): the
// raw stuff of being acquainted. Kept by the player's name and the
// person's, saved with the world.
public static class Acquaintance
{
    private static readonly Dictionary<string, HashSet<string>> _known = new();
    private static string Key(Agent p, Agent n) => p.DisplayName + "\u001f" + n.DisplayName;

    public static bool Has(Agent p, Agent n, string topic) => _known.TryGetValue(Key(p, n), out var s) && s.Contains(topic);
    public static void Add(Agent p, Agent n, string topic)
    {
        if (!_known.TryGetValue(Key(p, n), out var s)) _known[Key(p, n)] = s = new HashSet<string>();
        s.Add(topic);
    }
    public static void ForgetPrefix(Agent p, Agent n, string prefix)
    {
        if (_known.TryGetValue(Key(p, n), out var s)) s.RemoveWhere(t => t.StartsWith(prefix));
    }
    public static List<string> Topics(Agent p, Agent n) => _known.TryGetValue(Key(p, n), out var s) ? new List<string>(s) : new List<string>();

    public static void Clear() => _known.Clear();
    public static Godot.Collections.Dictionary Save()
    {
        var d = new Godot.Collections.Dictionary();
        foreach (var (k, s) in _known) d[k] = string.Join("|", s);
        return d;
    }
    public static void Restore(Godot.Collections.Dictionary d)
    {
        _known.Clear();
        foreach (var (k, v) in d) _known[k.AsString()] = new HashSet<string>(v.AsString().Split('|', System.StringSplitOptions.RemoveEmptyEntries));
    }
}
