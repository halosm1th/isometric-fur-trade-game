#!/usr/bin/env python3
import warnings; warnings.simplefilter("ignore", ResourceWarning)
"""python3 tools/campaign/test_campaign.py   (also: python3 -m unittest discover tools/campaign)"""
import json
import os
import re
import sys
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import check  # noqa: E402
import engine  # noqa: E402
import known  # noqa: E402
import lang  # noqa: E402
import run_conformance  # noqa: E402
import sim  # noqa: E402

ROOT = known.ROOT


def norm(x):
    if isinstance(x, dict):
        return {k: norm(v) for k, v in sorted(x.items())}
    if isinstance(x, list):
        return [norm(v) for v in x]
    if isinstance(x, float) and x == int(x):
        return int(x)
    return x


def compile_beat(body, head="beat b event 1816-06-19"):
    return lang.compile_text(f'campaign "t"\n{head}\n' + "\n".join("  " + l for l in body.strip("\n").split("\n")) + "\n")["beats"][0]


class Conformance(unittest.TestCase):
    def test_every_case(self):
        for c in json.load(open(os.path.join(HERE, "conformance.json")))["cases"]:
            with self.subTest(c["name"]):
                self.assertEqual(run_conformance.run_case(c), [])


class RoundTrip(unittest.TestCase):
    def test_existing_campaigns_survive(self):
        for name in ("historical", "Gemini and Clark"):
            with self.subTest(name):
                d = json.load(open(os.path.join(ROOT, "Campaigns", name + ".json")))
                self.assertEqual(norm(lang.compile_text(lang.decompile(d))), norm(d))

    def test_new_features_survive(self):
        src = open(os.path.join(HERE, "examples", "showcase.camp")).read()
        d = lang.compile_text(src)
        self.assertEqual(norm(lang.compile_text(lang.decompile(d))), norm(d))


class Conditions(unittest.TestCase):
    def cond(self, text):
        return lang.parse_condition(text)

    def test_compact_forms_stay_compact(self):
        self.assertEqual(self.cond("flag(x)"), "flag:x")
        self.assertEqual(self.cond("tension >= 40"), "tension>=40")
        self.assertEqual(self.cond("post(\"Fort William\")"), "post:Fort William")
        self.assertEqual(self.cond("convoy_late(\"the York boats\")"), "convoy_late:the York boats")
        self.assertEqual(self.cond("$hostility >= 3"), "var(hostility) >= 3")
        self.assertEqual(self.cond("stock(\"York Factory\", Rum) < 10"), {"m": "stock", "a": ["York Factory", "Rum"], "op": "<", "v": 10})

    def test_logic_and_precedence(self):
        self.assertEqual(self.cond("flag(a) or flag(b) and flag(c)"),
                         {"any": ["flag:a", {"all": ["flag:b", "flag:c"]}]})
        self.assertEqual(self.cond("(flag(a) or flag(b)) and not flag(c)"),
                         {"all": [{"any": ["flag:a", "flag:b"]}, {"not": "flag:c"}]})
        self.assertEqual(self.cond("not flag(a)"), {"not": "flag:a"})

    def test_a_bare_word_is_a_flag(self):
        self.assertEqual(self.cond("pemmican-proclamation"), "pemmican-proclamation")

    def test_conditions_mean_the_same_after_a_round_trip(self):
        for t in ("flag(a) or flag(b) and tension >= 40", "not (month(6-8) or chance(10))", "$x < 2 and stock(\"York Factory\", Rum) > 5"):
            with self.subTest(t):
                c = self.cond(t)
                self.assertEqual(norm(self.cond(lang.condition_text(c))), norm(c))

    def test_errors_name_the_problem(self):
        with self.assertRaises(lang.CampError):
            self.cond("flag(a) and")
        with self.assertRaises(lang.CampError):
            self.cond("tension >=")


