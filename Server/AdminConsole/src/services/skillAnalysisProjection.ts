import type { AdminSkillDiagnosticsEvents, SkillAnalysisEntityGroupProjection, SkillAnalysisEntityRelationProjection, SkillAnalysisFilterOptions, SkillAnalysisFilterState, SkillAnalysisFlatNodeProjection, SkillAnalysisNodeProjection, SkillAnalysisTimelineEventProjection } from '../types';
import { toNumber, toText } from './skillAcceptanceAnalysis';

export function createDefaultSkillAnalysisFilter(): SkillAnalysisFilterState {
  return {
    searchText: '',
    severity: 'all',
    stage: 'all',
    kind: 'all',
    entityKind: 'all',
    actorId: '',
    skillId: '',
    configId: '',
    rootId: '',
    contextId: '',
    onlyFailures: false
  };
}

export function inferSkillAnalysisStage(kind: string): string {
  const normalized = kind.toLowerCase();
  if (includesAny(normalized, ['condition', 'predicate', 'filter', 'budget', 'cost', 'cooldown'])) return 'trigger-condition';
  if (includesAny(normalized, ['trigger', 'event', 'passive', 'aura'])) return 'trigger-lineage';
  if (includesAny(normalized, ['action', 'plan', 'command'])) return 'trigger-action';
  if (includesAny(normalized, ['buff', 'debuff', 'modifier', 'stack'])) return 'buff-lifecycle';
  if (includesAny(normalized, ['continuous', 'periodic', 'tick', 'interval'])) return 'continuous-tick';
  if (includesAny(normalized, ['projectile', 'bullet', 'missile'])) return 'projectile-lifecycle';
  if (includesAny(normalized, ['area', 'aoe', 'zone'])) return 'area-lifecycle';
  if (includesAny(normalized, ['shield', 'absorb'])) return 'shield-lifecycle';
  if (includesAny(normalized, ['summon', 'pet', 'minion'])) return 'summon-lifecycle';
  if (includesAny(normalized, ['displacement', 'move', 'dash', 'blink', 'knockback', 'movement'])) return 'movement';
  if (includesAny(normalized, ['damage', 'heal', 'attackcalc', 'mitigate'])) return 'damage-pipeline';
  if (includesAny(normalized, ['presentation', 'cue', 'vfx', 'sfx', 'animation'])) return 'presentation-events';
  if (includesAny(normalized, ['effect'])) return 'effect-execution';
  if (includesAny(normalized, ['assert', 'expect', 'result'])) return 'assertion-result';
  if (includesAny(normalized, ['cast', 'skill', 'active', 'channel', 'charge'])) return 'cast-pipeline';
  return 'trigger-lineage';
}

export function inferSkillAnalysisSeverity(record: Record<string, unknown>, status: string): string {
  const severity = toText(record.severity).toLowerCase();
  if (severity) return severity;
  const failed = record.passed === false || status === 'failed' || Boolean(record.failReason) || Boolean(record.failure);
  return failed ? 'error' : 'info';
}

export function inferSkillAnalysisEntityKind(kind: string, stage: string, record: Record<string, unknown>): string {
  const text = buildClassificationText(kind, stage, record);
  if (includesAny(text, ['condition', 'predicate', 'filter', 'budget', 'cost', 'cooldown'])) return 'trigger-condition';
  if (includesAny(text, ['passive', 'aura'])) return 'passive-skill';
  if (includesAny(text, ['trigger', 'event'])) return 'trigger-source';
  if (includesAny(text, ['continuous', 'periodic', 'interval'])) return 'continuous';
  if (includesAny(text, ['projectile', 'bullet', 'missile'])) return 'projectile';
  if (includesAny(text, ['area', 'aoe', 'zone'])) return 'area';
  if (includesAny(text, ['shield', 'absorb'])) return 'shield';
  if (includesAny(text, ['summon', 'pet', 'minion'])) return 'summon';
  if (includesAny(text, ['displacement', 'move', 'dash', 'blink', 'knockback', 'movement'])) return 'movement';
  if (includesAny(text, ['buff', 'debuff', 'modifier', 'stack'])) return 'buff';
  if (includesAny(text, ['damage', 'heal', 'attackcalc', 'mitigate'])) return 'damage';
  if (includesAny(text, ['presentation', 'cue', 'vfx', 'sfx', 'animation'])) return 'presentation';
  if (includesAny(text, ['action', 'plan', 'command'])) return 'effect-action';
  if (includesAny(text, ['effect'])) return 'effect';
  if (includesAny(text, ['skill', 'cast', 'active', 'channel', 'charge'])) return 'actor-skill';
  return 'context';
}

