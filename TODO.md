# Ferbento — TODO

## Phase 1 — MVP (Complete ✓)

- [x] Core Runtime: VirtualMemory, ChipBus, IChip chips
- [x] Memory map: MemoryMap.cs (0x0000–0x7FFF Pico-8 layout)
- [x] Fixed-point math: FixedPoint.cs (16.16 signed), Pico8Random.cs (LFSR PRNG)
- [x] Rendering: PixelBuffer, DrawState, Renderer
- [x] GDScript parser: GDShrapt integration
- [x] Tree-walk interpreter: Interpreter, Environment, Coroutine, ScriptValue
- [x] Sandbox: ScriptSandbox (API injection via Pico8ApiRegistry)
- [x] All 11 Pico-8 API modules: Graphics, Audio, Input, Math, Memory, Table, String, CartData, Coroutine, System, Pico8Context
- [x] Game loop: GameLoop.cs (30/60fps, _init/_update/_update60/_draw/flip)
- [x] Headless runtime: FerbentoRuntime (construct → Boot → Step)
- [x] Editor shell: Program.cs, MainWindow, tab bar, run/stop, code editor
- [x] **Token counting** — `Pico8TokenCounter.Count()` scans source, enforces MaxTokens at load time
- [x] **Scripting.Tests** — 35 xUnit tests for interpreter, math API, token counter, sandbox
- [x] **CI pipeline** — `.github/workflows/ci.yml` (dotnet restore → build → test)
- [x] **`.editorconfig`** — indent style, charset, line endings

## Phase 2 — Visual & Audio Editors (Complete ✓)

- [x] **Sprite editor**: `Views/SpriteEditorView.cs` + `ViewModels/SpriteEditorViewModel.cs` — pixel canvas, 16-color palette, tool buttons, undo/redo stack, grid toggle
- [x] **Tilemap editor**: `Views/TilemapEditorView.cs` — scrollable canvas, layer checkboxes, tool buttons
- [x] **Palette editor**: `Views/PaletteEditorView.cs` — 16 color swatches with Pico-8 names, RGB edit area
- [x] **SFX editor**: `Views/SfxEditorView.cs` — slot/waveform selectors, play/stop, tracker grid
- [x] **Music tracker**: `Views/MusicTrackerView.cs` — pattern/speed controls, play/stop/loop, pattern grid
- [x] **Asset browser**: `Views/AssetBrowserView.cs` — category list, preview area, splitter
- [x] **Project manager**: `Views/ProjectManagerView.cs` — new/open/save/save-as, project list
- [x] **Debug panel**: `Views/DebugPanelView.cs` — call stack, variable watch, breakpoints, step over/into/out
- [x] **Profile editor**: `Views/ProfileEditorView.cs` — hardware profile selector/property viewer
- [x] **Autocomplete**: code editor shows Pico-8 API symbol list as you type
- [x] **BreakpointManager**: full infrastructure with pause/resume, step over/into/out, wait handles
- [x] **Sandbox hardening**: call depth limit (512), string length cap (64K), table size cap (64K), `extcmd` API, native call CPU accounting
- [x] **Undo/redo**: snapshot-based UndoRedoStack (max 256 entries), wired into sprite editor VM

## Phase 3 — PNG Cartridge (Complete ✓)

- [x] New project: `Ferbento.Cartridge`
  - `CartridgeFormat.cs` — magic `"FERB"`, sections, CRCs
  - `CartridgeWriter.cs` — serialize → compress → embed in PNG LSBs
  - `CartridgeReader.cs` — extract from PNG → validate CRCs → hydrate
- [x] Wire `load()` / `save()` in-game API
- [x] Cart metadata (title, author, label image) ✓ (built into CartridgeData)
- [ ] Import Pico-8 `.p8` cartridges
- [x] Compatibility roundtrip tests (6 xUnit tests)
- [x] `load()`/`save()` wired into SystemApi via CartridgeReader/Writer
- [x] Scripting → Cartridge dependency added
- [x] `CurrentSource` tracked in Pico8Context/ScriptSandbox

## Phase 4 — Hardware Profiles (Complete ✓)

- [x] Profile models: `HardwareProfile.cs`, all spec classes
- [x] Bundled `profiles/pico8.json` + schema `profiles/schema/v1/hardware-profile.schema.json`
- [x] Built-in profiles: Game Boy, GBC, NES, SNES, Mega Drive
- [x] `ProfileValidator.cs` — JSON + deserialization + semantic validation
- [x] `ProfileConstraints.cs` — runtime constraint enforcement model
- [x] `ProfileMatcher.cs` — match profile to known console by specs
- [x] `ProfileLoader.cs` — load profiles from files/directories
- [x] `BuiltInProfiles.cs` — programmatic profile definitions
- [x] Profile editor UI tab

## Phase 5 — Platform Export (Not Started)

- [ ] HTML/WASM export (Blazor + .NET WASM or Uno Platform)
- [ ] `IRomExportBackend` plugin interface
- [ ] Plugin SDK + `AssemblyLoadContext`-based loader
- [ ] Proof-of-concept ROM export

## Infrastructure / Polish

- [x] Sandbox hardening — call depth, string/table limits, extcmd handler
- [x] Undo/redo stacks for sprite editor
- [x] Auto-complete in code editor (Pico-8 API symbol table)
- [x] Breakpoints UI infrastructure (BreakpointManager with step over/into/out)
- [x] `extcmd("screenshot")` / `extcmd("gif")` — editor command hook
- [ ] Project file format `.fbp` (zip-based container)
- [ ] Additional built-in profile JSONs
