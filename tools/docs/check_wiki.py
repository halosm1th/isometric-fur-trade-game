#!/usr/bin/env python3
"""Lint the Obsidian-style wiki in WIKI/.

Checks, in order of how often they bite:
  links      [[Page]] / [[Page|text]] / [[Page#Heading]] that resolve to no page or alias
  images     ![[file.png]] that resolve to no file under WIKI/images
  frontmatter  missing title / status, or a status outside the project's vocabulary
  orphans    pages that nothing links to (warning only)

Usage:
  python3 tools/docs/check_wiki.py            # report, exit 1 if there are errors
  python3 tools/docs/check_wiki.py --strict   # warnings (orphans) also fail
  python3 tools/docs/check_wiki.py --quiet    # one summary line

Standard library only. Run from anywhere; paths are found relative to this file.
"""
import argparse
import re
import sys
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WIKI = ROOT / "WIKI"

STATUSES = {"implemented", "partial", "planned", "reference", "index", "stub"}
LINK = re.compile(r"(!?)\[\[([^\]\n]+)\]\]")
FENCE = re.compile(r"```.*?```", re.S)
INLINE = re.compile(r"`[^`\n]*`")


def norm(s: str) -> str:
    return re.sub(r"\s+", " ", s.strip()).lower()


def parse_frontmatter(text: str):
    if not text.startswith("---"):
        return {}, text
    end = text.find("\n---", 3)
    if end < 0:
        return {}, text
    fm = {}
    for line in text[3:end].splitlines():
        m = re.match(r"^([A-Za-z_]+):\s*(.*)$", line)
        if m:
            fm[m.group(1)] = m.group(2).strip()
    return fm, text[end + 4:]


def aliases_of(fm) -> list:
    raw = fm.get("aliases", "")
    if raw.startswith("["):
        return [a.strip().strip("'\"") for a in raw.strip("[]").split(",") if a.strip()]
    return []


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--strict", action="store_true")
    ap.add_argument("--quiet", action="store_true")
    args = ap.parse_args()

    pages = sorted(WIKI.rglob("*.md"))
    names = {}      # normalised name -> page path
    for p in pages:
        text = p.read_text(encoding="utf-8")
        fm, _ = parse_frontmatter(text)
        names[norm(p.stem)] = p
        for a in aliases_of(fm):
            names.setdefault(norm(a), p)
    images = {norm(f.name) for f in (WIKI / "images").rglob("*") if f.is_file()} if (WIKI / "images").exists() else set()

    errors, warnings = [], []
    inbound = defaultdict(int)

    for p in pages:
        rel = p.relative_to(ROOT)
        text = p.read_text(encoding="utf-8")
        fm, body = parse_frontmatter(text)
        if not fm:
            errors.append(f"{rel}: no frontmatter")
        else:
            if "title" not in fm:
                errors.append(f"{rel}: frontmatter has no title")
            st = fm.get("status", "").lower()
            if not st:
                errors.append(f"{rel}: frontmatter has no status")
            elif st not in STATUSES:
                errors.append(f"{rel}: status '{st}' is not one of {sorted(STATUSES)}")
        scan = INLINE.sub("", FENCE.sub("", body))
        for bang, inner in LINK.findall(scan):
            target = inner.split("|")[0].split("#")[0].strip().rstrip("\\").strip()
            if not target:
                continue
            if bang or re.search(r"\.(png|jpe?g|gif|svg|webp)$", target, re.I):
                if norm(Path(target).name) not in images:
                    errors.append(f"{rel}: missing image '{target}'")
                continue
            hit = names.get(norm(target))
            if hit is None:
                errors.append(f"{rel}: broken link [[{target}]]")
            elif hit != p:
                inbound[hit] += 1

    for p in pages:
        if inbound[p] == 0 and p.name not in ("Home.md",):
            warnings.append(f"{p.relative_to(ROOT)}: nothing links here")

    if not args.quiet:
        for e in errors:
            print("ERROR  ", e)
        for w in warnings:
            print("warning", w)
    print(f"{len(pages)} pages, {len(errors)} errors, {len(warnings)} warnings")
    return 1 if errors or (args.strict and warnings) else 0


if __name__ == "__main__":
    sys.exit(main())
