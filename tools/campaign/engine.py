"""The campaign language, evaluated outside the game.

A port of Scripts/Core/CampaignLogic.cs - the same conditions, the same
actions, the same roll - so a script can be simulated here and tested against
the cases in conformance.json, which the C# side runs too. If you change the
language, change both and add a case.
"""
import re
import datetime

EPOCH = datetime.date(1700, 1, 1)
CONVOY_FLAGS = {
    "convoy_late": "late:", "convoy_lost": "lost:", "convoy_robbed": "robbed:",
    "convoy_turned_back": "turned:", "convoy_out": "out:", "convoy_in": "in:",
}
_COMPARE = re.compile(r"^\s*([a-z_]+)\s*(?:\(([^)]*)\))?\s*(>=|<=|==|!=|>|<)\s*(-?\d+(?:\.\d+)?)\s*$")
_CALL = re.compile(r"^\s*([a-z_]+)\s*\(([^)]*)\)\s*$")


def day_number(y, m=None, d=None):
    if m is None:  # ISO string
        parts = str(y).strip().split("-")
        y = int(parts[0]) if parts and parts[0] else 1811
        m = int(parts[1]) if len(parts) > 1 else 1
        d = int(parts[2]) if len(parts) > 2 else 1
    y = min(max(y, 1700), 2100)
    m = min(max(m, 1), 12)
    return (datetime.date(y, m, 1) + datetime.timedelta(days=max(0, d - 1)) - EPOCH).days


def date_of(day):
    return EPOCH + datetime.timedelta(days=day)


class World:
    """What the game would answer. The simulator subclasses this."""

    def metric(self, name, args):
        return float("nan")

    def predicate(self, name, args):
        return False

    def effect(self, name, action):
        pass


class State:
    def __init__(self, today=0, seed=20240611):
        self.today = today
        self.flags = set()
        self.vars = {}
        self.done = set()
        self.done_day = {}
        self.timers = []  # (day, actions)
        self.forced = []
        self.seed = seed

    def roll(self):
        self.seed = (self.seed * 1664525 + 1013904223) & 0xFFFFFFFF
        return self.seed / 4294967296.0


def split_args(s):
    if s is None or not s.strip():
        return []
    return [p.strip().strip('"') for p in s.split(",")]


def _num(o):
    if o is None:
        return 0.0
    if isinstance(o, bool):
        return 1.0 if o else 0.0
    if isinstance(o, (int, float)):
        return float(o)
    try:
        return float(o)
    except (TypeError, ValueError):
        return 0.0


def _str(o):
    if o is None:
        return ""
    if isinstance(o, bool):
        return "true" if o else "false"
    if isinstance(o, float):
        return ("%.2f" % o).rstrip("0").rstrip(".") if o != int(o) else str(int(o))
    return str(o)


def _compared(l, op, r):
    if l != l:  # NaN: the world knows no such number
        return False
    return {">=": l >= r, "<=": l <= r, ">": l > r, "<": l < r,
            "==": abs(l - r) < 1e-9, "!=": abs(l - r) >= 1e-9}.get(op, False)


def all_of(conds, st, w):
    if conds is None:
        return True
    if isinstance(conds, list):
        return all(eval_cond(c, st, w) for c in conds)
    return eval_cond(conds, st, w)


def eval_cond(c, st, w):
    if c is None:
        return True
    if isinstance(c, bool):
        return c
    if isinstance(c, str):
        return _eval_string(c, st, w)
    if isinstance(c, list):
        return all_of(c, st, w)
    if isinstance(c, dict):
        if "all" in c:
            return all_of(c["all"], st, w)
        if "any" in c:
            a = c["any"]
            return any(eval_cond(x, st, w) for x in a) if isinstance(a, list) else eval_cond(a, st, w)
        if "not" in c:
            return not eval_cond(c["not"], st, w)
        if "p" in c:
            return _predicate(_str(c["p"]), _args(c), st, w)
        if "m" in c:
            v = _metric(_str(c["m"]), _args(c), st, w)
            return _compared(v, _str(c.get("op", ">=")), _num(c.get("v", 0)))
    return False


def _args(d):
    a = d.get("a")
    if a is None:
        return []
    return [_str(x) for x in a] if isinstance(a, list) else [_str(a)]


