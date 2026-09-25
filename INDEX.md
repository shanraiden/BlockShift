# BlockShift Documentation Index

Welcome to the BlockShift project documentation! This guide will help you navigate all available resources.

## 📚 Documentation Files

### **1. README.md** - Main Documentation
**Start here!** Comprehensive overview of the entire project.

**Contains:**
- Project overview and vision
- Core architecture explanation
- Complete system components guide
- Class diagram (UML)
- Game flow explanation
- Data structures
- How to use guide
- Extensibility patterns
- Performance considerations
- File structure
- Contributing guidelines

**Best For:** Understanding the big picture, learning how systems interact, finding method documentation

**Read Time:** 20-30 minutes

---

### **2. ARCHITECTURE.puml** - UML Architecture Diagram (PlantUML)
Visual representation of all classes and their relationships.

**Contains:**
- Block class hierarchy
- GridIsland and MultiGridManager relationships
- Physics system
- Input/Control system
- Level system
- Camera system
- Utility components

**Best For:** Visual learners, understanding class inheritance, seeing dependencies at a glance

**How to View:**
- Use PlantUML online renderer: http://www.plantuml.com/plantuml/uml/
- Copy contents into the renderer
- Or use local PlantUML tools (VSCode extensions available)

---

### **3. GAME_FLOW_SEQUENCES.puml** - Sequence Diagrams (PlantUML)
Step-by-step flows of how systems interact.

**Contains:**
- Level Initialization sequence
- Player Move Action sequence
- Block Movement Between Islands
- Gravity Application detailed steps
- Island Merge Process
- Coordinate System Transformation
- Block Type Decision Tree

**Best For:** Understanding how features work, tracing execution flow, debugging issues

**Usage:**
- View in PlantUML renderer
- Follow each numbered step
- Trace data flow through systems

---

### **4. QUICK_REFERENCE.md** - Developer Cheat Sheet
Concise reference for common tasks and patterns.

**Contains:**
- Core concepts summary
- API Quick Reference table
- 5 Ready-to-Use Code Examples
- Debugging Tips & Common Issues
- Console Logging patterns
- Performance Tuning guide
- Best practices
- Useful Editor Shortcuts

**Best For:** Quick lookups, copy-paste code snippets, finding method names, solving common problems

**Read Time:** 5-10 minutes (or reference as needed)

---

### **5. VISUAL_REFERENCE.md** - Diagrams & Visual Guides
ASCII diagrams and visual explanations.

**Contains:**
- System Overview diagram
- Coordinate System visualization
- Multi-Island System diagram
- Block Type Decision Flowchart
- Gravity Algorithm step-by-step
- Movement Pipeline sequence
- Data Flow diagram
- Class Dependency Graph
- File Organization tree
- Performance Profile timeline

**Best For:** Visual understanding, seeing relationships, understanding data flow, performance analysis

**Read Time:** 10-15 minutes

---

## 🎯 Quick Navigation by Task

### "I want to understand the project"
1. Read **README.md** (sections: Overview, Core Architecture, System Components)
2. Look at **VISUAL_REFERENCE.md** (System Overview Diagram)
3. View **ARCHITECTURE.puml** in PlantUML

### "I need to find a method"
1. Check **QUICK_REFERENCE.md** (API Quick Reference table)
2. Search **README.md** for class/method name
3. View **ARCHITECTURE.puml** for class structure

### "I want to add a new block type"
1. Read **README.md** (section: Extensibility → Adding New Block Types)
2. Copy template from **QUICK_REFERENCE.md** (Example 3)
3. Reference **ARCHITECTURE.puml** to see Block hierarchy

### "I need to debug something"
1. Check **QUICK_REFERENCE.md** (section: Debugging Tips)
2. Review **GAME_FLOW_SEQUENCES.puml** for relevant flow
3. Use console logging patterns from **QUICK_REFERENCE.md**

### "I want to understand gravity"
1. Read **README.md** (section: Gravity System)
2. View **VISUAL_REFERENCE.md** (Gravity Algorithm Visualization)
3. See sequence in **GAME_FLOW_SEQUENCES.puml** (Gravity Application)

### "I need to optimize performance"
1. Read **QUICK_REFERENCE.md** (Performance Tuning section)
2. Check **README.md** (Performance Considerations)
3. View **VISUAL_REFERENCE.md** (Performance Profile)

