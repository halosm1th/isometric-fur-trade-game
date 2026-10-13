"""Check a campaign script: things the game would warn about or silently ignore.

Errors are things that cannot work (a typo in an action, a beat that requires
a beat that does not exist, a cycle). Warnings are things that probably are not
what you meant (a flag nothing sets, a beat that can never fire, a date that
contradicts what it requires).
"""
import re

import engine
import known

KINDS = ("fixed", "conditional", "event", "pressure")
_COMPARE = re.compile(r"^\s*([a-z_]+)\s*(?:\(([^)]*)\))?\s*(>=|<=|==|!=|>|<)\s*(-?\d+(?:\.\d+)?)\s*$")
_CALL = re.compile(r"^\s*([a-z_]+)\s*\(([^)]*)\)\s*$")


class Report:
    def __init__(self):
        self.errors, self.warnings, self.infos = [], [], []

    def error(self, where, msg):
        self.errors.append((where, msg))

    def warn(self, where, msg):
        self.warnings.append((where, msg))

    def info(self, where, msg):
        self.infos.append((where, msg))

    def lines(self, verbose=False):
        out = [f"error: {w}: {m}" for w, m in self.errors] + [f"warning: {w}: {m}" for w, m in self.warnings]
        if verbose:
            out += [f"note: {w}: {m}" for w, m in self.infos]
        return out


class Facts:
    """Everything the script reads and writes, collected while walking it."""

    def __init__(self):
        self.flags_set, self.flags_read = {}, {}
        self.vars_set, self.vars_read = {}, {}
        self.triggers = {}


def condition_atoms(c, out=None):
    """-> [('p'|'m', name, [args], op, value)] for every leaf of a condition."""
    out = [] if out is None else out
    if c is None or isinstance(c, bool):
        return out
    if isinstance(c, list):
        for x in c:
            condition_atoms(x, out)
    elif isinstance(c, str):
        s = c.strip().lstrip("!").strip()
        m = _COMPARE.match(s)
        if m:
            out.append(("m", m.group(1), engine.split_args(m.group(2)), m.group(3), float(m.group(4))))
        elif ":" in s and s.partition(":")[0].strip() in engine.FLAG_PREFIXES:
            out.append(("p", "flag", [s], None, None))
        elif ":" in s:
            name, _, arg = s.partition(":")
            out.append(("p", name.strip(), [arg.strip()], None, None))
        elif _CALL.match(s):
            m = _CALL.match(s)
            out.append(("p", m.group(1), engine.split_args(m.group(2)), None, None))
        elif s:
            out.append(("p", "flag", [s], None, None))
    elif isinstance(c, dict):
        for k in ("all", "any"):
            if k in c:
                condition_atoms(c[k], out)
        if "not" in c:
            condition_atoms(c["not"], out)
        if "p" in c:
            out.append(("p", c["p"], [str(x) for x in c.get("a", [])], None, None))
        if "m" in c:
            out.append(("m", c["m"], [str(x) for x in c.get("a", [])], c.get("op", ">="), c.get("v", 0)))
    return out


