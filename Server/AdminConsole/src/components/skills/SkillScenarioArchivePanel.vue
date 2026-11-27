<template>
  <section class="card span-12 scenario-archive">
    <div class="card-head">
      <div><p class="section-kicker">离线验收 · .NET Console</p><h2>英雄技能批次排查</h2></div>
      <label class="archive-picker">导入批跑目录<input type="file" aria-label="导入批跑目录" webkitdirectory multiple @change="chooseDirectory" /></label>
    </div>
    <p class="muted">选择 Console 的输出根目录或单个批次目录。网页只在当前浏览器读取文件，不上传、不执行战斗。</p>
    <p v-if="scanning" role="status">正在读取批次索引…</p>
    <ul v-if="warnings.length" class="diagnostic-list warnings" role="status"><li v-for="warning in warnings" :key="warning">{{ warning }}</li></ul>
    <p v-if="!scanning && !runs.length" class="muted">{{ scanned ? '未找到已发布的 MOBA Console 批次索引。' : '尚未导入批次。' }}</p>

    <template v-if="run">
      <div class="archive-controls">
        <label>批次（{{ runs.length }}）<select :value="run.runId" @change="selectRun(($event.target as HTMLSelectElement).value)">
          <option v-for="candidate in runs" :key="candidate.runId" :value="candidate.runId">{{ candidate.startedAt || candidate.runId }} · {{ candidate.evidenceMode || '留证策略未记录' }} · {{ candidate.cases.length }} 场 · {{ candidate.runId }}</option>
        </select></label>
        <label>判定<select v-model="verdictFilter"><option value="">全部</option><option value="investigate">需排查</option><option v-for="(label, status) in verdictLabels" :key="status" :value="status">{{ label }}</option></select></label>
        <label>搜索用例<input v-model.trim="query" placeholder="用例 ID 或场景文件" /></label>
      </div>
      <section class="verdict-overview" aria-label="批次判定分布">
        <div class="verdict-heading"><strong>批次概览</strong><span class="muted">{{ run.cases.length }} 场 · {{ availableEvidenceCount }} 份可打开的证据</span></div>
        <div v-if="run.cases.length" class="verdict-strip" aria-hidden="true">
          <span v-for="item in activeVerdicts" :key="item.status" :data-verdict="item.status" :style="{ flexGrow: item.count }"></span>
        </div>
        <div class="verdict-cards" aria-label="按判定筛选用例">
          <button v-for="item in verdictDistribution" :key="item.status" type="button" class="secondary verdict-card" :data-verdict="item.status"
            :aria-pressed="verdictFilter === item.status" :disabled="item.count === 0 && verdictFilter !== item.status" @click="verdictFilter = verdictFilter === item.status ? '' : item.status">
            <span>{{ item.label }}</span><strong>{{ item.count }}</strong>
          </button>
        </div>
      </section>
      <div class="archive-cases" aria-label="批次用例">
        <button v-for="(candidate, index) in filteredCases" :key="`${candidate.summaryUri}-${index}`" type="button" class="secondary archive-case" :data-verdict="verdictKey(candidate.status)" :aria-pressed="entry === candidate" @click="selectCase(candidate)">
          <strong>{{ candidate.caseId }}</strong><span>{{ verdictLabel(candidate.status) }} · {{ evidenceLabel(candidate) }}</span>
        </button>
        <p v-if="!filteredCases.length" class="muted">没有符合条件的用例。</p>
      </div>
    </template>

    <template v-if="entry">
      <h3>{{ entry.caseId }} · {{ verdictLabel(entry.status) }}</h3>
      <p>{{ entry.summary || '未记录摘要。' }}</p>
      <p v-if="detailBusy || detailError" role="status">{{ detailError || '正在读取验收明细…' }}</p>
      <div v-if="detail" class="archive-counts">
        <span v-for="check in checks" :key="check.label" class="badge" :class="{ danger: check.matched < check.expected }">{{ check.label }} {{ check.matched }}/{{ check.expected }}</span>
        <span v-if="!checks.length" class="muted">没有可汇总的断言计数。</span>
      </div>
      <details v-if="detail"><summary>验收明细（原始记录）</summary><pre>{{ JSON.stringify(detail, null, 2) }}</pre></details>
      <div class="archive-actions">
        <span>诊断证据：{{ evidenceLabel(entry) }}</span>
        <button type="button" class="secondary" :disabled="!run?.files.has(entry.diagnosticUri) || evidenceBusy" @click="openEvidence">{{ evidenceBusy ? '正在读取…' : battle ? '重新读取证据' : '打开单场证据' }}</button>
      </div>
      <p v-if="evidenceError" role="alert">{{ evidenceError }}</p>
    </template>

    <template v-if="battle">
      <div class="archive-actions" aria-label="诊断视图">
        <button v-for="(label, key) in views" :key="key" type="button" class="secondary" :aria-pressed="view === key" @click="view = key">{{ label }}</button>
      </div>
      <p class="muted">先按帧定位。{{ tickRate ? `毫秒数按 ${tickRate} Hz 由帧号换算，不是墙钟耗时。` : '未记录 tick rate，仅显示帧号。' }} 证据用于解释，不补判验收。</p>

      <template v-if="view === 'trace'">
        <p v-if="trace.incomplete" role="status">Trace 可能不完整：网页可显示 {{ trace.records.length }}/{{ trace.total }} 个已保留节点，缺失节点不反推因果。</p>
        <div v-if="selectedNode" class="archive-actions">
          <span>选中节点：{{ selectedNode.nodeId }}</span>
          <button v-if="findActor(selectedNode.sourceActorId, selectedNode.raw.sourceActorGeneration)" type="button" class="secondary" @click="openActor(selectedNode.sourceActorId, selectedNode.raw.sourceActorGeneration)">查看来源实体</button>
          <button v-if="findActor(selectedNode.targetActorId, selectedNode.raw.targetActorGeneration)" type="button" class="secondary" @click="openActor(selectedNode.targetActorId, selectedNode.raw.targetActorGeneration)">查看目标实体</button>
        </div>
        <SkillTraceVisualizer title="同场技能溯源" :nodes="filteredTrace" :total-node-count="trace.total" :selected-node="selectedNode"
          :timeline="traceTimeline" :filter="traceFilter" :filter-options="traceOptions" :entity-relations="traceRelations"
          :frame-only="!tickRate" empty-text="该诊断包未保留匹配的 Trace 节点。" @select-node="selectedNodeId = $event.nodeId" />
      </template>

      <template v-else-if="view === 'events'">
        <ScenarioEventTimeline :timeline="eventTimeline" :selection="timelineSelection" :event-count="events.length" @select="selectTimelineRange" />
        <label class="archive-field">查帧、事件、配置或实体<input v-model.trim="eventQuery" placeholder="例如 F120、Damage、30030101" /></label>
        <div v-if="timelineSelection" class="archive-actions">
          <span class="badge" role="status">{{ scenarioEventLanes[timelineSelection.lane] }} · {{ formatScenarioFrameRange(timelineSelection) }}</span>
          <button class="secondary" type="button" @click="timelineSelection = null">清除轨道筛选</button>
        </div>
        <div class="archive-table"><table><thead><tr><th>帧 / 序号</th><th>事件 / 配置</th><th>来源 → 目标</th><th>摘要</th><th>溯源</th></tr></thead>
          <tbody><tr v-for="(event, index) in eventPageRows" :key="index">
            <td>{{ eventFrameLabel(event) }} / {{ display(event.sequence) }}</td><td>{{ scenarioEventName(event) }} · {{ id(event.configId) || '—' }}</td>
            <td><button v-if="findActor(event.sourceActorId, event.sourceActorGeneration)" class="secondary" type="button" @click="openActor(event.sourceActorId, event.sourceActorGeneration)">#{{ event.sourceActorId }}</button><span v-else>{{ id(event.sourceActorId) || '—' }}</span> → <button v-if="findActor(event.targetActorId, event.targetActorGeneration)" class="secondary" type="button" @click="openActor(event.targetActorId, event.targetActorGeneration)">#{{ event.targetActorId }}</button><span v-else>{{ id(event.targetActorId) || '—' }}</span></td>
            <td>{{ display(event.summary) }}</td><td><button v-if="id(event.contextId) && traceIds.has(String(id(event.contextId)))" class="secondary" type="button" @click="openTrace(event.contextId)">查看 Trace</button><span v-else>—</span></td>
          </tr></tbody></table></div>
        <p v-if="!matchingEvents.length" class="muted">没有符合条件的事件。</p>
        <div class="archive-actions"><span>{{ matchingEvents.length }} 条 · {{ eventPage + 1 }}/{{ eventPages }} 页</span><button type="button" class="secondary" :disabled="eventPage === 0" @click="eventPage--">上一页</button><button type="button" class="secondary" :disabled="eventPage + 1 >= eventPages" @click="eventPage++">下一页</button></div>
      </template>

      <template v-else>
        <div class="archive-controls"><label>快照实体 · F{{ display(row(battle.state).frame) }}<select v-model="actorKey"><option value="">请选择实体</option><option v-for="actor in actors" :key="keyOfActor(actor)" :value="keyOfActor(actor)">{{ actorLabel(actor) }}</option></select></label></div>
        <p v-if="!actors.length" class="muted">证据未保留实体状态。</p>
        <template v-if="actor">
          <p>{{ actorLabel(actor) }} · 坐标 ({{ display(actor.positionX) }}, {{ display(actor.positionY) }}, {{ display(actor.positionZ) }}) · {{ actor.isAlive === true ? '存活' : actor.isAlive === false ? '退场' : '状态未记录' }}</p>
          <div class="actor-vitals"><span>快照生命 {{ display(actor.health) }} / {{ display(actor.maximumHealth) }}</span><meter v-if="actorHealthRatio !== null" min="0" max="1" :value="actorHealthRatio" aria-label="快照生命比例"></meter></div>
          <div class="archive-table"><table><caption>属性 · F{{ display(row(battle.attributes).frame) }}</caption><thead><tr><th>属性</th><th>基础</th><th>最终</th><th>修饰器数</th></tr></thead>
            <tbody><tr v-for="(attribute, index) in actorAttributes" :key="index"><td>{{ text(attribute.name) || `#${display(attribute.attributeId)}` }}</td><td>{{ display(attribute.baseValue) }}</td><td>{{ display(attribute.finalValue) }}</td><td>{{ display(attribute.modifierCount) }}</td></tr></tbody></table><p v-if="!actorAttributes.length" class="muted">没有可关联的属性快照。</p></div>
        </template>
        <div class="archive-table"><table><caption>{{ actor ? '实体关联对象（按记录中的参与者 ID）' : '战斗对象（前 100 项）' }}</caption><thead><tr><th>对象 / 配置</th><th>生成 / 销毁</th><th>归属 / 来源 / 目标</th><th>溯源</th></tr></thead><tbody>
          <tr v-for="(object, index) in objects.slice(0, 100)" :key="index"><td>{{ objectKinds[number(object.kind)] || '对象' }} #{{ display(object.runtimeId) }} · {{ display(object.definitionId) }}</td><td>F{{ display(object.createdFrame) }} / {{ number(object.destroyedFrame) >= 0 ? `F${display(object.destroyedFrame)}` : '—' }}</td><td>{{ id(object.ownerActorId) || '—' }} / {{ id(object.sourceActorId) || '—' }} / {{ id(object.targetActorId) || '—' }}</td><td><button v-if="id(object.contextId) && traceIds.has(String(id(object.contextId)))" class="secondary" type="button" @click="openTrace(object.contextId)">查看 Trace</button><span v-else>—</span></td></tr>
        </tbody></table><p v-if="!objects.length" class="muted">没有可查看的对象记录。</p><p v-else-if="objects.length > 100" class="muted">共 {{ objects.length }} 项，此处展示前 100 项。</p></div>
      </template>
    </template>
  </section>
