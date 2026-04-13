using System;
using AbilityKit.Demo.Moba.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Demo.Moba;
using DamageType = AbilityKit.Demo.Moba.DamageType;
using CritType = AbilityKit.Demo.Moba.CritType;
using DamageReasonKind = AbilityKit.Demo.Moba.DamageReasonKind;
using DamageFormulaKind = AbilityKit.Demo.Moba.DamageFormulaKind;

namespace AbilityKit.Demo.Moba.Triggering.DamageActions
{
    public static class DamageActionSpecParser
    {
        public static DamageActionSpec ParseGiveDamage(ActionDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var args = def.Args;
            if (args == null) throw new InvalidOperationException("give_damage requires args");

            var spec = new DamageActionSpec
            {
                Rate = 1f,
                DamageType = DamageType.Physical,
                CritType = CritType.None,
                ReasonKind = DamageReasonKind.Skill,
                ReasonParam = 0,
                FormulaKind = (int)DamageFormulaKind.Standard,
                UseProjectileHitDecay = false,
                TargetMode = DamageActionSpec.DamageTargetMode.Explicit,
            };

            if (args.TryGetValue("value", out var vObj) && vObj != null)
            {
                spec.Value = vObj is float f ? f : vObj is int i ? i : Convert.ToSingle(vObj);
            }

            if (args.TryGetValue("damageType", out var dtObj) && dtObj != null)
            {
                spec.DamageType = TriggerActionArgUtil.ParseEnum(dtObj, DamageType.Physical);
            }

            if (args.TryGetValue("crit", out var cObj) && cObj != null)
            {
                spec.CritType = TriggerActionArgUtil.ParseEnum(cObj, CritType.None);
            }
            else if (args.TryGetValue("isCritical", out var isCritObj) && isCritObj is bool b && b)
            {
                spec.CritType = CritType.Critical;
            }

            if (args.TryGetValue("reasonKind", out var rkObj) && rkObj != null)
            {
                spec.ReasonKind = TriggerActionArgUtil.ParseEnum(rkObj, DamageReasonKind.Skill);
            }

            if (args.TryGetValue("reasonParam", out var rpObj) && rpObj != null)
            {
                spec.ReasonParam = rpObj is int rpi ? rpi : rpObj is long rpl ? (int)rpl : Convert.ToInt32(rpObj);
            }

            if (args.TryGetValue("formulaKind", out var fkObj) && fkObj != null)
            {
                spec.FormulaKind = (int)TriggerActionArgUtil.ParseEnum(fkObj, DamageFormulaKind.Standard);
            }

            spec.TargetKey = args.TryGetValue("targetKey", out var tkObj) && tkObj is string tks && !string.IsNullOrEmpty(tks) ? tks : null;
            spec.AttackerKey = args.TryGetValue("attackerKey", out var akObj) && akObj is string aks && !string.IsNullOrEmpty(aks) ? aks : null;

            if (args.TryGetValue("queryTemplateId", out var qObj) && qObj != null)
            {
                if (qObj is int qi) spec.QueryTemplateId = qi;
                else if (qObj is long ql) spec.QueryTemplateId = (int)ql;
                else if (qObj is string qs && int.TryParse(qs, out var parsed)) spec.QueryTemplateId = parsed;
            }

            if (args.TryGetValue("targetMode", out var tmObj) && tmObj != null)
            {
                spec.TargetMode = TriggerActionArgUtil.ParseEnum(tmObj, DamageActionSpec.DamageTargetMode.Explicit);
            }
            else
            {
                spec.TargetMode = spec.QueryTemplateId > 0 ? DamageActionSpec.DamageTargetMode.QueryTemplate : DamageActionSpec.DamageTargetMode.Explicit;
            }

            spec.AimPosKey = args.TryGetValue("aimPosKey", out var apObj) && apObj is string aps && !string.IsNullOrEmpty(aps) ? aps : null;
            spec.Log = args.TryGetValue("log", out var logObj) && logObj is bool lb && lb;

            return spec;
        }

        public static DamageActionSpec ParseTakeDamage(ActionDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var args = def.Args;
            if (args == null) throw new InvalidOperationException("take_damage requires args");

            var spec = new DamageActionSpec
            {
                Rate = 1f,
                DamageType = DamageType.Physical,
                CritType = CritType.None,
                ReasonKind = DamageReasonKind.Buff,
                ReasonParam = 0,
                FormulaKind = (int)DamageFormulaKind.Standard,
                UseProjectileHitDecay = true,
                TargetMode = DamageActionSpec.DamageTargetMode.Explicit,
            };

            if (args.TryGetValue("value", out var vObj) && vObj != null)
            {
                spec.Value = vObj is float f ? f : vObj is int i ? i : Convert.ToSingle(vObj);
            }

            if (args.TryGetValue("rate", out var rObj) && rObj != null)
            {
                spec.Rate = rObj is float rf ? rf : rObj is int ri ? ri : Convert.ToSingle(rObj);
            }

            if (args.TryGetValue("damageType", out var dtObj) && dtObj != null)
            {
                spec.DamageType = TriggerActionArgUtil.ParseEnum(dtObj, DamageType.Physical);
            }

            if (args.TryGetValue("crit", out var cObj) && cObj != null)
            {
                spec.CritType = TriggerActionArgUtil.ParseEnum(cObj, CritType.None);
            }

            if (args.TryGetValue("reasonKind", out var rkObj) && rkObj != null)
            {
                spec.ReasonKind = TriggerActionArgUtil.ParseEnum(rkObj, DamageReasonKind.Buff);
            }

            if (args.TryGetValue("reasonParam", out var rpObj) && rpObj != null)
            {
                spec.ReasonParam = rpObj is int rpi ? rpi : rpObj is long rpl ? (int)rpl : Convert.ToInt32(rpObj);
            }

            if (args.TryGetValue("formulaKind", out var fkObj) && fkObj != null)
            {
                spec.FormulaKind = (int)TriggerActionArgUtil.ParseEnum(fkObj, DamageFormulaKind.Standard);
            }

            spec.AttackerKey = args.TryGetValue("attackerKey", out var akObj) && akObj is string aks && !string.IsNullOrEmpty(aks) ? aks : null;
            spec.TargetKey = args.TryGetValue("targetKey", out var tkObj) && tkObj is string tks && !string.IsNullOrEmpty(tks) ? tks : null;

            if (args.TryGetValue("targetMode", out var tmObj) && tmObj != null)
            {
                spec.TargetMode = TriggerActionArgUtil.ParseEnum(tmObj, DamageActionSpec.DamageTargetMode.Explicit);
            }
            else
            {
                spec.TargetMode = DamageActionSpec.DamageTargetMode.Explicit;
            }

            return spec;
        }
    }
}
