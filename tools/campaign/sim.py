"""Play a campaign script forward on a made-up world, no game needed.

This is Campaign.Tick (Scripts/Core/Campaign.cs) ported line for line, driven
by a scenario file that says what the world would be doing: how tense the
companies are, which posts stand, when the brigades run late. It answers the
questions you cannot easily answer by playing: which beats ever fire, in what
order, and how often the dice or the world would have to cooperate.
"""
import datetime
import json
import random

import engine
import known

LEVELS = {"Gentle": 0, "Steady": 1, "Firm": 2, "Strict": 3}
SEASONS = {5: "Breakup", 6: "Breakup", 7: "OpenWater", 8: "OpenWater", 9: "FreezeUp", 10: "FreezeUp"}


def default_scenario():
    return {
        "start": "1811-08-20", "end": "1821-12-31", "adherence": "Steady", "player_faction": "HBC",
        "tension": {"1811-01-01": 20}, "standing": 20,
        "stocks": {}, "dead": [], "events": [], "random_events": [],
    }


def step_value(table, day):
    """A timeline written as {date: value}: the last value whose date has come."""
    best = None
    for d, v in sorted(table.items()):
        if engine.day_number(d) <= day:
            best = v
    return best if best is not None else next(iter(table.values()))


class SimWorld(engine.World):
    def __init__(self, scenario, rng):
        self.sc, self.rng = scenario, rng
        self.st = None
        self.world = known.world()
        self.tension_delta = 0.0
        self.founded = set()
        self.stocks = {k: float(v) for k, v in scenario.get("stocks", {}).items()}
        self.log = []
        self.start_year = 1811
        self.flag_expiry = []   # (day, flag)
        self.convoy_days_late = {}

    def tension(self):
        base = step_value(self.sc["tension"], self.st.today)
        return max(0.0, min(100.0, base + self.tension_delta))

    def post_exists(self, name):
        p = self.world.places.get(name)
        if not p or p.get("kind") != "post":
            return name in ("York Factory", "Oxford House")
        if name in self.founded:
            return True
        closed = p.get("closed", 0)
        if closed and closed <= engine.date_of(self.st.today).year:
            return False
        return p.get("year", 0) <= self.start_year

    def metric(self, name, args):
        if name == "tension":
            return self.tension()
        if name == "standing":
            return float(self.sc.get("standing", 20))
        if name == "stock":
            return self.stocks.get(f"{args[0]}|{args[1]}", 0.0) if len(args) > 1 else float("nan")
        if name == "furs":
            return self.stocks.get(f"{args[0]}|furs", 0.0)
        if name == "convoys_active":
            return float(sum(1 for f in self.st.flags if f.startswith("out:") and ("in:" + f[4:]) not in self.st.flags))
        if name == "convoy_days_late":
            return float(self.convoy_days_late.get(args[0], 0))
        if name == "convoy_days_out":
            return 0.0
        return float("nan")

    def predicate(self, name, args):
        arg = args[0] if args else ""
        if name == "post":
            return self.post_exists(arg)
        if name == "dead":
            return arg in self.sc.get("dead", [])
        if name == "player_faction":
            return arg.lower() == self.sc.get("player_faction", "HBC").lower()
        if name == "season":
            m = engine.date_of(self.st.today).month
            return arg.lower() == SEASONS.get(m, "Winter").lower()
        if name == "convoy_wintering":
            return False
        return False

    def effect(self, name, a):
        date = engine.date_of(self.st.today).isoformat()
        text = describe(name, a)
        self.log.append((date, a.get("_beat", ""), text))
        if name == "tension":
            self.tension_delta += float(a.get("add", 0))
        elif name == "found_post":
            self.founded.add(a.get("place", ""))
        elif name == "stock":
            k = f"{a.get('post')}|{a.get('item')}"
            self.stocks[k] = max(0.0, self.stocks.get(k, 0.0) + float(a.get("add", 0)))
        elif name == "brigade":
            run, eff = a.get("run", ""), a.get("effect", "delay")
            if eff == "turn_back":
                self.st.flags.update({"brigade_turned_back", f"turned:{run}"})
            elif eff == "lose":
                self.st.flags.update({"brigade_lost", f"lost:{run}"})
            elif eff == "launch":
                self.st.flags.update({f"out:{run}"})
                for p in (f"late:{run}", f"in:{run}", f"lost:{run}", f"robbed:{run}", f"turned:{run}"):
                    self.st.flags.discard(p)


