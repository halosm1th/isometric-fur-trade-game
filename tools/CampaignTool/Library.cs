using System;
using System.Collections.Generic;
using System.Linq;

namespace CampaignTool;

public sealed class LibParam
{
	public string Name = "", Type = "";
	public bool Optional, Rest;
	public string Doc = "";   // what this argument is: shown in the editor, the reference and the wiki
	public override string ToString() => $"{Type} {Name}{(Optional ? "?" : "")}{(Rest ? "..." : "")}";
}

// One thing the language gives you: a function, a property, or a named value.
// This table is the whole vocabulary - the compiler runs it, the checker reads
// it, the editor completes from it and the wiki's reference page is written
// from it - so they cannot disagree.
public sealed class Member
{
	public string Owner = "", Name = "", Kind = "function", Returns = "", Doc = "", Example = "";
	public LibParam[] Params = Array.Empty<LibParam>();
	public bool OpenNamed;      // action(...) takes any key: value pairs besides its own
	internal Func<Ctx, object, Args, object> Run;
	public string Full => Owner.Length > 0 ? $"{Owner}.{Name}" : Name;
	public string Signature => Kind == "function" ? $"{Full}({string.Join(", ", Params.Select(p => p.ToString()))}){(Returns.Length > 0 ? " : " + Returns : "")}" : $"{Full}{(Returns.Length > 0 ? " : " + Returns : "")}";
}

internal sealed class Ctx
{
	public Compiler C;
	public List<object> Out;        // where actions go; null while a condition is being read
	public Span At;
	public Env Env;
}

internal sealed class Args
{
	public readonly Dictionary<string, object> V = new();
	public readonly List<object> Rest = new();
	public readonly Dictionary<string, object> Extra = new();
	public bool Has(string k) => V.ContainsKey(k);
	public string S(string k) => CJson.Join(V[k]);
	public double N(string k) => (double)V[k];
	public int I(string k) => (int)(double)V[k];
	public object this[string k] => V[k];
}

public static class Library
{
	public static readonly string[] Companies = { "HBC", "NWC", "All" };
	public static readonly Dictionary<string, string> CompanyJson = new() { ["HBC"] = "HBC", ["NWC"] = "NWC", ["All"] = "all" };
	public static readonly Dictionary<string, string> Persons = new()
	{
		["Factor"] = "the Factor at York Factory", ["Postmaster"] = "the postmaster at Oxford House",
		["Partner"] = "the wintering partner", ["Chief"] = "the chief of the band",
	};
	public static readonly string[] Seasons = { "Breakup", "OpenWater", "FreezeUp", "Winter" };
	public static readonly string[] Kinds = { "Fixed", "Conditional", "Event", "Pressure" };

	public static readonly Dictionary<string, string> EnumDocs = new()
	{
		["Company"] = "The two companies (and everyone): Company.HBC, Company.NWC, Company.All.",
		["Person"] = "Who a letter is addressed to: Person.Factor, Person.Postmaster, Person.Partner, Person.Chief. A plain string names anyone else.",
		["Season"] = "The river's season, for world.season: Season.Breakup, OpenWater, FreezeUp, Winter.",
		["Kind"] = "The beat kinds, for the kind: argument when the derived one is not what you want: Kind.Fixed, Conditional, Event, Pressure.",
		["Item"] = "A trade good: Item.Rum, Item.Pemmican ... every item the game knows.",
		["Task"] = "The work a quest step can hand the player, as the Factor would: Task.CarryCargo, Task.Firewood, Task.Brigade ... (Player/PlayerTask.cs).",
	};

	public static IEnumerable<string> EnumValues(string type) => type switch
	{
		"Company" => Companies, "Person" => Persons.Keys, "Season" => Seasons, "Kind" => Kinds,
		"Item" => Known.World.Items.OrderBy(x => x, StringComparer.Ordinal),
		"Task" => Known.World.Tasks,
		_ => Array.Empty<string>(),
	};

	public static bool IsEnum(string name) => EnumDocs.ContainsKey(name);

	public static readonly List<Member> All = new();
	private static readonly Dictionary<(string, string), Member> Index = new();

