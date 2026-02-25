using System.Collections.Generic;
using AbilityKit.Ability.Explain;
using AbilityKit.Ability.Explain.Editor;
using UnityEditor;

namespace AbilityKit.Ability.Explain.Samples.MockIntegration
{
    [InitializeOnLoad]
    internal static class MockAbilityExplainIntegration
    {
        static MockAbilityExplainIntegration()
        {
            AbilityExplainRegistry.Register(new MockEntityProvider());
            AbilityExplainRegistry.Register(new MockExplainResolver());
            AbilityExplainRegistry.Register(new MockNavigator());
            AbilityExplainRegistry.Register(new MockDiscoveryPolicy());
            AbilityExplainRegistry.Register(new MockEntityListModule());
            AbilityExplainRegistry.Register(new MockContextEditorProvider());
        }

        private sealed class MockContextEditorProvider : IExplainContextEditorProvider
        {
            public int Priority => 0;

            public bool CanEdit(in PipelineItemKey key)
            {
                return key.Type == "Ability";
            }

            public string GetButtonText(in PipelineItemKey key)
            {
                return "强化/构筑";
            }

            public string GetWindowTitle(in PipelineItemKey key)
            {
                return $"Build: {key}";
            }

            public UnityEngine.UIElements.VisualElement BuildEditor(ExplainContextEditorContext context)
            {
                var root = new UnityEngine.UIElements.VisualElement();
                root.style.paddingLeft = 6;
                root.style.paddingRight = 6;
                root.style.paddingTop = 6;
                root.style.paddingBottom = 6;
                root.style.flexDirection = UnityEngine.UIElements.FlexDirection.Column;

                if (context.ResolveContext.Values == null)
                {
                    context.ResolveContext.Values = new Dictionary<string, string>();
                }

                bool Has(string k) => context.ResolveContext.Values.TryGetValue(k, out var v) && v == "1";
                void Set(string k, bool on)
                {
                    context.ResolveContext.Values[k] = on ? "1" : "0";
                }

                var title = new UnityEngine.UIElements.Label("Modifiers");
                title.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
                title.style.marginBottom = 4;
                root.Add(title);

                var close = new UnityEngine.UIElements.Button(context.Close) { text = "Close" };
                close.style.marginBottom = 6;
                root.Add(close);

                var tDiff = new UnityEngine.UIElements.Toggle("Show Diff") { value = Has("ui_show_diff") };
                tDiff.RegisterCallback<UnityEngine.UIElements.ChangeEvent<bool>>(evt =>
                {
                    Set("ui_show_diff", evt.newValue);
                    context.RequestResolve();
                });
                root.Add(tDiff);

                var tDamage = new UnityEngine.UIElements.Toggle("Damage +20%") { value = Has("mod_damage_plus") };
                tDamage.RegisterCallback<UnityEngine.UIElements.ChangeEvent<bool>>(evt =>
                {
                    Set("mod_damage_plus", evt.newValue);
                    context.RequestResolve();
                });
                root.Add(tDamage);

                var tSplit = new UnityEngine.UIElements.Toggle("Split Shot") { value = Has("mod_split_shot") };
                tSplit.RegisterCallback<UnityEngine.UIElements.ChangeEvent<bool>>(evt =>
                {
                    Set("mod_split_shot", evt.newValue);
                    context.RequestResolve();
                });
                root.Add(tSplit);

                var tReplaceProjectile = new UnityEngine.UIElements.Toggle("Replace Projectile (700 -> 701)") { value = Has("mod_replace_projectile") };
                tReplaceProjectile.RegisterCallback<UnityEngine.UIElements.ChangeEvent<bool>>(evt =>
                {
                    Set("mod_replace_projectile", evt.newValue);
                    context.RequestResolve();
                });
                root.Add(tReplaceProjectile);

                return root;
            }
        }

        private sealed class MockDiscoveryPolicy : IDiscoveryPolicy
        {
            public int Priority => 0;

