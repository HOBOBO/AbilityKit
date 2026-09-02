import { computed, reactive, ref, watch } from 'vue';
import { adminStorage } from '../services/storage';
import { AdminDomainApis } from '../services/domainApi';
import { buildAcceptanceAssertionGroups, buildAcceptanceTraceTree, filterAcceptanceCases, flattenAcceptanceTraceTree } from '../services/skillAcceptanceAnalysis';
import { buildAnalysisArtifactTraceRecords, buildSkillAnalysisEntityRelations, buildSkillAnalysisFilterOptions, buildSkillAnalysisTree, buildTimelineFromAnalysisNodes, buildTimelineFromRuntimeEvents, createDefaultSkillAnalysisFilter, filterSkillAnalysisNodes, flattenSkillAnalysisTree } from '../services/skillAnalysisProjection';
import type { AddRoomRobotsResponse, AdminApiCallLogItem, AdminClusterDiagnostics, AdminDashboardResponse, AdminServerOperationResponse, AdminServerStatus, AdminSkillAcceptanceArtifactDirectoryList, AdminSkillAcceptanceBatch, AdminSkillAcceptanceCase, AdminSkillAcceptanceDeleteResponse, AdminSkillAcceptanceRunPlan, AdminSkillAcceptanceRunRequest, AdminSkillAcceptanceRunResponse, AdminSkillAcceptanceTemplateList, AdminSkillAnalysisArtifact, AdminSkillAnalysisArtifactDirectoryList, AdminSkillAnalysisArtifactList, AdminSkillAnalysisModel, AdminSkillDiagnosticsEvents, AdminSkillDiagnosticsSummary, AdminStartRoomBattleResponse, ApiResult, CreateRoomResponse, GameplayDescriptor, RestoreRoomResponse, RoomRuntimeState, RoomSnapshot, RoomSummary, SessionResponse, ShooterSandboxState, ShooterWorldDiagnostics, SkillAnalysisFlatNodeProjection } from '../types';

const apis = new AdminDomainApis();

const busy = ref(false);
const sessionToken = ref(adminStorage.get('sessionToken', ''));
const region = ref(adminStorage.get('region', 'cn'));
const serverId = ref(adminStorage.get('serverId', 'dev'));
const selectedRoomType = ref('shooter');
const roomId = ref(adminStorage.get('roomId', ''));
const gameplays = ref<GameplayDescriptor[]>([]);
const rooms = ref<RoomSummary[]>([]);
const snapshot = ref<RoomSnapshot | null>(null);
const runtimeState = ref<RoomRuntimeState | null>(null);
const dashboard = ref<AdminDashboardResponse | null>(null);
const serverStatus = ref<AdminServerStatus | null>(null);
const clusterDiagnostics = ref<AdminClusterDiagnostics | null>(null);
const skillSummary = ref<AdminSkillDiagnosticsSummary | null>(null);
const skillEvents = ref<AdminSkillDiagnosticsEvents | null>(null);
const skillAnalysisModel = ref<AdminSkillAnalysisModel | null>(null);
const acceptanceBatch = ref<AdminSkillAcceptanceBatch | null>(null);
const acceptanceCase = ref<AdminSkillAcceptanceCase | null>(null);
const acceptanceRunPlan = ref<AdminSkillAcceptanceRunPlan | null>(null);
const acceptanceArtifactDirectories = ref<AdminSkillAcceptanceArtifactDirectoryList | null>(null);
const acceptanceTemplates = ref<AdminSkillAcceptanceTemplateList | null>(null);
const analysisArtifactDirectories = ref<AdminSkillAnalysisArtifactDirectoryList | null>(null);
const analysisArtifactList = ref<AdminSkillAnalysisArtifactList | null>(null);
const analysisArtifact = ref<AdminSkillAnalysisArtifact | null>(null);
const acceptanceLastRun = ref<AdminSkillAcceptanceRunResponse | null>(null);
const shooterWorldDiagnostics = ref<ShooterWorldDiagnostics | null>(null);
const lastResponse = ref('');
const lastRobotAdd = ref<AddRoomRobotsResponse | null>(null);
const lastBattleStart = ref<AdminStartRoomBattleResponse | null>(null);
const apiCallLog = ref<AdminApiCallLogItem[]>([]);
let apiCallLogId = 0;

const account = reactive({ accountId: adminStorage.get('accountId', ''), expireSeconds: 86400, kickExisting: true });
const create = reactive({ roomType: 'shooter', title: 'Shooter 后台房间', isPublic: true, maxPlayers: 4, tagsJson: '{\n  "source": "admin-console"\n}' });
const battle = reactive({ gameplayId: 2, ruleSetId: 0, configVersion: 1, protocolVersion: 1, worldType: 'shooter_battle', syncTemplateId: 'state-sync-authority' });
const skillLoadout = reactive({ heroId: 1, teamId: 1, spawnPointId: 1, level: 1, attributeTemplateId: 1, basicAttackSkillId: 1001, skillIdsText: '1002,1003,1004' });
const skillEventFilter = reactive<{ battleId: string; actorId: number | null; skillId: number | null; limit: number }>({ battleId: '', actorId: null, skillId: null, limit: 100 });
const sandbox = reactive<{ sandboxId: string; botCount: number; maxPlayers: number; tickRate: number; state: ShooterSandboxState | null }>({ sandboxId: 'default', botCount: 3, maxPlayers: 4, tickRate: 30, state: null });
const roomRobots = reactive({ count: 1, accountPrefix: 'room-robot', autoReady: true, mountBattleAi: true, battleAiProfileId: 'simple-battle' });
const serverOperation = reactive({ reason: '后台控制台操作' });
const acceptance = reactive({ artifactDirectory: 'artifacts/moba-acceptance', selectedCaseId: '', selectedCaseIds: [] as string[], traceLimit: 500, statusFilter: 'all', searchText: '', categoryFilter: '', tagFilter: '', sortKey: 'caseId', selectedTemplateId: 'lianpo-skill1-dash', runCaseId: '', runDescription: '运行真实英雄技能 DSL 场景', runActorId: 0, runTargetActorId: 0, runSkillId: 0, runEffectId: 0, runProjectileId: 0, runAreaId: 0, runBuffId: 0, runShieldId: 0, runBaseDamage: 0, runMitigatedDamage: 0, runShieldAbsorb: 0, runHpDamage: 0, runTickRate: 30, runDurationFrames: 0, runOperatorReason: '后台真实 DSL 技能分析' });
const analysisArtifacts = reactive({ artifactDirectory: 'sample-web-output-analysis', selectedFileName: '' });
const acceptanceAnalysisFilter = reactive(createDefaultSkillAnalysisFilter());
const runtimeAnalysisFilter = reactive(createDefaultSkillAnalysisFilter());
const artifactAnalysisFilter = reactive(createDefaultSkillAnalysisFilter());
const selectedAcceptanceAnalysisNodeKey = ref('');
const selectedRuntimeAnalysisNodeKey = ref('');
const selectedArtifactAnalysisNodeKey = ref('');
const selectedShooterInspectorRoomId = ref(adminStorage.get('selectedShooterInspectorRoomId', ''));
const selectedShooterWorldEntityKey = ref('');

