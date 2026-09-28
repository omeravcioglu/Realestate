# RealEstate Horror

**An online multiplayer prototype made in Unity 6 with Photon Fusion 2.** The players are agents at a haunted real-estate office. Ghost customers come up to the front desk, and each ghost shows its own agent (and only that player) the two "scary elements" it wants in a home, for example *Cold Spot + Phone Ring*. In the assignment phase, every agent has to give each of their ghosts the house that has exactly that pair.

<sub>The title is the one shown in the game's main menu. The Unity project and product name is `Realestate`. The multiplayer shell (menu, room browser, chat, voice, player prefabs) comes from the *Clean Multiplayer Pro* Asset Store template. The game systems on top of it are my own code.</sub>

---

## Gameplay

- **Setting:** a grey-box office level with a front desk and three lanes. Each lane has a ghost spawn point, a spot at the desk, a leave point off to the side, and a standing spot for one player behind the desk. If more players join, extra lanes are generated at runtime.
- **Session flow:**
  1. Main menu: enter a name, pick a region and one of three characters, then create or join a room (template UI).
  2. The room opens in the template's playground level. About 7 seconds later, every client plays a short intro video while the master client (the player who created the room) loads the game level, *Environment 2*, and unloads the playground.
  3. Each player is teleported to their own standing spot behind the desk, facing their lane, and frozen for a moment. Mouse-look keeps working while they're frozen.
  4. About 15 seconds after the level loads, the master client starts sending ghosts in waves: one per player every ~6.4 seconds until each player has received five.
  5. After the waves, the master client presses **R** to start the assignment phase.
- **Ghost customers:**
  - A ghost walks up its lane to the desk, stays about 5.5 seconds, then moves to the lane's leave point and waits there.
  - Floating text above the ghost shows its request, for example `Find: Cold Spot + Phone Ring`. Only the player who owns that ghost can see it.
  - The two elements are always different. They're drawn from a shuffled list of 20: Banging Door, Cold Spot, Flickering Light, Static TV, Whispering, Shadow Figure, Knocking and so on.
- **Houses:** for every ghost, the master client spawns one networked house tagged with that ghost's two elements and a serial number. The houses stand in a row, and a house is removed when its ghost despawns.
- **Assignment phase ("Phase 3" in the code):**
  - The ghosts walk back to the desk, and each player handles their own ghosts one at a time.
  - The active ghost's text changes to `Assign to: …`. When it reaches the desk, its owner gets a popup that lists every house by number (`House #0`, `House #1`, …).
  - The master client accepts a pick only if all three checks pass: the ghost belongs to that player, the house is still free, and the house has the ghost's two elements in either order.
  - When a pick is accepted, the house shows `(USED)` on every client, the ghost leaves, and that player's next ghost is called. The popup closes when the player has no ghosts left.
- **Core loop:** a ghost arrives and states its two elements → it waits at the side → in the assignment phase you pick the house with the matching pair → next ghost.
- **From the template:** room list with regions, passwords and a player limit, text chat, push-to-talk voice, vote-kick, a player list, pause and settings, and inventory pickups.

**Status:** unfinished grey-box prototype, developed September–October 2025. The networked loop runs end to end: menu → room → timed switch into the game level → lane seating → ghost waves → assignment validated by the master client.

Not built yet:

- Art. The desk, ghosts and houses are primitive cubes inside the template's grey-box playground.
- Showing a house's two elements in the world. They exist only in networked state, and the popup lists houses by number.
- The scary effects themselves. The level only has empty `FX_*` placeholder objects.
- Scoring, a timer and an end screen.

The assignment phase is started with a debug key. A rejected pick closes the popup, and it doesn't reopen.

## Tech stack

| Area | What it uses |
|---|---|
| Engine | **Unity 6** (6000.0.32f1), **Built-in Render Pipeline** with Post Processing Stack v2 |
| Networking | **Photon Fusion 2** (SDK 2.0.6) in *Shared* mode. The room creator (the Shared-mode master client) has state authority over the level's managers and spawns the ghosts and houses. Other clients act through RPCs. Fusion Physics add-on for the template's player prefabs. |
| Voice | **Photon Voice 2** (2.59), push-to-talk voice chat from the template |
| Multiplayer template | **Clean Multiplayer Pro** (Unity Asset Store): menu, room list with regions and passwords, chat, voice, vote-kick, player prefabs |
| Input | Unity **Input System** 1.11 for player controls. My debug hotkeys use the legacy Input Manager, so *Active Input Handling* is set to *Both*. |
| Camera | **Cinemachine** 2.9.7: a first-person virtual camera hard-locked to a target on the character's head |
| UI | uGUI and TextMesh Pro. The house picker is a uGUI popup that clones a hidden button for each house. |
| Dev tooling | **ParrelSync** 1.5.2, which runs clone editors so you can test multiplayer on one machine |

