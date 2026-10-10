#!/usr/bin/env node
// check_modifier_determinism.js - keep transcendental (drift-prone) math out of
// the modifier numeric kernel and its MOBA resolution/projection surfaces.
//
// Why this exists: the modifier composition kernel is ruled to stay float on the
// grounds that its arithmetic is IEEE bit-consistent (+ - * / and comparisons
// only). That ruling holds only as long as no System.Math/MathF transcendental
// (sqrt/pow/trig/log/exp) sneaks into the kernel or the resolver outlets - those
// functions are the classic cross-runtime drift source (see
// Docs/MobaModifierFormalDesign.md section 4 and the determinism skill's drift
// table). Drift-sensitive math must route through DeterministicMathBridge /
// DeterministicMath (float in / float out, integer kernel) instead.
//
// Allowed by design in the scanned scope: Math.Round / Abs / Min / Max / Clamp /
// Floor / Ceiling (deterministic), and anything on DeterministicMath /
// DeterministicMathBridge. The regex anchors on `Math.` / `MathF.` member access
// only, so `DeterministicMath.Sqrt` never reports.
//
// Usage:
//   node tools/check_modifier_determinism.js            # default scopes below
//   node tools/check_modifier_determinism.js <dir> [...] # scan custom subtrees
//
// Exits 0 when clean, 1 when any drift-prone call is found.

const fs = require('fs');
const path = require('path');

const DEFAULT_SCOPES = [
  'Unity/Packages/com.abilitykit.modifiers/Runtime',
  'Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Modifiers',
  'Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Continuous',
];

const SKIP_DIRS = new Set(['obj', 'bin', 'Tests', 'Samples~', '.git']);

// Sqrt/Cbrt/Pow/Exp/Log*/trig are NOT bit-consistent across .NET/Mono/IL2CPP.
const DRIFT_CALL = /\b(?:Math|MathF)\.(Sqrt|Cbrt|Pow|Exp|Log2|Log10|Log|Sin|Cos|Tan|Asin|Acos|Atan2|Atan|Sinh|Cosh|Tanh)\s*\(/;

function collectCs(dir, out) {
  if (!fs.existsSync(dir)) return out;
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (SKIP_DIRS.has(entry.name)) continue;
      collectCs(p, out);
    } else if (entry.isFile() && entry.name.endsWith('.cs')) {
      out.push(p);
    }
  }
  return out;
}

function checkFile(file, findings) {
  const lines = fs.readFileSync(file, 'utf8').split(/\r?\n/);
  for (let i = 0; i < lines.length; i++) {
    // `determinism-allowlist:` on the same line documents a known, reviewed
    // exception (no deterministic implementation exists yet). Keep this list
    // short and always cite the tracking doc.
    if (lines[i].includes('determinism-allowlist:')) continue;
    if (DRIFT_CALL.test(lines[i])) {
      findings.push(`${path.relative('.', file).replace(/\\/g, '/')}:${i + 1}: ${lines[i].trim()}`);
    }
  }
}

const scopes = process.argv.length > 2 ? process.argv.slice(2) : DEFAULT_SCOPES;
const findings = [];
for (const scope of scopes) {
  for (const file of collectCs(scope, [])) checkFile(file, findings);
}

if (findings.length > 0) {
  console.error(`check_modifier_determinism: ${findings.length} drift-prone math call(s) in the modifier numeric path:`);
  for (const finding of findings) console.error('  ' + finding);
  console.error('Route these through DeterministicMathBridge / DeterministicMath (float in / float out),');
  console.error('or keep the modifier kernel to + - * / and comparisons. See Docs/MobaModifierFormalDesign.md section 4.');
  process.exit(1);
}

console.log(`check_modifier_determinism: clean (${scopes.length} scope(s)).`);
