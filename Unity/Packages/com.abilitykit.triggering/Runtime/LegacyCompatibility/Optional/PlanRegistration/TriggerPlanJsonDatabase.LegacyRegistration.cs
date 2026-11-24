using System;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Runtime.Config.Plans;

namespace AbilityKit.Triggering.Runtime.Plan.Json
{
    public sealed partial class TriggerPlanJsonDatabase
    {
        /// <summary>
        /// 旧批量入口：将全局作用域的记录按扁平计划注册，不返回订阅句柄。
        /// </summary>
        /// <remarks>
        /// 保留历史调用语义。新接入应在宿主读取并校验记录，按计划形态注册并持有句柄。
        /// </remarks>
        public void RegisterAll<TCtx>(TriggerRunner<TCtx> runner)
        {
            RegisterAll(runner, TriggerPlanScope.Global);
        }

        /// <summary>
        /// 旧批量入口：按记录顺序注册指定作用域的扁平计划，跳过无事件 ID 的记录。
        /// </summary>
        /// <remarks>
        /// 使用 object 事件参数，不执行记录的执行根，也不返回句柄。中途失败不撤销已登记的订阅。
        /// 不作为执行树或需要受控订阅生命周期的注册入口。
        /// </remarks>
        public void RegisterAll<TCtx>(TriggerRunner<TCtx> runner, TriggerPlanScope scope)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));

            for (int i = 0; i < _store.Records.Count; i++)
            {
                var record = _store.Records[i];
                if (record.EventId == 0) continue;
                if (record.Scope != scope) continue;
                var key = new EventKey<object>(record.EventId);
                runner.RegisterPlan<object, TCtx>(key, record.Plan);
            }
        }
    }
}
