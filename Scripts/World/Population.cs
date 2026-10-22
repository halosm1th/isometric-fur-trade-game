using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.Items;

namespace IsometricFurTradeGame.Scripts.World;

// Puts the people into the world: the post's complement, the Cree band at
// the camp, and (on demand) a ship's crew. Everyone is named and has a bed.
public partial class Population : Node2D
{
	[Export] public PackedScene NpcScene;

	private static readonly string[] OrkneyMen =
	{
		"James Isbister", "William Flett", "Magnus Spence", "John Linklater", "Thomas Corrigal",
		"Peter Sinclair", "Andrew Setter", "George Tait", "Robert Garrick", "Malcolm Sabiston",
	};
	// Placeholder Cree given names (Cree words in common romanisation), to be
	// replaced with properly researched period names.
	private static readonly string[] CreeMen = { "Maskwa", "Wapistan", "Mahihkan", "Pisiskiw", "Kihiw", "Mistatim" };
	private static readonly string[] Sailors = { "Bos'n Hurst", "Ned Carrow", "Tom Ellery", "Will Pascoe", "Sam Rudd", "Jack Tremayne", "Davy Roskilly", "Harry Penrose" };

		private SpriteFrames _hbcFrames, _factorFrames, _creeFrames, _labourerFrames, _creeWomanFrames, _assiniboineFrames;

