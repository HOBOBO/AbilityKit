#!/usr/bin/env node
// check_unity_usings.js - catch a missing `using UnityEngine;` / `using UnityEditor;`
// before the real Unity build does.
//
// Why this exists: the .NET mirror of a package compiles fine no matter which
// Unity namespace a type lives in, because it never sees Unity types at all.
// Compiling the sources offline without a Unity assembly does not help either -
// every Unity type comes back "not found", so a *missing using* is
// indistinguishable from "Unity is absent entirely". That blind spot let
// `TextAsset` (UnityEngine, not UnityEditor) reach a real Unity build as CS0246.
//
// The check is deliberately a curated list of low-ambiguity type names. Names
// that also exist in the BCL (`Object`, `Debug`, `Application`) are excluded so
// a plain `System.Object` never reports a false positive.
//
// Scope: Editor folders only. Runtime code in framework packages declares
// `noEngineReferences: true`, so Unity types cannot legally appear there, and
// names like `Time`, `Vector3` or `Quaternion` collide with the packages' own
// types - scanning Runtime produced 498 findings, all of them false. Inside an
// Editor assembly those names unambiguously mean Unity's, which is where this
// check is worth anything.
//
// Usage:
//   node tools/check_unity_usings.js                 # scans Unity/Packages/*/Editor
//   node tools/check_unity_usings.js <directory>     # scans a subtree
//
// Exits 0 when clean, 1 when any file uses a Unity type without its namespace.

const fs = require('fs');
const path = require('path');

// Kept deliberately to names that are unambiguous in this repo. Generic words
// that double as AbilityKit type or namespace names (`Editor`, `Time`,
// `Transform`, `Canvas`, `Camera`, `Screen`) are left out: the whole repo
// compiles in Unity today, so a correct checker must report zero findings on it,
// and every one of those names produced false positives.
const UNITY_TYPES = {
  UnityEngine: [
    'Vector2', 'Vector3', 'Vector4', 'Quaternion', 'Color', 'Color32',
    'GUIContent', 'GUILayout', 'GUIStyle', 'GUISkin', 'GUILayoutOption',
    'TextAsset', 'ScriptableObject', 'Mathf', 'JsonUtility', 'PlayerPrefs',
    'Texture2D', 'Sprite', 'Material',
  ],
  UnityEditor: [
    'EditorWindow', 'EditorGUILayout', 'EditorStyles', 'EditorGUI', 'EditorGUIUtility',
    'MessageType', 'MenuItem', 'InitializeOnLoad', 'InitializeOnLoadMethod',
    'AssetDatabase', 'AssetImporter', 'EditorApplication', 'EditorUtility',
    'EditorPrefs', 'SerializedObject', 'SerializedProperty', 'PrefabUtility',
  ],
};

const root = process.argv[2] || path.join('Unity', 'Packages');
const SKIP_DIRS = new Set(['obj', 'bin', 'Tests', 'Samples~', '.git']);

function collectCs(dir, out, editorOnly) {
  if (!fs.existsSync(dir)) return out;
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (SKIP_DIRS.has(entry.name)) continue;
      collectCs(p, out, editorOnly);
    } else if (entry.name.endsWith('.cs')) {
      if (editorOnly) {
        const parts = p.split(path.sep);
        // An asmdef lives in .../Editor/<name>.asmdef, so a file is Editor code
        // when its nearest enclosing folder chain contains Editor.
        if (parts.indexOf('Editor') < 0) continue;
      }
      out.push(p);
    }
  }
  return out;
}

// Comments and string literals both carry type names that are not code: line
// comments account for commented-out API calls, and doc/description strings
// account for prose like "imported back into ScriptableObject assets".
function stripCommentsAndStrings(text) {
  const out = [];
  let i = 0;
  const n = text.length;

  while (i < n) {
    const c = text[i];

    if (c === '/' && text[i + 1] === '/') {
      while (i < n && text[i] !== '\n') i++;
      continue;
    }
    if (c === '/' && text[i + 1] === '*') {
      i += 2;
      while (i < n && !(text[i] === '*' && text[i + 1] === '/')) i++;
      i += 2;
      continue;
    }

    // Verbatim (@") and interpolated ($@"/@$"/$") string prefixes.
    let verbatim = false;
    let j = i;
    if (text[j] === '$') j++;
    if (text[j] === '@') { verbatim = true; j++; }
    else if (text[j] === '$') j++;
    if (text[j] === '"') {
      const isString = verbatim || j > i || text[i] === '"';
      if (isString) {
        i = j + 1;
        while (i < n) {
          if (verbatim && text[i] === '"' && text[i + 1] === '"') { i += 2; continue; }
          if (!verbatim && text[i] === '\\') { i += 2; continue; }
          if (text[i] === '"') { i++; break; }
          if (!verbatim && text[i] === '\n') break;
          i++;
        }
        out.push('""');
        continue;
      }
    }

    if (c === "'") {
      i++;
      while (i < n) {
        if (text[i] === '\\') { i += 2; continue; }
        if (text[i] === "'") { i++; break; }
        i++;
      }
      out.push("''");
      continue;
    }

    out.push(c);
    i++;
  }

  return out.join('');
}

const explicitRoot = process.argv[2];
const files = collectCs(root, [], !explicitRoot);
let problems = 0;
let scanned = 0;

for (const file of files) {
  const text = fs.readFileSync(file, 'utf8');
  const body = stripCommentsAndStrings(text);
  scanned++;

  for (const [ns, types] of Object.entries(UNITY_TYPES)) {
    // A leading dot means member access, not a type reference: this repo has
    // enum members such as TriggerValueType.Vector3.
    const used = types.filter((t) => new RegExp('(?<![.\\w])' + t + '\\b').test(body));
    if (used.length === 0) continue;

    // A file that declares a member with the same name shadows the type
    // (AbilityKit has `public string Color;` in an editor config). Without a
    // compiler this is ambiguous, so leave the name alone rather than guess.
    const shadowed = used.filter((t) =>
      new RegExp('\\b\\w+\\s+' + t + '\\s*[;={]').test(body));
    if (shadowed.length === used.length) continue;
    const candidates = used.filter((t) => shadowed.indexOf(t) < 0);
    if (candidates.length === 0) continue;

    // A type also reachable through a fully-qualified name does not need the using.
    const needed = candidates.filter((t) => !new RegExp(ns + '\\.' + t + '\\b').test(body));
    if (needed.length === 0) continue;

    const hasUsing = new RegExp('^\\s*using\\s+' + ns + '\\s*;', 'm').test(text);
    if (hasUsing) continue;

    console.log('  ' + file);
    console.log('    missing: using ' + ns + ';   (used: ' + needed.join(', ') + ')');
    problems++;
  }
}

console.log('');
console.log('scanned files: ' + scanned);
console.log(problems === 0
  ? 'OK - every Unity type has its namespace imported.'
  : problems + ' file(s) use a Unity type without importing its namespace.');
process.exit(problems === 0 ? 0 : 1);
