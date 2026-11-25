export type EntityMode = 'actor' | 'runtime';
export type ActorStatus = 'alive' | 'dead' | 'removed' | 'unknown';
export type RuntimeStatus = 'active' | 'ended' | 'partial' | 'unknown';

export interface ActorEntity {
  id: number;
  name: string;
  kind: 'Hero' | 'Minion' | 'Summon' | 'Neutral' | 'Unknown';
  config: string;
  team: 'Blue' | 'Red' | 'Neutral' | 'Unknown';
  status: ActorStatus;
  health: number | null;
  maxHealth: number | null;
  source: string;
  sourceState: 'known' | 'partial' | 'unknown';
  recent: boolean;
  runtimeIds: string[];
}

export interface RuntimeEntity {
  id: string;
  kind: 'Projectile' | 'Area' | 'Summon' | 'Effect';
  name: string;
  definition: string;
  status: RuntimeStatus;
  completeness: 'complete' | 'partial' | 'unknown';
  frameRange: string;
  sourceActorId: number | null;
  ownerActorId: number | null;
  targetActorId: number | null;
  trace: string;
  endReason: string;
}

export interface BuffEntity {
  id: string;
  name: string;
  definition: string;
  stacks: number;
  duration: string;
  remaining: string;
  sourceActorId: number | null;
  status: 'active' | 'expiring' | 'ended';
  traceId: string;
}

export interface BattleEvent {
  id: string;
  frame: number;
  time: string;
  actorId: number;
  actorName: string;
  kind: string;
  outcome: 'success' | 'rejected' | 'failed';
  summary: string;
  traceId: string;
  trigger?: TriggerExecutionDetails;
}

export interface TriggerExecutionDetails {
  triggerId: number;
  triggerName: string;
  eventId: string;
  context: string;
  origin: string;
  budget: { result: 'passed' | 'blocked'; depth: number; frameCount: number; rootCount: number; sameTriggerCount: number; reason: string };
  conditions: { name: string; expression: string; result: 'passed' | 'failed' | 'skipped'; actual: string; expected: string }[];
  plan: { result: 'passed' | 'blocked'; summary: string };
  actions: { name: string; target: string; result: 'completed' | 'failed' | 'skipped'; detail: string; frame: number }[];
  evidence: 'runtime-summary' | 'prototype-detail';
}

export interface TriggerAggregateGroup {
  triggerId: number;
  triggerName: string;
  contextKind: string;
  originKind: string;
  stage: 'Budget' | 'Conditions' | 'Plan' | 'Execution';
  result: 'Passed' | 'Failed' | 'Blocked' | 'Skipped';
  occurrenceCount: number;
  firstFrame: number;
  lastFrame: number;
  failureKey: string;
  sampleReason: string;
  sampleEventId: string;
}

export interface TraceNode {
  id: string;
  parentId: string | null;
  frame: number;
  actorId: number;
  kind: string;
  label: string;
  duration: string;
  status: 'success' | 'failed' | 'running';
  detail: string;
}

export interface AttributeHistorySample {
  frame: number;
  baseValue: number;
  finalValue: number;
}

export interface ActorAttribute {
  id: number;
  name: string;
  unit: string;
  baseValue: number;
  finalValue: number;
  modifierCount: number;
  history: AttributeHistorySample[];
}

export interface AttributeModifier {
  id: string;
  attributeId: number;
  operation: 'add' | 'multiply' | 'override' | 'addPercent';
  magnitude: number;
  priority: number;
  magnitudeType: string;
  declaredValue: number;
  stackedValue: number;
  projectedValue: number;
  currentValue: number | null;
  capturedValue: number | null;
  evaluationPolicy: string;
  captureMode: string;
  explanation: string;
  sourceId: number;
  sourceName: string;
  sourceActorId: number | null;
  stackCount: number;
  sourceKind: string;
  sourceState: 'active' | 'inactive';
  elapsed: string;
  remaining: string;
}

