# Architecture

## Shape

```
VoCards.slnx
├── src/VoCards.Core/       domain — no UI, no I/O, no browser
├── src/VoCards.Web/        Blazor WebAssembly PWA — presentation and interop
├── tests/VoCards.Core.Tests/   225 xUnit tests over the domain
└── legacy/                 the 2021 console app, preserved
```

The dependency arrow points one way: `Web → Core`. `Core` references nothing but
the base class library. That is the constraint everything else follows from.

## Why the domain is isolated

Every rule worth testing lives in `Core`: the two schedulers, the study state
machine, answer grading, statistics, achievements, import and export. None of it
touches a browser, a file, or a clock it does not own.

The clock in particular: every method that schedules takes a `DateTimeOffset`, and
anything stateful takes an `IClock`. The test suite hands it a `FixedClock` and
winds it forward by hand, so a test can drive months of review in microseconds and
get the same answer every time. This is why the suite runs in under half a second.

## Core, by folder

| Folder | What lives there |
| --- | --- |
| `Common/` | `Result<T>`, `IClock`, `TextTools` (diacritic folding, Damerau–Levenshtein) |
| `Models/` | `Library` → `Deck` → `Card`, plus `Review`/`ReviewLog`, `UserProfile`, `DeckSettings` |
| `Scheduling/` | `IScheduler`, `Sm2Scheduler`, `FsrsScheduler`, `CardReviewer` |
| `Study/` | `StudySession`, `DueSelector`, `AnswerGrader`, `StudyOptions`, `SessionSummary` |
| `Stats/` | `StatsService` and its DTOs |
| `Gamification/` | XP rules, the achievement catalogue, the unlock engine |
| `Search/` | `CardQuery` and `CardSearch` |
| `Serialization/` | versioned DTOs, source-generated JSON, CSV/TSV porter |
| `Seed/` | the starter library |

### The aggregate

`Library` is the aggregate root. It owns the decks, the review log and the profile,
and it is the only thing the web layer holds a reference to.

Counts are **derived, never stored**. `Deck.TotalCards` is `_cards.Count`;
`Deck.MaturedCards` is a predicate over the list. The 2021 version kept
`words_total_deck` and `words_learnt_deck` as fields it incremented by hand, and
they drifted from reality on the first failed removal. A derived count cannot drift.

Lookups are dictionary-backed. `Deck` keeps a `Dictionary<Guid, Card>` beside its
list, so `FindCard` is O(1). The original scanned the whole collection for every
lookup, without even breaking out of the loop on a hit.

### Scheduling

`IScheduler` has one meaningful method:

```csharp
SchedulingOutcome Schedule(Card card, Rating rating, DeckSettings settings, DateTimeOffset now);
```

Implementations must be **pure**: same inputs, same outcome, and never mutate the
card they are handed. That buys two things. Tests can assert on an outcome without
setting up a world, and the UI can ask "what would Easy do?" four times per card to
label the rating buttons — which is exactly what `PreviewIntervals` does.

Writing the outcome back is `CardReviewer`'s job, and only `CardReviewer`'s. Every
scheduling field on `Card` has an `internal` setter, so nothing outside the assembly
can move a card's schedule without going through it. Compare `Card.memorized` in the
original: a public field, mutable by anyone, protected only by a comment asking
callers not to.

**SM-2** tracks an ease factor and multiplies the last interval by it, with learning
steps before graduation and a lapse path that drops ease and re-enters relearning.

**FSRS 4.5** models memory as three quantities — stability, difficulty and
retrievability — and solves the interval directly for the deck's target retention.
Two identities from the paper are asserted in the tests: recall probability is
exactly 0.9 when elapsed time equals stability, and the interval for 90% retention
is exactly stability.

### The study session

`StudySession` is an explicit state machine, and it exists because of what it
replaced. The 2021 `Deck.GoThroughCards()` drove the entire interaction from one
method nested five levels deep — `else` → `for(;;)` → `if` → `for(;;)` → `if` —
reading from the console at the bottom, with a card counter it mutated while
looping over the collection it was counting.

Here every step is a public method that returns:

```
Prompting ──Reveal()──▶ Revealed ──Grade(rating)──▶ Prompting │ Finished
```

Modes layer on top rather than branching inside: `SubmitTyped`/`CommitTyped` for
typing, `Choose` for the choice modes, and `PeekBatch` for match, which hands back
the next N cards in queue order so the batch view can grade them back with ordinary
sequential `Grade` calls.

Cram mode is a flag on `StudyOptions` that skips the write-back entirely, so it can
never touch the schedule.

### Persistence

The on-disk format is an explicit set of versioned DTOs in `Serialization/`, not the
live object graph. Renaming a private field cannot break an existing save, and a
file written by a newer schema version is rejected with a sentence the UI can show
rather than an exception.

Serialization is `System.Text.Json` with **two source-generated contexts** — one
compact for storage, one indented for export. Source generation rather than
reflection because Blazor WebAssembly trims aggressively and reflection-based
serialization is fragile under trimming.

This replaces `BinaryFormatter`, which the original used for both save and load.
`BinaryFormatter.Deserialize` can construct arbitrary types from attacker-controlled
input; it was obsoleted in .NET 5, made an error in .NET 8, and removed from the
runtime in .NET 9.

## Web, by folder

| Folder | What lives there |
| --- | --- |
| `Services/` | `LibraryStore`, `JsBridge`, `AppState`, `ToastService`, `KeyboardService` |
| `Layout/` | `MainLayout` (shell), `StudyLayout` (full screen), nav, top bar, tab bar |
| `Components/` | design-system primitives, charts, the study-mode views |
| `Pages/` | Home, Decks, DeckDetail, Study, Browse, Stats, Achievements, Settings |

### State

`LibraryStore` owns the single `Library`, persists it, and raises `Changed`.
Components subscribe in `OnInitialized` and unsubscribe in `Dispose`.

Saving is debounced by 700 ms and coalesced, because a study session grades a card
every couple of seconds and serialising the whole library per keystroke is waste.
`SaveNowAsync` bypasses the debounce for moments that must not be lost — the end of
a session, and disposal.

### Interop

All JavaScript goes through `JsBridge`, one typed wrapper over one module. Every
call swallows the `JSException` a hostile environment throws, so a private window
with storage blocked, or a platform with no speech synthesis, degrades rather than
crashing.

Storage is IndexedDB with a `localStorage` fallback — a serious library outgrows the
~5 MB `localStorage` allows.

### Keyboard

Global key presses arrive at a `[JSInvokable]` on `App` and are offered to a **stack**
of handlers, topmost first. The study page pushes one while it is on screen, an open
modal pushes one on top, and only the topmost sees a press. That is what makes
"Escape closes the thing in front" work without any component knowing what else
is open.

One consequence is worth stating, because it caused a bug: a press dispatched this
way runs **outside the handling component's event dispatch**, so Blazor does not
re-render it the way it would after an `@onclick`. Keyboard-initiated work in
`Study.razor` therefore runs through a helper that calls `StateHasChanged` after
awaiting.

### Layouts

`MainLayout` is the shell. `StudyLayout` renders the page alone, full screen.

Both hold the page back until `Store.IsReady`. Without that gate, deep-linking
straight to `/study` builds a session against a library that has not come out of
IndexedDB yet, and reports that there is nothing to study.

An earlier version had `MainLayout` hide its chrome when an `AppState.Immersive`
flag was set. That depends on the page's `OnInitialized` running before the layout
renders — a race the layout lost. Choosing the layout per page is decided at routing
time and cannot race.
