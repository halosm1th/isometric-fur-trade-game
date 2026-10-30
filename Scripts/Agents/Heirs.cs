using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.Items;

namespace IsometricFurTradeGame.Scripts.Agents;

// One man of the house who has gone before (design doc P.6): what he was
// called, what he got done, and how it ended. The journal keeps them.
public class Forebear
{
    public string Name = "";
    public int Born, Died;
    public string Rank = "labourer";
    public string End = "";
    public string Did = "";
    public string Line => $"{Name}, {Rank}, {Born}-{Died}. {End}{(Did == "" ? "" : " " + Did)}";
}

// Death is a turn of the story, not a reset (design doc P.6). Someone of
// your house takes your place: a son if you have one grown, a wife's
// brother if you married into the country, else a kinsman off the ship.
// He inherits what a man really inherits - the grudges held against your
// name, your debts, your wife's people, your half-finished ambitions, your
// book, and whatever the men did not buy at the sale of your effects.
public static class Heirs
{
    private static readonly string[] Given = { "Alexander", "Donald", "Hugh", "Andrew", "Colin", "Angus", "Murdo", "Finlay" };

    public class Inheritance
    {
        public string Name = "";
        public string Relation = "";
        public int Age = 20;
        public float Standing;
        public readonly List<string> Items = new();
        public readonly List<string> Burdens = new();
        public readonly List<string> Ties = new();
        public readonly List<Ambition> Unfinished = new();
        public Npc Body;
    }

    // Who takes his place. Nearest first: a grown son, a wife's brother, a
    // kinsman sent out by the family.
    public static Inheritance Prepare(Player.PlayerController dead)
    {
        var inh = new Inheritance();
        var cal = GameCalendar.Instance;
        string surname = Surname(dead.DisplayName);

        Npc son = null, kin = null;
        foreach (var n in Sim.Npcs)
        {
            if (n.IsDead) continue;
            if (n.FatherName == dead.DisplayName && !n.IsFemale && n.Age >= 15) { son = n; break; }
            if (kin == null && dead.Spouse is Npc w && n.HomeStores == w.HomeStores && n.Role == Role.Hunter && n != w) kin = n;
        }

        if (son != null)
        {
            inh.Name = son.DisplayName; inh.Relation = "your son"; inh.Age = son.Age; inh.Body = son;
            inh.Ties.Add($"{son.DisplayName} was raised at the post and knows the country a little");
        }
        else if (kin != null)
        {
            inh.Name = kin.DisplayName; inh.Relation = "your wife's brother"; inh.Age = kin.Age; inh.Body = kin;
            inh.Ties.Add($"{kin.DisplayName} takes your place because he is of the family you married into");
        }
        else
        {
            inh.Name = $"{Given[(int)(GD.Randi() % (uint)Given.Length)]} {surname}";
            inh.Relation = "your brother, come out on the ship";
            inh.Age = (int)GD.RandRange(18, 26);
            inh.Ties.Add($"{inh.Name} has come out from Orkney on the Company's ship to take his brother's place");
        }

        // A name is worth something, or costs something (design doc 10.5).
        float goodwill = 0f;
        foreach (var n in Sim.Npcs)
        {
            if (n.IsDead) continue;
            float r = n.RapportWith(dead);
            if (r > 25f) goodwill += 1f; else if (r < -25f) goodwill -= 1f;
        }
        inh.Standing = Mathf.Clamp(dead.Standing * 0.4f + goodwill * 2f, 0f, 45f);
        if (goodwill > 1f) inh.Ties.Add($"your name stood well with {goodwill:0} of the men; some of that comes to him");
        else if (goodwill < -1f) inh.Burdens.Add("your name was ill thought of here, and he will carry that");

        // Blood debts are a family matter, and so pass with the family.
        foreach (var g in Grudges.All)
        {
            if (g.Settled || g.Against != dead.DisplayName) continue;
            inh.Burdens.Add($"{g.Holder} still holds your house to account for {g.Victim} - {g.DebtMb * 0.6f:0} MB would cover it");
        }
        // What the Company thinks it is owed.
        if (dead.Task.Kind != Player.TaskKind.None && !dead.Task.IsComplete) inh.Burdens.Add($"the work you left undone: {dead.Task.Describe()}");
        if (dead.ParoleUntilDay > (cal?.Day ?? 0)) inh.Burdens.Add("the parole you gave the other company dies with you");

        // Family.
        if (dead.Spouse is Npc wife && !wife.IsDead)
            inh.Ties.Add(son != null ? $"{wife.DisplayName} is his mother and the band is his people" : $"{wife.DisplayName} is a widow of your house; her people will watch what he does");

        // The ambitions, as unfinished business.
        foreach (var a in dead.Ambitions)
        {
            if (a.Abandoned) continue;
            inh.Unfinished.Add(new Ambition { Kind = a.Kind, DeclaredDay = a.DeclaredDay, DeclaredYear = a.DeclaredYear, Best = a.Best });
        }

        // The kit. The men buy what they want of it; the rest comes to him.
        foreach (var (id, n) in dead.Inventory.All())
        {
            var k = Items.Items.Of(id).Kind;
            if (k == ItemKind.Weapon || k == ItemKind.Clothing || k == ItemKind.Tool) inh.Items.Add($"{Items.Items.Of(id).Name}{(n > 1 ? $" x{n}" : "")}");
        }
        return inh;
    }