export const actors: ActorEntity[] = [
  { id: 1042, name: '远程单位 A', kind: 'Hero', config: 'HeroConfig:101', team: 'Blue', status: 'alive', health: 742, maxHealth: 1200, source: '玩家创建', sourceState: 'known', recent: true, runtimeIds: ['cast:421:1'] },
  { id: 1077, name: '近战单位 B', kind: 'Hero', config: 'HeroConfig:204', team: 'Red', status: 'alive', health: 310, maxHealth: 980, source: '玩家创建', sourceState: 'known', recent: true, runtimeIds: [] },
  { id: 1118, name: '前排兵线 01', kind: 'Minion', config: 'MinionConfig:12', team: 'Blue', status: 'alive', health: 280, maxHealth: 300, source: '波次 #18', sourceState: 'known', recent: false, runtimeIds: [] },
  { id: 1131, name: '前排兵线 02', kind: 'Minion', config: 'MinionConfig:12', team: 'Blue', status: 'alive', health: 274, maxHealth: 300, source: '波次 #18', sourceState: 'known', recent: false, runtimeIds: [] },
  { id: 1210, name: '中立营地', kind: 'Neutral', config: 'NeutralConfig:7', team: 'Neutral', status: 'dead', health: 0, maxHealth: 800, source: '中立刷新', sourceState: 'known', recent: false, runtimeIds: [] },
  { id: 2088, name: '', kind: 'Unknown', config: '', team: 'Unknown', status: 'unknown', health: null, maxHealth: null, source: '', sourceState: 'unknown', recent: false, runtimeIds: [] },
  { id: 3012, name: 'Tibbers', kind: 'Summon', config: 'SummonConfig:33', team: 'Blue', status: 'alive', health: 1520, maxHealth: 1700, source: 'Actor #1042', sourceState: 'known', recent: true, runtimeIds: ['summon:710:2'] },
  { id: 3019, name: '训练假人', kind: 'Neutral', config: 'TrainingTarget:1', team: 'Neutral', status: 'alive', health: 9999, maxHealth: 9999, source: '测试场景', sourceState: 'known', recent: false, runtimeIds: [] }
];

export const runtimeObjects: RuntimeEntity[] = [
  { id: 'summon:710:2', kind: 'Summon', name: 'Tibbers', definition: 'SummonDefinition:33', status: 'active', completeness: 'complete', frameRange: '1824 → now', sourceActorId: 1042, ownerActorId: 1042, targetActorId: 1077, trace: 'trace:cast-421', endReason: '' },
  { id: 'projectile:882:1', kind: 'Projectile', name: 'Arcane Bolt', definition: 'ProjectileDefinition:88', status: 'active', completeness: 'complete', frameRange: '1837 → 1846', sourceActorId: 1042, ownerActorId: 1042, targetActorId: 1077, trace: 'trace:cast-424', endReason: '' },
  { id: 'area:94:3', kind: 'Area', name: 'Burning Ground', definition: 'AreaDefinition:94', status: 'ended', completeness: 'partial', frameRange: '1802 → 1831', sourceActorId: 1042, ownerActorId: 1042, targetActorId: null, trace: 'trace:cast-398', endReason: '超出持续时间' },
  { id: 'effect:500:4', kind: 'Effect', name: '减速效果', definition: 'EffectDefinition:500', status: 'partial', completeness: 'partial', frameRange: '1840 → ?', sourceActorId: 1077, ownerActorId: 1042, targetActorId: 1077, trace: 'trace:cast-424', endReason: '结束帧未采集' },
  { id: 'projectile:900:1', kind: 'Projectile', name: '', definition: '', status: 'unknown', completeness: 'unknown', frameRange: '未知', sourceActorId: null, ownerActorId: null, targetActorId: null, trace: '', endReason: '' }
];