	public override void _Ready()
	{
		if (Engine.IsEditorHint()) return;
		_hbcFrames = GD.Load<SpriteFrames>("res://Scenes/Player/PlayerFrames.tres");
		_factorFrames = GD.Load<SpriteFrames>("res://Scenes/Npc/FactorFrames.tres");
		_creeFrames = GD.Load<SpriteFrames>("res://Scenes/Npc/CreeFrames.tres");
		_labourerFrames = GD.Load<SpriteFrames>("res://Scenes/Npc/LabourerFrames.tres");
		_creeWomanFrames = GD.Load<SpriteFrames>("res://Scenes/Npc/CreeWomanFrames.tres");
		_assiniboineFrames = GD.Load<SpriteFrames>("res://Scenes/Npc/AssiniboineFrames.tres");
		foreach (string arg in OS.GetCmdlineUserArgs()) if (arg == "--trace") _trace = true;
		Core.Director.Reset();
		Economy.Barter.Forget();
		Core.Director.ReadArgs();
		// Dev aid: `-- --shot=SECONDS` saves what's on screen to user://shot_N.png
		// every SECONDS (up to six), so a run can be checked without a window capture.
		foreach (string arg in OS.GetCmdlineUserArgs())
			if (arg.StartsWith("--shot=") && float.TryParse(arg.Substring(7), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float every))
				for (int i = 1; i <= 6; i++) { int n = i; GetTree().CreateTimer(every * i).Timeout += () => { var path = ProjectSettings.GlobalizePath($"user://shot_{n}.png"); GetViewport().GetTexture().GetImage().SavePng(path); GD.Print($"[shot] {path}"); }; }
		// Dev aid: `-- --kill=Name` - that person dies five seconds in (to watch
		// a household take a loss: mourning, the empty bunk, the sale of effects).
		foreach (string arg in OS.GetCmdlineUserArgs())
			if (arg.StartsWith("--kill=")) { string who = arg.Substring(7); GetTree().CreateTimer(5.0).Timeout += () => { foreach (var n in Sim.Npcs) if (n.DisplayName == who && !n.IsDead) { n.TakeDamage(9999f, null); break; } }; }
		// Dev aid: `-- --son` - the player has a grown son at the post, so the
		// nearer line of succession (design doc P.6) can be watched.
		foreach (string arg in OS.GetCmdlineUserArgs())
			if (arg == "--son") GetTree().CreateTimer(6.0).Timeout += () => {
				foreach (var pl in Sim.Players())
				{
					var home = pl.HomePost ?? (Household)Sim.Fort;
					var son = Spawn("William " + Agents.Heirs.Surname(pl.DisplayName), Role.Labourer, Faction.HBC, IsoGrid.Cell(WorldLayout.MensHouse.Top, 2, 3), IsoGrid.Cell(WorldLayout.MensHouse.Top, 2, 3), home, _labourerFrames, new Color(1f, 0.95f, 0.9f), 0.78f);
					son.Age = 18; son.FatherName = pl.DisplayName;
					GD.Print($"[dev] {son.DisplayName}, {pl.DisplayName}'s son, is grown at the post");
				}
			};
		// Dev aid: `-- --brawl` sets the two houses' armed men on each other by
		// the Company's gate, to watch a fight turn on nerve (design doc 15.6).
		foreach (string arg in OS.GetCmdlineUserArgs())
			if (arg == "--brawl") GetTree().CreateTimer(8.0).Timeout += () => {
				var ours = new List<Npc>(); var theirs = new List<Npc>();
				foreach (var n in Sim.Npcs)
				{
					if (n.IsDead || !n.IsFighter) continue;
					if (n.Faction == Faction.HBC && ours.Count < 3) ours.Add(n);
					else if (n.Faction == Faction.NWC && theirs.Count < 3) theirs.Add(n);
				}
				if (ours.Count == 0 || theirs.Count == 0) { GD.Print("[dev] nobody to brawl"); return; }
				var ground = IsoGrid.ToWorld(WorldLayout.GateOutside);
				for (int i = 0; i < ours.Count; i++) { ours[i].GlobalPosition = ground + new Vector2(-120, i * 70 - 70); ours[i].Brain.Clear(ours[i]); }
				for (int i = 0; i < theirs.Count; i++) { theirs[i].GlobalPosition = ground + new Vector2(120, i * 70 - 70); theirs[i].Brain.Clear(theirs[i]); }
				for (int i = 0; i < ours.Count; i++) ours[i].Brain.Push(ours[i], new Agents.Goals.AttackGoal(theirs[i % theirs.Count]));
				for (int i = 0; i < theirs.Count; i++) theirs[i].Brain.Push(theirs[i], new Agents.Goals.AttackGoal(ours[i % ours.Count]));
				GD.Print($"[dev] {ours.Count} of the Company against {theirs.Count} Nor'Westers at the gate");
			};
		// Dev aid: `-- --misdeed=Name` - five seconds in, that person is seen to
		// rob the traps right where the player stands (the player is a witness).
		foreach (string arg in OS.GetCmdlineUserArgs())
			if (arg.StartsWith("--misdeed=")) { string who = arg.Substring(10); GetTree().CreateTimer(5.0).Timeout += () => { foreach (var n in Sim.Npcs) if (n.DisplayName.Contains(who) && !n.IsDead && Sim.Player != null) { Chronicle.Event($"{n.DisplayName} robbed the traps at the beaver lodge.", Sim.Player.GlobalPosition, n, n.DisplayName, 1.5f); GD.Print($"[dev] you saw {n.DisplayName} rob the traps"); break; } }; }
		// Dev aid: `-- --yearstory=S` writes the year as it will be told, S seconds in.
		foreach (string arg in OS.GetCmdlineUserArgs())
			if (arg.StartsWith("--yearstory=") && double.TryParse(arg.Substring(12), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double ys)) GetTree().CreateTimer(ys).Timeout += () => { if (Sim.Player is Player.PlayerController yp) Core.YearStory.Write(yp); };
		// Dev aid: `-- --befriend=Name` - that person counts the local player a
		// close friend from the start (rapport 60, a hard winter shared).
		foreach (string arg in OS.GetCmdlineUserArgs())
			if (arg.StartsWith("--befriend=")) { string who = arg.Substring(11); GetTree().CreateTimer(3.0).Timeout += () => Befriend(who); }
		// Dev aid: `-- --liar=Name` makes that person a liar by temper (loyalty
		// 0.05, ambition 0.95); `--liar=Name:spite` also has him hate the player.
		foreach (string arg in OS.GetCmdlineUserArgs())
			if (arg.StartsWith("--liar=")) { string spec = arg.Substring(7); GetTree().CreateTimer(3.0).Timeout += () => MakeLiar(spec); }
		// Dev aid: `-- --converse=SECONDS:Name>option>option` - at that time the
		// local player steps up to Name, opens a conversation and picks each
		// option whose label contains the text, printing every step as [talk].
		// Several may be given. A way to drive dialogue without a keyboard.
		foreach (string arg in OS.GetCmdlineUserArgs())
			if (arg.StartsWith("--converse=")) _conversations.Add(arg.Substring(11));
		// Dev aid: `-- --rapport=Name:40` - that person thinks this well of the
		// local player from the start (to reach talk that wants trust).
		foreach (string arg in OS.GetCmdlineUserArgs())
			if (arg.StartsWith("--rapport=") && arg.LastIndexOf(':') is int colon and > 10 && float.TryParse(arg.Substring(colon + 1), out float amount))
			{
				string who = arg.Substring(10, colon - 10);
				GetTree().CreateTimer(3.0).Timeout += () => { foreach (var n in Sim.Npcs) if (n.DisplayName == who && Sim.Player is Agent p) { n.ChangeRapport(p, amount); GD.Print($"[dev] {who} thinks {n.RapportWith(p):0} of you"); } };
			}
		if (Net.Net.IsServer) CallDeferred(nameof(SpawnAll));
	}

