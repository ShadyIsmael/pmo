# UI Design System

---

## Color Palette

### Brand Colors
| Token | Value | Usage |
|-------|-------|-------|
| `--primary` | HSL(221, 83%, 53%) | Primary actions, links, active states |
| `--primary-foreground` | HSL(210, 40%, 98%) | Text on primary backgrounds |
| `--secondary` | HSL(210, 40%, 96%) | Secondary actions, subtle backgrounds |
| `--secondary-foreground` | HSL(222, 47%, 11%) | Text on secondary backgrounds |

### Navy (Sidebar & Navigation)
| Token | Value |
|-------|-------|
| `navy-900` | `#0f172a` (sidebar background) |
| `navy-800` | `#1e293b` |
| `navy-700` | `#334155` (sidebar hover, borders) |
| `navy-600` | `#475569` |
| `navy-500` | `#64748b` |
| `navy-400` | `#94a3b8` |
| `navy-300` | `#cbd5e1` (sidebar text) |
| `navy-200` | `#e2e8f0` |
| `navy-100` | `#f1f5f9` |
| `navy-50` | `#f8fafc` |

### Semantic Status Colors
| Token | Value | Usage |
|-------|-------|-------|
| `status-green` | `#22c55e` | On track, healthy, completed |
| `status-amber` | `#f59e0b` | Warning, at risk, delayed |
| `status-red` | `#ef4444` | Critical, blocked, over budget |
| `status-green-light` | `#dcfce7` | Background for green badges |
| `status-amber-light` | `#fef3c7` | Background for amber badges |
| `status-red-light` | `#fce4ec` | Background for red badges |

### Semantic Text and Action Hierarchy
- Semantic status wording uses text color only (`text-success`, `text-warning`, `text-destructive`, or `text-info`) on a transparent background. Do not add a tinted semantic fill behind status text; neutral text remains `text-muted-foreground`.
- Financial detail amounts use `text-success` when positive and `text-destructive` when negative; zero and unavailable values stay neutral. Aggregated totals use `text-financial-total` (dark blue in light mode, accessible blue in dark mode). Do not add semantic background fills to financial text.
- Primary page and form actions use the default Button variant and default size (40px height). Table-row, navigation, pagination, and retry actions use the outline variant at the small size (36px height).
- Destructive actions use an outline treatment with destructive text/border, not a destructive-filled button. Keep secondary/cancel actions outline and visually subordinate to the primary action.

### Base Surface Colors
| Token | Light | Dark |
|-------|-------|------|
| `--background` | White (`#ffffff`) | Navy-900 |
| `--foreground` | Navy-900 | Navy-100 |
| `--card` | White | Navy-800 |
| `--card-foreground` | Navy-900 | Navy-100 |
| `--popover` | White | Navy-800 |
| `--popover-foreground` | Navy-900 | Navy-100 |
| `--muted` | Navy-100 | Navy-800 |
| `--muted-foreground` | Navy-500 | Navy-400 |
| `--accent` | Navy-100 | Navy-800 |
| `--accent-foreground` | Navy-900 | Navy-100 |
| `--border` | Navy-200 | Navy-700 |
| `--input` | Navy-200 | Navy-700 |
| `--ring` | Primary | Primary |

---

## Typography

### Font Family
```css
--font-inter: 'Inter', system-ui, sans-serif;
```

### Type Scale
| Level | Size | Weight | Line Height |
|-------|------|--------|-------------|
| **h1** | 2.25rem (36px) | 700 (bold) | 2.5rem |
| **h2** | 1.875rem (30px) | 600 (semibold) | 2.25rem |
| **h3** | 1.5rem (24px) | 600 (semibold) | 2rem |
| **h4** | 1.25rem (20px) | 600 (semibold) | 1.75rem |
| **body-large** | 1rem (16px) | 400 (regular) | 1.5rem |
| **body** | 0.875rem (14px) | 400 (regular) | 1.25rem |
| **body-small** | 0.75rem (12px) | 400 (regular) | 1rem |
| **caption** | 0.6875rem (11px) | 500 (medium) | 1rem |
| **label** | 0.875rem (14px) | 500 (medium) | 1rem |

### Font Weights
- **Regular:** 400 -- body text
- **Medium:** 500 -- labels, buttons, table headers
- **Semibold:** 600 -- subheadings, active nav items
- **Bold:** 700 -- headings, emphasis

---

## Spacing Scale

Base unit: **4px**

| Token | Value |
|-------|-------|
| `space-1` | 4px |
| `space-2` | 8px |
| `space-3` | 12px |
| `space-4` | 16px |
| `space-5` | 20px |
| `space-6` | 24px |
| `space-8` | 32px |
| `space-10` | 40px |
| `space-12` | 48px |
| `space-16` | 64px |
| `space-20` | 80px |

---

## Component Library

NEXUS PMO uses **shadcn/ui** -- a collection of re-usable components built on Radix UI primitives and styled with Tailwind CSS.

### Available Components