export const buffs: BuffEntity[] = [
  { id: 'buff:771:3', name: '奥术灼烧', definition: 'BuffDefinition:771', stacks: 2, duration: '180 帧', remaining: '42 帧', sourceActorId: 1042, status: 'active', traceId: 'trace:cast-424' },
  { id: 'buff:114:8', name: '移速提升', definition: 'BuffDefinition:114', stacks: 1, duration: '90 帧', remaining: '11 帧', sourceActorId: 1042, status: 'expiring', traceId: 'trace:cast-421' },
  { id: 'buff:500:4', name: '减速', definition: 'BuffDefinition:500', stacks: 1, duration: '60 帧', remaining: '24 帧', sourceActorId: 1042, status: 'active', traceId: 'trace:cast-424' },
  { id: 'buff:203:1', name: '护盾', definition: 'BuffDefinition:203', stacks: 1, duration: '120 帧', remaining: '0 帧', sourceActorId: 1077, status: 'ended', traceId: 'trace:cast-419' },
  { id: 'buff:400:1', name: '守护祝福', definition: 'BuffDefinition:400', stacks: 1, duration: '240 帧', remaining: '0 帧', sourceActorId: 1077, status: 'ended', traceId: 'trace:cast-417' }
];

export const battleEvents: BattleEvent[] = [
  { id: 'evt-1842-022', frame: 1842, time: '12:04.733', actorId: 1042, actorName: '远程单位 A', kind: 'TriggerEvaluated', outcome: 'failed', summary: 'Trigger #771 · Execution 失败：伤害提交被拒绝', traceId: 'trace:cast-424', trigger: {
    triggerId: 771, triggerName: '命中后施加伤害与灼烧', eventId: 'projectile.hit', context: 'ProjectileHitContext · projectile:882:1', origin: '直接调用 · Global',
    budget: { result: 'passed', depth: 2, frameCount: 18, rootCount: 1, sameTriggerCount: 1, reason: 'Budget 内，允许继续评估' },
    conditions: [
      { name: '目标存在', expression: 'targetActorId > 0', result: 'passed', actual: '#1077', expected: '有效 Actor' },
      { name: '目标为敌方', expression: 'relation(caster, target) == Enemy', result: 'passed', actual: 'Enemy', expected: 'Enemy' },
      { name: '目标存活', expression: 'target.isAlive', result: 'passed', actual: 'true', expected: 'true' }
    ],
    plan: { result: 'passed', summary: '3 个条件通过 · 进入 Action 执行' },
    actions: [
      { name: 'ApplyDamage', target: 'Actor #1077', result: 'failed', detail: '提交结果被拒绝 · failureKey: damage.commit_rejected', frame: 1842 },
      { name: 'ApplyBuff', target: 'Actor #1077', result: 'skipped', detail: '前置 Action 失败，当前示例标记为未执行', frame: 1842 }
    ], evidence: 'prototype-detail'
  } },
  { id: 'evt-1841-013', frame: 1841, time: '12:04.700', actorId: 1042, actorName: '远程单位 A', kind: 'TriggerEvaluated', outcome: 'rejected', summary: 'Trigger #790 · Budget 阻断：递归深度达到上限', traceId: 'trace:cast-424', trigger: {
    triggerId: 790, triggerName: '连锁效果预算检查', eventId: 'effect.applied', context: 'EffectContext · effect:500:4', origin: '直接调用 · Global',
    budget: { result: 'blocked', depth: 8, frameCount: 21, rootCount: 3, sameTriggerCount: 4, reason: '递归深度达到当前上限，未继续评估条件' },
    conditions: [],
    plan: { result: 'blocked', summary: 'Budget 阶段拦截 · Conditions 与 Action 均未进入' },
    actions: [
      { name: 'ApplyDamage', target: 'Actor #1077', result: 'skipped', detail: 'Budget 已拦截，未调用 Plan Action', frame: 1841 }
    ], evidence: 'prototype-detail'
  } },
  { id: 'evt-1842-021', frame: 1842, time: '12:04.733', actorId: 1042, actorName: '远程单位 A', kind: 'TriggerEvaluated', outcome: 'success', summary: 'Trigger #771 · 命中后施加伤害与灼烧', traceId: 'trace:cast-424', trigger: {
    triggerId: 771, triggerName: '命中后施加伤害与灼烧', eventId: 'projectile.hit', context: 'ProjectileHitContext · projectile:882:1', origin: '直接调用 · Global',
    budget: { result: 'passed', depth: 2, frameCount: 18, rootCount: 1, sameTriggerCount: 1, reason: 'Budget 内，允许继续评估' },
    conditions: [
      { name: '目标存在', expression: 'targetActorId > 0', result: 'passed', actual: '#1077', expected: '有效 Actor' },
      { name: '目标为敌方', expression: 'relation(caster, target) == Enemy', result: 'passed', actual: 'Enemy', expected: 'Enemy' },
      { name: '目标存活', expression: 'target.isAlive', result: 'passed', actual: 'true', expected: 'true' }
    ],
    plan: { result: 'passed', summary: '3 个条件全部通过 · 进入 2 个 Action' },
    actions: [
      { name: 'ApplyDamage', target: 'Actor #1077', result: 'completed', detail: '魔法伤害 126 · HP 310 → 184', frame: 1842 },
      { name: 'ApplyBuff', target: 'Actor #1077', result: 'completed', detail: '奥术灼烧 · Buff #771 · 层数 1 → 2', frame: 1842 }
    ], evidence: 'prototype-detail'
  } },
  { id: 'evt-1842-019', frame: 1842, time: '12:04.733', actorId: 1042, actorName: '远程单位 A', kind: 'BuffApplied', outcome: 'success', summary: '对目标 #1077 施加奥术灼烧', traceId: 'trace:cast-424' },
  { id: 'evt-1842-018', frame: 1842, time: '12:04.700', actorId: 1077, actorName: '近战单位 B', kind: 'DamageResolved', outcome: 'success', summary: '受到 126 点魔法伤害', traceId: 'trace:cast-424' },
  { id: 'evt-1841-011', frame: 1841, time: '12:04.667', actorId: 1042, actorName: '远程单位 A', kind: 'ProjectileHit', outcome: 'success', summary: '投射物 projectile:882:1 命中目标', traceId: 'trace:cast-424' },
  { id: 'evt-1840-006', frame: 1840, time: '12:04.633', actorId: 1077, actorName: '近战单位 B', kind: 'SkillRejected', outcome: 'rejected', summary: '施法被控制状态阻止', traceId: 'trace:cast-423' },
  { id: 'evt-1837-003', frame: 1837, time: '12:04.533', actorId: 1042, actorName: '远程单位 A', kind: 'SkillCast', outcome: 'success', summary: '开始施放 Arcane Bolt', traceId: 'trace:cast-424' },
  { id: 'evt-1835-022', frame: 1835, time: '12:04.467', actorId: 3012, actorName: 'Tibbers', kind: 'TargetChanged', outcome: 'success', summary: '切换攻击目标至 #1077', traceId: 'trace:summon-710' },
  { id: 'evt-1834-016', frame: 1834, time: '12:04.433', actorId: 1042, actorName: '远程单位 A', kind: 'TriggerEvaluated', outcome: 'rejected', summary: 'Trigger #772 · 目标类型条件不通过，动作未执行', traceId: 'trace:cast-424', trigger: {
    triggerId: 772, triggerName: '命中后追加控制', eventId: 'projectile.hit', context: 'ProjectileHitContext · projectile:882:1', origin: '事件订阅 · OwnerBound',
    budget: { result: 'passed', depth: 1, frameCount: 12, rootCount: 1, sameTriggerCount: 1, reason: 'Budget 内，允许继续评估' },
    conditions: [
      { name: '目标存在', expression: 'targetActorId > 0', result: 'passed', actual: '#1077', expected: '有效 Actor' },
      { name: '目标属于英雄', expression: 'target.kind == Hero', result: 'failed', actual: 'Minion', expected: 'Hero' },
      { name: '目标存活', expression: 'target.isAlive', result: 'skipped', actual: '未评估', expected: 'true' }
    ],
    plan: { result: 'blocked', summary: '条件组合未通过 · 后续条件与 Action 按短路规则跳过' },
    actions: [
      { name: 'ApplyControl', target: 'Actor #1077', result: 'skipped', detail: 'Plan 未通过，未调用 Action', frame: 1834 }
    ], evidence: 'prototype-detail'
  } }
];