class Actions(unittest.TestCase):
    def test_blocks(self):
        b = compile_beat("""
happens
  if tension >= 50:
    flag hot
  elif tension >= 20:
    flag warm
  else:
    flag cool
""")
        a = b["happens"][0]
        self.assertEqual(a["do"], "if")
        self.assertEqual(a["then"], [{"do": "flag", "name": "hot"}])
        self.assertEqual(a["else"][0]["cond"], "tension>=20")
        self.assertEqual(a["else"][0]["else"], [{"do": "flag", "name": "cool"}])

    def test_random_after_set_add(self):
        b = compile_beat("""
happens
  random:
    30%:
      flag lucky
    70%:
      flag unlucky
  after 10 days:
    flag later
  set mood = 2
  add mood += 3
  add count
  add mood -= 1
""")
        h = b["happens"]
        self.assertEqual(h[0]["options"], [{"weight": 30, "do": [{"do": "flag", "name": "lucky"}]}, {"weight": 70, "do": [{"do": "flag", "name": "unlucky"}]}])
        self.assertEqual(h[1], {"do": "after", "days": 10, "then": [{"do": "flag", "name": "later"}]})
        self.assertEqual(h[2], {"do": "set", "var": "mood", "to": 2})
        self.assertEqual(h[3], {"do": "add", "var": "mood", "by": 3})
        self.assertEqual(h[4], {"do": "add", "var": "count"})
        self.assertEqual(h[5], {"do": "add", "var": "mood", "by": -1})

    def test_world_actions(self):
        b = compile_beat("""
happens
  tension +15 "Seven Oaks"
  tension -5
  london price=0.8 quota=0.9
  brigade "the York boats" turn_back
  brigade "the York boats" delay days=5
  stock "York Factory" Rum -10
  say "A line." to=HBC headline
  trigger seven-oaks
  found_post "Fort William"
""")
        h = b["happens"]
        self.assertEqual(h[0], {"do": "tension", "add": 15, "why": "Seven Oaks"})
        self.assertEqual(h[1], {"do": "tension", "add": -5})
        self.assertEqual(h[2], {"do": "london", "price": 0.8, "quota": 0.9})
        self.assertEqual(h[3], {"do": "brigade", "run": "the York boats", "effect": "turn_back"})
        self.assertEqual(h[4], {"do": "brigade", "run": "the York boats", "effect": "delay", "days": 5})
        self.assertEqual(h[5], {"do": "stock", "post": "York Factory", "item": "Rum", "add": -10})
        self.assertEqual(h[6], {"do": "say", "text": "A line.", "to": "HBC", "headline": True})
        self.assertEqual(h[7], {"do": "trigger", "beat": "seven-oaks"})
        self.assertEqual(h[8], {"do": "found_post", "place": "Fort William"})

    def test_news_with_a_long_text_and_a_short_addressee(self):
        b = compile_beat('''
happens
  news from="Fort Douglas" to=factor days=40 company=HBC
    subject "Blood on the plain"
    text """
    Line one.
      Indented line two.
    """
''')
        n = b["happens"][0]
        self.assertEqual(n["to"], "the Factor at York Factory")
        self.assertEqual(n["text"], "Line one.\n  Indented line two.")
        self.assertEqual(n["days"], 40)

    def test_comments_and_hashes_in_strings(self):
        b = compile_beat('happens\n  say "issue #4 # not a comment"   # but this is\n')
        self.assertEqual(b["happens"][0]["text"], "issue #4 # not a comment")

    def test_else_without_if_is_an_error(self):
        with self.assertRaises(lang.CampError):
            compile_beat("happens\n  else:\n    flag x\n")

    def test_bad_lines_say_which_line(self):
        with self.assertRaises(lang.CampError) as cm:
            lang.compile_text('campaign "t"\nbeat b event 1816-06-19\n  window\n')
        self.assertIn("line", str(cm.exception))


class Macros(unittest.TestCase):
    def test_macro_and_loop(self):
        d = lang.compile_text('''campaign "t"
macro dispatch(to, subject, days=40):
  news from="Montreal" to={{to}} subject={{subject}} days={{days}}

for y in 1818, 1819:
  beat war-{{y}} pressure {{y}}-06-01
    happens
      use dispatch to=factor subject="War {{y}}"

beat other event 1820-01-01
  happens
    use dispatch to=partner subject="Peace" days=60
''')
        ids = [b["id"] for b in d["beats"]]
        self.assertEqual(ids, ["war-1818", "war-1819", "other"])
        self.assertEqual(d["beats"][0]["happens"][0]["subject"], "War 1818")
        self.assertEqual(d["beats"][0]["happens"][0]["days"], 40)
        self.assertEqual(d["beats"][2]["happens"][0]["to"], "the wintering partner")
        self.assertEqual(d["beats"][2]["happens"][0]["days"], 60)

    def test_macro_with_a_block(self):
        d = lang.compile_text('''campaign "t"
macro when_late(run):
  if convoy_late({{run}}):
    {{block}}

beat b event 1820-01-01
  happens
    use when_late "the York boats"
      flag late
      tension +5
''')
        a = d["beats"][0]["happens"][0]
        self.assertEqual(a["cond"], "convoy_late:the York boats")
        self.assertEqual([x["do"] for x in a["then"]], ["flag", "tension"])

    def test_a_macro_that_calls_itself_stops(self):
        with self.assertRaises(lang.CampError):
            lang.compile_text('campaign "t"\nmacro a():\n  use a\nuse a\n')