	private void SpawnAll()
	{
		if (SaveGame.HasPending) { SaveGame.Restore(this); return; }
		// The starting cast is placed in the editor (Cast/*, NpcSpawn markers).
		var made = new Dictionary<NpcSpawn, Npc>();
		foreach (var node in GetTree().GetNodesInGroup(NpcSpawn.Group))
		{
			if (node is not NpcSpawn m) continue;
			var home = m.Household();
			if (home == null) continue;   // that household isn't in this world
			var npc = Spawn(m.DisplayName, m.Role, m.Faction, m.Shifted(m.BedCell, home), m.Shifted(m.WorkCell, home), home, m.Frames ?? _hbcFrames, m.Tint, m.SpriteScale, m.Female);
			if (m.Age > 0) { npc.Age = m.Age; if (npc.Age < 16) npc.ApplyAppearance(); }
			m.FillInventory(npc);
			if (npc.Inventory.Has(ItemId.Musket) && !npc.Inventory.Has(ItemId.Powder)) npc.Inventory.Add(ItemId.Powder);
			if (m.Leader) home.Leader = npc;
			made[m] = npc;
		}
		foreach (var (m, npc) in made)
		{
			if (m.Spouse != null && made.TryGetValue(m.Spouse, out var sp)) { npc.Spouse = sp; sp.Spouse = npc; }
			if (m.Father != null && made.TryGetValue(m.Father, out var fa)) { npc.Father = fa; if (!fa.Children.Contains(npc)) fa.Children.Add(npc); }
		}

		Posts.StaffUnmarked(this);
		Towns.SeedStores();
		Stress();
		Sim.Notice($"York Factory: {Sim.Npcs.Count} souls in the world.");
	}

	// A marriage bears children (design doc 8, P.6): a year or so in, if
	// the two of them have actually been together, a child is born at the
	// post and grows up in the country.
	private int _birthDay = -1;
	private void Births()
	{
		var cal = GameCalendar.Instance;
		if (cal == null || cal.Day == _birthDay) return;
		_birthDay = cal.Day;
		foreach (var p in Sim.Players())
		{
			if (p.IsDead || p.Spouse is not Npc wife || wife.IsDead) continue;
			if (p.MarriedOnDay < 0) { p.MarriedOnDay = cal.Day; continue; }
			int together = cal.Day - p.MarriedOnDay;
			if (together < 300 || GD.Randf() > 0.02f) continue;
			p.MarriedOnDay = cal.Day;
			bool boy = GD.Randf() < 0.52f;
			string[] boys = { "William", "James", "Thomas", "John", "Robert" };
			string[] girls = { "Nancy", "Isabella", "Margaret", "Christina" };
			string name = (boy ? boys[(int)(GD.Randi() % 5u)] : girls[(int)(GD.Randi() % 4u)]) + " " + Heirs.Surname(p.DisplayName);
			var home = p.HomePost ?? (Household)Sim.Fort;
			var child = Spawn(name, boy ? Role.Labourer : Role.Woman, Faction.HBC, wife.BedCell, wife.WorkCell, home, boy ? _labourerFrames : _hbcFrames, new Color(1f, 0.95f, 0.9f), 0.6f, !boy);
			child.Age = 0;
			child.FatherName = p.DisplayName;
			child.Father = null;
			child.Spouse = null;
			child.ApplyAppearance();   // now it knows it is a child
			wife.Children.Add(child);
			child.Skills.Seed(0.1f, 0.1f, 0.1f, 0.1f);
			Sim.Headline($"{wife.DisplayName} has borne {p.DisplayName} a {(boy ? "son" : "daughter")}: {name}.");
			Core.Reveal.Lands(p, $"You have a {(boy ? "son" : "daughter")}, {name}.", "", 1.6f);
			foreach (var n in Sim.Npcs) if (n.HomeStores == wife.Father?.HomeStores) n.ChangeRapport(p, 8f);
		}
	}

