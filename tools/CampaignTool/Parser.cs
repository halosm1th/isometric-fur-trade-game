using System;
using System.Collections.Generic;
using System.Linq;

namespace CampaignTool;

// campaign Name;
//   const / var / procedure sections, and beats, in any order
// end.
//
// Statements and conditions are Pascal's, with two kindnesses: a comparison
// binds tighter than and / or (so no brackets are needed round each side),
// and a string may be built from pieces with + (Pascal has no multi-line
// strings). Keywords and the built-in names are not case sensitive; your own
// names and strings are.
public sealed class Parser
{
	private readonly List<Tok> _t;
	private int _p;

	public Parser(List<Tok> toks) { _t = toks; }

	public static ProgramAst ParseText(string src) => new Parser(Lexer.Lex(src)).ParseProgram();

	private Tok Cur => _t[_p];
	private Tok Peek(int n = 1) => _t[Math.Min(_p + n, _t.Count - 1)];
	private bool At(string s) => Cur.Is(s);
	private bool AtKind(TokKind k) => Cur.Kind == k;

	private Tok Next() => _t[_p++];

	private bool Accept(string s)
	{
		if (!At(s)) return false;
		_p++;
		return true;
	}

	private Tok Expect(string s, string what = null)
	{
		if (!At(s)) throw new CampError($"expected {what ?? $"'{s}'"} but found {Cur}", Cur.Line);
		return Next();
	}

	private string Ident(string what)
	{
		if (!AtKind(TokKind.Ident)) throw new CampError($"expected {what} but found {Cur}", Cur.Line);
		return Next().Text;
	}

	private static readonly HashSet<string> Sections = new(StringComparer.OrdinalIgnoreCase) { "const", "var", "procedure", "beat", "for", "end" };

	// ---------------------------------------------------------------- program

	public ProgramAst ParseProgram()
	{
		var prog = new ProgramAst();
		Expect("campaign", "'campaign Name;' to begin the script");
		prog.Name = Ident("a name for the campaign");
		Expect(";");
		while (!AtKind(TokKind.Eof))
		{
			if (At("end")) { Next(); Expect(".", "'end.' to finish the script"); break; }
			if (At("const")) { Next(); ParseConsts(prog); }
			else if (At("var")) { Next(); ParseVars(prog); }
			else if (At("procedure")) ParseProc(prog);
			else if (At("beat") || At("for")) prog.Order.Add(ParseTop());
			else throw new CampError($"expected const, var, procedure, beat, for or 'end.' but found {Cur}", Cur.Line);
		}
		if (!AtKind(TokKind.Eof)) throw new CampError($"nothing may follow 'end.' (found {Cur})", Cur.Line);
		return prog;
	}

	private void ParseConsts(ProgramAst prog)
	{
		while (AtKind(TokKind.Ident) && !Sections.Contains(Cur.Text))
		{
			var c = new ConstDecl { Line = Cur.Line, Name = Next().Text };
			Expect("=");
			c.Value = ParseExpr();
			Expect(";");
			prog.Consts.Add(c);
			prog.Order.Add(c);
		}
	}

	private void ParseVars(ProgramAst prog)
	{
		while (AtKind(TokKind.Ident) && !Sections.Contains(Cur.Text))
		{
			var names = new List<string> { Ident("a variable name") };
			while (Accept(",")) names.Add(Ident("a variable name"));
			Expect(":");
			string type = Ident("a type (integer or real)");
			Expect(";");
			foreach (var n in names)
			{
				if (prog.Vars.ContainsKey(n)) throw new CampError($"variable {n} is declared twice", _t[_p - 1].Line);
				prog.Vars[n] = type.ToLowerInvariant();
			}
		}
	}

	private void ParseProc(ProgramAst prog)
	{
		int line = Next().Line;
		var p = new ProcDecl { Line = line, Name = Ident("a procedure name") };
		if (Accept("("))
		{
			do
			{
				var names = new List<string> { Ident("a parameter name") };
				while (Accept(",")) names.Add(Ident("a parameter name"));
				if (Accept(":")) Ident("a type");
				p.Params.AddRange(names);
			} while (Accept(";"));
			Expect(")");
		}
		Expect(";");
		p.Body = ParseCompound();
		Expect(";");
		if (prog.Procs.ContainsKey(p.Name)) throw new CampError($"procedure {p.Name} is declared twice", line);
		prog.Procs[p.Name] = p;
	}

	// ---------------------------------------------------------------- beats

