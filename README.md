# Last Shelter Protocol — Core Systems

Selected scripts from **Last Shelter Protocol**, a mobile survival game published on Google Play Store. The game is a real-time multiplayer experience where players compete to loot, survive a shrinking hazard zone, and eliminate opponents in a shelter showdown.

These files showcase the core engineering systems built with **Unity (C#)** and **Firebase**.

---

## Systems

### Session & Persistence — `GameManager.cs`
Manages the full game lifecycle (start, continue, death, save-and-quit) with Firebase Firestore as the backend. Each run is stored as a document with a unique GUID; the player's active game ID is kept in a separate `Users` collection so the "Continue" button only appears when a live session exists.

### Inventory Sync — `InventoryManager.cs`
Handles player and NPC inventories across scenes. Uses a dirty-flag pattern to batch writes every 10 seconds instead of writing on every pickup. NPC inventories are snapshotted per `uniqueID` on save and restored individually when each NPC spawns — surviving scene reloads without full re-serialization.

### Character Controller — `PlayerMovement.cs`
Built on Unity's new Input System. Input smoothing with configurable lerp speed, hysteresis on the moving/idle threshold to prevent flicker, and height-aware pick animations (crouching vs. standing). Sprint, jump, kick, and dance states block movement independently.

### Combat — `PlayerCombat.cs`
Uses `Physics.OverlapSphereNonAlloc` with a pre-allocated buffer to detect hits with zero heap allocation per frame — important for stable frame rates on mid-range mobile hardware. A failsafe `Invoke` unlocks the attack state if Unity swallows the animation event.

### Hazard Zone — `HazardZoneController.cs`
A shrinking sphere that moves toward the shelter exit over time (Battle Royale-style). Tracks inside/outside status per player in a `Dictionary` and applies damage on a fixed interval rather than every frame. The `SphereCollider` radius is kept in sync with the Transform scale manually, since Unity does not propagate non-uniform scale to physics automatically.

### Dynamic Music — `DynamicMusicManager.cs`
Drives a layered audio mix based on the percentage of time remaining. Tension and panic tracks fade in independently at configurable thresholds; one-shot stingers are guarded by boolean flags so they fire exactly once per round; a per-second tick countdown plays in the final 10 seconds.

---

## Architecture Notes

- `PlayerBehaviour` and `NPCBehaviour` are abstract base classes. Scene-specific logic (loot phase vs. shelter phase) is added as a component at runtime by `PlayerController`, keeping per-scene code isolated without inheritance chains.
- All Firebase writes use `async/await` and never block the main thread.
- Singletons (`GameManager`, `InventoryManager`, `DayManager`) use `DontDestroyOnLoad` and are the only cross-scene state carriers.

---

*Built with Unity 6 · Firebase SDK · Unity New Input System · Cinemachine*
