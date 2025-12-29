using System.Collections;
using System.Collections.Generic;
using NBC.ActionEditorExample;
using UnityEngine;

public class Init : MonoBehaviour
{
    void Start()
    {
        //添加个模拟配置
        SkillConfig skillConfig = new SkillConfig
        {
            Id = 66,
            Atk = 666,
            Def = 88,
            EventName = "1001"
        };

        //添加个模拟技能
        SkillPlayAttack skill = new SkillPlayAttack
        {
            SkillConfig = skillConfig
        };

        var skeleton = GameObject.Find("Skeleton");
        var hero = skeleton.AddComponent<HeroPlayer>(); //演示的role脚本直接继承MonoManager，减小代码量
        hero.AddSkill(skill);
        
        //释放技能
        hero.UseSkill(66);
    }
}