class Checker(unittest.TestCase):
    def doc(self, *beats):
        return {"title": "t", "beats": list(beats)}

    def messages(self, doc):
        r = check.check(doc)
        return [m for _, m in r.errors], [m for _, m in r.warnings]

    def test_clean(self):
        e, w = self.messages(self.doc({"id": "a", "kind": "fixed", "date": "1812-01-01", "happens": [{"do": "flag", "name": "x"}]},
                                      {"id": "b", "kind": "event", "date": "1813-01-01", "requires": ["a"], "when": ["flag:x"]}))
        self.assertEqual((e, w), ([], []))

    def test_catches_mistakes(self):
        cases = [
            ({"id": "a", "kind": "fixed", "happens": []}, "needs a date"),
            ({"id": "a", "kind": "fixed", "date": "1812-02-30"}, "not a real day"),
            ({"id": "a", "kind": "bogus", "date": "1812-01-01"}, "unknown kind"),
            ({"id": "a", "kind": "event", "date": "1812-01-01", "requires": ["ghost"]}, "requires 'ghost'"),
            ({"id": "a", "kind": "event", "date": "1812-01-01", "happens": [{"do": "teleport"}]}, "unknown action"),
            ({"id": "a", "kind": "event", "date": "1812-01-01", "happens": [{"do": "flag"}]}, "needs 'name'"),
            ({"id": "a", "kind": "event", "date": "1812-01-01", "happens": [{"do": "tension", "add": 5, "wat": 1}]}, "no field 'wat'"),
            ({"id": "a", "kind": "event", "date": "1812-01-01", "happens": [{"do": "trigger", "beat": "ghost"}]}, "unknown beat"),
            ({"id": "a", "kind": "event", "date": "1812-01-01", "happens": [{"do": "found_post", "place": "Nowhere"}]}, "not a post"),
            ({"id": "a", "kind": "event", "date": "1812-01-01", "happens": [{"do": "brigade", "run": "the Phantom boats"}]}, "not a brigade run"),
            ({"id": "a", "kind": "event", "date": "1812-01-01", "happens": [{"do": "brigade", "run": "the York boats", "effect": "sink"}]}, "is not one of"),
            ({"id": "a", "kind": "event", "date": "1812-01-01", "happens": [{"do": "stock", "post": "York Factory", "item": "Gold", "add": 1}]}, "not an item"),
            ({"id": "a", "kind": "event", "date": "1812-01-01", "when": ["telepathy:yes"]}, "unknown condition"),
            ({"id": "a", "kind": "event", "date": "1812-01-01", "when": ["wealth>5"]}, "unknown number"),
            ({"id": "a", "kind": "event", "date": "1812-01-01", "when": ["convoy_late:the Phantom boats"]}, "not a brigade run"),
            ({"id": "a", "kind": "event", "date": "1812-01-01", "when": ["post:Atlantis"]}, "not on the map"),
            ({"id": "a", "kind": "event", "date": "1812-01-01", "when": [{"m": "stock", "a": ["York Factory"], "op": "<", "v": 3}]}, "stock(POST, ITEM)"),
            ({"id": "a", "kind": "event", "date": "1812-01-01", "happens": [{"do": "random", "options": []}]}, "options"),
            ({"id": "a", "kind": "event", "date": "1812-01-01", "happens": [{"do": "news", "company": "XYZ"}]}, "company"),
        ]
        for beat, expect in cases:
            with self.subTest(expect):
                e, w = self.messages(self.doc(beat))
                self.assertTrue(any(expect in m for m in e), (expect, e))

    def test_duplicates_and_cycles(self):
        e, _ = self.messages(self.doc({"id": "a", "kind": "event", "date": "1812-01-01"}, {"id": "a", "kind": "event", "date": "1812-01-01"}))
        self.assertTrue(any("duplicate" in m for m in e))
        e, _ = self.messages(self.doc({"id": "a", "kind": "event", "date": "1812-01-01", "requires": ["b"]}, {"id": "b", "kind": "event", "date": "1812-01-01", "requires": ["a"]}))
        self.assertTrue(any("cycle" in m for m in e))

    def test_warnings(self):
        _, w = self.messages(self.doc({"id": "a", "kind": "event", "date": "1812-01-01", "when": ["flag:typo"]}))
        self.assertTrue(any("nothing sets" in m for m in w))
        _, w = self.messages(self.doc({"id": "a", "kind": "event", "date": "1811-01-01"}, {"id": "b", "kind": "event", "date": "1810-01-01", "requires": ["a"]}))
        self.assertTrue(any("will wait" in m for m in w))
        _, w = self.messages(self.doc({"id": "a", "kind": "event", "date": "1811-01-01", "when": ["var(never_set) >= 1"]}))
        self.assertTrue(any("always be 0" in m for m in w))

    def test_unreachable_chain(self):
        _, w = self.messages(self.doc({"id": "a", "kind": "event", "date": "1812-01-01", "requires": ["ghost"]}, {"id": "b", "kind": "event", "date": "1813-01-01", "requires": ["a"]}))
        self.assertTrue(any("can never happen" in m for m in w))

    def test_engine_flags_are_not_typos(self):
        e, w = self.messages(self.doc({"id": "a", "kind": "conditional", "when": ["brigade_late", "late:the York boats", "heard:x"]}))
        self.assertEqual((e, w), ([], []))

    def test_both_real_campaigns_are_clean(self):
        for name in ("historical", "Gemini and Clark"):
            r = check.check(json.load(open(os.path.join(ROOT, "Campaigns", name + ".json"))))
            self.assertEqual(r.errors, [], name)


