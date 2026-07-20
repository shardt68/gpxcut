---
project: "GpxCut - GPX Track Editor with OSM Visualization"
instructions_version: "1.0"
last_updated: "2026-07-20"
---

# Copilot Instructions for GpxCut

**All team members should use this guide to ensure consistent workflows and knowledge management.**

## Knowledge Management Rules

### ✅ What Goes Into Git (Version Control)

Store in Git when it's:
- **Process documentation** (how to release, build, deploy)
- **Architecture decisions** (AGENTS.md, skills/, software design)
- **Configuration & Setup** (development environment, tooling)
- **Reference material** (API docs, feature specifications)
- **Quality gates** (testing guidelines, performance targets)

**Locations:**
- Process docs → `.github/DEVELOPMENT.md` or `.github/docs/`
- Agent knowledge → `AGENTS.md` or `.github/skills/SKILL.md`
- Architecture → `.github/skills/*/references/software-architecture.md`
- Project rules → `README.md`, `DEVELOPMENT_SETUP.md`

### ⚠️ What Goes Into Memory (Temporary, Local)

Store in memory only when:
- **Session-specific** (progress on current task, temporary notes)
- **In-progress debugging** (error logs, intermediate findings)
- **Personal preferences** (user's favorite tools, patterns)

**Locations:**
- Current session progress → `/memories/session/` (auto-cleared after session)
- User preferences → `/memories/` (persists across sessions, local machine only)

### ❌ What Must NOT Go Into Memory

- Critical workflows or processes
- Any information needed for other developers
- Release procedures or automation knowledge
- Architecture diagrams or decision logs
- Performance baselines or test data

## Key Processes

### Releasing a New Version

**When:** User mentions "release", "tag", "publish", or "version bump"

**Reference:** `.github/DEVELOPMENT.md` → "Automated Release Process"

**Quick Steps:**
1. Update `VERSION_HISTORY.md`
2. `git commit -m "docs: update VERSION_HISTORY for vX.Y.Z"`
3. `git tag -a vX.Y.Z -m "..."`
4. `git push origin main --tags`
5. ✅ GitHub Actions builds and releases automatically

**No manual GitHub Release needed** - Workflow handles everything.

### Development Setup

**When:** User mentions "setup", "configure", "build", "test"

**Reference:** `.github/DEVELOPMENT.md` → "Local Development Setup"

**Key scripts:**
- `./scripts/dev/start-dev.ps1` - Full dev environment
- `./scripts/dev/build-debug.ps1` - Build with debug symbols
- `./scripts/dev/run-app.ps1` - Run app
- `dotnet test` - Run tests

### Performance Testing

**When:** User mentions "test 500k", "1M points", "benchmark", "performance"

**Reference:** `.github/DEVELOPMENT.md` → "Performance Testing"

**Command:**
```bash
dotnet run --project tests/GpxCut.Perf -- generate 1000000 test-1m.gpx
./scripts/dev/benchmark-performance.ps1 -Mode full
```

**Targets:** All operations must complete within MVP 10-second load time.

## Project Context Summary

**What:** Windows GPX track editor with OSM map visualization
**Why:** Edit very large tracks (500k-1M points) while preserving metadata
**How:** WPF+WebView2 (.NET 8) + MapLibre GL + C# streaming GPX I/O

**Core Capabilities:**
- Load & display large GPX files with progressive rendering
- Split track at position, delete range, export segment
- Real-time map interaction (pan, zoom, profile)
- Strict metadata preservation on export

**Technology Stack:**
- UI: WPF (MVVM)
- Map: WebView2 + MapLibre GL JS + OSM tiles
- GPX: XML streaming parser/writer in C#
- Tests: Unit + performance benchmarks

**Performance Requirements:**
- 1M points load in ≤10 seconds (MVP target)
- ≥30 FPS during pan/zoom
- Memory-efficient chunk processing

## Agent Skill

This repository has an agent skill in `.github/skills/gpxcut-track-editing/SKILL.md`.

**Invoke this skill when:**
- User asks about track editing operations (split, delete, export)
- Questions about GPX format, metadata handling, or roundtrip preservation
- Performance optimization or rendering strategy

**Skill topics:**
- Track manipulation (split, range deletion)
- GPX I/O with metadata preservation
- MapLibre rendering and viewport optimization
- Large dataset handling

---

## Summary: Where Things Go

| Need | Location | Commit? |
|------|----------|---------|
| Release instructions | `.github/DEVELOPMENT.md` | ✅ Yes |
| Agent expertise | `.github/skills/*/SKILL.md` | ✅ Yes |
| Architecture decision | `AGENTS.md` or `.github/skills/*/references/` | ✅ Yes |
| Development setup | `.github/DEVELOPMENT.md` | ✅ Yes |
| Performance baseline | `VERSION_HISTORY.md` + `.github/DEVELOPMENT.md` | ✅ Yes |
| Session progress | `/memories/session/*.md` | ❌ No (local) |
| User preferences | `/memories/*.md` | ❌ No (local) |
| Debugging notes | `/memories/session/debug.md` | ❌ No (temporary) |

---

**Last reviewed:** 2026-07-20  
**Next review:** When major process changes occur
