# TODO — Next Steps / Implementation Plan

- [ ] Create Unity project with LTS + URP + required packages (Input System, Cinemachine, Yarn Spinner)
- [ ] Add project folder structure:
  - [ ] Scenes/
  - [ ] Prefabs/
  - [ ] Scripts/
  - [ ] Art/placeholder/
  - [ ] Docs/
- [ ] Implement Player prefab:
  - [ ] CharacterController-based movement component
  - [ ] PlayerAttributes + ScriptableObject base values
  - [ ] Input action asset + PlayerInput
  - [ ] Simple Animator with idle/walk animations
- [ ] Implement Cinemachine virtual camera with orthographic projection and damping to follow player
- [ ] Implement procedural terrain generator that creates a Terrain from a heightmap at scene start
- [ ] Implement NPC prefab with NPCDialogue component and example Yarn script node
- [ ] Implement simple interactable (lever) prefab and wiring to events
- [ ] Add unit and play-mode tests as appropriate for runtime logic