	public static Member Find(string owner, string name) => Index.GetValueOrDefault((owner, name));
	public static IEnumerable<Member> Of(string owner) => All.Where(m => m.Owner == owner);

	// "amount:number", "why:string?", "options:Option..."
	private static LibParam[] P(params string[] specs) => specs.Select(s =>
	{
		var parts = s.Split(':');
		string type = parts[1];
		var p = new LibParam { Name = parts[0] };
		if (type.EndsWith("...")) { p.Rest = true; type = type[..^3]; }
		if (type.EndsWith("?")) { p.Optional = true; type = type[..^1]; }
		p.Type = type;
		return p;
	}).ToArray();

	private static void Add(string owner, string name, string kind, string[] ps, string returns, string doc, string example, Func<Ctx, object, Args, object> run, bool openNamed = false)
	{
		var m = new Member { Owner = owner, Name = name, Kind = kind, Params = P(ps), Returns = returns, Doc = doc, Example = example, Run = run, OpenNamed = openNamed };
		All.Add(m);
		Index[(owner, name)] = m;
	}

	private static void Fn(string owner, string name, string[] ps, string returns, string doc, string example, Func<Ctx, object, Args, object> run, bool openNamed = false)
		=> Add(owner, name, "function", ps, returns, doc, example, run, openNamed);

	private static void Prop(string owner, string name, string returns, string doc, string example, Func<Ctx, object, Args, object> run)
		=> Add(owner, name, "property", Array.Empty<string>(), returns, doc, example, run);

	// An action: needs somewhere to put it.
	internal static object Fx(Ctx c, string verb, params (string, object)[] kv)
	{
		if (c.Out == null) throw new CampError($"{verb} changes the world, so it belongs in an effect (OnActivate, OnAwaken ...), not in a condition", c.At);
		var d = Tree.Dict(("do", verb));
		foreach (var (k, v) in kv) if (v != null) d[k] = v;
		c.Out.Add(d);
		return new Unit();
	}

	private static object Json(object v) => v switch { double d => d, bool b => b, string s => s, DateV d => d.ToString(), EnumV e => e.Value, BeatV b => b.Id, ArrV a => a.Items.Select(Json).ToList(), _ => CJson.Join(v) };
	private static string Co(object v) => v is EnumV e ? Library.CompanyJson[e.Value] : CJson.Join(v);

	private static MetricV Metric(string name, params string[] args) => new(name, args.ToList());
	// A person for a quest: Person.Factor and friends by their short names, else the text as written.
	internal static string Who(object v) => v is EnumV { Type: "Person" } e ? e.Value : CJson.Join(v);
	private static CondV Pred(string name, string arg) => new(CJson.Predicate(name, new() { arg }));

