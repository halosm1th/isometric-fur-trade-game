using System.Collections.Generic;

namespace CampaignTool;

// What the parser makes of a script before anything is evaluated.

public abstract class Expr { public int Line; public bool Paren; }
public sealed class LitE : Expr { public object Value; public bool IsDate; }          // double, string, bool; or a date (string, IsDate)
public sealed class NameE : Expr { public string Name; }
public sealed class UnE : Expr { public string Op; public Expr Operand; }               // not, -
public sealed class BinE : Expr { public string Op; public Expr Left, Right; }          // and or = <> < > <= >= + - * /
public sealed class CallE : Expr { public string Name; public List<Expr> Args = new(); }

public abstract class Stmt { public int Line; }
public sealed class BlockS : Stmt { public List<Stmt> Body = new(); }
public sealed class IfS : Stmt { public Expr Cond; public Stmt Then, Else; }
public sealed class AssignS : Stmt { public string Var; public Expr Value; }
public sealed class CallS : Stmt { public string Name; public List<Expr> Args = new(); }
public sealed class ForS : Stmt { public string Var; public Expr From, To; public Stmt Body; }
public sealed class AfterS : Stmt { public Expr Days; public Stmt Body; }
public sealed class ChooseS : Stmt { public List<(Expr weight, Stmt body)> Options = new(); }

public sealed class ConstDecl { public string Name; public Expr Value; public int Line; }
public sealed class ProcDecl { public string Name; public List<string> Params = new(); public Stmt Body; public int Line; }

public sealed class BeatDecl
{
	public Expr Id, Date, Title;
	public string Kind = "event";
	public int Line;
	public readonly List<(string name, Expr value)> Numbers = new();   // window, lead, delay, repeat
	public readonly List<Expr> Requires = new(), Keywords = new(), When = new(), DoneWhen = new();
	public Stmt Pressure, Happens;
}

// A beat, or a run of them repeated for each value of a loop variable.
public sealed class TopItem
{
	public BeatDecl Beat;
	public string LoopVar;
	public Expr From, To;
	public List<TopItem> Body;
	public int Line;
}

public sealed class ProgramAst
{
	public string Name = "";
	public readonly List<ConstDecl> Consts = new();
	public readonly Dictionary<string, string> Vars = new(System.StringComparer.OrdinalIgnoreCase);   // name -> type
	public readonly Dictionary<string, ProcDecl> Procs = new(System.StringComparer.OrdinalIgnoreCase);
	public readonly List<object> Order = new();   // ConstDecl | TopItem, in file order (constants may be used after they are declared)
}
