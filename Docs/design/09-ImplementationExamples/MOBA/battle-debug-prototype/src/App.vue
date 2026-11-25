<script setup lang="ts">
import { computed, ref } from 'vue';
import { actors, actorAttributes, attributeModifiers, battleEvents, buffs, runtimeObjects, traceNodes, triggerAggregateGroups, type ActorEntity, type AttributeModifier, type BattleEvent, type BuffEntity, type EntityMode, type RuntimeEntity, type TriggerAggregateGroup } from './mock-data';

type ModuleItem = { id: string; label: string; count?: string; liveOnly?: boolean };
type Workspace = { id: string; label: string; description: string; modules: ModuleItem[] };

const workspaces: Workspace[] = [
  { id: 'actors', label: '角色检查', description: '单位身份、状态与来源', modules: [
    { id: 'actor-browser', label: 'Actor 浏览器', count: '8' },
    { id: 'actor-overview', label: '角色概览' },
    { id: 'attributes', label: '属性与来源' },
    { id: 'buffs', label: 'Buff 生命周期', count: '3' },
    { id: 'effects', label: '效果实例' },
    { id: 'tags', label: '标签与门禁' }
  ] },
  { id: 'runtime', label: '施法与对象', description: '施法进度、对象目录与生命周期', modules: [
    { id: 'skill-runtimes', label: '施法运行时', liveOnly: true },
    { id: 'runtime-objects', label: '运行时对象目录', count: '5' },
    { id: 'active-objects', label: '活跃对象监视', liveOnly: true }
  ] },
  { id: 'investigation', label: '事件调查', description: '从事件结果追到执行因果', modules: [
    { id: 'events', label: '战斗事件', count: '126' },
    { id: 'trace', label: '执行 Trace' }
  ] },
  { id: 'framesync', label: '帧同步', description: '预测、回滚与网络状态', modules: [
    { id: 'sync-overview', label: '同步总览', liveOnly: true },
    { id: 'prediction', label: '预测分析' },
    { id: 'rollback', label: '回滚分析' },
    { id: 'reconcile', label: '对账与控制', liveOnly: true },
    { id: 'time-sync', label: '时间同步', liveOnly: true },
    { id: 'network', label: '网络缓冲' }
  ] },
  { id: 'data', label: '数据与回放', description: '诊断来源、离线证据与复现', modules: [
    { id: 'health', label: '采集健康' },
    { id: 'state', label: '诊断快照' },
    { id: 'artifact', label: '诊断文件' },
    { id: 'replay', label: '录像回放' }
  ] },
  { id: 'authoring', label: '技能设计', description: '技能作用说明、流程编排与配置引用', modules: [
    { id: 'skill-explainer', label: '技能流程工作台', count: '26' }
  ] }
];

const requestedWorkspaceId = new URLSearchParams(window.location.search).get('workspace');
const requestedModuleId = new URLSearchParams(window.location.search).get('module');
const initialWorkspace = workspaces.find((workspace) => workspace.id === requestedWorkspaceId) ??
  workspaces.find((workspace) => workspace.modules.some((module) => module.id === requestedModuleId)) ??
  workspaces[0];
const initialModule = initialWorkspace.modules.find((module) => module.id === requestedModuleId) ?? initialWorkspace.modules[0];
const activeWorkspaceId = ref(initialWorkspace.id);
const activeModuleId = ref(initialModule.id);
const mode = ref<EntityMode>('actor');
const search = ref('');
const scope = ref<'selected' | 'recent' | 'all'>('all');
const kindFilter = ref('all');
const teamFilter = ref('all');
const statusFilter = ref('all');
const selectedActorId = ref(1042);
const selectedRuntimeId = ref('projectile:882:1');
const selectedBuffId = ref('buff:771:3');
const selectedEventId = ref('evt-1842-021');
const selectedTraceId = ref('trace:cast-424');
const selectedFrame = ref(1842);
const showAdvanced = ref(false);
const eventOutcome = ref('all');
const triggerFocus = ref<'issues' | 'all'>('issues');
const selectedAttributeId = ref(2);
const attributeView = ref<'values' | 'modifiers'>('values');
const attributeSearch = ref('');
const attributeSourceFilter = ref('all');
const attributeActiveOnly = ref(false);
const attributeRecording = ref(true);
const attributeSampleInterval = ref(1);
const showExpectedAttribute = ref(false);
const expectedAttributeValue = ref(84.5);
const skillView = ref<'capability' | 'lifecycle' | 'changes' | 'flow' | 'timeline' | 'trigger' | 'references'>('capability');
const skillFlowDraft = ref({ resourceType: 2, triggerId: 900101011, commitId: 'cast', timelineDurationMs: 5042, effectId: 30070301, effectAtMs: 250, abortOnFailure: true, eventTag: 'lux_30070301_30070301' });
const initialSkillFlowDraft = { ...skillFlowDraft.value };
const selectedSkillNodeId = ref('timeline');
const skillDraftChanged = ref(false);
const selectedSkillVariant = ref('modifier');
const skillChangeExamples = [
  { id: 'modifier', label: '数值强化', type: 'Modifier', before: '护盾基础值 40-100', after: 'Modifier 改写最终护盾值', detail: '展示 Modifier 来源、加算/乘算/覆盖顺序、条件与最终投影值；只改数值时不应伪造成新增对象。' },
  { id: 'extra-projectile', label: '额外投射物', type: '追加对象', before: '发射 1 个 Projectile', after: '增加 1 个 Projectile 子分支', detail: '新增发射动作或弹幕配置；每个子对象仍需独立显示碰撞、命中回调与销毁规则。' },
  { id: 'extra-effect', label: '追加效果', type: '追加 Action', before: '命中后：护盾', after: '命中后：护盾 + 额外效果', detail: '在原触发计划后追加 Action，展示条件、目标和失败策略，不覆盖已有效果。' },
  { id: 'replace-effect', label: '替换效果', type: '替换引用', before: '命中回调 → 原 Effect', after: '命中回调 → 替换后的 Effect', detail: '显示被替换节点、替换来源和受影响对象分支；不能与“追加效果”混成同一种变更。' },
  { id: 'summon', label: '生成召唤物', type: '新增子实体', before: '技能效果结束', after: '生成 Summon → 行为 / 受击 / 结束', detail: '召唤物作为技能派生对象，继续展开自身 AI、攻击触发、存活时长、死亡与召回链路。' }
];
const selectedSkillChange = computed(() => skillChangeExamples.find((item) => item.id === selectedSkillVariant.value)!);
const skillTriggerDetails = [
  { id: 30070301, name: 'W launch and self shield', entry: '技能 Effect 调用', condition: '无额外条件', actions: '自身加盾 → 播放表现 → 发射 Projectile #32070301' },
  { id: 30070311, name: 'W ally contact', entry: 'Projectile 命中回调', condition: '友方碰撞规则通过', actions: '为命中目标添加 Shield #30070390' },
  { id: 30070312, name: 'W successful return', entry: 'Projectile 退出回调', condition: 'projectile_exit_reason = 5', actions: '成功返程时为自身添加 Shield #30070390' }
];
const selectedSkillTriggerId = ref(30070301);
const selectedSkillTrigger = computed(() => skillTriggerDetails.find((trigger) => trigger.id === selectedSkillTriggerId.value)!);
const skillFlowNodes = computed(() => [
  { id: 'economy', kind: 'Economy', title: '资源预留', subtitle: '读取技能资源消耗 · 预留法力', ref: 'SkillFlow / Economy', state: 'complete' },
  { id: 'release', kind: 'RulePlan', title: '释放校验', subtitle: `Trigger Plan #${skillFlowDraft.value.triggerId}`, ref: 'SkillFlow / RulePlan', state: 'complete' },
  { id: 'commit', kind: 'CommitPoint', title: '提交施法', subtitle: `CommitId: ${skillFlowDraft.value.commitId}`, ref: 'SkillFlow / CommitPoint', state: 'complete' },
  { id: 'timeline', kind: 'Timeline', title: '时间轴阶段', subtitle: `持续 ${skillFlowDraft.value.timelineDurationMs} ms · 1 个 Effect 事件`, ref: 'SkillFlow / Timeline', state: 'selected' },
  { id: 'effect', kind: 'ExecuteEffect', title: '执行技能效果', subtitle: `t = ${skillFlowDraft.value.effectAtMs} ms · Effect #${skillFlowDraft.value.effectId}`, ref: 'ActionEditor / Submit', state: 'linked' }
]);
const selectedSkillNode = computed(() => skillFlowNodes.value.find((node) => node.id === selectedSkillNodeId.value) ?? skillFlowNodes.value[0]);
const entityKinds = ['all', 'Hero', 'Minion', 'Summon', 'Neutral', 'Unknown'];
const teams = ['all', 'Blue', 'Red', 'Neutral', 'Unknown'];
const statuses = ['all', 'alive', 'dead', 'removed', 'unknown'];
const runtimeKinds = ['all', 'Projectile', 'Area', 'Summon', 'Effect'];

const activeWorkspace = computed(() => workspaces.find((item) => item.id === activeWorkspaceId.value)!);
const activeModule = computed(() => activeWorkspace.value.modules.find((item) => item.id === activeModuleId.value)!);
const selectedActor = computed(() => actors.find((actor) => actor.id === selectedActorId.value));
const selectedRuntime = computed(() => runtimeObjects.find((item) => item.id === selectedRuntimeId.value));
const selectedBuff = computed(() => buffs.find((item) => item.id === selectedBuffId.value));
const selectedEvent = computed(() => battleEvents.find((item) => item.id === selectedEventId.value));
const selectedAttribute = computed(() => actorAttributes.find((item) => item.id === selectedAttributeId.value) ?? actorAttributes[0]);
const selectedAttributeModifiers = computed(() => attributeModifiers.filter((modifier) => modifier.attributeId === selectedAttribute.value.id));
const visibleActorAttributes = computed(() => actorAttributes.filter((attribute) => !attributeSearch.value || `${attribute.id} ${attribute.name}`.toLowerCase().includes(attributeSearch.value.trim().toLowerCase())));
const visibleAttributeModifiers = computed(() => attributeModifiers.filter((modifier) => {
  const searchText = `${modifier.sourceName} ${modifier.sourceId} ${modifier.attributeId} ${actorAttributes.find((item) => item.id === modifier.attributeId)?.name ?? ''}`.toLowerCase();
  return (!attributeSearch.value || searchText.includes(attributeSearch.value.trim().toLowerCase())) &&
    (attributeSourceFilter.value === 'all' || modifier.sourceKind === attributeSourceFilter.value) &&
    (!attributeActiveOnly.value || modifier.sourceState === 'active');
}));
const attributeChartSamples = computed(() => {
  const samples = selectedAttribute.value.history;
  const interval = Math.max(1, attributeSampleInterval.value);
  return samples.filter((_, index) => index % interval === 0 || index === samples.length - 1);
});
const attributeChartMin = computed(() => Math.min(...attributeChartSamples.value.flatMap((sample) => [sample.baseValue, sample.finalValue]), ...(showExpectedAttribute.value ? [expectedAttributeValue.value] : [])));
const attributeChartMax = computed(() => Math.max(...attributeChartSamples.value.flatMap((sample) => [sample.baseValue, sample.finalValue]), ...(showExpectedAttribute.value ? [expectedAttributeValue.value] : [])));
const normalizedSearch = computed(() => search.value.trim().toLowerCase());
const currentRows = computed(() => mode.value === 'actor' ? filteredActors.value : filteredRuntimeObjects.value);

const filteredActors = computed(() => actors.filter((actor) => {
  const text = [actor.id, actor.name, actor.kind, actor.config, actor.team, actor.source].join(' ').toLowerCase();
  return (!normalizedSearch.value || text.includes(normalizedSearch.value)) &&
    (scope.value === 'all' || (scope.value === 'recent' && actor.recent) || (scope.value === 'selected' && actor.id === selectedActorId.value)) &&
    (kindFilter.value === 'all' || actor.kind === kindFilter.value) &&
    (teamFilter.value === 'all' || actor.team === teamFilter.value) &&
    (statusFilter.value === 'all' || actor.status === statusFilter.value);
}));

const filteredRuntimeObjects = computed(() => runtimeObjects.filter((item) => {
  const text = [item.id, item.name, item.kind, item.definition, item.trace].join(' ').toLowerCase();
  return (!normalizedSearch.value || text.includes(normalizedSearch.value)) &&
    (kindFilter.value === 'all' || item.kind === kindFilter.value) &&
    (statusFilter.value === 'all' || item.status === statusFilter.value);
}));

