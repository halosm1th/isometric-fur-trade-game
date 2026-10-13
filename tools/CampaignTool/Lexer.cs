using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CampaignTool;

public sealed class CampError : Exception
{
	public int Line;
	public CampError(string msg, int line = 0) : base(line > 0 ? $"line {line}: {msg}" : msg) { Line = line; }
}

public enum TokKind { Ident, Number, String, Date, Symbol, Eof }

public sealed class Tok
{
	public TokKind Kind;
	public string Text;     // identifier or symbol text; the value of a string
	public double Value;    // a number
	public int Line;
	public bool Is(string s) => (Kind == TokKind.Symbol && Text == s) || (Kind == TokKind.Ident && string.Equals(Text, s, StringComparison.OrdinalIgnoreCase));
	public override string ToString() => Kind switch { TokKind.Eof => "the end of the file", TokKind.String => $"'{Text}'", TokKind.Number => Text, _ => $"'{Text}'" };
}

// Pascal's lexical rules: 'strings' with '' for a quote, #10 for a character,
// { braces }, (* stars *) and // line comments, := and <> and the rest.
// Dates (1816-06-19) are a token of their own.
public static class Lexer
{
	public static List<Tok> Lex(string src)
	{
		var toks = new List<Tok>();
		int i = 0, line = 1;
		while (i < src.Length)
		{
			char c = src[i];
			if (c == '\n') { line++; i++; continue; }
			if (char.IsWhiteSpace(c)) { i++; continue; }
			if (c == '{')
			{
				int start = line;
				while (i < src.Length && src[i] != '}') { if (src[i] == '\n') line++; i++; }
				if (i >= src.Length) throw new CampError("a { comment is never closed", start);
				i++; continue;
			}
			if (c == '(' && i + 1 < src.Length && src[i + 1] == '*')
			{
				int start = line;
				i += 2;
				while (i + 1 < src.Length && !(src[i] == '*' && src[i + 1] == ')')) { if (src[i] == '\n') line++; i++; }
				if (i + 1 >= src.Length) throw new CampError("a (* comment is never closed", start);
				i += 2; continue;
			}
			if (c == '/' && i + 1 < src.Length && src[i + 1] == '/') { while (i < src.Length && src[i] != '\n') i++; continue; }
			if (c == '\'' || c == '#')
			{
				// 'text' and #nn pieces written side by side make one string
				var sb = new StringBuilder();
				int start = line;
				while (i < src.Length && (src[i] == '\'' || src[i] == '#'))
				{
					if (src[i] == '#')
					{
						int j = ++i;
						while (j < src.Length && char.IsDigit(src[j])) j++;
						if (j == i) throw new CampError("# needs a character number (#10 is a new line)", line);
						sb.Append((char)int.Parse(src[i..j]));
						i = j;
					}
					else
					{
						i++;
						while (true)
						{
							if (i >= src.Length || src[i] == '\n') throw new CampError("a string is not closed on its line (join long ones with +)", start);
							if (src[i] == '\'')
							{
								if (i + 1 < src.Length && src[i + 1] == '\'') { sb.Append('\''); i += 2; continue; }
								i++; break;
							}
							sb.Append(src[i++]);
						}
					}
				}
				toks.Add(new Tok { Kind = TokKind.String, Text = sb.ToString(), Line = start });
				continue;
			}
			if (char.IsDigit(c))
			{
				int j = i;
				if (i + 10 <= src.Length && IsDate(src, i))
				{
					toks.Add(new Tok { Kind = TokKind.Date, Text = src.Substring(i, 10), Line = line });
					i += 10; continue;
				}
				while (j < src.Length && char.IsDigit(src[j])) j++;
				if (j + 1 < src.Length && src[j] == '.' && char.IsDigit(src[j + 1])) { j++; while (j < src.Length && char.IsDigit(src[j])) j++; }
				toks.Add(new Tok { Kind = TokKind.Number, Text = src[i..j], Value = double.Parse(src[i..j], CultureInfo.InvariantCulture), Line = line });
				i = j; continue;
			}
			if (char.IsLetter(c) || c == '_')
			{
				int j = i;
				while (j < src.Length && (char.IsLetterOrDigit(src[j]) || src[j] == '_')) j++;
				toks.Add(new Tok { Kind = TokKind.Ident, Text = src[i..j], Line = line });
				i = j; continue;
			}
			string two = i + 1 < src.Length ? src.Substring(i, 2) : "";
			if (two is ":=" or "<>" or "<=" or ">=" or ".." ) { toks.Add(new Tok { Kind = TokKind.Symbol, Text = two, Line = line }); i += 2; continue; }
			if ("=<>+-*/(),;:.".IndexOf(c) >= 0) { toks.Add(new Tok { Kind = TokKind.Symbol, Text = c.ToString(), Line = line }); i++; continue; }
			throw new CampError($"unexpected character '{c}'", line);
		}
		toks.Add(new Tok { Kind = TokKind.Eof, Text = "", Line = line });
		return toks;
	}

	private static bool IsDate(string s, int i) =>
		char.IsDigit(s[i]) && char.IsDigit(s[i + 1]) && char.IsDigit(s[i + 2]) && char.IsDigit(s[i + 3]) && s[i + 4] == '-' &&
		char.IsDigit(s[i + 5]) && char.IsDigit(s[i + 6]) && s[i + 7] == '-' && char.IsDigit(s[i + 8]) && char.IsDigit(s[i + 9]);
}