export const triggerAggregateGroups: TriggerAggregateGroup[] = [
  { triggerId: 772, triggerName: '命中后追加控制', contextKind: 'ProjectileHit', originKind: 'OwnerBound', stage: 'Conditions', result: 'Failed', occurrenceCount: 7, firstFrame: 1834, lastFrame: 1842, failureKey: 'target.kind', sampleReason: '目标类型 Minion，不满足 Hero 条件', sampleEventId: 'evt-1834-016' },
  { triggerId: 790, triggerName: '连锁效果预算检查', contextKind: 'EffectContext', originKind: 'Global', stage: 'Budget', result: 'Blocked', occurrenceCount: 1, firstFrame: 1841, lastFrame: 1841, failureKey: 'trigger_budget_depth', sampleReason: '递归深度达到当前上限', sampleEventId: 'evt-1840-006' },
  { triggerId: 771, triggerName: '命中后施加伤害与灼烧', contextKind: 'ProjectileHit', originKind: 'Global', stage: 'Execution', result: 'Failed', occurrenceCount: 2, firstFrame: 1839, lastFrame: 1842, failureKey: 'damage.commit_rejected', sampleReason: '伤害提交被拒绝，需检查目标状态与提交结果', sampleEventId: 'evt-1842-021' },
  { triggerId: 771, triggerName: '命中后施加伤害与灼烧', contextKind: 'ProjectileHit', originKind: 'Global', stage: 'Execution', result: 'Passed', occurrenceCount: 42, firstFrame: 1801, lastFrame: 1842, failureKey: '', sampleReason: '动作链正常完成；频繁出现本身不判定为异常', sampleEventId: 'evt-1842-021' },
  { triggerId: 771, triggerName: '命中后施加伤害与灼烧', contextKind: 'ProjectileHit', originKind: 'Global', stage: 'Conditions', result: 'Passed', occurrenceCount: 42, firstFrame: 1801, lastFrame: 1842, failureKey: '', sampleReason: '条件组通过', sampleEventId: 'evt-1842-021' }
];