	// Fever spreads between people who stand together (design doc 13.5),
	// and everyone is a year older each New Year (design doc 9.4).
	private float _contagionTimer;
	private int _lastYear = -1;
	private void SicknessAndYears(float hours)
	{
		var cal = GameCalendar.Instance;
		if (cal == null) return;
		if (_lastYear < 0) _lastYear = cal.Year;
		if (cal.Year != _lastYear)
		{
			_lastYear = cal.Year;
			foreach (var n in Sim.Npcs)
			{
				if (n.IsDead) continue;
				n.Age++; n.Immune = false;
				if (n.Age > 45) n.MoveSpeed = Mathf.Max(80f, n.MoveSpeed * 0.97f);
				// Children of the country grow into the post (design doc 9.4).
				// A town child of fourteen goes to the family's work: the son to
				// the fields or his father's trade, the daughter to the house.
				if (n.Role == Role.Child && n.Age >= 14)
				{
					n.Role = n.IsFemale ? Role.Woman : n.Father is { Role: Role.Farmer } ? Role.Farmer : n.Father is { Role: Role.Artisan } ? Role.Artisan : Role.Labourer;
					n.ResetBehaviour(); n.Brain.Clear(n);
					n.ApplyAppearance();
				}
				if (n.FatherName != "" && n.Age <= 16)
				{
					n.SetAppearance(GD.Load<SpriteFrames>(n.FramesPath), n.Tint, Mathf.Lerp(0.6f, 0.8f, Mathf.Clamp(n.Age / 16f, 0f, 1f)));
					if (n.Age == 16) Sim.Notice($"{n.DisplayName} is grown and takes a man's work at the post.");
				}
			}
			foreach (var p in Sim.Players()) { p.Age++; p.Immune = false; }
		}
		_contagionTimer -= hours;
		if (_contagionTimer > 0f) return;
		_contagionTimer = 1f;
		var everyone = new List<Agent>();
		foreach (var n in Sim.Npcs) if (!n.IsDead) everyone.Add(n);
		foreach (var p in Sim.Players()) if (!p.IsDead) everyone.Add(p);
		foreach (var a in everyone)
		{
			if (!a.IsSick) continue;
			foreach (var b in everyone)
			{
				if (b == a || b.IsSick || b.Immune) continue;
				if (a.GlobalPosition.DistanceTo(b.GlobalPosition) > 130f) continue;
				float chance = b.IsSheltered ? 0.10f : 0.05f;
				if (GD.Randf() < chance) b.CatchFever();
			}
		}
	}

	// "SECONDS:..." real seconds after start, or "hHOURS:..." game hours after it.
	private readonly System.Collections.Generic.List<string> _conversations = new();
	private float _startHours = -1f;
	private float _startSeconds;
	private void RunConversations()
	{
		if (_conversations.Count == 0) return;
		float now = GameCalendar.Instance.DayOfYear * 24f;
		if (_startHours < 0f) { _startHours = now; _startSeconds = Time.GetTicksMsec() / 1000f; }
		for (int i = _conversations.Count - 1; i >= 0; i--)
		{
			string script = _conversations[i];
			int colon = script.IndexOf(':');
			string when = colon > 0 ? script.Substring(0, colon) : "";
			bool hours = when.StartsWith("h");
			if (!float.TryParse(hours ? when.Substring(1) : when, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float at)) { _conversations.RemoveAt(i); continue; }
			if ((hours ? now - _startHours : Time.GetTicksMsec() / 1000f - _startSeconds) < at) continue;
			// A "*" target waits until someone fits; anything else goes once.
			if (Converse(script.Substring(colon + 1)) || !script.Substring(colon + 1).StartsWith("*")) _conversations.RemoveAt(i);
		}
	}

