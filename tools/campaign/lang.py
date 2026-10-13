"""The campaign authoring language: .camp text <-> the JSON the game reads.

    campaign "The Pemmican War"
    description "..."
    posts_by_founding_year

    beat seven-oaks event 1816-06-19 "Seven Oaks"
      window 45
      requires pemmican-proclamation
      when tension >= 40 and not flag(peace)
      happens
        flag governor_killed
        tension +25 "Seven Oaks"
        if convoy_late("the York boats"):
          say "The Company's boats are overdue, and the news is bad." to=HBC
        else:
          after 10 days:
            flag word-came
        news from="Fort Douglas" to=factor days=40 company=HBC
          subject "Blood on the plain"
          text \"\"\"
          A long text, indented under its key.
          \"\"\"

Indentation (two spaces) nests. `#` starts a comment. Names with spaces go in
quotes. Conditions: flag(x) beat(x) post("Fort William") after(1816-06-19)
tension >= 40 stock("York Factory", Rum) < 10 $hostility >= 3 month(6-8)
chance(30) convoy_late("the York boats") ... joined with and / or / not and
brackets. Macros and loops are expanded before anything is read:

    macro dispatch(to, subject, days=40):
      news from="Montreal" to={{to}} subject={{subject}} days={{days}}
    ...
    use dispatch to=factor subject="Peace" days=60
    for y in 1818, 1819, 1820:
      beat interior-war-{{y}} pressure {{y}}-06-01
"""
import json
import re
import textwrap

CMP = (">=", "<=", "==", "!=", ">", "<")

# Names that mean a person in a `news` letter's `to`, shortened.
ADDRESSEES = {
    "factor": "the Factor at York Factory",
    "postmaster": "the postmaster at Oxford House",
    "partner": "the wintering partner",
    "chief": "the chief of the band",
}
ADDRESSEE_NAMES = {v: k for k, v in ADDRESSEES.items()}


class CampError(Exception):
    def __init__(self, msg, line=None):
        super().__init__(f"line {line}: {msg}" if line else msg)
        self.line = line


# ---------------------------------------------------------------- lines

class Line:
    __slots__ = ("indent", "text", "no")

    def __init__(self, indent, text, no):
        self.indent, self.text, self.no = indent, text, no

    def __repr__(self):
        return f"{self.no}:{' ' * self.indent}{self.text}"


def strip_comment(s):
    out, quote, i = [], False, 0
    while i < len(s):
        ch = s[i]
        if ch == "\\" and quote and i + 1 < len(s):
            out.append(s[i:i + 2]); i += 2; continue
        if ch == '"':
            quote = not quote
        elif ch == "#" and not quote:
            break
        out.append(ch); i += 1
    return "".join(out).rstrip()


def logical_lines(text):
    """Physical text -> Lines, with \"\"\"blocks\"\"\" folded into one quoted string."""
    raw = text.replace("\r\n", "\n").split("\n")
    lines, i = [], 0
    while i < len(raw):
        no = i + 1
        line = raw[i]
        i += 1
        if '"""' in line:
            head, _, rest = line.partition('"""')
            body = []
            if '"""' in rest:  # opened and closed on one line
                inner, _, tail = rest.partition('"""')
                line = head + json.dumps(inner) + tail
            else:
                body.append(rest)
                while i < len(raw):
                    nxt = raw[i]; i += 1
                    if '"""' in nxt:
                        before, _, tail = nxt.partition('"""')
                        body.append(before)
                        break
                    body.append(nxt)
                else:
                    raise CampError('unterminated """ string', no)
                if body and body[0].strip() == "":
                    body = body[1:]
                if body and body[-1].strip() == "":
                    body = body[:-1]
                line = head + json.dumps(textwrap.dedent("\n".join(body))) + tail
        stripped = strip_comment(line)
        if not stripped.strip():
            continue
        if "\t" in stripped[:len(stripped) - len(stripped.lstrip())]:
            raise CampError("tabs are not allowed in indentation; use two spaces", no)
        indent = len(stripped) - len(stripped.lstrip(" "))
        lines.append(Line(indent, stripped.strip(), no))
    return lines