            public bool IsDiscoverable(in PipelineItemKey key)
            {
                return key.Type == "Projectile" || key.Type == "Buff";
            }
        }

        private sealed class MockEntityProvider : IEntityProviderEx
        {
            public int Priority => 0;

            public bool CanProvide(string searchText) => true;

            public IEnumerable<PipelineItemKey> Query(string searchText)
            {
                yield return new PipelineItemKey("Ability", "1001");
                yield return new PipelineItemKey("Ability", "1002");
                yield return new PipelineItemKey("Ability", "2001");
            }

            public string GetDisplayName(in PipelineItemKey key)
            {
                return key.ToString();
            }
        }

        private sealed class MockEntityListModule : IExplainEntityListModule
        {
            private bool _includePassive = true;

            public int Priority => 0;

            public bool CanHandle(IEntityProvider provider) => provider != null;

            public UnityEngine.UIElements.VisualElement BuildFilters(ExplainEntityListModuleContext context)
            {
                var row = new UnityEngine.UIElements.VisualElement();
                row.style.flexDirection = UnityEngine.UIElements.FlexDirection.Column;
                row.style.paddingLeft = 6;
                row.style.paddingRight = 6;
                row.style.paddingTop = 6;
                row.style.paddingBottom = 4;

                var t = new UnityEngine.UIElements.Toggle("显示被动") { value = _includePassive };
                t.RegisterCallback<UnityEngine.UIElements.ChangeEvent<bool>>(evt =>
                {
                    _includePassive = evt.newValue;
                    context.RequestRefresh?.Invoke();
                });
                row.Add(t);

                return row;
            }

            public List<ExplainEntityListGroup> BuildGroups(IEntityProvider provider, List<PipelineItemKey> entities)
            {
                var active = new ExplainEntityListGroup { Title = "主动技能" };
                var passive = new ExplainEntityListGroup { Title = "被动技能" };

                if (entities != null)
                {
                    for (var i = 0; i < entities.Count; i++)
                    {
                        var k = entities[i];
                        if (string.IsNullOrEmpty(k.Type) && string.IsNullOrEmpty(k.Id)) continue;

                        if (IsPassive(k))
                        {
                            if (_includePassive) passive.Items.Add(k);
                        }
                        else
                        {
                            active.Items.Add(k);
                        }
                    }
                }

                var groups = new List<ExplainEntityListGroup>();
                if (active.Items.Count > 0) groups.Add(active);
                if (passive.Items.Count > 0) groups.Add(passive);
                return groups;
            }

            private static bool IsPassive(in PipelineItemKey key)
            {
                if (key.Type != "Ability") return false;
                if (long.TryParse(key.Id, out var id)) return id >= 2000;
                return key.Id != null && key.Id.StartsWith("2");
            }
        }

        private sealed class MockExplainResolver : IExplainResolverEx
        {
            private readonly Dictionary<PipelineItemKey, ExplainForest> _cache = new Dictionary<PipelineItemKey, ExplainForest>();

            public int Priority => 0;

            public bool CanResolve(ExplainResolveRequest request) => true;

            public bool CanExpand(ExplainExpandRequest request) => true;

