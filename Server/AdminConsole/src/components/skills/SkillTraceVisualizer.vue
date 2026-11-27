<template>
  <div class="skill-trace-visualizer">
    <section class="trace-filter-panel">
      <div class="trace-section-head">
        <h3>{{ title }}过滤</h3>
        <span class="badge">{{ nodes.length }} / {{ totalNodeCount }} 个节点</span>
      </div>
      <div class="trace-filter-grid">
        <div><label>全文搜索</label><input v-model="filter.searchText" placeholder="类型 / 失败原因 / 节点 ID / 上下文 ID / 原始载荷" /></div>
        <div><label>严重级别</label><select v-model="filter.severity"><option value="all">全部</option><option v-for="item in filterOptions.severities" :key="item" :value="item">{{ item }}</option></select></div>
        <div><label>阶段</label><select v-model="filter.stage"><option value="all">全部</option><option v-for="item in filterOptions.stages" :key="item" :value="item">{{ item }}</option></select></div>
        <div><label>类型</label><select v-model="filter.kind"><option value="all">全部</option><option v-for="item in filterOptions.kinds" :key="item" :value="item">{{ item }}</option></select></div>
        <div><label>分析域</label><select v-model="filter.entityKind"><option value="all">全部</option><option v-for="item in filterOptions.entityKinds" :key="item" :value="item">{{ domainOptionLabel(item) }}</option></select></div>
        <div><label>角色 ID</label><input v-model="filter.actorId" placeholder="来源 / 目标 / 角色" /></div>
        <div><label>技能 ID</label><input v-model="filter.skillId" placeholder="技能配置" /></div>
        <div><label>配置 ID</label><input v-model="filter.configId" placeholder="效果 / 行为 / Buff" /></div>
        <div><label>根节点 ID</label><select v-model="filter.rootId"><option value="">全部</option><option v-for="item in filterOptions.rootIds" :key="item" :value="item">{{ item }}</option></select></div>
        <div><label>上下文 ID</label><input v-model="filter.contextId" placeholder="节点 / 父级 / 根节点 / 所有者" /></div>
        <label class="trace-check"><input v-model="filter.onlyFailures" type="checkbox" /> 仅看失败/异常链路</label>
      </div>
    </section>

    <section class="trace-domain-panel">
      <div class="trace-section-head">
        <h3>技能分析导览</h3>
        <span class="badge">{{ domainOverview.length }} 个分析域</span>
      </div>
      <p class="muted">按主动/被动技能、触发源、条件、行为、效果、Buff、持续行为、投射物、区域、护盾、召唤物、位移、伤害和表现事件重新组织追踪节点，优先定位“为什么触发、条件是否满足、后续行为做了什么”。</p>
      <div class="trace-domain-grid">
        <article v-for="domain in domainOverview" :key="domain.domain" class="trace-domain-card" :class="{ danger: domain.failureCount > 0, active: selectedNode?.domain === domain.domain }" role="button" tabindex="0" @click="selectDomain(domain.domain)" @keydown.enter.prevent="selectDomain(domain.domain)" @keydown.space.prevent="selectDomain(domain.domain)">
          <strong>{{ domain.label }}</strong>
          <small>{{ domain.lane }}</small>
          <div><span>{{ domain.count }} 节点</span><span :class="domain.failureCount > 0 ? 'danger-text' : ''">{{ domain.failureCount }} 异常</span></div>
        </article>
      </div>
      <div v-if="failureInsights.length" class="trace-failure-strip">
        <strong>异常热点</strong>
        <button v-for="node in failureInsights" :key="`${node.source}-${node.nodeId}`" class="secondary trace-chip-button" @click="selectNode(node)">{{ node.domainLabel }} · F{{ node.frame }} · {{ truncateLabel(node.failure || node.displayName, 22) }}</button>
      </div>
    </section>

    <section class="trace-system-panel">
      <div class="trace-section-head"><h3>运行时系统顺序</h3><span class="badge">MOBA Runtime</span></div>
      <p class="muted">按 MOBA 战斗帧内真实执行顺序标出当前追踪命中的阶段，避免只按树形父子关系误判执行先后。</p>
      <div class="trace-system-order">
        <article v-for="step in runtimeSystemSteps" :key="step.key" class="trace-system-step" :class="{ active: step.hitCount > 0, danger: step.failureCount > 0 }" role="button" tabindex="0" @click="selectSystemStep(step)" @keydown.enter.prevent="selectSystemStep(step)" @keydown.space.prevent="selectSystemStep(step)">
          <strong>{{ step.label }}</strong>
          <small>{{ step.description }}</small>
          <span>{{ step.hitCount }} 节点 / {{ step.failureCount }} 异常</span>
        </article>
      </div>
    </section>

    <section class="effect-structure-panel">
      <div class="trace-section-head"><h3>战斗效果结构图</h3><span class="badge">{{ effectStructureGraph.nodes.length }} 个节点 / {{ effectStructureGraph.edges.length }} 条连线</span></div>
      <p class="muted">按“入口 → 条件 → Effect / Plan → 运行时实体 → 结算 / 表现 → 上下文”分列展示，更适合观察技能如何分叉成 Buff、投射物、区域、护盾、召唤物、持续行为和伤害结算。</p>
      <div class="effect-structure-canvas" :style="{ minHeight: `${effectStructureGraph.height}px` }">
        <svg class="effect-structure-svg" :viewBox="`0 0 ${effectStructureGraph.width} ${effectStructureGraph.height}`" preserveAspectRatio="xMinYMin meet" role="img" aria-label="战斗效果结构 SVG 图">
          <defs>
            <marker id="effect-structure-arrow" markerWidth="10" markerHeight="10" refX="8" refY="3" orient="auto" markerUnits="strokeWidth">
              <path d="M0,0 L0,6 L8,3 z" class="effect-structure-arrow" />
            </marker>
          </defs>
          <g class="effect-structure-columns">
            <g v-for="column in effectStructureGraph.columns" :key="column.key">
              <rect class="effect-structure-column-bg" :class="`effect-structure-column-${column.key}`" :x="column.x" :y="42" :width="column.width" :height="effectStructureGraph.height - 58" rx="18" />
              <text class="effect-structure-column-title" :x="column.centerX" :y="20">{{ column.label }}</text>
              <text class="effect-structure-column-meta" :x="column.centerX" :y="38">{{ column.count }} 节点 / {{ column.failureCount }} 异常</text>
            </g>
          </g>
          <g class="effect-structure-edges">
            <path v-for="edge in effectStructureGraph.edges" :key="edge.id" :d="edge.path" :class="{ danger: edge.danger }" marker-end="url(#effect-structure-arrow)" />
          </g>
          <g class="effect-structure-nodes">
            <g v-for="item in effectStructureGraph.nodes" :key="item.key" class="effect-structure-node" :class="{ active: selectedNodeKey === buildNodeKey(item.node), danger: item.danger }" role="button" tabindex="0" @click="selectNode(item.node)" @keydown.enter.prevent="selectNode(item.node)" @keydown.space.prevent="selectNode(item.node)">
              <rect :x="item.x" :y="item.y" :width="effectStructureGraph.nodeWidth" :height="effectStructureGraph.nodeHeight" rx="14" />
              <text class="effect-structure-title" :x="item.x + 14" :y="item.y + 22">{{ truncateLabel(item.node.displayName || item.node.label, 24) }}</text>
              <text class="effect-structure-meta" :x="item.x + 14" :y="item.y + 40">{{ truncateLabel(item.node.domainLabel || item.node.configLabel || item.node.kind, 30) }}</text>
              <text class="effect-structure-meta" :x="item.x + 14" :y="item.y + 58">{{ truncateLabel(item.node.conditionLabel || item.node.actionLabel || item.node.summary, 32) }}</text>
            </g>
          </g>
        </svg>
        <p v-if="!effectStructureGraph.nodes.length" class="muted">当前没有足够的战斗效果节点；接入运行态追踪后，这里会按结构角色展示效果分叉与结果回流。</p>
      </div>
    </section>

    <section class="combat-effect-detail-panel">
      <div class="trace-section-head">
        <h3>战斗效果细节</h3>
        <span class="badge">{{ damageWaterfalls.length }} 组伤害 / {{ lifecycleSummaries.length }} 个生命周期</span>
      </div>
      <p class="muted">将伤害计算拆成瀑布式阶段，同时按 Buff、持续行为、投射物、区域、护盾、召唤物和位移聚合生命周期，补足“数值如何变化”和“对象如何存活”的第二视图。</p>
      <div class="combat-effect-detail-grid">
        <article class="damage-waterfall-panel">
          <div class="trace-section-head">
            <h4>伤害瀑布</h4>
            <span class="badge">Base → Mitigate → Shield → Final</span>
          </div>
          <div v-if="damageWaterfalls.length" class="damage-waterfall-list">
            <article
              v-for="item in damageWaterfalls"
              :key="item.key"
              class="damage-waterfall-card"
              :class="{ danger: item.danger, active: selectedNodeKey === buildNodeKey(item.node) }"
              role="button"
              tabindex="0"
              @click="selectNode(item.node)"
              @keydown.enter.prevent="selectNode(item.node)"
              @keydown.space.prevent="selectNode(item.node)"
            >
              <div class="damage-waterfall-head">
                <div>
                  <strong>{{ item.title }}</strong>
                  <small>{{ item.subtitle }}</small>
                </div>
                <span class="badge" :class="item.danger ? 'danger' : ''">{{ item.statusLabel }}</span>
              </div>
              <div class="damage-waterfall-bars">
                <div v-for="segment in item.segments" :key="segment.key" class="damage-waterfall-row">
                  <div class="damage-waterfall-row-head">
                    <span>{{ segment.label }}</span>
                    <code>{{ segment.valueText }}</code>
                  </div>
                  <div class="damage-waterfall-track"><span class="damage-waterfall-fill" :class="segment.key" :style="{ width: `${segment.width}%` }"></span></div>
                </div>
              </div>
            </article>
          </div>
          <p v-else class="muted">没有可展示的伤害节点；当运行态追踪输出 base / mitigate / shield / final 数值时，这里会显示瀑布式结算过程。</p>
        </article>
        <article class="lifecycle-summary-panel">
          <div class="trace-section-head">
            <h4>生命周期摘要</h4>
            <span class="badge">按运行时实体聚合</span>
          </div>
          <div v-if="lifecycleSummaries.length" class="lifecycle-summary-list">
            <article
              v-for="item in lifecycleSummaries"
              :key="item.key"
              class="lifecycle-summary-card"
              :class="{ danger: item.failureCount > 0, active: selectedNodeKey === buildNodeKey(item.node) }"
              role="button"
              tabindex="0"
              @click="selectNode(item.node)"
              @keydown.enter.prevent="selectNode(item.node)"
              @keydown.space.prevent="selectNode(item.node)"
            >
              <div class="lifecycle-summary-head">
                <div>
                  <strong>{{ item.label }}</strong>
                  <small>{{ item.subtitle }}</small>
                </div>
                <span class="badge" :class="item.failureCount > 0 ? 'danger' : ''">{{ item.statusLabel }}</span>
              </div>
              <div class="lifecycle-summary-grid">
                <span>首帧</span><code>F{{ item.firstFrame }}</code>
                <span>末帧</span><code>F{{ item.lastFrame }}</code>
                <span>持续</span><code>{{ item.durationLabel }}</code>
                <span>节点</span><code>{{ item.nodeCount }}</code>
                <span>异常</span><code>{{ item.failureCount }}</code>
                <span>实体</span><code>{{ item.entityKey }}</code>
              </div>
            </article>
          </div>
          <p v-else class="muted">没有可聚合的生命周期节点；当 Buff、Projectile、Area、Shield、Summon 或 Continuous 追踪接入后，这里会按实体汇总存活与清理过程。</p>
        </article>
      </div>
    </section>

    <section class="trace-flow-panel">
      <div class="trace-section-head"><h3>技能流图</h3><span class="badge">{{ flowGraph.nodes.length }} 个节点 / {{ flowGraph.edges.length }} 条连线</span></div>
      <div class="trace-flow-canvas" :style="{ minHeight: `${flowGraph.height}px` }">
        <svg class="trace-flow-svg" :viewBox="`0 0 ${flowGraph.width} ${flowGraph.height}`" preserveAspectRatio="xMinYMin meet" role="img" aria-label="技能执行流 SVG 图">
          <defs>
            <marker id="trace-arrow" markerWidth="10" markerHeight="10" refX="8" refY="3" orient="auto" markerUnits="strokeWidth">
              <path d="M0,0 L0,6 L8,3 z" class="trace-flow-arrow" />
            </marker>
          </defs>
          <g class="trace-flow-grid-lines">
            <line v-for="lane in flowGraph.lanes" :key="lane.stage" :x1="20" :x2="flowGraph.width - 20" :y1="lane.y" :y2="lane.y" />
            <text v-for="lane in flowGraph.lanes" :key="`${lane.stage}-label`" :x="24" :y="lane.y - 10">{{ lane.stage }}</text>
          </g>
          <g class="trace-flow-edges">
            <path v-for="edge in flowGraph.edges" :key="edge.id" :d="edge.path" :class="{ danger: edge.danger }" marker-end="url(#trace-arrow)" />
          </g>
          <g class="trace-flow-nodes">
            <g v-for="item in flowGraph.nodes" :key="item.key" class="trace-flow-node" :class="{ active: selectedNodeKey === buildNodeKey(item.node), danger: item.danger }" role="button" tabindex="0" @click="selectNode(item.node)" @keydown.enter.prevent="selectNode(item.node)" @keydown.space.prevent="selectNode(item.node)">
              <rect :x="item.x" :y="item.y" :width="flowGraph.nodeWidth" :height="flowGraph.nodeHeight" rx="14" />
              <text class="trace-flow-title" :x="item.x + 14" :y="item.y + 24">{{ truncateLabel(item.node.displayName || item.node.label, 24) }}</text>
              <text class="trace-flow-meta" :x="item.x + 14" :y="item.y + 44">{{ truncateLabel(item.node.domainLabel || item.node.configLabel || item.node.kind, 30) }}</text>
              <text class="trace-flow-meta" :x="item.x + 14" :y="item.y + 63">{{ truncateLabel(item.node.conditionLabel || item.node.actionLabel || `${item.node.sourceActorLabel || item.node.actorLabel || '角色 -'} → ${item.node.targetActorLabel || '-'}`, 32) }}</text>
            </g>
          </g>
        </svg>
        <p v-if="!flowGraph.nodes.length" class="muted">{{ emptyText }}</p>
      </div>
    </section>

    <div class="trace-visualizer-grid">
      <section class="trace-visualizer-tree">
        <div class="trace-section-head"><h3>{{ title }}</h3><span class="badge">已过滤 {{ nodes.length }}</span></div>
        <div class="trace-tree">
          <article v-for="node in nodes" :key="`${node.source}-${node.nodeId}`" class="trace-tree-node analysis-node" :class="{ active: selectedNodeKey === buildNodeKey(node), danger: node.severity === 'error' || node.severity === 'failed' }" :style="{ '--depth': node.depth }" role="button" tabindex="0" @click="selectNode(node)" @keydown.enter.prevent="selectNode(node)" @keydown.space.prevent="selectNode(node)">
            <span class="trace-branch"></span>
            <div><strong><span class="domain-pill">{{ node.domainLabel }}</span>{{ node.displayName || node.label }}</strong><small>{{ node.triggerLabel || node.detailLabel || `${node.stage} / ${node.entityKind}` }} / 节点 {{ node.nodeId }} / 父级 {{ node.parentId || '-' }} / 根节点 {{ node.rootId || '-' }}</small></div>
            <span class="badge" :class="node.severity === 'error' || node.severity === 'failed' ? 'danger' : ''">F{{ node.frame }}<template v-if="!frameOnly"> / {{ node.timeMs }}ms</template></span>
            <p>{{ node.conditionLabel || node.actionLabel || node.summary }}<template v-if="node.failure"> / 失败={{ node.failure }}</template></p>
          </article>
          <p v-if="!nodes.length" class="muted">{{ emptyText }}</p>
        </div>
      </section>
      <section class="trace-inspector">
        <div class="trace-section-head"><h3>检查器</h3><span class="badge">{{ selectedNode?.source || '无' }}</span></div>
        <div v-if="selectedNode" class="inspector-card">
          <strong>{{ selectedNode.displayName || selectedNode.label }}</strong>
          <p class="muted">{{ selectedNode.detailLabel || selectedNode.summary }}</p>
          <div class="inspector-section">
            <h4>归因 / 触发链路</h4>
            <div class="inspector-grid">
              <span>分析域</span><code>{{ selectedNode.domainLabel || selectedNode.domain }}</code>
              <span>泳道</span><code>{{ selectedNode.laneLabel }}</code>
              <span>触发源</span><code>{{ selectedNode.triggerLabel || '-' }}</code>
              <span>条件判定</span><code>{{ selectedNode.conditionLabel || '-' }}</code>
              <span>后续行为</span><code>{{ selectedNode.actionLabel || '-' }}</code>
            </div>
          </div>
          <div class="inspector-section">
            <h4>运行时上下文</h4>
            <div class="inspector-grid">
            <span>节点 ID</span><code>{{ selectedNode.nodeId }}</code>
            <span>阶段</span><code>{{ selectedNode.stage }}</code>
            <span>类型</span><code>{{ selectedNode.kind }}</code>
            <span>实体</span><code>{{ selectedNode.runtimeLabel || `${selectedNode.entityKind} / ${selectedNode.entityKey}` }}</code>
            <span>状态</span><code>{{ selectedNode.status }}</code>
            <span>角色</span><code>{{ selectedNode.actorLabel || selectedNode.actorId || '-' }}</code>
            <span>来源角色</span><code>{{ selectedNode.sourceActorLabel || selectedNode.sourceActorId || '-' }}</code>
            <span>目标角色</span><code>{{ selectedNode.targetActorLabel || selectedNode.targetActorId || '-' }}</code>
            <span>技能</span><code>{{ selectedNode.skillId ? `技能 #${selectedNode.skillId}` : '-' }}</code>
            <span>配置</span><code>{{ selectedNode.configLabel || selectedNode.configId || '-' }}</code>
            <span>来源上下文</span><code>{{ selectedNode.sourceContextId ?? '-' }}</code>
            <span>所有者上下文</span><code>{{ selectedNode.ownerContextId ?? '-' }}</code>
            <span>根上下文</span><code>{{ selectedNode.rootContextId ?? '-' }}</code>
            <span>数据源</span><code>{{ selectedNode.source }}</code>
            </div>
          </div>
          <div class="inspector-section">
            <h4>关键运行时参数</h4>
            <div v-if="selectedRuntimeFacts.length" class="runtime-fact-grid">
              <div v-for="fact in selectedRuntimeFacts" :key="fact.key" class="runtime-fact">
                <span>{{ fact.label }}</span>
                <code>{{ fact.value }}</code>
              </div>
            </div>
            <p v-else class="muted">当前节点没有可投影的关键参数；可展开原始追踪数据查看完整载荷。</p>
          </div>
          <p v-if="selectedNode.failure" class="failure-text">{{ selectedNode.failure }}</p>
          <details class="raw-details" open><summary>原始追踪数据</summary><pre>{{ JSON.stringify(selectedNode.raw, null, 2) }}</pre></details>
        </div>
        <p v-else class="muted">暂无可检查节点；请选择带追踪记录的场景用例，或等待运行态追踪接入。</p>
      </section>
    </div>

    <section class="trace-entity-panel">
      <div class="trace-section-head"><h3>战斗实体关联</h3><span class="badge">{{ entityRelations.length }} 个所有者</span></div>
      <div class="entity-relation-grid">
        <article v-for="group in entityRelations" :key="group.id" class="entity-group" :class="{ danger: group.failureCount > 0 }">
          <div class="entity-group-head"><strong>{{ group.label }}</strong><span class="badge" :class="group.failureCount > 0 ? 'danger' : ''">{{ group.totalNodes }} 个节点 / {{ group.failureCount }} 个失败</span></div>
          <small>角色 {{ group.actorId ?? '-' }} / 根节点 {{ group.rootId ?? '-' }}</small>
          <div class="entity-relation-list">
          <div v-for="relation in group.relations" :key="relation.id" class="entity-relation-row" :class="{ danger: relation.failureCount > 0 }" role="button" tabindex="0" @click="selectRelation(relation)" @keydown.enter.prevent="selectRelation(relation)" @keydown.space.prevent="selectRelation(relation)">
            <span class="entity-kind">{{ relation.entityKind }}</span>
            <div><strong>{{ relation.label }}</strong><small>{{ relation.entityKey }} / F{{ relation.firstFrame }} → F{{ relation.lastFrame }} / {{ relation.nodeCount }} 个节点</small></div>
              <span class="badge" :class="relation.failureCount > 0 ? 'danger' : ''">{{ relation.failureCount }}</span>
            </div>
          </div>
        </article>
        <p v-if="!entityRelations.length" class="muted">暂无可关联实体；过滤条件过窄或追踪接收端尚未投影来源、根节点、所有者上下文。</p>
      </div>
    </section>

    <section class="trace-timeline-placeholder">
      <div class="trace-section-head"><h3>时间轴</h3><span class="badge">{{ timeline.length }} 个事件</span></div>
      <div class="timeline-canvas">
        <svg v-if="timelineGraph.events.length" class="timeline-svg" :viewBox="`0 0 ${timelineGraph.width} ${timelineGraph.height}`" preserveAspectRatio="xMinYMin meet" role="img" aria-label="技能执行时间轴 SVG 图">
          <g class="timeline-axis">
            <line :x1="timelineGraph.marginX" :x2="timelineGraph.width - timelineGraph.marginX" :y1="timelineGraph.axisY" :y2="timelineGraph.axisY" />
            <g v-for="tick in timelineGraph.ticks" :key="tick.frame">
              <line :x1="tick.x" :x2="tick.x" :y1="timelineGraph.axisY - 6" :y2="timelineGraph.height - timelineGraph.marginY + 6" />
              <text :x="tick.x" :y="timelineGraph.axisY - 10">F{{ tick.frame }}</text>
            </g>
          </g>
          <g class="timeline-lane-lines">
            <line v-for="lane in timelineGraph.lanes" :key="`${lane.name}-line`" :x1="timelineGraph.marginX" :x2="timelineGraph.width - timelineGraph.marginX" :y1="lane.y" :y2="lane.y" />
          </g>
          <g class="timeline-lane-labels">
            <text v-for="lane in timelineGraph.lanes" :key="lane.name" :x="18" :y="lane.y + 5">{{ truncateLabel(lane.name, 13) }}</text>
          </g>
          <g class="timeline-event-nodes">
            <g v-for="event in timelineGraph.events" :key="event.id" class="timeline-event-node" :class="{ active: event.nodeId && selectedNode?.nodeId === event.nodeId, danger: event.danger }" role="button" tabindex="0" @click="selectTimelineEvent(event.event)" @keydown.enter.prevent="selectTimelineEvent(event.event)" @keydown.space.prevent="selectTimelineEvent(event.event)">
              <line class="timeline-event-stem" :x1="event.x" :x2="event.x" :y1="timelineGraph.axisY" :y2="event.y" />
              <circle :cx="event.x" :cy="event.y" :r="10" />
              <rect :x="event.x + 16" :y="event.y - 18" :width="timelineGraph.eventWidth" :height="36" rx="11" />
              <text class="timeline-event-title" :x="event.x + 28" :y="event.y - 2">{{ truncateLabel(event.event.label, 26) }}</text>
              <text class="timeline-event-meta" :x="event.x + 28" :y="event.y + 12">{{ event.event.source }}<template v-if="!frameOnly"> · {{ event.event.timeMs }}ms</template></text>
            </g>
          </g>
        </svg>
        <p v-else class="muted">时间轴结构已预留；运行态实时追踪接入后将按主动释放、触发条件、行为计划、Buff、投射物、位移、伤害、表现等业务泳道展示。</p>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { SkillAnalysisEntityGroupProjection, SkillAnalysisEntityRelationProjection, SkillAnalysisFilterOptions, SkillAnalysisFilterState, SkillAnalysisFlatNodeProjection, SkillAnalysisTimelineEventProjection } from '../../types';

