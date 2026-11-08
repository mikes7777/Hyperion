# Hyperion V2 — Vesper Ring

A rebuilt Unity arcade racer on a 2.04 km orbital circuit. Four original sculpted hypercars, six traffic-aware rivals, banked corners, drifting, nitro, three camera views, a planetary sky and a full garage-to-results race flow.

## Open and play

1. Extract this archive into a **new folder**; keep your V1 project separately.
2. In Unity Hub, add the extracted `Hyperion-Unity-v2` folder containing `Assets`, `Packages` and `ProjectSettings`.
3. Open with **Unity 2022.3.62f1 LTS**, using the **Built-in Render Pipeline**. The project does not require URP, HDRP or asset-store downloads.
4. Allow the first import to finish. Open `Assets/Scenes/Hyperion.unity` and press **Play**.
5. Choose a car, Grand Prix or Time Attack, and Launch. Click the Game view so it receives keyboard input.

The scene intentionally looks empty before Play: the world is built by the runtime bootstrap. There is no separate scene/prefab setup step. Unity generates the omitted `Library` cache and machine-specific settings on first import. Do not migrate this project into a URP template; its shaders and finish pass target the Built-in pipeline.

**Verification limit:** the runtime C# sources compile against Unity 2022.3.62 engine assemblies. Circuit geometry and free-running AI have independent numerical checks. The Unity Editor is unavailable in the development environment, so actual import, rendering, shaders, collisions, handling feel and a complete played race still need verification in Unity. This ZIP is the source project, not a tested standalone executable.

## Controls

| Action | Keyboard |
|---|---|
| Accelerate | W / Up |
| Brake; reverse when nearly stopped | S / Down |
| Steer | A / D or Left / Right |
| Nitro | Space / either Shift |
| Handbrake / drift | either Ctrl |
| Camera: Chase / Close / Bonnet | C |
| Recover to the road | F |
| Restart event | R |
| Pause / resume | Esc / P |
| Launch from garage | Enter |

Recovery stops the car at its current race distance; it never awards forward progress. Nitro needs forward speed, throttle and reserve. After emptying the tank, release the boost key before boosting again. Drifting regenerates reserve faster. Use the brake boards before the tighter uphill turns; full throttle through every corner is not the intended line.

## Events and settings

- **Grand Prix:** three laps against six rivals. Rookie, Sport and Apex change pace, corner speed, mistakes and modest catch-up. Rivals brake for actual upcoming curvature and consider nearby traffic before choosing a passing lane. They use kinematic movement, not the player's full chassis simulation.
- **Time Attack:** three solo laps without rivals. Lap records are saved per car locally. The race clock starts at GO; the lap clock starts when you cross the timing line, excluding the grid run-up.
- **Settings:** master volume, camera shake and High/Performance quality persist locally. Performance mode removes MSAA/shadows and skips bloom. Pause and focus loss freeze race progress and lap timing. The standalone garage includes Quit.

## What changed from V1

- Car-relative tire grip, smoothed steering, speed-dependent turning, handbrake slip, counter-steer, bank-aware chassis, reverse and progress-safe recovery.
- Four curved mesh bodies with wheel arches, panoramic cabins, carbon aero, cooling vents, brake details, spinning/steering wheels, brake lamps, boost plumes and drift trails. Static surfaces and wheel details are combined by material to reduce draw calls.
- A new 22 m-wide, 2,036.9 m circuit with ~51 m of elevation variation, smooth banking up to 19 degrees, physical safety walls, kerbs, illuminated guides, braking signs, structural supports and an orbital gallery.
- Equal-distance track sampling, seam-safe nearest-road projection and curvature-aware AI braking. Finished AI clears the timing line without blocking remaining racers.
- Three chase/bonnet views, velocity look-ahead, gradual horizon roll, speed/boost FOV and adjustable thrust vibration.
- Nebula sky, detailed cloud planets, atmospheric rims, striated rings, stars, balanced lighting, bloom and filmic highlight rolloff.
- New garage, live race order, minimap, lap timing/splits, nitro display, results and local records.
- Layered engine/gear synthesis, tire scrub, boost and wind, plus countdown/launch/lap/finish cues. Audio mutes appropriately outside driving.

The models, geometry, shader effects and synthesized audio are original procedural content bundled in the source. There are **no imported photorealistic production-car assets**. The retained V1 “GT3 RS” garage label identifies its existing handling slot; its mesh is an original stylized car, not a licensed replica.

## First local verification

1. After import, run **Tools > Hyperion > Validate V2** outside Play mode. The included editor tool checks the scene/build entry, finite car/track meshes, track frame/projection consistency and imported resource shaders in a temporary scene.
2. Play a Rookie Grand Prix: confirm the countdown, correct forward direction, steering/braking, safe barriers, all three cameras and recovery.
3. Pause during countdown and racing; resume, restart, return to the garage and change car. Confirm engine audio and wheels stop while paused.
4. Complete all three laps, check results, then run Time Attack and verify a personal best survives restarting Play mode.
5. Build through **File > Build Settings** for your installed platform module. `Hyperion.unity` is already the enabled scene. Make a development build first and inspect its log for runtime or shader errors.

For a headless editor validation after Unity is installed:

```sh
Unity -batchmode -nographics -projectPath /path/to/Hyperion-Unity-v2 \
  -executeMethod HyperionValidation.RunBatch -logFile validation.log
```

See `VALIDATION.md` for completed checks and limitations.

## Code map

| File | Responsibility |
|---|---|
| `HyperionGame.cs` | Bootstrap, garage, race states, timing, records, UI |
| `ArcadeCarController.cs` | Player input, chassis forces, drift, boost, recovery |
| `StellarAICar.cs` | Corner speed preview, traffic, lane planning, rival progress |
| `StellarTrack.cs` | Equal-distance spline, frames, track geometry and barriers |
| `StellarCarFactory.cs` | Catalog, curved car meshes, materials, mesh combining |
| `StellarVehicleVisuals.cs` | Wheel animation, lamps, boost, trails, mesh cleanup |
| `StellarFollowCamera.cs` | Showroom/race views and post-processing |
| `SpaceEnvironment.cs` | Planets, sky, stars, rings and lighting |
| `StellarEngineAudio.cs` | Audio-thread-safe engine/tire/wind/boost synthesis |
| `StellarRaceAudio.cs` | Interface and race cues |
| `StellarSettings.cs` | Persistent audio/camera/quality preferences |
| `Assets/Editor/HyperionValidation.cs` | Local Unity editor validation |
| `Assets/Shaders/Resources` | Resources-retained Built-in shaders |

Car stats live in `StellarCarCatalog.Cars`; chassis tuning lives in `ArcadeCarController`; control points, width and bank tuning live in `StellarTrack`. Keep changes in a copy until you've driven a complete race.
