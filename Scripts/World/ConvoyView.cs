using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Agents.Goals;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.Player;
using IsometricFurTradeGame.Scripts.UI;

namespace IsometricFurTradeGame.Scripts.World;

// What a convoy looks like when somebody is near it (design doc 18.2: the
// near tier is the far tier made visible, not a second simulation). Every
// peer draws the boats from the same rows - the server builds them from the
// convoys' numbers each frame and sends them out a few times a second - and
// the server adds the men: a guide you can talk to and as many hands as the
// boats hold, real people for as long as you are close and numbers again
// when you are not.
//
// The server's own player can also take a place in the boats and let the
// days go by with them (the ride), and the boats stop him themselves at
// every carrying place and landing.
public partial class ConvoyView : Node2D
{
	public static ConvoyView I { get; private set; }

	private const float ViewPx = 4800f;     // boats are drawn inside this
	private const float MenIn = 2800f, MenOut = 3800f;   // men are real inside the first, numbers outside the second
	private const float BoatGap = 120f;

	private sealed class Craft
	{
		public int Id, State, BoatCount;
		public string Name = "", Kind = "canoe";
		public Vector2 Pos, Dir = Vector2.Right;
		public double Seen;
		public bool Placed;
		public float Length = 120f, Beam = 60f, CrewTop;   // the drawn boat's size; how far down its sheet the painted crew ends
		public readonly List<Sprite2D> Boats = new();
	}

	private readonly Dictionary<int, Craft> _crafts = new();
	private readonly HashSet<PlayerController> _riding = new();
	private readonly Dictionary<Npc, (Vector2 goal, float wait)> _wander = new();
	private Node _yparent;
	private float _syncTimer;
	private Label _hud;

