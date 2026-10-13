using System;
using System.Collections.Generic;

namespace IsometricFurTradeGame.Scripts.Core;

// How hard the campaign holds the world to its script (design doc 2.3).
public enum Adherence { Gentle, Steady, Firm, Strict }

// What the campaign needs of the world beyond the language's own conditions
// and actions: a line for the log, the year turning over (posts are founded
// on 1 January), and history before the start.
public interface ICampaignHost : ICampaignWorld
{
	void Note(string text);
	void NewYear(int year);
	void Founded(string place);
}

public sealed class CampaignBeat
{
	public string Id = "", Title = "", Kind = "event";
	public int Date = -1, Window = 60, LeadDays = 30, DelayDays, RepeatDays;
	public string[] Requires = Array.Empty<string>(), Keywords = Array.Empty<string>();
	// The lifecycle (see docs/wiki/Beats-and-the-Lifecycle.md):
	//   When = AwakenWhen, OnAwaken, Pressure = OnApproach, ActivateWhen, Happens = OnActivate,
	//   DoneWhen = ResolveWhen, OnResolve, CancelWhen, OnCancel
	public List<object> When = new(), DoneWhen = new(), Pressure = new(), Happens = new();
	public List<object> OnAwaken = new(), ActivateWhen = new(), OnResolve = new(), CancelWhen = new(), OnCancel = new();
}

// A campaign script read into plain data: the beats, and what the file said
// about itself. Parsed from the JSON tree (dictionaries, lists, strings,
// doubles, bools) so that nothing here needs the engine.
public sealed class CampaignScript
{
	public string Title = "", Description = "";
	public bool PostsByFoundingYear;
	public uint? Seed;
	public readonly Dictionary<string, double> InitialVars = new();   // variables that start at something other than 0
	public readonly List<CampaignBeat> Beats = new();

	public static CampaignScript Parse(Dictionary<string, object> root, string fallbackTitle = "")
	{
		var s = new CampaignScript
		{
			Title = Text(root, "title", fallbackTitle), Description = Text(root, "description", ""),
			PostsByFoundingYear = root.TryGetValue("posts_by_founding_year", out var pby) && pby is true,
		};
		if (root.TryGetValue("seed", out var sd)) s.Seed = (uint)(long)CampaignLogic.Num(sd);
		if (root.GetValueOrDefault("vars") is Dictionary<string, object> vars) foreach (var kv in vars) s.InitialVars[kv.Key] = CampaignLogic.Num(kv.Value);
		if (root.GetValueOrDefault("beats") is not List<object> beats) return s;
		foreach (var bo in beats)
		{
			if (bo is not Dictionary<string, object> b) continue;
			var beat = new CampaignBeat
			{
				Id = Text(b, "id", $"beat{s.Beats.Count}"), Title = Text(b, "title", ""), Kind = Text(b, "kind", "event"),
				Date = b.TryGetValue("date", out var dt) ? CampaignLogic.DayNumber(CampaignLogic.Str(dt)) : -1,
				Window = Int(b, "window_days", 60), LeadDays = Int(b, "lead_days", 30), DelayDays = Int(b, "delay_days", 0), RepeatDays = Int(b, "repeat_days", 0),
				Requires = Strings(b, "requires"), Keywords = Strings(b, "keywords"),
				When = ListOf(b, "when"), DoneWhen = ListOf(b, "done_when"), Pressure = ListOf(b, "pressure"), Happens = ListOf(b, "happens"),
				OnAwaken = ListOf(b, "on_awaken"), ActivateWhen = ListOf(b, "activate_when"), OnResolve = ListOf(b, "on_resolve"),
				CancelWhen = ListOf(b, "cancel_when"), OnCancel = ListOf(b, "on_cancel"),
			};
			foreach (var effects in new[] { beat.Pressure, beat.Happens, beat.OnAwaken, beat.OnResolve, beat.OnCancel }) Defaults(effects, beat);
			s.Beats.Add(beat);
		}
		return s;
	}

	private static string Text(Dictionary<string, object> d, string k, string def) => d.TryGetValue(k, out var v) ? CampaignLogic.Str(v) : def;
	private static int Int(Dictionary<string, object> d, string k, int def) => d.TryGetValue(k, out var v) ? (int)CampaignLogic.Num(v) : def;

	private static string[] Strings(Dictionary<string, object> d, string k)
	{
		if (!d.TryGetValue(k, out var v) || v == null) return Array.Empty<string>();
		if (v is List<object> l) { var r = new string[l.Count]; for (int i = 0; i < r.Length; i++) r[i] = CampaignLogic.Str(l[i]); return r; }
		return new[] { CampaignLogic.Str(v) };
	}

