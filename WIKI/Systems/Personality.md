---
title: Personality
aliases: [Traits, Temperament, Loyalty, Ambition, Suspicion, Courage, Talkative]
tags: [system, personality, npc, ai]
status: implemented
---

# Personality

**Status:** Implemented (design doc 11.4: "personality filters behavior, not just memory"). Every NPC carries a `Personality` of **five numbers from 0 to 1**, rolled uniformly at random when the person is created. They decide what an agent does with what it knows and wants. The player does **not** have a personality struct; the player expresses it through the choices of the character sheet ([[Starting Paths]]).

> [!info]
> The design doc also lists "sociability" and "discretion"; the build has five traits: **Loyalty, Talkative, Suspicion, Ambition, Courage**. In conversation you can read three of them: "Tell me about yourself" shows *bold / steady / cautious* (Courage over 0.7 / under 0.3), *talkative / civil / close-mouthed* (Talkative over 0.6 / under 0.3), and *means to rise / is loyal to their own / wants a quiet life* (Ambition over 0.65, Loyalty over 0.65, else).

## Where each trait bites

### Loyalty
| Use | Rule |
|---|---|
| Lying | Only a man with Loyalty **under 0.5** will ever lie; `bent = 1 - 2 x Loyalty` sets his odds ([[Gossip, Lies and Leverage]]) |
| Obedience | Willing chance `+0.4 x Loyalty` in [[Delegation]]; a trusted man needs Trust `0.4 + 0.4 x Loyalty + regard/100` |
| Orders | You get `(1 - Loyalty) x 0.2` more compliance from a disloyal subordinate |
| Leaders | A loyal leader (over 0.6) "keeps to his orders" on hard asks |
| Skimming | A Factor with Loyalty under 0.35 and Ambition over 0.5 shades the books at the sailing (10 percent of the pelts) |
| Council | Loyalty to the concern as a whole; a low-loyalty partner with Ambition over 0.6 defies a lost vote |

### Ambition
| Use | Rule |
|---|---|
| Career | A clerk with Ambition over 0.6 whispers to the Factor; 0.55+ will lie to pull down a rival |
| Contest | A rival is anyone with Ambition 0.45+ |
| Delegation | An ambitious man takes hunts for credit (+0.15) but resists wood duty (-0.15) and refuses "beneath him" work |
| Barter | A hard trader (over 0.65) is visible to a sharp reader; each point of his Ambition lowers your press chance by 0.1 |
| Barks | "I'll not carry wood all my life" (over 0.7) |
| Raids | Chance a leader sends a party: `0.35 + 0.4 x Ambition` |
| Truce | Ambitious leaders refuse |

### Suspicion
| Use | Rule |
|---|---|
| Rumour | Inflates a retelling (`+0.3 x Suspicion` chance of "and worse than that") and sees through liars (over 0.7) |
| Negotiation | A suspicious leader (over 0.6) says no more often (`-0.25 x Suspicion`) and docks you more for declining (`3 + 3 x Suspicion` standing) |
| Delegation | Keeps key tasks from men he trusts less than 0.55 |
| Leverage | Adds to defiance (`0.5 x Suspicion`) |
| Replies | At the fire, a suspicious listener (over 0.65) answers a story with "Is that the truth of it?" |

### Talkative
| Use | Rule |
|---|---|
| Gossip frequency | The interval between stories is `4-9 s / Talkative` |
| Rumour accuracy | Garble chance `+15 percent x Talkative` |
| Rumours asked | He tells 4 lines (over 0.6), 2 (over 0.3), or 1 |
| Keeping a friend's secret | Repeats it `10 percent + 15 percent x Talkative` of the time |
| Gifts | A listener at a fire gains regard `1 + Talkative` when you tell a story |
| Close-mouthed | A man under 0.3 "says no more" after one line |

### Courage
| Use | Rule |
|---|---|
| Combat nerve | The floor of `Stands`: nerve = Courage + 0.12 x (friends - foes) + 0.15 if a leader is near + 0.1 if armed with a musket + 0.1 for a hunter in his own country + 0.15 drunk, minus 0.3 and 0.3 more as he is hurt (under 55, under 30) and 0.25 if his house was just fired on. He stands above **0.45**. See [[Combat]]. |
| Breaking | When a man falls, every comrade within 600 px re-checks; those who no longer stand (or have Courage 0.3 or less) flee together: the party "breaks" and its faction is shaken for 30 minutes |
| Friends | A friend fights for you if Courage is 0.25+ |
| Blood debt | A holder attacks only with Courage over 0.45 |
| Threats | You give in to a threat when `fear = (0.5 if you are armed, else 0.2) - 0.5 x his Courage + 0.2 if he is unarmed` is above 0.25; otherwise he fights |
| Raids | Defenders: Courage over 0.2 if the house has the odds, 0.7 if not |
| Leverage | Part of defiance |
| Hunts | `+0.3 x Courage` fitness |

## Authoring and debug

The game spawns NPCs with a random personality unless the scene sets one. The debug launch args can make a man "a liar by temper" (Loyalty 0.05, Ambition 0.95). Characters you read in [[Factor]] and [[Clerk]] therefore differ from save to save.

## Tips

> [!tip]
> "Tell me about yourself" is the cheapest way to read a leader before asking him for something big. Suspicious men refuse; ambitious men want the credit; loyal men keep to their orders.

## See also
[[Delegation]] | [[Gossip, Lies and Leverage]] | [[Memory and Knowledge]] | [[Rivalry]] | [[Combat]] | [[Councils and Accords]] | [[Roles]]
