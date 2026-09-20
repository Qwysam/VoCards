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
- [ ] T4.1 CSS custom-property token layer: colour, space, radius, shadow, motion, type scale
- [ ] T4.2 Light + dark themes, `prefers-color-scheme` aware, manual override
- [ ] T4.3 Multiple accent palettes the user can pick
- [ ] T4.4 Typography, fluid type scale, icon set (inline SVG — no icon-font dependency)
- [ ] T4.5 Core primitives: Button, Card, Badge, Input, Select, Toggle, Modal, Tooltip, Toast, Tabs, Progress, Skeleton
- [ ] T4.6 Motion system: flip, slide, stagger, spring — all gated on `prefers-reduced-motion`
- [ ] T4.7 Responsive shell: sidebar → bottom nav on mobile
- [ ] T4.8 Accessibility pass: focus rings, ARIA, contrast, full keyboard reachability

### Phase 5 — App features (`VoCards.Web`)
- [ ] T5.1 App shell, routing, nav, theme switcher
- [ ] T5.2 **Dashboard**: due-today, streak ring, XP/level, quick actions, activity heatmap
- [ ] T5.3 **Decks** grid: create, edit, duplicate, delete, colour/emoji picker, search
- [ ] T5.4 **Deck detail**: card table, bulk select, inline edit, per-deck settings
- [ ] T5.5 **Study view**: 3D flip card, rating bar, progress, keyboard-first
- [ ] T5.6 Study mode — Multiple choice
- [ ] T5.7 Study mode — Typing with diff-highlighted feedback
- [ ] T5.8 Study mode — Listening (TTS prompt)
- [ ] T5.9 Study mode — Match/pairs mini-game
- [ ] T5.10 Study mode — Speed round with timer
- [ ] T5.11 Session summary screen with per-card recap
- [ ] T5.12 **Text-to-speech** pronunciation + voice/locale picker
- [ ] T5.13 **Browse**: global search, filters, sort, bulk actions
- [ ] T5.14 **Stats page**: charts (retention, forecast, reviews/day), heatmap, per-deck mastery
- [ ] T5.15 **Achievements page** with locked/unlocked states
- [ ] T5.16 **Settings**: theme, accent, daily goal, scheduler, TTS, motion, data controls
- [ ] T5.17 **Import/Export** UI with file pick + download
- [ ] T5.18 **Command palette** (Ctrl/Cmd-K) over decks, cards, actions, navigation
- [ ] T5.19 Global keyboard shortcuts + a shortcuts cheat-sheet overlay
- [ ] T5.20 Toast notifications + undo for destructive actions
- [ ] T5.21 Onboarding / first-run experience
- [ ] T5.22 IndexedDB persistence via JS interop, with autosave + migration versioning
- [ ] T5.23 PWA: manifest, service worker, offline, installable
- [ ] T5.24 Empty states, loading skeletons, error boundary

### Phase 6 — Repo health
- [ ] T6.1 GitHub Actions CI: restore → build → test → publish artifact
- [ ] T6.2 GitHub Pages deployment workflow for the WASM app
- [ ] T6.3 Rewrite `README.md`: screenshots, features, architecture, quick start
- [ ] T6.4 `docs/ARCHITECTURE.md`, `docs/DESIGN.md`
- [ ] T6.5 Final: full build + full test run green, plan file ticked off

---

## 3. Non-goals

- No backend/server — the app stays local-first, 100% client-side.
- No account system or cloud sync.
- No external UI component library; the design system is hand-built.
