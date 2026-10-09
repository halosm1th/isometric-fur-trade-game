using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;

namespace IsometricFurTradeGame.Scripts.Agents;

// Asking a leader for something: a different task, more time, a man, an
// advance, or leave to do it your own way (design doc 3.9 step 5 - the
// subordinate decides - and P.2's collisions). Whether he wears it comes
// out of what the world actually holds: your rank, how he regards you,
// what you have done for him before, who speaks for you, and what he has
// heard. Every factor is named so the answer can say why.
public static class Negotiation
{
    public readonly struct Factor
    {
        public readonly string Why; public readonly float Weight;
        public Factor(string why, float weight) { Why = why; Weight = weight; }
    }

    public class Verdict
    {
        public bool Granted;
        public float Score;
        public readonly List<Factor> For = new();
        public readonly List<Factor> Against = new();
        public string Because(int n = 2)
        {
            var parts = new List<string>();
            For.Sort((a, b) => b.Weight.CompareTo(a.Weight));
            Against.Sort((a, b) => b.Weight.CompareTo(a.Weight));
            for (int i = 0; i < n && i < For.Count; i++) parts.Add(For[i].Why);
            string f = parts.Count > 0 ? string.Join("; ", parts) : "";
            parts.Clear();
            for (int i = 0; i < n && i < Against.Count; i++) parts.Add(Against[i].Why);
            string a = parts.Count > 0 ? string.Join("; ", parts) : "";
            if (f == "" && a == "") return "";
            if (a == "") return $"({f})";
            if (f == "") return $"({a})";
            return $"({f} - but {a})";
        }
    }

    // `difficulty` is how much of an imposition the ask is: 0.3 a small
    // favour, 1.0 a real one, 1.6 something he'd have to answer for.
    public static Verdict Weigh(Player.PlayerController p, Npc leader, float difficulty, string subject = "")
    {
        var v = new Verdict();
        void Plus(string why, float w) { if (w > 0.01f) { v.For.Add(new Factor(why, w)); v.Score += w; } }
        void Minus(string why, float w) { if (w > 0.01f) { v.Against.Add(new Factor(why, w)); v.Score -= w; } }

        // Rank: what the Company says you are.
        switch (p.Rank)
        {
            case "postmaster": Plus("you have held a post", 0.55f); break;
            case "clerk": Plus("you are a clerk, not a labouring hand", 0.35f); break;
            case "on notice": Minus("you are on notice", 0.3f); break;
        }
        // Standing with the house, and his own regard for you.
        float standing = Mathf.Clamp(p.Standing / 60f, -0.4f, 0.6f);
        if (standing > 0.05f) Plus($"your standing is {p.Standing:0}", standing);
        else if (standing < -0.05f) Minus($"your standing is {p.Standing:0}", -standing);
        float rapport = leader.RapportWith(p) / 100f;
        if (rapport > 0.05f) Plus("he thinks well of you", Mathf.Min(0.5f, rapport));
        else if (rapport < -0.05f) Minus("he does not care for you", Mathf.Min(0.5f, -rapport));

        // History: what you have actually done for him, and against him.
        if (p.TasksDone > 0) Plus($"you have done {p.TasksDone} thing{(p.TasksDone == 1 ? "" : "s")} he set you", Mathf.Min(0.45f, p.TasksDone * 0.08f));
        if (p.TasksRefused > 0) Minus($"you refused him {p.TasksRefused} time{(p.TasksRefused == 1 ? "" : "s")}", Mathf.Min(0.6f, p.TasksRefused * 0.22f));
        if (p.ParoleUntilDay > (GameCalendar.Instance?.Day ?? 0) && p.ParoleConfessed) Minus("you are on parole to the other side", 0.3f);

        // What he has heard: the memories he holds about you (design doc 10.2).
        int bad = 0, good = 0;
        foreach (var m in leader.Memories)
        {
            if (m.Subject != p.DisplayName) continue;
            if (m.Text.Contains("struck") || m.Text.Contains("robbed") || m.Text.Contains("broke") || m.Text.Contains("sold") || m.Text.Contains("refused") || m.Text.Contains("was seen")) bad++;
            else good++;
        }
        if (bad > 0) Minus($"he has heard {bad} ill thing{(bad == 1 ? "" : "s")} of you", Mathf.Min(0.5f, bad * 0.15f));
        if (good > 0) Plus("your name has come up well", Mathf.Min(0.3f, good * 0.08f));

        // Connections: who else in this house speaks for you, and who against.
        float friends = 0f, enemies = 0f;
        foreach (var n in Sim.Npcs)
        {
            if (n == leader || n.IsDead || n.HomeStores != leader.HomeStores) continue;
            float r = n.RapportWith(p);
            if (r > 35f) friends += n.Role == Role.Clerk ? 0.14f : 0.07f;
            else if (r < -25f) enemies += n.Role == Role.Clerk ? 0.14f : 0.07f;
        }
        if (friends > 0.01f) Plus("men of the house speak for you", Mathf.Min(0.4f, friends));
        if (enemies > 0.01f) Minus("men of the house speak against you", Mathf.Min(0.4f, enemies));

        // Kin: married into the country, on a matter touching the band.
        if (p.Spouse is Npc w && !w.IsDead && (subject == "band" || subject == "country"))
            Plus($"you are kin to {w.DisplayName}'s people", 0.3f);

        // The man himself.
        if (leader.Personality.Suspicion > 0.6f) Minus("he is a suspicious man", leader.Personality.Suspicion * 0.25f);
        if (leader.Personality.Loyalty > 0.6f && difficulty > 1f) Minus("he keeps to his orders", leader.Personality.Loyalty * 0.2f);
        if (leader.Personality.Ambition > 0.6f && subject == "returns") Plus("he wants the returns as much as you do", 0.2f);
        if (leader.Needs.Hunger < 40f || leader.IsSick) Minus("he is in no humour for it", 0.2f);

        // A little chance, so the same ask is not always the same answer.
        float roll = (float)GD.RandRange(-0.12, 0.12);
        v.Score += roll;
        v.Granted = v.Score >= difficulty - 0.5f;
        return v;
    }
}