const props = defineProps<{
  title: string;
  nodes: SkillAnalysisFlatNodeProjection[];
  totalNodeCount: number;
  selectedNode: SkillAnalysisFlatNodeProjection | null;
  timeline: SkillAnalysisTimelineEventProjection[];
  filter: SkillAnalysisFilterState;
  filterOptions: SkillAnalysisFilterOptions;
  entityRelations: SkillAnalysisEntityGroupProjection[];
  frameOnly?: boolean;
  emptyText: string;
}>();

const emit = defineEmits<{
  (event: 'select-node', node: SkillAnalysisFlatNodeProjection): void;
}>();

const selectedNodeKey = computed(() => props.selectedNode ? buildNodeKey(props.selectedNode) : '');

const runtimeSystemDefinitions = [
  { key: 'movement', label: 'Motion Tick', description: '位移源推进 / Dash / Pull / Blink', domains: ['movement'] },
  { key: 'passive-skill', label: 'Passive Skill Triggers', description: '被动技能与光环监听注册', domains: ['passive-skill'] },
  { key: 'trigger-source', label: 'Trigger Source', description: '事件源与全局触发器绑定', domains: ['trigger-source'] },
  { key: 'trigger-condition', label: 'Trigger Conditions', description: 'Budget / 条件 / 冷却 / 资源判定', domains: ['trigger-condition'] },
  { key: 'active-skill', label: 'Skill Pipeline', description: 'PreCast / Cast / Complete / Cancel', domains: ['active-skill'] },
  { key: 'effect', label: 'Effects Step', description: 'Effect Trace Scope 与 Action 子节点', domains: ['effect', 'trigger-action'] },
  { key: 'buff', label: 'Buff Commands / Lifecycle', description: 'Buff 队列、叠层、持续绑定与清理', domains: ['buff'] },
  { key: 'continuous', label: 'Continuous Tick', description: '持续行为周期执行', domains: ['continuous'] },
  { key: 'projectile', label: 'Projectile Sync', description: 'Spawn / Tick / Exit / Hit', domains: ['projectile'] },
  { key: 'area', label: 'Area Sync', description: 'Spawn / Enter / Stay / Exit / Expire', domains: ['area'] },
  { key: 'shield', label: 'Shield Lifecycle', description: 'Add / Absorb / Remove / Expire', domains: ['shield'] },
  { key: 'summon', label: 'Summon Lifecycle', description: '召唤、存活与清理', domains: ['summon'] },
  { key: 'damage', label: 'Damage Pipeline', description: 'Base / Mitigate / Shield / Final / Apply', domains: ['damage'] },
  { key: 'presentation', label: 'Presentation Events', description: 'VFX / SFX / Animation 表现事件', domains: ['presentation'] },
  { key: 'assertion', label: 'Assertion Result', description: '验收断言与结果归因', domains: ['assertion'] },
  { key: 'context', label: 'Context / Lineage', description: 'Root / Owner / Source 上下文溯源', domains: ['context'] }
];