# ---------------------------------------------------------------- macros and loops

_MACRO = re.compile(r"^macro\s+([\w\-]+)\s*\((.*)\)\s*:?$")
_USE = re.compile(r"^use\s+([\w\-]+)\s*(.*)$")
_FOR = re.compile(r"^for\s+(\w+)\s+in\s+(.+?)\s*:$")


def _block(lines, i):
    """Lines after lines[i] that are indented deeper than it."""
    base, j = lines[i].indent, i + 1
    while j < len(lines) and lines[j].indent > base:
        j += 1
    return lines[i + 1:j], j


def _subst(text, mapping):
    def sub(m):
        k = m.group(1)
        return mapping[k] if k in mapping else m.group(0)
    return re.sub(r"\{\{\s*([\w\-]+)\s*\}\}", sub, text)


def split_words(s):
    """Split on spaces and commas outside quotes: used for arg lists."""
    out, cur, quote, depth = [], [], False, 0
    i = 0
    while i < len(s):
        ch = s[i]
        if ch == "\\" and quote and i + 1 < len(s):
            cur.append(s[i:i + 2]); i += 2; continue
        if ch == '"':
            quote = not quote
        if not quote and ch in "([":
            depth += 1
        if not quote and ch in ")]":
            depth -= 1
        if not quote and depth == 0 and ch in ", \t":
            if cur:
                out.append("".join(cur)); cur = []
        else:
            cur.append(ch)
        i += 1
    if cur:
        out.append("".join(cur))
    return out


def _kv(words):
    pos, kw = [], {}
    for w in words:
        m = re.match(r"^([A-Za-z_][\w\-]*)=(.*)$", w)
        if m:
            kw[m.group(1)] = m.group(2)
        else:
            pos.append(w)
    return pos, kw


def expand(lines):
    macros = {}
    for _ in range(40):
        out, i, changed = [], 0, False
        while i < len(lines):
            ln = lines[i]
            m = _MACRO.match(ln.text)
            if m:
                body, j = _block(lines, i)
                params = []
                for p in split_words(m.group(2)):
                    name, _, default = p.partition("=")
                    params.append((name.strip(), default.strip() if "=" in p else None))
                macros[m.group(1)] = (params, body, ln.indent)
                i = j; changed = True
                continue
            u = _USE.match(ln.text)
            if u and u.group(1) in macros:
                params, body, base = macros[u.group(1)]
                children, j = _block(lines, i)
                pos, kw = _kv(split_words(u.group(2)))
                mapping = {}
                for n, (pname, default) in enumerate(params):
                    if pname in kw:
                        mapping[pname] = kw[pname]
                    elif n < len(pos):
                        mapping[pname] = pos[n]
                    elif default is not None:
                        mapping[pname] = default
                    else:
                        raise CampError(f"macro {u.group(1)} needs {pname}", ln.no)
                body_base = min((b.indent for b in body), default=0)
                for b in body:
                    pad = ln.indent + b.indent - body_base
                    if b.text.strip() == "{{block}}":
                        cbase = min((c.indent for c in children), default=0)
                        for c in children:
                            out.append(Line(pad + c.indent - cbase, c.text, c.no))
                    else:
                        out.append(Line(pad, _subst(b.text, mapping), ln.no))
                i = j; changed = True
                continue
            f = _FOR.match(ln.text)
            if f:
                body, j = _block(lines, i)
                body_base = min((b.indent for b in body), default=0)
                for v in split_words(f.group(2)):
                    for b in body:
                        out.append(Line(ln.indent + b.indent - body_base, _subst(b.text, {f.group(1): v.strip('"')}), ln.no))
                i = j; changed = True
                continue
            out.append(ln)
            i += 1
        lines = out
        if not changed:
            return lines
    raise CampError("macros expand too deeply (is one calling itself?)")


# ---------------------------------------------------------------- values

