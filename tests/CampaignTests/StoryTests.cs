using System.Collections.Generic;
using System.Linq;
using CampaignTool;
using IsometricFurTradeGame.Scripts.Core;
using Xunit;

namespace CampaignTests;

// The story campaign, 1811 to the union, played through on the game's own
// CampaignRunner (the tool's simulator) with its base campaign's beats merged
// in as the game merges them (Core/Campaign.cs, LoadScript): every quest the
// history opens must be opened by 1821, in the order the chapters are told,
// on either road and under every seed tried. A smoke test for the timeline:
// a beat renamed, re-dated or made to wait on the wrong thing strands a
// chapter, and nothing else would notice until a player had spent ten years.
public class StoryTests
{
	private const string Story = "Campaigns/company-servant.json";

	// The story's beats, with its base's (those it does not override) before them.
	private static Dictionary<string, object> Merged()
	{
		var own = U.Json(Story);
		var baseDoc = U.Json($"Campaigns/{Tree.S(own["extends"])}.json");
		var ownBeats = Tree.AsList(own["beats"]);
		var ids = new HashSet<string>(ownBeats.Cast<Dictionary<string, object>>().Select(b => Tree.S(b["id"])));
		var beats = Tree.AsList(baseDoc["beats"]).Cast<Dictionary<string, object>>().Where(b => !ids.Contains(Tree.S(b["id"]))).Cast<object>().ToList();
		beats.AddRange(ownBeats);
		own["beats"] = beats;
		if (!own.ContainsKey("start") && !own.ContainsKey("start_year") && baseDoc.TryGetValue("start", out var s)) own["start"] = s;
		return own;
	}

	private static IEnumerable<string> Strings(object tree)
	{
		switch (tree)
		{
			case string s: yield return s; break;
			case List<object> l: foreach (var x in l) foreach (var s in Strings(x)) yield return s; break;
			case Dictionary<string, object> d: foreach (var v in d.Values) foreach (var s in Strings(v)) yield return s; break;
		}
	}

	private static IEnumerable<(string road, Dictionary<string, object> quest)> Quests(Dictionary<string, object> doc) =>
		from l in Tree.AsList(doc["questlines"]).Cast<Dictionary<string, object>>()
		from q in Tree.AsList(l["quests"]).Cast<Dictionary<string, object>>()
		select (Tree.S(l.GetValueOrDefault("for")), q);

	// The history the chapters are told in, earliest first.
	private static readonly (string beat, int year)[] Timeline =
	{
		("pemmican-proclamation", 1814), ("souris-pemmican-seized", 1814), ("colony-dispersed", 1815),
		("grant-gathers", 1816), ("brandon-house-plundered", 1816), ("seven-oaks", 1816),
		("selkirk-fort-william", 1816), ("interior-war-1818", 1818), ("interior-war-1819", 1819), ("merger", 1821),
	};

	public static IEnumerable<object[]> Runs() =>
		from seed in new uint[] { 1, 7, 1811, 1816, 1821 }
		from road in new[] { "HBC", "NWC" }
		select new object[] { seed, road };

	[Theory, MemberData(nameof(Runs))]
	public void TheHistoryOpensEveryChapterInOrderBy1821(uint seed, string road)
	{
		var doc = Merged();
		var res = Sim.Simulate(doc, Tree.Dict(("end", "1821-12-31"), ("player_faction", road)), seed);
		var fired = new Dictionary<string, int>();
		foreach (var f in res.Runner.Fired) if (!fired.ContainsKey(f.Id)) fired[f.Id] = f.Day;
		int Year(string beat) => CampaignLogic.DateOf(fired[beat]).Year;

		// Every beat a quest of this road waits on happens.
		foreach (var (forCo, q) in Quests(doc))
		{
			if (forCo != "" && forCo != road) continue;
			foreach (var s in Strings(q.GetValueOrDefault("start_when")))
				if (s.StartsWith("beat:"))
					Assert.True(fired.ContainsKey(s[5..]), $"seed {seed}, {road}: quest {Tree.S(q["id"])} waits on {s[5..]}, which never happened by the end of 1821");
		}

		// The chapters' history in order, and in its year.
		string last = null;
		foreach (var (beat, year) in Timeline)
		{
			Assert.True(fired.ContainsKey(beat), $"seed {seed}, {road}: {beat} never happened by the end of 1821");
			if (year != 0) Assert.True(Year(beat) == year, $"seed {seed}, {road}: {beat} happened in {Year(beat)}, not {year}");
			if (last != null) Assert.True(fired[last] <= fired[beat], $"seed {seed}, {road}: {beat} ({CampaignLogic.DateOf(fired[beat])}) came before {last} ({CampaignLogic.DateOf(fired[last])})");
			last = beat;
		}

		// The Nor'Westers' ride on Brandon House stands aside for an NWC
		// player's: it is the only beat that reads a quest's flag to do so.
		Assert.Contains("grant_gathers", res.Runner.State.Flags);
	}

	// Every flag the story waits on is raised somewhere: by a choice, a step,
	// a quest's hooks or a beat (of the story or the history), or by the
	// engine itself (war:<band>:<outcome>, open_hostility).
	[Fact]
	public void EveryFlagTheStoryWaitsOnIsRaisedSomewhere()
	{
		var doc = Merged();
		var raised = new HashSet<string>();
		void Collect(object tree)
		{
			switch (tree)
			{
				case List<object> l: foreach (var x in l) Collect(x); break;
				case Dictionary<string, object> d:
					if (Tree.S(d.GetValueOrDefault("do")) == "flag") raised.Add(Tree.S(d.GetValueOrDefault("name")));
					foreach (var v in d.Values) Collect(v);
					break;
			}
		}
		Collect(doc);
		foreach (var b in Tree.AsList(doc["beats"]).Cast<Dictionary<string, object>>()) raised.Add(Tree.S(b["id"]));

		var missing = new List<string>();
		foreach (var (_, q) in Quests(doc))
			foreach (var s in Strings(q))
				if (s.StartsWith("flag:") && s[5..] is var f && !raised.Contains(f) && !f.StartsWith("war:") && f != "open_hostility")
					missing.Add($"{Tree.S(q["id"])} waits on {f}");
		Assert.True(missing.Count == 0, "flags nothing raises: " + string.Join("; ", missing.Distinct()));
	}

	// Each road has its three chapters, and its last quest opens with the union.
	[Theory, InlineData("HBC", "OneCompany"), InlineData("NWC", "TheCoalition")]
	public void EachRoadEndsWithTheUnion(string road, string last)
	{
		var doc = Merged();
		var quests = Quests(doc).Where(x => x.road == road).Select(x => x.quest).ToList();
		Assert.True(quests.Count >= 10, $"{road} has only {quests.Count} quests");
		var end = quests.Single(q => Tree.S(q["id"]) == last);
		Assert.Contains("beat:merger", Strings(end.GetValueOrDefault("start_when")));
		Assert.Contains(Strings(end.GetValueOrDefault("on_complete")), s => s.Contains("So ends the story"));
	}
}
