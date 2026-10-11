using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Economy;

namespace IsometricFurTradeGame.Scripts.Agents;

// Career is a contest between named men (design doc 3.7). Promotion is not a
// threshold on a bar: when the player means to rise, some other clerk at the
// same post means to rise too, and the seat goes to whichever of them the
// Factor would rather have. The rival is a real man running the same errands,
// reachable by the same means - outwork him, make a friend of him, or carry
// something true about him to the Factor and see whether it is believed.
public static class Rivals
{
	public class Contest
	{
		public Npc Him;
		public string Seat = "the clerk's desk";
		public float HisScore, YourScore;
		public bool HeWasAhead;
		public int LastWordDay = -999;
		public int HeMovedDay = -999;
		public int RefusalsSeen;
		public readonly List<string> Said = new();   // what the player has already carried
		public readonly List<string> HeSaid = new();   // and what he has carried about you
	}

	public static Contest Current;
	private static int _lastWeek = -99;

	public static void Reset() { Current = null; _lastWeek = -99; }

	// What the Factor is weighing, as far as either man's work shows.
	public static float Score(Npc n, FortLedger post)
	{
		if (n == null || n.IsDead) return 0f;
		float score = n.Standing + n.Personality.Ambition * 12f;
		if (post?.Leader != null && post.Leader != n) score += post.Leader.RapportWith(n) * 0.25f;
		if (n.Role == Role.Clerk) score += 10f;
		return score;
	}

	public static float Score(Player.PlayerController p)
	{
		if (p == null || p.IsDead) return 0f;
		float score = p.Standing + p.TasksDone * 3f - p.TasksRefused * 4f;
		if (p.Rank == "clerk") score += 10f;
		if (p.Rank == "postmaster") score += 20f;
		var post = p.HomePost ?? Sim.Fort;
		if (post?.Leader != null) score += post.Leader.RapportWith(p) * 0.25f;
		return score;
	}

	// Once a week: find the man who wants what you want, and tell the player
	// how the two of them stand when it changes - with the reason, which is
	// whatever the man actually did that the player could have heard about.
	public static void Tick()
	{
		var cal = GameCalendar.Instance;
		if (cal == null || !Net.Net.IsServer) return;
		int week = cal.Day / 7;
		if (week == _lastWeek) return;
		if (week < _lastWeek) Reset();
		_lastWeek = week;

		foreach (var p in Sim.Players())
		{
			if (p == null || p.IsDead) continue;
			string seat = Seat(p);
			if (seat == "") { Current = null; continue; }
			var post = p.HomePost ?? Sim.Fort;
			if (post == null) continue;

			if (Current == null || Current.Him == null || !GodotObject.IsInstanceValid(Current.Him) || Current.Him.IsDead || Current.Seat != seat)
			{
				var him = Pick(p, post);
				if (him == null) { Current = null; continue; }
				Current = new Contest { Him = him, Seat = seat };
				Current.HisScore = Score(him, post); Current.YourScore = Score(p);
				Current.HeWasAhead = Current.HisScore > Current.YourScore;
				Reveal.Word(p, $"{him.DisplayName} is after {seat} as well as you.",
					him.Role == Role.Clerk ? "he keeps the books and the Factor's ear" : "he has made himself useful this season", 1.3f);
				continue;
			}

			var c = Current;
			c.HisScore = Score(c.Him, post);
			c.YourScore = Score(p);
			HeMoves(c, p, post);
			bool aheadNow = c.HisScore > c.YourScore;
			if (aheadNow == c.HeWasAhead) continue;
			c.HeWasAhead = aheadNow;
			if (cal.Day - c.LastWordDay < 10) continue;
			c.LastWordDay = cal.Day;
			Reveal.Word(p, aheadNow
				? $"{c.Him.DisplayName} stands better for {c.Seat} than you do."
				: $"You stand better for {c.Seat} than {c.Him.DisplayName} does.",
				aheadNow ? Lately(c.Him) : "what you have got done this season", 1.4f);
		}
	}