export function buildSkillAnalysisNodeProjection(record: Record<string, unknown>, source: string, caseId = ''): SkillAnalysisNodeProjection | null {
  const numericNodeId = toNumber(record.nodeId);
  if (numericNodeId <= 0) return null;

  const kind = toText(record.kind) || toText(record.eventType) || '未知';
  const status = toText(record.status) || (record.isEnded === true ? 'ended' : 'running');
  const failure = toText(record.failReason) || toText(record.failure) || toText(record.reason) || null;
  const configId = toNumber(record.configId || record.runtimeConfigId || record.effectId || record.actionId);
  const sourceActorId = toNumber(record.sourceActorId || record.casterActorId);
  const targetActorId = toNumber(record.targetActorId);
  const actorId = toNumber(record.actorId || sourceActorId || targetActorId);
  const skillId = toNumber(record.skillId);
  const frame = toNumber(record.frame);
  const timeMs = toNumber(record.timeMs);
  const stage = toText(record.stage) || inferSkillAnalysisStage(kind);
  const entityKind = inferSkillAnalysisEntityKind(kind, stage, record);
  const domain = inferSkillAnalysisDomain(kind, stage, entityKind, record);
  const domainLabel = skillAnalysisDomainLabel(domain);
  const laneLabel = skillAnalysisLaneLabel(domain, stage, entityKind);
  const triggerLabel = resolveTriggerLabel(record, domain);
  const conditionLabel = resolveConditionLabel(record, domain);
  const actionLabel = resolveActionLabel(record, domain, entityKind);
  const sourceContextId = String(toNumber(record.sourceContextId || record.contextId || record.immediateContextId) || numericNodeId);
  const rootContextId = String(toNumber(record.rootContextId || record.rootId) || numericNodeId);
  const ownerContextId = String(toNumber(record.ownerContextId || record.parentContextId || record.rootId) || 0);
  const entityKey = buildEntityKey(entityKind, record, numericNodeId, configId, actorId, sourceContextId);
  const displayName = resolveTraceDisplayName(record, kind, entityKind, configId, skillId);
  const configLabel = resolveConfigLabel(record, kind, entityKind, configId, skillId);
  const runtimeLabel = resolveRuntimeLabel(record, entityKind, numericNodeId, sourceContextId);
  const actorLabel = resolveActorLabel(record, 'actor', actorId);
  const sourceActorLabel = resolveActorLabel(record, 'source', sourceActorId);
  const targetActorLabel = resolveActorLabel(record, 'target', targetActorId);
  const detailLabel = [configLabel, runtimeLabel, sourceActorLabel, targetActorLabel].filter(Boolean).join(' / ');

  return {
    nodeId: String(numericNodeId),
    numericNodeId,
    rootId: String(toNumber(record.rootId) || numericNodeId),
    numericRootId: toNumber(record.rootId) || numericNodeId,
    parentId: String(toNumber(record.parentId) || 0),
    numericParentId: toNumber(record.parentId),
    kind,
    stage,
    label: displayName,
    displayName,
    detailLabel,
    configLabel,
    actorLabel,
    sourceActorLabel,
    targetActorLabel,
    runtimeLabel,
    configId: configId > 0 ? configId : null,
    frame,
    timeMs,
    actorId: actorId > 0 ? actorId : null,
    skillId: skillId > 0 ? skillId : null,
    sourceActorId: sourceActorId > 0 ? sourceActorId : null,
    targetActorId: targetActorId > 0 ? targetActorId : null,
    rootContextId,
    ownerContextId,
    sourceContextId,
    entityKind,
    entityKey,
    domain,
    domainLabel,
    laneLabel,
    triggerLabel,
    conditionLabel,
    actionLabel,
    status,
    severity: inferSkillAnalysisSeverity(record, status),
    summary: buildNodeSummary(record, caseId),
    source,
    failure,
    raw: record,
    children: []
  };
}

export function buildSkillAnalysisTree(records: Record<string, unknown>[], source: string, caseId = ''): SkillAnalysisNodeProjection[] {
  const nodes = new Map<number, SkillAnalysisNodeProjection>();
  const roots: SkillAnalysisNodeProjection[] = [];

  const sortedRecords = [...records].sort((a, b) => {
    const rootCompare = toNumber(a.rootId) - toNumber(b.rootId);
    if (rootCompare !== 0) return rootCompare;
    const frameCompare = toNumber(a.frame) - toNumber(b.frame);
    if (frameCompare !== 0) return frameCompare;
    return toNumber(a.nodeId) - toNumber(b.nodeId);
  });

  for (const record of sortedRecords) {
    const node = buildSkillAnalysisNodeProjection(record, source, caseId);
    if (node) nodes.set(node.numericNodeId, node);
  }

  for (const node of nodes.values()) {
    const parent = node.numericParentId > 0 ? nodes.get(node.numericParentId) : null;
    if (parent) parent.children.push(node);
    else roots.push(node);
  }

  sortSkillAnalysisNodes(roots);
  return roots;
}

export function flattenSkillAnalysisTree(nodes: SkillAnalysisNodeProjection[], depth = 0): SkillAnalysisFlatNodeProjection[] {
  const result: SkillAnalysisFlatNodeProjection[] = [];
  for (const node of nodes) {
    result.push({ ...node, depth });
    if (node.children.length > 0) result.push(...flattenSkillAnalysisTree(node.children, depth + 1));
  }
  return result;
}

