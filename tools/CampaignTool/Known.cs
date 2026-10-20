using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CampaignTool;

// What the game knows: the vocabulary of the language, and the world a script
// may name (posts, brigade runs, items), read from the repo's own data so it
// cannot drift from it.
public static class Known
{
	// action -> (required fields, optional fields)
	public static readonly Dictionary<string, (string[] req, string[] opt)> Actions = new()
	{
		["flag"] = (new[] { "name" }, new string[0]),
		["unflag"] = (new[] { "name" }, new string[0]),
		["set"] = (new[] { "var", "to" }, new string[0]),
		["add"] = (new[] { "var" }, new[] { "by" }),
		["if"] = (new[] { "cond" }, new[] { "then", "else" }),
		["random"] = (new[] { "options" }, new string[0]),
		["after"] = (new[] { "days", "then" }, new string[0]),
		["trigger"] = (new[] { "beat" }, new string[0]),
		["tension"] = (new[] { "add" }, new[] { "why" }),
		["london"] = (new string[0], new[] { "price", "quota" }),
		["found_post"] = (new[] { "place" }, new string[0]),
		["news"] = (new string[0], new[] { "from", "to", "subject", "text", "days", "company" }),
		["brigade"] = (new[] { "run" }, new[] { "effect", "days", "leg" }),
		["stock"] = (new[] { "post", "item", "add" }, new string[0]),
		["say"] = (new[] { "text" }, new[] { "to", "headline" }),
		// What a quest does to the player it runs for (from a beat: every player).
		["posting"] = (new[] { "place" }, new string[0]),
		["standing"] = (new[] { "add" }, new string[0]),
		["rapport"] = (new[] { "who", "add" }, new string[0]),
		["regard"] = (new[] { "place", "add" }, new string[0]),
		["give"] = (new[] { "item" }, new[] { "count" }),
		["take"] = (new[] { "item" }, new[] { "count" }),
		["start_quest"] = (new[] { "quest" }, new string[0]),
		["end_quest"] = (new[] { "quest" }, new[] { "failed" }),
		["note"] = (new[] { "text" }, new string[0]),
		["war_party"] = (new[] { "from", "against" }, new[] { "why", "with_player" }),
		["truce"] = (new[] { "a", "b" }, new[] { "days" }),
		["feud"] = (new[] { "holder", "against" }, new[] { "add" }),
	};
	public static readonly string[] BrigadeEffects = { "delay", "turn_back", "lose", "cancel", "launch" };
	public static readonly HashSet<string> Predicates = new() { "flag", "beat", "after", "before", "chance", "month", "post", "dead", "player_faction", "season", "convoy_late", "convoy_lost", "convoy_robbed", "convoy_turned_back", "convoy_out", "convoy_in", "convoy_wintering", "quest", "quest_active", "quest_failed", "posted", "at_peace" };
	public static readonly HashSet<string> Metrics = new() { "var", "month", "year", "day", "day_of_year", "count", "beat_age", "tension", "standing", "stock", "furs", "convoys_active", "convoy_days_late", "convoy_days_out", "rapport", "regard", "feud" };
	public static readonly string[] Addressees = { "the Factor at York Factory", "the postmaster at Oxford House", "the wintering partner", "the chief of the band" };
	public static readonly string[] Companies = { "HBC", "NWC" };
	public static readonly string[] EngineFlagPrefixes = { "heard:", "late:", "lost:", "robbed:", "turned:", "out:", "in:", "deed:", "war:" };
	public static readonly HashSet<string> EngineFlags = new() { "brigade_late", "brigade_lost", "brigade_robbed", "brigade_turned_back", "brigade_in", "open_hostility" };

	public static string Root { get; } = FindRoot();

	private static string FindRoot()
	{
		foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
			for (var d = new DirectoryInfo(start); d != null; d = d.Parent)
				if (File.Exists(Path.Combine(d.FullName, "IsometricFurTradeGame.csproj"))) return d.FullName;
		throw new InvalidOperationException("run this from inside the game's repository");
	}

	public sealed class Place { public string Name, Kind; public int Year, Closed; }

	public sealed class WorldData
	{
		public readonly Dictionary<string, Place> Places = new();
		public readonly HashSet<string> Posts = new();
		public readonly HashSet<string> Ledgers = new();
		public readonly Dictionary<string, Dictionary<string, object>> Runs = new();
		public readonly HashSet<string> Items = new();
		public readonly List<string> Tasks = new();
		public readonly HashSet<string> Camps = new();

		public WorldData()
		{
			using (var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "Data/World/world.json"))))
				foreach (var p in doc.RootElement.GetProperty("places").EnumerateArray())
				{
					var place = new Place
					{
						Name = p.GetProperty("name").GetString(), Kind = p.TryGetProperty("kind", out var k) ? k.GetString() : "",
						Year = p.TryGetProperty("year", out var y) && y.ValueKind == JsonValueKind.Number ? y.GetInt32() : 0,
						Closed = p.TryGetProperty("closed", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : 0,
					};
					Places[place.Name] = place;
					if (place.Kind == "post") Posts.Add(place.Name);
				}
			Ledgers.UnionWith(Posts);
			Ledgers.Add("York Factory"); Ledgers.Add("Oxford House");
			var runs = Tree.ParseObject(File.ReadAllText(Path.Combine(Root, "Data/World/brigade_runs.json")));
			foreach (var r in Tree.AsList(runs["runs"])) { var rd = (Dictionary<string, object>)r; Runs[Tree.S(rd["name"])] = rd; }
			string src = File.ReadAllText(Path.Combine(Root, "Scripts/Items/Items.cs"));
			string body = src[src.IndexOf("enum ItemId", StringComparison.Ordinal)..];
			body = body[(body.IndexOf('{') + 1)..body.IndexOf('}')];
			body = Regex.Replace(body, @"//[^\n]*", "");
			foreach (var t in body.Split(',')) { var n = t.Trim().Split('=')[0].Trim(); if (n.Length > 0) Items.Add(n); }
			// The player's task kinds (Player/PlayerTask.cs), for quest steps that hand one out.
			string tsrc = File.ReadAllText(Path.Combine(Root, "Scripts/Player/PlayerTask.cs"));
			string tbody = tsrc[tsrc.IndexOf("enum TaskKind", StringComparison.Ordinal)..];
			tbody = tbody[(tbody.IndexOf('{') + 1)..tbody.IndexOf('}')];
			foreach (var t in tbody.Split(',')) { var n = t.Trim(); if (n.Length > 0 && n != "None") Tasks.Add(n); }
			// The country's camps by name: a quest can send you to one, or to its people.
			using (var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "Data/World/world.json"))))
				if (doc.RootElement.TryGetProperty("layout", out var lay) && lay.TryGetProperty("camps", out var cs))
					foreach (var c in cs.EnumerateArray()) Camps.Add(c.GetProperty("name").GetString());
		}
	}

	private static WorldData _world;
	public static WorldData World => _world ??= new WorldData();
}
