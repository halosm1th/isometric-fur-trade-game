using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CampaignTool;
using IsometricFurTradeGame.Scripts.Core;
using Xunit;
using static CampaignTests.U;

namespace CampaignTests;

public class CheckerTests
{
	private static (List<string> errors, List<string> warnings) Messages(Dictionary<string, object> doc)
	{
		var r = Checker.Check(doc);
		return (r.Errors.Select(e => e.msg).ToList(), r.Warnings.Select(w => w.msg).ToList());
	}

	[Fact]
	public void ACleanScriptIsClean()
	{
		var (e, w) = Messages(Doc(
			B("a", "fixed", "1812-01-01", ("happens", L(Act("flag", ("name", "x"))))),
			B("b", "event", "1813-01-01", ("requires", L("a")), ("when", L("flag:x")))));
		Assert.Empty(e); Assert.Empty(w);
	}

	[Theory]
	[InlineData("fixed", null, "needs a date")]
	[InlineData("bogus", "1812-01-01", "unknown kind")]
	public void BeatMistakes(string kind, string date, string expect) => Assert.Contains(Messages(Doc(B("a", kind, date))).errors, m => m.Contains(expect));

	[Fact]
	public void ADateThatIsNotADay() => Assert.Contains(Messages(Doc(B("a", "fixed", "1812-02-30"))).errors, m => m.Contains("not a real day"));

	public static IEnumerable<object[]> Mistakes()
	{
		object[] M(string expect, params (string, object)[] more) => new object[] { expect, B("a", "event", "1812-01-01", more) };
		yield return M("requires 'ghost'", ("requires", L("ghost")));
		yield return M("unknown action", ("happens", L(Act("teleport"))));
		yield return M("needs 'name'", ("happens", L(Act("flag"))));
		yield return M("no field 'wat'", ("happens", L(Act("tension", ("add", 5.0), ("wat", 1.0)))));
		yield return M("unknown beat", ("happens", L(Act("trigger", ("beat", "ghost")))));
		yield return M("not a post", ("happens", L(Act("found_post", ("place", "Nowhere")))));
		yield return M("not a brigade run", ("happens", L(Act("brigade", ("run", "the Phantom boats")))));
		yield return M("is not one of", ("happens", L(Act("brigade", ("run", "the York boats"), ("effect", "sink")))));
		yield return M("legs", ("happens", L(Act("brigade", ("run", "the York boats"), ("effect", "launch"), ("leg", 9.0)))));
		yield return M("not an item", ("happens", L(Act("stock", ("post", "York Factory"), ("item", "Gold"), ("add", 1.0)))));
		yield return M("not a post with books", ("happens", L(Act("stock", ("post", "Atlantis"), ("item", "Rum"), ("add", 1.0)))));
		yield return M("unknown condition", ("when", L("telepathy:yes")));
		yield return M("unknown number", ("when", L("wealth>5")));
		yield return M("not a brigade run", ("when", L("convoy_late:the Phantom boats")));
		yield return M("not on the map", ("when", L("post:Atlantis")));
		yield return M("stock(POST, ITEM)", ("when", L(Tree.Dict(("m", "stock"), ("a", L("York Factory")), ("op", "<"), ("v", 3.0)))));
		yield return M("options", ("happens", L(Act("random", ("options", L())))));
		yield return M("company", ("happens", L(Act("news", ("company", "XYZ"))))) ;
		yield return M("must be all", ("happens", L(Act("say", ("text", "x"), ("to", "Cree")))));
		yield return M("needs a date", ("when", L("after:soon")));
	}

	[Theory]
	[MemberData(nameof(Mistakes))]
	public void CatchesMistakes(string expect, Dictionary<string, object> beat) => Assert.Contains(Messages(Doc(beat)).errors, m => m.Contains(expect));

	[Fact]
	public void DuplicatesAndCycles()
	{
		Assert.Contains(Messages(Doc(B("a", "event", "1812-01-01"), B("a", "event", "1812-01-01"))).errors, m => m.Contains("duplicate"));
		Assert.Contains(Messages(Doc(B("a", "event", "1812-01-01", ("requires", L("b"))), B("b", "event", "1812-01-01", ("requires", L("a"))))).errors, m => m.Contains("cycle"));
	}