export function buildSkillAnalysisFilterOptions(nodes: SkillAnalysisFlatNodeProjection[]): SkillAnalysisFilterOptions {
  return {
    severities: uniqueSorted(nodes.map(x => x.severity)),
    stages: uniqueSorted(nodes.map(x => x.stage)),
    kinds: uniqueSorted(nodes.map(x => x.kind)),
    entityKinds: uniqueSorted(nodes.map(x => x.domain || x.entityKind)),
    actorIds: uniqueNumbers(nodes.flatMap(x => [x.actorId || 0, x.sourceActorId || 0, x.targetActorId || 0])),
    skillIds: uniqueNumbers(nodes.map(x => x.skillId || 0)),
    configIds: uniqueNumbers(nodes.map(x => x.configId || 0)),
    rootIds: uniqueSorted(nodes.map(x => x.rootId))
  };
}

export function filterSkillAnalysisNodes(nodes: SkillAnalysisFlatNodeProjection[], filter: SkillAnalysisFilterState): SkillAnalysisFlatNodeProjection[] {
  return nodes.filter(node => nodeMatchesSkillAnalysisFilter(node, filter));
}

export function nodeMatchesSkillAnalysisFilter(node: SkillAnalysisFlatNodeProjection, filter: SkillAnalysisFilterState): boolean {
  if (filter.onlyFailures && !isFailureNode(node)) return false;
  if (!matchesOption(node.severity, filter.severity)) return false;
  if (!matchesOption(node.stage, filter.stage)) return false;
  if (!matchesOption(node.kind, filter.kind)) return false;
  if (!matchesOption(node.domain || node.entityKind, filter.entityKind)) return false;
  if (!matchesNumberFilter([node.actorId, node.sourceActorId, node.targetActorId], filter.actorId)) return false;
  if (!matchesNumberFilter([node.skillId], filter.skillId)) return false;
  if (!matchesNumberFilter([node.configId], filter.configId)) return false;
  if (filter.rootId && node.rootId !== filter.rootId) return false;
  if (filter.contextId && ![node.nodeId, node.parentId, node.rootId, node.sourceContextId, node.rootContextId, node.ownerContextId].includes(filter.contextId)) return false;

  const text = filter.searchText.trim().toLowerCase();
  if (!text) return true;
  const haystack = [node.label, node.displayName, node.detailLabel, node.configLabel, node.actorLabel, node.sourceActorLabel, node.targetActorLabel, node.runtimeLabel, node.kind, node.stage, node.status, node.severity, node.summary, node.failure || '', node.entityKind, node.domain, node.domainLabel, node.laneLabel, node.triggerLabel, node.conditionLabel, node.actionLabel, node.entityKey, node.nodeId, node.parentId, node.rootId, toText(node.raw)].join(' ').toLowerCase();
  return haystack.includes(text);
}

export function buildSkillAnalysisEntityRelations(nodes: SkillAnalysisFlatNodeProjection[]): SkillAnalysisEntityGroupProjection[] {
  const groups = new Map<string, SkillAnalysisEntityGroupProjection>();
  const relationBuckets = new Map<string, SkillAnalysisFlatNodeProjection[]>();

  for (const node of nodes) {
    const ownerKey = buildOwnerKey(node);
    const group = ensureEntityGroup(groups, ownerKey, node);
    group.totalNodes += 1;
    if (isFailureNode(node)) group.failureCount += 1;

    const relationKey = `${ownerKey}:${node.domain || node.entityKind}:${node.entityKey}`;
    if (!relationBuckets.has(relationKey)) relationBuckets.set(relationKey, []);
    relationBuckets.get(relationKey)?.push(node);
  }

  for (const [bucketKey, bucketNodes] of relationBuckets.entries()) {
    const first = bucketNodes[0];
    const ownerKey = buildOwnerKey(first);
    const relation: SkillAnalysisEntityRelationProjection = {
      id: bucketKey,
      ownerKey,
      entityKind: first.domainLabel || first.entityKind,
      entityKey: first.entityKey,
      label: `${first.displayName} / ${first.runtimeLabel || first.entityKey}`,
      nodeCount: bucketNodes.length,
      failureCount: bucketNodes.filter(isFailureNode).length,
      firstFrame: Math.min(...bucketNodes.map(x => x.frame)),
      lastFrame: Math.max(...bucketNodes.map(x => x.frame)),
      nodes: bucketNodes
    };
    groups.get(ownerKey)?.relations.push(relation);
  }

  return [...groups.values()]
    .map(group => ({ ...group, relations: group.relations.sort((a, b) => b.failureCount - a.failureCount || a.firstFrame - b.firstFrame || a.label.localeCompare(b.label)) }))
    .sort((a, b) => b.failureCount - a.failureCount || b.totalNodes - a.totalNodes || a.label.localeCompare(b.label));
}