def parse_scalar(tok):
    tok = tok.strip()
    if tok.startswith('"'):
        try:
            return json.loads(tok)
        except ValueError:
            raise CampError(f"bad string {tok}")
    if tok in ("true", "false"):
        return tok == "true"
    if re.fullmatch(r"[+-]?\d+", tok):
        return int(tok)
    if re.fullmatch(r"[+-]?\d+\.\d+", tok):
        return float(tok)
    return tok


def num(v):
    return v if isinstance(v, (int, float)) and not isinstance(v, bool) else None


def fmt_scalar(v):
    if isinstance(v, bool):
        return "true" if v else "false"
    if isinstance(v, (int, float)):
        return repr(v) if isinstance(v, float) and v != int(v) else str(int(v))
    s = str(v)
    return s if re.fullmatch(r"[A-Za-z_][\w\-\.]*", s) and s not in ("true", "false") else json.dumps(s)


# ---------------------------------------------------------------- conditions

def parse_condition(text, no=None):
    p = _CondParser(text, no)
    c = p.parse_or()
    p.ws()
    if p.i != len(text):
        raise CampError(f"unexpected '{text[p.i:]}' in condition '{text}'", no)
    return c


_NAME = re.compile(r"[A-Za-z_$][\w\-]*")
_CMP = re.compile(r"\s*(>=|<=|==|!=|>|<)\s*(-?\d+(?:\.\d+)?)")


class _CondParser:
    def __init__(self, s, no):
        self.s, self.i, self.no = s, 0, no

    def ws(self):
        while self.i < len(self.s) and self.s[self.i] in " \t":
            self.i += 1

    def word(self, w):
        self.ws()
        if self.s.startswith(w, self.i) and (self.i + len(w) == len(self.s) or not (self.s[self.i + len(w)].isalnum() or self.s[self.i + len(w)] in "_-")):
            self.i += len(w)
            return True
        return False

    def parse_or(self):
        parts = [self.parse_and()]
        while self.word("or"):
            parts.append(self.parse_and())
        return parts[0] if len(parts) == 1 else {"any": parts}

    def parse_and(self):
        parts = [self.parse_not()]
        while self.word("and"):
            parts.append(self.parse_not())
        return parts[0] if len(parts) == 1 else {"all": parts}

    def parse_not(self):
        self.ws()
        if self.word("not") or (self.i < len(self.s) and self.s[self.i] == "!" and not self.s.startswith("!=", self.i) and self._bang()):
            return {"not": self.parse_not()}
        return self.parse_atom()

    def _bang(self):
        self.i += 1
        return True

    def parse_atom(self):
        self.ws()
        if self.i >= len(self.s):
            raise CampError(f"condition ends too soon: '{self.s}'", self.no)
        if self.s[self.i] == "(":
            self.i += 1
            c = self.parse_or()
            self.ws()
            if self.i >= len(self.s) or self.s[self.i] != ")":
                raise CampError(f"missing ) in '{self.s}'", self.no)
            self.i += 1
            return c
        if self.s[self.i] == '"':  # a raw legacy string, passed through
            m = re.compile(r'"(?:[^"\\]|\\.)*"').match(self.s, self.i)
            if not m:
                raise CampError(f"bad string in '{self.s}'", self.no)
            self.i = m.end()
            return json.loads(m.group(0))
        m = _NAME.match(self.s, self.i)
        if not m:
            raise CampError(f"cannot read a condition at '{self.s[self.i:]}'", self.no)
        name = m.group(0)
        self.i = m.end()
        args = None
        if self.i < len(self.s) and self.s[self.i] == "(":
            args, self.i = self._args(self.i)
        cm = _CMP.match(self.s, self.i)
        if name.startswith("$"):
            args, name = [name[1:]], "var"
        if cm:
            self.i = cm.end()
            v = cm.group(2)
            return metric(name, args or [], cm.group(1), float(v) if "." in v else int(v))
        if name.startswith("$"):
            raise CampError("a variable needs a comparison ($x >= 1)", self.no)
        if args is None:
            return name if name not in ("true", "false") else name == "true"
        return predicate(name, args)

    def _args(self, i):
        depth, quote, j = 0, False, i
        while j < len(self.s):
            ch = self.s[j]
            if ch == "\\" and quote:
                j += 2; continue
            if ch == '"':
                quote = not quote
            elif not quote and ch == "(":
                depth += 1
            elif not quote and ch == ")":
                depth -= 1
                if depth == 0:
                    break
            j += 1
        else:
            raise CampError(f"missing ) in '{self.s}'", self.no)
        inner = self.s[i + 1:j]
        return [a.strip().strip('"') if not a.strip().startswith('"') else json.loads(a.strip()) for a in _top_commas(inner)], j + 1


