# ItemConduit-Revisited Tech Stack

## Overview
Valheim mod using BepInEx + Jotunn framework for item transfer network system.

---

## Core Framework

| Component | Version | Purpose |
|-----------|---------|---------|
| **BepInEx** | 5.4.23.3 | Mod framework, DLL injection, config management |
| **Jotunn** | 2.21.x | Valheim abstraction (PieceManager, RPC, prefab cloning) |
| **Harmony** | 2.x | Runtime patching for game integration |
| **Unity** | 6000.0.46f1 | Asset bundle creation (matches Valheim) |

---

## Development Environment

- **IDE**: Visual Studio 2022 (C# workload)
- **Framework**: .NET Framework 4.6.2 (Valheim target)
- **Language**: C# 9 (max supported by .NET Framework 4.6.2)
- **Build**: `dotnet build` CLI + post-build script (TBD)

---

## Required DLL References (./libs/)

```
assembly_valheim.dll
assembly_guiutils.dll
assembly_utils.dll
UnityEngine.dll
UnityEngine.CoreModule.dll
UnityEngine.PhysicsModule.dll
UnityEngine.UI.dll
0Harmony.dll
BepInEx.dll
Jotunn.dll
```

---

## Project Structure

```
ItemConduit-Revisited/
├── ItemConduit/                    # Main C# project
│   ├── Plugin.cs                   # BepInEx entry point
│   ├── Config/                     # Configuration management
│   ├── Core/                       # Core systems
│   │   ├── ConduitNetworkManager.cs
│   │   └── ConduitNetwork.cs
│   ├── Components/                 # Unity MonoBehaviours
│   │   ├── Conduit.cs
│   │   └── ConduitVisualizer.cs
│   ├── Interfaces/                 # Abstractions
│   │   └── IContainerInterface.cs
│   ├── Containers/                 # Container implementations
│   │   └── VanillaContainer.cs
│   ├── Transfer/                   # Item transfer logic
│   │   └── TransferManager.cs
│   ├── Collision/                  # OBB-SAT detection
│   │   └── OBBCollision.cs
│   ├── GUI/                        # UI components
│   │   ├── ConduitGUIManager.cs
│   │   └── ConduitConfigPanel.cs
│   ├── Patches/                    # Harmony patches
│   │   └── ZDOManPatches.cs
│   ├── Commands/                   # Console commands
│   │   └── ConduitCommands.cs
│   └── Utils/                      # Utilities
├── libs/                           # Reference DLLs
├── docs/                           # Documentation
├── plans/                          # Implementation plans
└── ItemConduit.sln                 # Solution file
```

---

## Key Technical Decisions

### 1. Network Architecture
- **Server-side item transfer**: All transfers happen at ZDO level on server
- **Client-side visualization**: Wireframes and hover text client-only
- **ZDO custom fields**: Prefixed with `IC_` for namespace isolation

### 2. Prefab Cloning
- Clone `wood_beam` variants (1m, 2m, vertical)
- Remove `WearNTear` component for durability
- Register via `PieceManager` to Hammer tool

### 3. Collision Detection
- **OBB-SAT** for conduit connection detection
- Store bounds in `IC_Bound` ZDO field
- Spatial queries on server for network building

### 4. Synchronization Strategy
- Patch `ZDOMan.RPC_Data` to intercept conduit/container ZDOs
- Detect server vs client caller and process accordingly
- Use ZDO fields for persistent state, RPC for actions

---

## Configuration (BepInEx)

```csharp
[BepInPlugin("com.void.itemconduit", "ItemConduit", "1.0.0")]
[BepInDependency(Jotunn.Main.ModGuid)]
[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
```

### Config Options
| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `TransferRate` | int | 1 | Items per transfer tick |
| `TransferTick` | float | 1.0 | Seconds between transfers |
| `Wireframe` | bool | false | Toggle visualization |

---

## ZDO Custom Fields

| Field | Type | Description |
|-------|------|-------------|
| `IC_Mode` | int | 0=conduit, 1=extract, 2=insert |
| `IC_ContainerZDOID` | ZDOID | Connected container |
| `IC_ConnectionList` | byte[] | ZPackage binary serialized ZDOID list |
| `IC_NetworkID` | string | Network identifier (GUID) |
| `IC_Channel` | int | Transfer channel |
| `IC_Priority` | int | Transfer priority |
| `IC_FilterList` | string | Item prefab names |
| `IC_FilterMode` | int | 0=whitelist, 1=blacklist |
| `IC_Bound` | string | Serialized OBB bounds |
| `IC_TransferRate` | int | Override transfer rate |

---

## Performance Targets

- Support 600+ extract/insert nodes
- Frame-spread transfer to avoid frame spikes
- Spatial partitioning for network building queries