### "I want to understand movement"
1. View **GAME_FLOW_SEQUENCES.puml** (Player Move Action)
2. Read **README.md** (Key Systems → Movement System)
3. Check **VISUAL_REFERENCE.md** (Movement Pipeline)

---

## 📖 Learning Path (Recommended)

### For New Developers:
1. **Day 1:**
   - Read README.md (Overview → System Components)
   - View VISUAL_REFERENCE.md (System Overview + Coordinate System)
   - ~1 hour

2. **Day 2:**
   - Study GAME_FLOW_SEQUENCES.puml (Level Init → Player Move Action)
   - Read README.md (Game Flow section)
   - Trace through one movement manually
   - ~1.5 hours

3. **Day 3:**
   - Review ARCHITECTURE.puml (focus on Block hierarchy)
   - Read QUICK_REFERENCE.md (API section)
   - Try running a level, use debugger
   - ~1 hour

4. **Ongoing:**
   - Keep QUICK_REFERENCE.md bookmarked
   - Refer to specific diagrams as needed
   - Review DEBUGGING TIPS when stuck

### For Experienced Developers:
1. Skim README.md (Architecture overview)
2. Review ARCHITECTURE.puml (3 minutes)
3. Use QUICK_REFERENCE.md as needed
4. Dive into specific code files

---

## 🔍 Key Concepts Map

```
UNDERSTAND THESE FIRST:
├─ GridIsland (single grid-based island)
├─ Block (all interactive objects)
└─ MultiGridManager (coordinates multiple islands)

THEN UNDERSTAND:
├─ How blocks move on an island
├─ How gravity works
└─ How islands merge

FINALLY UNDERSTAND:
├─ Input system (player interactions)
├─ Level loading (instantiation)
└─ Camera system (following action)
```

---

## 📋 Class Reference Quick Links

### Core Classes
| Class | Purpose | Documentation |
|-------|---------|---|
| **Block** | Abstract base for all interactive objects | README.md § Block System |
| **GridIsland** | Single floating island with grid | README.md § GridIsland System |
| **MultiGridManager** | Coordinates multiple islands | README.md § MultiGridManager |
| **GravityManager** | Physics simulation | README.md § Gravity System |
| **LevelLoader** | Loads level from data | README.md § Level System |

### Block Types
| Class | Movement | Gravity | Player Control | Use Case |
|-------|----------|---------|---|---|
| **DynamicBlock** | ✓ | ✓ | ✓ | Main puzzles |
| **MovableStaticBlock** | ✓ | ✗ | ✓ | Platforms |
| **ImmovableBlock** | ✗ | ✗ | ✗ | Walls |
| **JointBlock** | Complex | Varies | ✓ | Multi-blocks |
| **GroundDeployer** | ✓ | ✗ | ✓ | Bridge builder |

### Manager Classes
| Class | Purpose | Documentation |
|-------|---------|---|
| **PlayerInput** | Input handling | README.md § Input System |
| **PlayerController** | NPC character | README.md § Player System |
| **CameraController** | Camera control | README.md § Camera System |
| **DynamicFpsManager** | Performance | README.md § Utility Systems |

---

## 🛠️ Common Tasks & Resources

### Task: Add a new block type
**Files to read:** README.md § Extensibility, QUICK_REFERENCE.md § Example 3, ARCHITECTURE.puml

### Task: Create a level
**Files to read:** README.md § How to Use, QUICK_REFERENCE.md § Example 1

### Task: Debug movement
**Files to read:** QUICK_REFERENCE.md § Debugging Tips, GAME_FLOW_SEQUENCES.puml § Player Move Action

### Task: Optimize performance
**Files to read:** QUICK_REFERENCE.md § Performance Tuning, VISUAL_REFERENCE.md § Performance Profile

### Task: Understand gravity
**Files to read:** README.md § Gravity System, VISUAL_REFERENCE.md § Gravity Algorithm

### Task: Trace an error
**Files to read:** GAME_FLOW_SEQUENCES.puml (relevant sequence), QUICK_REFERENCE.md § Debugging Tips

---

## 📊 Document Statistics

