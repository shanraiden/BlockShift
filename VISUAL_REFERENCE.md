# BlockShift - Visual Architecture Reference

## System Overview Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         BLOCKSHIFT GAME ENGINE                              │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌────────────────────────────┐          ┌──────────────────────────────┐  │
│  │    INPUT SYSTEM            │          │    RENDERING SYSTEM          │  │
│  ├────────────────────────────┤          ├──────────────────────────────┤  │
│  │ • PlayerInput              │          │ • SpriteRenderer             │  │
│  │ • Swipe Detection          │          │ • Animator (player/blocks)   │  │
│  │ • Block Selection          │          │ • CameraController          │  │
│  │ • Input Validation         │          │ • ParallaxController        │  │
│  └──────────────┬─────────────┘          └──────────────┬───────────────┘  │
│                 │                                       │                   │
│                 └─────────────────────┬─────────────────┘                   │
│                                       │                                     │
│  ┌────────────────────────────────────▼─────────────────────────────────┐  │
│  │                    BLOCK SYSTEM (Core)                               │  │
│  ├──────────────────────────────────────────────────────────────────────┤  │
│  │                                                                       │  │
│  │  Block (abstract)                                                    │  │
│  │  ├── DynamicBlock (gravity + player-movable)                         │  │
│  │  ├── MovableStaticBlock (no gravity, player-movable)                 │  │
│  │  ├── ImmovableBlock (fixed)                                          │  │
│  │  ├── JointBlock (multi-block)                                        │  │
│  │  └── GroundDeployerBlock (spawns ground + merges islands)            │  │
│  │                                                                       │  │
│  │  Properties:                                                         │  │
│  │  • gridPosition (local to island)                                    │  │
│  │  • currentIsland (reference)                                         │  │
│  │  • spriteRenderer (visual)                                           │  │
│  └──────────────────┬──────────────────────────────────────────────────┘  │
│                     │                                                      │
│  ┌──────────────────▼──────────────────┐      ┌─────────────────────────┐  │
│  │    GRID ISLAND SYSTEM               │      │  MULTI-GRID MANAGER     │  │
│  ├─────────────────────────────────────┤      ├─────────────────────────┤  │
│  │ GridIsland (per floating island)    │      │ Manages all active      │  │
│  │                                     │      │ islands and block       │  │
│  │ Data:                               │      │ transfers between them  │  │
│  │ • islandID, width, height           │      │                         │  │
│  │ • originPosition (world coords)     │      │ Methods:                │  │
│  │ • cellGrid[w][h] (TileType)         │      │ • RegisterIsland()      │  │
│  │ • localGrid[w][h] (Block refs)      │      │ • GetIslandAtWorldPos() │  │
│  │ • islandBlocks (list)               │      │ • TransferBlock()       │  │
│  │                                     │      └──────────────┬──────────┘  │
│  │ Operations:                         │                     │             │
│  │ • GridToLocalPosition()             │                     │             │
│  │ • WorldToGridPosition()             │                     │             │
│  │ • RegisterBlock/RemoveBlock()       │                     │             │
│  │ • ExpandGridIfNeeded()              │                     │             │
│  │ • DeployGroundTileAt()              │                     │             │
│  │ • MergeOtherIsland()                │      ◄──────────────┘             │
│  └─────────────────────────────────────┘                                  │
│                 ▲                                                          │
│                 │                                                          │
│  ┌──────────────┴────────────────────┐                                    │
│  │   PHYSICS SYSTEM                  │                                    │
│  ├───────────────────────────────────┤                                    │
│  │ GravityManager                    │                                    │
│  │ • ApplyGravityRoutine()           │                                    │
│  │ • Drops blocks until stable       │                                    │
│  │ • Smooth animations via DOTween   │                                    │
│  │                                   │                                    │
│  │ Rules:                            │                                    │
│  │ 1. Scan island grid bottom→top    │                                    │
│  │ 2. Check cell below each block    │                                    │
│  │ 3. If affected by gravity & empty │                                    │
│  │    → Fall 1 unit                  │                                    │
│  │ 4. Repeat until stable            │                                    │
│  └───────────────────────────────────┘                                    │
│                                                                            │
│  ┌────────────────────────────────────────────────────────────────────┐  │
│  │              LEVEL MANAGEMENT SYSTEM                               │  │
│  ├────────────────────────────────────────────────────────────────────┤  │
│  │                                                                    │  │
│  │  Runtime Path:               Editor Path:                         │  │
│  │  ┌────────────────┐          ┌──────────────────┐                 │  │
│  │  │ LevelData      │          │ LevelDataEditor  │                 │  │
│  │  │ (asset)        │◄────────►│ (visual editor)  │                 │  │
│  │  └────────┬───────┘          └──────────────────┘                 │  │
│  │           │                                                        │  │
│  │           ▼                                                        │  │
│  │  ┌────────────────┐                                               │  │
│  │  │ LevelLoader    │                                               │  │
│  │  │ • Parses data  │                                               │  │
│  │  │ • Creates objs │                                               │  │
│  │  │ • Links refs   │                                               │  │
│  │  └────────┬───────┘                                               │  │
│  │           │                                                        │  │
│  │           ▼                                                        │  │
│  │  Runtime objects ready!                                           │  │
│  │                                                                    │  │
│  │  LevelReset: Can restore to initial state                         │  │
│  │  PlayerGoal: Defines win conditions                               │  │
│  └────────────────────────────────────────────────────────────────────┘  │
│                                                                            │
└────────────────────────────────────────────────────────────────────────────┘
```

## Coordinate System Visualization

### Island Coordinate Space

```
One Island with width=4, height=3:

