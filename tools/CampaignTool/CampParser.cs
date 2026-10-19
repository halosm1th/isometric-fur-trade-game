using System;
using System.Collections.Generic;
using System.Linq;

namespace CampaignTool;

// The grammar (see docs/wiki/Language-Guide.md):
//
//   file     = campaign { decl }
//   campaign = "campaign" Ident "(" [args] ")" ";"
//   decl     = const | var | func | beat | loop
//   const    = "const" type Ident "=" expr ";"
//   var      = type Ident [ "=" expr ] ";"
//   func     = ( "void" | type ) Ident "(" [params] ")" ( block | "=>" expr ";" )
//   beat     = "beat" Ident "(" [args] ")" [ "[" exprs "]" ] ( ";" | "{" { member } "}" )
//   line     = "questline" Ident "(" [args] ")" "{" { quest } "}"
//   quest    = "quest" Ident "(" [args] ")" [ "[" exprs "]" ] "{" { questhook | step } "}"
//   step     = StepKind Ident "(" [args] ")" ( ";" | "=>" expr ";" | "{" { stephook | choice } "}" )
//   choice   = "choice" Ident "(" [args] ")" ( ";" | block )
//   member   = hook | const | var | func
//   hook     = HookName "(" ")" ( block | "=>" expr ";" )
//   loop     = ( for | foreach ) ( decl | "{" { decl } "}" )
//   stmt     = block | if | for | foreach | local | assignment | call ";"
//   expr     = ternary ; || && == != < > <= >= + - * / % ! - . () [] lambdas
public sealed class CampParser
{
	public static readonly string[] Hooks =
	{
		"AwakenWhen", "OnAwaken", "OnApproach", "ActivateWhen", "OnActivate", "ResolveWhen", "OnResolve", "CancelWhen", "OnCancel",
	};
	public static readonly string[] ValueTypes = { "int", "float", "bool", "string" };
	public static readonly string[] QuestHooks = { "StartWhen", "FailWhen", "OnStart", "OnComplete", "OnFail" };
	public static readonly string[] StepHooks = { "Says", "Done", "OnDone" };
	public static readonly string[] StepKinds = { "talk", "reach", "task", "bring", "have", "hunt", "when", "wait" };

	private readonly List<Token> _t;
	private int _p;

	public CampParser(List<Token> toks) { _t = toks; }

	public static ScriptAst Parse(string src) => new CampParser(CampLexer.Lex(src)).ParseScript();

	private Token Cur => _t[_p];
	private Token PeekTok(int n = 1) => _t[Math.Min(_p + n, _t.Count - 1)];
	private bool At(string s) => Cur.Is(s);
	private bool AtIdent => Cur.Kind == T.Ident;
	private Token Next() => _t[_p++];

	private bool Accept(string s)
	{
		if (!At(s)) return false;
		_p++;
		return true;
	}

	private CampError Err(string msg, Token at = null) => new(msg, (at ?? Cur).At);

	private Token Expect(string s, string what = null)
	{
		if (!At(s)) throw Err($"expected {what ?? $"'{s}'"} but found {Cur}");
		return Next();
	}

	private string Ident(string what)
	{
		if (!AtIdent) throw Err($"expected {what} but found {Cur}");
		return Next().Text;
	}

	private static readonly HashSet<string> Reserved = new() { "campaign", "beat", "questline", "quest", "choice", "const", "void", "if", "else", "for", "foreach", "in", "true", "false", "return", "while" };

	// ---------------------------------------------------------------- file

	public ScriptAst ParseScript()
	{
		var s = new ScriptAst();
		if (!At("campaign")) throw Err("a script begins with its header: campaign Name(title: \"...\");");
		s.Campaign = new CampaignDecl { At = Next().At };
		s.Campaign.Name = Ident("a name for the campaign");
		Expect("(");
		s.Campaign.Args = ParseArgs(")");
		Expect(";", "';' after the campaign header");
		while (Cur.Kind != T.Eof) s.Decls.Add(ParseDecl(topLevel: true));
		return s;
	}

	// ---------------------------------------------------------------- declarations

	private bool AtType() => AtIdent && !Reserved.Contains(Cur.Text) && (ValueTypes.Contains(Cur.Text) || Cur.Text == "var" || char.IsUpper(Cur.Text[0]));