const filteredEvents = computed(() => battleEvents.filter((event) => {
  const text = [event.id, event.actorName, event.kind, event.summary, event.traceId].join(' ').toLowerCase();
  return (!normalizedSearch.value || text.includes(normalizedSearch.value)) &&
    (eventOutcome.value === 'all' || event.outcome === eventOutcome.value);
}));
const sortedTriggerGroups = computed(() => triggerAggregateGroups
  .filter((group) => triggerFocus.value === 'all' || group.result === 'Failed' || group.result === 'Blocked')
  .slice()
  .sort((left, right) => {
    const priority = (group: TriggerAggregateGroup) => group.result === 'Failed' ? 0 : group.result === 'Blocked' ? 1 : 2;
    return priority(left) - priority(right) || right.occurrenceCount - left.occurrenceCount || left.firstFrame - right.firstFrame;
  }));
const triggerIssueGroupCount = computed(() => triggerAggregateGroups.filter((group) => group.result === 'Failed' || group.result === 'Blocked').length);
const repeatedTriggerGroupCount = computed(() => triggerAggregateGroups.filter((group) => group.occurrenceCount > 1).length);
const relatedTraceNodes = computed(() => traceNodes.filter((node) => node.id === selectedTraceId.value || node.parentId === selectedTraceId.value));

function attributeChartPath(key: 'baseValue' | 'finalValue') {
  const samples = attributeChartSamples.value;
  const range = Math.max(0.001, attributeChartMax.value - attributeChartMin.value);
  return samples.map((sample, index) => {
    const x = 48 + (index / Math.max(1, samples.length - 1)) * 684;
    const y = 178 - ((sample[key] - attributeChartMin.value) / range) * 150;
    return `${index === 0 ? 'M' : 'L'} ${x.toFixed(1)} ${y.toFixed(1)}`;
  }).join(' ');
}

function attributeChartY(value: number) {
  const range = Math.max(0.001, attributeChartMax.value - attributeChartMin.value);
  return (178 - ((value - attributeChartMin.value) / range) * 150).toFixed(1);
}

function formatAttributeValue(value: number | null) {
  return value === null ? '未采集' : Number.isInteger(value) ? value.toLocaleString('en-US') : value.toFixed(2).replace(/0+$/, '').replace(/\.$/, '');
}

function attributeDelta(attributeId: number) {
  const attribute = actorAttributes.find((item) => item.id === attributeId);
  return attribute ? attribute.finalValue - attribute.baseValue : 0;
}

function modifierOperation(modifier: AttributeModifier) {
  const values = { add: '加法', multiply: '乘算', override: '覆盖', addPercent: '百分比' };
  return values[modifier.operation];
}

function modifierFormula(modifier: AttributeModifier) {
  const attribute = actorAttributes.find((item) => item.id === modifier.attributeId);
  const name = attribute?.name ?? '属性';
  if (modifier.operation === 'multiply') return `${name} × ${formatAttributeValue(modifier.magnitude)}`;
  if (modifier.operation === 'addPercent') return `${name} + ${formatAttributeValue(modifier.magnitude)}%`;
  if (modifier.operation === 'override') return `${name} = ${formatAttributeValue(modifier.magnitude)}`;
  return `${name} + ${formatAttributeValue(modifier.magnitude)}`;
}

function openWorkspace(workspace: Workspace) {
  activeWorkspaceId.value = workspace.id;
  activeModuleId.value = workspace.modules[0].id;
  search.value = '';
}

function openModule(id: string) {
  activeModuleId.value = id;
  search.value = '';
  kindFilter.value = 'all';
  statusFilter.value = 'all';
}

function updateSkillDraft() {
  skillDraftChanged.value = true;
}

function discardSkillDraft() {
  skillFlowDraft.value = { ...initialSkillFlowDraft };
  skillDraftChanged.value = false;
}

function saveSkillDraft() {
  skillDraftChanged.value = false;
}

function openSkillTrigger(triggerId: number) {
  selectedSkillTriggerId.value = triggerId;
  skillView.value = 'trigger';
}

function selectSkillNode(id: string) {
  selectedSkillNodeId.value = id;
  if (id === 'economy' || id === 'release' || id === 'commit') skillView.value = 'flow';
  else if (id === 'effect') skillView.value = 'timeline';
}

function openBuffs(actorId = selectedActorId.value, sourceId?: number) {
  selectedActorId.value = actorId;
  if (sourceId !== undefined) {
    const sourceBuff = buffs.find((item) => item.id.startsWith(`buff:${sourceId}:`));
    if (sourceBuff) selectedBuffId.value = sourceBuff.id;
  }
  activeWorkspaceId.value = 'actors';
  openModule('buffs');
}

function openTrace(traceId: string) {
  selectedTraceId.value = traceId;
  activeWorkspaceId.value = 'investigation';
  openModule('trace');
}

function openEvents(traceId?: string, actorId?: number) {
  if (traceId) selectedTraceId.value = traceId;
  if (actorId) selectedActorId.value = actorId;
  activeWorkspaceId.value = 'investigation';
  openModule('events');
}

function selectActor(actor: ActorEntity) {
  selectedActorId.value = actor.id;
}

function selectRuntime(item: RuntimeEntity) {
  selectedRuntimeId.value = item.id;
}

function selectEvent(event: BattleEvent) {
  selectedEventId.value = event.id;
  selectedTraceId.value = event.traceId;
  selectedFrame.value = event.frame;
  selectedActorId.value = event.actorId;
}

function openTriggerSample(group: TriggerAggregateGroup) {
  const event = battleEvents.find((item) => item.id === group.sampleEventId);
  if (event) selectEvent(event);
}

function triggerStageLabel(stage: string) {
  const labels: Record<string, string> = { Budget: '预算', Conditions: '条件', Plan: '计划', Execution: '执行' };
  return labels[stage] ?? stage;
}

function triggerResultLabel(result: string) {
  const labels: Record<string, string> = { Passed: '通过', Failed: '失败', Blocked: '拦截', Skipped: '跳过' };
  return labels[result] ?? result;
}

function triggerRateLabel(group: TriggerAggregateGroup) {
  const frames = Math.max(1, group.lastFrame - group.firstFrame + 1);
  return `${(group.occurrenceCount / frames).toFixed(1)} 次 / 帧`;
}

function clearFilters() {
  search.value = '';
  scope.value = 'all';
  kindFilter.value = 'all';
  teamFilter.value = 'all';
  statusFilter.value = 'all';
  eventOutcome.value = 'all';
}

function displayName(actor: ActorEntity) {
  return actor.name || (actor.kind === 'Unknown' ? '未命名 Actor' : actor.kind);
}

function healthLabel(actor: ActorEntity) {
  if (actor.health === null || actor.maxHealth === null) return '未采集';
  return `${actor.health} / ${actor.maxHealth}`;
}

function statusLabel(status: string) {
  const labels: Record<string, string> = { alive: '存活', dead: '死亡', removed: '已移除', unknown: '未知', active: '进行中', ended: '已结束', partial: '部分', complete: '完整', expiring: '即将结束', success: '成功', rejected: '已拒绝', failed: '失败', running: '进行中' };
  return labels[status] ?? status;
}

function kindLabel(kind: string) {
  const labels: Record<string, string> = { Hero: '英雄', Minion: '小兵', Summon: '召唤物', Neutral: '中立单位', Unknown: '类型未知', Projectile: '投射物', Area: '区域', Effect: '效果' };
  return labels[kind] ?? kind;
}
</script>