def describe(name, a):
    keys = {k: v for k, v in a.items() if k not in ("do", "_beat")}
    if name == "tension":
        return f"tension {a.get('add', 0):+g} ({a.get('why', '')})"
    if name == "news":
        return f"news {a.get('from', 'Montreal')} -> {a.get('to', 'the Factor at York Factory')} in {a.get('days', 30)} days: {a.get('subject', '')}"
    if name == "say":
        return f"say to {a.get('to', 'all')}: {a.get('text', '')[:80]}"
    return f"{name} " + " ".join(f"{k}={v}" for k, v in keys.items())


class Beat:
    def __init__(self, b):
        self.id = b["id"]
        self.title = b.get("title", self.id)
        self.kind = b.get("kind", "event")
        self.date = engine.day_number(b["date"]) if b.get("date") else -1
        self.window = int(b.get("window_days", 60))
        self.lead = int(b.get("lead_days", 30))
        self.delay = int(b.get("delay_days", 0))
        self.repeat = int(b.get("repeat_days", 0))
        self.requires = _l(b.get("requires"))
        self.when = _l(b.get("when"))
        self.done_when = _l(b.get("done_when"))
        self.pressure = _l(b.get("pressure"))
        self.happens = _l(b.get("happens"))


def _l(x):
    return [] if x is None else x if isinstance(x, list) else [x]


def tag(actions, beat):
    """Stamp each action with its beat, as Campaign.Defaults does."""
    for a in _l(actions):
        if not isinstance(a, dict):
            continue
        a["_beat"] = beat.id
        if a.get("do") in ("flag", "unflag") and "name" not in a:
            a["name"] = beat.id
        if a.get("do") == "tension" and "why" not in a:
            a["why"] = beat.title
        for k in ("then", "else"):
            tag(a.get(k), beat)
        for o in a.get("options") or []:
            tag(o.get("do"), beat)


class Result:
    def __init__(self):
        self.fired = {}     # beat -> [(date, how)]
        self.log = []
        self.flags = set()
        self.vars = {}
        self.founded = []
        self.history = []