def _eval_string(c, st, w):
    c = c.strip()
    if not c:
        return True
    if c[0] == "!":
        return not _eval_string(c[1:], st, w)
    m = _COMPARE.match(c)
    if m:
        return _compared(_metric(m.group(1), split_args(m.group(2)), st, w), m.group(3), float(m.group(4)))
    colon = c.find(":")
    if colon > 0:
        return _predicate(c[:colon].strip(), [c[colon + 1:].strip()], st, w)
    call = _CALL.match(c)
    if call:
        return _predicate(call.group(1), split_args(call.group(2)), st, w)
    return c in st.flags


def _predicate(name, a, st, w):
    arg = a[0] if a else ""
    if name == "flag":
        return arg in st.flags
    if name == "beat":
        return arg in st.done
    if name == "after":
        return st.today >= day_number(arg)
    if name == "before":
        return st.today < day_number(arg)
    if name == "chance":
        return st.roll() * 100.0 < float(arg)
    if name == "month":
        return _month_in(date_of(st.today).month, arg)
    if name in CONVOY_FLAGS:
        return (CONVOY_FLAGS[name] + arg) in st.flags
    return bool(w.predicate(name, a))


def _month_in(month, spec):
    p = spec.split("-")
    lo = int(p[0].strip())
    hi = int(p[1].strip()) if len(p) > 1 else lo
    return lo <= month <= hi if lo <= hi else (month >= lo or month <= hi)


def _metric(name, a, st, w):
    arg = a[0] if a else ""
    d = date_of(st.today)
    if name == "var":
        return st.vars.get(arg, 0.0)
    if name == "month":
        return float(d.month)
    if name == "year":
        return float(d.year)
    if name == "day":
        return float(d.day)
    if name == "day_of_year":
        return float(d.timetuple().tm_yday)
    if name == "count":
        return st.vars.get("count:" + arg, 0.0)
    if name == "beat_age":
        return float(st.today - st.done_day[arg]) if arg in st.done_day else 99999.0
    return w.metric(name, a)


def interp(s, st):
    if "{" not in s:
        return s

    def sub(m):
        key = ("count:" + m.group(2)) if m.group(1) == "count" else m.group(2)
        return _str(round(st.vars.get(key, 0.0), 2))
    return re.sub(r"\{(var|count):([^}]+)\}", sub, s)


def run(actions, st, w, depth=0):
    if actions is None or depth > 12:
        return
    if isinstance(actions, dict):
        _run_one(actions, st, w, depth)
        return
    if isinstance(actions, list):
        for a in actions:
            if isinstance(a, dict):
                _run_one(a, st, w, depth)


def _run_one(a, st, w, depth):
    name = _str(a.get("do"))
    if name == "flag":
        st.flags.add(interp(_str(a.get("name")), st))
        return
    if name == "unflag":
        st.flags.discard(interp(_str(a.get("name")), st))
        return
    if name == "set":
        st.vars[_str(a.get("var"))] = _num(a.get("to"))
        return
    if name == "add":
        k = _str(a.get("var"))
        st.vars[k] = st.vars.get(k, 0.0) + _num(a.get("by", 1.0))
        return
    if name == "if":
        run(a.get("then") if eval_cond(a.get("cond"), st, w) else a.get("else"), st, w, depth + 1)
        return
    if name == "random":
        opts = a.get("options")
        if not isinstance(opts, list) or not opts:
            return
        total = sum(max(0.0, _num(o.get("weight", 1.0))) for o in opts if isinstance(o, dict))
        pick = st.roll() * total
        for o in opts:
            if not isinstance(o, dict):
                continue
            pick -= max(0.0, _num(o.get("weight", 1.0)))
            if pick < 0:
                run(o.get("do"), st, w, depth + 1)
                return
        return
    if name == "after":
        body = a.get("then")
        st.timers.append((st.today + int(_num(a.get("days", 1.0))), body if isinstance(body, list) else [body]))
        return
    if name == "trigger":
        st.forced.append(_str(a.get("beat")))
        return
    filled = {k: (interp(v, st) if isinstance(v, str) else v) for k, v in a.items()}
    w.effect(name, filled)


def due(st):
    """Timers that have come due, taken off the list."""
    out, keep = [], []
    for day, actions in st.timers:
        if day <= st.today:
            out.append(actions)
        else:
            keep.append((day, actions))
    st.timers = keep
    return out
