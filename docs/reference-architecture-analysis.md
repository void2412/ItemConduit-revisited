# ItemConduit Reference Architecture Analysis

## Executive Summary

The original ItemConduit is a Valheim mod framework using Jötunn that implements an interconnected item transfer network system. It's built for extensibility, supporting multiple container types through abstraction layers while maintaining clean separation between game integration (patches), business logic (network/nodes), and presentation (GUI).

---

## 1. Project Structure & Organization

### Three-Tier Architecture
- **Plugin Layer**: C# DLL compiled from Visual Studio solution
- **Asset Pipeline**: Dedicated Unity 2022.3.17f project for AssetBundles
- **Build System**: Automated deployment via PowerShell/Bash scripts

### Key Directories
```
ItemConduit/
├── Config/           # Configuration management (5 files)
├── DebugHelpers/     # Visualization utilities
├── Extensions/       # Container type adapters (8+ types)
├── GUI/              # UI components (insert/extract/filter nodes, tooltips)
├── Interfaces/       # Abstraction contracts (IContainerInterface, IFilterNode)
├── Network/          # Core networking (ConduitNetwork, NetworkManager, RebuildManager)
├── Nodes/            # Node types (BaseNode, ExtractNode, InsertNode, ConduitNode)
├── Patches/          # Harmony patches for game integration (10+ container types)
├── Properties/       # Assembly metadata
└── Utils/            # Utilities (ContainerWrappers, NodeRegistration)
```

---

## 2. Key Classes & Responsibilities

### Core Network System
| Class | Responsibility |
|-------|-----------------|
| `ConduitNetwork` | Manages connected node graph; validates networks exist (extract + insert nodes); tracks channels for multi-path distribution |
| `NetworkManager` | Orchestrates network lifecycle; handles persistence via ZDO |
| `BaseNode` | Abstract node with snappoint/OBB connection detection; RPC sync; optional visualization |
| `ExtractNode` / `InsertNode` | Source/destination nodes connected to containers |
| `ConduitNode` | Intermediate transfer points |

### Container Abstraction
| Class | Responsibility |
|-------|-----------------|
| `IContainerInterface` | Unified protocol for all container types (Add/Remove items, capacity checks) |
| `BaseExtension<T>` | Generic extension wrapping game containers; manages separate ZDO-persisted inventory |
| Container Extensions | 8+ specialized implementations (StandardContainer, Smeltery, Beehive, etc.) |

### UI & Presentation
| Class | Responsibility |
|-------|-----------------|
| `GUIController` | Singleton managing all node GUIs; tracks focus state; prevents input conflicts |
| `BaseNodeGUI` | Base window for node interaction |
| `ExtractNodeGUI` / `InsertNodeGUI` | Input/output node UI |
| `FilterNodeGUI` | Filter logic interface |
| `ItemDatabase` | Item info lookup |

### Integration
| Class | Responsibility |
|-------|-----------------|
| Harmony Patches | Injects extensions into containers; blocks duplicate interactions |
| `NodeRegistration` | Handles node discovery and registration |

---

## 3. Conduit Networking Implementation

### Connection Detection Strategy
**Dual-Mode System:**
- **Snappoint Detection**: Aligned connections using predefined snap points
- **OBB (Oriented Bounding Box) Detection**: Angled/irregular placements via collision bounds

**Coroutine-Based Async Detection:**
Lifecycle: `Awake()` → delayed `Start()` → continuous polling avoids race conditions with physics.

### Network Topology
```
Extract Node → Conduit Node(s) → Insert Node
                  ↓
            (Multi-channel support via unique IDs)
```

### Validation
Networks require `Extract.Count > 0 AND Insert.Count > 0` to be valid. Enables partial network construction.

### Synchronization
- **RPC-based**: Network IDs and active states synchronized across clients
- **ZDO-based**: Persistent container inventory state

---

## 4. Item Transfer Logic

### Flow Architecture
1. **Extraction Phase**: ExtractNode sources items from attached container
2. **Routing Phase**: Items traverse ConduitNode chain
3. **Insertion Phase**: InsertNode deposits into target container

### Channel-Based Distribution
`GetActiveChannels()` collects unique channel IDs from extract/insert nodes, enabling **labeled distribution paths** within single network (e.g., ore → smelter, ore → storage based on channel selection).

### Container Adaptation
Each container type (chest, furnace, beehive, etc.) wrapped via extension providing:
- Capacity calculation with type-specific logic
- Item filtering (e.g., only ore into smelter)
- ZDO inventory persistence

---

## 5. UI/GUI Implementation Approach

### Central Controller Pattern
`GUIController` singleton maintains:
- **Registry**: HashSet of active BaseNodeGUI windows
- **Focus Tracking**: Distinguishes UI typing vs gameplay input
- **Cascading Cleanup**: `CloseAll()` method to close all GUIs simultaneously

### Input Blocking Architecture
```
Harmony Patches
    ↓
HasActiveGUI() / HasInputFieldFocus()
    ↓
ConditionallyBlockPlayerInput
```

### UI Lifecycle
1. GUI registers on creation
2. System tracks focus state during text input
3. Patches query controller before processing player input
4. GUI unregisters/cleanup on close

### Widget Reusability
- `BaseItemSlot`: Reusable item display component
- `Itemtooltip`: Info overlay
- `HoverUI`: Context-sensitive help

---

## 6. Patterns Worth Learning

### 1. Interface-Based Container Abstraction
`IContainerInterface` enables polymorphic handling of 8+ container types without conditional logic. Each extension implements a unified protocol.

**Why It Works:**
- Decouples node logic from container specifics
- Adding new container types requires only new extension implementation
- No modification to core transfer logic needed

### 2. Extension Component Pattern
Rather than patching container classes directly, extensions added via Harmony `Awake` postfix:
```csharp
[HarmonyPatch(typeof(Container), "Awake")]
Postfix → Add StandardContainerExtension component
```

**Advantage**: Container functionality remains unchanged; mod behavior layered on top.

### 3. Bidirectional Observer Notifications
Extensions track connected nodes (Extract/Insert) within range and notify them of state changes. Nodes track their target containers. Creates **self-healing network** when containers move.

### 4. Dual Detection Methods (Snappoints + OBB)
Accommodates both grid-aligned and arbitrary placements. OBB falls back when snappoints insufficient.

### 5. Async Coroutine-Based Detection
Delayed `Start()` with coroutine-based polling avoids race conditions with physics simulation initialization.

### 6. Singleton with Global Access
```csharp
public static ItemConduit Instance { get; private set; }
```
Enables centralized initialization while avoiding tight coupling at component level.

### 7. Harmony Prefix for Behavior Override
Using prefix patches to return false blocks original method execution entirely—effective for "exclusive control" scenarios where extension should handle interaction.

### 8. ZDO-Based Persistence
Leveraging Valheim's ZDO (zone data object) system enables automatic network synchronization without custom RPC implementations for all state changes.

---

## Key Takeaways for ItemConduit-Revisited

1. **Start with interface abstraction** - IContainerInterface pattern proven scalable
2. **Use Harmony extensions smartly** - Postfix for injection, prefix for override
3. **Implement bidirectional observers** - Self-healing networks reduce manual sync needs
4. **Central UI controller** - Prevents input conflicts elegantly
5. **Async detection via coroutines** - Avoids physics race conditions
6. **Channel-based routing** - Enables flexible item distribution without complex pathfinding

---

## Unresolved Questions

None identified. Architecture clearly documented through source code analysis.
