using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Ecology;

public enum RunPhase { Muster, Running, Home, Over }

// The communal hunt (design doc 16.1): a camp's riders under a captain, the
// soldiers keeping order, a muster out of sight and downwind of the herd, and
// then one run on the captain's word in which every man kills what his horse
// and his arm are good for - many animals, not one. A man who goes at the
// herd before the word spooks it for everybody, and the soldiers deal with
// him: individual gain against the camp's winter.
public class BuffaloRun
{
    public static readonly List<BuffaloRun> Active = new();
    // The last day each camp ran the buffalo, by its title.
    public static readonly Dictionary<string, int> LastRun = new();

    public BandLedger Band;
    public SpawnSite Site;
    public Npc Captain;
    public readonly List<Npc> Riders = new();
    public readonly HashSet<Npc> AtMuster = new();
    public readonly HashSet<Player.PlayerController> Joined = new();
    public Vector2I Muster;
    public RunPhase Phase = RunPhase.Muster;
    public float Hours, RunHours;
    public int Kills;
    public bool Broken;
    public string BrokenBy = "";
    private int _liveAtStart;

    public string Where => Site.Name;

    // Out of sight of the herd and on the camp's side of it: two herd-widths off.
    public static Vector2I MusterFor(SpawnSite site, Vector2I camp)
    {
        var from = site.WorldPos; var to = IsoGrid.ToWorld(camp);
        var dir = (to - from).LengthSquared() > 1f ? (to - from).Normalized() : new Vector2(-1, 0);
        var spot = IsoGrid.FromWorld(from + dir * site.RadiusPx * 2.3f);
        var paths = Sim.Paths;
        if (paths == null || paths.IsWalkable(spot)) return spot;
        for (int r = 1; r < 8; r++)
            foreach (var c in IsoGrid.EdgeNeighbours(spot + new Vector2I(0, r)))
                if (paths.IsWalkable(c)) return c;
        return spot;
    }

    public static BuffaloRun Call(BandLedger band, SpawnSite site, List<Npc> riders, Npc captain)
    {
        var run = new BuffaloRun { Band = band, Site = site, Captain = captain, Muster = MusterFor(site, band.Camp) };
        run.Riders.AddRange(riders);
        run._liveAtStart = site.LiveCount;
        Active.Add(run);
        LastRun[band.Title] = Plains.Today;
        foreach (var r in riders)
        {
            r.Brain.Clear(r);
            r.Brain.Push(r, new Agents.Goals.BuffaloRunGoal(run));
        }
        captain?.Bark($"The buffalo are at {site.Name}. We ride - and nobody goes before the word.");
        foreach (var pc in Sim.Players())
        {
            if (pc.GlobalPosition.DistanceTo(IsoGrid.ToWorld(band.Camp)) > Tiers.BackgroundRadius && pc.GlobalPosition.DistanceTo(site.WorldPos) > Tiers.BackgroundRadius) continue;
            pc.Say($"{band.Name} are going out after the buffalo at {site.Name}. The riders muster {Plains.BearingWord(pc.GlobalPosition, IsoGrid.ToWorld(run.Muster))} of you. Ride with them if you have a horse - and wait for the captain's word.");
        }
        Chronicle.Event($"{band.Name} rode out to run the buffalo at {site.Name}.", IsoGrid.ToWorld(band.Camp), captain, band.Name, 0.8f);
        return run;
    }

    public void Arrived(Npc rider) => AtMuster.Add(rider);

