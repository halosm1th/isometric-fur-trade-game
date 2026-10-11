using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Core;

// Everything the game sounds like (docs/audio-prompts.md). Autoloaded, so the
// music carries across scene changes. It mostly listens rather than being
// told: sounds the simulation already makes (Sim.Noise - footsteps, shots,
// axes) reach it through Sim.ShowNoise on every machine, and the rest - the
// weather, the season, the hour, the ship, a fire, being indoors - it reads
// off the world each frame. Three buses under Master: Music, Ambience,
// Effects, each with a volume in Settings.
//
// Files live in res://assets/audio/{music,ambient,sfx,ui}. A sound is asked
// for by name; "step_snow" finds step_snow_01..09 and picks one at random,
// so more takes can be dropped in without touching code.
public partial class Audio : Node
{
    public static Audio I { get; private set; }

    private const string Root = "res://assets/audio/";
    private static readonly string[] Dirs = { "sfx", "ui", "ambient", "music" };
    private static readonly string[] Exts = { ".wav", ".ogg", ".mp3" };
    public const string MusicBus = "Music", AmbienceBus = "Ambience", EffectsBus = "Effects";

    // Which tracks play where. Song titles are the files' names.
    private static readonly string[] MenuTracks = { "claim_the_north" };
    private static readonly Dictionary<Season, string[]> SeasonTracks = new()
    {
        { Season.Winter, new[] { "the_long_winter_at_the_bay", "planks_beneath_the_frost" } },
        { Season.Breakup, new[] { "waiting_on_the_ice" } },
        { Season.OpenWater, new[] { "boots_on_the_pine_floor" } },
        { Season.FreezeUp, new[] { "waiting_on_the_ice", "the_long_winter_at_the_bay" } },
    };
    // Evenings near the men's house: the fiddle is going.
    private static readonly string[] ReelTracks = { "mens_house_reel", "boots_on_the_pine_floor" };

    private readonly Dictionary<string, List<AudioStream>> _cache = new();
    // --audiotrace prints every sound as it starts, for checking without ears.
    private static bool _trace;
    private static void Trace(string what) { if (_trace) GD.Print($"[audio] {what}"); }
    private readonly RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        I = this;
        ProcessMode = ProcessModeEnum.Always;
        _rng.Randomize();
        foreach (var arg in OS.GetCmdlineUserArgs()) if (arg == "--audiotrace") _trace = true;
        EnsureBuses();
        ApplyVolumes();
        if (DisplayServer.GetName() == "headless") { SetProcess(false); return; }

        _music = new AudioStreamPlayer { Bus = MusicBus };
        _music.Finished += OnMusicFinished;
        AddChild(_music);
        for (int i = 0; i < 6; i++) { var p = new AudioStreamPlayer { Bus = EffectsBus }; AddChild(p); _flat.Add(p); }
        for (int i = 0; i < 24; i++) { var p = new AudioStreamPlayer2D { Bus = EffectsBus, Attenuation = 1.6f }; AddChild(p); _pool.Add(p); }

        _wind = Bed("amb_winter_wind");
        _blizzard = Bed("amb_blizzard");
        _river = Bed("amb_river");
        _mosquitoes = Bed("amb_mosquitoes");
        _paddling = Bed("canoe_paddling_loop");
        _rain = Bed("amb_rain");
        // Voices aren't muffled by the walls you're inside with them.
        _chatter = Bed("amb_fort_chatter", EffectsBus);
        for (int i = 0; i < 3; i++) _fires.Add(Loop2D("fire_hearth_loop", 700f));
        _blaze = Loop2D("fire_hearth_loop", 1400f);
        _shipCreak = Loop2D("ship_creak_loop", 1300f);
        _kids = Loop2D("amb_kids_playing", 1100f);

        // The ambience goes dull through a log wall.
        int amb = AudioServer.GetBusIndex(AmbienceBus);
        if (AudioServer.GetBusEffectCount(amb) == 0) AudioServer.AddBusEffect(amb, new AudioEffectLowPassFilter { CutoffHz = 900f });
        AudioServer.SetBusEffectEnabled(amb, 0, false);

