#!/usr/bin/env node
// audit-versions.js — health check for AbilityKit package version consistency.
// Exits 0 if clean, 1 on any issue. Wire into CI and pre-release gates.
//
// Checks:
//   1. every internal reference resolves to the referenced package's declared version
//   2. no package.json carries a UTF-8 BOM
//   3. every framework package is on the cohort version
//
// Usage:
//   node tools/publish/audit-versions.js            # expect cohort 0.1.0
//   node tools/publish/audit-versions.js -V 0.2.0   # expect a different cohort

const fs = require("fs");
const path = require("path");

const ROOT = path.join(__dirname, "..", "..", "Unity", "Packages");
const DEFAULT_VERSION = "0.1.0";

function parseArgs(argv) {
  const a = { version: DEFAULT_VERSION };
  for (let i = 2; i < argv.length; i++) {
    const t = argv[i];
    if (t === "-V" || t === "--version") a.version = argv[++i];
  }
  return a;
}

function isFramework(name) {
  return name.startsWith("com.abilitykit.")
    && !name.includes(".thirdparty.")
    && !name.startsWith("com.abilitykit.demo.");
}

function read(p) {
  let t = fs.readFileSync(p, "utf8");
  const bom = t.charCodeAt(0) === 0xfeff;
  if (bom) t = t.slice(1);
  return { text: t, bom };
}

const args = parseArgs(process.argv);
const dirs = fs.readdirSync(ROOT).filter(
  d => d.startsWith("com.abilitykit.")
    && fs.existsSync(path.join(ROOT, d, "package.json"))
);

const map = {};
for (const d of dirs) {
  const { text, bom } = read(path.join(ROOT, d, "package.json"));
  const j = JSON.parse(text);
  map[j.name] = { dir: d, ver: j.version, deps: j.dependencies || {}, bom };
}
const ak = Object.keys(map);

const refs = {};
for (const n of ak)
  for (const dep in map[n].deps)
    if (map[dep]) (refs[dep] = refs[dep] || new Set()).add(map[n].deps[dep]);

let issues = 0;
console.log(`=== audit-versions (expect cohort ${args.version}) ===\n`);

let mism = 0;
for (const n of ak) {
  const decl = map[n].ver;
  const refed = [...(refs[n] || [])].sort();
  if (refed.length && !refed.every(v => v === decl)) {
    mism++; issues++;
    console.log(`  MISMATCH ${n}: declared=${decl} referenced=[${refed.join(", ")}]`);
  }
}
console.log(`version mismatches: ${mism}`);

let bom = 0;
for (const n of ak) if (map[n].bom) { bom++; issues++; console.log(`  BOM: ${n}`); }
console.log(`BOM remaining: ${bom}`);

let off = 0;
for (const n of ak)
  if (isFramework(n) && map[n].ver !== args.version) {
    off++; issues++; console.log(`  off-cohort framework: ${n} = ${map[n].ver}`);
  }
console.log(`framework packages off cohort: ${off}`);

console.log(`\n${issues === 0 ? "OK — clean." : issues + " issue(s) found."}`);
process.exit(issues === 0 ? 0 : 1);
