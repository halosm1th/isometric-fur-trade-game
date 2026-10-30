using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CampaignTool;

// What the language says about itself beyond the library: the beat's hooks and
// header, and the words that are not a library member. The editor completes
// from these, and the wiki is written from them.
public static class LangDocs
{
	public sealed record Hook(string Name, string Key, bool Condition, string Summary);
	public sealed record Arg(string Name, string Type, string Summary);

	public static readonly Hook[] Hooks =
	{
		new("AwakenWhen", "when", true, "A beat only does anything on the days this holds (and its requirements are met). The first such day it wakes, and OnAwaken runs; a dateless beat counts its delay from then. Leave it out and the beat is awake from the start."),
		new("OnAwaken", "on_awaken", false, "What happens at the moment the beat wakes, once. A repeating beat does it again each time round."),
		new("OnApproach", "pressure", false, "Pressure on the world as the beat's day draws near: it runs once, on the first awake day within the lead time before the date (Steady adherence and up; never at Gentle). This is how a beat makes the world ready for itself."),
		new("ActivateWhen", "activate_when", true, "A last condition the beat waits for once it is due, and that a forced event waits for too. If it does not hold the beat is due but not yet happening. trigger() does not ask it."),
		new("OnActivate", "happens", false, "What the beat does when it happens."),
		new("ResolveWhen", "done_when", true, "The world got there by itself: asked every day the beat is awake, even long before its date. The first day it holds, OnResolve runs and the beat is over without OnActivate running."),
		new("OnResolve", "on_resolve", false, "What happens when the world resolves the beat."),
		new("CancelWhen", "cancel_when", true, "The beat can no longer happen. Checked before everything else, on every day, whatever the adherence."),
		new("OnCancel", "on_cancel", false, "What happens when the beat is cancelled."),
	};

	public static readonly Arg[] HeaderArgs =
	{
		new("title", "string", "The campaign's name in the menu. Required."),
		new("description", "string", "A few sentences about the campaign, shown under its name."),
		new("note", "string", "A note kept in the JSON, for whoever opens it. Free text."),
		new("seed", "int", "The seed for the script's dice (chance, random). The same seed gives the same history."),
		new("postsByFoundingYear", "bool", "When true, posts on the world map appear on 1 January of the year they were founded. Without it, a post founded after the start appears only if a beat founds it."),
		new("start", "Date", "The world's first day, for every starting path: date(1815, 5, 1). Without it the world begins in 1811, on the starting path's own day (10 June, 12 July or 20 August). What the script dates before the first day is history. A developer's --date wins."),
		new("startYear", "int", "Only the year the world begins in: each starting path keeps its own day (the ship on 20 August and so on) in that year. Give start or startYear, not both."),
		new("include", "string", "Parts of this script kept in their own files, tools/CampaignTool/src/parts/<name>.camp (several: \"a, b\"). A part has no header: its consts, beats and quest lines are this campaign's own, compiled into one JSON. Opened in the editor, a part is checked, drawn and simulated inside the campaign that includes it."),
		new("extends", "string", "Another campaign (by its id, the file's name) whose beats this one plays as well: a story with quest lines can be built on \"historical\" and share its war. Where both have a beat of the same id, this campaign's wins."),
	};