| Component | Package | Usage |
|-----------|---------|-------|
| **Button** | `@radix-ui/react-slot` | Actions, CTAs, toggles |
| **Input** | -- | Text entry |
| **Textarea** | -- | Multi-line text |
| **Select** | `@radix-ui/react-select` | Dropdown selection |
| **Checkbox** | `@radix-ui/react-checkbox` | Boolean selection |
| **Switch** | `@radix-ui/react-switch` | Toggle settings |
| **Dialog** | `@radix-ui/react-dialog` | Modal dialogs |
| **Popover** | `@radix-ui/react-popover` | Floating panels |
| **Dropdown Menu** | `@radix-ui/react-dropdown-menu` | Context menus |
| **Tabs** | `@radix-ui/react-tabs` | Tabbed navigation |
| **Card** | -- | Content containers |
| **Badge** | -- | Status labels, tags |
| **Avatar** | `@radix-ui/react-avatar` | User avatars |
| **Tooltip** | `@radix-ui/react-tooltip` | Hover hints |
| **Progress** | `@radix-ui/react-progress` | Loading bars |
| **Scroll Area** | `@radix-ui/react-scroll-area` | Custom scrollbars |
| **Separator** | `@radix-ui/react-separator` | Visual dividers |
| **Sheet** | `@radix-ui/react-dialog` | Slide-over panels |
| **Command** | `cmdk` | Command palette, search |
| **Collapsible** | `@radix-ui/react-collapsible` | Expandable sections |
| **Skeleton** | -- | Loading placeholders |
| **Toast** | `@radix-ui/react-toast` | Notifications |
| **Table** | -- | Data tables |
| **Form** | `react-hook-form` | Form validation |
| **Label** | `@radix-ui/react-label` | Form labels |

### Confirmation dialogs

- Use `useConfirm` from `@components/ui/ConfirmProvider` for confirmations requested by event handlers; it returns a promise that resolves to `true` only after confirmation and `false` when dismissed.
- Keep operation-specific, localized context in the caller's description, and pass `destructive: true` only for destructive actions. The shared provider owns the title, button labels, keyboard behavior, focus handling, and dialog presentation.
- For a confirmation that must run an asynchronous action while the dialog remains open, use the shared `ConfirmDialog` component and keep the action's existing loading/error handling.
- Do not use `window.confirm` in the React application.

### Rich-text editors in height-constrained forms

- Rich-text editors may opt into a fill-available mode when their parent is a height-constrained Side Sheet or workspace. Keep the toolbar fixed, let the editor occupy the remaining vertical space, and scroll long text inside the editing area. The mode is opt-in so standard rich-text fields retain their natural height and existing behavior.

---

## Layout Patterns

### Shell Layout
```
┌────────────────────────────────────────────────────┐
│ ┌──────────┐  ┌──────────────────────────────────┐ │
│ │          │  │  Header (breadcrumbs, search,     │ │
│ │ Sidebar  │  │  user menu, theme toggle, locale) │ │
│ │ (collaps │  ├──────────────────────────────────┤ │
│ │ ible:    │  │                                   │ │
│ │ 64/256px)│  │  Main Content Area                │ │
│ │          │  │  (responsive, max-width 1400px)   │ │
│ │          │  │                                   │ │
│ │          │  │                                   │ │
│ └──────────┘  └──────────────────────────────────┘ │
└────────────────────────────────────────────────────┘
```

### Page Layout
- **Page Header** -- Title, subtitle, action buttons
- **Content Area** -- Cards, tables, forms, or custom layouts
- **Filters Bar** -- Search, dropdowns, date range pickers
- **Data Table** -- Sortable columns, pagination, row actions

---

## Responsive Behavior

| Breakpoint | Width | Behavior |
|------------|-------|----------|
| **Mobile** | < 768px | Sidebar auto-collapsed, stacked layouts, full-width cards |
| **Tablet** | 768-1024px | Sidebar collapsible, 2-column grids |
| **Desktop** | 1024-1400px | Full sidebar, multi-column layouts |
| **Wide** | > 1400px | Max-width container centered, side panels |

---

## Accessibility Standards

- All components built on **Radix UI primitives** which are WAI-ARIA compliant
- **Keyboard navigation** supported for all interactive elements
- **Focus indicators** visible on all focusable elements
- **Color contrast** meets WCAG 2.1 AA standards (4.5:1 for text, 3:1 for large text)
- **Screen reader** support via ARIA labels, roles, and live regions
- **Reduced motion** respected via `prefers-reduced-motion` media query
- **Form validation** with clear error messages associated to inputs via `aria-describedby`

### Floating Menus and Popovers

- Dropdowns, searchable selects, and other floating panels must render in a portal outside clipped page, table, dialog, and side-sheet containers.
- Custom pickers and context menus follow the same portal and viewport-boundary rules as shared dropdown components.
- Anchor each panel to its trigger with viewport-aware positioning; flip above when there is insufficient room below and clamp horizontally/vertically to the viewport.
- Preserve logical start/end alignment in both LTR and RTL, and keep long menus scrollable within the available viewport space.
- Outside interaction and Escape must dismiss the panel without treating interaction inside the portaled panel as an outside click.

---

## RTL Support

NEXUS PMO supports Arabic (ar) alongside English (en):

- **Layout flip** -- sidebar, margins, padding all reverse via Tailwind `rtl:` modifier
- **Text alignment** -- auto-adjusted based on locale
- **Icons** -- directional icons (arrows, chevrons) mirrored in RTL
- **Forms** -- input direction auto-detected
- **Date/Time** -- formatted according to locale conventions
- **Number formatting** -- locale-aware number, currency, and percentage formatting

Implementation:
- `next-intl` handles message resolution and locale detection
- Tailwind config uses `rtl:` variants for layout adjustments
- `html` tag `dir` attribute set dynamically based on locale

---

## Dark Mode Foundation

Dark mode is implemented via the `next-themes` library with CSS custom properties:

- **System detection** -- defaults to system preference
- **Manual override** -- user can toggle via header button
- **No flicker** -- inline script prevents flash of wrong theme
- **All components** support both themes via Tailwind's `dark:` variant
- **Persistent** -- preference stored in localStorage
