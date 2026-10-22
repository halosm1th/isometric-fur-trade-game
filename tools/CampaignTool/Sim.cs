using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using IsometricFurTradeGame.Scripts.Core;

namespace CampaignTool;

// Play a campaign script forward on a made-up world, no game needed. The
// scheduling is the game's own (CampaignRunner); only the world is invented:
// a scenario file says how tense the companies are, which posts stand, and
// when the brigades run late.
public sealed class SimResult
{
	public CampaignRunner Runner;
	public List<(string date, string beat, string text)> Log = new();
	public List<(string date, string name)> Founded = new();
	public string Start, End;
}

public sealed class SimHost : ICampaignHost
{
	private readonly Dictionary<string, object> _sc;
	public readonly CampaignRunner Runner;
	public readonly SimResult Result = new();
	public int StartYear;
	private readonly HashSet<string> _founded = new();
	private readonly Dictionary<string, double> _stocks = new();
	private readonly Dictionary<string, double> _convoyLate = new();
	private double _tensionDelta;

	public SimHost(Dictionary<string, object> scenario, CampaignRunner runner)
	{
		_sc = scenario; Runner = runner;
		if (scenario.GetValueOrDefault("stocks") is Dictionary<string, object> st) foreach (var kv in st) _stocks[kv.Key] = Tree.N(kv.Value);
	}

	private int Today => Runner.State.Today;

	public void SetConvoyDaysLate(string run, double days) => _convoyLate[run] = days;

	private double Tension()
	{
		double basis = 20;
		if (_sc.GetValueOrDefault("tension") is Dictionary<string, object> table)
		{
			bool any = false;
			foreach (var kv in table.OrderBy(k => k.Key, StringComparer.Ordinal))
			{
				if (CampaignLogic.DayNumber(kv.Key) <= Today) { basis = Tree.N(kv.Value); any = true; }
				else if (!any) { basis = Tree.N(kv.Value); any = true; break; }
			}
		}
		return Math.Max(0, Math.Min(100, basis + _tensionDelta));
	}

	private bool PostExists(string name)
	{
		var places = Known.World.Places;
		if (!places.TryGetValue(name, out var p) || p.Kind != "post") return name is "York Factory" or "Oxford House";
		if (_founded.Contains(name)) return true;
		if (p.Closed > 0 && p.Closed <= CampaignLogic.DateOf(Today).Year) return false;
		return p.Year <= StartYear;
	}

	public double Metric(string name, string[] a)
	{
		switch (name)
		{
			case "tension": return Tension();
			case "standing": return _sc.TryGetValue("standing", out var s) ? Tree.N(s) : 20;
			case "stock": return a.Length > 1 ? _stocks.GetValueOrDefault($"{a[0]}|{a[1]}") : double.NaN;
			case "furs": return _stocks.GetValueOrDefault($"{a[0]}|furs");
			case "convoys_active": return Runner.State.Flags.Count(f => f.StartsWith("out:") && !Runner.State.Flags.Contains("in:" + f[4..]));
			case "convoy_days_late": return _convoyLate.GetValueOrDefault(a[0]);
			case "convoy_days_out": return 0;
		}
		return double.NaN;
	}

	public bool Predicate(string name, string[] a)
	{
		string arg = a.Length > 0 ? a[0] : "";
		switch (name)
		{
			case "post": return PostExists(arg);
			case "dead": return Tree.AsList(_sc.GetValueOrDefault("dead")).Select(Tree.S).Contains(arg);
			case "player_faction": return string.Equals(arg, _sc.TryGetValue("player_faction", out var f) ? Tree.S(f) : "HBC", StringComparison.OrdinalIgnoreCase);
			case "deserted": return _sc.GetValueOrDefault("deserted") is true;
			case "season":
				{
					int m = CampaignLogic.DateOf(Today).Month;
					string season = m is 5 or 6 ? "Breakup" : m is 7 or 8 ? "OpenWater" : m is 9 or 10 ? "FreezeUp" : "Winter";
					return string.Equals(arg, season, StringComparison.OrdinalIgnoreCase);
				}
		}
		return false;
	}

	public void Note(string text) { }

