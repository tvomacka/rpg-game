# MVP implementation plan — "The first case"

> Agreed 2026-11-26. Implements DESIGN.md §8. Once agreed, this goes into the repo as `Docs/MVP-plan.md`.

## Goal

A stranger plays one small crime scene for 10–15 minutes: walks around, inspects clues, questions two
people, names a culprit and a method, and gets an ending that depends on what they found.
Placeholder art throughout. **No save/load, no character creation, no combat, no inventory.**

## The case (content draft)

*Working title: "The Stopped Clock".* A cloth merchant, Aldric, is found dead in his study one morning.
The town healer called it heart failure. His sister doesn't believe it and hires you.

| Clue | Where | How you find it | What it means |
|---|---|---|---|
| Withered plants on the study windowsill — only there | Study | Inspect | Strong life-draining magic was cast here |
| Clock stopped, hinges rusted overnight | Study | Inspect; **Arcane Sense** check reveals "aged, not broken" | Accelerated-aging side effect → the same spell |
| Ledger with a page torn out | Study desk | Inspect | Someone hid a business matter |
| Scorched chalk marks under the rug | Study | Inspect the rug (easy to miss) | A ritual circle — the spell was prepared, not spontaneous |
| Maid (Mira) is nauseous and can't recall the evening | Kitchen, dialogue | Talk; **Medicine** check names it as memory tampering | Someone wiped her memory — she's a witness, not the culprit |
| Partner (Hollen) claims he "hadn't seen Aldric in weeks" | Yard, dialogue | Talk | — |
| Hollen's boots: the same chalk dust | Yard, dialogue/inspect | Inspect Hollen, or ask about it after finding the chalk marks | He was in the study |
| Torn ledger page in the yard brazier | Yard | Inspect | Debt Hollen owed Aldric → motive |

- **Right answer:** Hollen, by life-draining magic (ritual). **Wrong answers that still end:** the maid;
  "natural death"; Hollen with the wrong method (partial ending).
- Two NPCs as DESIGN.md §8 says: Mira (witness, wrongly suspicious) and Hollen (culprit).
  The sister who hires you is a text intro, not an NPC.
- Two skill checks show pillar 1 without a full skill system: the character has fixed skill values;
  one check passes, one fails by default, and a debug toggle flips them so you can see the difference.

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
- Inspecting an object can grant a clue; skill-gated clues show extra text only when the check passes.
- Journal UI (J key + button): list of found clues with text; "new clue" notification.
- Tests: EditMode — clue granted once, skill gate pass/fail, event fires.

**M4 — Dialogue**
- Yarn Spinner DialogueRunner + line/option presenters (placeholder UI).
- Yarn commands/functions bridging to the case: `<<add_clue "id">>`, `has_clue("id")`, `skill("Medicine")`.
- Write Mira's and Hollen's dialogue, including options that only appear after finding clues.
- Tests: EditMode for the bridge functions.

**M5 — Conclusion and endings**
- "Close the case" panel in the journal: pick culprit + method, confirm.
- Ending lookup from (culprit, method) → ending text screen → restart.
- Tests: EditMode — every (culprit, method) pair maps to an ending.

**M6 — Make it readable, then playtest**
- Visual traces: withered plant, rusted clock, chalk marks under the rug — placeholder but distinct.
- Intro text (the sister's request), clear hover labels, journal hint when nothing is left.
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
