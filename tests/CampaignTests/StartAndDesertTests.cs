using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CampaignTool;
using IsometricFurTradeGame.Scripts.Core;
using Xunit;
using static CampaignTests.U;

namespace CampaignTests;

// Who the players come in as (startPath, startAt), and a player who leaves
// his side (player.desert, player.pardon, player.deserted): the language,
// the checker, the decompiler and the simulator.
public class StartAndDesertTests
{
	private const string Soldier = @"
campaign Rogue(title: ""The rogue"", start: date(1812, 6, 4), startPath: Path.Crown, startAt: ""York"");

beat GoesOver(date: date(1812, 7, 1)) {
    OnActivate() { player.desert(Company.American); }
}
beat Wanted(date: date(1812, 8, 1)) {
    AwakenWhen() => player.deserted && world.playerFaction == Company.American && player.did(""desert"");
    OnActivate() { say(""A deserter is cried at York."", Company.Crown); }
}
beat Pardoned(date: date(1813, 1, 1)) {
    OnActivate() { player.pardon(); }
}
beat Forgiven(date: date(1813, 2, 1)) {
    ActivateWhen() => !player.deserted;
    OnActivate() { say(""The King's pardon is read.""); }
}
";

	// ---- the header -----------------------------------------------------------------

	[Fact]
	public void TheStartIsWrittenForTheGame()
	{
		var doc = Compile(Soldier);
		Assert.Equal("Crown", doc["start_path"]);
		Assert.Equal("York", doc["start_at"]);
		var script = CampaignScript.Parse(doc);
		Assert.Equal("Crown", script.StartPath);
		Assert.Equal("York", script.StartAt);
	}

	[Fact]
	public void EitherCanBeGivenAlone()
	{
		Assert.False(Compile("campaign T(title: \"t\", startAt: \"Fort William\");").ContainsKey("start_path"));
		Assert.False(Compile("campaign T(title: \"t\", startPath: Path.NWC);").ContainsKey("start_at"));
		Assert.Equal("", CampaignScript.Parse(Compile("campaign T(title: \"t\");")).StartPath);
	}

	[Theory]
	[InlineData("startPath: \"Crown\"", "startPath is a road in")]
	[InlineData("startPath: Company.Crown", "startPath is a road in")]
	[InlineData("startPath: Path.Soldier", "Path has no 'Soldier'")]
	[InlineData("startAt: 5", "startAt is a place by its name")]
	[InlineData("startAt: \"  \"", "startAt is a place by its name")]
	[InlineData("startpath: Path.Crown", "Did you mean 'startPath'")]
	public void BadStartsAreExplained(string header, string message)
	{
		var e = Assert.Throws<CampError>(() => Compile($"campaign T(title: \"t\", {header});"));
		Assert.Contains(message, e.Message);
	}

	[Fact]
	public void TheCheckerKnowsTheRoadsAndThePlaces()
	{
		string[] Errors(string path, string at)
		{
			var doc = Doc();
			if (path != null) doc["start_path"] = path;
			if (at != null) doc["start_at"] = at;
			return Checker.Check(doc).Errors.Select(e => e.msg).ToArray();
		}
		Assert.Contains(Errors("Soldier", null), m => m.Contains("not a road in"));
		Assert.Contains(Errors(null, "Nowhere Landing"), m => m.Contains("not a post, town, fort, camp or place"));
		Assert.Empty(Errors("Crown", "York"));
		Assert.Empty(Errors("Crown", "Fort George"));         // a fort, by its own name
		Assert.Empty(Errors("HBC", "Edmonton House"));
		Assert.Empty(Errors("Nation", Known.World.Camps.First()));
	}

	[Fact]
	public void ASoldierSentWhereTheCrownHasNoGarrisonIsWarned()
	{
		List<string> Warnings(string at) { var d = Doc(); d["start_path"] = "Crown"; d["start_at"] = at; return Checker.Check(d).Warnings.Select(w => w.msg).ToList(); }
		Assert.Contains(Warnings("Detroit"), w => w.Contains("no garrison of the Crown"));          // American in 1811
		Assert.Contains(Warnings("Queenston"), w => w.Contains("no garrison of the Crown"));        // a town without a fort
		Assert.DoesNotContain(Warnings("Amherstburg"), w => w.Contains("garrison"));
	}

	// ---- desert, pardon, deserted -----------------------------------------------------

	[Fact]
	public void DesertingCompilesToOneAction()
	{
		Assert.Equal(L(Act("desert")), Happens("player.desert();"));
		Assert.Equal(L(Act("desert", ("to", "American"))), Happens("player.desert(Company.American);"));
		Assert.Equal(L(Act("desert", ("to", "Crown"))), Happens("player.desert(to: Company.Crown);"));
		Assert.Equal(L(Act("pardon")), Happens("player.pardon();"));
	}

	[Theory]
	[InlineData("player.desert(Company.All);")]
	[InlineData("player.desert(Company.Free);")]
	public void YouDesertToASideOrToNobody(string stmt)
	{
		var e = Assert.Throws<CampError>(() => Happens(stmt));
		Assert.Contains("leave to out to belong to nobody", e.Message);
	}

	[Fact]
	public void DesertedIsACondition()
	{
		var c = When("player.deserted");
		Assert.Equal(Tree.Dict(("p", "deserted"), ("a", L())), c);
		Assert.Throws<CampError>(() => When("player.desert()"));   // an effect, not a condition
	}

	[Fact]
	public void TheCheckerKnowsWhoYouCanGoOverTo()
	{
		var bad = B("b", "fixed", "1812-07-01", ("happens", L(Act("desert", ("to", "Free")))));
		Assert.Contains(Checker.Check(Doc(bad)).Errors, e => e.msg.Contains("desert: to must be one of"));
		var good = B("b", "fixed", "1812-07-01", ("happens", L(Act("desert", ("to", "American")), Act("pardon"), Act("say", ("text", "x"), ("to", "Crown")))));
		Assert.Empty(Checker.Check(Doc(good)).Errors);
	}

	[Fact]
	public void ItAllRoundTripsThroughTheDecompiler()
	{
		var doc = Compile(Soldier);
		string src = Decompiler.Decompile(doc, "Rogue");
		Assert.Contains("startPath: Path.Crown", src);
		Assert.Contains("startAt: \"York\"", src);
		Assert.Contains("player.desert(Company.American);", src);
		Assert.Contains("player.pardon();", src);
		Assert.Contains("player.deserted", src);
		Assert.Contains("Company.Crown", src);
		Assert.True(Tree.Same(doc, Compile(src)));
	}

	[Fact]
	public void QuestsCanBeForASoldierOrForADeserter()
	{
		var doc = Compile(@"
campaign T(title: ""t"", startPath: Path.Crown);
questline Ranks(title: ""In the ranks"", forCompany: Company.Crown) {
    quest Muster(title: ""Muster"") {
        reach Parade(place: ""Fort York"");
        talk Orders(who: ""officer@Fort George"", text: ""Report to the officer at Fort George"");
        talk Mess(who: ""sergeant@York"");
    }
}
questline Run(title: ""On the run"", forCompany: Company.Free) {
    quest Hide(title: ""Lie low"") { wait Low(days: 3); }
}");
		var lines = ((List<object>)doc["questlines"]).Cast<Dictionary<string, object>>().ToList();
		Assert.Equal("Crown", lines[0]["for"]);
		Assert.Equal("Free", lines[1]["for"]);
		Assert.Empty(Checker.Check(doc).Errors);
		Assert.True(Tree.Same(doc, Compile(Decompiler.Decompile(doc, "T"))));
	}

	// ---- the simulator ------------------------------------------------------------------

	[Fact]
	public void TheSimulatorPlaysTheSoldierWhoGoesOver()
	{
		var r = Sim.Simulate(Compile(Soldier), Tree.Dict(("end", "1813-06-01")), 1);
		var fired = r.Runner.Fired.Select(f => f.Id).ToList();
		Assert.Equal(new[] { "GoesOver", "Wanted", "Pardoned", "Forgiven" }, fired);
		Assert.Contains("deed:desert", r.Runner.State.Flags);
		Assert.Equal(1.0, r.Runner.State.Vars["deed:desert"]);
	}

	[Fact]
	public void TheScriptsRoadIsThePlayersSideInTheSimulator()
	{
		var doc = Compile("campaign T(title: \"t\", startPath: Path.Crown);\nbeat B(date: date(1812, 1, 1)) { ActivateWhen() => world.playerFaction == Company.Crown; OnActivate() { say(\"x\"); } }");
		Assert.Contains(Sim.Simulate(doc, null, 1).Runner.Fired, f => f.Id == "B");
		Assert.DoesNotContain(Sim.Simulate(doc, Tree.Dict(("player_faction", "HBC")), 1).Runner.Fired, f => f.Id == "B");   // the scenario still wins
	}

	[Fact]
	public void DesertingToNobodyLeavesYouFree()
	{
		var doc = Compile("campaign T(title: \"t\", startPath: Path.Crown);\nbeat A(date: date(1812, 1, 1)) { OnActivate() { player.desert(); } }\nbeat B(date: date(1812, 2, 1)) { ActivateWhen() => world.playerFaction == Company.Free && player.deserted; OnActivate() { say(\"x\"); } }");
		Assert.Contains(Sim.Simulate(doc, Tree.Dict(("end", "1812-12-31")), 1).Runner.Fired, f => f.Id == "B");
	}

	// ---- the editor's Play in game, and the game's own lists ------------------------------

	[Fact]
	public void PlayInGameStartsOnTheCampaignsRoad()
	{
		var doc = Compile(@"
campaign T(title: ""t"", startPath: Path.Crown);
questline Ranks(title: ""In the ranks"") {
    quest Muster(title: ""Muster"") { reach Parade(place: ""York""); }
}");
		Assert.Equal("--start=Crown", Launch.For("t", doc, "Muster", null).Args[1]);
	}

	[Fact]
	public void TheRoadsAreTheGamesRoads()
	{
		string sheet = Read("Scripts/Core/CharacterSheet.cs");
		var roads = Regex.Match(sheet, @"enum StartPath \{([^}]*)\}").Groups[1].Value.Split(',').Select(s => s.Trim());
		Assert.Equal(roads, Library.Paths);
		var sides = Regex.Match(Read("Scripts/Agents/Agent.cs"), @"enum Faction \{([^}]*)\}").Groups[1].Value.Split(',').Select(s => s.Trim()).ToHashSet();
		foreach (var c in Known.Companies) Assert.Contains(c, sides);
	}
}
