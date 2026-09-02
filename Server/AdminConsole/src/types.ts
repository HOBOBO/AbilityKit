export interface ApiResult<T> {
  ok: boolean;
  status: number;
  statusText?: string;
  method?: string;
  url?: string;
  durationMs?: number;
  body: T | string | null;
}

export interface AdminApiCallLogItem {
  id: number;
  ok: boolean;
  status: number;
  statusText: string;
  method: string;
  url: string;
  durationMs: number;
  occurredAt: string;
  summary: string;
}

export interface GameplayDescriptor {
  roomType: string;
  gameplayId: number;
  displayName: string;
  defaultMaxPlayers: number;
  allowJoinInProgress: boolean;
  defaultWorldType: string;
  defaultTickRate: number;
  defaultSyncTemplateId: string;
  supportedSyncTemplateIds: string[];
  supportsStateSyncPush: boolean;
  supportsFrameSync: boolean;
}

export interface RoomSummary {
  roomId: string;
  roomType: string;
  title?: string | null;
  playerCount: number;
  maxPlayers: number;
  isPublic: boolean;
  tags?: Record<string, string> | null;
}

export interface RoomSnapshot {
  summary?: RoomSummary;
  members?: unknown[];
  canStart?: boolean;
}

export interface RestoreRoomResponse {
  hasActiveRoom: boolean;
  snapshot?: RoomSnapshot | null;
}

export interface RoomRuntimeState {
  roomId: string;
  roomType: string;
  battleId?: string | null;
  worldId: number;
  isClosed: boolean;
  isInBattle: boolean;
  members: string[];
  memberStates?: Record<string, unknown> | null;
  serverNowTicks: number;
  tags?: Record<string, string> | null;
}

export interface ShooterSandboxState {
  running: boolean;
  region: string;
  serverId: string;
  roomId: string;
  battleId: string;
  worldId: number;
  botCount: number;
  currentFrame: number;
  serverNowTicks: number;
  snapshot?: RoomSnapshot | null;
}

export interface RoomRobotBattleAiMount {
  accountId: string;
  playerId: number;
  accepted: boolean;
  status: string;
  message: string;
}

export interface AddRoomRobotsResponse {
  roomId: string;
  requestedCount: number;
  addedCount: number;
  robotAccounts: string[];
  battleAiMounts: RoomRobotBattleAiMount[];
  snapshot: RoomSnapshot;
  serverNowTicks: number;
}

export interface StartRoomBattleResponse {
  battleId: string;
  worldId: number;
  started: boolean;
  worldStartAnchor?: unknown | null;
  serverNowTicks: number;
}

export interface MountRoomRobotBattleAiResponse {
  roomId: string;
  battleId: string;
  worldId: number;
  battleAiMounts: RoomRobotBattleAiMount[];
  serverNowTicks: number;
}

export interface AdminStartRoomBattleResponse {
  start: StartRoomBattleResponse;
  battleAiMount?: MountRoomRobotBattleAiResponse | null;
}

export interface ShooterWorldDiagnostics {
  battleId: string;
  worldType: string;
  worldId: number;
  frame: number;
  stateHash: number;
  entityCount: number;
  entities: ShooterWorldEntityDiagnostics[];
  componentChunks: ShooterWorldComponentChunkDiagnostics[];
  warnings: string[];
  serverNowTicks: number;
}

export interface ShooterWorldEntityDiagnostics {
  key: string;
  entityId: number;
  entityKind: string;
  group: string;
  label: string;
  alive: boolean;
  components: ShooterWorldComponentDiagnostics[];
}

export interface ShooterWorldComponentDiagnostics {
  name: string;
  componentKind: string;
  fields: Record<string, string>;
}

export interface ShooterWorldComponentChunkDiagnostics {
  componentKind: string;
  entityKind: string;
  count: number;
}

export interface AdminServerStatus {
  environmentName: string;
  applicationName: string;
  machineName: string;
  processId: number;
  processName: string;
  startTimeUtcTicks: number;
  uptimeSeconds: number;
  workingSetBytes: number;
  gcTotalMemoryBytes: number;
  threadCount: number;
  maintenanceMode: boolean;
  drainMode: boolean;
  restartRequested: boolean;
  lastOperationId?: string | null;
  lastOperationReason?: string | null;
  lastOperationRequestedBy?: string | null;
  lastOperationRequestedAtTicks?: number | null;
  serverNowTicks: number;
}