const selectedGameplay = computed(() => gameplays.value.find(x => x.roomType === selectedRoomType.value) || null);
const selectedRoomSummary = computed(() => roomId.value ? rooms.value.find(room => room.roomId === roomId.value) || null : null);
const effectiveRoomId = computed(() => roomId.value || snapshot.value?.summary?.roomId || selectedRoomSummary.value?.roomId || '');
const hasSession = computed(() => Boolean(sessionToken.value));
const hasCurrentRoom = computed(() => Boolean(effectiveRoomId.value));
const canOperateCurrentRoom = computed(() => !busy.value && hasSession.value && hasCurrentRoom.value);
const accountLabel = computed(() => dashboard.value?.accountId || account.accountId || (sessionToken.value ? '已登录会话' : '未登录'));
const sandboxLabel = computed(() => sandbox.state?.running ? '运行中' : '已停止');
const serverModeLabel = computed(() => {
  if (!serverStatus.value) return '未知';
  const flags = [];
  if (serverStatus.value.restartRequested) flags.push('已请求重启');
  if (serverStatus.value.maintenanceMode) flags.push('维护中');
  if (serverStatus.value.drainMode) flags.push('排空中');
  return flags.length > 0 ? flags.join(' / ') : '正常';
});
const clusterLabel = computed(() => clusterDiagnostics.value ? `${clusterDiagnostics.value.clusterId} / ${clusterDiagnostics.value.serviceId}` : '未知');
const skillBattleId = computed(() => skillEventFilter.battleId || skillSummary.value?.battleId || runtimeState.value?.battleId || '');
const battleId = computed(() => skillBattleId.value || runtimeState.value?.battleId || '');
const acceptancePassedCount = computed(() => acceptanceBatch.value?.cases?.filter(x => x.passed === true).length || 0);
const acceptanceFailedCount = computed(() => acceptanceBatch.value?.cases?.filter(x => x.passed === false).length || 0);
const acceptanceUnknownCount = computed(() => acceptanceBatch.value?.cases?.filter(x => x.passed !== true && x.passed !== false).length || 0);
const acceptanceFilteredCases = computed(() => filterAcceptanceCases(acceptanceBatch.value?.cases || [], acceptance));
const acceptanceSelectedCount = computed(() => acceptance.selectedCaseIds.length);
const acceptanceAllFilteredSelected = computed(() => acceptanceFilteredCases.value.length > 0 && acceptanceFilteredCases.value.every(item => acceptance.selectedCaseIds.includes(item.caseId)));
const acceptanceTracePreview = computed(() => acceptanceCase.value?.traceRecords?.slice(0, 80) || []);
const acceptanceTraceTree = computed(() => buildAcceptanceTraceTree(acceptanceCase.value?.traceRecords || []));
const acceptanceTraceFlat = computed(() => flattenAcceptanceTraceTree(acceptanceTraceTree.value));
const acceptanceAssertionGroups = computed(() => buildAcceptanceAssertionGroups(acceptanceCase.value?.summary || null, acceptanceCase.value?.caseId || acceptanceBatch.value?.cases?.find(x => x.caseId)?.caseId || ''));
const acceptanceAnalysisTree = computed(() => buildSkillAnalysisTree(acceptanceCase.value?.traceRecords || [], 'scenario-acceptance-artifacts', acceptanceCase.value?.caseId || acceptance.selectedCaseId || ''));
const acceptanceAnalysisFlat = computed(() => flattenSkillAnalysisTree(acceptanceAnalysisTree.value));
const acceptanceAnalysisFilterOptions = computed(() => buildSkillAnalysisFilterOptions(acceptanceAnalysisFlat.value));
const acceptanceAnalysisFilteredFlat = computed(() => filterSkillAnalysisNodes(acceptanceAnalysisFlat.value, acceptanceAnalysisFilter));
const acceptanceAnalysisTimeline = computed(() => buildTimelineFromAnalysisNodes(acceptanceAnalysisFilteredFlat.value));
const acceptanceAnalysisEntityRelations = computed(() => buildSkillAnalysisEntityRelations(acceptanceAnalysisFilteredFlat.value));
const runtimeAnalysisFlat = computed<SkillAnalysisFlatNodeProjection[]>(() => flattenSkillAnalysisTree(buildSkillAnalysisTree(buildRuntimeAnalysisRecords(skillEvents.value), 'runtime-diagnostics', skillBattleId.value)));
const runtimeAnalysisFilterOptions = computed(() => buildSkillAnalysisFilterOptions(runtimeAnalysisFlat.value));
const runtimeAnalysisFilteredFlat = computed(() => filterSkillAnalysisNodes(runtimeAnalysisFlat.value, runtimeAnalysisFilter));
const runtimeAnalysisTimeline = computed(() => runtimeAnalysisFilteredFlat.value.length > 0 ? buildTimelineFromAnalysisNodes(runtimeAnalysisFilteredFlat.value) : buildTimelineFromRuntimeEvents(skillEvents.value));
const runtimeAnalysisEntityRelations = computed(() => buildSkillAnalysisEntityRelations(runtimeAnalysisFilteredFlat.value));
const artifactAnalysisRecords = computed(() => buildAnalysisArtifactTraceRecords(analysisArtifact.value?.artifact || null));
const artifactAnalysisTree = computed(() => buildSkillAnalysisTree(artifactAnalysisRecords.value, 'offline-analysis-artifact', analysisArtifact.value?.fileName || analysisArtifacts.selectedFileName || ''));
const artifactAnalysisFlat = computed(() => flattenSkillAnalysisTree(artifactAnalysisTree.value));
const artifactAnalysisFilterOptions = computed(() => buildSkillAnalysisFilterOptions(artifactAnalysisFlat.value));
const artifactAnalysisFilteredFlat = computed(() => filterSkillAnalysisNodes(artifactAnalysisFlat.value, artifactAnalysisFilter));
const artifactAnalysisTimeline = computed(() => buildTimelineFromAnalysisNodes(artifactAnalysisFilteredFlat.value));
const artifactAnalysisEntityRelations = computed(() => buildSkillAnalysisEntityRelations(artifactAnalysisFilteredFlat.value));
const selectedAcceptanceAnalysisNode = computed(() => findSelectedAnalysisNode(acceptanceAnalysisFilteredFlat.value, selectedAcceptanceAnalysisNodeKey.value));
const selectedRuntimeAnalysisNode = computed(() => findSelectedAnalysisNode(runtimeAnalysisFilteredFlat.value, selectedRuntimeAnalysisNodeKey.value));
const selectedArtifactAnalysisNode = computed(() => findSelectedAnalysisNode(artifactAnalysisFilteredFlat.value, selectedArtifactAnalysisNodeKey.value));
const skillDiagnosticsWarnings = computed(() => [...(skillSummary.value?.warnings || []), ...(skillEvents.value?.warnings || []), ...(analysisArtifactList.value?.warnings || []), ...(analysisArtifact.value?.warnings || [])]);
const skillAnalysisModelNotes = computed(() => skillAnalysisModel.value?.notes || []);
const apiFailureCount = computed(() => apiCallLog.value.filter(item => !item.ok).length);
const shooterInspectorRooms = computed(() => rooms.value.filter(room => room.roomType === 'shooter'));
const selectedShooterWorldEntity = computed(() => shooterWorldDiagnostics.value?.entities?.find(entity => entity.key === selectedShooterWorldEntityKey.value) || shooterWorldDiagnostics.value?.entities?.[0] || null);

