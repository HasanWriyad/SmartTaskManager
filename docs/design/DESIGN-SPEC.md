# SmartTask — UI Design Spec

Smart Task Management System · Angular 18+ with Angular Material 3 · v1.1 (15 Nov 2026)

One accent colour, neutral greys, light and dark themes, responsive from 360px up.
Screens covered: **Login**, **Dashboard**, **Task list** (with the task form dialog).
Projects and Users screens reuse the same shell, table, filter and pagination patterns.

---

## 1. Colour tokens

Define these as CSS custom properties on `:root`, and override them under `.dark-theme`.

| Token | Light | Dark | Used for |
|---|---|---|---|
| `--bg` | `#F5F6F8` | `#0D0F13` | Page background |
| `--surface` | `#FFFFFF` | `#15181E` | Cards, sidebar, header, dialog |
| `--surface-2` | `#F0F2F5` | `#1C2027` | Table header, chart track, icon tiles, avatars |
| `--border` | `#E3E6EB` | `#272C35` | 1px outlines and dividers |
| `--hover` | `#F7F8FA` | `#1A1E25` | Table row hover |
| `--text` | `#111827` | `#E7E9EE` | Primary text |
| `--text-muted` | `#5B6475` | `#9BA3B2` | Labels, meta text, icons (≥ 4.5:1 contrast) |
| `--accent` | `#5B47E0` | `#998DEC` | Primary buttons, logo, focus ring, active filter border |
| `--accent-soft` | `#EFEDFC` | `#242249` | Active nav item, tonal buttons, "my" avatar |
| `--accent-text` | `#503EC5` | `#B5ACF1` | Links, text on `--accent-soft` |
| `--on-accent` | `#FFFFFF` | `#0D0F13` | Text on accent buttons |
| `--scrim` | `rgba(16,24,40,.45)` | `rgba(0,0,0,.6)` | Dialog and drawer backdrop |

Rules:
- Use only one accent. Status and priority hues are deliberately kept away from violet.
- In dark mode the accent is lightened (mixed 38% toward white), so it keeps contrast on dark surfaces.
- Theme switching:
  - Default to `prefers-color-scheme`.
  - The header toggle overrides it, and the choice is saved in `localStorage`.
  - Apply the theme with the `.dark-theme` class on `<html>`.

```css
:root {
  --bg:#F5F6F8; --surface:#FFFFFF; --surface-2:#F0F2F5; --border:#E3E6EB; --hover:#F7F8FA;
  --text:#111827; --text-muted:#5B6475;
  --accent:#5B47E0; --accent-soft:#EFEDFC; --accent-text:#503EC5; --on-accent:#FFFFFF;
}
.dark-theme {
  --bg:#0D0F13; --surface:#15181E; --surface-2:#1C2027; --border:#272C35; --hover:#1A1E25;
  --text:#E7E9EE; --text-muted:#9BA3B2;
  --accent:#998DEC; --accent-soft:#242249; --accent-text:#B5ACF1; --on-accent:#0D0F13;
}
```

Angular Material theme: generate an M3 palette with `#5B47E0` as the primary seed
(`ng generate @angular/material:theme-color`), then map the surfaces to the neutrals above.

---

## 2. Task status

Status is stored as an enum: `ToDo`, `InProgress`, `Completed`, `Cancelled`.

| Status | Light bg / text / dot | Dark bg / text / dot | Chart colour | Shape |
|---|---|---|---|---|
| To do | `#EEF0F3` / `#475063` / `#8B93A1` | `#252A33` / `#C3C9D4` / `#8B93A1` | `#8B93A1` | Filled dot |
| In progress | `#E1ECFE` / `#1E4FC2` / `#3B7BF6` | `#16284A` / `#8DB4FF` / `#4C8DFF` | `#3B7BF6` | Filled dot |
| Completed | `#DCF3E6` / `#13663F` / `#1F9D5C` | `#11301F` / `#74D9A2` / `#2FBF71` | `#1F9D5C` | Filled dot |
| Cancelled | `#FFFFFF`, border `#D5D9E0` / `#5B6475` / ring `#8B93A1` | transparent, border `#353B47` / `#9BA3B2` / ring `#7B8494` | `#CDD2DA` (dark `#3E4552`) | Outlined badge with hollow dot |

**Overdue** is derived, not stored:
- A task is overdue when its due date is before today and its status is To do or In progress.
- Show it as red due-date text: `#B42318` (dark `#FF9A8F`), weight 600, with a "Overdue by N days" caption.
- Also count it in the Overdue summary card. Icon tile colours: bg `#FDE6E4` / dark `#3A1715`.

**Pending** means To do + In progress. Cancelled counts as neither pending nor completed.

## 3. Task priority