const runtimeLanePriority = runtimeSystemDefinitions.flatMap(step => step.domains);

const domainOverview = computed(() => {
  const groups = new Map<string, { domain: string; label: string; lane: string; count: number; failureCount: number }>();
  for (const node of props.nodes) {
    const domain = node.domain || node.entityKind || 'context';
    const current = groups.get(domain) || { domain, label: node.domainLabel || domain, lane: node.laneLabel || node.stage, count: 0, failureCount: 0 };
    current.count += 1;
    if (isDangerNode(node)) current.failureCount += 1;
    groups.set(domain, current);
  }
  return [...groups.values()].sort((a, b) => domainPriority(a.domain) - domainPriority(b.domain) || b.failureCount - a.failureCount || b.count - a.count || a.label.localeCompare(b.label));
});

const runtimeSystemSteps = computed(() => runtimeSystemDefinitions.map(step => {
  const matched = props.nodes.filter(node => step.domains.includes(node.domain || node.entityKind || 'context'));
  return {
    ...step,
    hitCount: matched.length,
    failureCount: matched.filter(isDangerNode).length,
    nodes: matched
  };
}));

const selectedRuntimeFacts = computed(() => props.selectedNode ? buildRuntimeFacts(props.selectedNode) : []);

const failureInsights = computed(() => props.nodes.filter(isDangerNode).slice(0, 5));
const damageWaterfalls = computed(() => buildDamageWaterfalls(props.nodes));
const lifecycleSummaries = computed(() => buildLifecycleSummaries(props.nodes));

