using System;
using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;

namespace IsometricFurTradeGame.Scripts.Core;

// The shared message object (design doc 10.1.1): something written down,
// carried by somebody or something, and inert until it is read. London's
// directive, the Factor's letter to the chief, the year's accounts going
// home, a rumour that finally reaches the right ear - all one class. What
// the message does happens in OnRead, at the read event, not before.
public class Message
{
    public string Kind = "letter";        // directive, accounts, letter, mandate, rumour
    public string From = "", To = "";     // people, or places like "London"
    public string Subject = "";
    public string Text = "";
    public Faction Company = Faction.HBC; // whose paper it is (matters to whoever else reads it)
    public int DayWritten;
    public bool Read;
    public float TransitHoursLeft;        // for the abstract legs (a ship at sea, a runner off-map)
    public Godot.Collections.Dictionary Payload = new();

    public Action<Agent> OnRead;          // the intended effect, when the right person opens it
    public Action<Agent> OnIntercepted;   // what a rival learns from it instead
    public Func<string> Describe;         // for the pack tab

    public string Summary => $"{Kind}: {From} to {To} - {Subject}{(Read ? " (opened)" : " (sealed)")}";
}

// The post: what's in transit off the map, and the read event itself.
public static class Mail
{
    public static readonly List<Message> Abroad = new();
    public static readonly List<string> Log = new();

    // Send something on the abstract legs: a ship's passage to London, the
    // brigade's to Fort William. Delivered (and read there) when the hours run out.
    public static void Send(Message m, float hours)
    {
        m.TransitHoursLeft = hours;
        Abroad.Add(m);
        Log.Add($"{GameCalendar.Instance?.DateString}: {m.Summary} sent, {hours / 24f:0} days out.");
    }

    public static void Tick(float hours)
    {
        if (!Net.Net.IsServer || hours <= 0f) return;
        for (int i = Abroad.Count - 1; i >= 0; i--)
        {
            var m = Abroad[i];
            m.TransitHoursLeft -= hours;
            if (m.TransitHoursLeft > 0f) continue;
            Abroad.RemoveAt(i);
            Open(m, null);   // the far end reads it the moment it lands
        }
    }

    // The read event. Nothing a message carries happens until here.
    public static void Open(Message m, Agent reader)
    {
        if (m == null || m.Read) return;
        m.Read = true;
        bool intended = reader == null || reader.Faction == m.Company || reader.DisplayName == m.To;
        Log.Add($"{GameCalendar.Instance?.DateString}: {reader?.Describe() ?? m.To} opened {m.Kind} from {m.From}: {m.Subject}");
        if (intended) m.OnRead?.Invoke(reader);
        else m.OnIntercepted?.Invoke(reader);
        if (reader != null) Chronicle.Event($"{reader.Describe()} read a {m.Kind} from {m.From}.", reader.GlobalPosition, reader, weight: intended ? 0.5f : 1.5f);
    }

    // Hand a message to someone. If they're the addressee they'll read it
    // when they sit down; anyone else just carries it.
    public static void Give(Message m, Agent to)
    {
        if (to == null) return;
        to.Mail.Add(m);
        if (to is Npc n && !n.IsDead && (n.DisplayName == m.To || m.To == RoleName(n))) n.Brain.Push(n, new Agents.Goals.ReadMailGoal());
    }

    // "the Factor" / "the partner" as addressees, so a successor still opens the packet.
    public static string RoleName(Npc n) => n.Role switch
    {
        Role.Factor => "the Factor at York Factory",
        Role.Postmaster => "the postmaster at Oxford House",
        Role.NwcPartner => "the wintering partner",
        Role.CreeChief => "the chief of the band",
        _ => n.DisplayName,
    };

    public static Npc Addressee(Message m)
    {
        foreach (var n in Sim.Npcs) if (!n.IsDead && (n.DisplayName == m.To || RoleName(n) == m.To)) return n;
        return null;
    }
}

// The letters this world actually writes, so a save can put them back
// together (the effects are code, the payload is data).
public static class Letters
{
    public static Message Build(string kind, Godot.Collections.Dictionary payload)
    {
        return kind switch
        {
            "directive" => Directive((float)payload["price"], (float)payload["quota"], (int)payload["year"]),
            "accounts" => Accounts((float)payload["shipped"], (float)payload["quota"]),
            "mandate" => Mandate((float)payload["shipped"], (float)payload["quota"]),
            "letter" => LetterToChief((string)payload["from"]),
            "rumour" => Rumour((string)payload["about"], (string)payload["text"], (float)payload["standing"]),
            "reply" => Reply((string)payload["from"], (string)payload["answer"]),
            "report" => OutpostReport((string)payload["from"], (float)payload["furs"], (float)payload["food"], (int)payload["men"], (string)payload["news"]),
            _ => new Message { Kind = kind },
        };
    }

