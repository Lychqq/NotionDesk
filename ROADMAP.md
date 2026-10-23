# NotionDesk — Roadmap & Milestones

This document outlines technical milestones, architecture goals, and planned feature development for **NotionDesk**.

---

## Milestone 1: Performance & Input Stability (v1.1.0)

Focus: Selection responsiveness and font rendering correctness.

- [ ] **Virtualization & Zero-Lag Text Selection**:
  - Refactor renderer to avoid selection geometry re-calculations during rapid drag-selection.
  - Restrict `HitTest` and line measurement to visible viewport elements only.
  - Maintain low RAM usage ceiling (~20–30 MB) under heavy continuous selection.
- [ ] **DirectWrite OpenType Color Emoji Support**:
  - Enable native color glyph tables (COLR/CPAL) for `Segoe UI Emoji`.
  - Fix monochrome rendering caused by parent text `Foreground` overriding color emoji glyphs.
  - Full support for variant selectors (VS-16 `\uFE0F`) and Zero-Width Joiner (ZWJ) sequences.

---

## Milestone 2: Rich Media & Dynamic Typography (v1.2.0)

Focus: Markdown media rendering and interactive font sizing.

- [ ] **Markdown Inline and Block Images**:
  - Support `![Alt](path_or_url)` syntax.
  - Resolve local paths relative to the markdown file folder.
  - Non-blocking asynchronous caching and decoding of remote image URLs.
  - Image preview popup on click.
- [ ] **Dynamic Font Scale & Zoom**:
  - Interactive text scaling via `Ctrl + Mouse Wheel`.
  - Dedicated scaling controls in settings/context menu.
  - Persistent zoom levels per note in `config.json`.

---

## Milestone 3: Modular Desktop Widgets (v2.0.0)

Focus: Expand standalone notes into a modular desktop productivity surface.

- [ ] **Embedded Mini-Widgets**:
  - **Pomodoro Timer**: Compact work/break countdown in header with system notifications.
  - **Mini Calendar**: Monthly view with date navigation and daily notes integration.
  - **Inline Math / Scratchpad**: Real-time expression evaluation directly inside note blocks.
  - **Interactive Checkboxes**: Clickable `- [ ]` and `- [x]` markdown tasks with direct disk file state updates.

---

## Feedback & Feature Requests

Submit issues and feature requests through [GitHub Issues](https://github.com/Lychqq/NotionDesk/issues).
