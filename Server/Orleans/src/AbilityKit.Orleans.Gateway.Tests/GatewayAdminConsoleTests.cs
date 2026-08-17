using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using AbilityKit.Orleans.Contracts.Shooter;
using AbilityKit.Orleans.Gateway.HttpApi;
using Xunit;

namespace AbilityKit.Orleans.Gateway.Tests;

public sealed class GatewayAdminConsoleTests
{
    [Fact]
    public void Admin_console_should_be_independent_vite_vue_project()
    {
        var packageJson = File.ReadAllText(GetAdminConsoleProjectPath("package.json"));
        var viteConfig = File.ReadAllText(GetAdminConsoleProjectPath("vite.config.ts"));
        var main = File.ReadAllText(GetAdminConsoleProjectPath("src", "main.ts"));

        Assert.Contains("abilitykit-admin-console", packageJson);
        Assert.Contains("\"build\": \"vue-tsc --noEmit && vite build\"", packageJson);
        Assert.Contains("@vitejs/plugin-vue", packageJson);
        Assert.Contains("base: '/admin/'", viteConfig);
        Assert.Contains("outDir: '../Orleans/src/AbilityKit.Orleans.Gateway/wwwroot/admin'", viteConfig);
        Assert.Contains("createApp(App).mount('#app')", main);
    }

    [Fact]
    public void Admin_console_source_should_wrap_api_and_dashboard_calls()
    {
        var apiClient = File.ReadAllText(GetAdminConsoleProjectPath("src", "services", "adminApiClient.ts"));
        var domainApi = File.ReadAllText(GetAdminConsoleProjectPath("src", "services", "domainApi.ts"));
        var boundaries = File.ReadAllText(GetAdminConsoleProjectPath("src", "services", "adminApiBoundaries.ts"));
        var store = File.ReadAllText(GetAdminConsoleProjectPath("src", "stores", "adminConsoleStore.ts"));
        var app = File.ReadAllText(GetAdminConsoleProjectPath("src", "App.vue"));

        Assert.Contains("export class AdminApiClient", apiClient);
        Assert.Contains("VITE_ABILITYKIT_GATEWAY_URL", apiClient);
        Assert.Contains("export class AdminDomainApis", domainApi);
        Assert.Contains("export class AdminSkillApi", domainApi);
        Assert.Contains("export class RoomApi", domainApi);
        Assert.Contains("adminApiBoundaries", boundaries);
        Assert.Contains("/api/admin", boundaries);
        Assert.Contains("/api/rooms", boundaries);
        Assert.Contains("useAdminConsoleStore", store);
        Assert.Contains("/api/admin/dashboard", domainApi);
        Assert.Contains("refreshDashboard", store);
        Assert.Contains("adminStorage", store);
        Assert.Contains("AdminApiBoundaryPanel", app);
    }

    [Fact]
    public void Admin_console_source_should_expose_formal_layout_and_server_operations()
    {
        var app = File.ReadAllText(GetAdminConsoleProjectPath("src", "App.vue"));
        var sidebar = File.ReadAllText(GetAdminConsoleProjectPath("src", "components", "layout", "AdminSidebar.vue"));
        var topbar = File.ReadAllText(GetAdminConsoleProjectPath("src", "components", "layout", "AdminTopbar.vue"));
        var overviewMetrics = File.ReadAllText(GetAdminConsoleProjectPath("src", "components", "overview", "AdminOverviewMetrics.vue"));
        var opsPage = File.ReadAllText(GetAdminConsoleProjectPath("src", "pages", "AdminOpsPage.vue"));
        var clusterPage = File.ReadAllText(GetAdminConsoleProjectPath("src", "pages", "AdminClusterPage.vue"));
        var roomsPage = File.ReadAllText(GetAdminConsoleProjectPath("src", "pages", "AdminRoomsPage.vue"));
        var battlePage = File.ReadAllText(GetAdminConsoleProjectPath("src", "pages", "AdminBattlePage.vue"));
        var skillDiagnosticsPanel = File.ReadAllText(GetAdminConsoleProjectPath("src", "components", "skills", "SkillDiagnosticsPanel.vue"));
        var skillAcceptancePanel = File.ReadAllText(GetAdminConsoleProjectPath("src", "components", "skills", "SkillAcceptancePanel.vue"));
        var navigation = File.ReadAllText(GetAdminConsoleProjectPath("src", "navigation", "adminNavigation.ts"));
        var router = File.ReadAllText(GetAdminConsoleProjectPath("src", "router", "adminRouter.ts"));
        var store = File.ReadAllText(GetAdminConsoleProjectPath("src", "stores", "adminConsoleStore.ts"));
        var skillAcceptanceAnalysis = File.ReadAllText(GetAdminConsoleProjectPath("src", "services", "skillAcceptanceAnalysis.ts"));
        var domainApi = File.ReadAllText(GetAdminConsoleProjectPath("src", "services", "domainApi.ts"));
        var types = File.ReadAllText(GetAdminConsoleProjectPath("src", "types.ts"));
        var css = File.ReadAllText(GetAdminConsoleProjectPath("src", "styles.css"));

        Assert.Contains("class=\"admin-layout shell\"", app);
        Assert.Contains("class=\"sidebar\"", sidebar);
        Assert.Contains("class=\"workspace\"", app);
        Assert.Contains("class=\"topbar\"", topbar);
        Assert.Contains("id=\"overview\"", overviewMetrics);
        Assert.Contains("id=\"ops\"", opsPage);
        Assert.Contains("id=\"rooms\"", roomsPage);
        Assert.Contains("id=\"battle\"", battlePage);
        Assert.Contains("id=\"debug\"", app);
        Assert.Contains("服务器运维", opsPage);
        Assert.Contains("集群诊断", clusterPage);
        Assert.Contains("SkillDiagnosticsPanel", app);
        Assert.Contains("SkillAcceptancePanel", app);
        Assert.Contains("技能分析", skillDiagnosticsPanel);
        Assert.Contains("场景验收报告", skillAcceptancePanel);
        Assert.Contains("id=\"cluster\"", clusterPage);
        Assert.Contains("id=\"skills\"", skillDiagnosticsPanel);
        Assert.Contains("id=\"skill-acceptance\"", skillAcceptancePanel);
        Assert.Contains("cluster-panel", clusterPage);
        Assert.Contains("skill-panel", skillDiagnosticsPanel);
        Assert.Contains("acceptance-panel", skillAcceptancePanel);
        Assert.Contains("buildAcceptanceTraceTree", skillAcceptanceAnalysis);
        Assert.Contains("buildAcceptanceAssertionGroups", skillAcceptanceAnalysis);
        Assert.Contains("clusterLabel", store);
        Assert.Contains("serverModeLabel", store);
        Assert.Contains("refreshServerStatus", store);
        Assert.Contains("refreshClusterDiagnostics", store);
        Assert.Contains("refreshSkillSummary", store);
        Assert.Contains("refreshSkillEvents", store);
        Assert.Contains("submitMobaLoadout", store);
        Assert.Contains("setMaintenanceMode", store);
        Assert.Contains("setDrainMode", store);
        Assert.Contains("requestRestart", store);
        Assert.Contains("adminNavigationItems", navigation);
        Assert.Contains("resolveAdminRouteKey", navigation);
        Assert.Contains("useAdminRouter", router);
        Assert.Contains("/api/admin/server/status", domainApi);
        Assert.Contains("/api/admin/server/maintenance", domainApi);
        Assert.Contains("/api/admin/server/drain", domainApi);
        Assert.Contains("/api/admin/server/restart-request", domainApi);
        Assert.Contains("/api/admin/rooms/create", domainApi);
        Assert.Contains("/api/admin/rooms/pick-hero", domainApi);
        Assert.Contains("/api/admin/rooms/add-robots", domainApi);
        Assert.Contains("/api/admin/rooms/start-battle", domainApi);
        Assert.DoesNotContain("'/api/rooms/", domainApi);
        Assert.Contains("/api/admin/skills/summary", domainApi);
        Assert.Contains("/api/admin/skills/events", domainApi);
        Assert.Contains("export interface AdminServerStatus", types);
        Assert.Contains("export interface AdminClusterDiagnostics", types);
        Assert.Contains("export interface AdminClusterNodeProbe", types);
        Assert.Contains("export interface AdminSkillDiagnosticsSummary", types);
        Assert.Contains("export interface AdminSkillDiagnosticsEvents", types);
        Assert.Contains("export interface AdminSkillEvent", types);
        Assert.Contains("export interface AdminSkillAnalysisArtifact", types);
        Assert.Contains("export interface AdminServerOperationResponse", types);
        Assert.Contains("serverStatus?: AdminServerStatus | null", types);
        Assert.Contains(".admin-layout", css);
        Assert.Contains(".sidebar", css);
        Assert.Contains(".topbar", css);
        Assert.Contains(".featured-card", css);
        Assert.Contains(".ops-status", css);
        Assert.Contains(".cluster-panel", css);
        Assert.Contains(".diagnostic-grid", css);
        Assert.Contains(".probe-item", css);
        Assert.Contains(".skill-panel", css);
        Assert.Contains(".skill-layout", css);
        Assert.Contains(".event-timeline", css);
        Assert.Contains(".artifact-toolbar", css);
        Assert.Contains(".artifact-summary", css);
        Assert.Contains(".badge", css);
    }