	// Quests (see the Quests page of the guide).
	public static readonly Hook[] QuestHooks =
	{
		new("StartWhen", "start_when", true, "A quest starts (or is offered by its giver) only once this holds, and what it requires is done."),
		new("FailWhen", "fail_when", true, "Asked all the while the quest is under way: the first time it holds, the quest fails."),
		new("OnStart", "on_start", false, "What happens when the quest starts."),
		new("OnComplete", "on_complete", false, "What happens when the quest is done."),
		new("OnFail", "on_fail", false, "What happens when the quest fails."),
	};
	public static readonly Hook[] StepHooks =
	{
		new("Says", "dialogue.text", true, "What the person says when the player raises the step with them (a talk or bring step): Says() => \"...\";"),
		new("Done", "cond", true, "The step's condition: a when step is done once it holds (when Name(...) => condition; is the short form)."),
		new("OnDone", "on_done", false, "What happens when the step is done, before the quest moves on."),
	};
	public static readonly Arg[] LineArgs =
	{
		new("id", "string", "The line's id, if it is not to be its name."),
		new("title", "string", "The line's name in the journal. Required."),
		new("summary", "string", "A line about what the story is."),
		new("forCompany", "Company", "Only players of this company (Company.HBC or Company.NWC) are given the line's quests. Each quest can say otherwise."),
	};
	public static readonly Arg[] QuestArgs =
	{
		new("id", "string", "The quest's id, if it is not to be its name."),
		new("title", "string", "The quest's name in the journal and on the screen. Required."),
		new("summary", "string", "What the quest is: shown when it starts, or when its giver offers it."),
		new("giver", "Person", "Someone who offers the quest when spoken to (Person.Factor, \"leader@Edmonton House\", a name). Without one, the quest starts by itself once it may."),
		new("forCompany", "Company", "Only players of this company get the quest (default: the line's)."),
	};
	public static readonly Arg[] StepArgs =
	{
		new("id", "string", "The step's id, if it is not to be its name."),
		new("text", "string", "What the player is told to do, on the screen and in the journal."),
		new("who", "Person", "talk, bring: the person (a name, Person.Factor, or a role at a place: \"leader@Edmonton House\", \"chief@<camp>\", \"partner@Fort Augustus\")."),
		new("place", "string", "reach: a post, a camp or a place on the map."),
		new("radius", "int", "reach: how near counts as there, in pixels (default 400)."),
		new("item", "Item", "bring, have: what."),
		new("count", "int", "bring, have: how many; task: how much of the work; hunt: how many beasts (default 1)."),
		new("task", "Task", "task: the work handed out, as the Factor would (Task.CarryCargo ...)."),
		new("convoy", "string", "task Task.Brigade: the brigade run to go with."),
		new("dest", "string", "task Task.Brigade: where the boats are bound (default: the run's next landing)."),
		new("species", "string", "hunt: what to kill (Buffalo, Caribou ...; empty for anything)."),
		new("days", "int", "wait: how many days."),
		new("next", "Step", "Where the quest goes after this step (another step, \"complete\" or \"fail\"); default the next one written."),
	};
	public static readonly Arg[] ChoiceArgs =
	{
		new("label", "string", "What the player says. Required."),
		new("reply", "string", "What comes back."),
		new("when", "bool", "The choice is offered only while this holds."),
		new("goto", "Step", "Where the quest goes when this is chosen (a step, \"complete\" or \"fail\"); default the step after."),
	};
	public static readonly string[] StepKinds = { "talk", "reach", "task", "bring", "have", "hunt", "when", "wait" };

	public static readonly Arg[] BeatArgs =
	{
		new("id", "string", "The beat's id, if it is not to be its name. Needed when the id has a hyphen, or to give each beat of a loop its own."),
		new("title", "string", "What the log and the news call the beat. Defaults to the id."),
		new("kind", "Kind", "Fixed, Conditional, Event or Pressure. Almost never written: it is derived from what the beat has (see the guide)."),
		new("date", "Date", "The day the beat is due. A beat with no date is due the moment it wakes, plus its delay."),
		new("window", "Days", "How long the world has to do the beat itself after its date before it is forced: Strict forces it one window late, Firm two windows late. Default 60 days."),
		new("lead", "Days", "How long before its date the beat's pressure (OnApproach) begins, and its rumour is first told. Default 30 days."),
		new("delay", "Days", "For a beat with no date: days after waking before it is due. Default 0."),
		new("repeat", "Days", "The beat comes round again this many days after it happened: a dated beat's date moves on by this much, a dateless beat sleeps this long."),
		new("rumour", "string", "What the player is told while the beat is stirring or its day draws near: one line for the journal's \"What is stirring\" list, written as something a person at the post could have heard. The beat's own title is never shown there, so a rumour is the only thing that says anything before the beat happens."),
		new("keywords", "string[]", "Words the news director looks for when it chooses what to tell the player, from 120 days before a dated beat's date until two windows after it. Free text."),
	};

	public static readonly string[] Keywords = { "campaign", "beat", "questline", "quest", "choice", "talk", "reach", "task", "bring", "have", "hunt", "when", "wait", "const", "void", "if", "else", "for", "foreach", "in", "true", "false" };
	public static readonly string[] Types = { "int", "float", "string", "bool", "Date", "Days", "Company", "Person", "Item", "Season", "Kind", "Beat", "Brigade", "Quest", "Task" };