<template>
  <div class="app-shell">
    <header class="topbar">
      <div class="brand-lockup"><div class="brand-mark">AK</div><div><div class="eyebrow">ABILITYKIT / MOBA</div><div class="brand-title">战斗诊断工作台</div></div></div>
      <div class="session-strip"><span class="live-dot"></span><span>实时会话</span><span class="divider"></span><span>World-07</span><span>Battle #M-2048</span><span class="frame-pill">FRAME {{ selectedFrame }}</span></div>
      <button class="icon-button dark" type="button" title="刷新数据" aria-label="刷新数据">↻</button>
      <button class="quiet-button" type="button" @click="clearFilters">重置筛选</button>
    </header>

    <div class="workspace-switcher">
      <button v-for="workspace in workspaces" :key="workspace.id" type="button" :class="{ active: activeWorkspaceId === workspace.id }" @click="openWorkspace(workspace)">{{ workspace.label }}</button>
      <div class="switcher-spacer"></div>
      <button class="preset-button" type="button" @click="activeWorkspaceId = 'investigation'; openModule('events')">进入战斗调查</button>
    </div>

    <div class="app-body">
      <aside class="module-rail">
        <div class="rail-heading"><span class="panel-kicker">{{ activeWorkspace.label }}</span><span class="module-total">{{ activeWorkspace.modules.length }} 项</span></div>
        <p class="rail-description">{{ activeWorkspace.description }}</p>
        <nav class="module-list" aria-label="功能模块">
          <button v-for="module in activeWorkspace.modules" :key="module.id" type="button" :class="{ selected: activeModuleId === module.id }" @click="openModule(module.id)">
            <span class="module-mark"></span><span class="module-name">{{ module.label }}</span><span v-if="module.count" class="module-count">{{ module.count }}</span><span v-if="module.liveOnly" class="live-only">LIVE</span>
          </button>
        </nav>
        <div class="rail-bottom"><div class="capability-title">当前数据能力</div><span class="capability-chip supported">Actor 状态</span><span class="capability-chip supported">事件 / Trace</span><span class="capability-chip limited">对象来源 · 部分</span></div>
      </aside>

      <main class="main-content">
        <section class="context-bar">
          <template v-if="activeWorkspaceId === 'authoring'"><div class="context-primary"><span class="context-label">当前技能</span><button type="button" class="context-link skill-context-link">Lux W · #30070301 <span>▾</span></button><span class="context-separator"></span><span class="context-label">配置来源</span><strong class="frame-value">Unity Authoring / MOBA</strong></div><div class="context-meta"><span class="source-tag draft-tag">{{ skillDraftChanged ? '未保存草稿' : '本地配置快照' }}</span><span>只读来源 · 原型编辑</span></div></template>
          <template v-else><div class="context-primary"><span class="context-label">当前对象</span><button type="button" class="context-link" @click="activeWorkspaceId = 'actors'; openModule('actor-browser')">{{ selectedActor ? `${displayName(selectedActor)} · #${selectedActor.id}` : '未选择 Actor' }} <span>▾</span></button><span class="context-separator"></span><span class="context-label">诊断帧</span><input v-model.number="selectedFrame" type="range" min="1800" max="1860" /><strong class="frame-value">{{ selectedFrame }}</strong></div><div class="context-meta"><span class="freshness-dot"></span><span>最后更新 0.2 秒前</span><span class="source-tag">LIVE</span></div></template>
        </section>

        <section class="page-heading">
          <div><div class="section-kicker">{{ activeWorkspace.label.toUpperCase() }} / {{ activeModule.label.toUpperCase() }}</div><h1>{{ activeModule.label }}</h1><p v-if="activeWorkspaceId === 'authoring'">从技能入口追踪派生对象、生命周期、战斗效果和强化替换关系。</p><p v-else>{{ activeWorkspace.description }}。仅在打开模块后加载对应视图。</p></div>
          <div class="heading-actions"><template v-if="activeWorkspaceId === 'authoring'"><button class="outline-button" type="button" @click="discardSkillDraft">放弃原型改动</button><button class="primary-button" type="button" @click="saveSkillDraft">保存草稿</button></template><template v-else><button class="outline-button" type="button" @click="showAdvanced = !showAdvanced">{{ showAdvanced ? '收起筛选' : '高级筛选' }}</button><button class="primary-button" type="button" @click="clearFilters">清空条件</button></template></div>
        </section>

        <template v-if="activeModuleId === 'skill-explainer'">
          <section class="skill-toolbar surface"><div class="skill-identity"><span class="skill-icon">W</span><div><span class="panel-kicker">ABILITY EXPLAIN · CONFIGURATION VIEW</span><h2>Lux W <small>技能 #30070301</small></h2><p>返回飞行投射物，为友方提供护盾 · 根据 Trigger 名称与当前配置引用关系汇总</p></div></div><div class="skill-toolbar-meta"><span class="source-tag">流程引用完整</span><button class="outline-button" type="button" @click="activeWorkspaceId = 'investigation'; openModule('events')">查看运行时诊断 →</button></div></section>
          <section class="skill-explainer-layout">
            <aside class="surface skill-library"><div class="list-toolbar"><div><span class="panel-kicker">SKILL INDEX</span><h2>技能目录</h2></div><span class="sort-note">26 项</span></div><label class="small-search skill-search"><span>⌕</span><input placeholder="搜索技能 / ID" /></label><div class="skill-library-group"><span>Lux · 4 个技能</span><button class="skill-library-row" type="button"><i>Q</i><span><strong>光之束缚</strong><small>#30070101</small></span></button><button class="skill-library-row" type="button"><i>E</i><span><strong>透光奇点</strong><small>#30070201</small></span></button><button class="skill-library-row selected" type="button"><i>W</i><span><strong>曲光屏障</strong><small>#30070301</small></span></button><button class="skill-library-row" type="button"><i>R</i><span><strong>终极闪光</strong><small>#30070401</small></span></button></div><div class="skill-source-note"><strong>当前依据</strong><span>SkillFlowCO · LuxSkillFlows.json</span><span>ActionEditor Logic Timeline</span><span>Trigger Authoring Source</span></div></aside>
            <section class="skill-main-column">
              <div class="skill-view-tabs" role="tablist" aria-label="技能解释视图"><button type="button" :class="{ selected: skillView === 'capability' }" @click="skillView = 'capability'">能力总览</button><button type="button" :class="{ selected: skillView === 'lifecycle' }" @click="skillView = 'lifecycle'">对象生命周期</button><button type="button" :class="{ selected: skillView === 'changes' }" @click="skillView = 'changes'">强化与替换</button><button type="button" :class="{ selected: skillView === 'flow' }" @click="skillView = 'flow'">施法流程</button><button type="button" :class="{ selected: skillView === 'timeline' }" @click="skillView = 'timeline'">时间轴</button><button type="button" :class="{ selected: skillView === 'trigger' }" @click="skillView = 'trigger'">Trigger Plan</button><button type="button" :class="{ selected: skillView === 'references' }" @click="skillView = 'references'">配置引用</button></div>
              <section v-if="skillView === 'capability'" class="surface skill-capability-panel">
                <div class="skill-panel-heading"><div><span class="panel-kicker">ABILITY GRAPH · CONFIGURED RELATIONSHIPS</span><h2>从施法入口到派生对象与战斗结果</h2></div><span class="source-tag">Lux W · 已核对配置</span></div>
                <div class="capability-root"><span class="capability-root-mark">W</span><div><small>技能实体 · #30070301</small><strong>曲光屏障</strong><span>CastFlow 负责施法阶段；Trigger / Projectile / Shield 构成后续效果链</span></div><button class="text-command" type="button" @click="skillView = 'flow'">查看施法阶段 →</button></div>
                <div class="capability-section-label"><span>施法管线</span><small>入口与时序 · 只占能力图的一条分支</small></div>
                <div class="capability-pipeline"><span>资源预留</span><i>→</i><span>RulePlan #900101011</span><i>→</i><span>Commit: cast</span><i>→</i><span>Timeline · 5042 ms</span></div>
                <div class="capability-section-label"><span>派生对象与影响战斗的效果</span><small>按对象关联回调与效果，不把所有内容压成单条时间线</small></div>
                <div class="capability-branches">
                  <article class="capability-card projectile-card"><div class="capability-card-head"><span class="object-kind projectile-kind">PROJECTILE</span><span class="source-tag">已配置</span></div><h3>Lux W 回程投射物 <small>#32070301</small></h3><p>发射器 #33070301 · 速度 24 · 最大距离 11.5 · 生命周期 5000 ms · Pierce</p><div class="capability-state-chain"><span>发射</span><i>→</i><span>飞行与碰撞</span><i>→</i><span>返程</span><i>→</i><span>退出 / 到期</span></div><div class="capability-callbacks"><button type="button" @click="openSkillTrigger(30070311)"><b>命中友方</b><span>Trigger #30070311 · 添加护盾</span><em>查看回调 →</em></button><button type="button" @click="openSkillTrigger(30070312)"><b>成功返程</b><span>Trigger #30070312 · 再次为自身加盾</span><em>查看回调 →</em></button></div></article>
                  <article class="capability-card effects-card"><div class="capability-card-head"><span class="object-kind effect-kind">COMBAT EFFECTS</span><span class="source-tag">已配置</span></div><h3>护盾效果 <small>#30070390</small></h3><p>持续 2500 ms · 技能等级基础值 40 / 55 / 70 / 85 / 100 · 次级系数引用属性 #5</p><div class="impact-list"><div><b>施法时</b><span>自身立即获得护盾</span><small>Trigger #30070301</small></div><div><b>投射物命中</b><span>命中的友方获得护盾</span><small>Trigger #30070311</small></div><div><b>投射物成功返程</b><span>自身获得返程护盾</span><small>Trigger #30070312</small></div></div></article>
                </div>
                <div class="capability-child-summary"><div><b>派生实体</b><strong>Projectile 1 类</strong><small>每次施法 CountPerShot = 1</small></div><div><b>效果实例</b><strong>护盾可由多个回调创建</strong><small>Buff / Shield 生命周期可继续向 Actor 追踪</small></div><div class="unconfigured-branch"><b>召唤物</b><strong>当前技能未配置</strong><small>能力图保留 Summon 扩展分支，不填造 Lux W 行为</small></div><button class="outline-button" type="button" @click="skillView = 'changes'">查看技能变化如何改写能力图 →</button></div>
                <div class="capability-legend"><span><i class="legend-configured"></i>权威配置已核对</span><span><i class="legend-inferred"></i>需 Resolver 补充结构化解释</span><span><i class="legend-variant"></i>设计变体样例</span></div>
              </section>
              <section v-else-if="skillView === 'lifecycle'" class="surface skill-lifecycle-panel">
                <div class="skill-panel-heading"><div><span class="panel-kicker">OBJECT LIFECYCLE · CHILD ENTITY GRAPH</span><h2>技能派生对象各自拥有生命周期</h2></div><span class="sort-note">配置关系视图 · 非战斗实例</span></div>
                <article class="lifecycle-object"><div class="lifecycle-object-title"><span class="object-kind projectile-kind">PROJECTILE</span><div><h3>Lux W 回程投射物 #32070301</h3><small>父技能 #30070301 · 发射器 #33070301 · 当前每次发射 1 个</small></div></div><div class="lifecycle-track"><div><b>创建</b><span>Trigger #30070301</span><small>生成并设定发射方向</small></div><i>→</i><div><b>飞行</b><span>速度 24 · 最大距离 11.5</span><small>最长存活 5000 ms</small></div><i>→</i><div><b>碰撞回调</b><span>OnHitEffect → #30070311</span><small>友方命中施加护盾</small></div><i>→</i><div><b>返程</b><span>Return speed 24</span><small>Exit → #30070312</small></div><i>→</i><div><b>结束</b><span>退出 / 到期</span><small>移除实例并保留诊断来源</small></div></div></article>
                <article class="lifecycle-object"><div class="lifecycle-object-title"><span class="object-kind effect-kind">SHIELD</span><div><h3>护盾实例 #30070390</h3><small>由施法、命中和返程回调分别创建；具体运行时实例依目标区分</small></div></div><div class="lifecycle-track shield-track"><div><b>施加</b><span>Self / Hit Actor</span><small>来源保留 Trigger 与投射物引用</small></div><i>→</i><div><b>生效中</b><span>持续 2500 ms</span><small>数值受技能等级与属性引用影响</small></div><i>→</i><div><b>吸收 / 到期</b><span>运行时状态</span><small>实例变化由战斗诊断提供</small></div><i>→</i><div><b>结束</b><span>Buff 生命周期</span><small>可跳转 Actor / Buff 诊断</small></div></div></article>
                <div class="summon-empty-state"><span class="object-kind summon-kind">SUMMON</span><div><strong>当前 Lux W 没有召唤物配置</strong><small>其他技能出现 Summon 时，在此增加独立分支：生成条件 → 实例身份 → AI / 技能 → 受击与属性 → 死亡、超时或召回。</small></div></div>
                <div class="skill-boundary-note"><strong>区分配置生命周期与运行时生命周期</strong><span>本页展示配置定义了哪些阶段与回调；某一局里的实体是否创建、何时命中/死亡、最终造成什么结果，必须从运行时实例和 Trace 读取，不能由配置图代替。</span></div>
              </section>
              <section v-else-if="skillView === 'changes'" class="surface skill-changes-panel">
                <div class="skill-panel-heading"><div><span class="panel-kicker">ABILITY VARIANTS · MODIFIERS / APPEND / REPLACE</span><h2>强化与替换如何改写技能能力</h2></div><span class="source-tag draft-tag">通用设计样例 · 未绑定 Lux W</span></div>
                <div class="change-scenario-list"><button v-for="example in skillChangeExamples" :key="example.id" type="button" :class="{ selected: selectedSkillVariant === example.id }" @click="selectedSkillVariant = example.id"><span>{{ example.type }}</span><strong>{{ example.label }}</strong></button></div>
                <div class="change-preview"><div class="change-preview-heading"><div><span class="panel-kicker">VARIANT DIFF PREVIEW</span><h3>{{ selectedSkillChange.label }}</h3></div><span class="source-tag draft-tag">仅用于界面验证</span></div><div class="change-diff"><article><small>基准技能能力</small><strong>{{ selectedSkillChange.before }}</strong><span>来源：基础 Skill / Trigger / Object 配置</span></article><i>→</i><article class="change-result"><small>应用变化后</small><strong>{{ selectedSkillChange.after }}</strong><span>{{ selectedSkillChange.detail }}</span></article></div><div class="change-impact-grid"><div><b>变化作用域</b><span>{{ selectedSkillChange.type }}</span></div><div><b>受影响分支</b><span>对象生成 / 生命周期 / 触发效果</span></div><div><b>应保留信息</b><span>来源、条件、顺序、覆盖关系与冲突</span></div></div></div>
                <div class="skill-boundary-note"><strong>强化不是单个数值字段</strong><span>正式解释应显示变化由谁提供、何时生效、改写哪些节点、额外创建哪些对象，以及变体撤销后回到哪条基准链。追加与替换必须分别投影成不同关系。</span></div>
              </section>
              <section v-if="skillView === 'flow'" class="surface skill-flow-panel"><div class="skill-panel-heading"><div><span class="panel-kicker">EXPLAIN FOREST · SKILL CAST FLOW</span><h2>从按下技能到效果触发</h2></div><span class="flow-source-badge">SkillFlow #30070301</span></div><div class="flow-canvas"><div class="flow-lane-label">CAST PIPELINE <small>按 Phase 顺序</small></div><div class="flow-nodes"><template v-for="(node, index) in skillFlowNodes" :key="node.id"><button type="button" class="flow-node" :class="[{ selected: selectedSkillNodeId === node.id }, node.id]" @click="selectSkillNode(node.id)"><span class="flow-node-kind">{{ node.kind }}</span><strong>{{ node.title }}</strong><small>{{ node.subtitle }}</small><span class="flow-node-source">{{ node.ref }}</span></button><span v-if="index < skillFlowNodes.length - 1" class="flow-edge" aria-hidden="true">→</span></template></div><div class="flow-lane-label second">触发后的效果解释 <small>当前仅显示配置引用</small></div><div class="effect-summary-node"><span class="effect-mark">✳</span><div><span class="flow-node-kind">TRIGGER #{{ skillFlowDraft.effectId }}</span><strong>Lux W launch and self shield</strong><small>投射物发射 · 自身护盾 · 由 Trigger Plan 条件与 Action 定义</small></div><button class="text-command" type="button" @click="skillView = 'trigger'">查看 Trigger 内容 →</button></div></div><div class="skill-flow-foot"><span><b>可解释结果</b>：技能输入 → 资源处理 → 释放规则 → 提交点 → Timeline → Trigger 效果</span><span>Effect ID 不是运行时对象实例</span></div></section>
              <section v-else-if="skillView === 'timeline'" class="surface skill-timeline-panel"><div class="skill-panel-heading"><div><span class="panel-kicker">ACTIONEDITOR · LOGIC TIMELINE</span><h2>技能时间轴</h2></div><div class="timeline-meta"><span>总长 5.041667 s</span><span>SkillFlow Duration 5042 ms</span></div></div><div class="skill-timeline-ruler"><span>0 ms</span><span>1250 ms</span><span>2500 ms</span><span>3750 ms</span><span>5042 ms</span></div><div class="skill-timeline-track"><span class="track-label">Logic / Submit</span><div class="track-content"><button class="timeline-event-marker" type="button" :style="{ left: `${(skillFlowDraft.effectAtMs / skillFlowDraft.timelineDurationMs) * 100}%` }" @click="selectSkillNode('effect')"><i></i><span>250 ms · ExecuteEffect</span><small>Effect #{{ skillFlowDraft.effectId }}</small></button></div></div><div class="timeline-warning"><span>!</span><div><strong>运行时效果入口</strong><small>该事件在 Frame 时间轴中执行 Effect；更深一层 Trigger 结果需以该 ID 对应的正式计划和执行诊断为准。</small></div><button class="text-command" type="button" @click="skillView = 'trigger'">查看关联计划 →</button></div></section>
              <section v-else-if="skillView === 'trigger'" class="surface skill-trigger-panel"><div class="skill-panel-heading"><div><span class="panel-kicker">TRIGGER AUTHORING · TRIGGER #{{ selectedSkillTrigger.id }}</span><h2>{{ selectedSkillTrigger.name }}</h2></div><span class="source-tag">Lux W · 配置摘要</span></div><div class="trigger-explain-summary"><div><span>触发入口</span><strong>{{ selectedSkillTrigger.entry }}</strong></div><div><span>判定条件</span><strong>{{ selectedSkillTrigger.condition }}</strong></div><div><span>配置来源</span><strong>moba-skills.trigger.json · #{{ selectedSkillTrigger.id }}</strong></div></div><div class="trigger-plan-flow"><div><b>Trigger Entry</b><small>{{ selectedSkillTrigger.entry }}</small></div><i>→</i><div><b>Conditions</b><small>{{ selectedSkillTrigger.condition }}</small></div><i>→</i><div><b>Actions</b><small>{{ selectedSkillTrigger.actions }}</small></div></div><div class="skill-boundary-note"><strong>解释边界</strong><span>摘要对应已核对的 Lux W 配置关系。正式工作台应由 Resolver 返回逐条件表达式、动作参数、执行结果与 SourceRef；本页不代表已接入实时配置解析。</span></div></section>
              <section v-else class="surface skill-references-panel"><div class="skill-panel-heading"><div><span class="panel-kicker">CONFIGURATION REFERENCES</span><h2>配置引用链</h2></div><span class="sort-note">4 个权威来源</span></div><div class="reference-row"><span class="reference-step">01</span><div><strong>技能定义</strong><small>技能 ID → CastFlowId</small></div><code>J-技能表.xlsx / skills.json</code><b>已链接</b></div><div class="reference-row"><span class="reference-step">02</span><div><strong>施法流程</strong><small>Economy → RulePlan → Commit → Timeline</small></div><code>SkillFlowCO.asset / LuxSkillFlows.json</code><b>已链接</b></div><div class="reference-row"><span class="reference-step">03</span><div><strong>逻辑时间轴</strong><small>250ms ExecuteEffect → Effect #{{ skillFlowDraft.effectId }}</small></div><code>skill_30070301.moba.logic.json</code><b>已链接</b></div><div class="reference-row"><span class="reference-step">04</span><div><strong>触发计划</strong><small>Trigger #{{ skillFlowDraft.effectId }} · Lux W launch and self shield</small></div><code>moba-skills.trigger.json</code><b>名称已核对</b></div><div class="reference-open-note">点击流程节点可在右侧检查器查看相关字段；网页原型只编辑本地草稿，不会回写以上来源。</div></section>
              <div class="skill-workspace-footer"><span>ABILITY EXPLAIN · Forest / Issues / Source / Actions 映射</span><span>实体关系只覆盖当前可见、可解析的配置引用</span></div>
            </section>
            <aside class="surface skill-inspector"><div class="skill-inspector-head"><div><span class="panel-kicker">CONFIG INSPECTOR</span><h2>{{ selectedSkillNode.title }}</h2></div><button class="icon-button" type="button" title="定位配置来源">↗</button></div><p class="skill-inspector-subtitle">{{ selectedSkillNode.kind }} · {{ selectedSkillNode.ref }}</p><template v-if="selectedSkillNodeId === 'economy'"><label class="skill-field"><span>资源类型</span><select v-model.number="skillFlowDraft.resourceType" @change="updateSkillDraft"><option :value="0">无资源</option><option :value="1">生命</option><option :value="2">法力</option></select></label><div class="inspector-source-line">来源：SkillFlow Phase Economy</div><label class="skill-check-row"><input type="checkbox" checked @change="updateSkillDraft" /><span>使用解析后的消耗与冷却</span></label></template><template v-else-if="selectedSkillNodeId === 'release'"><label class="skill-field"><span>Trigger Plan ID</span><input v-model.number="skillFlowDraft.triggerId" type="number" @input="updateSkillDraft" /></label><label class="skill-check-row"><input type="checkbox" checked @change="updateSkillDraft" /><span>失败时中断技能</span></label><div class="inspector-source-line">失败键：skill_release_failed</div></template><template v-else-if="selectedSkillNodeId === 'commit'"><label class="skill-field"><span>提交点</span><select v-model="skillFlowDraft.commitId" @change="updateSkillDraft"><option value="cast">cast · 施法提交</option><option value="attack">attack · 攻击提交</option></select></label><div class="inspector-source-line">要求已有资源预留 · 显式提交</div></template><template v-else-if="selectedSkillNodeId === 'timeline'"><label class="skill-field"><span>阶段持续时间 (ms)</span><input v-model.number="skillFlowDraft.timelineDurationMs" type="number" min="0" step="1" @input="updateSkillDraft" /></label><label class="skill-field"><span>事件数量</span><input :value="1" type="number" disabled /></label><label class="skill-check-row"><input v-model="skillFlowDraft.abortOnFailure" type="checkbox" @change="updateSkillDraft" /><span>效果失败时中断技能</span></label><div class="inspector-source-line">PhaseId：lux_30070301_timeline</div></template><template v-else><label class="skill-field"><span>事件时间 (ms)</span><input v-model.number="skillFlowDraft.effectAtMs" type="number" min="0" :max="skillFlowDraft.timelineDurationMs" @input="updateSkillDraft" /></label><label class="skill-field"><span>Effect ID</span><input v-model.number="skillFlowDraft.effectId" type="number" @input="updateSkillDraft" /></label><label class="skill-field"><span>事件标记</span><input v-model="skillFlowDraft.eventTag" type="text" @input="updateSkillDraft" /></label><div class="inspector-source-line">Logic Track：Submit / ExecuteEffect</div></template><div class="skill-inspector-divider"></div><div class="skill-issues-heading"><span class="panel-kicker">ISSUES & LINKS</span><span class="state-pill complete">无结构错误</span></div><button class="inspector-link-row" type="button" @click="skillView = selectedSkillNodeId === 'effect' ? 'trigger' : 'references'"><span><strong>{{ selectedSkillNodeId === 'effect' ? '关联 Trigger Plan' : '来源与引用' }}</strong><small>{{ selectedSkillNodeId === 'effect' ? `#${skillFlowDraft.effectId} · 查看解释` : selectedSkillNode.ref }}</small></span><b>查看 →</b></button><div class="skill-save-note"><span class="draft-dot"></span><span>{{ skillDraftChanged ? '存在未保存的本地草稿' : '字段变更只在当前页面生效' }}</span></div></aside>
          </section>
        </template>

        <template v-else-if="activeModuleId === 'actor-browser'">
          <section class="summary-strip"><div><span>世界 Actor</span><strong>{{ actors.length }}</strong></div><div><span>匹配结果</span><strong>{{ currentRows.length }}</strong></div><div><span>活动 Buff</span><strong>{{ buffs.filter((item) => item.status !== 'ended').length }}</strong></div><div class="summary-context"><span>当前选择</span><strong>{{ selectedActor ? `#${selectedActor.id} ${displayName(selectedActor)}` : '未选择' }}</strong></div></section>
          <section class="browser-layout">
            <aside class="filter-panel surface">
              <div class="panel-heading"><div><span class="panel-kicker">FILTERS</span><h2>筛选对象</h2></div><span class="count-badge">{{ currentRows.length }} / {{ mode === 'actor' ? actors.length : runtimeObjects.length }}</span></div>
              <div class="segmented mode-switch"><button type="button" :class="{ active: mode === 'actor' }" @click="mode = 'actor'; clearFilters()">Actor</button><button type="button" :class="{ active: mode === 'runtime' }" @click="mode = 'runtime'; clearFilters()">运行时对象</button></div>
              <label class="search-box"><span>⌕</span><input v-model="search" type="search" :placeholder="mode === 'actor' ? '名称、Actor ID、配置 ID' : '对象 ID、名称、定义 ID'" /><kbd>/</kbd></label>
              <div v-if="mode === 'actor'" class="filter-section"><div class="filter-label">范围</div><div class="segmented compact-segmented"><button v-for="item in ['selected', 'recent', 'all']" :key="item" type="button" :class="{ active: scope === item }" @click="scope = item as 'selected' | 'recent' | 'all'">{{ item === 'selected' ? '当前' : item === 'recent' ? '最近' : '全部' }}</button></div></div>
              <div class="filter-section"><div class="filter-label">{{ mode === 'actor' ? '单位类型' : '对象类型' }}</div><div class="choice-grid"><button v-for="item in (mode === 'actor' ? entityKinds : runtimeKinds)" :key="item" type="button" :class="{ selected: kindFilter === item }" @click="kindFilter = item">{{ item === 'all' ? '全部' : kindLabel(item) }}</button></div></div>
              <div v-if="mode === 'actor'" class="filter-section"><div class="filter-label">阵营</div><div class="choice-grid two-col"><button v-for="team in teams" :key="team" type="button" :class="{ selected: teamFilter === team }" @click="teamFilter = team">{{ team === 'all' ? '全部' : team === 'Blue' ? '蓝方' : team === 'Red' ? '红方' : team === 'Neutral' ? '中立' : '未知' }}</button></div></div>
              <div class="filter-section"><div class="filter-label">状态</div><div class="choice-grid two-col"><button v-for="status in (mode === 'actor' ? statuses : ['all', 'active', 'ended', 'partial', 'unknown'])" :key="status" type="button" :class="{ selected: statusFilter === status }" @click="statusFilter = status">{{ status === 'all' ? '全部' : statusLabel(status) }}</button></div></div>
              <div v-if="showAdvanced" class="advanced-box"><div class="filter-label">高级表达式</div><div class="advanced-input"><span>id:</span><input placeholder="1042" /></div><div class="advanced-input"><span>tag:</span><input placeholder="CrowdControl" /></div></div>
              <div class="active-filters"><span class="filter-label">活动条件</span><span v-if="search || kindFilter !== 'all' || teamFilter !== 'all' || statusFilter !== 'all'" class="filter-chip">已应用筛选 <button type="button" @click="clearFilters">×</button></span><span v-else class="muted-note">暂无条件</span></div>
            </aside>

            <section class="list-panel surface">
              <div class="list-toolbar"><div><span class="panel-kicker">RESULTS</span><h2>{{ mode === 'actor' ? '角色 / 单位' : '独立运行时对象' }}</h2></div><span class="sort-note">按身份稳定排序</span></div>
              <div class="table-head" :class="mode"><span>对象</span><span>身份 ID</span><span>类型 / 定义</span><span>状态</span><span>来源</span></div>
              <div class="result-list">
                <button v-for="row in currentRows" :key="mode === 'actor' ? (row as ActorEntity).id : (row as RuntimeEntity).id" type="button" class="result-row" :class="{ selected: mode === 'actor' ? selectedActorId === (row as ActorEntity).id : selectedRuntimeId === (row as RuntimeEntity).id }" @click="mode === 'actor' ? selectActor(row as ActorEntity) : selectRuntime(row as RuntimeEntity)">
                  <template v-if="mode === 'actor'"><span class="entity-name"><i class="type-mark" :class="(row as ActorEntity).kind.toLowerCase()"></i><strong>{{ displayName(row as ActorEntity) }}</strong></span><span class="entity-id">#{{ (row as ActorEntity).id }}</span><span class="entity-kind">{{ kindLabel((row as ActorEntity).kind) }}<small>{{ (row as ActorEntity).config || '配置未提供' }}</small></span><span class="row-status"><b class="state-pill" :class="(row as ActorEntity).status">{{ statusLabel((row as ActorEntity).status) }}</b><small>{{ healthLabel(row as ActorEntity) }}</small></span><span class="source-text">{{ (row as ActorEntity).source || '来源未知' }}</span></template>
                  <template v-else><span class="entity-name"><i class="type-mark runtime"></i><strong>{{ (row as RuntimeEntity).name || '未命名对象' }}</strong></span><span class="entity-id">{{ (row as RuntimeEntity).id }}</span><span class="entity-kind">{{ kindLabel((row as RuntimeEntity).kind) }}<small>{{ (row as RuntimeEntity).definition || '定义未提供' }}</small></span><span><b class="state-pill" :class="(row as RuntimeEntity).status">{{ statusLabel((row as RuntimeEntity).status) }}</b><small class="row-secondary">{{ (row as RuntimeEntity).frameRange }}</small></span><span class="source-text">{{ (row as RuntimeEntity).completeness === 'unknown' ? '来源未知' : statusLabel((row as RuntimeEntity).completeness) }}</span></template>
                </button>
                <div v-if="!currentRows.length" class="empty-state"><strong>没有匹配结果</strong><span>保留当前选择，清空条件或扩大范围。</span><button type="button" @click="clearFilters">清空条件</button></div>
              </div>
              <div class="list-footer"><span>显示 {{ currentRows.length }} 项</span><span>当前选择不会被筛选清除</span></div>
            </section>

            <aside class="inspector-panel surface">
              <template v-if="mode === 'actor' && selectedActor"><div class="inspector-heading"><div><span class="panel-kicker">ACTOR INSPECTOR</span><h2>{{ displayName(selectedActor) }}</h2></div><span class="identity-tag">Actor</span></div><div class="identity-block"><div><strong>#{{ selectedActor.id }}</strong><span>{{ kindLabel(selectedActor.kind) }} · {{ selectedActor.team === 'Blue' ? '蓝方' : selectedActor.team === 'Red' ? '红方' : '中立' }}</span></div><b class="state-pill" :class="selectedActor.status">{{ statusLabel(selectedActor.status) }}</b></div><dl class="field-grid"><dt>配置</dt><dd>{{ selectedActor.config || '未提供' }}</dd><dt>生命值</dt><dd>{{ healthLabel(selectedActor) }}</dd><dt>来源</dt><dd>{{ selectedActor.source || '来源未知' }}</dd></dl><div class="inspector-section"><div class="section-row"><span class="inspector-label">相关诊断</span><span class="on-demand-tag">按需打开</span></div><button class="related-link" type="button" @click="openBuffs()"><span><strong>Buff 生命周期</strong><small>当前 Actor 的状态实例</small></span><b>{{ buffs.filter((item) => item.status !== 'ended').length }} 项 →</b></button><button class="related-link" type="button" @click="openEvents(undefined, selectedActor!.id)"><span><strong>战斗事件</strong><small>最近活动与结果</small></span><b>查看事件 →</b></button><button class="related-link" type="button" @click="openTrace(selectedRuntime?.trace || 'trace:cast-424')"><span><strong>执行 Trace</strong><small>跨实体的因果链</small></span><b>打开 Trace →</b></button></div><div class="inspector-footnote">实体面板只保留身份与入口，Buff 详情和 Trace 因果树在独立模块中加载。</div></template>
              <template v-else-if="selectedRuntime"><div class="inspector-heading"><div><span class="panel-kicker">OBJECT INSPECTOR</span><h2>{{ selectedRuntime.name || '未命名对象' }}</h2></div><span class="identity-tag object">Object</span></div><div class="identity-block"><div><strong>{{ selectedRuntime.id }}</strong><span>{{ kindLabel(selectedRuntime.kind) }} · {{ selectedRuntime.frameRange }}</span></div><b class="state-pill" :class="selectedRuntime.status">{{ statusLabel(selectedRuntime.status) }}</b></div><dl class="field-grid"><dt>定义</dt><dd>{{ selectedRuntime.definition || '未提供' }}</dd><dt>来源 Actor</dt><dd><button v-if="selectedRuntime.sourceActorId" class="inline-link" type="button" @click="activeWorkspaceId = 'actors'; mode = 'actor'; selectedActorId = selectedRuntime!.sourceActorId!; openModule('actor-browser')">#{{ selectedRuntime.sourceActorId }} →</button><span v-else>未知</span></dd><dt>所有者</dt><dd>{{ selectedRuntime.ownerActorId ? `Actor #${selectedRuntime.ownerActorId}` : '未提供' }}</dd><dt>结束原因</dt><dd>{{ selectedRuntime.endReason || '仍在进行' }}</dd></dl><div class="inspector-section"><div class="inspector-label">关联诊断</div><button class="related-link" type="button" @click="openTrace(selectedRuntime.trace)"><span><strong>执行 Trace</strong><small>{{ selectedRuntime.trace || '引用未提供' }}</small></span><b>打开 →</b></button><button class="related-link" type="button" @click="openEvents(selectedRuntime.trace)"><span><strong>关联事件</strong><small>按对象上下文过滤</small></span><b>查看 →</b></button></div></template>
            </aside>
          </section>
        </template>

        <template v-else-if="activeModuleId === 'buffs'">
          <section class="entity-context surface"><div class="context-avatar">{{ selectedActor?.kind === 'Hero' ? 'A' : 'U' }}</div><div><span class="panel-kicker">BUFF OWNER</span><strong>{{ selectedActor ? `${displayName(selectedActor)} · Actor #${selectedActor.id}` : '未选择 Actor' }}</strong><small>{{ selectedActor?.config }} · 帧 {{ selectedFrame }}</small></div><button class="outline-button" type="button" @click="activeWorkspaceId = 'actors'; openModule('actor-browser')">切换 Actor</button></section>
          <section class="summary-strip three"><div><span>活动实例</span><strong>3</strong></div><div><span>即将结束</span><strong>1</strong></div><div><span>当前筛选</span><strong>Actor #{{ selectedActorId }}</strong></div></section>
          <section class="data-layout"><div class="surface data-list"><div class="list-toolbar"><div><span class="panel-kicker">BUFF INSTANCES</span><h2>Buff 生命周期</h2></div><label class="small-search"><span>⌕</span><input placeholder="名称 / Buff ID / 来源" /></label></div><div class="data-table-head buff-grid"><span>Buff</span><span>实例 ID</span><span>层数</span><span>剩余时间</span><span>状态</span></div><button v-for="buff in buffs.filter((item) => item.status !== 'ended' || item.id === selectedBuffId)" :key="buff.id" class="data-row buff-grid" :class="{ selected: selectedBuffId === buff.id }" type="button" @click="selectedBuffId = buff.id"><span><strong>{{ buff.name }}</strong><small>{{ buff.definition }}</small></span><span class="entity-id">{{ buff.id }}</span><span>{{ buff.stacks }}</span><span>{{ buff.remaining }}</span><b class="state-pill" :class="buff.status">{{ statusLabel(buff.status) }}</b></button></div>
            <aside v-if="selectedBuff" class="surface detail-panel"><div class="panel-kicker">SELECTED BUFF</div><h2>{{ selectedBuff.name }}</h2><p class="detail-subtitle">{{ selectedBuff.id }} · {{ selectedBuff.definition }}</p><dl class="field-grid"><dt>层数</dt><dd>{{ selectedBuff.stacks }}</dd><dt>持续时间</dt><dd>{{ selectedBuff.duration }}</dd><dt>剩余时间</dt><dd>{{ selectedBuff.remaining }}</dd><dt>来源 Actor</dt><dd>Actor #{{ selectedBuff.sourceActorId }}</dd><dt>当前帧</dt><dd>{{ selectedFrame }}</dd></dl><div class="lifecycle"><div class="lifecycle-line"><i></i></div><div><strong>添加</strong><span>Frame 1662 · trace 命中效果</span></div><div><strong>层数更新</strong><span>Frame 1780 · 1 → 2</span></div><div><strong>预计结束</strong><span>Frame 1884 · 持续时间到期</span></div></div><button class="primary-button full-button" type="button" @click="openTrace(selectedBuff.traceId)">打开来源 Trace</button><button class="text-command" type="button" @click="openEvents(selectedBuff.traceId, selectedActorId)">查看相关事件 →</button></aside>
          </section>
        </template>

        <template v-else-if="activeModuleId === 'events'">
          <section class="summary-strip"><div><span>匹配事件</span><strong>{{ filteredEvents.length }}</strong></div><div><span>失败 / 拒绝</span><strong>{{ battleEvents.filter((item) => item.outcome !== 'success').length }}</strong></div><div><span>时间范围</span><strong>Frame 1834-1842</strong></div><div class="summary-context"><span>当前分析</span><strong>{{ selectedEvent?.trigger ? `Trigger #${selectedEvent.trigger.triggerId}` : '事件摘要' }}</strong></div></section>
          <section class="surface trigger-triage">
            <div class="trigger-triage-heading"><div><span class="panel-kicker">TRIGGER TRIAGE</span><h2>先看需要排查的结果</h2><p>失败与预算拦截优先；通过组按频次聚合展示，频繁本身不作为异常。</p></div><div class="trigger-focus-switch"><button type="button" :class="{ selected: triggerFocus === 'issues' }" @click="triggerFocus = 'issues'">问题优先 <b>{{ triggerIssueGroupCount }}</b></button><button type="button" :class="{ selected: triggerFocus === 'all' }" @click="triggerFocus = 'all'">全部聚合 <b>{{ triggerAggregateGroups.length }}</b></button></div></div>
            <div class="trigger-group-head"><span>阶段 / 结果</span><span>Trigger / 失败键</span><span>出现情况</span><span>代表样本</span></div>
            <button v-for="group in sortedTriggerGroups" :key="`${group.triggerId}:${group.stage}:${group.result}:${group.failureKey}`" class="trigger-group-row" :class="group.result.toLowerCase()" type="button" @click="openTriggerSample(group)">
              <span class="trigger-group-status"><b class="state-pill" :class="group.result === 'Failed' ? 'failed' : group.result === 'Blocked' ? 'rejected' : 'success'">{{ triggerResultLabel(group.result) }}</b><small>{{ triggerStageLabel(group.stage) }} · {{ group.contextKind }}</small></span>
              <span class="trigger-group-identity"><strong>#{{ group.triggerId }} · {{ group.triggerName }}</strong><small>{{ group.failureKey || group.originKind }}</small></span>
              <span class="trigger-group-frequency"><strong>{{ group.occurrenceCount }} 次 <i v-if="group.occurrenceCount > 1">· 约 {{ triggerRateLabel(group) }}</i></strong><small>Frame {{ group.firstFrame }} → {{ group.lastFrame }}</small></span>
              <span class="trigger-group-sample"><small>{{ group.sampleReason }}</small><b>打开样本 →</b></span>
            </button>
            <div class="trigger-triage-foot"><span>已聚合 {{ triggerAggregateGroups.length }} 组 · {{ repeatedTriggerGroupCount }} 组包含重复触发</span><span>当前重点组 {{ triggerIssueGroupCount }} 组</span></div>
          </section>
          <section class="investigation-layout"><div class="surface event-list"><div class="list-toolbar"><div><span class="panel-kicker">EVENT STREAM</span><h2>战斗事件</h2></div><div class="toolbar-filters"><select v-model="eventOutcome"><option value="all">全部结果</option><option value="success">成功</option><option value="rejected">拒绝</option><option value="failed">失败</option></select><label class="small-search"><span>⌕</span><input v-model="search" placeholder="事件 / Actor / 关键词" /></label></div></div><div class="event-row event-head"><span>帧 / 时间</span><span>事件</span><span>Actor</span><span>结果</span><span>摘要</span></div><button v-for="event in filteredEvents" :key="event.id" type="button" class="event-row" :class="{ selected: selectedEventId === event.id }" @click="selectEvent(event)"><span class="event-time"><strong>{{ event.frame }}</strong><small>{{ event.time }}</small></span><span class="event-kind">{{ event.kind }}<small>{{ event.id }}</small></span><span>{{ event.actorName }}<small class="row-secondary">#{{ event.actorId }}</small></span><b class="state-pill" :class="event.outcome">{{ statusLabel(event.outcome) }}</b><span class="event-summary">{{ event.summary }}</span></button></div>
            <aside class="surface event-inspector"><div class="panel-kicker">EVENT DETAILS</div><h2>{{ selectedEvent?.kind }}</h2><p class="detail-subtitle">{{ selectedEvent?.id }} · Frame {{ selectedEvent?.frame }}</p><dl class="field-grid"><dt>执行者</dt><dd><button class="inline-link" type="button" @click="activeWorkspaceId = 'actors'; openModule('actor-browser'); selectedActorId = selectedEvent!.actorId">{{ selectedEvent?.actorName }} #{{ selectedEvent?.actorId }} →</button></dd><dt>结果</dt><dd>{{ selectedEvent ? statusLabel(selectedEvent.outcome) : '' }}</dd><dt>描述</dt><dd>{{ selectedEvent?.summary }}</dd><dt>Trace 根</dt><dd>{{ selectedEvent?.traceId }}</dd><template v-if="selectedEvent?.trigger"><dt>触发器</dt><dd>#{{ selectedEvent.trigger.triggerId }} · {{ selectedEvent.trigger.triggerName }}</dd><dt>事件类型</dt><dd>{{ selectedEvent.trigger.eventId }}</dd><dt>上下文</dt><dd>{{ selectedEvent.trigger.context }}</dd><dt>来源</dt><dd>{{ selectedEvent.trigger.origin }}</dd></template></dl><div class="related-box"><span class="inspector-label">因果关联</span><div class="trace-mini"><i></i><div><strong>SkillCast</strong><small>Frame 1837 · 输入通过</small></div></div><div class="trace-mini"><i></i><div><strong>ProjectileHit</strong><small>Frame 1841 · 命中目标</small></div></div><div class="trace-mini current"><i></i><div><strong>{{ selectedEvent?.kind }}</strong><small>Frame {{ selectedEvent?.frame }} · 当前事件</small></div></div><button class="text-command" type="button" @click="openTrace(selectedEvent?.traceId || '')">在 Trace 中查看完整因果 →</button></div></aside>
          </section>
          <section v-if="selectedEvent?.trigger" class="surface trigger-workflow">
            <div class="trigger-workflow-heading"><div><span class="panel-kicker">TRIGGER EXECUTION</span><h2>#{{ selectedEvent.trigger.triggerId }} · {{ selectedEvent.trigger.triggerName }}</h2><p>{{ selectedEvent.trigger.eventId }} <span>·</span> Frame {{ selectedEvent.frame }} <span>·</span> {{ selectedEvent.trigger.context }}</p></div><span class="prototype-evidence">界面原型明细 · 非当前 Live DTO</span></div>
            <div class="trigger-stage-grid">
              <article class="trigger-stage" :class="selectedEvent.trigger.budget.result"><div class="trigger-stage-top"><span class="stage-index">01</span><span class="stage-result" :class="selectedEvent.trigger.budget.result">{{ selectedEvent.trigger.budget.result === 'passed' ? '通过' : '拦截' }}</span></div><h3>触发预算</h3><p>{{ selectedEvent.trigger.budget.reason }}</p><dl><dt>递归深度</dt><dd>{{ selectedEvent.trigger.budget.depth }}</dd><dt>当前帧计数</dt><dd>{{ selectedEvent.trigger.budget.frameCount }}</dd><dt>Root 计数</dt><dd>{{ selectedEvent.trigger.budget.rootCount }}</dd><dt>同 Trigger 计数</dt><dd>{{ selectedEvent.trigger.budget.sameTriggerCount }}</dd></dl></article>
              <article class="trigger-stage" :class="selectedEvent.trigger.conditions.some((condition) => condition.result === 'failed') ? 'failed' : 'passed'"><div class="trigger-stage-top"><span class="stage-index">02</span><span class="stage-result" :class="selectedEvent.trigger.conditions.some((condition) => condition.result === 'failed') ? 'failed' : 'passed'">{{ selectedEvent.trigger.conditions.filter((condition) => condition.result === 'passed').length }} / {{ selectedEvent.trigger.conditions.length }} 通过</span></div><h3>条件评估</h3><p>逐条查看期望值、实际值与是否参与评估</p><div v-for="condition in selectedEvent.trigger.conditions" :key="condition.name" class="condition-row"><i :class="condition.result"></i><div><strong>{{ condition.name }}</strong><code>{{ condition.expression }}</code></div><span class="condition-value"><small>实际 {{ condition.actual }}</small><small>期望 {{ condition.expected }}</small></span><b :class="condition.result">{{ condition.result === 'passed' ? '通过' : condition.result === 'failed' ? '失败' : '跳过' }}</b></div></article>
              <article class="trigger-stage" :class="selectedEvent.trigger.plan.result"><div class="trigger-stage-top"><span class="stage-index">03</span><span class="stage-result" :class="selectedEvent.trigger.plan.result">{{ selectedEvent.trigger.plan.result === 'passed' ? '继续执行' : '未进入执行' }}</span></div><h3>计划判定</h3><p>{{ selectedEvent.trigger.plan.summary }}</p><div class="plan-route"><span>Trigger Plan #{{ selectedEvent.trigger.triggerId }}</span><b>→</b><span>{{ selectedEvent.trigger.plan.result === 'passed' ? `${selectedEvent.trigger.actions.length} 个 Action` : 'Action 阶段跳过' }}</span></div></article>
              <article class="trigger-stage execution-stage"><div class="trigger-stage-top"><span class="stage-index">04</span><span class="stage-result passed">{{ selectedEvent.trigger.actions.filter((action) => action.result === 'completed').length }} 完成 · {{ selectedEvent.trigger.actions.filter((action) => action.result !== 'completed').length }} 未执行</span></div><h3>动作执行</h3><p>每个动作展开查看目标、帧和结果</p><div v-for="(action, index) in selectedEvent.trigger.actions" :key="`${action.name}-${index}`" class="action-row"><span class="action-index">{{ String(index + 1).padStart(2, '0') }}</span><div><strong>{{ action.name }}</strong><small>{{ action.target }} · Frame {{ action.frame }}</small><p>{{ action.detail }}</p></div><b :class="action.result">{{ action.result === 'completed' ? '完成' : action.result === 'failed' ? '失败' : '跳过' }}</b></div></article>
            </div>
            <div class="trigger-workflow-foot"><span><b>当前诊断契约：</b>阶段、结果、失败键/原因与预算计数可表达；逐条件和逐 Action 内容为原型假设。</span><button class="text-command" type="button" @click="openTrace(selectedEvent?.traceId || '')">打开关联 Trace →</button></div>
          </section>
        </template>

        <template v-else-if="activeModuleId === 'trace'">
          <section class="trace-toolbar surface"><div><span class="panel-kicker">TRACE ROOT</span><strong>{{ selectedTraceId }}</strong><small>Actor #1042 · Frame 1837-1842 · 6 节点</small></div><div class="trace-filters"><button class="outline-button active" type="button">因果树</button><button class="outline-button" type="button">瀑布图</button><button class="icon-button" type="button" title="聚焦所选">⌖</button></div></section>
          <section class="trace-layout"><div class="surface trace-tree"><div class="list-toolbar"><div><span class="panel-kicker">EXECUTION</span><h2>执行路径</h2></div><span class="state-pill complete">6 成功 · 1 失败</span></div><button v-for="node in traceNodes" :key="node.id" class="trace-node" :class="[{ selected: selectedTraceId === node.id }, `depth-${node.parentId ? (node.parentId.includes(':hit') ? 2 : 1) : 0}`]" type="button" @click="selectedTraceId = node.id"><span class="trace-node-mark" :class="node.status"></span><span><strong>{{ node.label }}</strong><small>Frame {{ node.frame }} · {{ node.kind }}</small></span><span class="node-duration">{{ node.duration }}</span></button></div>
            <div class="surface trace-waterfall"><div class="list-toolbar"><div><span class="panel-kicker">FRAME FLOW</span><h2>执行时间线</h2></div><span class="sort-note">当前区间 1837-1842</span></div><div class="timeline-axis"><span>1837</span><span>1838</span><span>1839</span><span>1840</span><span>1841</span><span>1842</span></div><div v-for="(node, index) in traceNodes.filter((item) => item.id.includes('cast-424'))" :key="node.id" class="waterfall-row"><span>{{ node.kind }}</span><div class="waterfall-track"><i :class="node.status" :style="{ left: `${Math.min(index * 11, 72)}%`, width: index === 0 ? '79%' : '14%' }"></i></div><button type="button" @click="activeWorkspaceId = 'actors'; openModule('actor-browser'); selectedActorId = node.actorId">#{{ node.actorId }}</button></div><div class="trace-selection"><span>当前节点</span><strong>{{ traceNodes.find((item) => item.id === selectedTraceId)?.label || '选择一个节点' }}</strong><button class="text-command" type="button" @click="openEvents(selectedTraceId, 1042)">查看相关事件 →</button></div></div>
            <aside class="surface detail-panel trace-detail"><div class="panel-kicker">NODE DETAILS</div><h2>{{ traceNodes.find((item) => item.id === selectedTraceId)?.label || '施法开始' }}</h2><p class="detail-subtitle">{{ traceNodes.find((item) => item.id === selectedTraceId)?.id || selectedTraceId }}</p><dl class="field-grid"><dt>类型</dt><dd>{{ traceNodes.find((item) => item.id === selectedTraceId)?.kind || 'SkillCast' }}</dd><dt>执行帧</dt><dd>{{ traceNodes.find((item) => item.id === selectedTraceId)?.frame || 1837 }}</dd><dt>Actor</dt><dd><button class="inline-link" type="button" @click="activeWorkspaceId = 'actors'; openModule('actor-browser')">Actor #1042 →</button></dd><dt>详情</dt><dd>{{ traceNodes.find((item) => item.id === selectedTraceId)?.detail || '技能实例 cast:424 · 输入校验通过' }}</dd></dl><div class="related-box"><span class="inspector-label">关联对象</span><button class="related-link" type="button" @click="activeWorkspaceId = 'runtime'; openModule('runtime-objects')"><span><strong>projectile:882:1</strong><small>Projectile · 命中目标 #1077</small></span><b>定位 →</b></button><button class="related-link" type="button" @click="openBuffs(1042)"><span><strong>buff:771:3</strong><small>奥术灼烧 · 2 层</small></span><b>查看 →</b></button></div></aside>
          </section>
        </template>

        <template v-else-if="activeModuleId === 'runtime-objects'">
          <section class="summary-strip"><div><span>运行时对象</span><strong>{{ runtimeObjects.length }}</strong></div><div><span>活动</span><strong>3</strong></div><div><span>记录不完整</span><strong>2</strong></div><div class="summary-context"><span>筛选上下文</span><strong>全部来源</strong></div></section>
          <section class="browser-layout compact-browser"><aside class="filter-panel surface"><span class="panel-kicker">FILTERS</span><h2>对象目录</h2><label class="search-box"><span>⌕</span><input v-model="search" placeholder="Runtime ID / 定义 / Trace" /></label><div class="filter-section"><div class="filter-label">对象类型</div><div class="choice-grid"><button v-for="kind in runtimeKinds" :key="kind" type="button" :class="{ selected: kindFilter === kind }" @click="kindFilter = kind">{{ kind === 'all' ? '全部' : kindLabel(kind) }}</button></div></div><div class="filter-section"><div class="filter-label">生命周期</div><div class="choice-grid"><button v-for="state in ['all', 'active', 'ended', 'partial', 'unknown']" :key="state" type="button" :class="{ selected: statusFilter === state }" @click="statusFilter = state">{{ state === 'all' ? '全部' : statusLabel(state) }}</button></div></div></aside><section class="list-panel surface"><div class="list-toolbar"><div><span class="panel-kicker">RUNTIME INDEX</span><h2>独立运行时对象</h2></div><span class="sort-note">Runtime ID + Generation</span></div><div class="table-head runtime"><span>对象</span><span>Runtime ID</span><span>类型 / 定义</span><span>帧范围</span><span>完整度</span></div><button v-for="item in filteredRuntimeObjects" :key="item.id" class="result-row runtime" :class="{ selected: selectedRuntimeId === item.id }" type="button" @click="selectRuntime(item)"><span class="entity-name"><i class="type-mark runtime"></i><strong>{{ item.name || '未命名对象' }}</strong></span><span class="entity-id">{{ item.id }}</span><span class="entity-kind">{{ kindLabel(item.kind) }}<small>{{ item.definition || '定义未提供' }}</small></span><span>{{ item.frameRange }}</span><span class="source-text">{{ statusLabel(item.completeness) }}</span></button></section><aside class="inspector-panel surface"><template v-if="selectedRuntime"><div class="panel-kicker">OBJECT DETAILS</div><h2>{{ selectedRuntime.name || '未命名对象' }}</h2><p class="detail-subtitle">{{ selectedRuntime.id }}</p><dl class="field-grid"><dt>定义</dt><dd>{{ selectedRuntime.definition || '未知' }}</dd><dt>来源 Actor</dt><dd>{{ selectedRuntime.sourceActorId || '未知' }}</dd><dt>目标 Actor</dt><dd>{{ selectedRuntime.targetActorId || '未知' }}</dd><dt>状态</dt><dd>{{ statusLabel(selectedRuntime.status) }}</dd><dt>结束原因</dt><dd>{{ selectedRuntime.endReason || '仍在进行' }}</dd><dt>Trace</dt><dd>{{ selectedRuntime.trace || '未提供' }}</dd></dl><button class="primary-button full-button" type="button" @click="openTrace(selectedRuntime.trace)">打开关联 Trace</button></template></aside></section>
        </template>

        <template v-else-if="activeModuleId === 'skill-runtimes' || activeModuleId === 'active-objects'">
          <section class="summary-strip"><div><span>活动施法</span><strong>2</strong></div><div><span>等待提交</span><strong>1</strong></div><div><span>活动投射物</span><strong>4</strong></div><div class="summary-context"><span>刷新策略</span><strong>模块激活时 · 4 Hz</strong></div></section>
          <section class="execution-layout"><div class="surface execution-list"><div class="list-toolbar"><div><span class="panel-kicker">{{ activeModuleId === 'skill-runtimes' ? 'SKILL RUNTIMES' : 'ACTIVE OBJECTS' }}</span><h2>{{ activeModule.label }}</h2></div><span class="state-pill active">实时</span></div><div v-for="(step, index) in ['输入接收 · Frame 1837', '目标查询 · Frame 1838', '生成投射物 · Frame 1839', '等待命中回调 · 当前']" :key="step" class="execution-step"><span class="step-index">0{{ index + 1 }}</span><div><strong>{{ step }}</strong><small>{{ index === 3 ? 'Projectile projectile:882:1 · 生命周期进行中' : 'cast:424 · Actor #1042' }}</small></div><button v-if="index === 2 || index === 3" class="text-command" type="button" @click="activeWorkspaceId = 'runtime'; openModule('runtime-objects')">对象详情 →</button></div></div><aside class="surface detail-panel"><div class="panel-kicker">CURRENT EXECUTION</div><h2>Arcane Bolt</h2><p class="detail-subtitle">Skill Runtime cast:424 · Generation 1</p><div class="progress-track"><i></i></div><dl class="field-grid"><dt>施法者</dt><dd>Actor #1042</dd><dt>技能配置</dt><dd>SkillConfig:424</dd><dt>当前阶段</dt><dd>等待投射物命中</dd><dt>挂起对象</dt><dd>projectile:882:1</dd><dt>已运行</dt><dd>5 帧</dd></dl><button class="primary-button full-button" type="button" @click="openTrace('trace:cast-424')">在 Trace 中检查 →</button></aside></section>
        </template>

        <template v-else-if="['sync-overview', 'prediction', 'rollback', 'reconcile', 'time-sync', 'network'].includes(activeModuleId)">
          <section class="summary-strip"><div><span>当前服务器帧</span><strong>1842</strong></div><div><span>最新确认帧</span><strong>1839</strong></div><div><span>回滚次数</span><strong>3</strong></div><div class="summary-context"><span>同步健康</span><strong class="healthy">稳定 · 1 个待检查区间</strong></div></section>
          <section class="sync-layout"><div class="surface sync-chart"><div class="list-toolbar"><div><span class="panel-kicker">{{ activeModule.label.toUpperCase() }}</span><h2>{{ activeModuleId === 'network' ? '网络缓冲与帧延迟' : activeModuleId === 'rollback' ? '回滚区间与原因' : activeModuleId === 'prediction' ? '预测命中与误差' : '世界同步状态' }}</h2></div><select><option>最近 10 秒</option><option>最近 60 秒</option></select></div><div class="chart-area"><div class="chart-guides"><span>100%</span><span>75%</span><span>50%</span><span>25%</span><span>0%</span></div><div class="chart-bars"><i style="height:42%"></i><i style="height:55%"></i><i style="height:49%"></i><i style="height:63%"></i><i style="height:58%"></i><i style="height:78%"></i><i class="warning" style="height:40%"></i><i style="height:65%"></i><i style="height:72%"></i><i style="height:83%"></i><i style="height:68%"></i><i style="height:91%"></i><i style="height:75%"></i><i style="height:80%"></i></div><div class="chart-frames"><span>1830</span><span>1834</span><span>1838</span><span>1842</span></div></div><div class="chart-legend"><span><i class="legend-green"></i>权威帧</span><span><i class="legend-blue"></i>预测帧</span><span><i class="legend-amber"></i>回滚 / 延迟区间</span></div><div class="timeline-alert"><b>!</b><span><strong>Frame 1838 存在一次状态校正</strong><small>预测位置与权威位置偏差 0.42 · 点击可跳转事件调查</small></span><button class="text-command" type="button" @click="openEvents(undefined, 1042)">调查此区间 →</button></div></div><aside class="surface detail-panel"><div class="panel-kicker">SELECTED FRAME</div><h2>Frame {{ selectedFrame }}</h2><p class="detail-subtitle">Server authoritative · World-07</p><dl class="field-grid"><dt>往返延迟</dt><dd>42 ms</dd><dt>抖动</dt><dd>8 ms</dd><dt>输入缓冲</dt><dd>2 帧</dd><dt>预测误差</dt><dd>0.08</dd><dt>状态校验</dt><dd class="healthy">一致</dd></dl><button class="outline-button full-button" type="button" @click="activeWorkspaceId = 'data'; openModule('replay')">定位回放帧 →</button><button v-if="activeModuleId === 'reconcile'" class="danger-button full-button" type="button">请求重新对账</button></aside></section>
        </template>

        <template v-else-if="activeModuleId === 'health' || activeModuleId === 'state'">
          <section class="summary-strip"><div><span>诊断会话</span><strong class="healthy">可用</strong></div><div><span>采样延迟</span><strong>18 ms</strong></div><div><span>数据完整度</span><strong>92%</strong></div><div class="summary-context"><span>更新时间</span><strong>0.2 秒前 · Frame 1842</strong></div></section>
          <section class="health-layout"><div class="surface capability-table"><div class="list-toolbar"><div><span class="panel-kicker">DATA CAPABILITIES</span><h2>{{ activeModule.label }}</h2></div><button class="outline-button" type="button">检查完整性</button></div><div class="capability-row head"><span>数据域</span><span>状态</span><span>来源</span><span>最近更新</span><span>说明</span></div><div v-for="row in [['Actor 状态', '可用', 'Server Snapshot', 'Frame 1842', '完整'], ['诊断事件', '可用', 'Runtime Store', 'Frame 1842', '保留最近 500 条'], ['Trace 因果', '部分', 'Runtime Store', 'Frame 1842', '部分节点缺少父引用'], ['运行时对象', '部分', 'Diagnostics', 'Frame 1841', '来源关系未完整采集'], ['网络指标', '可用', 'State Sync', 'Frame 1842', '实时指标']]" :key="row[0]" class="capability-row"><strong>{{ row[0] }}</strong><span class="state-pill" :class="row[1] === '可用' ? 'complete' : 'partial'">{{ row[1] }}</span><span>{{ row[2] }}</span><span>{{ row[3] }}</span><span>{{ row[4] }}</span></div></div><aside class="surface detail-panel"><div class="panel-kicker">QUALITY NOTES</div><h2>需要关注</h2><div class="quality-item"><b class="quality-mark amber">!</b><div><strong>Trace 关联部分缺失</strong><small>部分事件只有 Actor 引用，无法定位因果父节点。</small><button class="text-command" type="button" @click="activeWorkspaceId = 'investigation'; openModule('trace')">查看 Trace →</button></div></div><div class="quality-item"><b class="quality-mark green">✓</b><div><strong>快照与事件帧一致</strong><small>当前会话 watermark 已覆盖 Frame 1842。</small></div></div></aside></section>
        </template>

        <template v-else-if="activeModuleId === 'artifact'">
          <section class="artifact-layout"><div class="surface artifact-source"><div class="panel-kicker">DIAGNOSTIC ARTIFACT</div><h2>来源与数据包</h2><div class="drop-zone"><strong>打开诊断文件</strong><span>拖入 Artifact，或选择本地文件</span><button class="outline-button" type="button">选择文件</button></div><div class="artifact-meta"><span>当前来源</span><strong>live-session-World-07</strong><span>战斗 ID</span><strong>Battle #M-2048</strong><span>格式版本</span><strong>abilitykit-battle-diagnostics.v2</strong><span>帧范围</span><strong>Frame 1200-1842</strong></div></div><div class="surface artifact-capabilities"><div class="panel-kicker">CONTENTS</div><h2>可用内容</h2><div v-for="item in ['Actor Summary', 'Events', 'Trace', 'Buff Snapshots', 'State Snapshots', 'Frame Sync Metrics', 'Replay Video']" :key="item" class="artifact-content-row"><span>{{ item }}</span><b>{{ item === 'Replay Video' ? '未包含' : item === 'Buff Snapshots' ? '部分' : '已包含' }}</b></div><div class="artifact-actions"><button class="outline-button" type="button">导出当前诊断</button><button class="primary-button" type="button" @click="openWorkspace(workspaces[4]); openModule('replay')">打开回放工作区</button></div></div></section>
        </template>

        <template v-else-if="activeModuleId === 'replay'">
          <section class="replay-stage"><div class="arena-toolbar"><span>表现回放 · Frame {{ selectedFrame }}</span><span>World-07 · Seed 47</span><button class="icon-button" type="button" title="画面模式">◐</button></div><div class="arena-view"><div class="lane-grid"></div><div class="unit blue-unit" style="left:34%;top:51%"><b>A</b><span>Actor #1042</span></div><div class="unit red-unit" style="left:70%;top:43%"><b>B</b><span>Actor #1077</span></div><div class="unit summon-unit" style="left:43%;top:65%"><b>S</b><span>召唤物 #3012</span></div><div class="projectile-path"></div><div class="frame-overlay">Frame {{ selectedFrame }}<small>权威状态</small></div></div><div class="replay-controls"><button class="icon-button" type="button" title="上一帧">|◀</button><button class="icon-button play-button" type="button" title="播放">▶</button><button class="icon-button" type="button" title="下一帧">▶|</button><input v-model.number="selectedFrame" type="range" min="1200" max="1842" /><strong>{{ selectedFrame }} / 1842</strong><select><option>1×</option><option>0.5×</option><option>2×</option></select></div></section>
          <section class="replay-lower"><div class="surface replay-events"><div class="list-toolbar"><div><span class="panel-kicker">FRAME EVENTS</span><h2>当前帧事件</h2></div><button class="text-command" type="button" @click="activeWorkspaceId = 'investigation'; openModule('events')">打开事件调查 →</button></div><button v-for="event in battleEvents.slice(0, 3)" :key="event.id" class="replay-event-row" type="button" @click="selectEvent(event); activeWorkspaceId = 'investigation'; openModule('events')"><span>{{ event.frame }}</span><strong>{{ event.kind }}</strong><small>{{ event.summary }}</small></button></div><div class="surface replay-actor"><div class="panel-kicker">SELECTED ACTOR</div><h2>{{ selectedActor ? displayName(selectedActor) : '未选择' }}</h2><dl class="field-grid"><dt>Actor ID</dt><dd>#{{ selectedActorId }}</dd><dt>生命值</dt><dd>{{ selectedActor ? healthLabel(selectedActor) : '未知' }}</dd><dt>相关 Buff</dt><dd><button class="inline-link" type="button" @click="openBuffs()">查看生命周期 →</button></dd></dl></div></section>
        </template>

        <template v-else-if="activeModuleId === 'attributes'">
          <div class="attribute-modebar">
            <div class="segmented mode-switch"><button type="button" :class="{ active: attributeView === 'values' }" @click="attributeView = 'values'">属性值与曲线</button><button type="button" :class="{ active: attributeView === 'modifiers' }" @click="attributeView = 'modifiers'">修改器</button></div>
            <span>Actor #{{ selectedActorId }} · Frame {{ selectedFrame }} · 属性投影快照</span>
          </div>

          <section v-if="attributeView === 'values'" class="attribute-layout">
            <aside class="surface attribute-list-panel">
              <div class="list-toolbar"><div><span class="panel-kicker">ACTOR ATTRIBUTES</span><h2>属性列表</h2></div><span class="count-badge">{{ visibleActorAttributes.length }} 项</span></div>
              <label class="small-search attribute-search"><span>⌕</span><input v-model="attributeSearch" type="search" placeholder="搜索属性名或 ID" /></label>
              <button v-for="attribute in visibleActorAttributes" :key="attribute.id" type="button" class="attribute-row" :class="{ selected: selectedAttributeId === attribute.id }" @click="selectedAttributeId = attribute.id">
                <span class="attribute-row-name"><strong>{{ attribute.name }}</strong><small>ID {{ attribute.id }} · {{ attribute.unit }}</small></span>
                <span class="attribute-row-values"><strong>{{ formatAttributeValue(attribute.finalValue) }}</strong><small>基础 {{ formatAttributeValue(attribute.baseValue) }}</small></span>
                <b class="modifier-count" :class="{ none: attribute.modifierCount === 0 }">{{ attribute.modifierCount }} 修改</b>
              </button>
              <div v-if="!visibleActorAttributes.length" class="empty-state compact-empty"><strong>没有匹配属性</strong><button type="button" @click="attributeSearch = ''">清除搜索</button></div>
              <div class="attribute-list-foot">当前 Actor 的采样属性 · {{ actorAttributes.length }} 项</div>
            </aside>

            <div class="attribute-inspector">
              <section class="surface attribute-summary">
                <div class="attribute-summary-title"><div><span class="panel-kicker">ATTRIBUTE {{ selectedAttribute.id }}</span><h2>{{ selectedAttribute.name }}</h2></div><span class="identity-tag">{{ selectedAttribute.unit }}</span></div>
                <div class="attribute-metrics">
                  <div><span>基础值</span><strong>{{ formatAttributeValue(selectedAttribute.baseValue) }}</strong></div>
                  <div><span>最终值</span><strong class="metric-final">{{ formatAttributeValue(selectedAttribute.finalValue) }}</strong></div>
                  <div><span>修改差值</span><strong :class="attributeDelta(selectedAttribute.id) >= 0 ? 'delta-positive' : 'delta-negative'">{{ attributeDelta(selectedAttribute.id) >= 0 ? '+' : '' }}{{ formatAttributeValue(attributeDelta(selectedAttribute.id)) }}</strong></div>
                  <div><span>修改器</span><strong>{{ selectedAttribute.modifierCount }}</strong></div>
                </div>
              </section>

              <section class="surface attribute-chart">
                <div class="attribute-chart-heading"><div><span class="panel-kicker">RECENT VALUE HISTORY</span><h2>最近数值曲线</h2></div><div class="chart-controls"><button type="button" class="record-toggle" :class="{ paused: !attributeRecording }" @click="attributeRecording = !attributeRecording"><i></i>{{ attributeRecording ? '记录中' : '已暂停' }}</button><label>采样间隔<select v-model.number="attributeSampleInterval"><option :value="1">1 帧</option><option :value="2">2 帧</option><option :value="4">4 帧</option><option :value="6">6 帧</option></select></label><button type="button" class="outline-button chart-expected-toggle" :class="{ active: showExpectedAttribute }" @click="showExpectedAttribute = !showExpectedAttribute">预期值</button></div></div>
                <label v-if="showExpectedAttribute" class="expected-value-field">目标值<input v-model.number="expectedAttributeValue" type="number" step="0.1" /><span>偏差容差 ±0.01</span></label>
                <div class="attribute-chart-plot">
                  <div class="chart-y-labels"><span>{{ formatAttributeValue(attributeChartMax) }}</span><span>{{ formatAttributeValue((attributeChartMax + attributeChartMin) / 2) }}</span><span>{{ formatAttributeValue(attributeChartMin) }}</span></div>
                  <svg viewBox="0 0 760 220" preserveAspectRatio="none" role="img" :aria-label="`${selectedAttribute.name}基础值和最终值历史曲线`">
                    <line v-for="y in [28, 103, 178]" :key="y" x1="48" :y1="y" x2="732" :y2="y" class="chart-gridline" />
                    <line v-for="(sample, index) in attributeChartSamples" :key="`tick-${sample.frame}`" :x1="48 + index / Math.max(1, attributeChartSamples.length - 1) * 684" y1="28" :x2="48 + index / Math.max(1, attributeChartSamples.length - 1) * 684" y2="178" class="chart-vgridline" />
                    <line v-if="showExpectedAttribute" x1="48" :y1="attributeChartY(expectedAttributeValue)" x2="732" :y2="attributeChartY(expectedAttributeValue)" class="chart-expected-line" />
                    <path :d="attributeChartPath('baseValue')" class="attribute-line base-line" />
                    <path :d="attributeChartPath('finalValue')" class="attribute-line final-line" />
                    <circle v-if="attributeChartSamples.length" cx="732" :cy="attributeChartY(attributeChartSamples[attributeChartSamples.length - 1].baseValue)" r="4" class="chart-point base-point" />
                    <circle v-if="attributeChartSamples.length" cx="732" :cy="attributeChartY(attributeChartSamples[attributeChartSamples.length - 1].finalValue)" r="4" class="chart-point final-point" />
                  </svg>
                  <div class="chart-x-labels"><span>Frame {{ attributeChartSamples[0]?.frame }}</span><span>{{ attributeChartSamples[Math.floor(attributeChartSamples.length / 2)]?.frame }}</span><span>Frame {{ attributeChartSamples[attributeChartSamples.length - 1]?.frame }}</span></div>
                </div>
                <div class="attribute-chart-footer"><div class="attribute-legend"><span><i class="base-swatch"></i>基础值</span><span><i class="final-swatch"></i>最终值</span><span v-if="showExpectedAttribute"><i class="expected-swatch"></i>预期值</span></div><span>{{ attributeChartSamples.length }} 点 · 最多保留 240 点 · {{ attributeRecording ? '实时采样' : '采样已暂停' }}</span></div>
              </section>

              <section class="surface attribute-sources">
                <div class="list-toolbar"><div><span class="panel-kicker">VALUE CONTRIBUTION</span><h2>属性修改来源</h2></div><span class="count-badge">{{ selectedAttributeModifiers.length }} 项</span></div>
                <div v-if="selectedAttributeModifiers.length" class="modifier-card-list">
                  <article v-for="modifier in selectedAttributeModifiers" :key="modifier.id" class="attribute-modifier-card">
                    <div class="modifier-card-heading"><span class="source-type-mark" :class="modifier.sourceKind === 'Buff' ? 'buff-mark' : 'passive-mark'"></span><div class="modifier-source-title"><strong>{{ modifier.sourceName }}</strong><small>{{ modifier.sourceKind }} · 来源 #{{ modifier.sourceId }}</small></div><span class="state-pill" :class="modifier.sourceState">{{ modifier.sourceState === 'active' ? '生效中' : '已结束' }}</span></div>
                    <div class="modifier-primary-values"><strong>{{ modifier.operation === 'multiply' ? '×' : modifier.operation === 'override' ? '=' : modifier.operation === 'addPercent' ? '+%' : '+' }} {{ formatAttributeValue(modifier.magnitude) }}{{ modifier.operation === 'addPercent' ? '%' : '' }}</strong><span>{{ modifierOperation(modifier) }} · 优先级 {{ modifier.priority }} · {{ modifier.magnitudeType }}</span><span>层数 {{ modifier.stackCount }} · {{ modifier.evaluationPolicy }}</span></div>
                    <div class="modifier-breakdown"><div><span>声明值</span><strong>{{ formatAttributeValue(modifier.declaredValue) }}</strong></div><div><span>叠层值</span><strong>{{ formatAttributeValue(modifier.stackedValue) }}</strong></div><div><span>投影值</span><strong>{{ formatAttributeValue(modifier.projectedValue) }}</strong></div><div><span>当前值</span><strong>{{ formatAttributeValue(modifier.currentValue) }}</strong></div><div><span>捕获值</span><strong>{{ formatAttributeValue(modifier.capturedValue) }}</strong></div></div>
                    <p class="modifier-explanation">{{ modifier.explanation }}</p>
                    <div class="modifier-card-foot"><span>{{ modifier.sourceActorId ? `施加者 Actor #${modifier.sourceActorId}` : '来源 Actor 未采集' }} · {{ modifier.elapsed }} · 剩余 {{ modifier.remaining }}</span><button v-if="modifier.sourceKind === 'Buff'" class="text-command" type="button" @click="openBuffs(selectedActorId, modifier.sourceId)">查看 Buff →</button></div>
                  </article>
                </div>
                <div v-else class="empty-state compact-empty"><strong>此属性没有修改器</strong><span>基础值与最终值一致。</span></div>
              </section>
            </div>
          </section>

          <section v-else class="surface modifier-workspace">
            <div class="modifier-workspace-toolbar"><div><span class="panel-kicker">MODIFIER INSPECTOR</span><h2>修改器与持续来源</h2></div><label class="small-search"><span>⌕</span><input v-model="attributeSearch" type="search" placeholder="来源、属性名或 ID" /></label></div>
            <div class="modifier-filters"><label>来源类型<select v-model="attributeSourceFilter"><option value="all">全部</option><option value="Buff">Buff</option><option value="被动效果">被动效果</option></select></label><label class="modifier-check"><input v-model="attributeActiveOnly" type="checkbox" />仅显示生效中</label><span>持续来源状态与属性投影分开展示</span></div>
            <div class="modifier-table-head"><span>来源 / 生命周期</span><span>目标属性 / 操作</span><span>优先级 / 策略</span><span>计算值</span><span>来源实体</span></div>
            <article v-for="modifier in visibleAttributeModifiers" :key="modifier.id" class="modifier-table-row">
              <div class="modifier-table-source"><div><strong>{{ modifier.sourceName }}</strong><span class="state-pill" :class="modifier.sourceState">{{ modifier.sourceState === 'active' ? '生效中' : '已结束' }}</span></div><small>{{ modifier.sourceKind }} #{{ modifier.sourceId }} · 层数 {{ modifier.stackCount }} · {{ modifier.elapsed }} / {{ modifier.remaining }}</small></div>
              <div><strong>{{ actorAttributes.find((item) => item.id === modifier.attributeId)?.name }} ({{ modifier.attributeId }})</strong><small>{{ modifierFormula(modifier) }}</small></div>
              <div><strong>{{ modifier.priority }}</strong><small>{{ modifier.evaluationPolicy }} · {{ modifier.captureMode }}</small></div>
              <div class="modifier-table-value"><strong>{{ formatAttributeValue(modifier.currentValue) }}</strong><small>投影 {{ formatAttributeValue(modifier.projectedValue) }} · 捕获 {{ formatAttributeValue(modifier.capturedValue) }}</small></div>
              <div><button v-if="modifier.sourceKind === 'Buff'" class="inline-link" type="button" @click="openBuffs(selectedActorId, modifier.sourceId)">Actor #{{ modifier.sourceActorId }} · 查看 Buff →</button><span v-else>Actor #{{ modifier.sourceActorId }}</span><small>来源 ID {{ modifier.sourceId }}</small></div>
              <p>{{ modifier.explanation }}</p>
            </article>
            <div v-if="!visibleAttributeModifiers.length" class="empty-state compact-empty"><strong>没有匹配的修改器</strong><button type="button" @click="attributeSearch = ''; attributeSourceFilter = 'all'; attributeActiveOnly = false">清除筛选</button></div>
            <div class="modifier-workspace-foot"><span>离线 Artifact 可能只有属性投影，缺少持续实例或部分解释字段。</span><span>查询范围 Actor #{{ selectedActorId }} · {{ visibleAttributeModifiers.length }} 项</span></div>
          </section>
        </template>

        <template v-else>
          <section class="generic-module surface"><div class="generic-icon">{{ activeModuleId === 'effects' ? '◈' : activeModuleId === 'tags' ? '#' : '◎' }}</div><div><span class="panel-kicker">{{ activeWorkspace.label.toUpperCase() }} MODULE</span><h2>{{ activeModule.label }}</h2><p>该模块作为独立任务视图加载，共享当前会话、Actor 与帧上下文。</p></div><button class="outline-button" type="button" @click="activeWorkspaceId = 'actors'; openModule('actor-browser')">返回 Actor 浏览器</button><div class="generic-data"><div v-for="item in (activeModuleId === 'effects' ? ['持续效果实例', '周期动作', '来源与堆叠', '移除条件'] : activeModuleId === 'tags' ? ['有效标签', '来源贡献', '门禁规则', '结果标记'] : ['模块状态', '活动记录', '最近更新', '来源能力'])" :key="item"><span>{{ item }}</span><strong>{{ item === '模块状态' ? '数据可用' : '查看详情 →' }}</strong></div></div></section>
        </template>
      </main>
    </div>

    <footer class="prototype-footer"><span><i></i>可交互界面原型</span><span>Mock 视图数据 · 用于验证模块结构与跨页导航</span><span>{{ activeWorkspace.label }} / {{ activeModule.label }}</span></footer>
  </div>
</template>