    [Fact]
    public void Admin_console_source_should_default_to_shooter_operations()
    {
        var store = File.ReadAllText(GetAdminConsoleProjectPath("src", "stores", "adminConsoleStore.ts"));
        var domainApi = File.ReadAllText(GetAdminConsoleProjectPath("src", "services", "domainApi.ts"));

        Assert.Contains("selectedRoomType = ref('shooter')", store);
        Assert.Contains("roomType: 'shooter'", store);
        Assert.Contains("gameplayId: 2", store);
        Assert.Contains("worldType: 'shooter_battle'", store);
        Assert.Contains("syncTemplateId: 'state-sync-authority'", store);
        Assert.Contains("startShooterRoomQuick", store);
        Assert.Contains("roomRobots", store);
        Assert.Contains("addRoomRobots", store);
        Assert.Contains("/api/shooter-sandbox/start", domainApi);
    }

    [Fact]
    public void Gateway_gameplay_catalog_should_expose_server_supported_shooter_sync_templates()
    {
        var shooter = Assert.Single(GatewayGameplayCatalog.All, gameplay => gameplay.RoomType == "shooter");

        Assert.Equal(ShooterServerProtocol.StateSyncAuthorityTemplate, shooter.DefaultSyncTemplateId);
        Assert.Equal(ShooterServerProtocol.CreateStateSyncTemplateIds(), shooter.SupportedSyncTemplateIds);
        Assert.Contains(ShooterServerProtocol.BatchStateLowFrequencyTemplate, shooter.SupportedSyncTemplateIds);
        Assert.Contains(ShooterServerProtocol.MassBattleLodAoiTemplate, shooter.SupportedSyncTemplateIds);
        Assert.Contains(ShooterServerProtocol.HybridHeroPredictionTemplate, shooter.SupportedSyncTemplateIds);
    }

    [Fact]
    public void Admin_console_source_should_have_production_style_layout_css()
    {
        var css = File.ReadAllText(GetAdminConsoleProjectPath("src", "styles.css"));

        Assert.Contains(".shell", css);
        Assert.Contains(".metrics", css);
        Assert.Contains(".grid", css);
        Assert.Contains(".card", css);
        Assert.Contains("@media", css);
    }

    [Fact]
    public void Admin_console_build_output_should_be_served_by_gateway_wwwroot()
    {
        var index = File.ReadAllText(GetGatewaySourcePath("wwwroot", "admin", "index.html"));
        var assetsDirectory = GetGatewayDirectoryPath("wwwroot", "admin", "assets");

        Assert.Contains("AbilityKit Admin Console", index);
        Assert.True(Directory.Exists(assetsDirectory), "Expected Vite build assets under Gateway wwwroot/admin/assets.");
        Assert.True(Directory.EnumerateFiles(assetsDirectory, "*.js").Any(), "Expected at least one built JavaScript asset.");
        Assert.True(Directory.EnumerateFiles(assetsDirectory, "*.css").Any(), "Expected at least one built CSS asset.");
    }