	public void Founded(string place) => _founded.Add(place);

	public void NewYear(int year)
	{
		foreach (var p in Known.World.Places.Values)
			if (p.Kind == "post" && p.Year == year && year > StartYear && _founded.Add(p.Name))
				Result.Founded.Add((CampaignLogic.DateOf(Today).ToString("yyyy-MM-dd"), p.Name));
	}

	public void Effect(string name, Dictionary<string, object> a)
	{
		string date = CampaignLogic.DateOf(Today).ToString("yyyy-MM-dd");
		Result.Log.Add((date, a.TryGetValue("_beat", out var b) ? Tree.S(b) : "", Describe(name, a)));
		var flags = Runner.State.Flags;
		switch (name)
		{
			case "tension": _tensionDelta += Tree.N(a.GetValueOrDefault("add")); break;
			case "found_post": _founded.Add(Tree.S(a.GetValueOrDefault("place"))); break;
			// The scenario's one player goes over (or to nobody's), and is wanted until pardoned.
			case "desert":
				{
					string to = a.TryGetValue("to", out var dt) ? Tree.S(dt) : "Free";
					string from = _sc.TryGetValue("player_faction", out var pf) ? Tree.S(pf) : "HBC";
					if (string.Equals(to, from, StringComparison.OrdinalIgnoreCase)) break;
					if (from != "Free") _sc["deserted"] = true;
					_sc["player_faction"] = to;
					Runner.State.Deed("desert");
					break;
				}
			case "pardon": _sc["deserted"] = false; break;
			case "stock":
				{
					string k = $"{Tree.S(a.GetValueOrDefault("post"))}|{Tree.S(a.GetValueOrDefault("item"))}";
					_stocks[k] = Math.Max(0, _stocks.GetValueOrDefault(k) + Tree.N(a.GetValueOrDefault("add")));
					break;
				}
			case "brigade":
				{
					string run = Tree.S(a.GetValueOrDefault("run")), eff = a.TryGetValue("effect", out var e) ? Tree.S(e) : "delay";
					if (eff == "turn_back") { flags.Add("brigade_turned_back"); flags.Add($"turned:{run}"); }
					else if (eff == "lose") { flags.Add("brigade_lost"); flags.Add($"lost:{run}"); }
					else if (eff == "launch")
					{
						flags.Add($"out:{run}");
						foreach (var p in new[] { "late", "in", "lost", "robbed", "turned" }) flags.Remove($"{p}:{run}");
					}
					break;
				}
		}
	}

	private static string Describe(string name, Dictionary<string, object> a)
	{
		string F(object d) => Tree.S(d);
		if (name == "tension") return $"tension {(Tree.N(a.GetValueOrDefault("add")) >= 0 ? "+" : "")}{F(a.GetValueOrDefault("add"))} ({F(a.GetValueOrDefault("why"))})";
		if (name == "news") return $"news {(a.ContainsKey("from") ? F(a["from"]) : "Montreal")} -> {(a.ContainsKey("to") ? F(a["to"]) : "the Factor at York Factory")} in {(a.ContainsKey("days") ? F(a["days"]) : "30")} days: {F(a.GetValueOrDefault("subject"))}";
		if (name == "say") { string t = F(a.GetValueOrDefault("text")); return $"say to {(a.ContainsKey("to") ? F(a["to"]) : "all")}: {(t.Length > 80 ? t[..80] : t)}"; }
		return $"{name} " + string.Join(" ", a.Where(kv => kv.Key != "do" && kv.Key != "_beat").Select(kv => $"{kv.Key}={F(kv.Value)}"));
	}
}

public static class Sim
{
	public static readonly Dictionary<string, Adherence> Levels = new() { ["Gentle"] = Adherence.Gentle, ["Steady"] = Adherence.Steady, ["Firm"] = Adherence.Firm, ["Strict"] = Adherence.Strict };

	public static Dictionary<string, object> DefaultScenario() => Tree.Dict(
		("start", "1811-08-20"), ("end", "1821-12-31"), ("adherence", "Steady"), ("player_faction", "HBC"), ("deserted", false),
		("tension", Tree.Dict(("1811-01-01", 20.0))), ("standing", 20.0));

