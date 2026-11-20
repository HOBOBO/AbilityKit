/*
 * 在系统临时目录复制真实 CLI 并初始化独立 Git 仓库，验证发布计划的可观察行为。
 * 所有测试标签仅存在于夹具；不读取或修改工作仓库的清单、包状态、标签和远端。
 */
const assert = require("node:assert/strict");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const { execFileSync, spawnSync } = require("node:child_process");
const { test } = require("node:test");

const CORE = "com.abilitykit.core";
const DEP = "com.abilitykit.deterministic";
const VERSION = "0.1.0";
const tag = (name, version = VERSION) => `${name}/${version}`;
const batch = (id, status, packages) => ({ id, status, packages });
const env = Object.fromEntries(Object.entries(process.env).filter(([key]) => !key.startsWith("GIT_")));
env.GIT_CONFIG_NOSYSTEM = "1";
env.GIT_TERMINAL_PROMPT = "0";

function fixture(t) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "AbilityKit-ReleasePlan-"));
  const fixtureEnv = { ...env, GIT_CONFIG_GLOBAL: path.join(root, ".gitconfig") };
  fs.writeFileSync(fixtureEnv.GIT_CONFIG_GLOBAL, "", "utf8");
  /*
   * 递归清理仅针对本函数创建并核验过的独占临时目录，绝不从仓库或清单计算删除路径。
   */
  t.after(() => {
    const resolved = path.resolve(root);
    assert.equal(path.dirname(resolved), path.resolve(os.tmpdir()));
    assert.ok(path.basename(resolved).startsWith("AbilityKit-ReleasePlan-"));
    fs.rmSync(resolved, { recursive: true, force: true });
  });
  const script = path.join(root, "tools", "publish", "release.js");
  fs.mkdirSync(path.dirname(script), { recursive: true });
  fs.copyFileSync(path.join(__dirname, "release.js"), script);
  fs.copyFileSync(path.join(__dirname, "release-inputs.js"), path.join(path.dirname(script), "release-inputs.js"));
  const writeJson = (file, value) => {
    fs.mkdirSync(path.dirname(file), { recursive: true });
    fs.writeFileSync(file, JSON.stringify(value, null, 2) + "\n", "utf8");
  };
  const packagePath = name => path.join(root, "Unity", "Packages", name, "package.json");
  const writeFile = (relative, text) => {
    const file = path.join(root, relative);
    fs.mkdirSync(path.dirname(file), { recursive: true });
    fs.writeFileSync(file, text, "utf8");
  };
  const writePackage = (name, value) => writeJson(packagePath(name), value);
  const writeManifest = value => writeJson(path.join(path.dirname(script), "release-manifest.json"), value);
  const git = args => execFileSync("git", args, { cwd: root, env: fixtureEnv, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] });
  const manifest = {
    cohortVersion: VERSION,
    gitTagFormat: "{name}/{version}",
    neverReleased: { prefixes: ["com.abilitykit.thirdparty.", "com.abilitykit.demo."] },
    batches: [batch("main", "ready", [CORE, DEP])]
  };
  const core = { name: CORE, version: VERSION, dependencies: { [DEP]: VERSION } };
  const dependency = { name: DEP, version: VERSION };
  writePackage(CORE, core);
  writePackage(DEP, dependency);
  writeManifest(manifest);
  writeFile(`Unity/Packages/${CORE}/Runtime/Probe.cs`, "public class Probe { }\n");
  writeFile(`Unity/Packages/${DEP}/Runtime/Kernel.cs`, "public class Kernel { }\n");
  git(["init", "--quiet"]);
  const commit = () => {
    git(["add", "--all", "--force"]);
    git(["-c", "user.name=Release Fixture", "-c", "user.email=fixture@example.invalid",
      "-c", "commit.gpgsign=false", "-c", "core.hooksPath=disabled-hooks", "commit", "--quiet", "-m", "fixture"]);
  };
  commit();
  const tags = () => git(["tag", "--list"]).trim().split(/\r?\n/).filter(Boolean).sort();
  const snapshot = () => {
    const files = [];
    const walk = directory => {
      if (!fs.existsSync(directory)) return;
      for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
        const file = path.join(directory, entry.name);
        if (entry.isDirectory()) walk(file);
        else files.push([path.relative(root, file), fs.readFileSync(file, "hex")]);
      }
    };
    walk(path.join(root, ".git", "refs", "tags"));
    return files.sort(([a], [b]) => a.localeCompare(b));
  };
  const run = (args = []) => {
    const result = spawnSync(process.execPath, [script, ...args], {
      cwd: root, env: fixtureEnv, encoding: "utf8", timeout: 30000
    });
    assert.equal(result.error, undefined, `CLI 未正常执行：${result.error}`);
    assert.equal(result.signal, null, `CLI 被信号终止：${result.signal}`);
    return { code: result.status, text: result.stdout + result.stderr };
  };
  const reject = (expected, args = ["--tag", "--include-candidate"]) => {
    const before = snapshot();
    const result = run(args);
    assert.equal(result.code, 1, result.text);
    assert.match(result.text, expected);
    assert.deepEqual(snapshot(), before, `拒绝后标签发生变化：${result.text}`);
  };
  return { root, script, manifest, core, dependency, writeManifest, writePackage, writeFile, packagePath, git, tags, run, reject, commit };
}

