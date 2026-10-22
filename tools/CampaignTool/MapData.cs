using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CampaignTool;

// The map the editor draws (web editor's Map tab): the world (Data/World/
// world.json), the cast placed in the editor (the NpcSpawn markers in
// Scenes/World/YorkFactoryExterior.tscn), and, for a campaign, every place
// its quests and beats send the player or touch, resolved the way the game
// resolves them (Scripts/Core/Quests.cs) - or marked when a name resolves to
// nothing. Positions are world pixels, as IsoGrid.ToWorld gives them.
public static class MapData
{
	public const int TileW = 128, TileH = 64;
	public static (double X, double Y) ToWorld(int cx, int cy) => (TileW / 2.0 + TileW * cx + ((cy & 1) == 1 ? TileW / 2.0 : 0), TileH / 2.0 + TileH / 2.0 * cy);
	// IsoGrid.Cell(top, right, down), in world pixels: each step right is half a tile across and a row down.
	private static (double X, double Y) Offset(int cx, int cy, int right, int down)
	{
		var (x, y) = ToWorld(cx, cy);
		return (x + (right - down) * TileW / 2.0, y + (right + down) * TileH / 2.0);
	}

	private static int[] Cell(object v) { var l = Tree.AsList(v); return new[] { (int)Convert.ToDouble(l[0]), (int)Convert.ToDouble(l[1]) }; }

	// ---- the world -------------------------------------------------------------------

	public sealed class Spot { public string Name, Kind, Company, Nation, Post; public double X, Y; public int Year, Closed, People; }

	private static Dictionary<string, Spot> _spots;
	private static Dictionary<string, object> _world;
	private static DateTime _stamp;

	// Every named spot a campaign can name: posts (with books), camps, and the atlas's other places.
	public static Dictionary<string, Spot> Spots { get { Load(); return _spots; } }

	public static Dictionary<string, object> World() { Load(); return _world; }

	private static void Load()
	{
		string wf = Path.Combine(Known.Root, "Data/World/world.json"), sf = Path.Combine(Known.Root, "Scenes/World/YorkFactoryExterior.tscn");
		var stamp = new[] { File.GetLastWriteTimeUtc(wf), File.Exists(sf) ? File.GetLastWriteTimeUtc(sf) : DateTime.MinValue }.Max();
		if (_world != null && stamp == _stamp) return;
		_stamp = stamp;
		var doc = Tree.ParseObject(File.ReadAllText(wf));
		var lay = (Dictionary<string, object>)doc["layout"];
		var spots = new Dictionary<string, Spot>();
		var places = new List<object>();

		// The atlas's places first (real positions); a built post or camp below takes over its name.
		foreach (var po in Tree.AsList(doc["places"]))
		{
			var p = (Dictionary<string, object>)po; var c = Cell(p["cell"]); var (x, y) = ToWorld(c[0], c[1]);
			string name = Tree.S(p["name"]);
			spots[name] = new Spot { Name = name, Kind = Tree.S(p.GetValueOrDefault("kind")), Company = Tree.S(p.GetValueOrDefault("company")), X = x, Y = y,
				Year = p.GetValueOrDefault("year") is double yr ? (int)yr : 0 };
		}
		// The three hand-built places (World/WorldLayout.cs).
		void Built(string name, string key, int w, int d, string company)
		{
			var c = Cell(lay[key]); var (x, y) = Offset(c[0], c[1], w / 2, d / 2);
			spots[name] = new Spot { Name = name, Kind = "post", Company = company, X = x, Y = y, Year = name == "York Factory" ? 1684 : 1798 };
		}
		Built("York Factory", "fort_top", 22, 34, "HBC");
		Built("Nor'Westers' house (York)", "nwc_top", 12, 10, "NWC");
		Built("Oxford House", "outpost_top", 12, 9, "HBC");
		foreach (var po in Tree.AsList(lay["posts"]))
		{
			var p = (Dictionary<string, object>)po; var c = Cell(p["top"]); var (x, y) = Offset(c[0], c[1], 6, 4);
			string name = Tree.S(p["name"]);
			spots[name] = new Spot { Name = name, Kind = "post", Company = Tree.S(p["company"]), X = x, Y = y,
				Year = p.GetValueOrDefault("year") is double yr ? (int)yr : 0, Closed = p.GetValueOrDefault("closed") is double cl ? (int)cl : 0 };
		}
		foreach (var co in Tree.AsList(lay["camps"]))
		{
			var c0 = (Dictionary<string, object>)co; var c = Cell(c0["centre"]); var (x, y) = ToWorld(c[0], c[1]);
			string name = Tree.S(c0["name"]);
			spots[name] = new Spot { Name = name, Kind = Tree.S(c0["kind"]) == "family" ? "family" : "camp", Nation = Tree.S(c0.GetValueOrDefault("nation")), Post = Tree.S(c0.GetValueOrDefault("post")),
				X = x, Y = y, People = c0.GetValueOrDefault("people") is double pp ? (int)pp : 0 };
		}
		if (lay.TryGetValue("towns", out var towns))
			foreach (var to in Tree.AsList(towns))
			{
				var t0 = (Dictionary<string, object>)to; var c = Cell(t0["top"]);
				var (x, y) = Offset(c[0], c[1], (int)Tree.N(t0["width"]) / 2, (int)Tree.N(t0["depth"]) / 2);
				string name = Tree.S(t0["name"]);
				spots[name] = new Spot { Name = name, Kind = "town", Company = Tree.S(t0.GetValueOrDefault("polity")), X = x, Y = y,
					Year = t0.GetValueOrDefault("year") is double ty ? (int)ty : 0 };
			}
		foreach (var s in spots.Values)
			places.Add(Tree.Dict(("name", s.Name), ("kind", s.Kind), ("company", s.Company), ("nation", s.Nation), ("post", s.Post),
				("x", s.X), ("y", s.Y), ("year", (double)s.Year), ("closed", (double)s.Closed), ("people", (double)s.People)));

		var cols = Convert.ToDouble(doc["columns"]); var rows = Convert.ToDouble(doc["rows"]);
		_spots = spots;
		_world = Tree.Dict(("columns", cols), ("rows", rows), ("width", cols * TileW + TileW), ("height", rows * TileH / 2.0 + TileH / 2.0),
			("pxPerKm", doc.GetValueOrDefault("px_per_km")), ("places", places), ("cast", Cast(sf)));
	}

