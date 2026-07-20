# Changelog

All notable changes to this project are documented in this file.

## [1.5.1] - 2026-07-20

### Added
- ModIcon.png

### Changed
- Preview.png

## [1.5.0] - 2026-07-18

### Added
- **Hunters Use Melee! support.** When that mod is active, a colonist's Melee skill and melee power (not just Shooting) count toward how good a hunter they are, so a strong melee fighter is picked for hunting.
- **Vanilla Genetics Expanded support.** Its Genetics work type is now treated as the dedicated Intellectual specialist it is, instead of being buried near the bottom of the priority range.
- **"Active by default on new colony" setting** (General tab, on by default). Turn it off to start new colonies with auto mode off — you then optimize manually or switch it on per-colony from the Work tab. Existing colonies are unaffected.

## [1.4.0] - 2026-07-17

### Added
- **PriorityMaster support.** The mod now detects PriorityMaster's configured priority range (up to 1–99)

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