	private static void MakeLiar(string spec)
	{
		var parts = spec.Split(':');
		foreach (var n in Sim.Npcs)
		{
			if (!n.DisplayName.Contains(parts[0]) || n.IsDead) continue;
			n.Personality.Loyalty = 0.05f; n.Personality.Ambition = 0.95f;
			if (parts.Length > 1 && parts[1] == "spite" && Sim.Player is Player.PlayerController p) n.Rapport[p] = -40f;
			GD.Print($"[dev] {n.DisplayName} is a liar by temper{(parts.Length > 1 ? " and hates you" : "")}");
			return;
		}
	}

	private static void Befriend(string who)
	{
		if (Sim.Player is not Player.PlayerController p) return;
		foreach (var n in Sim.Npcs)
		{
			if (!n.DisplayName.Contains(who) || n.IsDead) continue;
			n.Rapport[p] = 60f;
			for (int i = 0; i < 2; i++)
				n.Memories.Add(new MemoryEntry { Text = $"{n.DisplayName} and {p.DisplayName} came through a hard winter together.", Subject = p.DisplayName, Day = 1f, Directness = 1, Weight = 1.2f, Key = $"befriend{i}{n.DisplayName}", Witnesses = new System.Collections.Generic.List<string> { n.DisplayName, p.DisplayName } });
			GD.Print($"[dev] {n.DisplayName} is {p.DisplayName}'s friend");
			return;
		}
	}

	private static bool Converse(string script)
	{
		var steps = script.Split('>');
		// "*scene": the prologue now open (Core/Starts); waits until it is.
		if (steps[0] == "*scene")
		{
			if (Sim.Player is not Player.PlayerController sp || !sp.Conversation.IsOpen) return false;
			var st = sp.Conversation.State();
			PrintTalk(st);
			for (int i = 1; i < steps.Length; i++)
			{
				var opts = st["options"].AsGodotArray();
				int pick = -1;
				for (int o = 0; o < opts.Count; o++) if (opts[o].AsString().Contains(steps[i], System.StringComparison.OrdinalIgnoreCase)) { pick = o; break; }
				if (pick < 0) { GD.Print($"[talk] no option like '{steps[i]}'"); break; }
				GD.Print($"[talk] > {opts[pick].AsString()}");
				st = sp.Conversation.Step(UI.Conversation.SceneId, pick);
				PrintTalk(st);
			}
			return true;
		}
		Npc who = null;
		// "*about-me": whoever is holding a story about the player's misdeeds.
		if (steps[0] == "*about-me" && Sim.Player is Player.PlayerController me)
			foreach (var n in Sim.Npcs) { if (n.IsDead) continue; foreach (var m in n.Memories) if (m.Actor == me && m.Liar != n.DisplayName && Lies.IsMisdeed(m.Text)) { who = n; break; } if (who != null) break; }
		// "*asker": whoever has asked the player a favour; "*held": someone the player saw do wrong.
		else if (steps[0] == "*asker" && Sim.Player is Player.PlayerController ap) { foreach (var f in Favours.All) if (f.Player == ap.DisplayName && !f.Done && !f.Lapsed) { who = f.Asker; break; } }
		else if (steps[0] == "*held" && Sim.Player is Player.PlayerController hp) { foreach (var n in Sim.Npcs) if (!n.IsDead && Leverage.Holds(hp, n).Count > 0) { who = n; break; } }
		else foreach (var n in Sim.Npcs) if (n.DisplayName.Contains(steps[0]) && !n.IsDead) { who = n; break; }
		if (who == null || Sim.Player is not Player.PlayerController p) { if (!steps[0].StartsWith("*")) GD.Print($"[talk] nobody called {steps[0]}"); return false; }
		p.GlobalPosition = who.GlobalPosition + new Vector2(45f, 12f);
		var state = p.Conversation.Start(who);
		PrintTalk(state);
		for (int i = 1; i < steps.Length; i++)
		{
			var opts = state["options"].AsGodotArray();
			int pick = -1;
			for (int o = 0; o < opts.Count; o++) if (opts[o].AsString().Contains(steps[i], System.StringComparison.OrdinalIgnoreCase)) { pick = o; break; }
			if (pick < 0) { GD.Print($"[talk] no option like '{steps[i]}'"); break; }
			GD.Print($"[talk] > {opts[pick].AsString()}");
			state = p.Conversation.Step(who.NetId, pick);
			PrintTalk(state);
		}
		return true;
	}

