# VoCards — Modernization & Expansion Plan

> Status legend: `[ ]` todo · `[~]` in progress · `[x]` done
>
> This file is the working checklist for the modernization effort. It is kept
> up to date as work lands, so the state of the tree can always be read here.

---

## 0. Where we started

The original VoCards (Sep–Dec 2021) was a **336-line .NET Core 3.1 console app**:

| File | Role |
| --- | --- |
| `Program.cs` | `Main` loop, console I/O, `BinaryFormatter` save/load, seed data |
| `Classes.cs` | `Progress` → `Deck` → `Card` object graph |

### Problems inventory

| # | Problem | Detail |
| --- | --- | --- |
| P1 | **Dead framework** | `netcoreapp3.1` went out of support in Dec 2022 |
| P2 | **`BinaryFormatter`** | Removed in .NET 9+; a known RCE vector (BinaryFormatter is obsolete as of .NET 5, error as of .NET 8) |
| P3 | **Deep nesting** | `GoThroughCards()` nests `else` → `for(;;)` → `if` → `for(;;)` → `if` — 5 levels; `Main` nests `while` → `for(;;)` → `switch` → `for(;;)` → `if` |
| P4 | **`for(;;)` spin loops** | Four infinite loops used as input validators; one can never exit (`Learn` branch accepts *any* non-`Add` string) |
| P5 | **Hand-rolled counters** | `words_total`, `words_learnt` mutated in parallel with the lists — they desync the moment anything is removed |
| P6 | **O(n) linear scans** | `HasTopic` scans the whole list *and doesn't early-exit*; `FindByTopic` likewise |
| P7 | **Public mutable field** | `Card.memorized` is a public field, mutated through a "please don't" comment |
| P8 | **No test coverage** | Zero tests, zero CI |
| P9 | **Binary learning model** | A card is "memorized" or not — forever. No forgetting, no review, no scheduling |
| P10 | **Console-only UI** | No visual design surface at all |
| P11 | **Build output committed** | `bin/` and `obj/` are tracked in git |
| P12 | **9 hard-coded words** | Three decks of three English→Russian cards |
| P13 | **No i18n / no locale** | Russian strings hard-coded into seed data |
| P14 | **Crash on bad input** | `int.TryParse` result ignored in one path; `progress[index]` with `index == -1` |

---

## 1. Target architecture

**.NET 10 (LTS)** — .NET 9 is already end-of-life, so the "modern .NET" target is 10.

```
VoCards.sln
├── src/
│   ├── VoCards.Core/        Pure domain library. No UI, no I/O, fully unit-testable.
│   └── VoCards.Web/         Blazor WebAssembly standalone PWA. The new UI.
├── tests/
│   └── VoCards.Core.Tests/  xUnit suite over the domain.
├── legacy/                  The original 2021 console app, preserved verbatim.
└── docs/                    Architecture, design system, feature docs.
```

**Why this shape:** the domain is a plain class library with zero UI references, so
every scheduling rule, stat and import path is testable without a browser. The Blazor
project holds only presentation + storage interop.

### De-nesting strategy (addresses P3/P4)

| Technique | Applied to |
| --- | --- |
| Guard clauses + early return | every validation path |
| LINQ over manual accumulator loops | `CountTotalWords`, `CountLearntWords`, `HasTopic`, `FindByTopic` |
| `record` types + immutability | `Card`, `Deck`, `Review` |
| Computed properties over mutable counters | kills P5 desync entirely |
| Pattern matching / switch expressions | scheduler + rating logic |
| Dictionary index over linear scan | deck/card lookup by id |
| State machine object over nested loops | `StudySession` replaces `GoThroughCards` |

---

## 2. Task list

### Phase 1 — Foundation
- [x] T1.1 Install .NET 10 SDK, verify Blazor WASM template builds
- [x] T1.2 Archive original app to `legacy/` with a README explaining its role
- [x] T1.3 Untrack `bin/`/`obj/`, write a comprehensive .NET `.gitignore` (P11)
- [x] T1.4 Create `VoCards.sln` + three projects, wire references
- [x] T1.5 `Directory.Build.props`: nullable enabled, implicit usings, warnings-as-errors, latest langver
- [x] T1.6 `.editorconfig` with C# style rules