Priority is stored as an enum: `Low`, `Medium`, `High`, `Critical`.

| Priority | Light bg / text | Dark bg / text | Bar / chart | Icon |
|---|---|---|---|---|
| Low | `#EEF0F3` / `#475063` | `#252A33` / `#C3C9D4` | `#8B93A1` | 1 of 4 bars filled |
| Medium | `#FEF0D3` / `#8A5300` | `#36280C` / `#F5C46B` | `#E3A008` (dark `#F2B53A`) | 2 of 4 |
| High | `#FEE4D8` / `#B03A0E` | `#3D1E10` / `#FF9E73` | `#F07A2B` (dark `#FF8A57`) | 3 of 4 |
| Critical | `#FDE2E1` / `#A3170F` | `#451414` / `#FF8B85` | `#D92D20` (dark `#F04438`) | 4 of 4 |

Badge anatomy (status and priority alike):
- Size: 24px tall, 8px horizontal padding, 6px radius, 12px/500 text, 6px gap.
- Status badges have a 7px dot. Priority badges have a 15×12 icon of four signal bars; unfilled bars are at 30% opacity.
- Never rely on colour alone. The dot or bar count always carries the meaning too.
- Badges are read-only, so build them as small components (`<app-status-badge>`, `<app-priority-badge>`), not `mat-chip`.

---

## 4. Typography

Font: **Inter** at 400, 500, 600 and 700, falling back to `system-ui, sans-serif`. Load it from Google Fonts.
Use `font-variant-numeric: tabular-nums` for counts, dates and pagination.

| Style | Size / line-height | Weight | Used for |
|---|---|---|---|
| Display | 28 / 36 | 600, −0.02em | Login title, metric values |
| Page title | 20 / 28 | 600, −0.01em | Header `<h1>` |
| Dialog title | 18 / 26 | 600 | Task form |
| Card title | 15 / 22 | 600 | "Tasks by status", "Upcoming due" |
| Body | 14 / 20 | 400 (500 for emphasis) | Table cells, form inputs |
| Label | 13 / 18 | 500 | Field labels, card labels |
| Caption / badge | 12 / 16 | 500 | Meta lines, badges, table headers |
| Overline | 11 / 16 | 600, uppercase, +0.06em | Sidebar "Admin" heading |

---

## 5. Spacing, radius, elevation

**Spacing** sits on a 4px grid: 4 · 8 · 12 · 16 · 20 · 24 · 32 · 40.

| Where | Value |
|---|---|
| Page padding | 24px top, 32px sides (16px on mobile) |
| Grid gap between cards | 16px (12px on mobile) |
| Metric card padding | 16px |
| Chart and list card padding | 20px |
| Form fields | 16px apart; label to field 6px |
| Table row | at least 60px tall, 20px left padding |
| Touch targets | at least 44×44px (icon buttons 40–44px) |

**Border radius**

| Element | Radius |
|---|---|
| Badges | 6px |
| Buttons, inputs, selects, nav items, icon buttons | 8px |
| Cards, AI suggestion panel | 12px |
| Dialog, phone frames | 16px |
| Avatars | 50% |

M3's default buttons are pill-shaped. Override them to 8px with the `mat.button-overrides()` mixin, or with `--mdc-*-container-shape` / `--mat-*-container-shape` tokens.

**Elevation and states**
- Use 1px `--border` outlines instead of shadows.
- Only the dialog (`0 24px 48px -12px rgba(16,24,40,.25)`) and the mobile drawer get a shadow.
- Focus: 2px `--accent` outline.
- Hover: `--hover` background on table rows and nav items.
- Disabled: 38% opacity.
- Active filter: the field border turns `--accent`.

---

## 6. App shell

```
┌──────────────┬──────────────────────────────────────────┐
│ ▣ SmartTask  │ Page title                    ☾   [+ New] │  ← header 64px
│              ├──────────────────────────────────────────┤
│ ▦ Dashboard  │                                          │
│ ▢ Projects   │   content (padding 24/32)                │
│ ☑ Tasks      │                                          │
│ ADMIN        │                                          │
│ 👥 Users     │                                          │
│              │                                          │
│ « Collapse   │                                          │
│ ──────────── │                                          │
│ (AR) Ayesha  │                                          │
│      Admin ⇥ │                                          │
└──────────────┴──────────────────────────────────────────┘
```

**Sidebar** (`mat-sidenav`, background `--surface`, 1px right border, padding 16px 12px):
- **Top:** a 32px accent logo tile (white check icon) and the "SmartTask" wordmark at 17px/700.
- **Nav:** each item is 44px tall, with a 20px icon, a 12px gap and 8px radius.
  - Active item: `--accent-soft` background, `--accent-text` text, weight 600.
  - Inactive item: `--text-muted` text, weight 500.