watch(sessionToken, value => adminStorage.set('sessionToken', value));
watch(() => account.accountId, value => adminStorage.set('accountId', value));
watch(region, value => adminStorage.set('region', value));
watch(serverId, value => adminStorage.set('serverId', value));
watch(roomId, value => adminStorage.set('roomId', value));
watch(selectedShooterInspectorRoomId, value => adminStorage.set('selectedShooterInspectorRoomId', value));

function summarizeApiBody(body: unknown): string {
  if (body === null || body === undefined) return '';
  if (typeof body === 'string') return body.slice(0, 180);
  if (typeof body === 'object') {
    const record = body as Record<string, unknown>;
    const message = record.message || record.error || record.title || record.operation || record.diagnosticsStatus;
    if (message) return String(message).slice(0, 180);
  }
  try {
    return JSON.stringify(body).slice(0, 180);
  } catch {
    return String(body).slice(0, 180);
  }
}

function pushApiCallLog(result: ApiResult<unknown>): void {
  apiCallLog.value = [{
    id: ++apiCallLogId,
    ok: result.ok,
    status: result.status,
    statusText: result.statusText || (result.ok ? 'OK' : 'Request failed'),
    method: result.method || 'HTTP',
    url: result.url || 'unknown',
    durationMs: result.durationMs ?? 0,
    occurredAt: new Date().toLocaleTimeString(),
    summary: summarizeApiBody(result.body)
  }, ...apiCallLog.value].slice(0, 24);
}

function pushApiErrorLog(error: unknown): void {
  apiCallLog.value = [{
    id: ++apiCallLogId,
    ok: false,
    status: 0,
    statusText: 'Network error',
    method: 'HTTP',
    url: 'network',
    durationMs: 0,
    occurredAt: new Date().toLocaleTimeString(),
    summary: String(error).slice(0, 180)
  }, ...apiCallLog.value].slice(0, 24);
}

async function call<T>(operation: Promise<ApiResult<T>>): Promise<T | null> {
  busy.value = true;
  try {
    const result = await operation;
    pushApiCallLog(result as ApiResult<unknown>);
    lastResponse.value = JSON.stringify(result, null, 2);
    return result.ok ? result.body as T : null;
  } catch (error) {
    pushApiErrorLog(error);
    lastResponse.value = String(error);
    return null;
  } finally {
    busy.value = false;
  }
}

async function command(operation: Promise<ApiResult<unknown>>): Promise<boolean> {
  busy.value = true;
  try {
    const result = await operation;
    pushApiCallLog(result);
    lastResponse.value = JSON.stringify(result, null, 2);
    return result.ok;
  } catch (error) {
    pushApiErrorLog(error);
    lastResponse.value = String(error);
    return false;
  } finally {
    busy.value = false;
  }
}

function buildQuery(params: Record<string, string | number | null | undefined>): string {
  const query = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== null && value !== undefined && value !== '') query.set(key, String(value));
  });
  const text = query.toString();
  return text ? `?${text}` : '';
}

function formatBytes(value: number): string {
  if (!Number.isFinite(value) || value <= 0) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB'];
  let size = value;
  let unit = 0;
  while (size >= 1024 && unit < units.length - 1) {
    size /= 1024;
    unit += 1;
  }
  return `${size.toFixed(unit === 0 ? 0 : 1)} ${units[unit]}`;
}

function formatDuration(seconds: number): string {
  const value = Math.max(0, Math.floor(seconds || 0));
  const days = Math.floor(value / 86400);
  const hours = Math.floor((value % 86400) / 3600);
  const minutes = Math.floor((value % 3600) / 60);
  if (days > 0) return `${days}d ${hours}h`;
  if (hours > 0) return `${hours}h ${minutes}m`;
  return `${minutes}m`;
}

function applyGameplayDefaults(): void {
  const gameplay = selectedGameplay.value;
  if (!gameplay) return;
  create.roomType = gameplay.roomType;
  create.maxPlayers = gameplay.defaultMaxPlayers || create.maxPlayers;
  battle.gameplayId = Number(gameplay.gameplayId || battle.gameplayId || 0);
  battle.worldType = gameplay.defaultWorldType || gameplay.roomType;
  battle.syncTemplateId = gameplay.defaultSyncTemplateId || '';
}

