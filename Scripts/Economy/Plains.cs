using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Agents.Goals;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Ecology;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Economy;

// The horse country (design doc 12.3, 16.1): the herds' days, the buffalo
// runs, the horse raids between the nations and the Métis cart brigades that
// carry the plains' pemmican to the posts. Where a player is, it is done
// with people (Ecology/BuffaloRun, HorseRaidGoal); everywhere else it is
// resolved weekly in numbers, so the country does not stand still while
// nobody is watching. Server only.
public static class Plains
{
    public static int Today => GameCalendar.Instance == null ? 0 : GameCalendar.Instance.Year * 366 + (int)GameCalendar.Instance.DayOfYear;

    private static int _lastDay = -1;
    private static float _hourAcc = 1f;
    private static readonly Dictionary<Household, int> _live = new();
    private static readonly Dictionary<Household, HerdView> _views = new();
    // Old scores between camps (raider|target -> how hot): a horse taken
    // is a horse to take back, a man killed is a man to answer for.
    public static readonly Dictionary<string, float> Feuds = new();

    private class LiveRaid
    {
        public Household Raider, Target;
        public readonly List<Npc> Party = new();
        public bool Told;
        public int Taken;
    }
    private static readonly List<LiveRaid> _raids = new();
    private static readonly List<(Household Raider, Household Target)> _tonight = new();

    public static void Reset()
    {
        _lastDay = -1; _hourAcc = 1f; _live.Clear(); Feuds.Clear(); _raids.Clear(); _tonight.Clear();
        foreach (var v in _views.Values) if (GodotObject.IsInstanceValid(v)) v.QueueFree();
        _views.Clear();
        BuffaloRun.Active.Clear(); BuffaloRun.LastRun.Clear();
    }

    public static void Tick()
    {
        var cal = GameCalendar.Instance;
        if (cal == null || !Net.Net.IsServer) return;
        float h = cal.DeltaGameHours;
        BuffaloRun.TickAll(h);
        if (_raids.Count > 0) WatchRaids(h);
        _hourAcc += h;
        if (_hourAcc >= 1f) { _hourAcc = 0f; Hourly(cal); }
        if (cal.Day != _lastDay)
        {
            bool first = _lastDay < 0;
            if (first) DevArgs();
            _lastDay = cal.Day;
            foreach (var hh in Horses.All()) hh.Herd.Day(hh);
            Recount();
            if (!first && Today % 7 == 0) Weekly(cal);
        }
    }