def _top_commas(s):
    if not s.strip():
        return []
    out, cur, quote = [], [], False
    i = 0
    while i < len(s):
        ch = s[i]
        if ch == "\\" and quote:
            cur.append(s[i:i + 2]); i += 2; continue
        if ch == '"':
            quote = not quote
        if ch == "," and not quote:
            out.append("".join(cur)); cur = []
        else:
            cur.append(ch)
        i += 1
    out.append("".join(cur))
    return out


def predicate(name, args):
    """The canonical JSON for a fact: the compact string for one argument."""
    if name == "flag" and len(args) == 1 and ":" not in args[0] and args[0][:1] != "!":
        return f"flag:{args[0]}"
    if len(args) == 1:
        return f"{name}:{args[0]}"
    return {"p": name, "a": list(args)}


def metric(name, args, op, v):
    if not args and name not in ("var", "stock"):
        return f"{name}{op}{v}"
    if len(args) == 1 and not re.search(r"[(),]", args[0]):
        return f"{name}({args[0]}) {op} {v}"
    return {"m": name, "a": list(args), "op": op, "v": v}


def condition_text(c):
    """JSON condition -> the expression the language reads."""
    if isinstance(c, bool):
        return "true" if c else "false"
    if isinstance(c, str):
        s = c.strip()
        if s.startswith("!"):
            return "not " + _wrap(condition_text(s[1:]))
        m = re.match(r"^\s*([a-z_]+)\s*(?:\(([^)]*)\))?\s*(>=|<=|==|!=|>|<)\s*(-?\d+(?:\.\d+)?)\s*$", s)
        if m:
            name, args, op, v = m.group(1), _top_commas(m.group(2) or ""), m.group(3), m.group(4)
            if name == "var" and len(args) == 1:
                return f"${args[0].strip()} {op} {v}"
            a = ", ".join(_arg(x.strip().strip('"')) for x in args)
            return f"{name}({a}) {op} {v}" if args else f"{name} {op} {v}"
        colon = s.find(":")
        if colon > 0:
            return f"{s[:colon].strip()}({_arg(s[colon + 1:].strip())})"
        if re.fullmatch(r"[A-Za-z_][\w\-]*", s) and s not in ("and", "or", "not"):
            return s
        return json.dumps(s)
    if isinstance(c, list):
        return " and ".join(_wrap(condition_text(x)) for x in c) if c else "true"
    if isinstance(c, dict):
        if "all" in c:
            return " and ".join(_wrap(condition_text(x)) for x in c["all"]) if c["all"] else "true"
        if "any" in c:
            a = c["any"] if isinstance(c["any"], list) else [c["any"]]
            return " or ".join(_wrap(condition_text(x)) for x in a) if a else "false"
        if "not" in c:
            return "not " + _wrap(condition_text(c["not"]))
        if "p" in c:
            return f"{c['p']}({', '.join(_arg(str(x)) for x in c.get('a', []))})"
        if "m" in c:
            args = [str(x) for x in c.get("a", [])]
            v = c.get("v", 0)
            if c["m"] == "var" and len(args) == 1:
                return f"${args[0]} {c.get('op', '>=')} {v}"
            a = ", ".join(_arg(x) for x in args)
            return f"{c['m']}({a}) {c.get('op', '>=')} {v}" if args else f"{c['m']} {c.get('op', '>=')} {v}"
    raise CampError(f"cannot write condition {c!r}")


