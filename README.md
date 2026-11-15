# AFrame

AFrame is a modular Unity 6 framework for small and medium-sized games. It keeps gameplay rules in pure C# modules and uses Unity as the composition and presentation layer.

The implementation target is documented in [Docs/AFrame-Architecture-v2.1.md](Docs/AFrame-Architecture-v2.1.md). The document distinguishes the current v1 implementation from the v2.1 migration target.

## Project layout

- `Packages/com.aframe.*` contains reusable framework modules.
- `Assets/Game/Features` contains project-owned vertical features.
- `Assets/Game/Generated` contains generated definition assets and machine-readable reports.
- `Tasks` contains bounded development task contracts.

## Editor workflow

Use the `AFrame` menu in Unity:

- `AFrame/Generate/Feature`
- `AFrame/Content/Import Definitions`
- `AFrame/Project/Validate Architecture`
- `AFrame/Project/Build Context`
- `AFrame/Project/Run Smoke Check`

The framework does not call an AI service. Its package boundaries, task files and validation reports give external development tools a stable way to extend the project without adding hidden global state or cross-feature dependencies.

## Runtime flow

The target runtime uses a fixed Tick. Input and network adapters enqueue typed commands, domain modules handle them, and queued domain events update views, UI, saving and network adapters. The first migration slice keeps the current Editor menu workflow while the runtime contracts are upgraded.

The first sample feature is `Character`, split into a Unity-independent domain assembly and a Unity adapter assembly.