	private Decl ParseDecl(bool topLevel)
	{
		var at = Cur.At;
		if (At("beat")) return ParseBeat();
		if (At("questline")) return ParseQuestLine();
		if (At("quest")) throw Err("a quest belongs inside a questline: questline Name(title: \"...\") { quest ... }");
		if (At("for") || At("foreach")) return ParseLoopDecl();
		if (At("campaign")) throw Err("a script has one campaign header, at the top");
		if (At("const"))
		{
			Next();
			string type = ParseTypeName();
			var c = new ConstDecl { At = at, Type = type, Name = Ident("a name") };
			Expect("=", "'=' (a const needs a value)");
			c.Value = ParseExpr();
			Expect(";");
			return c;
		}
		if (At("void") || (AtType() && PeekTok().Kind == T.Ident))
		{
			string type = At("void") ? Next().Text : ParseTypeName();
			var nameTok = Cur;
			string name = Ident("a name");
			if (At("(")) return ParseFunc(at, type, name);
			if (type == "void") throw Err("'void' is for functions", nameTok);
			var v = new VarDecl { At = at, Type = type, Name = name };
			if (Accept("=")) v.Value = ParseExpr();
			Expect(";");
			return v;
		}
		throw Err(topLevel
			? $"expected a beat, a function, a variable or a loop here but found {Cur}"
			: $"expected a declaration here but found {Cur}");
	}

	private string ParseTypeName()
	{
		if (!AtIdent) throw Err($"expected a type but found {Cur}");
		string t = Next().Text;
		if (At("[") && PeekTok().Is("]")) { Next(); Next(); t += "[]"; }
		return t;
	}

	private FuncDecl ParseFunc(Span at, string retType, string name)
	{
		var f = new FuncDecl { At = at, ReturnType = retType, Name = name };
		Expect("(");
		if (!At(")"))
			do
			{
				var pa = Cur.At;
				string t = ParseTypeName();
				f.Params.Add(new Param { At = pa, Type = t, Name = Ident("a parameter name") });
			} while (Accept(","));
		Expect(")");
		if (Accept("=>"))
		{
			f.ExprBody = ParseExpr();
			Expect(";");
		}
		else
		{
			if (!At("{")) throw Err($"expected '{{' or '=>' after {name}(...) but found {Cur}");
			f.Block = ParseBlock();
		}
		return f;
	}

	private BeatDecl ParseBeat()
	{
		var b = new BeatDecl { At = Next().At };
		b.NameAt = Cur.At;
		b.Name = Ident("a name for the beat");
		Expect("(", $"'(' (a beat's data goes in its header: beat {b.Name}(date: ..., title: ...))");
		b.Args = ParseArgs(")");
		if (Accept("["))
		{
			if (!At("]")) { b.Requires.Add(ParseExpr()); while (Accept(",")) b.Requires.Add(ParseExpr()); }
			Expect("]");
		}
		if (Accept(";")) return b;
		Expect("{", "'{' to begin the beat's members, or ';' if it has none");
		while (!At("}"))
		{
			if (Cur.Kind == T.Eof) throw Err($"beat {b.Name} is never closed with '}}'", new Token { At = b.At });
			b.Members.Add(ParseMember());
		}
		Expect("}");
		return b;
	}

	// ---------------------------------------------------------------- quests

	private QuestLineDecl ParseQuestLine()
	{
		var l = new QuestLineDecl { At = Next().At, NameAt = Cur.At };
		l.Name = Ident("a name for the quest line");
		Expect("(", $"'(' (questline {l.Name}(title: \"...\"))");
		l.Args = ParseArgs(")");
		Expect("{", "'{' to begin the line's quests");
		while (!At("}"))
		{
			if (Cur.Kind == T.Eof) throw Err($"questline {l.Name} is never closed with '}}'", new Token { At = l.At });
			if (!At("quest")) throw Err($"a questline holds quests (quest Name(title: \"...\") {{ ... }}), not {Cur}");
			l.Quests.Add(ParseQuest());
		}
		Expect("}");
		return l;
	}

	private QuestDecl ParseQuest()
	{
		var q = new QuestDecl { At = Next().At, NameAt = Cur.At };
		q.Name = Ident("a name for the quest");
		Expect("(", $"'(' (quest {q.Name}(title: \"...\"))");
		q.Args = ParseArgs(")");
		if (Accept("["))
		{
			if (!At("]")) { q.Requires.Add(ParseExpr()); while (Accept(",")) q.Requires.Add(ParseExpr()); }
			Expect("]");
		}
		Expect("{", "'{' to begin the quest's steps");
		while (!At("}"))
		{
			if (Cur.Kind == T.Eof) throw Err($"quest {q.Name} is never closed with '}}'", new Token { At = q.At });
			if (AtIdent && QuestHooks.Contains(Cur.Text) && PeekTok().Is("(")) { q.Hooks.Add(ParseHook()); continue; }
			if (AtIdent && StepKinds.Contains(Cur.Text) && PeekTok().Kind == T.Ident) { q.Steps.Add(ParseStep()); continue; }
			throw Err($"a quest holds steps ({string.Join(", ", StepKinds)}) and hooks ({string.Join(", ", QuestHooks)}), not {Cur}");
		}
		Expect("}");
		return q;
	}

