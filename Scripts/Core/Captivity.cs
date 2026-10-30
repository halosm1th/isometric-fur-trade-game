using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.Player;
using IsometricFurTradeGame.Scripts.World;
using NetHub = IsometricFurTradeGame.Scripts.Net.Net;

namespace IsometricFurTradeGame.Scripts.Core;

// Taken by the other company (design doc 9.1): "close to a passing-time
// screen: a brief narrative beat, maybe a skill check, goods lost, dropped
// back into the world." The blow that would have killed you puts you on the
// floor of their nearest post with your pack emptied into their stores
// (PlayerController.Capture); then this page.
//
// Their master questions you first - tell him the truth, lie, or say
// nothing - and then there are four ways out: wait the days out, be bought
// back on your own post's account, give your parole, or go over the
// pickets after dark. Everything you do here is witnessed by their men and
// goes round like anything else, so your own Factor hears of it in time.
//
// Server only.
public static class Captivity
{
    private const int Ransom = 12;            // pieces of goods

    // The other company's post the man who took you belongs to, or the
    // nearest one of his company's to where you fell.
    public static FortLedger PostFor(Npc captor, Vector2 at)
    {
        if (captor.HomeStores is FortLedger own && own.Company == captor.Faction) return own;
        FortLedger best = null; float bestD = float.MaxValue;
        foreach (var post in AllPosts())
        {
            if (post.Company != captor.Faction) continue;
            float d = IsoGrid.ToWorld(post.AccessCell).DistanceTo(at);
            if (d < bestD) { bestD = d; best = post; }
        }
        return best;
    }

    private static IEnumerable<FortLedger> AllPosts()
    {
        foreach (var p in Sim.Posts()) yield return p;
        foreach (var t in World.Posts.Standing()) if (t.Ledger != null) yield return t.Ledger;
    }

    public static void Begin(PlayerController p, FortLedger post, Npc captor, int taken)
    {
        NetHub.I?.PushDialogue(p, p.Conversation.StartScene(Taken(p, post, captor, taken)));
    }

    // A post by its name on the map: "York Factory", not "the Company's stores".
    public static string Name(FortLedger post) =>
        post == Sim.Fort ? "York Factory" : post == Sim.Nwc ? "the Nor'Westers' house" : World.Posts.Of(post)?.Name ?? post.Title;

    private static Npc Master(FortLedger post, Npc captor) =>
        post.Leader != null && GodotObject.IsInstanceValid(post.Leader) && !post.Leader.IsDead ? post.Leader : captor;

    private static string Them(FortLedger post) => post.Company == Faction.NWC ? "the Nor'Westers" : post.Company == Faction.HBC ? "the Company" : "the Americans";

    private static ScenePage Page(string title, string text) => new() { Title = title, Text = text, CanLeave = false };
    private static void Note(PlayerController p, string line) => p.Journal.Add($"{GameCalendar.Instance?.DateString}: {line}");

    // ---- the pages -----------------------------------------------------------------------

