#!/usr/bin/env python3
"""Copy the campaign-scripting wiki (docs/wiki, the source: its tests check it)
into the game's Obsidian wiki as WIKI/scripting, so both read the same.

  python3 tools/docs/sync_scripting_wiki.py

Each page gets the frontmatter WIKI wants (title, aliases, tags, status), and
its GitHub-wiki links [text](Page#anchor) become Obsidian links
[[Page#Heading|text]], so tools/docs/check_wiki.py can follow them. Images are
copied beside the pages. _Sidebar.md is the GitHub wiki's menu and is not copied
(Home has the same table). Run it after editing docs/wiki.
"""
from __future__ import annotations

import re
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "docs" / "wiki"
DST = ROOT / "WIKI" / "scripting"

# Home.md would clash with the game wiki's own Home: link to it by this alias.
HOME_ALIAS = "Campaign Scripting"

LINK = re.compile(r"(?<!!)\[([^\]\n]+)\]\(([A-Za-z][\w\-]*)(?:#([^)\s]+))?\)")
FENCE = re.compile(r"(```.*?```)", re.S)
HEADING = re.compile(r"^#+ (.*)$", re.M)


def slug(heading: str) -> str:
    h = heading.strip().lower()
    h = re.sub(r"[^\w\- ]", "", h)
    return h.replace(" ", "-")


def headings(text: str) -> dict[str, str]:
    """slug -> heading text, as GitHub would anchor them (code blocks skipped)."""
    out = {}
    for h in HEADING.findall(FENCE.sub("", text)):
        out.setdefault(slug(h), h.strip())
    return out


def title_of(name: str, text: str) -> str:
    m = HEADING.search(text)
    return m.group(1).strip() if m else name.replace("-", " ")


def convert(text: str, pages: dict[str, str]) -> str:
    def link(m: re.Match) -> str:
        label, page, anchor = m.group(1), m.group(2), m.group(3)
        if page not in pages:
            return m.group(0)
        target = HOME_ALIAS if page == "Home" else page
        if anchor:
            heading = headings(pages[page]).get(anchor)
            if heading:
                # Obsidian matches headings by text; backticks and brackets confuse it.
                target += "#" + re.sub(r"[`\[\]|#^]", "", heading)
        return f"[[{target}|{label}]]"

    # Leave code blocks alone: a script's text is not a link.
    parts = FENCE.split(text)
    return "".join(p if i % 2 else LINK.sub(link, p) for i, p in enumerate(parts))


def main() -> int:
    pages = {p.stem: p.read_text(encoding="utf-8") for p in sorted(SRC.glob("*.md")) if not p.stem.startswith("_")}
    DST.mkdir(parents=True, exist_ok=True)
    for old in DST.glob("*.md"):
        if old.stem not in pages:
            old.unlink()
    for name, text in pages.items():
        aliases = [HOME_ALIAS] if name == "Home" else [name.replace("-", " ")]
        generated = "Generated from docs/wiki/" + name + ".md" if name == "Library-Reference" else "Copied from docs/wiki/" + name + ".md"
        front = (
            "---\n"
            f"title: \"{title_of(name, text)}\"\n"
            f"aliases: [{', '.join(aliases)}]\n"
            "tags: [scripting, campaign, tool]\n"
            "status: reference\n"
            "---\n\n"
            f"<!-- {generated} by tools/docs/sync_scripting_wiki.py: edit that, not this. -->\n\n"
        )
        body = re.sub(r"^<!--.*?-->\s*", "", text, flags=re.S)
        (DST / f"{name}.md").write_text(front + convert(body, pages), encoding="utf-8")
    images = SRC / "images"
    if images.is_dir():
        (DST / "images").mkdir(exist_ok=True)
        for img in images.iterdir():
            if img.is_file():
                shutil.copy2(img, DST / "images" / img.name)
    print(f"{len(pages)} pages copied to {DST.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
