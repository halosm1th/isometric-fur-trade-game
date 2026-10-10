using System;
using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;

namespace IsometricFurTradeGame.Scripts.Core;

// Simulate, then curate (design doc 2.2). The director never makes an event,
// never changes an outcome and never hands anyone knowledge the propagation
// system would not have carried. It only biases things that were going to
// happen anyway: which of a man's memories he brings out, which of two
// equally good lodges a party walks to, and whether a thing that has been
// waiting to happen happens this afternoon or next week.
//
// The rule that keeps it honest: switch it off (`-- --nodirector`) and the
// world must still be valid. Every beat it put on stage would have happened
// somewhere; it decides what is on stage, never what is in the play.
public static class Director
{
	public static bool Enabled = true;

	private struct Beat { public string Kind; public float At; public float Weight; }
	private static readonly List<Beat> _beats = new();
	private static readonly Dictionary<string, int> _counts = new();

	private static float Now()
	{
		var cal = GameCalendar.Instance;
		return cal == null ? 0f : cal.Day * 24f + cal.Hour;
	}

	public static void Reset() { _beats.Clear(); _counts.Clear(); }

	public static void ReadArgs()
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
			if (arg == "--nodirector") { Enabled = false; GD.Print("[director] off: the world runs with nothing curated"); }
	}

	// ---- pacing memory -----------------------------------------------------

	public static void Saw(string kind, float weight = 1f)
	{
		// A loaded save puts the clock back; beats from a future that no longer
		// happened would otherwise freeze the pacing for good.
		float now = Now();
		_beats.RemoveAll(b => b.At > now);
		_beats.Add(new Beat { Kind = kind, At = now, Weight = weight });
		_counts[kind] = _counts.GetValueOrDefault(kind) + 1;
		if (_beats.Count > 64) _beats.RemoveRange(0, 32);
	}

	public static float HoursSince(string kind)
	{
		float best = 9999f, now = Now();
		foreach (var b in _beats) if (b.Kind == kind && b.At <= now) best = Mathf.Min(best, now - b.At);
		return best;
	}

	// How long since the last piece of real trouble. Ordinary news (a birth, a
	// ship, a death at another post) is a beat the director remembers but does
	// not count as trouble, or nothing heavy would ever get through.
	public static float QuietHours
	{
		get
		{
			float best = 9999f, now = Now();
			foreach (var b in _beats) if (b.Weight >= 1.5f && b.At <= now) best = Mathf.Min(best, now - b.At);
			return best;
		}
	}

	// Should a thing that has been waiting to happen happen now? A heavy beat
	// wants room on either side of it, the same kind of trouble should not come
	// twice in a row, and three crises in one afternoon is not a story. A long
	// quiet stretch makes the director readier to let the next one through.
	// `false` means "not yet" - the caller is expected to ask again later.
	public static bool MayActNow(string kind, float weight = 1f, Vector2? where = null)
	{
		if (!Enabled) return true;
		// A thing nobody can possibly witness is nothing to pace: let the world
		// have it. (Design doc 2.2: the director only decides what is on stage.)
		if (where.HasValue && NearestPlayerDistance(where.Value) > 6500f) return true;

		float quiet = QuietHours;
		float spacing = (5f + 12f * weight) * (quiet > 48f ? 0.4f : 1f);
		if (quiet < spacing) return false;
		// Nor on the heels of whatever news just reached him.
		if (HoursSince("headline") < 3f) return false;
		if (HoursSince(kind) < (quiet > 72f ? 18f : 40f)) return false;
		return true;
	}

	// ---- whose route crosses whose ----------------------------------------

	// Among options the chooser thinks are as good as each other, the one that
	// passes nearer a player. `score` is the chooser's own valuation; only
	// options within `tolerance` of the best are considered equal.
	public static T Equal<T>(IEnumerable<T> options, Func<T, float> score, Func<T, Vector2> at, float tolerance = 0.12f) where T : class
	{
		T best = null; float bestScore = float.MinValue;
		foreach (var o in options) { float s = score(o); if (s > bestScore) { bestScore = s; best = o; } }
		if (best == null || !Enabled || bestScore <= 0f) return best;
		T nearest = best; float nearestD = NearestPlayerDistance(at(best));
		foreach (var o in options)
		{
			if (score(o) < bestScore * (1f - tolerance)) continue;
			float d = NearestPlayerDistance(at(o));
			if (d < nearestD) { nearest = o; nearestD = d; }
		}
		return nearest;
	}

	public static float NearestPlayerDistance(Vector2 world)
	{
		float best = 999999f;
		foreach (var p in Sim.Players()) if (p != null && GodotObject.IsInstanceValid(p)) best = Mathf.Min(best, p.GlobalPosition.DistanceTo(world));
		return best;
	}

	// ---- who talks about what ---------------------------------------------

	// How much a piece of news is about the player's own life: his name, his
	// kin, the men he has business with, the names in his book, what he has
	// declared he means to do. Used to pick which of several true things a man
	// brings out, never to invent one.
	public static float Relevance(string text, Player.PlayerController p)
	{
		if (!Enabled || p == null || string.IsNullOrEmpty(text)) return 0f;
		float score = 0f;
		if (text.Contains(p.DisplayName)) score += 3f;
		if (p.Spouse != null && text.Contains(p.Spouse.DisplayName)) score += 2f;
		foreach (var f in p.House) if (text.Contains(f.Name)) score += 2f;
		foreach (var g in Grudges.All)
		{
			if (g.Settled) continue;
			if (g.Against == p.DisplayName && text.Contains(g.Holder)) score += 2f;
			if (g.Holder == p.DisplayName && text.Contains(g.Against)) score += 2f;
		}
		foreach (var a in p.Ambitions)
		{
			if (a.Abandoned) continue;
			foreach (string w in Keywords(a.Kind)) if (text.Contains(w, StringComparison.OrdinalIgnoreCase)) score += 1f;
		}
		return score;
	}

	// Pick the most relevant of several true things. Ties keep the caller's order.
	public static T MostRelevant<T>(IEnumerable<T> candidates, Func<T, string> text, Player.PlayerController p) where T : class
	{
		T best = null; float bestScore = -1f;
		foreach (var c in candidates)
		{
			float s = Relevance(text(c), p);
			if (best == null || s > bestScore) { best = c; bestScore = s; }
		}
		return best;
	}

	private static IEnumerable<string> Keywords(AmbitionKind kind) => kind switch
	{
		AmbitionKind.RiseAtThePost => new[] { "clerk", "postmaster", "Factor", "stands well" },
		AmbitionKind.MarryIn => new[] { "wife", "married", "daughter", "the band" },
		AmbitionKind.PrivateTrade => new[] { "furs", "trade", "goods", "price" },
		AmbitionKind.DriveOffTheRival => new[] { "Nor'Wester", "the other house", "raid", "seize" },
		AmbitionKind.KeepTheBandWhole => new[] { "the band", "hungry", "want", "camp" },
		AmbitionKind.FoundAPost => new[] { "Oxford House", "post", "the charge of", "inland" },
		_ => Array.Empty<string>(),
	};

	public static string Describe()
	{
		if (!Enabled) return "The director is off.";
		return $"Quiet {QuietHours:0} h; beats held: {_beats.Count}.";
	}
}