### Phase 2 — Domain (`VoCards.Core`)
- [x] T2.1 `Card` record: id, front, back, examples, notes, tags, starred, created/modified, `CardState`
- [x] T2.2 `Deck` record: id, name, description, emoji/icon, colour, language pair, cards, settings
- [x] T2.3 `Library` aggregate root replacing `Progress`, with dictionary-indexed lookup (P6)
- [x] T2.4 Computed stats — no stored counters (P5)
- [x] T2.5 `Review` record + `ReviewLog` history
- [x] T2.6 **SM-2 scheduler** — ease factor, interval, repetitions, graduation
- [x] T2.7 **FSRS-style scheduler** — stability/difficulty/retrievability, selectable per deck
- [x] T2.8 `IScheduler` abstraction so schedulers are swappable
- [x] T2.9 Leech detection (lapse threshold → tag + suspend)
- [x] T2.10 `StudySession` state machine (replaces nested `GoThroughCards`, P3)
- [x] T2.11 Study **direction**: front→back, back→front, mixed
- [x] T2.12 Study **modes**: Flip, MultipleChoice, Typing, Listening, Match, SpeedRound, Cram
- [x] T2.13 Answer grading: exact, case-insensitive, accent-insensitive, Levenshtein "close enough"
- [x] T2.14 `DueCardSelector` — new/learning/review mixing with daily caps
- [x] T2.15 Stats service: retention, forecast, heatmap buckets, per-deck mastery, streaks
- [x] T2.16 Gamification: XP curve, levels, daily goals, streak + streak-freeze
- [x] T2.17 Achievement engine + a catalogue of badges
- [x] T2.18 Search/filter: text, tag, state, starred, leech, difficulty
- [x] T2.19 Import/Export: JSON, CSV, TSV (Anki-style), with round-trip fidelity
- [x] T2.20 `System.Text.Json` source-generated context (kills `BinaryFormatter`, P2)
- [x] T2.21 Seed content — a real starter library, far beyond the original 9 words (P12)
- [x] T2.22 `Result<T>` / validation types so bad input never throws (P14)

### Phase 3 — Tests (`VoCards.Core.Tests`)
- [x] T3.1 Model + library invariants
- [x] T3.2 SM-2 scheduler behaviour incl. lapse/graduation edges
- [x] T3.3 FSRS scheduler behaviour
- [x] T3.4 `StudySession` transitions across all modes
- [x] T3.5 Answer-grading matrix (accents, case, typos)
- [x] T3.6 Stats/streak/XP calculations
- [x] T3.7 Achievement unlock conditions
- [x] T3.8 Import/export round-trips
- [x] T3.9 Search/filter matrix
- [x] T3.10 Regression tests for every original bug in the P-table

### Phase 4 — Design system (`VoCards.Web`)
- [x] T4.1 CSS custom-property token layer: colour, space, radius, shadow, motion, type scale
- [x] T4.2 Light + dark themes, `prefers-color-scheme` aware, manual override
- [x] T4.3 Multiple accent palettes the user can pick
- [x] T4.4 Typography, fluid type scale, icon set (inline SVG — no icon-font dependency)
- [x] T4.5 Core primitives: Button, Card, Badge, Input, Select, Toggle, Modal, Tooltip, Toast, Tabs, Progress, Skeleton
- [x] T4.6 Motion system: flip, slide, stagger, spring — all gated on `prefers-reduced-motion`
- [x] T4.7 Responsive shell: sidebar → bottom nav on mobile
- [x] T4.8 Accessibility pass: focus rings, ARIA, contrast, full keyboard reachability