            public bool TryResolve(ExplainResolveRequest request, out ExplainResolveResult result)
            {
                var key = request != null ? request.Key : default;
                var ctx = request != null ? request.Context : null;
                var hasMods = ctx != null && ctx.Values != null && ctx.Values.Count > 0;
                var cacheKey = key;

                if (hasMods)
                {
                    var suffix = "";
                    if (TryGetMod(ctx, "mod_damage_plus")) suffix += "+dmg";
                    if (TryGetMod(ctx, "mod_split_shot")) suffix += "+split";
                    if (TryGetMod(ctx, "mod_replace_projectile")) suffix += "+proj";
                    if (!string.IsNullOrEmpty(suffix)) cacheKey = new PipelineItemKey(key.Type, key.Id + suffix);
                }

                if (_cache.TryGetValue(cacheKey, out var cachedForest))
                {
                    result = ExplainResolveResult.FromForest(cachedForest);
                    result.CacheHit = true;
                    result.Debug = "Mock resolver: cache hit";
                    AppendMockIssues(result, key,
                        ExplainNodeId.FromParts("mock_cond", "CastCondition", "200"),
                        ExplainNodeId.FromParts("mock_proj", "Projectile", "700"));
                    return true;
                }

                var forest = new ExplainForest();

                var flowRoot = ExplainNode.Create($"流程 {key}");
                flowRoot.NodeId = ExplainNodeId.FromKey("mock_flow", key);
                flowRoot.Kind = "flow";
                flowRoot.SummaryLines.Add("示例：释放条件 -> 选目标 -> 时间轴 -> 发射子弹");

                var cond = ExplainNode.Create("释放条件 (CastCondition#200)");
                cond.NodeId = ExplainNodeId.FromParts("mock_cond", "CastCondition", "200");
                cond.Kind = "condition";
                cond.SummaryLines.Add("需要目标");
                cond.SummaryLines.Add("距离 <= 8");
                cond.Source = ExplainSourceRef.TableRow("CastCondition", "200");
                cond.Actions.Add(ExplainAction.Navigate("打开表行", NavigationTarget.OpenTableRow("CastCondition", "200")));

                var projectileRef = ExplainNode.Create("发射子弹 (Projectile#700)");
                projectileRef.NodeId = ExplainNodeId.FromParts("mock_proj", "Projectile", "700");
                projectileRef.Kind = "effect";
                projectileRef.SummaryLines.Add("速度 12 / 生命周期 1.2s");
                projectileRef.Source = ExplainSourceRef.TableRow("SkillTimeline", "9001", "event[2]");
                projectileRef.Actions.Add(ExplainAction.Navigate("打开表行", NavigationTarget.OpenTableRow("Projectile", "700")));
                projectileRef.Actions.Add(ExplainAction.Navigate("跳转到子弹树", NavigationTarget.OpenEditor("focus_tree", new Dictionary<string, string> { { "type", "Projectile" }, { "id", "700" } })));

                if (TryGetMod(ctx, "mod_replace_projectile"))
                {
                    projectileRef.Title = "发射子弹 (Projectile#701)";
                    projectileRef.NodeId = ExplainNodeId.FromParts("mock_proj", "Projectile", "701");
                    projectileRef.Actions.Clear();
                    projectileRef.Actions.Add(ExplainAction.Navigate("打开表行", NavigationTarget.OpenTableRow("Projectile", "701")));
                    projectileRef.Actions.Add(ExplainAction.Navigate("跳转到子弹树", NavigationTarget.OpenEditor("focus_tree", new Dictionary<string, string> { { "type", "Projectile" }, { "id", "701" } })));
                }

                if (TryGetMod(ctx, "mod_split_shot"))
                {
                    var split = ExplainNode.Create("词条：分裂射击 x3");
                    split.Kind = "modifier";
                    split.SummaryLines.Add("示例：子弹命中后分裂") ;
                    projectileRef.Children.Add(split);
                }

                var timelineNode = ExplainNode.Create("时间轴 (SkillTimeline#9001)");
                timelineNode.NodeId = ExplainNodeId.FromParts("mock_timeline", "SkillTimeline", "9001");
                timelineNode.Kind = "timeline";
                timelineNode.Source = ExplainSourceRef.TableRow("SkillTimeline", "9001");
                timelineNode.Actions.Add(ExplainAction.Navigate("打开时间轴编辑器", NavigationTarget.OpenEditor("actioneditor", new Dictionary<string, string> { { "timeline", "9001" } })));

                var evtCast = ExplainNode.Create("t=0.00 Cast");
                evtCast.NodeId = ExplainNodeId.FromParts("mock_timeline_evt", "cast", "0.00");
                evtCast.Kind = "event";
                evtCast.Actions.Add(ExplainAction.Navigate("打开时间轴编辑器", NavigationTarget.OpenEditor("actioneditor", new Dictionary<string, string> { { "timeline", "9001" }, { "time", "0.00" } })));

                var triggerRef = ExplainNode.Create("触发器：对目标施加 Buff (Trigger#101)");
                triggerRef.NodeId = ExplainNodeId.FromParts("mock_trigger_ref", "Trigger", "101");
                triggerRef.Kind = "trigger_ref";
                triggerRef.SummaryLines.Add("示例：时间轴直接调用触发器");
                triggerRef.Source = ExplainSourceRef.TableRow("Trigger", "101");
                triggerRef.Actions.Add(ExplainAction.Navigate("打开表行", NavigationTarget.OpenTableRow("Trigger", "101")));
                evtCast.Children.Add(triggerRef);

                var buffRef = ExplainNode.Create("施加 Buff (Buff#33)");
                buffRef.NodeId = ExplainNodeId.FromParts("mock_buff", "Buff", "33");
                buffRef.Kind = "effect";
                buffRef.SummaryLines.Add("持续 2.0s / 增加移速 20%");
                buffRef.Source = ExplainSourceRef.TableRow("SkillTimeline", "9001", "event[0]");
                buffRef.Actions.Add(ExplainAction.Navigate("打开表行", NavigationTarget.OpenTableRow("Buff", "33")));
                evtCast.Children.Add(buffRef);

                var evtSpawn = ExplainNode.Create("t=0.20 SpawnProjectile");
                evtSpawn.NodeId = ExplainNodeId.FromParts("mock_timeline_evt", "spawn", "0.20");
                evtSpawn.Kind = "event";
                evtSpawn.Actions.Add(ExplainAction.Navigate("打开时间轴编辑器", NavigationTarget.OpenEditor("actioneditor", new Dictionary<string, string> { { "timeline", "9001" }, { "time", "0.20" } })));
                evtSpawn.Children.Add(projectileRef);

                var evtHit = ExplainNode.Create("t=0.60 Hit");
                evtHit.NodeId = ExplainNodeId.FromParts("mock_timeline_evt", "hit", "0.60");
                evtHit.Kind = "event";
                evtHit.Actions.Add(ExplainAction.Navigate("打开时间轴编辑器", NavigationTarget.OpenEditor("actioneditor", new Dictionary<string, string> { { "timeline", "9001" }, { "time", "0.60" } })));

                var damage = ExplainNode.Create("造成伤害 (Damage)");
                damage.NodeId = ExplainNodeId.FromParts("mock_damage", "Damage", "500");
                damage.Kind = "effect";
                var dmgLine = "基础 120 + 系数 0.8AP";
                if (TryGetMod(ctx, "mod_damage_plus")) dmgLine += " (x1.2)";
                damage.SummaryLines.Add(dmgLine);
                damage.Source = ExplainSourceRef.TableRow("SkillTimeline", "9001", "event[3]");
                damage.Actions.Add(ExplainAction.Navigate("打开表行", NavigationTarget.OpenTableRow("Damage", "500")));

                var applyDebuff = ExplainNode.Create("施加 Debuff (Buff#34)");
                applyDebuff.NodeId = ExplainNodeId.FromParts("mock_buff", "Buff", "34");
                applyDebuff.Kind = "effect";
                applyDebuff.SummaryLines.Add("持续 1.5s / 减速 30%");
                applyDebuff.Source = ExplainSourceRef.TableRow("SkillTimeline", "9001", "event[3]");
                applyDebuff.Actions.Add(ExplainAction.Navigate("打开表行", NavigationTarget.OpenTableRow("Buff", "34")));

                evtHit.Children.Add(damage);
                evtHit.Children.Add(applyDebuff);

                timelineNode.Children.Add(evtCast);
                timelineNode.Children.Add(evtSpawn);
                timelineNode.Children.Add(evtHit);

                flowRoot.Children.Add(cond);
                flowRoot.Children.Add(timelineNode);

                forest.Roots.Add(new ExplainTreeRoot
                {
                    Kind = "flow",
                    Key = key,
                    Title = "技能流程",
                    Root = flowRoot
                });

                if (request?.Options == null || request.Options.IncludeDiscovered)
                {
                    PrefetchDiscoveries(forest, forest.Roots);
                }

                _cache[cacheKey] = forest;
                result = ExplainResolveResult.FromForest(forest);
                result.CacheHit = false;
                result.Debug = "Mock resolver: built new forest";

                AppendMockIssues(result, key, cond.NodeId, projectileRef.NodeId);
                return true;
            }