    // Every frame while the run is on.
    public void Tick(float hours)
    {
        Hours += hours;
        Riders.RemoveAll(r => r == null || !GodotObject.IsInstanceValid(r) || r.IsDead);
        switch (Phase)
        {
            case RunPhase.Muster:
                // Anyone at the herd before the word: a shot into it, or a
                // rider (yours truly included) going in among them.
                if (Site.LiveCount < _liveAtStart) { Break(NearestTo(Site.WorldPos), "a beast was shot before the word"); return; }
                _liveAtStart = Mathf.Max(_liveAtStart, Site.LiveCount);
                foreach (var pc in Sim.Players())
                    if (!pc.IsDead && pc.GlobalPosition.DistanceTo(Site.WorldPos) < Site.RadiusPx * 0.85f) { Break(pc, "rode in among the herd before the word"); return; }
                if (AtMuster.Count >= Riders.Count || Hours > 3f) Signal();
                break;
            case RunPhase.Running:
                RunHours += hours;
                bool anyRunning = false;
                foreach (var r in Riders) if (r.Brain.Top is Agents.Goals.BuffaloRunGoal g && g.Running) anyRunning = true;
                if (RunHours > 1.5f || !anyRunning && RunHours > 0.3f) Finish();
                break;
            case RunPhase.Home:
                bool out_ = false;
                foreach (var r in Riders) if (r.Brain.Has<Agents.Goals.BuffaloRunGoal>()) out_ = true;
                if (!out_ || Hours > 14f) Phase = RunPhase.Over;
                break;
        }
    }

    private Agent NearestTo(Vector2 p)
    {
        Agent best = null; float bd = float.MaxValue;
        foreach (var a in Sim.AgentsWithin(p, Site.RadiusPx * 2f))
        {
            if (a is Animal || a.IsDead) continue;
            float d = a.GlobalPosition.DistanceTo(p);
            if (d < bd) { bd = d; best = a; }
        }
        return best;
    }

    // The captain's word: everyone goes at once.
    private void Signal()
    {
        Phase = RunPhase.Running;
        if (Plains.Log) foreach (var r in Riders) GD.Print($"[plains] word given at {Hours:0.0}h: {r.DisplayName} {r.GlobalPosition.DistanceTo(IsoGrid.ToWorld(Muster)):0} px from the muster, {r.GlobalPosition.DistanceTo(Site.WorldPos):0} from the herd, mounted {r.Mounted}, goal {r.Brain.Top?.Label}, v {r.Velocity.Length():0}, arrived {r.Arrived}, failed {r.MoveFailed}, sleeping {r.Sleeping}, conv {r.Conversing}, tier {r.Tier}");
        Captain?.Bark("Now! Ride!");
        foreach (var pc in Sim.Players())
            if (!pc.IsDead && pc.Mounted && pc.GlobalPosition.DistanceTo(IsoGrid.ToWorld(Muster)) < 1500f)
            {
                Joined.Add(pc);
                pc.Say("The captain's arm comes down and the whole line goes. Ride in among them - every man for what he can drop.");
            }
        Sim.Noise(Site.WorldPos, Site.RadiusPx * 3f, Captain, "shot");
    }

    // Somebody went early. The herd is off before the line can close.
    public void Break(Agent who, string how)
    {
        if (Phase != RunPhase.Muster) return;
        Broken = true;
        BrokenBy = who?.DisplayName ?? "someone";
        Phase = RunPhase.Running;
        Sim.Noise(Site.WorldPos, Site.RadiusPx * 3f, who, "shot");
        Spook();
        Captain?.Bark($"{BrokenBy}! The herd is going!");
        Chronicle.Event($"{BrokenBy} {how} at {Site.Name}, and the herd ran before {Band.Name}'s line was formed.", Site.WorldPos, who, Band.Name, 2f);
        if (who is Player.PlayerController pc)
        {
            // The soldiers: they take what you killed and give you to understand it.
            foreach (var n in Sim.Npcs) if (n.HomeStores == Band && !n.IsDead) n.ChangeRapport(pc, -15f);
            int meat = pc.Inventory.TakeUpTo(ItemId.Venison, 999) + pc.Inventory.TakeUpTo(ItemId.BuffaloRobe, 99);
            if (pc.Mounted) pc.HorseCondition = Mathf.Max(5f, pc.HorseCondition - 25f);
            pc.Say($"You went at the herd before the word, and it has run. {Band.Name}'s soldiers ride you down{(meat > 0 ? ", take everything you killed" : "")} and lay a quirt across your horse and your back. The whole camp will remember who spoiled the hunt.");
            Band.OnInsult("spoiled the buffalo hunt");
        }
        else if (who is Npc rider && rider.HomeStores == Band)
        {
            foreach (var n in Sim.Npcs) if (n.HomeStores == Band && !n.IsDead && n != rider) n.ChangeRapport(rider, -20f);
            Sim.Notice($"{rider.DisplayName} broke from the line at {Site.Name} and the herd ran. The soldiers cut up his lodge cover for it.");
        }
    }

