#!/usr/bin/env python3
"""Run tools/campaign/conformance.json against the Python engine.
The same file is run against the C# core by tests/CampaignTests."""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import engine  # noqa: E402


class FakeWorld(engine.World):
    def __init__(self, spec):
        self.metrics = spec.get("metrics", {})
        self.predicates = spec.get("predicates", {})
        self.effects = []

    @staticmethod
    def _key(name, args):
        return f"{name}({','.join(args)})"

    def metric(self, name, args):
        for k in (self._key(name, args), name):
            if k in self.metrics:
                return float(self.metrics[k])
        return float("nan")

    def predicate(self, name, args):
        for k in (self._key(name, args), name):
            if k in self.predicates:
                return bool(self.predicates[k])
        return False

    def effect(self, name, action):
        self.effects.append({k: v for k, v in action.items() if not k.startswith("_")})


def make_state(spec):
    st = engine.State(engine.day_number(spec["today"]), spec.get("seed", 20240611))
    st.flags = set(spec.get("flags", []))
    st.vars = {k: float(v) for k, v in spec.get("vars", {}).items()}
    st.done = set(spec.get("done", []))
    st.done_day = {k: engine.day_number(v) for k, v in spec.get("doneDay", {}).items()}
    st.timers = [(engine.day_number(t["on"]), t["actions"]) for t in spec.get("timers", [])]
    return st


def check(expect, st, w):
    errs = []
    if "flags" in expect and sorted(st.flags) != sorted(expect["flags"]):
        errs.append(f"flags {sorted(st.flags)} != {sorted(expect['flags'])}")
    if "vars" in expect:
        for k, v in expect["vars"].items():
            if abs(st.vars.get(k, 0.0) - v) > 1e-9:
                errs.append(f"var {k} = {st.vars.get(k, 0.0)}, wanted {v}")
    if "effects" in expect and w.effects != expect["effects"]:
        errs.append(f"effects {w.effects} != {expect['effects']}")
    if "forced" in expect and st.forced != expect["forced"]:
        errs.append(f"forced {st.forced} != {expect['forced']}")
    if "seed" in expect and st.seed != expect["seed"]:
        errs.append(f"seed {st.seed} != {expect['seed']}")
    if "timers" in expect:
        got = [{"on": engine.date_of(d).isoformat(), "count": len(a)} for d, a in st.timers]
        if got != expect["timers"]:
            errs.append(f"timers {got} != {expect['timers']}")
    return errs


def run_case(c):
    st = make_state(c["state"])
    w = FakeWorld(c["world"])
    if c["kind"] == "cond":
        got = engine.eval_cond(c["cond"], st, w)
        return [] if got == c["expect"] else [f"got {got}, wanted {c['expect']}"]
    if c["kind"] == "run":
        engine.run(c["actions"], st, w)
        return check(c["expect"], st, w)
    if c["kind"] == "due":
        for actions in engine.due(st):
            engine.run(actions, st, w)
        return check(c["expect"], st, w)
    return [f"unknown kind {c['kind']}"]


def main():
    cases = json.load(open(os.path.join(HERE, "conformance.json")))["cases"]
    bad = 0
    for c in cases:
        errs = run_case(c)
        if errs:
            bad += 1
            print(f"FAIL {c['name']}: " + "; ".join(errs))
    print(f"{len(cases) - bad}/{len(cases)} conformance cases pass (python)")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