</template>

<script setup lang="ts">
import { computed, reactive, ref, shallowRef, watch } from 'vue';
import ScenarioEventTimeline from './ScenarioEventTimeline.vue';
import SkillTraceVisualizer from './SkillTraceVisualizer.vue';
import { display, id, number, readScenarioEvidence, readScenarioVerdict, row, rows, scanScenarioArchives, scenarioTrace, text, type EvidenceRow, type ScenarioArchiveCase, type ScenarioArchiveRun } from '../../services/scenarioArchive';
import { buildSkillAnalysisEntityRelations, buildSkillAnalysisFilterOptions, buildSkillAnalysisTree, buildTimelineFromAnalysisNodes, createDefaultSkillAnalysisFilter, filterSkillAnalysisNodes, flattenSkillAnalysisTree } from '../../services/skillAnalysisProjection';
import { buildScenarioEventTimeline, eventFrameLabel, formatScenarioFrameRange, matchesScenarioTimelineSelection, scenarioEventLanes, scenarioEventName, type ScenarioTimelineSelection } from '../../services/scenarioEventTimeline';

const verdictLabels: Readonly<Record<string, string>> = { passed: '通过', failed: '断言失败', error: '运行错误', 'no-assertions': '无断言', unknown: '未知判定' };
const evidenceLabels: Readonly<Record<string, string>> = { available: '已保留', 'not-retained': '未保留', 'not-captured': '未采集', error: '采集或导出失败' };
const views = { trace: '技能溯源', events: '战斗事件', actors: '实体与属性' } as const;
const actorKinds = ['未知', '英雄', '小兵', '野怪', '建筑', '召唤物', '投射物', '区域'];
const objectKinds = ['未知', '实体', '投射物', '区域', '召唤物'];
const verdictKey = (status: string) => Object.hasOwn(verdictLabels, status) ? status : 'unknown';
const verdictLabel = (status: string) => verdictLabels[verdictKey(status)];
const evidenceLabel = (item: ScenarioArchiveCase) => item.diagnosticUri && !run.value?.files.has(item.diagnosticUri) ? '文件缺失或引用无效' : Object.hasOwn(evidenceLabels, item.evidenceStatus) ? evidenceLabels[item.evidenceStatus] : '未知';
const errorText = (error: unknown) => error instanceof Error ? error.message : String(error);