    [Fact]
    public void Gateway_pipeline_should_expose_admin_console_redirect()
    {
        var source = File.ReadAllText(GetGatewaySourcePath("HttpApi", "GatewayModuleExtensions.cs"));

        Assert.Contains("/admin", source);
        Assert.Contains("/admin/index.html", source);
        Assert.Contains("Gateway.AdminConsole", source);
        Assert.Contains("/debug/index.html", source);
    }

    [Fact]
    public void Gateway_api_should_expose_admin_dashboard_endpoint()
    {
        var source = File.ReadAllText(GetGatewaySourcePath("HttpApi", "GatewayHttpApi.cs"));

        Assert.Contains("MapGatewayAdminEndpoints", source);
        Assert.Contains("/api/admin", source);
        Assert.Contains("/dashboard", source);
        Assert.Contains("BuildAdminDashboardAsync", source);
        Assert.Contains("AdminDashboardHttpResponse", source);
        Assert.Contains("GatewayGameplayCatalog.All", source);
        Assert.Contains("GatewayAdminOperations.GetStatus", source);
    }

    [Fact]
    public void Gateway_api_should_expose_admin_cluster_diagnostics()
    {
        var api = File.ReadAllText(GetGatewaySourcePath("HttpApi", "GatewayHttpApi.cs"));
        var models = File.ReadAllText(GetGatewaySourcePath("HttpApi", "GatewayHttpApiModels.cs"));
        var diagnostics = File.ReadAllText(GetGatewaySourcePath("HttpApi", "GatewayClusterDiagnostics.cs"));

        Assert.Contains("/cluster/diagnostics", api);
        Assert.Contains("Gateway.AdminClusterDiagnostics", api);
        Assert.Contains("GatewayClusterDiagnostics.GetDiagnostics", api);
        Assert.Contains("AbilityKitOrleansClusterOptions", api);
        Assert.Contains("AdminClusterDiagnosticsHttpResponse", models);
        Assert.Contains("AdminClusterNodeProbeHttpResponse", models);
        Assert.Contains("internal static class GatewayClusterDiagnostics", diagnostics);
        Assert.Contains("runtimeMetrics", diagnostics);
        Assert.Contains("gateway-client", diagnostics);
        Assert.Contains("local-silo", diagnostics);
    }

