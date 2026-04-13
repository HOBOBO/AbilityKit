using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Config.BattleDemo.MO
{
    public sealed class ComponentTemplateMO
    {
        public int Id { get; }
        public string Name { get; }

        public IReadOnlyList<ComponentOpMO> Ops { get; }

        public ComponentTemplateMO(ComponentTemplateDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            Id = dto.Id;
            Name = dto.Name;

            if (dto.Ops != null && dto.Ops.Length > 0)
            {
                var list = new List<ComponentOpMO>(dto.Ops.Length);
                for (int i = 0; i < dto.Ops.Length; i++)
                {
                    var op = dto.Ops[i];
                    if (op == null) continue;
                    list.Add(new ComponentOpMO(op));
                }
                Ops = list;
            }
            else
            {
                Ops = Array.Empty<ComponentOpMO>();
            }
        }
    }

    public sealed class ComponentOpMO
    {
        public int Kind { get; }
        public int IntValue { get; }
        public float FloatValue { get; }
        public bool BoolValue { get; }

        public ComponentOpMO(ComponentOpDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            Kind = dto.Kind;
            IntValue = dto.IntValue;
            FloatValue = dto.FloatValue;
            BoolValue = dto.BoolValue;
        }
    }
}
