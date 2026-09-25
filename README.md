# BlockShift - A Tile-Based Puzzle Game Engine

## Overview

**BlockShift** is a sophisticated tile-based puzzle game built in Unity with C#. The game features multiple floating islands connected by a grid-based movement system, gravity mechanics, block manipulation, and dynamic puzzle solving. Players interact with various types of blocks to navigate and complete objectives.

---

## Table of Contents

1. [Core Architecture](#core-architecture)
2. [System Components](#system-components)
3. [Key Systems](#key-systems)
4. [Class Diagram (UML)](#class-diagram-uml)
5. [Game Flow](#game-flow)
6. [Data Structures](#data-structures)
7. [How to Use](#how-to-use)
8. [Extensibility](#extensibility)

---

## Core Architecture

BlockShift uses a **multi-island grid-based system** where:

- **GridIsland**: Independent floating islands with their own grid systems
- **Block**: Polymorphic block types with different behaviors (Movable, Immovable, Dynamic, etc.)
- **MultiGridManager**: Manages communication between multiple islands
- **GravityManager**: Applies physics-like gravity to blocks
- **LevelLoader**: Constructs levels from serialized data

### Design Patterns Used

- **Strategy Pattern**: Different block types implement different movement behaviors
- **Observer Pattern**: Input system observes player actions
- **Factory Pattern**: Level loader creates blocks based on tile type
- **Singleton Pattern**: Manager components manage global state

---

## System Components

### 1. **Block System** (Block.cs)

Base abstract class for all interactive objects in the game.

**Key Properties:**
- `gridPosition`: 2D position on the island grid
- `currentIsland`: Reference to the island containing this block
- `spriteRenderer`: Visual representation
- Visual feedback (color changes, shake animations)

**Key Methods:**
- `CanMoveTo()`: Check if movement to target position is legal
- `CanPlayerMoveDirectly()`: Can player interact with this block?
- `IsAffectedByGravity()`: Does gravity apply to this block?
- `MoveToGridPosition()`: Animate block movement

**Subclasses:**
- **DynamicBlock**: Player-controlled blocks (affected by gravity, player movable)
- **MovableStaticBlock**: Can move but not affected by gravity
- **ImmovableBlock**: Fixed in place, cannot move
- **JointBlock**: Special block for complex interactions
- **GroundDeployerBlock**: Creates ground tiles while moving

---

### 2. **GridIsland System** (GridIsland.cs)

Represents a single floating island with its own grid-based coordinate system.

**Key Properties:**
- `islandID`: Unique identifier
- `width`, `height`: Grid dimensions
- `originPosition`: World position of island origin
- `cellGrid[,]`: Data grid storing tile types
- `localGrid[,]`: Reference grid storing block instances
- `islandBlocks`: List of all blocks on this island

**Key Methods:**

| Method | Purpose |
|--------|---------|
| `InitializeIsland()` | Set up island with initial grid state |
| `GridToLocalPosition()` | Convert grid coordinates to 3D world position |
| `WorldToGridPosition()` | Convert world position to grid coordinates |
| `GetBlockAtLocalPos()` | Retrieve block at specific grid position |
| `RegisterBlock()` | Add block to island grid |
| `RemoveBlock()` | Remove block from island grid |
| `ExpandGridIfNeeded()` | Dynamically grow grid when blocks move beyond bounds |
| `DeployGroundTileAt()` | Spawn ground tile at location |
| `MergeOtherIsland()` | Combine two islands into one |
| `IsValidLocalPos()` | Check if position is within grid bounds |

**Grid Coordinate System:**
```
(0,H-1) ──────── (W-1,H-1)
   │                 │
   │    GRID         │
   │   (0-based)     │
   │                 │
(0,0) ──────────── (W-1,0)

Origin: Bottom-left (0,0)
X-axis: Left to Right
Y-axis: Bottom to Top
```

---

### 3. **MultiGridManager** (MultiGridManager.cs)

Central hub for managing communication between multiple islands.

**Key Methods:**
- `RegisterIsland()`: Add island to active management
- `GetActiveIslands()`: Retrieve all current islands
- `GetIslandAtWorldPos()`: Find island containing world position
- `TryTransferBlockBetweenIslands()`: Move block from one island to another
- `ClearIslands()`: Reset all islands

---

### 4. **Gravity System** (GravityManager.cs)

Simulates gravity by dropping blocks downward until they rest on support.

**Physics Rules:**
1. Each block checks if the cell below is occupied
2. If empty, block falls one grid unit
3. Process repeats until all blocks are stable
4. Multiple passes ensure cascading falls resolve correctly

**Key Methods:**
- `ApplyGravityRoutine()`: Coroutine applying gravity to all islands
- Blocks fall with smooth DOTween animations

---

### 5. **Player System** (PlayerController.cs)

Manages player character movement and interaction with the game world.

**Features:**
- Waypoint-based pathfinding
- Obstacle detection via raycasting
- Animation state management (Idle, Run)
- Hover idle animation
- Sprite orientation (facing direction)

---

### 6. **Input System** (PlayerInput.cs)

Handles player input for block manipulation.

**Supported Actions:**
- Block selection via clicking
- Swipe direction detection (Up, Down, Left, Right)
- Input validation (is block movable?)

---

### 7. **Level System**

#### **LevelData.cs**
Serializable data structure for level configuration.

**Key Classes:**
- `TileType` enum: Defines block types (Empty, Ground, Immovable, etc.)
- `GridCell`: Represents a single cell with type and color ID
- `GridIslandData`: Serializable island configuration
- `LevelData`: Complete level definition with all islands

#### **LevelLoader.cs**
Instantiates level from LevelData.

**Process:**
1. Parse LevelData 
2. Create GridIsland GameObjects
3. Spawn blocks based on tile types
4. Initialize MultiGridManager
5. Set up camera and environment

#### **LevelDataEditor.cs** (Editor Only)
Visual level editor in Unity Editor.

**Features:**
- Add/delete islands
- Configure island dimensions
- Paint tiles into grid
- Real-time preview
- Gizmo debugging

#### **LevelReset.cs**
Resets level to initial state for retry functionality.

---

### 8. **Camera System**

#### **CameraController.cs**
Manages camera positioning and movement.

#### **ParallaxController.cs**
Implements parallax scrolling for depth effect.

#### **CameraConfig.cs**
Configuration data for camera behavior.

---

### 9. **Utility Systems**

#### **DynamicFpsManager.cs**
Optimizes frame rate based on gameplay state.

#### **PlayerGoal.cs**
Defines level objectives and win conditions.

#### **LevelDataGizmoDrawer.cs**
Visualizes grid and island bounds in editor.

---

## Key Systems

### Movement System

**Player Interaction Flow:**
```
PlayerInput.DetectSwipe()
	↓
Block.TryMove(direction)
	↓
CanMoveTo(targetPos) ← Check for obstacles
	↓
GridIsland.ExpandGridIfNeeded() ← Grow if moving to edge
	↓
GridIsland.CheckAndBridgeIslands() ← Merge if overlapping
	↓
GridIsland.RemoveBlock() + RegisterBlock()
	↓
Block.MoveToGridPosition() ← Animate movement
```

### Gravity System

**Gravity Application Flow:**
```
GravityManager.ApplyGravityRoutine()
	↓
For each Island in ActiveIslands:
	↓
	Scan grid bottom-to-top
	↓
	For each block: Check if cell below is empty
	↓
	If empty → Drop block one unit
	↓
	Animate fall with DOTween
	↓
Repeat until no blocks fell in entire pass
```

### Island Merging

**When two islands collide:**
1. Detection via overlap box on deployed ground tile
2. MergeOtherIsland() combines both islands
3. Grid expanded to accommodate both
4. All blocks transferred to main island
5. Coordinates automatically updated

### Ground Deployment

**GroundDeployerBlock specific logic:**
```
TryMoveAndDeploy(direction)
	↓
Check if target cell has ground
	↓
If empty AND ammo > 0:
	├─ Deploy ground tile
	├─ Decrement ammo
	└─ CheckAndBridgeAdjacentIslands()
	↓
Calculate final position post-merge
	↓
Move deployer to final position
```

---

## Class Diagram (UML)

```
┌─────────────────────────────────────────────────────────────────┐
│                        GAME ARCHITECTURE                         │
└─────────────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────────────────┐
│                     <<abstract>> Block                              │
├────────────────────────────────────────────────────────────────────┤
│ - gridPosition: Vector2Int                                          │
│ - currentIsland: GridIsland                                         │
│ - spriteRenderer: SpriteRenderer                                    │
│ - defaultColor: Color                                              │
│ - illegalColor: Color                                              │
├────────────────────────────────────────────────────────────────────┤
│ + CanMoveTo(targetPos): bool                                       │
│ + CanPlayerMoveDirectly(): bool <<abstract>>                       │
│ + IsAffectedByGravity(): bool <<abstract>>                         │
│ + MoveToGridPosition(pos)                                          │
│ + AnimateMovement(targetPos, duration)                             │
└────────────────────────────────────────────────────────────────────┘
		▲           ▲           ▲          ▲            ▲
		│           │           │          │            │
		│           │           │          │            │
	┌───┴────┐  ┌───┴──────┐ ┌──┴─────┐ ┌─┴──────┐  ┌──┴──────┐
	│ Dynamic │  │ Movable  │ │Immov- │ │ Joint  │  │ Ground  │
	│ Block   │  │ Static   │ │ able   │ │ Block  │  │Deployer │
	│         │  │ Block    │ │ Block  │ │        │  │         │
	├─────────┤  ├──────────┤ ├────────┤ ├────────┤  ├─────────┤
	│affected │  │not       │ │fixed   │ │special │  │deploys  │
	│by       │  │affected  │ │in      │ │inter-  │  │ground   │
	│gravity  │  │by grav   │ │place   │ │action  │  │tiles    │
	│player   │  │movable   │ │cannot  │ │complex │  │ammo     │
	│move     │  │not       │ │move    │ │logic   │  │system   │
	│         │  │player    │ │        │ │        │  │         │
	└─────────┘  └──────────┘ └────────┘ └────────┘  └─────────┘


┌────────────────────────────────────────────────────────────────────┐
│                      GridIsland                                     │
├────────────────────────────────────────────────────────────────────┤
│ - islandID: int                                                    │
│ - width, height: int                                               │
│ - originPosition: Vector2Int                                       │
│ - cellGrid[,]: GridCell                                            │
│ - localGrid[,]: Block                                              │
│ - islandBlocks: List<Block>                                        │
│ - scaleFactorX, scaleFactorY: int                                  │
├────────────────────────────────────────────────────────────────────┤
│ + InitializeIsland(id, w, h, origin, cells)                        │
│ + GridToLocalPosition(gridPos, z): Vector3                         │
│ + WorldToGridPosition(worldPos): Vector2Int                        │
│ + GetBlockAtLocalPos(pos): Block                                   │
│ + RegisterBlock(block, pos)                                        │
│ + RemoveBlock(pos)                                                 │
│ + ExpandGridIfNeeded(pos)                                          │
│ + DeployGroundTileAt(pos, prefab)                                  │
│ + MergeOtherIsland(otherIsland)                                    │
│ + IsValidLocalPos(pos): bool                                       │
│ + CanRemoveBlock(pos): bool                                        │
└────────────────────────────────────────────────────────────────────┘
							▲
							│
					  contains many
							│
					  ┌──────┴────────┐
					  │               │
					  │               │
				Block (0..*)      GridCell (W×H)


┌────────────────────────────────────────────────────────────────────┐
│                  MultiGridManager                                   │
├────────────────────────────────────────────────────────────────────┤
│ - activeIslands: List<GridIsland>                                  │
├────────────────────────────────────────────────────────────────────┤
│ + RegisterIsland(island)                                           │
│ + GetActiveIslands(): List<GridIsland>                             │
│ + GetIslandAtWorldPos(pos): GridIsland                             │
│ + TryTransferBlockBetweenIslands(block, src, pos): bool            │
│ + ClearIslands()                                                   │
└────────────────────────────────────────────────────────────────────┘
							▲
							│
					  manages
							│
			┌───────────────┴──────────────┐
			│                              │
	 GridIsland (0..*)          Block Transfer System


┌────────────────────────────────────────────────────────────────────┐
│                   GravityManager                                    │
├────────────────────────────────────────────────────────────────────┤
│ - fallStepDuration: float                                          │
│ - fallEase: Ease                                                   │
├────────────────────────────────────────────────────────────────────┤
│ + ApplyGravityRoutine(islands): IEnumerator                        │
│   - Scans each island's blocks                                     │
│   - Drops unsupported blocks                                       │
│   - Animates falls                                                 │
│   - Repeats until stable                                           │
└────────────────────────────────────────────────────────────────────┘
							▲
							│
					  applies to
							│
					  GridIsland


┌────────────────────────────────────────────────────────────────────┐
│                 PlayerController                                    │
├────────────────────────────────────────────────────────────────────┤
│ - moveSpeed: float                                                 │
│ - pathWaypoints: List<Transform>                                   │
│ - waypointThreshold: float                                         │
│ - hoverAmplitude, hoverFrequency: float                            │
├────────────────────────────────────────────────────────────────────┤
│ + MoveTo(target)                                                   │
│ + FollowPath()                                                     │
│ + HandleObstacles()                                                │
│ + UpdateAnimation()                                                │
│ + PlayHoverIdle()                                                  │
└────────────────────────────────────────────────────────────────────┘


┌────────────────────────────────────────────────────────────────────┐
│                  PlayerInput                                        │
├────────────────────────────────────────────────────────────────────┤
│ - currentBlock: Block                                              │
├────────────────────────────────────────────────────────────────────┤
│ + DetectSwipe(): Vector2Int                                        │
│ + TrySelectBlock(pos): bool                                        │
│ + ValidateMoveDirection(block, dir): bool                          │
└────────────────────────────────────────────────────────────────────┘
							▲
							│
					  interacts with
							│
						   Block


┌────────────────────────────────────────────────────────────────────┐
│                   Data Structures                                   │
├────────────────────────────────────────────────────────────────────┤
│ GridCell: TileType + int blockColorID                              │
│ TileType: enum {Empty, Ground, Immovable, Static, Dynamic, ...}   │
│ GridIslandData: Serializable island config                         │
│ LevelData: Collection of GridIslandData                            │
└────────────────────────────────────────────────────────────────────┘


┌────────────────────────────────────────────────────────────────────┐
│                    Level System                                     │
├─────────────────────────────────┬──────────────────────────────────┤
│  Editor (LevelDataEditor)        │  Runtime (LevelLoader)           │
├─────────────────────────────────┼──────────────────────────────────┤
│ + AddIsland()                    │ + LoadLevel(levelData)           │
│ + DeleteIsland()                 │ + ParseTileTypes()               │
│ + PaintTile(x, y, type)          │ + InstantiateBlocks()            │
│ + ConfigureGridSize()            │ + LinkReferences()               │
│ + RealTimePreview()              │ + InitializeIslands()            │
│ + DrawGizmos()                   │                                  │
└─────────────────────────────────┴──────────────────────────────────┘


┌────────────────────────────────────────────────────────────────────┐
│                   Camera System                                     │
├──────────────────────────┬──────────────────────────────────────────┤
│ CameraController         │ CameraConfig                             │
├──────────────────────────┼──────────────────────────────────────────┤
│ + FollowTarget()         │ - followSpeed: float                    │
│ + SetBounds()            │ - zDistance: float                      │
│ + UpdatePosition()       │ - boundsMin, boundsMax: Vector2         │
│                          │                                          │
│ + ParallaxController     │                                         │
│   ├─ CreateLayers()      │                                         │
│   ├─ ScrollBackground()  │                                         │
│   └─ AdjustDepth()       │                                         │
└──────────────────────────┴──────────────────────────────────────────┘
```

---

## Game Flow

### Level Initialization

```
LevelLoader.LoadLevel(levelData)
	│
	├─ Create GridIsland GameObjects
	│
	├─ For each island in levelData:
	│   ├─ InitializeIsland(width, height, cells)
	│   ├─ Create block GameObjects
	│   └─ RegisterBlock() for each block
	│
	├─ Create MultiGridManager
	├─ RegisterIsland() for each island
	│
	├─ Create CameraController
	├─ Create PlayerController
	│
	└─ Scene ready for play
```

### Turn Sequence (Per Player Action)

```
PlayerInput.DetectSwipe()
	│
	├─ Determine direction (Up, Down, Left, Right)
	│
	├─ Get Block at swipe start position
	│
	├─ Validate: CanPlayerMoveDirectly()?
	│
	├─ Calculate target position
	│
	├─ Block.TryMove(direction)
	│   ├─ CanMoveTo(target)? (check obstacles)
	│   ├─ Island.ExpandGridIfNeeded(target)
	│   ├─ (Special: GroundDeployerBlock deploys ground)
	│   └─ Move block + animate
	│
	├─ GravityManager.ApplyGravityRoutine()
	│   └─ All blocks fall until stable
	│
	└─ Check win condition (PlayerGoal)
```

### Island Merge Sequence

```
GroundDeployerBlock deploys ground tile
	│
	├─ CheckAndBridgeAdjacentIslands()
	│   │
	│   └─ Physics2D.OverlapBoxAll() at deployed tile
	│       │
	│       ├─ Detect nearby blocks/islands
	│       │
	│       └─ For each detected island:
	│           │
	│           ├─ mainIsland.MergeOtherIsland(detected)
	│           │
	│           └─ Grid expanded to fit both
	│               ├─ Blocks transferred
	│               ├─ References updated
	│               └─ World positions adjusted
	│
	└─ Deployer moves to final merged position
```

---

## Data Structures

### GridCell
```csharp
public struct GridCell
{
	public TileType type;           // What occupies this cell
	public int blockColorID;        // Visual color index
}
```

### TileType Enum
```csharp
public enum TileType
{
	Empty,           // Void/empty space
	Ground,          // Walkable background
	Immovable,       // Fixed block
	MovableStatic,   // Static block
	Dynamic,         // Player-movable block
	Joint,           // Complex interaction block
	GroundDeployer   // Tile spawning block
}
```

### GridIslandData (Serializable)
```csharp
public class GridIslandData
{
	public int islandID;
	public Vector2Int originPosition;    // World position
	public int width, height;             // Grid dimensions
	public int scaleFactorX, scaleFactorY; // Block size scaling
	public List<GridCell> gridData;       // Flat grid data (y*width + x)
}
```

### LevelData
```csharp
public class LevelData : ScriptableObject
{
	public List<GridIslandData> islands;
	public GameObject emptyCellPrefab;
	public Dictionary<int, BlockConfig> blockConfigs;
}
```

---

## How to Use

### Creating a Level

1. **In Unity Editor:**
   - Create a new LevelData ScriptableObject: Right-click → Create → LevelData
   - Add islands using the Level Data Editor
   - Paint tiles using the grid UI

2. **Via Code:**
   ```csharp
   LevelData levelData = new LevelData();
   GridIslandData island = new GridIslandData
   {
	   islandID = 1,
	   width = 10,
	   height = 8,
	   originPosition = Vector2Int.zero
   };
   levelData.islands.Add(island);
   LevelLoader.LoadLevel(levelData);
   ```

### Adding Custom Block Types

1. Create new class inheriting from `Block`:
   ```csharp
   public class MyCustomBlock : Block
   {
	   public override bool CanPlayerMoveDirectly() => true;
	   public override bool IsAffectedByGravity() => true;

	   // Custom behavior
   }
   ```

2. Add tile type to `TileType` enum
3. Register in `LevelLoader.InstantiateBlock()`
4. Add to `LevelDataEditor.cs` tile palette

### Input Handling

```csharp
// PlayerInput handles swipe detection
// Subscribe to movement events:
PlayerInput.onBlockSelected += HandleBlockSelection;
PlayerInput.onSwipeDetected += HandleSwipe;
```

### Accessing Islands

```csharp
MultiGridManager manager = FindObjectOfType<MultiGridManager>();
List<GridIsland> islands = manager.GetActiveIslands();

// Find island at world position
GridIsland island = manager.GetIslandAtWorldPos(worldPos);
```

---

## Extensibility

### Adding Physics Features

**Extend GravityManager:**
```csharp
public void ApplyVelocity(Block block, Vector2 force)
{
	// Custom momentum/sliding
}
```

### Custom Win Conditions

**Extend PlayerGoal:**
```csharp
public bool CheckWinCondition()
{
	// Custom logic
	return true;
}
```

### New Block Interactions

**Create specialized Block subclass:**
```csharp
public class SpringBlock : Block
{
	public override bool CanPlayerMoveDirectly() => false;

	public void OnBlockLands()
	{
		// Launch nearby blocks
	}
}
```

### Camera Effects

**Extend CameraController:**
```csharp
public void ApplyScreenShake(float intensity, float duration)
{
	// Shake effect
}
```

---

## Performance Considerations

- **Grid Operations**: O(n) for typical island sizes (10×10 to 30×30)
- **Gravity Passes**: Multiple passes until stable (typically 3-5)
- **Physics Checks**: OverlapBoxAll limited to deployed tile vicinity
- **DOTween**: Efficient animation pooling via Sequence

---

## Dependencies

- **Unity 2020.3+**
- **DOTween** (Animation library)
- **TextMesh Pro** (UI text)

---

## File Structure

```
Assets/
├── Resources/
│   ├── Scripts/
│   │   ├── Global/
│   │   │   ├── Block.cs (abstract)
│   │   │   ├── GridIsland.cs
│   │   │   ├── MultiGridManager.cs
│   │   │   ├── GravityManager.cs
│   │   │   ├── LevelData.cs
│   │   │   ├── LevelLoader.cs
│   │   │   ├── LevelReset.cs
│   │   │   ├── PlayerGoal.cs
│   │   │   ├── PlayerInput.cs
│   │   │   ├── DynamicFpsManager.cs
│   │   │   ├── LevelDataGizmoDrawer.cs
│   │   │   ├── Blocks/
│   │   │   │   ├── DynamicBlock.cs
│   │   │   │   ├── MovableStaticBlock.cs
│   │   │   │   ├── ImmovableBlock.cs
│   │   │   │   ├── JointBlock.cs
│   │   │   │   └── GroundDeployerBlock.cs
│   │   │   ├── Camera/
│   │   │   │   ├── CameraController.cs
│   │   │   │   ├── CameraConfig.cs
│   │   │   │   └── ParallaxController.cs
│   │   │   └── Editor/
│   │   │       └── LevelDataEditor.cs (Editor-only)
│   │   └── Player/
│   │       └── PlayerController.cs
│   ├── Prefabs/
│   │   ├── Blocks/
│   │   └── UI/
│   └── Shaders/
└── Editor/
	└── (Editor tools)
```

---

## Contributing

When extending BlockShift:

1. **Follow naming conventions**: `ClassName.cs`, `methodName()`, `_privateField`
2. **Use serialized fields** for inspector tuning: `[SerializeField] private Type field;`
3. **Document public APIs** with XML comments
4. **Test with multiple island configurations**
5. **Ensure grid coordinate consistency**

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Block doesn't move | Check `CanPlayerMoveDirectly()` returns true |
| Blocks don't fall | Verify `IsAffectedByGravity()` returns true; check grid below |
| Island merge fails | Ensure overlap detection works; check collision layers |
| Grid misalignment | Verify `originPosition` and coordinate conversions |
| Performance lag | Reduce island size; optimize number of active blocks |

---

**Version**: 1.0  
**Last Updated**: 2024  
**Author**: BlockShift Development Team