	// A field that may be one thing or a list of them (a condition, an action).
	private static List<object> ListOf(Dictionary<string, object> d, string key)
	{
		if (!d.TryGetValue(key, out var v) || v == null) return new List<object>();
		return v is List<object> l ? l : new List<object> { v };
	}

	// What a bare action took from its beat before there were conditionals:
	// a flag with no name is the beat's own, a tension with no reason is its title,
	// a news letter remembers which beat sent it. Every action is stamped with its beat.
	private static void Defaults(object actions, CampaignBeat b)
	{
		if (actions is Dictionary<string, object> one) actions = new List<object> { one };
		if (actions is not List<object> list) return;
		foreach (var o in list)
		{
			if (o is not Dictionary<string, object> a) continue;
			string act = a.TryGetValue("do", out var n) ? CampaignLogic.Str(n) : "";
			a["_beat"] = b.Id;
			if (act is "flag" or "unflag" && !a.ContainsKey("name")) a["name"] = b.Id;
			if (act == "tension" && !a.ContainsKey("why")) a["why"] = b.Title;
			if (act == "news" && !a.ContainsKey("subject")) a["subject"] = b.Title;
			Defaults(a.GetValueOrDefault("then"), b); Defaults(a.GetValueOrDefault("else"), b);
			if (a.GetValueOrDefault("options") is List<object> opts) foreach (var op in opts) if (op is Dictionary<string, object> od) Defaults(od.GetValueOrDefault("do"), b);
		}
	}
}

public sealed class FiredBeat
{
	public string Id = "", How = "";
	public int Day;
}

// The campaign's day (design doc 2.3, 4.4), with nothing of the engine in it:
// which beats are due, which are pressed, which are made to happen, at each
// level of adherence. Campaign.cs feeds it the real world; tools/CampaignTool
// feeds it a made-up one and plays a script forward.
public sealed class CampaignRunner
{
	public CampaignScript Script = new();
	public Adherence Level = Adherence.Steady;
	public readonly CampaignState State = new();
	public readonly HashSet<string> Pressured = new();
	public readonly HashSet<string> Awakened = new(), Cancelled = new();
	public readonly Dictionary<string, int> ReadySince = new();
	public readonly Dictionary<string, int> Shift = new(), Cooldown = new();   // a repeating beat's date moves on; a conditional one waits
	public readonly List<FiredBeat> Fired = new();
	public readonly List<string> History = new();

	public void Reset()
	{
		State.Flags.Clear(); State.Done.Clear(); State.Vars.Clear(); State.DoneDay.Clear(); State.Timers.Clear(); State.Forced.Clear();
		State.Seed = 20240611;
		Pressured.Clear(); Awakened.Clear(); Cancelled.Clear(); ReadySince.Clear(); Shift.Clear(); Cooldown.Clear(); Fired.Clear(); History.Clear();
	}

	public void Load(CampaignScript script)
	{
		Script = script;
		if (script.Seed is uint seed) State.Seed = seed;
	}

	private bool Ready(CampaignBeat b) => CampaignLogic.AllIn(b.Requires, r => State.Done.Contains(r) || State.Flags.Contains(r));

	// A game that starts later: what was dated before it is history already.
	// Its flags hold, but nobody is sent news of it or pushed toward it.
	public void Begin(int today, ICampaignHost host)
	{
		State.Today = today;
		foreach (var kv in Script.InitialVars) if (!State.Vars.ContainsKey(kv.Key)) State.Vars[kv.Key] = kv.Value;
		foreach (var b in Script.Beats)
		{
			if (b.Date < 0 || b.Date >= today || b.RepeatDays > 0 || !Ready(b)) continue;
			foreach (var o in b.Happens)
			{
				if (o is not Dictionary<string, object> a) continue;
				string act = a.TryGetValue("do", out var n) ? CampaignLogic.Str(n) : "";
				if (act == "flag") State.Flags.Add(CampaignLogic.Str(a["name"]));
				else if (act == "unflag") State.Flags.Remove(CampaignLogic.Str(a["name"]));
				else if (act == "found_post") host.Founded(a.TryGetValue("place", out var pl) ? CampaignLogic.Str(pl) : "");
			}
			State.Done.Add(b.Id); State.Flags.Add(b.Id); State.DoneDay[b.Id] = b.Date;
			History.Add(b.Id);
		}
	}

	// The script's own date for a beat, moved on by however often it has come round.
	private int DateOfBeat(CampaignBeat b) => b.Date < 0 ? -1 : b.Date + Shift.GetValueOrDefault(b.Id);

