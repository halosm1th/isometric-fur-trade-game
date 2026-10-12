using System.Linq;
using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;
using NetHub = IsometricFurTradeGame.Scripts.Net.Net;

namespace IsometricFurTradeGame.Scripts.Player;

// A player is an Agent driven by a person instead of a goal stack. One
// PlayerController exists per player on every machine:
//  - the LOCAL one reads input and moves (the client owns its own feet);
//  - the SERVER copy runs needs, inventory and every world-changing action,
//    then mirrors state back to the owner;
//  - PUPPET copies on other machines just follow snapshots.
// In solo play all three are the same object.
public partial class PlayerController : Agent
{
	[Signal] public delegate void NoticeEventHandler(string text);
	[Signal] public delegate void RevivedEventHandler();

	private const float PickupReach = 100f;

	public int OwnerPeer = 1;
	public bool IsLocal => NetHub.I == null || OwnerPeer == NetHub.I.LocalPeerId;
	public bool IsServerCopy => NetHub.IsServer;

	private AnimatedSprite2D _sprite;
	private Camera2D _camera;
	private Label _label;
	private UI.DialogueMenu _dialogue;
	private float _busyUntil;           // eating/drinking: no walking for a moment (local)
	private string _oneShotAnim = "";   // server asks the owner to play this
	private bool _teleported;           // server moved us; the owner must snap
	private bool _remoteSneak, _remoteSprint;
	private Vector2 _netTarget; private bool _netHasTarget;

	public Npc Spouse;

	// Standing with the Company (design doc 3.6): slow, bureaucratic, reviewed by results.
	public float Standing = 10f;
	public readonly PlayerTask Task = new();
	public readonly HashSet<string> KnownSites = new();
	public readonly Dictionary<string, Vector2I> SiteBelief = new();
	// Things you've seen happen (keys into the Chronicle).
	public readonly HashSet<string> Known = new();
	public readonly List<string> Journal = new();
	public string Rank = "labourer";
	// For the year's account when the ship sails (design doc P.3).
	public float LastYearStanding = -1000f;
	public string LastYearRank = "";
	public bool StoodGuardOverTheQuota;
	// The country's tongue (design doc 10.6): 0 = none, 1 = fluent. Grows with talk and marriage.
	public float CreeFluency = 0.1f;
	public float AssiniboineFluency = 0.05f;
	public float FluencyWith(Agent who) => who != null && who.Faction == Faction.Assiniboine ? (HasInterpreterFor(who) ? 1f : AssiniboineFluency) : EffectiveFluency;
	public void Learn(Agent who, float amount) { if (who != null && who.Faction == Faction.Assiniboine) AssiniboineFluency = Mathf.Min(1f, AssiniboineFluency + amount); else CreeFluency = Mathf.Min(1f, CreeFluency + amount); }
	// A woman of the country speaks for you with her own people; a Cree wife knows enough Nakoda for trade.
	public bool HasInterpreterFor(Agent who)
	{
		if (who == null || who.Faction != Faction.Assiniboine) return HasInterpreter;
		if (Spouse != null && IsInstanceValid(Spouse) && !Spouse.IsDead && GlobalPosition.DistanceTo(Spouse.GlobalPosition) < 300f) return true;
		foreach (var a in Sim.AgentsWithin(GlobalPosition, 300f)) if (a is Npc n && n.Faction == Faction.Assiniboine && n.Role == Role.Wife) return true;
		return false;
	}
	// You told the Factor you'd be across the river (design doc 11.5): sightings till then file as authorized.
	public int AuthorizedAcrossUntilDay = -1;
	private int _lastSeenAcrossDay = -1;
	public bool HasInterpreter
	{
		get
		{
			if (Spouse != null && IsInstanceValid(Spouse) && !Spouse.IsDead && GlobalPosition.DistanceTo(Spouse.GlobalPosition) < 300f) return true;
			foreach (var a in Sim.AgentsWithin(GlobalPosition, 300f)) if (a is Npc n && (n.Role == Role.Wife || n.Role == Role.Woman) && n.Faction == Faction.HBC) return true;
			return false;
		}
	}
	public float EffectiveFluency => HasInterpreter ? 1f : CreeFluency;
	// Time-skip agreement: how many hours this player wants to skip (0 = none).
	public float WantsSkipHours;
	// Server-side conversation state with whoever you're talking to.
	public UI.Conversation Conversation;

	// Which company's post you belong to.
	// Where you're posted (design doc 3.9 step 4): the Bay unless the Factor sends you inland.
	public bool PostedInland;
	// Captivity (design doc 9.1): the other company's men take a beaten
	// man rather than kill him - his goods, and a few days of his time.
	public Economy.FortLedger CaptiveOf;
	public int CaptiveUntilDay = -1;
	// A word given to the other company (design doc 9.1): no arms against
	// them till it runs out. Breaking it is witnessed and remembered.
	public int ParoleUntilDay = -1;
	public Faction ParoleTo;
	public bool ParoleConfessed;
	public bool OnParoleAgainst(Agent other) => other != null && ParoleUntilDay > (GameCalendar.Instance?.Day ?? 0) && other.Faction == ParoleTo;
	// The marriage's obligations (design doc 8.4): her father expects to be
	// remembered each spring, and a wife left too long goes home.
	public int LastSawWifeDay = -1;
	// What you have counted with your own eyes in the post's stores
	// (design doc P.3: the HUD shows your belief, not the true ledger).
	public float BelievedFursMb;
	public float BelievedQuotaMb = 60f;
	public int LastCountedDay = -99;
	// What the player is doing with the year (design doc P.2), and the
	// history a leader weighs when the player asks him for something.
	public readonly List<Ambition> Ambitions = new();
	public int TasksDone, TasksRefused;
	public float PrivateWorthMb;   // furs and goods kept out of the Company's books
	public bool AmbitionsDeclared;
	// Who you came in as (design doc 5.4, Core/CharacterSheet): the road in,
	// the background on it, and the people you count as family.
	public Core.StartPath StartPath = Core.StartPath.HBC;
	public string BackgroundTitle = "";
	public readonly List<string> KinNames = new();
	public string HomeCamp = "";
	// Which sheet the player is drawn from (design doc 5.4): set by the
	// character's road in, carried to every peer with the player.
	public string FramesPath = "";
	public void ApplyLook()
	{
		if (_sprite == null || string.IsNullOrEmpty(FramesPath) || !ResourceLoader.Exists(FramesPath)) return;
		_sprite.SpriteFrames = GD.Load<SpriteFrames>(FramesPath);
		_sprite.Play(_sprite.SpriteFrames.HasAnimation("idle") ? "idle" : _sprite.Animation);
	}   // born to one of the country's band camps (World/Camps), if any
	public string LastYearStory = "";   // Core/YearStory: the last year as it will be told

	// Set down somewhere by the host (a new character's start, Core/Starts):
	// the owner's copy is told to jump there rather than glide.
	public void SetStartPosition(Vector2 at) { GlobalPosition = at; _netTarget = at; _teleported = true; }
	// Those of your house who went before (design doc P.6): the family is
	// the save file, and the journal keeps the line.
	public readonly List<Forebear> House = new();
	public int MarriedOnDay = -1;
	public int LastGiftToKinYear = 0;
	public bool TravellingServer;   // the owner is on the road: the clock marches
	public Economy.FortLedger HomePost => IsBandPerson ? null : Faction == Faction.NWC ? Sim.Nwc : PostedInland && Sim.Outpost != null ? Sim.Outpost : Sim.Fort;   // born to a band: no company post
	public bool WantsToDefect;

	// Afloat: which canoe carries you (server), mirrored as a flag.
	public Canoe Aboard;
	public bool IsAfloat => Aboard != null || _afloatMirror;
	private bool _afloatMirror;
	public void SetAfloatMirror(bool v) => _afloatMirror = v;
	private Sprite2D _canoeSprite;

	private void UpdateCanoeVisual()
	{
		bool afloat = IsAfloat;
		if (_canoeSprite == null)
		{
			_canoeSprite = new Sprite2D { Texture = GD.Load<Texture2D>("res://assets/sprites/canoe.png"), Offset = new Vector2(0, -8), ZIndex = -1, Visible = false };
			AddChild(_canoeSprite);
		}
		_canoeSprite.Visible = afloat && !IsDead;
		CollisionMask = afloat ? 1u : 5u;
	}

	// Paddling: you can be over water or ice; the moment you're on a land
	// cell you've stepped out and the canoe stays at the water's edge.
	private void ServerCanoeTick()
	{
		if (Aboard == null) return;
		var t = Sim.Terrain;
		if (t == null) return;
		var cell = Cell;
		if (!t.IsWaterOrIce(cell) && Sim.Paths != null && Sim.Paths.IsWalkable(cell))
		{
			// Landed. Leave the canoe on the nearest water cell behind us.
			Vector2I water = cell;
			foreach (var n in IsoGrid.EdgeNeighbours(cell)) if (t.IsWaterOrIce(n)) { water = n; break; }
			Aboard.SetState(IsoGrid.ToWorld(water), 0);
			Aboard = null;
			Say("You step ashore and draw the canoe up.");
		}
		else Aboard.Position = GlobalPosition;
	}

	// Change companies (design doc 1). The people you leave remember it.
	public void ChangeSides(Faction to, Npc witness)
	{
		var from = Faction;
		Faction = to;
		WantsToDefect = false;
		Standing = 10f;
		Task.Kind = TaskKind.None;
		foreach (var n in Sim.Npcs)
		{
			if (n.Faction == from && (from == Faction.HBC || from == Faction.NWC)) n.ChangeRapport(this, -25f);
			if (n.Faction == to) n.ChangeRapport(this, 5f);
		}
		Chronicle.Event($"{DisplayName} left the {(from == Faction.HBC ? "Hudson's Bay Company" : "North West Company")} for the {(to == Faction.HBC ? "Hudson's Bay Company" : "North West Company")}.", GlobalPosition, this, weight: 2f);
		Sim.Notice($"{DisplayName} has gone over to the {(to == Faction.HBC ? "Hudson's Bay Company" : "Nor'Westers")}.");
	}

	// Mirrored from the server for the owner's HUD.
	public string TaskText = "";
	// The last thing a friend took you aside to tell you (design doc P.8), and
	// the "what matters now" lines the HUD shows (P.4), mirrored to the owner.
	public string LastWarning = "";
	public int LastWarningDay = -999;
	public string FocusText = "";
	public string SkipStatus = "";

	public override string CurrentAnimation => _sprite?.Animation ?? "idle";
	public override bool FlipH => _sprite?.FlipH ?? false;

	// ---- setup ---------------------------------------------------------------

