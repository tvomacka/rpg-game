# Conversation Log (planning session)

Date: 2026-01-26
User: @tvomacka

This file is a machine-readable/author-friendly log of the planning conversation that led to the DESIGN.md decisions. It is intended for recordkeeping and for future reference while implementing the MVP.

Summary of user requirements and choices
- MVP scope (in priority order):
  1. Player movement & camera — player controls basic movement of PC with keyboard and mouse (direct control).
  2. Basic terrain generation — procedural terrain generated from map data.
  3. Static NPCs with dialogue — single NPC, player can approach and initiate conversation.
  4. Basic interactive environment — levers, buttons, switches.
  - All with placeholder art/animations. Items/inventory/UI/character sheet excluded from MVP.

- Target platform: PC (Windows primary), macOS later.

- Tech stack (agreed):
  - Unity LTS
  - Universal Render Pipeline (URP)
  - New Input System
  - Cinemachine
  - Terrain generation: Unity Terrain or custom mesh generation (start with Terrain)
  - Addressables (optional later)
  - Dialogue: Yarn Spinner

- Movement & control specifics:
  - Control: Direct control (WASD/arrow keys), camera-relative movement.
  - Movement implementation: CharacterController (kinematic).
  - Movement defaults: walk speed 4.0 m/s, no sprint, rotation smoothing 0.08 s, no jump.
  - Requirement: Movement and stat values must be data-driven and runtime-modifiable (e.g., ScriptableObjects + PlayerAttributes + StatModifiers).

- Camera:
  - Orthographic fixed isometric camera, smooth (damped/spring) follow via Cinemachine.

- Interaction:
  - Interaction model: Hybrid — show prompt in range, require press (E) to start dialogue.
  - Dialogue: Branching (Yarn Spinner).
  - Dialogue must be able to modify game state (Yarn variables/events can trigger quests, stat changes, etc).

- Dialogue tooling:
  - Yarn Spinner selected for authoring and runtime.
  - DialogueManager and DialogueUI to be implemented as lightweight bridge.

- Additional decisions:
  - Prepared to use CharacterController and new Input System integration.
  - Placeholder art and Animator-based placeholder animations will be used.

Files added/created (current state)
- docs/DESIGN.md — added to repository with the design notes above.
  - Commit: (created during this session)

Pending actions
- Add docs/CONVERSATION.md to the repository (this file is a draft in the planning notes and can be committed to the repo).
- Start implementation steps listed in DESIGN.md in the Unity project.

Notes for implementers
- Keep systems data-driven and event-friendly to allow later features (items, modifiers, quests, more NPCs).
- Prefer ScriptableObjects for editable base data and simple runtime data containers for active state.
- Keep placeholder art simple to accelerate iteration.

(end of conversation log)