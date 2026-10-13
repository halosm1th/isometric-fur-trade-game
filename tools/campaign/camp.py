#!/usr/bin/env python3
"""Campaign scripts: write them in the compact language, check them, draw them, play them.

  camp.py compile  story.camp [-o story.json]   .camp -> the JSON the game reads (checks it first)
  camp.py decompile story.json [-o story.camp]  the other way, to move an old script across
  camp.py check    story.camp|story.json        errors and warnings, no output file
  camp.py graph    story.camp|story.json        the beats and what leads to what (mermaid, or --dot)
  camp.py sim      story.camp|story.json        play it forward; --runs N for how often each beat happens
  camp.py install  story.camp                   compile into Campaigns/ where the game finds it
  camp.py build                                 compile every tools/campaign/src/*.camp into Campaigns/

See lang.py for the language, sim.py for scenarios.
"""
import argparse
import collections
import json
import os
import statistics
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import check as checker  # noqa: E402
import engine  # noqa: E402
import lang  # noqa: E402
import sim  # noqa: E402

CAMPAIGNS = os.path.abspath(os.path.join(HERE, "..", "..", "Campaigns"))


def load(path):
    with open(path, encoding="utf-8") as f:
        text = f.read()
    if path.endswith(".json"):
        return json.loads(text)
    try:
        return lang.compile_text(text)
    except lang.CampError as e:
        sys.exit(f"{path}: {e}")


def report(doc, path, quiet=False, verbose=False):
    rep = checker.check(doc)
    for line in rep.lines(verbose):
        print(f"{os.path.basename(path)}: {line}", file=sys.stderr)
    if not quiet:
        print(f"{os.path.basename(path)}: {len(doc.get('beats', []))} beats, {len(rep.errors)} errors, {len(rep.warnings)} warnings", file=sys.stderr)
    return rep


def write_json(doc, out):
    with open(out, "w", encoding="utf-8") as f:
        json.dump(doc, f, indent=2, ensure_ascii=False)
        f.write("\n")


def cmd_compile(a):
    doc = load(a.file)
    rep = report(doc, a.file)
    if rep.errors and not a.force:
        sys.exit("not written: fix the errors (or --force)")
    out = a.output or os.path.splitext(a.file)[0] + ".json"
    write_json(doc, out)
    print(f"wrote {out}")


def cmd_decompile(a):
    doc = load(a.file)
    out = a.output or os.path.splitext(a.file)[0] + ".camp"
    with open(out, "w", encoding="utf-8") as f:
        f.write(lang.decompile(doc))
    print(f"wrote {out}")


def cmd_check(a):
    rep = report(load(a.file), a.file, verbose=a.verbose)
    sys.exit(1 if rep.errors or (a.strict and rep.warnings) else 0)


def cmd_install(a):
    doc = load(a.file)
    rep = report(doc, a.file)
    if rep.errors:
        sys.exit("not installed: fix the errors")
    out = os.path.join(CAMPAIGNS, os.path.splitext(os.path.basename(a.file))[0] + ".json")
    write_json(doc, out)
    print(f"installed {out}")


def cmd_build(a):
    src = os.path.join(HERE, "src")
    bad = 0
    for name in sorted(os.listdir(src)):
        if not name.endswith(".camp"):
            continue
        path = os.path.join(src, name)
        doc = load(path)
        rep = report(doc, path, quiet=True)
        if rep.errors:
            bad += 1
            continue
        write_json(doc, os.path.join(CAMPAIGNS, name[:-5] + ".json"))
        print(f"built Campaigns/{name[:-5]}.json from src/{name}")
    sys.exit(1 if bad else 0)


def graph_edges(doc):
    beats = {b["id"]: b for b in doc.get("beats", [])}
    edges = []
    facts = checker.Facts()
    sets, reads = collections.defaultdict(set), collections.defaultdict(set)
    for b in beats.values():
        for r in checker._aslist(b.get("requires")):
            edges.append((r, b["id"], "requires"))
            reads[r].add(b["id"])
        for key in ("when", "done_when"):
            for c in checker._aslist(b.get(key)):
                for kind, name, args, _, _ in checker.condition_atoms(c):
                    if kind == "p" and name in ("flag", "beat") and args:
                        reads[args[0]].add(b["id"])
        _walk(b.get("pressure"), b["id"], sets, edges)
        _walk(b.get("happens"), b["id"], sets, edges)
    for flag, setters in sets.items():
        for s in setters:
            for r in reads.get(flag, ()):
                if r != s and (s, r, "requires") not in edges:
                    edges.append((s, r, f"flag {flag}"))
    return beats, edges


def _walk(acts, bid, sets, edges):
    for a in checker._aslist(acts):
        if not isinstance(a, dict):
            continue
        if a.get("do") == "flag" and "name" in a:
            sets[a["name"]].add(bid)
        if a.get("do") == "trigger":
            edges.append((bid, a.get("beat"), "triggers"))
        for k in ("then", "else"):
            _walk(a.get(k), bid, sets, edges)
        for o in a.get("options") or []:
            _walk(o.get("do"), bid, sets, edges)


