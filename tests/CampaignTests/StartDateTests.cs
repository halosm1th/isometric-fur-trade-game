using System.Linq;
using CampaignTool;
using IsometricFurTradeGame.Scripts.Core;
using Xunit;
using static CampaignTests.U;

namespace CampaignTests;

// A campaign can say when the world begins: start (the first day itself, for
// every starting path) or startYear (each path's own day, in that year).
public class StartDateTests
{
	private const string Later = @"
campaign T(title: ""t"", start: date(1815, 5, 1));
beat Before(date: date(1814, 9, 1)) { OnActivate() { flags.set(""before""); } }
beat After(date: date(1815, 6, 1)) { OnActivate() { say(""after""); } }
";

	[Fact]
	public void StartIsWrittenAsADay()
	{
		var doc = Compile(Later);
		Assert.Equal("1815-05-01", doc["start"]);
		Assert.Equal("1815-05-01", CampaignScript.Parse(doc).Start);
	}

	[Fact]
	public void StartYearIsWrittenAsAYear()
	{
		var doc = Compile("campaign T(title: \"t\", startYear: 1820);");
		Assert.Equal(1820.0, doc["start_year"]);
		Assert.Equal(1820, CampaignScript.Parse(doc).StartYear);
	}

	[Theory]
	[InlineData("start: 1815", "start is the world's first day")]
	[InlineData("start: date(1815, 5, 1), startYear: 1815", "not both")]
	[InlineData("startYear: 1815.5", "startYear is a year")]
	[InlineData("starts: date(1815, 5, 1)", "Did you mean 'start'")]
	public void BadStartsAreExplained(string header, string message)
	{
		var e = Assert.Throws<CampError>(() => Compile($"campaign T(title: \"t\", {header});"));
		Assert.Contains(message, e.Message);
	}

	[Fact]
	public void ItRoundTripsThroughTheDecompiler()
	{
		foreach (var src in new[] { Later, "campaign T(title: \"t\", startYear: 1820);" })
		{
			var doc = Compile(src);
			Assert.True(Tree.Same(doc, Compile(Decompiler.Decompile(doc, "T"))));
		}
	}

	[Fact]
	public void TheCheckerWantsARealDay()
	{
		var doc = Doc(); doc["start"] = "1815-02-30";
		Assert.Contains(Checker.Check(doc).Errors, e => e.msg.Contains("not a real day"));
	}

	[Fact]
	public void TheSimulatorBeginsOnTheScriptsDay()
	{
		var r = Sim.Simulate(Compile(Later), null, 1);
		Assert.Equal("1815-05-01", r.Start);
		Assert.Contains("Before", r.Runner.History);                 // dated before the first day: history
		Assert.Contains(r.Runner.Fired, f => f.Id == "After");
	}

	[Fact]
	public void AScenarioStartStillWins()
	{
		var r = Sim.Simulate(Compile(Later), Tree.Dict(("start", "1814-08-01")), 1);
		Assert.Equal("1814-08-01", r.Start);
		Assert.DoesNotContain("Before", r.Runner.History);
	}

	[Fact]
	public void StartYearInTheSimulatorIsTheShipsDayThatYear()
	{
		var r = Sim.Simulate(Compile("campaign T(title: \"t\", startYear: 1825);\nbeat B(date: date(1826, 1, 1)) { OnActivate() { say(\"x\"); } }"), null, 1);
		Assert.Equal("1825-08-20", r.Start);
		Assert.Contains(r.Runner.Fired, f => f.Id == "B");          // the end moves past the start
	}
}

public class LibraryDocsTests
{
	[Fact]
	public void EveryArgumentOfEveryFunctionIsDescribed()
	{
		var missing = Library.All.SelectMany(m => m.Params.Where(p => string.IsNullOrWhiteSpace(p.Doc)).Select(p => $"{m.Full}({p.Name})")).ToList();
		Assert.True(missing.Count == 0, "no description for: " + string.Join(", ", missing));
		var stale = Library.ParamDocs.Keys.Where(k => !Library.All.Any(m => m.Params.Any(p => $"{m.Full}.{p.Name}" == k))).ToList();
		Assert.True(stale.Count == 0, "described but not an argument: " + string.Join(", ", stale));
	}
}