    private static ScenePage Taken(PlayerController p, FortLedger post, Npc captor, int taken)
    {
        var master = Master(post, captor);
        string what = taken > 0 ? $"Your pack is gone into their stores - {taken} lots - and they have left you your coat and your knife." : "They have been through your things and found little worth the taking.";
        Note(p, $"Taken prisoner by {captor.DisplayName} and carried to {Name(post)}.");
        return Page("Taken", $"You come to on the floor of {Name(post)} with your head ringing. {captor.DisplayName} is standing over you. {what}\n\n{(master == captor ? "\"Up. You have some questions to answer.\"" : $"\"Up. {master.DisplayName} wants a word with you.\"")}")
            .Add("Get up and face him", () => Questioned(p, post, master));
    }

    private static ScenePage Questioned(PlayerController p, FortLedger post, Npc master)
    {
        var home = p.HomePost;
        string where = home != null ? Name(home) : "your people";
        return Page("Questions", $"{master.DisplayName} sits on a bale with a pipe going and looks you over for a long moment.\n\n\"How are the stores at {where}? How many men are wintering, and when do the boats go? Answer me straight and you'll find us reasonable.\"")
            .Add("Tell him the truth", () =>
            {
                master.ChangeRapport(p, 6f);
                p.CaptiveUntilDay -= 1;
                Chronicle.Event($"{p.DisplayName}, a prisoner, told {master.DisplayName} what {where} holds and when its boats go.", master.GlobalPosition, p, p.DisplayName, 1.6f);
                Note(p, $"Told {master.DisplayName} the truth about {where}. They will know it now, and so in time will my own people.");
                return Held(p, post, master, $"He nods slowly and writes it down. \"A man of sense. We'll not keep you longer than we must.\"");
            })
            .Add("Lie to him", () =>
            {
                float caught = Mathf.Clamp(0.2f + master.Personality.Suspicion * 0.5f - p.Skills.Trade * 0.4f, 0.05f, 0.85f);
                if (GD.Randf() < caught)
                {
                    Rough(p, 15f);
                    master.ChangeRapport(p, -6f);
                    p.CaptiveUntilDay += 1;
                    Note(p, $"Lied to {master.DisplayName} and was caught at it. They knocked me about for it.");
                    return Held(p, post, master, $"He hears you out, then nods to the man at the door. \"We had a man down from {where} a week since, and he tells it different.\" They take it out of you in the store-house, and you'll be held a day longer for the trouble.");
                }
                Note(p, $"Told {master.DisplayName} a false tale of {where}. He seemed to believe it.");
                return Held(p, post, master, "He writes it all down and seems satisfied. Whatever he does with it, it was not true.");
            })
            .Add("Say nothing", () =>
            {
                master.ChangeRapport(p, -3f);
                bool hard = master.Personality.Courage > 0.5f || master.Personality.Suspicion > 0.6f;
                if (hard) Rough(p, 10f);
                Chronicle.Event($"{p.DisplayName}, a prisoner, would tell {master.DisplayName} nothing of {where}.", master.GlobalPosition, p, p.DisplayName, 1.2f);
                Note(p, $"Told {master.DisplayName} nothing about {where}.");
                return Held(p, post, master, hard ? "He asks again, then has you taken out and knocked about until he is tired of it. You still say nothing." : "He shrugs. \"Suit yourself. You'll be the poorer for it.\"");
            });
    }

    private static ScenePage Held(PlayerController p, FortLedger post, Npc master, string said)
    {
        if (p.CaptiveOf == null) return null;
        int left = Mathf.Max(1, p.CaptiveUntilDay - (GameCalendar.Instance?.Day ?? 0));
        var home = p.HomePost;
        var page = Page("Held", $"{said}\n\nThey put you in the store-house with the door barred. {left} day{(left == 1 ? "" : "s")} of this, he says, and then they'll turn you loose.");
        page.Add("Wait it out", () => { p.WaitOutCaptivity(); return null; });
        if (home != null && home.Company != post.Company)
            page.Add($"Ask to be bought back on {Name(home)}'s account ({Ransom} pieces of goods)", () => Bought(p, post, master, home));
        page.Add("Give your parole: no arms against them till the ice goes out", () =>
        {
            string r = Parole(p, master);
            return p.CaptiveOf == null ? Page("Paroled", r + "\n\nThey open the gate for you.").Add("Go", () => null) : Held(p, post, master, r);
        });
        page.Add("Go over the pickets after dark", () => Escape(p, post, master));
        return page;
    }

    private static ScenePage Bought(PlayerController p, FortLedger post, Npc master, FortLedger home)
    {
        if (home.Stores.Count(ItemId.TradeGoods) < Ransom)
            return Held(p, post, master, $"\"{Name(home)} has not the goods to stand for you, by what I hear.\" He will not take a promise.");
        home.Stores.Remove(ItemId.TradeGoods, Ransom);
        post.Stores.Add(ItemId.TradeGoods, Ransom);
        Release(p);
        p.Standing -= 3f;
        master.ChangeRapport(p, 4f);
        Chronicle.Event($"{p.DisplayName} was bought back from {Them(post)} for {Ransom} pieces out of {Name(home)}'s stores.", master.GlobalPosition, p, p.DisplayName, 1.5f);
        Note(p, $"Bought back from {Them(post)} for {Ransom} pieces of {Name(home)}'s goods. The Factor will not thank me (standing -3).");
        return Page("Bought back", $"A note goes round by the next man going your way, and {master.DisplayName} takes your post's word for {Ransom} pieces. \"A man who is paid for is a man of sense.\" The gate is opened for you.\n\nThe goods come out of {Name(home)}'s stores, and your master will know whose fault that is. (standing -3)")
            .Add("Go", () => null);
    }

    private static ScenePage Escape(PlayerController p, FortLedger post, Npc master)
    {
        float chance = Mathf.Clamp(0.3f + p.Skills.Woodcraft * 0.5f - master.Personality.Suspicion * 0.2f, 0.1f, 0.85f);
        p.Skills.Learn(ref p.Skills.Woodcraft, 0.01f, "going over the pickets");
        if (GD.Randf() < chance)
        {
            var gate = IsoGrid.ToWorld(post.AccessCell);
            var towards = p.HomePost != null ? (IsoGrid.ToWorld(p.HomePost.AccessCell) - gate).Normalized() : Vector2.Right;
            if (towards == Vector2.Zero) towards = Vector2.Right;
            p.EscapeCaptivity(Convoys.LandNear(gate + towards * 900f));
            Note(p, $"Went over the pickets at {Name(post)} in the night and got clear.");
            return Page("Over the pickets", "When the post is asleep you work a board loose at the back of the store-house, wait for the man on watch to go round the far side, and go up and over the pickets. Nobody shouts. By first light you are well away.\n\nAn escaped prisoner: they will not forget it, and your own people will tell it.")
                .Add("Go", () => null);
        }
        Rough(p, 12f);
        p.CaptiveUntilDay += 2;
        master.ChangeRapport(p, -8f);
        Note(p, $"Tried to go over the pickets at {Name(post)} and was caught.");
        return Held(p, post, master, "You are half over the pickets when a dog starts up and a lantern comes round the corner. They drag you back by the legs and take it out of you, and you are to be held two days longer.");
    }

    // ---- the ways out, shared with the talk menu ---------------------------------------

    // Bought back with goods in hand, from the talk menu.
    public static string RansomFromPack(PlayerController p, Npc captor)
    {
        int have = p.Inventory.Count(ItemId.TradeGoods);
        if (have < Ransom) return $"\"Twelve pieces of goods and you may walk out. You have {have}.\"";
        p.Inventory.Remove(ItemId.TradeGoods, Ransom);
        captor.HomeStores?.Stores.Add(ItemId.TradeGoods, Ransom);
        var post = p.CaptiveOf;
        Release(p);
        captor.ChangeRapport(p, 4f);
        Chronicle.Event($"{p.DisplayName} bought his freedom from {captor.DisplayName} for {Ransom} pieces.", captor.GlobalPosition, p, weight: 1.2f);
        return $"\"A man who pays is a man of sense.\" {captor.DisplayName} counts the goods and waves you at the gate. ({(post != null ? Name(post) : "They")} keep your kit.)";
    }

    public static string Parole(PlayerController p, Npc captor)
    {
        float r = captor.RapportWith(p);
        if (captor.Personality.Suspicion > 0.65f && r < 10f) return "\"Your word? I have seen what your word is worth.\"";
        Release(p);
        p.ParoleUntilDay = GameCalendar.Instance.Day + 120;
        p.ParoleTo = captor.Faction;
        captor.ChangeRapport(p, 8f);
        Chronicle.Event($"{p.DisplayName} gave his parole to {captor.DisplayName}.", captor.GlobalPosition, p, weight: 1.5f);
        Note(p, $"Gave my parole to {captor.DisplayName}: no arms against them for four months.");
        return "\"Then go. No arms against us till the ice goes out - and I will hear of it if you break it.\" You are free, and bound by your word for four months.";
    }

    private static void Release(PlayerController p) { p.CaptiveOf = null; p.CaptiveUntilDay = -1; }

    // Knocked about: it hurts, and it does not kill a prisoner.
    private static void Rough(PlayerController p, float amount) => p.Needs.Health = Mathf.Max(5f, p.Needs.Health - amount);
}
