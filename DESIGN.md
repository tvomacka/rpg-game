# rpg-game — Design

> Single source of truth for the game. Replaces README gameplay sections, Docs/ideas.md and InternalDocs/*.
> Status: draft 3 (2026-11-27). Sections marked **OPEN** need a decision.

## 1. Pitch

An isometric open-world RPG in a medieval world where magic is everywhere — and so is magical crime.
You play an investigator who learns to read the traces magic leaves behind: on victims, on witnesses,
on plants, animals and stone. The world is free to explore; the investigations are what pull you through it.

## 2. Pillars

What every feature is measured against. If a feature serves none of these, it waits.

1. **Magic leaves traces.** Spells have physical and mental side-effects that a trained eye can spot
   (memory tampering → nausea, confusion; strong magic → withered plants, deformed wildlife,
   prematurely aged materials). This is the game's signature mechanic, not just flavour.
   What magic can and can't do, and exactly which traces it leaves, is fixed in §3 Rules of magic.
2. **Many ways through.** Every case, quest or obstacle has more than one solution — talk, sneak, fight,
   use magic, or find evidence. Not every route is open to every character, but there is never only one.
3. **A world worth wandering.** No pressure to follow the main case. Side cases, hidden ruins and
   dungeons are found by paying attention — a rumour, a clue, an odd detail — not by map markers.
4. **Character is skills, not class.** Who you are is your race, attributes, skills and perks.

## 3. Rules of magic

> Agreed with the owner 2026-11-27. Every case must be solvable using only these rules, and no case may rely on magic that breaks them. If a story needs a new rule, add it here first.

### 1. Quintessence
All magic runs on one resource, **quintessence**, present in everything. Nobody can create it; magic only moves it. The stronger the effect, the more quintessence is drawn and the wider the area it is drawn from.

### 2. Traces are centred on the diagram
Every working has a **diagram**: a circle drawn on a surface, or the one on a scroll. Quintessence is drawn from around the diagram at the moment it fires, so the damage is centred **on the diagram, not on the target**. With a scroll, that is where its user stood.

### 3. How materials react to the draw
| Material | Reaction |
|---|---|
| Living plants and animals | wither, sicken, die at the centre |
| Dead organic matter (bread, herbs, leather, wood) | dries out, moulds, rots |
| Metal | rusts, tarnishes |
| Mechanisms (springs, clocks, locks) | seize and stop |
| Stone and plaster | crumbles at the edges |

A **stopped clock** within the radius shows when a working fired.

### 4. Traces fade on a known schedule
Fresh for a few hours, readable for one to two days, gone within a week. Withered plants don't recover; trained senses can tell a fresh trace from an old one.

### 5. Diagrams, scrolls and triggers
- **Only trained mages can make a working diagram.** Drawing one puts a starting charge into it. Copying the lines without that charge produces nothing.
- **Scrolls** are portable diagrams. **Anyone** can use a scroll by following it; the scroll is consumed first, so each scroll works once. Making a scroll draws its starting charge at the mage's workshop, which leaves a lasting trace there. Places where scrolls are made are always surrounded by dead gardens.
- **Chalk circles** can be drawn on a floor or wall and given a **trigger** (for example, someone stepping on it). The working fires when the trigger is met, with nobody present.
- **Targeting:** a working meant for one specific person needs a piece of their body (hair, nail, a few drops of blood) worked into the diagram. Without it, the working hits whoever sets it off.
- **Cost:** triggered and targeted workings are far more expensive than plain scrolls. Who could pay is a question in every case.

### 6. Schools and what they leave on the target
| School | What it does | Signs on the target |
|---|---|---|
| Life | drains or restores vitality | drained death looks like heart failure; pallor; cold skin |
| Mind | alters memory and feelings | nausea, lost time, confusion, memories that feel "borrowed" |
| Matter | changes or binds objects | warped, fused or unnaturally clean materials |
| Sight | illusions | a faint shimmer; witnesses repeat the same detail word for word |

### 7. Hiding magic is magic
A working that hides traces draws quintessence too and leaves its own fainter trace. A place that is suspiciously free of ordinary decay (no dust, no wilting) is itself a clue.

### 8. What magic cannot do
- No raising the dead, and no reading a dead person's memories.
- No seeing into the past.
- No reliable lie detection.
- **No spell reveals who cast or commissioned a working.** Magic shows *what*, *where*, *when* and *how strongly*. *Who* is always found by investigation.

### 9. The investigator's senses
Arcane Sense reads a trace: its strength, age, school and centre, within the rules above. It never names a person. Medicine reads the signs on a body or a witness.

## 4. Not in this game (for now)

Explicit, so they stop creeping back in:

- Procedural terrain or procedural world generation — the world is hand-built.
- Character classes.
- Free / third-person camera.
- Multiplayer.
- Seasons and weather — **deferred**, not rejected; revisit after the first playable region.
- Crafting and trading — deferred until the investigation loop is fun.

## 5. Core loop

**Investigation loop (one case):**
1. *Hook* — a case reaches you (a commission, a rumour, a body).
2. *Examine* — inspect places, objects and people; your skills decide what you notice.
3. *Interview* — dialogue with witnesses and suspects; clues and reputation unlock options.
4. *Deduce* — the player connects clues into conclusions themselves (e.g. names the culprit and
   presents evidence). Skills decide what you *notice*, never what you *conclude*.
5. *Resolve* — act on it: arrest, confront, bargain, cover up, or get it wrong. A case can be solved
   wrongly or failed; the game goes on and the consequences stick.

**Exploration loop (between cases):** travel → notice something → follow it → find a location,
side case or reward → it feeds back into skills, reputation or future cases.

## 6. Player character

- **Races:** humans, dwarves, elves, halflings (classic set; final list **OPEN**).
- **Attributes:** strength, dexterity, intelligence, … (final list **OPEN**).
- **Skills:** combat (swords, archery…), movement (running, swimming, riding), stealth, social,
  and **investigation skills** — e.g. Arcane Sense, Medicine, Lore, Insight. These are what make
  pillar 1 work and should be designed first.
- **Perks / special abilities:** picked at creation and earned in play. Some reveal hidden dialogue
  options or clues (in the spirit of VtM Bloodlines disciplines).
- No classes.

## 7. World

- Medieval setting, high magic. Wilderness with creatures, camps, settlements, dungeons, ruins.
- Not all areas are safe for a weak character; not everything is visible — some locations exist
  only once you have the information that points to them.
- **Hand-built terrain** using Unity's terrain tools (heightmaps painted or imported, not generated).
- Isometric constraint: you can't see landmarks on the horizon, so discovery comes from
  information (rumours, maps, clues), local detail, and fog-of-war — not from vistas.
- World is a set of **connected regions** (each its own scene), joined by exits/travel points and
  loaded as needed. Lets each region be hand-built and tested on its own.
- Day/night cycle: in scope after MVP (NPC schedules make it matter for investigations).

## 8. Systems

| System | Role | When |
|---|---|---|
| Movement & camera | Click-to-move (NavMesh pathfinding); isometric orthographic camera, smooth follow | MVP |
| Interaction | Hover highlights interactables; click to walk there and use/inspect/talk | MVP |
| Clue inspection | Examine objects/people; skill checks reveal traces | MVP |
| Dialogue | Branching; gated by clues, reputation, skills, perks; changes game state | MVP |
| Case journal | Records clues and conclusions | MVP (minimal) |
| Combat | Real-time | After MVP |
| Stealth | Avoid detection as an alternative route | After MVP |
| Magic (player) | Spells, including investigative ones | After MVP |
| Reputation | Affects dialogue and outcomes | After MVP |
| Inventory | Items, evidence | After MVP (evidence first) |
| Day/night | NPC schedules, lighting | After MVP |
| Seasons/weather, crafting, trading | — | Deferred |

## 9. MVP — "The first case"

One small, playable crime scene, with placeholder art. It proves the investigation loop, not the engine.

- One small hand-built area (a house and its yard, or a village corner).
- Click-to-move player with the isometric camera.
- 4–6 inspectable clues, at least one magical trace (e.g. withered plants near the window).
- 2 NPCs to interview; at least one dialogue option unlocked by a found clue.
- A minimal journal listing found clues.
- An ending that differs depending on what you found (at least a right and a wrong conclusion).

Done when a stranger can play it in 10–15 minutes and reach a conclusion without help.

The case content, milestones and verification steps are in **[Docs/MVP-plan.md](Docs/MVP-plan.md)**.

## 10. Tech stack

- Unity 6.3 LTS, URP, new Input System, Cinemachine (orthographic follow), AI Navigation (NavMesh) for click-to-move.
- Yarn Spinner for dialogue (variables and commands drive clues and game state).
- Base data in ScriptableObjects (clues, NPCs, character stats). Keep it simple until a second use appears.
- Tests: unit tests for deterministic logic (clue/journal state, dialogue conditions);
  play-mode tests only where they pay for themselves.
- PC (Windows) first.

## 11. Open questions

1. **Final lists** of races, attributes and skills (investigation skills first).
2. **Deduction UI:** how the player states a conclusion (journal board, accusation dialogue, …).
3. **Region travel:** walk to the edge, or a travel map between known places?

## 12. Decision log

| Date | Decision |
|---|---|
| 2026-01-26 | Unity LTS, URP, Input System, Cinemachine, Yarn Spinner; PC first. |
| 2026-01-26 | Orthographic isometric camera. |
| 2026-11-25 | Genre is open-world RPG; investigation is the plot and core loop. |
| 2026-11-25 | Camera stays isometric. |
| 2026-11-25 | No procedural terrain; the world is hand-built. |
| 2026-11-25 | Investigation is a core mechanic (traces, clue inspection, clue-gated dialogue), not only plot. |
| 2026-11-25 | Click-to-move replaces WASD (and the E-to-interact key). |
| 2026-11-25 | The player deduces; skills only reveal clues. |
| 2026-11-25 | Cases can be failed or solved wrongly; the game continues. |
| 2026-11-25 | World is connected hand-built regions, one scene each. |
| 2026-11-25 | MVP replaced: one playable crime scene instead of movement/terrain/lever tech demo. |
| 2026-11-25 | Existing Unity project to be discarded and recreated. |
| 2026-11-27 | All magic runs on quintessence, present in everything; magic only moves it, never creates it. |
| 2026-11-27 | Traces are centred on the diagram (the circle or scroll), not on the target. |
| 2026-11-27 | Only trained mages can make diagrams and scrolls; anyone can use a scroll, once. |
| 2026-11-27 | Chalk circles can be triggered (fire with nobody present) and targeted (needs a piece of the target's body); both cost far more than a plain scroll. |
| 2026-11-27 | First case reworked around a triggered, targeted chalk circle drawn by a mage Hollen hired. |

## 13. Influences

Elder Scrolls (freedom, rich world), Fallout 1 & 2 (isometric, many solutions), Planescape: Torment
(dialogue as gameplay), VtM Bloodlines (abilities that open dialogue).