export interface AdminClusterNodeProbe {
  nodeId: string;
  role: string;
  endpoint: string;
  status: string;
  diagnostics: string;
}

export interface AdminClusterDiagnostics {
  clusterId: string;
  serviceId: string;
  siloPort?: number | null;
  orleansGatewayPort?: number | null;
  clientConnected: boolean;
  clientStatus: string;
  nodes: AdminClusterNodeProbe[];
  runtimeMetrics: string[];
  warnings: string[];
  gatewayProcess: AdminServerStatus;
  serverNowTicks: number;
}

export interface AdminSkillActorSummary {
  accountId: string;
  actorId: number;
  basicAttackSkillId: number;
  skillIds: number[];
  diagnostics: string;
}

export interface AdminSkillMetric {
  name: string;
  value: number;
  unit: string;
  source: string;
}

export interface AdminSkillDiagnosticsSummary {
  roomId?: string | null;
  roomType?: string | null;
  battleId?: string | null;
  worldId: number;
  isInBattle: boolean;
  currentFrame: number;
  members: string[];
  diagnosticsStatus: string;
  actors: AdminSkillActorSummary[];
  metrics: AdminSkillMetric[];
  warnings: string[];
  serverNowTicks: number;
}

export interface AdminSkillEventFilter {
  battleId?: string | null;
  actorId?: number | null;
  skillId?: number | null;
  limit: number;
}

export interface AdminSkillEvent {
  frame: number;
  actorId: number;
  skillId: number;
  skillInstanceId: number;
  stage: string;
  eventType: string;
  targetActorId?: number | null;
  value?: number | null;
  message?: string | null;
  severity: string;
  battleId: string;
  worldId: string;
  sessionId: string;
  generation: number;
  sequence: number;
  monotonicTimestamp: number;
  nodeId: number;
  rootId: number;
  parentId: number;
  sourceContextId: number;
  rootContextId: number;
  ownerContextId: number;
}

export interface AdminSkillDiagnosticsEvents {
  diagnosticsStatus: string;
  filters: AdminSkillEventFilter;
  events: AdminSkillEvent[];
  warnings: string[];
  serverNowTicks: number;
  schemaVersion: string;
  isDataAvailable: boolean;
  unavailableReason?: string | null;
  storeRevision?: number;
  hasMore?: boolean;
  monotonicTimestampFrequency?: number;
  offset?: number;
  limit?: number;
  dataAvailability?: string | null;
}

export interface AdminSkillAnalysisStage {
  id: string;
  displayName: string;
  runtimeSource: string;
  acceptanceSource: string;
  fields: string[];
}

export interface AdminSkillAnalysisField {
  name: string;
  description: string;
  requiredForCorrelation: boolean;
}

export interface AdminSkillAnalysisProjectionSchema {
  id: string;
  displayName: string;
  description: string;
  fields: string[];
}

export interface AdminSkillAnalysisModel {
  modelVersion: string;
  sources: string[];
  stages: AdminSkillAnalysisStage[];
  correlationFields: AdminSkillAnalysisField[];
  projectionSchemas: AdminSkillAnalysisProjectionSchema[];
  notes: string[];
  serverNowTicks: number;
}

export interface AdminSkillAnalysisArtifactDirectory {
  artifactDirectory: string;
  displayName: string;
  exists: boolean;
  artifactCount: number;
  lastWriteUtcTicks: number;
}

export interface AdminSkillAnalysisArtifactDirectoryList {
  artifactRootDirectory: string;
  directories: AdminSkillAnalysisArtifactDirectory[];
  warnings: string[];
  serverNowTicks: number;
}

export interface AdminSkillAnalysisArtifactListItem {
  fileName: string;
  sessionId: string;
  schemaVersion: string;
  project?: string | null;
  scenario?: string | null;
  rootCount: number;
  nodeCount: number;
  startFrame: number;
  endFrame: number;
  generatedAtUtcTicks: number;
  fileSizeBytes: number;
  path: string;
}