async function refreshDashboard(): Promise<void> {
  const requestedRoomId = roomId.value;
  const data = await call<AdminDashboardResponse>(apis.dashboard.dashboard({ sessionToken: sessionToken.value || null, region: region.value, serverId: serverId.value, roomType: selectedRoomType.value, limit: 50, sandboxId: sandbox.sandboxId || 'default' }));
  if (!data) return;
  dashboard.value = data;
  gameplays.value = data.gameplays || [];
  rooms.value = data.rooms || [];
  sandbox.state = data.shooterSandbox || null;
  serverStatus.value = data.serverStatus || serverStatus.value;
  runtimeState.value = data.runtimeState || runtimeState.value;
  if (data.currentRoomId) roomId.value = data.currentRoomId;
  if (data.currentRoom?.snapshot) snapshot.value = data.currentRoom.snapshot;
  reconcileSelectedRoomAfterDashboard(requestedRoomId, data.currentRoomId || '');
  if (!gameplays.value.some(x => x.roomType === selectedRoomType.value) && gameplays.value.length > 0) selectedRoomType.value = gameplays.value[0].roomType;
  applyGameplayDefaults();
  if (!skillEventFilter.battleId && data.runtimeState?.battleId) skillEventFilter.battleId = data.runtimeState.battleId;
}

function reconcileSelectedRoomAfterDashboard(previousRoomId: string, currentRoomId: string): void {
  if (!roomId.value && !previousRoomId) return;
  const selectedStillExists = roomId.value ? rooms.value.some(room => room.roomId === roomId.value) : false;
  if (selectedStillExists) return;

  if (currentRoomId && rooms.value.some(room => room.roomId === currentRoomId)) {
    roomId.value = currentRoomId;
    return;
  }

  snapshot.value = null;
  runtimeState.value = null;
  roomId.value = '';
}

async function refreshServerStatus(): Promise<void> {
  const data = await call<AdminServerStatus>(apis.ops.status());
  if (data) serverStatus.value = data;
}

async function refreshClusterDiagnostics(): Promise<void> {
  const data = await call<AdminClusterDiagnostics>(apis.cluster.diagnostics());
  if (!data) return;
  clusterDiagnostics.value = data;
  serverStatus.value = data.gatewayProcess || serverStatus.value;
}

async function refreshSkillSummary(): Promise<void> {
  const data = await call<AdminSkillDiagnosticsSummary>(apis.skills.summary(buildQuery({ roomId: roomId.value || runtimeState.value?.roomId, battleId: skillBattleId.value })));
  if (!data) return;
  skillSummary.value = data;
  if (!skillEventFilter.battleId && data.battleId) skillEventFilter.battleId = data.battleId;
}

async function refreshSkillEvents(): Promise<void> {
  const data = await call<AdminSkillDiagnosticsEvents>(apis.skills.events(buildQuery({ battleId: skillBattleId.value, actorId: skillEventFilter.actorId, skillId: skillEventFilter.skillId, limit: skillEventFilter.limit || 100 })));
  if (data) skillEvents.value = data;
}

async function refreshSkillAnalysisModel(): Promise<void> {
  const data = await call<AdminSkillAnalysisModel>(apis.skills.analysisModel());
  if (data) skillAnalysisModel.value = data;
}

async function refreshSkillDiagnostics(): Promise<void> {
  await refreshSkillSummary();
  await refreshSkillEvents();
  await refreshSkillAnalysisModel();
}

async function refreshAcceptanceArtifactDirectories(): Promise<void> {
  const data = await call<AdminSkillAcceptanceArtifactDirectoryList>(apis.skills.acceptanceArtifactDirectories());
  if (data) acceptanceArtifactDirectories.value = data;
}

async function refreshAcceptanceTemplates(): Promise<void> {
  const data = await call<AdminSkillAcceptanceTemplateList>(apis.skills.acceptanceTemplates());
  if (!data) return;
  acceptanceTemplates.value = data;
  if (!acceptance.selectedTemplateId && data.templates?.length) acceptance.selectedTemplateId = data.templates[0].id;
}

async function refreshAcceptanceBatch(): Promise<void> {
  const data = await call<AdminSkillAcceptanceBatch>(apis.skills.acceptanceBatch(buildQuery({ artifactDirectory: acceptance.artifactDirectory })));
  if (!data) return;
  acceptanceBatch.value = data;
  const caseIds = new Set((data.cases || []).map(item => item.caseId));
  acceptance.selectedCaseIds = acceptance.selectedCaseIds.filter(caseId => caseIds.has(caseId));
  if (!acceptance.selectedCaseId || !caseIds.has(acceptance.selectedCaseId)) acceptance.selectedCaseId = data.cases?.[0]?.caseId || '';
}

async function refreshAcceptanceCase(caseId = acceptance.selectedCaseId): Promise<void> {
  if (!caseId) return;
  acceptance.selectedCaseId = caseId;
  const data = await call<AdminSkillAcceptanceCase>(apis.skills.acceptanceCase(caseId, buildQuery({ artifactDirectory: acceptance.artifactDirectory, traceLimit: acceptance.traceLimit || 500 })));
  if (data) acceptanceCase.value = data;
}

async function refreshAcceptanceRunPlan(): Promise<void> {
  const data = await call<AdminSkillAcceptanceRunPlan>(apis.skills.acceptanceRunPlan(buildQuery({ artifactDirectory: acceptance.artifactDirectory })));
  if (data) acceptanceRunPlan.value = data;
}

function toNumber(value: unknown): number {
  if (typeof value === 'number' && Number.isFinite(value)) return value;
  if (typeof value === 'string' && value.trim() !== '') {
    const parsed = Number(value);
    if (Number.isFinite(parsed)) return parsed;
  }
  return 0;
}

function toText(value: unknown): string {
  if (value === null || value === undefined) return '';
  if (typeof value === 'string') return value;
  if (typeof value === 'number' || typeof value === 'boolean') return String(value);
  try {
    return JSON.stringify(value);
  } catch {
    return String(value);
  }
}