	public static SimResult Simulate(Dictionary<string, object> doc, Dictionary<string, object> scenario = null, uint? seed = null, string level = null)
	{
		var sc = DefaultScenario();
		// The script's own first day, unless the scenario (or --start) says otherwise:
		// start is that day; start_year is the ship's day (20 August) in that year.
		string own = Tree.S(doc.GetValueOrDefault("start"));
		if (own.Length == 0 && doc.GetValueOrDefault("start_year") is double sy) own = $"{(int)sy:0000}-08-20";
		if (own.Length > 0)
		{
			sc["start"] = own;
			int y = CampaignLogic.DateOf(CampaignLogic.DayNumber(own)).Year;
			if (CampaignLogic.DayNumber(Tree.S(sc["end"])) < CampaignLogic.DayNumber(own)) sc["end"] = $"{y + 10:0000}-12-31";
		}
		// The script's road in is the player's side, unless the scenario says.
		string road = Tree.S(doc.GetValueOrDefault("start_path"));
		if (road is "HBC" or "NWC" or "American" or "Crown") sc["player_faction"] = road;
		if (scenario != null) foreach (var kv in scenario) sc[kv.Key] = kv.Value;
		var script = CampaignScript.Parse(doc);
		var runner = new CampaignRunner { Level = Levels[level ?? Tree.S(sc["adherence"])] };
		runner.Load(script);
		if (seed is uint s) runner.State.Seed = s;
		var host = new SimHost(sc, runner);
		var rng = new Random(unchecked((int)(runner.State.Seed ^ 0x9E3779B9)));
		int start = CampaignLogic.DayNumber(Tree.S(sc["start"])), end = CampaignLogic.DayNumber(Tree.S(sc["end"]));
		host.StartYear = CampaignLogic.DateOf(start).Year;
		var res = host.Result;
		res.Runner = runner; res.Start = Tree.S(sc["start"]); res.End = Tree.S(sc["end"]);
		runner.Begin(start, host);

		var events = Tree.AsList(sc.GetValueOrDefault("events")).Cast<Dictionary<string, object>>().ToList();
		var random = Tree.AsList(sc.GetValueOrDefault("random_events")).Cast<Dictionary<string, object>>().ToList();
		var expiry = new List<(int day, string flag)>();
		var flags = runner.State.Flags;
		for (int today = start; today <= end; today++)
		{
			runner.State.Today = today;
			var cal = CampaignLogic.DateOf(today);
			// the scenario's own weather: fixed events and dice
			foreach (var ev in events)
				if (CampaignLogic.DayNumber(Tree.S(ev["on"])) == today)
				{
					if (ev.TryGetValue("flag", out var f)) flags.Add(Tree.S(f));
					if (ev.TryGetValue("unflag", out var u)) flags.Remove(Tree.S(u));
					if (ev.TryGetValue("deed", out var dd)) runner.State.Deed(Tree.S(dd));
					if (ev.TryGetValue("convoy_days_late", out var cl)) host.SetConvoyDaysLate(Tree.S(ev["run"]), Tree.N(cl));
				}
			foreach (var rv in random)
			{
				var months = (rv.TryGetValue("months", out var mo) ? Tree.S(mo) : "1-12").Split('-');
				int lo = int.Parse(months[0]), hi = months.Length > 1 ? int.Parse(months[1]) : lo;
				double perYear = rv.TryGetValue("per_year", out var py) ? Tree.N(py) : 0.2;
				if (lo <= cal.Month && cal.Month <= hi && rng.NextDouble() < perYear / Math.Max(1, (hi - lo + 1) * 30))
				{
					flags.Add(Tree.S(rv["flag"]));
					if (rv.TryGetValue("clear_after_days", out var ca)) expiry.Add((today + (int)Tree.N(ca), Tree.S(rv["flag"])));
				}
			}
			for (int i = expiry.Count - 1; i >= 0; i--)
				if (expiry[i].day <= today) { flags.Remove(expiry[i].flag); expiry.RemoveAt(i); }

			runner.Tick(today, host);
		}
		return res;
	}
}