    [Fact]
    public void Gateway_api_should_expose_admin_skill_diagnostics()
    {
        var api = File.ReadAllText(GetGatewaySourcePath("HttpApi", "GatewayHttpApi.cs"));
        var models = File.ReadAllText(GetGatewaySourcePath("HttpApi", "GatewayHttpApiModels.cs"));
        var diagnostics = File.ReadAllText(GetGatewaySourcePath("HttpApi", "GatewaySkillDiagnostics.cs"));
        var modelProvider = File.ReadAllText(GetGatewaySourcePath("HttpApi", "GatewaySkillAnalysisModelProvider.cs"));
        var analysisArtifacts = File.ReadAllText(GetGatewaySourcePath("HttpApi", "GatewaySkillAnalysisArtifacts.cs"));
        var types = File.ReadAllText(GetAdminConsoleProjectPath("src", "types.ts"));
        var domainApi = File.ReadAllText(GetAdminConsoleProjectPath("src", "services", "domainApi.ts"));
        var projection = File.ReadAllText(GetAdminConsoleProjectPath("src", "services", "skillAnalysisProjection.ts"));
        var completeAnalysis = File.ReadAllText(FindWorkspacePath(new[] { "sample-web-output-analysis", "moba-complete-flow.analysis.json" }, File.Exists));
        var visualizer = File.ReadAllText(GetAdminConsoleProjectPath("src", "components", "skills", "SkillTraceVisualizer.vue"));
        var store = File.ReadAllText(GetAdminConsoleProjectPath("src", "stores", "adminConsoleStore.ts"));
        var panel = File.ReadAllText(GetAdminConsoleProjectPath("src", "components", "skills", "SkillDiagnosticsPanel.vue"));
 
        Assert.Contains("/skills/summary", api);
        Assert.Contains("/skills/events", api);
        Assert.Contains("/skills/analysis-model", api);
        Assert.Contains("/skills/analysis-artifacts/directories", api);
        Assert.Contains("/skills/analysis-artifacts", api);
        Assert.Contains("/skills/analysis-artifacts/{fileName}", api);
        Assert.Contains("Gateway.AdminSkillDiagnosticsSummary", api);
        Assert.Contains("Gateway.AdminSkillDiagnosticsEvents", api);
        Assert.Contains("Gateway.AdminSkillAnalysisModel", api);
        Assert.Contains("Gateway.AdminSkillAnalysisArtifactDirectories", api);
        Assert.Contains("Gateway.AdminSkillAnalysisArtifacts", api);
        Assert.Contains("Gateway.AdminSkillAnalysisArtifact", api);
        Assert.Contains("GatewaySkillDiagnostics.GetSummaryAsync", api);
        Assert.Contains("GatewaySkillDiagnostics.GetEventsAsync", api);
        Assert.Contains("GatewaySkillDiagnostics.GetAnalysisModel", api);
        Assert.Contains("GatewaySkillAnalysisArtifacts.ListArtifactDirectories", api);
        Assert.Contains("GatewaySkillAnalysisArtifacts.ListArtifacts", api);
        Assert.Contains("GatewaySkillAnalysisArtifacts.GetArtifact", api);
        Assert.Contains("AdminSkillDiagnosticsSummaryHttpResponse", models);
        Assert.Contains("AdminSkillDiagnosticsEventsHttpResponse", models);
        Assert.Contains("AdminSkillEventHttpResponse", models);
        Assert.Contains("AdminSkillAnalysisModelHttpResponse", models);
        Assert.Contains("AdminSkillAnalysisStageHttpResponse", models);
        Assert.Contains("AdminSkillAnalysisFieldHttpResponse", models);
        Assert.Contains("AdminSkillAnalysisProjectionSchemaHttpResponse", models);
        Assert.Contains("ProjectionSchemas", models);
        Assert.Contains("AdminSkillAnalysisArtifactDirectoryHttpResponse", models);
        Assert.Contains("AdminSkillAnalysisArtifactDirectoryListHttpResponse", models);
        Assert.Contains("AdminSkillAnalysisArtifactListItemHttpResponse", models);
        Assert.Contains("AdminSkillAnalysisArtifactListHttpResponse", models);
        Assert.Contains("AdminSkillAnalysisArtifactHttpResponse", models);
        Assert.Contains("internal static class GatewaySkillDiagnostics", diagnostics);
        Assert.Contains("RuntimeContextOnly", diagnostics);
        Assert.DoesNotContain("TraceNotConnected", diagnostics);
        Assert.DoesNotContain("Skill trace sink is not connected", diagnostics);
        Assert.Contains("GetCurrentFrameAsync", diagnostics);
        Assert.Contains("GatewaySkillAnalysisModelProvider.GetModel", diagnostics);
        Assert.Contains("internal static class GatewaySkillAnalysisModelProvider", modelProvider);
        Assert.Contains("ModelVersion = \"skill-analysis-v1\"", modelProvider);
        Assert.Contains("SkillAnalysisStageDefinition", modelProvider);
        Assert.Contains("SkillAnalysisFieldDefinition", modelProvider);
        Assert.Contains("SkillAnalysisProjectionSchemaDefinition", modelProvider);
        Assert.Contains("analysis-node-v1", modelProvider);
        Assert.Contains("analysis-filter-v1", modelProvider);
        Assert.Contains("analysis-entity-relation-v1", modelProvider);
        Assert.Contains("analysis-timeline-event-v1", modelProvider);
        Assert.Contains("SkillPipelineContext / SkillPipelineRunner", modelProvider);
        Assert.Contains("完整技能链路以 Scenario artifact trace 为主数据源", modelProvider);
        Assert.DoesNotContain("Future live traces", modelProvider);
        Assert.Contains("internal static class GatewaySkillAnalysisArtifacts", analysisArtifacts);
        Assert.Contains("DefaultArtifactDirectory = \"sample-web-output-analysis\"", analysisArtifacts);
        Assert.Contains("AnalysisSearchPattern = \"*.analysis.json\"", analysisArtifacts);
        Assert.Contains("ExpectedSchemaVersion = \"abilitykit-analysis.v1\"", analysisArtifacts);
        Assert.Contains("ResolveArtifactDirectory", analysisArtifacts);
        Assert.Contains("ValidateAnalysisFileName", analysisArtifacts);
        Assert.Contains("ReadJsonNode", analysisArtifacts);
        Assert.Contains("AdminSkillAnalysisArtifact", domainApi);
        Assert.Contains("/api/admin/skills/analysis-artifacts/directories", domainApi);
        Assert.Contains("/api/admin/skills/analysis-artifacts", domainApi);
        Assert.Contains("export interface SkillAnalysisFilterState", types);
        Assert.Contains("export interface SkillAnalysisEntityRelationProjection", types);
        Assert.Contains("export interface AdminSkillAnalysisArtifactDirectory", types);
        Assert.Contains("export interface AdminSkillAnalysisArtifactList", types);
        Assert.Contains("export interface AdminSkillAnalysisArtifact", types);
        Assert.Contains("createDefaultSkillAnalysisFilter", projection);
        Assert.Contains("filterSkillAnalysisNodes", projection);
        Assert.Contains("buildAnalysisArtifactTraceRecords", projection);
        Assert.Contains("normalizeAnalysisMetadataProperties", projection);
        Assert.Contains("metadata.configId", projection);
        Assert.Contains("metadata.SourceActorId", projection);
        Assert.Contains("originSource", projection);
        Assert.Contains("rawArtifactNode", projection);
        Assert.Contains("buildSkillAnalysisEntityRelations", projection);
        Assert.Contains("moba-complete-flow-demo", completeAnalysis);
        Assert.Contains("EffectAction", completeAnalysis);
        Assert.Contains("DamageApply", completeAnalysis);
        Assert.Contains("ProjectileHit", completeAnalysis);
        Assert.Contains("BuffTick", completeAnalysis);
        Assert.Contains("AreaStay", completeAnalysis);
        Assert.Contains("PresentationPlay", completeAnalysis);
        Assert.Contains("originSource", completeAnalysis);
        Assert.Contains("properties", completeAnalysis);
        Assert.Contains("inferSkillAnalysisEntityKind", projection);
        Assert.Contains("trace-filter-panel", visualizer);
        Assert.Contains("select-node", visualizer);
        Assert.Contains("selectTimelineEvent", visualizer);
        Assert.Contains("战斗实体关联", visualizer);
        Assert.Contains("sourceContext", visualizer);
        Assert.Contains("acceptanceAnalysisFilteredFlat", store);
        Assert.Contains("runtimeAnalysisEntityRelations", store);
        Assert.Contains("analysisArtifactDirectories", store);
        Assert.Contains("refreshOfflineAnalysisArtifacts", store);
        Assert.Contains("artifactAnalysisFilteredFlat", store);
        Assert.Contains("artifactAnalysisEntityRelations", store);
        Assert.Contains("离线技能分析文件", panel);
        Assert.Contains("artifact-toolbar", panel);
    }