function buildRuntimeAnalysisRecords(events: AdminSkillDiagnosticsEvents | null): Record<string, unknown>[] {
  if (!events?.isDataAvailable) return [];

  return (events.events || [])
    .filter(event => event.nodeId > 0 && event.rootId > 0)
    .map(event => {
      const severity = event.severity || 'info';
      const label = `${event.stage || 'runtime'} · ${event.eventType}`;

      return {
        schemaVersion: events.schemaVersion,
        battleId: event.battleId,
        worldId: event.worldId,
        sessionId: event.sessionId,
        generation: event.generation,
        sequence: event.sequence,
        monotonicTimestamp: event.monotonicTimestamp,
        nodeId: event.nodeId,
        rootId: event.rootId,
        parentId: event.parentId,
        kind: event.eventType || 'runtime-event',
        eventType: event.eventType,
        stage: event.stage || 'runtime-event',
        status: severity,
        severity,
        frame: Number(event.frame || 0),
        actorId: event.actorId,
        sourceActorId: event.actorId,
        targetActorId: event.targetActorId,
        skillId: event.skillId,
        sourceContextId: String(event.sourceContextId),
        rootContextId: String(event.rootContextId),
        ownerContextId: String(event.ownerContextId),
        entityKind: 'runtime-event',
        runtimeKind: event.eventType || 'runtime-event',
        entityId: event.skillInstanceId || event.nodeId,
        displayName: label,
        name: label,
        debugName: label,
        message: event.message || label,
        result: event.value ?? event.message ?? label,
        eventId: event.sequence,
        instanceId: event.skillInstanceId,
        value: event.value
      };
    });
}

function getAnalysisNodeKey(node: SkillAnalysisFlatNodeProjection): string {
  return `${node.source}:${node.nodeId}`;
}

function findSelectedAnalysisNode(nodes: SkillAnalysisFlatNodeProjection[], key: string): SkillAnalysisFlatNodeProjection | null {
  return nodes.find(node => getAnalysisNodeKey(node) === key) || nodes[0] || null;
}

function selectAcceptanceAnalysisNode(node: SkillAnalysisFlatNodeProjection): void {
  selectedAcceptanceAnalysisNodeKey.value = getAnalysisNodeKey(node);
}

function selectRuntimeAnalysisNode(node: SkillAnalysisFlatNodeProjection): void {
  selectedRuntimeAnalysisNodeKey.value = getAnalysisNodeKey(node);
}

async function refreshAnalysisArtifactDirectories(): Promise<void> {
  const data = await call<AdminSkillAnalysisArtifactDirectoryList>(apis.skills.analysisArtifactDirectories());
  if (data) analysisArtifactDirectories.value = data;
}

async function refreshAnalysisArtifacts(): Promise<void> {
  const data = await call<AdminSkillAnalysisArtifactList>(apis.skills.analysisArtifacts(buildQuery({ artifactDirectory: analysisArtifacts.artifactDirectory })));
  if (!data) return;
  analysisArtifactList.value = data;
  const fileNames = new Set((data.artifacts || []).map(item => item.fileName));
  if (!analysisArtifacts.selectedFileName || !fileNames.has(analysisArtifacts.selectedFileName)) analysisArtifacts.selectedFileName = data.artifacts?.[0]?.fileName || '';
}

async function refreshAnalysisArtifact(fileName = analysisArtifacts.selectedFileName): Promise<void> {
  if (!fileName) return;
  analysisArtifacts.selectedFileName = fileName;
  const data = await call<AdminSkillAnalysisArtifact>(apis.skills.analysisArtifact(fileName, buildQuery({ artifactDirectory: analysisArtifacts.artifactDirectory })));
  if (data) analysisArtifact.value = data;
}

async function refreshOfflineAnalysisArtifacts(): Promise<void> {
  await refreshAnalysisArtifactDirectories();
  await refreshAnalysisArtifacts();
  if (analysisArtifacts.selectedFileName) await refreshAnalysisArtifact(analysisArtifacts.selectedFileName);
}

function selectAnalysisArtifactDirectory(value: string): void {
  if (!value) return;
  analysisArtifacts.artifactDirectory = value;
  analysisArtifacts.selectedFileName = '';
  analysisArtifactList.value = null;
  analysisArtifact.value = null;
  selectedArtifactAnalysisNodeKey.value = '';
}

function selectArtifactAnalysisNode(node: SkillAnalysisFlatNodeProjection): void {
  selectedArtifactAnalysisNodeKey.value = getAnalysisNodeKey(node);
}

async function refreshAcceptanceArtifacts(): Promise<void> {
  await refreshAcceptanceArtifactDirectories();
  await refreshAcceptanceTemplates();
  await refreshAcceptanceBatch();
  await refreshAcceptanceRunPlan();
  if (acceptance.selectedCaseId) await refreshAcceptanceCase(acceptance.selectedCaseId);
}

function isAcceptanceCaseSelected(caseId: string): boolean {
  return acceptance.selectedCaseIds.includes(caseId);
}

function toggleAcceptanceCaseSelection(caseId: string): void {
  if (!caseId) return;
  if (isAcceptanceCaseSelected(caseId)) {
    acceptance.selectedCaseIds = acceptance.selectedCaseIds.filter(item => item !== caseId);
    return;
  }

  acceptance.selectedCaseIds = [...acceptance.selectedCaseIds, caseId];
}

function selectAllFilteredAcceptanceCases(): void {
  acceptance.selectedCaseIds = acceptanceFilteredCases.value.map(item => item.caseId);
}

function clearAcceptanceCaseSelection(): void {
  acceptance.selectedCaseIds = [];
}

async function deleteAcceptanceCases(caseIds = acceptance.selectedCaseIds): Promise<void> {
  const requestedCaseIds = [...new Set((caseIds || []).map(caseId => caseId.trim()).filter(Boolean))];
  if (!requestedCaseIds.length) return;
  const data = await call<AdminSkillAcceptanceDeleteResponse>(apis.skills.acceptanceDelete({ sessionToken: sessionToken.value || null, artifactDirectory: acceptance.artifactDirectory, caseIds: requestedCaseIds, operatorReason: acceptance.runOperatorReason || null }));
  if (!data) return;
  acceptanceBatch.value = data.batch;
  acceptance.selectedCaseIds = acceptance.selectedCaseIds.filter(caseId => !data.deletedCaseIds.includes(caseId));
  if (acceptance.selectedCaseId && data.deletedCaseIds.includes(acceptance.selectedCaseId)) {
    acceptance.selectedCaseId = data.batch.cases?.[0]?.caseId || '';
  }
  if (acceptance.selectedCaseId) await refreshAcceptanceCase(acceptance.selectedCaseId);
}