- **Order:** Dashboard, Projects, Tasks, then an "ADMIN" overline, then Users.
  - Users and the overline render only when `role === 'Admin'`.
- **Bottom:**
  - A "Collapse" button, shown only on desktop.
  - A divider.
  - A user row: 36px avatar with initials, name (14/600), role (12px, muted) and a sign-out icon button.

**Breakpoints** (use CDK `BreakpointObserver`):

| Width | Sidebar | `mat-sidenav` mode |
|---|---|---|
| ≥ 1200px | Expanded, **256px** (the user can collapse it; save the choice in localStorage) | `side`, opened |
| 720–1199px | Icon rail, **72px** (labels hidden, icons centred, user row stacks) | `side`, opened |
| < 720px | Slide-out drawer, **280px**, with scrim; the header shows a menu button | `over`, closed |

**Header:** 64px tall (56px on mobile), `--surface` background, 1px bottom border.
- It holds only the menu button (mobile only), the page title, the theme toggle and the page's main action.
- There's no navigation in the header.

---

## 7. Screens

### 7.1 Login

- **Layout:** two equal columns.
  - **Left:** the form panel on `--surface`.
  - **Right:** a product panel on `--bg` with a headline and three sample task rows with status badges.
  - Below 900px the right panel is hidden.
- **Top bar:** logo and wordmark, plus the theme toggle.
- **Form:** max-width 400px, centred vertically.
  1. "Sign in" (28/600) and a subtitle in muted 14px.
  2. An error banner (`role="alert"`, overdue colours). It shows only after a failed sign-in.
  3. Email field (44px tall).
  4. Password field with a show/hide icon button; "Forgot password?" link on the label row.
  5. "Keep me signed in" checkbox.
  6. "Sign in" primary button: full width, 44px tall.
  7. "New to SmartTask? **Create an account**", linking to the registration page.
- Validation: reactive form, required + email validators, errors shown inline under fields.
- On success, store the JWT and refresh token, then go to `/dashboard`.

### 7.2 Dashboard

1. **Summary cards.** A grid of 6 columns at ≥ 1360px, 3 columns at ≥ 720px, and 2 columns below that. Each card has:
   - A label (13/500, muted).
   - A 32px icon tile.
   - A value (28/600, tabular figures).
   - A 12px footnote.

   | Card | Icon tile colour | Footnote |
   |---|---|---|
   | Total projects | neutral | "Across all teams" |
   | Total tasks | neutral | "In all projects" |
   | Completed | Completed | "N% of all tasks" |
   | Pending | In progress | "N to do · N in progress" |
   | Overdue (value in red) | Overdue | "Past due, not completed" |
   | My tasks | accent-soft | "Assigned to you" |

2. **Charts.** Two cards side by side at ≥ 960px, stacked below that.
   - **Tasks by status:**
     - A 168px doughnut, 16px thick, with the total in the centre.
     - Next to it, a legend with dot, label, count and percentage for each status.
     - A note under the legend: "N of the open tasks are overdue".
   - **Tasks by priority:**
     - A horizontal bar chart, ordered Critical → High → Medium → Low.
     - Bars are 28px tall with 6px radius, on a `--surface-2` track.
     - Each row shows its count and percentage.
     - A footnote: "N critical or high-priority tasks are still open".
   - The two charts together cover the assignment's "Completed vs Pending" requirement.
3. **Upcoming due.** Tasks due in the next 7 days that aren't completed or cancelled; show 5.
   - Columns: Task (title + project) · Assignee (avatar + name) · Due (date + "Tomorrow" / "In N days") · Priority · Status.
   - Below 960px, hide Assignee and Status. Below 720px, hide Priority as well.
   - A "View all tasks" link in the card header.

### 7.3 Task list

1. **Header:** the "Tasks" title, the theme toggle and a "+ New task" primary button (icon only on mobile).
2. **Filter bar.** One card that wraps on narrow screens. It contains:
   - Search field with a search icon (flex 2). It matches title and project, debounced by 300ms.
   - Status select: All, To do, In progress, Completed, Cancelled.
   - Priority select: All, Critical, High, Medium, Low.
   - Assignee select: All, Me, then each user.
   - Due date select: Any, Overdue, Due today, Next 7 days, Later.
   - "Clear filters" text button, shown only when a filter is active.

   An active filter gets an accent border. Keep the filter state in the URL query params.
