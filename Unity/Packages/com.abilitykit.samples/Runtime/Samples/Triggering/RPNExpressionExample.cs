#nullable enable

using System;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Triggering
{
    /// <summary>
    /// RPNExpressionExample - RPN 逆波兰表达式示例
    /// 演示 Triggering 模块中的 RPN 表达式求值系统
    /// </summary>
    [Sample]
    public sealed class RPNExpressionExample : SampleBase
    {
        public override string Title => "RPN Expression";
        public override string Description => "演示 RPN 逆波兰表达式求值、变量引用、函数调用";
        public override SampleCategory Category => SampleCategory.Triggering;

        protected override void OnRun()
        {
            Log("=== RPN 逆波兰表达式示例 ===");
            Output.Divider();

            // 1. 什么是 RPN
            Log("【1】什么是 RPN 表达式");
            Output.Bullet("RPN = Reverse Polish Notation (逆波兰表达式)");
            Output.Bullet("又称后缀表达式，运算符在操作数之后");
            Output.Bullet("无需括号即可表达复杂表达式");
            Output.Bullet("求值算法简单高效，适合运行时解析");
            Log("");

            // 2. 中缀 vs RPN 对照
            Log("【2】中缀表达式 vs RPN");
            Log("");
            Log("  中缀表达式: (a + b) * c");
            Log("  RPN: a b + c *");
            Log("");
            Log("  中缀表达式: (a > 10) AND (b < 20)");
            Log("  RPN: a 10 > b 20 < AND");
            Log("");
            Log("  中缀表达式: a ? b : c");
            Log("  RPN: a b c ?:");
            Log("");

            // 3. 支持的操作符
            Log("【3】支持的运算符");
            Log("");
            Log("  算术运算符:");
            Log("    +   -   *   /   %     (加减乘除取模)");
            Log("    ^                 (幂运算)");
            Log("    -                  (取反)");
            Log("");
            Log("  比较运算符:");
            Log("    >   <   >=  <=  ==  !=    (大于/小于/等于)");
            Log("");
            Log("  逻辑运算符:");
            Log("    AND  OR   NOT  XOR          (与/或/非/异或)");
            Log("");
            Log("  特殊运算符:");
            Log("    ?:  (三元条件)");
            Log("    IN  (在范围内)");
            Log("    MIN MAX (最小/最大值)");
            Log("");

            // 4. 变量引用
            Log("【4】变量引用 (ValueRef)");
            Log("");
            Log("  Payload 字段引用:");
            Log("    payload.amount      - 事件载荷的 amount 字段");
            Log("    payload.target.hp   - 嵌套字段访问");
            Log("");
            Log("  Blackboard 引用:");
            Log("    bb:combo:count     - 黑板变量");
            Log("    bb:player:health   - 玩家生命值");
            Log("");
            Log("  常量:");
            Log("    100                 - 整数常量");
            Log("    3.14                - 浮点常量");
            Log("    \"Hello\"             - 字符串常量");
            Log("    true / false        - 布尔常量");
            Log("");

            // 5. RPN 函数
            Log("【5】RPN 函数");
            Output.Bullet("ABS(x) - 绝对值");
            Output.Bullet("FLOOR(x) / CEIL(x) - 向下/向上取整");
            Output.Bullet("ROUND(x, n) - 四舍五入");
            Output.Bullet("MIN(a, b, ...) / MAX(a, b, ...) - 最小/最大值");
            Output.Bullet("CLAMP(x, min, max) - 限制范围");
            Output.Bullet("LERP(a, b, t) - 线性插值");
            Output.Bullet("RANDOM(min, max) - 随机数");
            Output.Bullet("DISTANCE(x1, y1, x2, y2) - 距离计算");
            Log("");

            // 6. 代码示例 - 表达式解析
            Log("【6】代码示例 - 表达式解析");
            Log("");
            Log("  // 创建表达式上下文");
            Log("  var context = new ExpressionContext();");
            Log("  context.Variables[\"a\"] = 10f;");
            Log("  context.Variables[\"b\"] = 20f;");
            Log("");
            Log("  // 解析并求值");
            Log("  var expr = RPNExpression.Parse(\"a b + 2 *\");");
            Log("  float result = expr.Evaluate(context);  // (10 + 20) * 2 = 60");
            Log("");

            // 7. 代码示例 - 在 Predicate 中使用
            Log("【7】代码示例 - 在 Predicate 中使用");
            Log("");
            Log("  // 创建表达式条件");
            Log("  var predicate = new ExpressionPredicate(");
            Log("      \"payload.amount 100 > caster.mp 50 >= AND\"");
            Log("  );");
            Log("");
            Log("  // 等价于:");
            Log("  // if (payload.amount > 100 && caster.mp >= 50)");
            Log("");

            // 8. 复杂表达式示例
            Log("【8】复杂表达式示例");
            Log("");
            Log("  // 计算最终伤害");
            Log("  // finalDamage = baseDamage * (1 + bonusPercent) * comboMultiplier");
            Log("  \"base_damage 100 * (1 bonus_percent +) combo_mult *\"");
            Log("");
            Log("  // 复杂条件判断");
            Log("  // if (hp < maxHp * 0.3 && !hasShield && distance < 10)");
            Log("  \"hp max_hp 0.3 * < has_shield NOT AND distance 10 < AND\"");
            Log("");
            Log("  // 技能冷却检查");
            Log("  // if (skillCooldown <= 0 && mana >= skillCost)");
            Log("  \"skill_cooldown 0 <= skill_mana skill_cost >= AND\"");
            Log("");

            // 9. 在 TriggerPlanConfig 中使用
            Log("【9】TriggerPlanConfig 中的使用");
            Log("");
            Log("  // JSON 配置示例");
            Log("  {");
            Log("    \"triggerId\": 1001,");
            Log("    \"predicate\": {");
            Log("      \"type\": \"ExpressionPredicate\",");
            Log("      \"expression\": \"payload.damage 50 >\"");
            Log("    },");
            Log("    \"actions\": [");
            Log("      { \"type\": \"Call\", \"function\": \"spawn_effect\" }");
            Log("    ]");
            Log("  }");
            Log("");

            // 10. 性能优化
            Log("【10】性能优化建议");
            Output.Bullet("频繁使用的表达式可以预编译缓存");
            Output.Bullet("避免在表达式中进行复杂计算");
            Output.Bullet("使用局部变量减少重复访问");
            Output.Bullet("布尔表达式使用短路求值");
            Log("");

            // 11. API 参考
            Log("【11】关键 API 参考");
            Output.Bullet("AbilityKit.Triggering.Variables.Numeric.Expression");
            Output.Bullet("INumericRpnFunctionRegistry - 函数注册表");
            Output.Bullet("DefaultNumericRpnFunctions - 默认函数集");
            Output.Bullet("INumericVarDomainRegistry - 变量域注册表");
            Log("");

            Output.Divider();
            Log("【总结】RPN 表达式提供声明式的条件配置能力，是 Trigger 系统配置化的核心");
        }
    }
}