	[Fact]
	public void Warnings()
	{
		Assert.Contains(Messages(Doc(B("a", "event", "1812-01-01", ("when", L("flag:typo"))))).warnings, m => m.Contains("nothing sets"));
		Assert.Contains(Messages(Doc(B("a", "event", "1811-01-01"), B("b", "event", "1810-01-01", ("requires", L("a"))))).warnings, m => m.Contains("will wait"));
		Assert.Contains(Messages(Doc(B("a", "event", "1811-01-01", ("when", L("var(never_set) >= 1"))))).warnings, m => m.Contains("always be 0"));
		Assert.Contains(Messages(Doc(B("a", "event", "1812-01-01", ("requires", L("ghost"))), B("b", "event", "1813-01-01", ("requires", L("a"))))).warnings, m => m.Contains("can never happen"));
	}

	[Fact]
	public void FlagsTheEngineRaisesAreNotTypos()
	{
		var (e, w) = Messages(Doc(B("a", "conditional", null, ("when", L("brigade_late", "late:the York boats", "heard:x")))));
		Assert.Empty(e); Assert.Empty(w);
	}

	[Theory]
	[InlineData("Campaigns/historical.json")]
	[InlineData("Campaigns/Gemini and Clark.json")]
	public void TheShippedCampaignsAreClean(string path) => Assert.Empty(Checker.Check(Json(path)).Errors);
}

public class SimulationTests
{
	private static Dictionary<string, object> Sc(string start, string end, params (string, object)[] more)
	{
		var sc = Tree.Dict(("start", start), ("end", end));
		foreach (var (k, v) in more) sc[k] = v;
		return sc;
	}

	private static List<(string date, string how)> Fired(SimResult r, string id) =>
		r.Runner.Fired.Where(f => f.Id == id).Select(f => (CampaignLogic.DateOf(f.Day).ToString("yyyy-MM-dd"), f.How)).ToList();

	[Fact]
	public void AFixedBeatHappensOnItsDay()
	{
		var r = Sim.Simulate(Doc(B("a", "fixed", "1815-03-04", ("happens", L(Act("flag", ("name", "x")))))), Sc("1815-01-01", "1816-01-01"));
		Assert.Equal(new[] { ("1815-03-04", "on its day") }, Fired(r, "a"));
		Assert.Contains("x", r.Runner.State.Flags);
	}

	[Fact]
	public void HistoryBeforeTheStartIsAppliedNotFired()
	{
		var r = Sim.Simulate(Doc(B("a", "fixed", "1810-03-04", ("happens", L(Act("flag", ("name", "x")))))), Sc("1811-01-01", "1812-01-01"));
		Assert.Empty(Fired(r, "a"));
		Assert.Contains("a", r.Runner.History);
		Assert.Contains("x", r.Runner.State.Flags);
	}

	private static Dictionary<string, object> EventDoc() => Doc(B("a", "event", "1815-01-01", ("window_days", 30.0), ("happens", L(Act("flag", ("name", "x"))))));

	[Fact]
	public void AdherenceDecidesWhetherAnEventIsForced()
	{
		var sc = Sc("1814-06-01", "1817-01-01");
		Assert.Empty(Fired(Sim.Simulate(EventDoc(), sc, level: "Steady"), "a"));
		Assert.Equal(("1815-01-31", "forced"), Fired(Sim.Simulate(EventDoc(), sc, level: "Strict"), "a")[0]);
		Assert.Equal(("1815-03-02", "forced"), Fired(Sim.Simulate(EventDoc(), sc, level: "Firm"), "a")[0]);
	}