World Space:                   Local Grid Space:

Island Position (2, 5)         (0,2) ─── (1,2) ─── (2,2) ─── (3,2)
in world                         │         │         │         │
								 │         │         │         │
							   (0,1) ─── (1,1) ─── (2,1) ─── (3,1)
								 │         │         │         │
								 │         │         │         │
							   (0,0) ─── (1,0) ─── (2,0) ─── (3,0)

Grid cell = 1 unit square

Conversions:
  GridToLocalPosition(2, 1) = (2.0, 1.0, z)
  WorldToGridPosition(world) = Grid coordinates
```

### Multi-Island System

```
World View: Two separate islands that can merge

┌─────────────────┐      ┌──────────────────┐
│  Island 1       │      │  Island 2        │
│  Origin: (0,0)  │      │  Origin: (6, 0)  │
│  Size: 5×3      │      │  Size: 4×3       │
│                 │      │                  │
│  [D][D][G][ ][ ]│      │  [D][ ][ ][ ]    │
│  [D][D][G][ ][ ]│      │  [G][G][G][ ]    │
│  [G][G][G][G][G]│      │  [G][G][G][G]    │
└─────────────────┘      └──────────────────┘

Legend:
  [D] = DynamicBlock
  [G] = Ground/ImmovableBlock
  [ ] = Empty

When Islands Merge (via GroundDeployer):
  Detected at boundary
  → MergeOtherIsland()
  → Grid expanded to fit both
  → All blocks transferred
  → Single unified island

┌──────────────────────────────┐
│  Island 1+2 (Merged)         │
│  Origin: (0, 0)              │
│  Size: 9×3                   │
│                              │
│  [D][D][G][ ][ ][D][ ][ ][ ]│
│  [D][D][G][ ][ ][G][G][G][ ]│
│  [G][G][G][G][G][G][G][G][G]│
└──────────────────────────────┘
```

## Block Type Decision Flowchart

```
Player Swipes Block
		│
		▼
Is block CanPlayerMoveDirectly()?
		│
   ┌────┴────┐
   │         │
  YES       NO
   │         │
   ▼         ▼
Allow      Play Error
Move       (block locked)
   │
   ▼
