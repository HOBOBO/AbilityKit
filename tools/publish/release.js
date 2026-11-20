#!/usr/bin/env node
/*
 * 根据发布清单校验包、依赖闭包和本地标签，默认只读预览。
 * ready 批次才可创建标签；candidate 仅通过 --include-candidate 加入预览。
 * 本工具不执行稳定性签核、提交、推送或注册表发布，详细约定见同目录 README。
 */
const fs = require("fs");
const path = require("path");
const { execFileSync } = require("child_process");
const { verifyInputs } = require("./release-inputs");

const ROOT = path.join(__dirname, "..", "..");
const PKGS = path.join(ROOT, "Unity", "Packages");
const MANIFEST = path.join(__dirname, "release-manifest.json");
const PACKAGE_NAME = /^com\.abilitykit\.[a-z0-9]+(?:[.-][a-z0-9]+)*$/;
const EXACT_VERSION = /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*)?(?:\+[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*)?$/;

function requireValid(condition, message) {
  if (!condition) throw new Error(message);
}

function isObject(value) {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}

/*
 * 使用参数数组执行 Git，失败必须传给调用方，不能把失败伪装成空标签或干净工作区。
 */
function git(args, input) {
  try {
    return execFileSync("git", args, {
      cwd: ROOT, encoding: "utf8", input, maxBuffer: 32 * 1024 * 1024,
      env: { ...process.env, GIT_OPTIONAL_LOCKS: "0" },
      stdio: [input === undefined ? "ignore" : "pipe", "pipe", "pipe"]
    });
  } catch (error) {
    const detail = error.stderr ? String(error.stderr).trim() : error.message;
    throw new Error(`git ${args[0]} 失败：${detail}`);
  }
}

function parseArgs(argv) {
  const options = { doTag: false, verifyInputs: false, includeCandidate: false, batch: null };
  const seen = new Set();
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    requireValid(["--tag", "--dry-run", "--verify-inputs", "--include-candidate", "--batch"].includes(arg), `未知参数：${arg}`);
    requireValid(!seen.has(arg), `重复参数：${arg}`);
    seen.add(arg);
    if (arg === "--batch") {
      const value = argv[++i];
      requireValid(typeof value === "string" && value.length > 0 && !value.startsWith("--"), "--batch 缺少批次 ID");
      options.batch = value;
    }
    if (arg === "--tag") options.doTag = true;
    if (arg === "--verify-inputs") options.verifyInputs = true;
    if (arg === "--include-candidate") options.includeCandidate = true;
  }
  requireValid(!(seen.has("--tag") && seen.has("--dry-run")), "--tag 与 --dry-run 不能同时使用");
  return options;
}

/*
 * 先检查整个清单的身份与归属，避免批次筛选掩盖重复声明或非法状态。
 * shipped 仅是记录，不能在缺少真实标签时作为未来依赖提供者。
 */
function readManifest() {
  const manifest = JSON.parse(fs.readFileSync(MANIFEST, "utf8"));
  requireValid(isObject(manifest) && EXACT_VERSION.test(manifest.cohortVersion), "cohortVersion 必须是精确 SemVer 版本");
  const format = manifest.gitTagFormat === undefined ? "{name}/{version}" : manifest.gitTagFormat;
  requireValid(typeof format === "string" && format.split("{name}").length === 2
    && format.split("{version}").length === 2
    && !/[{}]/.test(format.replace("{name}", "").replace("{version}", "")),
  "gitTagFormat 必须各包含一次 {name} 和 {version}，且不能含其他占位符");
  requireValid(isObject(manifest.neverReleased) && Array.isArray(manifest.neverReleased.prefixes)
    && manifest.neverReleased.prefixes.length > 0
    && manifest.neverReleased.prefixes.every(p => typeof p === "string" && p.length > 0),
  "neverReleased.prefixes 必须是非空字符串数组");
  requireValid(Array.isArray(manifest.batches), "batches 必须是数组");
  const ids = new Set();
  const owners = new Map();
  for (const batch of manifest.batches) {
    requireValid(isObject(batch) && typeof batch.id === "string" && batch.id.trim().length > 0, "批次必须包含非空 ID");
    requireValid(!ids.has(batch.id), `重复批次 ID：${batch.id}`);
    ids.add(batch.id);
    requireValid(["candidate", "ready", "shipped"].includes(batch.status), `${batch.id}：未知状态 ${batch.status}`);
    requireValid(Array.isArray(batch.packages), `${batch.id}：packages 必须是数组`);
    for (const name of batch.packages) {
      requireValid(typeof name === "string" && PACKAGE_NAME.test(name), `${batch.id}：非法框架包名 ${name}`);
      requireValid(!owners.has(name), `${name}：重复包归属（${owners.get(name)} / ${batch.id}）`);
      owners.set(name, batch.id);
    }
  }
  return { ...manifest, gitTagFormat: format };
}

function readPackages() {
  const packages = new Map();
  for (const directory of fs.readdirSync(PKGS).sort()) {
    if (!directory.startsWith("com.abilitykit.")) continue;
    const file = path.join(PKGS, directory, "package.json");
    if (!fs.existsSync(file)) continue;
    const text = fs.readFileSync(file, "utf8");
    const bom = text.charCodeAt(0) === 0xfeff;
    const json = JSON.parse(bom ? text.slice(1) : text);
    requireValid(isObject(json) && typeof json.name === "string" && PACKAGE_NAME.test(json.name), `${directory}：非法 package.json 包名`);
    requireValid(!packages.has(json.name), `重复 package.json 包名：${json.name}`);
    requireValid(json.name === directory, `${directory}：目录与 package.json 包名 ${json.name} 不一致`);
    requireValid(json.dependencies === undefined || isObject(json.dependencies), `${directory}：dependencies 必须是对象`);
    packages.set(json.name, { version: json.version, deps: json.dependencies || {}, bom });
  }
  return packages;
}