export function buildTimelineFromAnalysisNodes(nodes: SkillAnalysisFlatNodeProjection[]): SkillAnalysisTimelineEventProjection[] {
  return nodes
    .map(node => ({
      id: `${node.source}:${node.nodeId}`,
      frame: node.frame,
      timeMs: node.timeMs,
      lane: node.laneLabel || skillAnalysisLaneLabel(node.domain, node.stage, node.entityKind),
      label: node.detailLabel ? `${node.displayName} · ${node.detailLabel}` : node.displayName,
      nodeId: node.nodeId,
      severity: node.severity,
      source: node.source
    }))
    .sort((a, b) => a.frame - b.frame || a.timeMs - b.timeMs || a.id.localeCompare(b.id));
}

export function buildTimelineFromRuntimeEvents(events: AdminSkillDiagnosticsEvents | null): SkillAnalysisTimelineEventProjection[] {
  if (!events?.isDataAvailable) return [];

  const runtimeEvents = events.events || [];
  const frequency = events.monotonicTimestampFrequency || 0;
  const timestamps = runtimeEvents
    .map(event => event.monotonicTimestamp)
    .filter(timestamp => Number.isFinite(timestamp));
  const origin = timestamps.length > 0 ? Math.min(...timestamps) : 0;
  const hasUsableClock = frequency > 0 && Number.isFinite(origin);

  return runtimeEvents
    .map(event => ({
      id: `runtime:${event.sessionId}:${event.generation}:${event.sequence}`,
      frame: event.frame,
      timeMs: hasUsableClock && Number.isFinite(event.monotonicTimestamp)
        ? Math.max(0, (event.monotonicTimestamp - origin) * 1000 / frequency)
        : 0,
      lane: skillAnalysisLaneLabel(inferRuntimeEventDomain(event.stage, event.eventType), event.stage || 'runtime-event', event.eventType),
      label: `${event.eventType} 角色 ${event.actorId} 技能 ${event.skillId}`,
      nodeId: event.nodeId > 0 ? String(event.nodeId) : null,
      severity: event.severity || 'info',
      source: 'runtime-diagnostics'
    }))
    .sort((a, b) => a.frame - b.frame || a.timeMs - b.timeMs || a.id.localeCompare(b.id));
}

export function buildAnalysisArtifactTraceRecords(artifact: Record<string, unknown> | null | undefined): Record<string, unknown>[] {
  const trace = pickRecord(artifact, 'trace', 'Trace');
  const roots = pickArray(trace, 'roots', 'Roots');
  const records: Record<string, unknown>[] = [];
  for (const root of roots) {
    const rootRecord = asRecord(root);
    const rootId = toNumber(rootRecord.rootId || rootRecord.RootId);
    const nodes = pickArray(rootRecord, 'nodes', 'Nodes');
    for (const node of nodes) {
      const source = asRecord(node);
      const metadata = pickRecord(source, 'metadata', 'Metadata') || {};
      const properties = normalizeAnalysisMetadataProperties(metadata);
      const contextId = toNumber(source.contextId || source.ContextId);
      if (contextId <= 0) continue;
      const parentId = toNumber(source.parentId || source.ParentId);
      const effectiveRootId = toNumber(source.rootId || source.RootId || rootId || contextId);
      const kind = toText(source.kindName || source.KindName) || toText(source.kind || source.Kind) || 'TraceNode';
      const endReason = toText(source.endReasonName || source.EndReasonName) || toText(source.endReason || source.EndReason);
      const metadataDisplay = toText(metadata.display || metadata.Display);
      records.push({
        ...properties,
        contextId,
        nodeId: contextId,
        parentContextId: parentId,
        parentId,
        rootContextId: effectiveRootId,
        rootId: effectiveRootId,
        kind,
        eventType: kind,
        configId: toNumber(properties.configId || metadata.configId || metadata.ConfigId),
        sourceActorId: toNumber(properties.sourceActorId || metadata.sourceActorId || metadata.SourceActorId),
        targetActorId: toNumber(properties.targetActorId || metadata.targetActorId || metadata.TargetActorId),
        sourceId: toNumber(properties.sourceId || metadata.sourceId || metadata.SourceId),
        targetId: toNumber(properties.targetId || metadata.targetId || metadata.TargetId),
        originSourceId: toNumber(properties.originSourceId || metadata.originSourceId || metadata.OriginSourceId),
        originTargetId: toNumber(properties.originTargetId || metadata.originTargetId || metadata.OriginTargetId),
        originSource: toText(properties.originSource || metadata.originSource || metadata.OriginSource),
        originTarget: toText(properties.originTarget || metadata.originTarget || metadata.OriginTarget),
        stage: toText(properties.stage) || inferSkillAnalysisStage(kind),
        status: source.isEnded === true || source.IsEnded === true ? 'ended' : 'running',
        severity: inferArtifactNodeSeverity(source, endReason),
        frame: toNumber(source.endedFrame || source.EndedFrame || properties.frame),
        timeMs: toNumber(properties.timeMs),
        childCount: toNumber(source.childCount || source.ChildCount),
        isRoot: source.isRoot ?? source.IsRoot ?? parentId <= 0,
        isEnded: source.isEnded ?? source.IsEnded ?? false,
        endReason,
        displayName: toText(properties.displayName) || toText(properties.name) || metadataDisplay || `${kind} #${contextId}`,
        runtimeLabel: toText(properties.runtimeLabel) || metadataDisplay || `${kind} context #${contextId}`,
        message: toText(properties.message) || metadataDisplay || endReason,
        rawArtifactNode: source
      });
    }
  }

  return records;
}

