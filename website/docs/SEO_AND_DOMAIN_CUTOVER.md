# SEO and Domain Cutover

Canonical production origin is `https://playrevora.com`. Localized public URLs use `/ar/` and `/en/`; `/` is an `x-default` language choice with crawlable links and no forced geo/language redirect. Each public page has a self-canonical, reciprocal Arabic/English hreflang and x-default, localized title/description, Open Graph metadata and one meaningful H1.

The indexing gate was opened on 2026-08-28 after public pages, HTTPS hostnames, canonical/hreflang/sitemap output, honest content, and representative browser layouts passed validation. Legitimate public pages are indexable. Login, registration, verification/recovery, dashboard, Admin, maintenance and diagnostic/error pages remain noindex permanently and are excluded from the sitemap.

`www.playrevora.com` permanently redirects to the corresponding canonical apex URI. The active certificate covers apex, www, and panel through 2026-11-26. `panel.playrevora.com` continues to proxy only AMP; the public website never proxies to AMP. `srv1936586.hstgr.cloud` remains available.

The generated sitemap lists only eligible public localized routes. `robots.txt` reflects the indexing gate. News exposes a localized feed. Structured data contains truthful `WebSite`, `Organization`, breadcrumb and game-project facts only—no unsupported ratings, player counts, launch date or availability.