interface FlowGraphNode {
  key: string;
  node: SkillAnalysisFlatNodeProjection;
  x: number;
  y: number;
  danger: boolean;
}

interface FlowGraphEdge {
  id: string;
  path: string;
  danger: boolean;
}

interface EffectStructureColumnDefinition {
  key: string;
  label: string;
  domains: string[];
}

interface EffectStructureColumnLayout extends EffectStructureColumnDefinition {
  x: number;
  width: number;
  centerX: number;
  count: number;
  failureCount: number;
}

interface EffectStructureNode {
  key: string;
  node: SkillAnalysisFlatNodeProjection;
  columnKey: string;
  x: number;
  y: number;
  danger: boolean;
}

interface FlowGraphLane {
  stage: string;
  y: number;
}

interface TimelineGraphLane {
  name: string;
  y: number;
}

interface TimelineGraphTick {
  frame: number;
  x: number;
}

interface TimelineGraphEvent {
  id: string;
  event: SkillAnalysisTimelineEventProjection;
  nodeId: string | null;
  x: number;
  y: number;
  danger: boolean;
}

interface DamageWaterfallSegment {
  key: string;
  label: string;
  value: number;
  valueText: string;
  width: number;
}

interface DamageWaterfallItem {
  key: string;
  node: SkillAnalysisFlatNodeProjection;
  title: string;
  subtitle: string;
  statusLabel: string;
  danger: boolean;
  segments: DamageWaterfallSegment[];
}