	static Library()
	{
		// ---- values and the clock
		Fn("", "days", P1("n:int"), "Days", "A length of time in days.", "window: days(45)", (c, s, a) => new DaysV(a.N("n")));
		Fn("", "date", P1("year:int", "month:int", "day:int"), "Date", "A calendar date.", "date: date(1816, 6, 19)", (c, s, a) =>
		{
			var d = new DateV(a.I("year"), a.I("month"), a.I("day"));
			if (d.M < 1 || d.M > 12 || d.D < 1 || d.Y < 1 || d.D > DateTime.DaysInMonth(d.Y, d.M)) throw new CampError($"{d.Y}-{d.M}-{d.D} is not a date", c.At);
			return d;
		});

		// ---- conditions
		Fn("", "chance", P1("percent:int"), "bool", "True this often (a percent), decided by the campaign's seed. It rolls again every time it is asked: in AwakenWhen, chance(10) is a one-in-ten chance each day.", "chance(30)",
			(c, s, a) => Pred("chance", CJson.Num(a.N("percent"))));
		Fn("", "raw", P1("condition:string"), "bool", "A condition written in the game's own form, for anything the language has no word for yet.", "raw(\"tension>=40\")",
			(c, s, a) => new CondV(a.S("condition").TrimStart().StartsWith("{") ? Tree.Parse(a.S("condition")) : a.S("condition")));

		// ---- the world
		Prop("world", "tension", "int", "How close the two companies are to blows, 0-100. It starts at 20, falls by 1.2 a day on its own, and at 70 or more the companies are at open hostility.", "world.tension >= 50", (c, s, a) => Metric("tension"));
		Prop("world", "standing", "int", "The player's standing with their company (the local player; 0 if there is none).", "world.standing < 10", (c, s, a) => Metric("standing"));
		Prop("world", "month", "int", "The month, 1-12.", "world.month == 6", (c, s, a) => Metric("month"));
		Prop("world", "year", "int", "The year.", "world.year >= 1818", (c, s, a) => Metric("year"));
		Prop("world", "day", "int", "The day of the month.", "world.day == 1", (c, s, a) => Metric("day"));
		Prop("world", "dayOfYear", "int", "The day of the year, 1-366.", "world.dayOfYear > 200", (c, s, a) => Metric("day_of_year"));
		Prop("world", "convoysActive", "int", "How many brigade crossings are on the rivers now (not yet arrived).", "world.convoysActive == 0", (c, s, a) => Metric("convoys_active"));
		Prop("world", "date", "Date", "Today. Compare it with a date: world.date >= date(1816, 6, 19).", "world.date < date(1812, 1, 1)", (c, s, a) => new DateQ());
		Prop("world", "season", "Season", "The river's season. Compare it with Season.Winter and so on.", "world.season == Season.Winter", (c, s, a) => new QueryV("season"));
		Prop("world", "playerFaction", "Company", "Which company a player works for: in multiplayer, true if any player does.", "world.playerFaction == Company.HBC", (c, s, a) => new QueryV("player_faction"));
		Fn("world", "stock", P1("post:string", "item:Item"), "int", "How many pieces of an item a post's ledger holds. A post that does not stand has no ledger and answers nothing, so every comparison with it is false.", "world.stock(\"York Factory\", Item.Rum) < 20", (c, s, a) => Metric("stock", a.S("post"), a.S("item")));
		Fn("world", "furs", P1("post:string"), "int", "The furs in a post's ledger, in Made Beaver.", "world.furs(\"York Factory\") > 500", (c, s, a) => Metric("furs", a.S("post")));
		Fn("world", "hasPost", P1("place:string"), "bool", "True while a post stands there: founded by the start year and not given up, or founded since (by the script, or on 1 January of its year with postsByFoundingYear).", "world.hasPost(\"Fort William\")", (c, s, a) => Pred("post", a.S("place")));
		Fn("world", "isDead", P1("name:string"), "bool", "True once the person with exactly this name is dead. Names are written as the game shows them (\"Mr. William Auld\", \"Maskwa (chief)\"); a name nobody in the world has is never dead.", "world.isDead(\"Miles Macdonell\")", (c, s, a) => Pred("dead", a.S("name")));
		Fn("world", "inMonths", P1("first:int", "last:int?"), "bool", "True in these months of the year (a range if two are given).", "world.inMonths(6, 8)",
			(c, s, a) => Pred("month", a.Has("last") ? $"{a.I("first")}-{a.I("last")}" : $"{a.I("first")}"));
		Fn("world", "raiseTension", P1("amount:int", "why:string?"), "effect", "Raise (or, if negative, lower) the tension, with a line for the log. The result is kept within 0-100.", "world.raiseTension(10, \"the colony is armed\")",
			(c, s, a) => Fx(c, "tension", ("add", a.N("amount")), ("why", a.Has("why") ? a.S("why") : null)));
		Fn("world", "setLondon", P1("price:float", "quota:float"), "effect", "Multiply next year's London price and quota at York Factory (1 leaves one alone). They compound: two calls of 0.9 make 0.81.", "world.setLondon(0.85, 0.85)",
			(c, s, a) => Fx(c, "london", ("price", a.N("price")), ("quota", a.N("quota"))));
		Fn("world", "foundPost", P1("place:string"), "effect", "A post on the world map is founded there and appears in the world.", "world.foundPost(\"Fort William\")", (c, s, a) => Fx(c, "found_post", ("place", a.S("place"))));
		Fn("world", "addStock", P1("post:string", "item:Item", "amount:int"), "effect", "Put goods in (or, if negative, take them out of) a post's ledger. Nothing happens if the post does not stand.", "world.addStock(\"York Factory\", Item.Rum, -5)",
			(c, s, a) => Fx(c, "stock", ("post", a.S("post")), ("item", a.S("item")), ("add", a.N("amount"))));

		// ---- the player
		Fn("player", "did", P1("deed:string"), "bool", "True once the player has done this at least once: truce, raid, escort or courier.", "player.did(\"truce\")", (c, s, a) => Pred("flag", "deed:" + a.S("deed")));
		Fn("player", "deeds", P1("deed:string"), "int", "How many times the player has done it.", "player.deeds(\"escort\") >= 2", (c, s, a) => Metric("var", "deed:" + a.S("deed")));

		Fn("player", "posted", P1("place:string"), "bool", "True when the player (the one a quest runs for; from a beat, any player) is on the books of that post.", "player.posted(\"Edmonton House\")", (c, s, a) => Pred("posted", a.S("place")));
		Fn("player", "rapport", P1("who:string"), "int", "What the person makes of the player, -100 to 100. Who is a name, or a role at a place: \"leader@Fort Augustus\", \"chief@Beaver Hills Cree\".", "player.rapport(\"leader@Fort Augustus\") > 20", (c, s, a) => Metric("rapport", Who(a["who"])));
		Fn("player", "postTo", P1("place:string"), "effect", "Put the player on the books of another post: it is their home from now on (its stores, its master, its men).", "player.postTo(\"Edmonton House\")", (c, s, a) => Fx(c, "posting", ("place", a.S("place"))));
		Fn("player", "addStanding", P1("amount:int"), "effect", "Raise (or, if negative, lower) the player's standing with their company.", "player.addStanding(3)", (c, s, a) => Fx(c, "standing", ("add", a.N("amount"))));
		Fn("player", "addRapport", P1("who:string", "amount:int"), "effect", "Change what someone makes of the player. Who is a name or a role at a place, as for rapport.", "player.addRapport(\"leader@Fort Augustus\", 10)", (c, s, a) => Fx(c, "rapport", ("who", Who(a["who"])), ("add", a.N("amount"))));
		Fn("player", "give", P1("item:Item", "count:int?"), "effect", "Put something in the player's pack (1 if no count).", "player.give(Item.Tobacco, 2)", (c, s, a) => Fx(c, "give", ("item", a.S("item")), ("count", a.Has("count") ? a.N("count") : null)));
		Fn("player", "take", P1("item:Item", "count:int?"), "effect", "Take something from the player's pack, as much as they have up to the count.", "player.take(Item.Rum)", (c, s, a) => Fx(c, "take", ("item", a.S("item")), ("count", a.Has("count") ? a.N("count") : null)));
		Fn("player", "note", P1("text:string"), "effect", "A line in the player's journal, said over their head.", "player.note(\"Mr. Bird wants you at the hunt.\")", (c, s, a) => Fx(c, "note", ("text", a.S("text"))));
		Fn("world", "regard", P1("place:string"), "int", "How a band (by its camp's name) regards the Company, 0-100.", "world.regard(\"Beaver Hills Cree\") >= 60", (c, s, a) => Metric("regard", a.S("place")));
		Fn("world", "addRegard", P1("place:string", "amount:int"), "effect", "Change how a band (by its camp's name) regards the Company; kept within 0-100.", "world.addRegard(\"Beaver Hills Cree\", 5)", (c, s, a) => Fx(c, "regard", ("place", a.S("place")), ("add", a.N("amount"))));

		// ---- quests
		Prop("Quest", "done", "bool", "True once the quest is done (for the player it runs for; from a beat, any player).", "Landfall.done", (c, s, a) => Pred("quest", ((QuestV)s).Id));
		Prop("Quest", "active", "bool", "True while the quest is under way.", "Landfall.active", (c, s, a) => Pred("quest_active", ((QuestV)s).Id));
		Prop("Quest", "failed", "bool", "True once the quest has failed.", "Landfall.failed", (c, s, a) => Pred("quest_failed", ((QuestV)s).Id));
		Fn("Quest", "start", P1(), "effect", "Start the quest now, whatever it requires and whoever gives it.", "TheHunt.start()", (c, s, a) => Fx(c, "start_quest", ("quest", ((QuestV)s).Id)));
		Fn("Quest", "complete", P1(), "effect", "End the quest as done.", "TheHunt.complete()", (c, s, a) => Fx(c, "end_quest", ("quest", ((QuestV)s).Id)));
		Fn("Quest", "fail", P1(), "effect", "End the quest as failed.", "TheHunt.fail()", (c, s, a) => Fx(c, "end_quest", ("quest", ((QuestV)s).Id), ("failed", true)));

		// ---- flags
		Fn("flags", "has", P1("name:string"), "bool", "True when the flag is raised.", "flags.has(\"governor_killed\")", (c, s, a) => Pred("flag", a.S("name")));
		Fn("flags", "set", P1("name:string"), "effect", "Raise a flag.", "flags.set(\"governor_killed\")", (c, s, a) => Fx(c, "flag", ("name", a.S("name"))));
		Fn("flags", "clear", P1("name:string"), "effect", "Lower a flag.", "flags.clear(\"calm\")", (c, s, a) => Fx(c, "unflag", ("name", a.S("name"))));

		// ---- beats
		Prop("Beat", "done", "bool", "True once the beat has happened.", "SevenOaks.done", (c, s, a) => Pred("beat", ((BeatV)s).Id));
		Prop("Beat", "count", "int", "How many times the beat has happened (it can repeat).", "Raid.count >= 2", (c, s, a) => Metric("count", ((BeatV)s).Id));
		Prop("Beat", "age", "int", "Days since the beat last happened (99999 if it never has).", "SevenOaks.age > 90", (c, s, a) => Metric("beat_age", ((BeatV)s).Id));

		// ---- brigades
		Fn("", "brigade", P1("run:string"), "Brigade", "A brigade run on the rivers, by name (Data/World/brigade_runs.json).", "brigade(\"the York boats\").isLate", (c, s, a) => new BrigadeV(a.S("run")));
		foreach (var (name, pred, doc) in new[]
		{
			("isLate", "convoy_late", "has missed its day (until it arrives or sets out again)"), ("isLost", "convoy_lost", "has lost pieces on this crossing"), ("isRobbed", "convoy_robbed", "was robbed on this crossing"),
			("wasTurnedBack", "convoy_turned_back", "was turned back on this crossing"), ("isOut", "convoy_out", "has set out at least once (it stays true: on the river now is isOut && !isIn)"), ("isIn", "convoy_in", "has arrived (until it sets out again)"),
			("isWintering", "convoy_wintering", "is wintering on the way"),
		})
			Prop("Brigade", name, "bool", $"True when the brigade {doc}.", $"brigade(\"the York boats\").{name}", (c, s, a) => Pred(pred, ((BrigadeV)s).Run));
		Prop("Brigade", "daysLate", "int", "How many days late the brigade's crossing is (0 when it is not on the river).", "brigade(\"the York boats\").daysLate > 10", (c, s, a) => Metric("convoy_days_late", ((BrigadeV)s).Run));
		Prop("Brigade", "daysOut", "int", "How many days the brigade's crossing has been out (0 when it is not on the river).", "brigade(\"the York boats\").daysOut > 40", (c, s, a) => Metric("convoy_days_out", ((BrigadeV)s).Run));
		Fn("Brigade", "delay", P1("days:int?"), "effect", "Hold the brigade on the bank for some days (5 if not given).", "brigade(\"the York boats\").delay(4)", (c, s, a) => Fx(c, "brigade", ("run", ((BrigadeV)s).Run), ("effect", "delay"), ("days", a.Has("days") ? a.N("days") : null)));
		Fn("Brigade", "turnBack", P1("days:int?"), "effect", "Turn the brigade back toward the post it left. (The game does not use days.)", "brigade(\"the York boats\").turnBack()", (c, s, a) => Fx(c, "brigade", ("run", ((BrigadeV)s).Run), ("effect", "turn_back"), ("days", a.Has("days") ? a.N("days") : null)));
		Fn("Brigade", "lose", P1(), "effect", "Half of every kind of piece the brigade carries is lost on the road; the boats go on.", "brigade(\"the York boats\").lose()", (c, s, a) => Fx(c, "brigade", ("run", ((BrigadeV)s).Run), ("effect", "lose")));
		Fn("Brigade", "cancel", P1(), "effect", "Stand down the crossing now on the road: its pieces go back on the books of the post that loaded them. Later crossings still run.", "brigade(\"the York boats\").cancel()", (c, s, a) => Fx(c, "brigade", ("run", ((BrigadeV)s).Run), ("effect", "cancel")));
		Fn("Brigade", "launch", P1("leg:int?"), "effect", "Send one crossing of the run out now: leg is its place in the run's list of legs, counting from 0.", "brigade(\"the York boats\").launch()", (c, s, a) => Fx(c, "brigade", ("run", ((BrigadeV)s).Run), ("effect", "launch"), ("leg", a.Has("leg") ? a.N("leg") : null)));

		// ---- effects
		Fn("", "news", P1("from:string", "to:Person", "subject:string", "text:string", "days:int", "company:Company?"), "effect",
			"A sealed letter, sent now. After some days it reaches the first living person it is addressed to (it waits if there is nobody), and it is known only once it is read; reading it raises the flag heard:<this beat's id>.", "news(from: \"Montreal\", to: Person.Partner, subject: \"war\", text: \"...\", days: 75, company: Company.NWC)",
			(c, s, a) => Fx(c, "news", ("from", a.S("from")), ("to", a["to"] is EnumV pe ? Persons[pe.Value] : a.S("to")), ("subject", a.S("subject")), ("text", a.S("text")), ("days", a.N("days")), ("company", a.Has("company") ? Co(a["company"]) : null)));
		Fn("", "say", P1("text:string", "to:Company?"), "effect", "A line in the journal of every player of one company (or of everyone), spoken over their heads.", "say(\"The boats are overdue.\", Company.HBC)",
			(c, s, a) => Fx(c, "say", ("text", a.S("text")), ("to", a.Has("to") ? Co(a["to"]) : null)));
		Fn("", "shout", P1("text:string", "to:Company?"), "effect", "Like say, but as a headline.", "shout(\"The governor is dead!\")",
			(c, s, a) => Fx(c, "say", ("text", a.S("text")), ("to", a.Has("to") ? Co(a["to"]) : null), ("headline", true)));
		Fn("", "trigger", P1("beat:Beat"), "effect", "Make another beat happen at the end of today: its OnActivate runs whatever its date, requirements, ActivateWhen, or cancellation.", "trigger(TheEnd)", (c, s, a) => Fx(c, "trigger", ("beat", a.S("beat"))));
		Fn("", "after", P1("delay:Days", "then:Lambda"), "effect", "Do something some days from now (saved with the game).", "after(days(10), () => trigger(TheEnd))",
			(c, s, a) =>
			{
				var body = new List<object>();
				c.C.RunLambda((LambdaV)a["then"], body);
				return Fx(c, "after", ("days", ((DaysV)a["delay"]).N), ("then", body));
			});
		Fn("", "option", P1("weight:int", "then:Lambda"), "Option", "One way a random() can go, with its weight.", "option(30, () => world.raiseTension(5))",
			(c, s, a) => new OptionV(a.N("weight"), (LambdaV)a["then"]));
		Fn("", "random", P1("options:Option..."), "effect", "Do one of the options, chosen by weight.", "random(option(30, () => ...), option(70, () => ...))",
			(c, s, a) =>
			{
				var opts = new List<object>();
				foreach (var o in a.Rest.Cast<OptionV>())
				{
					var body = new List<object>();
					c.C.RunLambda(o.Then, body);
					opts.Add(Tree.Dict(("weight", o.Weight), ("do", body)));
				}
				return Fx(c, "random", ("options", opts));
			});
		Fn("", "action", P1("verb:string"), "effect", "Any action the game has that the language has no word for: action(\"name\", key: value, ...).", "action(\"tension\", add: 5)",
			(c, s, a) =>
			{
				var kv = new List<(string, object)>();
				foreach (var (k, v) in a.Extra) kv.Add((k, Json(v)));
				return Fx(c, a.S("verb"), kv.ToArray());
			}, openNamed: true);
		DocParams();
	}