def check(doc, report=None):
    rep = report or Report()
    facts = Facts()
    world = known.world()
    beats = doc.get("beats", [])
    ids = {}
    for i, b in enumerate(beats):
        bid = b.get("id", f"beat{i}")
        if bid in ids:
            rep.error(f"beat {bid}", "duplicate id")
        ids[bid] = b

    for b in beats:
        bid = b.get("id", "?")
        where = f"beat {bid}"
        kind = b.get("kind", "event")
        if kind not in KINDS:
            rep.error(where, f"unknown kind '{kind}' (use {', '.join(KINDS)})")
        if b.get("date"):
            if not re.fullmatch(r"\d{4}-\d\d-\d\d", str(b["date"])):
                rep.error(where, f"date '{b['date']}' is not YYYY-MM-DD")
            else:
                try:
                    engine.date_of(engine.day_number(b["date"]))
                    y, m, d = map(int, b["date"].split("-"))
                    import datetime
                    datetime.date(y, m, d)
                except ValueError:
                    rep.error(where, f"date '{b['date']}' is not a real day")
        elif kind == "fixed":
            rep.error(where, "a fixed beat needs a date")
        elif kind in ("event", "pressure") and not b.get("requires") and not b.get("when"):
            rep.warn(where, f"{kind} beat with no date and nothing to wait for: it will happen on the first day")
        if kind == "pressure" and b.get("happens"):
            rep.warn(where, "a pressure beat's 'happens' never runs (only its pressure does)")
        if b.get("done_when") and kind in ("fixed", "pressure"):
            rep.warn(where, "done_when is only used by event beats and conditional ones")
        if b.get("repeat_days") and not b.get("date") and kind != "conditional":
            rep.warn(where, "repeat without a date repeats only after its cooldown")
        for key in ("when", "done_when"):
            for j, c in enumerate(_aslist(b.get(key))):
                _check_condition(c, f"{where} > {key}[{j}]", rep, facts, world)
        for key in ("pressure", "happens"):
            _check_actions(b.get(key), f"{where} > {key}", rep, facts, world, bid)
        for r in _aslist(b.get("requires")):
            facts.flags_read.setdefault(r, []).append(where)

    # cross references
    for t, wheres in facts.triggers.items():
        if t not in ids:
            for w in wheres:
                rep.error(w, f"trigger of unknown beat '{t}'")
    settable = set(facts.flags_set) | set(ids) | known.ENGINE_FLAGS
    for b in beats:
        for r in _aslist(b.get("requires")):
            if r not in ids and r not in settable and not r.startswith(known.ENGINE_FLAG_PREFIXES):
                rep.error(f"beat {b.get('id')}", f"requires '{r}', which is not a beat and no action sets it as a flag")
    for name, wheres in facts.flags_read.items():
        if name in settable or name.startswith(known.ENGINE_FLAG_PREFIXES):
            continue
        rep.warn(wheres[0], f"reads flag '{name}', which nothing sets")
    for name in facts.flags_set:
        if name not in facts.flags_read and name not in ids:
            rep.info(facts.flags_set[name][0], f"sets flag '{name}', which nothing reads (fine if it is only for the log)")
    for name, wheres in facts.vars_read.items():
        if name not in facts.vars_set:
            rep.warn(wheres[0], f"reads variable '{name}', which nothing sets (it will always be 0)")

    _cycles(beats, ids, rep)
    _reachability(beats, ids, facts, rep)
    _date_order(beats, ids, rep)
    return rep


def _aslist(x):
    return [] if x is None else x if isinstance(x, list) else [x]


def _check_condition(c, where, rep, facts, world):
    for kind, name, args, op, val in condition_atoms(c):
        if kind == "p":
            if name not in known.PREDICATES:
                rep.error(where, f"unknown condition '{name}' (known: {', '.join(sorted(known.PREDICATES))})")
                continue
            arg = args[0] if args else ""
            if name == "flag":
                facts.flags_read.setdefault(arg, []).append(where)
            elif name == "beat":
                facts.flags_read.setdefault(arg, []).append(where)
            elif name in ("after", "before") and not re.fullmatch(r"\d{4}-\d\d-\d\d", arg):
                rep.error(where, f"{name}:{arg} needs a date (YYYY-MM-DD)")
            elif name == "post" and arg not in world.posts and arg not in ("York Factory", "Oxford House"):
                rep.error(where, f"post '{arg}' is not on the map")
            elif name == "month" and not re.fullmatch(r"\d{1,2}(-\d{1,2})?", arg):
                rep.error(where, f"month:{arg} should be a month number or a range like 6-8")
            elif name == "chance":
                try:
                    float(arg)
                except ValueError:
                    rep.error(where, f"chance:{arg} needs a percentage")
            elif name.startswith("convoy_") and name != "convoy_wintering" and arg not in world.runs:
                rep.error(where, f"'{arg}' is not a brigade run (known: {', '.join(sorted(world.runs))})")
        else:
            if name not in known.METRICS:
                rep.error(where, f"unknown number '{name}' (known: {', '.join(sorted(known.METRICS))})")
                continue
            if name == "var" and args:
                facts.vars_read.setdefault(args[0], []).append(where)
            if name == "stock":
                if len(args) != 2 or args[0] not in world.ledgers or args[1] not in world.items:
                    rep.error(where, f"stock(POST, ITEM): got ({', '.join(args)})")
            if name in ("convoy_days_late", "convoy_days_out") and (not args or args[0] not in world.runs):
                rep.error(where, f"{name} needs a brigade run")