interface LifecycleSummaryItem {
  key: string;
  node: SkillAnalysisFlatNodeProjection;
  label: string;
  subtitle: string;
  statusLabel: string;
  entityKey: string;
  firstFrame: number;
  lastFrame: number;
  durationLabel: string;
  nodeCount: number;
  failureCount: number;
}

const effectStructureColumns: EffectStructureColumnDefinition[] = [
  { key: 'entry', label: '入口 / 触发', domains: ['active-skill', 'passive-skill', 'trigger-source'] },
  { key: 'guard', label: '条件 / 预算', domains: ['trigger-condition'] },
  { key: 'effect', label: 'Effect / Plan', domains: ['effect', 'trigger-action'] },
  { key: 'runtime', label: '运行时实体', domains: ['buff', 'continuous', 'projectile', 'area', 'shield', 'summon', 'movement'] },
  { key: 'result', label: '结算 / 表现', domains: ['damage', 'presentation', 'assertion'] },
  { key: 'context', label: '上下文', domains: ['context'] }
];

const effectStructureGraph = computed(() => {
  const nodeWidth = 210;
  const nodeHeight = 76;
  const columnGap = 26;
  const rowGap = 18;
  const marginX = 28;
  const headerHeight = 54;
  const columnWidth = nodeWidth + 28;
  const columnSlots = new Map<string, number>();
  const columnByDomain = new Map(effectStructureColumns.flatMap(column => column.domains.map(domain => [domain, column.key])));
  const positioned = props.nodes
    .slice()
    .sort((a, b) => a.frame - b.frame || a.timeMs - b.timeMs || domainPriority(a.domain) - domainPriority(b.domain) || a.numericNodeId - b.numericNodeId)
    .map(node => {
      const columnKey = columnByDomain.get(node.domain || node.entityKind || 'context') || 'context';
      const slot = columnSlots.get(columnKey) || 0;
      columnSlots.set(columnKey, slot + 1);
      const columnIndex = Math.max(0, effectStructureColumns.findIndex(column => column.key === columnKey));
      return {
        key: buildNodeKey(node),
        node,
        columnKey,
        x: marginX + columnIndex * (columnWidth + columnGap) + 14,
        y: headerHeight + slot * (nodeHeight + rowGap),
        danger: isDangerNode(node)
      } satisfies EffectStructureNode;
    });
  const byNodeId = new Map(positioned.map(item => [item.node.nodeId, item]));
  const edges = positioned.flatMap(item => {
    if (!item.node.parentId) return [];
    const parent = byNodeId.get(item.node.parentId);
    if (!parent) return [];
    const startX = parent.x + nodeWidth;
    const startY = parent.y + nodeHeight / 2;
    const endX = item.x;
    const endY = item.y + nodeHeight / 2;
    const midX = startX + Math.max(32, (endX - startX) / 2);
    return [{
      id: `${parent.key}->${item.key}`,
      path: `M ${startX} ${startY} C ${midX} ${startY}, ${midX} ${endY}, ${endX} ${endY}`,
      danger: parent.danger || item.danger
    } satisfies FlowGraphEdge];
  });
  const columns = effectStructureColumns.map((column, index) => {
    const columnNodes = positioned.filter(item => item.columnKey === column.key);
    const x = marginX + index * (columnWidth + columnGap);
    return {
      ...column,
      x,
      width: columnWidth,
      centerX: x + columnWidth / 2,
      count: columnNodes.length,
      failureCount: columnNodes.filter(item => item.danger).length
    } satisfies EffectStructureColumnLayout;
  });
  const maxRows = Math.max(1, ...Array.from(columnSlots.values()));
  const width = Math.max(980, marginX * 2 + effectStructureColumns.length * columnWidth + (effectStructureColumns.length - 1) * columnGap);
  const height = Math.max(280, headerHeight + maxRows * (nodeHeight + rowGap) + 28);
  return { nodes: positioned, edges, columns, width, height, nodeWidth, nodeHeight };
});

