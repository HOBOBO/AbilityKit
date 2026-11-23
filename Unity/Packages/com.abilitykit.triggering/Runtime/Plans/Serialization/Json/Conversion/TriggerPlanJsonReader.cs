using System;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Runtime.Config.Plans;

namespace AbilityKit.Triggering.Runtime.Plan.Json
{
    /// <summary>
    /// 将 JSON 文档转成可查询的运行时记录；完整转换成功后才返回新存储。
    /// </summary>
    internal static class TriggerPlanJsonReader
    {
        private static readonly TriggerPlanJsonParser _parser = new TriggerPlanJsonParser();
        private static readonly TriggerPlanConverter _converter = new TriggerPlanConverter();

        internal static TriggerPlanRecordStore<TriggerPlanJsonDatabase.Record> Read(
            string json, string sourceName, Func<TriggerCueDescriptor, ITriggerCue> createCue,
            out TriggerPlanJsonDatabase.TriggerPlanDatabaseDto dto)
        {
            if (string.IsNullOrEmpty(json))
                throw new InvalidOperationException($"Trigger plan json is empty: {sourceName ?? "<json>"}");

            var result = _parser.Parse(json, sourceName);
            dto = result.Dto;
            if (result.Success && dto != null)
                return Read(dto, createCue);

            var error = result.FirstError;
            var message = string.IsNullOrEmpty(error.Message)
                ? "Unknown trigger plan json parse error"
                : error.Message;
            throw new InvalidOperationException($"Failed to parse trigger plan json: {sourceName ?? "<json>"}. {message}", error.Exception);
        }

        internal static TriggerPlanRecordStore<TriggerPlanJsonDatabase.Record> Read(
            TriggerPlanJsonDatabase.TriggerPlanDatabaseDto dto, Func<TriggerCueDescriptor, ITriggerCue> createCue)
        {
            dto = TriggerPlanModuleExpander.Expand(dto);
            var store = new TriggerPlanRecordStore<TriggerPlanJsonDatabase.Record>(
                record => (record.TriggerId, record.ExecutionRoot), dto?.Strings, dto?.Blackboards);
            if (dto?.Triggers == null) return store;

            foreach (var trigger in dto.Triggers)
            {
                if (trigger == null || trigger.TriggerId <= 0) continue;

                var eventId = trigger.EventId;
                if (eventId == 0 && !string.IsNullOrEmpty(trigger.EventName))
                    eventId = StableStringId.Get("event:" + trigger.EventName);

                var cueDescriptor = new TriggerCueDescriptor(
                    kind: trigger.CueId,
                    cueId: trigger.CueId,
                    primaryAssetId: trigger.CuePrimaryAssetId,
                    secondaryAssetId: trigger.CueSecondaryAssetId,
                    payload: trigger.CuePayload);
                var cue = createCue(cueDescriptor) ?? NullTriggerCue.Instance;
                var plan = _converter.Convert(trigger, cue);
                var executionRoot = _converter.ConvertExecutionRoot(trigger, dto);
                var scope = Enum.IsDefined(typeof(TriggerPlanScope), trigger.Scope)
                    ? trigger.Scope
                    : TriggerPlanScope.Global;
                var record = new TriggerPlanJsonDatabase.Record(
                    trigger.TriggerId, trigger.EventName, eventId, scope, in plan, executionRoot);
                store.Append(in record);
            }

            return store;
        }
    }
}