class Simulation(unittest.TestCase):
    def doc(self, *beats, **extra):
        return dict({"title": "t", "beats": list(beats)}, **extra)

    def test_fixed_beat_happens_on_its_day(self):
        r = sim.simulate(self.doc({"id": "a", "kind": "fixed", "date": "1815-03-04", "happens": [{"do": "flag", "name": "x"}]}), {"start": "1815-01-01", "end": "1816-01-01"})
        self.assertEqual(r.fired["a"], [("1815-03-04", "on its day")])
        self.assertIn("x", r.flags)

    def test_history_before_the_start_is_applied_not_fired(self):
        r = sim.simulate(self.doc({"id": "a", "kind": "fixed", "date": "1810-03-04", "happens": [{"do": "flag", "name": "x"}]}), {"start": "1811-01-01", "end": "1812-01-01"})
        self.assertNotIn("a", r.fired)
        self.assertIn("a", r.history)
        self.assertIn("x", r.flags)

    def event_doc(self):
        return self.doc({"id": "a", "kind": "event", "date": "1815-01-01", "window_days": 30, "happens": [{"do": "flag", "name": "x"}]})

    def test_adherence_decides_whether_an_event_is_forced(self):
        sc = {"start": "1814-06-01", "end": "1817-01-01"}
        self.assertNotIn("a", sim.simulate(self.event_doc(), sc, level="Steady").fired)
        self.assertEqual(sim.simulate(self.event_doc(), sc, level="Strict").fired["a"][0], ("1815-01-31", "forced"))
        self.assertEqual(sim.simulate(self.event_doc(), sc, level="Firm").fired["a"][0], ("1815-03-02", "forced"))

    def test_the_world_can_get_there_first(self):
        d = self.doc({"id": "a", "kind": "event", "date": "1815-01-01", "done_when": ["flag:world_did_it"]})
        r = sim.simulate(d, {"start": "1814-06-01", "end": "1817-01-01", "events": [{"on": "1814-12-01", "flag": "world_did_it"}]}, level="Strict")
        self.assertEqual(r.fired["a"][0], ("1814-12-01", "by the world"))

    def test_conditional_waits_for_its_conditions(self):
        d = self.doc({"id": "a", "kind": "conditional", "when": ["tension>=50"], "delay_days": 5, "happens": [{"do": "flag", "name": "x"}]})
        r = sim.simulate(d, {"start": "1814-01-01", "end": "1815-01-01", "tension": {"1814-01-01": 20, "1814-06-01": 60}})
        self.assertEqual(r.fired["a"][0], ("1814-06-06", "when its conditions held"))

    def test_actions_move_tension_and_beats_see_it(self):
        d = self.doc({"id": "a", "kind": "fixed", "date": "1814-02-01", "happens": [{"do": "tension", "add": 40, "why": "x"}]},
                     {"id": "b", "kind": "conditional", "requires": ["a"], "when": ["tension>=50"], "happens": []})
        r = sim.simulate(d, {"start": "1814-01-01", "end": "1815-01-01"})
        self.assertIn("b", r.fired)

    def test_timers_trigger_and_repeat(self):
        d = self.doc({"id": "a", "kind": "fixed", "date": "1814-02-01", "happens": [{"do": "after", "days": 10, "then": [{"do": "trigger", "beat": "t"}]}]},
                     {"id": "t", "kind": "pressure", "date": "2000-01-01", "happens": [{"do": "flag", "name": "tripped"}]},
                     {"id": "r", "kind": "fixed", "date": "1814-03-01", "repeat_days": 365, "happens": []})
        r = sim.simulate(d, {"start": "1814-01-01", "end": "1817-01-01"})
        self.assertEqual(r.fired["t"][0], ("1814-02-11", "triggered"))
        self.assertEqual([x[0] for x in r.fired["r"]], ["1814-03-01", "1815-03-01", "1816-02-29"])   # 365 days is not a calendar year in a leap year

    def test_random_is_repeatable_from_a_seed(self):
        d = self.doc({"id": "a", "kind": "fixed", "date": "1814-02-01", "happens": [
            {"do": "random", "options": [{"weight": 1, "do": [{"do": "flag", "name": "h"}]}, {"weight": 1, "do": [{"do": "flag", "name": "t"}]}]}]})
        sc = {"start": "1814-01-01", "end": "1814-12-31"}
        flags = {tuple(sorted(sim.simulate(d, sc, seed=20240611 + s * 7919).flags - {"a"})) for s in range(12)}
        self.assertEqual(flags, {("h",), ("t",)})
        self.assertEqual(sim.simulate(d, sc, seed=5).flags, sim.simulate(d, sc, seed=5).flags)

    def test_the_real_historical_script_plays(self):
        d = json.load(open(os.path.join(ROOT, "Campaigns", "historical.json")))
        r = sim.simulate(d, level="Strict")
        for bid in ("war-of-1812", "astoria-sold", "pemmican-proclamation", "seven-oaks", "merger"):
            self.assertIn(bid, r.fired, bid)