| Document | Type | Size | Read Time |
|----------|------|------|-----------|
| README.md | Markdown | ~25KB | 25-30 min |
| ARCHITECTURE.puml | UML Diagram | ~8KB | 5-10 min |
| GAME_FLOW_SEQUENCES.puml | UML Sequences | ~15KB | 10-15 min |
| QUICK_REFERENCE.md | Reference | ~20KB | 5-10 min |
| VISUAL_REFERENCE.md | Diagrams + Text | ~18KB | 10-15 min |
| **Total** | **All docs** | **~86KB** | **2-3 hours** |

---

## 🎓 Study Checklist

After reading the documentation, you should be able to answer:

- [ ] What is a GridIsland and why are they independent?
- [ ] How do blocks move on an island?
- [ ] What is the coordinate system and how do conversions work?
- [ ] How does gravity work and when does it apply?
- [ ] What are the 5 block types and their differences?
- [ ] How do two islands merge?
- [ ] What is the role of MultiGridManager?
- [ ] How does player input translate to block movement?
- [ ] How is a level loaded from LevelData?
- [ ] What happens in a typical game turn?

**All answers are in the documentation!**

---

## 🔗 Cross-References

Quick links to related sections:

**Understanding Movement:**
- README.md → Key Systems → Movement System
- VISUAL_REFERENCE.md → Movement Pipeline Sequence
- GAME_FLOW_SEQUENCES.puml → Player Swipes Block

**Understanding Gravity:**
- README.md → Gravity System
- VISUAL_REFERENCE.md → Gravity Algorithm Visualization
- GAME_FLOW_SEQUENCES.puml → Gravity Application Detailed

**Understanding Island Merging:**
- README.md → Ground Deployment
- VISUAL_REFERENCE.md → Multi-Island System
- GAME_FLOW_SEQUENCES.puml → Island Merge Process

**Understanding Grid Coordinates:**
- README.md → Data Structures
- VISUAL_REFERENCE.md → Coordinate System Visualization
- GAME_FLOW_SEQUENCES.puml → Coordinate System Transformation

---

## 📞 FAQ - Which Document Should I Read?

**Q: I'm new to the project. Where do I start?**
A: Read README.md (Overview + Core Architecture sections)

**Q: I need to find a specific method.**
A: Check QUICK_REFERENCE.md (API Quick Reference table)

**Q: I want to see how things work visually.**
A: Look at VISUAL_REFERENCE.md diagrams

**Q: I'm debugging an issue.**
A: Check QUICK_REFERENCE.md (Debugging Tips) + relevant GAME_FLOW_SEQUENCES.puml

**Q: I want to extend the system.**
A: Read README.md (Extensibility section) + QUICK_REFERENCE.md (Code Examples)

**Q: I need performance optimization.**
A: See QUICK_REFERENCE.md (Performance Tuning) + README.md (Performance Considerations)

**Q: I want to understand data flow.**
A: View VISUAL_REFERENCE.md (Data Flow Diagram)

**Q: I'm confused about class relationships.**
A: Look at ARCHITECTURE.puml (UML diagram)

---

## 📝 Notes for Keeping Documentation Updated

When modifying code:
1. Update relevant section in README.md
2. Update API table in QUICK_REFERENCE.md
3. Update UML diagrams if class structure changes
4. Add example to QUICK_REFERENCE.md if new pattern emerges
5. Update VISUAL_REFERENCE.md if systems change

**Documentation Maintainers:** Keep files in sync!

---

## 🚀 Getting Started Checklist

- [ ] Read README.md overview
- [ ] Review VISUAL_REFERENCE.md System Overview
- [ ] Study your specific area of interest
- [ ] Try a code example from QUICK_REFERENCE.md
- [ ] Run the game and experiment
- [ ] Reference documentation as needed
- [ ] Contribute improvements to docs!

---

**Documentation Version:** 1.0  
**Last Updated:** 2024  
**Maintained By:** BlockShift Development Team

---

## 📂 Files in This Documentation Suite

```
├─ README.md                    (Main comprehensive guide)
├─ ARCHITECTURE.puml            (UML class diagram)
├─ GAME_FLOW_SEQUENCES.puml    (Sequence diagrams)
├─ QUICK_REFERENCE.md          (Cheat sheet + examples)
├─ VISUAL_REFERENCE.md         (Diagrams + visual guides)
└─ INDEX.md                     (This file)
```

**All files serve specific purposes. Keep all files together!**

---

Happy coding! 🎮