export const traceNodes: TraceNode[] = [
  { id: 'trace:cast-424', parentId: null, frame: 1837, actorId: 1042, kind: 'SkillCast', label: 'Arcane Bolt · 施法开始', duration: '1837-1842', status: 'success', detail: '技能实例 cast:424 · 输入校验通过' },
  { id: 'trace:cast-424:target', parentId: 'trace:cast-424', frame: 1838, actorId: 1042, kind: 'TargetQuery', label: '目标查询', duration: '1 帧', status: 'success', detail: '命中 Actor #1077 · 距离 6.2' },
  { id: 'trace:cast-424:projectile', parentId: 'trace:cast-424', frame: 1839, actorId: 1042, kind: 'ProjectileSpawned', label: '生成投射物', duration: '2 帧', status: 'success', detail: 'Runtime Object projectile:882:1' },
  { id: 'trace:cast-424:hit', parentId: 'trace:cast-424:projectile', frame: 1841, actorId: 1042, kind: 'ProjectileHit', label: '投射物命中', duration: '1 帧', status: 'success', detail: '目标 Actor #1077 · 命中策略 Hit' },
  { id: 'trace:cast-424:damage', parentId: 'trace:cast-424:hit', frame: 1842, actorId: 1042, kind: 'DamageResolved', label: '伤害结算', duration: '1 帧', status: 'success', detail: '魔法伤害 126 · 目标 HP 310 → 184' },
  { id: 'trace:cast-424:buff', parentId: 'trace:cast-424:hit', frame: 1842, actorId: 1042, kind: 'BuffApplied', label: '施加奥术灼烧', duration: '1 帧', status: 'success', detail: 'Buff buff:771:3 · 层数 2' },
  { id: 'trace:cast-423', parentId: null, frame: 1840, actorId: 1077, kind: 'SkillRejected', label: '施法被拒绝', duration: '0 帧', status: 'failed', detail: '拒绝原因：控制状态不允许施法' }
];

