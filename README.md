# Flame Emblem

A personal, original 2D tactical RPG project inspired by classic grid-based strategy RPG design.

## Technical baseline

- Engine: Godot 4.6.x .NET edition
- Language: C# / .NET 8
- Runtime: fully local single-player
- Enemy behavior: deterministic/rule-based game logic only; no LLM or generative AI is used by the game
- Content direction: original characters, story, maps, UI, audio and art assets rather than copied commercial game assets

## Code comment policy

All C# source files in this repository must contain meaningful comments. Public classes and methods should use XML documentation where practical, and important game-state transitions, combat formulas, pathfinding rules, and non-obvious branches must include concise inline comments.

Configuration files such as `project.godot`, `.tscn`, and JSON data do not support the same C# comment style consistently, so their structure and important fields are documented in repository documentation instead of adding invalid syntax.

## Development

Gameplay implementation is developed on feature branches before being merged into `main`.
