using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CampaignTool;

// "Play in game" (web editor): install the campaign, then start the game at
// a quest step - on the step's road, on the right day, standing where it
// happens - so an author sees a step without playing ten years to reach it.
// The game's own dev arguments do the work: --campaign, --start, --quest,
// --spawn and --date (Scripts/UI/MainMenu.cs, Player/PlayerController.cs).
public static class Launch
{
	// The Godot editor binary: $GODOT if set, else a Godot*/ folder beside the
	// repository (or above it), console build first so its output shows.
	public static string FindGodot()
	{
		string env = Environment.GetEnvironmentVariable("GODOT");
		if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;
		for (var d = new DirectoryInfo(Known.Root).Parent; d != null; d = d.Parent)
		{
			if (!d.Exists) continue;
			foreach (var g in d.GetDirectories("Godot*"))
			{
				var exes = g.GetFiles("Godot*.exe", SearchOption.AllDirectories).Concat(g.GetFiles("Godot*.x86_64", SearchOption.AllDirectories)).ToList();
				var pick = exes.FirstOrDefault(f => f.Name.Contains("_console")) ?? exes.FirstOrDefault();
				if (pick != null) return pick.FullName;
			}
		}
		return null;
	}

	public sealed record Plan(string Campaign, string Road, string Quest, string Step, string Spawn, string Date, List<string> Args, string Why);

	// Which step the editor means: the one named, else the one whose line is
	// nearest above the caret in the file on screen.
	public static (string Quest, string Step) StepAt(CompileResult res, Func<int, (string Path, int Line)> pathOf, string path, int line)
	{
		(string, string) best = (null, null); int bestLine = -1;
		foreach (var (key, l) in res.StepLines)
		{
			var (p, local) = pathOf(l);
			if (p != path || local > line || local <= bestLine) continue;
			var parts = key.Split('/');
			best = (parts[0], parts[1]); bestLine = local;
		}
		if (best.Item1 != null) return best;
		foreach (var (q, l) in res.QuestLines)
		{
			var (p, local) = pathOf(l);
			if (p == path && local <= line && local > bestLine) { best = (q, null); bestLine = local; }
		}
		return best;
	}

	// The arguments that start the game at this step of this campaign.
	public static Plan For(string campaign, Dictionary<string, object> doc, string quest, string step)
	{
		var line = Tree.AsList(doc.GetValueOrDefault("questlines")).OfType<Dictionary<string, object>>()
			.FirstOrDefault(l => Tree.AsList(l.GetValueOrDefault("quests")).OfType<Dictionary<string, object>>().Any(q => Tree.S(q["id"]) == quest))
			?? throw new CampError($"there is no quest '{quest}' in this campaign");
		var q = Tree.AsList(line["quests"]).OfType<Dictionary<string, object>>().First(x => Tree.S(x["id"]) == quest);
		string road = Tree.S(line.GetValueOrDefault("for")) is { Length: > 0 } f ? f : "HBC";
		var steps = Tree.AsList(q.GetValueOrDefault("objectives")).OfType<Dictionary<string, object>>().ToList();
		int at = step == null ? 0 : Math.Max(0, steps.FindIndex(s => Tree.S(s.GetValueOrDefault("id")) == step));
		step ??= steps.Count > 0 ? Tree.S(steps[0].GetValueOrDefault("id")) : null;

		// Where to stand: the step's place or person, else the nearest step before it that has one, else the giver.
		string spawn = null;
		for (int i = at; i >= 0 && spawn == null && i < steps.Count; i--)
		{
			var s = steps[i];
			var (spot, _) = MapData.Resolve(s.ContainsKey("who") ? Tree.S(s["who"]) : null, s.ContainsKey("place") ? Tree.S(s["place"]) : null);
			if (s.ContainsKey("who") || s.ContainsKey("place")) spawn = spot?.Kind == "person" ? null : spot?.Name;
		}
		if (spawn == null && q.TryGetValue("giver", out var g)) spawn = MapData.Resolve(Tree.S(g), null).Spot?.Name;

		// When: the day after whatever the quest - or any quest it requires, all the
		// way back - waits for (a date, or a beat of the story or its history).
		var byId = Tree.AsList(doc.GetValueOrDefault("questlines")).OfType<Dictionary<string, object>>()
			.SelectMany(l => Tree.AsList(l.GetValueOrDefault("quests")).OfType<Dictionary<string, object>>()).ToDictionary(x => Tree.S(x["id"]));
		DateTime? date = null;
		var seen = new HashSet<string>();
		void Waits(Dictionary<string, object> qq)
		{
			if (!seen.Add(Tree.S(qq["id"]))) return;
			foreach (var cond in Tree.AsList(qq.GetValueOrDefault("start_when")).OfType<string>())
			{
				DateTime? d = null;
				if (cond.StartsWith("after:") && DateTime.TryParse(cond[6..], CultureInfo.InvariantCulture, DateTimeStyles.None, out var a)) d = a;
				if (cond.StartsWith("beat:")) d = BeatDate(doc, cond[5..]);
				if (d != null && (date == null || d > date)) date = d;
			}
			foreach (var r in Tree.AsList(qq.GetValueOrDefault("requires"))) if (byId.TryGetValue(Tree.S(r), out var rq)) Waits(rq);
		}
		Waits(q);
		string dateArg = date?.AddDays(1).ToString("yyyy-MM-dd");

		var args = new List<string> { $"--campaign={campaign}", $"--start={road}", $"--quest={quest}{(step != null ? ":" + step : "")}" };
		if (spawn != null) args.Add($"--spawn={spawn}");
		if (dateArg != null) args.Add($"--date={dateArg}");
		string why = $"{road} road, quest {quest}{(step != null ? $" at {step}" : "")}{(spawn != null ? $", at {spawn}" : "")}{(dateArg != null ? $", on {dateArg}" : "")}";
		return new Plan(campaign, road, quest, step, spawn, dateArg, args, why);
	}

	// A beat's day, from this campaign or the one it extends.
	private static DateTime? BeatDate(Dictionary<string, object> doc, string id)
	{
		var docs = new List<Dictionary<string, object>> { doc };
		if (doc.GetValueOrDefault("extends") is string ext && File.Exists(Path.Combine(Known.Root, "Campaigns", ext + ".json")))
			docs.Add(Tree.ParseObject(File.ReadAllText(Path.Combine(Known.Root, "Campaigns", ext + ".json"))));
		foreach (var d in docs)
			foreach (var b in Tree.AsList(d.GetValueOrDefault("beats")).OfType<Dictionary<string, object>>())
				if (Tree.S(b["id"]) == id && b.GetValueOrDefault("date") is string ds && DateTime.TryParse(ds, CultureInfo.InvariantCulture, DateTimeStyles.None, out var bd))
					return bd.AddDays(Convert.ToDouble(b.GetValueOrDefault("delay_days") ?? 0.0));
		return null;
	}

	// Start the game, detached; the editor keeps running.
	public static string Start(Plan plan)
	{
		string godot = FindGodot() ?? throw new CampError("cannot find Godot: set the GODOT environment variable to the Godot editor's .exe, or put its Godot_v*/ folder next to the repository");
		var psi = new ProcessStartInfo(godot) { UseShellExecute = false, WorkingDirectory = Known.Root };
		psi.ArgumentList.Add("--path"); psi.ArgumentList.Add(Known.Root);
		psi.ArgumentList.Add("--");
		foreach (var a in plan.Args) psi.ArgumentList.Add(a);
		Process.Start(psi);
		return $"{Path.GetFileName(godot)} --path . -- {string.Join(" ", plan.Args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a))}";
	}
}
