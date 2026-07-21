# Time / WorldStart

源文件：`Runtime/Time/*.cs` + `Runtime/WorldStart/*.cs`

## FixedStepTickRunner（非 module，工具类）

`Runtime/Time/FixedStepTickRunner.cs`

```csharp
public sealed class FixedStepTickRunner {
    public float SpeedMultiplier { get; set; }
    public int Frame { get; }
    public float Time { get; }

    public int Step(float elapsedSeconds, Action<float> tick);   // 返回执行的帧数
    public void RunFrames(int frames, Action<float> tick);
    public void Reset(int frame = 0, float time = 0f);
}
```

固定步长 tick runner，累积 elapsed time 后按 fixed delta 推进多帧。

## ServerFrameTimeModule

详见 [host_modules.md](host_modules.md)。

关键点：**弱依赖** `IFrameSyncDriverEvents`。若存在则挂在 PostStep（与帧同步对齐），否则降级到 `options.PostTick`（host tick 对齐）。

`OnBeforeCreateWorld`：注册 `IFrameTime`（`FrameTime` 实例）到 `options.ServiceBuilder`。

`TryGet(WorldId, out IFrameTime)`：取 world-scoped 时间服务。

## WorldAutoStartModule

详见 [host_modules.md](host_modules.md)。

## IWorldAutoStartHandler（world-scoped service）

`Runtime/WorldStart/IWorldAutoStartHandler.cs`

```csharp
public interface IWorldAutoStartHandler : IService {
    bool TryAutoStart(IWorld world, float deltaTime);
}
```

由应用层实现（如 moba 的 `MobaWorldAutoStartHandler`），返回 `true` 表示 world 已启动成功，`WorldAutoStartModule` 不再重试。