	private static void PrintTalk(Godot.Collections.Dictionary state)
	{
		var opts = new System.Collections.Generic.List<string>();
		foreach (var o in state["options"].AsGodotArray()) opts.Add(o.AsString());
		GD.Print($"[talk] {state["title"]}: {state["text"]} || {string.Join(" | ", opts)}");
	}

	// A household whose food runs out goes hungry together, and remembers it
	// together (design doc P.9). Once a day; a spell is remembered once a fortnight.
	private int _hungerCheckDay = -1;
	private readonly System.Collections.Generic.Dictionary<Household, int> _hungrySince = new();
	private void HungrySpells()
	{
		int day = (int)(GameCalendar.Instance?.DayOfYear ?? 0f);
		if (day == _hungerCheckDay) return;
		_hungerCheckDay = day;
		foreach (var h in Chronicle.Households())
		{
			int mouths = 0;
			foreach (var n in Sim.Npcs) if (n.HomeStores == h && !n.IsDead) mouths++;
			if (mouths == 0 || h.FoodDays(mouths) >= 1f) continue;
			if (_hungrySince.TryGetValue(h, out int last) && Mathf.PosMod(day - last, 365) < 14) continue;
			_hungrySince[h] = day;
			Chronicle.Shared(h, "the food ran out and everyone went hungry", 1.3f);
		}
	}

	// Dev aid: `-- --trace` prints everyone's goal and needs every 2 game hours.
	private bool _trace;
	private float _traceTimer;
	public override void _Process(double delta)
	{
		if (Net.Net.IsServer) { SicknessAndYears(GameCalendar.Instance?.DeltaGameHours ?? 0f); Tiers.Tick((float)delta); HungrySpells(); Losses.Tick(); Births(); RunConversations(); }
		if (!_trace) return;
		_traceTimer -= GameCalendar.Instance?.DeltaGameHours ?? 0f;
		if (_traceTimer > 0f) return;
		_traceTimer = 1f;
		var cal = GameCalendar.Instance;
		var sb = new System.Text.StringBuilder($"[trace] {cal.DateString} {cal.TimeString} fps={Performance.GetMonitor(Performance.Monitor.TimeFps):0} paths={Pathfinder.Calls} ({Pathfinder.TotalMs:0} ms, {Pathfinder.SlowCalls} slow) nodes={Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)}");
		sb.AppendLine();
		foreach (var n in Sim.Npcs)
			sb.AppendLine($"  {n.DisplayName,-24} {n.Cell} {(n.IsDead ? "DEAD" : n.GoalDescription)}  H{n.Needs.Hunger:0} W{n.Needs.Warmth:0} T{n.Needs.Thirst:0} HP{n.Needs.Health:0}");
		sb.Append($"  {Tiers.Report()}; animals live: {Sim.Animals.Count}; fort furs {Sim.Fort?.FursInStoreMb:0} MB; goods {Sim.Fort?.Stores.Count(ItemId.TradeGoods)}; ship {Sim.Ship?.Status()}; band cache: {Sim.Band?.Stores.Summary()}; director: {Core.Director.Describe()}");
		sb.Append($"; outpost: {Sim.Outpost?.Stores.Summary()}");
		GD.Print(sb.ToString());
	}