3. **Table** (`mat-table` + `matSort`). It sits inside a card, has a minimum width of 940px and scrolls horizontally inside the card.

   | Column | Width | Sortable | Content |
   |---|---|---|---|
   | Title | 2.6fr (min 260px) | ✓ | Title (14/500, opens the edit dialog) + project (12px, muted) |
   | Status | 140px | ✓ (To do → Cancelled) | Status badge |
   | Priority | 120px | ✓ (Low → Critical) | Priority badge |
   | Assignee | 1.2fr | — | 28px avatar + name ("(you)" for the current user) |
   | Due date | 150px | ✓ (default, ascending) | "16 Nov 2026" + relative caption; overdue in red |
   | Actions | 56px | — | Edit icon button (`aria-label="Edit <title>"`) |

   - Sort indicator: an arrow icon. It's at 30% opacity when inactive, and rotates 180° when the sort is descending. Set `aria-sort` on the header cell.
   - Empty state: "No tasks match these filters", a hint line and a "Clear filters" button.
   - Below 720px, rows become cards: title, project, status badge, priority badge, initials and due date.
4. **Pagination** (`mat-paginator`, server-side):
   - Rows-per-page options 5 / 10 / 25 (default 10).
   - A "1–10 of 24" range label.
   - Previous/next icon buttons, dimmed when disabled.

### 7.4 Task form dialog

Built with `MatDialog`: 600px wide, full screen below 600px, 16px radius, reactive form.

- **Header:** "New task" or "Edit task" (18/600), plus a close icon button.
- **Fields** (labels 13/500 muted above 44px outlined fields):
  - Title (required).
  - Description: textarea, 4 rows, vertical resize.
  - A 2-column grid with Project, Assignee, Status, Priority and Due date (datepicker).
- **Improve with AI** is a tonal button on the Description label row: sparkle icon, `--accent-soft` background, `--accent-text` text, 32px tall.
  - On click it calls `POST /api/ai/improve-description` with `{ title, description }`.
    - While loading, the button reads "Improving…" and is disabled, and a skeleton panel shows.
  - The suggestion panel has a 12px radius, a `--accent` 4% tint background and a 25% tint border. It contains:
    - The header "Improved description", with "Review before applying" on the right.
    - The improved description (pre-line text): a one-line summary, then **Steps** and **Done when** bullet lists.
    - "What changed" tags: Grammar fixed · Clearer wording · Expanded · Actionable steps.
    - **Apply suggestion** (primary) and **Discard** (outlined) buttons.
  - The suggestion never overwrites the field without asking. After Apply, a green confirmation line shows: "Description updated. You can still edit it before saving."
  - If the description is empty, the AI writes one from the title.
- **Footer:** 1px top border. "Cancel" (text button) and "Create task" or "Save changes" (primary).

---

## 8. Roles and visibility

| Role | Navigation | Actions (hide in the UI and enforce in the API) |
|---|---|---|
| Admin | Dashboard, Projects, Tasks, Users | Everything, including user management |
| Project Manager | Dashboard, Projects, Tasks | Create/update/delete projects and tasks, assign tasks |
| Team Member | Dashboard, Projects, Tasks | View projects, update the status of their own tasks |

Enforce these with route guards (`canMatch`) for Users and an `*appHasRole` structural directive for buttons.
Always back them with API authorization.

---

## 9. Angular Material mapping

| UI part | Component |
|---|---|
| Shell | `mat-sidenav-container` / `mat-sidenav` (mode from `BreakpointObserver`), `mat-nav-list` + `routerLinkActive` |
| Summary cards | `mat-card appearance="outlined"` in a CSS grid |
| Charts | `ng2-charts` (Chart.js): doughnut for status, horizontal bar for priority; colours from the tokens |
| Upcoming due | Compact `mat-table` or `mat-list` |
| Search and filters | `mat-form-field appearance="outline" subscriptSizing="dynamic"`, `mat-select`, `mat-datepicker` |
| Task table | `mat-table` + `matSort` + `mat-paginator` (server-side) |
| Badges | Custom `app-status-badge`, `app-priority-badge` |
| Task form | `MatDialog` + reactive form |
| Buttons | `mat-flat-button` (primary), `mat-button` (text), `mat-stroked-button` (secondary), `mat-icon-button` |
| Icons | Material Symbols Rounded, weight 400, 20px (18px inside cards) |
| Feedback | `MatSnackBar` for save/delete confirmations; inline `role="alert"` for form errors |

---

## 10. Accessibility checklist

- Text contrast is at least 4.5:1, including muted 12px labels. All tokens above pass on their surfaces.
- Status and priority meaning is never colour-only: there's always a dot, ring or bar count plus a text label.
- Every icon-only button has an `aria-label`. Charts have `role="img"` and an `aria-label` that states the numbers.
- The dialog uses `aria-modal` and `aria-labelledby`, and the AI panel uses `aria-live="polite"`.
- Focus is visible everywhere (2px accent outline), and the dialog traps focus (MatDialog does this by default).
- Touch targets are at least 44px. The layout works at 360px wide with no horizontal page scroll; only the table scrolls inside its card.