const runs = shallowRef<ScenarioArchiveRun[]>([]);
const run = shallowRef<ScenarioArchiveRun | null>(null);
const entry = shallowRef<ScenarioArchiveCase | null>(null);
const detail = shallowRef<EvidenceRow | null>(null);
const battle = shallowRef<EvidenceRow | null>(null);
const warnings = ref<string[]>([]);
const scanning = ref(false);
const scanned = ref(false);
const detailError = ref('');
const evidenceError = ref('');
const detailBusy = ref(false);
const evidenceBusy = ref(false);
const query = ref('');
const verdictFilter = ref('');
const view = ref<keyof typeof views>('events');
const actorKey = ref('');
const eventQuery = ref('');
const eventPage = ref(0);
const timelineSelection = shallowRef<ScenarioTimelineSelection | null>(null);
const selectedNodeId = ref('');
const traceFilter = reactive(createDefaultSkillAnalysisFilter());
let directoryVersion = 0;
let selectionVersion = 0;

const counts = computed(() => (run.value?.cases || []).reduce<Record<string, number>>((result, item) => {
  const status = verdictKey(item.status);
  result[status] = (result[status] || 0) + 1;
  return result;
}, {}));
const verdictDistribution = computed(() => Object.entries(verdictLabels).map(([status, label]) => ({ status, label, count: counts.value[status] || 0 }))
  .filter(item => item.status !== 'unknown' || item.count || verdictFilter.value === 'unknown'));
