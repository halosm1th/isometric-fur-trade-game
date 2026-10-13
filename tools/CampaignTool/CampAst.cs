using System.Collections.Generic;

namespace CampaignTool;

public abstract class Node { public Span At; }

// ---- expressions
public abstract class Expr : Node { public bool Paren; }   // Paren: written in brackets, so a grouping that is kept
public sealed class LitExpr : Expr { public object Value; }                       // long, double, bool, string
public sealed class NameExpr : Expr { public string Name; }
public sealed class MemberExpr : Expr { public Expr Target; public string Name; public Span NameAt; }
public sealed class CallExpr : Expr { public Expr Callee; public List<Arg> Args = new(); }
public sealed class IndexExpr : Expr { public Expr Target, Index; }
public sealed class UnaryExpr : Expr { public string Op; public Expr Operand; }
public sealed class BinaryExpr : Expr { public string Op; public Expr Left, Right; }
public sealed class TernaryExpr : Expr { public Expr Cond, Then, Else; }
public sealed class ArrayExpr : Expr { public List<Expr> Items = new(); }
public sealed class RangeExpr : Expr { public Expr From, To; }
public sealed class LambdaExpr : Expr { public Expr Body; public BlockStmt Block; }
public sealed class Arg { public string Name; public Span NameAt; public Expr Value; }

// ---- statements
public abstract class Stmt : Node { }
public sealed class BlockStmt : Stmt { public List<Stmt> Body = new(); }
public sealed class IfStmt : Stmt { public Expr Cond; public Stmt Then, Else; }
public sealed class ForStmt : Stmt { public string Var; public Expr From, Limit; public string LimitOp; public Stmt Body; public Expr Step; public bool Down; }
public sealed class ForeachStmt : Stmt { public string Var; public Expr Source; public Stmt Body; }
public sealed class LocalStmt : Stmt { public string Type, Name; public Expr Value; }
public sealed class ExprStmt : Stmt { public Expr Value; }
public sealed class AssignStmt : Stmt { public Expr Target; public string Op; public Expr Value; }   // =  +=  -=  ++  --
public sealed class EmptyStmt : Stmt { }

// ---- declarations
public abstract class Decl : Node { }
public sealed class CampaignDecl : Decl { public string Name; public List<Arg> Args = new(); }
public sealed class ConstDecl : Decl { public string Type, Name; public Expr Value; }
public sealed class VarDecl : Decl { public string Type, Name; public Expr Value; }
public sealed class Param { public string Type, Name; public Span At; }
public sealed class FuncDecl : Decl { public string ReturnType, Name; public List<Param> Params = new(); public BlockStmt Block; public Expr ExprBody; }
public sealed class HookDecl : Decl { public string Name; public BlockStmt Block; public Expr ExprBody; }
public sealed class BeatDecl : Decl { public string Name; public Span NameAt; public List<Arg> Args = new(); public List<Expr> Requires = new(); public List<Decl> Members = new(); }
public sealed class LoopDecl : Decl { public ForStmt For; public ForeachStmt Foreach; public List<Decl> Body = new(); }

public sealed class ScriptAst
{
	public CampaignDecl Campaign;
	public List<Decl> Decls = new();
}