def _wrap(t):
    return f"({t})" if re.search(r"\b(and|or)\b", t) else t


def _arg(s):
    return s if re.fullmatch(r"[\w\-\./]+", s) and not re.fullmatch(r"\d{4}-\d\d(-\d\d)?", s) or re.fullmatch(r"\d{4}-\d\d-\d\d", s) or re.fullmatch(r"\d+-\d+", s) else json.dumps(s)


# ---------------------------------------------------------------- actions

def _num_tok(tok, no):
    v = parse_scalar(tok)
    if num(v) is None:
        raise CampError(f"expected a number, got '{tok}'", no)
    return v


def _words(text):
    return split_words(text)


def parse_action_line(ln, children):
    """One action line (plus its indented children) -> an action dict, or a list for block forms."""
    text = ln.text
    no = ln.no
    head = text.split(None, 1)
    verb = head[0].rstrip(":")
    rest = head[1] if len(head) > 1 else ""

    if verb == "if":
        return _parse_if(ln, children)
    if verb == "random":
        return {"do": "random", "options": _parse_options(children)}
    if verb == "after":
        m = re.match(r"^(\d+)\s*(?:days?)?\s*:?$", rest.strip())
        if not m:
            raise CampError("after N days:", no)
        return {"do": "after", "days": int(m.group(1)), "then": parse_actions(children)}

    words = _words(rest.rstrip(":")) if rest else []
    pos, kw = _kv(words)
    act = {"do": verb}

    if verb in ("flag", "unflag"):
        if len(pos) != 1:
            raise CampError(f"{verb} NAME", no)
        act["name"] = parse_scalar(pos[0]) if pos[0].startswith('"') else pos[0]
    elif verb == "set":
        m = re.match(r"^\$?([\w\-]+)\s*=\s*(.+)$", rest.strip())
        if not m:
            raise CampError("set NAME = N", no)
        act.update(var=m.group(1), to=_num_tok(m.group(2), no))
    elif verb == "add":
        m = re.match(r"^\$?([\w\-]+)\s*(?:([+-])=\s*(\d+(?:\.\d+)?))?$", rest.strip())
        if not m:
            raise CampError("add NAME += N", no)
        act["var"] = m.group(1)
        if m.group(3) is not None:
            v = _num_tok(m.group(3), no)
            act["by"] = v if m.group(2) == "+" else -v
    elif verb == "trigger":
        act["beat"] = pos[0] if len(pos) == 1 else (_ for _ in ()).throw(CampError("trigger BEAT", no))
    elif verb == "tension":
        if not pos:
            raise CampError('tension +N "why"', no)
        act["add"] = _num_tok(pos[0], no)
        if len(pos) > 1:
            act["why"] = parse_scalar(pos[1])
    elif verb == "found_post":
        act["place"] = parse_scalar(pos[0]) if pos else ""
    elif verb == "brigade":
        if len(pos) < 2:
            raise CampError('brigade "run" EFFECT', no)
        act.update(run=parse_scalar(pos[0]), effect=pos[1])
    elif verb == "stock":
        if len(pos) < 3:
            raise CampError('stock "post" Item +N', no)
        act.update(post=parse_scalar(pos[0]), item=pos[1], add=_num_tok(pos[2], no))
    elif verb == "say":
        if not pos:
            raise CampError('say "text"', no)
        act["text"] = parse_scalar(pos[0])
        for extra in pos[1:]:
            if extra == "headline":
                act["headline"] = True
            else:
                raise CampError(f"say: unexpected '{extra}'", no)
    else:
        if pos:
            raise CampError(f"{verb}: write its values as key=value (got '{pos[0]}')", no)
    for k, v in kw.items():
        val = parse_scalar(v)
        if verb == "news" and k == "to" and isinstance(val, str) and val in ADDRESSEES:
            val = ADDRESSEES[val]
        act[k] = val
    if verb == "say" and any(w == "headline" for w in pos):
        pass
    # children of a plain action are `key value` lines (a long news text, say)
    for c in children:
        m = re.match(r"^([\w\-]+)\s*=?\s*(.+)$", c.text)
        if not m:
            raise CampError(f"cannot read '{c.text}' under {verb}", c.no)
        val = parse_scalar(m.group(2))
        if verb == "news" and m.group(1) == "to" and isinstance(val, str) and val in ADDRESSEES:
            val = ADDRESSEES[val]
        act[m.group(1)] = val
    return act