    // Hand the world over to him.
    public static void Succeed(Player.PlayerController p, Inheritance inh)
    {
        var cal = GameCalendar.Instance;
        var forebear = new Forebear
        {
            Name = p.DisplayName, Born = (cal?.Year ?? 1811) - p.Age, Died = cal?.Year ?? 1811, Rank = p.Rank,
            End = p.Killer != null ? $"Killed by {p.Killer.Describe()}." : "Died in the country.",
            Did = Done(p),
        };
        p.House.Add(forebear);

        // The dead man's name passes out of use; grudges against him follow
        // the family, at a debt a generation can actually settle.
        foreach (var g in new List<Grudge>(Grudges.All))
        {
            if (g.Settled || g.Against != p.DisplayName) continue;
            g.Against = inh.Name;
            g.DebtMb *= 0.6f;
        }
        // Everyone's regard resets to what they thought of the house, not the man.
        foreach (var n in Sim.Npcs)
        {
            if (!n.Rapport.ContainsKey(p)) continue;
            n.Rapport[p] = Mathf.Clamp(n.Rapport[p] * 0.4f, -40f, 40f);
        }

        string old = p.DisplayName;
        p.DisplayName = inh.Name;
        p.Age = inh.Age;
        p.Standing = inh.Standing;
        p.Rank = "labourer";
        p.TasksDone = 0; p.TasksRefused = 0;
        p.ParoleUntilDay = -1; p.ParoleConfessed = false;
        p.CaptiveOf = null;
        p.Spouse = null;
        p.OtherWives.Clear();
        p.PrivateWorthMb *= 0.25f;
        p.Ambitions.Clear();
        foreach (var a in inh.Unfinished) p.Ambitions.Add(a);
        p.AmbitionsDeclared = p.Ambitions.Count > 0;
        p.Task.Kind = Player.TaskKind.None;

        // The heir who was already a person in the world steps out of it to
        // become the player; a brother off the ship simply arrives.
        if (inh.Body != null && GodotObject.IsInstanceValid(inh.Body))
        {
            p.CreeFluency = Mathf.Max(p.CreeFluency, inh.Body.Faction == Faction.Cree ? 0.8f : p.CreeFluency);
            p.Skills.Hunting = Mathf.Max(p.Skills.Hunting, inh.Body.Skills.Hunting);
            p.Skills.Trapping = Mathf.Max(p.Skills.Trapping, inh.Body.Skills.Trapping);
            p.GlobalPosition = inh.Body.GlobalPosition;
            inh.Body.QueueFree();
        }

        p.Journal.Add($"{cal?.DateString}: {old} is dead. You are {inh.Name}, {inh.Relation}.");
        foreach (var b in inh.Burdens) p.Journal.Add($"{cal?.DateString}: Inherited - {b}");
        Sim.Headline($"{old} is dead. {inh.Name} takes his place: {inh.Relation}.");
        Chronicle.Event($"{old} is dead; {inh.Name} has come to take his place.", p.GlobalPosition, p, inh.Name, 2f);
    }

    private static string Done(Player.PlayerController p)
    {
        var bits = new List<string>();
        if (p.Rank == "postmaster") bits.Add("He had a post of his own.");
        else if (p.Rank == "clerk") bits.Add("He rose to clerk.");
        if (p.Spouse != null) bits.Add($"He married {p.Spouse.Describe()}.");
        if (p.PrivateWorthMb > 40f) bits.Add($"He put by {p.PrivateWorthMb:0} MB of his own.");
        foreach (var a in p.Ambitions) if (a.Best >= 1f) bits.Add($"He did what he set out to do: {a.Title.ToLower()}.");
        return string.Join(" ", bits);
    }

    public static string Surname(string full)
    {
        int i = full.LastIndexOf(' ');
        string s = i > 0 ? full.Substring(i + 1) : full;
        return s.Replace("(clerk)", "").Replace("(postmaster)", "").Trim();
    }
}