            private static bool TryGetMod(ExplainResolveContext ctx, string key)
            {
                if (ctx == null || ctx.Values == null) return false;
                return ctx.Values.TryGetValue(key, out var v) && v == "1";
            }

            private static void PrefetchDiscoveries(ExplainForest forest, List<ExplainTreeRoot> roots)
            {
                if (forest == null || roots == null) return;

                var policy = AbilityExplainRegistry.GetDiscoveryPolicy();

                var refCount = new Dictionary<string, int>();

                void Add(string type, string id)
                {
                    if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(id)) return;
                    if (policy != null && !policy.IsDiscoverable(new PipelineItemKey(type, id))) return;
                    var k = $"{type}:{id}";
                    refCount.TryGetValue(k, out var c);
                    refCount[k] = c + 1;
                }

                void Walk(ExplainNode n)
                {
                    if (n == null) return;

                    if (n.Source != null && n.Source.Kind == "table_row")
                    {
                        Add(n.Source.TableName, n.Source.RowId);

                        if (n.Source.TableName == "Projectile" && n.Source.RowId == "700")
                        {
                            Add("Buff", "34");
                            Add("Buff", "35");
                        }

                        if (n.Source.TableName == "Trigger" && n.Source.RowId == "101")
                        {
                            Add("Buff", "36");
                        }
                    }

                    if (n.Actions != null)
                    {
                        foreach (var a in n.Actions)
                        {
                            var t = a != null ? a.NavigateTo : null;
                            if (t != null && t.Kind == "open_table_row")
                            {
                                Add(t.TableName, t.RowId);

                                if (t.TableName == "Projectile" && t.RowId == "700")
                                {
                                    Add("Buff", "34");
                                    Add("Buff", "35");
                                }

                                if (t.TableName == "Trigger" && t.RowId == "101")
                                {
                                    Add("Buff", "36");
                                }
                            }
                        }
                    }

                    if (n.Children == null) return;
                    foreach (var c in n.Children) Walk(c);
                }