export function inferSkillAnalysisDomain(kind: string, stage: string, entityKind: string, record: Record<string, unknown>): string {
  const text = buildClassificationText(kind, stage, record, entityKind);
  if (includesAny(text, ['active', '主动', 'cast', 'skillcast', 'manual'])) return 'active-skill';
  if (includesAny(text, ['passive', '被动', 'aura', '光环'])) return 'passive-skill';
  if (includesAny(text, ['condition', 'predicate', 'filter', 'budget', 'cost', 'cooldown', '条件'])) return 'trigger-condition';
  if (includesAny(text, ['continuous', 'periodic', 'interval', '持续'])) return 'continuous';
  if (includesAny(text, ['buff', 'debuff', 'modifier', 'stack'])) return 'buff';
  if (includesAny(text, ['projectile', 'bullet', 'missile', '子弹', '投射'])) return 'projectile';
  if (includesAny(text, ['area', 'aoe', 'zone', '区域'])) return 'area';
  if (includesAny(text, ['shield', 'absorb', '护盾', '吸收'])) return 'shield';
  if (includesAny(text, ['summon', 'pet', 'minion', '召唤'])) return 'summon';
  if (includesAny(text, ['displacement', 'move', 'dash', 'blink', 'knockback', 'movement', '位移'])) return 'movement';
  if (includesAny(text, ['damage', 'heal', 'attackcalc', 'mitigate', '伤害', '治疗'])) return 'damage';
  if (includesAny(text, ['presentation', 'cue', 'vfx', 'sfx', 'animation', '表现'])) return 'presentation';
  if (includesAny(text, ['action', 'plan', 'command', '行为', '动作'])) return 'trigger-action';
  if (includesAny(text, ['effect', '效果'])) return 'effect';
  if (includesAny(text, ['trigger', 'event', '触发'])) return 'trigger-source';
  if (includesAny(text, ['assert', 'expect', 'result'])) return 'assertion';
  return entityKind === 'actor-skill' ? 'active-skill' : 'context';
}

function inferRuntimeEventDomain(stage: string, eventType: string): string {
  const text = `${stage} ${eventType}`.toLowerCase();
  if (includesAny(text, ['condition', 'predicate', 'filter', 'budget', 'cost', 'cooldown'])) return 'trigger-condition';
  if (includesAny(text, ['continuous', 'periodic', 'interval'])) return 'continuous';
  if (includesAny(text, ['action', 'plan', 'command'])) return 'trigger-action';
  if (includesAny(text, ['buff', 'debuff', 'modifier', 'stack'])) return 'buff';
  if (includesAny(text, ['projectile', 'bullet', 'missile'])) return 'projectile';
  if (includesAny(text, ['area', 'aoe', 'zone'])) return 'area';
  if (includesAny(text, ['shield', 'absorb'])) return 'shield';
  if (includesAny(text, ['summon', 'pet', 'minion'])) return 'summon';
  if (includesAny(text, ['displacement', 'move', 'dash', 'blink', 'knockback', 'movement'])) return 'movement';
  if (includesAny(text, ['damage', 'heal', 'attackcalc', 'mitigate'])) return 'damage';
  if (includesAny(text, ['presentation', 'cue', 'vfx', 'sfx', 'animation'])) return 'presentation';
  if (includesAny(text, ['effect'])) return 'effect';
  if (includesAny(text, ['trigger', 'event', 'passive', 'aura'])) return 'trigger-source';
  if (includesAny(text, ['cast', 'skill', 'active', 'channel', 'charge'])) return 'active-skill';
  return 'context';
}

export function skillAnalysisDomainLabel(domain: string): string {
  const labels: Record<string, string> = {
    'active-skill': '主动技能',
    'passive-skill': '被动/光环',
    'trigger-source': '触发源',
    'trigger-condition': '触发条件',
    'trigger-action': '触发行为',
    effect: '效果执行',
    buff: 'Buff 生命周期',
    continuous: '持续行为',
    projectile: '投射物/子弹',
    area: '区域效果',
    shield: '护盾生命周期',
    summon: '召唤物生命周期',
    movement: '位移/运动',
    damage: '伤害/治疗',
    presentation: '表现事件',
    assertion: '验收断言',
    context: '上下文'
  };
  return labels[domain] || domain || '上下文';
}