        _nextCritter = _rng.RandfRange(10f, 25f);
    }

    // ---- buses and volume -------------------------------------------------

    private static void EnsureBuses()
    {
        foreach (var name in new[] { MusicBus, AmbienceBus, EffectsBus })
        {
            if (AudioServer.GetBusIndex(name) >= 0) continue;
            AudioServer.AddBus();
            int i = AudioServer.BusCount - 1;
            AudioServer.SetBusName(i, name);
            AudioServer.SetBusSend(i, "Master");
        }
    }

    public static void ApplyVolumes()
    {
        EnsureBuses();
        SetBus("Master", Settings.MasterVolume);
        SetBus(MusicBus, Settings.MusicVolume);
        SetBus(AmbienceBus, Settings.AmbienceVolume);
        SetBus(EffectsBus, Settings.EffectsVolume);
    }

    private static void SetBus(string name, float linear)
    {
        int i = AudioServer.GetBusIndex(name);
        if (i < 0) return;
        AudioServer.SetBusMute(i, linear <= 0.001f);
        AudioServer.SetBusVolumeDb(i, Mathf.LinearToDb(Mathf.Max(linear, 0.001f)));
    }

    // ---- finding sounds -----------------------------------------------------

    private List<AudioStream> Streams(string name)
    {
        if (_cache.TryGetValue(name, out var list)) return list;
        list = new List<AudioStream>();
        foreach (var dir in Dirs)
            foreach (var ext in Exts)
            {
                string single = $"{Root}{dir}/{name}{ext}";
                if (ResourceLoader.Exists(single)) list.Add(GD.Load<AudioStream>(single));
                for (int n = 1; n <= 9; n++)
                {
                    string take = $"{Root}{dir}/{name}_{n:00}{ext}";
                    if (ResourceLoader.Exists(take)) list.Add(GD.Load<AudioStream>(take));
                }
            }
        if (list.Count == 0) GD.PushWarning($"[audio] no file for '{name}' under {Root}");
        _cache[name] = list;
        return list;
    }

    private AudioStream Pick(string name)
    {
        var list = Streams(name);
        return list.Count == 0 ? null : list[_rng.RandiRange(0, list.Count - 1)];
    }

    private static AudioStream Looping(AudioStream s)
    {
        switch (s)
        {
            case AudioStreamWav w when w.LoopMode == AudioStreamWav.LoopModeEnum.Disabled:
                w.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
                w.LoopBegin = 0;
                w.LoopEnd = (int)(w.GetLength() * w.MixRate);
                break;
            case AudioStreamMP3 m: m.Loop = true; break;
            case AudioStreamOggVorbis o: o.Loop = true; break;
        }
        return s;
    }

    // ---- one-shots ----------------------------------------------------------

    private readonly List<AudioStreamPlayer> _flat = new();
    private readonly List<AudioStreamPlayer2D> _pool = new();

    // Where the ear is: the middle of the view.
    private Vector2 Listener => GetViewport()?.GetCamera2D()?.GetScreenCenterPosition() ?? Sim.Player?.GlobalPosition ?? Vector2.Zero;

    // A sound at a place in the world. Skipped if it's too far to hear or
    // every voice is busy (a crowd of footsteps shouldn't drown a shot).
    public static void At(string name, Vector2 pos, float db = 0f, float maxDistance = 2000f, float pitchVariance = 0.06f, float delay = 0f)
    {
        if (I == null || !I.IsProcessing()) return;
        if (delay > 0f) { I.GetTree().CreateTimer(delay).Timeout += () => At(name, pos, db, maxDistance, pitchVariance); return; }
        if (I.Listener.DistanceTo(pos) > maxDistance) return;
        var stream = I.Pick(name);
        if (stream == null) return;
        AudioStreamPlayer2D free = null;
        foreach (var p in I._pool) if (!p.Playing) { free = p; break; }
        if (free == null) return;
        free.Stream = stream;
        free.GlobalPosition = pos;
        free.MaxDistance = maxDistance;
        free.VolumeDb = db;
        free.PitchScale = 1f + I._rng.RandfRange(-pitchVariance, pitchVariance);
        free.Play();
        Trace($"{name} at {pos.X:0},{pos.Y:0} {db:0}dB");
    }

    // A sound with no place: the interface, a salute heard across the fort.
    public static void Play(string name, float db = 0f, float pitchVariance = 0f, string bus = EffectsBus, float delay = 0f)
    {
        if (I == null || !I.IsProcessing()) return;
        if (delay > 0f) { I.GetTree().CreateTimer(delay).Timeout += () => Play(name, db, pitchVariance, bus); return; }
        var stream = I.Pick(name);
        if (stream == null) return;
        AudioStreamPlayer free = null;
        foreach (var p in I._flat) if (!p.Playing) { free = p; break; }
        free ??= I._flat[0];
        free.Stream = stream;
        free.Bus = bus;
        free.VolumeDb = db;
        free.PitchScale = 1f + I._rng.RandfRange(-pitchVariance, pitchVariance);
        free.Play();
        Trace($"{name} {db:0}dB");
    }

    public static void Ui(string name, float db = -6f) => Play(name, db, 0.04f);

    // ---- sounds the simulation makes ----------------------------------------

    private float _stepBudget;

    // Called from Sim.ShowNoise, which runs on every machine.
    public static void Noise(Vector2 pos, float radius, string kind)
    {
        if (I == null || !I.IsProcessing() || Sim.Player == null) return;
        var me = Sim.Player;
        switch (kind)
        {
            case "step":
                // Your own feet, and anyone close; not the whole fort's.
                bool mine = me.GlobalPosition.DistanceSquaredTo(pos) < 40f * 40f;
                if (!mine && (I._stepBudget <= 0f || I.Listener.DistanceTo(pos) > 650f)) return;
                if (!mine) I._stepBudget -= 1f;
                // How far the step carried is how hard it was put down.
                float db = Mathf.Lerp(-18f, -5f, Mathf.Clamp((radius - 40f) / 380f, 0f, 1f)) + (mine ? 0f : -3f);
                At(I.StepSound(pos), pos, db, 700f, 0.1f);
                break;
            case "shot":
                At("musket_fire", pos, 0f, 4000f, 0.04f);
                // Then the long business of loading again (design doc 15.6).
                At("musket_reload", pos, -10f, 650f, 0.03f, 1.4f);
                // Far off, a shot is a flat boom that rolls across the country.
                float d = I.Listener.DistanceTo(pos);
                if (d > 1400f) Play("cannon_distant", Mathf.Lerp(-6f, -18f, Mathf.Clamp((d - 1400f) / 4000f, 0f, 1f)), 0.05f);
                break;
            case "chop":
                for (int i = 0; i < 4; i++) At("axe_chop", pos, -4f, 1600f, 0.08f, i * 0.65f + I._rng.RandfRange(0f, 0.15f));
                break;
            case "fell":
                At("tree_fall", pos, -2f, 2200f, 0.05f);
                break;
            case "hit":
                At("punch", pos, -3f, 1000f, 0.12f);
                break;
            case "bite":
                At("wolf_bite", pos, -2f, 1200f, 0.08f);
                break;
            case "fall":
                At("body_fall", pos, -4f, 900f, 0.08f, 0.15f);
                break;
            case "flint":
                At("flint_strike", pos, -4f, 900f, 0.05f);
                break;
        }
    }

    private HashSet<Vector2I> _wharf;
    private string StepSound(Vector2 pos)
    {
        // Boards first: a house floor, the wharf.
        if (Sim.Structures != null && Sim.Structures.IsPlankFloor(pos)) return "step_planks";
        _wharf ??= new HashSet<Vector2I>(WorldLayout.WharfCells());
        if (_wharf.Contains(IsoGrid.FromWorld(pos))) return "step_planks";
        var cal = GameCalendar.Instance;
        if (cal == null) return "step_snow";
        // Winter is snow, a crust on it after a hard clear night; the rest of
        // the year the lowlands are muskeg.
        if (cal.CurrentSeason == Season.Winter || (cal.CurrentSeason == Season.FreezeUp && cal.Month >= 10 && cal.Day > 15) || (cal.CurrentSeason == Season.Breakup && cal.Month == 5 && cal.Day < 10))
            return cal.Weather.Current == SkyState.Clear && _rng.Randf() < 0.5f ? "step_snow_crust" : "step_snow";
        return "step_mud";
    }

    // ---- ambience beds ------------------------------------------------------

    private class BedPlayer
    {
        public AudioStreamPlayer Player;
        public float Level, Target;
    }

    private BedPlayer _wind, _blizzard, _river, _mosquitoes, _paddling, _chatter, _rain;
    private readonly List<BedPlayer> _beds = new();

    private BedPlayer Bed(string name, string bus = AmbienceBus)
    {
        var s = Pick(name);
        var p = new AudioStreamPlayer { Bus = bus, Stream = s != null ? Looping(s) : null, VolumeDb = -80f };
        AddChild(p);
        var b = new BedPlayer { Player = p };
        _beds.Add(b);
        return b;
    }

    private void UpdateBeds(float dt)
    {
        foreach (var b in _beds)
        {
            // Weather comes in over a few seconds, not with a click.
            b.Level = Mathf.MoveToward(b.Level, b.Target, dt * 0.25f);
            if (b.Player.Stream == null) continue;
            if (b.Level <= 0.001f) { if (b.Player.Playing) { b.Player.Stop(); Trace($"bed off {b.Player.Stream.ResourcePath}"); } continue; }
            if (!b.Player.Playing) Trace($"bed on {b.Player.Stream.ResourcePath} -> {b.Target:0.00}");
            if (!b.Player.Playing) b.Player.Play(_rng.RandfRange(0f, (float)b.Player.Stream.GetLength() * 0.9f));
            b.Player.VolumeDb = Mathf.LinearToDb(b.Level);
        }
    }

    private float _scan;
    private float _waterNear;   // 0..1, how close the nearest open water is
    private bool _wasIndoors;

    private void ReadTheWorld(float dt)
    {
        var cal = GameCalendar.Instance;
        var me = Sim.Player;
        bool inWorld = cal != null && me != null && IsInstanceValid(me);
        foreach (var b in _beds) b.Target = 0f;
        if (!inWorld) { SetMuffled(false); return; }

        var sky = cal.Weather.Current;
        var season = cal.CurrentSeason;
        bool indoors = me.IsSheltered;
        float hour = cal.Hour;
        bool day = hour >= 6f && hour < 21f;

        if (indoors != _wasIndoors)
        {
            At("door_open", me.GlobalPosition, -8f, 600f, 0.08f);
            _wasIndoors = indoors;
        }
        SetMuffled(indoors);
        float walls = indoors ? 0.45f : 1f;

        // Wind: always something on the Bay, the most in winter.
        float wind = season switch { Season.Winter => 0.4f, Season.FreezeUp => 0.3f, Season.Breakup => 0.2f, _ => 0.1f };
        if (sky == SkyState.Overcast) wind += 0.1f;
        if (sky == SkyState.Snow || sky == SkyState.Rain) wind += 0.2f;
        if (sky == SkyState.Blizzard) { wind = 0.15f; _blizzard.Target = 1f * walls; }
        if (sky == SkyState.Storm) { wind = 0.2f; _blizzard.Target = 0.6f * walls; }
        _wind.Target = wind * walls;
        // Rain on the roof is louder than rain in the open, not softer.
        if (sky == SkyState.Rain) _rain.Target = indoors ? 0.6f : 0.75f;
        if (sky == SkyState.Storm) _rain.Target = indoors ? 0.8f : 1f;

        // Open water close by. Scanned twice a second: it's a few hundred
        // cell lookups.
        _scan -= dt;
        if (_scan <= 0f) { _scan = 0.5f; _waterNear = WaterNear(me.GlobalPosition); _crowd = Crowd(me.GlobalPosition); }
        _river.Target = _waterNear * 0.55f * (indoors ? 0.25f : 1f);

        // The flies: high summer, by day, out of doors, when it isn't raining.
        bool flySeason = season == Season.OpenWater || (season == Season.Breakup && cal.Month == 6);
        if (flySeason && day && !indoors && sky is SkyState.Clear or SkyState.Overcast) _mosquitoes.Target = 0.22f;

        // The murmur of a post with people about: the yard by day, the men's
        // house of an evening. More of it the more people there are.
        if (_crowd > 1 && NearAPost(me.GlobalPosition))
            _chatter.Target = Mathf.Clamp((_crowd - 1) / 8f, 0f, 1f) * (indoors ? 0.5f : 0.35f);

        // Paddling.
        if (me is Player.PlayerController pc && pc.IsAfloat && pc.Velocity.LengthSquared() > 25f) _paddling.Target = 0.8f;

        UpdateFires(me.GlobalPosition);
        UpdateShip();
        UpdateBlaze();
        UpdateKids(cal, day);
        Critters(dt, cal, me, indoors, day);
    }

    // How close open water is, 1 at the edge, 0 at Reach px or more. The
    // grid is staggered: columns 128 px apart, rows 32.
    private const float WaterReach = 640f;
    private static float WaterNear(Vector2 pos)
    {
        var centre = IsoGrid.FromWorld(pos);
        int cols = Mathf.CeilToInt(WaterReach / 128f) + 1, rows = Mathf.CeilToInt(WaterReach / 32f) + 1;
        float best = float.MaxValue;
        for (int dx = -cols; dx <= cols; dx++)
            for (int dy = -rows; dy <= rows; dy++)
            {
                var c = new Vector2I(centre.X + dx, centre.Y + dy);
                bool water = Sim.Terrain != null ? Sim.Terrain.IsWater(c) : WorldMap.IsWater(c);
                if (water) best = Mathf.Min(best, IsoGrid.ToWorld(c).DistanceTo(pos));
            }
        return best == float.MaxValue ? 0f : 1f - Mathf.Clamp(best / WaterReach, 0f, 1f);
    }

    private int _crowd;   // waking people within earshot
    private static int Crowd(Vector2 pos)
    {
        int n = 0;
        foreach (var a in Sim.AgentsWithin(pos, 520f))
            if (a is Agents.Npc npc && !npc.IsDead && !npc.Sleeping && npc.Tier == Tier.Live) n++;
        return n;
    }

    private static bool NearAPost(Vector2 pos)
    {
        if (pos.DistanceTo(WorldLayout.FortCentre) < 1800f) return true;
        foreach (var post in Sim.Posts())
            if (post != null && IsoGrid.ToWorld(post.AccessCell).DistanceTo(pos) < 1200f) return true;
        return false;
    }

    private bool _muffled;
    private void SetMuffled(bool on)
    {
        if (on == _muffled) return;
        _muffled = on;
        int amb = AudioServer.GetBusIndex(AmbienceBus);
        if (amb >= 0 && AudioServer.GetBusEffectCount(amb) > 0) AudioServer.SetBusEffectEnabled(amb, 0, on);
    }

    // ---- looping sounds at places -------------------------------------------

    private readonly List<AudioStreamPlayer2D> _fires = new();
    private AudioStreamPlayer2D _blaze, _shipCreak;

    private AudioStreamPlayer2D Loop2D(string name, float maxDistance)
    {
        var s = Pick(name);
        var p = new AudioStreamPlayer2D { Bus = AmbienceBus, Stream = s != null ? Looping(s) : null, MaxDistance = maxDistance, Attenuation = 1.4f };
        AddChild(p);
        return p;
    }

    private static void Hold(AudioStreamPlayer2D p, Vector2? at, float db)
    {
        if (p.Stream == null) return;
        if (at == null) { if (p.Playing) p.Stop(); return; }
        p.GlobalPosition = at.Value;
        p.VolumeDb = db;
        if (!p.Playing) { p.Play((float)GD.RandRange(0.0, p.Stream.GetLength() * 0.9)); Trace($"loop on {p.Stream.ResourcePath} at {at.Value.X:0},{at.Value.Y:0}"); }
    }

    private readonly List<Hearth> _nearFires = new();
    private void UpdateFires(Vector2 here)
    {
        _nearFires.Clear();
        var hearths = Sim.Structures?.AllHearths;
        if (hearths != null)
            foreach (var h in hearths)
                if (IsInstanceValid(h) && h.IsBurning && h.GlobalPosition.DistanceSquaredTo(here) < 700f * 700f) _nearFires.Add(h);
        _nearFires.Sort((a, b) => a.GlobalPosition.DistanceSquaredTo(here).CompareTo(b.GlobalPosition.DistanceSquaredTo(here)));
        for (int i = 0; i < _fires.Count; i++)
            Hold(_fires[i], i < _nearFires.Count ? _nearFires[i].GlobalPosition : null, Mathf.LinearToDb(0.35f + 0.5f * (i < _nearFires.Count ? _nearFires[i].Intensity : 0f)) - 4f);
    }

    // Children at play round the nearest camp, by day and out of the worst
    // weather.
    private AudioStreamPlayer2D _kids;
    private void UpdateKids(GameCalendar cal, bool day)
    {
        Vector2? at = null;
        bool fit = day && cal.Hour >= 8f && cal.Hour < 20f && cal.Weather.Current is not (SkyState.Blizzard or SkyState.Storm or SkyState.Rain);
        if (fit)
        {
            float best = 1100f;
            foreach (var band in Sim.Bands)
            {
                if (band == null) continue;
                var camp = IsoGrid.ToWorld(band.Camp);
                float d = camp.DistanceTo(Listener);
                if (d < best) { best = d; at = camp; }
            }
        }
        Hold(_kids, at, -6f);
    }

    private int _blazeCount;
    private void UpdateBlaze()
    {
        var fire = Fire.I;
        if (fire == null || !IsInstanceValid(fire)) { Hold(_blaze, null, 0f); _blazeCount = 0; return; }
        var near = fire.NearestBurning(Listener, 1400f);
        Hold(_blaze, near != null ? IsoGrid.ToWorld(near.Value) : null, 4f);
        // A fire starting in the fort rings the bell.
        if (_blazeCount == 0 && fire.Count > 0 && Listener.DistanceTo(WorldLayout.FortCentre) < 3500f)
            At("fort_bell_alarm", IsoGrid.ToWorld(WorldLayout.FlagCell), 0f, 4500f, 0f, 1.5f);
        _blazeCount = fire.Count;
    }

    private Ship.State? _shipWas;
    private void UpdateShip()
    {
        var ship = Sim.Ship;
        if (ship == null || !IsInstanceValid(ship)) { Hold(_shipCreak, null, 0f); _shipWas = null; return; }
        var now = ship.Current;
        if (_shipWas != null && now != _shipWas)
        {
            if (now == Ship.State.Arriving)
            {
                // The ship's gun, the fort's answer, the bell.
                Play("cannon_distant", -2f, 0.03f);
                Play("cannon_distant", -4f, 0.03f, EffectsBus, 2.5f);
                At("fort_bell_work", IsoGrid.ToWorld(WorldLayout.FlagCell), 0f, 4500f, 0f, 5f);
            }
            else if (now == Ship.State.Departing)
                At("fort_bell_work", IsoGrid.ToWorld(WorldLayout.FlagCell), -2f, 4500f, 0f, 0.5f);
        }
        _shipWas = now;
        Hold(_shipCreak, ship.Visible && ship.Current != Ship.State.Away ? ship.GlobalPosition : null, -6f);
    }

    // ---- the calendar -------------------------------------------------------

    private GameCalendar _hookedCal;
    private void HookCalendar()
    {
        var cal = GameCalendar.Instance;
        if (cal == null || cal == _hookedCal) return;
        _hookedCal = cal;
        cal.HourPassed += OnHour;
        cal.SeasonChanged += OnSeason;
    }

    private void OnHour(int hour)
    {
        var cal = GameCalendar.Instance;
        if (Sim.Player == null || cal == null) return;
        // The bell to work and from it, at the post.
        bool foul = cal.Weather.Current is SkyState.Blizzard or SkyState.Storm;
        if ((hour == 7 || hour == 18) && !foul && Listener.DistanceTo(WorldLayout.FortCentre) < 3500f)
            At("fort_bell_work", IsoGrid.ToWorld(WorldLayout.FlagCell), -2f, 4500f, 0f);
    }

    private void OnSeason(int season)
    {
        if (Sim.Player == null) return;
        // The river ice booming as it sets fast, or as it goes out.
        var s = (Season)season;
        if (s != Season.Winter && s != Season.Breakup) return;
        Play("ice_crack", -6f, 0.08f);
        Play("ice_crack", -9f, 0.08f, EffectsBus, 7f);
        Play("ice_crack", -7f, 0.08f, EffectsBus, 16f);
    }

    // ---- what lives out there -----------------------------------------------

    private float _nextCritter, _nextHowl = 20f, _nextGrowl;
    private void Wolves(float dt, GameCalendar cal)
    {
        _nextHowl -= dt; _nextGrowl -= dt;
        if (_nextHowl > 0f && _nextGrowl > 0f) return;
        var ear = Listener;
        Agents.Animal nearest = null; float nearestD = float.MaxValue; int pack = 0;
        foreach (var a in Sim.Animals)
        {
            if (a == null || !IsInstanceValid(a) || a.IsDead || a.Species != Ecology.Species.Wolf) continue;
            float d = a.GlobalPosition.DistanceTo(ear);
            if (d < 3500f) pack++;
            if (d < nearestD) { nearestD = d; nearest = a; }
        }
        if (nearest == null) return;
        // Close by, a wolf that has you in mind growls.
        if (_nextGrowl <= 0f)
        {
            _nextGrowl = _rng.RandfRange(5f, 10f);
            if (nearestD < 420f) At("wolf_growl", nearest.GlobalPosition, -4f, 700f, 0.08f);
        }
        // At night, and more in winter, they sing.
        if (_nextHowl <= 0f)
        {
            bool night = cal.Hour >= 20f || cal.Hour < 6f;
            _nextHowl = _rng.RandfRange(35f, 90f) * (cal.CurrentSeason == Season.Winter ? 0.6f : 1f);
            if (night && nearestD < 3500f)
                At(pack >= 3 ? "wolf_pack_howl" : "wolf_howl", nearest.GlobalPosition, -2f, 4000f, 0.05f);
        }
    }

    private void Critters(float dt, GameCalendar cal, Agents.Agent me, bool indoors, bool day)
    {
        Wolves(dt, cal);
        _nextCritter -= dt;
        if (_nextCritter > 0f) return;
        _nextCritter = _rng.RandfRange(18f, 50f);
        if (indoors || cal.Weather.Current is SkyState.Blizzard or SkyState.Storm) return;
        var season = cal.CurrentSeason;
        float hour = cal.Hour;
        Vector2 Around(float min, float max) => Listener + Vector2.FromAngle(_rng.RandfRange(0f, Mathf.Tau)) * _rng.RandfRange(min, max);

        // Geese overhead, spring and autumn: a skein, several voices.
        if ((season == Season.Breakup || season == Season.FreezeUp) && day && _rng.Randf() < 0.6f)
        {
            var at = Around(400f, 900f);
            int n = _rng.RandiRange(2, 5);
            for (int i = 0; i < n; i++) At("goose_honk", at + new Vector2(_rng.RandfRange(-120f, 120f), _rng.RandfRange(-60f, 60f)), -8f - i, 1500f, 0.15f, i * _rng.RandfRange(0.3f, 0.9f));
            return;
        }
        // Ravens round the post all winter.
        if ((season == Season.Winter || season == Season.FreezeUp) && day && _rng.Randf() < 0.45f)
        {
            At("raven_croak", Around(300f, 800f), -10f, 1300f, 0.1f);
            return;
        }
        // A dog team coming in.
        if (season == Season.Winter && day && _rng.Randf() < 0.12f && Listener.DistanceTo(WorldLayout.FortCentre) < 2500f)
        {
            Play("dogsled_pass", -12f, 0.03f);
            return;
        }
        // Loons on the water, summer evenings and nights.
        if ((season == Season.OpenWater || (season == Season.Breakup && cal.Month == 6)) && (hour >= 19f || hour < 5f) && _waterNear > 0f && _rng.Randf() < 0.55f)
            Loon(Around(500f, 1100f));
    }

    // The loon file is a string of calls; play a stretch of it.
    private void Loon(Vector2 at)
    {
        var stream = Pick("loon_call");
        if (stream == null) return;
        foreach (var p in _pool)
        {
            if (p.Playing) continue;
            p.Stream = stream; p.GlobalPosition = at; p.MaxDistance = 1600f; p.VolumeDb = -8f; p.PitchScale = 1f;
            float len = (float)stream.GetLength();
            p.Play(_rng.RandfRange(0f, Mathf.Max(0f, len - 8f)));
            GetTree().CreateTimer(7.0).Timeout += () => { if (p.Stream == stream) p.Stop(); };
            return;
        }
    }

    // ---- music --------------------------------------------------------------

    private AudioStreamPlayer _music;
    private string _musicKind = "";     // "menu", "world", "reel"
    private float _musicGap;            // seconds of quiet before the next piece
    private float _musicFade = 1f;      // 1 = full, falls to 0 when we want it gone
    private bool _fadingOut;

    private void UpdateMusic(float dt)
    {
        string want = WantedMusic();
        if (want != _musicKind)
        {
            // Let the old piece fade before the new kind starts.
            if (_music.Playing && !_fadingOut) _fadingOut = true;
            if (!_music.Playing || _musicFade <= 0f)
            {
                _music.Stop(); _fadingOut = false; _musicFade = 1f;
                _musicKind = want;
                _musicGap = want == "world" ? _rng.RandfRange(20f, 45f) : 0.5f;
            }
        }
        if (_fadingOut) _musicFade = Mathf.MoveToward(_musicFade, 0f, dt / 3f);
        if (_music.Playing) _music.VolumeDb = Mathf.LinearToDb(Mathf.Max(_musicFade, 0.001f)) + (_musicKind == "reel" ? -6f : 0f);

        if (_music.Playing || _musicKind == "") return;
        _musicGap -= dt;
        if (_musicGap > 0f) return;
        string[] list = _musicKind switch
        {
            "menu" => MenuTracks,
            "reel" => ReelTracks,
            _ => SeasonTracks.TryGetValue(GameCalendar.Instance?.CurrentSeason ?? Season.Winter, out var l) ? l : MenuTracks,
        };
        var s = Pick(list[_rng.RandiRange(0, list.Length - 1)]);
        if (s == null) { _musicGap = 60f; return; }
        _musicFade = 1f; _fadingOut = false;
        _music.Stream = s;
        _music.VolumeDb = _musicKind == "reel" ? -6f : 0f;
        _music.Play();
        Trace($"music ({_musicKind}) {s.ResourcePath}");
    }

    private string WantedMusic()
    {
        var me = Sim.Player;
        // Between a death and the heir taking over there's no player either;
        // only the main menu gets the menu theme.
        if (me == null || !IsInstanceValid(me))
            return GetTree().CurrentScene?.SceneFilePath?.Contains("MainMenu") == true ? "menu" : _musicKind;
        var cal = GameCalendar.Instance;
        if (cal != null && (cal.Hour >= 19f && cal.Hour < 23.5f) && me.IsSheltered)
        {
            var men = WorldLayout.MensHouse;
            var middle = IsoGrid.ToWorld(IsoGrid.Cell(men.Top, men.Width / 2, men.Depth / 2));
            if (me.GlobalPosition.DistanceTo(middle) < 450f) return "reel";
        }
        return "world";
    }

    private void OnMusicFinished()
    {
        // The menu theme goes round; in the world, quiet comes between pieces.
        _musicGap = _musicKind switch { "menu" => 2f, "reel" => 4f, _ => _rng.RandfRange(120f, 240f) };
    }

    // ---- frame ---------------------------------------------------------------

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _stepBudget = Mathf.Min(2f, _stepBudget + dt * 4f);
        HookCalendar();
        ReadTheWorld(dt);
        UpdateBeds(dt);
        UpdateMusic(dt);
    }
}