	[Fact]
	public void TheWorldCanGetThereFirst()
	{
		var d = Doc(B("a", "event", "1815-01-01", ("done_when", L("flag:world_did_it"))));
		var r = Sim.Simulate(d, Sc("1814-06-01", "1817-01-01", ("events", L(Tree.Dict(("on", "1814-12-01"), ("flag", "world_did_it"))))), level: "Strict");
		Assert.Equal(("1814-12-01", "by the world"), Fired(r, "a")[0]);
	}

	[Fact]
	public void AConditionalWaitsForItsConditions()
	{
		var d = Doc(B("a", "conditional", null, ("when", L("tension>=50")), ("delay_days", 5.0), ("happens", L(Act("flag", ("name", "x"))))));
		var r = Sim.Simulate(d, Sc("1814-01-01", "1815-01-01", ("tension", Tree.Dict(("1814-01-01", 20.0), ("1814-06-01", 60.0)))));
		Assert.Equal(("1814-06-06", "when its conditions held"), Fired(r, "a")[0]);
	}

	[Fact]
	public void ActionsMoveTensionAndBeatsSeeIt()
	{
		var d = Doc(B("a", "fixed", "1814-02-01", ("happens", L(Act("tension", ("add", 40.0), ("why", "x"))))),
			B("b", "conditional", null, ("requires", L("a")), ("when", L("tension>=50"))));
		Assert.NotEmpty(Fired(Sim.Simulate(d, Sc("1814-01-01", "1815-01-01")), "b"));
	}

	[Fact]
	public void TimersTriggersAndRepeats()
	{
		var d = Doc(B("a", "fixed", "1814-02-01", ("happens", L(Act("after", ("days", 10.0), ("then", L(Act("trigger", ("beat", "t")))))))),
			B("t", "pressure", "2000-01-01", ("happens", L(Act("flag", ("name", "tripped"))))),
			B("r", "fixed", "1814-03-01", ("repeat_days", 365.0)));
		var r = Sim.Simulate(d, Sc("1814-01-01", "1817-01-01"));
		Assert.Equal(("1814-02-11", "triggered"), Fired(r, "t")[0]);
		Assert.Equal(new[] { "1814-03-01", "1815-03-01", "1816-02-29" }, Fired(r, "r").Select(x => x.date));   // 365 days is not a calendar year in a leap year
	}

	[Fact]
	public void RandomIsRepeatableFromASeed()
	{
		var opts = L(Tree.Dict(("weight", 1.0), ("do", L(Act("flag", ("name", "h"))))), Tree.Dict(("weight", 1.0), ("do", L(Act("flag", ("name", "t"))))));
		var d = Doc(B("a", "fixed", "1814-02-01", ("happens", L(Act("random", ("options", opts))))));
		var sc = Sc("1814-01-01", "1814-12-31");
		var outcomes = Enumerable.Range(0, 12).Select(s => string.Join("", Sim.Simulate(d, sc, (uint)(20240611 + s * 7919)).Runner.State.Flags.Where(f => f is "h" or "t").OrderBy(f => f))).ToHashSet();
		Assert.Equal(new HashSet<string> { "h", "t" }, outcomes);
		Assert.Equal(string.Join(",", Sim.Simulate(d, sc, 5).Runner.State.Flags.OrderBy(f => f)), string.Join(",", Sim.Simulate(d, sc, 5).Runner.State.Flags.OrderBy(f => f)));
	}

	[Fact]
	public void TheHistoricalScriptPlaysAtStrict()
	{
		var r = Sim.Simulate(Json("Campaigns/historical.json"), level: "Strict");
		var fired = r.Runner.Fired.Select(f => f.Id).ToHashSet();
		foreach (var id in new[] { "war-of-1812", "astoria-sold", "pemmican-proclamation", "seven-oaks", "merger" }) Assert.Contains(id, fired);
	}
}

public class ShippedTests
{
	private static IEnumerable<string> Sources() => Directory.GetFiles(Path.Combine(Root, "tools/CampaignTool/src"), "*.camp");
	public static IEnumerable<string> AllSources() => Sources().Concat(Directory.GetFiles(Path.Combine(Root, "tools/CampaignTool/examples"), "*.camp"));

