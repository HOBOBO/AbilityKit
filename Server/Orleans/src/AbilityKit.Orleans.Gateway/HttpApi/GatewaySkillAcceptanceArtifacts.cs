namespace AbilityKit.Orleans.Gateway.HttpApi;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

internal static class GatewaySkillAcceptanceArtifacts
{
    public const string DefaultArtifactDirectory = "artifacts/moba-acceptance";
    private const string ArtifactRootDirectory = "artifacts";
    private const string BatchSummaryFileName = "batch_summary.json";
    private const string SummaryFileSuffix = "_summary.json";
    private const string TraceFileSuffix = "_trace.jsonl";
    private const string SummarySearchPattern = "*_summary.json";
    private const int DefaultTraceLimit = 500;
    private const int MaxTraceLimit = 5000;
    private static readonly string[] BuiltInArtifactDirectories =
    {
        DefaultArtifactDirectory,
        "artifacts/moba-acceptance-web-analysis-persistence",
        "artifacts/admin-combat-analysis-runs"
    };
 
    public static AdminSkillAcceptanceArtifactDirectoryListHttpResponse ListArtifactDirectories()
    {
        var root = ResolveArtifactRootDirectory();
        var warnings = new List<string>();
        if (!Directory.Exists(root.FullPath))
        {
            warnings.Add($"Artifact root does not exist: {root.DisplayPath}");
        }

        var directories = new Dictionary<string, AdminSkillAcceptanceArtifactDirectoryHttpResponse>(StringComparer.OrdinalIgnoreCase);
        foreach (var builtIn in BuiltInArtifactDirectories)
        {
            AddArtifactDirectory(directories, builtIn);
        }

        if (Directory.Exists(root.FullPath))
        {
            foreach (var directory in Directory.EnumerateDirectories(root.FullPath, "*", SearchOption.TopDirectoryOnly))
            {
                AddArtifactDirectory(directories, NormalizePath(directory));
            }
        }

        return new AdminSkillAcceptanceArtifactDirectoryListHttpResponse(
            root.DisplayPath,
            directories.Values.OrderByDescending(item => item.LastWriteUtcTicks).ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray(),
            warnings.ToArray(),
            DateTime.UtcNow.Ticks);
    }

    public static AdminSkillAcceptanceTemplateListHttpResponse GetTemplates()
    {
        return new AdminSkillAcceptanceTemplateListHttpResponse(
            ScenarioCatalog.Values
                .OrderBy(scenario => scenario.DisplayName, StringComparer.Ordinal)
                .Select(scenario => BuildScenarioTemplate(scenario))
                .ToArray(),
            DateTime.UtcNow.Ticks);
    }