export interface AdminSkillAnalysisArtifactList {
  artifactDirectory: string;
  artifacts: AdminSkillAnalysisArtifactListItem[];
  warnings: string[];
  serverNowTicks: number;
}

export interface AdminSkillAnalysisArtifact {
  artifactDirectory: string;
  fileName: string;
  path: string;
  artifact?: Record<string, unknown> | null;
  warnings: string[];
  serverNowTicks: number;
}

export interface SkillAnalysisNodeProjection {
  nodeId: string;
  numericNodeId: number;
  rootId: string;
  numericRootId: number;
  parentId: string;
  numericParentId: number;
  kind: string;
  stage: string;
  label: string;
  displayName: string;
  detailLabel: string;
  configLabel: string;
  actorLabel: string;
  sourceActorLabel: string;
  targetActorLabel: string;
  runtimeLabel: string;
  configId?: number | null;
  frame: number;
  timeMs: number;
  actorId?: number | null;
  skillId?: number | null;
  sourceActorId?: number | null;
  targetActorId?: number | null;
  rootContextId?: string | null;
  ownerContextId?: string | null;
  sourceContextId?: string | null;
  entityKind: string;
  entityKey: string;
  domain: string;
  domainLabel: string;
  laneLabel: string;
  triggerLabel: string;
  conditionLabel: string;
  actionLabel: string;
  status: string;
  severity: string;
  summary: string;
  source: string;
  failure?: string | null;
  raw: Record<string, unknown>;
  children: SkillAnalysisNodeProjection[];
}

export type SkillAnalysisFlatNodeProjection = SkillAnalysisNodeProjection & { depth: number };

export interface SkillAnalysisTimelineEventProjection {
  id: string;
  frame: number;
  timeMs: number;
  lane: string;
  label: string;
  nodeId?: string | null;
  severity: string;
  source: string;
}

export interface SkillAnalysisFilterState {
  searchText: string;
  severity: string;
  stage: string;
  kind: string;
  entityKind: string;
  actorId: string;
  skillId: string;
  configId: string;
  rootId: string;
  contextId: string;
  onlyFailures: boolean;
}

export interface SkillAnalysisFilterOptions {
  severities: string[];
  stages: string[];
  kinds: string[];
  entityKinds: string[];
  actorIds: number[];
  skillIds: number[];
  configIds: number[];
  rootIds: string[];
}

export interface SkillAnalysisEntityRelationProjection {
  id: string;
  ownerKey: string;
  entityKind: string;
  entityKey: string;
  label: string;
  nodeCount: number;
  failureCount: number;
  firstFrame: number;
  lastFrame: number;
  nodes: SkillAnalysisFlatNodeProjection[];
}

export interface SkillAnalysisEntityGroupProjection {
  id: string;
  ownerKey: string;
  label: string;
  actorId?: number | null;
  rootId?: string | null;
  totalNodes: number;
  failureCount: number;
  relations: SkillAnalysisEntityRelationProjection[];
}

export interface AdminSkillAcceptanceCaseListItem {
  caseId: string;
  description?: string | null;
  worldId?: string | null;
  tickRate: number;
  accelerated: boolean;
  passed?: boolean | null;
  finalFrame: number;
  finalTimeMs: number;
  traceNodeCount: number;
  summaryPath: string;
  tracePath: string;
  category?: string | null;
  tags: string[];
  generatedFrom?: string | null;
  lastReviewedAt?: string | null;
  missingTraceNodes?: string | null;
  unexpectedTraceNodes?: string | null;
  missingActions?: string | null;
  missingRelationships?: string | null;
}

export interface AdminSkillAcceptanceBatch {
  artifactDirectory: string;
  hasBatchSummary: boolean;
  batchSummary?: Record<string, unknown> | null;
  cases: AdminSkillAcceptanceCaseListItem[];
  warnings: string[];
  serverNowTicks: number;
}

export interface AdminSkillAcceptanceCase {
  caseId: string;
  artifactDirectory: string;
  summary?: Record<string, unknown> | null;
  traceRecords: Record<string, unknown>[];
  summaryPath: string;
  tracePath: string;
  warnings: string[];
  serverNowTicks: number;
}