const activeVerdicts = computed(() => verdictDistribution.value.filter(item => item.count));
const availableEvidenceCount = computed(() => run.value?.cases.filter(item => run.value?.files.has(item.diagnosticUri)).length ?? 0);
const filteredCases = computed(() => (run.value?.cases || []).filter(item =>
  (!verdictFilter.value || verdictKey(item.status) === verdictFilter.value || (verdictFilter.value === 'investigate' && ['failed', 'error'].includes(item.status))) &&
  `${item.caseId} ${item.scenario}`.toLowerCase().includes(query.value.toLowerCase())));
const assertions = computed(() => row(detail.value?.assertions));
const checks = computed(() => {
  const result = row(assertions.value.result);
  return [
    { label: 'Trace', matched: number(result.matchedExpectedTraceNodeCount), expected: number(result.expectedTraceNodeCount) },
    { label: '动作', matched: number(result.executedExpectedActionCount), expected: number(result.expectedActionCount) },
    { label: '关系', matched: number(result.satisfiedRelationshipCount), expected: number(result.expectedRelationshipCount) },
    { label: '状态', matched: number(result.satisfiedStateCount), expected: number(result.expectedStateCount) },
    { label: '上下文', matched: number(result.satisfiedContextCount), expected: number(result.expectedContextCount) }
  ].filter(check => check.expected > 0);
});
const tickRate = computed(() => number(assertions.value.tickRate));
const trace = computed(() => scenarioTrace(battle.value || {}, tickRate.value));
const traceNodes = computed(() => flattenSkillAnalysisTree(buildSkillAnalysisTree(trace.value.records, 'console-scenario', entry.value?.caseId)));
const traceIds = computed(() => new Set(traceNodes.value.map(node => node.nodeId)));
const traceOptions = computed(() => buildSkillAnalysisFilterOptions(traceNodes.value));
const filteredTrace = computed(() => filterSkillAnalysisNodes(traceNodes.value, traceFilter));
const traceTimeline = computed(() => buildTimelineFromAnalysisNodes(filteredTrace.value));
const traceRelations = computed(() => buildSkillAnalysisEntityRelations(filteredTrace.value));
const selectedNode = computed(() => filteredTrace.value.find(node => node.nodeId === selectedNodeId.value) || null);