	private TopItem ParseTop()
	{
		int line = Cur.Line;
		if (Accept("for"))
		{
			var item = new TopItem { Line = line, LoopVar = Ident("a loop variable") };
			Expect(":=");
			item.From = ParseExpr();
			Expect("to");
			item.To = ParseExpr();
			Expect("do");
			item.Body = new List<TopItem>();
			if (Accept("begin"))
			{
				while (!At("end")) item.Body.Add(ParseTop());
				Expect("end");
				Expect(";");
			}
			else item.Body.Add(ParseTop());
			return item;
		}
		return new TopItem { Line = line, Beat = ParseBeat() };
	}

	private BeatDecl ParseBeat()
	{
		var b = new BeatDecl { Line = Expect("beat").Line };
		b.Id = ParseExpr();
		Expect(":");
		b.Kind = Ident("a kind (fixed, conditional, event or pressure)").ToLowerInvariant();
		if (!Lang.BeatKinds.Contains(b.Kind)) throw new CampError($"kind must be one of {string.Join(", ", Lang.BeatKinds)} (got '{b.Kind}')", _t[_p - 1].Line);
		if (Accept("on")) b.Date = ParseExpr();
		if (!At(";")) b.Title = ParseExpr();
		Expect(";");
		while (!At("end"))
		{
			if (AtKind(TokKind.Eof)) throw new CampError($"beat {Describe(b.Id)} is never closed with 'end;'", b.Line);
			int line = Cur.Line;
			string word = Cur.Text.ToLowerInvariant();
			if (!AtKind(TokKind.Ident)) throw new CampError($"expected a beat clause (window, lead, delay, repeat, requires, keywords, when, done when, pressure, happens) but found {Cur}", line);
			Next();
			switch (word)
			{
				case "window": case "lead": case "delay": case "repeat":
					b.Numbers.Add((word, ParseExpr())); Expect(";"); break;
				case "requires": b.Requires.AddRange(ParseList()); Expect(";"); break;
				case "keywords": b.Keywords.AddRange(ParseList()); Expect(";"); break;
				case "when": b.When.Add(ParseExpr()); Expect(";"); break;
				case "done": Expect("when"); b.DoneWhen.Add(ParseExpr()); Expect(";"); break;
				case "pressure": b.Pressure = Merge(b.Pressure, ParseStmt()); Expect(";"); break;
				case "happens": b.Happens = Merge(b.Happens, ParseStmt()); Expect(";"); break;
				default: throw new CampError($"unknown beat clause '{Cur.Text}' (window, lead, delay, repeat, requires, keywords, when, done when, pressure, happens)", line);
			}
		}
		Expect("end");
		Expect(";");
		return b;
	}

	private static string Describe(Expr e) => e is LitE { Value: string s } ? $"'{s}'" : "(unnamed)";

	private static Stmt Merge(Stmt a, Stmt b)
	{
		if (a == null) return b;
		var block = new BlockS { Line = a.Line };
		block.Body.Add(a); block.Body.Add(b);
		return block;
	}

	private List<Expr> ParseList()
	{
		var l = new List<Expr> { ParseExpr() };
		while (Accept(",")) l.Add(ParseExpr());
		return l;
	}

	// ---------------------------------------------------------------- statements

	private BlockS ParseCompound()
	{
		var b = new BlockS { Line = Expect("begin").Line };
		while (!At("end"))
		{
			if (AtKind(TokKind.Eof)) throw new CampError("a 'begin' is never closed with 'end'", b.Line);
			if (Accept(";")) continue;
			b.Body.Add(ParseStmt());
			if (!At("end")) Expect(";", "';' between statements");
		}
		Expect("end");
		return b;
	}

