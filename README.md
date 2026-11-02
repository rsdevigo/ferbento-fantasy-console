# Ferbento

Ferbento is a **fantasy console** written in C#: a virtual computer with deliberately limited specs, in the spirit of Pico-8, with an integrated environment for making retro games, from code to sprites, maps and sound.

- **Pico-8 behavioral compatibility**: the console API (`spr`, `map`, `pset`, `btn`, `sfx`, `music`, `peek`/`poke`, etc.) reproduces Pico-8 semantics. Behavioral fidelity takes priority over implementation convenience.
- **GDScript as the language**: games are written in GDScript (Godot's language) instead of Lua, run by a custom sandboxed interpreter with the `_init` / `_update` / `_update60` / `_draw` loop.
- **Hardware defined in JSON**: resolution, palette, sprites, map, memory, audio channels and code limits come from a *hardware profile*. Built-in profiles inspired by Pico-8, Game Boy, Game Boy Color, NES, SNES and Mega Drive are included.

## Editor tools

The desktop editor (Avalonia) brings together, in tabs:

- **Code**: editor with syntax highlighting and API autocompletion.
- **Sprites**: sprite sheet editing with flags, brush, eraser, fill and zoom.
- **Tilemap**: map editing with layers via sprite flags.
- **Palette**: editable RGB colors, with the color count limited by the active profile.
- **SFX**: piano roll with effects, custom instruments and waveform slots.
- **Music**: pattern tracker that combines SFX.
- **Hardware profiles**, **project manager**, **asset browser** and **debug panel**.

All tools adapt to the active hardware profile.

## Structure

| Project | Purpose |
| --- | --- |
| `src/Ferbento.Core` | Console runtime: virtual memory, chips (video, sprites, tilemap, sound, music, input), rendering and audio |
| `src/Ferbento.Scripting` | GDScript interpreter and the Pico-8-compatible API |
| `src/Ferbento.Profiles` | Hardware profile model, schema and validation |
| `src/Ferbento.Cartridge` | PNG cartridge format (data embedded in the image) |
| `src/Ferbento.Editor` | Desktop editor (Avalonia) |
| `profiles/` | Built-in hardware profiles and the JSON Schema (`schema/v1`) |
| `tests/` | Core, scripting, editor and Pico-8 compatibility tests |

## Getting started

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```sh
dotnet build Ferbento.sln
dotnet run --project src/Ferbento.Editor
dotnet test Ferbento.sln
```

## Status

Work in progress. Still to come: cartridge import/export (`.p8`, `.p8.png`, `.ferb.png`) from the editor, asset import/export, and HTML/WASM export.