	// Dev aid: `-- --stress=N` adds N labourers spread over the standing
	// posts far from York, to see what a bigger population costs;
	// `-- --perf` prints the frame time and the counts every ten seconds.
	private void Stress()
	{
		int n = 0;
		foreach (string arg in OS.GetCmdlineUserArgs()) if (arg.StartsWith("--stress=")) int.TryParse(arg.Substring(9), out n);
		var far = new List<TradingPost>();
		foreach (var p in Posts.Standing()) if (p.Centre.DistanceTo(WorldLayout.FortCentre) > 40000f) far.Add(p);
		ulong t0 = Time.GetTicksMsec();
		for (int i = 0; i < n && far.Count > 0; i++)
		{
			var p = far[i % far.Count];
			var c = IsoGrid.Cell(p.House.Top, i % 5, 3);
			Spawn($"Stress {i}", Role.Labourer, p.Faction, c, p.WoodpileCell, p.Ledger, _labourerFrames, Colors.White);
		}
		if (n > 0) GD.Print($"[perf] {n} extra people spawned in {Time.GetTicksMsec() - t0} ms");
		foreach (string arg in OS.GetCmdlineUserArgs())
			if (arg == "--perf")
			{
				var timer = new Timer { WaitTime = 10.0, Autostart = true };
				timer.Timeout += () => { GD.Print($"[perf] npc steps {Npc.PerfUsec / 10000f:0.0} ms/s"); Npc.PerfUsec = 0; };
				timer.Timeout += () => GD.Print($"[perf] {Engine.GetFramesPerSecond()} fps, process {Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000:0.0} ms, physics {Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000:0.0} ms, {Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)} nodes, {Tiers.Report()}, mem {OS.GetStaticMemoryUsage() / 1048576} MB");
				AddChild(timer);
			}
	}

	public Npc Spawn(string name, Role role, Faction faction, Vector2I bed, Vector2I work, Household home, SpriteFrames frames, Color tint, float scale = 0.8f, bool female = false)
	{
		var npc = NpcScene.Instantiate<Npc>();
		npc.DisplayName = name;
		npc.Role = role;
		npc.Age = role switch
		{
			Role.Factor => GD.RandRange(46, 58), Role.NwcPartner => GD.RandRange(35, 50), Role.CreeChief => GD.RandRange(40, 55),
			Role.Clerk => GD.RandRange(19, 28), Role.CreeDaughter => GD.RandRange(16, 20), Role.Sailor => GD.RandRange(18, 40),
			_ => GD.RandRange(20, 38),
		};
		npc.Faction = faction;
		npc.Skills.Seed(
			role is Role.CreeHunter or Role.CreeChief ? (float)GD.RandRange(0.5, 0.8) : role is Role.Labourer or Role.Voyageur ? (float)GD.RandRange(0.15, 0.4) : 0.1f,
			role is Role.CreeHunter or Role.CreeChief ? (float)GD.RandRange(0.5, 0.85) : role == Role.Voyageur ? 0.35f : (float)GD.RandRange(0.1, 0.35),
			role is Role.Factor or Role.Clerk or Role.NwcPartner or Role.Postmaster or Role.CreeChief ? (float)GD.RandRange(0.5, 0.8) : 0.15f,
			role is Role.Labourer or Role.Voyageur or Role.CreeHunter or Role.CreeWoman or Role.Woman ? (float)GD.RandRange(0.3, 0.6) : 0.15f);
		npc.BedCell = bed;
		npc.WorkCell = work;
		npc.HomeStores = home;
		npc.IsFemale = female;
		npc.Position = IsoGrid.ToWorld(Sim.Paths?.NearestWalkable(bed) ?? bed);
		AddChild(npc);
		npc.SetAppearance(frames, tint, scale);
		npc.Name = $"npc_{npc.NetId}";
		Net.Net.I?.Announce(npc);
		return npc;
	}

