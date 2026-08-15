#!/usr/bin/env node
// align-versions.js
//
// Align every embedded AbilityKit package onto a single locked version.
//
// Cohort rule: framework packages (com.abilitykit.* minus .thirdparty.* and
// demo.*) are released together and share one version number. References that
// point at framework packages are rewritten to that number. References to
// thirdparty/demo packages are left untouched (they are not released).
//
// The rewrite is text-level: only the version *values* change, so each file's
// own indentation, key order, and colon spacing are preserved. UTF-8 BOM, if
// present, is stripped on write (some registry/CI tooling chokes on it).
//
// Usage:
//   node tools/publish/align-versions.js              # dry-run, print plan
//   node tools/publish/align-versions.js --apply      # write to disk
//   node tools/publish/align-versions.js --apply -V 0.2.0   # custom target

const fs = require("fs");
const path = require("path");

const ROOT = path.join(__dirname, "..", "..", "Unity", "Packages");
const DEFAULT_VERSION = "0.1.0";

function parseArgs(argv) {
  const a = { apply: false, version: DEFAULT_VERSION };
  for (let i = 2; i < argv.length; i++) {
    const t = argv[i];
    if (t === "--apply") a.apply = true;
    else if (t === "-V" || t === "--version") a.version = argv[++i];
  }
  return a;
}

function isFramework(name) {
  return name.startsWith("com.abilitykit.")
    && !name.includes(".thirdparty.")
    && !name.startsWith("com.abilitykit.demo.");
}

function readText(p) {
  let t = fs.readFileSync(p, "utf8");
  const hadBom = t.charCodeAt(0) === 0xfeff;
  if (hadBom) t = t.slice(1);
  return { text: t, hadBom };
}

function main() {
  const args = parseArgs(process.argv);
  const dirs = fs.readdirSync(ROOT).filter(
    d => d.startsWith("com.abilitykit.")
      && fs.existsSync(path.join(ROOT, d, "package.json"))
  );

  // Pass 1: classify every package by name so reference targets can be resolved.
  const meta = {}; // name -> { dir, fw }
  for (const d of dirs) {
    const { text } = readText(path.join(ROOT, d, "package.json"));
    const j = JSON.parse(text);
    meta[j.name] = { dir: d, fw: isFramework(j.name) };
  }

  const versionBumps = []; // { file, from, to }
  const depAligns = [];    // { file, key, from, to }
  const bomRemoved = [];   // file
  let unchanged = 0;

  for (const d of dirs) {
    const fp = path.join(ROOT, d, "package.json");
    const { text: orig, hadBom } = readText(fp);
    let text = orig;
    const name = JSON.parse(text).name;
    const fw = meta[name].fw;

    // Top-level version (first "version" at line start). Framework only.
    if (fw) {
      const m = text.match(/^(\s*"version"\s*:\s*)"([^"]*)"/m);
      if (m && m[2] !== args.version) {
        text = text.replace(/^(\s*"version"\s*:\s*)"([^"]*)"/m, `$1"${args.version}"`);
        versionBumps.push({ file: d, from: m[2], to: args.version });
      }
    }

    // Internal references. Capture the original colon spacing (sep) and put it
    // back unchanged so entitas-style double-spacing etc. is preserved.
    text = text.replace(
      /"(com\.abilitykit\.[^"]+)"(\s*:\s*)"([^"]*)"/g,
      (full, key, sep, oldv) => {
        const target = meta[key];
        if (!target || !target.fw || oldv === args.version) return full;
        depAligns.push({ file: d, key, from: oldv, to: args.version });
        return `"${key}"${sep}"${args.version}"`;
      }
    );

    const changed = text !== orig;
    if (hadBom) bomRemoved.push(d);

    if (args.apply && (changed || hadBom)) {
      fs.writeFileSync(fp, text, "utf8"); // Node writes UTF-8 without BOM.
    }
    if (!changed && !hadBom) unchanged++;
  }

  const mode = args.apply ? "APPLIED" : "DRY-RUN (no files written; pass --apply to write)";
  console.log(`\n=== align-versions ${mode} — target ${args.version} ===\n`);

  console.log(`framework version bumps: ${versionBumps.length}`);
  for (const v of versionBumps) console.log(`  ${v.file}: ${v.from} -> ${v.to}`);

  console.log(`\ninternal reference alignments: ${depAligns.length}`);
  const byFile = {};
  for (const d of depAligns) (byFile[d.file] = byFile[d.file] || []).push(d);
  const sorted = Object.keys(byFile).sort();
  for (const f of sorted) {
    console.log(`  ${f} (${byFile[f].length} ref${byFile[f].length > 1 ? "s" : ""})`);
  }

  console.log(`\nBOM stripped from: ${bomRemoved.length} file${bomRemoved.length === 1 ? "" : "s"}`);
  console.log(`files unchanged: ${unchanged}`);
  console.log(`total packages scanned: ${dirs.length}`);

  if (!args.apply) {
    console.log("\n(dry-run) re-run with --apply to write these changes.");
  }
}

main();