async function deleteAcceptanceCase(caseId: string): Promise<void> {
  if (!caseId) return;
  await deleteAcceptanceCases([caseId]);
}

function selectAcceptanceArtifactDirectory(value: string): void {
  if (!value) return;
  acceptance.artifactDirectory = value;
  acceptance.selectedCaseId = '';
  acceptance.selectedCaseIds = [];
  acceptanceCase.value = null;
}

function applyAcceptanceTemplate(templateId = acceptance.selectedTemplateId): void {
  const template = acceptanceTemplates.value?.templates?.find(item => item.id === templateId);
  if (!template) return;
  acceptance.selectedTemplateId = template.id;
  acceptance.runCaseId = template.defaults.caseId || '';
  acceptance.runDescription = template.description || acceptance.runDescription;
  acceptance.runSkillId = template.defaults.skillId;
  acceptance.runTickRate = template.defaults.tickRate;
}

function buildAcceptanceRunRequest(): AdminSkillAcceptanceRunRequest {
  return {
    sessionToken: sessionToken.value || null,
    artifactDirectory: acceptance.artifactDirectory,
    caseId: acceptance.runCaseId || null,
    description: acceptance.runDescription,
    actorId: Number(acceptance.runActorId || 1),
    targetActorId: Number(acceptance.runTargetActorId || 2),
    skillId: Number(acceptance.runSkillId || 1002),
    effectId: Number(acceptance.runEffectId || 2001),
    projectileId: Number(acceptance.runProjectileId || 0),
    areaId: Number(acceptance.runAreaId || 0),
    buffId: Number(acceptance.runBuffId || 0),
    shieldId: Number(acceptance.runShieldId || 0),
    baseDamage: Number(acceptance.runBaseDamage || 0),
    mitigatedDamage: Number(acceptance.runMitigatedDamage || 0),
    shieldAbsorb: Number(acceptance.runShieldAbsorb || 0),
    hpDamage: Number(acceptance.runHpDamage || 0),
    tickRate: Number(acceptance.runTickRate || 30),
    durationFrames: Number(acceptance.runDurationFrames || 12),
    templateId: acceptance.selectedTemplateId || null,
    operatorReason: acceptance.runOperatorReason || null
  };
}

async function runAcceptanceAnalysis(): Promise<void> {
  const data = await call<AdminSkillAcceptanceRunResponse>(apis.skills.acceptanceRun(buildAcceptanceRunRequest()));
  if (!data) return;
  acceptanceLastRun.value = data;
  acceptanceBatch.value = data.batch;
  acceptance.artifactDirectory = data.artifactDirectory;
  acceptance.selectedCaseId = data.caseId;
  await refreshAcceptanceArtifactDirectories();
  await refreshAcceptanceCase(data.caseId);
}

async function setMaintenanceMode(enabled: boolean): Promise<void> {
  const data = await call<AdminServerOperationResponse>(apis.ops.maintenance({ sessionToken: sessionToken.value, enabled, reason: serverOperation.reason }));
  if (data?.status) serverStatus.value = data.status;
}

async function setDrainMode(enabled: boolean): Promise<void> {
  const data = await call<AdminServerOperationResponse>(apis.ops.drain({ sessionToken: sessionToken.value, enabled, reason: serverOperation.reason }));
  if (data?.status) serverStatus.value = data.status;
}

async function requestRestart(): Promise<void> {
  const data = await call<AdminServerOperationResponse>(apis.ops.restartRequest({ sessionToken: sessionToken.value, enabled: true, reason: serverOperation.reason }));
  if (data?.status) serverStatus.value = data.status;
}

async function guestLogin(): Promise<void> {
  const data = await call<SessionResponse>(apis.session.guestLogin());
  if (data?.sessionToken) sessionToken.value = data.sessionToken;
  await refreshDashboard();
}

async function accountLogin(): Promise<void> {
  const data = await call<SessionResponse>(apis.session.accountLogin({ accountId: account.accountId, expireSeconds: Number(account.expireSeconds || 86400), kickExisting: account.kickExisting === true }));
  if (data?.sessionToken) sessionToken.value = data.sessionToken;
  await refreshDashboard();
}

async function validateSession(): Promise<void> {
  await call<SessionResponse>(apis.session.validate({ sessionToken: sessionToken.value }));
}

async function logout(): Promise<void> {
  await call(apis.session.logout({ sessionToken: sessionToken.value }));
  sessionToken.value = '';
  await refreshDashboard();
}

function parseTags(): Record<string, string> | null {
  const text = create.tagsJson.trim();
  return text ? JSON.parse(text) as Record<string, string> : null;
}

function parseSkillIds(): number[] {
  return skillLoadout.skillIdsText.split(',').map(x => Number(x.trim())).filter(x => Number.isFinite(x) && x > 0);
}

async function submitMobaLoadout(): Promise<void> {
  const data = await call<RoomSnapshot>(apis.rooms.pickHero({ sessionToken: sessionToken.value, roomId: roomId.value, heroId: Number(skillLoadout.heroId || 1), teamId: Number(skillLoadout.teamId || 1), spawnPointId: Number(skillLoadout.spawnPointId || 1), level: Number(skillLoadout.level || 1), attributeTemplateId: Number(skillLoadout.attributeTemplateId || 1), basicAttackSkillId: Number(skillLoadout.basicAttackSkillId || 1), skillIds: parseSkillIds() }));
  if (data) snapshot.value = data;
  await refreshDashboard();
  await refreshSkillDiagnostics();
}

async function createRoom(): Promise<void> {
  let tags: Record<string, string> | null = null;
  try { tags = parseTags(); } catch { lastResponse.value = '标签 JSON 格式无效'; return; }
  const data = await call<CreateRoomResponse>(apis.rooms.create({ sessionToken: sessionToken.value, region: region.value, serverId: serverId.value, roomType: create.roomType, title: create.title, isPublic: create.isPublic === true, maxPlayers: Number(create.maxPlayers || 0), tags }));
  if (data?.roomId) roomId.value = data.roomId;
  await refreshDashboard();
}

