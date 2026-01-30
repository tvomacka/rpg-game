# TODO — Next Steps / Implementation Plan

- [x] Create Unity project with LTS + URP + required packages (Input System, Cinemachine, Yarn Spinner)
- [x] Add project folder structure:
  - [x] Assets/Scenes/
  - [x] Assets/Prefabs/
  - [x] Assets/Scripts/
  - [x] Assets/Art/placeholder/
  - [x] Assets/Docs/
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
