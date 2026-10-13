using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CampaignTool;
using Xunit;
using static CampaignTests.U;

namespace CampaignTests;

public class LexerTests
{
	[Fact]
	public void CommentsAndEscapes()
	{
		var h = Happens("say(\"a \\\"quote\\\" \\\\ and // not a comment /* nor this */\"); // trailing\n /* block\n comment */ say(\"b\\n\\tc\");");
		Assert.Equal("a \"quote\" \\ and // not a comment /* nor this */", ((Dictionary<string, object>)h[0])["text"]);
		Assert.Equal("b\n\tc", ((Dictionary<string, object>)h[1])["text"]);
	}

	[Theory]
	[InlineData("campaign T(title: \"x);", "not closed")]
	[InlineData("campaign T(title: \"x\"); /* never", "never closed")]
	[InlineData("campaign T(title: \"a\\q\");", "unknown escape")]
	[InlineData("campaign T(title: \"x\"); #", "unexpected character")]
	public void LexicalErrors(string src, string expect)
	{
		var e = Assert.Throws<CampError>(() => Compile(src));
		Assert.Contains(expect, e.Message);
	}

	[Fact]
	public void ErrorsSayWhichLineAndColumn()
	{
		var e = Assert.Throws<CampError>(() => Compile("campaign T(title: \"t\");\n\nbeat B(wat: 3);\n"));
		Assert.Equal(3, e.Line);
		Assert.StartsWith("line 3:", e.Message);
		Assert.Equal(8, e.At.Col);
	}
}

public class ConditionTests
{
	[Theory]
	[InlineData("flags.has(\"x\")", "flag:x")]
	[InlineData("world.tension >= 40", "tension>=40")]
	[InlineData("world.hasPost(\"Fort William\")", "post:Fort William")]
	[InlineData("brigade(\"the York boats\").isLate", "convoy_late:the York boats")]
	[InlineData("brigade(\"the York boats\").wasTurnedBack", "convoy_turned_back:the York boats")]
	[InlineData("brigade(\"the York boats\").daysLate > 3", "convoy_days_late(the York boats) > 3")]
	[InlineData("world.inMonths(6, 8)", "month:6-8")]
	[InlineData("world.inMonths(7)", "month:7")]
	[InlineData("world.inMonths(11, 3)", "month:11-3")]
	[InlineData("chance(30)", "chance:30")]
	[InlineData("world.season == Season.Winter", "season:Winter")]
	[InlineData("world.playerFaction == Company.HBC", "player_faction:HBC")]
	[InlineData("world.year >= 1816", "year>=1816")]
	[InlineData("world.dayOfYear < 100", "day_of_year<100")]
	[InlineData("world.convoysActive == 0", "convoys_active==0")]
	[InlineData("world.isDead(\"Semple\")", "dead:Semple")]
	[InlineData("raw(\"heard:red-river-grant\")", "heard:red-river-grant")]
	public void CompactForms(string source, string json) => Assert.Equal(json, When(source));

	[Fact]
	public void BeatsAreNamesAndHaveMembers()
	{
		const string decls = "beat SevenOaks(id: \"seven-oaks\");";
		Assert.Equal("beat:seven-oaks", When("SevenOaks.done", decls));
		Assert.Equal("count(seven-oaks) == 2", When("SevenOaks.count == 2", decls));
		Assert.Equal("beat_age(seven-oaks) > 30", When("SevenOaks.age > 30", decls));
	}

	[Fact]
	public void DeclaredVariablesAreNumbers() => Assert.Equal("var(mood) >= 3", When("mood >= 3", "int mood;"));

	[Fact]
	public void StockTakesAPostAndAnItem() =>
		Assert.True(Tree.Same(Tree.Dict(("m", "stock"), ("a", L("York Factory", "Rum")), ("op", "<"), ("v", 10.0)), When("world.stock(\"York Factory\", Item.Rum) < 10")));

