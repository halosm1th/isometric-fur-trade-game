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
	// Things you've seen happen (keys into the Chronicle).
	public readonly HashSet<string> Known = new();
	// Time-skip agreement: how many hours this player wants to skip (0 = none).
	public float WantsSkipHours;
	// Server-side conversation state with whoever you're talking to.
	public UI.Conversation Conversation;

	// Mirrored from the server for the owner's HUD.
	public string TaskText = "";
	public string SkipStatus = "";

	public override string CurrentAnimation => _sprite?.Animation ?? "idle";
	public override bool FlipH => _sprite?.FlipH ?? false;

	// ---- setup ---------------------------------------------------------------

	public override void _Ready()
	{
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
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
	}

	public void BecomeLocal()
	{
		IsPuppet = false;
		if (_camera != null) { _camera.Enabled = true; _camera.MakeCurrent(); }
		if (_label != null) _label.Visible = false;
		CallDeferred(nameof(FindDialogue));
		GetTree().CreateTimer(1.5).Timeout += () =>
		{
			if (!IsInstanceValid(this)) return;
			EmitSignal(SignalName.Notice, "York Factory, Hudson's Bay. You are the Company's new hand. E talks, 1-9 uses, left click strikes, Tab chats, F1 opens the test console.");
		};
		// Dev aids: `--spawn=depot` moves you there (server permitting), `--provoke`
		// knifes the nearest person after 3 s, `--talk` opens a conversation after 4 s.
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("--spawn=") && !NetHub.IsServer) { string spot = arg.Substring(8); GetTree().CreateTimer(1.0).Timeout += () => NetHub.I?.Act("dev_teleport", new Godot.Collections.Array { spot }); }
			if (arg == "--provoke") GetTree().CreateTimer(3.0).Timeout += () => { var n = NearestNpc(400f); if (n != null) NetHub.I?.Act("attack", new Godot.Collections.Array { n.NetId }); };
			if (arg == "--talk") GetTree().CreateTimer(4.0).Timeout += () => NetHub.I?.Act("interact");
		}
	}

	private void FindDialogue() => _dialogue = UI.DialogueMenu.Instance;

	// The Factor's morning orders for the new hand (server side).
	public void AssignTask(TaskKind kind, int need)
	{
		var cal = GameCalendar.Instance;
		if (Task.Kind != TaskKind.None && !Task.IsComplete && Task.DayAssigned != cal.Day) { Standing -= 2f; Say("Yesterday's task went undone. Mr. Auld noticed."); }
		if (Task.Kind != TaskKind.None && Task.IsComplete) Standing += 5f;
		Task.Kind = kind; Task.Need = need; Task.Done = 0; Task.DayAssigned = cal.Day;
		if (kind != TaskKind.None) Say(Task.Describe());
	}

	// A line for this player's owner, wherever they are.
	public void Say(string text) => NetHub.I?.NoticeTo(this, text);

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

		if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
		{
			var target = AgentUnderCursor();
			if (target != null) NetHub.I?.Act("attack", new Godot.Collections.Array { target.NetId });
			return;
		}
		if (@event.IsActionPressed("attack_nearest")) { NetHub.I?.Act("attack_nearest"); return; }
		if (@event.IsActionPressed("interact")) { NetHub.I?.Act("interact"); return; }
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

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		float dt = (float)delta;

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
		Velocity = input * EffectiveSpeed;
		if (input.X != 0) _sprite.FlipH = input.X < 0;

		string target;
		if (IsSneaking)
		{
			bool side = Mathf.Abs(input.X) > Mathf.Abs(input.Y) * 1.2f;
			target = input == Vector2.Zero ? "sneak_idle" : side ? "sneak_side" : (input.Y < -0.3f ? "sneak_back" : "sneak");
		}
		else target = input == Vector2.Zero ? "idle" : (input.Y < -0.3f ? "walk_back" : "walk");
		if (_sprite.Animation != target) _sprite.Play(target);
		_sprite.SpeedScale = IsSprinting && input != Vector2.Zero ? 1.6f : 1f;
		MoveAndSlide();
	}

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
			["weapon"] = PreferredWeapon != null ? (int)PreferredWeapon.Value : -1, ["skip"] = NetHub.I?.SkipStatus() ?? "",
			["px"] = GlobalPosition.X, ["py"] = GlobalPosition.Y, ["tele"] = _teleported,
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
		if ((bool)d["tele"]) GlobalPosition = new Vector2((float)d["px"], (float)d["py"]);
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
			case "drink": return Drink() ? "You drink." : "No water within reach.";
			case "wait":
			{
				var cal = GameCalendar.Instance;
				if (cal.IsSkipping) return "";
				bool night = cal.Hour >= 19f || cal.Hour < 6f;
				float hours = night ? ((7f - cal.Hour + 24f) % 24f) : 1f;
				return RequestSkip(hours, night ? "You settle down for the night." : "You wait.");
			}
			case "cancel_wait": WantsSkipHours = 0f; return "";
			case "dev_teleport":
				if (!OS.IsDebugBuild()) return "";
				GlobalPosition = IsoGrid.ToWorld(TerrainPainter.NamedSpot((string)args[0]));
				_netTarget = GlobalPosition; _teleported = true;
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

	private string Respawn()
	{
		Revive();
		GiveStartingKit();
		GlobalPosition = IsoGrid.ToWorld(WorldLayout.GateOutside);
		_netTarget = GlobalPosition; _teleported = true;
		Task.Kind = TaskKind.None; Standing = Mathf.Max(0f, Standing - 5f);
		if (_sprite != null) _sprite.Play("idle");
		if (_label != null) _label.Text = DisplayName;
		if (IsLocal) EmitSignal(SignalName.Revived);
		Sim.Notice($"{DisplayName} begins again as a new hand off the ship.");
		return "";
	}

	private string TryAttack(Agent target)
	{
		if (target == null || !IsInstanceValid(target) || target == this) return "";
		var (dmg, range, _) = AttackProfile();
		float dist = GlobalPosition.DistanceTo(target.GlobalPosition);
		if (dist > range + 20f) return $"Too far for the {(Weapon != null ? Items.Items.Of(Weapon.Value).Name.ToLower() : "fist")}.";
		if (!CanAttack) return "";
		if (_sprite != null) _sprite.FlipH = target.GlobalPosition.X < GlobalPosition.X;
		bool hit = Attack(target);
		if (hit && target.IsDead && target is Animal dead && dead.IsDangerous) Task.Progress(TaskKind.DriveOffAnimals, 1);
		string what = Weapon != null ? Items.Items.Of(Weapon.Value).Name : "fists";
		if (hit) return target.IsDead ? $"{target.Describe()} is dead." : $"You strike {target.Describe()} with your {what.ToLower()}.";
		return "Missed.";
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
				if (!Consume(id)) return "";
				_oneShotAnim = "drink";
				return $"You drink the {def.Name.ToLower()}.";
			case ItemKind.Fuel:
			{
				var hearth = Sim.Structures?.HearthNear(GlobalPosition);
				if (hearth == null) return "No fire near enough to feed.";
				FeedFire(id);
				return $"You put a log on the fire. ({hearth.FuelHours:0.#} h of fuel)";
			}
			case ItemKind.Clothing:
				return $"You're wearing the {def.Name.ToLower()}. (+{def.Warmth:0} warmth)";
			case ItemKind.Tool when id == ItemId.SteelTrap:
			{
				var site = Sim.Ecology?.SiteNear(GlobalPosition, Ecology.Species.Beaver);
				if (site == null) return "No beaver lodge here. Traps go where the beaver are - along the river.";
				if (site.IsDead) return "This lodge is dead. Nothing will come to a trap here.";
				int n = Inventory.TakeUpTo(ItemId.SteelTrap, 6);
				var line = site.SetTraps(TrapOwner, n);
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
				return animal.Butcher(Inventory) ? $"You butcher the {animal.Describe().ToLower()}." : "Nothing more to take.";
			int moved = 0;
			foreach (var (id, n) in new List<(ItemId, int)>(body.Inventory.All())) { body.Inventory.Remove(id, n); Inventory.Add(id, n); moved += n; }
			return moved > 0 ? $"You take what {body.Describe()} carried." : "Nothing left on the body.";
		}

		var pickup = NearestPickup();
		if (pickup != null)
		{
			Inventory.Add(pickup.Item, pickup.Count);
			string label = pickup.Label;
			Sim.Spawner?.Consume(pickup);
			return $"Picked up {label}.";
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

		var bed = Sim.Structures?.NearestBed(GlobalPosition, 90f);
		if (bed != null)
		{
			var cal = GameCalendar.Instance;
			if (cal.IsSkipping) return "";
			bool night = cal.Hour >= 18f || cal.Hour < 6f;
			float hours = night ? ((7f - cal.Hour + 24f) % 24f) : 1f;
			return RequestSkip(hours, night ? "You turn in." : "You lie down for an hour.");
		}

		var fort = Sim.Fort;
		if (fort != null)
		{
			if (GlobalPosition.DistanceTo(IsoGrid.ToWorld(fort.WoodpileCell)) < 120f)
			{
				int n = Inventory.Count(ItemId.Firewood);
				if (n > 0) { Inventory.Remove(ItemId.Firewood, n); fort.Stores.Add(ItemId.Firewood, n); Task.Progress(TaskKind.Firewood, n); return $"You stack {n} firewood on the pile ({fort.Stores.Count(ItemId.Firewood)} there now)."; }
				int take = fort.Stores.TakeUpTo(ItemId.Firewood, 2); Inventory.Add(ItemId.Firewood, take);
				return take > 0 ? $"You take {take} firewood from the pile." : "The woodpile is bare.";
			}
			if (WorldLayout.Warehouse.Contains(Cell))
			{
				int moved = 0;
				foreach (var (id, n) in new List<(ItemId, int)>(Inventory.All()))
				{
					var kind = Items.Items.Of(id).Kind;
					if (kind == ItemKind.Weapon || kind == ItemKind.Clothing || kind == ItemKind.Fuel || kind == ItemKind.Tool) continue;
					if (kind == ItemKind.Food || kind == ItemKind.Drink) continue;
					Inventory.Remove(id, n); fort.Stores.Add(id, n); moved += n;
					if (kind == ItemKind.Goods) Task.Progress(TaskKind.CarryCargo, n);
				}
				return moved > 0 ? $"You stow {moved} lots in the warehouse." : "Nothing to stow. (Goods and furs go in here; keep your food and kit.)";
			}
			if (GlobalPosition.DistanceTo(IsoGrid.ToWorld(fort.AccessCell)) < 110f && Inventory.CountOfKind(ItemKind.Food) < 3)
			{
				if (fort.TakeRation(this, 2)) return "You draw a ration from the stores.";
			}
			var ship = Sim.Ship;
			if (ship != null && ship.IsDocked && GlobalPosition.DistanceTo(IsoGrid.ToWorld(WorldLayout.WharfEnd)) < 160f && ship.Cargo.SlotCount > 0)
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
