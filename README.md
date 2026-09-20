<div align="center">

# VoCards

**A local-first spaced-repetition vocabulary trainer.**
SM-2 and FSRS scheduling, six study modes, no account, no server.

[![CI](https://github.com/Qwysam/VoCards/actions/workflows/ci.yml/badge.svg)](https://github.com/Qwysam/VoCards/actions/workflows/ci.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![Blazor WebAssembly](https://img.shields.io/badge/Blazor-WebAssembly-5C2D91)](https://learn.microsoft.com/aspnet/core/blazor/)
[![Tests](https://img.shields.io/badge/tests-220%20passing-2ea043)](tests/VoCards.Core.Tests)

</div>

---

## What it is

VoCards started in September 2021 as a 336-line .NET Core 3.1 console app with
nine hard-coded English→Russian words and a `BinaryFormatter` save file. It is now a
.NET 10 domain library with two spaced-repetition schedulers behind a Blazor
WebAssembly front end.

Everything runs in the browser. There is no backend, no account, and nothing is
uploaded anywhere — your library lives in IndexedDB on your own machine, and the
export button hands you the whole thing as a JSON file.

## Features

### Scheduling

- **Two algorithms, per deck.** [SM-2](https://super-memory.com/english/ol/sm2.htm)
  with configurable learning steps, lapse handling and ease clamping; or
  **FSRS 4.5**, which models memory as stability, difficulty and retrievability
  and solves each interval for your target retention.
- **Interval previews** on every rating button, so you can see what Again, Hard,
  Good and Easy will each cost before you press one.
- **Overdue credit** — a card answered late gets credit for the extra days it
  actually survived.
- **Leech detection.** Cards you keep forgetting are flagged so you can rewrite
  or bury them rather than grinding them forever.
- **Daily caps** on new cards and reviews, so today's enthusiasm doesn't ruin
  next Tuesday.

### Six ways to study

| Mode | What it does |
| --- | --- |
| **Flip** | The classic card. Read, flip, grade yourself. |
| **Multiple choice** | Pick from plausible distractors drawn from the same deck. |
| **Typing** | Type the answer. Graded with accent folding, case folding and Damerau–Levenshtein typo tolerance, with a character-level diff on a near miss. |
| **Listening** | Hear the prompt spoken, then answer. |
| **Match** | Pair prompts with answers against the clock. |
| **Speed round** | Multiple choice with a countdown. |

Plus **cram mode**, which ignores due dates and never touches your schedule, and
three directions: front→back, back→front, or randomly mixed.

### Everything else

- **Statistics** — a year-long activity heatmap, a 30-day forecast, a retention
  curve against the 90% target, reviews by hour, per-deck mastery, and the cards
  giving you the most trouble.
- **Gamification** — XP on a widening curve, levels, daily goals, streaks with
  automatic freezes for a missed day, and 30 achievements across six categories.
- **Text to speech** with automatic per-deck voice matching, so a multilingual
  library reads each face in the right language.
- **Command palette** (`Ctrl`/`⌘` + `K`) over decks, cards, actions and navigation.
- **Keyboard-first throughout**, with a `?` cheat sheet.
- **Import and export** — full JSON backups, single shareable decks, or plain
  CSV/TSV word lists with delimiter sniffing and per-row error reporting.
- **Six accent palettes**, light and dark themes, and a reduced-motion switch.
- **Installable PWA** that works fully offline.

## Getting started

```bash
# .NET 10 SDK required: https://dotnet.microsoft.com/download
git clone https://github.com/Qwysam/VoCards.git
cd VoCards

dotnet restore VoCards.slnx
dotnet test VoCards.slnx        # 220 tests
dotnet run --project src/VoCards.Web
```

Then open the URL it prints. On first run you are given a starter library of nine
decks across six languages — including the three original 2021 decks, with their
original words still in them.

## Architecture

```
VoCards.slnx
├── src/
│   ├── VoCards.Core/     Domain. No UI, no I/O, no browser. All of the logic.
│   └── VoCards.Web/      Blazor WebAssembly PWA. Presentation and interop only.
├── tests/
│   └── VoCards.Core.Tests/   220 xUnit tests over the domain.
├── legacy/               The original 2021 console app, preserved.
└── docs/                 Architecture and design notes.
```

`VoCards.Core` has no reference to anything web. Every scheduling rule, statistic
and import path is a pure function over plain objects, which is why the test suite
can drive a year of study in milliseconds without a browser: the domain takes an
`IClock`, and the tests hand it one they wind by hand.

See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) for the detail, and
[`docs/DESIGN.md`](docs/DESIGN.md) for the design system and the rules the charts
follow.

## What changed from 2021

The original is kept in [`legacy/`](legacy/). [`PLAN.md`](PLAN.md) catalogues the
fourteen problems the rewrite set out to fix, and ten of them have a named
regression test so they cannot quietly return. The headlines:

| Then | Now |
| --- | --- |
| `netcoreapp3.1`, out of support since 2022 | .NET 10 LTS |
| `BinaryFormatter` — removed from .NET 9 as an RCE vector | Source-generated `System.Text.Json` with versioned DTOs |
| `GoThroughCards()` nested five levels deep around two `for(;;)` loops | `StudySession`, an explicit state machine |
| `words_total` / `words_learnt` counters mutated by hand, desyncing on removal | Every total computed from the collection |
| `HasTopic` scanned the whole list without early-exit; `FindByTopic` returned the *last* match | Dictionary-backed O(1) lookup |
| `Card.memorized`, a public field guarded by a comment | Full scheduling state, writable only through `CardReviewer` |
| A card marked memorised was excluded from study forever | Cards lapse, relearn and come back |
| No tests, no CI | 220 tests, warnings-as-errors, GitHub Actions |
| Nine hard-coded words | Nine decks across six languages, plus import |

## Licence

See the repository for licence details.

---

<div align="center">
<sub>Built on the 2021 original by <a href="https://github.com/Qwysam">Vsevolod Zhdanov</a>.</sub>
</div>