async function selectRoom(value: string): Promise<void> {
  roomId.value = value;
  const selected = rooms.value.find(room => room.roomId === value);
  if (selected) {
    selectedRoomType.value = selected.roomType || selectedRoomType.value;
    create.roomType = selected.roomType || create.roomType;
  }

  snapshot.value = selected ? { summary: selected, members: [], canStart: false } : null;
  runtimeState.value = null;
  if (sessionToken.value && value) await joinRoom(value);
}

async function joinRoom(targetRoomId = effectiveRoomId.value): Promise<boolean> {
  if (!targetRoomId) return false;
  roomId.value = targetRoomId;
  const data = await call<CreateRoomResponse>(apis.rooms.join({ sessionToken: sessionToken.value, roomId: targetRoomId }));
  if (!data?.snapshot) return false;
  roomId.value = data.snapshot.summary?.roomId || targetRoomId;
  snapshot.value = data.snapshot;
  selectedRoomType.value = data.snapshot.summary?.roomType || selectedRoomType.value;
  await refreshDashboard();
  return true;
}

async function restoreCurrentRoom(): Promise<void> {
  const data = await call<RestoreRoomResponse>(apis.rooms.restoreCurrent({ sessionToken: sessionToken.value }));
  if (data?.hasActiveRoom && data.snapshot) {
    snapshot.value = data.snapshot;
    roomId.value = data.snapshot.summary?.roomId || roomId.value;
  }
}

async function leaveRoom(targetRoomId = effectiveRoomId.value): Promise<void> {
  if (!targetRoomId) return;
  roomId.value = targetRoomId;
  const leavingRoomId = targetRoomId;
  const ok = await command(apis.rooms.leave({ sessionToken: sessionToken.value, roomId: leavingRoomId }));
  if (!ok) return;
  snapshot.value = null;
  runtimeState.value = null;
  roomId.value = '';
  await refreshDashboard();
  await selectNextAvailableRoom(leavingRoomId);
}

async function markOffline(targetRoomId = effectiveRoomId.value): Promise<void> {
  if (!targetRoomId) return;
  roomId.value = targetRoomId;
  const data = await call<RoomRuntimeState>(apis.rooms.markOffline({ sessionToken: sessionToken.value, roomId: targetRoomId }));
  if (data) runtimeState.value = data;
}

async function closeRoom(targetRoomId = effectiveRoomId.value): Promise<void> {
  if (!targetRoomId) return;
  const closingCurrentRoom = roomId.value === targetRoomId || snapshot.value?.summary?.roomId === targetRoomId;
  const ok = await command(apis.rooms.close({ sessionToken: sessionToken.value, roomId: targetRoomId }));
  if (!ok) return;
  rooms.value = rooms.value.filter(room => room.roomId !== targetRoomId);
  if (closingCurrentRoom) {
    snapshot.value = null;
    runtimeState.value = null;
    roomId.value = '';
  }
  await refreshDashboard();
  if (!roomId.value || roomId.value === targetRoomId) selectNextAvailableRoom(targetRoomId);
}

async function refreshShooterWorldDiagnostics(targetRoomId = selectedShooterInspectorRoomId.value || effectiveRoomId.value): Promise<void> {
  if (!targetRoomId) return;
  selectedShooterInspectorRoomId.value = targetRoomId;
  const data = await call<ShooterWorldDiagnostics>(apis.rooms.shooterWorld(buildQuery({ roomId: targetRoomId })));
  if (!data) return;
  shooterWorldDiagnostics.value = data;
  if (!selectedShooterWorldEntityKey.value || !data.entities.some(entity => entity.key === selectedShooterWorldEntityKey.value)) {
    selectedShooterWorldEntityKey.value = data.entities[0]?.key || '';
  }
}

function selectShooterWorldEntity(key: string): void {
  selectedShooterWorldEntityKey.value = key;
}

function selectNextAvailableRoom(excludedRoomId: string): void {
  const next = rooms.value.find(room => room.roomId !== excludedRoomId);
  if (!next) return;
  roomId.value = next.roomId;
  selectedRoomType.value = next.roomType || selectedRoomType.value;
  create.roomType = next.roomType || create.roomType;
  snapshot.value = { summary: next, members: [], canStart: false };
  runtimeState.value = null;
}

async function startShooterRoomQuick(): Promise<void> {
  battle.gameplayId = 2;
  battle.ruleSetId = 0;
  battle.configVersion = 1;
  battle.protocolVersion = 1;
  battle.worldType = 'shooter_battle';
  battle.syncTemplateId = battle.syncTemplateId || 'state-sync-authority';
  await call(apis.rooms.ready({ sessionToken: sessionToken.value, roomId: roomId.value, ready: true }));
  await startBattle();
}

async function setRoomReady(ready: boolean): Promise<void> {
  if (!effectiveRoomId.value) return;
  await call(apis.rooms.ready({ sessionToken: sessionToken.value, roomId: effectiveRoomId.value, ready }));
  await refreshDashboard();
}

async function addRoomRobots(): Promise<void> {
  if (!effectiveRoomId.value) return;
  const data = await call<AddRoomRobotsResponse>(apis.rooms.addRobots({ sessionToken: sessionToken.value, roomId: effectiveRoomId.value, count: Number(roomRobots.count || 1), accountPrefix: roomRobots.accountPrefix || null, autoReady: roomRobots.autoReady === true, mountBattleAi: roomRobots.mountBattleAi === true, battleAiProfileId: roomRobots.battleAiProfileId || 'simple-battle' }));
  if (!data) return;
  lastRobotAdd.value = data;
  snapshot.value = data.snapshot;
  roomId.value = data.roomId || roomId.value;
  await refreshDashboard();
  await refreshSkillDiagnostics();
}

async function startBattle(): Promise<void> {
  const data = await call<AdminStartRoomBattleResponse>(apis.rooms.startBattle({ sessionToken: sessionToken.value, roomId: roomId.value, gameplayId: Number(battle.gameplayId || 0), ruleSetId: Number(battle.ruleSetId || 0), configVersion: Number(battle.configVersion || 1), protocolVersion: Number(battle.protocolVersion || 1), worldType: battle.worldType || null, clientId: 'admin-console', syncTemplateId: battle.syncTemplateId || null, syncModel: null, networkEnvironmentId: 'admin-console', carrierName: 'admin', enableAuthoritativeWorld: true, interpolationEnabled: true, inputDelayFrames: 0 }));
  if (data) {
    lastBattleStart.value = data;
    if (data.start?.battleId) skillEventFilter.battleId = data.start.battleId;
  }

  await refreshDashboard();
  await refreshSkillDiagnostics();
}

