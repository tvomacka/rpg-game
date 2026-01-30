# RPG Game — Design Notes (MVP)

## Overview
This document captures the agreed design decisions for the first MVP of the rpg-game project and implementation notes for developers. It reflects the conversation and choices made by the project owner (tvomacka) during initial planning.

## MVP (in priority order)
1. Player movement & camera
   - Player can control basic movement of the PC with keyboard and mouse (direct control, camera-relative).
2. Basic terrain generation
   - Terrain is procedurally generated from map data (Unity Terrain or custom mesh generation as implementation detail).
3. Static NPCs with dialogue
   - Single NPC in MVP. Player can approach and initialize branching dialogue.
4. Basic interactive environment
   - Levers, switches, buttons that can be activated.

All of the above use placeholder art and animations. Items, inventory, UI, character sheet, and other systems are excluded from MVP.

## Tech Stack (decided)
- Unity: 6.3LTS (latest long-term support version).
- Render pipeline: Universal Render Pipeline (URP).
- Input: New Unity Input System package.
- Camera: Cinemachine (for smooth follow and damping).
- Terrain: Unity Terrain or custom mesh generation (choose based on iteration speed; Terrain recommended for fast MVP).
- Asset loading: Addressables (optional; plan for later streaming/asset management).
- Dialogue: Yarn Spinner (authoring and runtime).
- Target platforms (MVP): PC (Windows primary, macOS later).

## Player Movement
- Control scheme: Direct control (WASD / arrow keys) with camera-relative movement.
- Implementation: Kinematic CharacterController (chosen for predictable, responsive movement).
- Movement parameters (defaults):
  - Walk speed: 4.0 m/s
  - Sprint: no (default)
  - Rotation smoothing: 0.08 s (rotation smooth time)
  - Jump: no (default)
- Runtime configurability: Movement and related stats MUST NOT be hard-coded. Base values will be stored in a serializable container (ScriptableObject recommended) and a runtime PlayerAttributes component will compute effective values by applying StatModifiers (additive/multiplicative).
- Events: PlayerAttributes exposes events (e.g., OnStatChanged) so other systems can react.

## Camera
- Chosen option: Orthographic fixed isometric camera (classic isometric view).
- Behavior: Camera follows the player with smooth (damped/spring) motion using Cinemachine.
- Camera-relative movement: Input is interpreted relative to the camera forward/right vectors (note orthographic camera has no perspective).

## Dialogue & NPCs
- Interaction model: Hybrid — show an interaction prompt when the player is in range; player must press the interact key (E) to start dialogue.
- Dialogue type: Branching dialogue (Yarn Spinner to author branching nodes and variables).
- Dialogue capabilities: Dialogue will be able to modify game state (Yarn variables, trigger events like "start quest", apply stat modifiers, change NPC disposition) per agreement.
- Runtime bridge: DialogueManager to mediate Yarn runtime and DialogueUI prefab. NPCDialogue component to point to Yarn nodes and handle range/prompt/interact flow.

## Interactive Environment
- Implement basic interactables (levers, buttons, switches) with simple state machines.
- Interactables should expose events so they can be linked to world effects or dialogue.

## Procedural Terrain
- For MVP, use Unity Terrain with runtime-generated heightmaps (Perlin noise or seeded map data). This is quickest to prototype.
- Optionally implement a custom mesh generator for finer control later.

## Placeholder Art & Animation
- Use simple primitives (cubes, capsules) or low-poly placeholder meshes.
- Animator + animation clips for basic idle/walk animations; these can be placeholders and swapped later.

## Systems Design Notes
- Data-driven where possible: use ScriptableObjects for base data (PlayerStats, NPCData, Dialogue references).
- Runtime modifiers: Implement a small StatModifier system (priority order, additive/multiplicative) applied to PlayerAttributes.
- Input mapping: Use the new Input System with an input action asset (e.g., Player.inputactions) and a PlayerInput component.
- Scene scaffold: Create a sample scene "MVP_Scene" with placeholder terrain, player prefab, one NPC, and an example lever.

## Next Steps / Implementation Plan
1. Create Unity project with LTS + URP + required packages (Input System, Cinemachine, Yarn Spinner).
2. Add project folder structure: Scenes/, Prefabs/, Scripts/, Art/placeholder/, Docs/.
3. Implement Player prefab:
   - CharacterController-based movement component
   - PlayerAttributes + ScriptableObject base values
   - Input action asset + PlayerInput
   - Simple Animator with idle/walk animations
4. Implement Cinemachine virtual camera with orthographic projection and damping to follow player.
5. Implement procedural terrain generator that creates a Terrain from a heightmap at scene start.
6. Implement NPC prefab with NPCDialogue component and example Yarn script node.
7. Implement simple interactable (lever) prefab and wiring to events.
8. Add unit and play-mode tests as appropriate for runtime logic.

## TODOs / Open questions for later
- Decide whether to use Unity Terrain or custom mesh generation for final system; start with Terrain for the MVP.
- Addressables integration for streaming larger maps (deferred until after MVP).
- Expand dialogue tooling and localization support in subsequent iterations.
