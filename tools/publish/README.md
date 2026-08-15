# AbilityKit Package Publishing

AbilityKit is a single git repo (monorepo) holding 80+ Unity packages under
`Unity/Packages/com.abilitykit.*`. During development every package is an
**embedded package** — Unity picks it up straight from disk, so iteration stays
fast and no registry round-trip is needed. Publishing is a *separate*, opt-in
output for external consumers.

Stable framework packages are released to **OpenUPM** (`https://package.openupm.com`)
as one version cohort. Consumers add a scoped registry and install by name;
transitive dependencies resolve automatically — something plain git-URL
references cannot do at this scale.

## What gets released

| Category | Prefix | Released? | Why |
|---|---|---|---|
| Framework | `com.abilitykit.*` (not below) | **Yes** | The product. |
| Third-party | `com.abilitykit.thirdparty.*` | No | Vendored upstream code (entitas, svelto, rvo2, luban, …) — licensing. |
| Demos | `com.abilitykit.demo.*` | No | Samples to read, not dependencies to install. |

## Tools

| Tool | Purpose |
|---|---|
| `align-versions.js` | Rewrites every framework package's declared version and internal references onto one cohort version; strips BOM. Run before each release. |
| `audit-versions.js` | Verifies consistency (0 mismatches, 0 BOM, all framework on cohort). Exits non-zero on failure — wire into CI. |
| `release.js` | Tags the packages in `release-manifest.json` for OpenUPM. Dry-run by default. |
| `release-manifest.json` | The allow-list of what ships, in batches. Edit this to add packages. |

## Versioning

All released framework packages share **one cohort version** (currently
`0.1.0`). They move together: bump the cohort, re-run `align-versions.js`, ship.
This keeps the dependency graph trivially consistent while the surface is still
settling. Independent per-package semver can come later once packages mature at
different rates.

## One-time setup (do this once, manually)

1. **Push the repo to a public GitHub remote** if not already. OpenUPM only
   indexes public GitHub repos.
2. **Submit the repo to OpenUPM** at <https://openupm.com/packages/submit/>.
   For each package you intend to release, set its `gitTagPrefix` to
   `<package-name>/` (e.g. `com.abilitykit.core/`). OpenUPM auto-detects
   packages in `Packages/com.abilitykit.*` subdirectories — no path config
   needed.
3. **Add the `com.abilitykit` scope** to the consumer's OpenUPM scoped registry
   (see *Consumer install* below). The dev repo's `Unity/Packages/manifest.json`
   already declares OpenUPM; just extend its `scopes` with `com.abilitykit`.

## Release flow

```bash
# 1. make sure everything is consistent and on the cohort version
node tools/publish/audit-versions.js
# (if you bumped the cohort) node tools/publish/align-versions.js --apply -V 0.2.0

# 2. set the batch status to "ready" in release-manifest.json, then preview
node tools/publish/release.js                  # dry-run: lists tags it would create

# 3. create the tags locally
node tools/publish/release.js --tag

# 4. push the tags to trigger OpenUPM builds (irreversible — public)
git push origin --tags
```

Tags use the format `<package-name>/<version>` (e.g. `com.abilitykit.core/0.1.0`),
which matches the `gitTagPrefix` configured on OpenUPM. Once pushed, OpenUPM's
pipeline builds and publishes each package; the cohort version then becomes
installable.

## Adding packages to a release

1. Make sure the package is a framework package (not thirdparty/demo) and its
   internal AbilityKit dependencies are already released (earlier batch) or in
   the same batch. `release.js` checks this and refuses otherwise.
2. Add its name to a batch in `release-manifest.json`, or start a new batch.
3. `release.js --dry-run` to confirm the dependency closure is satisfiable.

Rule of thumb for batching: **leaves first, then the layers that depend only on
leaves, then upward**. Never ship a package before its dependencies.

## Consumer install

In the consuming project's `Packages/manifest.json`:

```json
{
  "scopedRegistries": [
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": ["com.abilitykit"]
    }
  ],
  "dependencies": {
    "com.abilitykit.core": "0.1.0"
  }
}
```

Transitive AbilityKit dependencies resolve automatically from the same registry.

## Why not plain git-URL references?

UPM's git-URL references do **not** resolve transitive dependencies. A consumer
installing `com.abilitykit.ability` would have to hand-write git URLs for every
package in its dependency chain (8+ direct, more transitively) and keep their
versions aligned by hand. At 60+ framework packages that is unmaintainable. A
scoped registry resolves transitive deps by version, which is why OpenUPM is the
release channel.