	private StepDecl ParseStep()
	{
		var st = new StepDecl { At = Cur.At, Kind = Next().Text, NameAt = Cur.At };
		st.Name = Ident("a name for the step");
		Expect("(", $"'(' ({st.Kind} {st.Name}(text: \"...\"))");
		st.Args = ParseArgs(")");
		if (Accept(";")) return st;
		if (Accept("=>")) { st.Done = ParseExpr(); Expect(";"); return st; }
		Expect("{", "'{', '=>' or ';' after the step's header");
		while (!At("}"))
		{
			if (Cur.Kind == T.Eof) throw Err($"step {st.Name} is never closed with '}}'", new Token { At = st.At });
			if (At("choice")) { st.Choices.Add(ParseChoice()); continue; }
			if (AtIdent && StepHooks.Contains(Cur.Text) && PeekTok().Is("(")) { st.Hooks.Add(ParseHook()); continue; }
			throw Err($"a step holds {string.Join(", ", StepHooks)} and choices, not {Cur}");
		}
		Expect("}");
		return st;
	}

	private ChoiceDecl ParseChoice()
	{
		var c = new ChoiceDecl { At = Next().At, NameAt = Cur.At };
		c.Name = Ident("a name for the choice");
		Expect("(", $"'(' (choice {c.Name}(label: \"...\"))");
		c.Args = ParseArgs(")");
		if (Accept(";")) return c;
		c.Block = ParseBlock();
		return c;
	}

	private HookDecl ParseHook()
	{
		var h = new HookDecl { At = Cur.At, Name = Next().Text };
		Expect("(");
		Expect(")", $"')' ({h.Name} takes no arguments)");
		if (Accept("=>")) { h.ExprBody = ParseExpr(); Expect(";"); }
		else
		{
			if (!At("{")) throw Err($"expected '{{' or '=>' after {h.Name}() but found {Cur}");
			h.Block = ParseBlock();
		}
		return h;
	}

	private Decl ParseMember()
	{
		var at = Cur.At;
		if (AtIdent && Hooks.Contains(Cur.Text) && PeekTok().Is("("))
		{
			var h = new HookDecl { At = at, Name = Next().Text };
			Expect("(");
			Expect(")", $"')' ({h.Name} takes no arguments)");
			if (Accept("=>")) { h.ExprBody = ParseExpr(); Expect(";"); }
			else
			{
				if (!At("{")) throw Err($"expected '{{' or '=>' after {h.Name}() but found {Cur}");
				h.Block = ParseBlock();
			}
			return h;
		}
		if (AtIdent && !Reserved.Contains(Cur.Text) && PeekTok().Is("(") && !ValueTypes.Contains(Cur.Text))
		{
			var near = Hooks.OrderBy(x => Distance(x.ToLowerInvariant(), Cur.Text.ToLowerInvariant())).First();
			throw Err($"'{Cur.Text}' is not one of a beat's members. The members are {string.Join(", ", Hooks)}; did you mean {near}? (A helper function needs a return type: void or bool, for example.)");
		}
		return ParseDecl(topLevel: false);
	}

	private LoopDecl ParseLoopDecl()
	{
		var loop = new LoopDecl { At = Cur.At };
		if (At("for")) loop.For = ParseForHeader();
		else loop.Foreach = ParseForeachHeader();
		if (Accept("{"))
		{
			while (!At("}"))
			{
				if (Cur.Kind == T.Eof) throw Err("this loop is never closed with '}'", new Token { At = loop.At });
				loop.Body.Add(ParseDecl(true));
			}
			Expect("}");
		}
		else loop.Body.Add(ParseDecl(true));
		return loop;
	}

	// ---------------------------------------------------------------- statements