    // London's once-a-year instruction set (design doc 10.1.2). Inert till the Factor opens it.
    public static Message Directive(float price, float quota, int year)
    {
        var m = new Message
        {
            Kind = "directive", From = "the Governor and Committee, London", To = "the Factor at York Factory", Company = Faction.HBC,
            Subject = $"the outfit of {year}", DayWritten = 1,
            Text = $"Pay {price:0.00} MB in goods per MB of fur. The Committee expects {quota:0} MB of returns by the ship. Keep the Indians in your interest and watch the Canadians.",
        };
        m.Payload["price"] = price; m.Payload["quota"] = quota; m.Payload["year"] = year;
        m.OnRead = reader =>
        {
            var fort = Sim.Fort; if (fort == null) return;
            fort.PeltPrice = price; fort.QuotaMb = quota;
            fort.DirectiveText = m.Text; fort.DirectiveReadDay = GameCalendar.Instance?.Day ?? 0; fort.DirectiveYear = year;
            Sim.Notice($"{reader?.Describe() ?? "The Factor"} reads the Committee's orders: pay {price:0.00} MB in goods per MB of fur; London expects {quota:0} MB of returns.");
        };
        m.OnIntercepted = reader =>
        {
            // The Nor'Westers learn the Company's standard and undercut it.
            var nwc = Sim.Nwc; if (nwc == null || reader == null || reader.Faction != Faction.NWC) return;
            nwc.PeltPrice = Mathf.Clamp(Mathf.Max(nwc.PeltPrice, price * 1.15f), 0.5f, 3f);
            Economy.Rivalry.Raise(6f, "the Company's packet read across the river");
            Sim.Notice($"{reader.Describe()} reads London's orders to the Company and sets the Nor'Westers' price at {nwc.PeltPrice:0.00} to beat them.");
        };
        return m;
    }

    // The year's books going home on the ship; London reads them months later (design doc 3.9 step 8).
    public static Message Accounts(float shipped, float quota)
    {
        var m = new Message { Kind = "accounts", From = "the Factor at York Factory", To = "London", Company = Faction.HBC, Subject = $"returns of {shipped:0} MB against {quota:0}", Text = $"Returns {shipped:0} MB; quota {quota:0}." };
        m.Payload["shipped"] = shipped; m.Payload["quota"] = quota;
        m.OnRead = _ => { if (Sim.Fort != null) Economy.LondonCommittee.ReviewReturns(Sim.Fort, shipped); };
        return m;
    }

    // The July council's word to a wintering partner (design doc 10.1.3), carried by the brigade.
    public static Message Mandate(float shipped, float quota)
    {
        var m = new Message { Kind = "mandate", From = "the partners at Fort William", To = "the wintering partner", Company = Faction.NWC, Subject = "the winter's plan", Text = "" };
        m.Payload["shipped"] = shipped; m.Payload["quota"] = quota;
        m.OnRead = reader =>
        {
            var nwc = Sim.Nwc; if (nwc == null) return;
            nwc.PeltPrice = Mathf.Clamp(shipped < quota * 0.7f ? nwc.PeltPrice * 1.2f : nwc.PeltPrice * 0.95f, 0.5f, 3f);
            nwc.DirectiveText = $"The partners {(shipped < quota * 0.7f ? "are displeased with the returns and want the Bay post undercut" : "are content and want the price held")}. Pay {nwc.PeltPrice:0.00}; bring {nwc.QuotaMb:0} MB to the rendezvous.";
            nwc.DirectiveReadDay = GameCalendar.Instance?.Day ?? 0;
            Sim.Notice($"{reader?.Describe() ?? "The partner"} reads the council's mandate: the Nor'Westers will pay {nwc.PeltPrice:0.00} for beaver.");
        };
        m.OnIntercepted = reader =>
        {
            var fort = Sim.Fort; if (fort == null || reader == null || reader.Faction != Faction.HBC) return;
            Sim.Notice($"{reader.Describe()} reads the Nor'Westers' mandate: they mean to pay {Sim.Nwc?.PeltPrice:0.00}. The Factor can answer it.");
            Economy.Rivalry.Raise(6f, "the Nor'Westers' packet read at the Bay");
        };
        return m;
    }