def _split_children(lines):
    """Lines (same block) -> [(Line, [child Lines])]"""
    out, i = [], 0
    while i < len(lines):
        kids, j = _block(lines, i)
        out.append((lines[i], kids))
        i = j
    return out


def parse_actions(lines):
    items = _split_children(lines)
    out, i = [], 0
    while i < len(items):
        ln, kids = items[i]
        if ln.text.split(None, 1)[0].rstrip(":") in ("elif", "else"):
            raise CampError(f"'{ln.text.split()[0]}' without an if", ln.no)
        a = parse_action_line(ln, kids)
        if isinstance(a, dict) and a.get("do") == "if":
            # absorb following elif / else siblings
            tail = a
            while i + 1 < len(items) and items[i + 1][0].text.split(None, 1)[0].rstrip(":") in ("elif", "else"):
                nl, nk = items[i + 1]
                kw = nl.text.split(None, 1)[0].rstrip(":")
                if kw == "else":
                    tail["else"] = parse_actions(nk)
                    i += 1
                    break
                cond = nl.text.split(None, 1)[1].rstrip(":") if len(nl.text.split(None, 1)) > 1 else ""
                nxt = {"do": "if", "cond": parse_condition(cond, nl.no), "then": parse_actions(nk)}
                tail["else"] = [nxt]
                tail = nxt
                i += 1
        out.append(a)
        i += 1
    return out


def _parse_if(ln, children):
    cond = ln.text.split(None, 1)[1].rstrip(":") if len(ln.text.split(None, 1)) > 1 else ""
    if not cond:
        raise CampError("if CONDITION:", ln.no)
    return {"do": "if", "cond": parse_condition(cond, ln.no), "then": parse_actions(children)}


def _parse_options(children):
    opts = []
    for ln, kids in _split_children(children):
        m = re.match(r"^(?:weight\s+)?(\d+(?:\.\d+)?)\s*%?\s*:?$", ln.text)
        if not m:
            raise CampError("random options read `30%:` or `weight 2:`", ln.no)
        w = float(m.group(1))
        opts.append({"weight": int(w) if w == int(w) else w, "do": parse_actions(kids)})
    return opts


# ---------------------------------------------------------------- beats and the file

BEAT_KINDS = ("fixed", "conditional", "event", "pressure")


def compile_text(text):
    lines = expand(logical_lines(text))
    out = {"title": "", "description": "", "beats": []}
    i = 0
    while i < len(lines):
        ln = lines[i]
        kids, j = _block(lines, i)
        head = ln.text.split(None, 1)
        word, rest = head[0], (head[1] if len(head) > 1 else "")
        if ln.indent != 0:
            raise CampError("unexpected indentation", ln.no)
        if word == "campaign":
            out["title"] = parse_scalar(rest)
        elif word == "description":
            out["description"] = parse_scalar(rest)
        elif word == "note":
            out["note"] = parse_scalar(rest)
        elif word == "posts_by_founding_year":
            out["posts_by_founding_year"] = True
        elif word == "seed":
            out["seed"] = int(rest)
        elif word == "beat":
            out["beats"].append(_parse_beat(ln, kids))
        else:
            raise CampError(f"unknown top-level '{word}' (campaign, description, note, beat, posts_by_founding_year, seed)", ln.no)
        i = j
    return out