	[Fact]
	public void HookConditionsAreLists_AndChainsFlatten()
	{
		Assert.Equal(new object[] { "flag:a", "flag:b", "flag:c" }, Whens("flags.has(\"a\") && flags.has(\"b\") && flags.has(\"c\")").ToArray());
		Assert.Single(Whens("(flags.has(\"a\") && flags.has(\"b\"))"));
		Assert.True(Tree.Same(Tree.Dict(("all", L("flag:a", "flag:b"))), When("(flags.has(\"a\") && flags.has(\"b\"))")));
		Assert.True(Tree.Same(L("flag:a", Tree.Dict(("all", L("flag:b", "flag:c")))), Whens("flags.has(\"a\") && (flags.has(\"b\") && flags.has(\"c\"))")));
	}

	[Fact]
	public void LogicAndPrecedence()
	{
		Assert.True(Tree.Same(Tree.Dict(("any", L("flag:a", Tree.Dict(("all", L("flag:b", "flag:c")))))), When("flags.has(\"a\") || flags.has(\"b\") && flags.has(\"c\")")));
		Assert.True(Tree.Same(L(Tree.Dict(("any", L("flag:a", "flag:b"))), Tree.Dict(("not", "flag:c"))), Whens("(flags.has(\"a\") || flags.has(\"b\")) && !flags.has(\"c\")")));
		Assert.True(Tree.Same(Tree.Dict(("not", "flag:a")), When("!flags.has(\"a\")")));
		Assert.True(Tree.Same(L("tension>=40", "flag:x"), Whens("world.tension >= 40 && flags.has(\"x\")")));
	}

	[Fact]
	public void TodayIsComparedWithADate()
	{
		Assert.Equal("after:1816-06-19", When("world.date >= date(1816, 6, 19)"));
		Assert.Equal("before:1816-06-19", When("world.date < date(1816, 6, 19)"));
		Assert.Equal("after:1816-06-20", When("world.date > date(1816, 6, 19)"));
		Assert.Equal("before:1816-06-20", When("world.date <= date(1816, 6, 19)"));
		Assert.Throws<CampError>(() => When("world.date == date(1816, 6, 19)"));
	}

	[Fact]
	public void DateArithmetic() => Assert.Equal("after:1816-07-04", When("world.date >= date(1816, 6, 19) + days(15)"));

	[Fact]
	public void ANumberMayComeFirst() => Assert.Equal("tension<10", When("10 > world.tension"));

	[Fact]
	public void ConstantsStandInForNumbersAndAreFolded()
	{
		Assert.Equal("tension>=50", When("world.tension >= hot", "const int hot = 50;"));
		Assert.Equal("tension>=60", When("world.tension >= hot + 10", "const int hot = 50;"));
		Assert.Equal(true, When("1 < 2"));
	}

	[Fact]
	public void ValueFunctionsAreInlined() =>
		Assert.Equal("tension>=70", When("severe()", "bool severe() => world.tension >= 70;"));

	[Fact]
	public void FunctionsTakeArguments() =>
		Assert.Equal("convoy_late:the York boats", When("late(\"the York boats\")", "bool late(string run) => brigade(run).isLate;"));

