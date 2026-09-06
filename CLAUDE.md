# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Use Unity MCP tools whenever possible rather than asking me to manually manipulate the editor.
Use placeholder shapes only — no art, no polish yet.
Work incrementally and check the Unity console for errors as you go.

## What this project is

A Unity 6 (6000.5.7f1, URP) scratch project for developing and visually debugging a **voxel line rasterizer** — an integer-grid DDA/Bresenham variant that answers "which grid cells does this line segment pass through?" in 2D and 3D. It is a sandbox extracted from a larger ForceMasters game, not a shippable app: the scene is the stock URP template scene and the rasterizer is exercised by dropping cubes at every cell the algorithm reports.

The reference the algorithm is derived from is cited in the source: http://members.chello.at/~easyfilter/bresenham.html

## Commands

The `unity` CLI (`~/.unity/bin/unity`) drives the editor from the terminal. Run these from the project root.

```bash
unity status                    # live editor connections: port, project, version, PID, state
unity open                      # open this project (NEVER pass the folder name as an arg from
                                # inside the project — it resolves relative to cwd and doubles the path)
unity test --mode EditMode      # run tests, writes test-results.xml
unity test --mode EditMode --filter "Rasterizer4"   # run a single test / matching subset
unity build                     # batch-mode build
unity command --help            # list tools the Pipeline package registers on the editor
```

There is no lint step, no build script, and **no test assembly yet** — `com.unity.test-framework` is installed but `Assets/` contains no `.asmdef` and no Tests folder, so `unity test` currently has nothing to run. Adding tests means creating an assembly definition first; without one, all scripts land in the default `Assembly-CSharp` / `Assembly-CSharp-Editor` and are not referenceable from a test assembly.

### Unity MCP

The `unity-editor-mcp` server (`unity mcp`) is configured at user level in `~/.claude.json` and connects to whichever editor is running. Prefer it over the CLI for inspecting and editing scene state. Two things to know:

- Target GameObjects by `hierarchyPath` (e.g. `/Cube`), not `instanceId` — the MCP layer returns a corrupted, non-unique `instanceId` for every object (64-bit precision loss in JSON).
- The entry is unpinned. If more than one Unity project is ever open at once, re-register with `unity mcp configure --project-path "<abs path>"`.

## Architecture

All meaningful code is three files in `Assets/Scripts/`. `Assets/TutorialInfo/` is untouched Unity template boilerplate.


