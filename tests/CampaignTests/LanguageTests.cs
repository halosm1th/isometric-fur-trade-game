using System;
using System.Collections.Generic;
using System.Linq;
using CampaignTool;
using Xunit;
using static CampaignTests.U;

namespace CampaignTests;

public class ConditionTests
{
	[Theory]
	[InlineData("Flag('x')", "flag:x")]
	[InlineData("Tension >= 40", "tension>=40")]
	[InlineData("Post('Fort William')", "post:Fort William")]
	[InlineData("ConvoyLate('the York boats')", "convoy_late:the York boats")]
	[InlineData("Count('seven-oaks') = 2", "count(seven-oaks) == 2")]
	[InlineData("BeatAge('b') > 30", "beat_age(b) > 30")]
	[InlineData("InMonths(6, 8)", "month:6-8")]
	[InlineData("InMonths(7)", "month:7")]
	[InlineData("InMonths(11, 3)", "month:11-3")]
	[InlineData("Chance(30)", "chance:30")]
	[InlineData("Season(Winter)", "season:Winter")]
	[InlineData("PlayerFaction(HBC)", "player_faction:HBC")]
	[InlineData("Year >= 1816", "year>=1816")]
	[InlineData("Flag(governor_killed)", "flag:governor_killed")]
	public void CompactForms(string source, string json) => Assert.Equal(json, When(source));

	[Fact]
	public void DeclaredVariablesAreNumbers() => Assert.Equal("var(mood) >= 3", When("mood >= 3", "var mood : integer;"));

	[Fact]
	public void StockTakesTwoArguments() =>
		Assert.True(Tree.Same(Tree.Dict(("m", "stock"), ("a", U.L("York Factory", "Rum")), ("op", "<"), ("v", 10.0)), When("Stock('York Factory', Rum) < 10")));

	[Fact]
	public void LogicAndPrecedence()
	{
		Assert.True(Tree.Same(Tree.Dict(("any", U.L("flag:a", Tree.Dict(("all", U.L("flag:b", "flag:c")))))), When("Flag('a') or Flag('b') and Flag('c')")));
		Assert.True(Tree.Same(Tree.Dict(("all", U.L(Tree.Dict(("any", U.L("flag:a", "flag:b"))), Tree.Dict(("not", "flag:c"))))), When("(Flag('a') or Flag('b')) and not Flag('c')")));
		Assert.True(Tree.Same(Tree.Dict(("not", "flag:a")), When("not Flag('a')")));
	}

	[Fact]
	public void ComparisonsBindTighterThanAndOr() =>
		Assert.True(Tree.Same(Tree.Dict(("all", U.L("tension>=40", "flag:x"))), When("Tension >= 40 and Flag('x')")));

	[Fact]
	public void ChainsFlattenButBracketsKeepTheirGrouping()
	{
		Assert.True(Tree.Same(Tree.Dict(("all", U.L("flag:a", "flag:b", "flag:c"))), When("Flag('a') and Flag('b') and Flag('c')")));
		Assert.True(Tree.Same(Tree.Dict(("all", U.L("flag:a", Tree.Dict(("all", U.L("flag:b", "flag:c")))))), When("Flag('a') and (Flag('b') and Flag('c'))")));
		Assert.True(Tree.Same(Tree.Dict(("all", U.L(Tree.Dict(("all", U.L("flag:a", "flag:b"))), "flag:c"))), When("(Flag('a') and Flag('b')) and Flag('c')")));
	}

	[Fact]
	public void TodayIsComparedWithADate()
	{
		Assert.Equal("after:1816-06-19", When("Today >= 1816-06-19"));
		Assert.Equal("before:1816-06-19", When("Today < 1816-06-19"));
		Assert.Equal("after:1816-06-20", When("Today > 1816-06-19"));
		Assert.Equal("before:1816-06-20", When("Today <= 1816-06-19"));
		Assert.Throws<CampError>(() => When("Today = 1816-06-19"));
	}

	[Fact]
	public void ANumberMayComeFirst() => Assert.Equal("tension<10", When("10 > Tension"));

	[Fact]
	public void ConstantsStandInForNumbers() => Assert.Equal("tension>=50", When("Tension >= Hot", "const Hot = 50;"));

	[Fact]
	public void RawPassesAConditionStraightThrough() => Assert.Equal("heard:red-river-grant", When("Raw('heard:red-river-grant')"));

	[Theory]
	[InlineData("Flag('a') or Flag('b') and Tension >= 40")]
	[InlineData("not (InMonths(6, 8) or Chance(10))")]
	[InlineData("mood < 2 and Stock('York Factory', Rum) > 5")]
	[InlineData("Today >= 1816-06-19 and ConvoyLost('the York boats')")]
	public void ConditionsMeanTheSameAfterARoundTrip(string source)
	{
		var doc = Doc((Dictionary<string, object>)Beat($"when {source};", decls: "var mood : integer;"));
		var again = Lang.CompileText(Decompiler.Decompile(doc, "T"));
		Assert.True(Tree.Same(doc["beats"], again["beats"]));
	}