const events = computed(() => rows(row(battle.value?.events).items).sort((left, right) => number(left.frame) - number(right.frame) || number(left.sequence) - number(right.sequence)));
const eventTimeline = computed(() => buildScenarioEventTimeline(events.value));
const matchingEvents = computed(() => events.value.filter(event =>
  (!timelineSelection.value || matchesScenarioTimelineSelection(event, timelineSelection.value)) &&
  `${eventFrameLabel(event)} ${scenarioEventName(event)} ${display(event.configId)} ${display(event.sourceActorId)} ${display(event.targetActorId)} ${display(event.summary)}`.toLowerCase().includes(eventQuery.value.toLowerCase())));
const eventPageSize = 50;
const eventPages = computed(() => Math.max(1, Math.ceil(matchingEvents.value.length / eventPageSize)));
const eventPageRows = computed(() => matchingEvents.value.slice(eventPage.value * eventPageSize, (eventPage.value + 1) * eventPageSize));
watch([eventQuery, timelineSelection], () => { eventPage.value = 0; });

const actors = computed(() => rows(row(battle.value?.state).actors).filter(actor => id(actor.actorId)));
const keyOfActor = (item: EvidenceRow) => `${id(item.actorId)}:${id(item.generation)}`;
const actorLabel = (item: EvidenceRow) => `${text(item.displayName) || actorKinds[number(item.kind)] || '实体'} #${display(item.actorId)} · 队伍 ${display(item.teamId)} · 配置 ${display(item.configId)}`;
const actor = computed(() => actors.value.find(item => keyOfActor(item) === actorKey.value) || null);
const actorHealthRatio = computed(() => {
  const current = actor.value?.health;
  const maximum = actor.value?.maximumHealth;
  return typeof current === 'number' && Number.isFinite(current) && typeof maximum === 'number' && Number.isFinite(maximum) && maximum > 0
    ? Math.max(0, Math.min(1, current / maximum)) : null;
});
const actorAttributes = computed(() => rows(row(battle.value?.attributes).items).filter(item => actor.value && findActor(item.actorId, item.generation) === actor.value));
const objects = computed(() => rows(row(battle.value?.objects).items).filter(item => !actor.value ||
  [item.relatedActorId, item.ownerActorId, item.sourceActorId, item.targetActorId].some(value => findActor(value, 0) === actor.value)));

function findActor(actorId: unknown, generation: unknown) {
  if (!id(actorId)) return null;
  const matches = actors.value.filter(item => id(item.actorId) === id(actorId) && (!id(generation) || id(item.generation) === id(generation)));
  return matches.length === 1 ? matches[0] : null;
}

function openActor(actorId: unknown, generation: unknown) {
  const item = findActor(actorId, generation);
  if (item) { actorKey.value = keyOfActor(item); view.value = 'actors'; }
}

function openTrace(context: unknown) {
  Object.assign(traceFilter, createDefaultSkillAnalysisFilter());
  selectedNodeId.value = String(id(context));
  view.value = 'trace';
}

function selectTimelineRange(selection: ScenarioTimelineSelection) {
  const current = timelineSelection.value;
  timelineSelection.value = current?.lane === selection.lane && current.start === selection.start && current.end === selection.end ? null : selection;
}

function clearSelection() {
  selectionVersion++;
  entry.value = detail.value = battle.value = null;
  detailError.value = evidenceError.value = '';
  detailBusy.value = evidenceBusy.value = false;
  actorKey.value = eventQuery.value = selectedNodeId.value = '';
  eventPage.value = 0;
  timelineSelection.value = null;
  view.value = 'events';
  Object.assign(traceFilter, createDefaultSkillAnalysisFilter());
}

function selectRun(runId: string) {
  clearSelection();
  run.value = runs.value.find(candidate => candidate.runId === runId) || null;
}

