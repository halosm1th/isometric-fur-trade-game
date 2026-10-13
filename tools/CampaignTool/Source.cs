using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CampaignTool;

// Where something is in the source, for error messages and the editor.
public readonly record struct Span(int Line, int Col, int Length)
{
	public static readonly Span None = new(0, 0, 0);
	public Span To(Span end) => new(Line, Col, end.Line == Line ? Math.Max(Length, end.Col + end.Length - Col) : Length);
}

public sealed class Diagnostic
{
	public string Severity = "error";   // error | warning | note
	public string Message = "";
	public Span At;
	public override string ToString() => At.Line > 0 ? $"line {At.Line}:{At.Col}: {Message}" : Message;
}

public sealed class CampError : Exception
{
	public Span At;
	public int Line => At.Line;
	public CampError(string msg, Span at = default) : base(at.Line > 0 ? $"line {at.Line}: {msg}" : msg) { At = at; Msg = msg; }
	public CampError(string msg, int line) : this(msg, new Span(line, 1, 1)) { }
	public string Msg { get; }
}

public enum T { Ident, Int, Float, String, Symbol, Eof }

public sealed class Token
{
	public T Kind;
	public string Text = "";
	public double Number;
	public Span At;
	public bool Is(string s) => (Kind == T.Symbol || Kind == T.Ident) && Text == s;
	public override string ToString() => Kind switch { T.Eof => "the end of the file", T.String => $"\"{Text}\"", _ => $"'{Text}'" };
}

// C-family lexical rules: // and /* */ comments, "strings" with \n \t \" \\ escapes,
// whole numbers and numbers with a point, and the usual operators.
public static class CampLexer
{
	private static readonly string[] Two = { "=>", "==", "!=", "<=", ">=", "&&", "||", "++", "--", "+=", "-=", "..", "*=", "/=" };

	public static List<Token> Lex(string src)
	{
		var toks = new List<Token>();
		int i = 0, line = 1, lineStart = 0;
		Span At(int start, int len) => new(line, start - lineStart + 1, len);
		while (i < src.Length)
		{
			char c = src[i];
			if (c == '\n') { line++; i++; lineStart = i; continue; }
			if (char.IsWhiteSpace(c)) { i++; continue; }
			if (c == '/' && i + 1 < src.Length && src[i + 1] == '/') { while (i < src.Length && src[i] != '\n') i++; continue; }
			if (c == '/' && i + 1 < src.Length && src[i + 1] == '*')
			{
				var start = At(i, 2);
				i += 2;
				while (i + 1 < src.Length && !(src[i] == '*' && src[i + 1] == '/')) { if (src[i] == '\n') { line++; lineStart = i + 1; } i++; }
				if (i + 1 >= src.Length) throw new CampError("this /* comment is never closed", start);
				i += 2; continue;
			}
			if (c == '"')
			{
				int start = i;
				var sb = new StringBuilder();
				i++;
				while (true)
				{
					if (i >= src.Length || src[i] == '\n') throw new CampError("this string is not closed on its line (join long ones with +)", At(start, Math.Min(i, src.Length) - start));
					char d = src[i];
					if (d == '"') { i++; break; }
					if (d == '\\' && i + 1 < src.Length)
					{
						char e = src[i + 1];
						sb.Append(e switch { 'n' => '\n', 't' => '\t', '"' => '"', '\\' => '\\', '\'' => '\'', _ => throw new CampError($"unknown escape \\{e} (use \\n \\t \\\" \\\\)", At(i, 2)) });
						i += 2; continue;
					}
					sb.Append(d); i++;
				}
				toks.Add(new Token { Kind = T.String, Text = sb.ToString(), At = At(start, i - start) });
				continue;
			}
			if (char.IsDigit(c))
			{
				int start = i;
				while (i < src.Length && char.IsDigit(src[i])) i++;
				bool isFloat = false;
				if (i + 1 < src.Length && src[i] == '.' && char.IsDigit(src[i + 1])) { isFloat = true; i++; while (i < src.Length && char.IsDigit(src[i])) i++; }
				string text = src[start..i];
				toks.Add(new Token { Kind = isFloat ? T.Float : T.Int, Text = text, Number = double.Parse(text, System.Globalization.CultureInfo.InvariantCulture), At = At(start, i - start) });
				continue;
			}
			if (char.IsLetter(c) || c == '_')
			{
				int start = i;
				while (i < src.Length && (char.IsLetterOrDigit(src[i]) || src[i] == '_')) i++;
				toks.Add(new Token { Kind = T.Ident, Text = src[start..i], At = At(start, i - start) });
				continue;
			}
			string pair = i + 1 < src.Length ? src.Substring(i, 2) : "";
			if (Two.Contains(pair)) { toks.Add(new Token { Kind = T.Symbol, Text = pair, At = At(i, 2) }); i += 2; continue; }
			if ("+-*/%=<>!?:.,;()[]{}".IndexOf(c) >= 0) { toks.Add(new Token { Kind = T.Symbol, Text = c.ToString(), At = At(i, 1) }); i++; continue; }
			throw new CampError($"unexpected character '{c}'", At(i, 1));
		}
		toks.Add(new Token { Kind = T.Eof, At = At(i, 0) });
		return toks;
	}
}