	[Theory]
	[InlineData("Flag('a') and", "expected a value")]
	[InlineData("Tension >=", "expected a value")]
	[InlineData("Flag('a'", "expected ')'")]
	[InlineData("Teleport('x')", "unknown condition")]
	[InlineData("wealth > 5", "")]
	[InlineData("Tension >= 'high'", "compare with a number")]
	public void MistakesSayWhatIsWrong(string source, string expect)
	{
		var e = Assert.Throws<CampError>(() => When(source));
		Assert.Contains(expect, e.Message);
	}
}

public class StatementTests
{
	[Fact]
	public void IfElseIfElse()
	{
		var a = (Dictionary<string, object>)Happens("if Tension >= 50 then Flag('hot') else if Tension >= 20 then Flag('warm') else Flag('cool');")[0];
		Assert.Equal("if", a["do"]);
		Assert.Equal("tension>=50", a["cond"]);
		Assert.True(Tree.Same(U.L(U.Act("flag", ("name", "hot"))), a["then"]));
		var inner = (Dictionary<string, object>)((List<object>)a["else"])[0];
		Assert.Equal("tension>=20", inner["cond"]);
		Assert.True(Tree.Same(U.L(U.Act("flag", ("name", "cool"))), inner["else"]));
	}

	[Fact]
	public void RandomAfterAndVariables()
	{
		var h = Happens(@"
			case random of
			  30: Flag('lucky');
			  70: Flag('unlucky');
			end;
			after 10 days do Flag('later');
			mood := 2;  mood := mood + 3;  Inc(mood);  Dec(mood);  Dec(mood, 2);  Inc(mood, 4);  mood := mood - 1;", "var mood : integer;");
		Assert.True(Tree.Same(U.Act("random", ("options", U.L(
			Tree.Dict(("weight", 30.0), ("do", U.L(U.Act("flag", ("name", "lucky"))))),
			Tree.Dict(("weight", 70.0), ("do", U.L(U.Act("flag", ("name", "unlucky")))))))), h[0]));
		Assert.True(Tree.Same(U.Act("after", ("days", 10.0), ("then", U.L(U.Act("flag", ("name", "later"))))), h[1]));
		Assert.True(Tree.Same(U.Act("set", ("var", "mood"), ("to", 2.0)), h[2]));
		Assert.True(Tree.Same(U.Act("add", ("var", "mood"), ("by", 3.0)), h[3]));
		Assert.True(Tree.Same(U.Act("add", ("var", "mood")), h[4]));
		Assert.True(Tree.Same(U.Act("add", ("var", "mood"), ("by", -1.0)), h[5]));
		Assert.True(Tree.Same(U.Act("add", ("var", "mood"), ("by", -2.0)), h[6]));
		Assert.True(Tree.Same(U.Act("add", ("var", "mood"), ("by", 4.0)), h[7]));
		Assert.True(Tree.Same(U.Act("add", ("var", "mood"), ("by", -1.0)), h[8]));
	}

	[Fact]
	public void WorldActions()
	{
		var h = Happens(@"
			Tension(15, 'Seven Oaks');  Tension(-5);  London(0.8, 0.9);
			Brigade('the York boats', TurnBack);  Brigade('the York boats', Delay, 5);  Brigade('the York boats', Launch, 1);
			Stock('York Factory', Rum, -10);  Say('A line.', HBC);  Shout('Loud.');  Trigger('seven-oaks');  FoundPost('Fort William');");
		Assert.True(Tree.Same(U.Act("tension", ("add", 15.0), ("why", "Seven Oaks")), h[0]));
		Assert.True(Tree.Same(U.Act("tension", ("add", -5.0)), h[1]));
		Assert.True(Tree.Same(U.Act("london", ("price", 0.8), ("quota", 0.9)), h[2]));
		Assert.True(Tree.Same(U.Act("brigade", ("run", "the York boats"), ("effect", "turn_back")), h[3]));
		Assert.True(Tree.Same(U.Act("brigade", ("run", "the York boats"), ("effect", "delay"), ("days", 5.0)), h[4]));
		Assert.True(Tree.Same(U.Act("brigade", ("run", "the York boats"), ("effect", "launch"), ("leg", 1.0)), h[5]));
		Assert.True(Tree.Same(U.Act("stock", ("post", "York Factory"), ("item", "Rum"), ("add", -10.0)), h[6]));
		Assert.True(Tree.Same(U.Act("say", ("text", "A line."), ("to", "HBC")), h[7]));
		Assert.True(Tree.Same(U.Act("say", ("text", "Loud."), ("headline", true)), h[8]));
		Assert.True(Tree.Same(U.Act("trigger", ("beat", "seven-oaks")), h[9]));
		Assert.True(Tree.Same(U.Act("found_post", ("place", "Fort William")), h[10]));
	}

	[Fact]
	public void NewsWithAShortAddresseeAndAJoinedText()
	{
		var n = (Dictionary<string, object>)Happens("News('Fort Douglas', Factor, 'Blood on the plain', 'Line one.' + #10 + 'Line two ' + 'continued.', 40, HBC);")[0];
		Assert.Equal("the Factor at York Factory", n["to"]);
		Assert.Equal("Line one.\nLine two continued.", n["text"]);
		Assert.Equal(40.0, n["days"]);
		Assert.Equal("HBC", n["company"]);
	}

	[Fact]
	public void StringsAndComments()
	{
		var s = (Dictionary<string, object>)Happens("Say('it''s a {brace} // not a comment (* nor this *)'); { a comment } (* another *) // and a third\n Say('done');")[0];
		Assert.Equal("it's a {brace} // not a comment (* nor this *)", s["text"]);
	}

	[Fact]
	public void KeywordsAndBuiltinsIgnoreCase()
	{
		var h = Happens("FLAG('x'); IF tension >= 3 THEN flag('y');");
		Assert.Equal(2, h.Count);
	}

	[Fact]
	public void TheGenericActionIsAnEscapeHatch()
	{
		var a = (Dictionary<string, object>)Happens("Action('london', 'quota', 0.95);")[0];
		Assert.True(Tree.Same(U.Act("london", ("quota", 0.95)), a));
	}

	[Theory]
	[InlineData("mood := 1;", "not declared")]
	[InlineData("Inc(mood);", "not declared")]
	[InlineData("Teleport('x');", "unknown statement")]
	[InlineData("Flag();", "Flag(")]
	[InlineData("Tension();", "Tension(")]
	[InlineData("Say('a' 'b' x);", "")]
	public void Mistakes(string body, string expect)
	{
		var e = Assert.ThrowsAny<CampError>(() => Happens(body));
		Assert.Contains(expect, e.Message);
	}
}

public class StructureTests
{
	[Fact]
	public void BeatHeaderAndClauses()
	{
		var b = Beat(@"window 45; lead 90; delay 3; repeat 365;
			requires 'a', 'b'; keywords 'Semple', 'Red River';
			when Tension >= 40; when not Flag('peace'); done when Flag('seven_oaks');
			pressure Tension(10, 'armed');
			happens Flag('x');", "'seven-oaks' : event on 1816-06-19 'Seven Oaks'");
		Assert.Equal("seven-oaks", b["id"]);
		Assert.Equal("Seven Oaks", b["title"]);
		Assert.Equal("event", b["kind"]);
		Assert.Equal("1816-06-19", b["date"]);
		Assert.Equal(45.0, b["window_days"]); Assert.Equal(90.0, b["lead_days"]); Assert.Equal(3.0, b["delay_days"]); Assert.Equal(365.0, b["repeat_days"]);
		Assert.True(Tree.Same(U.L("a", "b"), b["requires"]));
		Assert.True(Tree.Same(U.L("Semple", "Red River"), b["keywords"]));
		Assert.Equal(2, ((List<object>)b["when"]).Count);
		Assert.True(Tree.Same(U.L("flag:seven_oaks"), b["done_when"]));
		Assert.Single((List<object>)b["pressure"]);
	}

	[Fact]
	public void TheTitleDefaultsToTheId() => Assert.Equal("b", Beat("")["title"]);

	[Fact]
	public void ConstantsSetTheCampaignsDetails()
	{
		var d = Lang.CompileText("campaign T; const Title = 'A'; Description = 'B'; Note = 'C'; Seed = 7; PostsByFoundingYear = true; end.");
		Assert.Equal("A", d["title"]); Assert.Equal("B", d["description"]); Assert.Equal("C", d["note"]); Assert.Equal(7.0, d["seed"]); Assert.Equal(true, d["posts_by_founding_year"]);
	}

	[Fact]
	public void ProceduresAreMacros()
	{
		var h = Happens("Dispatch(Factor, 'Hello', 30); Dispatch(Partner, 'Peace', 60);",
			"procedure Dispatch(who, subject : string; days : integer); begin News('Montreal', who, subject, 'Text.', days); end;");
		var a = (Dictionary<string, object>)h[0]; var b = (Dictionary<string, object>)h[1];
		Assert.Equal("the Factor at York Factory", a["to"]); Assert.Equal(30.0, a["days"]); Assert.Equal("Hello", a["subject"]);
		Assert.Equal("the wintering partner", b["to"]); Assert.Equal(60.0, b["days"]);
	}

	[Fact]
	public void AProcedureMayCallAnotherAndUseConstants()
	{
		var h = Happens("Both;", "const N = 3; procedure One; begin Inc(x, N); end; procedure Both; begin One; One; end; var x : integer;");
		Assert.Equal(2, h.Count);
		Assert.Equal(3.0, ((Dictionary<string, object>)h[0])["by"]);
	}

	[Fact]
	public void ALoopWritesABeatForEachValue()
	{
		var d = Lang.CompileText("campaign T; for y := 1818 to 1820 do beat 'war-' + y : pressure on y + '-06-01'; requires 'x'; end; end.");
		var beats = ((List<object>)d["beats"]).Cast<Dictionary<string, object>>().ToList();
		Assert.Equal(new[] { "war-1818", "war-1819", "war-1820" }, beats.Select(b => (string)b["id"]));
		Assert.Equal("1819-06-01", beats[1]["date"]);
	}

	[Fact]
	public void ALoopInsideAStatementUnrolls()
	{
		var h = Happens("for i := 1 to 3 do Stock('York Factory', Rum, i * 10);");
		Assert.Equal(new[] { 10.0, 20.0, 30.0 }, h.Cast<Dictionary<string, object>>().Select(a => (double)a["add"]));
	}

	[Fact]
	public void LoopsMayNest()
	{
		var d = Lang.CompileText("campaign T; for y := 1 to 2 do begin for z := 1 to 2 do beat 'b' + y + z : conditional; end; end; end.");
		Assert.Equal(new[] { "b11", "b12", "b21", "b22" }, ((List<object>)d["beats"]).Cast<Dictionary<string, object>>().Select(b => (string)b["id"]));
	}

	[Theory]
	[InlineData("procedure P; begin P; end; beat 'b' : event; happens P; end;", "calls itself")]
	[InlineData("procedure P(a); begin Flag(a); end; beat 'b' : event; happens P; end;", "takes 1 argument")]
	[InlineData("beat 'b' : sideways; end;", "kind must be")]
	[InlineData("beat 'b' : event; window; end;", "expected a value")]
	[InlineData("beat 'b' : event; frobnicate 3; end;", "unknown beat clause")]
	[InlineData("beat 'b' : event; ", "expected ';'")]
	[InlineData("beat 'b' : event on 1816-06-31 'x'; end;", "")]
	public void Mistakes(string body, string expect)
	{
		var e = Record.Exception(() => { var d = Lang.CompileText($"campaign T;\n{body}\nend.\n"); Checker.Check(d).Errors.ForEach(x => throw new CampError(x.msg)); });
		Assert.IsType<CampError>(e);
		Assert.Contains(expect, e.Message);
	}

	[Fact]
	public void ErrorsSayWhichLine()
	{
		var e = Assert.Throws<CampError>(() => Lang.CompileText("campaign T;\n\nbeat 'b' : event;\n  window;\nend;\nend.\n"));
		Assert.Equal(4, e.Line);
		Assert.StartsWith("line 4:", e.Message);
	}

	[Fact]
	public void LexicalErrors()
	{
		Assert.Throws<CampError>(() => Lang.CompileText("campaign T; { never closed"));
		Assert.Throws<CampError>(() => Lang.CompileText("campaign T; (* never closed"));
		Assert.Throws<CampError>(() => Lang.CompileText("campaign T; const A = 'no end\n; end."));
		Assert.Throws<CampError>(() => Lang.CompileText("campaign T; const A = 3 @ 4; end."));
		Assert.Throws<CampError>(() => Lang.CompileText("campaign T; end. beat 'x' : event; end;"));
	}
}

public class RoundTripTests
{
	private static void RoundTrips(Dictionary<string, object> json)
	{
		var again = Lang.CompileText(Decompiler.Decompile(json, "Script"));
		Assert.True(Tree.Same(json, again), "the decompiled script does not compile back to the same JSON");
	}

	[Theory]
	[InlineData("Campaigns/historical.json")]
	[InlineData("Campaigns/Gemini and Clark.json")]
	public void TheShippedCampaignsSurvive(string path) => RoundTrips(Json(path));

	[Fact]
	public void TheShowcaseSurvives() => RoundTrips(Lang.CompileText(Read("tools/CampaignTool/examples/showcase.camp")));

	[Fact]
	public void TheHistoricalSourceSurvives() => RoundTrips(Lang.CompileText(Read("tools/CampaignTool/src/historical.camp")));

	[Fact]
	public void TextsWithNewLinesAndQuotesSurvive()
	{
		var json = Doc(B("a", "event", "1816-06-19", ("happens", L(Act("news", ("from", "Montreal"), ("to", "the wintering partner"), ("subject", "It's \"news\""), ("text", "One.\n\nTwo, with 'quotes' and a very long line that has to be broken into pieces to fit the page nicely."), ("days", 40.0))))));
		RoundTrips(json);
	}
}