const flowGraph = computed(() => {
  const nodeWidth = 210;
  const nodeHeight = 82;
  const columnGap = 86;
  const laneGap = 112;
  const marginX = 44;
  const marginY = 48;
  const laneOrder = orderedLanes(props.nodes.map(node => node.laneLabel || node.stage || 'unknown'));
  const laneIndex = new Map(laneOrder.map((stage, index) => [stage, index]));
  const depthSlots = new Map<number, number>();
  const positioned = props.nodes.map(node => {
    const depth = Math.max(0, node.depth || 0);
    const slot = depthSlots.get(depth) || 0;
    depthSlots.set(depth, slot + 1);
    const stageIndex = laneIndex.get(node.laneLabel || node.stage || 'unknown') || 0;
    const yOffset = slot * 10;
    return {
      key: buildNodeKey(node),
      node,
      x: marginX + depth * (nodeWidth + columnGap),
      y: marginY + stageIndex * laneGap + yOffset,
      danger: isDangerNode(node),
    } satisfies FlowGraphNode;
  });
  const byNodeId = new Map(positioned.map(item => [item.node.nodeId, item]));
  const edges = positioned.flatMap(item => {
    if (!item.node.parentId) return [];
    const parent = byNodeId.get(item.node.parentId);
    if (!parent) return [];
    const startX = parent.x + nodeWidth;
    const startY = parent.y + nodeHeight / 2;
    const endX = item.x;
    const endY = item.y + nodeHeight / 2;
    const midX = startX + Math.max(28, (endX - startX) / 2);
    return [{
      id: `${parent.key}->${item.key}`,
      path: `M ${startX} ${startY} C ${midX} ${startY}, ${midX} ${endY}, ${endX} ${endY}`,
      danger: parent.danger || item.danger,
    } satisfies FlowGraphEdge];
  });
  const width = Math.max(760, marginX * 2 + (Math.max(0, ...positioned.map(item => item.node.depth || 0)) + 1) * nodeWidth + Math.max(0, ...positioned.map(item => item.node.depth || 0)) * columnGap);
  const height = Math.max(260, marginY * 2 + Math.max(1, laneOrder.length) * laneGap + Math.max(0, ...Array.from(depthSlots.values())) * 10);
  const lanes = laneOrder.map((stage, index) => ({ stage, y: marginY + index * laneGap + nodeHeight / 2 } satisfies FlowGraphLane));
  return { nodes: positioned, edges, lanes, width, height, nodeWidth, nodeHeight };
});

const timelineGraph = computed(() => {
  const marginX = 90;
  const marginY = 44;
  const laneGap = 72;
  const timeline = props.timeline;
  const trackWidth = Math.max(680, Math.max(0, ...timeline.map(item => item.frame)) * 66);
  const laneOrder = orderedLanes(timeline.map(item => item.lane || 'runtime-event'));
  const laneIndex = new Map(laneOrder.map((lane, index) => [lane, index]));
  const minFrame = timeline.length > 0 ? Math.min(...timeline.map(item => item.frame)) : 0;
  const maxFrame = timeline.length > 0 ? Math.max(...timeline.map(item => item.frame)) : 0;
  const frameRange = Math.max(1, maxFrame - minFrame);
  const events = timeline.map((event, index) => {
    const lane = event.lane || 'runtime-event';
    const normalizedFrame = event.frame - minFrame;
    const x = marginX + (normalizedFrame / frameRange) * trackWidth;
    const y = marginY + (laneIndex.get(lane) || 0) * laneGap;
    return {
      id: event.id,
      event,
      nodeId: event.nodeId ?? null,
      x,
      y,
      danger: event.severity === 'error' || event.severity === 'failed'
    } satisfies TimelineGraphEvent;
  });
  const ticks = uniqueFrameTicks(timeline).map(frame => ({ frame, x: marginX + ((frame - minFrame) / frameRange) * trackWidth } satisfies TimelineGraphTick));
  const width = Math.max(820, marginX * 2 + trackWidth);
  const height = Math.max(220, marginY * 2 + Math.max(1, laneOrder.length) * laneGap + 46);
  const lanes = laneOrder.map((name, index) => ({ name, y: marginY + index * laneGap } satisfies TimelineGraphLane));
  return {
    events,
    ticks,
    lanes,
    width,
    height,
    marginX,
    marginY,
    axisY: marginY - 4,
    eventWidth: 170
  };
});