	private static string[] P1(params string[] specs) => specs;

	// What each argument of each function is, by "owner.function.argument"
	// ("" owner for the plain functions). A test insists every argument has one.
	public static readonly Dictionary<string, string> ParamDocs = new()
	{
		["days.n"] = "How many days: a whole number, 0 or more.",
		["date.year"] = "The year, e.g. 1816.",
		["date.month"] = "The month, 1 (January) to 12 (December).",
		["date.day"] = "The day of the month, 1 to 31 (it must exist in that month).",
		["chance.percent"] = "How often it comes out true, 0-100. 30 means three times in ten.",
		["raw.condition"] = "The condition as the game writes it: \"tension>=40\", \"flag:x\", \"post:Fort William\" ... (see the JSON format page).",
		["world.stock.post"] = "A post that keeps books, by its exact name: \"York Factory\", \"Cumberland House\" ... (World reference).",
		["world.stock.item"] = "Which item: Item.Rum, Item.Pemmican, Item.TradeGoods ...",
		["world.furs.post"] = "A post that keeps books, by its exact name.",
		["world.hasPost.place"] = "A post on the world map, by its exact name: \"Fort William\", \"Norway House\" ...",
		["world.isDead.name"] = "A person's exact name as the game shows it: \"Mr. William Auld\", \"Maskwa (chief)\". A name nobody has is never dead.",
		["world.inMonths.first"] = "The first month, 1-12.",
		["world.inMonths.last"] = "The last month, 1-12 (leave it out for one month). Smaller than first wraps the year's end: inMonths(11, 3) is November to March.",
		["world.raiseTension.amount"] = "How much to add to the tension (0-100 scale); negative lowers it. It falls by 1.2 a day on its own, so 10 lasts about a week.",
		["world.raiseTension.why"] = "A few words for the log saying why. Leave it out and the beat's title is used.",
		["world.setLondon.price"] = "Multiplies next year's standard of trade (goods paid per MB of fur). 1 leaves it alone, 0.85 is a hard year, 1.15 a good one.",
		["world.setLondon.quota"] = "Multiplies the returns London expects next year. 1 leaves it alone.",
		["world.foundPost.place"] = "A post on the world map, by its exact name.",
		["world.addStock.post"] = "A post that keeps books, by its exact name. It must stand, or nothing happens.",
		["world.addStock.item"] = "Which item: Item.Rum, Item.Pemmican ...",
		["world.addStock.amount"] = "How many pieces to add; negative takes them away (never below 0).",
		["world.regard.place"] = "A band, by its camp's name: \"Beaver Hills Cree (Amiskwacîwiyiniwak)\" ...",
		["world.addRegard.place"] = "A band, by its camp's name.",
		["world.addRegard.amount"] = "How much to change the band's regard for the Company (kept within 0-100); negative lowers it.",
		["player.did.deed"] = "Which deed: \"truce\", \"raid\", \"escort\" or \"courier\".",
		["player.deeds.deed"] = "Which deed: \"truce\", \"raid\", \"escort\" or \"courier\".",
		["player.posted.place"] = "A post that keeps books, by its exact name.",
		["player.rapport.who"] = "The person: an exact name, Person.Factor, or a role at a place such as \"leader@Fort Augustus\" or \"chief@<camp>\".",
		["player.postTo.place"] = "The post that becomes the player's home: one that keeps books, by its exact name.",
		["player.addStanding.amount"] = "How much to add to the player's standing with their company; negative lowers it.",
		["player.addRapport.who"] = "The person: an exact name, Person.Factor, or a role at a place such as \"leader@Fort Augustus\".",
		["player.addRapport.amount"] = "How much to change what they make of the player (-100 to 100 scale); negative sours it.",
		["player.give.item"] = "What to put in the player's pack: Item.Tobacco ...",
		["player.give.count"] = "How many (1 if left out).",
		["player.take.item"] = "What to take from the player's pack.",
		["player.take.count"] = "How many at most (1 if left out); they lose what they have up to this.",
		["player.note.text"] = "The line for the player's journal, also said over their head.",
		["flags.has.name"] = "The flag's name, as text: \"governor_killed\". Also a beat's id, or a game flag like \"late:the York boats\".",
		["flags.set.name"] = "The flag's name, as text. Any name you like; other beats test it with flags.has.",
		["flags.clear.name"] = "The flag's name, as text.",
		["brigade.run"] = "A brigade run's exact name: \"the York boats\", \"the Saskatchewan brigade\", \"the Montreal canoes\", \"the Saskatchewan canoes\" or \"the Athabasca brigade\".",
		["Brigade.delay.days"] = "How many days the boats are held on the bank (5 if left out).",
		["Brigade.turnBack.days"] = "Accepted, but the game does not use it: the boats simply go back.",
		["Brigade.launch.leg"] = "Which crossing of the run, counting from 0 in the run's list of legs (0 if left out). The World reference lists them.",
		["news.from"] = "Who the letter is from, as it will be read: \"the Governor and Committee, London\", \"Montreal\" ... Free text.",
		["news.to"] = "Who it is for: Person.Factor, Person.Postmaster, Person.Partner, Person.Chief, or text: a person's exact name or \"the master at <post>\". It waits until someone fits.",
		["news.subject"] = "The letter's subject line.",
		["news.text"] = "What the letter says.",
		["news.days"] = "How many days it takes to arrive: London to the Bay is most of a year, Montreal to the interior two or three months.",
		["news.company"] = "Whose letter it is: Company.HBC (if left out) or Company.NWC. Read by the other company, it has been intercepted.",
		["say.text"] = "The line itself.",
		["say.to"] = "Who hears it: Company.HBC (only HBC players), Company.NWC (only NWC players), or leave it out (or Company.All) for every player.",
		["shout.text"] = "The headline itself.",
		["shout.to"] = "Whose journals get the line: Company.HBC, Company.NWC, or leave it out for every player. The headline is shown to everyone either way.",
		["trigger.beat"] = "The beat to happen now: its name (TheEnd), or its id as text for a beat made in a loop (\"ship-1816\").",
		["after.delay"] = "How long to wait: days(10).",
		["after.then"] = "What to do then: () => one effect; or () => { several; effects; }",
		["option.weight"] = "How likely this option is, relative to the others' weights (they need not add to 100).",
		["option.then"] = "What happens if this option is chosen: () => effect, or () => { ... }",
		["random.options"] = "Two or more option(weight, () => ...); exactly one is done.",
		["action.verb"] = "The game's own name for an action (\"tension\", \"stock\" ...), followed by its fields as name: value (see the JSON format page).",
	};

	// What each type is, for the editor's hover and the reference.
	public static readonly Dictionary<string, string> TypeDocs = new()
	{
		["int"] = "A whole number: 3, -5.",
		["float"] = "Any number: 0.85.",
		["string"] = "Text in double quotes: \"York Factory\".",
		["bool"] = "A condition the world is asked (world.tension > 50), or true/false.",
		["Date"] = "A day: date(1816, 6, 19).",
		["Days"] = "A length of time: days(45).",
		["Beat"] = "A beat, by its name (SevenOaks), or its id as text.",
		["Brigade"] = "A brigade run: brigade(\"the York boats\").",
		["Quest"] = "A quest, by its name (Landfall).",
		["Lambda"] = "Effects to run later: () => effect; or () => { several; effects; }",
		["Option"] = "One way a random() can go: option(weight, () => ...).",
		["effect"] = "It changes the world, so it belongs in an effect hook (OnActivate ...), not in a condition.",
	};

	public static string ParamDoc(Member m, LibParam p) => ParamDocs.GetValueOrDefault($"{m.Full}.{p.Name}", "");

	private static void DocParams()
	{
		foreach (var m in All) foreach (var p in m.Params) p.Doc = ParamDoc(m, p);
	}
}