	[Fact]
	public void TheInstalledJsonIsWhatTheSourceBuilds()
	{
		foreach (var src in Sources())
		{
			var built = CampLang.CompileText(File.ReadAllText(src));
			var installed = Json($"Campaigns/{Path.GetFileNameWithoutExtension(src)}.json");
			Assert.True(Tree.Same(built, installed), $"Campaigns/{Path.GetFileNameWithoutExtension(src)}.json is stale: run  dotnet run --project tools/CampaignTool -- build");
		}
	}

	[Fact]
	public void SourcesAreClean()
	{
		foreach (var src in AllSources())
		{
			var r = Checker.Check(CampLang.CompileText(File.ReadAllText(src)));
			Assert.True(r.Errors.Count == 0, $"{Path.GetFileName(src)}: {string.Join("; ", r.Errors.Select(e => e.where + " " + e.msg))}");
			Assert.True(r.Warnings.Count == 0, $"{Path.GetFileName(src)}: {string.Join("; ", r.Warnings.Select(e => e.where + " " + e.msg))}");
		}
	}

	[Fact]
	public void TheBrigadeBeatsBehave()
	{
		var doc = CampLang.CompileText(Read("tools/CampaignTool/src/historical.camp"));
		var sc = Json("tools/CampaignTool/examples/pemmican-war.scenario.json");
		var r = Sim.Simulate(doc, sc, 5);
		int MostPerYear(string id) => r.Runner.Fired.Where(f => f.Id == id).GroupBy(f => CampaignLogic.DateOf(f.Day).Year).Max(g => g.Count());
		// the brigades are acted on about every six weeks of open water, not every other week
		Assert.InRange(MostPerYear("war-slows-the-canoes"), 1, 4);
		Assert.InRange(MostPerYear("interior-ambushes"), 1, 3);
		Assert.InRange(MostPerYear("pemmican-starves-the-canoes"), 1, 4);
		// the ones that answer what the boats did, and the dated history untouched
		var fired = r.Runner.Fired.Select(f => f.Id).ToHashSet();
		Assert.Contains("fort-william-stood-down", fired);
		Assert.Contains("canoes-stood-down", fired);
		Assert.Equal("1821-03-26", CampaignLogic.DateOf(r.Runner.Fired.First(f => f.Id == "merger").Day).ToString("yyyy-MM-dd"));
	}
}

// The tool's idea of the language must match the game's.
public class DriftTests
{
	private static string Game => Read("Scripts/Core/CampaignLogic.cs") + Read("Scripts/Core/Campaign.cs") + Read("Scripts/Core/CampaignRunner.cs");

	[Fact]
	public void EveryActionTheToolKnowsIsKnownToTheGame() { foreach (var name in Known.Actions.Keys) Assert.Matches($"\"{name}\"", Game); }

	[Fact]
	public void EveryConditionAndNumberIsKnownToTheGame() { foreach (var name in Known.Predicates.Concat(Known.Metrics)) Assert.Matches($"\"{name}\"", Game); }

	[Fact]
	public void EveryBrigadeEffectIsKnownToTheGame() { foreach (var name in Known.BrigadeEffects) Assert.Matches($"\"{name}\"", Read("Scripts/World/Convoys.cs")); }

	[Fact]
	public void TheGameHasNoActionTheToolHasNotHeardOf()
	{
		string campaign = Read("Scripts/Core/Campaign.cs");
		string effect = campaign[campaign.IndexOf("public void Effect(", StringComparison.Ordinal)..];
		string logic = Read("Scripts/Core/CampaignLogic.cs");
		string runOne = logic[logic.IndexOf("private static void RunOne(", StringComparison.Ordinal)..logic.IndexOf("// Timers that have come due", StringComparison.Ordinal)];
		foreach (var text in new[] { effect, runOne })
			foreach (Match m in Regex.Matches(text, "case \"([a-z_]+)\":"))
				Assert.True(Known.Actions.ContainsKey(m.Groups[1].Value), $"the game runs '{m.Groups[1].Value}' and the tool does not know it");
	}
}