    [Fact]
    public void Gateway_api_should_expose_admin_skill_acceptance_artifacts()
    {
        var api = File.ReadAllText(GetGatewaySourcePath("HttpApi", "GatewayHttpApi.cs"));
        var models = File.ReadAllText(GetGatewaySourcePath("HttpApi", "GatewayHttpApiModels.cs"));
        var artifacts = File.ReadAllText(GetGatewaySourcePath("HttpApi", "GatewaySkillAcceptanceArtifacts.cs"));
        var domainApi = File.ReadAllText(GetAdminConsoleProjectPath("src", "services", "domainApi.ts"));
        var store = File.ReadAllText(GetAdminConsoleProjectPath("src", "stores", "adminConsoleStore.ts"));
        var panel = File.ReadAllText(GetAdminConsoleProjectPath("src", "components", "skills", "SkillAcceptancePanel.vue"));

        Assert.Contains("/skills/acceptance/artifact-directories", api);
        Assert.Contains("/skills/acceptance/templates", api);
        Assert.Contains("/skills/acceptance/batch", api);
        Assert.Contains("/skills/acceptance/cases/{caseId}", api);
        Assert.Contains("/skills/acceptance/run", api);
        Assert.Contains("/skills/acceptance/delete", api);
        Assert.Contains("/skills/acceptance/run-plan", api);
        Assert.Contains("Gateway.AdminSkillAcceptanceArtifactDirectories", api);
        Assert.Contains("Gateway.AdminSkillAcceptanceTemplates", api);
        Assert.Contains("Gateway.AdminSkillAcceptanceBatch", api);
        Assert.Contains("Gateway.AdminSkillAcceptanceCase", api);
        Assert.Contains("Gateway.AdminSkillAcceptanceRun", api);
        Assert.Contains("Gateway.AdminSkillAcceptanceDelete", api);
        Assert.Contains("Gateway.AdminSkillAcceptanceRunPlan", api);
        Assert.Contains("GatewaySkillAcceptanceArtifacts.ListArtifactDirectories", api);
        Assert.Contains("GatewaySkillAcceptanceArtifacts.GetTemplates", api);
        Assert.Contains("GatewaySkillAcceptanceArtifacts.GetBatch", api);
        Assert.Contains("GatewaySkillAcceptanceArtifacts.GetCase", api);
        Assert.Contains("GatewaySkillAcceptanceArtifacts.Run", api);
        Assert.Contains("GatewaySkillAcceptanceArtifacts.Delete", api);
        Assert.Contains("GatewaySkillAcceptanceArtifacts.GetRunPlan", api);
        Assert.Contains("AdminApiErrorHttpResponse", models);
        Assert.Contains("AdminSkillAcceptanceBatchHttpResponse", models);
        Assert.Contains("AdminSkillAcceptanceCaseListItemHttpResponse", models);
        Assert.Contains("AdminSkillAcceptanceCaseHttpResponse", models);
        Assert.Contains("AdminSkillAcceptanceArtifactDirectoryHttpResponse", models);
        Assert.Contains("AdminSkillAcceptanceArtifactDirectoryListHttpResponse", models);
        Assert.Contains("AdminSkillAcceptanceRunRequest", models);
        Assert.Contains("AdminSkillAcceptanceRunResponse", models);
        Assert.Contains("AdminSkillAcceptanceDeleteRequest", models);
        Assert.Contains("AdminSkillAcceptanceDeleteResponse", models);
        Assert.Contains("AdminSkillAcceptanceTemplateHttpResponse", models);
        Assert.Contains("AdminSkillAcceptanceTemplateListHttpResponse", models);
        Assert.Contains("AdminSkillAcceptanceRunPlanHttpResponse", models);
        Assert.Contains("AdminSkillAcceptanceExecutionStrategyHttpResponse", models);
        Assert.Contains("AdminSkillAcceptanceAllowedScriptHttpResponse", models);
        Assert.Contains("DefaultArtifactDirectory = \"artifacts/moba-acceptance\"", artifacts);
        Assert.Contains("ArtifactRootDirectory = \"artifacts\"", artifacts);
        Assert.Contains("BatchSummaryFileName = \"batch_summary.json\"", artifacts);
        Assert.Contains("SummaryFileSuffix = \"_summary.json\"", artifacts);
        Assert.Contains("TraceFileSuffix = \"_trace.jsonl\"", artifacts);
        Assert.Contains("SummarySearchPattern = \"*_summary.json\"", artifacts);
        Assert.Contains("ListArtifactDirectories", artifacts);
        Assert.Contains("GetTemplates", artifacts);
        Assert.Contains("Run(AdminSkillAcceptanceRunRequest request)", artifacts);
        Assert.Contains("Delete(AdminSkillAcceptanceDeleteRequest request)", artifacts);
        Assert.Contains("DeleteArtifactFile", artifacts);
        Assert.Contains("ScenarioCatalog", artifacts);
        Assert.Contains("BuildScenarioTemplate", artifacts);
        Assert.Contains("ExecuteScenario", artifacts);
        Assert.Contains("ReadExecutionResult", artifacts);
        Assert.Contains("tools", artifacts);
        Assert.Contains("run_moba_skill_analysis.ps1", artifacts);
        Assert.Contains("unity-dsl-allow-list", artifacts);
        Assert.Contains("server-side allow-list", artifacts);
        Assert.Contains("execution-result.json", artifacts);
        Assert.Contains("WriteOrUpdateGeneratedBatch", artifacts);
        Assert.Contains("BuildAllowedScripts", artifacts);
        Assert.Contains("BuildExecutionStrategies", artifacts);
        Assert.Contains("ResolveArtifactDirectory", artifacts);
        Assert.Contains("ResolveArtifactRootDirectory", artifacts);
        Assert.Contains("ArtifactDirectoryResolution", artifacts);
        Assert.Contains("ArtifactDirectoryOutOfBounds", artifacts);
        Assert.Contains("BuildError", artifacts);
        Assert.Contains("ValidateCaseId", artifacts);
        Assert.Contains("SanitizeCaseId", artifacts);
        Assert.Contains("ReadTraceRecords", artifacts);
        Assert.Contains("ReadStringArray", artifacts);
        Assert.Contains("ReadNullableBool", artifacts);
        Assert.Contains("/api/admin/skills/acceptance/delete", domainApi);
        Assert.Contains("AdminSkillAcceptanceDeleteResponse", domainApi);
        Assert.Contains("selectedCaseIds", store);
        Assert.Contains("toggleAcceptanceCaseSelection", store);
        Assert.Contains("deleteAcceptanceCases", store);
        Assert.Contains("deleteAcceptanceCase", store);
        Assert.Contains("全选过滤结果", panel);
        Assert.Contains("删除选中", panel);
        Assert.Contains("刷新列表", panel);
    }

