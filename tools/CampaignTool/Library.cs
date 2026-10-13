using System;
using System.Collections.Generic;
using System.Linq;

namespace CampaignTool;

public sealed class LibParam
{
	public string Name = "", Type = "";
	public bool Optional, Rest;
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
	};

	public static IEnumerable<string> EnumValues(string type) => type switch
	{
		"Company" => Companies, "Person" => Persons.Keys, "Season" => Seasons, "Kind" => Kinds,
		"Item" => Known.World.Items.OrderBy(x => x, StringComparer.Ordinal),
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
		Fn("", "chance", P1("percent:int"), "bool", "True this often (a percent), decided by the campaign's seed.", "chance(30)",
			(c, s, a) => Pred("chance", CJson.Num(a.N("percent"))));
		Fn("", "raw", P1("condition:string"), "bool", "A condition written in the game's own form, for anything the language has no word for yet.", "raw(\"tension>=40\")",
			(c, s, a) => new CondV(a.S("condition").TrimStart().StartsWith("{") ? Tree.Parse(a.S("condition")) : a.S("condition")));

		// ---- the world
		Prop("world", "tension", "int", "How tense the colony and the companies are, 0-100.", "world.tension >= 50", (c, s, a) => Metric("tension"));
		Prop("world", "standing", "int", "The player's standing with their company.", "world.standing < 10", (c, s, a) => Metric("standing"));
		Prop("world", "month", "int", "The month, 1-12.", "world.month == 6", (c, s, a) => Metric("month"));
		Prop("world", "year", "int", "The year.", "world.year >= 1818", (c, s, a) => Metric("year"));
		Prop("world", "day", "int", "The day of the month.", "world.day == 1", (c, s, a) => Metric("day"));
		Prop("world", "dayOfYear", "int", "The day of the year, 1-366.", "world.dayOfYear > 200", (c, s, a) => Metric("day_of_year"));
		Prop("world", "convoysActive", "int", "How many brigades are on the rivers.", "world.convoysActive == 0", (c, s, a) => Metric("convoys_active"));
		Prop("world", "date", "Date", "Today. Compare it with a date: world.date >= date(1816, 6, 19).", "world.date < date(1812, 1, 1)", (c, s, a) => new DateQ());
		Prop("world", "season", "Season", "The river's season. Compare it with Season.Winter and so on.", "world.season == Season.Winter", (c, s, a) => new QueryV("season"));
		Prop("world", "playerFaction", "Company", "Which company the player works for.", "world.playerFaction == Company.HBC", (c, s, a) => new QueryV("player_faction"));
		Fn("world", "stock", P1("post:string", "item:Item"), "int", "How much of an item a post's ledger holds.", "world.stock(\"York Factory\", Item.Rum) < 20", (c, s, a) => Metric("stock", a.S("post"), a.S("item")));
		Fn("world", "furs", P1("post:string"), "int", "The furs in a post's ledger.", "world.furs(\"York Factory\") > 500", (c, s, a) => Metric("furs", a.S("post")));
		Fn("world", "hasPost", P1("place:string"), "bool", "True once a post stands there.", "world.hasPost(\"Fort William\")", (c, s, a) => Pred("post", a.S("place")));
		Fn("world", "isDead", P1("name:string"), "bool", "True once the named person is dead.", "world.isDead(\"Miles Macdonell\")", (c, s, a) => Pred("dead", a.S("name")));
		Fn("world", "inMonths", P1("first:int", "last:int?"), "bool", "True in these months of the year (a range if two are given).", "world.inMonths(6, 8)",
			(c, s, a) => Pred("month", a.Has("last") ? $"{a.I("first")}-{a.I("last")}" : $"{a.I("first")}"));
		Fn("world", "raiseTension", P1("amount:int", "why:string?"), "effect", "Raise (or, if negative, lower) the tension, with a line for the log.", "world.raiseTension(10, \"the colony is armed\")",
			(c, s, a) => Fx(c, "tension", ("add", a.N("amount")), ("why", a.Has("why") ? a.S("why") : null)));
		Fn("world", "setLondon", P1("price:float", "quota:float"), "effect", "Set the London market's price and quota multipliers.", "world.setLondon(0.85, 0.85)",
			(c, s, a) => Fx(c, "london", ("price", a.N("price")), ("quota", a.N("quota"))));
		Fn("world", "foundPost", P1("place:string"), "effect", "A post is founded there.", "world.foundPost(\"Fort William\")", (c, s, a) => Fx(c, "found_post", ("place", a.S("place"))));
		Fn("world", "addStock", P1("post:string", "item:Item", "amount:int"), "effect", "Put goods in (or, if negative, take them out of) a post's ledger.", "world.addStock(\"York Factory\", Item.Rum, -5)",
			(c, s, a) => Fx(c, "stock", ("post", a.S("post")), ("item", a.S("item")), ("add", a.N("amount"))));

		// ---- the player
		Fn("player", "did", P1("deed:string"), "bool", "True once the player has done this at least once: truce, raid, escort or courier.", "player.did(\"truce\")", (c, s, a) => Pred("flag", "deed:" + a.S("deed")));
		Fn("player", "deeds", P1("deed:string"), "int", "How many times the player has done it.", "player.deeds(\"escort\") >= 2", (c, s, a) => Metric("var", "deed:" + a.S("deed")));

		// ---- flags
		Fn("flags", "has", P1("name:string"), "bool", "True when the flag is raised.", "flags.has(\"governor_killed\")", (c, s, a) => Pred("flag", a.S("name")));
		Fn("flags", "set", P1("name:string"), "effect", "Raise a flag.", "flags.set(\"governor_killed\")", (c, s, a) => Fx(c, "flag", ("name", a.S("name"))));
		Fn("flags", "clear", P1("name:string"), "effect", "Lower a flag.", "flags.clear(\"calm\")", (c, s, a) => Fx(c, "unflag", ("name", a.S("name"))));

		// ---- beats
		Prop("Beat", "done", "bool", "True once the beat has happened.", "SevenOaks.done", (c, s, a) => Pred("beat", ((BeatV)s).Id));
		Prop("Beat", "count", "int", "How many times the beat has happened (it can repeat).", "Raid.count >= 2", (c, s, a) => Metric("count", ((BeatV)s).Id));
		Prop("Beat", "age", "int", "Days since the beat last happened.", "SevenOaks.age > 90", (c, s, a) => Metric("beat_age", ((BeatV)s).Id));

		// ---- brigades
		Fn("", "brigade", P1("run:string"), "Brigade", "A brigade run on the rivers, by name (Data/World/brigade_runs.json).", "brigade(\"the York boats\").isLate", (c, s, a) => new BrigadeV(a.S("run")));
		foreach (var (name, pred, doc) in new[]
		{
			("isLate", "convoy_late", "has missed its day"), ("isLost", "convoy_lost", "has been lost"), ("isRobbed", "convoy_robbed", "has been robbed"),
			("wasTurnedBack", "convoy_turned_back", "was turned back"), ("isOut", "convoy_out", "is out on the river"), ("isIn", "convoy_in", "has arrived"),
			("isWintering", "convoy_wintering", "is wintering on the way"),
		})
			Prop("Brigade", name, "bool", $"True when the brigade {doc}.", $"brigade(\"the York boats\").{name}", (c, s, a) => Pred(pred, ((BrigadeV)s).Run));
		Prop("Brigade", "daysLate", "int", "How many days late the brigade is.", "brigade(\"the York boats\").daysLate > 10", (c, s, a) => Metric("convoy_days_late", ((BrigadeV)s).Run));
		Prop("Brigade", "daysOut", "int", "How many days the brigade has been out.", "brigade(\"the York boats\").daysOut > 40", (c, s, a) => Metric("convoy_days_out", ((BrigadeV)s).Run));
		Fn("Brigade", "delay", P1("days:int?"), "effect", "Hold the brigade up.", "brigade(\"the York boats\").delay(4)", (c, s, a) => Fx(c, "brigade", ("run", ((BrigadeV)s).Run), ("effect", "delay"), ("days", a.Has("days") ? a.N("days") : null)));
		Fn("Brigade", "turnBack", P1("days:int?"), "effect", "Turn the brigade back.", "brigade(\"the York boats\").turnBack()", (c, s, a) => Fx(c, "brigade", ("run", ((BrigadeV)s).Run), ("effect", "turn_back"), ("days", a.Has("days") ? a.N("days") : null)));
		Fn("Brigade", "lose", P1(), "effect", "The brigade is lost with all hands and goods.", "brigade(\"the York boats\").lose()", (c, s, a) => Fx(c, "brigade", ("run", ((BrigadeV)s).Run), ("effect", "lose")));
		Fn("Brigade", "cancel", P1(), "effect", "The brigade does not sail this year.", "brigade(\"the York boats\").cancel()", (c, s, a) => Fx(c, "brigade", ("run", ((BrigadeV)s).Run), ("effect", "cancel")));
		Fn("Brigade", "launch", P1("leg:int?"), "effect", "Send the brigade out now, from a leg of its route.", "brigade(\"the York boats\").launch()", (c, s, a) => Fx(c, "brigade", ("run", ((BrigadeV)s).Run), ("effect", "launch"), ("leg", a.Has("leg") ? a.N("leg") : null)));

		// ---- effects
		Fn("", "news", P1("from:string", "to:Person", "subject:string", "text:string", "days:int", "company:Company?"), "effect",
			"A letter arrives for the player after some days.", "news(from: \"Montreal\", to: Person.Partner, subject: \"war\", text: \"...\", days: 75, company: Company.NWC)",
			(c, s, a) => Fx(c, "news", ("from", a.S("from")), ("to", a["to"] is EnumV pe ? Persons[pe.Value] : a.S("to")), ("subject", a.S("subject")), ("text", a.S("text")), ("days", a.N("days")), ("company", a.Has("company") ? Co(a["company"]) : null)));
		Fn("", "say", P1("text:string", "to:Company?"), "effect", "A line in the log, for one company or for everyone.", "say(\"The boats are overdue.\", Company.HBC)",
			(c, s, a) => Fx(c, "say", ("text", a.S("text")), ("to", a.Has("to") ? Co(a["to"]) : null)));
		Fn("", "shout", P1("text:string", "to:Company?"), "effect", "Like say, but as a headline.", "shout(\"The governor is dead!\")",
			(c, s, a) => Fx(c, "say", ("text", a.S("text")), ("to", a.Has("to") ? Co(a["to"]) : null), ("headline", true)));
		Fn("", "trigger", P1("beat:Beat"), "effect", "Make another beat happen now.", "trigger(TheEnd)", (c, s, a) => Fx(c, "trigger", ("beat", a.S("beat"))));
		Fn("", "after", P1("delay:Days", "then:Lambda"), "effect", "Do something some days from now.", "after(days(10), () => trigger(TheEnd))",
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
	}

	private static string[] P1(params string[] specs) => specs;
}