	public override void _Ready()
	{
		I = this;
		_yparent = GetParent();
		var layer = new CanvasLayer { Layer = 4 };
		_hud = new Label { Visible = false, HorizontalAlignment = HorizontalAlignment.Center };
		_hud.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
		_hud.GrowHorizontal = Control.GrowDirection.Both;
		_hud.OffsetTop = 100f;
		_hud.AddThemeFontSizeOverride("font_size", 17);
		_hud.AddThemeColorOverride("font_color", new Color(1f, 0.92f, 0.75f));
		_hud.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.7f));
		layer.AddChild(_hud);
		AddChild(layer);
	}

	public override void _ExitTree()
	{
		if (I == this) I = null;
		foreach (var c in _crafts.Values) foreach (var b in c.Boats) if (IsInstanceValid(b)) b.QueueFree();
		_crafts.Clear();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		if (Net.Net.IsServer)
		{
			ServerStep(dt);
			RideStep();
		}
		double now = Time.GetTicksMsec() / 1000.0;
		foreach (int id in new List<int>(_crafts.Keys))
		{
			var c = _crafts[id];
			if (now - c.Seen > 3.0) { foreach (var b in c.Boats) if (IsInstanceValid(b)) b.QueueFree(); _crafts.Remove(id); continue; }
			Place(c, dt);
		}
		if (Net.Net.IsServer) PlaceMen(dt); else ClientRide();
		UpdateHud();
	}

	// ---- the server: which convoys are near, the rows they go out as, their men ----

	private void ServerStep(float dt)
	{
		var rows = new Godot.Collections.Array();
		foreach (var c in Convoys.All)
		{
			var where = c.Where;
			float d = float.MaxValue;
			foreach (var p in Sim.Players()) if (p != null && IsInstanceValid(p)) d = Mathf.Min(d, p.GlobalPosition.DistanceTo(where));
			if (d < MenIn) EnsureMen(c);
			else if (d > MenOut && c.Men.Count > 0) Convoys.DropMenOf(c);
			if (d > ViewPx) continue;
			var at = c.Moving || !c.HasCamp ? c.Afloat : c.CampAt;
			var heading = c.Heading;
			rows.Add(new Godot.Collections.Array { c.Id, at.X, at.Y, heading.X, heading.Y, (int)c.Now, c.Boats, c.Run.Craft.Name, c.Name });
		}
		if (rows.Count > 0) ApplyRows(rows);
		_syncTimer -= dt;
		if (_syncTimer <= 0f) { _syncTimer = 0.35f; if (rows.Count > 0) Net.Net.I?.BroadcastConvoys(rows); }
	}

	public static void Apply(Godot.Collections.Array rows) => I?.ApplyRows(rows);

	private void ApplyRows(Godot.Collections.Array rows)
	{
		double now = Time.GetTicksMsec() / 1000.0;
		foreach (var rv in rows)
		{
			var r = (Godot.Collections.Array)rv;
			int id = (int)r[0];
			if (!_crafts.TryGetValue(id, out var c)) _crafts[id] = c = new Craft { Id = id };
			c.Pos = new Vector2((float)r[1], (float)r[2]);
			c.Dir = new Vector2((float)r[3], (float)r[4]);
			c.State = (int)r[5];
			c.BoatCount = (int)r[6];
			string kind = (string)r[7];
			c.Name = (string)r[8];
			c.Seen = now;
			if (c.Boats.Count != c.BoatCount || c.Kind != kind) Rebuild(c, kind);
		}
	}

	private void Rebuild(Craft c, string kind)
	{
		foreach (var b in c.Boats) if (IsInstanceValid(b)) b.QueueFree();
		c.Boats.Clear();
		c.Kind = kind;
		c.Placed = false;
		var spec = Convoys.KindNamed(kind);
		var tex = spec != null && spec.Sprite != "" ? GD.Load<Texture2D>(spec.Sprite) : GD.Load<Texture2D>("res://assets/sprites/canoe.png");
		float scale = spec?.Scale ?? 0.6f;
		c.Length = tex.GetSize().X * scale;
		c.Beam = tex.GetSize().Y * scale;
		// The loaded sheets have their crew painted in, sitting above the
		// gunwale (the top third of the sheet).
		c.CrewTop = spec != null && spec.Sprite != "" ? 0.36f : 0f;
		for (int i = 0; i < c.BoatCount; i++)
		{
			var s = new Sprite2D { Texture = tex, Scale = Vector2.One * scale, Offset = new Vector2(0, -tex.GetSize().Y * 0.15f) };
			s.AddToGroup("convoy_craft");
			_yparent.AddChild(s);
			c.Boats.Add(s);
		}
	}

	// Line astern on the water; drawn up side by side on the bank.
	private void Place(Craft c, float dt)
	{
		var perp = new Vector2(-c.Dir.Y, c.Dir.X);
		bool afloat = c.State == (int)ConvoyState.Paddling;
		// Astern of one another a boat's length and a bit apart, not piled
		// into one; on the bank a beam apart.
		float astern = Mathf.Max(BoatGap, c.Length * 1.1f), abreast = Mathf.Max(BoatGap * 0.6f, c.Beam * 0.9f);
		for (int i = 0; i < c.Boats.Count; i++)
		{
			var b = c.Boats[i];
			if (!IsInstanceValid(b)) continue;
			// Drawn up on the bank the crew are out of her: only the hull is drawn.
			bool empty = !afloat && c.CrewTop > 0f && b.Texture != null;
			if (empty != b.RegionEnabled)
			{
				var size = b.Texture.GetSize();
				b.RegionEnabled = empty;
				b.RegionRect = new Rect2(0f, size.Y * c.CrewTop, size.X, size.Y * (1f - c.CrewTop));
				b.Offset = new Vector2(0, -size.Y * 0.15f + (empty ? size.Y * c.CrewTop * 0.5f : 0f));
			}
			var target = afloat ? c.Pos - c.Dir * astern * i : c.Pos + perp * abreast * (i - (c.Boats.Count - 1) * 0.5f);
			b.Position = c.Placed ? b.Position.Lerp(target, Mathf.Clamp(dt * 6f, 0f, 1f)) : target;
			b.FlipH = c.Dir.X < 0f;
			b.Rotation = 0f;
		}
		c.Placed = true;
	}

	// ---- the men -------------------------------------------------------------------------

	private static readonly string[] OrkneyFirst = { "John", "William", "James", "Thomas", "Peter", "Magnus", "Robert", "Alexander" };
	private static readonly string[] OrkneyLast = { "Isbister", "Flett", "Linklater", "Spence", "Rendall", "Sinclair", "Harcus", "Kirkness", "Tait", "Cromarty" };
	private static readonly string[] FrenchFirst = { "Baptiste", "Pierre", "Antoine", "Joseph", "Louis", "Francois", "Michel", "Jacques" };
	private static readonly string[] FrenchLast = { "Cadotte", "Lavallee", "Boucher", "Lafrance", "Houle", "Desrochers", "Beauchamp", "Plouffe", "Gagnon", "Morin" };

	private static string CrewName(Faction company)
	{
		var f = company == Faction.NWC ? FrenchFirst : OrkneyFirst;
		var l = company == Faction.NWC ? FrenchLast : OrkneyLast;
		return $"{f[(int)(GD.Randi() % (uint)f.Length)]} {l[(int)(GD.Randi() % (uint)l.Length)]}";
	}

	private void EnsureMen(Convoy c)
	{
		var pop = GetTree()?.GetFirstNodeInGroup("population") as Population;
		if (pop == null) return;
		// The dead stay where they fell, and the boats are a man short.
		for (int i = c.Men.Count - 1; i >= 0; i--)
		{
			var n = c.Men[i];
			if (n == null || !IsInstanceValid(n)) { c.Men.RemoveAt(i); continue; }
			if (n.IsDead) { c.Men.RemoveAt(i); c.Crew = Mathf.Max(1, c.Crew - 1); }
		}
		int want = 1 + Mathf.Min(Mathf.Max(0, c.Crew - 1), c.Moving ? Mathf.Min(c.Boats * 2, 8) : 6);
		var frames = GD.Load<SpriteFrames>(Agents.Appearance.VoyageurSheet);
		var at = c.Moving || !c.HasCamp ? c.Where : c.CampAt;
		while (c.Men.Count < want)
		{
			bool guide = c.Men.Count == 0;
			var cell = IsoGrid.FromWorld(at);
			var npc = pop.Spawn(guide ? c.Guide : CrewName(c.Company), Role.Voyageur, c.Company, cell, cell, null, frames, Colors.White, 0.8f);
			if (npc == null) break;
			npc.Age = guide ? GD.RandRange(38, 52) : GD.RandRange(18, 40);
			npc.Convoy = c;
			npc.GlobalPosition = at;
			npc.Brain.Clear(npc);
			npc.Brain.Push(npc, new ConvoyGoal());
			c.Men.Add(npc);
		}
		while (c.Men.Count > want)
		{
			var n = c.Men[^1];
			c.Men.RemoveAt(c.Men.Count - 1);
			if (n != null && IsInstanceValid(n) && !n.IsDead) n.QueueFree();
		}
	}

	// Sitting in the boats, or about the fire on the bank.
	private void PlaceMen(float dt)
	{
		foreach (var c in Convoys.All)
		{
			if (c.Men.Count == 0 || !_crafts.TryGetValue(c.Id, out var craft) || craft.Boats.Count == 0) continue;
			for (int i = 0; i < c.Men.Count; i++)
			{
				var n = c.Men[i];
				if (n == null || !IsInstanceValid(n)) continue;
				if (n.IsDead || n.Conversing || n.Brain.Top is not ConvoyGoal) { n.Visible = true; continue; }
				// Under way the painted crew are the men: the real ones ride
				// unseen with them, so the boats aren't crewed twice over.
				n.Visible = !c.Moving || craft.CrewTop <= 0f;
				if (c.Moving)
				{
					var boat = craft.Boats[i % craft.Boats.Count].Position;
					// Just in front of the boat's own line, so the y-sort draws him over her.
					n.GlobalPosition = boat + new Vector2(-22f + (i / craft.Boats.Count) * 44f, 4f);
					n.HoldPose("paddle");
					_wander.Remove(n);
					continue;
				}
				if (i == 0) { n.GlobalPosition = n.GlobalPosition.MoveToward(craft.Pos + new Vector2(70f, 30f), 90f * dt); continue; }
				(Vector2 goal, float wait) w = _wander.TryGetValue(n, out var cur) ? cur : (n.GlobalPosition, 0f);
				w.wait -= dt;
				if (w.wait <= 0f && n.GlobalPosition.DistanceTo(w.goal) < 6f)
				{
					float a = GD.Randf() * Mathf.Tau, r = 45f + GD.Randf() * 80f;
					w.goal = craft.Pos + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.5f);
					w.wait = 1f + GD.Randf() * 5f;
				}
				if (n.GlobalPosition.DistanceTo(w.goal) > 6f)
				{
					n.FaceToward(w.goal);
					n.GlobalPosition = n.GlobalPosition.MoveToward(w.goal, 38f * dt);
					if (GD.Randf() < dt * 1.5f) n.PlayAnimation("walk_side");
				}
				_wander[n] = w;
			}
		}
	}

	// ---- the ride -----------------------------------------------------------------------------

	private void RideStep()
	{
		var cal = GameCalendar.Instance;
		if (cal == null) return;
		_riding.RemoveWhere(p => !Convoys.Riders.ContainsKey(p));
		foreach (var p in new List<PlayerController>(Convoys.Riders.Keys))
		{
			var c = Convoys.Riders[p];
			if (!IsInstanceValid(p) || p.IsDead || c.Now == ConvoyState.Arrived || !Convoys.All.Contains(c)) { Convoys.Leave(p, ""); continue; }
			bool local = p == Sim.Player;   // the host's own player steps off on his key; a joined one asks (ride_off)
			bool typing = ChatBox.Instance?.IsTyping ?? false;
			bool stepping = local && !typing && Input.GetVector("move_left", "move_right", "move_up", "move_down") != Vector2.Zero;

			if (!c.Moving)
			{
				p.RideId = 0; p.TravellingServer = false;
				// Ashore with the brigade: your own legs until they push off.
				if (_riding.Remove(p))
				{
					if (local) cal.CancelSkip();
					p.SetStartPosition(Convoys.LandNear(c.Now == ConvoyState.Calling ? Convoys.Landing(c.At) : c.HasCamp ? c.CampAt : c.Where));
					p.Say(c.Now switch
					{
						ConvoyState.Portaging => $"{c.Name} come in to {c.At}. Out of the boat; the pieces go over on men's backs.",
						ConvoyState.Calling => $"{c.Name} are in at {c.At} for the night.",
						ConvoyState.Wintering => $"{c.Name} are frozen in. There is nothing for it but to wait for the ice.",
						_ => $"{c.Name} lie up on the bank.",
					});
				}
				else if (p.GlobalPosition.DistanceTo(c.HasCamp ? c.CampAt : c.Where) > 2600f)
					Convoys.Leave(p, $"You have wandered too far from {c.Name}; they go on without you.");
				continue;
			}

			// The boats are on the water.
			if (!_riding.Contains(p))
			{
				if (p.GlobalPosition.DistanceTo(c.Where) > 700f) { Convoys.Leave(p, $"{c.Name} pushed off without you."); continue; }
				_riding.Add(p);
				p.Say($"The boats push off for {c.ToName}. You take your place.");
			}
			if (stepping) { _riding.Remove(p); Convoys.Leave(p, "You step ashore."); continue; }
			p.RideId = c.Id;
			p.SetStartPosition(c.Afloat + new Vector2(0f, -24f));
			// The road goes by fast, except while a choice is put to him (World/JourneyIncidents).
			if (local) { if (p.Conversation.IsOpen) cal.CancelSkip(); else if (cal.SkipRemaining < 0.5f) cal.SkipHours(0.5f - cal.SkipRemaining); }
			else p.TravellingServer = true;   // a joined player cannot skip the shared day; it marches instead
			if (p.Needs.Hunger < 45f) Convoys.FeedRider(p, c);
			p.Needs.Thirst = 100f;   // a cup over the side: there is no want of water on a river
		}
	}

	// A joined player's machine: sit in the boats the server sent, and ask to
	// step ashore on the first step of your own.
	private void ClientRide()
	{
		if (Net.Net.IsServer || Sim.Player is not PlayerController me || me.RideId == 0) return;
		if (!_crafts.TryGetValue(me.RideId, out var c)) return;
		bool typing = ChatBox.Instance?.IsTyping ?? false;
		if (!typing && Input.GetVector("move_left", "move_right", "move_up", "move_down") != Vector2.Zero)
		{
			me.RideId = 0;
			Net.Net.I?.Act("ride_off");
			return;
		}
		me.GlobalPosition = c.Pos + new Vector2(0f, -24f);
	}

	private void UpdateHud()
	{
		var me = Sim.Player as PlayerController;
		int id = me?.RideId ?? 0;
		_hud.Visible = id != 0 && _crafts.ContainsKey(id);
		if (!_hud.Visible) return;
		var boardedNow = Convoys.BoardedBy(me);   // only the server knows how far there is to go
		string far = boardedNow != null ? $"{Mathf.RoundToInt(boardedNow.KmToGo)} km to {boardedNow.ToName}  -  " : "";
		_hud.Text = $"With {_crafts[id].Name}  -  {far}{GameCalendar.Instance?.DateString}  -  any step takes you ashore";
	}

	// ---- for the prompt and the interaction ------------------------------------------------------

	public static int NearestId(Vector2 pos, float reach)
	{
		if (I == null) return 0;
		int best = 0; float bd = reach;
		foreach (var c in I._crafts.Values)
			foreach (var b in c.Boats)
			{
				if (!IsInstanceValid(b)) continue;
				float d = b.Position.DistanceTo(pos);
				if (d < bd) { bd = d; best = c.Id; }
			}
		return best;
	}

	public static string NameOf(int id) => I != null && I._crafts.TryGetValue(id, out var c) ? c.Name : "the boats";
}

// A man of the brigade keeps to his boat: ConvoyView puts him there, and this
// goal only holds the place (and the belly) so that the ordinary day's
// routines don't send him off to bed or to the fishing hole.
public class ConvoyGoal : Goal
{
	public override string Label => "with the brigade";

	public override GoalStatus Tick(Npc npc, float dt, float hours)
	{
		npc.Needs.Hunger = Mathf.Max(npc.Needs.Hunger, 60f);
		npc.Needs.Thirst = 100f;
		npc.Needs.Warmth = Mathf.Max(npc.Needs.Warmth, 60f);
		return GoalStatus.Running;
	}
}