    [Fact]
    public void Gateway_skill_acceptance_artifacts_should_persist_and_reload_web_analysis_content()
    {
        var artifactDirectory = Path.Combine("artifacts", "moba-acceptance-web-analysis-persistence");
        var fullArtifactDirectory = Path.GetFullPath(artifactDirectory, GetWorkspaceRoot());
        if (Directory.Exists(fullArtifactDirectory))
        {
            Directory.Delete(fullArtifactDirectory, recursive: true);
        }

        Directory.CreateDirectory(fullArtifactDirectory);

        var caseId = "skill_web_analysis_persistence";
        var summaryPath = Path.Combine(fullArtifactDirectory, caseId + "_summary.json");
        var tracePath = Path.Combine(fullArtifactDirectory, caseId + "_trace.jsonl");
        var batchPath = Path.Combine(fullArtifactDirectory, "batch_summary.json");

        File.WriteAllText(batchPath, """
            {
              "total": 1,
              "passed": 1,
              "failed": 0,
              "allPassed": true
            }
            """);
        File.WriteAllText(summaryPath, $$"""
            {
              "caseId": "{{caseId}}",
              "description": "web analysis persistence smoke",
              "worldId": "web_analysis_world",
              "tickRate": 30,
              "accelerated": true,
              "summaryJsonPath": "{{NormalizeJsonPath(summaryPath)}}",
              "traceJsonlPath": "{{NormalizeJsonPath(tracePath)}}",
              "result": {
                "passed": true,
                "finalFrame": 12,
                "finalTimeMs": 400,
                "traceNodeCount": 3,
                "skillCastTraceFound": true,
                "effectExecutionTraceFound": true,
                "projectileLaunched": true
              }
            }
            """);
        File.WriteAllLines(tracePath, new[]
        {
            "{\"contextId\":101,\"parentContextId\":0,\"rootContextId\":101,\"kind\":\"SkillCast\",\"stage\":\"skill-cast\",\"actorId\":1,\"skillId\":10010101,\"frame\":1,\"severity\":\"info\",\"message\":\"cast accepted\"}",
            "{\"contextId\":102,\"parentContextId\":101,\"rootContextId\":101,\"kind\":\"EffectExecution\",\"stage\":\"effect-execution\",\"sourceActorId\":1,\"targetActorId\":2,\"configId\":2001,\"frame\":2,\"severity\":\"info\",\"message\":\"effect executed\"}",
            "{\"contextId\":103,\"parentContextId\":102,\"rootContextId\":101,\"ownerContextId\":102,\"kind\":\"ProjectileLaunch\",\"stage\":\"projectile\",\"sourceActorId\":1,\"targetActorId\":2,\"configId\":3001,\"frame\":3,\"severity\":\"info\",\"message\":\"projectile launched\"}"
        });

        var batch = GatewaySkillAcceptanceArtifacts.GetBatch(artifactDirectory);
        var caseResult = GatewaySkillAcceptanceArtifacts.GetCase(caseId, artifactDirectory, traceLimit: 500);
        var acceptanceCase = Assert.IsType<AdminSkillAcceptanceCaseHttpResponse>(GetMinimalApiResultValue(caseResult));

        Assert.True(batch.HasBatchSummary);
        Assert.Empty(batch.Warnings);
        var caseItem = Assert.Single(batch.Cases);
        Assert.Equal(caseId, caseItem.CaseId);
        Assert.Equal(3, caseItem.TraceNodeCount);
        Assert.True(caseItem.Passed);
        Assert.Equal(NormalizeJsonPath(summaryPath), caseItem.SummaryPath);
        Assert.Equal(NormalizeJsonPath(tracePath), caseItem.TracePath);

        Assert.Equal(caseId, acceptanceCase.CaseId);
        Assert.NotNull(acceptanceCase.Summary);
        Assert.Equal(3, acceptanceCase.TraceRecords.Length);
        Assert.Empty(acceptanceCase.Warnings);
        Assert.Equal("SkillCast", acceptanceCase.TraceRecords[0]?["kind"]?.GetValue<string>());
        Assert.Equal("EffectExecution", acceptanceCase.TraceRecords[1]?["kind"]?.GetValue<string>());
        Assert.Equal("ProjectileLaunch", acceptanceCase.TraceRecords[2]?["kind"]?.GetValue<string>());
        Assert.Equal(102, acceptanceCase.TraceRecords[2]?["ownerContextId"]?.GetValue<int>());
    }
 
    [Fact]
    public void Gateway_skill_analysis_artifacts_should_list_and_reload_analysis_json_content()
    {
        var artifactDirectory = Path.Combine("artifacts", "moba-analysis-web-persistence");
        var fullArtifactDirectory = Path.GetFullPath(artifactDirectory, GetWorkspaceRoot());
        if (Directory.Exists(fullArtifactDirectory))
        {
            Directory.Delete(fullArtifactDirectory, recursive: true);
        }

        Directory.CreateDirectory(fullArtifactDirectory);

        var fileName = "skill_web_analysis.analysis.json";
        var artifactPath = Path.Combine(fullArtifactDirectory, fileName);
        File.WriteAllText(artifactPath, """
            {
              "schemaVersion": "abilitykit-analysis.v1",
              "session": {
                "sessionId": "skill-web-analysis",
                "project": "AbilityKit.Demo.Moba",
                "scenario": "web analysis json persistence",
                "generatedAtUtc": "2026-07-01T00:00:00.0000000Z"
              },
              "time": {
                "startFrame": 1,
                "endFrame": 3
              },
              "trace": {
                "roots": [
                  {
                    "rootId": 101,
                    "nodes": [
                      { "contextId": 101, "parentId": 0, "rootId": 101, "kindName": "SkillCast", "endedFrame": 1, "isRoot": true, "isEnded": true, "metadata": { "properties": { "actorId": 1, "skillId": 1002, "message": "cast accepted" } } },
                      { "contextId": 102, "parentId": 101, "rootId": 101, "kindName": "EffectExecution", "endedFrame": 2, "isEnded": true, "metadata": { "properties": { "sourceActorId": 1, "targetActorId": 2, "configId": 2001 } } }
                    ]
                  }
                ]
              }
            }
            """);

        var list = GatewaySkillAnalysisArtifacts.ListArtifacts(artifactDirectory);
        var detailResult = GatewaySkillAnalysisArtifacts.GetArtifact(fileName, artifactDirectory);
        var detail = Assert.IsType<AdminSkillAnalysisArtifactHttpResponse>(GetMinimalApiResultValue(detailResult));

        Assert.Empty(list.Warnings);
        var item = Assert.Single(list.Artifacts);
        Assert.Equal(fileName, item.FileName);
        Assert.Equal("skill-web-analysis", item.SessionId);
        Assert.Equal("abilitykit-analysis.v1", item.SchemaVersion);
        Assert.Equal("AbilityKit.Demo.Moba", item.Project);
        Assert.Equal("web analysis json persistence", item.Scenario);
        Assert.Equal(1, item.RootCount);
        Assert.Equal(2, item.NodeCount);
        Assert.Equal(1, item.StartFrame);
        Assert.Equal(3, item.EndFrame);
        Assert.Equal(NormalizeJsonPath(artifactPath), item.Path);

        Assert.Equal(fileName, detail.FileName);
        Assert.Equal(NormalizeJsonPath(artifactPath), detail.Path);
        Assert.Empty(detail.Warnings);
        Assert.Equal("abilitykit-analysis.v1", detail.Artifact?["schemaVersion"]?.GetValue<string>());
        Assert.Equal("skill-web-analysis", detail.Artifact?["session"]?["sessionId"]?.GetValue<string>());
        Assert.Equal(2, (detail.Artifact?["trace"]?["roots"]?[0]?["nodes"] as JsonArray)?.Count);
    }
 
