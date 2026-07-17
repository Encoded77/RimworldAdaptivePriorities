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
- **Modded work types** are supported automatically (a sensible policy is derived from the work type itself). (first party support can be easily added via xml patches)
- **Modded passions** Most modded passions should work out of the box due to using a passions defined learning rate. First party support for Alpha Skills and VSE.
- **Mechs & drones** — Biotech and Alpha Mechs work mechs are accounted for automatically; **Vanilla Quests Expanded – Drone Factory** drones have first-party support. Any other mod's automatons — or quality-craft work types — can be added with plain XML.
- **Harmony** is required.


# Changelog

All notable changes to this project are documented in this file.

## [1.3.0] - 2026-07-17

### Added
- **External worker awareness.** The mod now takes your mechanoids and drones into account when assigning colonist work. Where automatons can cover a job, colonists are pulled off it and freed for the work only they can do — and the pawns freed are always the worst-suited ones, so your best workers keep the job.
  - Works out of the box with **Biotech mechanoids** and any mod's work mechs (including **Alpha Mechs**), discovered automatically from what each mech is capable of.
  - First-party support for **Vanilla Quests Expanded – Drone Factory** drones (hauler, cleaner, farming, miner).
  - A **full-time automaton counts for more than one colonist** on menial jobs — a colonist splits their day across everything, while an automaton works continuously.
  - New **External workers** settings tab: turn mechs and drones on or off independently, and set each work type to **Off**, **Reduce** (hand over some of the work but keep a backup of colonists), or **Full** (hand the job over entirely). Live readouts show the current capacity on your map.
  - Emergency and self-care work (firefighting, doctoring, patient care, childcare…) is never thinned by default, and quality crafts keep extra colonist crafters so a mech supplements your workforce instead of replacing it.
  - Fully data-driven — other mods' automatons and quality-craft work types can be added with plain XML.

## [1.2.0] - 2026-07-17

### Added
- **New settings menu.** Settings are now organized into four tabs — General, Scoring, Work types, and Passions — instead of one long list.
- **Work types table.** All work types are shown side by side so you can compare their importance at a glance. With advanced settings on, each row expands to show its detailed options.
- **Passions tab.** Set the score bonus of every passion — including modded ones from Vanilla Skills Expanded and Alpha Skills, shown with their own icons. Passions you don't touch keep adjusting automatically.
- **Helpful tooltips everywhere.** Every setting explains what it does, shows its default value, and where possible shows what your current value means in practice (e.g. how many workers a cap allows in your colony right now).

### Changed
- Options that currently have no effect (for example, specialist options while "Everyone does this" is on) are greyed out, with the reason shown in their tooltip.

---

## [1.1.0] - 2026-07-17

### Added
- Improved passion support across the mod.
- Added dedicated support and tuning for **Alpha Skills** passions.
- Added dedicated support and tuning for **Vanilla Expanded Skills** passions.
- Added a debug action to generate a full colony-wide calculation report, making it easier to inspect and troubleshoot score calculations.

### Changed
- Hauling calculations now take both **carrying weight capacity** and **movement speed** into account, resulting in more accurate hauling behavior.

---

## [1.0.0] - 2026-07-16

### Added
- Initial public release.