	// What he does about you. He has the same verbs you have: the master's ear,
	// something true he saw you do, a lie if he has nothing true and is loose
	// enough to tell one, and the work you would not take (design doc 3.7).
	private static void HeMoves(Contest c, Player.PlayerController p, FortLedger post)
	{
		var him = c.Him;
		var master = post?.Leader;
		if (him == null || master == null || him == master || him.IsDead) return;
		int day = GameCalendar.Instance?.Day ?? 0;

		// A word at the right time, every week. This is most of how a seat is won.
		master.ChangeRapport(him, 1.5f);

		if (day - c.HeMovedDay < 7) return;
		bool behind = c.HisScore < c.YourScore;

		// Something true he saw you do, carried to the man who decides.
		string dirt = HeHasOnYou(him, p, c);
		if (behind && dirt != "" && GD.Randf() < 0.3f + him.Personality.Ambition * 0.4f)
		{
			c.HeMovedDay = day;
			c.HeSaid.Add(dirt);
			p.Standing = Mathf.Max(0f, p.Standing - 8f);
			master.ChangeRapport(p, -6f);
			Chronicle.Event($"{him.DisplayName} told {master.DisplayName} what {p.DisplayName} had done.", him.GlobalPosition, him, p.DisplayName, 1.3f);
			Reveal.Word(p, $"{him.DisplayName} has been in with the Factor about you, and your standing is down 8.", dirt, 1.6f);
			return;
		}

		// Nothing true, and a man loose enough to invent one: the lie travels
		// by the ordinary rules, and you may yet hear him tell it.
		if (behind && him.Personality.Loyalty < 0.5f && GD.Randf() < 0.2f + him.Personality.Ambition * 0.25f)
		{
			c.HeMovedDay = day;
			Lies.Tell(him, master, p, $"{p.DisplayName} has been trading on his own account", null, $"to get {c.Seat}");
			return;
		}

		// Work you would not take, he takes - and the master marks it.
		if (p.TasksRefused > c.RefusalsSeen)
		{
			c.RefusalsSeen = p.TasksRefused;
			c.HeMovedDay = day;
			him.Standing += 3f;
			master.ChangeRapport(him, 4f);
			Reveal.Word(p, $"{him.DisplayName} took the work you would not, and the Factor has marked it.", "you refused it", 1.2f);
		}
	}

	// Something true and damaging he witnessed himself about the player, that
	// he has not already carried.
	private static string HeHasOnYou(Npc him, Player.PlayerController p, Contest c)
	{
		for (int i = him.Memories.Count - 1; i >= 0; i--)
		{
			var m = him.Memories[i];
			if (m.Directness != 1 || m.Liar != null) continue;
			if (m.Subject != p.DisplayName && m.Actor != p) continue;
			if (!Lies.IsMisdeed(m.Text) || c.HeSaid.Contains(m.Text)) continue;
			return m.Text;
		}
		return "";
	}

	// Which seat the player is actually after, from what he has declared.
	private static string Seat(Player.PlayerController p)
	{
		foreach (var a in p.Ambitions)
		{
			if (a.Abandoned) continue;
			if (a.Kind == AmbitionKind.FoundAPost) return "the charge of Oxford House";
			if (a.Kind == AmbitionKind.RiseAtThePost) return p.Rank == "clerk" ? "the charge of a post" : "the clerk's desk";
		}
		return "";
	}

	private static Npc Pick(Player.PlayerController p, FortLedger post)
	{
		Npc best = null; float bestScore = 0f;
		foreach (var n in Sim.Npcs)
		{
			if (n.IsDead || n.Faction != p.Faction) continue;
			if (n.Role != Role.Clerk && n.Role != Role.Labourer) continue;
			if (n.Personality.Ambition < 0.45f) continue;
			float s = Score(n, post);
			if (best == null || s > bestScore) { best = n; bestScore = s; }
		}
		return best;
	}

	// The last thing he did that anyone at the post would be talking about.
	private static string Lately(Npc him)
	{
		for (int i = him.Memories.Count - 1; i >= 0; i--)
		{
			var m = him.Memories[i];
			if (m.Directness != 1 || m.Subject != him.DisplayName) continue;
			if (m.Weight < 0.5f) continue;
			return m.Text;
		}
		return him.AssignedTaskName != "" ? $"he was set to {him.AssignedTaskName} and did it" : "he has been making himself useful";
	}

	// ---- what the player can do about him ---------------------------------