function buildNodeKey(node: Pick<SkillAnalysisFlatNodeProjection, 'source' | 'nodeId'>): string {
  return `${node.source}:${node.nodeId}`;
}

function isDangerNode(node: SkillAnalysisFlatNodeProjection): boolean {
  return node.severity === 'error' || node.severity === 'failed' || Boolean(node.failure);
}

function selectNode(node: SkillAnalysisFlatNodeProjection): void {
  emit('select-node', node);
}

function selectDomain(domain: string): void {
  const firstFailure = props.nodes.find(node => node.domain === domain && isDangerNode(node));
  const firstNode = firstFailure || props.nodes.find(node => node.domain === domain);
  if (firstNode) selectNode(firstNode);
}

function selectSystemStep(step: { nodes: SkillAnalysisFlatNodeProjection[] }): void {
  const firstFailure = step.nodes.find(isDangerNode);
  const firstNode = firstFailure || step.nodes[0];
  if (firstNode) selectNode(firstNode);
}

function domainOptionLabel(value: string): string {
  const first = props.nodes.find(node => node.domain === value || node.entityKind === value);
  return first?.domainLabel || value;
}

function selectRelation(relation: SkillAnalysisEntityRelationProjection): void {
  const firstFailure = relation.nodes.find(node => node.severity === 'error' || node.severity === 'failed' || Boolean(node.failure));
  const firstNode = firstFailure || relation.nodes[0];
  if (firstNode) selectNode(firstNode);
}

function selectTimelineEvent(event: SkillAnalysisTimelineEventProjection): void {
  if (!event.nodeId) return;
  const node = props.nodes.find(item => item.nodeId === event.nodeId && item.source === event.source) || props.nodes.find(item => item.nodeId === event.nodeId);
  if (node) selectNode(node);
}

function orderedLanes(lanes: string[]): string[] {
  return [...new Set(lanes)].sort((a, b) => lanePriority(a) - lanePriority(b) || a.localeCompare(b));
}

function lanePriority(lane: string): number {
  const normalized = lane.toLowerCase();
  const index = runtimeLanePriority.findIndex(domain => normalized.includes(domain) || normalized.includes(domain.replace('-', ' ')) || normalized.includes(domainLabelKeyword(domain)));
  return index >= 0 ? index : runtimeLanePriority.length + 1;
}

function domainPriority(domain: string): number {
  const index = runtimeLanePriority.indexOf(domain);
  return index >= 0 ? index : runtimeLanePriority.length + 1;
}

function domainLabelKeyword(domain: string): string {
  const labelMap: Record<string, string> = {
    'active-skill': 'skill pipeline',
    'passive-skill': 'passive',
    'trigger-source': 'trigger source',
    'trigger-condition': 'trigger conditions',
    'trigger-action': 'trigger plan actions',
    effect: 'effect execution',
    buff: 'buff lifecycle',
    continuous: 'continuous tick',
    projectile: 'projectile lifecycle',
    area: 'area lifecycle',
    shield: 'shield lifecycle',
    summon: 'summon lifecycle',
    movement: 'movement',
    damage: 'damage',
    presentation: 'presentation',
    assertion: 'assertion',
    context: 'context'
  };
  return labelMap[domain] || domain;
}

function buildRuntimeFacts(node: SkillAnalysisFlatNodeProjection): Array<{ key: string; label: string; value: string }> {
  const raw = node.raw || {};
  const facts: Array<{ key: string; label: string; value: string }> = [];
  addFact(facts, raw, 'eventId', '事件 ID');
  addFact(facts, raw, 'triggerId', '触发器 ID');
  addFact(facts, raw, 'effectId', '效果 ID');
  addFact(facts, raw, 'actionId', '行为 ID');
  addFact(facts, raw, 'runtimeId', '运行时 ID');
  addFact(facts, raw, 'instanceId', '实例 ID');
  addFact(facts, raw, 'projectileId', '投射物 ID');
  addFact(facts, raw, 'launcherId', '发射器 ID');
  addFact(facts, raw, 'areaId', '区域 ID');
  addFact(facts, raw, 'templateId', '模板 ID');
  addFact(facts, raw, 'buffId', 'Buff ID');
  addFact(facts, raw, 'buffIds', 'Buff 列表');
  addFact(facts, raw, 'shieldId', '护盾 ID');
  addFact(facts, raw, 'damageType', '伤害类型');
  addFact(facts, raw, 'reasonKind', '原因类型');
  addFact(facts, raw, 'reasonParam', '原因参数');
  addFact(facts, raw, 'baseDamage', '基础伤害');
  addFact(facts, raw, 'rawDamage', '原始伤害');
  addFact(facts, raw, 'mitigatedDamage', '减免后伤害');
  addFact(facts, raw, 'shieldAbsorb', '护盾吸收');
  addFact(facts, raw, 'hpDamage', '生命伤害');
  addFact(facts, raw, 'finalDamage', '最终伤害');
  addFact(facts, raw, 'value', '数值');
  addFact(facts, raw, 'durationMs', '持续毫秒');
  addFact(facts, raw, 'durationFrames', '持续帧');
  addFact(facts, raw, 'lifetimeFrames', '生命周期帧');
  addFact(facts, raw, 'stayIntervalFrames', '停留间隔帧');
  addFact(facts, raw, 'startFrame', '开始帧');
  addFact(facts, raw, 'expireFrame', '过期帧');
  addFact(facts, raw, 'priority', '优先级');
  addFact(facts, raw, 'stackingPolicy', '叠层策略');
  addFact(facts, raw, 'consumePolicy', '消耗策略');
  addFact(facts, raw, 'absorbRatio', '吸收比例');
  addFact(facts, raw, 'damageTypeMask', '伤害掩码');
  return facts.slice(0, 24);
}

