# BlockShift - Quick Reference & Developer Guide

## Quick Navigation

- **[Core Concepts](#core-concepts)** - Understand fundamental ideas
- **[API Quick Reference](#api-quick-reference)** - Common methods
- **[Code Examples](#code-examples)** - Copy-paste implementations
- **[Debugging Tips](#debugging-tips)** - Common issues and fixes
- **[Performance Tuning](#performance-tuning)** - Optimization guide

---

## Core Concepts

### 1. Grid Coordinate System

BlockShift uses a **local island grid** with (0,0) at bottom-left:

```
Y-axis (up)
^
|  (0,9)  (1,9)  (2,9)
|  (0,8)  (1,8)  (2,8)
|  (0,1)  (1,1)  (2,1)
|  (0,0)  (1,0)  (2,0) ----> X-axis (right)
+
```

**World Position** ↔ **Grid Position** conversion via `GridIsland.GridToLocalPosition()` and `WorldToGridPosition()`

### 2. Block Hierarchy

```
Block (abstract)
├── DynamicBlock (player-movable, gravity-affected)
├── MovableStaticBlock (player-movable, NO gravity)
├── ImmovableBlock (fixed in place)
├── JointBlock (multi-block interactions)
└── GroundDeployerBlock (spawns ground, bridges islands)
```

### 3. Island Independence

Each `GridIsland`:
- Has its own `cellGrid[width, height]` for tile data
- Has its own `localGrid[width, height]` for block references
- Can grow dynamically via `ExpandGridIfNeeded()`
- Can merge with other islands via `MergeOtherIsland()`

### 4. Gravity Rules

Gravity applies **per island** in order:
1. Scan blocks from bottom to top
2. Check cell directly below
3. If empty AND block is affected by gravity → fall 1 unit
4. Repeat until all blocks stable

### 5. Movement Pipeline

```
Player Swipe
	↓
Input.DetectSwipe()
	↓
Block.TryMove(direction)
	↓
GridIsland.ExpandGridIfNeeded()
	↓
Block animate movement
	↓
GravityManager.ApplyGravityRoutine()
	↓
Check win condition
```

---

## API Quick Reference

### Block Methods

| Method | Purpose | Returns |
|--------|---------|---------|
| `CanPlayerMoveDirectly()` | Is this block movable by player? | `bool` |
| `IsAffectedByGravity()` | Does block fall when unsupported? | `bool` |
| `CanMoveTo(targetPos)` | Is target cell available? | `bool` |
| `TryMove(direction)` | Attempt movement in direction | `bool` |
| `MoveToGridPosition(pos)` | Animate to new position | `void` |

### GridIsland Methods

| Method | Purpose | Returns |
|--------|---------|---------|
| `GridToLocalPosition(gridPos, z)` | Convert grid → 3D position | `Vector3` |
| `WorldToGridPosition(worldPos)` | Convert 3D → grid position | `Vector2Int` |
| `GetBlockAtLocalPos(pos)` | Find block at grid position | `Block` |
| `RegisterBlock(block, pos)` | Add block to grid | `void` |
| `RemoveBlock(pos)` | Remove block from grid | `bool` |
| `ExpandGridIfNeeded(pos)` | Grow grid to accommodate position | `void` |
| `DeployGroundTileAt(pos, prefab)` | Spawn ground tile | `void` |
| `MergeOtherIsland(other)` | Combine with another island | `void` |
| `IsValidLocalPos(pos)` | Is position within bounds? | `bool` |
| `ContainsWorldPos(worldPos)` | Does island contain world position? | `bool` |
| `CanRemoveBlock(pos)` | Is it safe to remove block? | `bool` |

### MultiGridManager Methods

| Method | Purpose | Returns |
|--------|---------|---------|
| `RegisterIsland(island)` | Add island to manager | `void` |
| `GetActiveIslands()` | Get all active islands | `List<GridIsland>` |
| `GetIslandAtWorldPos(pos)` | Find island at world position | `GridIsland` |
| `TryTransferBlockBetweenIslands(block, src, pos)` | Move block between islands | `bool` |
| `ClearIslands()` | Remove all islands | `void` |

### GravityManager Methods

| Method | Purpose | Returns |
|--------|---------|---------|
| `ApplyGravityRoutine(islands)` | Apply gravity to all blocks | `IEnumerator` |

### LevelLoader Methods

| Method | Purpose | Returns |
|--------|---------|---------|
| `LoadLevel(levelData)` | Instantiate complete level | `void` |

---

## Code Examples

### Example 1: Create a Level Programmatically

```csharp
void CreateLevelProgrammatically()
{
	// Create level data
	LevelData levelData = ScriptableObject.CreateInstance<LevelData>();
	levelData.islands = new List<GridIslandData>();

	// Island 1: 5x5 grid
	GridIslandData island1 = new GridIslandData
	{
		islandID = 1,
		originPosition = Vector2Int.zero,
		width = 5,
		height = 5,
		gridData = new List<GridCell>()
	};

	// Fill with Ground tiles
	for (int y = 0; y < 5; y++)
	{
		for (int x = 0; x < 5; x++)
		{
			island1.gridData.Add(new GridCell(TileType.Ground));
		}
	}

	// Add dynamic block at (2, 2)
	island1.gridData[2 * 5 + 2] = new GridCell(TileType.Dynamic);

	levelData.islands.Add(island1);

	// Load level
	LevelLoader loader = FindObjectOfType<LevelLoader>();
	loader.LoadLevel(levelData);
}
```

### Example 2: Manually Move a Block

```csharp
void MoveBlockManually(Block block, Vector2Int direction)
{
	if (block == null || block.currentIsland == null)
		return;

	Vector2Int targetPos = block.gridPosition + direction;

	// Check if move is valid
	if (!block.CanMoveTo(targetPos))
	{
		Debug.Log("Cannot move to target position!");
		return;
	}

	// Expand island if needed
	block.currentIsland.ExpandGridIfNeeded(targetPos);

	// Remove from old position
	block.currentIsland.RemoveBlock(block.gridPosition);

	// Move block
	block.gridPosition = targetPos;
	block.currentIsland.RegisterBlock(block, targetPos);

	// Animate movement
	block.MoveToGridPosition(targetPos);

	// Apply gravity
	GravityManager gravity = FindObjectOfType<GravityManager>();
	StartCoroutine(gravity.ApplyGravityRoutine(
		FindObjectOfType<MultiGridManager>().GetActiveIslands()
	));
}
```

### Example 3: Create Custom Block Type

```csharp
public class BounceBlock : Block
{
	[SerializeField] private float bounceForce = 3f;
	[SerializeField] private int bounceDistance = 2;

	public override bool CanPlayerMoveDirectly() => true;
	public override bool IsAffectedByGravity() => false;

	public void OnBlockLandsOnMe(Block incomingBlock)
	{
		// Find direction of incoming block
		Vector2Int direction = incomingBlock.gridPosition - gridPosition;
		direction = direction.normalized; // Get unit direction

		// Calculate bounce target
		Vector2Int bounceTarget = gridPosition + (direction * bounceDistance);

		// Move ourselves in opposite direction
		if (currentIsland.IsValidLocalPos(bounceTarget))
		{
			TryMoveToward(direction * bounceDistance);
		}
	}

	private void TryMoveToward(Vector2Int targetOffset)
	{
		Vector2Int targetPos = gridPosition + targetOffset;
		if (CanMoveTo(targetPos))
		{
			currentIsland.RemoveBlock(gridPosition);
			gridPosition = targetPos;
			currentIsland.RegisterBlock(this, targetPos);
			MoveToGridPosition(targetPos);
		}
	}
}
```

### Example 4: Access Blocks on Island

```csharp
void PrintAllBlocksOnIsland(GridIsland island)
{
	Debug.Log($"Island {island.islandID} contains:");

	for (int y = 0; y < island.height; y++)
	{
		for (int x = 0; x < island.width; x++)
		{
			Block block = island.GetBlockAtLocalPos(new Vector2Int(x, y));
			if (block != null)
			{
				Debug.Log($"  [{x}, {y}]: {block.GetType().Name}");
			}
		}
	}
}
```

### Example 5: Check Win Condition

```csharp
bool CheckIfPlayerReachedGoal(Vector2Int goalPosition, GridIsland goalIsland)
{
	// Find player block (assuming it's a DynamicBlock)
	Block player = null;
	List<GridIsland> islands = 
		FindObjectOfType<MultiGridManager>().GetActiveIslands();

	foreach (var island in islands)
	{
		// Find first DynamicBlock (player)
		for (int y = 0; y < island.height; y++)
		{
			for (int x = 0; x < island.width; x++)
			{
				Block block = island.GetBlockAtLocalPos(new Vector2Int(x, y));
				if (block is DynamicBlock)
				{
					player = block;
					break;
				}
			}
			if (player != null) break;
		}
		if (player != null) break;
	}

	if (player == null)
		return false;

	// Check if player is on goal island at goal position
	return player.currentIsland == goalIsland && 
		   player.gridPosition == goalPosition;
}
```

---

## Debugging Tips

### Common Issues & Solutions

#### Issue: Block doesn't move when player swipes

**Checklist:**
```csharp
// 1. Is block selectable?
bool canSelect = block.CanPlayerMoveDirectly();  // Should be true

// 2. Is target position valid?
bool canMoveTo = block.CanMoveTo(targetPos);    // Should be true

// 3. Is block on an island?
bool hasIsland = block.currentIsland != null;   // Should be true

// 4. Is target position within bounds (or expandable)?
bool validPos = block.currentIsland.IsValidLocalPos(targetPos);
```

#### Issue: Blocks don't fall with gravity

**Checklist:**
```csharp
// 1. Is block affected by gravity?
bool affectedByGravity = block.IsAffectedByGravity();  // Should be true

// 2. Is there support below?
Block supportBelow = block.currentIsland.GetBlockAtLocalPos(
	block.gridPosition + Vector2Int.down
);
bool hasSupport = supportBelow != null;  // If null, should fall

// 3. Is GravityManager running?
GravityManager gravity = FindObjectOfType<GravityManager>();
bool gravityExists = gravity != null;  // Should exist
```

#### Issue: Islands don't merge

**Checklist:**
```csharp
// 1. Is there a GroundDeployer block?
// 2. Did it deploy a ground tile?
// 3. Are islands close enough to overlap?

// Manually test overlap:
Physics2D.OverlapBoxAll(
	checkPoint,
	new Vector2(1.2f, 1.2f),
	0
);
```

### Debug Visualization

Add this to visualize grid:

```csharp
void OnDrawGizmos()
{
	GridIsland island = GetComponent<GridIsland>();
	if (island == null) return;

	for (int x = 0; x < island.width; x++)
	{
		for (int y = 0; y < island.height; y++)
		{
			Vector3 localPos = island.GridToLocalPosition(new Vector2Int(x, y));
			Vector3 worldPos = transform.TransformPoint(localPos);

			Gizmos.color = Color.white;
			Gizmos.DrawWireCube(worldPos, Vector3.one * 0.8f);
		}
	}

	// Draw origin
	Gizmos.color = Color.red;
	Gizmos.DrawSphere(transform.position, 0.2f);
}
```

### Console Logging

```csharp
// Log block position and island
Debug.Log($"Block {block.name} at grid ({block.gridPosition.x}, {block.gridPosition.y})");
Debug.Log($"  Island: {block.currentIsland.islandID}");
Debug.Log($"  World: {block.transform.position}");

// Log island state
Debug.Log($"Island {island.islandID}: {island.width}×{island.height}, " +
		  $"blocks: {island.GetBlockCount()}");

// Log grid cell
GridCell cell = island.cellGrid[x, y];
Debug.Log($"Cell[{x},{y}]: {cell.type}");
```

---

## Performance Tuning

### Grid Size Impact

| Grid Size | Typical Cost | Notes |
|-----------|-------------|-------|
| 5×5 | Negligible | Good for simple puzzles |
| 10×10 | <1ms | Standard level size |
| 20×20 | ~2-3ms | Performance acceptable |
| 50×50 | ~10ms | Notable impact on gravity |
| 100×100+ | >20ms | Consider optimization |

**Optimization:** Limit grid scans to active regions

### Gravity Performance

```csharp
// Default: Multiple passes until stable
// Cost: O(n * m * p) where p = passes

// Optimize: Limit max passes
public void OptimizedGravityRoutine()
{
	const int MAX_PASSES = 5;  // Cap passes

	for (int pass = 0; pass < MAX_PASSES; pass++)
	{
		if (!ApplySingleGravityPass())
			break;  // No blocks fell, done
	}
}
```

### Memory Optimization

```csharp
// Reuse grids instead of recreating
private GridCell[] cellGridFlat;  // Use flat array
private bool[] cellOccupied;      // Quick occupancy check

// Access: cellOccupied[y * width + x]
```

### Physics Checks

```csharp
// Optimize overlap detection
// Only check near deployed tile, not entire island

Collider2D[] nearby = Physics2D.OverlapBoxAll(
	deployedPos,
	new Vector2(2f, 2f),  // Small box, not huge
	0
);
```

### Block Count Limits

```csharp
// Monitor active blocks
int totalBlocks = 0;
foreach (var island in manager.GetActiveIslands())
{
	totalBlocks += island.GetBlockCount();
}

// Keep under reasonable limit (typically < 50 blocks per level)
if (totalBlocks > 100)
	Debug.LogWarning("Too many blocks, performance may suffer");
```

---

## Project Structure Best Practices

### Naming Conventions

```csharp
// Classes: PascalCase
public class PlayerController { }

// Fields: camelCase (private with underscore)
private int _blockCount;
public Vector2Int gridPosition;  // Public OK

// Methods: PascalCase
public void MoveBlock() { }
private void UpdatePosition() { }

// Constants: UPPER_SNAKE_CASE
private const int MAX_BLOCKS = 50;
private const float FALL_SPEED = 5f;
```

### Folder Organization

```
Assets/
├── Resources/
│   ├── Scripts/
│   │   ├── Global/          (Level/grid systems)
│   │   ├── Global/Blocks/   (Block types)
│   │   ├── Camera/          (Camera systems)
│   │   ├── Player/          (Player character)
│   │   └── Editor/          (Editor-only tools)
│   ├── Prefabs/
│   │   ├── Blocks/          (Block GameObjects)
│   │   ├── Tiles/           (Ground tiles)
│   │   └── UI/              (UI elements)
│   ├── Data/                (ScriptableObjects)
│   │   └── Levels/
│   └── Shaders/
└── Scenes/
	├── MainMenu.unity
	└── GameLevel.unity
```

### Documentation Standards

```csharp
/// <summary>
/// Moves block to target position with animation.
/// </summary>
/// <param name="targetPos">Local grid position on current island</param>
/// <returns>True if movement succeeded, false if blocked</returns>
/// <remarks>
/// This method handles coordinate validation, animation,
/// and updates island's localGrid. Does NOT apply gravity.
/// Caller must invoke GravityManager after movement.
/// </remarks>
public bool TryMove(Vector2Int targetPos)
{
	// Implementation
}
```

---

## Testing Checklist

Before shipping a level:

- [ ] Player can move all movable blocks
- [ ] Gravity applies correctly (blocks fall, don't get stuck)
- [ ] Islands merge when deployer touches them
- [ ] Can reach goal position without softlock
- [ ] No blocks spawn outside island bounds
- [ ] Camera follows player correctly
- [ ] No performance issues (<60fps)
- [ ] Win condition triggers properly
- [ ] Level reset works

---

## Useful Editor Shortcuts

```csharp
// Quick scene testing
[MenuItem("Debug/Spawn Test Block")]
public static void SpawnTestBlock()
{
	GameObject go = new GameObject("TestBlock");
	go.AddComponent<DynamicBlock>();
}

// Reset level
[MenuItem("Debug/Reset Level")]
public static void ResetLevel()
{
	FindObjectOfType<LevelReset>().ResetLevel();
}

// Print all islands
[MenuItem("Debug/Print Islands")]
public static void PrintIslands()
{
	var mgr = FindObjectOfType<MultiGridManager>();
	foreach (var island in mgr.GetActiveIslands())
	{
		Debug.Log($"Island {island.islandID}: {island.width}×{island.height}");
	}
}
```

---

## External Resources

- **DOTween Documentation**: http://dotween.demigiant.com/
- **Unity Physics2D**: https://docs.unity3d.com/Manual/Physics2DReference.html
- **Grid-Based Game Design**: Search "tile-based puzzle game architecture"

---

**Last Updated**: 2024  
**For Questions**: Refer to README.md for complete architecture overview