def _parse_beat(ln, kids):
    words = split_words(ln.text[len("beat"):])
    if len(words) < 2:
        raise CampError('beat ID KIND [YYYY-MM-DD] ["Title"]', ln.no)
    beat = {"id": words[0], "title": "", "kind": words[1]}
    if beat["kind"] not in BEAT_KINDS:
        raise CampError(f"kind must be one of {', '.join(BEAT_KINDS)} (got '{beat['kind']}')", ln.no)
    for w in words[2:]:
        if w.startswith('"'):
            beat["title"] = parse_scalar(w)
        elif re.fullmatch(r"\d{4}-\d\d-\d\d", w):
            beat["date"] = w
        else:
            raise CampError(f"cannot read '{w}' in the beat line", ln.no)
    if not beat["title"]:
        beat["title"] = beat["id"]
    for pl, pkids in _split_children(kids):
        head = pl.text.split(None, 1)
        key, rest = head[0].rstrip(":"), (head[1] if len(head) > 1 else "")
        if key in ("window", "lead", "delay", "repeat"):
            try:
                beat[{"window": "window_days", "lead": "lead_days", "delay": "delay_days", "repeat": "repeat_days"}[key]] = int(float(rest.split()[0]))
            except (IndexError, ValueError):
                raise CampError(f"{key} needs a number of days", pl.no)
        elif key == "requires":
            beat.setdefault("requires", []).extend(parse_scalar(w) if w.startswith('"') else w for w in split_words(rest))
        elif key == "keywords":
            beat.setdefault("keywords", []).extend(parse_scalar(w) if w.startswith('"') else w for w in split_words(rest))
        elif key in ("when", "done_when"):
            if not rest.strip():
                raise CampError(f"{key} CONDITION", pl.no)
            beat.setdefault(key, []).append(parse_condition(rest.rstrip(":"), pl.no))
        elif key in ("pressure", "happens"):
            beat.setdefault(key, []).extend(parse_actions(pkids))
        elif key == "date":
            beat["date"] = rest.strip()
        elif key == "title":
            beat["title"] = parse_scalar(rest)
        else:
            raise CampError(f"unknown beat line '{key}' (window, lead, delay, repeat, requires, keywords, when, done_when, pressure, happens)", pl.no)
    return beat


# ---------------------------------------------------------------- decompile

def decompile(d):
    out = []
    out.append(f"campaign {json.dumps(d.get('title', ''))}")
    if d.get("description"):
        out.append(f"description {json.dumps(d['description'])}")
    if d.get("note"):
        out.append(f"note {json.dumps(d['note'])}")
    if d.get("posts_by_founding_year"):
        out.append("posts_by_founding_year")
    if "seed" in d:
        out.append(f"seed {d['seed']}")
    for b in d.get("beats", []):
        out.append("")
        head = f"beat {b['id']} {b.get('kind', 'event')}"
        if b.get("date"):
            head += f" {b['date']}"
        if b.get("title") and b["title"] != b["id"]:
            head += f" {json.dumps(b['title'])}"
        out.append(head)
        for key, short in (("window_days", "window"), ("lead_days", "lead"), ("delay_days", "delay"), ("repeat_days", "repeat")):
            if key in b:
                out.append(f"  {short} {fmt_scalar(b[key])}")
        for key in ("requires", "keywords"):
            v = b.get(key)
            if v:
                v = [v] if isinstance(v, str) else v
                out.append(f"  {key} " + ", ".join(fmt_scalar(x) for x in v))
        for key in ("when", "done_when"):
            v = b.get(key)
            if v:
                for c in ([v] if isinstance(v, str) else v):
                    out.append(f"  {key} {condition_text(c)}")
        for key in ("pressure", "happens"):
            acts = b.get(key)
            if acts:
                out.append(f"  {key}")
                out.extend(action_lines(acts, 4))
    return "\n".join(out) + "\n"


def action_lines(acts, indent):
    out = []
    for a in acts:
        out.extend(action_text(a, indent))
    return out


def _keys_ok(a, allowed):
    return set(a) - {"do"} <= set(allowed)


LONG = 60


