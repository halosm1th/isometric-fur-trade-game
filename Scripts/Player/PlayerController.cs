using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Player;

// The player is an Agent driven by input instead of a goal stack.
public partial class PlayerController : Agent
{
	[Signal] public delegate void NoticeEventHandler(string text);

	private const float PickupReach = 100f;

	private AnimatedSprite2D _sprite;
	private bool _busy;   // eating or drinking: no walking until the animation ends
	private UI.DialogueMenu _dialogue;

	public Npc Spouse;

	// Standing with the Company (design doc 3.6): slow, bureaucratic, reviewed by results.
	public float Standing = 10f;
	public readonly PlayerTask Task = new();
	public readonly System.Collections.Generic.HashSet<string> KnownSites = new();
	private int _lastTaskDay = -1;

	// The Factor's morning orders for the new hand.
	public void AssignTask(TaskKind kind, int need)
	{
		var cal = GameCalendar.Instance;
		if (Task.Kind != TaskKind.None && !Task.IsComplete && Task.DayAssigned != cal.Day) { Standing -= 2f; EmitSignal(SignalName.Notice, "Yesterday's task went undone. Mr. Auld noticed."); }
		if (Task.Kind != TaskKind.None && Task.IsComplete) Standing += 5f;
		Task.Kind = kind; Task.Need = need; Task.Done = 0; Task.DayAssigned = cal.Day;
		if (kind != TaskKind.None) EmitSignal(SignalName.Notice, Task.Describe());
	}

	public override void _Ready()
	{
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		DisplayName = "Thomas Ross";
		Faction = Faction.HBC;
		Sim.Player = this;
		CallDeferred(nameof(FindDialogue));
		GetTree().CreateTimer(1.5).Timeout += () =>
		{
			if (!IsInstanceValid(this)) return;
			EmitSignal(SignalName.Notice, "York Factory, Hudson's Bay. You are the Company's new hand. E talks, 1-9 uses, left click strikes, F1 opens the test console.");
		};
		_sprite.AnimationFinished += () => { string a = _sprite.Animation; if (a == "eat" || a == "drink") _busy = false; };

		// What a new hand stepping off the ship would carry.
		Inventory.Add(ItemId.Pemmican, 4);
		Inventory.Add(ItemId.DriedGoose, 2);
		Inventory.Add(ItemId.Firewood, 4);
		Inventory.Add(ItemId.Capote, 1);
		Inventory.Add(ItemId.Knife, 1);
		Inventory.Add(ItemId.TradeGoods, 6);
	}

	private void FindDialogue() => _dialogue = GetTree().GetFirstNodeInGroup("dialogue") as UI.DialogueMenu;

	public override void _UnhandledInput(InputEvent @event)
	{
		if (IsDead)
		{
			if (@event.IsActionPressed("restart"))
			{
				GameCalendar.Instance.Reset();
				GetTree().ReloadCurrentScene();
			}
			return;
		}
		if (_busy) return;
		if (_dialogue != null && _dialogue.IsOpen) return;

		// Left click: strike whoever is under the cursor with what you carry.
		if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
		{
			var target = AgentUnderCursor();
			if (target != null) TryAttack(target);
			return;
		}
		if (@event.IsActionPressed("attack_nearest"))
		{
			Agent best = null; float bestD = 400f;
			foreach (var a in Sim.AgentsWithin(GlobalPosition, 400f))
			{
				if (a == this || !IsHostileTo(a)) continue;
				float d = GlobalPosition.DistanceTo(a.GlobalPosition);
				if (d < bestD) { bestD = d; best = a; }
			}
			if (best != null) TryAttack(best); else EmitSignal(SignalName.Notice, "Nothing hostile within reach.");
			return;
		}

		if (@event.IsActionPressed("interact"))
		{
			Interact();
		}
		else if (@event.IsActionPressed("drink"))
		{
			EmitSignal(SignalName.Notice, Drink() ? "You drink from the water." : "No water within reach.");
		}
	}

	private Agent AgentUnderCursor()
	{
		var mouse = GetGlobalMousePosition();
		Agent best = null; float bestD = 70f;
		foreach (var a in Sim.AgentsWithin(mouse, 120f))
		{
			if (a == this) continue;
			// Sprites stand above their feet; test against the body's middle.
			float d = (a.GlobalPosition + new Vector2(0, -40)).DistanceTo(mouse);
			if (d < bestD) { bestD = d; best = a; }
		}
		return best;
	}