	// The Company recruited in Orkney every year (design doc 5.5): the ship
	// lands enough new hands to bring the post back to strength.
	public int LandRecruits(int wanted)
	{
		var fort = Sim.Fort; var mens = WorldLayout.MensHouse;
		if (fort == null) return 0;
		int alive = 0;
		var taken = new HashSet<string>();
		foreach (var n in Sim.Npcs) { if (!n.IsDead) taken.Add(n.DisplayName); if (n.HomeStores == fort && n.Role == Role.Labourer && !n.IsDead) alive++; }
		int landed = 0;
		for (int i = 0; alive + landed < wanted && i < OrkneyMen.Length; i++)
		{
			if (taken.Contains(OrkneyMen[i])) continue;
			int slot = alive + landed;
			var bed = slot < 5 ? IsoGrid.Cell(mens.Top, 1 + slot, 2) : IsoGrid.Cell(mens.Top, 1 + slot % 5, 3);
			var m = Spawn(OrkneyMen[i], Role.Labourer, Faction.HBC, bed, IsoGrid.Cell(WorldLayout.FortTop, 8 + slot, 8), fort, _labourerFrames, Colors.White);
			m.Inventory.Add(ItemId.Capote); m.Inventory.Add(ItemId.Knife); m.Inventory.Add(ItemId.Bread, 2);
			m.Age = GD.RandRange(17, 24);
			m.GlobalPosition = IsoGrid.ToWorld(WorldLayout.WharfEnd) + new Vector2(GD.RandRange(-40, 40), 30 + i * 10);
			m.Brain.Push(m, new Agents.Goals.ReportForDutyGoal());   // design doc 5.5: his first order
			landed++;
		}
		if (landed > 0) Sim.Notice($"{landed} new hands off the ship from Stromness: {(landed == 1 ? "a green lad" : "green lads")} with a five-year contract.");
		return landed;
	}

	// Fort William sends men north with the brigade the same way.
	public int LandVoyageurs(int wanted)
	{
		var nwc = Sim.Nwc; var house = WorldLayout.NwcHouse;
		if (nwc == null) return 0;
		string[] names = { "Baptiste Lagimodiere", "Pierre Falcon", "Antoine Desjarlais", "Joseph Cadotte", "Louis Nolin", "Francois Deschamps", "Jean-Baptiste Lajimoniere" };
		int alive = 0;
		var taken = new HashSet<string>();
		foreach (var n in Sim.Npcs) { if (!n.IsDead) taken.Add(n.DisplayName); if (n.Faction == Faction.NWC && n.Role == Role.Voyageur && !n.IsDead) alive++; }
		int landed = 0;
		for (int i = 0; alive + landed < wanted && i < names.Length; i++)
		{
			if (taken.Contains(names[i])) continue;
			int slot = alive + landed;
			var v = Spawn(names[i], Role.Voyageur, Faction.NWC, IsoGrid.Cell(house.Top, 2 + slot % 3, 3), IsoGrid.Cell(WorldLayout.NwcTop, 6, 5), nwc, _factorFrames, new Color(0.85f, 0.75f, 0.75f));
			v.Inventory.Add(ItemId.Capote); v.Inventory.Add(ItemId.Knife); v.Inventory.Add(ItemId.Pemmican, 2);
			v.Brain.Push(v, new Agents.Goals.ReportForDutyGoal());
			landed++;
		}
		if (landed > 0) Sim.Notice($"{landed} voyageurs came north with the brigade to fill the Nor'Westers' ranks.");
		return landed;
	}

	// A ship's crew, spawned on the wharf when she ties up.
	public List<Npc> SpawnCrew(int n)
	{
		var crew = new List<Npc>();
		var end = WorldLayout.WharfEnd;
		for (int i = 0; i < n; i++)
		{
			var cell = IsoGrid.Cell(end, 0, i / 2 + 1);
			var s = Spawn(Sailors[i % Sailors.Length], Role.Sailor, Faction.ShipsCrew, cell, cell, Sim.Fort, _hbcFrames, new Color(0.75f, 0.8f, 0.9f), 0.78f);
			s.Inventory.Add(ItemId.Knife); s.Inventory.Add(ItemId.Bread, 3); s.Inventory.Add(ItemId.Capote);
			if (i < 2) s.Inventory.Add(ItemId.Musket);
			crew.Add(s);
		}
		return crew;
	}
}