def _check_actions(acts, where, rep, facts, world, bid):
    for i, a in enumerate(_aslist(acts)):
        w = f"{where}[{i}]"
        if not isinstance(a, dict):
            rep.error(w, f"not an action: {a!r}")
            continue
        verb = a.get("do")
        if verb not in known.ACTIONS:
            rep.error(w, f"unknown action '{verb}' (known: {', '.join(sorted(known.ACTIONS))})")
            continue
        req, opt = known.ACTIONS[verb]
        for f in req:
            if f not in a:
                rep.error(w, f"{verb} needs '{f}'")
        for f in a:
            if f not in ("do", "_beat") and f not in req and f not in opt:
                rep.error(w, f"{verb} has no field '{f}'")
        if verb == "flag" and "name" in a:
            facts.flags_set.setdefault(a["name"], []).append(w)
        if verb == "unflag" and "name" in a:
            facts.flags_read.setdefault(a["name"], []).append(w)
        if verb in ("set", "add") and "var" in a:
            facts.vars_set.setdefault(a["var"], []).append(w)
        if verb == "trigger" and "beat" in a:
            facts.triggers.setdefault(a["beat"], []).append(w)
        if verb == "if":
            _check_condition(a.get("cond"), w + " > cond", rep, facts, world)
            _check_actions(a.get("then"), w + " > then", rep, facts, world, bid)
            _check_actions(a.get("else"), w + " > else", rep, facts, world, bid)
        elif verb == "random":
            opts = a.get("options") or []
            if not opts or sum(max(0, o.get("weight", 1)) for o in opts if isinstance(o, dict)) <= 0:
                rep.error(w, "random needs options with a weight above 0")
            for j, o in enumerate(opts):
                _check_actions(o.get("do"), f"{w} > option[{j}]", rep, facts, world, bid)
        elif verb == "after":
            if not isinstance(a.get("days"), (int, float)) or a["days"] < 0:
                rep.error(w, "after needs a number of days")
            _check_actions(a.get("then"), w + " > then", rep, facts, world, bid)
        elif verb == "found_post" and a.get("place") not in world.posts:
            rep.error(w, f"found_post: '{a.get('place')}' is not a post on the map")
        elif verb == "brigade":
            if a.get("run") not in world.runs:
                rep.error(w, f"brigade: '{a.get('run')}' is not a brigade run (known: {', '.join(sorted(world.runs))})")
            if a.get("effect", "delay") not in known.BRIGADE_EFFECTS:
                rep.error(w, f"brigade effect '{a.get('effect')}' is not one of {', '.join(known.BRIGADE_EFFECTS)}")
            elif a.get("effect") == "launch" and a.get("run") in world.runs and not 0 <= a.get("leg", 0) < len(world.runs[a["run"]]["legs"]):
                rep.error(w, f"brigade launch: {a['run']} has {len(world.runs[a['run']]['legs'])} legs (0-based)")
        elif verb == "stock":
            if a.get("post") not in world.ledgers:
                rep.error(w, f"stock: '{a.get('post')}' is not a post with books")
            if a.get("item") not in world.items:
                rep.error(w, f"stock: '{a.get('item')}' is not an item")
        elif verb == "news":
            if a.get("to") and a["to"] not in known.ADDRESSEES:
                rep.warn(w, f"news to '{a['to']}': the usual addressees are {', '.join(known.ADDRESSEES)}; a person's name works only if they are alive and known")
            if a.get("company") and a["company"] not in known.COMPANIES:
                rep.error(w, f"news company must be HBC or NWC (got '{a['company']}')")
        elif verb == "say" and a.get("to") not in (None, "all", "HBC", "NWC"):
            rep.error(w, "say: to must be all, HBC or NWC")
        elif verb == "tension" and isinstance(a.get("add"), (int, float)) and abs(a["add"]) > 100:
            rep.warn(w, f"tension {a['add']:+} is more than the whole 0-100 scale")


def _cycles(beats, ids, rep):
    graph = {b["id"]: [r for r in _aslist(b.get("requires")) if r in ids] for b in beats if "id" in b}
    state = {}

    def visit(n, path):
        if state.get(n) == 2:
            return
        if state.get(n) == 1:
            rep.error(f"beat {n}", "requires itself through a cycle: " + " -> ".join(path[path.index(n):] + [n]))
            return
        state[n] = 1
        for m in graph.get(n, []):
            visit(m, path + [n])
        state[n] = 2

    for n in graph:
        visit(n, [])


def _reachability(beats, ids, facts, rep):
    """A beat can never fire if something it requires can never happen."""
    possible = set(known.ENGINE_FLAGS)
    possible |= {f for f in facts.flags_set}
    dead = set()
    changed = True
    while changed:
        changed = False
        for b in beats:
            bid = b.get("id")
            if bid in dead:
                continue
            for r in _aslist(b.get("requires")):
                if r in dead or (r not in ids and r not in possible and not r.startswith(known.ENGINE_FLAG_PREFIXES)):
                    dead.add(bid)
                    changed = True
                    rep.warn(f"beat {bid}", f"can never happen: it requires '{r}', which can never happen")
                    break


def _date_order(beats, ids, rep):
    for b in beats:
        if not b.get("date"):
            continue
        for r in _aslist(b.get("requires")):
            o = ids.get(r)
            if o and o.get("date") and o["date"] > b["date"]:
                rep.warn(f"beat {b['id']}", f"dated {b['date']} but requires {r} ({o['date']}): it will wait for it")