> Timeline and Visual Scripting are installed but not used by any scene or script.

## What I built

My code is **26 scripts in `Assets/ScriptsImade/`** plus **7 scripts at the root of `Assets/`**: 33 C# files, about 3.7k lines. `InputSystem_Actions.cs` and `PlayerInputAsset.cs` in the same folder are generated by the Input System and aren't counted. I also made the `Ghost` and `House` network prefabs in `Assets/Prefabs/`.

| System | Key scripts |
|---|---|
| Ghost waves: one ghost per player per wave until each player has *N*, extra lanes generated for extra players | `GhostSequenceManager` |
| Networked ghost movement: walk to the desk → visit → leave, plus park, recall and force-leave | `GhostController` |
| Ghost requests: two different elements per ghost from a shuffled bag, with the owner, element IDs and display line replicated | `RequestAllocator`, `ScaryElementLibrary`, `GhostRequestTag` |
| Request text above each ghost that only its owner can see | `GhostOwnerBillboard` |
| One networked house per ghost, tagged with the ghost's elements and a serial number. The row of spawn points grows as needed, and a house despawns with its ghost. | `HouseSpawnManager`, `HouseTag` |
| Assignment phase: per-player ghost queues, a popup that waits for the ghost to arrive, validation on the master client, a "USED" broadcast to every client | `AssignmentManager` |
| House-picker popup and UI glue | `Phase3PopupUI_Manual` (the popup used in the level), `Phase3ManualUIBridge` (event-driven show/hide that closes only on a confirmed success), `Phase3PopupUI` (a variant built entirely in code, on an inactive object) |
| Lane seating: the same lane on every client, a teleport that works with the CharacterController, and a timed movement freeze | `PlayerLaneAutoSeat`, `AutoSeatOnReady`, `SeatZone`, `FreezeMovement` |
| Changes to template code | `ThirdPersonController.cs`: converted to first person (camera-relative movement, body yaw follows the camera). While `FreezeMovement` is active it stops movement but keeps mouse-look. `InGame_Manager.cs`: a timed sequence that plays the intro video on all clients by RPC and swaps Environment 1 for Environment 2. `Player Prefab Green.prefab`: seating components added. |
| Debug tools | `Phase3DebugHotkey.cs` (class `Phase3Hotkey`: on the master client, R starts the assignment phase for everyone), `PhaseManager.cs` (class `DebugRecallHotkey`: on the master client, the same R key also recalls all ghosts to the desk), `SeatDoctor` (on-screen seating panel, disabled on the prefab), F6 to re-seat, context-menu test actions |
| Earlier offline house/request design, not wired into the current level | `HouseRandomizer`, `HouseDefinition`, `GhostRequests` |
| Superseded or inactive | `HouseController` (single-element house, replaced by `HouseTag`); `GhostLabel`, `GhostLabelSyncSimple`, `GhostSpeaker` (older label and audio path; on the Ghost prefab they're disabled or do nothing); `TeleportTrigger`, `DelayedOpen`, `SeatOnTrigger` (earlier timed teleport-and-freeze trigger, under an inactive object in the level); `TeleportUtility`, `GhostLabelUtil` (unused helpers); `GhostSpawner` (empty stub) |
| Intro timer | `wait` (hides an intro-video object and shows the menu after a delay) |

### Code highlights

- **`Assets/AssignmentManager.cs`:** the assignment phase runs on the master client. Clients only send RPCs (`RpcBeginMyPhase3`, `RpcBeginPhase3ForAll`, `RpcTryAssignActiveGhostToHouse`).
  - The master client keeps a queue of ghost `NetworkId`s per player and recalls one ghost at a time.
  - It checks every frame until the ghost reaches its lane target (8-second timeout), and only then tells that player's client to open the popup.
  - Each pick is checked for ownership, availability and an order-independent element match before the house is used up.
  - Results go out as RPCs to all clients. Each client filters them by `Runner.LocalPlayer`, and the UI scripts subscribe to static C# events.
- **`Assets/ScriptsImade/GhostSequenceManager.cs`** + **`HouseSpawnManager.cs`:** the wave spawner counts ghosts per player and adds lanes at runtime (offset copies of lane 0) when there are more players than lanes. For each ghost it asks the house spawner for a matching house. It also keeps a record of every ghost so the assignment phase can recall it or send it away. The house spawner ties each house to its ghost with a coroutine that despawns the house once the ghost is gone.
- **`Assets/ScriptsImade/GhostController.cs`:** a small networked state machine (`Idle → ToTarget → Visiting → ToLeave → Done`). It's built from `[Networked]` properties and a `TickTimer`, runs in `FixedUpdateNetwork` on the state authority only, and uses `NetworkTransform` to replicate the movement.
- **`Assets/ScriptsImade/PlayerLaneAutoSeat.cs`:** gives every client the same lane for each player by sorting the active players' `PlayerId`s.
  - It waits until the level's manager and lanes exist.
  - It freezes the player before the teleport so the controller can't pull them back, and turns the `CharacterController` off during the teleport.
  - It retries a few times, and falls back to a virtual lane offset from lane 0.
- **`Assets/HouseTag.cs`** + **`Assets/Phase3PopupUI_Manual.cs`:** the house's state is replicated (elements, serial, `IsAssigned`). `HouseTag` notices when the replicated `IsAssigned` flag flips in `Render()` and raises a static event, so every open popup disables that button right away. While open, the popup also re-checks all houses every frame as a fallback.
- **`Assets/GhostOwnerBillboard.cs`:** builds a `TextMesh` at runtime, turns it toward the camera and scales it with distance so it stays readable. It shows the replicated request line only when the local player owns the ghost, and switches off the older label components.
- **`Assets/ScriptsImade/HouseRandomizer.cs`** (earlier design, not used in the current level): a backtracking search that gives a random set of houses unique one- or two-element combinations under a per-element usage cap. It then switches on the matching objects on each house.

## Scenes (build order)

| # | Scene | Purpose |
|---|---|---|
| 0 | `Assets/Clean Multiplayer Pro/Scenes/Menu.unity` | Main menu titled *RealEstate Horror*: name, region, character selection, room list and room creation, settings (template UI). An intro-video-then-menu sequence (`wait.cs`) is in the scene but switched off. |
| 1 | `Assets/Clean Multiplayer Pro/Scenes/Game.unity` | Session scene: template HUD (chat, player list, vote-kick, inventory, pause, leave), Fusion managers and the first-person camera. `InGame_Manager` plays the intro video (`Mchunter In.mp4`) and swaps Environment 1 for Environment 2. A *Switch Scene* HUD button starts the same sequence. |
| 2 | `Assets/Clean Multiplayer Pro/Scenes/Environment 1.unity` | The template's grey-box playground. Loaded additively when the session starts, unloaded when the game level loads. |
| 3 | `Assets/Clean Multiplayer Pro/Scenes/Environment 2.unity` | The game level. It contains the front desk with three lanes (spawn, desk, leave and standing points), the ghost, house and assignment managers, the element list, the house-picker popup and the debug hotkeys. Uses 16 of my scripts. |
| – | `Assets/Clean Multiplayer Pro/Scenes/Intro.unity` | Early standalone intro scene (`wait.cs`). Unchecked in Build Settings, and its video clip is no longer in the project. |

The menu starts the session in `Game` by scene name. `Environment 1` and `Environment 2` are loaded by build index (2 and 3), so keep this order.

## Integrated third-party assets

The game is built on top of these packs. Only the files the scenes above use, plus the code needed to compile, are included.

| Asset | Used for |
|---|---|
| Clean Multiplayer Pro (Unity Asset Store template) | The multiplayer shell: menu, room list, Fusion session start and player spawning, text chat, push-to-talk voice, vote-kick, inventory pickups, pause and settings, three player prefabs with a Starter Assets–based controller, and the grey-box playground the game level is built in. Changed for this game: `ThirdPersonController.cs`, `InGame_Manager.cs`, `Player Prefab Green.prefab` and the Menu, Game and Environment 2 scenes. |
| Photon Fusion 2 SDK (2.0.6) + Fusion Physics add-on | Networking |
| Photon Voice 2 (2.59), including the Photon Realtime and Chat libraries it ships with | Voice chat used by the template |
| ParrelSync (1.5.2) | Multiplayer testing with clone editors. Its source is in `Assets/Clean Multiplayer Pro/Scenes/ParrelSync-master/` and its settings asset is in `Assets/Plugins/ParrelSync/`. |
| TextMesh Pro | UI and world-space text |

## About this repository

This public repository is a **showcase**. It contains the documentation and the **33 source files I wrote** for this project. The complete project, including licensed third-party assets that cannot be redistributed, is kept in a private repository.

Copyright © Omer Avcioglu (McHunter Studio). **All rights reserved.** Viewing only; see [LICENSE](LICENSE).
