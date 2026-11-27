# MVP implementation plan — "The first case"

> Agreed 2026-11-26. Implements DESIGN.md §9. Once agreed, this goes into the repo as `Docs/MVP-plan.md`.

## Goal

A stranger plays one small crime scene for 10–15 minutes: walks around, inspects clues, questions two
people, names a culprit and a method, and gets an ending that depends on what they found.
Placeholder art throughout. **No save/load, no character creation, no combat, no inventory.**

## The case (content draft)

*Working title: "The Stopped Clock".* Reworked 2026-11-27 to follow the rules of magic (DESIGN.md §3).

A cloth merchant, **Aldric**, is found dead in his study one morning. The town healer called it heart failure. His sister doesn't believe it and hires you.

**What really happened:** Aldric's business partner **Hollen** owed Aldric a large debt he couldn't repay. He borrowed from a shady lender to hire a mage, who drew a **triggered chalk circle targeted at Aldric** (using hair from Aldric's hairbrush) under the rug by the desk. To get the mage in at night, Hollen had the maid **Mira** open the yard gate and the front door, then wiped her memory of it with a cheap **Mind scroll** in the kitchen. The next evening Aldric stepped on the rug and the circle fired. Hollen was at the guild hall, a solid alibi.

| Clue | Where | How you find it | What it means (by the rules) |
|---|---|---|---|
| Withered plants on the windowsill nearest the desk; the far windowsill is fine | Study | Inspect | A strong working fired near the desk, not at the door |
| Wall clock stopped at 9:40 pm, spring seized | Study | Inspect; **Arcane Sense** shows "seized by a draw, not broken" | Time the working fired = time of death |
| Faint chalk circle under the rug; a few grey hairs in its lines | Study | Inspect the rug (easy to miss) | Triggered, targeted ritual. Nobody was present when it fired, and someone needed Aldric's hair |
| Aldric's hairbrush on the study side table, picked clean of hair | Study (side table) | Inspect | Where the hair came from; who has access? (Mira cleans there) |
| Mira: nauseous, can't account for the night before last | Kitchen, dialogue | Talk; **Medicine** recognises a Mind working | Someone wiped her memory; she's a witness, not the culprit |
| Mouldy bread and dried herbs only on one kitchen shelf | Kitchen | Inspect | A second, small working fired here (the memory wipe) |
| Hollen: "I was at the guild hall all evening" (true) | Yard, dialogue | Talk | A real alibi that only clears him if the death needed someone present |
| Chalk dust on Hollen's boots | Yard, dialogue/inspect | Inspect Hollen, or ask about it after finding the circle | He was in the study when the circle was drawn |
| Half-burnt papers in the yard brazier: torn ledger page (Hollen's debt) and a loan note from a lender | Yard | Inspect | Motive, plus the money for an expensive triggered ritual |

**Conclusion panel:** culprit = Hollen; method = triggered, targeted chalk circle (Life school).
- **Right:** Hollen + triggered circle.
- **Partial:** Hollen + a method that needed him present (his alibi makes the case collapse in court).
- **Wrong:** Mira (she was used); "natural death"; anyone + "scroll".

**Left for later cases:** the mage who drew the circle, and the lender.

This stays at **two NPCs** and about nine clues. It teaches the core rules through play: centre of the draw, stopped clock, triggers, targeting, Mind symptoms, and money.

## Decided: how the player concludes (DESIGN.md open question 2)

**A "Close the case" panel in the journal** (decided 2026-11-26). The player picks a culprit (from the people
they met) and a method (from the traces they found), then confirms. It works for every future case,
keeps deduction in the player's head, and the endings are a simple lookup.
Rejected: accusing in dialogue (every case would need custom branching; easy to brute-force).

## Milestones

Each milestone is one PR, playable in the editor when merged.

**M1 — Walk around the scene**
- Grey-box house (study, kitchen) and yard in `FirstCase`. Baked NavMesh.
- Click-to-move: left click raycasts to the NavMesh, NavMeshAgent walks there; click marker.
- Cinemachine orthographic camera at a fixed isometric angle, damped follow, scroll-wheel zoom within limits. No rotation.
- **Isometric occlusion:** roof hides when the player is inside (trigger volume); walls facing the camera are low cutaway walls. Without this you can't see anything indoors.
- Tests: PlayMode — the agent reaches a clicked point.

**M2 — Interact with things**
- One `Interactable` component: hover highlight + cursor change; click → walk to its interaction point → run its action (Inspect / Talk).
- Inspect shows a short text popup. Interacting with a person faces them.
- Tests: EditMode for the interaction-point / range logic.

**M3 — Clues and the journal**
- ScriptableObjects: `ClueDefinition` (id, title, text, optional skill check), `CaseDefinition` (clues, suspects, methods, endings).
- Runtime `CaseState`: found clues, fires `OnClueFound`. Fixed `PlayerSkills` (Arcane Sense, Medicine) + debug toggle.
  The two checks show pillar 1 without a full skill system: one passes and one fails by default; the toggle flips them.
- Inspecting an object can grant a clue; skill-gated clues show extra text only when the check passes
  (Arcane Sense on the stopped clock: "seized by a draw, not broken").
- Inspectable clues in the house and yard: withered windowsill plants (near desk vs far), stopped clock (9:40 pm),
  chalk circle with hairs under the rug, picked-clean hairbrush, mouldy kitchen shelf, brazier papers (ledger page + loan note).
- Scene props the reworked case needs in `FirstCase` (placeholder art is fine): a second study windowsill with healthy
  plants (the comparison), a side table with Aldric's hairbrush, a kitchen shelf with bread and herbs (the second trace),
  and a wall clock with a readable stopped time.
- Journal UI (J key + button): list of found clues with text; "new clue" notification.
- Tests: EditMode — clue granted once, skill gate pass/fail, event fires.

**M4 — Dialogue**
- Yarn Spinner DialogueRunner + line/option presenters (placeholder UI).
- Yarn commands/functions bridging to the case: `<<add_clue "id">>`, `has_clue("id")`, `skill("Medicine")`.
- Write Mira's and Hollen's dialogue (still two NPCs), including options that only appear after finding clues:
  **Medicine** on Mira recognises a Mind working; Hollen gives his true guild-hall alibi; his chalk-dusted boots can be
  inspected, or raised once the chalk circle is found.
- Tests: EditMode for the bridge functions.

**M5 — Conclusion and endings**
- "Close the case" panel in the journal: pick culprit + method, confirm.
- Ending lookup from (culprit, method) → ending text screen → restart. Right: Hollen + triggered, targeted chalk circle;
  partial: Hollen + a method that needed him present; wrong: Mira, "natural death", anyone + "scroll".
- Tests: EditMode — every (culprit, method) pair maps to an ending.

**M6 — Make it readable, then playtest**
- Visual traces: withered plants on the windowsill near the desk (far one healthy), stopped wall clock,
  faint chalk circle under the rug, hairbrush on the side table, mouldy shelf in the kitchen — placeholder but distinct.
- Intro text (the sister's request; she is a text intro, not an NPC), clear hover labels, journal hint when nothing is left.
- Playtest with someone who hasn't seen it; fix what confuses them. This is the MVP "done" check.

## Technical choices (for the workers)

- Code lives in `Assets/_Project/Scripts`, one runtime assembly (split later only if it hurts).
- Plain C# for logic (`CaseState`, ending lookup, skill checks) so it's EditMode-testable; MonoBehaviours stay thin.
- UI: uGUI for everything in the MVP — Yarn Spinner's presenters are uGUI, so one UI system.
- Input: one Input Actions asset (Click, Point, Zoom, Journal).
- Data: case content in ScriptableObjects + `.yarn` files; no hard-coded clue text in scripts.

## Verification

`.\run-unity.ps1 -RunEditModeTests -RunPlayModeTests` works in batch mode (licence fixed 2026-11-26).
Every PR must compile and pass both test suites before it's called ready.

## Process

One worker per milestone, in order. Each opens a PR, compiles and runs tests, and owns it until it's
ready. You play it in the editor and merge. I review each PR against this plan before you do.