test("默认 dry-run 校验同版本 ready 闭包且不创建标签", t => {
  const f = fixture(t);
  const result = f.run();
  assert.equal(result.code, 0, result.text);
  assert.match(result.text, /\[TAG   \] com\.abilitykit\.core\/0\.1\.0/);
  assert.deepEqual(f.tags(), []);
});

test("显式 --dry-run 与默认只读行为一致", t => {
  const f = fixture(t);
  assert.deepEqual(f.run(["--dry-run"]), f.run());
  assert.deepEqual(f.tags(), []);
});

test("candidate 默认不选择，显式加入后仅预览，--tag 也不创建候选标签", t => {
  const f = fixture(t);
  f.manifest.batches[0].status = "candidate";
  f.writeManifest(f.manifest);
  f.commit();
  const omitted = f.run();
  assert.equal(omitted.code, 0, omitted.text);
  assert.doesNotMatch(omitted.text, /\[cand/);
  for (const args of [["--include-candidate"], ["--include-candidate", "--tag"]]) {
    const result = f.run(args);
    assert.equal(result.code, 0, result.text);
    assert.match(result.text, /\[cand  \] com\.abilitykit\.core\/0\.1\.0/);
    assert.deepEqual(f.tags(), []);
  }
});

test("ready 依赖 candidate 时即使包含候选也拒绝打标", t => {
  const f = fixture(t);
  f.manifest.batches = [batch("ready", "ready", [CORE]), batch("candidate", "candidate", [DEP])];
  f.writeManifest(f.manifest);
  f.reject(/无同版本可用计划/);
  f.reject(/无同版本可用计划/, []);
});

test("同计划包名相同但请求版本不同不能满足依赖", t => {
  const f = fixture(t);
  f.core.dependencies[DEP] = "0.2.0";
  f.writePackage(CORE, f.core);
  f.reject(/deterministic@0\.2\.0/);
});

test("candidate 预览同样拒绝请求版本不匹配", t => {
  const f = fixture(t);
  f.manifest.batches[0].status = "candidate";
  f.core.dependencies[DEP] = "0.2.0";
  f.writeManifest(f.manifest);
  f.writePackage(CORE, f.core);
  f.reject(/deterministic@0\.2\.0/, ["--include-candidate"]);
});

test("shipped 状态不能代替依赖标签", t => {
  const f = fixture(t);
  f.manifest.batches = [batch("ready", "ready", [CORE]), batch("old", "shipped", [DEP])];
  f.writeManifest(f.manifest);
  f.reject(/无同版本可用计划/);
});

test("请求版本已有标签可满足依赖，工作区该包版本可不同", t => {
  const f = fixture(t);
  f.manifest.batches = [batch("main", "ready", [CORE]), batch("old", "shipped", [DEP])];
  f.core.dependencies[DEP] = "0.0.9";
  f.writePackage(CORE, f.core);
  f.writeManifest(f.manifest);
  f.git(["tag", tag(DEP, "0.0.9")]);
  const result = f.run(["--batch", "main"]);
  assert.equal(result.code, 0, result.text);
  assert.deepEqual(f.tags(), [tag(DEP, "0.0.9")]);
});

test("已打标 candidate 依赖仍可满足 ready 的精确请求", t => {
  const f = fixture(t);
  f.manifest.batches = [batch("main", "ready", [CORE]), batch("later", "candidate", [DEP])];
  f.writeManifest(f.manifest);
  f.commit();
  f.git(["tag", tag(DEP)]);
  const result = f.run(["--tag"]);
  assert.equal(result.code, 0, result.text);
  assert.deepEqual(f.tags(), [tag(CORE), tag(DEP)].sort());
});

test("其他 ready 批次仅在同次选择时提供依赖", t => {
  const f = fixture(t);
  f.manifest.batches = [batch("core", "ready", [CORE]), batch("dep", "ready", [DEP])];
  f.writeManifest(f.manifest);
  assert.equal(f.run().code, 0);
  f.reject(/未选择@0\.1\.0/, ["--tag", "--batch", "core"]);
});

test("所有 ready 标签可创建，重复执行不移动已有标签", t => {
  const f = fixture(t);
  const head = f.git(["rev-parse", "HEAD"]).trim();
  const result = f.run(["--tag"]);
  assert.equal(result.code, 0, result.text);
  assert.deepEqual(f.tags(), [tag(CORE), tag(DEP)].sort());
  for (const name of [CORE, DEP]) assert.equal(f.git(["rev-parse", tag(name)]).trim(), head);
  const again = f.run(["--tag"]);
  assert.equal(again.code, 0, again.text);
  assert.match(again.text, /EXISTS/);
  assert.doesNotMatch(again.text, /tagged /);
});

test("无关 candidate 可显示，但 --tag 只创建 ready 标签", t => {
  const f = fixture(t);
  const extra = "com.abilitykit.extra";
  f.writePackage(extra, { name: extra, version: VERSION });
  f.manifest.batches.push(batch("later", "candidate", [extra]));
  f.writeManifest(f.manifest);
  f.commit();
  const result = f.run(["--tag", "--include-candidate"]);
  assert.equal(result.code, 0, result.text);
  assert.match(result.text, /\[cand  \] com\.abilitykit\.extra/);
  assert.deepEqual(f.tags(), [tag(CORE), tag(DEP)].sort());
});

const invalidArguments = [
  [["--batch"], /缺少批次 ID/],
  [["--batch", "--tag"], /缺少批次 ID/],
  [["--batch", "unknown"], /未知批次/],
  [["--unknown"], /未知参数/],
  [["--tag", "--tag"], /重复参数/],
  [["--batch", "main", "--batch", "main"], /重复参数/],
  [["--tag", "--dry-run"], /不能同时使用/]
];
for (const [args, expected] of invalidArguments) {
  test(`参数拒绝：${args.join(" ")}`, t => fixture(t).reject(expected, args));
}

const invalidManifests = [
  ["重复批次 ID", m => m.batches.push(batch("main", "candidate", [])), /重复批次 ID/],
  ["批内重复包", m => m.batches[0].packages.push(CORE), /重复包归属/],
  ["跨批重复包", m => m.batches.push(batch("later", "candidate", [CORE])), /重复包归属/],
  ["未知批次状态", m => m.batches[0].status = "temporary", /未知状态/],
  ["缺少 packages", m => delete m.batches[0].packages, /packages 必须是数组/],
  ["非法 cohort", m => m.cohortVersion = "^0.1.0", /精确 SemVer/],
  ["标签格式缺占位符", m => m.gitTagFormat = "{name}", /gitTagFormat/],
  ["标签格式重复占位符", m => m.gitTagFormat = "{name}/{version}/{name}", /gitTagFormat/],
  ["非法标签引用", m => m.gitTagFormat = "bad ref/{name}/{version}", /check-ref-format 失败/],
  ["缺包", m => m.batches[0].packages.push("com.abilitykit.missing"), /缺少包/],
  ["禁止发布 demo", m => m.batches[0].packages.push("com.abilitykit.demo.example"), /禁止发布/]
];
for (const [name, mutate, expected] of invalidManifests) {
  test(`清单拒绝：${name}`, t => {
    const f = fixture(t);
    if (name === "禁止发布 demo") f.writePackage("com.abilitykit.demo.example", { name: "com.abilitykit.demo.example", version: VERSION });
    mutate(f.manifest);
    f.writeManifest(f.manifest);
    f.reject(expected);
  });
}

for (const requested of ["^0.1.0", "*", "file:../deterministic", "0.1.0;echo injected", "01.1.0", "0.1.0-01", 100]) {
  test(`拒绝非精确内部依赖：${requested}`, t => {
    const f = fixture(t);
    f.core.dependencies[DEP] = requested;
    f.writePackage(CORE, f.core);
    f.reject(/精确 SemVer/);
  });
}

test("精确预发布及构建版本可形成闭包", t => {
  const f = fixture(t);
  const version = "0.1.0-rc.1+build.2";
  f.manifest.cohortVersion = version;
  f.core.version = version;
  f.core.dependencies[DEP] = version;
  f.dependency.version = version;
  f.writeManifest(f.manifest);
  f.writePackage(CORE, f.core);
  f.writePackage(DEP, f.dependency);
  const result = f.run();
  assert.equal(result.code, 0, result.text);
  assert.deepEqual(f.tags(), []);
});

test("禁止依赖 thirdparty/demo，即使已有标签", t => {
  const f = fixture(t);
  const forbidden = "com.abilitykit.thirdparty.example";
  f.core.dependencies[forbidden] = VERSION;
  f.writePackage(CORE, f.core);
  f.writePackage(forbidden, { name: forbidden, version: VERSION });
  f.git(["tag", tag(forbidden)]);
  f.reject(/依赖禁止发布的包/);
});

test("缺少依赖包不能由标签掩盖", t => {
  const f = fixture(t);
  fs.unlinkSync(f.packagePath(DEP));
  f.git(["tag", tag(DEP)]);
  f.reject(/依赖缺少的包/);
});

test("包版本偏离 cohort 拒绝全部标签", t => {
  const f = fixture(t);
  f.core.version = "0.2.0";
  f.writePackage(CORE, f.core);
  f.reject(/不等于 cohort/);
});

test("包文件 BOM 拒绝全部标签", t => {
  const f = fixture(t);
  fs.writeFileSync(f.packagePath(CORE), "\ufeff" + JSON.stringify(f.core), "utf8");
  f.reject(/含 BOM/);
});

test("重复 package.json 包名拒绝", t => {
  const f = fixture(t);
  f.writePackage("com.abilitykit.zzduplicate", f.core);
  f.reject(/重复 package.json 包名/);
});

test("目录与包名不一致拒绝", t => {
  const f = fixture(t);
  f.core.name = "com.abilitykit.other";
  f.writePackage(CORE, f.core);
  f.reject(/目录与 package.json 包名/);
});

test("非法 dependencies 结构拒绝", t => {
  const f = fixture(t);
  f.core.dependencies = [];
  f.writePackage(CORE, f.core);
  f.reject(/dependencies 必须是对象/);
});

test("Git 标签枚举失败返回非零，不能当作空标签", t => {
  const f = fixture(t);
  fs.writeFileSync(path.join(f.root, ".git", "packed-refs"), "invalid packed refs\n");
  f.reject(/git tag 失败/);
});

test("Git 状态失败在创建标签前返回非零", t => {
  const f = fixture(t);
  fs.writeFileSync(path.join(f.root, ".git", "index"), "invalid index\n");
  f.reject(/git status 失败/);
});

test("标签创建失败返回非零，保留先行成功项并停止后续项", t => {
  const f = fixture(t);
  const extra = "com.abilitykit.zzlast";
  f.writePackage(extra, { name: extra, version: VERSION });
  f.manifest.batches[0].packages.push(extra);
  f.writeManifest(f.manifest);
  f.commit();
  const lock = path.join(f.root, ".git", "refs", "tags", `${tag(DEP)}.lock`);
  fs.mkdirSync(path.dirname(lock), { recursive: true });
  fs.writeFileSync(lock, "fixture lock\n");
  const result = f.run(["--tag"]);
  assert.equal(result.code, 1, result.text);
  assert.match(result.text, /已创建 1 个标签/);
  assert.deepEqual(f.tags(), [tag(CORE)]);
  assert.equal(fs.readFileSync(lock, "utf8"), "fixture lock\n");
});

test("未提交 package.json 阻止打标，普通预览仍可用", t => {
  const f = fixture(t);
  f.core.description = "仅工作区内容";
  f.writePackage(CORE, f.core);
  f.reject(/与 HEAD 不一致/);
  assert.equal(f.run().code, 0);
});

const strictArgs = ["--dry-run", "--verify-inputs"];
const evidenceOf = result => {
  assert.equal(result.code, 0, result.text);
  const line = result.text.split(/\r?\n/).find(s => s.startsWith("input-evidence: "));
  assert.ok(line, result.text);
  return JSON.parse(line.slice("input-evidence: ".length));
};

test("严格只读校验绑定 HEAD、工具 blob 与完整包 tree，重复证据稳定", t => {
  const f = fixture(t);
  const indexBefore = fs.readFileSync(path.join(f.root, ".git", "index"));
  const evidence = evidenceOf(f.run(strictArgs));
  assert.equal(evidence.head, f.git(["rev-parse", "HEAD"]).trim());
  assert.equal(evidence.schemaVersion, 1);
  assert.equal(Object.keys(evidence.tools).length, 3);
  for (const input of evidence.packages) {
    const tree = f.git(["rev-parse", `HEAD:Unity/Packages/${input.name}`]).trim();
    assert.equal(input.tree, tree);
  }
  assert.deepEqual(evidenceOf(f.run(strictArgs)), evidence);
  assert.deepEqual(fs.readFileSync(path.join(f.root, ".git", "index")), indexBefore);
  assert.deepEqual(f.tags(), []);
  t.diagnostic(`fixture-input-evidence: ${JSON.stringify(evidence)}`);
});

for (const [name, mutate, expected] of [
  ["Runtime 修改", f => f.writeFile(`Unity/Packages/${CORE}/Runtime/Probe.cs`, "public class Changed { }\n"), /与 HEAD 不一致/],
  ["依赖 Runtime 修改", f => f.writeFile(`Unity/Packages/${DEP}/Runtime/Kernel.cs`, "public class Changed { }\n"), /与 HEAD 不一致/],
  ["暂存区修改", f => { f.writeFile(`Unity/Packages/${CORE}/Runtime/Probe.cs`, "changed\n"); f.git(["add", "--all"]); }, /与 HEAD 不一致/],
  ["删除输入", f => fs.unlinkSync(f.packagePath(DEP).replace("package.json", "Runtime/Kernel.cs")), /已删除或未检出/],
  ["未跟踪源码", f => f.writeFile(`Unity/Packages/${CORE}/Runtime/New.cs`, "new\n"), /未提交输入/],
  ["忽略文件", f => { f.writeFile(".gitignore", "*.ignored\n"); f.commit(); f.writeFile(`Unity/Packages/${CORE}/Runtime/source.ignored`, "ignored\n"); }, /未提交输入/],
  ["清单修改", f => { f.manifest.description = "uncommitted"; f.writeManifest(f.manifest); }, /release-manifest.json.*与 HEAD 不一致/],
  ["CLI 修改", f => fs.appendFileSync(f.script, "\n/* 未提交工具修改 */\n"), /release.js.*与 HEAD 不一致/],
  ["校验模块修改", f => fs.appendFileSync(path.join(path.dirname(f.script), "release-inputs.js"), "\n/* 未提交模块修改 */\n"), /release-inputs.js.*与 HEAD 不一致/],
  ["assume-unchanged 隐藏修改", f => { f.git(["update-index", "--assume-unchanged", `Unity/Packages/${CORE}/Runtime/Probe.cs`]); f.writeFile(`Unity/Packages/${CORE}/Runtime/Probe.cs`, "hidden change\n"); }, /实际文件内容与 HEAD 不一致/]
]) {
  test(`严格校验拒绝：${name}`, t => {
    const f = fixture(t);
    mutate(f);
    f.reject(expected, strictArgs);
    f.reject(expected, ["--tag"]);
  });
}

test("暂存区不同而工作文件恢复为 HEAD 仍拒绝", t => {
  const f = fixture(t);
  const file = `Unity/Packages/${CORE}/Runtime/Probe.cs`;
  f.writeFile(file, "staged\n");
  f.git(["add", file]);
  f.writeFile(file, "public class Probe { }\n");
  f.reject(/工作区或暂存区与 HEAD 不一致/, strictArgs);
});

test("新包即使声明版本正确也必须提交", t => {
  const f = fixture(t);
  const extra = "com.abilitykit.extra";
  f.writePackage(extra, { name: extra, version: VERSION });
  f.manifest.batches[0].packages.push(extra);
  f.writeManifest(f.manifest);
  f.git(["add", "tools/publish/release-manifest.json"]);
  f.git(["-c", "user.name=Fixture", "-c", "user.email=f@example.invalid", "-c", "commit.gpgsign=false", "commit", "--quiet", "-m", "manifest only"]);
  f.reject(/尚未提交到 HEAD/, strictArgs);
});

test("范围外脏改不影响所选包的一致性", t => {
  const f = fixture(t);
  f.writeFile("unrelated/dirty.txt", "outside selected inputs\n");
  evidenceOf(f.run(strictArgs));
  assert.deepEqual(f.tags(), []);
});

test("已有标签同版本不同包内容拒绝，即使无需创建新标签", t => {
  const f = fixture(t);
  f.git(["tag", tag(CORE)]);
  f.git(["tag", tag(DEP)]);
  f.writeFile(`Unity/Packages/${CORE}/Runtime/Probe.cs`, "public class Replacement { }\n");
  f.commit();
  f.reject(/同版本不同内容标签/, strictArgs);
  f.reject(/同版本不同内容标签/, ["--tag"]);
});

test("不同提交但同包子树的已有标签允许复用", t => {
  const f = fixture(t);
  f.git(["tag", tag(CORE)]);
  f.git(["tag", tag(DEP)]);
  const previous = f.git(["rev-parse", "HEAD"]).trim();
  f.writeFile("unrelated/committed.txt", "new unrelated commit\n");
  f.commit();
  const evidence = evidenceOf(f.run(strictArgs));
  assert.notEqual(evidence.head, previous);
  assert.ok(evidence.released.every(item => item.commit === previous));
  assert.equal(f.run(["--tag"]).code, 0);
});

test("注解标签可剥离到提交并记录标签对象", t => {
  const f = fixture(t);
  f.git(["-c", "user.name=Fixture", "-c", "user.email=f@example.invalid", "tag", "-a", tag(DEP), "-m", "annotated"]);
  const evidence = evidenceOf(f.run(strictArgs));
  const item = evidence.released.find(p => p.name === DEP);
  assert.ok(item);
  assert.notEqual(item.object, item.commit);
  assert.equal(item.commit, evidence.head);
});

test("标签指向 blob 而非提交时拒绝", t => {
  const f = fixture(t);
  const blob = f.git(["rev-parse", `HEAD:Unity/Packages/${DEP}/package.json`]).trim();
  f.git(["tag", tag(DEP), blob]);
  f.reject(/rev-parse 失败/, strictArgs);
});

test("标签名称版本正确但标签内 package.json 版本错误时拒绝", t => {
  const f = fixture(t);
  f.manifest.batches = [batch("main", "ready", [CORE])];
  f.writeManifest(f.manifest);
  f.commit();
  f.git(["tag", tag(DEP, "0.0.9")]);
  f.core.dependencies[DEP] = "0.0.9";
  f.writePackage(CORE, f.core);
  f.commit();
  f.reject(/包名或版本与标签请求不符/, strictArgs);
});

test("已发布旧依赖从旧标签读取，当前未选源码变化不冒充旧输入", t => {
  const f = fixture(t);
  f.dependency.version = "0.0.9";
  f.writePackage(DEP, f.dependency);
  f.commit();
  f.git(["tag", tag(DEP, "0.0.9")]);
  const oldTree = f.git(["rev-parse", `HEAD:Unity/Packages/${DEP}`]).trim();
  f.dependency.version = VERSION;
  f.writePackage(DEP, f.dependency);
  f.core.dependencies[DEP] = "0.0.9";
  f.writePackage(CORE, f.core);
  f.manifest.batches = [batch("main", "ready", [CORE])];
  f.writeManifest(f.manifest);
  f.commit();
  f.writeFile(`Unity/Packages/${DEP}/Runtime/Kernel.cs`, "unselected current source\n");
  const evidence = evidenceOf(f.run(strictArgs));
  assert.equal(evidence.packages.length, 1);
  assert.equal(evidence.released[0].tree, oldTree);
  assert.equal(evidence.released[0].version, "0.0.9");
});

test("标签内的传递依赖也必须有可用输入", t => {
  const f = fixture(t);
  f.dependency.dependencies = { "com.abilitykit.missing": VERSION };
  f.writePackage(DEP, f.dependency);
  f.commit();
  f.git(["tag", tag(DEP)]);
  f.manifest.batches = [batch("main", "ready", [CORE])];
  f.writeManifest(f.manifest);
  f.commit();
  f.reject(/missing\/0.1.0 缺少标签或本次同版本可用输入/, strictArgs);
});

test("Git 换行规范化允许等价 CRLF，中文与空格路径可校验", t => {
  const f = fixture(t);
  f.writeFile(".gitattributes", "*.cs text eol=lf\n");
  const file = `Unity/Packages/${CORE}/Runtime/中文 空格.cs`;
  f.writeFile(file, "public class Localized { }\n");
  f.commit();
  f.writeFile(file, "public class Localized { }\r\n");
  evidenceOf(f.run(strictArgs));
});

test("所选依赖的同版本标签内容不符同样拒绝", t => {
  const f = fixture(t);
  f.git(["tag", tag(DEP)]);
  f.writeFile(`Unity/Packages/${DEP}/Runtime/Kernel.cs`, "public class NewKernel { }\n");
  f.commit();
  f.reject(/同版本不同内容标签/, strictArgs);
});

test("已有标签的传递闭包记录所有实际标签输入", t => {
  const f = fixture(t);
  const extra = "com.abilitykit.leaf";
  f.writePackage(extra, { name: extra, version: VERSION });
  f.dependency.dependencies = { [extra]: VERSION };
  f.writePackage(DEP, f.dependency);
  f.manifest.batches = [batch("main", "ready", [CORE])];
  f.writeManifest(f.manifest);
  f.commit();
  f.git(["tag", tag(extra)]);
  f.git(["tag", tag(DEP)]);
  const evidence = evidenceOf(f.run(strictArgs));
  assert.deepEqual(evidence.released.map(item => item.name).sort(), [DEP, extra].sort());
  assert.equal(evidence.packages.length, 1);
});

test("标签内非法依赖结构拒绝", t => {
  const f = fixture(t);
  f.dependency.dependencies = null;
  f.writePackage(DEP, f.dependency);
  f.commit();
  f.git(["tag", tag(DEP)]);
  delete f.dependency.dependencies;
  f.writePackage(DEP, f.dependency);
  f.manifest.batches = [batch("main", "ready", [CORE])];
  f.writeManifest(f.manifest);
  f.commit();
  f.reject(/dependencies 必须是对象/, strictArgs);
});

test("未跟踪目录联接或符号链接不能引入外部源码", t => {
  const f = fixture(t);
  const external = path.join(f.root, "outside");
  fs.mkdirSync(external);
  fs.writeFileSync(path.join(external, "External.cs"), "outside\n");
  const link = path.join(f.root, "Unity", "Packages", CORE, "Runtime", "Linked");
  fs.symlinkSync(external, link, process.platform === "win32" ? "junction" : "dir");
  f.reject(/符号链接或目录联接/, strictArgs);
});

test("候选严格预览不提升状态或创建标签", t => {
  const f = fixture(t);
  f.manifest.batches[0].status = "candidate";
  f.writeManifest(f.manifest);
  f.commit();
  evidenceOf(f.run([...strictArgs, "--include-candidate"]));
  assert.equal(JSON.parse(fs.readFileSync(path.join(path.dirname(f.script), "release-manifest.json"), "utf8")).batches[0].status, "candidate");
  assert.deepEqual(f.tags(), []);
});

test("标签提交不含请求的包目录时拒绝", t => {
  const f = fixture(t);
  const previous = f.git(["rev-parse", "HEAD"]).trim();
  f.git(["rm", "-r", `Unity/Packages/${DEP}`]);
  f.commit();
  f.git(["tag", tag(DEP)]);
  f.git(["checkout", previous, "--", `Unity/Packages/${DEP}`]);
  f.commit();
  f.reject(/不是包目录/, strictArgs);
});