    public static Message LetterToChief(string from)
    {
        var m = new Message { Kind = "letter", From = from, To = "the chief of the band", Company = Faction.HBC, Subject = "an invitation to trade", Text = $"{from} invites the band to bring their furs to the post, promising a fair standard and a dram for the men." };
        m.Payload["from"] = from;
        m.OnRead = reader =>
        {
            var band = Sim.Band; if (band == null) return;
            band.InvitedToTrade = true;
            band.StandingWithCompany = Mathf.Min(100f, band.StandingWithCompany + 6f);
        };
        m.OnIntercepted = reader =>
        {
            if (Sim.Nwc != null && reader != null && reader.Faction == Faction.NWC) { Sim.Nwc.PeltPrice = Mathf.Max(Sim.Nwc.PeltPrice, (Sim.Fort?.PeltPrice ?? 1f) * 1.1f); Sim.Notice($"The Nor'Westers, having read the Factor's letter, raise their price to {Sim.Nwc.PeltPrice:0.00}."); }
        };
        return m;
    }

    // The inland post's account of itself, carried down the river road
    // (design doc 3.9 step 6): the Factor's picture of Oxford House is the
    // last one of these plus the days since.
    public static Message OutpostReport(string from, float furs, float food, int men, string news)
    {
        var m = new Message { Kind = "report", From = from, To = "the Factor at York Factory", Company = Faction.HBC, Subject = "the state of Oxford House", Text = $"Oxford House holds {furs:0} MB in furs and {food:0} days of provisions; {men} men fit. {news}" };
        m.Payload["from"] = from; m.Payload["furs"] = furs; m.Payload["food"] = food; m.Payload["men"] = men; m.Payload["news"] = news;
        m.OnRead = reader =>
        {
            var fort = Sim.Fort; if (fort == null) return;
            fort.OutpostReportText = m.Text; fort.LastOutpostReportDay = GameCalendar.Instance?.Day ?? 0;
            fort.OutpostBelievedFursMb = furs; fort.OutpostBelievedFoodDays = food;
            if (news.Contains("no master")) fort.KnowsOutpostMasterless = true;
            Sim.Notice($"{reader?.Describe() ?? "The Factor"} reads the report from Oxford House: {m.Text}");
        };
        m.OnIntercepted = reader =>
        {
            if (reader != null && reader.Faction == Faction.NWC) { Sim.Notice($"{reader.Describe()} reads the Company's inland report: {furs:0} MB sitting at Oxford House, {men} men to guard it."); Economy.Rivalry.Raise(4f, "the inland report read across the river"); }
        };
        return m;
    }

    // The chief's answer to the Factor, carried back by whoever brought the letter.
    public static Message Reply(string from, string answer)
    {
        var m = new Message { Kind = "reply", From = from, To = "the Factor at York Factory", Company = Faction.HBC, Subject = "the band's answer, with a string of beads", Text = $"{from} sends a string of beads and says {answer}." };
        m.Payload["from"] = from; m.Payload["answer"] = answer;
        m.OnRead = reader =>
        {
            if (Sim.Band != null) Sim.Band.StandingWithCompany = Mathf.Min(100f, Sim.Band.StandingWithCompany + 2f);
            foreach (var pc in Sim.Players()) if (pc.Task.Kind == Player.TaskKind.DeliverLetter && !pc.Task.IsComplete) { pc.Task.Progress(Player.TaskKind.DeliverLetter, 1); pc.Standing += 3f; }
            Sim.Notice($"The Factor reads the chief's answer: {answer}.");
        };
        return m;
    }

    // Word that takes days to reach the right ear: the blowback of a sold packet (design doc 10.3).
    public static Message Rumour(string about, string text, float standing)
    {
        var m = new Message { Kind = "rumour", From = "the country", To = "the Factor at York Factory", Company = Faction.HBC, Subject = $"word about {about}", Text = text };
        m.Payload["about"] = about; m.Payload["text"] = text; m.Payload["standing"] = standing;
        m.OnRead = _ =>
        {
            foreach (var pc in Sim.Players()) if (pc.DisplayName == about) { pc.Standing -= standing; Core.Reveal.Lands(pc, $"Word has reached the Factor and your standing falls by {standing:0}.", "sold", 1.5f); }
            Sim.Notice($"The Factor has heard: {text}");
        };
        return m;
    }
}