def simulate(doc, scenario=None, seed=None, level=None, rng=None):
    sc = dict(default_scenario())
    sc.update(scenario or {})
    rng = rng or random.Random(seed)
    level = LEVELS[level or sc.get("adherence", "Steady")]
    beats = [Beat(json.loads(json.dumps(b))) for b in doc.get("beats", [])]
    for b in beats:
        tag(b.pressure, b)
        tag(b.happens, b)
    st = engine.State(0, seed if seed is not None else int(doc.get("seed", 20240611)))
    w = SimWorld(sc, rng)
    w.st = st
    start, end = engine.day_number(sc["start"]), engine.day_number(sc["end"])
    start_year = engine.date_of(start).year
    w.start_year = start_year
    st.today = start
    res = Result()
    posts_by_year = bool(doc.get("posts_by_founding_year"))
    pressured, ready_since, shift, cooldown = set(), {}, {}, {}

    # Begin: what was dated before the start is history already.
    for b in beats:
        if b.date < 0 or b.date >= start or b.repeat or not all(r in st.done or r in st.flags for r in b.requires):
            continue
        for a in b.happens:
            if a.get("do") == "flag":
                st.flags.add(a["name"])
            elif a.get("do") == "unflag":
                st.flags.discard(a["name"])
            elif a.get("do") == "found_post":
                w.founded.add(a.get("place", ""))
        st.done.add(b.id)
        st.flags.add(b.id)
        st.done_day[b.id] = b.date
        res.history.append(b.id)

    def finish(b, how):
        st.done.add(b.id)
        st.flags.add(b.id)
        st.done_day[b.id] = st.today
        st.vars["count:" + b.id] = st.vars.get("count:" + b.id, 0.0) + 1
        res.fired.setdefault(b.id, []).append((engine.date_of(st.today).isoformat(), how))
        if b.repeat > 0:
            st.done.discard(b.id)
            pressured.discard(b.id)
            ready_since.pop(b.id, None)
            if b.date >= 0:
                shift[b.id] = shift.get(b.id, 0) + b.repeat
            else:
                cooldown[b.id] = st.today + b.repeat

    def date_of_beat(b):
        return -1 if b.date < 0 else b.date + shift.get(b.id, 0)

    for today in range(start, end + 1):
        st.today = today
        cal = engine.date_of(today)
        # the scenario's own weather: fixed events and dice
        for ev in sc.get("events", []):
            if engine.day_number(ev["on"]) == today:
                if "flag" in ev:
                    st.flags.add(ev["flag"])
                if "unflag" in ev:
                    st.flags.discard(ev["unflag"])
                if "convoy_days_late" in ev:
                    w.convoy_days_late[ev["run"]] = ev["convoy_days_late"]
        for rv in sc.get("random_events", []):
            lo, _, hi = str(rv.get("months", "1-12")).partition("-")
            lo, hi = int(lo), int(hi or lo)
            if lo <= cal.month <= hi and rng.random() < rv.get("per_year", 0.2) / max(1, (hi - lo + 1) * 30):
                st.flags.add(rv["flag"])
                if "clear_after_days" in rv:
                    w.flag_expiry.append((today + rv["clear_after_days"], rv["flag"]))
        for d, f in list(w.flag_expiry):
            if d <= today:
                st.flags.discard(f)
                w.flag_expiry.remove((d, f))

        if posts_by_year and cal.month == 1 and cal.day == 1:
            for n, y in w.world.founded.items():
                if y == cal.year and y > start_year and n not in w.founded:
                    w.founded.add(n)
                    res.founded.append((cal.isoformat(), n))

        for actions in engine.due(st):
            engine.run(actions, st, w)

        for b in beats:
            if b.id in st.done:
                continue
            if b.id in cooldown and today < cooldown[b.id]:
                continue
            if not all(r in st.done or r in st.flags for r in b.requires) or not engine.all_of(b.when, st, w):
                continue
            ready_since.setdefault(b.id, today)
            if b.done_when and engine.all_of(b.done_when, st, w):
                finish(b, "by the world")
                continue
            date = date_of_beat(b)
            due = date if date >= 0 else ready_since[b.id] + b.delay
            if today < due - b.lead:
                continue
            if level >= 1 and b.id not in pressured:
                pressured.add(b.id)
                engine.run(b.pressure, st, w)
            if today < due:
                continue
            if b.kind in ("fixed", "conditional"):
                engine.run(b.happens, st, w)
                finish(b, "on its day" if b.kind == "fixed" else "when its conditions held")
            elif b.kind == "pressure":
                if level == 0 or b.id in pressured:
                    finish(b, "as pressure")
            else:
                grace = max(b.window, 1)
                if level == 3 and today >= due + grace or level == 2 and today >= due + 2 * grace:
                    engine.run(b.happens, st, w)
                    finish(b, "forced")

        for _ in range(20):
            if not st.forced:
                break
            ids, st.forced = st.forced, []
            for i in ids:
                fb = next((x for x in beats if x.id == i), None)
                if fb and fb.id not in st.done:
                    engine.run(fb.happens, st, w)
                    finish(fb, "triggered")

    res.log = w.log
    res.flags = st.flags
    res.vars = st.vars
    res.beats = beats
    return res
