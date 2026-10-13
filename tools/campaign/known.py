"""What the game knows: the vocabulary of the language, and the world a script
may name (posts, brigade runs, items). Read from the repo's own data so it
cannot drift from it."""
import json
import os
import re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))

# action -> (required fields, optional fields); the language owns the first
# eight, the world carries out the rest (Scripts/Core/Campaign.cs, GodotWorld).
ACTIONS = {
    "flag": (["name"], []),
    "unflag": (["name"], []),
    "set": (["var", "to"], []),
    "add": (["var"], ["by"]),
    "if": (["cond"], ["then", "else"]),
    "random": (["options"], []),
    "after": (["days", "then"], []),
    "trigger": (["beat"], []),
    "tension": (["add"], ["why"]),
    "london": ([], ["price", "quota"]),
    "found_post": (["place"], []),
    "news": ([], ["from", "to", "subject", "text", "days", "company"]),
    "brigade": (["run"], ["effect", "days", "leg"]),
    "stock": (["post", "item", "add"], []),
    "say": (["text"], ["to", "headline"]),
}
BRIGADE_EFFECTS = ("delay", "turn_back", "lose", "cancel", "launch")
# facts (no number) and numbers; the engine raises flags for the convoy_* ones.
PREDICATES = {"flag", "beat", "after", "before", "chance", "month", "post", "dead", "player_faction", "season",
              "convoy_late", "convoy_lost", "convoy_robbed", "convoy_turned_back", "convoy_out", "convoy_in", "convoy_wintering"}
METRICS = {"var", "month", "year", "day", "day_of_year", "count", "beat_age", "tension", "standing", "stock", "furs",
           "convoys_active", "convoy_days_late", "convoy_days_out"}
ADDRESSEES = ("the Factor at York Factory", "the postmaster at Oxford House", "the wintering partner", "the chief of the band")
COMPANIES = ("HBC", "NWC")
# Flags the engine itself raises: reading them is fine even though no script sets them.
ENGINE_FLAG_PREFIXES = ("heard:", "late:", "lost:", "robbed:", "turned:", "out:", "in:")
ENGINE_FLAGS = {"brigade_late", "brigade_lost", "brigade_robbed", "brigade_turned_back", "brigade_in"}


def _load(path):
    with open(os.path.join(ROOT, path), encoding="utf-8") as f:
        return json.load(f)


class World:
    def __init__(self):
        w = _load("Data/World/world.json")
        self.places = {p["name"]: p for p in w["places"]}
        self.posts = {n for n, p in self.places.items() if p.get("kind") == "post"}
        self.founded = {n: p.get("year", 0) for n, p in self.places.items() if p.get("kind") == "post"}
        self.ledgers = set(self.posts) | {"York Factory", "Oxford House"}
        runs = _load("Data/World/brigade_runs.json")["runs"]
        self.runs = {r["name"]: r for r in runs}
        with open(os.path.join(ROOT, "Scripts/Items/Items.cs"), encoding="utf-8") as f:
            src = f.read()
        body = src[src.index("enum ItemId"):]
        body = body[body.index("{") + 1:body.index("}")]
        body = re.sub(r"//[^\n]*", "", body)
        self.items = {t.strip().split("=")[0].strip() for t in body.split(",") if t.strip()}


_world = None


def world():
    global _world
    if _world is None:
        _world = World()
    return _world
