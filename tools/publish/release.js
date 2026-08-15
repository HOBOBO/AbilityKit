#!/usr/bin/env node
// release.js — tag framework packages from release-manifest.json for OpenUPM.
//
// OpenUPM publishes by git tag: a tag `<name>/<version>` on this repo triggers
// OpenUPM to build and index that package version. This script plans and
// creates those tags from the manifest's "ready" batches. It never pushes —
// pushing tags is a separate, deliberate, public step (see README).
//
// Validation gates every release:
//   - package must be a framework package (not thirdparty/demo)
//   - declared version must equal the cohort version
//   - package.json must be BOM-free
//   - every internal dependency must already be released (tagged) or in the
//     same batch; a dependency on a never-released package (thirdparty/demo)
//     is rejected because it would break consumers
//
// Usage:
//   node tools/publish/release.js                         # dry-run plan + validation
//   node tools/publish/release.js --tag                   # create local tags (ready batches)
//   node tools/publish/release.js --batch batch-1-leaves  # restrict to one batch
//   node tools/publish/release.js --include-candidate     # also plan candidate batches

const fs = require("fs");
const path = require("path");
const { execSync } = require("child_process");

const ROOT = path.join(__dirname, "..", "..");
const PKGS = path.join(ROOT, "Unity", "Packages");
const MANIFEST = path.join(__dirname, "release-manifest.json");

function isNeverReleased(name, prefixes) {
  return prefixes.some(p => name.startsWith(p));
}
function readPkg(p) {
  let t = fs.readFileSync(p, "utf8");
  const bom = t.charCodeAt(0) === 0xfeff;
  if (bom) t = t.slice(1);
  return { json: JSON.parse(t), bom };
}
function git(args) {
  try {
    return execSync(`git ${args}`, { cwd: ROOT, encoding: "utf8", stdio: ["pipe", "pipe", "ignore"] });
  } catch {
    return "";
  }
}
function existingTags() {
  return new Set(git("tag").split("\n").map(s => s.trim()).filter(Boolean));
}
function worktreeDirtyFiles() {
  return git("status --porcelain -- Unity/Packages/*/package.json")
    .split("\n").map(s => s.trim()).filter(Boolean);
}

const args = process.argv.slice(2);
const doTag = args.includes("--tag");
const batchIdx = args.indexOf("--batch");
const batchFilter = batchIdx >= 0 ? args[batchIdx + 1] : null;
const includeCandidate = args.includes("--include-candidate");

const manifest = JSON.parse(fs.readFileSync(MANIFEST, "utf8"));
const cohort = manifest.cohortVersion;
const tagFmt = manifest.gitTagFormat || "{name}/{version}";
const tagOf = (name, ver) => tagFmt.replace("{name}", name).replace("{version}", ver);

// Load every package once.
const pkgs = {}; // name -> { dir, ver, deps, bom }
for (const d of fs.readdirSync(PKGS)) {
  if (!d.startsWith("com.abilitykit.")) continue;
  const fp = path.join(PKGS, d, "package.json");
  if (!fs.existsSync(fp)) continue;
  const { json, bom } = readPkg(fp);
  pkgs[json.name] = { dir: d, ver: json.version, deps: json.dependencies || {}, bom };
}

const tags = existingTags();

// Resolve which batches/packages to plan, and which are cleared to tag.
const batches = manifest.batches.filter(b => !batchFilter || b.id === batchFilter);
const ready = new Set();
const planning = new Set();
for (const b of batches) {
  for (const p of b.packages) {
    planning.add(p);
    if (b.status === "ready") ready.add(p);
  }
}

console.log(`\n=== release.js ${doTag ? "TAG" : "DRY-RUN"} — cohort ${cohort} ===`);

const errors = [];
const plan = [];

for (const name of [...planning].sort()) {
  const pkg = pkgs[name];
  const willTag = ready.has(name);
  const e = [];

  if (!pkg) { errors.push(`${name}: not found under Unity/Packages`); continue; }
  if (isNeverReleased(name, manifest.neverReleased.prefixes))
    e.push("never-released package (thirdparty/demo)");
  if (pkg.ver !== cohort) e.push(`version ${pkg.ver} != cohort ${cohort}`);
  if (pkg.bom) e.push("package.json has BOM (run align-versions.js --apply)");

  for (const dep in pkg.deps) {
    if (!dep.startsWith("com.abilitykit.")) continue;
    if (isNeverReleased(dep, manifest.neverReleased.prefixes)) {
      e.push(`depends on ${dep} (never released) -> would break consumers`);
      continue;
    }
    const depPkg = pkgs[dep];
    if (!depPkg) { e.push(`depends on unknown ${dep}`); continue; }
    const needVer = pkg.deps[dep];
    const depReleased = tags.has(tagOf(dep, needVer)) || planning.has(dep);
    if (!depReleased)
      e.push(`depends on ${dep}@${needVer} not released (no tag ${tagOf(dep, needVer)})`);
  }

  const tag = tagOf(name, pkg.ver || cohort);
  const exists = tags.has(tag);

  if (e.length) {
    for (const m of e) errors.push(`${name}: ${m}`);
    continue;
  }
  plan.push({ name, tag, willTag: willTag && !exists, exists, candidate: !willTag });
}

if (plan.length) {
  console.log("\nplan:");
  for (const p of plan) {
    const flag = p.exists ? "EXISTS" : p.willTag ? "TAG   " : "cand  ";
    console.log(`  [${flag}] ${p.tag}`);
  }
}

if (errors.length) {
  console.log("\nerrors (nothing tagged):");
  for (const x of errors) console.log("  " + x);
  console.log("\naborted: resolve the above first.\n");
  process.exit(1);
}

if (!doTag) {
  const cands = plan.filter(p => p.candidate).length;
  console.log(cands
    ? `\n(dry-run) ${cands} candidate package(s) shown — set their batch status to "ready" in release-manifest.json to tag them.`
    : "\n(dry-run) re-run with --tag to create these tags locally, then `git push origin --tags`.");
  process.exit(0);
}

// Create tags.
const dirty = worktreeDirtyFiles();
if (dirty.length) {
  console.log("\nWARN: uncommitted package.json changes — tags will point at HEAD, not your working copy:");
  for (const d of dirty) console.log("  " + d);
}

let made = 0;
for (const p of plan.filter(p => p.willTag)) {
  try {
    execSync(`git tag ${p.tag}`, { cwd: ROOT, stdio: "ignore" });
    console.log(`  tagged ${p.tag}`);
    made++;
  } catch {
    console.log(`  FAILED ${p.tag}`);
  }
}
console.log(`\ncreated ${made} tag(s). Push when ready: git push origin --tags`);