                foreach (var r in roots)
                {
                    if (r?.Root == null) continue;
                    Walk(r.Root);
                }

                var existing = new HashSet<string>();
                if (forest.Discovered != null)
                {
                    foreach (var d in forest.Discovered)
                    {
                        if (d == null) continue;
                        existing.Add($"{d.Key.Type}:{d.Key.Id}");
                    }
                }

                foreach (var kv in refCount)
                {
                    if (existing.Contains(kv.Key)) continue;

                    var parts = kv.Key.Split(':');
                    if (parts.Length != 2) continue;

                    var type = parts[0];
                    var id = parts[1];

                    forest.Discovered.Add(new ExplainTreeDiscovery
                    {
                        Kind = type.ToLowerInvariant(),
                        Key = new PipelineItemKey(type, id),
                        Title = $"{type}: {type}#{id}",
                        RefCount = kv.Value
                    });
                }
            }

            private static void AppendMockIssues(ExplainResolveResult result, PipelineItemKey key, string condNodeId, string projectileNodeId)
            {
                if (result == null) return;

                var warn = ExplainIssue.Create(ExplainSeverity.Warning, "示例：技能时间轴缺少事件", $"Ability {key} has a missing timeline event.");
                warn.NodeId = condNodeId;
                warn.Source = ExplainSourceRef.TableRow("SkillTimeline", "9001", "event[3]");
                result.Issues.Add(warn);

                var err = ExplainIssue.Create(ExplainSeverity.Error, "示例：子弹配置找不到", "Referenced projectile id=700 is missing in projectile table.");
                err.NodeId = projectileNodeId;
                err.NavigateTo = NavigationTarget.OpenTableRow("Projectile", "700");
                result.Issues.Add(err);
            }