function skillAnalysisLaneLabel(domain: string, stage: string, entityKind: string): string {
  const effectiveDomain = domain || inferSkillAnalysisDomain(entityKind, stage, entityKind, {});
  const laneMap: Record<string, string> = {
    'active-skill': 'Skill Pipeline 主动释放',
    'passive-skill': 'Passive / Aura 被动监听',
    'trigger-source': 'Trigger Source 触发源',
    'trigger-condition': 'Trigger Conditions 条件判定',
    'trigger-action': 'Trigger Plan Actions 行为计划',
    effect: 'Effect Execution 效果执行',
    buff: 'Buff Lifecycle Buff 生命周期',
    continuous: 'Continuous Tick 持续行为',
    projectile: 'Projectile Lifecycle 投射物生命周期',
    area: 'Area Lifecycle 区域生命周期',
    shield: 'Shield Lifecycle 护盾生命周期',
    summon: 'Summon Lifecycle 召唤物生命周期',
    movement: 'Movement / Displacement 位移运动',
    damage: 'Damage / Heal Pipeline 结算管线',
    presentation: 'Presentation Events 表现事件',
    assertion: 'Assertion Result 验收断言',
    context: 'Context / Lineage 上下文溯源'
  };
  return laneMap[effectiveDomain] || `${stage}/${entityKind}`;
}

function resolveTriggerLabel(record: Record<string, unknown>, domain: string): string {
  const explicit = firstText(record, ['triggerName', 'triggerDisplayName', 'eventType', 'triggerType', 'sourceEvent', 'listenEvent']);
  if (explicit) return explicit;
  if (domain === 'active-skill') return '玩家/AI 主动释放';
  if (domain === 'passive-skill') return '被动监听/光环刷新';
  if (domain === 'trigger-condition' || domain === 'trigger-action' || domain === 'trigger-source') return '事件触发链路';
  return '';
}

function resolveConditionLabel(record: Record<string, unknown>, domain: string): string {
  const explicit = firstText(record, ['conditionName', 'conditionDisplayName', 'predicateName', 'filterName', 'budgetName', 'costName', 'cooldownName', 'failReason', 'failure', 'reason']);
  if (explicit) return explicit;
  if (domain === 'trigger-condition') return '条件 / 资源 / 冷却判定';
  return '';
}

function resolveActionLabel(record: Record<string, unknown>, domain: string, entityKind: string): string {
  const explicit = firstText(record, ['actionName', 'actionDisplayName', 'effectName', 'behaviorName', 'commandName', 'planName', 'runtimeName']);
  if (explicit) return explicit;
  if (domain === 'trigger-action') return '触发后行为计划';
  if (domain === 'projectile') return '发射/飞行/命中';
  if (domain === 'area') return '生成/进入/停留/离开/过期';
  if (domain === 'shield') return '添加/吸收/移除/过期清理';
  if (domain === 'summon') return '召唤/运行/清理';
  if (domain === 'continuous') return '绑定/周期 Tick/结束';
  if (domain === 'movement') return '位移/运动控制';
  if (domain === 'buff') return '附加/叠层/持续/移除';
  if (domain === 'damage') return '伤害/治疗结算';
  if (domain === 'effect' || entityKind === 'effect') return '效果执行';
  return '';
}

function buildClassificationText(kind: string, stage: string, record: Record<string, unknown>, entityKind = ''): string {
  return [
    kind,
    stage,
    entityKind,
    toText(record.runtimeKind),
    toText(record.entityKind),
    toText(record.eventType),
    toText(record.eventId),
    toText(record.skillKind),
    toText(record.castKind),
    toText(record.triggerType),
    toText(record.triggerId),
    toText(record.effectType),
    toText(record.effectId),
    toText(record.actionType),
    toText(record.actionId),
    toText(record.behaviorType),
    toText(record.conditionType),
    toText(record.configName),
    toText(record.displayName),
    toText(record.message),
    toText(record.projectileId),
    toText(record.launcherId),
    toText(record.areaId),
    toText(record.templateId),
    toText(record.buffId),
    toText(record.buffIds),
    toText(record.shieldId),
    toText(record.instanceId),
    toText(record.damageType),
    toText(record.reasonKind),
    toText(record.reasonParam)
  ].join(' ').toLowerCase();
}

function includesAny(value: string, needles: string[]): boolean {
  return needles.some(needle => value.includes(needle));
}

function sortSkillAnalysisNodes(nodes: SkillAnalysisNodeProjection[]): void {
  nodes.sort((a, b) => {
    const rootCompare = a.numericRootId - b.numericRootId;
    if (rootCompare !== 0) return rootCompare;
    const frameCompare = a.frame - b.frame;
    if (frameCompare !== 0) return frameCompare;
    return a.numericNodeId - b.numericNodeId;
  });
  nodes.forEach(node => sortSkillAnalysisNodes(node.children));
}