export const actorAttributes: ActorAttribute[] = [
  { id: 1, name: '最大生命值', unit: 'HP', baseValue: 1000, finalValue: 1200, modifierCount: 2, history: [
    { frame: 1831, baseValue: 1000, finalValue: 1200 }, { frame: 1832, baseValue: 1000, finalValue: 1200 },
    { frame: 1833, baseValue: 1000, finalValue: 1200 }, { frame: 1834, baseValue: 1000, finalValue: 1200 },
    { frame: 1835, baseValue: 1000, finalValue: 1200 }, { frame: 1836, baseValue: 1000, finalValue: 1200 },
    { frame: 1837, baseValue: 1000, finalValue: 1200 }, { frame: 1838, baseValue: 1000, finalValue: 1200 },
    { frame: 1839, baseValue: 1000, finalValue: 1200 }, { frame: 1840, baseValue: 1000, finalValue: 1200 },
    { frame: 1841, baseValue: 1000, finalValue: 1200 }, { frame: 1842, baseValue: 1000, finalValue: 1200 }
  ] },
  { id: 2, name: '攻击力', unit: 'AD', baseValue: 72, finalValue: 84.5, modifierCount: 2, history: [
    { frame: 1831, baseValue: 72, finalValue: 80 }, { frame: 1832, baseValue: 72, finalValue: 80 },
    { frame: 1833, baseValue: 72, finalValue: 80 }, { frame: 1834, baseValue: 72, finalValue: 80 },
    { frame: 1835, baseValue: 72, finalValue: 80 }, { frame: 1836, baseValue: 72, finalValue: 80 },
    { frame: 1837, baseValue: 72, finalValue: 80 }, { frame: 1838, baseValue: 72, finalValue: 80 },
    { frame: 1839, baseValue: 72, finalValue: 80 }, { frame: 1840, baseValue: 72, finalValue: 80 },
    { frame: 1841, baseValue: 72, finalValue: 80 }, { frame: 1842, baseValue: 72, finalValue: 84.5 }
  ] },
  { id: 3, name: '法术强度', unit: 'AP', baseValue: 120, finalValue: 120, modifierCount: 0, history: [
    { frame: 1831, baseValue: 120, finalValue: 120 }, { frame: 1832, baseValue: 120, finalValue: 120 },
    { frame: 1833, baseValue: 120, finalValue: 120 }, { frame: 1834, baseValue: 120, finalValue: 120 },
    { frame: 1835, baseValue: 120, finalValue: 120 }, { frame: 1836, baseValue: 120, finalValue: 120 },
    { frame: 1837, baseValue: 120, finalValue: 120 }, { frame: 1838, baseValue: 120, finalValue: 120 },
    { frame: 1839, baseValue: 120, finalValue: 120 }, { frame: 1840, baseValue: 120, finalValue: 120 },
    { frame: 1841, baseValue: 120, finalValue: 120 }, { frame: 1842, baseValue: 120, finalValue: 120 }
  ] },
  { id: 4, name: '移动速度', unit: 'u/s', baseValue: 350, finalValue: 385, modifierCount: 1, history: [
    { frame: 1831, baseValue: 350, finalValue: 385 }, { frame: 1832, baseValue: 350, finalValue: 385 },
    { frame: 1833, baseValue: 350, finalValue: 385 }, { frame: 1834, baseValue: 350, finalValue: 385 },
    { frame: 1835, baseValue: 350, finalValue: 385 }, { frame: 1836, baseValue: 350, finalValue: 385 },
    { frame: 1837, baseValue: 350, finalValue: 385 }, { frame: 1838, baseValue: 350, finalValue: 385 },
    { frame: 1839, baseValue: 350, finalValue: 385 }, { frame: 1840, baseValue: 350, finalValue: 385 },
    { frame: 1841, baseValue: 350, finalValue: 385 }, { frame: 1842, baseValue: 350, finalValue: 385 }
  ] },
  { id: 5, name: '攻击间隔', unit: 's', baseValue: 1.4, finalValue: 1.4, modifierCount: 0, history: [
    { frame: 1831, baseValue: 1.4, finalValue: 1.4 }, { frame: 1832, baseValue: 1.4, finalValue: 1.4 },
    { frame: 1833, baseValue: 1.4, finalValue: 1.4 }, { frame: 1834, baseValue: 1.4, finalValue: 1.4 },
    { frame: 1835, baseValue: 1.4, finalValue: 1.4 }, { frame: 1836, baseValue: 1.4, finalValue: 1.4 },
    { frame: 1837, baseValue: 1.4, finalValue: 1.4 }, { frame: 1838, baseValue: 1.4, finalValue: 1.4 },
    { frame: 1839, baseValue: 1.4, finalValue: 1.4 }, { frame: 1840, baseValue: 1.4, finalValue: 1.4 },
    { frame: 1841, baseValue: 1.4, finalValue: 1.4 }, { frame: 1842, baseValue: 1.4, finalValue: 1.4 }
  ] }
];

