# Dashboard Premium Redesign - Design Specification

## Overview
Complete redesign of the dashboard screen with an enterprise, premium, and creative aesthetic. This design focuses on modern UI patterns, sophisticated visual hierarchy, and a polished user experience.

## Design Philosophy

### Key Principles
1. **Enterprise Professionalism**: Clean, authoritative, trustworthy appearance
2. **Premium Feel**: Sophisticated details, subtle animations, high-quality interactions
3. **Creative Expression**: Dynamic elements, engaging visual patterns, personality
4. **Data-First**: Clear information hierarchy, scannable at a glance
5. **Responsive**: Seamless experience across all device sizes

## Color Palette

### Primary Colors
- **Primary Deep**: `#0F172A` (Dark slate - backgrounds, headers)
- **Primary Accent**: `#3B82F6` (Vibrant blue - CTAs, highlights)
- **Primary Light**: `#F1F5F9` (Light slate - secondary backgrounds)

### Semantic Colors
- **Success**: `#10B981` (Emerald green)
- **Warning**: `#F59E0B` (Amber)
- **Danger**: `#EF4444` (Red)
- **Info**: `#06B6D4` (Cyan)

### Surface Colors
- **Surface 0**: `#FFFFFF` (Pure white - cards, content)
- **Surface 1**: `#F8FAFC` (Very light gray - page background)
- **Surface 2**: `#F1F5F9` (Light gray - nested elements)
- **Surface 3**: `#E2E8F0` (Medium gray - borders, dividers)

### Text Colors
- **Text Primary**: `#0F172A` (Dark slate - headings, important text)
- **Text Secondary**: `#475569` (Slate gray - body text)
- **Text Tertiary**: `#94A3B8` (Light slate - labels, metadata)
- **Text Muted**: `#CBD5E1` (Very light slate - placeholders)

## Typography

### Font Family
- **Primary**: Inter or system-ui (clean, modern sans-serif)
- **Headings**: Inter with tighter letter-spacing
- **Numbers**: Tabular nums for data alignment

### Scale
- **H1**: 32px / 40px (1.25) - Page titles
- **H2**: 24px / 32px (1.33) - Section headers
- **H3**: 18px / 24px (1.33) - Card titles
- **Body**: 14px / 20px (1.43) - Main content
- **Small**: 12px / 16px (1.33) - Labels, metadata
- **Tiny**: 11px / 14px (1.27) - Timestamps, footnotes

### Weights
- **Regular**: 400
- **Medium**: 500
- **Semibold**: 600
- **Bold**: 700

## Layout Strategy

### Grid System
- **12-column grid** for desktop
- **6-column grid** for tablet
- **1-column grid** for mobile
- **24px gutters** between widgets
- **16px padding** inside cards

### Widget Sizes
- **Small (KPI)**: 2x2 (16% width)
- **Medium (Chart)**: 3x3 (25% width)
- **Large (Complex)**: 4x4 (33% width)
- **Full Width**: 12x2 (100% width)

### Spacing
- **Page Padding**: 32px (desktop), 24px (tablet), 16px (mobile)
- **Widget Gap**: 24px
- **Card Padding**: 20px
- **Element Gap**: 12px

## Widget Design Guidelines

