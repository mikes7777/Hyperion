# Hyperion — Unity prototype

Hyperion is a playable 3D arcade racer set on a floating circuit in deep space. The entire first version is generated at runtime: the orbital track, glowing rails, starfield, planets, four supercars, AI opponents, lighting, UI, and engine sound all come from the included C# scripts. There are no asset packs or downloads to reconnect.

## Open and play

1. Install **Unity 2022.3 LTS** (Built-in Render Pipeline). Unity 6 can also open and upgrade the project.
2. In Unity Hub, choose **Add project from disk** and select this folder.
3. Open `Assets/Scenes/Hyperion.unity` if Unity does not open it automatically.
4. Press **Play**.

The default car is the orange GT3 RS. Pick a different car or NPC difficulty in the garage, then select **Launch Race**.

## Controls

| Action | Keyboard |
| --- | --- |
| Accelerate | `W` or `Up Arrow` |
| Brake | `S` or `Down Arrow` |
| Steer | `A` / `D` or arrow keys |
| Nitro | `Space` or `Shift` |
| Pause | `Esc` or `P` |
| Restart race | `R` |

## Included gameplay

- Four cars with different top speed, acceleration, handling, and nitro behavior
- GT3 RS selected by default
- Rookie, Sport, and Apex NPC difficulty settings
- Six AI opponents with varied pace, racing lines, overtakes, mistakes, and rubber banding
- Three-lap race, live position, lap counter, timer, speedometer, nitro meter, pause, and results
- Rigidbody arcade driving, collisions, edge slowdown, recovery, track assist, and speed-sensitive camera
- Procedural engine audio that reacts to speed and nitro
- Procedural space track, neon rails, lane markers, gantry, planets, moons, rings, stars, and lighting

## Project layout

- `HyperionGame.cs` — bootstrap, race flow, garage, HUD, pause, and results
- `ArcadeCarController.cs` — player physics and input
- `StellarAICar.cs` — NPC pace and racing lines
- `StellarTrack.cs` — generated track mesh, colliders, rails, and spline sampling
- `StellarCarFactory.cs` — car catalog and generated car models
- `SpaceEnvironment.cs` — planets, starfield, rings, sun, and lighting
- `StellarFollowCamera.cs` — showroom orbit and race chase camera
- `StellarEngineAudio.cs` — asset-free procedural engine sound

## Useful tuning points

Car stats live in `StellarCarCatalog.Cars` in `StellarCarFactory.cs`. Difficulty pace and behavior live in the `Difficulties` array near the top of `HyperionGame.cs`. Track shape and width live in `StellarTrack.cs`.

This prototype uses simple generated geometry so the gameplay can be reviewed immediately. Replacing a generated car with a licensed production model later does not require changing the race or AI systems.
