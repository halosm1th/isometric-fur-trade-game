using System.Collections.Generic;
using System.Linq;
using CampaignTool;
using IsometricFurTradeGame.Scripts.Core;
using Xunit;

namespace CampaignTests;

// Quest lines: the language, the decompiler, the checker, and the chain the game walks.
public class QuestTests
{
	private const string Sample = @"
campaign T(title: ""t"", extends: ""historical"");

questline Servant(title: ""The Company's Servant"", forCompany: Company.HBC) {
    quest Landfall(title: ""Report"", summary: ""You are the new hand."") {
        OnComplete() { player.postTo(""Edmonton House""); }
        talk Report(who: Person.Factor, text: ""Report to the Factor"") {
            Says() => ""He looks you over."";
            choice Keen(label: ""Put me to work."", reply: ""Good."", goto: Unload) { player.addStanding(2); }
            choice Sullen(label: ""Supper first?"", when: world.standing < 5, goto: ""fail"");
        }
        task Unload(task: Task.CarryCargo, count: 6, text: ""Unload the ship"");
        reach Inland(place: ""Edmonton House"", radius: 600);
    }
    quest Neighbours(id: ""the-neighbours"", title: ""The other house"", giver: ""leader@Edmonton House"") [Landfall] {
        StartWhen() => Landfall.done;
        talk Meet(who: ""partner@Fort Augustus"", next: Hunt);
        wait Rest(days: 2);
        hunt Hunt(species: ""Buffalo"", count: 2);
        bring Gift(who: ""chief@Beaver Hills Cree (Amiskwacîwiyiniwak)"", item: Item.Tobacco, count: 2) { Says() => ""He takes it.""; }
        when Done(text: ""Wait for word"") => flags.has(""word_came"");
    }
}

beat Word(date: date(1812, 1, 1)) {
    AwakenWhen() => Neighbours.active;
    OnActivate() { flags.set(""word_came""); Neighbours.complete(); }
}
";

	private static List<Dictionary<string, object>> Quests(Dictionary<string, object> doc) =>
		((List<object>)((Dictionary<string, object>)((List<object>)doc["questlines"])[0])["quests"]).Cast<Dictionary<string, object>>().ToList();

	private static List<Dictionary<string, object>> Steps(Dictionary<string, object> q) =>
		((List<object>)q["objectives"]).Cast<Dictionary<string, object>>().ToList();

	[Fact]
	public void QuestsCompileToTheShapeTheGameReads()
	{
		var doc = U.Compile(Sample);
		Assert.Equal("historical", doc["extends"]);
		var line = (Dictionary<string, object>)((List<object>)doc["questlines"])[0];
		Assert.Equal("HBC", line["for"]);
		var qs = Quests(doc);
		Assert.Equal(new[] { "Landfall", "the-neighbours" }, qs.Select(q => q["id"]));
		Assert.Equal(new List<object> { "Landfall" }, qs[1]["requires"]);
		Assert.Equal(new List<object> { "quest:Landfall" }, qs[1]["start_when"]);
		var report = Steps(qs[0])[0];
		Assert.Equal("Factor", report["who"]);
		var choices = (List<object>)((Dictionary<string, object>)report["dialogue"])["choices"];
		Assert.Equal("Unload", ((Dictionary<string, object>)choices[0])["goto"]);
		Assert.Equal("fail", ((Dictionary<string, object>)choices[1])["goto"]);
		Assert.Equal(new List<object> { "standing<5" }, ((Dictionary<string, object>)choices[1])["when"]);
		Assert.Equal("CarryCargo", Steps(qs[0])[1]["task"]);
		Assert.Equal("Hunt", Steps(qs[1])[0]["next"]);
		// A beat can ask after a quest, and end it.
		var beat = (Dictionary<string, object>)((List<object>)doc["beats"])[0];
		Assert.Equal(new List<object> { "quest_active:the-neighbours" }, beat["when"]);
	}

	[Fact]
	public void QuestsSurviveTheRoundTrip()
	{
		var doc = U.Compile(Sample);
		var back = U.Compile(Decompiler.Decompile(doc, "T"));
		Assert.True(Tree.Same(doc, back), Tree.Compact(back));
	}

	[Theory]
	[InlineData("talk A(who: \"x\") { choice C(label: \"y\", goto: Nowhere); }", "not defined")]
	[InlineData("talk A(text: \"x\");", "talk step needs who")]
	[InlineData("reach A(text: \"x\");", "reach step needs place")]
	[InlineData("wait A(days: 1) { choice C(label: \"y\"); }", "only a talk step has choices")]
	[InlineData("task A(task: Task.Brigade);", "Brigade task needs convoy")]
	[InlineData("talk A(who: \"x\", colour: \"red\");", "has no 'colour'")]
	[InlineData("talk A(who: \"x\"); talk A(who: \"y\");", "two steps called A")]
	public void StepMistakesAreCaught(string steps, string expect)
	{
		var src = $"campaign T(title: \"t\");\nquestline L(title: \"l\") {{ quest Q(title: \"q\") {{ {steps} }} }}";
		var e = Assert.Throws<CampError>(() => U.Compile(src));
		Assert.Contains(expect, e.Message);
	}

	[Fact]
	public void TheCheckerKnowsTheWorldQuestsName()
	{
		var src = @"campaign T(title: ""t"");
questline L(title: ""l"") {
    quest Q(title: ""q"") {
        reach A(place: ""Atlantis"");
        talk B(who: ""leader@Nowhere House"");
        bring C(who: ""Factor"", item: Item.Tobacco) { Says() => ""x""; }
    }
}
beat X(date: date(1812, 1, 1)) { OnActivate() { player.postTo(""Nowhere House""); } }";
		var rep = Checker.Check(U.Compile(src));
		var lines = string.Join("\n", rep.Lines());
		Assert.Contains("'Atlantis' is not a post, a camp or a place", lines);
		Assert.Contains("'Nowhere House' is not a post, a camp or a garrisoned town", lines);
		Assert.Contains("posting: 'Nowhere House' is not a post with books", lines);
		Assert.Empty(Checker.Check(U.Compile(Sample)).Errors);
	}

	[Fact]
	public void TheChainGoesWhereItIsSent()
	{
		var lines = QuestParse.Lines(((Dictionary<string, object>)U.Compile(Sample))["questlines"]);
		var landfall = lines[0].Quests[0];
		var neighbours = lines[0].Quests[1];
		Assert.Equal("HBC", landfall.For);   // a quest takes its line's company
		Assert.Equal(2, landfall.After(0, "Inland"));
		Assert.Equal(1, landfall.After(0));
		Assert.Equal(QuestDef.Fail, landfall.After(0, "fail"));
		Assert.Equal(QuestDef.Complete, landfall.After(2));                 // past the last step
		Assert.Equal(neighbours.IndexOf("Hunt"), neighbours.After(0));       // the step's own next
		Assert.Equal(6, landfall.Objectives[1].Count);
		Assert.Equal(400f, neighbours.Objectives[0].Radius);                 // defaults when not written
		Assert.Equal("He takes it.", neighbours.Objectives[3].Dialogue.Text);
	}
}