function main() {
  const options = parseArgs(process.argv.slice(2));
  const manifest = readManifest();
  requireValid(options.batch === null || manifest.batches.some(b => b.id === options.batch), `未知批次：${options.batch}`);
  const packages = readPackages();
  const tags = new Set(git(["tag", "--list"]).split(/\r?\n/).filter(Boolean));
  const tagOf = (name, version) => manifest.gitTagFormat.replace("{name}", name).replace("{version}", version);
  const neverReleased = name => manifest.neverReleased.prefixes.some(p => name.startsWith(p));
  const selected = new Map();
  for (const batch of manifest.batches) {
    if (options.batch !== null && batch.id !== options.batch) continue;
    if (batch.status !== "ready" && !(options.includeCandidate && batch.status === "candidate")) continue;
    for (const name of batch.packages) selected.set(name, batch.status);
  }

  console.log(`\n=== release.js ${options.doTag ? "TAG" : "DRY-RUN"} / cohort ${manifest.cohortVersion} ===`);
  const errors = [];
  const plan = [];
  const refs = new Set();
  for (const [name, status] of [...selected].sort(([a], [b]) => a.localeCompare(b, "en"))) {
    const pkg = packages.get(name);
    if (!pkg) {
      errors.push(`${name}：Unity/Packages 中缺少包`);
      continue;
    }
    if (neverReleased(name)) errors.push(`${name}：禁止发布 thirdparty/demo 包`);
    if (!EXACT_VERSION.test(pkg.version) || pkg.version !== manifest.cohortVersion)
      errors.push(`${name}：版本 ${pkg.version} 不等于 cohort ${manifest.cohortVersion} 的精确版本`);
    if (pkg.bom) errors.push(`${name}：package.json 含 BOM`);

    for (const [dependency, requested] of Object.entries(pkg.deps)) {
      if (!dependency.startsWith("com.abilitykit.")) continue;
      if (neverReleased(dependency)) {
        errors.push(`${name}：依赖禁止发布的包 ${dependency}`);
        continue;
      }
      if (!packages.has(dependency)) {
        errors.push(`${name}：依赖缺少的包 ${dependency}`);
        continue;
      }
      if (typeof requested !== "string" || !EXACT_VERSION.test(requested)) {
        errors.push(`${name}：内部依赖 ${dependency} 必须请求精确 SemVer 版本，实际为 ${requested}`);
        continue;
      }
      const dependencyTag = tagOf(dependency, requested);
      refs.add(dependencyTag);
      if (tags.has(dependencyTag)) continue;
      const provider = packages.get(dependency);
      const providerStatus = selected.get(dependency);
      /*
       * 候选预览允许候选闭包，但 ready 包只能依赖已打标或同次计划中的 ready 包。
       * 同计划仅提供它实际声明的版本，不能仅凭包名掩盖请求版本不满足。
       */
      const allowedStatus = providerStatus === "ready" || (status === "candidate" && providerStatus === "candidate");
      if (!allowedStatus || provider.version !== requested)
        errors.push(`${name}：依赖 ${dependency}@${requested} 无标签 ${dependencyTag}，且无同版本可用计划（${providerStatus || "未选择"}@${provider.version}）`);
    }

    const tag = tagOf(name, pkg.version);
    refs.add(tag);
    plan.push({ name, version: pkg.version, tag, candidate: status === "candidate", exists: tags.has(tag) });
  }

  requireValid(errors.length === 0, `校验失败，未创建标签：\n  ${errors.join("\n  ")}`);
  for (const tag of refs) git(["check-ref-format", `refs/tags/${tag}`]);
  const evidence = (options.verifyInputs || options.doTag) && plan.length > 0
    ? verifyInputs({ root: ROOT, git, plan, packages, selected, tags, tagOf, neverReleased, exactVersion: EXACT_VERSION })
    : null;
  if (evidence) console.log(`input-evidence: ${JSON.stringify(evidence)}`);
  console.log("\nplan:");
  for (const item of plan) console.log(`  [${item.exists ? "EXISTS" : item.candidate ? "cand  " : "TAG   "}] ${item.tag}`);
  if (plan.length === 0) console.log("  无符合条件的包；默认只选择 ready，候选预览需 --include-candidate，shipped 不纳入计划。");
  if (!options.doTag) {
    console.log(`\n(dry-run) ${evidence ? "提交内容一致性" : "计划声明"}只读校验完成；candidate 不具备打标资格。`);
    return;
  }

  const toTag = plan.filter(p => !p.candidate && !p.exists);
  if (toTag.length === 0) {
    console.log("\n无待创建的 ready 标签。");
    return;
  }
  let made = 0;
  for (const item of toTag) {
    try {
      git(["tag", item.tag, evidence.head]);
    } catch (error) {
      throw new Error(`${error.message}\n已创建 ${made} 个标签，保留已成功标签且停止后续操作；请人工检查，不自动回滚或推送。`);
    }
    made++;
    console.log(`  tagged ${item.tag}`);
  }
  console.log(`\ncreated ${made} tag(s). 仅本地创建，未推送。`);
}

try {
  main();
} catch (error) {
  console.error(`\nERROR：${error.message}`);
  process.exitCode = 1;
}