export const attributeModifiers: AttributeModifier[] = [
  { id: 'mod:buff-771:attack', attributeId: 2, operation: 'add', magnitude: 8, priority: 20, magnitudeType: '固定值', declaredValue: 4, stackedValue: 8, projectedValue: 8, currentValue: 8, capturedValue: null, evaluationPolicy: '实时', captureMode: '不适用', explanation: 'Buff 当前 2 层，每层提供 +4 攻击力。', sourceId: 771, sourceName: '奥术灼烧', sourceActorId: 1042, stackCount: 2, sourceKind: 'Buff', sourceState: 'active', elapsed: '2.3 秒', remaining: '1.4 秒' },
  { id: 'mod:passive-14:attack', attributeId: 2, operation: 'addPercent', magnitude: 4.5, priority: 10, magnitudeType: '属性引用', declaredValue: 4.5, stackedValue: 4.5, projectedValue: 4.5, currentValue: 4.5, capturedValue: 4.5, evaluationPolicy: '应用时快照', captureMode: 'OnApply', explanation: '基于施加时的攻击力快照计算；当前值与捕获值一致。', sourceId: 14, sourceName: '战术增幅', sourceActorId: 1042, stackCount: 1, sourceKind: '被动效果', sourceState: 'active', elapsed: '持续中', remaining: '持续中' },
  { id: 'mod:buff-114:speed', attributeId: 4, operation: 'multiply', magnitude: 1.1, priority: 15, magnitudeType: '固定值', declaredValue: 1.1, stackedValue: 1.1, projectedValue: 385, currentValue: 385, capturedValue: null, evaluationPolicy: '实时', captureMode: '不适用', explanation: '当前移动速度 = 基础移动速度 × 1.1。', sourceId: 114, sourceName: '移速提升', sourceActorId: 1042, stackCount: 1, sourceKind: 'Buff', sourceState: 'active', elapsed: '1.1 秒', remaining: '11 帧' },
  { id: 'mod:buff-400:health', attributeId: 1, operation: 'add', magnitude: 200, priority: 30, magnitudeType: '等级曲线', declaredValue: 200, stackedValue: 200, projectedValue: 200, currentValue: null, capturedValue: 200, evaluationPolicy: '应用时快照', captureMode: 'OnApply', explanation: '按施加帧等级曲线计算；旧 Artifact 可能不包含当前值。', sourceId: 400, sourceName: '守护祝福', sourceActorId: 1077, stackCount: 1, sourceKind: 'Buff', sourceState: 'inactive', elapsed: '已结束', remaining: '0 帧' }
];
