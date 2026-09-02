namespace AbilityKit.EnvironmentModel
{
/// <summary>
/// 已解析环境 Profile 与具体世界/实体运行时之间的适配边界。框架从不知道如何生成一个角色、放一个碰撞体、
/// 挂一个标签或施加一个修饰器——那些是项目的世界装配原语（「原语层」）。项目实现这个接口，
/// 把扁平的关注点/取值选择与构建原语翻译成自己的构造代码，并返回别名 → <typeparamref name="THandle"/> 的绑定结果。
///
/// <para><typeparamref name="THandle"/> 是项目的实体 handle 类型（实体 id / 实体引用 / 接口），框架对其无任何约束、只透传。
/// 这与 DSL 验收层里既有的 <c>IBehaviorProfileBinder</c> 同构，区别在于这里要 <b>返回</b> 绑定结果——
/// 预览/测试会话需要知道「binder 生成了谁」，才能继续施放技能并观测。</para>
/// </summary>
public interface IEnvironmentProfileBinder<THandle>
{
    /// <summary>把解析后的环境构建进项目世界，并返回别名 → handle 的绑定结果。</summary>
    EnvironmentBindResult<THandle> Bind(in ResolvedEnvironmentProfile profile);
}
}
