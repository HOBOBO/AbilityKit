import type { EvidenceRow } from './scenarioArchive';

export const scenarioEventLanes = {
  issues: '失败与告警',
  skills: '技能与效果',
  impact: '伤害与治疗',
  objects: '对象与 Buff',
  trace: '触发与溯源',
  other: '输入与其他'
} as const;

export type ScenarioEventLane = keyof typeof scenarioEventLanes;

const eventKinds: readonly (readonly [string, ScenarioEventLane])[] = [
  ['Unknown', 'other'], ['SkillRuntimeStarted', 'skills'], ['SkillRuntimeEnded', 'skills'],
  ['TraceNodeStarted', 'trace'], ['TraceNodeEnded', 'trace'], ['Damage', 'impact'], ['Heal', 'impact'],
  ['BuffAdded', 'objects'], ['BuffRemoved', 'objects'], ['ProjectileSpawned', 'objects'],
  ['ProjectileEnded', 'objects'], ['AreaSpawned', 'objects'], ['AreaEnded', 'objects'],
  ['Warning', 'issues'], ['Exception', 'issues'], ['Sync', 'other'], ['SummonSpawned', 'objects'],
  ['SummonEnded', 'objects'], ['EffectStarted', 'skills'], ['EffectEnded', 'skills'],
  ['ProjectileHit', 'objects'], ['TriggerAnalysis', 'trace'], ['SkillFailure', 'issues'],
  ['TriggerAnalysisAggregate', 'trace'], ['TargetSearch', 'skills'], ['InputCommand', 'other'],
  ['SkillEconomy', 'skills'], ['TracePredictionRetracted', 'trace']
];

function eventKind(event: EvidenceRow) {
  return typeof event.kind === 'number' ? eventKinds[event.kind] : undefined;
}

export const scenarioEventName = (event: EvidenceRow) => {
  const value = event.kind;
  return eventKind(event)?.[0] ?? `事件 ${typeof value === 'number' || typeof value === 'string' ? value : '—'}`;
};
const eventLane = (event: EvidenceRow): ScenarioEventLane => eventKind(event)?.[1] ?? 'other';

function eventFrame(event: EvidenceRow): number | null {
  const frame = event.frame;
  return typeof frame === 'number' && Number.isSafeInteger(frame) && frame >= 0 ? frame : null;
}

export function eventFrameLabel(event: EvidenceRow): string {
  const frame = eventFrame(event);
  return frame === null ? '—' : `F${frame}`;
}

interface FrameRange { start: number; end: number }

export interface ScenarioTimelineSelection extends FrameRange { lane: ScenarioEventLane }

export function formatScenarioFrameRange({ start, end }: FrameRange): string {
  return start === end ? `F${start}` : `F${start}–F${end}`;
}

export function matchesScenarioTimelineSelection(event: EvidenceRow, selection: ScenarioTimelineSelection): boolean {
  const frame = eventFrame(event);
  return frame !== null && frame >= selection.start && frame <= selection.end && eventLane(event) === selection.lane;
}

export function buildScenarioEventTimeline(events: readonly EvidenceRow[]) {
  let first = Number.POSITIVE_INFINITY;
  let last = -1;
  let unplaced = 0;
  for (const event of events) {
    const frame = eventFrame(event);
    if (frame === null) { unplaced++; continue; }
    first = Math.min(first, frame);
    last = Math.max(last, frame);
  }
  if (last < 0) return { range: null, buckets: [], tracks: [], unplaced };

  const width = Math.ceil((last - first + 1) / 16);
  const buckets = Array.from({ length: Math.ceil((last - first + 1) / width) }, (_, index) => {
    const start = first + index * width;
    return { start, end: Math.min(last, start + width - 1) };
  });
  const tracks = (Object.keys(scenarioEventLanes) as ScenarioEventLane[]).map(lane => ({
    lane, total: 0, peak: 0, counts: buckets.map(() => 0)
  }));
  const byLane = new Map(tracks.map(track => [track.lane, track]));
  for (const event of events) {
    const frame = eventFrame(event);
    if (frame === null) continue;
    const track = byLane.get(eventLane(event));
    if (!track) continue;
    const index = Math.floor((frame - first) / width);
    track.peak = Math.max(track.peak, ++track.counts[index]);
    track.total++;
  }
  return { range: { start: first, end: last }, buckets, tracks: tracks.filter(track => track.total > 0), unplaced };
}