	private void TryAttack(Agent target)
	{
		var (dmg, range, _) = AttackProfile();
		float dist = GlobalPosition.DistanceTo(target.GlobalPosition);
		if (dist > range + 20f) { EmitSignal(SignalName.Notice, $"Too far for the {(Weapon != null ? Items.Items.Of(Weapon.Value).Name.ToLower() : "fist")}."); return; }
		if (!CanAttack) return;
		_sprite.FlipH = target.GlobalPosition.X < GlobalPosition.X;
		bool hit = Attack(target);
		if (hit && target.IsDead && target is Animal dead && dead.IsDangerous) Task.Progress(TaskKind.DriveOffAnimals, 1);
		string what = Weapon != null ? Items.Items.Of(Weapon.Value).Name : "fists";
		if (hit) EmitSignal(SignalName.Notice, target.IsDead ? $"{target.Describe()} is dead." : $"You strike {target.Describe()} with your {what.ToLower()}.");
		else EmitSignal(SignalName.Notice, "Missed.");
	}

	// E: talk to whoever is close, else pick up, loot a body, use the woodpile
	// or warehouse, work a door, look at a fire.
	private void Interact()
	{
		var npc = NearestNpc(110f);
		if (npc != null && !npc.IsDead && _dialogue != null)
		{
			npc.FaceToward(GlobalPosition);
			_dialogue.Open(npc);
			return;
		}
		var body = NearestCorpse(110f);
		if (body != null)
		{
			if (body is Animal animal)
			{
				EmitSignal(SignalName.Notice, animal.Butcher(Inventory) ? $"You butcher the {animal.Describe().ToLower()}." : "Nothing more to take.");
			}
			else
			{
				int moved = 0;
				foreach (var (id, n) in new System.Collections.Generic.List<(ItemId, int)>(body.Inventory.All())) { body.Inventory.Remove(id, n); Inventory.Add(id, n); moved += n; }
				EmitSignal(SignalName.Notice, moved > 0 ? $"You take what {body.Describe()} carried." : "Nothing left on the body.");
			}
			return;
		}

		var pickup = NearestPickup();
		if (pickup != null)
		{
			Inventory.Add(pickup.Item, pickup.Count);
			EmitSignal(SignalName.Notice, $"Picked up {pickup.Label}.");
			pickup.QueueFree();
			return;
		}

		// The Company's woodpile and warehouse take what you carry.
		var fort = Sim.Fort;
		if (fort != null)
		{
			if (GlobalPosition.DistanceTo(IsoGrid.ToWorld(fort.WoodpileCell)) < 120f)
			{
				int n = Inventory.Count(ItemId.Firewood);
				if (n > 0) { Inventory.Remove(ItemId.Firewood, n); fort.Stores.Add(ItemId.Firewood, n); Task.Progress(TaskKind.Firewood, n); EmitSignal(SignalName.Notice, $"You stack {n} firewood on the pile ({fort.Stores.Count(ItemId.Firewood)} there now)."); }
				else { int take = fort.Stores.TakeUpTo(ItemId.Firewood, 2); Inventory.Add(ItemId.Firewood, take); EmitSignal(SignalName.Notice, take > 0 ? $"You take {take} firewood from the pile." : "The woodpile is bare."); }
				return;
			}
			if (WorldLayout.Warehouse.Contains(Cell))
			{
				int moved = 0;
				foreach (var (id, n) in new System.Collections.Generic.List<(ItemId, int)>(Inventory.All()))
				{
					var kind = Items.Items.Of(id).Kind;
					if (kind == ItemKind.Weapon || kind == ItemKind.Clothing || kind == ItemKind.Fuel) continue;
					if (kind == ItemKind.Food || kind == ItemKind.Drink) continue;   // keep your own rations
					Inventory.Remove(id, n); fort.Stores.Add(id, n); moved += n;
					if (kind == ItemKind.Goods) Task.Progress(TaskKind.CarryCargo, n);
				}
				EmitSignal(SignalName.Notice, moved > 0 ? $"You stow {moved} lots in the warehouse." : "Nothing to stow. (Goods and furs go in here; keep your food and kit.)");
				return;
			}
			if (GlobalPosition.DistanceTo(IsoGrid.ToWorld(fort.AccessCell)) < 110f && Inventory.CountOfKind(ItemKind.Food) < 3)
			{
				if (fort.TakeRation(this, 2)) { EmitSignal(SignalName.Notice, "You draw a ration from the stores."); return; }
			}
			var ship = Sim.Ship;
			if (ship != null && ship.IsDocked && GlobalPosition.DistanceTo(IsoGrid.ToWorld(WorldLayout.WharfEnd)) < 160f && ship.Cargo.SlotCount > 0)
			{
				int carried = 0;
				foreach (var (id, n) in new System.Collections.Generic.List<(ItemId, int)>(ship.Cargo.All()))
				{
					int take = Mathf.Min(n, 4 - carried); if (take <= 0) break;
					ship.Cargo.Remove(id, take); Inventory.Add(id, take); carried += take;
				}
				EmitSignal(SignalName.Notice, $"You shoulder {carried} lots from the ship. Take them to the warehouse.");
				return;
			}
		}

		var structures = Sim.Structures;
		if (structures == null) return;
		if (structures.TryToggleDoorNear(Position)) return;

		var hearth = structures.HearthNear(GlobalPosition);
		if (hearth != null)
		{
			EmitSignal(SignalName.Notice, FeedFire(ItemId.Firewood)
				? $"You put a log on the fire. ({hearth.FuelHours:0.#} h of fuel)"
				: (Inventory.Has(ItemId.Firewood) ? "" : "No firewood to burn."));
		}
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
			if (node is not Agent a || !IsInstanceValid(a)) continue;
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

	// Hotbar: use whatever sits in slot i. Returns a line for the HUD.
	public string UseSlot(int i)
	{
		if (_busy || IsDead) return "";
		var slot = Inventory.Slot(i);
		if (slot == null) return "";
		var (id, _) = slot.Value;
		var def = Items.Items.Of(id);

		switch (def.Kind)
		{
			case ItemKind.Food:
				if (!Consume(id)) return "";
				_busy = true;
				_sprite.Play("eat");
				return $"You eat the {def.Name.ToLower()}.";

			case ItemKind.Drink:
				if (!Consume(id)) return "";
				_busy = true;
				_sprite.Play("drink");
				return $"You drink the {def.Name.ToLower()}.";

			case ItemKind.Fuel:
				var structures = Sim.Structures;
				var hearth = structures?.HearthNear(GlobalPosition);
				if (hearth == null) return "No fire near enough to feed.";
				FeedFire(id);
				return $"You put a log on the fire. ({hearth.FuelHours:0.#} h of fuel)";

			case ItemKind.Clothing:
				return $"You're wearing the {def.Name.ToLower()}. (+{def.Warmth:0} warmth)";

			case ItemKind.Weapon:
				PreferredWeapon = id;
				return $"You ready the {def.Name.ToLower()} (damage {def.Damage:0}, reach {def.Range:0}).";

			case ItemKind.Fur:
			case ItemKind.Goods:
				return $"{def.Name}: worth {def.ValueMb:0.#} MB at the counter.";
		}
		return "";
	}

	public override void _PhysicsProcess(double delta)
	{
		IsSprinting = Input.IsActionPressed("sprint");
		base._PhysicsProcess(delta);

		if (IsDead)
		{
			if (_sprite.Animation != "death") _sprite.Play("death");
			return;
		}
		if (_busy)
		{
			Velocity = Vector2.Zero;
			return;
		}

		_sprite.Modulate = HurtFlash > 0f ? new Color(1f, 0.45f, 0.45f) : Colors.White;
		Vector2 input = Input.GetVector("move_left", "move_right", "move_up", "move_down");
		Velocity = input * EffectiveSpeed;

		if (input.X != 0)
		{
			_sprite.FlipH = input.X < 0;
		}

		// Walking away from the camera uses the back-view cycle.
		string target = input == Vector2.Zero ? "idle" : (input.Y < -0.3f ? "walk_back" : "walk");
		if (_sprite.Animation != target)
		{
			_sprite.Play(target);
		}
		_sprite.SpeedScale = IsSprinting && input != Vector2.Zero ? 1.6f : 1f;

		MoveAndSlide();
	}
}