    // A rider has dropped one.
    public void Kill(Npc rider)
    {
        if (Site.Count < 1f) return;
        Kills++;
        Plains.TakeBuffalo(Site, 1);
        Sim.Noise(rider.GlobalPosition, 900f, rider, "shot");
    }

    // The herd runs off a way: a run moves it, and a spoiled one moves it further.
    private void Spook()
    {
        var away = (Site.WorldPos - IsoGrid.ToWorld(Muster)).Normalized();
        var to = IsoGrid.FromWorld(Site.WorldPos + away * Site.RadiusPx * (Broken ? 4f : 2f));
        if (WorldMap.InBounds(to) && !WorldMap.IsWater(to)) Site.Cell = to;
    }

    // The run is over: the women come out to the kill with the travois and
    // the carts, and what they can carry home is the camp's winter.
    private void Finish()
    {
        if (Phase is RunPhase.Home or RunPhase.Over) return;
        Phase = RunPhase.Home;
        if (Plains.Log) foreach (var r in Riders) GD.Print($"[plains] run over at {Hours:0.0}h: {r.DisplayName} {r.GlobalPosition.DistanceTo(Site.WorldPos):0} px from the herd (radius {Site.RadiusPx:0}), goal {r.Brain.Top?.Label}");
        if (!Broken) Spook();
        int haul = Plains.Settle(Band, Kills);
        foreach (var n in Sim.Npcs)
            if (n.HomeStores == Band && n.Role == Role.CreeWoman && n.Tier == Tier.Live && !n.IsDead && !n.Sleeping && GD.Randf() < 0.6f)
            {
                n.Brain.Clear(n);
                n.Brain.Push(n, new Agents.Goals.GoToGoal(Site.Cell, Site.RadiusPx * 0.5f));
            }
        foreach (var pc in Joined)
        {
            if (!GodotObject.IsInstanceValid(pc) || pc.IsDead) continue;
            // A share of the meat for the man who rode in the line.
            pc.Inventory.Add(ItemId.DriedMeat, 10);
            pc.Inventory.Add(ItemId.BuffaloRobe, 1);
            foreach (var n in Sim.Npcs) if (n.HomeStores == Band && !n.IsDead) n.ChangeRapport(pc, 4f);
            pc.Say($"{Band.Name} killed {Kills} at {Site.Name}. Your share of the meat comes to you: ten pieces of dried meat and a robe.");
        }
        string how = Broken ? $"a spoiled run - {BrokenBy} went before the word - and only {Kills} killed" : $"{Kills} killed";
        Sim.Notice($"{Band.Name} ran the buffalo at {Site.Name}: {how}. The women are bringing in the meat - {haul} pieces dried, and more fresh.");
        Chronicle.Event($"{Band.Name} ran the buffalo at {Site.Name}: {how}.", Site.WorldPos, Captain, Band.Name, Broken ? 1.5f : 1f);
    }

    public static void TickAll(float hours)
    {
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            Active[i].Tick(hours);
            if (Active[i].Phase == RunPhase.Over) Active.RemoveAt(i);
        }
    }

    public static BuffaloRun Of(BandLedger band) { foreach (var r in Active) if (r.Band == band) return r; return null; }
}
