# AFrame Game Workspace

`Assets/Game` contains project-owned features and composition assets. Reusable framework code lives in the local UPM packages under `Packages/com.aframe.*`.

Each feature keeps public Contracts, Unity-independent Core code, and Unity adapters in separate assemblies. Feature data sources live under `Definitions`; generated runtime data is maintained by the AFrame import command.

Use the Unity menu under `AFrame` to import definitions, generate feature scaffolding, validate architecture, and build the machine-readable project context used by development tools.

Generated files under `Assets/Game/Generated` are outputs. Project context is generated under the root `Context` directory. Edit source definitions, feature metadata, and task files instead.