    [Fact]
    public void Gateway_api_should_expose_admin_room_facade_endpoints()
    {
        var api = File.ReadAllText(GetGatewaySourcePath("HttpApi", "GatewayHttpApi.cs"));
        var models = File.ReadAllText(GetGatewaySourcePath("HttpApi", "GatewayHttpApiModels.cs"));
        var domainApi = File.ReadAllText(GetAdminConsoleProjectPath("src", "services", "domainApi.ts"));
        var boundaries = File.ReadAllText(GetAdminConsoleProjectPath("src", "services", "adminApiBoundaries.ts"));
        var types = File.ReadAllText(GetAdminConsoleProjectPath("src", "types.ts"));
        var store = File.ReadAllText(GetAdminConsoleProjectPath("src", "stores", "adminConsoleStore.ts"));
        var battlePage = File.ReadAllText(GetAdminConsoleProjectPath("src", "pages", "AdminBattlePage.vue"));
        var roomGrain = File.ReadAllText(GetGrainsSourcePath("Rooms", "RoomGrain.cs"));
        var robotContract = File.ReadAllText(GetContractsSourcePath("Automation", "IRoomRobotManagerGrain.cs"));
        var robotGrain = File.ReadAllText(GetGrainsSourcePath("Automation", "RoomRobotManagerGrain.cs"));

        Assert.Contains("/rooms/create", api);
        Assert.Contains("/rooms/join", api);
        Assert.Contains("/rooms/restore-current", api);
        Assert.Contains("/rooms/leave", api);
        Assert.Contains("/rooms/close", api);
        Assert.Contains("/rooms/mark-offline", api);
        Assert.Contains("/rooms/ready", api);
        Assert.Contains("/rooms/pick-hero", api);
        Assert.Contains("/rooms/start-battle", api);
        Assert.Contains("/rooms/add-robots", api);
        Assert.Contains("Gateway.AdminCreateRoom", api);
        Assert.Contains("Gateway.AdminJoinRoom", api);
        Assert.Contains("Gateway.AdminRestoreCurrentRoom", api);
        Assert.Contains("Gateway.AdminPickRoomHero", api);
        Assert.Contains("Gateway.AdminAddRoomRobots", api);
        Assert.Contains("Gateway.AdminStartRoomBattle", api);
        Assert.Contains("/api/admin/rooms/create", domainApi);
        Assert.Contains("/api/admin/rooms/join", domainApi);
        Assert.Contains("/api/admin/rooms/restore-current", domainApi);
        Assert.Contains("/api/admin/rooms/mark-offline", domainApi);
        Assert.Contains("/api/admin/rooms/pick-hero", domainApi);
        Assert.Contains("/api/admin/rooms/start-battle", domainApi);
        Assert.Contains("/api/admin/rooms/add-robots", domainApi);
        Assert.Contains("WebStartRoomBattleResponse", models);
        Assert.Contains("MountRoomRobotBattleAiResponse? BattleAiMount", models);
        Assert.Contains("new WebStartRoomBattleResponse(response, mountResponse)", api);
        Assert.Contains("!gameplay.CanStart(gameplayState)", roomGrain);
        Assert.Contains("Room is not ready to start.", roomGrain);
        Assert.Contains("export interface AdminStartRoomBattleResponse", types);
        Assert.Contains("lastBattleStart", store);
        Assert.Contains("AdminStartRoomBattleResponse", store);
        Assert.Contains("battleAiMount", battlePage);
        Assert.Contains("AI 挂载", battlePage);
        Assert.Contains("后台页面默认只依赖这里", boundaries);
        Assert.Contains("后台不直接调用", boundaries);
        Assert.Contains("public interface IRoomRobotManagerGrain", robotContract);
        Assert.Contains("AddRobotsAsync", robotContract);
        Assert.Contains("MountBattleAiAsync", robotContract);
        Assert.Contains("public sealed class RoomRobotManagerGrain", robotGrain);
        Assert.Contains("JoinRoomMemberRequest(accountId, IsBot: true)", robotGrain);
        Assert.Contains("BattleBotAiMountRequest", robotGrain);
    }

    [Fact]
    public void Gateway_api_should_expose_admin_server_operations()
    {
        var api = File.ReadAllText(GetGatewaySourcePath("HttpApi", "GatewayHttpApi.cs"));
        var models = File.ReadAllText(GetGatewaySourcePath("HttpApi", "GatewayHttpApiModels.cs"));
        var operations = File.ReadAllText(GetGatewaySourcePath("HttpApi", "GatewayAdminOperations.cs"));

        Assert.Contains("/server/status", api);
        Assert.Contains("/server/maintenance", api);
        Assert.Contains("/server/drain", api);
        Assert.Contains("/server/restart-request", api);
        Assert.Contains("ExecuteAdminServerOperationAsync", api);
        Assert.Contains("Gateway.AdminServerStatus", api);
        Assert.Contains("Gateway.AdminSetMaintenanceMode", api);
        Assert.Contains("Gateway.AdminSetDrainMode", api);
        Assert.Contains("Gateway.AdminRequestServerRestart", api);
        Assert.Contains("AdminServerStatusHttpResponse", models);
        Assert.Contains("AdminServerOperationHttpRequest", models);
        Assert.Contains("AdminServerOperationHttpResponse", models);
        Assert.Contains("internal static class GatewayAdminOperations", operations);
        Assert.Contains("SetMaintenanceMode", operations);
        Assert.Contains("SetDrainMode", operations);
        Assert.Contains("RequestRestart", operations);
        Assert.Contains("maintenanceMode = true", operations);
        Assert.Contains("drainMode = true", operations);
    }