            public bool TryExpandDiscoveredRoot(ExplainExpandRequest request, out ExplainTreeRoot root)
            {
                var rootKey = request != null ? request.RootKey : default;
                ExplainNode node;

                if (rootKey.Type == "Projectile" && rootKey.Id == "700")
                {
                    node = ExplainNode.Create($"子弹 Projectile#{rootKey.Id}");
                    node.NodeId = ExplainNodeId.FromParts("mock_proj_tree", "Projectile", rootKey.Id);
                    node.Kind = "projectile";
                    node.SummaryLines.Add("命中触发 / 销毁触发示例");
                    node.Source = ExplainSourceRef.TableRow("Projectile", rootKey.Id);
                    node.Actions.Add(ExplainAction.Navigate("打开表行", NavigationTarget.OpenTableRow("Projectile", rootKey.Id)));

                    var onHit = ExplainNode.Create("触发器：进入碰撞 (OnHit)");
                    onHit.NodeId = ExplainNodeId.FromParts("mock_trigger", "OnHit", rootKey.Id);
                    onHit.Kind = "trigger";
                    onHit.SummaryLines.Add("当子弹命中单位时触发");

                    var hitCond = ExplainNode.Create("条件：目标存活 && 非友军");
                    hitCond.NodeId = ExplainNodeId.FromParts("mock_trigger_cond", "OnHit", "alive_enemy");
                    hitCond.Kind = "condition";
                    hitCond.SummaryLines.Add("示例条件，仅用于展示");

                    var hitDamage = ExplainNode.Create("行为：造成伤害 (Damage#500)");
                    hitDamage.NodeId = ExplainNodeId.FromParts("mock_damage", "Damage", "500");
                    hitDamage.Kind = "effect";
                    hitDamage.SummaryLines.Add("基础 120 + 系数 0.8AP");
                    hitDamage.Source = ExplainSourceRef.TableRow("Damage", "500");
                    hitDamage.Actions.Add(ExplainAction.Navigate("打开表行", NavigationTarget.OpenTableRow("Damage", "500")));

                    var hitDebuff = ExplainNode.Create("行为：施加 Debuff (Buff#34)");
                    hitDebuff.NodeId = ExplainNodeId.FromParts("mock_buff", "Buff", "34");
                    hitDebuff.Kind = "effect";
                    hitDebuff.SummaryLines.Add("持续 1.5s / 减速 30%");
                    hitDebuff.Source = ExplainSourceRef.TableRow("Buff", "34");
                    hitDebuff.Actions.Add(ExplainAction.Navigate("打开表行", NavigationTarget.OpenTableRow("Buff", "34")));

                    var hitBuff = ExplainNode.Create("行为：施加 Buff (Buff#35)");
                    hitBuff.NodeId = ExplainNodeId.FromParts("mock_buff", "Buff", "35");
                    hitBuff.Kind = "effect";
                    hitBuff.SummaryLines.Add("持续 3.0s / 增加护盾 80");
                    hitBuff.Source = ExplainSourceRef.TableRow("Buff", "35");
                    hitBuff.Actions.Add(ExplainAction.Navigate("打开表行", NavigationTarget.OpenTableRow("Buff", "35")));

                    onHit.Children.Add(hitCond);
                    onHit.Children.Add(hitDamage);
                    onHit.Children.Add(hitDebuff);
                    onHit.Children.Add(hitBuff);

                    var onDestroy = ExplainNode.Create("触发器：销毁 (OnDestroy)");
                    onDestroy.NodeId = ExplainNodeId.FromParts("mock_trigger", "OnDestroy", rootKey.Id);
                    onDestroy.Kind = "trigger";
                    onDestroy.SummaryLines.Add("当子弹生命周期结束或被销毁时触发");

                    var destroyFx = ExplainNode.Create("行为：播放特效 (Fx#901)");
                    destroyFx.NodeId = ExplainNodeId.FromParts("mock_fx", "Fx", "901");
                    destroyFx.Kind = "effect";
                    destroyFx.SummaryLines.Add("爆炸特效：半径 2.5");
                    destroyFx.Source = ExplainSourceRef.TableRow("Fx", "901");
                    destroyFx.Actions.Add(ExplainAction.Navigate("打开表行", NavigationTarget.OpenTableRow("Fx", "901")));

                    onDestroy.Children.Add(destroyFx);

                    node.Children.Add(onHit);
                    node.Children.Add(onDestroy);
                }
                else if (rootKey.Type == "Trigger" && rootKey.Id == "101")
                {
                    node = ExplainNode.Create($"触发器 Trigger#{rootKey.Id}");
                    node.NodeId = ExplainNodeId.FromParts("mock_trigger_tree", "Trigger", rootKey.Id);
                    node.Kind = "trigger";
                    node.SummaryLines.Add("示例：时间轴直接调用的触发器");
                    node.Source = ExplainSourceRef.TableRow("Trigger", rootKey.Id);
                    node.Actions.Add(ExplainAction.Navigate("打开表行", NavigationTarget.OpenTableRow("Trigger", rootKey.Id)));

                    var cond = ExplainNode.Create("条件：目标存在");
                    cond.NodeId = ExplainNodeId.FromParts("mock_trigger_cond", "Trigger", "101_target");
                    cond.Kind = "condition";
                    cond.SummaryLines.Add("示例条件，仅用于展示");

                    var applyBuff = ExplainNode.Create("行为：施加 Buff (Buff#36)");
                    applyBuff.NodeId = ExplainNodeId.FromParts("mock_buff", "Buff", "36");
                    applyBuff.Kind = "effect";
                    applyBuff.SummaryLines.Add("持续 4.0s / 增加攻击 15%");
                    applyBuff.Source = ExplainSourceRef.TableRow("Buff", "36");
                    applyBuff.Actions.Add(ExplainAction.Navigate("打开表行", NavigationTarget.OpenTableRow("Buff", "36")));

                    node.Children.Add(cond);
                    node.Children.Add(applyBuff);
                }
                else
                {
                    node = ExplainNode.Create($"{rootKey.Type} {rootKey.Id}");
                    node.Kind = "discovered_root";
                    node.SummaryLines.Add("示例：这是自动发现的子树，默认折叠，点击后懒加载展开。");
                    node.Source = ExplainSourceRef.TableRow(rootKey.Type, rootKey.Id);
                    node.Actions.Add(ExplainAction.Navigate("打开表行", NavigationTarget.OpenTableRow(rootKey.Type, rootKey.Id)));
                }

                root = new ExplainTreeRoot
                {
                    Kind = rootKey.Type.ToLowerInvariant(),
                    Key = rootKey,
                    Title = rootKey.ToString(),
                    Root = node
                };

                return true;
            }
        }

        private sealed class MockNavigator : INavigatorEx
        {
            public int Priority => 0;

            public bool CanNavigateExt(NavigationTarget target) => CanNavigate(target);

            public bool CanNavigate(NavigationTarget target) => target != null;

            public void Navigate(NavigationTarget target)
            {
                UnityEngine.Debug.Log($"[AbilityExplain] Navigate: kind={target.Kind}, table={target.TableName}, row={target.RowId}, editor={target.EditorId}");
            }
        }
    }
}
