# Adaptive Priorities

A RimWorld 1.6 mod that assigns colonist work priorities for you — based on skills, passions, traits, and what the colony actually needs. Install and it just works; no setup required.

## Features

- **Skill- and passion-aware scoring.** Ranks every colonist for every job by skill level, passions, and the traits/genes/bionics that affect work speed.
- **Coverage guarantee.** Critical jobs (cooking, doctoring, firefighting, hauling…) always get a worker, even in a colony of poor fits — no more "nobody cooks".
- **Specialization.** Your best cook cooks; work isn't spread thin across everyone.
- **Respects the game's rules.** Incapable pawns, ideology restrictions, and downed/drafted/mental-break states are handled automatically — impossible work is never assigned.
- **Inspiration-aware.** Routes inspired work to the inspired pawn before the inspiration expires.
- **Ideology-opposed work** is assigned only as a last resort, and only if you allow it.
- **Auto mode.** On by default for new colonies; reassigns on a calm interval (once per in-game day by default) so priorities don't chase short-lived events. Or turn it off and press **Optimize** manually.
- **Locks.** Middle-click in the Work tab to protect hand-tuned assignments: a colonist's name locks the whole pawn, a column header locks a work type, a cell locks one assignment. Locked items show a padlock and are never touched automatically.
- **Customizable settings.** Per-work-type importance and "everyone does this" toggles, plus an advanced section for the full scoring formula. Fully data-driven — other mods can rebalance or extend it with plain XML.

## Compatibility

- **Fluffy's Work Tab** — full lock/toggle UI works in its grid too, with no hard dependency. Extended priority ranges (e.g. 1–9) are detected automatically.
- **Modded work types** are supported automatically (a sensible policy is derived from the work type itself). (first party support can be easily added via xml patches)
- **Modded passions** (Vanilla Skills Expanded and similar) are handled generically.
- **Harmony** is required.