    // Dev: `-- --horse` gives every player a horse and puts him up on it;
    // `--plainslog` prints each run and raid as it is called.
    public static readonly bool Log = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--plainslog") >= 0;
    private static void DevArgs()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg == "--horseraid") _devRaid = true;
            if (arg != "--horse") continue;
            foreach (var pc in Sim.Players())
            {
                pc.GiveHorse("the bay", 0, 80f);
                pc.MountNow();
            }
        }
    }

    // ---- who rides ----

    public static bool TakeHorse(Npc npc)
    {
        if (npc.Mounted) return true;
        var h = npc.HomeStores;
        if (h == null || h.Herd.Free <= 0) return false;
        h.Herd.Out++;
        npc.SetMounted(Horses.CoatFor(npc.DisplayName));
        return true;
    }

    public static void ReturnHorse(Npc npc)
    {
        if (!npc.Mounted) return;
        npc.SetMounted(-1);
        var h = npc.HomeStores;
        if (h != null) h.Herd.Out = Mathf.Max(0, h.Herd.Out - 1);
    }

    // Who is actually under a horse, put right once a day; a man left in
    // the saddle by a goal cut short gets down.
    private static void Recount()
    {
        var count = new Dictionary<Household, int>();
        foreach (var n in Sim.Npcs)
        {
            if (n == null || !GodotObject.IsInstanceValid(n) || !n.Mounted) continue;
            bool riding = n.Brain.Has<BuffaloRunGoal>() || n.Brain.Has<HorseRaidGoal>() || n.Brain.Has<HuntSiteGoal>() || n.Brain.Has<RoadGoal>();
            if (n.IsDead || !riding && n.Tier == Tier.Background) { n.SetMounted(-1); continue; }
            if (n.HomeStores != null) count[n.HomeStores] = count.GetValueOrDefault(n.HomeStores) + 1;
        }
        foreach (var h in Horses.All()) h.Herd.Out = Mathf.Min(count.GetValueOrDefault(h), h.Herd.Horses);
    }

    // Dev: `-- --horseraid` sends the nearest enemy camp's young men after
    // the herd of the camp you stand in, the first night.
    private static bool _devRaid;
    private static void DevRaid()
    {
        _devRaid = false;
        BandLedger target = null;
        foreach (var b in PlainsBands()) if (IsLive(b) && (target == null || b.Herd.Free > target.Herd.Free)) target = b;
        if (target == null) { GD.Print("[plains] --horseraid: no camp with horses near you"); return; }
        BandLedger raider = null; float bd = float.MaxValue;
        foreach (var b in PlainsBands())
        {
            if (b == target || !Enemies(b.Nation, target.Nation) || b.Mouths < 3 || b is CountryBand { Country.IsFamily: true }) continue;
            float d = IsoGrid.ToWorld(b.Camp).DistanceTo(IsoGrid.ToWorld(target.Camp));
            if (d < bd) { bd = d; raider = b; }
        }
        if (raider == null) { GD.Print($"[plains] --horseraid: nobody is at war with {target.Name}"); return; }
        GD.Print($"[plains] --horseraid: {raider.Name} will come for {target.Name}'s horses tonight");
        _tonight.Add((raider, target));
    }

    // ---- every hour ----

    private static void Hourly(GameCalendar cal)
    {
        _live.Clear();
        foreach (var n in Sim.Npcs)
            if (n != null && GodotObject.IsInstanceValid(n) && !n.IsDead && n.Tier == Tier.Live && n.HomeStores != null && !n.Brain.Has<HorseRaidGoal>())
                _live[n.HomeStores] = _live.GetValueOrDefault(n.HomeStores) + 1;
        Views();
        if (_devRaid && _live.Count > 0) DevRaid();
        if (cal.Hour >= 6f && cal.Hour < 9f) CallRuns(cal);
        // The small hours, when the camp sleeps and the horse guard nods.
        if (cal.Hour >= 1f && cal.Hour < 3f) LaunchRaids();
    }

    public static bool IsLive(Household h) => _live.GetValueOrDefault(h) > 0;

    private static IEnumerable<BandLedger> PlainsBands()
    {
        foreach (var b in Sim.Bands) if (b.Herd.Horses > 0) yield return b;
        foreach (var c in Camps.All) if (c.Ledger != null && c.Ledger.Herd.Horses > 0) yield return c.Ledger;
    }

    private static bool InSeason(int month, bool near) => month is >= 6 and <= 11 || near && month is 12 or <= 3;

    // The herd within a day's ride of the camp: the thickest, a near one
    // before a far one, so the camps of a country spread over its herds
    // instead of all running the nearest into the ground.
    public static SpawnSite HerdNear(BandLedger b, float reach)
    {
        SpawnSite best = null; float bestScore = 0f;
        if (Sim.Ecology == null) return null;
        var at = IsoGrid.ToWorld(b.Camp);
        foreach (var s in Sim.Ecology.Sites)
        {
            if (s.Species != Species.Buffalo || s.IsDead || s.Count < 1f) continue;
            float d = s.WorldPos.DistanceTo(at);
            if (d > reach) continue;
            float score = s.Count * (1f - 0.5f * d / reach);
            if (score > bestScore) { bestScore = score; best = s; }
        }
        return best;
    }

    private static void CallRuns(GameCalendar cal)
    {
        foreach (var b in PlainsBands())
        {
            if (!IsLive(b) || b.Herd.Free < 2 || BuffaloRun.Of(b) != null || b is CountryBand { Country.IsFamily: true }) continue;
            if (BuffaloRun.LastRun.TryGetValue(b.Title, out int last) && Today - last < 5) continue;
            var site = HerdNear(b, 9000f);
            if (site == null || site.Count < site.Capacity * 0.25f) continue;
            if (!InSeason(cal.Month, site.WorldPos.DistanceTo(IsoGrid.ToWorld(b.Camp)) < 5000f)) continue;
            var riders = new List<Npc>();
            foreach (var n in Sim.Npcs)
            {
                if (riders.Count >= b.Herd.Free) break;
                if (n.HomeStores != b || n.IsDead || n.Tier != Tier.Live || n.Sleeping || n.Away || n.IsFemale || n.HeldBy != null) continue;
                if (n.Role is not (Role.CreeHunter or Role.CreeChief) || n.Needs.Health < 50f) continue;
                if (n.Brain.Has<BuffaloRunGoal>() || n.Brain.Has<HorseRaidGoal>()) continue;
                riders.Add(n);
            }
            if (riders.Count < 2) { if (Log) GD.Print($"[plains] {cal.DateString} {b.Name}: only {riders.Count} riders fit to go (horses free {b.Herd.Free})"); continue; }
            var captain = b.Leader != null && riders.Contains(b.Leader) ? b.Leader : riders[0];
            b.Believe(site.Name, site.Cell);
            if (Log) GD.Print($"[plains] {cal.DateString} {b.Name} run at {site.Name}: {riders.Count} riders, herd {site.Count:0}");
            BuffaloRun.Call(b, site, riders, captain);
        }
    }

    // A herd's count is the herd within a camp's reach, and a beast of it is
    // a band of animals rather than one: the plains behind it are wide and
    // full, and a run of a dozen buffalo takes a piece of the herd, not a
    // twelfth of it. Kills by the run come off the count at this rate.
    public const int BuffaloPerHead = 8;
    public static void TakeBuffalo(SpawnSite site, int kills)
    {
        int units = kills / BuffaloPerHead + (GD.Randf() < (kills % BuffaloPerHead) / (float)BuffaloPerHead ? 1 : 0);
        if (units > 0) site.Take(units);
    }

    // What a run brings home: dried on the kill ground by the women and
    // carried in on the travois and the carts. A camp with carts brings in
    // everything; a camp afoot leaves most of the meat for the wolves.
    public static int Settle(BandLedger b, int kills)
    {
        if (kills <= 0) return 0;
        float haul = b.Herd.Carts > 0 ? 1f : b.Herd.Horses > 0 ? 0.8f : 0.45f;
        int dried = Mathf.RoundToInt(kills * 40 * haul);
        b.Stores.Add(ItemId.DriedMeat, dried);
        b.Stores.Add(ItemId.Venison, Mathf.RoundToInt(kills * 10 * haul));
        // The robe hunt is a winter hunt: the hair is prime from the fall.
        int m = GameCalendar.Instance?.Month ?? 7;
        if (m is >= 10 or <= 3) b.Stores.Add(ItemId.BuffaloRobe, Mathf.Max(1, Mathf.RoundToInt(kills * haul)));
        return dried;
    }

    // ---- every week, in numbers ----

    private static void Weekly(GameCalendar cal)
    {
        foreach (var b in new List<BandLedger>(PlainsBands()))
        {
            // The camp's scouts keep the herd in sight: where it has gone this
            // week is known at the lodges (design doc 16.5).
            if (b is CountryBand)
                foreach (var s in Sim.Ecology?.Sites ?? new List<SpawnSite>())
                    if (s.Species == Species.Buffalo && !s.IsDead && s.WorldPos.DistanceTo(IsoGrid.ToWorld(b.Camp)) < 16000f)
                    { b.KnownSites.Add(s.Name); b.Believe(s.Name, s.Cell); }
            if (!IsLive(b)) RunInNumbers(b, cal);
            MakePemmican(b);
            Freight(b, cal);
        }
        Raids(cal);
        if (Log)
        {
            var herds = new List<string>();
            foreach (var s in Sim.Ecology?.Sites ?? new List<SpawnSite>()) if (s.Species == Species.Buffalo) herds.Add($"{s.Count:0}");
            int horses = 0; foreach (var h in Horses.All()) horses += h.Herd.Horses;
            GD.Print($"[plains] {cal.DateString} week: herds {string.Join(" ", herds)}; {horses} horses in the country");
        }
    }

    private static void RunInNumbers(BandLedger b, GameCalendar cal)
    {
        // A family by a post hunts the post's country, not the herd: the run is a camp's.
        if (b is CountryBand { Country.IsFamily: true }) return;
        if (b.Herd.Free < 2 || GD.Randf() > 0.4f) return;
        var site = HerdNear(b, 9000f);
        if (site == null || site.Count < site.Capacity * 0.25f) return;
        if (!InSeason(cal.Month, site.WorldPos.DistanceTo(IsoGrid.ToWorld(b.Camp)) < 5000f)) return;
        int riders = Mathf.Min(b.Herd.Free, Mathf.Max(2, b.Mouths / 3));
        int kills = 0;
        for (int i = 0; i < riders; i++) kills += GD.RandRange(1, 5);
        kills = Mathf.Min(kills, Mathf.Max(1, Mathf.RoundToInt(site.Count * BuffaloPerHead * 0.05f)));
        TakeBuffalo(site, kills);
        Settle(b, kills);
        BuffaloRun.LastRun[b.Title] = Today;
        if (Log) GD.Print($"[plains] {cal.DateString} {b.Name} ran {site.Name} in numbers: {kills} killed, herd now {site.Count:0}; horses {b.Herd.Describe()}");
        Chronicle.Event($"{b.Name} ran the buffalo at {site.Name}: {kills} killed.", site.WorldPos, subject: b.Name, weight: 0.4f);
    }

    // The women make the dried meat up into pemmican, the plains' currency.
    private static void MakePemmican(BandLedger b)
    {
        int spare = b.Stores.Count(ItemId.DriedMeat) - b.Mouths * 40;
        if (spare < 20) return;
        int bags = Mathf.Min(spare, 200) / 2;
        b.Stores.Remove(ItemId.DriedMeat, bags * 2);
        b.Stores.Add(ItemId.Pemmican, bags);
    }

    // The pemmican goes to the posts by cart and travois (design doc 13.1):
    // the Métis carts carry it by the ton, a camp of travois by the bag.
    private static void Freight(BandLedger b, GameCalendar cal)
    {
        if (b is not CountryBand cb || cal.Month is < 6 or > 10) return;
        int spare = b.Stores.Count(ItemId.Pemmican) - b.Mouths * 20;
        if (spare < 30) return;
        int capacity = b.Herd.Carts * 500 + b.Herd.Free * 100 + b.Mouths * 20;
        int lot = Mathf.Min(Mathf.Min(spare, capacity), 600);
        FortLedger post = null; float bd = 20000f;
        var at = IsoGrid.ToWorld(b.Camp);
        foreach (var p in Posts.Standing())
        {
            if (p.Ledger == null) continue;
            float d = IsoGrid.ToWorld(p.Ledger.AccessCell).DistanceTo(at);
            if (d < bd) { bd = d; post = p.Ledger; }
        }
        if (post == null) return;
        int pay = Mathf.Min(post.Stores.Count(ItemId.TradeGoods), Mathf.Max(1, Mathf.RoundToInt(lot * Items.Items.Of(ItemId.Pemmican).ValueMb * post.PeltPrice)));
        if (pay <= 0) return;
        lot = Mathf.Min(lot, Mathf.RoundToInt(pay / (Items.Items.Of(ItemId.Pemmican).ValueMb * post.PeltPrice)) + 1);
        if (Log) GD.Print($"[plains] {cal.DateString} {cb.Name} freight {lot} pemmican to {post.Title} ({(b.Herd.Carts > 0 ? $"{b.Herd.Carts} carts" : "travois")}), {pay} goods");
        b.Stores.Remove(ItemId.Pemmican, lot);
        post.Stores.Add(ItemId.Pemmican, lot);
        post.Stores.Remove(ItemId.TradeGoods, pay);
        b.Stores.Add(ItemId.TradeGoods, pay);
        post.GoodsPaidMb += pay;
        string by = b.Herd.Carts > 0 ? $"in {b.Herd.Carts} carts" : b.Herd.Horses > 0 ? "on the travois" : "on their backs";
        Chronicle.Event($"{cb.Name} brought {lot} pemmican to {post.Title} {by}.", IsoGrid.ToWorld(post.AccessCell), subject: cb.Name, weight: 0.5f);
        if (NearPlayer(IsoGrid.ToWorld(post.AccessCell), 6000f))
            Sim.Notice($"{cb.Name} have come in {by} with {lot} pieces of pemmican for {post.Title}, and taken {pay} in goods.");
    }

    // ---- raids ----

    // Who goes after whose horses (design doc 7.2): the Blackfoot and their
    // allies against the Cree, the Assiniboine and the peoples across the
    // mountains; the Sioux against the Assiniboine, the Ojibwe, the Cree and
    // the Métis; the Crow and the Shoshone against the Blackfoot and the Sioux.
    public static bool Enemies(Faction a, Faction b)
    {
        static bool Pair(Faction x, Faction y, Faction p, Faction q) => x == p && y == q || x == q && y == p;
        return Pair(a, b, Faction.Blackfoot, Faction.Cree) || Pair(a, b, Faction.Blackfoot, Faction.Assiniboine) || Pair(a, b, Faction.Blackfoot, Faction.Plateau)
            || Pair(a, b, Faction.Blackfoot, Faction.Missouri) || Pair(a, b, Faction.Sioux, Faction.Assiniboine) || Pair(a, b, Faction.Sioux, Faction.Ojibwe)
            || Pair(a, b, Faction.Sioux, Faction.Cree) || Pair(a, b, Faction.Sioux, Faction.Metis) || Pair(a, b, Faction.Sioux, Faction.Missouri);
    }

    private static Faction NationOf(Household h) => h is BandLedger b ? b.Nation : h is FortLedger f ? f.Company : Faction.HBC;
    private static string NameOf(Household h) => h is BandLedger b ? b.Name : h.Title;
    private static string Key(Household a, Household b) => $"{NameOf(a)}|{NameOf(b)}";
    public static float Feud(Household holder, Household against) => Feuds.GetValueOrDefault(Key(holder, against));
    private static void Heat(Household holder, Household against, float by) => Feuds[Key(holder, against)] = Mathf.Min(10f, Feud(holder, against) + by);

    private static void Raids(GameCalendar cal)
    {
        // Old scores cool a little every week.
        foreach (var k in new List<string>(Feuds.Keys)) { Feuds[k] *= 0.93f; if (Feuds[k] < 0.05f) Feuds.Remove(k); }
        if (cal.Month is < 4 or > 10) return;   // nobody goes for horses in the deep snow
        foreach (var raider in new List<BandLedger>(PlainsBands()))
        {
            if (raider.Mouths < 5 || raider is CountryBand { Country.IsFamily: true }) continue;   // young men of a camp, not a family by a post
            float perHead = raider.Herd.Horses / (float)Mathf.Max(1, raider.Mouths);
            Household target = null; float best = 0f;
            var at = IsoGrid.ToWorld(raider.Camp);
            foreach (var h in Horses.All())
            {
                if (h == raider || h.Herd.Free < 3) continue;
                bool post = h is FortLedger;
                // The Cheyenne had come in with the Lakota by then.
                if (NameOf(h).Contains("Cheyenne") && raider.Nation == Faction.Sioux || raider.Name.Contains("Cheyenne") && NationOf(h) == Faction.Sioux) continue;
                if (post ? raider.Nation is not (Faction.Blackfoot or Faction.Assiniboine or Faction.Cree or Faction.Sioux) : !Enemies(raider.Nation, NationOf(h))) continue;
                float d = IsoGrid.ToWorld(h.AccessCell).DistanceTo(at);
                if (d > 20000f) continue;
                float score = (1f + Feud(raider, h) * 2f) * h.Herd.Free / (1f + d / 4000f) * (post ? 0.4f : 1f);
                if (score > best) { best = score; target = h; }
            }
            if (target == null) continue;
            float chance = 0.04f * (perHead < 1.5f ? 1.6f : 1f) * (1f + Feud(raider, target));
            if (GD.Randf() > chance) continue;
            if (Log) GD.Print($"[plains] {cal.DateString} raid {raider.Name} -> {NameOf(target)} ({(IsLive(target) ? "live" : "numbers")})");
            if (IsLive(target)) _tonight.Add((raider, target));
            else RaidInNumbers(raider, target);
        }
    }

    private static void RaidInNumbers(BandLedger raider, Household target)
    {
        int before = target.Herd.Horses;
        RaidOutcome(raider, target);
        if (Log) GD.Print($"[plains] {GameCalendar.Instance.DateString}   {raider.Name} -> {NameOf(target)}: target {before} -> {target.Herd.Horses} horses, raider now {raider.Herd.Horses}");
    }

    private static void RaidOutcome(BandLedger raider, Household target)
    {
        var where = IsoGrid.ToWorld(target.AccessCell);
        bool told = NearPlayer(where, 12000f) || NearPlayer(IsoGrid.ToWorld(raider.Camp), 12000f);
        if (GD.Randf() < 0.55f)
        {
            int n = Mathf.Clamp(GD.RandRange(3, 10), 1, Mathf.Max(1, target.Herd.Free / 4));
            target.Herd.Lose(n);
            raider.Herd.Horses += n;
            Heat(target, raider, 1f);
            Chronicle.Event($"{raider.Name}'s young men ran off {n} horses from {NameOf(target)}.", where, subject: raider.Name, weight: 1f);
            if (told) Sim.Notice($"Horse thieves: {raider.Name}'s young men ran off {n} horses from {NameOf(target)} in the night.");
            return;
        }
        if (GD.Randf() < 0.3f)
        {
            Npc dead = null;
            foreach (var m in Sim.Npcs) if (m.HomeStores == raider && !m.IsDead && m.Role == Role.CreeHunter && m.Tier == Tier.Background && !m.Away) { dead = m; break; }
            if (dead != null)
            {
                Heat(raider, target, 2f);
                Chronicle.Event($"{dead.DisplayName} of {raider.Name} was killed going after {NameOf(target)}'s horses.", where, dead, raider.Name, 2f);
                if (told) Sim.Notice($"{dead.DisplayName} of {raider.Name} was killed stealing horses from {NameOf(target)}. His people will want it answered.");
                dead.DieQuietly();
                return;
            }
        }
        Chronicle.Event($"{raider.Name}'s young men went after {NameOf(target)}'s horses and were seen off.", where, subject: raider.Name, weight: 0.5f);
    }

    // A raid on a herd a player is near is done with people: the young men
    // come up in the dark, on foot, and go home riding.
    private static void LaunchRaids()
    {
        if (_tonight.Count == 0) return;
        foreach (var (raider, target) in _tonight)
        {
            var herd = HerdCell(target);
            // They come up on the herd from the far side of it, away from the lodges.
            var lodges = target is BandLedger tb ? IsoGrid.ToWorld(tb.Camp) : IsoGrid.ToWorld(target.AccessCell);
            var dir = (IsoGrid.ToWorld(herd) - lodges).Normalized();
            var start = FindOpen(IsoGrid.FromWorld(IsoGrid.ToWorld(herd) + dir * 3000f));
            var raid = new LiveRaid { Raider = raider, Target = target };
            foreach (var n in Sim.Npcs)
            {
                if (raid.Party.Count >= 3) break;
                if (n.HomeStores != raider || n.IsDead || n.Away || n.IsFemale || n.Role != Role.CreeHunter || n.HeldBy != null || n.Tier != Tier.Background) continue;
                n.Tier = Tier.Live;
                n.ProcessMode = Node.ProcessModeEnum.Inherit;
                n.Visible = true;
                n.GlobalPosition = IsoGrid.ToWorld(start) + new Vector2(GD.RandRange(-80, 80), GD.RandRange(-40, 40));
                n.Brain.Clear(n);
                n.Brain.Push(n, new HorseRaidGoal(target, raider, herd));
                Net.Net.I?.AnnounceTier(n, false);
                raid.Party.Add(n);
            }
            if (Log) GD.Print($"[plains] {GameCalendar.Instance.DateString} raiders from {NameOf(raider)} ({raid.Party.Count}) come up on {NameOf(target)}'s herd");
            if (Log) foreach (var w in Sim.Npcs) if (w.HomeStores == target && !w.IsDead && !w.IsFemale) GD.Print($"[plains]   {w.DisplayName}: {w.GlobalPosition.DistanceTo(IsoGrid.ToWorld(herd)):0} px from the herd, asleep {w.Sleeping}, {w.Brain.Top?.Label}, tier {w.Tier}");
            if (raid.Party.Count > 0) _raids.Add(raid);
            else RaidInNumbers((BandLedger)raider, target);
        }
        _tonight.Clear();
    }

    // Whoever is awake at the herd sees them; a shot or a blow and it is a fight.
    private static void WatchRaids(float hours)
    {
        // A man awake within sight notices them in the end, not at once: in
        // a quarter of an hour, more often than not.
        float notice = 1f - Mathf.Pow(0.06f, hours);
        for (int i = _raids.Count - 1; i >= 0; i--)
        {
            var raid = _raids[i];
            bool goingOn = false, seen = false;
            int taken = raid.Taken;
            foreach (var r in raid.Party)
            {
                if (r == null || !GodotObject.IsInstanceValid(r)) continue;
                if (r.Brain.Top is HorseRaidGoal || r.Brain.Has<HorseRaidGoal>()) goingOn = true;
                foreach (var g in r.Brain.Stack) if (g is HorseRaidGoal hg) seen |= hg.Seen;
                if (r.IsDead || r.LastAttackedAgo < 2f) seen = true;
                // In the dark a man is seen only close to; by day, across the camp.
                float hour = GameCalendar.Instance.Hour;
                float sight = hour >= 21f || hour < 5f ? 380f : 900f;
                if (!seen && !raid.Told && !r.IsDead)
                    foreach (var a in Sim.AgentsWithin(r.GlobalPosition, sight))
                    {
                        if (a is Player.PlayerController pc && !pc.IsDead && GD.Randf() < notice) { seen = true; if (Log) GD.Print($"[plains] the raiders were seen by {pc.DisplayName}"); break; }
                        if (a is Npc w && w.HomeStores == raid.Target && !w.IsDead && !w.Sleeping && !w.IsFemale && GD.Randf() < notice) { seen = true; if (Log) GD.Print($"[plains] the raiders were seen by {w.DisplayName} ({w.Brain.Top?.Label}, {w.GlobalPosition.DistanceTo(r.GlobalPosition):0} px, raider doing {r.Brain.Top?.Label}, hour {hour:0.0})"); break; }
                    }
            }
            if (seen && !raid.Told)
            {
                raid.Told = true;
                foreach (var r in raid.Party) foreach (var g in r.Brain.Stack) if (g is HorseRaidGoal hg) hg.Seen = true;
                // The camp turns out: the armed men go for them.
                foreach (var w in Sim.Npcs)
                {
                    if (w.HomeStores != raid.Target || w.IsDead || w.Tier != Tier.Live || w.IsFemale || w.Weapon == null) continue;
                    Npc nearest = null; float nd = 4000f;
                    foreach (var r in raid.Party) if (!r.IsDead && r.GlobalPosition.DistanceTo(w.GlobalPosition) < nd) { nd = r.GlobalPosition.DistanceTo(w.GlobalPosition); nearest = r; }
                    if (nearest == null) continue;
                    w.Sleeping = false;
                    w.Brain.Clear(w);
                    w.Brain.Push(w, new AttackGoal(nearest));
                }
                Sim.Notice($"Horse thieves at {NameOf(raid.Target)}'s herd! {NameOf(raid.Raider)}'s young men are among the horses.");
            }
            if (goingOn) continue;
            // It is over: count what got away and who did not come home.
            int dead = 0;
            foreach (var r in raid.Party) if (r == null || !GodotObject.IsInstanceValid(r) || r.IsDead) dead++;
            if (taken > 0) { raid.Raider.Herd.Horses += taken; Heat(raid.Target, raid.Raider, 1f); }
            if (dead > 0) Heat(raid.Raider, raid.Target, 2f * dead);
            string how = taken > 0 ? $"got away with {taken} horses" : "went home with nothing";
            Chronicle.Event($"{NameOf(raid.Raider)}'s young men came for {NameOf(raid.Target)}'s horses and {how}{(dead > 0 ? $"; {dead} of them did not go home" : "")}.", IsoGrid.ToWorld(raid.Target.AccessCell), subject: NameOf(raid.Raider), weight: 1.5f);
            Sim.Notice($"The horse thieves from {NameOf(raid.Raider)} {how}{(dead > 0 ? $", and left {dead} dead behind them" : "")}.");
            foreach (var pc in Sim.Players())
                foreach (var r in raid.Party)
                    if (r != null && GodotObject.IsInstanceValid(r) && r.IsDead && r.LastAttacker == pc)
                        foreach (var n in Sim.Npcs) if (n.HomeStores == raid.Target && !n.IsDead) n.ChangeRapport(pc, 8f);
            _raids.RemoveAt(i);
        }
    }

    // Raiders among the herd cut out what they can drive.
    public static int CutOut(Npc raider, Household target)
    {
        // What one man can drive off in the dark: a few, and never the whole herd.
        int n = Mathf.Min(Mathf.Max(1, target.Herd.Free / 4), GD.RandRange(2, 5));
        if (target.Herd.Free <= 0) return 0;
        target.Herd.Lose(n);
        foreach (var raid in _raids) if (raid.Party.Contains(raider)) raid.Taken += n;
        if (Log) GD.Print($"[plains] {raider.DisplayName} cut out {n} horses from {NameOf(target)}'s herd");
        raider.SetMounted(GD.RandRange(0, Horses.Coats.Length - 1));
        return n;
    }

    // The player after a horse from somebody's herd: at night and unseen, or not at all.
    public static string TryCutOut(Player.PlayerController pc)
    {
        foreach (var (h, view) in _views)
        {
            if (!GodotObject.IsInstanceValid(view) || view.GlobalPosition.DistanceTo(pc.GlobalPosition) > 320f || h.Herd.Free <= 0) continue;
            var cal = GameCalendar.Instance;
            bool dark = cal.Hour >= 21f || cal.Hour < 5f;
            if (!dark) return $"These are {NameOf(h)}'s horses, and the whole camp can see you. Trade for one, or come back in the dark.";
            foreach (var a in Sim.AgentsWithin(pc.GlobalPosition, 900f))
                if (a is Npc w && w.HomeStores == h && !w.IsDead && !w.Sleeping && !w.IsFemale)
                {
                    foreach (var n in Sim.Npcs) if (n.HomeStores == h && !n.IsDead) n.ChangeRapport(pc, -25f);
                    Chronicle.Event($"{pc.DisplayName} was caught at {NameOf(h)}'s horses in the night.", pc.GlobalPosition, pc, NameOf(h), 2f);
                    return $"{w.DisplayName} is awake among the horses and sees you at the hobbles. {NameOf(h)} will not forget it.";
                }
            h.Herd.Lose(1);
            pc.GiveHorse(HorseNames[GD.RandRange(0, HorseNames.Length - 1)], GD.RandRange(0, Horses.Coats.Length - 1), h.Herd.Condition);
            pc.MountNow();
            Chronicle.Event($"A horse was taken from {NameOf(h)}'s herd in the night; nobody saw who.", pc.GlobalPosition, null, NameOf(h), 1f);
            return $"You cut a horse out of {NameOf(h)}'s herd in the dark and get up on it bareback. Nobody saw. Ride.";
        }
        return null;
    }

    public static readonly string[] HorseNames = { "the bay", "the grey", "the buckskin", "the paint", "the roan", "the sorrel", "the black", "the dun" };

    // ---- the herds you can see ----

    public static Vector2I HerdCell(Household h)
    {
        // Out on the grass a few hundred yards off: near enough to watch, far
        // enough not to eat the lodges' own ground bare.
        var near = h is BandLedger b ? IsoGrid.Cell(b.Camp, 16, 6) : IsoGrid.Cell(h.AccessCell, 18, 5);
        return FindOpen(near);
    }

    private static Vector2I FindOpen(Vector2I near)
    {
        var paths = Sim.Paths;
        if (paths == null) return near;
        for (int r = 0; r < 10; r++)
            for (int dy = -r * 2; dy <= r * 2; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    var c = near + new Vector2I(dx, dy);
                    if (paths.IsWalkable(c) && !WorldLayout.ForestAt(c)) return c;
                }
        return near;
    }

    private static void Views()
    {
        var host = Sim.Ecology;
        if (host == null) return;
        var want = new HashSet<Household>();
        foreach (var h in Horses.All())
        {
            if (h.Herd.Free <= 0) continue;
            bool near = h is BandLedger ? IsLive(h) : NearPlayer(IsoGrid.ToWorld(h.AccessCell), Tiers.LiveRadius);
            if (near) want.Add(h);
        }
        foreach (var h in new List<Household>(_views.Keys))
            if (!want.Contains(h) || !GodotObject.IsInstanceValid(_views[h])) { if (GodotObject.IsInstanceValid(_views[h])) _views[h].QueueFree(); _views.Remove(h); }
        foreach (var h in want)
        {
            if (!_views.TryGetValue(h, out var v))
            {
                v = new HerdView { Name = "Herd", Position = IsoGrid.ToWorld(HerdCell(h)), Seed = NameOf(h) };
                host.AddChild(v);
                _views[h] = v;
            }
            v.SetCount(Mathf.Min(h.Herd.Free, 10));
        }
    }

    // ---- small things ----

    public static bool NearPlayer(Vector2 p, float r)
    {
        foreach (var pc in Sim.Players()) if (pc != null && GodotObject.IsInstanceValid(pc) && pc.GlobalPosition.DistanceTo(p) < r) return true;
        return false;
    }

    public static string BearingWord(Vector2 from, Vector2 to)
    {
        var d = to - from;
        string ns = d.Y < -Mathf.Abs(d.X) * 0.4f ? "north" : d.Y > Mathf.Abs(d.X) * 0.4f ? "south" : "";
        string ew = d.X < -Mathf.Abs(d.Y) * 0.4f ? "west" : d.X > Mathf.Abs(d.Y) * 0.4f ? "east" : "";
        return ns != "" && ew != "" ? $"{ns}-{ew}" : ns + ew == "" ? "close by" : ns + ew;
    }

    public static Godot.Collections.Dictionary Save()
    {
        var f = new Godot.Collections.Dictionary(); foreach (var kv in Feuds) f[kv.Key] = kv.Value;
        var l = new Godot.Collections.Dictionary(); foreach (var kv in BuffaloRun.LastRun) l[kv.Key] = kv.Value;
        return new Godot.Collections.Dictionary { ["feuds"] = f, ["lastRun"] = l };
    }

    public static void Load(Godot.Collections.Dictionary d)
    {
        Feuds.Clear(); BuffaloRun.LastRun.Clear();
        if (d == null) return;
        if (d.ContainsKey("feuds")) foreach (var kv in d["feuds"].AsGodotDictionary()) Feuds[kv.Key.AsString()] = kv.Value.AsSingle();
        if (d.ContainsKey("lastRun")) foreach (var kv in d["lastRun"].AsGodotDictionary()) BuffaloRun.LastRun[kv.Key.AsString()] = kv.Value.AsInt32();
    }
}
