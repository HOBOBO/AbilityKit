namespace AbilityKit.Orleans.Gateway.HttpApi;

using System;
using Orleans;
using AbilityKit.Orleans.Contracts.Accounts;
using AbilityKit.Orleans.Contracts.Automation;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Contracts.Shooter;
using AbilityKit.Orleans.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

internal static class GatewayHttpApi
{
    public static void MapGatewayHttpApi(this WebApplication app)
    {
        app.MapGatewaySessionEndpoints();
        app.MapGatewayGameplayEndpoints();
        app.MapGatewaySandboxEndpoints();
        app.MapGatewayAdminEndpoints();
        app.MapGatewayRoomEndpoints();
    }

    private static void MapGatewaySessionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/session")
            .WithTags("Session");

        group.MapPost("/guest/login", async (IClusterClient client) =>
        {
            var session = client.GetGrain<ISessionGrain>("global");
            var resp = await session.CreateGuestAsync();
            return Results.Ok(resp);
        })
        .WithName("Gateway.CreateGuestSession")
        .Produces(StatusCodes.Status200OK);

        group.MapPost("/accounts/login", async (AccountLoginRequest wire, IClusterClient client) =>
        {
            if (string.IsNullOrWhiteSpace(wire.AccountId))
            {
                return Results.BadRequest("AccountId is required");
            }

            var session = client.GetGrain<ISessionGrain>("global");
            try
            {
                var resp = await session.CreateSessionForAccountAsync(new CreateSessionForAccountRequest(
                    wire.AccountId,
                    wire.ExpireSeconds,
                    wire.KickExisting));
                return Results.Ok(resp);
            }
            catch (Exception exception)
            {
                return RoomHttpErrorMapper.ToResult(exception);
            }
        })
        .WithName("Gateway.LoginAccount")
        .Accepts<AccountLoginRequest>("application/json")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status500InternalServerError);

        group.MapPost("/validate", async (SessionTokenRequest wire, IClusterClient client) =>
        {
            if (string.IsNullOrWhiteSpace(wire.SessionToken))
            {
                return Results.BadRequest("SessionToken is required");
            }

            var session = client.GetGrain<ISessionGrain>("global");
            var resp = await session.ValidateAsync(new ValidateSessionRequest(wire.SessionToken));
            return Results.Ok(resp);
        })
        .WithName("Gateway.ValidateSession")
        .Accepts<SessionTokenRequest>("application/json")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/renew", async (RenewSessionRequest wire, IClusterClient client) =>
        {
            if (string.IsNullOrWhiteSpace(wire.SessionToken))
            {
                return Results.BadRequest("SessionToken is required");
            }

            var session = client.GetGrain<ISessionGrain>("global");
            var resp = await session.RenewAsync(wire);
            return Results.Ok(resp);
        })
        .WithName("Gateway.RenewSession")
        .Accepts<RenewSessionRequest>("application/json")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/logout", async (SessionTokenRequest wire, IClusterClient client) =>
        {
            if (string.IsNullOrWhiteSpace(wire.SessionToken))
            {
                return Results.BadRequest("SessionToken is required");
            }

            var accountId = await ValidateAccountAsync(client, wire.SessionToken);
            if (!string.IsNullOrWhiteSpace(accountId))
            {
                await MarkCurrentRoomOfflineAsync(client, accountId);
            }

            var session = client.GetGrain<ISessionGrain>("global");
            var resp = await session.LogoutAsync(new LogoutRequest(wire.SessionToken));
            return Results.Ok(resp);
        })
        .WithName("Gateway.LogoutSession")
        .Accepts<SessionTokenRequest>("application/json")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        app.MapPost("/api/guest/login", async (IClusterClient client) =>
        {
            var session = client.GetGrain<ISessionGrain>("global");
            var resp = await session.CreateGuestAsync();
            return Results.Ok(resp);
        })
        .WithName("Gateway.CreateGuestSessionCompat")
        .Produces(StatusCodes.Status200OK);

        app.MapPost("/api/accounts/login", async (AccountLoginRequest wire, IClusterClient client) =>
        {
            if (string.IsNullOrWhiteSpace(wire.AccountId))
            {
                return Results.BadRequest("AccountId is required");
            }

            var session = client.GetGrain<ISessionGrain>("global");
            try
            {
                var resp = await session.CreateSessionForAccountAsync(new CreateSessionForAccountRequest(
                    wire.AccountId,
                    wire.ExpireSeconds,
                    wire.KickExisting));
                return Results.Ok(resp);
            }
            catch (Exception exception)
            {
                return RoomHttpErrorMapper.ToResult(exception);
            }
        })
        .WithName("Gateway.LoginAccountCompat")
        .Accepts<AccountLoginRequest>("application/json")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status500InternalServerError);
    }

    private static void MapGatewayGameplayEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api")
            .WithTags("Gameplay");

        group.MapGet("/gameplays", ListGameplays)
            .WithName("Gateway.ListGameplays")
            .Produces(StatusCodes.Status200OK);
    }

    private static void MapGatewayAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin")
            .WithTags("Admin");

        group.MapPost("/dashboard", BuildAdminDashboardAsync)
            .WithName("Gateway.AdminDashboard")
            .Accepts<AdminDashboardHttpRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);

        group.MapPost("/rooms/create", CreateAdminRoomAsync)
            .WithName("Gateway.AdminCreateRoom")
            .Accepts<WebCreateRoomRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);

        group.MapPost("/rooms/join", JoinAdminRoomAsync)
            .WithName("Gateway.AdminJoinRoom")
            .Accepts<WebRoomRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);

        group.MapPost("/rooms/restore-current", RestoreCurrentAdminRoomAsync)
            .WithName("Gateway.AdminRestoreCurrentRoom")
            .Accepts<SessionTokenRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError);

        group.MapPost("/rooms/leave", LeaveAdminRoomAsync)
            .WithName("Gateway.AdminLeaveRoom")
            .Accepts<WebRoomRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);

        group.MapPost("/rooms/close", CloseAdminRoomAsync)
            .WithName("Gateway.AdminCloseRoom")
            .Accepts<WebRoomRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);

        group.MapPost("/rooms/mark-offline", MarkAdminRoomMemberOfflineAsync)
            .WithName("Gateway.AdminMarkRoomMemberOffline")
            .Accepts<WebRoomRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);

        group.MapPost("/rooms/ready", SetAdminRoomReadyAsync)
            .WithName("Gateway.AdminSetRoomReady")
            .Accepts<WebRoomReadyRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);

        group.MapPost("/rooms/pick-hero", PickAdminRoomHeroAsync)
            .WithName("Gateway.AdminPickRoomHero")
            .Accepts<WebRoomPickHeroRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);

        group.MapPost("/rooms/add-robots", AddAdminRoomRobotsAsync)
            .WithName("Gateway.AdminAddRoomRobots")
            .Accepts<WebAddRoomRobotsRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);

        group.MapPost("/rooms/start-battle", StartAdminRoomBattleAsync)
            .WithName("Gateway.AdminStartRoomBattle")
            .Accepts<WebStartRoomBattleRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);

        group.MapGet("/server/status", (IWebHostEnvironment environment) =>
            Results.Ok(GatewayAdminOperations.GetStatus(environment)))
            .WithName("Gateway.AdminServerStatus")
            .Produces<AdminServerStatusHttpResponse>(StatusCodes.Status200OK);

        group.MapGet("/cluster/diagnostics", (IClusterClient client, IOptions<AbilityKitOrleansClusterOptions> clusterOptions, IWebHostEnvironment environment) =>
            Results.Ok(GatewayClusterDiagnostics.GetDiagnostics(client, clusterOptions.Value, environment)))
            .WithName("Gateway.AdminClusterDiagnostics")
            .Produces<AdminClusterDiagnosticsHttpResponse>(StatusCodes.Status200OK);

        group.MapGet("/skills/summary", async (string? roomId, string? battleId, IClusterClient client) =>
            Results.Ok(await GatewaySkillDiagnostics.GetSummaryAsync(client, roomId, battleId)))
            .WithName("Gateway.AdminSkillDiagnosticsSummary")
            .Produces<AdminSkillDiagnosticsSummaryHttpResponse>(StatusCodes.Status200OK);

        group.MapGet("/skills/events", async (
                string? battleId,
                int? actorId,
                int? skillId,
                int? limit,
                IClusterClient client) =>
            Results.Ok(await GatewaySkillDiagnostics.GetEventsAsync(
                client,
                battleId,
                actorId,
                skillId,
                limit ?? 100)))
            .WithName("Gateway.AdminSkillDiagnosticsEvents")
            .Produces<AdminSkillDiagnosticsEventsHttpResponse>(StatusCodes.Status200OK);

        group.MapGet("/skills/analysis-model", () =>
            Results.Ok(GatewaySkillDiagnostics.GetAnalysisModel()))
            .WithName("Gateway.AdminSkillAnalysisModel")
            .Produces<AdminSkillAnalysisModelHttpResponse>(StatusCodes.Status200OK);

        group.MapGet("/skills/analysis-artifacts/directories", () =>
            Results.Ok(GatewaySkillAnalysisArtifacts.ListArtifactDirectories()))
            .WithName("Gateway.AdminSkillAnalysisArtifactDirectories")
            .Produces<AdminSkillAnalysisArtifactDirectoryListHttpResponse>(StatusCodes.Status200OK);

        group.MapGet("/skills/analysis-artifacts", (string? artifactDirectory) =>
            Results.Ok(GatewaySkillAnalysisArtifacts.ListArtifacts(artifactDirectory)))
            .WithName("Gateway.AdminSkillAnalysisArtifacts")
            .Produces<AdminSkillAnalysisArtifactListHttpResponse>(StatusCodes.Status200OK);

        group.MapGet("/skills/analysis-artifacts/{fileName}", (string fileName, string? artifactDirectory) =>
            GatewaySkillAnalysisArtifacts.GetArtifact(fileName, artifactDirectory))
            .WithName("Gateway.AdminSkillAnalysisArtifact")
            .Produces<AdminSkillAnalysisArtifactHttpResponse>(StatusCodes.Status200OK)
            .Produces<AdminSkillAnalysisArtifactHttpResponse>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/skills/acceptance/artifact-directories", () =>
            Results.Ok(GatewaySkillAcceptanceArtifacts.ListArtifactDirectories()))
            .WithName("Gateway.AdminSkillAcceptanceArtifactDirectories")
            .Produces<AdminSkillAcceptanceArtifactDirectoryListHttpResponse>(StatusCodes.Status200OK);

        group.MapGet("/skills/acceptance/templates", () =>
            Results.Ok(GatewaySkillAcceptanceArtifacts.GetTemplates()))
            .WithName("Gateway.AdminSkillAcceptanceTemplates")
            .Produces<AdminSkillAcceptanceTemplateListHttpResponse>(StatusCodes.Status200OK);

        group.MapGet("/skills/acceptance/batch", (string? artifactDirectory) =>
            Results.Ok(GatewaySkillAcceptanceArtifacts.GetBatch(artifactDirectory)))
            .WithName("Gateway.AdminSkillAcceptanceBatch")
            .Produces<AdminSkillAcceptanceBatchHttpResponse>(StatusCodes.Status200OK);

        group.MapPost("/skills/acceptance/run", (AdminSkillAcceptanceRunRequest request) =>
            GatewaySkillAcceptanceArtifacts.Run(request))
            .WithName("Gateway.AdminSkillAcceptanceRun")
            .Accepts<AdminSkillAcceptanceRunRequest>("application/json")
            .Produces<AdminSkillAcceptanceRunResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/skills/acceptance/delete", (AdminSkillAcceptanceDeleteRequest request) =>
            GatewaySkillAcceptanceArtifacts.Delete(request))
            .WithName("Gateway.AdminSkillAcceptanceDelete")
            .Accepts<AdminSkillAcceptanceDeleteRequest>("application/json")
            .Produces<AdminSkillAcceptanceDeleteResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/skills/acceptance/cases/{caseId}", (string caseId, string? artifactDirectory, int? traceLimit) =>
            GatewaySkillAcceptanceArtifacts.GetCase(caseId, artifactDirectory, traceLimit))
            .WithName("Gateway.AdminSkillAcceptanceCase")
            .Produces<AdminSkillAcceptanceCaseHttpResponse>(StatusCodes.Status200OK)
            .Produces<AdminSkillAcceptanceCaseHttpResponse>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/skills/acceptance/run-plan", (string? artifactDirectory) =>
            Results.Ok(GatewaySkillAcceptanceArtifacts.GetRunPlan(artifactDirectory)))
            .WithName("Gateway.AdminSkillAcceptanceRunPlan")
            .Produces<AdminSkillAcceptanceRunPlanHttpResponse>(StatusCodes.Status200OK);

        group.MapGet("/shooter/world", async (string? roomId, string? battleId, IClusterClient client) =>
            await GetAdminShooterWorldDiagnosticsAsync(roomId, battleId, client))
            .WithName("Gateway.AdminShooterWorldDiagnostics")
            .Produces<BattleWorldDiagnostics>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/server/maintenance", async (AdminServerOperationHttpRequest request, IClusterClient client, IWebHostEnvironment environment) =>
            await ExecuteAdminServerOperationAsync(request, client, environment, GatewayAdminOperations.SetMaintenanceMode))
            .WithName("Gateway.AdminSetMaintenanceMode")
            .Accepts<AdminServerOperationHttpRequest>("application/json")
            .Produces<AdminServerOperationHttpResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/server/drain", async (AdminServerOperationHttpRequest request, IClusterClient client, IWebHostEnvironment environment) =>
            await ExecuteAdminServerOperationAsync(request, client, environment, GatewayAdminOperations.SetDrainMode))
            .WithName("Gateway.AdminSetDrainMode")
            .Accepts<AdminServerOperationHttpRequest>("application/json")
            .Produces<AdminServerOperationHttpResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/server/restart-request", async (AdminServerOperationHttpRequest request, IClusterClient client, IWebHostEnvironment environment) =>
            await ExecuteAdminServerOperationAsync(request, client, environment, GatewayAdminOperations.RequestRestart))
            .WithName("Gateway.AdminRequestServerRestart")
            .Accepts<AdminServerOperationHttpRequest>("application/json")
            .Produces<AdminServerOperationHttpResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);
    }

    private static void MapGatewaySandboxEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api")
            .WithTags("Sandbox");

        group.MapPost("/shooter-sandbox/start", async (ShooterSandboxHttpStartRequest wire, IClusterClient client) =>
        {
            var sandboxId = ResolveSandboxId(wire.SandboxId, wire.ServerId);
            var sandbox = client.GetGrain<IShooterSandboxGrain>(sandboxId);
            var resp = await sandbox.StartAsync(new StartShooterSandboxRequest(
                wire.Region,
                wire.ServerId,
                wire.BotCount,
                wire.MaxPlayers,
                wire.TickRate,
                wire.Title,
                wire.Tags));
            return Results.Ok(resp);
        })
        .WithName("Gateway.StartShooterSandbox")
        .Accepts<ShooterSandboxHttpStartRequest>("application/json")
        .Produces(StatusCodes.Status200OK);

        group.MapGet("/shooter-sandbox/{sandboxId?}", async (string? sandboxId, IClusterClient client) =>
        {
            var sandbox = client.GetGrain<IShooterSandboxGrain>(ResolveSandboxId(sandboxId, null));
            var resp = await sandbox.GetStateAsync();
            return Results.Ok(resp);
        })
        .WithName("Gateway.GetShooterSandboxState")
        .Produces(StatusCodes.Status200OK);

        group.MapPost("/shooter-sandbox/stop", async (ShooterSandboxHttpRequest wire, IClusterClient client) =>
        {
            var sandbox = client.GetGrain<IShooterSandboxGrain>(ResolveSandboxId(wire.SandboxId, wire.ServerId));
            await sandbox.StopAsync();
            return Results.Ok(new { Success = true });
        })
        .WithName("Gateway.StopShooterSandbox")
        .Produces(StatusCodes.Status200OK);
    }

    private static async Task<string?> ValidateAccountAsync(IClusterClient client, string? sessionToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return null;
        }

        var session = client.GetGrain<ISessionGrain>("global");
        var result = await session.ValidateAsync(new ValidateSessionRequest(sessionToken));
        return result.IsValid ? result.AccountId : null;
    }

    private static async Task MarkCurrentRoomOfflineAsync(IClusterClient client, string accountId)
    {
        var mapping = client.GetGrain<IRoomIdMappingGrain>("global");
        var roomId = await mapping.TryGetAccountRoomAsync(accountId);
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return;
        }

        var room = client.GetGrain<IRoomGrain>(roomId);
        await room.MarkOfflineAsync(accountId);
    }

    private static Task<IResult> CreateAdminRoomAsync(WebCreateRoomRequest request, IClusterClient client)
    {
        return GatewayEndpointHelpers.ExecuteRoomOperationAsync(async () =>
        {
            var accountId = await ValidateAccountAsync(client, request.SessionToken);
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return InvalidAdminRoomSession();
            }

            var directory = client.GetGrain<IRoomDirectoryGrain>($"{NormalizeAdminRegion(request.Region)}:{NormalizeAdminServerId(request.ServerId)}");
            var response = await directory.CreateRoomAsync(new CreateRoomRequest(
                accountId,
                NormalizeAdminRegion(request.Region),
                NormalizeAdminServerId(request.ServerId),
                string.IsNullOrWhiteSpace(request.RoomType) ? ShooterServerProtocol.RoomType : request.RoomType,
                request.Title ?? string.Empty,
                request.IsPublic,
                request.MaxPlayers,
                request.Tags));

            if (request.AutoJoin)
            {
                var room = client.GetGrain<IRoomGrain>(response.RoomId);
                await room.JoinMemberAsync(new JoinRoomMemberRequest(accountId));
                await BindAdminCurrentRoomAsync(client, accountId, response.RoomId);
            }

            return Results.Ok(response);
        });
    }

    private static Task<IResult> JoinAdminRoomAsync(WebRoomRequest request, IClusterClient client)
    {
        return GatewayEndpointHelpers.ExecuteRoomOperationAsync(async () =>
        {
            var accountId = await ValidateAccountAsync(client, request.SessionToken);
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return InvalidAdminRoomSession();
            }

            if (string.IsNullOrWhiteSpace(request.RoomId))
            {
                return AdminRoomBadRequest("RoomId is required.");
            }

            var room = client.GetGrain<IRoomGrain>(request.RoomId);
            var response = await room.JoinMemberAsync(new JoinRoomMemberRequest(accountId));
            await BindAdminCurrentRoomAsync(client, accountId, request.RoomId);
            return Results.Ok(response);
        });
    }

    private static Task<IResult> RestoreCurrentAdminRoomAsync(SessionTokenRequest request, IClusterClient client)
    {
        return GatewayEndpointHelpers.ExecuteRoomOperationAsync(async () =>
        {
            var accountId = await ValidateAccountAsync(client, request.SessionToken);
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return InvalidAdminRoomSession();
            }

            var mapping = client.GetGrain<IRoomIdMappingGrain>("global");
            var roomId = await mapping.TryGetAccountRoomAsync(accountId);
            if (string.IsNullOrWhiteSpace(roomId))
            {
                return GatewayEndpointHelpers.ToRoomHttpError(
                    RoomGatewayErrorCodes.AccountNotInRoom,
                    "No current room for account.",
                    StatusCodes.Status404NotFound,
                    GatewayStatusCode.NotFound);
            }

            var room = client.GetGrain<IRoomGrain>(roomId);
            var restore = await room.RestoreAsync(accountId);
            return Results.Ok(restore);
        });
    }

    private static Task<IResult> LeaveAdminRoomAsync(WebRoomRequest request, IClusterClient client)
    {
        return GatewayEndpointHelpers.ExecuteRoomOperationAsync(async () =>
        {
            var accountId = await ValidateAccountAsync(client, request.SessionToken);
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return InvalidAdminRoomSession();
            }

            if (string.IsNullOrWhiteSpace(request.RoomId))
            {
                return AdminRoomBadRequest("RoomId is required.");
            }

            var room = client.GetGrain<IRoomGrain>(request.RoomId);
            await room.LeaveAsync(accountId);
            var mapping = client.GetGrain<IRoomIdMappingGrain>("global");
            await mapping.ClearAccountRoomAsync(accountId, request.RoomId);
            return Results.Ok(new { Success = true });
        });
    }

    private static Task<IResult> CloseAdminRoomAsync(WebRoomRequest request, IClusterClient client)
    {
        return GatewayEndpointHelpers.ExecuteRoomOperationAsync(async () =>
        {
            var accountId = await ValidateAccountAsync(client, request.SessionToken);
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return InvalidAdminRoomSession();
            }

            if (string.IsNullOrWhiteSpace(request.RoomId))
            {
                return AdminRoomBadRequest("RoomId is required.");
            }

            var room = client.GetGrain<IRoomGrain>(request.RoomId);
            await room.CloseAsync(accountId);
            var mapping = client.GetGrain<IRoomIdMappingGrain>("global");
            await mapping.ClearAccountRoomAsync(accountId, request.RoomId);
            return Results.Ok(new { Success = true });
        });
    }

    private static Task<IResult> MarkAdminRoomMemberOfflineAsync(WebRoomRequest request, IClusterClient client)
    {
        return GatewayEndpointHelpers.ExecuteRoomOperationAsync(async () =>
        {
            var accountId = await ValidateAccountAsync(client, request.SessionToken);
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return InvalidAdminRoomSession();
            }

            if (string.IsNullOrWhiteSpace(request.RoomId))
            {
                return AdminRoomBadRequest("RoomId is required.");
            }

            var room = client.GetGrain<IRoomGrain>(request.RoomId);
            await room.MarkOfflineAsync(accountId);
            var state = await room.GetRuntimeStateAsync();
            return Results.Ok(state);
        });
    }

    private static Task<IResult> SetAdminRoomReadyAsync(WebRoomReadyRequest request, IClusterClient client)
    {
        return GatewayEndpointHelpers.ExecuteRoomOperationAsync(async () =>
        {
            var accountId = await ValidateAccountAsync(client, request.SessionToken);
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return InvalidAdminRoomSession();
            }

            if (string.IsNullOrWhiteSpace(request.RoomId))
            {
                return AdminRoomBadRequest("RoomId is required.");
            }

            var room = client.GetGrain<IRoomGrain>(request.RoomId);
            await room.SetReadyAsync(new RoomReadyRequest(accountId, request.Ready));
            var snapshot = await room.GetSnapshotAsync();
            return Results.Ok(snapshot);
        });
    }

    private static Task<IResult> PickAdminRoomHeroAsync(WebRoomPickHeroRequest request, IClusterClient client)
    {
        return GatewayEndpointHelpers.ExecuteRoomOperationAsync(async () =>
        {
            var accountId = await ValidateAccountAsync(client, request.SessionToken);
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return InvalidAdminRoomSession();
            }

            if (string.IsNullOrWhiteSpace(request.RoomId))
            {
                return AdminRoomBadRequest("RoomId is required.");
            }

            var room = client.GetGrain<IRoomGrain>(request.RoomId);
            await room.SubmitGameplayCommandAsync(RoomGameplayCommandRequest.CreateMobaLoadout(
                accountId,
                request.HeroId,
                request.TeamId,
                request.SpawnPointId,
                request.Level,
                request.AttributeTemplateId,
                request.BasicAttackSkillId,
                request.SkillIds));
            await room.SetReadyAsync(new RoomReadyRequest(accountId, true));
            var snapshot = await room.GetSnapshotAsync();
            return Results.Ok(snapshot);
        });
    }

    private static Task<IResult> AddAdminRoomRobotsAsync(WebAddRoomRobotsRequest request, IClusterClient client)
    {
        return GatewayEndpointHelpers.ExecuteRoomOperationAsync(async () =>
        {
            var accountId = await ValidateAccountAsync(client, request.SessionToken);
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return InvalidAdminRoomSession();
            }

            if (string.IsNullOrWhiteSpace(request.RoomId))
            {
                return AdminRoomBadRequest("RoomId is required.");
            }

            var manager = client.GetGrain<IRoomRobotManagerGrain>(request.RoomId);
            var response = await manager.AddRobotsAsync(new AddRoomRobotsRequest(
                request.RoomId,
                accountId,
                request.Count,
                request.AccountPrefix,
                request.AutoReady,
                request.MountBattleAi,
                request.BattleAiProfileId));
            return Results.Ok(response);
        });
    }

    private static Task<IResult> StartAdminRoomBattleAsync(WebStartRoomBattleRequest request, IClusterClient client)
    {
        return GatewayEndpointHelpers.ExecuteRoomOperationAsync(async () =>
        {
            var accountId = await ValidateAccountAsync(client, request.SessionToken);
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return InvalidAdminRoomSession();
            }

            if (string.IsNullOrWhiteSpace(request.RoomId))
            {
                return AdminRoomBadRequest("RoomId is required.");
            }

            var room = client.GetGrain<IRoomGrain>(request.RoomId);
            var response = await room.StartBattleAsync(new StartRoomBattleRequest(
                accountId,
                request.GameplayId,
                request.RuleSetId,
                request.ConfigVersion,
                request.ProtocolVersion,
                request.WorldType,
                request.ClientId,
                new BattleSyncStartOptions(
                    request.SyncTemplateId,
                    request.SyncModel ?? 0,
                    request.NetworkEnvironmentId,
                    request.CarrierName,
                    request.EnableAuthoritativeWorld,
                    request.InterpolationEnabled,
                    request.InputDelayFrames)));

            MountRoomRobotBattleAiResponse? mountResponse = null;
            if (response.Started)
            {
                var manager = client.GetGrain<IRoomRobotManagerGrain>(request.RoomId);
                mountResponse = await manager.MountBattleAiAsync(new MountRoomRobotBattleAiRequest(request.RoomId));
            }

            return Results.Ok(new WebStartRoomBattleResponse(response, mountResponse));
        });
    }

    private static Task BindAdminCurrentRoomAsync(IClusterClient client, string accountId, string roomId)
    {
        var mapping = client.GetGrain<IRoomIdMappingGrain>("global");
        return mapping.BindAccountRoomAsync(accountId, roomId);
    }

    private static string NormalizeAdminRegion(string? region)
    {
        return string.IsNullOrWhiteSpace(region) ? "dev" : region;
    }

    private static string NormalizeAdminServerId(string? serverId)
    {
        return string.IsNullOrWhiteSpace(serverId) ? ShooterServerProtocol.DefaultServerId : serverId;
    }

    private static IResult InvalidAdminRoomSession()
    {
        return GatewayEndpointHelpers.ToRoomHttpError(
            RoomGatewayErrorCodes.BadRequest,
            "Invalid session",
            StatusCodes.Status400BadRequest,
            GatewayStatusCode.BadRequest);
    }

    private static IResult AdminRoomBadRequest(string message)
    {
        return GatewayEndpointHelpers.ToRoomHttpError(
            RoomGatewayErrorCodes.BadRequest,
            message,
            StatusCodes.Status400BadRequest,
            GatewayStatusCode.BadRequest);
    }

    private static IResult ListGameplays()
    {
        return Results.Ok(GatewayGameplayCatalog.All);
    }

    private static async Task<IResult> BuildAdminDashboardAsync(AdminDashboardHttpRequest request, IClusterClient client, IWebHostEnvironment environment)
    {
        var region = string.IsNullOrWhiteSpace(request.Region) ? "cn" : request.Region;
        var serverId = string.IsNullOrWhiteSpace(request.ServerId) ? "dev" : request.ServerId;
        var roomType = string.IsNullOrWhiteSpace(request.RoomType) ? ShooterServerProtocol.RoomType : request.RoomType;
        var limit = request.Limit <= 0 ? 20 : Math.Min(request.Limit, 100);
        var accountId = await ValidateAccountAsync(client, request.SessionToken);

        var directory = client.GetGrain<IRoomDirectoryGrain>($"{region}:{serverId}");
        var rooms = await directory.ListRoomsAsync(new ListRoomsRequest(
            accountId ?? "admin-dashboard",
            region,
            serverId,
            0,
            limit,
            roomType));

        string? currentRoomId = null;
        RestoreRoomResponse? currentRoom = null;
        RoomRuntimeState? runtimeState = null;
        if (!string.IsNullOrWhiteSpace(accountId))
        {
            var mapping = client.GetGrain<IRoomIdMappingGrain>("global");
            currentRoomId = await mapping.TryGetAccountRoomAsync(accountId);
            if (!string.IsNullOrWhiteSpace(currentRoomId))
            {
                var room = client.GetGrain<IRoomGrain>(currentRoomId);
                currentRoom = await room.RestoreAsync(accountId);
                runtimeState = await room.GetRuntimeStateAsync();
            }
        }

        var sandbox = client.GetGrain<IShooterSandboxGrain>(ResolveSandboxId(request.SandboxId, serverId));
        var sandboxState = await sandbox.GetStateAsync();

        return Results.Ok(new AdminDashboardHttpResponse(
            GatewayGameplayCatalog.All,
            rooms.Rooms,
            accountId,
            currentRoomId,
            currentRoom,
            runtimeState,
            sandboxState,
            DateTime.UtcNow.Ticks,
            GatewayAdminOperations.GetStatus(environment)));
    }

    private static async Task<IResult> GetAdminShooterWorldDiagnosticsAsync(string? roomId, string? battleId, IClusterClient client)
    {
        var resolvedBattleId = battleId;
        if (string.IsNullOrWhiteSpace(resolvedBattleId) && !string.IsNullOrWhiteSpace(roomId))
        {
            var room = client.GetGrain<IRoomGrain>(roomId);
            var runtimeState = await room.GetRuntimeStateAsync();
            resolvedBattleId = runtimeState.BattleId;
        }

        if (string.IsNullOrWhiteSpace(resolvedBattleId))
        {
            return Results.NotFound("Room has no running Shooter battleId.");
        }

        var battle = client.GetGrain<IBattleLogicHostGrain>(resolvedBattleId);
        var diagnostics = await battle.GetWorldDiagnosticsAsync();
        if (diagnostics is null)
        {
            return Results.NotFound("Shooter world diagnostics are not available for this battle.");
        }

        return Results.Ok(diagnostics);
    }

    private static async Task<IResult> ExecuteAdminServerOperationAsync(
        AdminServerOperationHttpRequest request,
        IClusterClient client,
        IWebHostEnvironment environment,
        Func<AdminServerOperationHttpRequest, string, IWebHostEnvironment, AdminServerOperationHttpResponse> operation)
    {
        var accountId = await ValidateAccountAsync(client, request.SessionToken);
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return Results.BadRequest("A valid admin session token is required.");
        }

        return Results.Ok(operation(request, accountId, environment));
    }

    private static string ResolveSandboxId(string? sandboxId, string? serverId)
    {
        if (!string.IsNullOrWhiteSpace(sandboxId))
        {
            return sandboxId;
        }

        if (!string.IsNullOrWhiteSpace(serverId))
        {
            return serverId;
        }

        return ShooterServerProtocol.DefaultSandboxId;
    }
}