	public override void _Ready()
	{
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		ApplyLook();
		_camera = GetNodeOrNull<Camera2D>("Camera2D");
		_label = GetNodeOrNull<Label>("NameLabel");
		Faction = Faction.HBC;
		Conversation = new UI.Conversation(this);
		if (_camera != null) _camera.Enabled = false;
		if (_label != null) _label.Text = DisplayName;
		_sprite.AnimationFinished += () => { string a = _sprite.Animation; if (a == "eat" || a == "drink") _busyUntil = 0f; };

		if (IsServerCopy) GiveStartingKit();
		if (IsLocal) BecomeLocal();
	}

	private void GiveStartingKit()
	{
		// What a new hand stepping off the ship would carry.
		Inventory.Clear();
		Inventory.Add(ItemId.Pemmican, 4);
		Inventory.Add(ItemId.DriedGoose, 2);
		Inventory.Add(ItemId.Firewood, 4);
		Inventory.Add(ItemId.Capote, 1);
		Inventory.Add(ItemId.Knife, 1);
		Inventory.Add(ItemId.TradeGoods, 6);
		Inventory.Add(ItemId.SteelTrap, 3);
		Inventory.Add(ItemId.Kettle, 1);
		Inventory.Add(ItemId.Water, 2);
		Inventory.Add(ItemId.Flint, 1);
		Inventory.Add(ItemId.Tumpline, 1);   // a labourer's pack strap: 40 lb more before you slow
		Inventory.Add(ItemId.Axe, 1);
		Inventory.Add(ItemId.FishingLine, 1);
	}

	// Travel mode (design doc 18.3): on the river road the world goes by at
	// a march, the clock with it. Any step of your own ends it.
	private List<Vector2> _travel;
	private int _travelLeg;
	public bool Travelling => _travel != null;
	private float _travelSkipTimer;

	private bool NearRoadHead(out bool atYork)
	{
		atYork = GlobalPosition.DistanceTo(IsoGrid.ToWorld(WorldLayout.RoadHeadYork)) < 200f;
		bool atOutpost = GlobalPosition.DistanceTo(IsoGrid.ToWorld(WorldLayout.RoadHeadOutpost)) < 200f;
		return atYork || atOutpost;
	}

	private void StartTravel(bool south)
	{
		var road = WorldLayout.RiverRoad;
		_travel = new List<Vector2>();
		if (south) for (int i = 1; i < road.Length; i++) _travel.Add(road[i]); else for (int i = road.Length - 2; i >= 0; i--) _travel.Add(road[i]);
		_travelLeg = 0;
		NetHub.I?.Act("travel", new Godot.Collections.Array { true });
		EmitSignal(SignalName.Notice, south ? "You take the river road inland. A long day's march; any step of your own stops it." : "You take the river road down to the Bay.");
	}

	private void StopTravel(string why)
	{
		if (_travel == null) return;
		_travel = null;
		NetHub.I?.Act("travel", new Godot.Collections.Array { false });
		if (why != "") EmitSignal(SignalName.Notice, why);
	}