Try Move
   │
   ├─── Check CanMoveTo()
   │    ├─ Obstacle block? → FAIL
   │    └─ Path clear → OK
   │
   ├─── ExpandGridIfNeeded()
   │    └─ Grow island if moving to edge
   │
   ├─── Special Action?
   │    ├─ GroundDeployer? → Deploy tile & merge islands
   │    └─ Other → Normal move
   │
   ├─── Execute Movement
   │    ├─ RemoveBlock(old position)
   │    ├─ RegisterBlock(new position)
   │    └─ Animate movement (DOTween)
   │
   └─── Apply Gravity
		└─ Blocks fall until stable
```

## Gravity Algorithm Visualization

```
Before Gravity:          After Gravity (1 pass):      After Gravity (stable):

[I][D][ ]               [I][ ][ ]                    [I][ ][ ]
[D][ ][D]               [D][D][ ]                    [D][ ][ ]
[G][G][G]               [G][G][D]                    [I][D][D]
													 [G][G][G]

Step-by-step (first pass):

Scan y=1 (bottom row):
  [y=1, x=0]: [D] → Check y=0 below → [G] support → STAY
  [y=1, x=1]: [D] → Check y=0 below → [G] support → STAY

Scan y=2 (middle row):
  [y=2, x=0]: [I] → Check y=1 below → [D] support → STAY
  [y=2, x=1]: [ ] → Empty → Skip
  [y=2, x=2]: [D] → Check y=1 below → [ ] empty → FALL
					 Move to (2, 1)
					 Set blockFellThisStep = true

Scan y=3 (top row):
  [y=3, x=0]: [I] → Check y=2 below → [I] support → STAY
  [y=3, x=1]: [D] → Check y=2 below → [ ] empty → FALL
					 Move to (1, 2)
					 Set blockFellThisStep = true

Second pass needed? YES (blocks fell)

Continue until stable (no blocks fall in pass)
```

## Movement Pipeline Sequence

```
Player Input
	│
	▼
┌─────────────────────────────────────────────────────┐
│ 1. SWIPE DETECTION                                  │
│    PlayerInput.DetectSwipe() → Vector2Int direction │
└─────────────────┬───────────────────────────────────┘
				  │
				  ▼
┌─────────────────────────────────────────────────────┐
│ 2. BLOCK SELECTION                                  │
│    Get Block at swipe start position                │
│    Validate: CanPlayerMoveDirectly()?               │
└─────────────────┬───────────────────────────────────┘
				  │
				  ▼
┌─────────────────────────────────────────────────────┐
│ 3. CALCULATE TARGET                                 │
│    targetPos = gridPosition + direction             │
└─────────────────┬───────────────────────────────────┘
				  │
				  ▼
┌─────────────────────────────────────────────────────┐
│ 4. VALIDATE MOVE                                    │
│    CanMoveTo(targetPos)?                            │
│    └─ Check for obstacle blocks                    │
└─────────────────┬───────────────────────────────────┘
				  │
				  ▼
┌─────────────────────────────────────────────────────┐
│ 5. EXPAND GRID (if needed)                         │
│    ExpandGridIfNeeded(targetPos)                   │
│    └─ Grow if moving beyond current bounds        │
└─────────────────┬───────────────────────────────────┘
				  │
				  ▼
┌─────────────────────────────────────────────────────┐
│ 6. SPECIAL ACTIONS                                  │
│    ├─ GroundDeployer? Deploy tile                  │
│    ├─ Bridge adjacent islands?                     │
│    └─ Merge if overlapping                         │
└─────────────────┬───────────────────────────────────┘
				  │
				  ▼
┌─────────────────────────────────────────────────────┐
│ 7. EXECUTE MOVEMENT                                │
│    ├─ RemoveBlock(gridPosition)                    │
│    ├─ Update gridPosition = targetPos              │
│    ├─ RegisterBlock(new position)                  │
│    └─ MoveToGridPosition() → Animate               │
└─────────────────┬───────────────────────────────────┘
				  │
				  ▼
