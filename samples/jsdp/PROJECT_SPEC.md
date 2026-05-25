# JoyMon — Tile Overworld Prototype

## Product goal

Deliver a MonoGame tile overworld where the player moves on a grid, triggers encounter zones, and enters a minimal turn-based battle loop.

## Tech stack

- MonoGame 3.8
- .NET 8
- Tiled map pipeline

## Core systems

- Tilemap rendering and collision
- Overworld movement with zone transitions
- Encounter zone hooks
- Turn-order battle state machine

## Domain concepts

- `OverworldScene`
- `EncounterZone`
- `BattleLoop`
- `TileCollider`

## Constraints

- Deterministic turn order (no hidden RNG in MVP)
- All gameplay state serializable for replay tests
- No network stack in slice 1

## Non-goals

- Multiplayer
- Full Pokédex-style content pipeline
- Procedural world generation

## Acceptance criteria

- Player can walk on a 16×16 tilemap with solid tiles blocking movement
- Entering an encounter zone starts a battle with at least one enemy action
- Battle ends with win/loss UI feedback

## Verification

- dotnet build JoyMon.sln
- dotnet test JoyMon.Tests/JoyMon.Tests.csproj
- echo "simulation: overworld smoke"
