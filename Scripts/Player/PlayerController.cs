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

	public override void _Ready()
	{
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_sprite.AnimationFinished += () => { string a = _sprite.Animation; if (a == "eat" || a == "drink") _busy = false; };

		// What a new hand stepping off the ship would carry.
		Inventory.Add(ItemId.Pemmican, 4);
		Inventory.Add(ItemId.DriedGoose, 2);
		Inventory.Add(ItemId.Firewood, 4);
		Inventory.Add(ItemId.Capote, 1);
	}

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

		if (@event.IsActionPressed("interact"))
		{
			Interact();
		}
		else if (@event.IsActionPressed("drink"))
		{
			EmitSignal(SignalName.Notice, Drink() ? "You drink from the water." : "No water within reach.");
		}
	}

	// E: pick up what's at your feet, else work a door, else tend a fire.
	private void Interact()
	{
		var pickup = NearestPickup();
		if (pickup != null)
		{
			Inventory.Add(pickup.Item, pickup.Count);
			EmitSignal(SignalName.Notice, $"Picked up {pickup.Label}.");
			pickup.QueueFree();
			return;
		}

		var structures = GetTree().GetFirstNodeInGroup("structures") as StructurePainter;
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
				var structures = GetTree().GetFirstNodeInGroup("structures") as StructurePainter;
				var hearth = structures?.HearthNear(GlobalPosition);
				if (hearth == null) return "No fire near enough to feed.";
				FeedFire(id);
				return $"You put a log on the fire. ({hearth.FuelHours:0.#} h of fuel)";

			case ItemKind.Clothing:
				return $"You're wearing the {def.Name.ToLower()}. (+{def.Warmth:0} warmth)";
		}
		return "";
	}

	public override void _PhysicsProcess(double delta)
	{
		IsSprinting = Input.IsActionPressed("sprint");
		base._PhysicsProcess(delta);

		if (IsDead)
		{
			_sprite.Play("idle");
			return;
		}
		if (_busy)
		{
			Velocity = Vector2.Zero;
			return;
		}

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
