# Frontend Design Patterns

This document defines the default layout and spacing patterns for the Capitalism frontend.

It exists because the app uses a global reset in [main.css](../src/assets/styles/main.css) that removes all default margins and padding. Every page therefore needs explicit spacing and grouping, or the UI collapses into dense blocks.

The guidance below follows the same principles used by mature design systems such as Atlassian and Carbon: a consistent spacing scale, parent-owned layout gaps, generous section separation, and wider gutters for dense components like tables.

## Core Rules

1. Use the 8px spacing rhythm for all new layouts.
2. Let the parent own spacing with `gap-*`, `space-y-*`, `grid gap-*`, and section `pt/pb` utilities.
3. Reserve larger values for page structure, not component internals.
4. Never rely on semantic defaults like `<h1>` or `<p>` margins. They are reset to `0`.
5. Dense data needs wider gutters than simple text blocks.

## Spacing Scale

Use these Tailwind values by default:

| Intent                           | Pixels   | Tailwind                             |
| -------------------------------- | -------- | ------------------------------------ |
| Micro spacing inside tight UI    | 8        | `gap-2`, `px-2`, `py-2`              |
| Compact component spacing        | 12       | `gap-3`, `px-3`, `py-3`              |
| Standard component spacing       | 16       | `gap-4`, `px-4`, `py-4`              |
| Comfortable card/form spacing    | 24       | `gap-6`, `px-6`, `py-6`              |
| Section separation               | 32       | `gap-8`, `py-8`                      |
| Major page section separation    | 40 to 48 | `gap-10`, `gap-12`, `py-10`, `py-12` |
| Hero and large layout separation | 64+      | `py-16`, `py-20`, `pt-8`, `pb-20`    |

Avoid arbitrary values unless optical balancing is genuinely needed.

## Page Shell Pattern

Every page should start with a shell that creates breathing room below the sticky 64px header.

```vue
<main class="container pb-16 pt-6 lg:pb-20 lg:pt-8">
  <div class="mx-auto flex max-w-6xl flex-col gap-10 lg:gap-12">
    <!-- page sections -->
  </div>
</main>
```

Rules:

1. `pt-6 lg:pt-8` is the default page-top clearance.
2. `pb-16 lg:pb-20` is the default page-bottom clearance.
3. The inner stack owns section rhythm with `gap-10` or `gap-12`.

## Hero Pattern

Heroes and lead sections must never touch the sticky header or the next content block.

```vue
<section class="relative overflow-hidden rounded-[28px] border border-divider shadow-lg">
  <div class="relative z-20 flex min-h-[23rem] flex-col items-center justify-center gap-6 px-6 py-14 text-center sm:px-10 lg:min-h-[27rem] lg:px-16 lg:py-20">
    <!-- hero content -->
  </div>
</section>
```

Rules:

1. Treat a hero as its own block with a visible edge or padding buffer.
2. Use at least `gap-6` inside hero content.
3. Keep the CTA separated from copy with `mt-2` to `mt-4`.

## Card Pattern

Use this for standalone panels, forms, summaries, and section containers.

```vue
<section class="rounded-2xl border border-divider bg-card p-6 shadow-sm sm:p-8">
  <div class="flex flex-col gap-6">
    <!-- card content -->
  </div>
</section>
```

Rules:

1. Default card padding is `p-6`; use `sm:p-8` for primary content areas.
2. Use `rounded-2xl` for primary surfaces.
3. Use `shadow-sm` or `shadow-lg` only when it adds hierarchy, not everywhere.

## Section Header Pattern

```vue
<div class="flex flex-wrap items-center justify-between gap-4 px-6 py-5 sm:px-8 sm:py-6">
  <h2 class="text-3xl font-bold text-body">Title</h2>
  <button class="btn btn-secondary">Action</button>
</div>
```

Rules:

1. Header spacing should feel distinct from body spacing.
2. Primary section headers use `text-2xl` or `text-3xl`, not small label sizing.
3. Use `gap-4` for title/action wrap safety.

## Data Table Pattern

Dense tables must live in a table shell, not directly on the page background.

```vue
<section class="overflow-hidden rounded-2xl border border-divider bg-card shadow-sm">
  <div class="px-6 py-5 sm:px-8 sm:py-6">
    <!-- title / toolbar -->
  </div>
  <div class="overflow-x-auto border-t border-divider">
    <table class="w-full min-w-[38rem] border-collapse">
      <thead class="bg-card-raised">
        <tr>
          <th class="px-8 py-5 text-left text-xs font-semibold uppercase tracking-[0.16em] text-muted">...</th>
        </tr>
      </thead>
    </table>
  </div>
</section>
```

Rules:

1. Table headers and rows must use matching visual height.
2. First and last columns need at least 24 to 32px horizontal gutter from the card edge.
3. Table headers should not feel cramped; default to `px-8 py-5` in primary page tables.
4. Give data tables wide breathing room. Do not trap them inside tiny inner cards unless density is intentional.

## Forms and Finance Panels

1. Form fields stack with `gap-5` or `gap-6`.
2. Label-to-input spacing stays tight (`gap-1.5` to `gap-2`), but field groups stay roomier.
3. Financial panels should use the page shell + card pattern, not raw sections with scattered `mb-*` utilities.

## Anti-patterns

Avoid these:

1. A page root with no `pt-*` below the sticky header.
2. Recreating layout rhythm by sprinkling `mb-*` across children.
3. Tiny table headers in a primary dashboard table.
4. Cards touching adjacent sections without a larger parent gap.
5. Full-width hero media with no surrounding clearance unless the entire shell is intentionally edge-to-edge.

## Validation Checklist

Before finishing any page or component layout change:

1. Check that the first visible section does not touch the header.
2. Check that major sections are separated more strongly than elements inside a section.
3. Check that titles, tables, and CTA areas have obvious gutters.
4. Run `npm run lint`.
5. Run `npm run build`.
6. Run the targeted Playwright spec for the affected page.
