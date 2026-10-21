"""副本（战斗实例）定义与跨表引用校验。

副本表只负责**客户端装配契约**：进这个副本要加载哪个流程 Profile、挂哪些扩展点实现、
以及入场协议约束（人数、宿主模式）。玩法常量（时长、队伍规模、胜负条件、地图资源）
一律由 ``gameplays`` / ``battle_maps`` 等表负责，副本表只引用不复制，避免两份配置漂移。

FlowProfileId 不在 Excel 里校验，因为它归 ``battle_flow_profiles.json``（Flow 编辑流程）管有，
运行时由 ``MobaDungeonValidator`` 完成校验。
"""

HOST_MODES = ("Local", "GatewayRemote")


class Rule:
    def references(self):
        return ({"dungeons": {"GameplayId": "gameplays"}}, set())

    def normalize(self, table, row):
        if table != "dungeons":
            return row
        modes = [value.strip() for value in (row.get("AllowedHostModes") or [])
                 if value and value.strip()]
        for mode in modes:
            if mode not in HOST_MODES:
                raise ValueError("dungeons/%s: unknown host mode %s" % (row.get("Id"), mode))
        for field in ("SceneProviderId", "GameplayHandlerId", "OutcomeRuleId"):
            value = row.get(field)
            if value is not None and not str(value).strip():
                row[field] = None
        # 人数是入场协议约束（客户端拦截），不是玩法常量，所以仍然由副本表声明。
        minimum = row.get("MinPlayerCount")
        maximum = row.get("MaxPlayerCount")
        if minimum is not None and minimum < 0:
            raise ValueError("dungeons/%s: MinPlayerCount must not be negative" % row.get("Id"))
        if minimum and maximum and minimum > maximum:
            raise ValueError("dungeons/%s: MinPlayerCount exceeds MaxPlayerCount" % row.get("Id"))
        return row

    def validate(self, context):
        dungeons = context.tables.get("dungeons") or []
        if not dungeons:
            raise ValueError("dungeons: table is empty")
        identifiers = set()
        for dungeon in dungeons:
            identifier = dungeon.get("Id")
            if identifier in identifiers:
                raise ValueError("dungeons: duplicate ID %s" % identifier)
            identifiers.add(identifier)
            if not dungeon.get("FlowProfileId"):
                raise ValueError("dungeons/%s: FlowProfileId is required" % identifier)
            if not dungeon.get("GameplayId"):
                raise ValueError("dungeons/%s: GameplayId is required" % identifier)
            if not dungeon.get("GameplayHandlerId"):
                raise ValueError("dungeons/%s: GameplayHandlerId is required" % identifier)
            if not dungeon.get("OutcomeRuleId"):
                raise ValueError("dungeons/%s: OutcomeRuleId is required" % identifier)
