using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CampaignTool;
using IsometricFurTradeGame.Scripts.Core;
using Xunit;

namespace CampaignTests;

// cases.json: what the campaign language must do, case by case. State dates
// are ISO; world numbers are keyed 'name' or 'name(arg,arg)', facts 'name(arg)'.
public class ConformanceTests
{
	private sealed class FakeWorld : ICampaignHost
	{
		public readonly Dictionary<string, double> Metrics = new();
		public readonly Dictionary<string, bool> Predicates = new();
		public readonly List<Dictionary<string, object>> Effects = new();
		private static string Key(string name, string[] args) => $"{name}({string.Join(",", args)})";
		public double Metric(string name, string[] args) => Metrics.TryGetValue(Key(name, args), out var v) || Metrics.TryGetValue(name, out v) ? v : double.NaN;
		public bool Predicate(string name, string[] args) => Predicates.TryGetValue(Key(name, args), out var v) || Predicates.TryGetValue(name, out v) ? v : false;
		public void Effect(string name, Dictionary<string, object> action)
		{
			var copy = new Dictionary<string, object>();
			foreach (var kv in action) if (!kv.Key.StartsWith("_")) copy[kv.Key] = kv.Value;
			Effects.Add(copy);
		}
		public void Note(string text) { }
		public void NewYear(int year) { }
		public void Founded(string place) { }
	}

	public static IEnumerable<object[]> Cases()
	{
		using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "cases.json")));
		foreach (var c in doc.RootElement.GetProperty("cases").EnumerateArray()) yield return new object[] { c.GetProperty("name").GetString(), c.GetRawText() };
	}

	private static CampaignState MakeState(JsonElement s)
	{
		var st = new CampaignState { Today = CampaignLogic.DayNumber(s.GetProperty("today").GetString()) };
		if (s.TryGetProperty("seed", out var seed)) st.Seed = (uint)seed.GetInt64();
		foreach (var f in s.GetProperty("flags").EnumerateArray()) st.Flags.Add(f.GetString());
		foreach (var v in s.GetProperty("vars").EnumerateObject()) st.Vars[v.Name] = v.Value.GetDouble();
		foreach (var d in s.GetProperty("done").EnumerateArray()) st.Done.Add(d.GetString());
		foreach (var d in s.GetProperty("doneDay").EnumerateObject()) st.DoneDay[d.Name] = CampaignLogic.DayNumber(d.Value.GetString());
		if (s.TryGetProperty("timers", out var timers))
			foreach (var t in timers.EnumerateArray())
				st.Timers.Add(new CampaignTimer { Day = CampaignLogic.DayNumber(t.GetProperty("on").GetString()), Actions = (List<object>)Tree.Read(t.GetProperty("actions")) });
		return st;
	}

	private static FakeWorld MakeWorld(JsonElement w)
	{
		var fw = new FakeWorld();
		foreach (var m in w.GetProperty("metrics").EnumerateObject()) fw.Metrics[m.Name] = m.Value.GetDouble();
		foreach (var p in w.GetProperty("predicates").EnumerateObject()) fw.Predicates[p.Name] = p.Value.GetBoolean();
		return fw;
	}

	[Theory]
	[MemberData(nameof(Cases))]
	public void Case(string name, string json)
	{
		using var doc = JsonDocument.Parse(json);
		var c = doc.RootElement;
		var st = MakeState(c.GetProperty("state"));
		var w = MakeWorld(c.GetProperty("world"));
		var expect = c.GetProperty("expect");
		switch (c.GetProperty("kind").GetString())
		{
			case "cond":
				Assert.Equal(expect.GetBoolean(), CampaignLogic.Eval(Tree.Read(c.GetProperty("cond")), st, w));
				return;
			case "run": CampaignLogic.Run(Tree.Read(c.GetProperty("actions")), st, w); break;
			case "due": foreach (var actions in CampaignLogic.Due(st)) CampaignLogic.Run(actions, st, w); break;
			default: throw new InvalidOperationException("unknown kind");
		}
		if (expect.TryGetProperty("flags", out var flags))
			Assert.Equal(flags.EnumerateArray().Select(x => x.GetString()).OrderBy(x => x, StringComparer.Ordinal), st.Flags.OrderBy(x => x, StringComparer.Ordinal));
		if (expect.TryGetProperty("vars", out var vars))
			foreach (var v in vars.EnumerateObject()) Assert.Equal(v.Value.GetDouble(), st.Vars.GetValueOrDefault(v.Name), 9);
		if (expect.TryGetProperty("effects", out var effects)) Assert.Equal(Tree.Write(Tree.Read(effects)), Tree.Write(w.Effects.Cast<object>().ToList()));
		if (expect.TryGetProperty("forced", out var forced)) Assert.Equal(forced.EnumerateArray().Select(x => x.GetString()), st.Forced);
		if (expect.TryGetProperty("seed", out var seed)) Assert.Equal((uint)seed.GetInt64(), st.Seed);
		if (expect.TryGetProperty("timers", out var timers))
			Assert.Equal(timers.EnumerateArray().Select(t => (t.GetProperty("on").GetString(), t.GetProperty("count").GetInt32())),
				st.Timers.Select(t => (CampaignLogic.DateOf(t.Day).ToString("yyyy-MM-dd"), t.Actions.Count)));
	}
}