def action_text(a, indent):
    pad = " " * indent
    verb = a.get("do", "")
    if verb == "if":
        out = [f"{pad}if {condition_text(a.get('cond'))}:"] + action_lines(a.get("then", []), indent + 2)
        el = a.get("else")
        while el and len(el) == 1 and el[0].get("do") == "if" and set(el[0]) <= {"do", "cond", "then", "else"}:
            n = el[0]
            out += [f"{pad}elif {condition_text(n.get('cond'))}:"] + action_lines(n.get("then", []), indent + 2)
            el = n.get("else")
        if el:
            out += [f"{pad}else:"] + action_lines(el, indent + 2)
        return out
    if verb == "random":
        out = [f"{pad}random:"]
        for o in a.get("options", []):
            out.append(f"{pad}  {fmt_scalar(o.get('weight', 1))}:")
            out += action_lines(o.get("do", []), indent + 4)
        return out
    if verb == "after" and _keys_ok(a, ("days", "then")):
        return [f"{pad}after {fmt_scalar(a.get('days', 1))} days:"] + action_lines(a.get("then", []), indent + 2)
    if verb in ("flag", "unflag") and _keys_ok(a, ("name",)):
        return [f"{pad}{verb} {fmt_scalar(a.get('name', ''))}"]
    if verb == "set" and _keys_ok(a, ("var", "to")):
        return [f"{pad}set {a['var']} = {fmt_scalar(a.get('to', 0))}"]
    if verb == "add" and _keys_ok(a, ("var", "by")):
        by = a.get("by", 1)
        if "by" not in a:
            return [f"{pad}add {a['var']}"]
        return [f"{pad}add {a['var']} {'+=' if by >= 0 else '-='} {fmt_scalar(abs(by))}"]
    if verb == "trigger" and _keys_ok(a, ("beat",)):
        return [f"{pad}trigger {a['beat']}"]
    if verb == "tension" and _keys_ok(a, ("add", "why")) and "add" in a:
        v = a["add"]
        sign = "+" if v >= 0 else "-"
        why = f" {json.dumps(a['why'])}" if "why" in a else ""
        return [f"{pad}tension {sign}{fmt_scalar(abs(v))}{why}"]
    if verb == "found_post" and _keys_ok(a, ("place",)):
        return [f"{pad}found_post {json.dumps(a.get('place', ''))}"]
    if verb == "brigade" and _keys_ok(a, ("run", "effect", "days", "leg")) and "run" in a:
        extra = "".join(f" {k}={fmt_scalar(a[k])}" for k in ("days", "leg") if k in a)
        return [f"{pad}brigade {json.dumps(a['run'])} {a.get('effect', 'delay')}{extra}"]
    if verb == "stock" and _keys_ok(a, ("post", "item", "add")) and {"post", "item", "add"} <= set(a):
        v = a["add"]
        return [f"{pad}stock {json.dumps(a['post'])} {a['item']} {'+' if v >= 0 else '-'}{fmt_scalar(abs(v))}"]
    if verb == "say" and _keys_ok(a, ("text", "to", "headline")) and "text" in a and a.get("headline") in (None, True):
        head = f"{pad}say {json.dumps(a['text'])}"
        if "to" in a:
            head += f" to={fmt_scalar(a['to'])}"
        if a.get("headline"):
            head += " headline"
        return [head]
    # anything else: verb key=value ..., long strings on their own lines beneath
    inline, below = [], []
    for k, v in a.items():
        if k == "do":
            continue
        if verb == "news" and k == "to" and v in ADDRESSEE_NAMES:
            v = ADDRESSEE_NAMES[v]
        if isinstance(v, str) and (len(v) > LONG or "\n" in v):
            below.append((k, v))
        elif isinstance(v, (str, int, float, bool)):
            inline.append(f"{k}={fmt_scalar(v)}")
        else:
            below.append((k, v))
    out = [f"{pad}{verb}" + ("".join(" " + x for x in inline))]
    for k, v in below:
        if isinstance(v, str) and "\n" in v:
            out.append(f"{pad}  {k} " + '"""')
            out.extend(f"{pad}  {line}" if line else "" for line in v.split("\n"))
            out.append(f'{pad}  """')
        else:
            out.append(f"{pad}  {k} {json.dumps(v)}")
    return out