	private bool _becameLocal;
	private static bool _devClick;
	public void BecomeLocal()
	{
		if (_becameLocal) return;
		_becameLocal = true;
		IsPuppet = false;
		if (_camera != null) { _camera.Enabled = true; _camera.MakeCurrent(); }
		if (_label != null) _label.Visible = false;
		CallDeferred(nameof(FindDialogue));
		GetTree().CreateTimer(1.5).Timeout += () =>
		{
			if (!IsInstanceValid(this)) return;
			EmitSignal(SignalName.Notice, $"{Core.Starts.Intro(this)} {Core.Keybinds.KeyFor("interact")} talks, {Core.Keybinds.HotbarKeys()} uses, left click strikes, {Core.Keybinds.KeyFor("chat")} chats, {Core.Keybinds.KeyFor("debug_menu")} opens the test console.");
			if (!AmbitionsDeclared) EmitSignal(SignalName.Notice, "A man wants something of a country like this. Esc - What you mean to do.");
		};
		// Dev aids: `--spawn=depot` moves you there (server permitting), `--provoke`
		// knifes the nearest person after 3 s, `--talk` opens a conversation after 4 s.
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("--spawn=") && NetHub.IsServer) { string spot = arg.Substring(8); GetTree().CreateTimer(1.0).Timeout += () => { GlobalPosition = IsoGrid.ToWorld(TerrainPainter.NamedSpot(spot)); _netTarget = GlobalPosition; _teleported = true; GD.Print($"[dev] host moved to {spot} {Cell}"); }; }
			if (arg.StartsWith("--spawn=") && !NetHub.IsServer) { string spot = arg.Substring(8); GetTree().CreateTimer(1.0).Timeout += () => NetHub.I?.Act("dev_teleport", new Godot.Collections.Array { spot }); }
			if (arg == "--provoke") GetTree().CreateTimer(3.0).Timeout += () => { var n = NearestNpc(400f); if (n != null) NetHub.I?.Act("attack", new Godot.Collections.Array { n.NetId }); };
			// Dev: `--provoke=8` keeps swinging, every two seconds, so a fight runs its course.
			if (arg.StartsWith("--provoke=") && int.TryParse(arg.Substring(10), out int blows)) for (int i = 0; i < blows; i++) GetTree().CreateTimer(3.0 + i * 2.0).Timeout += () => { var n = NearestNpc(400f); if (n != null) NetHub.I?.Act("attack", new Godot.Collections.Array { n.NetId }); };
			// Dev: `--talkto=Name` stands you beside that person and opens the talk.
			if (arg.StartsWith("--talkto=") && NetHub.IsServer) { string who = arg.Substring(9); GetTree().CreateTimer(7.0).Timeout += () => { foreach (var n in Sim.Npcs) if (!n.IsDead && n.DisplayName.Contains(who)) { GlobalPosition = n.GlobalPosition + new Vector2(40, 0); _netTarget = GlobalPosition; _teleported = true; GD.Print($"[dev] stood beside {n.DisplayName}"); var st = Conversation.Start(n); NetHub.I?.PushDialogue(this, st); break; } }; }
			// Dev: `--kit=BeaverPelt:4,TradeGoods:30` puts those in your pack.
			if (arg.StartsWith("--kit=") && NetHub.IsServer) { foreach (string part in arg.Substring(6).Split(',')) { var bits = part.Split(':'); if (!System.Enum.TryParse<ItemId>(bits[0], true, out var kid)) continue; int howMany = bits.Length > 1 && int.TryParse(bits[1], out int c) ? c : 1; Inventory.Add(kid, howMany); GD.Print($"[dev] {howMany} x {Items.Items.Of(kid).Name} in your pack"); } }
			// Dev: `--dumpbook=S` prints your journal to the console at that second.
			if (arg.StartsWith("--dumpbook=") && double.TryParse(arg.Substring(11), out double bsecs) && NetHub.IsServer) GetTree().CreateTimer(bsecs).Timeout += () => { GD.Print("[book] " + DisplayName + "'s journal:"); foreach (string line in Journal) GD.Print("[book] " + line); };
			// Dev: `--quitafter=S` closes the game cleanly after S seconds, so a
			// test run ends without the runtime being killed mid-print.
			if (arg.StartsWith("--quitafter=") && double.TryParse(arg.Substring(12), out double qsecs)) GetTree().CreateTimer(qsecs).Timeout += () => { GD.Print("[dev] quitting after " + qsecs + "s"); GetTree().Quit(); };
			// Dev: `--ambition=RiseAtThePost` declares one at the start, so the
			// systems that hang off a declared ambition can be watched.
			if (arg.StartsWith("--ambition=") && NetHub.IsServer) { if (System.Enum.TryParse<Agents.AmbitionKind>(arg.Substring(11), true, out var ak)) { var cal = GameCalendar.Instance; Ambitions.Add(new Agents.Ambition { Kind = ak, DeclaredDay = cal?.Day ?? 0, DeclaredYear = cal?.Year ?? 1811 }); AmbitionsDeclared = true; GD.Print($"[dev] you have set yourself {ak}"); } }
			// Dev: `--skip=12` skips twelve game hours a few seconds in, to measure
			// what a night of sleeping or travelling costs a frame.
			if (arg.StartsWith("--skip=") && float.TryParse(arg.Substring(7), out float skipH) && NetHub.IsServer) GetTree().CreateTimer(6.0).Timeout += () => { GD.Print($"[dev] skipping {skipH} h"); GameCalendar.Instance?.SkipHours(skipH); };
			// Dev: `--standing=60` sets your standing with the Company at the start.
			if (arg.StartsWith("--standing=") && float.TryParse(arg.Substring(11), out float st0) && NetHub.IsServer) { Standing = st0; GD.Print($"[dev] standing {st0:0}"); }
			// Dev: `--census=8` prints what this machine can see of the world after
			// eight seconds - the count of bodies, the local man, his mirrored needs
			// and what the snapshots have given him. The way to check a client is
			// actually being told about the world (design doc 21).
			if (arg.StartsWith("--census=") && double.TryParse(arg.Substring(9), out double csecs)) GetTree().CreateTimer(csecs).Timeout += () =>
			{
				int live = 0, hidden = 0;
				foreach (var n in Sim.Npcs) { if (n == null) continue; if (n.Visible) live++; else hidden++; }
				GD.Print($"[census] {(NetHub.IsServer ? "server" : "client")} agents={Sim.Agents.Count} npcs={Sim.Npcs.Count} (visible {live}, hidden {hidden}) animals={Sim.Animals.Count} players={PlayerCount()}");
				GD.Print($"[census] ground: water={Sim.Terrain?.IsWaterOrIce(Cell)} walkable={Sim.Paths?.IsWalkable(Cell)} map={World.WorldMap.At(Cell)}");
				GD.Print($"[census] me={DisplayName} rank={Rank} at {Cell} h{Needs.Health:0} f{Needs.Hunger:0} w{Needs.Warmth:0} t{Needs.Thirst:0} standing={Standing:0} task=\"{(Task.Kind == TaskKind.None ? "none" : Task.Describe())}\" inv={Inventory.SlotCount} journal={Journal.Count}");
				foreach (var o in Sim.Players()) if (o != this) GD.Print($"[census] also here: {o.DisplayName} at {o.Cell} h{o.Needs.Health:0}");
			};
			if (arg == "--talk") GetTree().CreateTimer(4.0).Timeout += () => NetHub.I?.Act("interact");
			if (arg.StartsWith("--use=") && int.TryParse(arg.Substring(6), out int slot)) GetTree().CreateTimer(3.0).Timeout += () => NetHub.I?.Act("use", new Godot.Collections.Array { slot });
			if (arg.StartsWith("--click=")) { _devClick = true; double when = 3.0; foreach (var part in arg.Substring(8).Split(',')) { if (!int.TryParse(part, out int cslot)) continue; GetTree().CreateTimer(when).Timeout += () => { var p = GlobalPosition + new Vector2(90, 40); NetHub.I?.Act("click", new Godot.Collections.Array { cslot, p.X, p.Y }); }; when += 3.0; } }
			if (arg == "--travel") GetTree().CreateTimer(3.0).Timeout += () => StartTravel(true);
			if (arg == "--kill" && NetHub.IsServer) GetTree().CreateTimer(3.0).Timeout += () => { var n = NearestNpc(500f); if (n != null) { n.TakeDamage(999f, this); GD.Print($"[dev] killed {n.DisplayName}"); } };
			if (arg == "--follow" && NetHub.IsServer) GetTree().CreateTimer(3.0).Timeout += () => { var n = NearestNpc(600f); if (n != null) { n.Brain.Clear(n); n.Brain.Push(n, new Agents.Goals.FollowGoal(this, 10f)); GD.Print($"[dev] {n.DisplayName} follows"); GetTree().CreateTimer(8.0).Timeout += () => { GlobalPosition = IsoGrid.ToWorld(WorldLayout.GateOutside) + new Vector2(0, 600); GD.Print("[dev] player moved outside the gate"); }; } };
			if (arg == "--scurvy" && NetHub.IsServer) GetTree().CreateTimer(2.0).Timeout += () => { foreach (var n in Sim.Npcs) if (n.Faction == Faction.HBC) n.DaysSinceFresh = 41.5f; DaysSinceFresh = 41.5f; };
			if (arg.StartsWith("--autoload=") && double.TryParse(arg.Substring(11), out double lsecs) && NetHub.IsServer) GetTree().CreateTimer(lsecs).Timeout += () => { if (!SaveGame.BeginLoad(GetTree())) GD.Print("[load] " + SaveGame.LastMessage); };
			if (arg.StartsWith("--autosave=") && double.TryParse(arg.Substring(11), out double secs)) GetTree().CreateTimer(secs).Timeout += () => { SaveGame.Save(); GD.Print("[save] " + SaveGame.LastMessage); };
			// Dev: die at a given second, so succession can be watched end to end.
			if (arg.StartsWith("--killme=") && double.TryParse(arg.Substring(9), out double kms) && NetHub.IsServer) GetTree().CreateTimer(kms).Timeout += () => { TakeDamage(9999f, NearestNpc(900f)); GD.Print("[dev] the player is killed"); GetTree().CreateTimer(4.0).Timeout += () => { GD.Print("[dev] " + (LookAtInheritance() is var ih ? $"heir would be {ih.Name} ({ih.Relation}), burdens {ih.Burdens.Count}" : "")); NetHub.I?.Act("respawn", new Godot.Collections.Array()); }; };
			if (arg == "--leave") GetTree().CreateTimer(6.0).Timeout += () => { var d = UI.DialogueMenu.Instance; if (d != null && d.IsOpen) NetHub.I?.DialogueRequest(d.CurrentNpc, d.OptionCount - 1); };
		}
	}

	private void FindDialogue() => _dialogue = UI.DialogueMenu.Instance;

	// The Factor's morning orders for the new hand (server side).
	public void AssignTask(TaskKind kind, int need, string intent = "")
	{
		var cal = GameCalendar.Instance;
		if (Task.Kind != TaskKind.None && !Task.IsComplete && Task.DayAssigned != cal.Day) { Standing -= 2f; Say("Yesterday's task went undone. Mr. Auld noticed."); }
		if (Task.Kind != TaskKind.None && Task.IsComplete) { Standing += 5f; TasksDone++; }
		Task.Kind = kind; Task.Need = need; Task.Done = 0; Task.DayAssigned = cal.Day; Task.Intent = intent ?? ""; Task.Deadline = cal.Day + 1; Task.Accepted = false; Task.OwnWay = false;
		if (kind != TaskKind.None) Say(Task.Describe());
	}

	// A line for this player's owner, wherever they are.
	public override void TakeDamage(float amount, Agent from)
	{
		// A company man's blow that would kill you takes you prisoner instead (design doc 9.1).
		if (!IsDead && !Invulnerable && from is Npc captor && Economy.Rivalry.AreRivals(captor.Faction, Faction) && Needs.Health - amount <= 0f && CaptiveOf == null)
		{
			Needs.Health = 8f;
			Capture(Economy.Rivalry.PostOf(captor.Faction), captor);
			return;
		}
		base.TakeDamage(amount, from);
	}

	private void Capture(Economy.FortLedger post, Npc captor)
	{
		if (post == null) { Needs.Health = 0f; OnDeath(); return; }
		CaptiveOf = post; CaptiveUntilDay = GameCalendar.Instance.Day + 3;
		// Everything worth having goes into their stores; they leave you your coat and your knife.
		int taken = 0;
		foreach (var (id, n) in new List<(ItemId, int)>(Inventory.All()))
		{
			var k = Items.Items.Of(id).Kind;
			if (k == ItemKind.Clothing || id == ItemId.Knife || k == ItemKind.Food && n <= 2) continue;
			Inventory.Remove(id, n); post.Stores.Add(id, n); taken += n;
		}
		foreach (var m in new List<Message>(Mail)) { Mail.Remove(m); if (post.Leader != null && !post.Leader.IsDead) { post.Leader.Mail.Add(m); Core.Mail.Open(m, post.Leader); } }
		StopTravel(""); Raiding = false; RaidTimer = 0f;
		GlobalPosition = IsoGrid.ToWorld(post.AccessCell) + new Vector2(0, 60);
		ForgetAttacker();
		Economy.Rivalry.Raise(5f, $"{DisplayName} taken prisoner");
		Say($"You come to on the floor of {post.Title}, {captor.DisplayName} standing over you. They have taken {taken} lots off you. You are held three days - walk out of their yard and you're an escaped man; wait, and they'll turn you loose.");
		Chronicle.Event($"{captor.DisplayName} took {DisplayName} prisoner.", GlobalPosition, captor, DisplayName, 2f);
		Sim.Notice($"{DisplayName} has been taken by the {(post.Company == Faction.NWC ? "Nor'Westers" : "Company")}.");
	}

	private int _captiveDay = -1;
	private void CaptiveTick()
	{
		if (CaptiveOf == null) return;
		var cal = GameCalendar.Instance; if (cal == null) return;
		bool inYard = CaptiveOf.Company == Faction.NWC ? WorldLayout.NwcYardCells().Contains(Cell) : WorldLayout.YardCells().Contains(Cell);
		if (!inYard && GlobalPosition.DistanceTo(IsoGrid.ToWorld(CaptiveOf.AccessCell)) > 700f)
		{
			// Escaped (design doc 9.1): the story travels, and they remember.
			CaptiveOf = null; Standing += 2f;
			Economy.Rivalry.Raise(4f, $"{DisplayName} escaped");
			Chronicle.Event($"{DisplayName} escaped from the other company's post.", GlobalPosition, this, weight: 1.5f);
			Say("You are out of their yard and away. An escaped prisoner; they will not forget it.");
			return;
		}
		if (cal.Day != _captiveDay)
		{
			_captiveDay = cal.Day;
			if (cal.Day >= CaptiveUntilDay)
			{
				var post = CaptiveOf; CaptiveOf = null;
				Inventory.Add(ItemId.Pemmican, 2);
				Say($"They turn you loose with two days' pemmican and a warning. \"Go home. Tell your Factor what it costs to cross us.\"");
				Chronicle.Event($"{DisplayName} was let go by the {(post.Company == Faction.NWC ? "Nor'Westers" : "Company")}.", GlobalPosition, this, weight: 1f);
			}
		}
	}

	public void Say(string text) => NetHub.I?.NoticeTo(this, text);

	private int[] UnwornList() { var a = new int[Inventory.Unworn.Count]; int i = 0; foreach (var id in Inventory.Unworn) a[i++] = (int)id; return a; }

	private string[] MailSummary()
	{
		var a = new string[Mail.Count];
		for (int i = 0; i < Mail.Count; i++) a[i] = $"{Mail[i].Kind}|{Mail[i].From}|{Mail[i].To}|{Mail[i].Subject}|{(Mail[i].Read ? 1 : 0)}|{Mail[i].Text}";
		return a;
	}

	// ---- input (local only) -------------------------------------------------------

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!IsLocal) return;
		if (IsDead)
		{
			if (@event.IsActionPressed("restart")) NetHub.I?.Act("respawn");
			return;
		}
		if (_dialogue != null && _dialogue.IsOpen) return;
		if (UI.ChatBox.Instance != null && UI.ChatBox.Instance.IsTyping) return;
		if (UI.PauseMenu.IsOpen || UI.MapPanel.IsOpen || UI.JournalPanel.IsOpen) return;
		// The clock: Space stops it, [ and ] slow and quicken it (host).
		if (@event.IsActionPressed("pause_clock")) { NetHub.I?.TogglePause(); GetViewport().SetInputAsHandled(); return; }
		if (@event.IsActionPressed("clock_slower")) { NetHub.I?.ChangeSpeed(2f); return; }
		if (@event.IsActionPressed("clock_faster")) { NetHub.I?.ChangeSpeed(0.5f); return; }
		// Quick save / load: the host's privilege.
		if (@event is InputEventKey { Pressed: true, Echo: false } key && NetHub.IsServer)
		{
			if (key.Keycode == Key.F5) { SaveGame.Save(); Say(SaveGame.LastMessage); return; }
			if (key.Keycode == Key.F9) { if (!SaveGame.BeginLoad(GetTree())) Say(SaveGame.LastMessage); return; }
		}

		if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
		{
			// Left click (design doc 3.3): strike with a readied weapon, use the
			// item in hand on the ground, or just interact.
			var target = AgentUnderCursor();
			int slot = UI.Hotbar.SelectedSlot;
			var held = Inventory.Slot(slot);
			bool weapon = held != null && Items.Items.Of(held.Value.id).Kind == ItemKind.Weapon;
			if (target != null && (weapon || held == null || target is Animal && Items.Items.Of(held.Value.id).Kind != ItemKind.Food)) { NetHub.I?.Act("attack", new Godot.Collections.Array { target.NetId }); return; }
			if (target != null && target is Npc) { NetHub.I?.Act("interact"); return; }
			var mouse = GetGlobalMousePosition();
			NetHub.I?.Act("click", new Godot.Collections.Array { slot, mouse.X, mouse.Y });
			return;
		}
		if (@event.IsActionPressed("attack_nearest")) { NetHub.I?.Act("attack_nearest"); return; }
		if (@event.IsActionPressed("interact"))
		{
			if (_travel != null) { StopTravel("You stop."); return; }
			if (NearRoadHead(out bool atYork) && NearestNpc(110f) == null) { StartTravel(atYork); return; }
			NetHub.I?.Act("interact"); return;
		}
		if (@event.IsActionPressed("drink")) { NetHub.I?.Act("drink"); return; }
		if (@event.IsActionPressed("wait")) { NetHub.I?.Act("wait"); return; }
	}

	// Hotbar: the owner asks the server to use slot i.
	public void UseSlot(int i) => NetHub.I?.Act("use", new Godot.Collections.Array { i });

	private Agent AgentUnderCursor()
	{
		var mouse = GetGlobalMousePosition();
		Agent best = null; float bestD = 70f;
		foreach (var a in Sim.AgentsWithin(mouse, 120f))
		{
			if (a == this) continue;
			float d = (a.GlobalPosition + new Vector2(0, -40)).DistanceTo(mouse);
			if (d < bestD) { bestD = d; best = a; }
		}
		return best;
	}

	// ---- movement ------------------------------------------------------------------

	// Standing in the storehouse, you can see what is in it.
	// At your own post, the mess, the barrel and the hearth take care of
	// the ordinary needs (design doc P.5): no clicking. When the stores are
	// empty they stop, and that is the crisis the player should feel.
	private float _messTimer;
	private void PostKeep(float delta)
	{
		var fort = HomePost;
		if (fort == null || IsDead || CaptiveOf != null) return;
		bool home = GlobalPosition.DistanceTo(IsoGrid.ToWorld(fort.AccessCell)) < 900f;
		if (!home) return;
		_messTimer -= delta;
		if (_messTimer > 0f) return;
		_messTimer = 2f;
		// Mess call: the plainest thing on the shelf, while there is any.
		if (Needs.Hunger < 70f)
		{
			foreach (var id in new[] { ItemId.Berries, ItemId.Hare, ItemId.Ptarmigan, ItemId.Fish, ItemId.Venison, ItemId.Goose, ItemId.DriedMeat, ItemId.DriedGoose, ItemId.Corn, ItemId.Bread, ItemId.Pemmican })
			{
				if (!fort.Stores.Remove(id)) continue;
				Needs.Hunger = Mathf.Min(100f, Needs.Hunger + Items.Items.Of(id).Nutrition);
				if (id == ItemId.Venison || id == ItemId.Goose || id == ItemId.Fish || id == ItemId.Hare) DaysSinceFresh = 0f;
				break;
			}
		}
		// The water barrel.
		if (Needs.Thirst < 70f && (fort.Stores.Remove(ItemId.Water) || !GameCalendar.Instance.IsFrozen)) Needs.Thirst = Mathf.Min(100f, Needs.Thirst + 45f);
	}

	// What you have of your own (design doc P.2's private trade): furs and
	// goods on your back, plus whatever you have put by in a canoe.
	private void CountPrivate()
	{
		float mb = 0f;
		foreach (var (id, n) in Inventory.All())
		{
			var k = Items.Items.Of(id).Kind;
			if (k == ItemKind.Fur || k == ItemKind.Goods) mb += Items.Items.Of(id).ValueMb * n;
		}
		if (Sim.Spawner != null)
			foreach (var c in Sim.Spawner.Canoes)
				foreach (var (id, n) in c.Cargo.All())
				{
					var k = Items.Items.Of(id).Kind;
					if (k == ItemKind.Fur || k == ItemKind.Goods) mb += Items.Items.Of(id).ValueMb * n * 0.5f;   // a cache you must still get home
				}
		PrivateWorthMb = Mathf.Max(PrivateWorthMb * 0.995f, mb);   // the high-water mark, decaying slowly if you spend it
	}

	private void CountStores()
	{
		var fort = HomePost;
		if (fort == null || IsDead) return;
		bool inStore = fort == Sim.Nwc ? WorldLayout.NwcHouse.Contains(Cell) : fort == Sim.Outpost ? WorldLayout.OutpostHouse.Contains(Cell) : WorldLayout.Warehouse.Contains(Cell);
		if (!inStore && GlobalPosition.DistanceTo(IsoGrid.ToWorld(fort.AccessCell)) > 160f) return;
		BelievedFursMb = fort.FursInStoreMb;
		BelievedQuotaMb = fort.QuotaMb;
		LastCountedDay = GameCalendar.Instance?.Day ?? 0;
	}

	private int _marriageDay = -1;
	private void MarriageTick()
	{
		var cal = GameCalendar.Instance;
		if (cal == null || cal.Day == _marriageDay || Spouse is not Npc wife) return;
		_marriageDay = cal.Day;
		if (!IsInstanceValid(wife) || wife.IsDead) { Spouse = null; return; }
		if (GlobalPosition.DistanceTo(wife.GlobalPosition) < 900f) LastSawWifeDay = cal.Day;
		int apart = LastSawWifeDay < 0 ? 0 : cal.Day - LastSawWifeDay;
		if (apart == 25) Say($"{wife.DisplayName} has not seen you in {apart} days. Her people are asking where her husband is.");
		if (apart >= 45)
		{
			// Left behind (design doc 8.4): she goes back to her people, and they remember it.
			var band = wife.Father?.HomeStores as Economy.BandLedger ?? Sim.Band;
			wife.Spouse = null; Spouse = null;
			wife.Role = Role.CreeDaughter; wife.HomeStores = band; wife.ResetBehaviour(); wife.Brain.Clear(wife);
			if (band != null) { wife.BedCell = new List<Vector2I>(band.Tipis())[0]; wife.WorkCell = IsoGrid.Cell(band.Camp, 1, 2); band.StandingWithCompany = Mathf.Max(0f, band.StandingWithCompany - 15f); }
			foreach (var n in Sim.Npcs) if (n.HomeStores == band) n.ChangeRapport(this, -20f);
			Core.Reveal.Lands(this, $"{wife.DisplayName} has gone back to her people.", "", 1.6f);
			Say($"A husband who is never there is no husband; her family will not forget it. ({apart} days since you last saw her.)");
			Chronicle.Event($"{wife.DisplayName} left {DisplayName} and went home to her people.", wife.GlobalPosition, wife, DisplayName, 2f);
		}
		// Spring: her father looks for his due.
		if (cal.Month == 6 && cal.Day == 6 && LastGiftToKinYear < cal.Year && wife.Father != null && !wife.Father.IsDead)
		{
			LastGiftToKinYear = cal.Year;
			if (wife.Father.GiftsFromPlayerMb >= 3f * (cal.Year - 1810)) { wife.Father.ChangeRapport(this, 5f); Say($"{wife.Father.DisplayName} speaks well of his son-in-law this spring."); }
			else { wife.Father.ChangeRapport(this, -12f); (wife.Father.HomeStores as Economy.BandLedger)?.OnInsult(); Say($"{wife.Father.DisplayName} says his son-in-law forgets what is owed a wife's family. A gift of a few pieces each spring is expected."); }
		}
	}

	// Being seen where you shouldn't be (design doc 11.5): a Company man
	// inside the Nor'Westers' palisade is a story, unless he said he'd go.
	private void SeenAcross()
	{
		var cal = GameCalendar.Instance;
		if (cal == null || cal.Day == _lastSeenAcrossDay || IsDead) return;
		bool inRival = Faction == Faction.HBC ? WorldLayout.NwcYardCells().Contains(Cell) : Faction == Faction.NWC && WorldLayout.YardCells().Contains(Cell);
		if (!inRival) return;
		_lastSeenAcrossDay = cal.Day;
		string where = Faction == Faction.HBC ? "the Nor'Westers' post" : "the Company's fort";
		bool authorized = cal.Day <= AuthorizedAcrossUntilDay;
		// Only your own people passing by would think anything of it - the event is witnessed like any other.
		Chronicle.Event(authorized ? $"{DisplayName} went over to {where} on the Factor's business." : $"{DisplayName} was seen at {where}.", GlobalPosition, this, DisplayName, authorized ? 0.2f : 1f);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (NetHub.IsServer) { SeenAcross(); MarriageTick(); CaptiveTick(); CountStores(); CountPrivate(); PostKeep((float)delta); }
		base._PhysicsProcess(delta);
		float dt = (float)delta;
		if (IsServerCopy) ServerCanoeTick();
		UpdateCanoeVisual();

		if (IsLocal)
		{
			LocalMove(dt);
			return;
		}
		// Server copy of a remote player, or a puppet: glide to the last report.
		if (_netHasTarget) GlobalPosition = GlobalPosition.Lerp(_netTarget, Mathf.Clamp(dt * 14f, 0f, 1f));
		if (IsServerCopy) { IsSneaking = _remoteSneak; IsSprinting = _remoteSprint; Velocity = _netHasTarget && GlobalPosition.DistanceTo(_netTarget) > 2f ? Vector2.One : Vector2.Zero; }
		UpdateReadyBar();
	}

	private void LocalMove(float dt)
	{
		UpdateReadyBar();
		if (IsDead)
		{
			if (_sprite.Animation != "death") _sprite.Play("death");
			return;
		}
		_sprite.Modulate = HurtFlash > 0f ? new Color(1f, 0.45f, 0.45f) : Colors.White;

		if (_oneShotAnim != "")
		{
			_sprite.Play(_oneShotAnim); _oneShotAnim = "";
			_busyUntil = 0.9f;
		}
		if (_busyUntil > 0f) { _busyUntil -= dt; Velocity = Vector2.Zero; return; }

		bool typing = UI.ChatBox.Instance != null && UI.ChatBox.Instance.IsTyping;
		IsSneaking = !typing && Input.IsActionPressed("sneak") && !Input.IsActionPressed("sprint");
		IsSprinting = !typing && Input.IsActionPressed("sprint");
		Vector2 input = typing ? Vector2.Zero : Input.GetVector("move_left", "move_right", "move_up", "move_down");
		if (input != Vector2.Zero && WantsSkipHours > 0f) NetHub.I?.Act("cancel_wait");
		if (_travel != null)
		{
			if (input != Vector2.Zero) StopTravel("You leave the road.");
			else if (GameCalendar.Instance.Weather.Current is SkyState.Blizzard or SkyState.Storm) StopTravel("The weather closes in. Nobody walks the road in this - find shelter or make a fire.");
			else
			{
				var to = _travel[_travelLeg];
				if (GlobalPosition.DistanceTo(to) < 90f) { _travelLeg++; if (_travelLeg >= _travel.Count) { StopTravel(_travel[^1].DistanceTo(IsoGrid.ToWorld(WorldLayout.OutpostGateOutside)) < 200f ? "Oxford House. The gate is ahead." : "York Factory. The gate is ahead."); } }
				if (_travel != null)
				{
					input = (to - GlobalPosition).Normalized();
					IsSprinting = false; IsSneaking = false;
				}
			}
		}
		bool slippery = false;
		float ground = Sim.Terrain != null ? Sim.Terrain.SpeedFactorAt(GlobalPosition, Inventory.IsWorn(ItemId.Snowshoes), out slippery) : 1f;
		if (IsAfloat) { ground = 1.5f * (Aboard != null && Aboard.Overloaded ? 0.6f : Aboard != null ? Mathf.Lerp(1f, 0.75f, Aboard.LoadLbs / Canoe.CapacityLbs) : 1f); slippery = false; }
		// On the road the march is compressed with the clock (design doc 18.3).
		float travelBoost = _travel != null ? Mathf.Clamp(NetHub.IsServer ? GameCalendar.Instance.MotionScale : GameCalendar.TravelBoost, 1f, 8f) : 1f;
		var want = input * EffectiveSpeed * ground * travelBoost;
		// Ice: no grip. You keep going the way you were going.
		Velocity = slippery ? Velocity.Lerp(want, 0.06f) : want;
		if (input.X != 0) _sprite.FlipH = input.X < 0;
		// Afloat you can't climb the bank except where it's low: any land cell
		// counts as landing, so just don't let the canoe grind onto a wall.

		string target;
		if (IsSneaking)
		{
			bool side = Mathf.Abs(input.X) > Mathf.Abs(input.Y) * 1.2f;
			target = input == Vector2.Zero ? "sneak_idle" : side ? "sneak_side" : (input.Y < -0.3f ? "sneak_back" : "sneak");
		}
		else if (input == Vector2.Zero) target = _facing == "walk_side" ? "idle_side" : _facing == "walk_back" ? "idle_back" : "idle";
		else
		{
			bool side = Mathf.Abs(input.X) > Mathf.Abs(input.Y) * 1.2f;
			target = side ? "walk_side" : input.Y < -0.3f ? "walk_back" : "walk";
			_facing = target;
			if (side && IsSprinting) target = "run_side";
		}
		if (!_sprite.SpriteFrames.HasAnimation(target)) target = target.StartsWith("idle") ? "idle" : target == "run_side" ? "walk_side" : "walk";
		if (_sprite.Animation != target) _sprite.Play(target);
		_sprite.SpeedScale = IsSprinting && input != Vector2.Zero && target != "run_side" ? 1.6f : 1f;
		MoveAndSlide();
	}

	private string _facing = "walk";

	public override void ApplySnapshot(Vector2 pos, string anim, bool flip)
	{
		_netTarget = pos; _netHasTarget = true;
		if (_sprite != null && _sprite.SpriteFrames.HasAnimation(anim) && _sprite.Animation != anim) _sprite.Play(anim);
		if (_sprite != null) _sprite.FlipH = flip;
	}

	public void SetRemoteMotion(bool sneaking, bool sprinting) { _remoteSneak = sneaking; _remoteSprint = sprinting; }

	public override void PuppetDie()
	{
		if (_sprite != null && _sprite.Animation != "death") _sprite.Play("death");
		if (_label != null) _label.Text = $"{DisplayName} (dead)";
		AddToGroup("corpses");
	}

	private ReadyBar _readyBar;
	private void UpdateReadyBar()
	{
		if (_readyBar == null) { _readyBar = new ReadyBar { Position = new Vector2(0, -122), ZIndex = 25, ZAsRelative = false }; AddChild(_readyBar); }
		_readyBar.Fraction = _cooldownMirror;
		_readyBar.Visible = _cooldownMirror > 0f && !IsDead;
		_readyBar.QueueRedraw();
	}
	private float _cooldownMirror => IsServerCopy ? CooldownFraction : _clientCooldown;
	private float _clientCooldown;

	// ---- state mirrored to the owner --------------------------------------------------

	public Godot.Collections.Dictionary PackState()
	{
		var inv = new Godot.Collections.Array();
		foreach (var (id, n) in Inventory.All()) inv.Add(new Godot.Collections.Array { (int)id, n });
		var d = new Godot.Collections.Dictionary
		{
			["h"] = Needs.Health, ["w"] = Needs.Warmth, ["f"] = Needs.Hunger, ["t"] = Needs.Thirst,
			["inv"] = inv, ["standing"] = Standing, ["task"] = Task.Kind == TaskKind.None ? "" : Task.Describe() + (Task.IsComplete ? " - done" : ""),
			["dead"] = IsDead, ["anim"] = _oneShotAnim, ["cd"] = CooldownFraction, ["sheltered"] = IsSheltered,
			["weapon"] = PreferredWeapon != null ? (int)PreferredWeapon.Value : -1, ["skip"] = NetHub.I?.SkipStatus() ?? "", ["focus"] = Core.Focus.For(this),
			["px"] = GlobalPosition.X, ["py"] = GlobalPosition.Y, ["tele"] = _teleported, ["faction"] = (int)Faction, ["afloat"] = Aboard != null,
			["drunk"] = Intoxication, ["sick"] = SickDaysLeft, ["rank"] = Rank, ["fresh"] = DaysSinceFresh, ["mail"] = MailSummary(), ["tongue"] = CreeFluency, ["tongue2"] = AssiniboineFluency, ["skills"] = new float[] { Skills.Hunting, Skills.Trapping, Skills.Trade, Skills.Woodcraft }, ["furs"] = BelievedFursMb, ["quota"] = BelievedQuotaMb, ["counted"] = LastCountedDay, ["unworn"] = UnwornList(),
			["journal"] = new Godot.Collections.Array(Journal.ConvertAll(s => (Variant)s)),
		};
		_oneShotAnim = ""; _teleported = false;
		return d;
	}

	public void UnpackState(Godot.Collections.Dictionary d)
	{
		bool wasDead = Needs.Health <= 0f;
		Needs.Health = (float)d["h"]; Needs.Warmth = (float)d["w"]; Needs.Hunger = (float)d["f"]; Needs.Thirst = (float)d["t"];
		Inventory.Clear();
		foreach (var e in (Godot.Collections.Array)d["inv"]) { var row = (Godot.Collections.Array)e; Inventory.Add((ItemId)(int)row[0], (int)row[1]); }
		Standing = (float)d["standing"];
		TaskText = (string)d["task"];
		_clientCooldown = (float)d["cd"];
		string anim = (string)d["anim"];
		if (anim != "") _oneShotAnim = anim;
		int w = (int)d["weapon"]; PreferredWeapon = w >= 0 ? (ItemId)w : null;
		SkipStatus = (string)d["skip"];
		if (d.ContainsKey("focus")) FocusText = (string)d["focus"];
		if ((bool)d["tele"]) GlobalPosition = new Vector2((float)d["px"], (float)d["py"]);
		Faction = (Faction)(int)d["faction"];
		_afloatMirror = (bool)d["afloat"];
		Intoxication = (float)d["drunk"]; SickDaysLeft = (float)d["sick"]; Rank = (string)d["rank"]; if (d.ContainsKey("fresh")) DaysSinceFresh = (float)d["fresh"];
		if (d.ContainsKey("tongue")) CreeFluency = (float)d["tongue"];
		if (d.ContainsKey("tongue2")) AssiniboineFluency = (float)d["tongue2"];
		if (d.ContainsKey("furs")) { BelievedFursMb = (float)d["furs"]; BelievedQuotaMb = (float)d["quota"]; LastCountedDay = (int)d["counted"]; }
		if (d.ContainsKey("skills")) { var sk = (float[])d["skills"]; if (sk.Length == 4) { Skills.Hunting = sk[0]; Skills.Trapping = sk[1]; Skills.Trade = sk[2]; Skills.Woodcraft = sk[3]; } }
		if (d.ContainsKey("unworn")) { Inventory.Unworn.Clear(); foreach (int u in (int[])d["unworn"]) Inventory.Unworn.Add((ItemId)u); }
		if (d.ContainsKey("mail") && !NetHub.IsServer) { Mail.Clear(); foreach (string line in (string[])d["mail"]) { var parts = line.Split('|'); Mail.Add(new Message { Kind = parts[0], From = parts[1], To = parts[2], Subject = parts[3], Read = parts[4] == "1", Text = parts.Length > 5 ? parts[5] : "" }); } }
		Journal.Clear(); foreach (var e in (Godot.Collections.Array)d["journal"]) Journal.Add((string)e);
		bool dead = (bool)d["dead"];
		if (dead && !wasDead) EmitSignal(SignalName.Died);
		if (!dead && wasDead) EmitSignal(SignalName.Revived);
	}

	// ---- actions (server side) --------------------------------------------------------

	public string ServerAction(string action, Godot.Collections.Array args)
	{
		if (!IsServerCopy) return "";
		if (IsDead) return action == "respawn" ? Respawn() : "";
		switch (action)
		{
			case "interact": return Interact();
			case "use": return UseSlotServer((int)args[0]);
			case "travel": TravellingServer = (bool)args[0]; return "";
			case "click": { var r = ClickAt((int)args[0], new Vector2((float)args[1], (float)args[2])); if (_devClick) GD.Print("[click] " + r); return r; }
			case "wear": { var s = Inventory.Slot((int)args[0]); if (s == null || !Inventory.Wearable(s.Value.id)) return ""; Inventory.ToggleWorn(s.Value.id); return ""; }
			case "move_slot": Inventory.MoveSlot((int)args[0], (int)args[1]); return "";
			case "attack":
			{
				int id = (int)args[0];
				if (NetHub.I != null && NetHub.I.Agents.TryGetValue(id, out var target)) return TryAttack(target);
				foreach (var a in Sim.Agents) if (a.NetId == id) return TryAttack(a);
				return "";
			}
			case "attack_nearest":
			{
				Agent best = null; float bestD = 400f;
				foreach (var a in Sim.AgentsWithin(GlobalPosition, 400f))
				{
					if (a == this || !IsHostileTo(a)) continue;
					float d = GlobalPosition.DistanceTo(a.GlobalPosition);
					if (d < bestD) { bestD = d; best = a; }
				}
				return best != null ? TryAttack(best) : "Nothing hostile within reach.";
			}
			case "drink":
			{
				// At the water with a kettle you fill it; otherwise you drink what's there.
				if (Inventory.Has(ItemId.Kettle) && Sim.Terrain != null && Sim.Terrain.IsWaterNear(GlobalPosition) && !GameCalendar.Instance.IsFrozen)
				{
					Inventory.Add(ItemId.Water, 3); Needs.Thirst = 100f;
					return "You drink and fill the kettle (3 water).";
				}
				if (Inventory.Has(ItemId.Kettle) && GameCalendar.Instance.IsFrozen && HeatNear > 0.3f)
				{
					var h = Sim.Structures?.HearthNear(GlobalPosition, 400f);
					if (h != null && h.IsBurning) { h.FuelHours = Mathf.Max(0.1f, h.FuelHours - 0.6f); Inventory.Add(ItemId.Water, 3); Needs.Thirst = 100f; return "You melt a kettle of snow at the fire (3 water)."; }
				}
				return Drink() ? "You drink." : "No water within reach - carry some, or find a fire in winter.";
			}
			case "wait":
			{
				var cal = GameCalendar.Instance;
				if (cal.IsSkipping) return "";
				bool night = cal.Hour >= 19f || cal.Hour < 6f;
				float hours = night ? ((7f - cal.Hour + 24f) % 24f) : 1f;
				return RequestSkip(hours, night ? "You settle down for the night." : "You wait.");
			}
			case "cancel_wait": WantsSkipHours = 0f; return "";
			case "canoe_load": return CanoeLoad(Aboard ?? Sim.Spawner?.NearestFreeCanoe(GlobalPosition, 150f), (bool)args[0]);
			case "canoe":
			{
				if (Aboard != null) return "Paddle to a bank to step out.";
				if (GameCalendar.Instance.IsFrozen) return "The canoes are frozen in till breakup. Walk on the ice.";
				var canoe = Sim.Spawner?.NearestFreeCanoe(GlobalPosition, 130f);
				if (canoe == null) return "No canoe here.";
				canoe.SetState(GlobalPosition, NetId);
				Aboard = canoe;
				return "You push off. Paddle onto the water; step onto any bank to land.";
			}
			case "dev_teleport":
				if (!OS.IsDebugBuild()) return "";
				GlobalPosition = IsoGrid.ToWorld(TerrainPainter.NamedSpot((string)args[0]));
				_netTarget = GlobalPosition; _teleported = true;
				GD.Print($"[dev] moved {DisplayName} to {args[0]} {Cell}");
				return $"(dev) moved to {args[0]}";
			case "respawn": return Respawn();
		}
		return "";
	}

	// Sleeping and waiting are agreed by everyone alive (design doc: one
	// shared clock). Solo, that's just you.
	private string RequestSkip(float hours, string text)
	{
		WantsSkipHours = hours;
		NetHub.I?.ServerCheckSkip();
		int others = 0; foreach (var p in Sim.Players()) if (p != this && !p.IsDead) others++;
		return others > 0 && WantsSkipHours > 0f ? $"{text} Waiting for the others to turn in." : text;
	}

	// Death is a turn of the story (design doc P.6): someone of the house
	// takes your place, and begins with what you left him.
	private string Respawn()
	{
		var inh = LookAtInheritance();
		PendingInheritance = null;
		Heirs.Succeed(this, inh);
		Revive();
		Inventory.Clear();
		GiveStartingKit();
		GlobalPosition = IsoGrid.ToWorld(WorldLayout.GateOutside);
		_netTarget = GlobalPosition; _teleported = true;
		Task.Kind = TaskKind.None;
		if (_sprite != null) _sprite.Play("idle");
		if (_label != null) _label.Text = DisplayName;
		if (IsLocal) EmitSignal(SignalName.Revived);
		return "";
	}

	// What the heir is handed, for the overlay to read out before he takes it up.
	public Heirs.Inheritance PendingInheritance;
	public Heirs.Inheritance LookAtInheritance() => PendingInheritance ??= Heirs.Prepare(this);

	private string TryAttack(Agent target)
	{
		if (target == null || !IsInstanceValid(target) || target == this) return "";
		var (dmg, range, _) = AttackProfile();
		float dist = GlobalPosition.DistanceTo(target.GlobalPosition);
		if (dist > range + 20f) return $"Too far for the {(Weapon != null ? Items.Items.Of(Weapon.Value).Name.ToLower() : "fist")}.";
		if (!CanAttack) return "";
		if (_sprite != null) _sprite.FlipH = target.GlobalPosition.X < GlobalPosition.X;
		if (OnParoleAgainst(target))
		{
			ParoleUntilDay = -1;
			Standing -= 10f;
			Economy.Rivalry.Raise(8f, $"{DisplayName} broke his parole");
			Chronicle.Event($"{DisplayName} broke the parole he gave the other company.", GlobalPosition, this, DisplayName, 2.5f);
			Say("You raise your hand against men you gave your word to. Whoever saw it will tell it.");
		}
		var before = Weapon;
		bool hit = Attack(target);
		if (before == ItemId.Musket) _oneShotAnim = "shoot";
		if (hit && target.IsDead && target is Animal dead && dead.IsDangerous) Task.Progress(TaskKind.DriveOffAnimals, 1);
		string what = Weapon != null ? Items.Items.Of(Weapon.Value).Name : "fists";
		if (hit) return target.IsDead ? $"{target.Describe()} is dead." : $"You strike {target.Describe()} with your {what.ToLower()}.";
		return "Missed.";
	}

	// Use what's in hand on a spot in the world: lay wood for a fire, put a
	// spark to it, set a building alight - or, with nothing to use, interact.
	private string ClickAt(int slot, Vector2 where)
	{
		var held = Inventory.Slot(slot);
		if (held == null) return Interact();
		var id = held.Value.id; var def = Items.Items.Of(id);
		if (where.DistanceTo(GlobalPosition) > 220f) return "Too far. Step closer.";
		var cell = IsoGrid.FromWorld(where);
		var structures = Sim.Structures; var fire = World.Fire.I;
		switch (id)
		{
			case ItemId.Firewood:
			{
				var h = structures?.HearthAt(cell) ?? structures?.HearthNear(where, 120f);
				if (h != null) { Inventory.Remove(id); h.Feed(def.FuelHours); return h.Lit ? "You feed the fire." : "You lay more wood. It wants a spark yet."; }
				if (IsSheltered || (structures?.IsShelteredCell(cell) ?? false)) return "Not indoors. Feed the hearth instead.";
				if (Sim.Terrain?.IsWaterOrIce(cell) ?? true) return "Not on the water.";
				var built = structures?.BuildCampfire(cell);
				if (built == null) return "The ground here won't take a fire.";
				Inventory.Remove(id); built.Feed(def.FuelHours);
				if (Inventory.Has(ItemId.Flint)) { built.Light(); return "You lay a fire and strike a spark into it. It catches."; }
				built.Lit = false;
				return "You lay a fire ring and wood - but you have no flint and steel to strike a spark, and laid wood gives no heat.";
			}
			case ItemId.Flint:
			{
				var h = structures?.HearthAt(cell) ?? structures?.HearthNear(where, 140f);
				if (h != null)
				{
					if (h.IsBurning) return "It's burning already.";
					if (h.FuelHours <= 0f) { if (!Inventory.Has(ItemId.Firewood)) return "No wood on it and none in your pack."; Inventory.Remove(ItemId.Firewood); h.Feed(Items.Items.Of(ItemId.Firewood).FuelHours); }
					h.Light();
					return "The spark takes. The fire catches.";
				}
				if (structures != null && fire != null && structures.IsFlammable(cell))
				{
					// Your own post's timbers only burn on purpose: crouch to mean it.
					bool ours = HomePost != null && (Faction == Faction.NWC ? WorldLayout.NwcYardCells().Contains(cell) : WorldLayout.YardCells().Contains(cell));
					if (ours && !IsSneaking) return $"That's the Company's own timber. Crouch ({Core.Keybinds.KeyFor("sneak")}) if you truly mean to burn it.";
					if (!fire.Ignite(cell)) return "It won't catch.";
					Chronicle.Event($"{DisplayName} set fire to the buildings.", GlobalPosition, this, weight: 3f);
					foreach (var a in Sim.AgentsWithin(GlobalPosition, 650f))
					{
						if (a is not Npc n || n.IsDead || n.HomeStores == null) continue;
						if (n.HomeStores.HearthCells.Count > 0 && IsoGrid.ToWorld(n.HomeStores.HearthCells[0]).DistanceTo(where) < 900f) { n.ChangeRapport(this, -50f); if (n.Weapon != null) n.Brain.Push(n, new Agents.Goals.AttackGoal(this)); }
					}
					return "You put the spark to the wood. It catches.";
				}
				return "Nothing there will take a spark: click a fire ring, a hearth, or wood.";
			}
			default:
				if (def.Kind == ItemKind.Food || def.Kind == ItemKind.Drink || def.Kind == ItemKind.Tool) return UseSlotServer(slot);
				return Interact();
		}
	}

	private string UseSlotServer(int i)
	{
		var slot = Inventory.Slot(i);
		if (slot == null) return "";
		var (id, _) = slot.Value;
		var def = Items.Items.Of(id);
		switch (def.Kind)
		{
			case ItemKind.Food:
				if (!Consume(id)) return "";
				_oneShotAnim = "eat";
				return $"You eat the {def.Name.ToLower()}.";
			case ItemKind.Drink:
			{
				// Water goes on a fire first if one's at hand.
				if (id == ItemId.Water && World.Fire.I != null && World.Fire.I.NearestBurning(GlobalPosition, 130f) is Vector2I burning)
				{
					Inventory.Remove(id);
					return World.Fire.I.Douse(burning) ? "You throw the water on. The flames die down." : "You throw the water on. It hisses and burns on.";
				}
				if (!Consume(id)) return "";
				_oneShotAnim = "drink";
				return $"You drink the {def.Name.ToLower()}.";
			}
			case ItemKind.Fuel:
			{
				var hearth = Sim.Structures?.HearthNear(GlobalPosition);
				if (hearth == null) return "No fire near enough to feed.";
				FeedFire(id);
				return $"You put a log on the fire. ({hearth.FuelHours:0.#} h of fuel)";
			}
			case ItemKind.Clothing:
				Inventory.ToggleWorn(id);
				return Inventory.IsWorn(id) ? $"You put the {def.Name.ToLower()} on. (+{def.Warmth:0} warmth)" : $"You take the {def.Name.ToLower()} off and stow it.";
			case ItemKind.Tool when id == ItemId.Axe:
			{
				var tree = Sim.Structures?.NearestTree(GlobalPosition, 150f);
				if (tree == null) return "No tree within reach of the axe. The bush starts west and south of the post.";
				if (Sim.Structures.FellTreeAt(tree.Value)) { Inventory.Add(ItemId.Firewood, Skills.Woodcraft >= 0.5f ? 6 : 4); Skills.Learn(ref Skills.Woodcraft, 0.02f); _busyUntil = Skills.Woodcraft >= 0.5f ? 0.8f : 1.2f; Chronicle.Event($"{DisplayName} felled a tree.", GlobalPosition, this, weight: 0.1f); return "You fell the spruce and split it: 4 firewood."; }
				return "The axe glances off.";
			}
			case ItemKind.Tool when id == ItemId.Snowshoes:
				Inventory.ToggleWorn(id);
				return Inventory.IsWorn(id) ? "You lace on the snowshoes. Snow is no trouble now." : "You take the snowshoes off.";
			case ItemKind.Tool when id == ItemId.Kettle:
				return ServerAction("drink", new Godot.Collections.Array());
			case ItemKind.Tool when id == ItemId.FishingLine:
			{
				if (Sim.Terrain == null || !Sim.Terrain.IsWaterNear(GlobalPosition)) return "Cast where there's water.";
				if (GameCalendar.Instance.IsFrozen) return "The water is frozen. You could jig through the ice if you had the patience - not yet.";
				if (!CanAttack) return "";
				_attackCooldownPublic(3f);
				if (GD.Randf() < 0.45f) { int n = GD.RandRange(1, 2); Inventory.Add(ItemId.Fish, n); return $"You land {n} fish."; }
				return "Nothing on the line.";
			}
			case ItemKind.Tool when id == ItemId.Flint:
			{
				var fire = World.Fire.I;
				if (fire == null) return "";
				// A cold hearth in reach: light it (with wood, if it's empty).
				var cold = Sim.Structures.HearthNear(GlobalPosition, 160f);
				if (cold != null && !cold.IsBurning)
				{
					if (!Inventory.Has(ItemId.Firewood)) return "The hearth is cold and you have no wood for it.";
					Inventory.Remove(ItemId.Firewood);
					cold.Feed(Items.Items.Of(ItemId.Firewood).FuelHours);
					return "You lay the wood, strike a spark, and the hearth catches.";
				}
				// Arson takes intent: crouched (Ctrl), against wood, deliberately.
				Vector2I target = Cell;
				if (IsSneaking)
				{
					if (!Sim.Structures.IsFlammable(target))
					{
						foreach (var n in IsoGrid.EdgeNeighbours(Cell)) if (Sim.Structures.IsFlammable(n)) { target = n; break; }
					}
				}
				if (!IsSneaking || !Sim.Structures.IsFlammable(target))
				{
					// Otherwise: make camp, if you have wood and open ground.
					if (IsSheltered) return "Not indoors. Take it outside, or feed the hearth. (Crouch with Ctrl if you mean to burn the place.)";
					if (!Inventory.Has(ItemId.Firewood)) return "You have flint but no wood. Cut some first.";
					Vector2I spot = Cell;
					foreach (var n in IsoGrid.EdgeNeighbours(Cell)) if (n != Cell && !(Sim.Terrain?.IsWaterOrIce(n) ?? true) && !Sim.Structures.IsCellSolid(n)) { spot = n; break; }
					if (spot == Cell) return "No clear ground here for a fire.";
					var hearth = Sim.Structures.BuildCampfire(spot);
					if (hearth == null) return "The ground here won't take a fire.";
					Inventory.Remove(ItemId.Firewood);
					hearth.Feed(Items.Items.Of(ItemId.Firewood).FuelHours);
					if (HomePost != null && GlobalPosition.DistanceTo(IsoGrid.ToWorld(HomePost.AccessCell)) > 1500f) Chronicle.Event($"{DisplayName} made camp in the open.", GlobalPosition, this, weight: 0.3f);
					return "You strike a spark into the tinder and the fire catches. Feed it wood from the hotbar; it warms about six paces and melts snow for drinking.";
				}
				if (!fire.Ignite(target)) return "It won't catch.";
				Chronicle.Event($"{DisplayName} set fire to the buildings.", GlobalPosition, this, weight: 3f);
				// Anyone whose house this is, and saw it, comes for you.
				foreach (var a in Sim.AgentsWithin(GlobalPosition, 650f))
					if (a is Npc n2 && !n2.IsDead && n2.HomeStores != null && n2.HomeStores.HearthCells.Count > 0 && IsoGrid.ToWorld(n2.HomeStores.AccessCell).DistanceTo(GlobalPosition) < 1200f)
					{ n2.ChangeRapport(this, -60f); if (n2.Weapon != null) n2.Brain.Push(n2, new Agents.Goals.AttackGoal(this)); }
				return "You strike sparks into the tinder. It catches.";
			}
			case ItemKind.Tool when id == ItemId.SteelTrap:
			{
				var site = Sim.Ecology?.SiteNear(GlobalPosition, Ecology.Species.Beaver);
				if (site == null) return "No beaver lodge here. Traps go where the beaver are - along the river.";
				if (site.IsDead) return "This lodge is dead. Nothing will come to a trap here.";
				int n = Inventory.TakeUpTo(ItemId.SteelTrap, 6);
				var line = site.SetTraps(TrapOwner, n, Skills.Trapping);
				Skills.Learn(ref Skills.Trapping, 0.02f);
				Chronicle.Event($"{DisplayName} set traps at {site.Name}.", GlobalPosition, this, weight: 0.5f);
				return $"You set {n} traps at {site.Name} ({line.Traps} on your line). Come back tomorrow.";
			}
			case ItemKind.Weapon:
				PreferredWeapon = id;
				return $"You ready the {def.Name.ToLower()} (damage {def.Damage:0}, reach {def.Range:0}).";
			case ItemKind.Fur:
			case ItemKind.Goods:
				return $"{def.Name}: worth {def.ValueMb:0.#} MB at the counter.";
		}
		return "";
	}

	public string TrapOwner => $"player:{DisplayName}";

	// E: talk to whoever is close, else pick up, loot a body, lift the traps,
	// a bed, the woodpile or warehouse, work a door, look at a fire.
	private string Interact()
	{
		var npc = NearestNpc(110f);
		if (npc != null && !npc.IsDead)
		{
			var state = Conversation.Start(npc);
			NetHub.I?.PushDialogue(this, state);
			return "";
		}
		var body = NearestCorpse(110f);
		if (body != null)
		{
			if (body is Animal animal)
			{
				if (!animal.Butcher(this, out int took, out int left)) return animal.MeatLeft > 0 ? "You cannot carry another pound of it." : "Nothing more to take.";
				return left > 0
					? $"You butcher what you can carry of the {animal.Describe().ToLower()}: {took} pieces, and {left} left lying. Come back for it, or bring a man."
					: $"You butcher the {animal.Describe().ToLower()}: {took} pieces.";
			}
			int moved = 0;
			foreach (var (id, n) in new List<(ItemId, int)>(body.Inventory.All())) { body.Inventory.Remove(id, n); Inventory.Add(id, n); moved += n; }
			int papers = body.Mail.Count;
			if (papers > 0) { Mail.AddRange(body.Mail); body.Mail.Clear(); Chronicle.Event($"{DisplayName} took the papers off {body.Describe()}'s body.", GlobalPosition, this, weight: 1.5f); }
			return moved + papers > 0 ? $"You take what {body.Describe()} carried{(papers > 0 ? $", papers and all ({papers})" : "")}." : "Nothing left on the body.";
		}

		var pickup = NearestPickup();
		if (pickup != null)
		{
			Inventory.Add(pickup.Item, pickup.Count);
			string label = pickup.Label;
			Sim.Spawner?.Consume(pickup);
			return $"Picked up {label}.";
		}

		// A bush in fruit, picked by hand (design doc 13.5): the one food
		// in this country that answers the scurvy and costs nothing but
		// stooping for it.
		var bush = Sim.Structures?.NearestBerryBush(GlobalPosition, 110f, true);
		if (bush != null)
		{
			string what = Sim.Structures.BushName(bush.Value);
			int got = Sim.Structures.PickBerriesAt(bush.Value);
			if (got > 0)
			{
				Inventory.Add(ItemId.Berries, got);
				_busyUntil = 1.2f;
				return $"You strip the {what}: {got} of berries.";
			}
		}

		var lodge = Sim.Ecology?.SiteNear(GlobalPosition, Ecology.Species.Beaver);
		if (lodge != null)
		{
			var line = lodge.LineOf(TrapOwner);
			if (line != null && line.Traps > 0)
			{
				int got = line.Caught; line.Caught = 0; line.HoursSinceCheck = 0f;
				for (int i = 0; i < got; i++) Ecology.SpeciesInfo.Yield(Ecology.Species.Beaver, Inventory);
				string robbed = line.Robbed > 0 ? $" Someone has been at your traps - {line.Robbed} taken." : "";
				line.Robbed = 0;
				return got > 0 ? $"{got} beaver in the traps. Pelts and meat in your pack.{robbed}" : $"The traps are empty.{robbed} ({line.Traps} traps set here)";
			}
			int poached = Ecology.TraplineRules.Poach(lodge, TrapOwner, 1f);
			if (poached > 0)
			{
				for (int i = 0; i < poached; i++) Ecology.SpeciesInfo.Yield(Ecology.Species.Beaver, Inventory);
				Chronicle.Event($"{DisplayName} robbed another party's traps at {lodge.Name}.", GlobalPosition, this, weight: 1.5f);
				return $"You take {poached} beaver from someone else's traps. If anyone saw, they'll remember.";
			}
		}

		var nearCanoe = Aboard ?? Sim.Spawner?.NearestFreeCanoe(GlobalPosition, 240f);
		if (nearCanoe != null) { NetHub.I?.PushDialogue(this, Conversation.StartCanoe(nearCanoe)); return ""; }

		var bed = Sim.Structures?.NearestBed(GlobalPosition, 90f);
		if (bed != null)
		{
			var cal = GameCalendar.Instance;
			if (cal.IsSkipping) return "";
			bool night = cal.Hour >= 17f || cal.Hour < 6f;
			float hours = night ? ((7f - cal.Hour + 24f) % 24f) : 1f;
			return RequestSkip(hours, night ? "You turn in till morning." : "You lie down for an hour. (From 17:00 a bed takes you through to 07:00.)");
		}

		// Inside the other company's storehouse: help yourself, and take what
		// follows (design doc 7.1, 15.1).
		var rivalPost = Economy.Rivalry.PostOf(Economy.Rivalry.RivalOf(Faction));
		if (rivalPost != null && (rivalPost.Company == Faction.NWC ? WorldLayout.NwcHouse.Contains(Cell) : WorldLayout.Warehouse.Contains(Cell)))
		{
			int took = Economy.Rivalry.Plunder(this, rivalPost, 4);
			Task.Progress(TaskKind.Raid, took);
			if (!Economy.Rivalry.OpenHostility) Standing -= 3f;
			return took > 0 ? $"You shoulder {took} lots of the {(rivalPost.Company == Faction.NWC ? "Nor'Westers'" : "Company's")} goods. Now get them home." : "Nothing left worth carrying.";
		}

		// Somebody else's woodpile (design doc 16.3's zero-sum, in firewood): you can take, and be seen taking.
		foreach (var other in OtherHouseholds())
		{
			if (GlobalPosition.DistanceTo(IsoGrid.ToWorld(other.WoodpileCell)) >= 120f) continue;
			NetHub.I?.PushDialogue(this, Conversation.StartWoodpile(other));
			return "";
		}

		var fort = HomePost;
		if (fort != null)
		{
			if (GlobalPosition.DistanceTo(IsoGrid.ToWorld(fort.WoodpileCell)) < 120f)
			{
				NetHub.I?.PushDialogue(this, Conversation.StartWoodpile(fort));
				return "";
			}
			bool inStore = fort == Sim.Nwc ? WorldLayout.NwcHouse.Contains(Cell) : fort == Sim.Outpost ? WorldLayout.OutpostHouse.Contains(Cell) : WorldLayout.Warehouse.Contains(Cell);
			if (inStore)
			{
				// The storeroom chest: stow, or draw kit (design doc 12.5's stow/withdraw boundary).
				var state = Conversation.StartStores(fort);
				NetHub.I?.PushDialogue(this, state);
				return "";
			}
			if (GlobalPosition.DistanceTo(IsoGrid.ToWorld(fort.AccessCell)) < 110f && Inventory.CountOfKind(ItemKind.Food) < 3)
			{
				if (fort.TakeRation(this, 2)) return "You draw a ration from the stores.";
			}
			var ship = Sim.Ship;
			if (Faction == Faction.HBC && ship != null && ship.IsDocked && GlobalPosition.DistanceTo(IsoGrid.ToWorld(WorldLayout.WharfEnd)) < 160f && ship.Cargo.SlotCount > 0)
			{
				int carried = 0;
				foreach (var (id, n) in new List<(ItemId, int)>(ship.Cargo.All()))
				{
					int take = Mathf.Min(n, 4 - carried); if (take <= 0) break;
					ship.Cargo.Remove(id, take); Inventory.Add(id, take); carried += take;
				}
				return $"You shoulder {carried} lots from the ship. Take them to the warehouse.";
			}
		}

		var structures = Sim.Structures;
		if (structures == null) return "";
		if (structures.TryToggleDoorNear(Position)) return "";

		var hearth = structures.HearthNear(GlobalPosition);
		return hearth != null ? hearth.Describe() : "";
	}

	// What E would do right now, for the prompt above the hotbar. Runs on
	// the owning client from what it can see; mirrors Interact()'s order.
	public string InteractHint()
	{
		if (IsDead) return "";
		if (_travel != null) return "On the river road - E or any step stops";
		if (NearRoadHead(out bool atYork) && NearestNpc(110f) == null) return atYork ? $"{Core.Keybinds.KeyFor("interact")}  Take the river road inland to Oxford House (a day's march)" : $"{Core.Keybinds.KeyFor("interact")}  Take the river road down to York Factory";
		var npc = NearestNpc(110f);
		if (npc != null) return $"{Core.Keybinds.KeyFor("interact")}  Talk to {npc.DisplayName}";
		var body = NearestCorpse(110f);
		if (body != null) return body is Animal ? $"{Core.Keybinds.KeyFor("interact")}  Butcher the {body.Describe().ToLower()}" : $"{Core.Keybinds.KeyFor("interact")}  Search {body.Describe()}";
		var pickup = NearestPickup();
		if (pickup != null) return $"{Core.Keybinds.KeyFor("interact")}  Pick up {pickup.Label}";
		var bush = Sim.Structures?.NearestBerryBush(GlobalPosition, 110f, true);
		if (bush != null) return $"{Core.Keybinds.KeyFor("interact")}  Pick the {Sim.Structures.BushName(bush.Value)}";
		var lodge = Sim.Ecology?.SiteNear(GlobalPosition, Ecology.Species.Beaver);
		if (lodge != null) { var line = lodge.LineOf(TrapOwner); return line != null && line.Traps > 0 ? $"{Core.Keybinds.KeyFor("interact")}  Check your traps ({lodge.Name})" : Inventory.Has(ItemId.SteelTrap) ? $"Use a steel trap here to set a line ({lodge.Name})" : $"{lodge.Name}"; }
		if ((Aboard ?? Sim.Spawner?.NearestFreeCanoe(GlobalPosition, 240f)) != null) return $"{Core.Keybinds.KeyFor("interact")}  The canoe";
		if (Sim.Structures?.NearestBed(GlobalPosition, 90f) != null) return (GameCalendar.Instance?.Hour ?? 12f) is >= 17f or < 6f ? $"{Core.Keybinds.KeyFor("interact")}  Sleep till morning" : $"{Core.Keybinds.KeyFor("interact")}  Rest an hour";
		foreach (var h in OtherHouseholds()) if (GlobalPosition.DistanceTo(IsoGrid.ToWorld(h.WoodpileCell)) < 120f) return $"{Core.Keybinds.KeyFor("interact")}  {h.Title}'s woodpile";
		var fort = HomePost;
		if (fort != null)
		{
			if (GlobalPosition.DistanceTo(IsoGrid.ToWorld(fort.WoodpileCell)) < 120f) return $"{Core.Keybinds.KeyFor("interact")}  The woodpile";
			if (fort == Sim.Nwc ? WorldLayout.NwcHouse.Contains(Cell) : fort == Sim.Outpost ? WorldLayout.OutpostHouse.Contains(Cell) : WorldLayout.Warehouse.Contains(Cell)) return $"{Core.Keybinds.KeyFor("interact")}  The storeroom chest";
			if (GlobalPosition.DistanceTo(IsoGrid.ToWorld(fort.AccessCell)) < 110f && Inventory.CountOfKind(ItemKind.Food) < 3) return $"{Core.Keybinds.KeyFor("interact")}  Draw a ration";
			if (Faction == Faction.HBC && Sim.Ship != null && Sim.Ship.IsDocked && GlobalPosition.DistanceTo(IsoGrid.ToWorld(WorldLayout.WharfEnd)) < 160f && Sim.Ship.Cargo.SlotCount > 0) return $"{Core.Keybinds.KeyFor("interact")}  Shoulder cargo from the ship";
		}
		var rival = Economy.Rivalry.PostOf(Economy.Rivalry.RivalOf(Faction));
		if (rival != null && (rival.Company == Faction.NWC ? WorldLayout.NwcHouse.Contains(Cell) : WorldLayout.Warehouse.Contains(Cell))) return $"{Core.Keybinds.KeyFor("interact")}  Plunder their stores";
		if (Sim.Structures != null && Sim.Structures.HasDoorNear(Position)) return $"{Core.Keybinds.KeyFor("interact")}  Open / close the door";
		var hearth = Sim.Structures?.HearthNear(GlobalPosition);
		if (hearth != null) return hearth.IsBurning ? $"{Core.Keybinds.KeyFor("interact")}  Look at the fire" : hearth.FuelHours > 0f ? "Flint on it to light it" : $"{Core.Keybinds.KeyFor("interact")}  Cold hearth (feed it wood)";
		return "";
	}

	// Loading and unloading a canoe, for the bank menu.
	public string CanoeLoad(Canoe c, bool loading)
	{
		int moved = 0;
		if (loading)
		{
			foreach (var (id, n) in new List<(ItemId, int)>(Inventory.All()))
			{
				var k = Items.Items.Of(id).Kind;
				if (k == ItemKind.Clothing || k == ItemKind.Weapon || (k == ItemKind.Food && n <= 2)) continue;
				int give = k == ItemKind.Food ? n - 2 : n;
				float each = Items.Items.Of(id).WeightLbs;
				if (c.LoadLbs + each * give > Canoe.CapacityLbs) give = Mathf.FloorToInt((Canoe.CapacityLbs - c.LoadLbs) / Mathf.Max(0.1f, each));
				if (give <= 0) continue;
				Inventory.Remove(id, give); c.Cargo.Add(id, give); moved += give;
			}
			return moved > 0 ? $"You stow {moved} lots in her ({c.LoadLbs:0}/{Canoe.CapacityLbs:0} lb)." : "Nothing to stow, or she's full.";
		}
		foreach (var (id, n) in new List<(ItemId, int)>(c.Cargo.All())) { c.Cargo.Remove(id, n); Inventory.Add(id, n); moved += n; }
		return moved > 0 ? $"You take {moved} lots out of her." : "She's empty.";
	}

	private IEnumerable<Economy.Household> OtherHouseholds()
	{
		if (Sim.Fort != null && Sim.Fort != HomePost) yield return Sim.Fort;
		if (Sim.Nwc != null && Sim.Nwc != HomePost) yield return Sim.Nwc;
		if (Sim.Band != null) yield return Sim.Band;
	}

	// Stow what the warehouse takes (design doc 12.5): goods, furs, and -
	// if asked - provisions for the mess. Kit and personal food stay with you.
	public int StowInto(Economy.FortLedger fort, bool provisions)
	{
		int moved = 0;
		foreach (var (id, n) in new List<(ItemId, int)>(Inventory.All()))
		{
			var kind = Items.Items.Of(id).Kind;
			bool goods = kind == ItemKind.Goods || kind == ItemKind.Fur;
			bool food = kind == ItemKind.Food && (id == ItemId.Goose || id == ItemId.Venison || id == ItemId.Fish || id == ItemId.Hare || id == ItemId.DriedMeat || id == ItemId.DriedGoose || id == ItemId.Pemmican);
			if (!(provisions ? food : goods)) continue;
			int take = provisions && n <= 2 ? 0 : provisions ? n - 2 : n;   // keep a couple of meals
			if (take <= 0) continue;
			Inventory.Remove(id, take); fort.Stores.Add(id, take); moved += take;
			if (kind == ItemKind.Goods) { Task.Progress(TaskKind.CarryCargo, take); if (fort == Sim.Outpost) Task.Progress(TaskKind.Courier, take); }
			if (id == ItemId.Goose) Task.Progress(TaskKind.Geese, take);
			// Leave to do it your way: anything of worth put into the stores counts.
			if (Task.OwnWay && !Task.IsComplete && (kind == ItemKind.Fur || kind == ItemKind.Goods)) Task.Progress(Task.Kind, Mathf.Max(1, Mathf.RoundToInt(Items.Items.Of(id).ValueMb * take)));
		}
		if (provisions && moved > 0) { Standing += Mathf.Min(4f, moved * 0.5f); Chronicle.Event($"{DisplayName} brought {moved} of meat to the stores.", GlobalPosition, this, weight: 0.5f); }
		return moved;
	}

	// How many players this machine knows about (Sim.Players is a sequence).
	private static int PlayerCount()
	{
		int n = 0;
		foreach (var _ in Sim.Players()) n++;
		return n;
	}

	private Npc NearestNpc(float reach)
	{
		Npc best = null; float bestD = reach;
		foreach (var a in Sim.AgentsWithin(GlobalPosition, reach))
		{
			if (a is not Npc n || n.IsDead) continue;
			float d = GlobalPosition.DistanceTo(a.GlobalPosition);
			if (d < bestD) { bestD = d; best = n; }
		}
		return best;
	}

	private Agent NearestCorpse(float reach)
	{
		Agent best = null; float bestD = reach;
		foreach (var node in GetTree().GetNodesInGroup("corpses"))
		{
			if (node is not Agent a || !IsInstanceValid(a) || a == this) continue;
			float d = GlobalPosition.DistanceTo(a.GlobalPosition);
			if (d < bestD) { bestD = d; best = a; }
		}
		return best;
	}

	private WorldItem NearestPickup()
	{
		WorldItem best = null;
		float bestDist = PickupReach;
		foreach (var node in GetTree().GetNodesInGroup("pickups"))
		{
			if (node is not WorldItem item) continue;
			float d = item.GlobalPosition.DistanceTo(GlobalPosition);
			if (d < bestDist) { bestDist = d; best = item; }
		}
		return best;
	}
}
