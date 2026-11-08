# Website: plain HTML, CSS and JavaScript

The primary public website is **[`website/site/`](../website/site/)**. It is a
buildless static site. It does not need ASP.NET, .NET, npm packages, a bundler,
or a database to display its pages.

Live preview: <https://sheekovic.github.io/Revora-OpenSource/>

## Run and edit

Open `website/site/index.html` directly, double-click `START-REVORA.cmd`, or run:

```powershell
node scripts/serve-site.mjs --port 5080
```

The local server uses only Node's standard library and binds to `127.0.0.1`.
It serves only the static site directory. It does not expose source files,
`.local/`, `.git/`, credentials, or database configuration.

| File | What to edit |
| --- | --- |
| `website/site/index.html` | Page structure and English fallback content |
| `website/site/styles.css` | Layout, colors, typography and responsive styles |
| `website/site/app.js` | Arabic/English translations, tabs, dialogs and navigation |
| `website/site/config.js` | Public repository and optional HTTPS account-page links |
| `website/site/assets/` | Existing Revora branding and artwork |

All local asset paths are relative, so the site works under a GitHub Pages
project path. Language selection is the only browser preference stored by the
site. No account passwords, authentication tokens, analytics, or fake player
counts are implemented in this public preview.

## GitHub Pages

The [Pages workflow](../.github/workflows/pages.yml) tests and deploys only
`website/site/`. It never uploads the repository root or `.local/`.
Push changes to `main` to publish them. For a fork, enable **Settings → Pages →
GitHub Actions**, update the public links/preview metadata to your repository,
and run the Pages workflow.

GitHub Pages hosts static website files; game processes, PostgreSQL and account
APIs need a separate operator-controlled host. See
[CONNECT_COMPONENTS.md](CONNECT_COMPONENTS.md).

## Accounts and the preserved ASP.NET project

`config.js` defaults `accountUrl` to `null`. Clicking Account explains that no
account service is connected. After validating your own account backend, set
this public value to its HTTPS account-page URL. No database credentials or
API secrets belong in `config.js` or any browser file.

The old ASP.NET project and shared libraries remain under `website/src/` as
an **optional legacy backend/reference**, with their history documented. They
are not the primary frontend and are not needed for GitHub Pages or the local
static preview. To build them, use `scripts/Build.ps1 -Component Website` with
.NET 10, then `scripts/Test.ps1 -Component Website`.

Its registration client now requires an explicitly configured
`RevoraWeb:GameIdentityRegistrationUrl`. There is no default submission to the
original Revora service, and credential-bearing registration redirects are
disabled. Registration remains disabled in the public static site.

## Verify

```powershell
node --test tests/site-server.test.mjs
node --check website/site/app.js
```

The tests check assets, static configuration, method restrictions, private-path
isolation and JavaScript syntax. Browser interaction/visual validation is a
separate check; HTTP success alone is not visual or account-flow qualification.
