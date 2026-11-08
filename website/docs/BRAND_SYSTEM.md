# Revora Brand System

Revora's approved identity uses the supplied gold-and-teal `R` crest and Revora wordmark. The original files were provided under `source-assets/extracted/` and copied byte-for-byte into the production static-asset directory; the website never serves the source directory or its ZIP archive.

## Approved production mapping

- `revora-header.png` → `wwwroot/assets/revora-header-logo.png`: horizontal navigation, mobile header, dashboard sidebar, and Admin sidebar.
- `revora-logo.png` → `wwwroot/assets/revora-full-logo.png`: neutral language landing and account entry pages where a larger square mark is appropriate.
- `revora-favicon.ico` → `wwwroot/favicon.ico`: multi-size browser favicon.
- `revora.png`: inspected horizontal RGBA alternate; intentionally not shipped because the explicitly designated header export already fills that role and duplicate public assets are avoided.

The copied files preserve source hashes, transparency, dimensions, aspect ratios, and image data. CSS changes only the rendered bounding size using automatic dimensions and `object-fit: contain`; it does not crop, stretch, recolor, or transform the images.

## Visual language

- Midnight `#071013` and deep slate `#0c171a` establish the cinematic base.
- Oxidized teal `#76b8ad` communicates magic and system health.
- Muted bronze `#d4b46f` marks focus and primary action without excessive glow.
- Ink `#edf3ef` and muted copy `#a8b7b2` preserve readable contrast.
- Latin display text uses platform Georgia for a restrained editorial tone; UI copy uses Inter when locally available with Segoe UI fallbacks. Arabic uses Noto Sans Arabic when available with Tahoma/Segoe UI fallbacks. No font download blocks rendering.
- Spacing is fluid through `clamp()` and a 1280px content measure. Panels use a 1.15rem radius, one-pixel borders and restrained depth.

Buttons, forms, cards, badges, alerts, navigation, tables/empty-state foundations, loading-safe layout and dashboard components share these tokens. Focus is visible, content survives without JavaScript, forced colors are respected, and reduced-motion removes transitions.

## Guardian asset

`src/Revora.Web/wwwroot/assets/revora-guardian.png` was generated with OpenAI's built-in image generation tool on 2026-08-28. Final prompt: an entirely original poised desert-city sentinel in dark teal and weathered bronze martial-fantasy armor, holding a crescent-bladed staff before an original cliffside city at pre-dawn; cinematic digital painting, guardian on the right third with calm left-side negative space, cool teal and restrained amber lighting; no text, logos, watermark, copied costume, franchise reference, excessive glow, gore, firearm or sexualization.

The image is decorative and CSS-layered. Semantic content, contrast, layout and calls to action remain complete if it is removed. It must not be used as evidence of implemented gameplay.