	// The editor reads this once.
	public static Dictionary<string, object> LibraryJson()
	{
		var members = new List<object>();
		foreach (var m in Library.All)
			members.Add(Tree.Dict(
				("owner", m.Owner), ("name", m.Name), ("kind", m.Kind), ("signature", m.Signature), ("returns", m.Returns), ("doc", m.Doc), ("example", m.Example),
				("params", m.Params.Select(p => (object)Tree.Dict(("name", p.Name), ("type", p.Type), ("optional", p.Optional), ("rest", p.Rest), ("doc", p.Doc))).ToList())));
		var enums = new Dictionary<string, object>();
		foreach (var e in Library.EnumDocs.Keys) enums[e] = Library.EnumValues(e).Cast<object>().ToList();
		var enumDocs = new Dictionary<string, object>();
		foreach (var kv in Library.EnumDocs) enumDocs[kv.Key] = kv.Value;
		return Tree.Dict(
			("members", members), ("enums", enums), ("enumDocs", enumDocs),
			("typeDocs", Library.TypeDocs.ToDictionary(kv => kv.Key, kv => (object)kv.Value)),
			("hooks", Hooks.Select(h => (object)Tree.Dict(("name", h.Name), ("key", h.Key), ("condition", h.Condition), ("summary", h.Summary))).ToList()),
			("beatArgs", BeatArgs.Select(a => (object)Tree.Dict(("name", a.Name), ("type", a.Type), ("summary", a.Summary))).ToList()),
			("headerArgs", HeaderArgs.Select(a => (object)Tree.Dict(("name", a.Name), ("type", a.Type), ("summary", a.Summary))).ToList()),
			("questHooks", QuestHooks.Concat(StepHooks).Select(h => (object)Tree.Dict(("name", h.Name), ("key", h.Key), ("condition", h.Condition), ("summary", h.Summary))).ToList()),
			("questArgs", LineArgs.Concat(QuestArgs).Concat(StepArgs).Concat(ChoiceArgs).GroupBy(a => a.Name).Select(g => (object)Tree.Dict(("name", g.Key), ("type", g.First().Type), ("summary", g.First().Summary))).ToList()),
			("keywords", Keywords.Cast<object>().ToList()), ("types", Types.Cast<object>().ToList()),
			("objects", Tree.List("world", "flags", "player")),
			("stepKinds", StepKinds.Cast<object>().ToList()),
			("names", WorldNames()));
	}

	// The names a script may write in quotes, for the editor to suggest inside a string.
	private static Dictionary<string, object> WorldNames()
	{
		var w = Known.World;
		List<object> L(IEnumerable<string> xs) => xs.Distinct().OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Cast<object>().ToList();
		return Tree.Dict(
			("places", L(w.Places.Keys.Concat(w.Ledgers))), ("posts", L(w.Ledgers)), ("runs", L(w.Runs.Keys)),
			("towns", L(w.Towns.Keys)), ("buildings", L(w.Towns.Values.SelectMany(b => b))), ("camps", L(w.Camps)),
			("holders", L(Known.Holders)), ("deeds", L(new[] { "truce", "raid", "escort", "courier" })),
			("flags", L(Known.EngineFlags)));
	}

