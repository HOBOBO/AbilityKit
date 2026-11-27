export type EvidenceRow = Record<string, unknown>;

export interface ScenarioArchiveCase {
  caseId: string;
  scenario: string;
  status: string;
  summary: string;
  summaryUri: string;
  evidenceStatus: string;
  diagnosticUri: string;
}

export interface ScenarioArchiveRun {
  runId: string;
  startedAt: string;
  evidenceMode: string;
  cases: ScenarioArchiveCase[];
  files: ReadonlyMap<string, File>;
}

export const row = (value: unknown): EvidenceRow =>
  value && typeof value === 'object' && !Array.isArray(value) ? value as EvidenceRow : {};
export const rows = (value: unknown): EvidenceRow[] => Array.isArray(value) ? value.map(row) : [];
export const text = (value: unknown): string => typeof value === 'string' ? value : '';
export const number = (value: unknown): number => typeof value === 'number' && Number.isFinite(value) ? value : 0;
export const id = (value: unknown): number => Number.isSafeInteger(value) && number(value) > 0 ? number(value) : 0;
export const display = (value: unknown): string => typeof value === 'number' || typeof value === 'boolean' ? String(value) : text(value) || '—';

const indexName = 'batch_summary_v1.json';
const mebibyte = 1024 * 1024;

async function readJson(file: File, maximumBytes: number): Promise<EvidenceRow> {
  if (file.size > maximumBytes) throw new Error(`${file.name} 超出网页读取上限（${maximumBytes / mebibyte} MiB）`);
  return row(JSON.parse(await file.text()));
}

export async function scanScenarioArchives(files: Iterable<File>) {
  const byPath = new Map<string, File>();
  const indexes: [string, File][] = [];
  for (const file of files) {
    const parts = file.webkitRelativePath.split('/');
    if (parts.length < 2 || parts.some(part => !part || part.startsWith('.') || /[\\:]/.test(part))) continue;
    parts.shift();
    const path = parts.join('/');
    byPath.set(path, file);
    if (file.name === indexName && parts.length <= 2) indexes.push([path, file]);
  }

  const runs: ScenarioArchiveRun[] = [];
  const warnings: string[] = [];
  for (const [path, file] of indexes) {
    try {
      const index = await readJson(file, 16 * mebibyte);
      if (index.suiteId !== 'moba-battle-scenario') continue;
      const runId = text(index.runId);
      const directory = file.webkitRelativePath.split('/').at(-2);
      if (index.schemaVersion !== 1 || !runId || runId !== directory || !Array.isArray(index.cases)) {
        throw new Error('批次标识或索引版本无效');
      }
      const caseFiles = new Map<string, File>();
      const prefix = path.slice(0, -indexName.length);
      const cases = rows(index.cases).filter(entry => text(entry.caseId)).map(entry => {
        const summaryUri = text(entry.summaryUri);
        const diagnosticUri = text(entry.diagnosticUri);
        const sameCase = summaryUri.replace(/\/summary\.json$/, '/battle-diagnostics.json') === diagnosticUri;
        for (const [uri, pattern] of [[summaryUri, /^cases\/[0-9]+\/summary\.json$/], [diagnosticUri, /^cases\/[0-9]+\/battle-diagnostics\.json$/]] as const) {
          if (uri === diagnosticUri && !sameCase) continue;
          const match = pattern.exec(uri);
          const linked = match?.[0] === uri ? byPath.get(prefix + uri) : undefined;
          if (linked) caseFiles.set(uri, linked);
        }
        return {
          caseId: text(entry.caseId), scenario: text(entry.scenario), status: text(entry.status),
          summary: text(entry.summary), summaryUri, evidenceStatus: text(entry.evidenceStatus), diagnosticUri
        };
      });
      if (cases.length !== index.cases.length) throw new Error('索引中存在无标识的用例');
      runs.push({ runId, startedAt: text(index.startedAt), evidenceMode: text(index.evidenceMode), cases, files: caseFiles });
    } catch (error) {
      warnings.push(`${path}：${error instanceof Error ? error.message : String(error)}`);
    }
  }
  return { runs: runs.sort((left, right) => right.runId.localeCompare(left.runId)), warnings };
}

export async function readScenarioVerdict(run: ScenarioArchiveRun, entry: ScenarioArchiveCase) {
  const file = run.files.get(entry.summaryUri);
  if (!file) throw new Error('批次缺少验收明细；索引中的判定仍可查看。');
  const detail = await readJson(file, mebibyte);
  if (detail.schemaVersion !== 1 || detail.caseId !== entry.caseId || detail.status !== entry.status) {
    throw new Error('验收明细与批次索引不一致。');
  }
  return detail;
}

export async function readScenarioEvidence(run: ScenarioArchiveRun, entry: ScenarioArchiveCase) {
  const file = run.files.get(entry.diagnosticUri);
  if (!file) throw new Error('批次缺少单场证据文件；这不会改变验收判定。');
  const artifact = await readJson(file, 96 * mebibyte);
  const battle = row(artifact.battleDiagnostics);
  if (artifact.schemaVersion !== 'abilitykit-analysis.v1' || battle.schemaVersion !== 'abilitykit-battle-diagnostics.v2') {
    throw new Error('不支持该诊断包版本。');
  }
  return battle;
}

const traceViewLimit = 2000;

export function scenarioTrace(battle: EvidenceRow, tickRate: number) {
  const trace = row(battle.trace);
  const nodes = rows(trace.nodes);
  nodes.sort((left, right) => number(left.startFrame) - number(right.startFrame) || id(left.contextId) - id(right.contextId));
  const sampled = new Map<number, EvidenceRow>();
  for (const node of nodes) {
    const context = id(node.contextId);
    if (context) sampled.set(context, node);
    if (sampled.size >= traceViewLimit) break;
  }
  let hasUnlinkedParents = false;
  const records = [...sampled].map(([context, node]) => {
    const frame = number(node.startFrame);
    let ancestor = id(node.parentContextId);
    let validParent = sampled.has(ancestor);
    for (let depth = 0; validParent && ancestor; depth++) {
      if (ancestor === context || depth >= 64) validParent = false;
      ancestor = id(sampled.get(ancestor)?.parentContextId);
    }
    if (number(node.parentContextId) > 0 && !validParent) hasUnlinkedParents = true;
    return {
      ...node, contextId: context, nodeId: context, rootId: id(node.rootContextId) || context,
      rootContextId: id(node.rootContextId) || context, parentContextId: validParent ? id(node.parentContextId) : 0,
      parentId: validParent ? id(node.parentContextId) : 0,
      sourceActorId: id(node.actorId), actorId: id(node.actorId), targetActorId: id(node.targetActorId),
      sourceContextId: context, frame,
      timeMs: tickRate > 0 && frame >= 0 ? Math.round(frame * 1000 / tickRate) : 0,
      kind: text(node.kind) || 'TraceNode', status: number(node.endFrame) >= 0 ? 'ended' : 'observed'
    };
  });
  return { records, total: nodes.length, incomplete: trace.truncated === true || trace.isStable === false || records.length < nodes.length || hasUnlinkedParents };
}