### Phase 5 — App features (`VoCards.Web`)
- [x] T5.1 App shell, routing, nav, theme switcher
- [x] T5.2 **Dashboard**: due-today, streak ring, XP/level, quick actions, activity heatmap
- [x] T5.3 **Decks** grid: create, edit, duplicate, delete, colour/emoji picker, search
- [x] T5.4 **Deck detail**: card table, bulk select, inline edit, per-deck settings
- [x] T5.5 **Study view**: 3D flip card, rating bar, progress, keyboard-first
- [x] T5.6 Study mode — Multiple choice
- [x] T5.7 Study mode — Typing with diff-highlighted feedback
- [x] T5.8 Study mode — Listening (TTS prompt)
- [x] T5.9 Study mode — Match/pairs mini-game
- [x] T5.10 Study mode — Speed round with timer
- [x] T5.11 Session summary screen with per-card recap
- [x] T5.12 **Text-to-speech** pronunciation + voice/locale picker
- [x] T5.13 **Browse**: global search, filters, sort, bulk actions
- [x] T5.14 **Stats page**: charts (retention, forecast, reviews/day), heatmap, per-deck mastery
- [x] T5.15 **Achievements page** with locked/unlocked states
- [x] T5.16 **Settings**: theme, accent, daily goal, scheduler, TTS, motion, data controls
- [x] T5.17 **Import/Export** UI with file pick + download
- [x] T5.18 **Command palette** (Ctrl/Cmd-K) over decks, cards, actions, navigation
- [x] T5.19 Global keyboard shortcuts + a shortcuts cheat-sheet overlay
- [x] T5.20 Toast notifications + undo for destructive actions
- [x] T5.21 Onboarding / first-run experience
- [x] T5.22 IndexedDB persistence via JS interop, with autosave + migration versioning
- [x] T5.23 PWA: manifest, service worker, offline, installable
- [x] T5.24 Empty states, loading skeletons, error boundary

### Phase 6 — Repo health
- [x] T6.1 GitHub Actions CI: restore → build → test → publish artifact
- [x] T6.2 GitHub Pages deployment workflow for the WASM app
- [x] T6.3 Rewrite `README.md`: screenshots, features, architecture, quick start
- [x] T6.4 `docs/ARCHITECTURE.md`, `docs/DESIGN.md`
- [x] T6.5 Final: full build + full test run green, plan file ticked off

---

## 3. Non-goals

- No backend/server — the app stays local-first, 100% client-side.
- No account system or cloud sync.
- No external UI component library; the design system is hand-built.

---

## 4. Outcome

All six phases are complete. Final state:

| | |
| --- | --- |
| Target framework | .NET 10 (LTS) — .NET 9 reached end of life before this work started |
| Tests | 225, all passing, running in under half a second |
| Build | Clean with nullable enabled and warnings as errors |
| Decks shipped | 9, across 6 languages, including the original three |
| Cards shipped | 187, including all nine original words |
| Study modes | 6, all reachable from the study launcher |
| Schedulers | 2 (SM-2, FSRS 4.5), selectable per deck |
| Achievements | 30, across 6 categories |
| Pages | 8 |

### Bugs the work surfaced

Writing the tests found two real defects in code that compiled cleanly:

1. `TextTools.EditDistance` used `previous[j]` instead of `previous[j - 1]` for the
   substitution term, so it returned nonsense for any non-trivial pair.
2. Plain Levenshtein scores a transposition as two edits, which put "recieve"
   against "receive" at 0.71 similarity and rejected the single most common kind of
   typo. Replaced with Damerau–Levenshtein.

Driving the app in a real browser found four more that type-checked and rendered:

3. The study view kept the app shell behind it — a render-order race on a shared
   flag, fixed with a dedicated layout.
4. Ending a session with Escape built the recap and discarded it, because keyboard
   events arrive outside the component's event dispatch and nothing re-rendered it.
5. The lifecycle donut was invisible: its own CSS set `stroke` and `stroke-width`,
   and CSS beats SVG presentation attributes.
6. Deep-linking to `/study` reported "no decks to study", because the new study
   layout did not hold the page back until the library had loaded from IndexedDB.