	// ---- the cast placed in the editor ------------------------------------------------

	private static readonly Regex NodeHead = new(@"^\[node name=""([^""]*)"" type=""(\w+)""(?: parent=""([^""]*)"")?");
	private static readonly Regex Prop = new(@"^(\w+) = (.*)$");
	private static readonly Regex Vec = new(@"Vector2\(([-\d.e]+),\s*([-\d.e]+)\)");

	private static string[] EnumNames(string file, string enumName)
	{
		string src = File.ReadAllText(Path.Combine(Known.Root, file));
		int i = src.IndexOf("enum " + enumName, StringComparison.Ordinal);
		if (i < 0) return Array.Empty<string>();
		string body = src[(src.IndexOf('{', i) + 1)..src.IndexOf('}', i)];
		body = Regex.Replace(body, @"//[^\n]*", "");
		return body.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToArray();
	}

	// Each NpcSpawn marker: who, what, of which house, and where.
	private static List<object> Cast(string scene)
	{
		var list = new List<object>();
		if (!File.Exists(scene)) return list;
		var roles = EnumNames("Scripts/Agents/Npc.cs", "Role");
		var factions = EnumNames("Scripts/Agents/Agent.cs", "Faction");
		var homes = EnumNames("Scripts/World/NpcSpawn.cs", "HomePost");
		string Name(string[] e, string v, string def) => int.TryParse(v, out int k) && k >= 0 && k < e.Length ? e[k] : def;
		string spawnRes = null;
		var pos = new Dictionary<string, (double X, double Y)>();   // node path -> its own position
		Dictionary<string, string> cur = null; string curPath = null, curParent = null;
		void Flush()
		{
			if (cur == null) return;
			(double X, double Y) own = cur.TryGetValue("position", out var pv) && Vec.Match(pv) is { Success: true } m ? (double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)) : (0, 0);
			pos[curPath] = own;
			if (spawnRes != null && cur.TryGetValue("script", out var sc) && sc.Contains($"\"{spawnRes}\""))
			{
				double gx = own.X, gy = own.Y;
				for (string p = curParent; !string.IsNullOrEmpty(p) && p != "."; p = p.Contains('/') ? p[..p.LastIndexOf('/')] : null)
					if (pos.TryGetValue(p, out var pp)) { gx += pp.X; gy += pp.Y; }
				string S(string k) => cur.TryGetValue(k, out var v) ? v.Trim('"') : "";
				string home = Name(homes, S("Home"), "Fort");
				list.Add(Tree.Dict(("name", S("DisplayName") is { Length: > 0 } dn ? dn : curPath[(curPath.LastIndexOf('/') + 1)..]),
					("role", Name(roles, S("Role"), "Labourer")), ("faction", Name(factions, S("Faction"), "HBC")), ("home", home),
					("place", HomePlace(home, S("PostName"))), ("leader", S("Leader") == "true"), ("female", S("Female") == "true"),
					("x", gx), ("y", gy)));
			}
			cur = null;
		}
		foreach (var raw in File.ReadLines(scene))
		{
			string line = raw.TrimEnd();
			if (line.StartsWith("[ext_resource") && line.Contains("NpcSpawn.cs") && Regex.Match(line, @"\bid=""([^""]+)""") is { Success: true } idm) spawnRes = idm.Groups[1].Value;
			if (line.StartsWith("["))
			{
				Flush();
				var h = NodeHead.Match(line);
				if (h.Success)
				{
					string parent = h.Groups[3].Success ? h.Groups[3].Value : "";
					curParent = parent;
					curPath = parent is "" or "." ? h.Groups[1].Value : parent + "/" + h.Groups[1].Value;
					cur = new Dictionary<string, string>();
				}
				continue;
			}
			if (cur != null && Prop.Match(line) is { Success: true } pm) cur[pm.Groups[1].Value] = pm.Groups[2].Value;
		}
		Flush();
		return list;
	}

	// Which spot on the map a marker's household is (NpcSpawn.Household()).
	private static string HomePlace(string home, string postName) => home switch
	{
		"Fort" => "York Factory",
		"Nwc" => "Nor'Westers' house (York)",
		"Outpost" => "Oxford House",
		"CreeBand" => "Cree winter camp (York)",
		"Assiniboine" => "Assiniboine winter camp",
		// A townsman's household is "Town|house": his place is the town.
		"Town" => postName.Contains('|') ? postName[..postName.IndexOf('|')] : postName,
		_ => postName,
	};

	// ---- a campaign's places ----------------------------------------------------------

	// The start's own people (Library.Persons) live at the hand-built places.
	private static readonly Dictionary<string, string> PersonPlace = new()
	{
		["Factor"] = "York Factory", ["Postmaster"] = "Oxford House", ["Partner"] = "Nor'Westers' house (York)", ["Chief"] = "Cree winter camp (York)",
	};

	private static readonly (string Key, string Role)[] ActionPlaces =
	{
		("place", "place"), ("post", "post"), ("from", "from"), ("against", "against"), ("a", "with"), ("b", "with"), ("holder", "holder"), ("who", "who"),
	};

	// Where a quest's `who` or `place` is, and a readable reason when it is nowhere.
	public static (Spot Spot, string Why) Resolve(string who, string place)
	{
		var spots = Spots;
		if (!string.IsNullOrEmpty(place))
			return spots.TryGetValue(place, out var s) ? (s, null) : (null, $"'{place}' is not a post, a camp or a place on the map");
		if (string.IsNullOrEmpty(who)) return (null, "names nobody");
		if (PersonPlace.TryGetValue(who, out var pp)) return (spots.GetValueOrDefault(pp), null);
		int at = who.IndexOf('@');
		if (at >= 0)
		{
			string p = who[(at + 1)..].Trim();
			return spots.TryGetValue(p, out var s2) && s2.Kind is "post" or "camp" or "family" or "nwc_post" ? (s2, null) : (null, $"'{p}' is not a post or a camp, so nobody is {who[..at].Trim()} there");
		}
		// A person by name: wherever the cast puts them.
		foreach (var c in Tree.AsList(World()["cast"]).OfType<Dictionary<string, object>>())
			if (Tree.S(c["name"]) == who) return (new Spot { Name = Tree.S(c["place"]) is { Length: > 0 } cp ? cp : who, Kind = "person", X = (double)c["x"], Y = (double)c["y"] }, null);
		return (null, $"nobody called '{who}' is placed in the world (a name works only while someone of exactly that name is alive)");
	}

	// Every pin for a compiled campaign: quest givers and steps (in order, so the
	// editor can draw the road), and the places beats and quests act on.
	// pathOf: the file a program line is in and its line there (a part's lines are its own).
	public static Dictionary<string, object> Pins(CompileResult res, Func<int, (string Path, int Line)> pathOf = null)
	{
		var pins = new List<object>();
		var json = res.Json;
		// Each quest's and beat's own title, for the legend and the quest picker.
		var titles = new Dictionary<string, string>();
		foreach (var lo in Tree.AsList(json.GetValueOrDefault("questlines")).OfType<Dictionary<string, object>>())
			foreach (var q in Tree.AsList(lo.GetValueOrDefault("quests")).OfType<Dictionary<string, object>>()) titles[Tree.S(q["id"])] = Tree.S(q.GetValueOrDefault("title"));
		foreach (var b in Tree.AsList(json.GetValueOrDefault("beats")).OfType<Dictionary<string, object>>()) titles.TryAdd(Tree.S(b["id"]), Tree.S(b.GetValueOrDefault("title")) is { Length: > 0 } bt ? bt : Tree.S(b["id"]));
		void Pin(string group, string quest, string step, string label, string kind, string who, string place, int line, int order, string title = null)
		{
			var (spot, why) = Resolve(who, place);
			var (file, fline) = pathOf?.Invoke(line) ?? (null, res.Where(line).Line);
			pins.Add(Tree.Dict(("group", group), ("quest", quest), ("title", title ?? titles.GetValueOrDefault(quest ?? "", quest)), ("step", step), ("label", label), ("kind", kind), ("who", who), ("place", place ?? spot?.Name),
				("line", (double)fline), ("path", file), ("order", (double)order), ("ok", spot != null), ("why", why),
				("x", spot?.X), ("y", spot?.Y), ("at", spot?.Name)));
		}
		// Actions anywhere in a list, nested ones too (if/then/else, random options, after/then).
		void Actions(object node, string group, string quest, string label, int line)
		{
			if (node is List<object> list) { foreach (var x in list) Actions(x, group, quest, label, line); return; }
			if (node is not Dictionary<string, object> a) return;
			if (a.GetValueOrDefault("do") is string what)
				foreach (var (key, role) in ActionPlaces)
				{
					if (!a.TryGetValue(key, out var v) || v is not string s || s.Length == 0) continue;
					if (key == "who") Pin(group, quest, null, $"{label}: {what} with {s}", "effect", s, null, line, -1);
					else if (what is "posting" or "regard" or "found_post" or "stock" or "war_party" or "truce" or "feud") Pin(group, quest, null, $"{label}: {what} ({role})", "effect", null, s, line, -1);
				}
			foreach (var v in a.Values) if (v is List<object> or Dictionary<string, object>) Actions(v, group, quest, label, line);
		}
		foreach (var lo in Tree.AsList(json.GetValueOrDefault("questlines")).OfType<Dictionary<string, object>>())
			foreach (var q in Tree.AsList(lo.GetValueOrDefault("quests")).OfType<Dictionary<string, object>>())
			{
				string qid = Tree.S(q["id"]), title = Tree.S(q.GetValueOrDefault("title"));
				int qline = res.QuestLines.GetValueOrDefault(qid, 1);
				if (q.TryGetValue("giver", out var g)) Pin("quest", qid, null, $"{title}: given by {Tree.S(g)}", "giver", Tree.S(g), null, qline, 0);
				int order = 1;
				foreach (var o in Tree.AsList(q.GetValueOrDefault("objectives")).OfType<Dictionary<string, object>>())
				{
					string sid = Tree.S(o.GetValueOrDefault("id")), kind = Tree.S(o.GetValueOrDefault("kind")) is { Length: > 0 } k ? k : "talk";
					int line = res.StepLines.GetValueOrDefault($"{qid}/{sid}", qline);
					string text = Tree.S(o.GetValueOrDefault("text"));
					string label = $"{title} - {sid}: {(text.Length > 0 ? text : kind)}";
					if (o.ContainsKey("who")) Pin("quest", qid, sid, label, kind, Tree.S(o["who"]), null, line, order);
					if (o.ContainsKey("place")) Pin("quest", qid, sid, label, kind, null, Tree.S(o["place"]), line, order);
					if (o.ContainsKey("dest")) Pin("quest", qid, sid, label + " (destination)", kind, null, Tree.S(o["dest"]), line, order);
					Actions(o.GetValueOrDefault("on_done"), "quest", qid, $"{title} - {sid}", line);
					if (o.GetValueOrDefault("dialogue") is Dictionary<string, object> d)
						foreach (var c in Tree.AsList(d.GetValueOrDefault("choices")).OfType<Dictionary<string, object>>()) Actions(c.GetValueOrDefault("do"), "quest", qid, $"{title} - {sid}", line);
					order++;
				}
				foreach (var key in new[] { "on_start", "on_complete", "on_fail" }) Actions(q.GetValueOrDefault(key), "quest", qid, $"{title} ({key.Replace('_', ' ')})", qline);
			}
		foreach (var b in Tree.AsList(json.GetValueOrDefault("beats")).OfType<Dictionary<string, object>>())
		{
			string id = Tree.S(b["id"]);
			int line = res.BeatLines.GetValueOrDefault(id, 1);
			string bt = Tree.S(b.GetValueOrDefault("title")) is { Length: > 0 } t ? t : id;
			foreach (var (k, v) in b) if (k != "when") Actions(v, "beat", id, bt, line);
		}
		return Tree.Dict(("pins", pins));
	}
}