	[Theory]
	[InlineData("flags.has(\"a\") &&", "expected a value")]
	[InlineData("world.tension >=", "expected a value")]
	[InlineData("flags.has(\"a\"", "expected ')'")]
	[InlineData("teleport(\"x\")", "no function called teleport")]
	[InlineData("wealth > 5", "'wealth' is not defined")]
	[InlineData("world.tension >= \"high\"", "compare with a number")]
	[InlineData("world.tension", "this is a number, not a condition")]
	[InlineData("world.tensoin > 3", "Did you mean 'tension'")]
	[InlineData("flags.hass(\"a\")", "Did you mean 'has'")]
	[InlineData("world.season == Company.HBC", "compared with Season")]
	[InlineData("flags.set(\"x\")", "changes the world")]
	[InlineData("world.tension > world.standing", "not with another changing number")]
	[InlineData("flags.has(3)", "should be text")]
	[InlineData("flags.has()", "needs name")]
	[InlineData("flags.has(\"a\", \"b\")", "takes 1 argument")]
	[InlineData("world.hasPost(place: \"x\", wat: 1)", "no argument 'wat'")]
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
		var a = (Dictionary<string, object>)Happens("if (world.tension >= 50) { flags.set(\"hot\"); } else if (world.tension >= 20) { flags.set(\"warm\"); } else { flags.set(\"cool\"); }")[0];
		Assert.Equal("if", a["do"]);
		Assert.Equal("tension>=50", a["cond"]);
		Assert.True(Tree.Same(L(Act("flag", ("name", "hot"))), a["then"]));
		var inner = (Dictionary<string, object>)((List<object>)a["else"])[0];
		Assert.Equal("tension>=20", inner["cond"]);
		Assert.True(Tree.Same(L(Act("flag", ("name", "cool"))), inner["else"]));
	}

	[Fact]
	public void AnIfOnAKnownConstantChoosesAtCompileTime()
	{
		var h = Happens("if (debug) { flags.set(\"on\"); } else { flags.set(\"off\"); }", "const bool debug = false;");
		Assert.True(Tree.Same(L(Act("flag", ("name", "off"))), h));
	}

	[Fact]
	public void RandomAfterAndVariables()
	{
		var h = Happens(@"
			random(option(30, () => flags.set(""lucky"")), option(70, () => { flags.set(""unlucky""); }));
			after(days(10), () => flags.set(""later""));
			mood = 2;  mood = mood + 3;  mood++;  mood--;  mood -= 2;  mood += 4;  mood = mood - 1;", "int mood;");
		Assert.True(Tree.Same(Act("random", ("options", L(
			Tree.Dict(("weight", 30.0), ("do", L(Act("flag", ("name", "lucky"))))),
			Tree.Dict(("weight", 70.0), ("do", L(Act("flag", ("name", "unlucky")))))))), h[0]));
		Assert.True(Tree.Same(Act("after", ("days", 10.0), ("then", L(Act("flag", ("name", "later"))))), h[1]));
		Assert.True(Tree.Same(Act("set", ("var", "mood"), ("to", 2.0)), h[2]));
		Assert.True(Tree.Same(Act("add", ("var", "mood"), ("by", 3.0)), h[3]));
		Assert.True(Tree.Same(Act("add", ("var", "mood")), h[4]));
		Assert.True(Tree.Same(Act("add", ("var", "mood"), ("by", -1.0)), h[5]));
		Assert.True(Tree.Same(Act("add", ("var", "mood"), ("by", -2.0)), h[6]));
		Assert.True(Tree.Same(Act("add", ("var", "mood"), ("by", 4.0)), h[7]));
		Assert.True(Tree.Same(Act("add", ("var", "mood"), ("by", -1.0)), h[8]));
	}

	[Fact]
	public void WorldActions()
	{
		var h = Happens(@"
			world.raiseTension(15, ""Seven Oaks"");  world.raiseTension(-5);  world.setLondon(0.8, 0.9);
			brigade(""the York boats"").turnBack();  brigade(""the York boats"").delay(5);  brigade(""the York boats"").launch(1);
			brigade(""the York boats"").lose();  brigade(""the York boats"").cancel();
			world.addStock(""York Factory"", Item.Rum, -10);  say(""A line."", Company.HBC);  shout(""Loud."");  trigger(""seven-oaks"");
			world.foundPost(""Fort William"");  flags.clear(""calm"");");
		Assert.True(Tree.Same(Act("tension", ("add", 15.0), ("why", "Seven Oaks")), h[0]));
		Assert.True(Tree.Same(Act("tension", ("add", -5.0)), h[1]));
		Assert.True(Tree.Same(Act("london", ("price", 0.8), ("quota", 0.9)), h[2]));
		Assert.True(Tree.Same(Act("brigade", ("run", "the York boats"), ("effect", "turn_back")), h[3]));
		Assert.True(Tree.Same(Act("brigade", ("run", "the York boats"), ("effect", "delay"), ("days", 5.0)), h[4]));
		Assert.True(Tree.Same(Act("brigade", ("run", "the York boats"), ("effect", "launch"), ("leg", 1.0)), h[5]));
		Assert.True(Tree.Same(Act("brigade", ("run", "the York boats"), ("effect", "lose")), h[6]));
		Assert.True(Tree.Same(Act("brigade", ("run", "the York boats"), ("effect", "cancel")), h[7]));
		Assert.True(Tree.Same(Act("stock", ("post", "York Factory"), ("item", "Rum"), ("add", -10.0)), h[8]));
		Assert.True(Tree.Same(Act("say", ("text", "A line."), ("to", "HBC")), h[9]));
		Assert.True(Tree.Same(Act("say", ("text", "Loud."), ("headline", true)), h[10]));
		Assert.True(Tree.Same(Act("trigger", ("beat", "seven-oaks")), h[11]));
		Assert.True(Tree.Same(Act("found_post", ("place", "Fort William")), h[12]));
		Assert.True(Tree.Same(Act("unflag", ("name", "calm")), h[13]));
	}

	[Fact]
	public void TriggerTakesABeatByName() =>
		Assert.True(Tree.Same(L(Act("trigger", ("beat", "the-end"))), Happens("trigger(TheEnd);", "beat TheEnd(id: \"the-end\");")));

	[Fact]
	public void NewsNamesItsArguments()
	{
		var n = (Dictionary<string, object>)Happens("news(from: \"Fort Douglas\", to: Person.Factor, subject: \"Blood\", text: \"Line one.\\n\" + \"Line two \" + \"continued.\", days: 40, company: Company.HBC);")[0];
		Assert.Equal("the Factor at York Factory", n["to"]);
		Assert.Equal("Line one.\nLine two continued.", n["text"]);
		Assert.Equal(40.0, n["days"]);
		Assert.Equal("HBC", n["company"]);
	}

	[Fact]
	public void NamedArgumentsMayComeInAnyOrder()
	{
		var n = (Dictionary<string, object>)Happens("news(days: 40, text: \"t\", subject: \"s\", to: \"Cree chief\", from: \"f\");")[0];
		Assert.Equal("Cree chief", n["to"]);
	}

	[Fact]
	public void TheGenericActionIsAnEscapeHatch()
	{
		var a = (Dictionary<string, object>)Happens("action(\"london\", quota: 0.95);")[0];
		Assert.True(Tree.Same(Act("london", ("quota", 0.95)), a));
		var f = (Dictionary<string, object>)Happens("action(\"flag\", name: \"x\");")[0];
		Assert.True(Tree.Same(Act("flag", ("name", "x")), f));
	}

	[Fact]
	public void LoopsUnroll()
	{
		var h = Happens("for (int i = 1; i <= 3; i++) world.addStock(\"York Factory\", Item.Rum, i * 10);");
		Assert.Equal(new[] { 10.0, 20.0, 30.0 }, h.Cast<Dictionary<string, object>>().Select(a => (double)a["add"]));
		var g = Happens("foreach (string p in [\"A\", \"B\"]) flags.set(\"seen_\" + p);");
		Assert.Equal(new[] { "seen_A", "seen_B" }, g.Cast<Dictionary<string, object>>().Select(a => (string)a["name"]));
		Assert.Equal(2, Happens("for (int i = 5; i > 3; i--) flags.set(\"x\" + i);").Count);
	}

	[Fact]
	public void LocalsAreConstants()
	{
		var h = Happens("int n = 3; string who = \"x\"; world.raiseTension(n * 2, who);");
		Assert.True(Tree.Same(L(Act("tension", ("add", 6.0), ("why", "x"))), h));
	}

	[Theory]
	[InlineData("mood = 1;", "no variable called mood")]
	[InlineData("mood++;", "no variable called mood")]
	[InlineData("teleport(\"x\");", "no function called teleport")]
	[InlineData("flags.set();", "needs name")]
	[InlineData("world.raiseTension();", "needs amount")]
	[InlineData("flags.has(\"x\");", "this does nothing")]
	[InlineData("world.tension > 3;", "this does nothing")]
	[InlineData("say(\"a\" \"b\");", "expected")]
	[InlineData("n = 3;", "no variable called n")]
	[InlineData("while (true) { }", "not part of the language")]
	[InlineData("int n = \"text\";", "declared int")]
	[InlineData("world.raiseTension(\"x\");", "should be a whole number")]
	[InlineData("world.raiseTension(world.tension);", "should be a whole number")]
	[InlineData("say(\"x\", Company.Nobody);", "has no 'Nobody'")]
	[InlineData("world.addStock(\"York Factory\", Item.Rumm, 1);", "Did you mean 'Rum'")]
	[InlineData("for (int i = 0; i < 100000; i++) flags.set(\"x\");", "more than 5000")]
	public void Mistakes(string body, string expect)
	{
		var e = Assert.ThrowsAny<CampError>(() => Happens(body));
		Assert.Contains(expect, e.Message);
	}

	[Fact]
	public void AConstantCannotBeAssigned() =>
		Assert.Contains("is a const", Assert.Throws<CampError>(() => Happens("n = 4;", "const int n = 3;")).Message);
}

public class BeatTests
{
	[Fact]
	public void TheHeaderGivesTheBeatItsData()
	{
		var b = Beat("", "title: \"Seven Oaks\", kind: Kind.Event, date: date(1816, 6, 19), window: days(45), lead: days(90), delay: days(3), repeat: days(365), keywords: [\"Semple\", \"Red River\"]");
		Assert.Equal("b", b["id"]);
		Assert.Equal("Seven Oaks", b["title"]);
		Assert.Equal("event", b["kind"]);
		Assert.Equal("1816-06-19", b["date"]);
		Assert.Equal(45.0, b["window_days"]); Assert.Equal(90.0, b["lead_days"]); Assert.Equal(3.0, b["delay_days"]); Assert.Equal(365.0, b["repeat_days"]);
		Assert.True(Tree.Same(L("Semple", "Red River"), b["keywords"]));
	}

	[Fact]
	public void AllNineHooksMapToTheirKeys()
	{
		var b = Beat(@"
			AwakenWhen() => world.tension >= 1;
			OnAwaken() { flags.set(""a""); }
			OnApproach() { flags.set(""b""); }
			ActivateWhen() => world.tension >= 2;
			OnActivate() { flags.set(""c""); }
			ResolveWhen() => world.tension >= 3;
			OnResolve() { flags.set(""d""); }
			CancelWhen() => world.tension >= 4;
			OnCancel() { flags.set(""e""); }");
		foreach (var (key, kind) in new[] { ("when", 'c'), ("on_awaken", 'e'), ("pressure", 'e'), ("activate_when", 'c'), ("happens", 'e'), ("done_when", 'c'), ("on_resolve", 'e'), ("cancel_when", 'c'), ("on_cancel", 'e') })
			Assert.True(b.ContainsKey(key), key);
		Assert.Equal("tension>=3", ((List<object>)b["done_when"])[0]);
		Assert.Equal("d", ((Dictionary<string, object>)((List<object>)b["on_resolve"])[0])["name"]);
	}

	[Fact]
	public void AHookMayBeASingleExpression() =>
		Assert.Equal("c", ((Dictionary<string, object>)((List<object>)Beat("OnActivate() => flags.set(\"c\");")["happens"])[0])["name"]);

	[Theory]
	[InlineData("date: date(1816, 6, 19)", "", "fixed")]
	[InlineData("date: date(1816, 6, 19), window: days(30)", "", "event")]
	[InlineData("date: date(1816, 6, 19)", "ResolveWhen() => flags.has(\"x\");", "event")]
	[InlineData("date: date(1816, 6, 19)", "OnApproach() { flags.set(\"p\"); }", "event")]
	[InlineData("title: \"t\"", "", "conditional")]
	public void TheKindIsDerived(string header, string extra, string kind)
	{
		var b = Beat("OnActivate() { flags.set(\"x\"); } " + extra, header);
		Assert.Equal(kind, b["kind"]);
	}

	[Fact]
	public void ABeatWithNoOnActivateIsPressure() =>
		Assert.Equal("pressure", Beat("OnApproach() { world.raiseTension(5); }")["kind"]);

	[Fact]
	public void AnExplicitKindWins() =>
		Assert.Equal("pressure", Beat("OnActivate() { flags.set(\"x\"); }", "date: date(1816, 6, 19), kind: Kind.Pressure")["kind"]);

	[Fact]
	public void TheTitleAndIdDefaultToTheName()
	{
		var d = Compile("campaign T(title: \"t\"); beat Seven_Oaks();");
		var b = (Dictionary<string, object>)((List<object>)d["beats"])[0];
		Assert.Equal("Seven_Oaks", b["id"]); Assert.Equal("Seven_Oaks", b["title"]);
	}

	[Fact]
	public void RequiresNamesBeats_AndMayLookAhead()
	{
		var d = Compile("campaign T(title: \"t\"); beat A() [B, \"a-flag\"]; beat B(id: \"bee\");");
		var a = (Dictionary<string, object>)((List<object>)d["beats"])[0];
		Assert.True(Tree.Same(L("bee", "a-flag"), a["requires"]));
	}

	[Fact]
	public void TheCampaignHeaderSetsTheCampaignsDetails()
	{
		var d = Compile("campaign T(title: \"A\", description: \"B\", note: \"C\", seed: 7, postsByFoundingYear: true);");
		Assert.Equal("A", d["title"]); Assert.Equal("B", d["description"]); Assert.Equal("C", d["note"]); Assert.Equal(7.0, d["seed"]); Assert.Equal(true, d["posts_by_founding_year"]);
	}

	[Fact]
	public void VariablesMayStartAboveZero()
	{
		var d = Compile("campaign T(title: \"t\"); int a; int b = 3; float c = 0.5;");
		var vars = (Dictionary<string, object>)d["vars"];
		Assert.Equal(2, vars.Count);
		Assert.Equal(3.0, vars["b"]); Assert.Equal(0.5, vars["c"]);
	}

	[Fact]
	public void FunctionsAreInlinedWithTheirArguments()
	{
		var h = Happens("dispatch(Person.Factor, \"Hello\", 30); dispatch(Person.Partner, \"Peace\", 60);",
			"void dispatch(Person who, string subject, int days) { news(from: \"Montreal\", to: who, subject: subject, text: \"Text.\", days: days); }");
		var a = (Dictionary<string, object>)h[0]; var b = (Dictionary<string, object>)h[1];
		Assert.Equal("the Factor at York Factory", a["to"]); Assert.Equal(30.0, a["days"]); Assert.Equal("Hello", a["subject"]);
		Assert.Equal("the wintering partner", b["to"]); Assert.Equal(60.0, b["days"]);
	}

	[Fact]
	public void AFunctionMayCallAnotherAndUseConstants()
	{
		var h = Happens("both();", "const int n = 3; int x; void one() { x += n; } void both() { one(); one(); }");
		Assert.Equal(2, h.Count);
		Assert.Equal(3.0, ((Dictionary<string, object>)h[0])["by"]);
	}

	[Fact]
	public void ABeatMayHaveItsOwnHelpers()
	{
		var h = Beat("void warn() { say(\"careful\"); } OnActivate() { warn(); warn(); }")["happens"];
		Assert.Equal(2, ((List<object>)h).Count);
	}

	[Fact]
	public void ALoopWritesABeatForEachValue()
	{
		var d = Compile("campaign T(title: \"t\"); for (int y = 1818; y <= 1820; y++) beat War(id: \"war-\" + y, date: date(y, 6, 1)) [Root]; beat Root();");
		var beats = ((List<object>)d["beats"]).Cast<Dictionary<string, object>>().ToList();
		Assert.Equal(new[] { "war-1818", "war-1819", "war-1820" }, beats.Take(3).Select(b => (string)b["id"]));
		Assert.Equal("1819-06-01", beats[1]["date"]);
	}

	[Fact]
	public void LoopsMayNest_AndHoldSeveralBeats()
	{
		var d = Compile("campaign T(title: \"t\"); foreach (int y in 1..2) { foreach (int z in 1..2) { beat B(id: \"b\" + y + z); } }");
		Assert.Equal(new[] { "b11", "b12", "b21", "b22" }, ((List<object>)d["beats"]).Cast<Dictionary<string, object>>().Select(b => (string)b["id"]));
	}

	[Theory]
	[InlineData("campaign T(title: \"t\"); void p() { p(); } beat B() { OnActivate() { p(); } }", "without end")]
	[InlineData("campaign T(title: \"t\"); void p(string a) { flags.set(a); } beat B() { OnActivate() { p(); } }", "needs a")]
	[InlineData("campaign T(title: \"t\"); beat B(kind: Kind.Sideways);", "has no 'Sideways'")]
	[InlineData("campaign T(title: \"t\"); beat B(window: 30);", "number of days")]
	[InlineData("campaign T(title: \"t\"); beat B(wat: 3);", "a beat has no 'wat'")]
	[InlineData("campaign T(title: \"t\"); beat B(date: date(1816, 6, 31));", "is not a date")]
	[InlineData("campaign T(title: \"t\"); beat B() { OnActivate() { } OnActivate() { } }", "written twice")]
	[InlineData("campaign T(title: \"t\"); beat B() { AwakenWhen() { } }", "is a condition")]
	[InlineData("campaign T(title: \"t\"); beat B() { OnActivat() { } }", "did you mean OnActivate")]
	[InlineData("campaign T(title: \"t\"); beat B(); beat B();", "two beats with the id")]
	[InlineData("campaign T(title: \"t\"); beat B() [Nowhere];", "'Nowhere' is not defined")]
	[InlineData("campaign T(title: \"t\"); beat B()", "expected")]
	[InlineData("campaign T(title: \"t\") beat B();", "expected ';'")]
	[InlineData("beat B();", "begins with its header")]
	[InlineData("campaign T(); beat B();", "needs a title")]
	[InlineData("campaign T(title: \"t\", colour: \"red\");", "no 'colour'")]
	[InlineData("campaign T(title: \"t\"); string s;", "int or a float")]
	[InlineData("campaign T(title: \"t\"); beat B() { int x; }", "top of the campaign")]
	[InlineData("campaign T(title: \"t\"); beat B(title: 5);", "title is text")]
	[InlineData("campaign T(title: \"t\"); beat B(title: \"a\", title: \"b\");", "given twice")]
	[InlineData("campaign T(title: \"t\"); beat B(\"x\");", "named")]
	public void Mistakes(string src, string expect)
	{
		var e = Assert.Throws<CampError>(() => Compile(src));
		Assert.Contains(expect, e.Message, StringComparison.OrdinalIgnoreCase);
	}
}

public class LibraryTests
{
	[Fact]
	public void EveryMemberIsDocumented()
	{
		Assert.NotEmpty(Library.All);
		foreach (var m in Library.All)
		{
			Assert.False(string.IsNullOrWhiteSpace(m.Doc), $"{m.Full} has no description");
			Assert.False(string.IsNullOrWhiteSpace(m.Example), $"{m.Full} has no example");
		}
	}

	[Fact]
	public void EveryNameTheLibraryCompilesToIsKnownToTheGame()
	{
		string game = Read("Scripts/Core/CampaignLogic.cs") + Read("Scripts/Core/Campaign.cs");
		foreach (var m in Library.All.Where(m => m.Owner is "world" or "Brigade" or "Beat" or "flags"))
		{
			// calling the member must produce something the game reads
			Assert.NotNull(Library.Find(m.Owner, m.Name));
		}
		foreach (var name in Known.Metrics.Concat(Known.Predicates)) Assert.Contains($"\"{name}\"", game);
	}

	[Fact]
	public void EveryEnumValueCompiles()
	{
		foreach (var type in new[] { "Company", "Person", "Season", "Kind" })
			foreach (var v in Library.EnumValues(type))
				Assert.NotNull(Beat("OnActivate() { flags.set(\"x\"); }", $"title: {type}.{v} == {type}.{v} ? \"ok\" : \"no\""));
	}

	[Fact]
	public void EveryItemTheGameHasIsAnItem()
	{
		foreach (var item in Known.World.Items) Assert.Contains(item, Library.EnumValues("Item"));
		foreach (var item in Known.World.Items.Take(5)) Assert.NotEmpty(Happens($"world.addStock(\"York Factory\", Item.{item}, 1);"));
	}

	[Fact]
	public void AWrongItemSuggestsTheRightOne() =>
		Assert.Contains("Pemmican", Assert.Throws<CampError>(() => Happens("world.addStock(\"York Factory\", Item.Pemican, 1);")).Message);
}

public class RoundTripTests
{
	private static void RoundTrips(Dictionary<string, object> json)
	{
		string text = Decompiler.Decompile(json, "Script");
		var again = Compile(text);
		Assert.True(Tree.Same(json, again), "the decompiled script does not compile back to the same JSON:\n" + text);
	}

	[Theory]
	[InlineData("Campaigns/historical.json")]
	[InlineData("Campaigns/Gemini and Clark.json")]
	public void TheShippedCampaignsSurvive(string path) => RoundTrips(Json(path));

	[Fact]
	public void EverySourceSurvives()
	{
		foreach (var src in ShippedTests.AllSources()) RoundTrips(Compile(File.ReadAllText(src)));
	}

	[Fact]
	public void TextsWithNewLinesAndQuotesSurvive()
	{
		RoundTrips(Doc(B("a", "event", "1816-06-19", ("happens", L(Act("news", ("from", "Montreal"), ("to", "the wintering partner"), ("subject", "It's \"news\""), ("text", "One.\n\nTwo, with 'quotes' and a very long line that has to be broken into pieces to fit the page nicely."), ("days", 40.0)))))));
	}

	[Fact]
	public void WhatTheLanguageHasNoWordForSurvivesAsRawAndAction()
	{
		var json = Doc(B("a", "conditional", null,
			("when", L("heard:somebody", "!flag:x", "tension>=40", Tree.Dict(("p", "weird"), ("a", L("a", "b"))))),
			("happens", L(Act("teleport", ("to", "Moon"), ("when", 3.0), ("fast", true))))));
		RoundTrips(json);
	}

	[Fact]
	public void ConditionsAndStatementsOfEveryShapeSurvive()
	{
		var cond = Tree.Dict(("any", L("flag:a", Tree.Dict(("all", L("flag:b", Tree.Dict(("not", "flag:c"))))), Tree.Dict(("not", Tree.Dict(("any", L("month:6-8", "chance:10"))))))));
		var elseIf = Act("if", ("cond", "year>=1818"), ("then", L(Act("flag", ("name", "b")))), ("else", L(Act("flag", ("name", "c")))));
		var ifs = Act("if", ("cond", "tension>=3"), ("then", L(Act("flag", ("name", "a")))), ("else", L(elseIf)));
		var after = Act("after", ("days", 10.0), ("then", L(Act("flag", ("name", "a")), Act("flag", ("name", "b")))));
		var lucky = Tree.Dict(("weight", 30.0), ("do", L(Act("flag", ("name", "a")))));
		var nothing = Tree.Dict(("weight", 70.0), ("do", L()));
		var random = Act("random", ("options", L(lucky, nothing)));
		var b = B("a", "conditional", null,
			("when", L(cond, Tree.Dict(("all", L("flag:x", "flag:y"))))),
			("done_when", L(Tree.Dict(("all", L("after:1816-06-19", "convoy_lost:the York boats"))))),
			("cancel_when", L("season:Winter")),
			("on_awaken", L(Act("tension", ("add", 5.0)))),
			("on_resolve", L(Act("flag", ("name", "r")))),
			("on_cancel", L(Act("unflag", ("name", "r")))),
			("activate_when", L("player_faction:HBC")),
			("happens", L(ifs, after, random, Act("set", ("var", "mood"), ("to", 4.0)), Act("add", ("var", "mood")), Act("add", ("var", "mood"), ("by", -3.0)))),
			("pressure", L(Act("say", ("text", "x"), ("to", "all")))),
			("requires", L("someflag")));
		var json = Doc(b);
		json["vars"] = Tree.Dict(("mood", 2.0));
		RoundTrips(json);
	}
}