### Widget Shell
- **Border Radius**: 12px (consistent modern feel)
- **Border**: 1px solid #E2E8F0 (subtle)
- **Shadow**: 0 1px 3px rgba(0,0,0,0.05), 0 1px 2px rgba(0,0,0,0.1)
- **Background**: White (#FFFFFF)
- **Hover Shadow**: 0 4px 6px rgba(0,0,0,0.07), 0 2px 4px rgba(0,0,0,0.06)

### Widget Header
- **Height**: 48px
- **Padding**: 16px horizontal
- **Background**: Transparent
- **Border Bottom**: 1px solid #F1F5F9
- **Title Font**: 13px, Semibold, Uppercase, Tracking-wider
- **Icon Size**: 16px

### Widget Content
- **Padding**: 16px
- **Min Height**: 120px
- **Loading State**: Skeleton with subtle shimmer
- **Error State**: Red border, icon, message

### Widget Actions
- **Action Buttons**: 32px square, icon-only
- **Hover**: Light gray background (#F1F5F9)
- **Active**: Medium gray background (#E2E8F0)
- **Icon Size**: 14px

## Component Specifications

### KPI Cards
- **Number Display**: 36px, Bold, Primary color
- **Label**: 12px, Medium, Text secondary
- **Trend Indicator**: 14px icon + percentage
- **Sparkline**: Mini chart (optional)
- **Color Coding**: Success (green), Warning (amber), Danger (red)

### Chart Widgets
- **Chart Height**: 100% of content area
- **Grid Lines**: Light gray (#E2E8F0), dashed
- **Axis Labels**: 11px, Text tertiary
- **Legend**: 12px, Pills with color dots
- **Tooltip**: Dark background, white text, 10px padding

### List Widgets
- **Row Height**: 44px
- **Border Bottom**: 1px solid #F1F5F9
- **Hover**: Light gray background (#F8FAFC)
- **Avatar**: 32px circle
- **Progress Bar**: 4px height, rounded
- **Badge**: 20px height, 8px padding

### Progress Indicators
- **Height**: 8px
- **Border Radius**: 4px
- **Background**: #E2E8F0
- **Fill**: Primary color with gradient
- **Animation**: Smooth fill on load

## Special Effects

### Micro-interactions
- **Button Hover**: Subtle scale (1.02), shadow increase
- **Card Hover**: Lift effect (translateY -2px)
- **Drag Handle**: Cursor change, opacity 0.8
- **Loading**: Skeleton shimmer animation
- **Success**: Green flash on data update

### Gradients
- **Primary Gradient**: Linear from #3B82F6 to #2563EB
- **Success Gradient**: Linear from #10B981 to #059669
- **Warning Gradient**: Linear from #F59E0B to #D97706
- **Danger Gradient**: Linear from #EF4444 to #DC2626

### Glassmorphism (Premium Touch)
- **Backdrop Filter**: blur(10px)
- **Background**: rgba(255,255,255,0.8)
- **Border**: 1px solid rgba(255,255,255,0.2)
- **Use Case**: Overlay panels, tooltips

## Implementation Steps

### Phase 1: Foundation
1. Create new design tokens file (colors, spacing, typography)
2. Update Card component with new styling
3. Update WidgetShell with premium design
4. Create new button variants

### Phase 2: Widget Redesign
1. Redesign KPI widgets (active-projects, projects-at-risk, total-budget, etc.)
2. Redesign chart widgets (projects-by-health, budget-vs-actual, etc.)
3. Redesign list widgets (upcoming-meetings, overdue-tasks, etc.)
4. Add premium visual effects (gradients, shadows, animations)

### Phase 3: Layout & Navigation
1. Update DashboardHeader with new design
2. Improve responsive breakpoints
3. Add smooth transitions for view switching
4. Enhance drag-and-drop experience

### Phase 4: Polish
1. Add loading states with skeleton UI
2. Add error states with helpful messages
3. Add subtle animations for data updates
4. Test across all device sizes

## Files to Modify

### Core Components
- `apps/web/src/components/ui/card.tsx` - Enhanced card styling
- `apps/web/src/components/dashboard-builder/WidgetShell.tsx` - Premium widget shell
- `apps/web/src/components/dashboard-builder/DashboardBuilder.tsx` - Layout improvements

### Widget Components
- `apps/web/src/components/dashboard-builder/widgets/kpi-widgets.tsx` - KPI redesign
- `apps/web/src/components/dashboard-builder/widgets/chart-widgets.tsx` - Chart redesign
- `apps/web/src/components/dashboard-builder/widgets/list-widgets.tsx` - List redesign

### Supporting Files
- `apps/web/src/components/dashboard-builder/DashboardHeader.tsx` - Header redesign
- `apps/web/src/components/dashboard-builder/DashboardGrid.tsx` - Grid improvements
- New: `apps/web/src/components/dashboard-builder/design-tokens.ts` - Design system

## Design Tokens

```typescript
// colors
export const colors = {
  primary: {
    50: '#EFF6FF',
    100: '#DBEAFE',
    200: '#BFDBFE',
    300: '#93C5FD',
    400: '#60A5FA',
    500: '#3B82F6',
    600: '#2563EB',
    700: '#1D4ED8',
    800: '#1E40AF',
    900: '#1E3A8A',
  },
  // ... other color scales
};

// spacing
export const spacing = {
  1: '4px',
  2: '8px',
  3: '12px',
  4: '16px',
  5: '20px',
  6: '24px',
  8: '32px',
  12: '48px',
};

// border radius
export const borderRadius = {
  sm: '4px',
  md: '8px',
  lg: '12px',
  xl: '16px',
  full: '9999px',
};

// shadows
export const shadows = {
  sm: '0 1px 2px rgba(0,0,0,0.05)',
  md: '0 4px 6px rgba(0,0,0,0.07)',
  lg: '0 10px 15px rgba(0,0,0,0.1)',
  xl: '0 20px 25px rgba(0,0,0,0.15)',
};
```

## Notes
- Keep existing functionality intact during redesign
- Test with RTL (right-to-left) languages
- Ensure accessibility (WCAG AA compliant)
- Maintain performance (no heavy animations)
- Use CSS custom properties for easy theming