	// The wiki's Library-Reference page, written from the table.
	public static string ReferenceMarkdown()
	{
		var sb = new StringBuilder();
		sb.Append("<!-- Generated by `CampaignTool docs` from the library table (tools/CampaignTool/Library.cs). Do not edit by hand. -->\n\n");
		sb.Append("# Library reference\n\nEvery name the language gives you, with what it does and an example. ");
		sb.Append("Anything in `world`, `flags` and the beat and brigade types is a **member**: write it after a dot. ");
		sb.Append("The rest are **functions** you can call anywhere.\n\n");
		sb.Append("Types: `int` is a whole number, `float` any number, `string` text, `bool` a condition or true/false. ");
		sb.Append("A *Days* is a length of time, `days(10)`; a *Date* is `date(1816, 6, 19)`. ");
		sb.Append("*effect* means the call changes the world, so it belongs in an effect hook (`OnActivate` ...) and not in a condition.\n\n");
		sb.Append("The names you pass in (posts, brigade runs, items, people, letter addressees) are listed in the [World reference](World-Reference); what each effect really does once the game carries it out is in [Effects in the game](Effects-in-the-Game); the flags the game raises by itself are in [State, flags and variables](State-Flags-and-Variables).\n\n");
		sb.Append("## Beat header\n\n| Argument | Type | What it is |\n|---|---|---|\n");
		foreach (var a in BeatArgs) sb.Append($"| `{a.Name}` | `{a.Type}` | {a.Summary} |\n");
		sb.Append("\n## Campaign header\n\n| Argument | Type | What it is |\n|---|---|---|\n");
		foreach (var a in HeaderArgs) sb.Append($"| `{a.Name}` | `{a.Type}` | {a.Summary} |\n");
		sb.Append("\n## Hooks\n\n| Hook | Kind | What it does |\n|---|---|---|\n");
		foreach (var h in Hooks) sb.Append($"| `{h.Name}()` | {(h.Condition ? "condition (`=>`)" : "effects (`{ }`)")} | {h.Summary} |\n");
		void Table(string title, Arg[] args)
		{
			sb.Append($"\n## {title}\n\n| Argument | Type | What it is |\n|---|---|---|\n");
			foreach (var a in args) sb.Append($"| `{a.Name}` | `{a.Type}` | {a.Summary} |\n");
		}
		Table("Questline header", LineArgs);
		Table("Quest header", QuestArgs);
		Table($"Step header ({string.Join(", ", StepKinds)})", StepArgs);
		Table("Choice header", ChoiceArgs);
		sb.Append("\n## Quest and step hooks\n\n| Hook | Kind | What it does |\n|---|---|---|\n");
		foreach (var h in QuestHooks.Concat(StepHooks)) sb.Append($"| `{h.Name}()` | {(h.Condition ? "condition (`=>`)" : "effects (`{ }`)")} | {h.Summary} |\n");

		void Section(string title, string intro, IEnumerable<Member> ms)
		{
			var list = ms.ToList();
			if (list.Count == 0) return;
			sb.Append($"\n## {title}\n\n{intro}\n\n");
			foreach (var m in list)
			{
				sb.Append($"### `{m.Full}{(m.Kind == "function" ? "(" + string.Join(", ", m.Params.Select(p => p.Name + (p.Optional ? "?" : "") + (p.Rest ? "..." : ""))) + ")" : "")}`\n\n");
				sb.Append($"`{m.Signature}`\n\n{m.Doc}\n\n");
				if (m.Params.Length > 0)
				{
					foreach (var p in m.Params)
						sb.Append($"- `{p.Name}` ({p.Type}{(p.Optional ? ", optional" : "")}{(p.Rest ? ", one or more" : "")}): {p.Doc}\n");
					sb.Append('\n');
				}
				sb.Append($"```c\n{m.Example}\n```\n\n");
			}
		}
		Section("Functions", "Called by name, anywhere.", Library.Of("").Where(m => m.Kind == "function"));
		Section("world", "The state of the world: asked in conditions, changed in effects.", Library.Of("world"));
		Section("flags", "Named switches the campaign raises and lowers.", Library.Of("flags"));
		Section("player", "What the player has done, as the campaign has been told of it: the game records a *deed* when the player brokers a truce (`truce`), leads or joins a raid (`raid`), rides a brigade to its landing (`escort`) or carries a packet through (`courier`).", Library.Of("player"));
		Section("A beat", "A beat's own name is a value: `SevenOaks.done`. Beats are written in `[...]` after the header to be required, and passed to `trigger(...)`.", Library.Of("Beat"));
		Section("A brigade", "`brigade(\"name\")` is one of the runs in `Data/World/brigade_runs.json`.", Library.Of("Brigade"));
		Section("A quest", "A quest's own name is a value: `Landfall.done`. Quests are written in `[...]` after a quest's header to be required.", Library.Of("Quest"));

		sb.Append("\n## Named values\n\n");
		foreach (var kv in Library.EnumDocs)
			sb.Append($"- **`{kv.Key}`**: {kv.Value}\n" + (kv.Key == "Item" ? "" : "  Values: " + string.Join(", ", Library.EnumValues(kv.Key).Select(v => $"`{kv.Key}.{v}`")) + "\n"));
		sb.Append($"\nItems: {string.Join(", ", Library.EnumValues("Item").Select(v => $"`{v}`"))}.\n");
		return sb.ToString();
	}
}
