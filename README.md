# StellarCommand

A VR command bridge for Stellaris. Stand at the helm of your galactic empire — resources, fleets, galaxy map and alerts displayed as holographic panels around you in real time.

## Requirements

- Unity 6 LTS (6000.0.x) with HDRP
- Meta Quest 3 + Meta Quest Link (Air Link or USB)
- Stellaris (Steam)
- Windows 10/11

## Features (roadmap)

- [ ] 3D holographic command bridge environment
- [ ] Live Stellaris save file parsing
- [ ] Galaxy map rendered in 3D
- [ ] Resource & economy panels
- [ ] Fleet tracker
- [ ] Event / alert feed
- [ ] VR hand interaction with panels

## Setup

1. Open in Unity Hub → Unity 6 LTS
2. Install Meta XR SDK via Package Manager
3. Configure OpenXR for Meta Quest
4. Connect Quest 3 via Air Link
5. Press Play

## Architecture

```
Assets/
├── Scripts/
│   ├── SaveParser/     # Stellaris .sav file reader
│   ├── UI/             # Holographic panel logic
│   ├── Galaxy/         # 3D galaxy map renderer
│   └── VR/             # VR interaction & setup
├── Scenes/
│   └── CommandBridge   # Main VR scene
├── Shaders/            # Hologram & sci-fi shaders
└── Prefabs/            # Reusable UI panels
```

## Inspired by

[Stellaris In VR-is](https://johnjoemcbob.com/stellaris_in_vr-is/) by johnjoemcbob — rebuilt from scratch for Unity 6, Meta Quest 3, and modern Stellaris.
