# Adaptive Priorities

A RimWorld 1.6 mod that assigns colonist work priorities for you — based on skills, passions, traits, and what the colony actually needs. Install and it just works; no setup required.

## Features

- **Skill- and passion-aware scoring.** Ranks every colonist for every job by skill level, passions, and the traits/genes/bionics that affect work speed.
- **Coverage guarantee.** Critical jobs (cooking, doctoring, firefighting, hauling…) always get a worker, even in a colony of poor fits — no more "nobody cooks". Can be turned off in settings if you'd rather leave a job undone than force a bad fit.
- **Specialization.** Your best cook cooks; work isn't spread thin across everyone.
- **Respects the game's rules.** Incapable pawns, ideology restrictions, and downed/drafted/mental-break states are handled automatically — impossible work is never assigned.
- **Inspiration-aware.** Routes inspired work to the inspired pawn before the inspiration expires.
- **External worker awareness.** Accounts for your mechanoids and drones: where automatons cover a job (hauling, cleaning, mining…), colonists are freed for the work only they can do — and the best-suited pawns keep the job while the worst fits are the ones freed. Works automatically with Biotech mechs and Alpha Mechs, with first-party support for Vanilla Quests Expanded – Drone Factory. Configure it per work type (off / reduce / full offload, plus a colonist backup) in its own settings tab, or turn mechs and drones off entirely.
- **Ideology-opposed work** is assigned only as a last resort, and only if you allow it.
- **Auto mode.** On by default for new colonies; reassigns on a calm interval (once per in-game day by default) so priorities don't chase short-lived events. Or turn it off and press **Optimize** manually.
- **Locks.** Middle-click in the Work tab to protect hand-tuned assignments: a colonist's name locks the whole pawn, a column header locks a work type, a cell locks one assignment. Locked items show a padlock and are never touched automatically.
- **Customizable settings.** A tabbed settings menu: compare every work type's importance side by side, expand a work type for its detailed options, and tune the bonus of every passion — modded ones included. Every setting has a tooltip explaining what it does and what your current value means in practice, and anything you've changed can be reset individually. Fully data-driven — other mods can rebalance or extend it with plain XML.

## Compatibility

- **Fluffy's Work Tab** — full lock/toggle UI works in its grid too, with no hard dependency. Extended priority ranges (e.g. 1–9) are detected automatically.
- **PriorityMaster** — its configured priority range (up to 1–99) is detected automatically, with no hard dependency.
- **Modded work types** are supported automatically (a sensible policy is derived from the work type itself). (first party support can be easily added via xml patches)
- **Modded passions** Most modded passions should work out of the box due to using a passions defined learning rate. First party support for Alpha Skills and VSE.
- **Mechs & drones** — Biotech and Alpha Mechs work mechs are accounted for automatically; **Vanilla Quests Expanded – Drone Factory** drones have first-party support. Any other mod's automatons — or quality-craft work types — can be added with plain XML.
- **Harmony** is required.

See [CHANGELOG.md](CHANGELOG.md) for release notes.