class Drift(unittest.TestCase):
    """The tool's idea of the language must match the game's."""

    def read(self, path):
        with open(os.path.join(ROOT, path), encoding="utf-8") as f:
            return f.read()

    def test_every_action_is_known_to_the_game(self):
        game = self.read("Scripts/Core/CampaignLogic.cs") + self.read("Scripts/Core/Campaign.cs")
        for name in known.ACTIONS:
            self.assertRegex(game, rf'"{name}"', f"action {name} is in the tool but not in the game")

    def test_every_condition_and_number_is_known_to_the_game(self):
        game = self.read("Scripts/Core/CampaignLogic.cs") + self.read("Scripts/Core/Campaign.cs")
        for name in known.PREDICATES | known.METRICS:
            self.assertRegex(game, rf'"{name}"', f"{name} is in the tool but not in the game")

    def test_every_brigade_effect_is_known_to_the_game(self):
        game = self.read("Scripts/World/Convoys.cs")
        for name in known.BRIGADE_EFFECTS:
            self.assertRegex(game, rf'"{name}"')

    def test_the_game_has_no_action_the_tool_has_not_heard_of(self):
        # case "x": labels in the world's Effect(), and in the language's own RunOne()
        game = self.read("Scripts/Core/Campaign.cs") + self.read("Scripts/Core/CampaignLogic.cs")
        effect = self.read("Scripts/Core/Campaign.cs")
        effect = effect[effect.index("public void Effect("):]
        run_one = game[game.index("private static void RunOne("):game.index("// Timers that have come due")]
        for text in (effect, run_one):
            for name in re.findall(r'case "([a-z_]+)":', text):
                self.assertIn(name, known.ACTIONS, f"the game runs '{name}' and the tool does not know it")


if __name__ == "__main__":
    unittest.main()