def cmd_graph(a):
    doc = load(a.file)
    beats, edges = graph_edges(doc)
    ident = lambda s: "n_" + "".join(c if c.isalnum() else "_" for c in s)
    if a.dot:
        out = ["digraph campaign {", "  rankdir=LR; node [shape=box, style=rounded];"]
        for b in beats.values():
            out.append(f'  {ident(b["id"])} [label="{b["id"]}\\n{b.get("date", b.get("kind", ""))}"];')
        for s, t, label in edges:
            style = {"requires": "", "triggers": " style=dashed"}.get(label, " style=dotted")
            out.append(f'  {ident(s)} -> {ident(t)} [label="{label}"{style}];')
        out.append("}")
    else:
        out = ["flowchart LR"]
        for b in beats.values():
            when = b.get("date") or b.get("kind", "")
            shape = {"fixed": '(["{}"])', "pressure": '>"{}"]', "conditional": '{{"{}"}}'}.get(b.get("kind"), '["{}"]')
            out.append(f'  {ident(b["id"])}{shape.format(b["id"] + "<br/>" + when)}')
        for s, t, label in edges:
            arrow = {"requires": "-->", "triggers": "-.->"}.get(label, "-.->")
            lab = "" if label == "requires" else f'|{label}|'
            out.append(f"  {ident(s)} {arrow}{lab} {ident(t)}")
    print("\n".join(out))


def cmd_sim(a):
    doc = load(a.file)
    scenario = None
    if a.scenario:
        with open(a.scenario, encoding="utf-8") as f:
            scenario = json.load(f)
    if a.start:
        scenario = dict(scenario or {}, start=a.start)
    if a.end:
        scenario = dict(scenario or {}, end=a.end)
    if a.runs > 1:
        return monte_carlo(doc, scenario, a)
    res = sim.simulate(doc, scenario, seed=a.seed, level=a.adherence)
    sc = dict(sim.default_scenario(), **(scenario or {}))
    print(f"{sc['start']} .. {sc['end']}  {a.adherence or sc['adherence']}  seed {a.seed if a.seed is not None else doc.get('seed', 20240611)}")
    events = []
    for bid, hits in res.fired.items():
        b = next(x for x in res.beats if x.id == bid)
        for date, how in hits:
            events.append((date, f"{bid:28s} {how:24s} {b.title}"))
    for date, name in res.founded:
        events.append((date, f"{'(post founded)':28s} {name}"))
    for date, line in sorted(events):
        print(f"{date}  {line}")
    if res.history:
        print(f"\nalready history at the start: {', '.join(res.history)}")
    never = [b.id for b in res.beats if b.id not in res.fired and b.id not in res.history]
    print(f"\nnever happened: {', '.join(never) if never else 'none'}")
    if a.log:
        print("\nwhat they did:")
        for date, bid, text in res.log:
            print(f"{date}  {bid:24s} {text}")
    if a.state:
        print("\nflags:", ", ".join(sorted(res.flags)))
        print("variables:", {k: v for k, v in sorted(res.vars.items()) if not k.startswith("count:")})


def monte_carlo(doc, scenario, a):
    seen = collections.defaultdict(list)
    forced = collections.Counter()
    for n in range(a.runs):
        res = sim.simulate(doc, scenario, seed=(a.seed or 1) + n * 7919, level=a.adherence)
        for bid, hits in res.fired.items():
            date, how = hits[0]
            seen[bid].append(engine.day_number(date))
            if how in ("forced", "triggered"):
                forced[bid] += 1
    print(f"{a.runs} runs, {a.adherence or 'script adherence'}")
    print(f"{'beat':30s} {'fired':>6s}  {'forced':>6s}  earliest / median / latest")
    for b in doc.get("beats", []):
        days = seen.get(b["id"], [])
        if not days:
            print(f"{b['id']:30s} {'0%':>6s}  {'':6s}  never")
            continue
        iso = lambda d: engine.date_of(int(d)).isoformat()
        print(f"{b['id']:30s} {100 * len(days) // a.runs:5d}%  {100 * forced[b['id']] // len(days):5d}%  {iso(min(days))} / {iso(statistics.median(days))} / {iso(max(days))}")


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    for name, fn in (("compile", cmd_compile), ("decompile", cmd_decompile), ("check", cmd_check), ("install", cmd_install), ("graph", cmd_graph), ("sim", cmd_sim)):
        p = sub.add_parser(name)
        p.add_argument("file")
        p.set_defaults(fn=fn)
        if name in ("compile", "decompile"):
            p.add_argument("-o", "--output")
        if name == "compile":
            p.add_argument("--force", action="store_true", help="write even with errors")
        if name == "check":
            p.add_argument("--strict", action="store_true", help="warnings fail too")
            p.add_argument("-v", "--verbose", action="store_true", help="also print notes")
        if name == "graph":
            p.add_argument("--dot", action="store_true", help="graphviz instead of mermaid")
        if name == "sim":
            p.add_argument("--scenario", help="JSON: what the world does (see sim.py)")
            p.add_argument("--start")
            p.add_argument("--end")
            p.add_argument("--runs", type=int, default=1, help="repeat with different dice and report how often each beat happens")
            p.add_argument("--seed", type=int)
            p.add_argument("--adherence", choices=sorted(sim.LEVELS))
            p.add_argument("--log", action="store_true", help="print what each beat did")
            p.add_argument("--state", action="store_true", help="print the final flags and variables")
    pb = sub.add_parser("build")
    pb.set_defaults(fn=cmd_build)
    a = ap.parse_args(argv)
    try:
        a.fn(a)
    except lang.CampError as e:
        sys.exit(f"error: {e}")


if __name__ == "__main__":
    main()
