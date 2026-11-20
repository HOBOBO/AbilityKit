/*
 * 为发布计划绑定固定 Git 提交、完整包子树及标签依赖闭包。
 * 这里只做本地只读检查；不提交、不刷新索引、不创建或修改 Git 对象。
 */
const fs = require("node:fs");
const path = require("node:path");

function verifyInputs({ root, git, plan, packages, selected, tags, tagOf, neverReleased, exactVersion }) {
  const fail = message => { throw new Error(`发布输入校验失败，未创建标签：${message}`); };
  const head = git(["rev-parse", "--verify", "HEAD^{commit}"]).trim();
  const packagePath = name => `Unity/Packages/${name}`;
  const entriesAt = (commit, relative) => git(["ls-tree", "-r", "-z", commit, "--", relative])
    .split("\0").filter(Boolean).map(record => {
      const tab = record.indexOf("\t");
      const [mode, type, oid] = record.slice(0, tab).split(" ");
      return { mode, type, oid, file: record.slice(tab + 1) };
    });
  const treeAt = (commit, relative) => {
    const record = git(["ls-tree", commit, "--", relative]).trim();
    const match = /^040000 tree ([a-f0-9]+)\t/.exec(record);
    if (!match) fail(`${relative} 在 ${commit} 中不是包目录`);
    return match[1];
  };

  /*
   * 整个包目录都是输入，包括文档、元数据和忽略文件。
   * 不跟随符号链接或目录联接，也不接受子模块，以免把仓库外内容当作提交快照。
   */
  const workingFiles = relative => {
    const files = [];
    const walk = current => {
      const absolute = path.join(root, current);
      const stat = fs.lstatSync(absolute);
      if (stat.isSymbolicLink()) fail(`${current} 是符号链接或目录联接`);
      if (stat.isDirectory()) {
        for (const name of fs.readdirSync(absolute).sort()) walk(`${current}/${name}`);
      } else if (stat.isFile()) files.push(current);
      else fail(`${current} 不是普通文件`);
    };
    walk(relative);
    return files;
  };

  /*
   * hash-object 不带 -w，仅计算 Git 规范化后的内容身份。
   * 批量路径使用 Git 的双引号转义，支持空格、中文、制表符及换行；不经 shell。
   */
  const quotePath = value => JSON.stringify(value).replace(/\\u00([a-f0-9]{2})/gi,
    (_, hex) => `\\${parseInt(hex, 16).toString(8).padStart(3, "0")}`);
  const checkWorking = (relative, directory) => {
    const entries = entriesAt(head, relative);
    if (entries.length === 0) fail(`${relative} 尚未提交到 HEAD`);
    if (entries.some(e => e.type !== "blob" || !["100644", "100755"].includes(e.mode)))
      fail(`${relative} 含符号链接、子模块或不支持的 Git 类型`);
    const expected = new Set(entries.map(e => e.file));
    const actual = directory ? workingFiles(relative) : [relative];
    for (const file of actual) if (!expected.has(file)) fail(`${file} 是未提交输入（包括忽略文件）`);
    for (const entry of entries) {
      const absolute = path.join(root, entry.file);
      if (!fs.existsSync(absolute)) fail(`${entry.file} 已删除或未检出`);
      const stat = fs.lstatSync(absolute);
      if (!stat.isFile() || stat.isSymbolicLink()) fail(`${entry.file} 不是普通文件`);
      if (process.platform !== "win32" && Boolean(stat.mode & 0o111) !== (entry.mode === "100755"))
        fail(`${entry.file} 的可执行权限与 HEAD 不一致`);
    }
    const dirty = git(["status", "--porcelain", "-z", "--untracked-files=all", "--", relative]);
    /*
     * 暂存状态必须干净；工作文件以规范化内容哈希验收，避免仅换行差异被状态缓存误报。
     * 这也覆盖 assume-unchanged 等索引标记隐藏的实际内容变化。
     */
    if (dirty.split("\0").filter(Boolean).some(record => record[0] !== " "))
      fail(`${relative} 的工作区或暂存区与 HEAD 不一致`);
    const hashes = git(["hash-object", "--stdin-paths"], entries.map(e => quotePath(e.file)).join("\n") + "\n")
      .trim().split(/\r?\n/);
    if (hashes.length !== entries.length || entries.some((e, i) => e.oid !== hashes[i]))
      fail(`${relative} 的实际文件内容与 HEAD 不一致（包含被 Git 状态标记隐藏的修改）`);
    return directory ? treeAt(head, relative) : entries[0].oid;
  };

  const tools = {};
  for (const file of ["tools/publish/release.js", "tools/publish/release-inputs.js", "tools/publish/release-manifest.json"])
    tools[file] = checkWorking(file, false);
  const inputs = plan.map(item => ({ name: item.name, version: item.version,
    tree: checkWorking(packagePath(item.name), true) }));
  const byName = new Map(inputs.map(item => [item.name, item]));
  const released = new Map();

  /*
   * 已发布依赖读取标签中的 package.json，不用当前工作区冒充历史版本。
   * 同版本已选包的标签子树必须与本次固定输入一致；不同提交但相同子树允许复用。
   */
  const visitTag = (name, version) => {
    const tag = tagOf(name, version);
    if (released.has(tag)) return;
    git(["check-ref-format", `refs/tags/${tag}`]);
    const reference = `refs/tags/${tag}`;
    const object = git(["rev-parse", "--verify", reference]).trim();
    const commit = git(["rev-parse", "--verify", `${reference}^{commit}`]).trim();
    const relative = packagePath(name);
    const tree = treeAt(commit, relative);
    const entries = entriesAt(commit, relative);
    if (entries.some(e => e.type !== "blob" || !["100644", "100755"].includes(e.mode)))
      fail(`${tag} 的包子树含不支持的 Git 类型`);
    const metadata = entries.find(e => e.file === `${relative}/package.json`);
    if (!metadata) fail(`${tag} 缺少 package.json`);
    const text = git(["cat-file", "blob", metadata.oid]);
    if (text.charCodeAt(0) === 0xfeff) fail(`${tag} 的 package.json 含 BOM`);
    const json = JSON.parse(text);
    if (json.name !== name || json.version !== version) fail(`${tag} 的包名或版本与标签请求不符`);
    const planned = byName.get(name);
    if (planned && planned.version === version && planned.tree !== tree)
      fail(`${tag} 的包子树与 HEAD 输入不一致，禁止复用同版本不同内容标签`);
    released.set(tag, { name, version, tag, object, commit, tree });
    visitDependencies(json.dependencies === undefined ? {} : json.dependencies, "ready");
  };
  const visitDependencies = (dependencies, status) => {
    if (dependencies === null || typeof dependencies !== "object" || Array.isArray(dependencies))
      fail("标签依赖的 dependencies 必须是对象");
    for (const [name, version] of Object.entries(dependencies)) {
      if (!name.startsWith("com.abilitykit.")) continue;
      if (neverReleased(name) || typeof version !== "string" || !exactVersion.test(version))
        fail(`${name}@${version} 不是可发布的精确内部依赖`);
      const tag = tagOf(name, version);
      if (tags.has(tag)) visitTag(name, version);
      else {
        const provider = byName.get(name);
        const providerStatus = selected.get(name);
        if (!provider || provider.version !== version || !(providerStatus === "ready"
          || (status === "candidate" && providerStatus === "candidate")))
          fail(`${tag} 缺少标签或本次同版本可用输入`);
      }
    }
  };
  for (const item of plan) {
    if (tags.has(item.tag)) visitTag(item.name, item.version);
    visitDependencies(packages.get(item.name).deps, selected.get(item.name));
  }
  for (const item of released.values()) {
    if (git(["rev-parse", "--verify", `refs/tags/${item.tag}`]).trim() !== item.object)
      fail(`${item.tag} 在校验期间发生变化，请重新执行`);
  }
  if (git(["rev-parse", "--verify", "HEAD^{commit}"]).trim() !== head) fail("校验期间 HEAD 已变化，请重新执行");
  return { schemaVersion: 1, head, tools, packages: inputs, released: [...released.values()] };
}

module.exports = { verifyInputs };