async function chooseDirectory(event: Event) {
  const input = event.target as HTMLInputElement;
  const files = Array.from(input.files || []);
  input.value = '';
  if (!files.length) return;
  const version = ++directoryVersion;
  selectRun('');
  warnings.value = [];
  runs.value = [];
  scanning.value = scanned.value = true;
  try {
    const result = await scanScenarioArchives(files);
    if (version !== directoryVersion) return;
    runs.value = result.runs;
    warnings.value = result.warnings;
    selectRun(result.runs[0]?.runId || '');
  } catch (error) {
    if (version === directoryVersion) warnings.value = [errorText(error)];
  } finally {
    if (version === directoryVersion) scanning.value = false;
  }
}

async function selectCase(candidate: ScenarioArchiveCase) {
  const selectedRun = run.value;
  if (!selectedRun) return;
  clearSelection();
  const version = selectionVersion;
  entry.value = candidate;
  detailBusy.value = true;
  try {
    const result = await readScenarioVerdict(selectedRun, candidate);
    if (version === selectionVersion) detail.value = result;
  } catch (error) {
    if (version === selectionVersion) detailError.value = errorText(error);
  } finally {
    if (version === selectionVersion) detailBusy.value = false;
  }
}

async function openEvidence() {
  const selectedRun = run.value;
  const selectedCase = entry.value;
  if (!selectedRun || !selectedCase || evidenceBusy.value) return;
  const version = selectionVersion;
  evidenceError.value = '';
  evidenceBusy.value = true;
  try {
    const result = await readScenarioEvidence(selectedRun, selectedCase);
    if (version === selectionVersion) battle.value = result;
  } catch (error) {
    if (version === selectionVersion) evidenceError.value = errorText(error);
  } finally {
    if (version === selectionVersion) evidenceBusy.value = false;
  }
}
</script>

<style scoped>
.scenario-archive { min-width: 0; }
.archive-picker { display: grid; gap: .35rem; }
.archive-controls, .archive-counts, .archive-actions { display: flex; flex-wrap: wrap; gap: .6rem; align-items: center; margin-block: .75rem; }
.archive-controls > label, .archive-field { display: grid; gap: .25rem; min-width: 10rem; }
.archive-controls > label:first-child { flex: 1; min-width: min(20rem, 100%); }
.archive-controls select { width: 100%; }
.archive-cases { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(14rem, 100%), 1fr)); gap: .4rem; max-height: 16rem; overflow-y: auto; }
.archive-case { display: grid; gap: .25rem; text-align: left; overflow-wrap: anywhere; }
.archive-case span { font-size: .8rem; color: var(--verdict-color); }
.verdict-overview { padding: .85rem; margin-block: .75rem; background: var(--panel); border: 1px solid var(--line-soft); border-radius: 14px; }
.verdict-heading { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: baseline; gap: .4rem; }
[data-verdict] { --verdict-color: var(--muted); }
[data-verdict="passed"] { --verdict-color: var(--ok); }
[data-verdict="failed"] { --verdict-color: var(--bad); }
[data-verdict="error"] { --verdict-color: var(--warn); }
[data-verdict="no-assertions"] { --verdict-color: var(--accent); }
.verdict-strip { display: flex; overflow: hidden; height: .45rem; margin-block: .7rem; border-radius: 8px; }
.verdict-strip span { flex-basis: 0; background: var(--verdict-color); }
.verdict-cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(7rem, 1fr)); gap: .5rem; }
.verdict-card { display: flex; justify-content: space-between; align-items: center; text-align: left; gap: .45rem; }
.verdict-card strong { color: var(--verdict-color); font-variant-numeric: tabular-nums; }
.actor-vitals { display: flex; flex-wrap: wrap; align-items: center; gap: .65rem; }
.actor-vitals meter { width: min(14rem, 100%); }
button[aria-pressed="true"] { outline: 2px solid currentColor; outline-offset: -2px; }
.archive-table { overflow: auto; max-height: 25rem; margin-block: .6rem; }
table { width: 100%; border-collapse: collapse; text-align: left; }
td, th, caption { padding: .5rem; border-bottom: 1px solid color-mix(in srgb, currentColor 20%, transparent); }
caption { text-align: left; }
pre { max-height: 20rem; overflow: auto; white-space: pre-wrap; overflow-wrap: anywhere; }
</style>