┌─────────────────────────────────────────────────────┐
│ 8. APPLY GRAVITY                                   │
│    GravityManager.ApplyGravityRoutine()             │
│    └─ Blocks fall until all stable                │
└─────────────────┬───────────────────────────────────┘
				  │
				  ▼
┌─────────────────────────────────────────────────────┐
│ 9. CHECK WIN CONDITION                             │
│    PlayerGoal.CheckWinCondition()?                 │
│    ├─ YES → Level Complete!                        │
│    └─ NO → Ready for next move                     │
└─────────────────────────────────────────────────────┘
```

## Data Flow Diagram

```
┌──────────────────┐
│  LevelData.asset │ (ScriptableObject)
│                  │
│ ├─ List<Island>  │
│ │  ├─ ID         │
│ │  ├─ Size       │
│ │  └─ GridData[] │
│ └─ Prefabs       │
└────────┬─────────┘
		 │ Load
		 ▼
┌──────────────────────────────────┐
│  LevelLoader.LoadLevel()         │
│                                  │
│  For each island in LevelData:   │
│  ├─ Instantiate GridIsland GO    │
│  ├─ InitializeIsland()           │
│  │  ├─ Copy gridData → cellGrid  │
│  │  └─ Create localGrid[][]      │
│  │                               │
│  └─ For each block:              │
│     ├─ Instantiate Block GO      │
│     ├─ Set sprite/color          │
│     └─ RegisterBlock() in island │
└────────┬─────────────────────────┘
		 │ Creates
		 ▼
┌──────────────────────────────────┐
│  Runtime Game Objects            │
│                                  │
│  ├─ GridIsland Components        │
│  │  ├─ cellGrid[w][h] (data)     │
│  │  ├─ localGrid[w][h] (refs)    │
│  │  └─ Transform (position)      │
│  │                               │
│  ├─ Block Components             │
│  │  ├─ gridPosition (location)   │
│  │  ├─ currentIsland (ref)       │
│  │  └─ Transform (3D position)   │
│  │                               │
│  └─ Manager Components           │
│     ├─ MultiGridManager          │
│     └─ GravityManager            │
└────────┬─────────────────────────┘
		 │ Used by
		 ▼
┌──────────────────────────────────┐
│  Game Systems                    │
│                                  │
│  ├─ PlayerInput                  │
│  │  └─ Sends commands to blocks  │
│  │                               │
│  ├─ Block Movement               │
│  │  └─ Uses island methods       │
│  │                               │
│  ├─ Gravity Application          │
│  │  └─ Reads island grids        │
│  │                               │
│  └─ Camera/Rendering             │
│     └─ Follows player, renders   │
└──────────────────────────────────┘
```

## Class Dependency Graph

```
					┌─────────────────┐
					│  MonoBehaviour  │
					└────────┬────────┘
							 │ inherits
				   ┌─────────┴─────────┐
				   │                   │
			  ┌────▼─────┐        ┌────▼────────┐
			  │  Block   │        │ GridIsland  │
			  └────┬─────┘        └────┬────────┘
				   │                   │
		┌──────────┼──────────┐        │
		│          │          │        │
	┌───▼──┐  ┌───▼──┐  ┌───▼──┐    │
	│Dyn.  │  │Move  │  │Immov │    │
	│Block │  │Static│  │Block │    │
	└──────┘  └──────┘  └──────┘    │
		│          │          │      │
		└──────────┼──────────┘      │
				   │                 │
			  ┌────▼─────────┐       │
			  │ JointBlock   │       │
			  └──────────────┘       │
				   │                 │
			  ┌────▼──────────────┐  │
			  │GroundDeployer    │  │
			  └───────────────────┘  │
									 │
			  All blocks use  ──────┘

			  GridIsland methods:
			  ├─ RegisterBlock()
			  ├─ RemoveBlock()
			  ├─ GetBlockAtLocalPos()
			  ├─ GridToLocalPosition()
			  └─ WorldToGridPosition()


MultiGridManager ──── manages ──── GridIsland[]
	   │
	   └─ coordinates
		  BlockTransfers

