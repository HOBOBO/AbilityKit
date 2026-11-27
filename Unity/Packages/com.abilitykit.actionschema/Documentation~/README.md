# ActionSchema 使用指南

> 文档类型：使用指南。适用版本见 [package.json](../package.json)。
> 核校状态：2026-11-25，核校 DTO 分层、严格加载和本地示例；不覆盖 Editor 导出和技能执行。

ActionSchema 提供纯 C# 时间线 DTO、JSON 读取与分层校验，不负责编辑器资产保存、文件发布或战斗执行。旧说明保留在[包内设计](../Document/ActionSchema动作时间线数据模块开发设计文档.md)。

1. 编写一份 DTO，活动 Clip 显式声明 `logic` 或 `presentation`。使用 `ActionTimelinePartition.Create` 生成两份产物；它会校验所有活动 Clip，再按层筛选，复制参数字典，不修改源 DTO。
2. 生产者完成序列化后，消费者用 `LoadForRuntime(json, runtimeType)` 校验版本 `1`、顶层运行类型和活动 Clip。旧 `LoadFromJson` / `LoadFromFile` 是普通反序列化，不提供这些准入保证。
3. 校验通过不等于能执行：具体 Clip、参数与资源由各运行时决定。发布后的可变 DTO 和集合按只读使用。公共 `TimelinePlayer` 只是 `TriggerLog` 触发示例，不是通用技能或动画播放器。

## 完整示例

本地 .NET Console，引用 ActionSchema 镜像和它的 Newtonsoft.Json 依赖。直接构造最小 DTO，不读取项目生产资产。

```csharp
using System;
using System.IO;
using AbilityKit.ActionSchema;
using Newtonsoft.Json;

var source = new SkillAssetDto { length = 1f };
var group = new GroupDto { active = true };
var track = new TrackDto { active = true };
group.tracks.Add(track);
source.groups.Add(group);
track.clips.Add(new ClipDto { type = "Sample.TriggerLog", runtimeType = ActionTimelineRuntimeTypes.Logic });
track.clips.Add(new ClipDto { type = "Sample.PlayAnimation", runtimeType = ActionTimelineRuntimeTypes.Presentation });

var logicJson = JsonConvert.SerializeObject(ActionTimelinePartition.Create(source, ActionTimelineRuntimeTypes.Logic));
var presentationJson = JsonConvert.SerializeObject(ActionTimelinePartition.Create(source, ActionTimelineRuntimeTypes.Presentation));
var logic = ActionTimelineJson.LoadForRuntime(logicJson, ActionTimelineRuntimeTypes.Logic);
var presentation = ActionTimelineJson.LoadForRuntime(presentationJson, ActionTimelineRuntimeTypes.Presentation);
if (logic.groups[0].tracks[0].clips.Count != 1 || presentation.groups[0].tracks[0].clips.Count != 1 ||
    track.clips.Count != 2)
    throw new Exception("分层结果或源数据不符合预期");

try
{
    ActionTimelineJson.LoadForRuntime(presentationJson, ActionTimelineRuntimeTypes.Logic);
    throw new Exception("错误的运行侧不应加载成功");
}
catch (InvalidDataException)
{
    Console.WriteLine("ActionSchema: partition, load and wrong-runtime rejection OK");
}
```

预期输出末行。示例只证明类型分层和加载；不检查实际效果 ID、动画资源、数据发布时间与回滚。源码见 [ActionTimelineRuntime](../Runtime/Ability/Share/ActionSchema/ActionTimelineRuntime.cs) 与 [DTO](../Runtime/Ability/Share/ActionSchema/ActionTimelineDtos.cs)。
