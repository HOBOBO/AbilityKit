from pathlib import Path

replacements = {
    Path("src/AbilityKit.BehaviorTree.Tests/BtTestSupport.cs"): [
        ("    /// <summary>\n    /// 娴嬭瘯鐢ㄨ剼鏈妭鐐癸細琛屼负瀹屽叏鐢遍粦鏉块┍鍔紙resultKey/condKey锛夛紝鏃犻潤鎬佸彲鍙樼姸鎬侊紝\n    /// 鍏煎 xunit 骞惰銆傚畾涔夊湪娴嬭瘯绋嬪簭闆嗕腑锛屽悓鏃堕獙璇佸寘澶栨墿灞曪紙ScanAssembly + 灞炴€?schema锛夈€?    /// </summary>",
         "    /// <summary>\n    /// 测试用脚本节点：行为完全由黑板驱动（resultKey/condKey），不含静态可变状态，\n    /// 兼容 xUnit 并行执行。定义在测试程序集中，同时验证包外扩展\n    /// （ScanAssembly + 属性 schema）。\n    /// </summary>"),
        ("    /// <summary>娴嬭瘯鐢ㄨ鏁板姩浣滐細Start/Stop 鍚勮嚜绱姞榛戞澘璁℃暟锛岀敤浜庨獙璇佹墽琛岄『搴忎笌涓琛屼负銆?/summary>",
         "    /// <summary>测试用计数动作：Start/Stop 分别累加黑板计数，用于验证执行顺序与中止行为。</summary>"),
        ("    /// <summary>娴嬭瘯鐢ㄨ剼鏈潯浠讹細璇婚粦鏉?bool key銆?/summary>",
         "    /// <summary>测试用脚本条件：读取黑板 bool key。</summary>"),
        ("        /// <summary>娉ㄥ唽娴嬭瘯鑺傜偣鐩綍锛堝惈灞炴€?schema锛夈€?/summary>",
         "        /// <summary>注册测试节点目录（包含属性 schema）。</summary>"),
        ("ScriptedAction, \"鑴氭湰鍔ㄤ綔\", \"娴嬭瘯\"", "ScriptedAction, \"脚本动作\", \"测试\""),
        ("CountingAction, \"璁℃暟鍔ㄤ綔\", \"娴嬭瘯\"", "CountingAction, \"计数动作\", \"测试\""),
        ("ScriptedCondition, \"鑴氭湰鏉′欢\", \"娴嬭瘯\"", "ScriptedCondition, \"脚本条件\", \"测试\""),
        ("    /// <summary>寤烘爲 DSL锛氬揩閫熺粍瑁呮爲瀹氫箟銆?/summary>",
         "    /// <summary>建树 DSL：快速组装树定义。</summary>"),
    ],
    Path("src/AbilityKit.BehaviorTree.Tests/BtAuthoringExportTests.cs"): [
        ("    /// <summary>鎺堟潈鏂囨。 鈫?杩愯鏃?IR 鐨勫鍑虹绾匡細甯冨眬鍓ョ銆乬olden 绋冲畾銆佹牎楠岄棬绂併€乺oundtrip銆?/summary>",
         "    /// <summary>Authoring 文档到运行时 IR 的导出管线：布局剥离、golden 稳定、校验门禁与 roundtrip。</summary>"),
        ("Title = \"鎴樻枟鎰忓浘\"", "Title = \"战斗意图\""),
        ("Text = \"浠呬緵绛栧垝闃呰\"", "Text = \"仅供策划阅读\""),
        ("            // 杩愯鏃?IR 涓嶅惈甯冨眬/鍒嗙粍锛屼篃涓嶅惈鎺堟潈鍏冩暟鎹?            Assert.DoesNotContain(\"\\\"layout\\\"\", json);",
         "            // 运行时 IR 不包含布局、分组、注释或其他编辑器 authoring 元数据。\n            Assert.DoesNotContain(\"\\\"layout\\\"\", json);"),
        ("Assert.DoesNotContain(\"浠呬緵绛栧垝闃呰\", json);", "Assert.DoesNotContain(\"仅供策划阅读\", json);"),
        ("            // 淇敼杩斿洖鍊间笉褰卞搷缂栬緫鎬佹枃妗?            definition.Nodes[1].Type = \"polluted.type\";",
         "            // 修改返回值不会污染编辑态文档。\n            definition.Nodes[1].Type = \"polluted.type\";"),
        ("Assert.Equal(\"鎴樻枟鎰忓浘\", loaded.Groups[0].Title);", "Assert.Equal(\"战斗意图\", loaded.Groups[0].Title);"),
        ("Assert.Equal(\"浠呬緵绛栧垝闃呰\", loaded.Notes[0].Text);", "Assert.Equal(\"仅供策划阅读\", loaded.Notes[0].Text);"),
        ("            // castWait 鎴愬姛 鈫?Selector 瀹屾垚 鈫?hold 鏈繍琛岋紝out.hold 淇濇寔榛樿 true",
         "            // castWait 成功 -> Selector 完成 -> hold 未运行，out.hold 保持默认 true。"),
    ],
    Path("src/AbilityKit.BehaviorTree.Tests/BtExecutionSemanticsTests.cs"): [
        ("    /// <summary>鎵ц璇箟锛氱粍鍚?瑁呴グ鎺ㄨ繘銆佸苟琛屽垎鏀€佹潯浠朵腑鏂紙Self/LowerPriority/Both锛夈€佽楗板櫒鎶㈠崰銆?/summary>",
         "    /// <summary>\n    /// 执行语义：组合/装饰推进、并行分支、条件中止\n    /// （Self/LowerPriority/Both）以及装饰器抢占。\n    /// </summary>"),
        ("// a 澶辫触", "// a 失败"),
        ("// 浠讳竴鍒嗘敮澶辫触 鈫?骞惰鏁翠綋 Failure", "// 任一分支失败 -> 并行整体 Failure。"),
        ("// 鎸佺画澶辫触", "// 持续失败"),
        ("// 鍒濇 + 2 娆￠噸璇?= 3 娆″惎鍔紝涔嬪悗 Failure", "// 初次执行 + 2 次重试 = 3 次启动，之后 Failure。"),
        ("// 瀛愯妭鐐规寔缁?Running", "// 子节点持续 Running"),
        ("// 瀛愯妭鐐硅涓寮瑰嚭", "// 子节点被中止弹出"),
        ("// 鍐峰嵈鏈熷唴", "// 冷却期内"),
        ("// resultOnCooldown 榛樿 Failure", "// resultOnCooldown 默认 Failure"),
        ("// 鍐峰嵈缁撴潫", "// 冷却结束"),
        ("// resultAfterFirst 榛樿 Failure", "// resultAfterFirst 默认 Failure"),
        ("            // Selector(Self)[cond, action]锛歝ond 澶辫触璁╅€夋嫨鍣ㄦ帹杩涘埌 action锛圧unning锛夛紝",
         "            // Selector(Self)[cond, action]：cond 失败让选择器推进到 action（Running）。"),
        ("// 鏉′欢澶辫触 鈫?鎺ㄨ繘鍒?action", "// 条件失败 -> 推进到 action"),
        ("// action 鎸佺画 Running", "// action 持续 Running"),
        ("            // 鏉′欢缈荤湡锛歋elf 涓柇 鈫?action 琚?Stop锛岄€夋嫨鍣ㄩ噸鏂拌瘎浼板悗浠?Success 瀹屾垚",
         "            // 条件翻真：Self 中止 -> action 被 Stop，选择器重新评估后以 Success 完成。"),
        ("// 鏈噸鏂板惎鍔紙閫夋嫨鍣ㄧ洿鎺ュ畬鎴愶級", "// 未重新启动（选择器直接完成）"),
        ("// 楂樹紭鍏堢骇鏉′欢缈荤湡 鈫?浣庡垎鏀涓锛岄珮鍒嗘敮鎺ユ墜", "// 高优先级条件翻真 -> 低分支被中止，高分支接手。"),
        ("// 缈荤湡 鈫?浣庡垎鏀涓", "// 翻真 -> 低分支被中止。"),
        ("// root = Sequence[ inner, c ]; inner = Sequence[ a, b ] 鈥斺€?宓屽缁撴瀯涓嬪瓙鑺傜偣绱㈠紩蹇呴』姝ｇ‘",
         "// root = Sequence[ inner, c ]; inner = Sequence[ a, b ] —— 嵌套结构下子节点索引必须正确。"),
    ],
    Path("src/AbilityKit.BehaviorTree.Tests/BtSubtreeTests.cs"): [
        ("    /// <summary>瀛愭爲寮曠敤鑺傜偣锛氬唴鑱斿睍寮€銆佸墠缂€銆侀粦鏉垮悎骞躲€佺幆妫€娴嬨€佹潵婧愯拷韪€佽繍琛屾椂/蹇収鍏煎銆?/summary>",
         "    /// <summary>\n    /// 子树引用节点：内联展开、前缀、黑板合并、环检测、\n    /// 来源追踪以及运行时/快照兼容。\n    /// </summary>"),
        ("// 琚紩鐢ㄨ妭鐐逛互 \"sub.\" 鍓嶇紑鍐呰仈", "// 被引用节点以 \"sub.\" 前缀内联。"),
        ("// root 鐨勭浜屼釜瀛愯妭鐐硅鏇挎崲涓哄唴鑱旀牴", "// root 的第二个子节点被替换为内联根。"),
        ("// 鏉ユ簮杩借釜", "// 来源追踪。"),
        ("// wait 榛樿 1s", "// wait 默认 1s"),
        ("// 骞堕泦锛氫袱涓?key 閮藉湪", "// 并集：两个 key 都存在。"),
        ("// 鍚屽悕涓嶅悓绫诲瀷 鈫?鍐茬獊", "// 同名不同类型 -> 冲突。"),
        ("// 杩愯鏃剁粡璋冭瘯瑙嗗浘鏆撮湶缁欒瀵熺", "// 运行时通过调试视图暴露给观察端。"),
    ],
}

for path, pairs in replacements.items():
    text = path.read_text(encoding="utf-8-sig")
    original = text
    for old, new in pairs:
        if old not in text:
            raise SystemExit(f"Missing expected text in {path}: {old[:80]!r}")
        text = text.replace(old, new)
    text = text.rstrip() + "\n"
    if text != original:
        path.write_text(text, encoding="utf-8")
        print(f"patched {path}")
    else:
        print(f"unchanged {path}")