function buildDamageWaterfalls(nodes: SkillAnalysisFlatNodeProjection[]): DamageWaterfallItem[] {
  const damageNodes = nodes.filter(node => node.domain === 'damage' || node.entityKind === 'damage');
  return damageNodes
    .slice()
    .sort((a, b) => a.frame - b.frame || a.timeMs - b.timeMs || a.numericNodeId - b.numericNodeId)
    .slice(0, 8)
    .map(node => {
      const raw = node.raw || {};
      const base = toNumeric(raw.baseDamage, raw.rawDamage, raw.value, raw.amount);
      const mitigated = toNumeric(raw.mitigatedDamage, base);
      const shieldAbsorb = toNumeric(raw.shieldAbsorb, raw.absorbAmount);
      const hpDamage = toNumeric(raw.hpDamage, Math.max(0, mitigated - shieldAbsorb));
      const finalDamage = toNumeric(raw.finalDamage, raw.value, hpDamage);
      const maxValue = Math.max(base, mitigated, shieldAbsorb, hpDamage, finalDamage, 1);
      const segments: DamageWaterfallSegment[] = [
        { key: 'base', label: '基础 / 原始', value: base, valueText: formatMetricValue(base), width: ratioWidth(base, maxValue) },
        { key: 'mitigate', label: '减免后', value: mitigated, valueText: formatMetricValue(mitigated), width: ratioWidth(mitigated, maxValue) },
        { key: 'shield', label: '护盾吸收', value: shieldAbsorb, valueText: formatMetricValue(shieldAbsorb), width: ratioWidth(shieldAbsorb, maxValue) },
        { key: 'hp', label: '生命伤害', value: hpDamage, valueText: formatMetricValue(hpDamage), width: ratioWidth(hpDamage, maxValue) },
        { key: 'final', label: '最终结算', value: finalDamage, valueText: formatMetricValue(finalDamage), width: ratioWidth(finalDamage, maxValue) }
      ];
      const danger = isDangerNode(node);
      return {
        key: buildNodeKey(node),
        node,
        title: node.displayName || node.label || '伤害节点',
        subtitle: `${node.laneLabel || node.stage} · ${node.summary || node.actionLabel || node.conditionLabel || node.domainLabel || node.entityKey}`,
        statusLabel: danger ? '异常' : '正常',
        danger,
        segments
      } satisfies DamageWaterfallItem;
    });
}

function buildLifecycleSummaries(nodes: SkillAnalysisFlatNodeProjection[]): LifecycleSummaryItem[] {
  const lifecycleDomains = new Set(['buff', 'continuous', 'projectile', 'area', 'shield', 'summon', 'movement']);
  const groups = new Map<string, { node: SkillAnalysisFlatNodeProjection; nodes: SkillAnalysisFlatNodeProjection[] }>();
  for (const node of nodes) {
    if (!lifecycleDomains.has(node.domain) && !lifecycleDomains.has(node.entityKind)) continue;
    const key = `${node.domain || node.entityKind}:${node.entityKey || node.runtimeLabel || node.nodeId}`;
    const group = groups.get(key) || { node, nodes: [] };
    group.nodes.push(node);
    if (node.frame < group.node.frame || (node.frame === group.node.frame && node.timeMs < group.node.timeMs)) group.node = node;
    groups.set(key, group);
  }
  return [...groups.entries()]
    .map(([key, group]) => {
      const ordered = group.nodes.slice().sort((a, b) => a.frame - b.frame || a.timeMs - b.timeMs || a.numericNodeId - b.numericNodeId);
      const first = ordered[0];
      const last = ordered[ordered.length - 1];
      const failureCount = ordered.filter(isDangerNode).length;
      return {
        key,
        node: group.node,
        label: group.node.domainLabel || group.node.entityKind || group.node.domain,
        subtitle: `${group.node.laneLabel || group.node.stage} · ${group.node.runtimeLabel || group.node.entityKey}`,
        statusLabel: failureCount > 0 ? '异常' : `${ordered.length} 节点`,
        entityKey: group.node.entityKey || group.node.runtimeLabel || group.node.nodeId,
        firstFrame: first.frame,
        lastFrame: last.frame,
        durationLabel: formatLifecycleDuration(first, last),
        nodeCount: ordered.length,
        failureCount
      } satisfies LifecycleSummaryItem;
    })
    .sort((a, b) => a.firstFrame - b.firstFrame || b.failureCount - a.failureCount || b.nodeCount - a.nodeCount || a.label.localeCompare(b.label))
    .slice(0, 10);
}

function addFact(facts: Array<{ key: string; label: string; value: string }>, raw: Record<string, unknown>, key: string, label: string): void {
  const value = raw[key];
  if (value === undefined || value === null || value === '') return;
  facts.push({ key, label, value: formatFactValue(value) });
}

function formatFactValue(value: unknown): string {
  if (Array.isArray(value)) return value.map(formatFactValue).join(', ');
  if (typeof value === 'object' && value !== null) return JSON.stringify(value);
  return String(value);
}

function toNumeric(...values: unknown[]): number {
  for (const value of values) {
    const numeric = typeof value === 'number' ? value : typeof value === 'string' && value.trim() !== '' ? Number(value) : Number.NaN;
    if (Number.isFinite(numeric)) return numeric;
  }
  return 0;
}

function ratioWidth(value: number, maxValue: number): number {
  if (!Number.isFinite(value) || !Number.isFinite(maxValue) || maxValue <= 0) return 0;
  return Math.max(8, Math.min(100, (value / maxValue) * 100));
}

function formatMetricValue(value: number): string {
  return Number.isInteger(value) ? `${value}` : value.toFixed(1);
}

function formatLifecycleDuration(first: SkillAnalysisFlatNodeProjection, last: SkillAnalysisFlatNodeProjection): string {
  const frameSpan = Math.max(0, last.frame - first.frame);
  const timeSpan = Math.max(0, last.timeMs - first.timeMs);
  if (frameSpan > 0 && timeSpan > 0) return `F${frameSpan} / ${timeSpan}ms`;
  if (frameSpan > 0) return `F${frameSpan}`;
  if (timeSpan > 0) return `${timeSpan}ms`;
  return '单帧';
}

function uniqueFrameTicks(events: SkillAnalysisTimelineEventProjection[]): number[] {
  return [...new Set(events.map(item => item.frame))].sort((a, b) => a - b).slice(0, 12);
}

function truncateLabel(value: string, maxLength: number): string {
  if (!value) return '';
  return value.length > maxLength ? `${value.slice(0, Math.max(0, maxLength - 1))}…` : value;
}
</script>
