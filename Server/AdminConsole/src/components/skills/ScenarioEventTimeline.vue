<template>
  <section class="event-heatmap" aria-labelledby="scenario-event-heatmap-title">
    <div class="heatmap-heading">
      <div><h4 id="scenario-event-heatmap-title">战斗事件轨道</h4><p class="muted">按帧区间统计已记录事件。点击柱子，筛选下方明细。</p></div>
      <span v-if="timeline.range" class="badge">{{ formatScenarioFrameRange(timeline.range) }} · {{ eventCount }} 条</span>
    </div>
    <p v-if="!timeline.range" class="muted">没有可绘制的事件帧。</p>
    <div v-else class="heatmap-scroll">
      <div class="heatmap-grid" :style="{ '--bucket-count': timeline.buckets.length }">
        <span class="heatmap-sticky muted">事件类型</span>
        <span v-for="bucket in timeline.buckets" :key="bucket.start" class="heatmap-axis muted" :title="formatScenarioFrameRange(bucket)">F{{ bucket.start }}</span>
        <template v-for="track in timeline.tracks" :key="track.lane">
          <span class="heatmap-sticky heatmap-name" :data-lane="track.lane"><i aria-hidden="true"></i>{{ scenarioEventLanes[track.lane] }}<small>{{ track.total }}</small></span>
          <template v-for="(bucket, index) in timeline.buckets" :key="bucket.start">
            <button v-if="track.counts[index]" type="button" class="heatmap-cell" :data-lane="track.lane"
              :aria-pressed="selection?.lane === track.lane && selection.start === bucket.start && selection.end === bucket.end"
              :aria-label="bucketLabel(track.lane, bucket, track.counts[index])" :title="bucketLabel(track.lane, bucket, track.counts[index])"
              @click="$emit('select', { lane: track.lane, ...bucket })">
              <span aria-hidden="true" :style="{ height: `${Math.max(15, track.counts[index] * 100 / track.peak)}%` }"></span>
            </button>
            <span v-else class="heatmap-cell empty" aria-hidden="true"></span>
          </template>
        </template>
      </div>
    </div>
    <div class="heatmap-footer muted">
      <small v-if="timeline.range">柱高以各轨峰值为基准，不用于跨轨比较。F 表示模拟帧。</small>
      <small v-if="timeline.unplaced">{{ timeline.unplaced }} 条未记录有效帧号，仅在未按帧筛选时显示。</small>
    </div>
  </section>
</template>

<script setup lang="ts">
import { buildScenarioEventTimeline, formatScenarioFrameRange, scenarioEventLanes, type ScenarioEventLane, type ScenarioTimelineSelection } from '../../services/scenarioEventTimeline';

defineProps<{ timeline: ReturnType<typeof buildScenarioEventTimeline>; selection: ScenarioTimelineSelection | null; eventCount: number }>();
defineEmits<{ select: [selection: ScenarioTimelineSelection] }>();

const bucketLabel = (lane: ScenarioEventLane, bucket: { start: number; end: number }, count: number) =>
  `${scenarioEventLanes[lane]} · ${formatScenarioFrameRange(bucket)} · ${count} 条`;
</script>

<style scoped>
.event-heatmap { margin-block: .75rem; padding: 14px; border: 1px solid var(--line-soft); border-radius: 14px; background: var(--panel); }
.heatmap-heading, .heatmap-footer { display: flex; flex-wrap: wrap; align-items: baseline; justify-content: space-between; gap: .5rem; }
.heatmap-heading h4 { margin: 0; }
.heatmap-heading p { margin: .35rem 0 .7rem; }
.heatmap-scroll { overflow-x: auto; padding: 4px; }
.heatmap-grid { display: grid; grid-template-columns: 8rem repeat(var(--bucket-count), minmax(2.25rem, 1fr)); gap: 4px; }
.heatmap-sticky { position: sticky; z-index: 1; left: 0; background: var(--panel); }
.heatmap-sticky, .heatmap-axis { align-self: center; font-size: 11px; }
.heatmap-axis { text-align: center; white-space: nowrap; font-variant-numeric: tabular-nums; }
.heatmap-name { display: flex; align-items: center; gap: .4rem; padding: .3rem .4rem .3rem 0; }
.heatmap-name i { width: .5rem; height: .5rem; border-radius: 50%; background: var(--lane-color); }
.heatmap-name small { margin-left: auto; color: var(--muted); font-variant-numeric: tabular-nums; }
[data-lane] { --lane-color: #a3abc1; }
[data-lane="issues"] { --lane-color: #ff8698; }
[data-lane="skills"] { --lane-color: #65c8ff; }
[data-lane="impact"] { --lane-color: #ffc76e; }
[data-lane="objects"] { --lane-color: #60ddaf; }
[data-lane="trace"] { --lane-color: #b5a0ff; }
.heatmap-cell { display: flex; align-items: end; min-width: 0; height: 29px; padding: 2px; border: 1px solid var(--line-soft); border-radius: 5px; background: rgba(136, 161, 196, .08); box-shadow: none; }
.heatmap-cell span { width: 100%; border-radius: 3px; background: var(--lane-color); }
.heatmap-cell[aria-pressed="true"], .heatmap-cell:hover { border-color: var(--lane-color); background: rgba(136, 161, 196, .2); }
.heatmap-cell[aria-pressed="true"], .heatmap-cell:focus-visible { outline: 2px solid var(--text); outline-offset: 1px; }
.heatmap-cell.empty { opacity: .45; }
.heatmap-footer { margin-top: .55rem; }
</style>
