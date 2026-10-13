using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using IsometricFurTradeGame.Scripts.Core;

// Runs tools/campaign/conformance.json against the campaign language
// (Scripts/Core/CampaignLogic.cs). tools/campaign/run_conformance.py runs
// the same file against the Python port; both must pass.
//   dotnet run --project tests/CampaignTests
namespace CampaignTests;

sealed class FakeWorld : ICampaignWorld
{
	public readonly Dictionary<string, double> Metrics = new();
	public readonly Dictionary<string, bool> Predicates = new();
	public readonly List<Dictionary<string, object>> Effects = new();

	private static string Key(string name, string[] args) => $"{name}({string.Join(",", args)})";

	public double Metric(string name, string[] args)
	{
		if (Metrics.TryGetValue(Key(name, args), out var v) || Metrics.TryGetValue(name, out v)) return v;
		return double.NaN;
	}

	public bool Predicate(string name, string[] args) =>
		Predicates.TryGetValue(Key(name, args), out var v) || Predicates.TryGetValue(name, out v) ? v : false;

	public void Effect(string name, Dictionary<string, object> action)
	{
		var copy = new Dictionary<string, object>();
		foreach (var kv in action) if (!kv.Key.StartsWith("_")) copy[kv.Key] = kv.Value;
		Effects.Add(copy);
	}
}

static class Program
{
	static object Tree(JsonElement e)
	{
		switch (e.ValueKind)
		{
			case JsonValueKind.Object: { var d = new Dictionary<string, object>(); foreach (var p in e.EnumerateObject()) d[p.Name] = Tree(p.Value); return d; }
			case JsonValueKind.Array: { var l = new List<object>(); foreach (var x in e.EnumerateArray()) l.Add(Tree(x)); return l; }
			case JsonValueKind.String: return e.GetString();
			case JsonValueKind.True: return true;
			case JsonValueKind.False: return false;
			case JsonValueKind.Number: return e.GetDouble();
			default: return null;
		}
	}

	static CampaignState MakeState(JsonElement s)
	{
		var st = new CampaignState { Today = CampaignLogic.DayNumber(s.GetProperty("today").GetString()) };
		if (s.TryGetProperty("seed", out var seed)) st.Seed = (uint)seed.GetInt64();
		foreach (var f in s.GetProperty("flags").EnumerateArray()) st.Flags.Add(f.GetString());
		foreach (var v in s.GetProperty("vars").EnumerateObject()) st.Vars[v.Name] = v.Value.GetDouble();
		foreach (var d in s.GetProperty("done").EnumerateArray()) st.Done.Add(d.GetString());
		foreach (var d in s.GetProperty("doneDay").EnumerateObject()) st.DoneDay[d.Name] = CampaignLogic.DayNumber(d.Value.GetString());
		if (s.TryGetProperty("timers", out var timers))
			foreach (var t in timers.EnumerateArray())
				st.Timers.Add(new CampaignTimer { Day = CampaignLogic.DayNumber(t.GetProperty("on").GetString()), Actions = (List<object>)Tree(t.GetProperty("actions")) });
		return st;
	}

	static FakeWorld MakeWorld(JsonElement w)
	{
		var fw = new FakeWorld();
		foreach (var m in w.GetProperty("metrics").EnumerateObject()) fw.Metrics[m.Name] = m.Value.GetDouble();
		foreach (var p in w.GetProperty("predicates").EnumerateObject()) fw.Predicates[p.Name] = p.Value.GetBoolean();
		return fw;
	}

	static string Norm(object o) => JsonSerializer.Serialize(o);

	static List<string> Check(JsonElement expect, CampaignState st, FakeWorld w)
	{
		var errs = new List<string>();
		if (expect.TryGetProperty("flags", out var flags))
		{
			var want = flags.EnumerateArray().Select(x => x.GetString()).OrderBy(x => x, StringComparer.Ordinal).ToList();
			var got = st.Flags.OrderBy(x => x, StringComparer.Ordinal).ToList();
			if (!want.SequenceEqual(got)) errs.Add($"flags [{string.Join(",", got)}] != [{string.Join(",", want)}]");
		}
		if (expect.TryGetProperty("vars", out var vars))
			foreach (var v in vars.EnumerateObject())
				if (Math.Abs(st.Vars.GetValueOrDefault(v.Name) - v.Value.GetDouble()) > 1e-9) errs.Add($"var {v.Name} = {st.Vars.GetValueOrDefault(v.Name)}, wanted {v.Value.GetDouble()}");
		if (expect.TryGetProperty("effects", out var effects))
		{
			string want = Norm(Tree(effects)), got = Norm(w.Effects);
			if (want != got) errs.Add($"effects {got} != {want}");
		}
		if (expect.TryGetProperty("forced", out var forced) && !forced.EnumerateArray().Select(x => x.GetString()).SequenceEqual(st.Forced)) errs.Add($"forced [{string.Join(",", st.Forced)}]");
		if (expect.TryGetProperty("seed", out var seed) && st.Seed != (uint)seed.GetInt64()) errs.Add($"seed {st.Seed} != {seed.GetInt64()}");
		if (expect.TryGetProperty("timers", out var timers))
		{
			var want = timers.EnumerateArray().Select(t => (t.GetProperty("on").GetString(), t.GetProperty("count").GetInt32())).ToList();
			var got = st.Timers.Select(t => (CampaignLogic.DateOf(t.Day).ToString("yyyy-MM-dd"), t.Actions.Count)).ToList();
			if (!want.SequenceEqual(got)) errs.Add($"timers [{string.Join(",", got)}] != [{string.Join(",", want)}]");
		}
		return errs;
	}

	static int Main()
	{
		using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "conformance.json")));
		int bad = 0, n = 0;
		foreach (var c in doc.RootElement.GetProperty("cases").EnumerateArray())
		{
			n++;
			string name = c.GetProperty("name").GetString();
			var st = MakeState(c.GetProperty("state"));
			var w = MakeWorld(c.GetProperty("world"));
			List<string> errs;
			switch (c.GetProperty("kind").GetString())
			{
				case "cond":
					{
						bool got = CampaignLogic.Eval(Tree(c.GetProperty("cond")), st, w), want = c.GetProperty("expect").GetBoolean();
						errs = got == want ? new List<string>() : new List<string> { $"got {got}, wanted {want}" };
						break;
					}
				case "run":
					CampaignLogic.Run(Tree(c.GetProperty("actions")), st, w);
					errs = Check(c.GetProperty("expect"), st, w);
					break;
				case "due":
					foreach (var actions in CampaignLogic.Due(st)) CampaignLogic.Run(actions, st, w);
					errs = Check(c.GetProperty("expect"), st, w);
					break;
				default: errs = new List<string> { "unknown kind" }; break;
			}
			if (errs.Count > 0) { bad++; Console.WriteLine($"FAIL {name}: {string.Join("; ", errs)}"); }
		}
		Console.WriteLine($"{n - bad}/{n} conformance cases pass (c#)");
		return bad == 0 ? 0 : 1;
	}
}