	// One day.
	public void Tick(int today, ICampaignHost host)
	{
		State.Today = today;
		var date = CampaignLogic.DateOf(today);
		if (Script.PostsByFoundingYear && date.Month == 1 && date.Day == 1) host.NewYear(date.Year);

		// What an action asked to happen some days on.
		foreach (var due in CampaignLogic.Due(State)) CampaignLogic.Run(due, State, host);

		foreach (var b in Script.Beats)
		{
			if (State.Done.Contains(b.Id) || Cancelled.Contains(b.Id)) continue;
			if (Cooldown.TryGetValue(b.Id, out int resting) && today < resting) continue;
			// It can never happen now.
			if (b.CancelWhen.Count > 0 && CampaignLogic.All(b.CancelWhen, State, host))
			{
				Cancelled.Add(b.Id);
				CampaignLogic.Run(b.OnCancel, State, host);
				host.Note($"[cancelled] {b.Title}");
				continue;
			}
			if (!Ready(b) || !CampaignLogic.All(b.When, State, host)) continue;
			if (!ReadySince.ContainsKey(b.Id))
			{
				ReadySince[b.Id] = today;
				if (Awakened.Add(b.Id)) CampaignLogic.Run(b.OnAwaken, State, host);   // the moment it wakes
			}
			// The world got there by itself.
			if (b.DoneWhen.Count > 0 && CampaignLogic.All(b.DoneWhen, State, host))
			{
				CampaignLogic.Run(b.OnResolve, State, host);
				Finish(b, "by the world", false, host);
				continue;
			}
			int when = DateOfBeat(b);
			int due = when >= 0 ? when : ReadySince[b.Id] + b.DelayDays;
			if (today < due - b.LeadDays) continue;

			if (Level >= Adherence.Steady && Pressured.Add(b.Id)) CampaignLogic.Run(b.Pressure, State, host);
			if (today < due) continue;
			switch (b.Kind)
			{
				// Outside anyone's reach (design doc 4.4): the blockade, the
				// colony's founding, Selkirk's answer to a governor's killing.
				case "fixed" or "conditional":
					if (!CampaignLogic.All(b.ActivateWhen, State, host)) break;   // due, but not yet
					CampaignLogic.Run(b.Happens, State, host);
					Finish(b, b.Kind == "fixed" ? "on its day" : "when its conditions held", false, host);
					break;
				case "pressure":
					if (Level == Adherence.Gentle || Pressured.Contains(b.Id)) Finish(b, "as pressure", false, host);
					break;
				default:
					// An event the world may still produce itself; once its grace has run out it is made to happen (when ActivateWhen allows).
					int grace = Math.Max(b.Window, 1);
					bool force = Level == Adherence.Strict && today >= due + grace || Level == Adherence.Firm && today >= due + 2 * grace;
					if (force && CampaignLogic.All(b.ActivateWhen, State, host)) { CampaignLogic.Run(b.Happens, State, host); Finish(b, "forced", true, host); }
					break;
			}
		}

		// Beats an action called for by name happen now, whatever their date.
		for (int guard = 0; State.Forced.Count > 0 && guard < 20; guard++)
		{
			var ids = new List<string>(State.Forced);
			State.Forced.Clear();
			foreach (var id in ids)
			{
				var fb = Script.Beats.Find(x => x.Id == id);
				if (fb == null) { host.Note($"trigger: no beat '{id}'"); continue; }
				if (State.Done.Contains(id)) continue;
				CampaignLogic.Run(fb.Happens, State, host);
				Finish(fb, "triggered", true, host);
			}
		}
	}

	private void Finish(CampaignBeat b, string how, bool forced, ICampaignHost host)
	{
		State.Done.Add(b.Id);
		State.Flags.Add(b.Id);
		State.DoneDay[b.Id] = State.Today;
		State.Vars["count:" + b.Id] = State.Vars.GetValueOrDefault("count:" + b.Id) + 1;
		Fired.Add(new FiredBeat { Id = b.Id, How = how, Day = State.Today });
		host.Note(forced ? $"[forced] {b.Title}" : b.Title);
		// A beat that comes round again: undone now, with its date (or its
		// waiting) moved on, so it can happen next year as well.
		if (b.RepeatDays > 0)
		{
			State.Done.Remove(b.Id); Pressured.Remove(b.Id); ReadySince.Remove(b.Id); Awakened.Remove(b.Id);
			if (b.Date >= 0) Shift[b.Id] = Shift.GetValueOrDefault(b.Id) + b.RepeatDays;
			else Cooldown[b.Id] = State.Today + b.RepeatDays;
		}
	}

	// Words the director listens for while a beat is near (design doc 2.2's
	// "who talks about what"): news bearing on what is coming gets told first.
	public IEnumerable<string> Keywords(int today)
	{
		foreach (var b in Script.Beats)
		{
			if (State.Done.Contains(b.Id) || b.Date < 0 || today < b.Date - 120 || today > b.Date + 2 * b.Window) continue;
			foreach (var k in b.Keywords) yield return k;
		}
	}
}