    [Fact]
    public void Orleans_tools_should_support_one_click_and_multi_profile_launch()
    {
        var launcher = File.ReadAllText(GetOrleansToolPath("start_abilitykit.ps1"));
        var startScript = File.ReadAllText(GetOrleansToolPath("start_orleans_dev.ps1"));
        var stopScript = File.ReadAllText(GetOrleansToolPath("stop_abilitykit.ps1"));
        var restartWrapper = File.ReadAllText(GetOrleansToolPath("restart_all.ps1"));
        var profiles = File.ReadAllText(GetOrleansToolPath("abilitykit_launch_profiles.json"));

        var siloScript = File.ReadAllText(GetOrleansToolPath("start_orleans_silo.ps1"));

        Assert.Contains("abilitykit_launch_profiles.json", launcher);
        Assert.Contains("ListProfiles", launcher);
        Assert.Contains("string[]]$Profile", launcher);
        Assert.Contains("GatewayPort", launcher);
        Assert.Contains("SiloPort", launcher);
        Assert.Contains("SiloGatewayPort", launcher);
        Assert.Contains("TcpPort", launcher);
        Assert.Contains("PrimarySiloPort", launcher);
        Assert.Contains("silos", launcher);
        Assert.Contains("start_orleans_dev.ps1", launcher);
        Assert.Contains("ClusterId", startScript);
        Assert.Contains("ServiceId", startScript);
        Assert.Contains("InstanceName", startScript);
        Assert.Contains("start_orleans_silo.ps1", startScript);
        Assert.Contains("PrimarySiloPort", startScript);
        Assert.Contains("--AbilityKit:Orleans:ClusterId", startScript);
        Assert.Contains("--AbilityKit:Gateway:Http:Port", startScript);
        Assert.Contains("--AbilityKit:Gateway:Tcp:Port", startScript);
        Assert.Contains("/health/ready", startScript);
        Assert.Contains("/health/live", startScript);
        Assert.Contains("Test-AbilityKitTcpPort", startScript);
        Assert.Contains("Waiting for Orleans client gateway endpoint", startScript);
        Assert.Contains("Waiting for Orleans Silo Gateway TCP endpoint", siloScript);
        Assert.Contains("SiloGatewayWaitSeconds", startScript);
        Assert.Contains("ForceStartGateway", startScript);
        Assert.Contains("dotnet run --project", startScript);
        Assert.Contains("--no-build", startScript);
        Assert.Contains("Runtime windows will use dotnet run --no-build", startScript);
        Assert.Contains("Gateway startup skipped", startScript);
        Assert.Contains("Orleans client gateway is not reachable", startScript);
        Assert.Contains("logs", startScript);
        Assert.Contains("stop_abilitykit.ps1", startScript);
        Assert.Contains("Stop-AbilityKitServices", stopScript);
        Assert.Contains("--AbilityKit:Orleans:PrimarySiloPort", siloScript);
        Assert.Contains("--AbilityKit:Deployment:SiloRole:Role", siloScript);
        Assert.Contains("--AbilityKit:Deployment:RuntimeProfile:Role", siloScript);
        Assert.Contains("[switch]$All", stopScript);
        Assert.Contains("silos", stopScript);
        Assert.Contains("AbilityKit.Orleans.Host.csproj", stopScript);
        Assert.Contains("AbilityKit.Orleans.Gateway.csproj", stopScript);
        Assert.Contains("start_abilitykit.ps1", restartWrapper);
        Assert.Contains("PrimarySiloPort", restartWrapper);
        Assert.Contains("dev-scaled", profiles);
        Assert.Contains("Session", profiles);
        Assert.Contains("Room", profiles);
        Assert.Contains("Battle", profiles);
        Assert.Contains("ops-a", profiles);
        Assert.Contains("ops-b", profiles);
    }

    private static string GetAdminConsoleProjectPath(params string[] segments)
    {
        return FindWorkspacePath(Prepend(new[] { "Server", "AdminConsole" }, segments), File.Exists);
    }

    private static string GetGatewaySourcePath(params string[] segments)
    {
        return FindWorkspacePath(Prepend(new[] { "Server", "Orleans", "src", "AbilityKit.Orleans.Gateway" }, segments), File.Exists);
    }

    private static string GetGatewayDirectoryPath(params string[] segments)
    {
        return FindWorkspacePath(Prepend(new[] { "Server", "Orleans", "src", "AbilityKit.Orleans.Gateway" }, segments), Directory.Exists);
    }

    private static string GetContractsSourcePath(params string[] segments)
    {
        return FindWorkspacePath(Prepend(new[] { "Server", "Orleans", "src", "AbilityKit.Orleans.Contracts" }, segments), File.Exists);
    }

    private static string GetGrainsSourcePath(params string[] segments)
    {
        return FindWorkspacePath(Prepend(new[] { "Server", "Orleans", "src", "AbilityKit.Orleans.Grains" }, segments), File.Exists);
    }

    private static string GetOrleansToolPath(params string[] segments)
    {
        return FindWorkspacePath(Prepend(new[] { "Server", "Orleans", "tools" }, segments), File.Exists);
    }

    private static object? GetMinimalApiResultValue(object result)
    {
        var valueProperty = result.GetType().GetProperty("Value");
        Assert.NotNull(valueProperty);
        return valueProperty.GetValue(result);
    }

    private static string NormalizeJsonPath(string path)
    {
        return Path.GetFullPath(path, GetWorkspaceRoot()).Replace('\\', '/');
    }

    private static string GetWorkspaceRoot()
    {
        return FindWorkspacePath(Array.Empty<string>(), directory =>
            File.Exists(Path.Combine(directory, "LICENSE"))
            && Directory.Exists(Path.Combine(directory, "Server"))
            && Directory.Exists(Path.Combine(directory, "Unity")));
    }

    private static string FindWorkspacePath(string[] segments, Func<string, bool> exists)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var parts = new string[1 + segments.Length];
            parts[0] = directory.FullName;
            Array.Copy(segments, 0, parts, 1, segments.Length);
            var candidate = Path.Combine(parts);
            if (exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate workspace path: {Path.Combine(segments)}.");
    }

    private static string[] Prepend(string[] prefix, string[] suffix)
    {
        var result = new string[prefix.Length + suffix.Length];
        Array.Copy(prefix, 0, result, 0, prefix.Length);
        Array.Copy(suffix, 0, result, prefix.Length, suffix.Length);
        return result;
    }
}