async function startShooterSandbox(): Promise<void> {
  await call(apis.sandbox.start({ sandboxId: sandbox.sandboxId || 'default', region: region.value, serverId: serverId.value, botCount: Number(sandbox.botCount || 3), maxPlayers: Number(sandbox.maxPlayers || 4), tickRate: Number(sandbox.tickRate || 30), title: 'Shooter 后台沙盒', tags: { source: 'admin-console' } }));
  await refreshDashboard();
}

async function refreshShooterSandboxState(): Promise<void> {
  const data = await call<ShooterSandboxState>(apis.sandbox.state(sandbox.sandboxId || 'default'));
  if (data) sandbox.state = data;
}

async function stopShooterSandbox(): Promise<void> {
  await call(apis.sandbox.stop({ sandboxId: sandbox.sandboxId || 'default' }));
  await refreshDashboard();
}

async function refreshAdminWorkspace(): Promise<void> {
  await refreshDashboard();
  await refreshClusterDiagnostics();
  await refreshSkillDiagnostics();
}

export function useAdminConsoleStore() {
  return {
    busy,
    sessionToken,
    region,
    serverId,
    selectedRoomType,
    roomId,
    gameplays,
    rooms,
    snapshot,
    runtimeState,
    dashboard,
    serverStatus,
    clusterDiagnostics,
    skillSummary,
    skillEvents,
    skillAnalysisModel,
    acceptanceBatch,
    acceptanceCase,
    acceptanceRunPlan,
    acceptanceArtifactDirectories,
    acceptanceTemplates,
    analysisArtifactDirectories,
    analysisArtifactList,
    analysisArtifact,
    acceptanceLastRun,
    shooterWorldDiagnostics,
    lastResponse,
    lastRobotAdd,
    lastBattleStart,
    apiCallLog,
    account,
    create,
    battle,
    skillLoadout,
    skillEventFilter,
    sandbox,
    roomRobots,
    serverOperation,
    acceptance,
    analysisArtifacts,
    acceptanceAnalysisFilter,
    runtimeAnalysisFilter,
    artifactAnalysisFilter,
    selectedAcceptanceAnalysisNodeKey,
    selectedRuntimeAnalysisNodeKey,
    selectedArtifactAnalysisNodeKey,
    selectedShooterInspectorRoomId,
    selectedShooterWorldEntityKey,
    selectedGameplay,
    selectedRoomSummary,
    effectiveRoomId,
    hasSession,
    hasCurrentRoom,
    canOperateCurrentRoom,
    accountLabel,
    sandboxLabel,
    serverModeLabel,
    clusterLabel,
    skillBattleId,
    battleId,
    acceptancePassedCount,
    acceptanceFailedCount,
    acceptanceUnknownCount,
    acceptanceFilteredCases,
    acceptanceSelectedCount,
    acceptanceAllFilteredSelected,
    acceptanceTracePreview,
    acceptanceTraceTree,
    acceptanceTraceFlat,
    acceptanceAssertionGroups,
    acceptanceAnalysisTree,
    acceptanceAnalysisFlat,
    acceptanceAnalysisFilterOptions,
    acceptanceAnalysisFilteredFlat,
    acceptanceAnalysisTimeline,
    acceptanceAnalysisEntityRelations,
    runtimeAnalysisFlat,
    runtimeAnalysisFilterOptions,
    runtimeAnalysisFilteredFlat,
    runtimeAnalysisTimeline,
    runtimeAnalysisEntityRelations,
    artifactAnalysisRecords,
    artifactAnalysisTree,
    artifactAnalysisFlat,
    artifactAnalysisFilterOptions,
    artifactAnalysisFilteredFlat,
    artifactAnalysisTimeline,
    artifactAnalysisEntityRelations,
    selectedAcceptanceAnalysisNode,
    selectedRuntimeAnalysisNode,
    selectedArtifactAnalysisNode,
    skillDiagnosticsWarnings,
    skillAnalysisModelNotes,
    apiFailureCount,
    shooterInspectorRooms,
    selectedShooterWorldEntity,
    selectAcceptanceAnalysisNode,
    selectRuntimeAnalysisNode,
    selectArtifactAnalysisNode,
    isAcceptanceCaseSelected,
    toggleAcceptanceCaseSelection,
    selectAllFilteredAcceptanceCases,
    clearAcceptanceCaseSelection,
    deleteAcceptanceCases,
    deleteAcceptanceCase,
    formatBytes,
    formatDuration,
    applyGameplayDefaults,
    refreshDashboard,
    refreshServerStatus,
    refreshClusterDiagnostics,
    refreshSkillSummary,
    refreshSkillEvents,
    refreshSkillDiagnostics,
    refreshSkillAnalysisModel,
    refreshAcceptanceBatch,
    refreshAcceptanceCase,
    refreshAcceptanceRunPlan,
    refreshAcceptanceArtifactDirectories,
    refreshAcceptanceTemplates,
    refreshAcceptanceArtifacts,
    refreshAnalysisArtifactDirectories,
    refreshAnalysisArtifacts,
    refreshAnalysisArtifact,
    refreshOfflineAnalysisArtifacts,
    selectAnalysisArtifactDirectory,
    selectAcceptanceArtifactDirectory,
    applyAcceptanceTemplate,
    runAcceptanceAnalysis,
    setMaintenanceMode,
    setDrainMode,
    requestRestart,
    guestLogin,
    accountLogin,
    validateSession,
    logout,
    submitMobaLoadout,
    createRoom,
    selectRoom,
    joinRoom,
    restoreCurrentRoom,
    leaveRoom,
    markOffline,
    refreshShooterWorldDiagnostics,
    selectShooterWorldEntity,
    closeRoom,
    setRoomReady,
    startShooterRoomQuick,
    addRoomRobots,
    startBattle,
    startShooterSandbox,
    refreshShooterSandboxState,
    stopShooterSandbox,
    refreshAdminWorkspace
  };
}