    public static IResult Run(AdminSkillAcceptanceRunRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TemplateId) || !ScenarioCatalog.TryGetValue(request.TemplateId, out var scenario))
        {
            return Results.BadRequest(BuildError("UnknownScenario", "templateId must name a server-side allow-listed DSL scenario.", "templateId"));
        }

        var operationId = $"admin-dsl-{Guid.NewGuid():N}";
        var directory = ResolveArtifactDirectory(Path.Combine("artifacts/admin-combat-analysis-runs", operationId));
        if (!directory.IsAllowed)
        {
            return Results.BadRequest(BuildError("ArtifactDirectoryOutOfBounds", directory.ErrorMessage ?? "Artifact directory is outside the allowed artifact root.", "artifactDirectory"));
        }

        Directory.CreateDirectory(directory.FullPath);
        var warnings = new List<string>();
        var execution = ExecuteScenario(scenario, directory, warnings);
        var summaryPath = execution.SummaryPath;
        var tracePath = execution.TracePath;
        var caseId = ReadCaseId(summaryPath) ?? scenario.CaseId;
        WriteOrUpdateGeneratedBatch(directory.FullPath);
        var batch = GetBatch(directory.DisplayPath);
        var success = execution.ExitCode == 0 && execution.Status == "passed" && batch.Cases.Any(item => item.CaseId == caseId && item.Passed == true);
        if (!success && string.IsNullOrWhiteSpace(execution.Error)) warnings.Add("DSL execution did not produce a passing case. Inspect the Unity log and execution result for details.");
        if (!string.IsNullOrWhiteSpace(execution.Error)) warnings.Add(execution.Error);

        return Results.Ok(new AdminSkillAcceptanceRunResponse(
            success,
            operationId,
            directory.DisplayPath,
            caseId,
            summaryPath ?? string.Empty,
            tracePath ?? string.Empty,
            batch,
            warnings.ToArray(),
            DateTime.UtcNow.Ticks,
            scenario.Id,
            execution.Status,
            execution.ExitCode,
            execution.LogPath ?? string.Empty,
            execution.ResultPath ?? string.Empty,
            execution.StartedAtUtc ?? string.Empty,
            execution.EndedAtUtc ?? string.Empty,
            execution.DurationMs));
    }

    public static IResult Delete(AdminSkillAcceptanceDeleteRequest request)
    {
        var directory = ResolveArtifactDirectory(request.ArtifactDirectory);
        if (!directory.IsAllowed)
        {
            return Results.BadRequest(BuildError("ArtifactDirectoryOutOfBounds", directory.ErrorMessage ?? "Artifact directory is outside the allowed artifact root.", "artifactDirectory"));
        }

        if (!Directory.Exists(directory.FullPath))
        {
            return Results.BadRequest(BuildError("ArtifactDirectoryNotFound", $"Artifact directory does not exist: {directory.DisplayPath}", "artifactDirectory"));
        }

        var requestedCaseIds = (request.CaseIds ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (requestedCaseIds.Length == 0)
        {
            return Results.BadRequest(BuildError("NoCaseIds", "At least one caseId is required.", "caseIds"));
        }

        var deletedCaseIds = new List<string>();
        var deletedPaths = new List<string>();
        var missingCaseIds = new List<string>();
        var warnings = new List<string>();
        foreach (var requestedCaseId in requestedCaseIds)
        {
            var validation = ValidateCaseId(requestedCaseId);
            if (!validation.IsValid)
            {
                warnings.Add($"Skip invalid caseId {requestedCaseId}: {validation.ErrorMessage}");
                continue;
            }

            var safeCaseId = validation.CaseId;
            var summaryPath = Path.Combine(directory.FullPath, safeCaseId + SummaryFileSuffix);
            var tracePath = Path.Combine(directory.FullPath, safeCaseId + TraceFileSuffix);
            var deleted = false;
            DeleteArtifactFile(summaryPath, deletedPaths, warnings, ref deleted);
            DeleteArtifactFile(tracePath, deletedPaths, warnings, ref deleted);
            if (deleted)
            {
                deletedCaseIds.Add(safeCaseId);
            }
            else
            {
                missingCaseIds.Add(safeCaseId);
            }
        }

        WriteOrUpdateGeneratedBatch(directory.FullPath);
        var batch = GetBatch(directory.DisplayPath);
        return Results.Ok(new AdminSkillAcceptanceDeleteResponse(
            deletedCaseIds.Count > 0,
            directory.DisplayPath,
            deletedCaseIds.ToArray(),
            deletedPaths.ToArray(),
            missingCaseIds.ToArray(),
            warnings.Concat(batch.Warnings).ToArray(),
            batch,
            DateTime.UtcNow.Ticks));
    }
 
    public static AdminSkillAcceptanceBatchHttpResponse GetBatch(string? artifactDirectory)
    {
        var directory = ResolveArtifactDirectory(artifactDirectory);
        var warnings = new List<string>();
        if (!directory.IsAllowed)
        {
            warnings.Add(directory.ErrorMessage ?? "Artifact directory is outside the allowed artifact root.");
            return new AdminSkillAcceptanceBatchHttpResponse(
                directory.DisplayPath,
                false,
                null,
                Array.Empty<AdminSkillAcceptanceCaseListItemHttpResponse>(),
                warnings.ToArray(),
                DateTime.UtcNow.Ticks);
        }

        var batchPath = Path.Combine(directory.FullPath, BatchSummaryFileName);

        if (!Directory.Exists(directory.FullPath))
        {
            warnings.Add($"Artifact directory does not exist: {directory.DisplayPath}");
            return new AdminSkillAcceptanceBatchHttpResponse(
                directory.DisplayPath,
                false,
                null,
                Array.Empty<AdminSkillAcceptanceCaseListItemHttpResponse>(),
                warnings.ToArray(),
                DateTime.UtcNow.Ticks);
        }

        JsonNode? batch = null;
        if (File.Exists(batchPath))
        {
            batch = ReadJsonNode(batchPath, warnings);
        }
        else
        {
            warnings.Add($"Batch summary does not exist: {NormalizePath(batchPath)}");
        }

        var cases = Directory.EnumerateFiles(directory.FullPath, SummarySearchPattern, SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(Path.GetFileName(path), BatchSummaryFileName, StringComparison.OrdinalIgnoreCase))
            .Select(path => BuildCaseListItem(directory.FullPath, path, warnings))
            .Where(item => item is not null)
            .Cast<AdminSkillAcceptanceCaseListItemHttpResponse>()
            .OrderBy(item => item.CaseId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new AdminSkillAcceptanceBatchHttpResponse(
            directory.DisplayPath,
            batch is not null,
            batch,
            cases,
            warnings.ToArray(),
            DateTime.UtcNow.Ticks);
    }

    public static IResult GetCase(string caseId, string? artifactDirectory, int? traceLimit)
    {
        var caseIdValidation = ValidateCaseId(caseId);
        if (!caseIdValidation.IsValid)
        {
            return Results.BadRequest(BuildError("InvalidCaseId", caseIdValidation.ErrorMessage ?? "caseId is invalid.", "caseId"));
        }

        var directory = ResolveArtifactDirectory(artifactDirectory);
        if (!directory.IsAllowed)
        {
            return Results.BadRequest(BuildError("ArtifactDirectoryOutOfBounds", directory.ErrorMessage ?? "Artifact directory is outside the allowed artifact root.", "artifactDirectory"));
        }

        var safeCaseId = caseIdValidation.CaseId;
        var summaryPath = Path.Combine(directory.FullPath, safeCaseId + SummaryFileSuffix);
        var tracePath = Path.Combine(directory.FullPath, safeCaseId + TraceFileSuffix);
        var warnings = new List<string>();

        if (!File.Exists(summaryPath))
        {
            return Results.NotFound(new AdminSkillAcceptanceCaseHttpResponse(
                safeCaseId,
                directory.DisplayPath,
                null,
                Array.Empty<JsonNode>(),
                NormalizePath(summaryPath),
                NormalizePath(tracePath),
                warnings.Concat(new[] { $"Summary artifact does not exist: {NormalizePath(summaryPath)}" }).ToArray(),
                DateTime.UtcNow.Ticks));
        }

        var summary = ReadJsonNode(summaryPath, warnings);
        var records = ReadTraceRecords(tracePath, traceLimit ?? DefaultTraceLimit, warnings);

        return Results.Ok(new AdminSkillAcceptanceCaseHttpResponse(
            safeCaseId,
            directory.DisplayPath,
            summary,
            records,
            NormalizePath(summaryPath),
            NormalizePath(tracePath),
            warnings.ToArray(),
            DateTime.UtcNow.Ticks));
    }

    public static AdminSkillAcceptanceRunPlanHttpResponse GetRunPlan(string? artifactDirectory)
    {
        var directory = ResolveArtifactDirectory(artifactDirectory);
        var allowedScripts = BuildAllowedScripts(directory.FullPath);
        return new AdminSkillAcceptanceRunPlanHttpResponse(
            directory.IsAllowed,
            directory.IsAllowed
                ? "AdminConsole 可运行服务端白名单内的 Unity DSL 场景；浏览器不能提交命令、路径或 DSL 内容。"
                : directory.ErrorMessage ?? "Artifact directory is outside the allowed artifact root.",
            directory.DisplayPath,
            "unity-dsl-allow-list",
            directory.IsAllowed,
            BuildExecutionStrategies(),
            allowedScripts,
            new[]
            {
                "Authenticated admin session",
                "Explicit operator reason",
                "Environment allowExecution=true configuration",
                "Script id must exist in server-side allow-list"
            },
            new[]
            {
                "operationId",
                "requestedBy",
                "requestedAtUtcTicks",
                "artifactDirectory",
                "scriptId or ciWorkflowId",
                "scenarioFilter",
                "exitCode",
                "logPath",
                "producedArtifactDirectory"
            },
            new[]
            {
                "Artifact browsing and real Unity DSL execution are available now.",
                "The gateway invokes one fixed server-side script with a server-side scenario allow-list.",
                "Scenario JSON remains the stable contract shared by Web, Unity Editor, CLI and CI.",
                "The gateway must never execute arbitrary command text from the browser.",
                $"Artifact browsing is constrained to the workspace {ArtifactRootDirectory}/ root."
            },
            DateTime.UtcNow.Ticks);
    }

    private static AdminSkillAcceptanceTemplateHttpResponse BuildScenarioTemplate(SkillAcceptanceScenarioDefinition scenario)
    {
        return new AdminSkillAcceptanceTemplateHttpResponse(
            scenario.Id,
            scenario.DisplayName,
            scenario.Description,
            scenario.Covers,
            new AdminSkillAcceptanceRunRequest(
                null,
                "artifacts/admin-combat-analysis-runs",
                scenario.CaseId,
                scenario.Description,
                0,
                0,
                scenario.SkillId,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                30,
                0,
                scenario.Id,
                "运行白名单 DSL 场景"));
    }

    private static readonly IReadOnlyDictionary<string, SkillAcceptanceScenarioDefinition> ScenarioCatalog =
        new Dictionary<string, SkillAcceptanceScenarioDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["lianpo-skill1-dash"] = new("lianpo-skill1-dash", "廉颇一技能冲锋命中", "真实 DSL：冲锋、命中伤害与击飞动作。", "skill_10010101_scenario_dash_hit_damage_knockup", 10010101, new[] { "lianpo", "dash", "damage", "knockup" }),
            ["lianpo-skill2-area"] = new("lianpo-skill2-area", "廉颇二技能护盾区域", "真实 DSL：护盾、延迟区域、伤害和减速。", "skill_10010201_scenario_shield_area_damage_slow", 10010201, new[] { "lianpo", "shield", "area", "buff" }),
            ["lianpo-skill3-combo"] = new("lianpo-skill3-combo", "廉颇三技能三段连击", "真实 DSL：三段区域伤害与末段击飞。", "skill_10010301_scenario_three_stage_damage_knockup", 10010301, new[] { "lianpo", "area", "damage", "knockup" }),
            ["xiaoqiao-skill1-projectile"] = new("xiaoqiao-skill1-projectile", "小乔一技能投射物", "真实 DSL：投射物沿施法方向命中目标。", "skill_10020101_scenario_damage", 10020101, new[] { "xiaoqiao", "projectile", "damage" }),
            ["xiaoqiao-skill2-area"] = new("xiaoqiao-skill2-area", "小乔二技能目标点区域", "真实 DSL：延迟区域在目标点命中目标。", "skill_10020201_scenario_damage", 10020201, new[] { "xiaoqiao", "area", "damage" }),
            ["xiaoqiao-skill3-ultimate"] = new("xiaoqiao-skill3-ultimate", "小乔三技能持续伤害", "真实 DSL：持续效果、重复命中与减伤。", "skill_10020301_scenario_interval_damage", 10020301, new[] { "xiaoqiao", "buff", "damage", "interval" })
        };

    private static ScenarioExecutionResult ExecuteScenario(SkillAcceptanceScenarioDefinition scenario, ArtifactDirectoryResolution directory, List<string> warnings)
    {
        var workspace = ResolveWorkspaceRoot();
        var scriptPath = Path.Combine(workspace, "tools", "run_moba_skill_analysis.ps1");
        var resultPath = Path.Combine(directory.FullPath, "execution-result.json");
        if (!File.Exists(scriptPath)) return ScenarioExecutionResult.Failed("Fixed DSL runner script was not found.", resultPath);

        var outputRelativePath = Path.GetRelativePath(workspace, directory.FullPath);
        var startedAt = DateTime.UtcNow;
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                WorkingDirectory = workspace,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.ArgumentList.Add("-ScenarioId");
            startInfo.ArgumentList.Add(scenario.Id);
            startInfo.ArgumentList.Add("-OutputDirectory");
            startInfo.ArgumentList.Add(outputRelativePath);

            using var process = Process.Start(startInfo);
            if (process is null) return ScenarioExecutionResult.Failed("Failed to start fixed DSL runner process.", resultPath, startedAt);
            process.WaitForExit();
            var completed = ReadExecutionResult(resultPath, process.ExitCode, startedAt, DateTime.UtcNow);
            if (completed.ExitCode != 0) warnings.Add($"Unity DSL runner exited with code {completed.ExitCode}.");
            return completed;
        }
        catch (Exception exception)
        {
            return ScenarioExecutionResult.Failed(exception.Message, resultPath, startedAt);
        }
    }

    private static ScenarioExecutionResult ReadExecutionResult(string resultPath, int fallbackExitCode, DateTime startedAt, DateTime endedAt)
    {
        var warnings = new List<string>();
        var result = ReadJsonNode(resultPath, warnings);
        var status = ReadString(result, "status") ?? (fallbackExitCode == 0 ? "passed" : "failed");
        return new ScenarioExecutionResult(
            status,
            ReadInt(result, "exitCode") is var exitCode && exitCode != 0 ? exitCode : fallbackExitCode,
            ReadString(result, "summaryPath"),
            ReadString(result, "tracePath"),
            ReadString(result, "logPath"),
            NormalizePath(resultPath),
            ReadString(result, "startedAtUtc") ?? startedAt.ToString("O"),
            ReadString(result, "endedAtUtc") ?? endedAt.ToString("O"),
            ReadInt(result, "durationMs"),
            ReadString(result, "error"));
    }

    private static string? ReadCaseId(string? summaryPath)
    {
        if (string.IsNullOrWhiteSpace(summaryPath) || !File.Exists(summaryPath)) return null;
        return ReadString(ReadJsonNode(summaryPath, new List<string>()), "caseId");
    }

    private static AdminSkillAcceptanceExecutionStrategyHttpResponse[] BuildExecutionStrategies()
    {
        return new[]
        {
            new AdminSkillAcceptanceExecutionStrategyHttpResponse(
                "local-script",
                "本机 allow-listed 脚本包装",
                "AdminConsole may only request a predefined script id; raw command, arbitrary path and user supplied shell text stay forbidden.",
                "available",
                "A fixed server-side PowerShell wrapper invokes a Unity execute-method command for one allow-listed DSL scenario."),
            new AdminSkillAcceptanceExecutionStrategyHttpResponse(
                "ci-job",
                "CI Job 包装",
                "AdminConsole may enqueue a fixed CI workflow with scenario path and artifact target parameters; execution remains outside the gateway process.",
                "recommended",
                "Prefer CI for shared environments so logs, approvals, retention and artifact publishing are handled by the build system."),
            new AdminSkillAcceptanceExecutionStrategyHttpResponse(
                "manual-refresh",
                "手动生成后刷新",
                "Unity/unit-test/headless runner produces artifacts first; this API only reads the resulting JSON/JSONL files.",
                "available",
                "Existing Unity, unit-test and CI artifacts can still be read without running a new scenario.")
        };
    }

    private static AdminSkillAcceptanceAllowedScriptHttpResponse[] BuildAllowedScripts(string directory)
    {
        var scripts = new[]
        {
            new SkillAcceptanceAllowedScriptDefinition(
                "moba-acceptance-local",
                "MOBA DSL 本机验收",
                "tools/run_moba_skill_analysis.ps1",
                "powershell-fixed-wrapper",
                new[] { "-ScenarioId <server allow-list>", "-OutputDirectory <server generated under artifacts/>" },
                new[] { "execution-result.json", SummarySearchPattern, "*" + TraceFileSuffix }),
            new SkillAcceptanceAllowedScriptDefinition(
                "moba-acceptance-ci",
                "MOBA Scenario CI 验收",
                ".github/workflows/moba-acceptance.yml",
                "ci-workflow",
                new[] { "scenarioFilter", "artifactDirectory" },
                new[] { "moba-acceptance artifact bundle", "gateway-readable JSON/JSONL reports" })
        };

        return scripts.Select(script => new AdminSkillAcceptanceAllowedScriptHttpResponse(
            script.Id,
            script.DisplayName,
            NormalizePath(script.RelativePath),
            script.Shell,
            File.Exists(Path.GetFullPath(script.RelativePath, Directory.GetCurrentDirectory())),
            script.Arguments,
            script.Produces)).ToArray();
    }

    private static void AddArtifactDirectory(Dictionary<string, AdminSkillAcceptanceArtifactDirectoryHttpResponse> directories, string artifactDirectory)
    {
        var directory = ResolveArtifactDirectory(artifactDirectory);
        if (!directory.IsAllowed) return;

        var exists = Directory.Exists(directory.FullPath);
        var batchPath = Path.Combine(directory.FullPath, BatchSummaryFileName);
        var caseCount = exists ? Directory.EnumerateFiles(directory.FullPath, SummarySearchPattern, SearchOption.TopDirectoryOnly).Count(path => !string.Equals(Path.GetFileName(path), BatchSummaryFileName, StringComparison.OrdinalIgnoreCase)) : 0;
        var lastWrite = exists ? Directory.GetLastWriteTimeUtc(directory.FullPath).Ticks : 0;
        directories[directory.DisplayPath] = new AdminSkillAcceptanceArtifactDirectoryHttpResponse(
            directory.DisplayPath,
            Path.GetFileName(directory.DisplayPath.TrimEnd('/')),
            exists,
            File.Exists(batchPath),
            caseCount,
            lastWrite);
    }

    private static JsonObject BuildGeneratedSummary(AdminSkillAcceptanceRunRequest request, string caseId, string operationId, string summaryPath, string tracePath, DateTime now, JsonObject[] traceRecords)
    {
        var description = string.IsNullOrWhiteSpace(request.Description) ? "AdminConsole 参数化战斗分析导出" : request.Description!;
        return new JsonObject
        {
            ["caseId"] = caseId,
            ["description"] = description,
            ["worldId"] = "admin_combat_analysis_world",
            ["tickRate"] = NormalizePositive(request.TickRate, 30),
            ["accelerated"] = true,
            ["summaryJsonPath"] = NormalizePath(summaryPath),
            ["traceJsonlPath"] = NormalizePath(tracePath),
            ["adminRun"] = new JsonObject
            {
                ["operationId"] = operationId,
                ["templateId"] = request.TemplateId ?? string.Empty,
                ["operatorReason"] = request.OperatorReason ?? string.Empty,
                ["requestedAtUtcTicks"] = now.Ticks
            },
            ["scenario"] = new JsonObject
            {
                ["description"] = description,
                ["actorId"] = NormalizePositive(request.ActorId, 1),
                ["targetActorId"] = NormalizePositive(request.TargetActorId, 2),
                ["skillId"] = NormalizePositive(request.SkillId, 1002),
                ["effectId"] = NormalizePositive(request.EffectId, 2001),
                ["projectileId"] = Math.Max(0, request.ProjectileId),
                ["areaId"] = Math.Max(0, request.AreaId),
                ["buffId"] = Math.Max(0, request.BuffId),
                ["shieldId"] = Math.Max(0, request.ShieldId)
            },
            ["result"] = new JsonObject
            {
                ["passed"] = true,
                ["finalFrame"] = Math.Max(8, request.DurationFrames),
                ["finalTimeMs"] = Math.Max(8, request.DurationFrames) * 1000 / NormalizePositive(request.TickRate, 30),
                ["traceNodeCount"] = traceRecords.Length,
                ["skillCastTraceFound"] = true,
                ["effectExecutionTraceFound"] = true,
                ["damagePipelineTraceFound"] = true,
                ["projectileLaunched"] = request.ProjectileId > 0,
                ["areaCreated"] = request.AreaId > 0,
                ["buffApplied"] = request.BuffId > 0,
                ["shieldObserved"] = request.ShieldId > 0 || request.ShieldAbsorb > 0
            },
            ["traceDictionaryVersion"] = "moba-trace-dictionary/v1 source=admin-built-in-export",
            ["traceDictionary"] = BuildGeneratedTraceDictionary(traceRecords)
        };
    }

    private static JsonObject[] BuildGeneratedTraceRecords(AdminSkillAcceptanceRunRequest request, string caseId, string operationId)
    {
        var actorId = NormalizePositive(request.ActorId, 1);
        var targetActorId = NormalizePositive(request.TargetActorId, 2);
        var skillId = NormalizePositive(request.SkillId, 1002);
        var effectId = NormalizePositive(request.EffectId, 2001);
        var baseDamage = NormalizePositive(request.BaseDamage, 100);
        var mitigatedDamage = NormalizePositive(request.MitigatedDamage, Math.Max(1, baseDamage * 8 / 10));
        var shieldAbsorb = Math.Max(0, request.ShieldAbsorb);
        var hpDamage = Math.Max(0, request.HpDamage > 0 ? request.HpDamage : mitigatedDamage - shieldAbsorb);
        var durationFrames = Math.Max(8, request.DurationFrames);
        var records = new List<JsonObject>
        {
            TraceRecord(1001, 0, 1001, "SkillCast", "skill-cast", 1, "info", "主动技能进入 SkillPipelineRunner", actorId, targetActorId, skillId, null, null, operationId, caseId, configKind: "skill"),
            TraceRecord(1002, 1001, 1001, "TriggerCondition", "trigger-condition", 2, "info", "预算、距离、目标与释放条件通过", actorId, targetActorId, skillId, null, null, operationId, caseId, configKind: "skill"),
            TraceRecord(1003, 1001, 1001, "EffectExecution", "effect-execution", 3, "info", "MobaEffectExecutionService 执行效果计划", actorId, targetActorId, skillId, effectId, 1001, operationId, caseId, configKind: "effect")
        };

        var parent = 1003;
        if (request.ProjectileId > 0)
        {
            records.Add(TraceRecord(1004, parent, 1001, "ProjectileLaunch", "projectile", 4, "info", "MobaProjectileSyncSystem 创建并推进投射物", actorId, targetActorId, skillId, request.ProjectileId, parent, operationId, caseId, configKind: "projectile"));
            parent = 1004;
        }

        if (request.AreaId > 0)
        {
            records.Add(TraceRecord(1005, parent, 1001, "AreaLifecycle", "area", 5, "info", "MobaAreaSyncSystem 创建区域并完成命中查询", actorId, targetActorId, skillId, request.AreaId, parent, operationId, caseId, configKind: "area"));
            parent = 1005;
        }

        if (request.BuffId > 0)
        {
            records.Add(TraceRecord(1006, parent, 1001, "BuffLifecycle", "buff", 6, "info", "MobaBuffLifecycleReconcileSystem 应用 Buff 并建立持续行为", actorId, targetActorId, skillId, request.BuffId, parent, operationId, caseId, configKind: "buff"));
            records.Add(TraceRecord(1007, 1006, 1001, "ContinuousTick", "continuous", Math.Min(durationFrames, 12), "info", "MobaContinuousTickSystem 触发持续 Tick", actorId, targetActorId, skillId, request.BuffId, 1006, operationId, caseId, configKind: "buff"));
        }

        if (request.ShieldId > 0 || shieldAbsorb > 0)
        {
            records.Add(TraceRecord(1008, 1003, 1001, "ShieldLifecycle", "shield", 7, "info", "MobaShieldService 参与护盾吸收", actorId, targetActorId, skillId, request.ShieldId, 1003, operationId, caseId, configKind: "shield"));
        }

        records.Add(TraceRecord(1009, 1003, 1001, "DamagePipeline", "damage", Math.Max(8, durationFrames - 2), "info", "DamagePipelineService 完成基础、减免、护盾与生命扣减", actorId, targetActorId, skillId, effectId, 1003, operationId, caseId, baseDamage, mitigatedDamage, shieldAbsorb, hpDamage, "effect"));
        records.Add(TraceRecord(1010, 1009, 1001, "PresentationCue", "presentation", Math.Max(9, durationFrames - 1), "info", "表现层接收伤害、命中和生命周期提示", actorId, targetActorId, skillId, effectId, 1009, operationId, caseId, configKind: "presentation"));
        records.Add(TraceRecord(1011, 1009, 1001, "Assertion", "assertion", durationFrames, "info", "后台导出断言通过", actorId, targetActorId, skillId, effectId, 1009, operationId, caseId, configKind: "assertion"));
        return records.ToArray();
    }

    private static JsonObject TraceRecord(int contextId, int parentContextId, int rootContextId, string kind, string stage, int frame, string severity, string message, int actorId, int targetActorId, int skillId, int? configId, int? ownerContextId, string operationId, string caseId, int? baseDamage = null, int? mitigatedDamage = null, int? shieldAbsorb = null, int? hpDamage = null, string configKind = "config")
    {
        var effectiveConfigId = configId ?? skillId;
        var configLabel = BuildGeneratedConfigLabel(configKind, effectiveConfigId);
        var runtimeLabel = $"{kind} context #{contextId}";
        var node = new JsonObject
        {
            ["contextId"] = contextId,
            ["nodeId"] = contextId,
            ["parentContextId"] = parentContextId,
            ["parentId"] = parentContextId,
            ["rootContextId"] = rootContextId,
            ["rootId"] = rootContextId,
            ["kind"] = kind,
            ["stage"] = stage,
            ["actorId"] = actorId,
            ["sourceActorId"] = actorId,
            ["targetActorId"] = targetActorId,
            ["skillId"] = skillId,
            ["displayName"] = configLabel,
            ["configLabel"] = configLabel,
            ["runtimeLabel"] = runtimeLabel,
            ["actorLabel"] = $"来源角色 #{actorId}",
            ["sourceActorLabel"] = $"来源角色 #{actorId}",
            ["targetActorLabel"] = $"目标角色 #{targetActorId}",
            ["configSource"] = "admin-built-in-template",
            ["semanticVersion"] = "moba-trace-dictionary/v1 source=admin-built-in-export",
            ["frame"] = frame,
            ["timeMs"] = frame * 33,
            ["severity"] = severity,
            ["message"] = message,
            ["operationId"] = operationId,
            ["caseId"] = caseId
        };
        node["configId"] = effectiveConfigId;
        if (ownerContextId.HasValue) node["ownerContextId"] = ownerContextId.Value;
        if (baseDamage.HasValue) node["baseDamage"] = baseDamage.Value;
        if (mitigatedDamage.HasValue) node["mitigatedDamage"] = mitigatedDamage.Value;
        if (shieldAbsorb.HasValue) node["shieldAbsorb"] = shieldAbsorb.Value;
        if (hpDamage.HasValue) node["hpDamage"] = hpDamage.Value;
        if (hpDamage.HasValue) node["finalDamage"] = hpDamage.Value;
        return node;
    }

    private static JsonArray BuildGeneratedTraceDictionary(JsonObject[] traceRecords)
    {
        var entries = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var record in traceRecords)
        {
            AddGeneratedDictionaryEntry(entries, "trace-kind", ReadString(record, "kind") ?? string.Empty, ReadString(record, "kind") ?? string.Empty, "MobaExecutionKind");
            AddGeneratedDictionaryEntry(entries, "config", ReadInt(record, "configId").ToString(), ReadString(record, "configLabel") ?? string.Empty, ReadString(record, "configSource") ?? "admin-built-in-template");
            AddGeneratedDictionaryEntry(entries, "actor", ReadInt(record, "sourceActorId").ToString(), ReadString(record, "sourceActorLabel") ?? string.Empty, "admin-built-in-template");
            AddGeneratedDictionaryEntry(entries, "actor", ReadInt(record, "targetActorId").ToString(), ReadString(record, "targetActorLabel") ?? string.Empty, "admin-built-in-template");
        }

        var array = new JsonArray();
        foreach (var entry in entries.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value))
        {
            array.Add(entry);
        }

        return array;
    }

    private static void AddGeneratedDictionaryEntry(Dictionary<string, JsonObject> entries, string kind, string id, string label, string source)
    {
        if (string.IsNullOrWhiteSpace(id) || string.Equals(id, "0", StringComparison.Ordinal)) return;
        var key = kind + ":" + id;
        if (entries.ContainsKey(key)) return;
        entries[key] = new JsonObject
        {
            ["key"] = key,
            ["kind"] = kind,
            ["id"] = id,
            ["name"] = string.IsNullOrWhiteSpace(label) ? id : label,
            ["label"] = string.IsNullOrWhiteSpace(label) ? kind + " #" + id : label,
            ["source"] = source,
            ["sourceVersion"] = "moba-trace-dictionary/v1 source=admin-built-in-export"
        };
    }

    private static string BuildGeneratedConfigLabel(string configKind, int configId)
    {
        var kind = string.IsNullOrWhiteSpace(configKind) ? "config" : configKind;
        return configId > 0 ? kind + " #" + configId : kind;
    }

    private static void DeleteArtifactFile(string path, List<string> deletedPaths, List<string> warnings, ref bool deleted)
    {
        if (!File.Exists(path)) return;
        try
        {
            File.Delete(path);
            deleted = true;
            deletedPaths.Add(NormalizePath(path));
        }
        catch (Exception exception)
        {
            warnings.Add($"Failed to delete artifact {NormalizePath(path)}: {exception.Message}");
        }
    }

    private static void WriteOrUpdateGeneratedBatch(string directory)
    {
        var summaries = Directory.EnumerateFiles(directory, SummarySearchPattern, SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(Path.GetFileName(path), BatchSummaryFileName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var passed = summaries.Select(path => ReadJsonNode(path, new List<string>()))
            .Count(summary => ReadNullableBool(summary?["result"], "passed") == true);
        var batch = new JsonObject
        {
            ["total"] = summaries.Length,
            ["passed"] = passed,
            ["failed"] = summaries.Length - passed,
            ["allPassed"] = summaries.Length == passed,
            ["generatedBy"] = "AdminConsole built-in combat analysis export",
            ["updatedAtUtcTicks"] = DateTime.UtcNow.Ticks
        };
        File.WriteAllText(Path.Combine(directory, BatchSummaryFileName), batch.ToJsonString());
    }

    private static int NormalizePositive(int value, int fallback)
    {
        return value > 0 ? value : fallback;
    }

    private static AdminSkillAcceptanceCaseListItemHttpResponse? BuildCaseListItem(string directory, string summaryPath, List<string> warnings)
    {
        var summary = ReadJsonNode(summaryPath, warnings);
        if (summary is null) return null;

        var caseId = ReadString(summary, "caseId") ?? Path.GetFileName(summaryPath).Replace(SummaryFileSuffix, string.Empty, StringComparison.OrdinalIgnoreCase);
        var result = summary["result"];
        var tracePath = ReadString(summary, "traceJsonlPath");
        if (string.IsNullOrWhiteSpace(tracePath))
        {
            tracePath = Path.Combine(directory, caseId + TraceFileSuffix);
        }

        return new AdminSkillAcceptanceCaseListItemHttpResponse(
            caseId,
            ReadString(summary, "description") ?? ReadString(summary["scenario"], "description"),
            ReadString(summary, "worldId"),
            ReadInt(summary, "tickRate"),
            ReadBool(summary, "accelerated"),
            ReadNullableBool(result, "passed"),
            ReadInt(result, "finalFrame"),
            ReadInt(result, "finalTimeMs"),
            ReadInt(result, "traceNodeCount"),
            NormalizePath(summaryPath),
            NormalizePath(tracePath),
            ReadString(summary, "category") ?? ReadString(summary["scenario"], "category") ?? "contract",
            ReadStringArray(summary, "tags") ?? ReadStringArray(summary["scenario"], "tags") ?? Array.Empty<string>(),
            ReadString(summary, "generatedFrom"),
            ReadString(summary, "lastReviewedAt"),
            ReadString(summary["coverage"], "missingTraceNodes"),
            ReadString(summary["coverage"], "unexpectedTraceNodes"),
            ReadString(summary["coverage"], "missingActions"),
            ReadString(summary["coverage"], "missingRelationships"));
    }

    private static JsonNode? ReadJsonNode(string path, List<string> warnings)
    {
        try
        {
            return JsonNode.Parse(File.ReadAllText(path));
        }
        catch (Exception exception)
        {
            warnings.Add($"Failed to read json artifact {NormalizePath(path)}: {exception.Message}");
            return null;
        }
    }

    private static JsonNode[] ReadTraceRecords(string tracePath, int limit, List<string> warnings)
    {
        if (!File.Exists(tracePath))
        {
            warnings.Add($"Trace artifact does not exist: {NormalizePath(tracePath)}");
            return Array.Empty<JsonNode>();
        }

        var effectiveLimit = Math.Clamp(limit <= 0 ? DefaultTraceLimit : limit, 1, MaxTraceLimit);
        var records = new List<JsonNode>(Math.Min(effectiveLimit, 256));
        var lineNumber = 0;
        foreach (var line in File.ReadLines(tracePath))
        {
            lineNumber++;
            if (records.Count >= effectiveLimit) break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var node = JsonNode.Parse(line);
                if (node is not null) records.Add(node);
            }
            catch (Exception exception)
            {
                warnings.Add($"Failed to parse trace line {lineNumber}: {exception.Message}");
            }
        }

        return records.ToArray();
    }

    private static ArtifactDirectoryResolution ResolveArtifactRootDirectory()
    {
        var baseDirectory = ResolveWorkspaceRoot();
        var fullPath = Path.GetFullPath(ArtifactRootDirectory, baseDirectory);
        return new ArtifactDirectoryResolution(fullPath, NormalizePath(fullPath), true, null);
    }

    private static ArtifactDirectoryResolution ResolveArtifactDirectory(string? artifactDirectory)
    {
        var baseDirectory = ResolveWorkspaceRoot();
        var requested = string.IsNullOrWhiteSpace(artifactDirectory) ? DefaultArtifactDirectory : artifactDirectory.Trim();
        var fullPath = Path.IsPathRooted(requested)
            ? Path.GetFullPath(requested)
            : Path.GetFullPath(requested, baseDirectory);
        var artifactRoot = Path.GetFullPath(ArtifactRootDirectory, baseDirectory);
        var isAllowed = IsPathUnderDirectory(fullPath, artifactRoot);
        var displayPath = NormalizePath(fullPath);
        var error = isAllowed
            ? null
            : $"Artifact directory must stay under {NormalizePath(artifactRoot)}. Requested: {displayPath}";
        return new ArtifactDirectoryResolution(fullPath, displayPath, isAllowed, error);
    }

    private static string ResolveWorkspaceRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LICENSE"))
                && Directory.Exists(Path.Combine(directory.FullName, "Server"))
                && Directory.Exists(Path.Combine(directory.FullName, "Unity")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static SkillAcceptanceCaseIdValidation ValidateCaseId(string caseId)
    {
        if (string.IsNullOrWhiteSpace(caseId))
        {
            return new SkillAcceptanceCaseIdValidation(string.Empty, false, "caseId is required.");
        }

        var trimmed = caseId.Trim();
        var safeCaseId = SanitizeCaseId(trimmed);
        if (string.IsNullOrWhiteSpace(safeCaseId) || !string.Equals(trimmed, safeCaseId, StringComparison.Ordinal))
        {
            return new SkillAcceptanceCaseIdValidation(safeCaseId, false, "caseId contains unsupported characters.");
        }

        return new SkillAcceptanceCaseIdValidation(safeCaseId, true, null);
    }

    private static string SanitizeCaseId(string caseId)
    {
        var trimmed = caseId.Trim();
        var chars = trimmed.Where(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch == '.').ToArray();
        return new string(chars);
    }

    private static bool IsPathUnderDirectory(string path, string root)
    {
        var normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static AdminApiErrorHttpResponse BuildError(string code, string message, string target)
    {
        return new AdminApiErrorHttpResponse(code, message, target, DateTime.UtcNow.Ticks);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static string[]? ReadStringArray(JsonNode? node, string name)
    {
        if (node?[name] is not JsonArray array) return null;
        var result = new List<string>(array.Count);
        foreach (var item in array)
        {
            var text = item?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(text)) result.Add(text);
        }

        return result.ToArray();
    }

    private static string? ReadString(JsonNode? node, string name)
    {
        return node?[name]?.GetValue<string>();
    }

    private sealed record SkillAcceptanceScenarioDefinition(string Id, string DisplayName, string Description, string CaseId, int SkillId, string[] Covers);

    private sealed record ScenarioExecutionResult(
        string Status,
        int ExitCode,
        string? SummaryPath,
        string? TracePath,
        string? LogPath,
        string? ResultPath,
        string? StartedAtUtc,
        string? EndedAtUtc,
        int DurationMs,
        string? Error)
    {
        public static ScenarioExecutionResult Failed(string error, string resultPath, DateTime? startedAt = null)
        {
            return new ScenarioExecutionResult("failed", -1, null, null, null, NormalizePath(resultPath), startedAt?.ToString("O"), DateTime.UtcNow.ToString("O"), 0, error);
        }
    }

    private static int ReadInt(JsonNode? node, string name)
    {
        return node?[name]?.GetValue<int>() ?? 0;
    }

    private static bool ReadBool(JsonNode? node, string name)
    {
        return node?[name]?.GetValue<bool>() ?? false;
    }

    private static bool? ReadNullableBool(JsonNode? node, string name)
    {
        return node?[name]?.GetValue<bool>();
    }

    private sealed record ArtifactDirectoryResolution(string FullPath, string DisplayPath, bool IsAllowed, string? ErrorMessage);

    private sealed record SkillAcceptanceCaseIdValidation(string CaseId, bool IsValid, string? ErrorMessage);

    private sealed record SkillAcceptanceAllowedScriptDefinition(string Id, string DisplayName, string RelativePath, string Shell, string[] Arguments, string[] Produces);
}