function buildNodeSummary(record: Record<string, unknown>, caseId: string): string {
  const parts = [
    caseId ? `case=${caseId}` : '',
    toNumber(record.childCount) > 0 ? `children=${toNumber(record.childCount)}` : '',
    record.isEnded === true ? `ended@${toNumber(record.endedFrame)}` : '',
    toText(record.message),
    toText(record.result)
  ].filter(Boolean);
  return parts.join(' | ') || '追踪节点投影';
}

function resolveTraceDisplayName(record: Record<string, unknown>, kind: string, entityKind: string, configId: number, skillId: number): string {
  const explicit = firstText(record, ['displayName', 'name', 'traceName', 'skillName', 'effectName', 'actionName', 'projectileName', 'buffName', 'entityName', 'runtimeName', 'configName', 'phaseName', 'triggerName', 'slotName']);
  if (explicit) return explicit;

  const domainName = domainKindName(kind, entityKind);
  if (entityKind === 'actor-skill' && skillId > 0) return `${domainName} · 技能 #${skillId}`;
  if (configId > 0) return `${domainName} · 配置 #${configId}`;
  return domainName;
}

function resolveConfigLabel(record: Record<string, unknown>, kind: string, entityKind: string, configId: number, skillId: number): string {
  const exported = firstText(record, ['configLabel']);
  if (exported) return exported;

  const explicit = firstText(record, ['configDisplayName', 'configName', 'skillName', 'effectName', 'actionName', 'projectileName', 'buffName', 'triggerName']);
  const configType = configKindName(kind, entityKind);
  if (explicit && configId > 0) return `${configType} ${explicit} (#${configId})`;
  if (explicit) return `${configType} ${explicit}`;
  if (entityKind === 'actor-skill' && skillId > 0) return `技能配置 #${skillId}`;
  if (configId > 0) return `${configType} #${configId}`;
  return '';
}

function resolveRuntimeLabel(record: Record<string, unknown>, entityKind: string, nodeId: number, sourceContextId: string): string {
  const explicit = firstText(record, ['runtimeLabel', 'runtimeDisplayName', 'runtimeName', 'debugName', 'entityName', 'objectName', 'viewName']);
  const runtimeKind = toText(record.runtimeKind) || entityKind;
  const runtimeId = toNumber(record.runtimeId || record.instanceId || record.entityId || record.projectileRuntimeId || record.buffRuntimeId || record.contextId || record.sourceContextId);
  if (explicit && runtimeId > 0) return `${explicit} (${runtimeKind}#${runtimeId})`;
  if (explicit) return explicit;
  if (runtimeId > 0) return `${runtimeKind} 运行时 #${runtimeId}`;
  if (sourceContextId && sourceContextId !== '0') return `上下文 #${sourceContextId}`;
  return `${entityKind} 节点 #${nodeId}`;
}

function resolveActorLabel(record: Record<string, unknown>, role: 'actor' | 'source' | 'target', actorId: number): string {
  const fieldMap: Record<typeof role, string[]> = {
    actor: ['actorLabel', 'actorName', 'actorDisplayName', 'entityName'],
    source: ['sourceActorLabel', 'sourceActorName', 'sourceName', 'casterName', 'ownerName'],
    target: ['targetActorLabel', 'targetActorName', 'targetName']
  };
  const explicit = firstText(record, fieldMap[role]);
  const roleName = role === 'source' ? '施法者' : role === 'target' ? '目标' : '角色';
  if (explicit && actorId > 0) return `${roleName} ${explicit} (#${actorId})`;
  if (explicit) return `${roleName} ${explicit}`;
  if (actorId > 0) return `${roleName} #${actorId}`;
  return '';
}

function firstText(record: Record<string, unknown>, fields: string[]): string {
  for (const field of fields) {
    const value = toText(record[field]);
    if (value) return value;
  }
  return '';
}

function domainKindName(kind: string, entityKind: string): string {
  const text = `${kind} ${entityKind}`.toLowerCase();
  if (text.includes('skill') || text.includes('cast')) return '技能释放';
  if (text.includes('projectile')) return '投射物发射';
  if (text.includes('area') || text.includes('aoe')) return '区域效果';
  if (text.includes('shield')) return '护盾';
  if (text.includes('summon')) return '召唤物';
  if (text.includes('continuous')) return '持续行为';
  if (text.includes('buff')) return 'Buff 附加';
  if (text.includes('damage')) return '伤害结算';
  if (text.includes('action')) return '效果动作';
  if (text.includes('effect')) return '效果执行';
  if (text.includes('presentation')) return '表现事件';
  if (text.includes('assert') || text.includes('expect')) return '验收断言';
  return kind || entityKind || '追踪节点';
}

