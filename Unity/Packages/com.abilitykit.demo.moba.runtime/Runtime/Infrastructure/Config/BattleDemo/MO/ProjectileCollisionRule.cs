using System;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba.Share.Config;

namespace AbilityKit.Demo.Moba.Config.BattleDemo.MO
{
    /// <summary>Immutable launch-time response rule. Zero classification values match any.</summary>
    public readonly struct ProjectileCollisionRule
    {
        public readonly ProjectileCollisionRole Role;
        public readonly EntityMainType MainType;
        public readonly UnitSubType UnitSubType;
        public readonly ProjectileCollisionRelation Relation;
        public readonly ProjectileCollisionResponse Response;

        public ProjectileCollisionRule(ProjectileCollisionRuleDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Role = dto.Role;
            MainType = (EntityMainType)dto.MainType;
            UnitSubType = (UnitSubType)dto.UnitSubType;
            Relation = dto.Relation;
            Response = (ProjectileCollisionResponse)dto.Response;
        }

        public bool TryValidate(out string error)
        {
            error = null;
            if (!Enum.IsDefined(typeof(ProjectileCollisionRole), Role) ||
                !Enum.IsDefined(typeof(EntityMainType), MainType) ||
                !Enum.IsDefined(typeof(UnitSubType), UnitSubType) ||
                !Enum.IsDefined(typeof(ProjectileCollisionRelation), Relation) ||
                !Enum.IsDefined(typeof(ProjectileCollisionResponse), Response))
                error = "Collision rule contains an unknown role, classification, relation or response.";
            else if (Role == ProjectileCollisionRole.StaticWorld &&
                     (Response == ProjectileCollisionResponse.Hit || MainType != 0 || UnitSubType != 0 || Relation != 0))
                error = "StaticWorld rules only support Ignore/Block without Actor classification or relation.";
            else if (MainType != 0 && MainType != EntityMainType.Unit && UnitSubType != 0)
                error = "UnitSubType cannot be combined with a non-Unit MainType.";
            return error == null;
        }
    }
}
