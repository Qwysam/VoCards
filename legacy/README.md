# Legacy VoCards (2021)

This folder preserves the **original VoCards** exactly as it stood at commit
`d433332` — a .NET Core 3.1 console flashcard app written in September–December 2021.

| File | Description |
| --- | --- |
| `Program.cs` | Entry point, console menu loop, `BinaryFormatter` persistence, seed decks |
| `Classes.cs` | `Progress` / `Deck` / `Card` domain classes |
| `VoCards.Legacy.csproj.original` | The original project file, targeting `netcoreapp3.1` |

## Why it is not buildable here

The project file carries the `.original` suffix on purpose, so that neither
`dotnet build` nor CI globs pick it up. It could not be built by a modern SDK anyway:

* `netcoreapp3.1` reached end of support in December 2022.
* `BinaryFormatter` — which `Program.cs` uses for both save and load — was made
  obsolete in .NET 5, an error in .NET 8, and removed from the runtime in .NET 9.

The code is kept purely as a historical reference: it is where every feature in the
modern app originated, and `PLAN.md` catalogues the specific problems (P1–P14) that
the rewrite set out to fix.

The modern rewrite lives in [`../src`](../src).