GravityManager ──── affects ──── Block[] (on each island)
	   │
	   └─ reads from
		  GridIsland.localGrid
```

## File Organization

```
Assets/
│
├─ Resources/
│  │
│  ├─ Scripts/
│  │  │
│  │  ├─ Global/
│  │  │  ├─ Block.cs ..................... Abstract base
│  │  │  ├─ GridIsland.cs ............... Single island logic
│  │  │  ├─ MultiGridManager.cs ......... Multi-island coordination
│  │  │  ├─ GravityManager.cs ........... Physics simulation
│  │  │  ├─ LevelData.cs ............... Data structures
│  │  │  ├─ LevelLoader.cs ............. Level instantiation
│  │  │  ├─ LevelReset.cs .............. Checkpoint system
│  │  │  ├─ PlayerInput.cs ............. Input handling
│  │  │  ├─ PlayerGoal.cs .............. Win conditions
│  │  │  ├─ DynamicFpsManager.cs ....... Performance
│  │  │  └─ LevelDataGizmoDrawer.cs ... Debug visualization
│  │  │
│  │  ├─ Global/Blocks/
│  │  │  ├─ DynamicBlock.cs ............ Player-movable + gravity
│  │  │  ├─ MovableStaticBlock.cs ..... Player-movable, no gravity
│  │  │  ├─ ImmovableBlock.cs ......... Fixed in place
│  │  │  ├─ JointBlock.cs ............. Multi-block logic
│  │  │  └─ GroundDeployerBlock.cs ... Ground spawner + merger
│  │  │
│  │  ├─ Camera/
│  │  │  ├─ CameraController.cs ....... Camera positioning
│  │  │  ├─ CameraConfig.cs ........... Camera settings
│  │  │  └─ ParallaxController.cs .... Background effects
│  │  │
│  │  ├─ Player/
│  │  │  └─ PlayerController.cs ....... NPC player character
│  │  │
│  │  └─ Editor/
│  │     └─ LevelDataEditor.cs ........ Visual level editor
│  │
│  ├─ Prefabs/
│  │  ├─ Blocks/
│  │  │  ├─ DynamicBlockPrefab.prefab
│  │  │  ├─ ImmovableBlockPrefab.prefab
│  │  │  └─ ...
│  │  │
│  │  ├─ Tiles/
│  │  │  └─ GroundTilePrefab.prefab
│  │  │
│  │  └─ UI/
│  │     └─ ... UI elements
│  │
│  └─ Data/
│     └─ Levels/
│        ├─ Level1.asset (LevelData)
│        ├─ Level2.asset (LevelData)
│        └─ ...
│
└─ Scenes/
   ├─ MainMenu.unity
   ├─ GameLevel.unity
   └─ ...
```

## Performance Profile

```
Typical Frame (60 FPS = 16.67ms budget):

Frame Timeline:
├─ Input Detection: 0.1ms
│  └─ Swipe parsing, block selection
│
├─ Movement Update: 0.5ms
│  └─ Block position updates, animations
│
├─ Grid Operations: 1-3ms (depends on size)
│  ├─ Coordinate conversions
│  └─ Island expansion (if triggered)
│
├─ Physics/Gravity: 2-5ms (varies)
│  └─ Gravity passes, fall animations
│
├─ Rendering: 8-12ms
│  ├─ Sprite rendering
│  ├─ Camera positioning
│  └─ Parallax effects
│
└─ Total: ~12-22ms (well under budget)

Bottleneck Locations:
 ⚠ Large grids (>50×50) in gravity passes
 ⚠ Many simultaneous DOTween animations
 ⚠ Physics2D.OverlapBoxAll on large areas
 ⚠ Frequent island merges
```

---

**Visual Reference Version**: 1.0  
**For Detailed Explanations**: See README.md  
**For Sequences**: See GAME_FLOW_SEQUENCES.puml  
**For Quick API Reference**: See QUICK_REFERENCE.md