	// Something true and damaging the player knows about the rival, in his own
	// journal or witnessed by him. Nothing is invented; if the player has not
	// seen or heard anything, there is nothing to carry.
	public static string DirtOn(Player.PlayerController p, Npc him)
	{
		if (p == null || him == null) return "";
		string[] damaging = { "refused", "on his own account", "was seen at", "drunk", "turned back", "would not", "struck", "took the papers" };
		for (int i = p.Journal.Count - 1; i >= 0; i--)
		{
			string line = p.Journal[i];
			if (!line.Contains(him.DisplayName)) continue;
			foreach (string w in damaging) if (line.Contains(w)) return line;
		}
		return "";
	}

	// Carry it to the Factor. Whether he takes it depends on the ordinary
	// weighing (design doc 3.6, 3.9): your rank, your standing with him, your
	// history - and a man who runs another man down had better be believed.
	public static string SpeakAgainst(Player.PlayerController p, Npc leader)
	{
		var c = Current;
		if (c?.Him == null || leader == null) return "\"I have nothing to say against anyone.\"";
		string dirt = DirtOn(p, c.Him);
		if (dirt == "") return $"\"{c.Him.DisplayName}? I have nothing on him that I saw myself.\"";
		if (c.Said.Contains(dirt)) return "\"You have told me that already.\"";
		c.Said.Add(dirt);

		var v = Negotiation.Weigh(p, leader, 0.55f, $"what {c.Him.DisplayName} did");
		if (v.Granted)
		{
			c.Him.Standing = Mathf.Max(0f, c.Him.Standing - 10f);
			leader.ChangeRapport(c.Him, -8f);
			p.Standing += 2f;
			Chronicle.Event($"{p.DisplayName} told {leader.DisplayName} what {c.Him.DisplayName} had done.", p.GlobalPosition, p, c.Him.DisplayName, 1.2f);
			c.Him.ChangeRapport(p, -25f);   // he will hear of it, and he will not forget
			Reveal.Word(p, $"{c.Him.DisplayName}'s standing has fallen with the Factor.", "you carried it to him yourself", 1.3f);
			return $"He hears you out, and writes it down. ({v.Because()}) It will not stay a secret that it came from you.";
		}
		p.Standing -= 4f;
		leader.ChangeRapport(p, -6f);
		c.Him.ChangeRapport(p, -15f);
		return $"\"I have no use for a man who runs another down.\" ({v.Because()}) He will remember that you came to him with it. (standing -4)";
	}

	// Ask for the seat outright, which is the honest way and weighed the same.
	public static string AskForTheSeat(Player.PlayerController p, Npc leader)
	{
		var c = Current;
		string seat = c?.Seat ?? "the seat";
		if (leader == null) return "";
		float difficulty = c != null && c.HisScore > c.YourScore ? 0.8f : 0.55f;
		var v = Negotiation.Weigh(p, leader, difficulty, seat);
		if (v.Granted)
		{
			p.Standing += 6f;
			Reveal.Word(p, $"The Factor will put your name forward for {seat}.", "you asked him for it, and the books bore you out", 1.5f);
			return $"\"I will put your name forward.\" ({v.Because()})";
		}
		return c?.Him != null
			? $"\"{c.Him.DisplayName} is the better man for it as things stand.\" ({v.Because()})"
			: $"\"Not yet.\" ({v.Because()})";
	}

	// A seat has been filled. If it was the one the player was after, he hears
	// who had it and what carried the day (design doc P.4).
	public static void SeatFilled(Npc who, string seat)
	{
		foreach (var p in Sim.Players())
		{
			if (p == null || p.IsDead) continue;
			if (Seat(p) != seat) continue;
			var post = p.HomePost ?? Sim.Fort;
			bool mine = who == null;
			if (mine) continue;
			string cause = post?.Leader != null && post.Leader.RapportWith(who) > post.Leader.RapportWith(p)
				? $"{who.DisplayName} stood better with the master than you did"
				: $"{who.DisplayName}'s record was the longer one";
			Reveal.Word(p, $"{who.DisplayName} has {seat}, and you wanted it.", cause, 1.8f);
			if (Current?.Him == who) Current = null;
		}
	}

	public static string Describe()
	{
		var c = Current;
		if (c?.Him == null) return "";
		return c.HisScore > c.YourScore
			? $"{c.Him.DisplayName} is ahead of you for {c.Seat}."
			: $"You are ahead of {c.Him.DisplayName} for {c.Seat}.";
	}
}