	private BlockStmt ParseBlock()
	{
		var b = new BlockStmt { At = Expect("{").At };
		while (!At("}"))
		{
			if (Cur.Kind == T.Eof) throw Err("this '{' is never closed with '}'", new Token { At = b.At });
			b.Body.Add(ParseStmt());
		}
		Expect("}");
		return b;
	}

	private ForStmt ParseForHeader()
	{
		var f = new ForStmt { At = Expect("for").At };
		Expect("(");
		if (At("int") || At("var")) Next();
		f.Var = Ident("a loop variable");
		Expect("=", "'=' (for (int i = 1; i <= 3; i++))");
		f.From = ParseExpr();
		Expect(";");
		string v2 = Ident("the loop variable");
		if (v2 != f.Var) throw Err($"the loop condition should test {f.Var}, not {v2}");
		if (Cur.Kind != T.Symbol || !(Cur.Text is "<" or "<=" or ">" or ">=")) throw Err($"expected <, <=, > or >= but found {Cur}");
		f.LimitOp = Next().Text;
		f.Limit = ParseExpr();
		Expect(";");
		string v3 = Ident("the loop variable");
		if (v3 != f.Var) throw Err($"the loop step should change {f.Var}, not {v3}");
		if (Accept("++")) { }
		else if (Accept("--")) f.Down = true;
		else if (Accept("+=")) f.Step = ParseExpr();
		else if (Accept("-=")) { f.Step = ParseExpr(); f.Down = true; }
		else throw Err($"expected ++, --, += or -= but found {Cur}");
		Expect(")");
		return f;
	}

	private ForeachStmt ParseForeachHeader()
	{
		var f = new ForeachStmt { At = Expect("foreach").At };
		Expect("(");
		if (!(AtIdent && PeekTok().Kind == T.Ident)) throw Err("expected a type and a name (foreach (int y in 1818..1820))");
		Next();
		f.Var = Ident("a name");
		Expect("in");
		var from = ParseExpr();
		f.Source = Accept("..") ? new RangeExpr { At = from.At, From = from, To = ParseExpr() } : from;
		Expect(")");
		return f;
	}

	private Stmt ParseStmt()
	{
		var at = Cur.At;
		if (At("{")) return ParseBlock();
		if (Accept(";")) return new EmptyStmt { At = at };
		if (At("if"))
		{
			Next();
			Expect("(");
			var s = new IfStmt { At = at, Cond = ParseExpr() };
			Expect(")");
			s.Then = ParseStmt();
			if (Accept("else")) s.Else = ParseStmt();
			return s;
		}
		if (At("for")) { var f = ParseForHeader(); f.Body = ParseStmt(); return f; }
		if (At("foreach")) { var f = ParseForeachHeader(); f.Body = ParseStmt(); return f; }
		if (At("while") || At("return")) throw Err($"'{Cur.Text}' is not part of the language: the game runs a script's effects once, so there is nothing to loop or return from (a loop with fixed bounds is for or foreach)");
		if (At("const") || At("var") || (AtType() && PeekTok().Kind == T.Ident && PeekTok(2).Is("=")))
		{
			if (At("const")) Next();
			string type = ParseTypeName();
			var l = new LocalStmt { At = at, Type = type, Name = Ident("a name") };
			Expect("=", "'=' (a local must have a value: int n = 3;)");
			l.Value = ParseExpr();
			Expect(";");
			return l;
		}
		var e = ParseExpr();
		if (Cur.Kind == T.Symbol && Cur.Text is "=" or "+=" or "-=" or "*=" or "/=")
		{
			string op = Next().Text;
			var a = new AssignStmt { At = at, Target = e, Op = op, Value = ParseExpr() };
			Expect(";");
			return a;
		}
		if (Cur.Kind == T.Symbol && Cur.Text is "++" or "--")
		{
			var a = new AssignStmt { At = at, Target = e, Op = Next().Text };
			Expect(";");
			return a;
		}
		Expect(";", "';' at the end of the statement");
		return new ExprStmt { At = at, Value = e };
	}

	// ---------------------------------------------------------------- expressions

	public List<Arg> ParseArgs(string close)
	{
		var args = new List<Arg>();
		if (Accept(close)) return args;
		do
		{
			if (At(close)) break;   // a trailing comma
			var a = new Arg();
			if (AtIdent && PeekTok().Is(":")) { a.NameAt = Cur.At; a.Name = Next().Text; Next(); }
			a.Value = ParseExpr();
			args.Add(a);
		} while (Accept(","));
		Expect(close, $"'{close}' to close the argument list");
		return args;
	}

	public Expr ParseExpr() => ParseTernary();

