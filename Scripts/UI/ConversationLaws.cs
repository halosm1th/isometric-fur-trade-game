using System;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.UI;

// The laws of the place, talked over (Core/Laws): what they are, what is
// before the meeting and how this man means to vote, a word in his ear for
// it or against it, and a motion of your own - put round the houses of a
// town or a camp, or written to London through the Factor.
public partial class Conversation
{
    private static Body LawsBody(Npc npc)
    {
        if (npc.HomeStores is TownHousehold th && th.Town != null && th.Town.Standing) return Laws.Of(th.Town);
        if (npc.HomeStores is BandLedger bl && npc.Role is Role.Chief or Role.Hunter) return Laws.Of(bl);
        if (npc.Role == Role.Factor && npc.Faction == Faction.HBC) return Laws.Company;
        return null;
    }

    private static string LawsLabel(Npc npc) => LawsBody(npc)?.Kind switch
    {
        BodyKind.Band => "The council's rules...",
        BodyKind.Company => "The Committee's orders...",
        _ => "The town's laws...",
    };

    private void ShowLaws(Npc npc, string said = null)
    {
        var b = LawsBody(npc);
        if (b == null) { ShowTopics(Greeting(npc)); return; }
        var lines = new System.Text.StringBuilder();
        if (said != null) lines.Append(said).Append("\n\n");
        lines.Append(b.Laws.Count == 0 ? $"{char.ToUpper(b.Name[0])}{b.Name[1..]} has made no rule on these things." : $"What {b.Name} has laid down:");
        foreach (var l in b.Laws) lines.Append("\n  - ").Append(Laws.Text(l, b));
        if (b.Kind == BodyKind.Town && b.Chest > 0) lines.Append($"\nIn the poor chest from the duties: {Money.Format(b.Chest, b.Town)}.");
        var p = Laws.OpenFor(b);
        Voter me = p == null ? null : b.Voters().Find(v => v.Npc == npc);
        if (p != null)
        {
            lines.Append($"\n\nBefore {b.Name}: a motion {p.Title}, moved by {p.Sponsor}{(p.Opponent != "" ? $" and opposed by {p.Opponent}" : "")}. ");
            lines.Append(p.Stage == MotionStage.Canvassing ? "It is being carried round; no meeting is called yet." : b.Sits ? $"The {b.Meeting} sits {Laws.When(p)}." : $"London will read it with the returns, {Laws.When(p).Replace(", at two", "")}.");
            if (me != null) lines.Append("\n").Append(HowHeVotes(npc, p, me));
        }
        foreach (var m in Laws.MinutesOf(b, 3)) lines.Append("\n  ").Append(m);
        _text = lines.ToString();
        ClearOptions();
        if (p != null && me != null && p.Stage is MotionStage.Canvassing or MotionStage.Called)
        {
            AddOption("Urge him to vote for it", () => Urge(npc, p, me, 1));
            AddOption("Urge him to vote against it", () => Urge(npc, p, me, -1));
        }
        if (p != null && p.Sponsor == _player.DisplayName && p.Stage == MotionStage.Canvassing && b.Sits)
            AddOption($"Call the {b.Meeting} for three days hence", () => { Laws.Call(p, 3); ShowLaws(npc, $"\"Three days, then. I'll be there.\""); });
        if (p == null) AddOption(b.Kind == BodyKind.Company ? "Write to the Committee proposing..." : "Put a motion...", () => ShowMotions(npc, b));
        AddOption("Back", () => ShowTopics(Greeting(npc)));
    }


    // What he will say of his vote: a close man keeps it to himself unless he likes you.
    private string HowHeVotes(Npc npc, Proposal p, Voter me)
    {
        if (npc.Personality.Suspicion > 0.7f && npc.RapportWith(_player) < 15f) return "\"How I vote is my own affair.\"";
        float l = Laws.Lean(p, me);
        return l > 0.35f ? "\"I'm for it, and I'll say so.\"" : l > 0.05f ? "\"I lean to it, I suppose.\"" : l < -0.35f ? "\"Over my dead body.\"" : l < -0.05f ? "\"I don't much like it.\"" : "\"I haven't made up my mind.\"";
    }

    private void Urge(Npc npc, Proposal p, Voter me, int side)
    {
        float before = Laws.Lean(p, me);
        float gave = Laws.Persuade(p, _player, me, side);
        p.PlayerSign = side;
        float after = Laws.Lean(p, me);
        npc.ChangeRapport(_player, (after - before) * side > 0.05f ? 1f : -1f);
        string said = gave < 0.03f ? "\"You've said all that before.\"" : (after - before) * side > 0.08f ? "\"There's something in that. I'll think on it.\"" : "He hears you out, and says nothing to it.";
        ShowLaws(npc, said);
    }

    private void ShowMotions(Npc npc, Body b)
    {
        _text = b.Kind == BodyKind.Company ? "What will you put to London?" : "What will you move?";
        ClearOptions();
        foreach (var k in Laws.Competent(b))
        {
            var kind = k;
            var cur = b.Get(k);
            if (cur != null)
            {
                AddOption($"To repeal {Laws.Short(k)}", () => Move(npc, b, kind, Motion.Repeal, 0f));
                if (k != LawKind.NoSpirits)
                {
                    float lo = cur.Value * 0.5f, hi = Mathf.Min(1f, cur.Value * 1.5f);
                    AddOption($"{Up(Laws.Describe(k, lo, b))}", () => Move(npc, b, kind, Motion.Enact, lo));
                    AddOption($"{Up(Laws.Describe(k, hi, b))}", () => Move(npc, b, kind, Motion.Enact, hi));
                }
            }
            else AddOption($"{Up(Laws.Describe(k, Laws.DefaultValue(k), b))}", () => Move(npc, b, kind, Motion.Enact, Laws.DefaultValue(kind)));
        }
        AddOption("Back", () => ShowLaws(npc));
    }

    private static string Up(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

    private void Move(Npc npc, Body b, LawKind k, Motion m, float v)
    {
        var p = Laws.Propose(b, k, m, v, _player);
        if (p == null) { ShowLaws(npc, "\"There's a motion before them already.\""); return; }
        p.PlayerSign = 1;
        if (b.Kind == BodyKind.Company)
        {
            // A letter home with the ship: read at the November committee with the returns.
            foreach (var cv in b.Voters()) Laws.Persuade(p, _player, cv, 1, 0.3f);
            var cal = GameCalendar.Instance;
            int y = cal?.Year ?? 1811;
            int nov = y * 366 + new DateTime(y, 11, 1).DayOfYear;
            if (nov <= Laws.Today) nov = (y + 1) * 366 + new DateTime(y + 1, 11, 1).DayOfYear;
            p.Stage = MotionStage.Called; p.MeetingDay = nov;
            ShowLaws(npc, "\"I'll send it home with the packet. Don't expect them to thank you for it.\"");
            return;
        }
        if (npc.RapportWith(_player) > 0f) Laws.Persuade(p, _player, b.Voters().Find(x => x.Npc == npc) ?? new Voter { Name = npc.DisplayName, Npc = npc }, 1);
        ShowLaws(npc, $"\"Then you'd best go round the houses. You'll want a meeting called once you have the votes.\"");
    }
}