function configKindName(kind: string, entityKind: string): string {
  const text = `${kind} ${entityKind}`.toLowerCase();
  if (text.includes('skill') || text.includes('cast')) return '技能';
  if (text.includes('projectile')) return '投射物';
  if (text.includes('area') || text.includes('aoe')) return '区域';
  if (text.includes('shield')) return '护盾';
  if (text.includes('summon')) return '召唤物';
  if (text.includes('continuous')) return '持续行为';
  if (text.includes('buff')) return 'Buff';
  if (text.includes('damage')) return '伤害';
  if (text.includes('action')) return '动作';
  if (text.includes('effect')) return '效果';
  if (text.includes('presentation')) return '表现';
  return '配置';
}

function buildEntityKey(entityKind: string, record: Record<string, unknown>, nodeId: number, configId: number, actorId: number, sourceContextId: string): string {
  const runtimeKind = toText(record.runtimeKind);
  const runtimeConfigId = toNumber(record.runtimeConfigId);
  const entityId = toNumber(record.entityId || record.runtimeId || record.instanceId || record.projectileRuntimeId || record.projectileId || record.buffRuntimeId || record.buffId || record.areaRuntimeId || record.areaId || record.shieldId || record.summonId || record.contextId || record.sourceContextId);
  if (entityId > 0) return `${entityKind}:${entityId}`;
  if (runtimeKind && runtimeConfigId > 0) return `${runtimeKind}:${runtimeConfigId}`;
  if (configId > 0) return `${entityKind}:${configId}`;
  if (actorId > 0 && entityKind === 'actor-skill') return `actor:${actorId}`;
  if (sourceContextId && sourceContextId !== '0') return `context:${sourceContextId}`;
  return `${entityKind}:node-${nodeId}`;
}

function buildOwnerKey(node: SkillAnalysisFlatNodeProjection): string {
  if (node.sourceActorId) return `actor:${node.sourceActorId}`;
  if (node.actorId) return `actor:${node.actorId}`;
  if (node.ownerContextId && node.ownerContextId !== '0') return `owner-context:${node.ownerContextId}`;
  if (node.rootContextId && node.rootContextId !== '0') return `root-context:${node.rootContextId}`;
  return `root:${node.rootId}`;
}

function ensureEntityGroup(groups: Map<string, SkillAnalysisEntityGroupProjection>, ownerKey: string, node: SkillAnalysisFlatNodeProjection): SkillAnalysisEntityGroupProjection {
  const existing = groups.get(ownerKey);
  if (existing) return existing;
  const group: SkillAnalysisEntityGroupProjection = {
    id: ownerKey,
    ownerKey,
    label: node.sourceActorLabel || node.actorLabel || `主实体 ${ownerKey}`,
    actorId: node.sourceActorId || node.actorId || null,
    rootId: node.rootId || null,
    totalNodes: 0,
    failureCount: 0,
    relations: []
  };
  groups.set(ownerKey, group);
  return group;
}

function isFailureNode(node: SkillAnalysisFlatNodeProjection): boolean {
  const severity = node.severity.toLowerCase();
  return severity === 'error' || severity === 'failed' || Boolean(node.failure);
}

function matchesOption(value: string, filterValue: string): boolean {
  return !filterValue || filterValue === 'all' || value === filterValue;
}

function matchesNumberFilter(values: Array<number | null | undefined>, filterText: string): boolean {
  if (!filterText) return true;
  const expected = Number(filterText);
  if (!Number.isFinite(expected)) return true;
  return values.some(value => Number(value || 0) === expected);
}

function pickRecord(record: Record<string, unknown> | null | undefined, camelName: string, pascalName: string): Record<string, unknown> | null {
  const value = record?.[camelName] ?? record?.[pascalName];
  return asRecord(value);
}

function normalizeAnalysisMetadataProperties(metadata: Record<string, unknown>): Record<string, unknown> {
  const raw = metadata?.properties ?? metadata?.Properties;
  if (Array.isArray(raw)) {
    const result: Record<string, unknown> = {};
    for (const item of raw) {
      const record = asRecord(item);
      const key = toText(record.key || record.Key);
      if (!key) continue;
      result[key] = record.value ?? record.Value ?? '';
    }
    return result;
  }

  return asRecord(raw);
}

function pickArray(record: Record<string, unknown> | null | undefined, camelName: string, pascalName: string): unknown[] {
  const value = record?.[camelName] ?? record?.[pascalName];
  return Array.isArray(value) ? value : [];
}

function asRecord(value: unknown): Record<string, unknown> {
  return value && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : {};
}

function inferArtifactNodeSeverity(node: Record<string, unknown>, endReason: string): string {
  const explicit = toText(node.severity || node.Severity).toLowerCase();
  if (explicit) return explicit;
  const reason = endReason.toLowerCase();
  if (includesAny(reason, ['fail', 'error', 'exception', 'invalid', 'timeout'])) return 'error';
  return 'info';
}

function uniqueSorted(values: string[]): string[] {
  return [...new Set(values.filter(Boolean))].sort((a, b) => a.localeCompare(b));
}

function uniqueNumbers(values: number[]): number[] {
  return [...new Set(values.filter(value => Number.isFinite(value) && value > 0))].sort((a, b) => a - b);
}