export interface AdminSkillAcceptanceDeleteRequest {
  sessionToken?: string | null;
  artifactDirectory?: string | null;
  caseIds: string[];
  operatorReason?: string | null;
}

export interface AdminSkillAcceptanceDeleteResponse {
  success: boolean;
  artifactDirectory: string;
  deletedCaseIds: string[];
  deletedPaths: string[];
  missingCaseIds: string[];
  warnings: string[];
  batch: AdminSkillAcceptanceBatch;
  serverNowTicks: number;
}

export interface AdminSkillAcceptanceExecutionStrategy {
  id: string;
  displayName: string;
  boundary: string;
  status: string;
  description: string;
}

export interface AdminSkillAcceptanceAllowedScript {
  id: string;
  displayName: string;
  relativePath: string;
  shell: string;
  exists: boolean;
  arguments: string[];
  produces: string[];
}

export interface AdminSkillAcceptanceArtifactDirectory {
  artifactDirectory: string;
  displayName: string;
  exists: boolean;
  hasBatchSummary: boolean;
  caseCount: number;
  lastWriteUtcTicks: number;
}

export interface AdminSkillAcceptanceArtifactDirectoryList {
  artifactRootDirectory: string;
  directories: AdminSkillAcceptanceArtifactDirectory[];
  warnings: string[];
  serverNowTicks: number;
}

export interface AdminSkillAcceptanceRunRequest {
  sessionToken?: string | null;
  artifactDirectory?: string | null;
  caseId?: string | null;
  description?: string | null;
  actorId: number;
  targetActorId: number;
  skillId: number;
  effectId: number;
  projectileId: number;
  areaId: number;
  buffId: number;
  shieldId: number;
  baseDamage: number;
  mitigatedDamage: number;
  shieldAbsorb: number;
  hpDamage: number;
  tickRate: number;
  durationFrames: number;
  templateId?: string | null;
  operatorReason?: string | null;
}

export interface AdminSkillAcceptanceRunResponse {
  success: boolean;
  operationId: string;
  artifactDirectory: string;
  caseId: string;
  summaryPath: string;
  tracePath: string;
  batch: AdminSkillAcceptanceBatch;
  warnings: string[];
  serverNowTicks: number;
  scenarioId: string;
  executionStatus: string;
  exitCode: number;
  logPath: string;
  executionResultPath: string;
  startedAtUtc: string;
  endedAtUtc: string;
  durationMs: number;
}

export interface AdminSkillAcceptanceTemplate {
  id: string;
  displayName: string;
  description: string;
  covers: string[];
  defaults: AdminSkillAcceptanceRunRequest;
}

export interface AdminSkillAcceptanceTemplateList {
  templates: AdminSkillAcceptanceTemplate[];
  serverNowTicks: number;
}

export interface AdminSkillAcceptanceRunPlan {
  allowed: boolean;
  message: string;
  artifactDirectory: string;
  executionMode: string;
  canRequestFromAdmin: boolean;
  strategies: AdminSkillAcceptanceExecutionStrategy[];
  allowedScripts: AdminSkillAcceptanceAllowedScript[];
  requiredApprovals: string[];
  auditFields: string[];
  notes: string[];
  serverNowTicks: number;
}

export interface AdminDashboardResponse {
  gameplays: GameplayDescriptor[];
  rooms: RoomSummary[];
  accountId?: string | null;
  currentRoomId?: string | null;
  currentRoom?: RestoreRoomResponse | null;
  runtimeState?: RoomRuntimeState | null;
  shooterSandbox?: ShooterSandboxState | null;
  serverNowTicks: number;
  serverStatus?: AdminServerStatus | null;
}

export interface AdminServerOperationResponse {
  success: boolean;
  operation: string;
  operationId: string;
  requestedBy: string;
  reason?: string | null;
  status: AdminServerStatus;
}

export interface SessionResponse {
  accountId?: string | null;
  sessionToken?: string;
  expireAtTicks?: number;
  isValid?: boolean;
}

export interface CreateRoomResponse {
  roomId?: string;
  snapshot?: RoomSnapshot;
}
