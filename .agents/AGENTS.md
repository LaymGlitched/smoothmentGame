## Player Architecture
The `Player` GameObject in `smoothmentGame` uses a modular component structure consisting of the following modules:
1. **Core Physics & Collision**: `Transform`, `Rigidbody` (Continuous Collision, mass 1), `CapsuleCollider`.
2. **Movement & View**: `RigidbodyFPController`, `MouseLookController`, `GroundSensor`, `PlayerInputHandler`, and `FPAnimationController`.
3. **Parkour / Advanced Movement**: `LedgeTraversalController`, `WallRunController`, `WallDetector`, `SlideController`, and `AirDashController`.
4. **Stats & Combat**: `Health`, `Mana`, `StaminaSystem` (currently disabled), and `PlayerDeathHandler`.
5. **Magic**: `SpellCaster` (handles spell cycling, charging, and casting with specific hit masks).
