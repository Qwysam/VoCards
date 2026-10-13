# Design system

## Tokens

Every colour, space, radius, shadow and duration resolves through a CSS custom
property defined in `wwwroot/css/tokens.css`. No component hard-codes a value, so
themes and accents work by re-pointing tokens and nothing else has to know.

Five layers load in cascade order:

| Layer | Holds |
| --- | --- |
| `tokens.css` | the custom properties, both themes, six accents, deck colours |
| `base.css` | reset, typography, focus, scrollbars, motion preferences |
| `components.css` | buttons, cards, inputs, badges, modals, toasts, tables |
| `layout.css` | the shell, sidebar, top bar, tab bar, grids, study layout |
| `features.css` | flashcard, charts, heatmap, trophies, command palette |

## Themes

Light and dark are **both selected**, not one flipped from the other. The dark
surfaces are their own values, and the chart ramps are re-stepped for a dark
background rather than inverted.

Each dark value is declared twice: once under `prefers-color-scheme: dark` guarded
by `:root:not([data-theme="light"])`, and once under `:root[data-theme="dark"]`.
The guard lets an explicit light choice beat an OS set to dark, and the explicit
scope lets an explicit dark choice beat an OS set to light. The in-app toggle wins
either way.

A small module stamps the stored theme onto `<html>` before Blazor boots, so there
is no flash of the wrong colour scheme while the WebAssembly runtime downloads.

## Accents

Six palettes — violet, aurora, ember, forest, rose, midnight. The accent styles
**chrome only**: buttons, focus rings, progress fills, the streak ring, deck glyph
tints.

Data series never use the accent. That is deliberate: it means switching accent
cannot disturb a chart palette that was validated for contrast and colour-vision
deficiency.

## Motion

Durations and easings are tokens. Everything is disabled two ways: the OS
`prefers-reduced-motion` media query, and a `data-motion="reduced"` attribute driven
by the in-app switch, for people whose OS does not expose the preference.

The card flip degrades rather than disappearing — with animation off, the two faces
cross-fade by visibility instead of rotating in 3D.

## Charts

Chart colours come from a validated reference palette, and every set used in the app
was run through the six-check validator — lightness band, chroma floor, adjacent-pair
CVD separation, normal-vision floor, contrast against the surface — in **both** light
and dark. The results are recorded here so the next person does not have to re-derive
them.

### The lifecycle donut

New → Learning → Young → Mature is an *ordered progression*, not a set of unrelated
categories, so it uses an **ordinal single-hue ramp** rather than categorical hues.
Suspended sits outside the progression and takes a neutral grey.

The dark steps run in the opposite hex direction from the light ones:

| Step | Light | Dark |
| --- | --- | --- |
| New | `#86b6ef` | `#184f95` |
| Learning | `#5598e7` | `#256abf` |
| Young | `#2a78d6` | `#3987e5` |
| Mature | `#184f95` | `#86b6ef` |

That is not an oversight. The encoded quantity is *distance from the surface* — on
a light background the least-progressed step is the lightest, and on a dark
background it is the darkest. Both pass the ordinal checks: monotone lightness,
adjacent ΔL ≥ 0.06, single hue, and the surface-adjacent end clearing 2:1.

### The forecast bars

Two categorical series, mature and young, using slots 1 and 2 (blue, orange). Both
modes pass all-pairs CVD separation comfortably (ΔE 24.7 light, 26.8 dark). A legend
is always present, and the stacked segments are separated by a 2 px surface gap.

### The retention curve

One series, so **no legend box** — the panel title names it. 2 px line, 5 px markers
ringed in the surface colour, and direct labels on the endpoints only, never a number
on every point. A dashed reference line marks the 90% target.

### The activity heatmap

Sequential encoding: one hue, five steps, where level 0 is the empty cell.

Intensity scales against the **90th percentile** of non-empty days rather than the
maximum, so one marathon session cannot flatten a year of ordinary ones.

### Rules that cost something to learn

- **A donut segment *is* its stroke.** A `.donut-seg` rule that sets `stroke` or
  `stroke-width` silently repaints every segment, because CSS beats SVG presentation
  attributes. The chart rendered as an invisible 2 px ring in the surface colour
  until that rule was removed. The inter-segment gap comes from shortening each arc
  instead.
- **Razor reserves `<text>`.** SVG text elements are written `<svg:text>`;
  `createElementNS` resolves that to localName `text` in the SVG namespace, which is
  what the browser renders.
- **Razor parses a leading `<` in a switch expression as a markup tag.** Relational
  patterns in `.razor` files are written with `>=` instead.
- **Every number interpolated into SVG or CSS goes through `Format`**, which uses the
  invariant culture. A browser will not accept `0,75` as a `stroke-dashoffset`, so
  doing this by hand breaks the app for anyone with a comma decimal separator.

## Accessibility

- Focus is always visible for keyboard users and never painted on a click
  (`:focus-visible`, never `:focus`).
- Every chart carries an `aria-label` describing what it shows in words, and the
  underlying numbers appear in a table or legend beside it — colour is never the only
  channel.
- Status is icon plus label, never colour alone.
- The layout works from 320 px up, with a 16 px minimum side gutter and no horizontal
  page scroll. The heatmap scrolls inside itself rather than widening the page.
- A skip link precedes the shell.
- Hit targets on the tab bar and rating buttons are at least 44 px tall.