	private Stmt ParseStmt()
	{
		int line = Cur.Line;
		if (At("begin")) return ParseCompound();
		if (Accept("if"))
		{
			var s = new IfS { Line = line, Cond = ParseExpr() };
			Expect("then");
			s.Then = ParseStmt();
			if (Accept("else")) s.Else = ParseStmt();
			return s;
		}
		if (Accept("for"))
		{
			var s = new ForS { Line = line, Var = Ident("a loop variable") };
			Expect(":=");
			s.From = ParseExpr();
			Expect("to");
			s.To = ParseExpr();
			Expect("do");
			s.Body = ParseStmt();
			return s;
		}
		if (At("after") && Peek().Kind != TokKind.Symbol)
		{
			Next();
			var s = new AfterS { Line = line, Days = ParseExpr() };
			Expect("days", "'days' (after 10 days do ...)");
			Expect("do");
			s.Body = ParseStmt();
			return s;
		}
		if (At("case"))
		{
			Next();
			Expect("random", "'random' (case random of 30: ...; 70: ...; end)");
			Expect("of");
			var s = new ChooseS { Line = line };
			while (!At("end"))
			{
				if (AtKind(TokKind.Eof)) throw new CampError("a 'case' is never closed with 'end'", line);
				if (Accept(";")) continue;
				var w = ParseExpr();
				Expect(":", "':' after the weight");
				s.Options.Add((w, ParseStmt()));
				if (!At("end")) Expect(";");
			}
			Expect("end");
			return s;
		}
		string name = Ident("a statement");
		if (Accept(":="))
			return new AssignS { Line = line, Var = name, Value = ParseExpr() };
		var call = new CallS { Line = line, Name = name };
		if (Accept("("))
		{
			if (!At(")")) { call.Args.Add(ParseExpr()); while (Accept(",")) call.Args.Add(ParseExpr()); }
			Expect(")");
		}
		return call;
	}

	// ---------------------------------------------------------------- expressions
	// or  <  and  <  not  <  comparison  <  + -  <  * /  <  unary -

	public Expr ParseExpr() => ParseOr();

	private Expr ParseOr()
	{
		var l = ParseAnd();
		while (At("or")) { int line = Next().Line; l = new BinE { Line = line, Op = "or", Left = l, Right = ParseAnd() }; }
		return l;
	}

	private Expr ParseAnd()
	{
		var l = ParseNot();
		while (At("and")) { int line = Next().Line; l = new BinE { Line = line, Op = "and", Left = l, Right = ParseNot() }; }
		return l;
	}

	private Expr ParseNot()
	{
		if (At("not")) { int line = Next().Line; return new UnE { Line = line, Op = "not", Operand = ParseNot() }; }
		return ParseRelation();
	}

	private static readonly string[] Relations = { "=", "<>", "<=", ">=", "<", ">" };

	private Expr ParseRelation()
	{
		var l = ParseSum();
		foreach (var op in Relations)
			if (Cur.Kind == TokKind.Symbol && Cur.Text == op) { int line = Next().Line; return new BinE { Line = line, Op = op, Left = l, Right = ParseSum() }; }
		return l;
	}

	private Expr ParseSum()
	{
		var l = ParseTerm();
		while (Cur.Kind == TokKind.Symbol && (Cur.Text == "+" || Cur.Text == "-"))
		{
			var op = Next();
			l = new BinE { Line = op.Line, Op = op.Text, Left = l, Right = ParseTerm() };
		}
		return l;
	}

	private Expr ParseTerm()
	{
		var l = ParseUnary();
		while (Cur.Kind == TokKind.Symbol && (Cur.Text == "*" || Cur.Text == "/"))
		{
			var op = Next();
			l = new BinE { Line = op.Line, Op = op.Text, Left = l, Right = ParseUnary() };
		}
		return l;
	}

	private Expr ParseUnary()
	{
		if (Cur.Kind == TokKind.Symbol && (Cur.Text == "-" || Cur.Text == "+"))
		{
			var op = Next();
			var e = ParseUnary();
			return op.Text == "-" ? new UnE { Line = op.Line, Op = "-", Operand = e } : e;
		}
		return ParsePrimary();
	}

	private Expr ParsePrimary()
	{
		var t = Cur;
		switch (t.Kind)
		{
			case TokKind.Number: Next(); return new LitE { Line = t.Line, Value = t.Value };
			case TokKind.String: Next(); return new LitE { Line = t.Line, Value = t.Text };
			case TokKind.Date: Next(); return new LitE { Line = t.Line, Value = t.Text, IsDate = true };
			case TokKind.Ident:
				{
					Next();
					if (Accept("("))
					{
						var c = new CallE { Line = t.Line, Name = t.Text };
						if (!At(")")) { c.Args.Add(ParseExpr()); while (Accept(",")) c.Args.Add(ParseExpr()); }
						Expect(")");
						return c;
					}
					return new NameE { Line = t.Line, Name = t.Text };
				}
			case TokKind.Symbol when t.Text == "(":
				{
					Next();
					var e = ParseExpr();
					Expect(")");
					e.Paren = true;
					return e;
				}
		}
		throw new CampError($"expected a value but found {t}", t.Line);
	}
}