	private Expr ParseTernary()
	{
		var c = ParseOr();
		if (!At("?")) return c;
		var at = Next().At;
		var t = ParseExpr();
		Expect(":");
		return new TernaryExpr { At = at, Cond = c, Then = t, Else = ParseExpr() };
	}

	private Expr Binary(Func<Expr> next, params string[] ops)
	{
		var l = next();
		while (Cur.Kind == T.Symbol && ops.Contains(Cur.Text))
		{
			var op = Next();
			l = new BinaryExpr { At = op.At, Op = op.Text, Left = l, Right = next() };
		}
		return l;
	}

	private Expr ParseOr() => Binary(ParseAnd, "||");
	private Expr ParseAnd() => Binary(ParseEq, "&&");
	private Expr ParseEq() => Binary(ParseRel, "==", "!=");
	private Expr ParseRel() => Binary(ParseAdd, "<", ">", "<=", ">=");
	private Expr ParseAdd() => Binary(ParseMul, "+", "-");
	private Expr ParseMul() => Binary(ParseUnary, "*", "/", "%");

	private Expr ParseUnary()
	{
		if (Cur.Kind == T.Symbol && (Cur.Text == "!" || Cur.Text == "-"))
		{
			var op = Next();
			return new UnaryExpr { At = op.At, Op = op.Text, Operand = ParseUnary() };
		}
		return ParsePostfix();
	}

	private Expr ParsePostfix()
	{
		var e = ParsePrimary();
		while (true)
		{
			if (At("."))
			{
				Next();
				var nameTok = Cur;
				e = new MemberExpr { At = e.At, Target = e, Name = Ident("a member name after '.'"), NameAt = nameTok.At };
			}
			else if (At("("))
			{
				var at = Next().At;
				e = new CallExpr { At = e.At, Callee = e, Args = ParseArgs(")") };
			}
			else if (At("["))
			{
				Next();
				var idx = ParseExpr();
				Expect("]");
				e = new IndexExpr { At = e.At, Target = e, Index = idx };
			}
			else return e;
		}
	}

	private Expr ParsePrimary()
	{
		var t = Cur;
		switch (t.Kind)
		{
			case T.Int: Next(); return new LitExpr { At = t.At, Value = (long)t.Number };
			case T.Float: Next(); return new LitExpr { At = t.At, Value = t.Number };
			case T.String: Next(); return new LitExpr { At = t.At, Value = t.Text };
			case T.Ident:
				if (t.Text == "true" || t.Text == "false") { Next(); return new LitExpr { At = t.At, Value = t.Text == "true" }; }
				if (Reserved.Contains(t.Text)) throw Err($"'{t.Text}' cannot be used as a value here");
				Next();
				return new NameExpr { At = t.At, Name = t.Text };
			case T.Symbol when t.Text == "(":
				if (PeekTok().Is(")") && PeekTok(2).Is("=>"))
				{
					Next(); Next(); Next();
					var lam = new LambdaExpr { At = t.At };
					if (At("{")) lam.Block = ParseBlock();
					else
					{
						var body = ParseExpr();
						// () => mood++  and  () => mood += 2  are one-statement blocks
						if (Cur.Kind == T.Symbol && Cur.Text is "=" or "+=" or "-=" or "++" or "--")
						{
							var a = new AssignStmt { At = body.At, Target = body, Op = Next().Text };
							if (a.Op.Length == 1 || a.Op[1] == '=') a.Value = ParseExpr();
							lam.Block = new BlockStmt { At = body.At, Body = { a } };
						}
						else lam.Body = body;
					}
					return lam;
				}
				{
					Next();
					var e = ParseExpr();
					Expect(")");
					e.Paren = true;
					return e;
				}
			case T.Symbol when t.Text == "[":
				{
					Next();
					var a = new ArrayExpr { At = t.At };
					if (!At("]")) { a.Items.Add(ParseExpr()); while (Accept(",")) { if (At("]")) break; a.Items.Add(ParseExpr()); } }
					Expect("]");
					return a;
				}
		}
		throw Err($"expected a value but found {t}");
	}

	// ---------------------------------------------------------------- helpers

	public static int Distance(string a, string b)
	{
		var d = new int[a.Length + 1, b.Length + 1];
		for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
		for (int j = 0; j <= b.Length; j++) d[0, j] = j;
		for (int i = 1; i <= a.Length; i++)
			for (int j = 1; j <= b.Length; j++)
				d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
		return d[a.Length, b.Length];
	}
}
