#if UNITY_EDITOR
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Panels;
using AbilityKit.Ability.Editor.Utilities;
using NUnit.Framework;

namespace AbilityKit.Ability.Editor.Tests
{
    public sealed class TriggerAuthoringTemplateMatrixTests
    {
        [Test]
        public void BuildRows_FiltersByTemplateAndKeepsDeclaredParameterOrder()
        {
            var first = Instance(20, "damage.function");
            Bind(first, "amount", ConstantNumber(2));
            var second = Instance(10, "other.function");
            var entries = new[]
            {
                Entry(4, first),
                Entry(1, second)
            };
            var parameters = new[]
            {
                Parameter("amount", TriggerValueType.Number),
                Parameter("count", TriggerValueType.Integer)
            };

            var rows = TriggerAuthoringTemplateMatrixModel.BuildRows(
                entries, "damage.function", "2", 0, true, parameters);

            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0].Index, Is.EqualTo(4));
            Assert.That(rows[0].FindBinding("amount").Value.NumberValue, Is.EqualTo(2));
            Assert.That(rows[0].FindBinding("count"), Is.Null);
        }

        [Test]
        public void ValueCodec_ParsesEverySupportedReferenceSource()
        {
            var parameter = Parameter("amount", TriggerValueType.Number);
            parameter.AllowedSources = TriggerTemplateValueSourceMask.InstanceBinding;

            AssertValue("local:trigger:bonus", parameter, TriggerValueSource.LocalBlackboard, "trigger:bonus");
            AssertValue("global:damage_scale", parameter, TriggerValueSource.GlobalBlackboard, "damage_scale");
            AssertValue("payload:amount", parameter, TriggerValueSource.Payload, "amount");
            AssertValue("context:source", parameter, TriggerValueSource.Context, "source");
            Assert.That(TriggerAuthoringTemplateValueTextCodec.TryParse(
                "expr:base * scale", parameter, out var expression, out var error), Is.True, error);
            Assert.That(expression.Source, Is.EqualTo(TriggerValueSource.Expression));
            Assert.That(expression.Expression, Is.EqualTo("base * scale"));
        }

        [Test]
        public void PastePlan_MatchesTriggerIdInsteadOfTsvRowOrder()
        {
            var template = Template(Parameter("amount", TriggerValueType.Number));
            var first = Instance(10, template.TemplateId);
            var second = Instance(20, template.TemplateId);
            Bind(first, "amount", ConstantNumber(1));
            Bind(second, "amount", ConstantNumber(2));
            var tsv = "TriggerId\t名称\t业务分组\tamount\n" +
                      "20\t第二条改名\t技能/强化\t200.5\n" +
                      "10\t第一条改名\t技能/普通\t100.5";

            var plan = TriggerAuthoringTemplateBindingPastePlan.Create(
                tsv, new[] { first, second }, template);

            Assert.That(plan.Errors, Is.Empty);
            Assert.That(plan.ChangedTriggerCount, Is.EqualTo(2));
            plan.Apply();
            Assert.That(first.Name, Is.EqualTo("第一条改名"));
            Assert.That(first.Template.Bindings[0].Value.NumberValue, Is.EqualTo(100.5));
            Assert.That(second.Name, Is.EqualTo("第二条改名"));
            Assert.That(second.Template.Bindings[0].Value.NumberValue, Is.EqualTo(200.5));
        }

        [Test]
        public void PastePlan_EmptyCellDoesNotModifyAndDefaultMarkerRemovesOverride()
        {
            var amount = Parameter("amount", TriggerValueType.Number);
            amount.Required = false;
            amount.HasDefault = true;
            amount.DefaultValue = ConstantNumber(5);
            var count = Parameter("count", TriggerValueType.Integer);
            var template = Template(amount, count);
            var trigger = Instance(10, template.TemplateId);
            Bind(trigger, "amount", ConstantNumber(12));
            Bind(trigger, "count", ConstantInteger(3));

            var plan = TriggerAuthoringTemplateBindingPastePlan.Create(
                "TriggerId\tamount\tcount\n10\t<default>\t",
                new[] { trigger },
                template);

            Assert.That(plan.Errors, Is.Empty);
            Assert.That(plan.ChangeCount, Is.EqualTo(1));
            plan.Apply();
            Assert.That(Find(trigger, "amount"), Is.Null);
            Assert.That(Find(trigger, "count").Value.IntegerValue, Is.EqualTo(3));
        }

        [Test]
        public void PastePlan_RejectsUnknownDuplicateAndDisallowedSources()
        {
            var amount = Parameter("amount", TriggerValueType.Number);
            amount.AllowedSources = TriggerTemplateValueSourceMask.Constant;
            var template = Template(amount);
            var trigger = Instance(10, template.TemplateId);
            var tsv = "TriggerId\tamount\n" +
                      "10\tglobal:damage\n" +
                      "10\t2\n" +
                      "99\t3";

            var plan = TriggerAuthoringTemplateBindingPastePlan.Create(tsv, new[] { trigger }, template);

            Assert.That(plan.Errors, Has.Count.EqualTo(3));
            Assert.That(plan.ChangeCount, Is.EqualTo(0));
        }

        [Test]
        public void PastePlan_PreviewDoesNotMutateNullBindings()
        {
            var template = Template(Parameter("amount", TriggerValueType.Number));
            var trigger = Instance(10, template.TemplateId);
            trigger.Template.Bindings = null;

            var plan = TriggerAuthoringTemplateBindingPastePlan.Create(
                "TriggerId\tamount\n10\t25",
                new[] { trigger },
                template);

            Assert.That(plan.Errors, Is.Empty);
            Assert.That(trigger.Template.Bindings, Is.Null, "预览阶段不得写入实例");
            plan.Apply();
            Assert.That(trigger.Template.Bindings, Has.Count.EqualTo(1));
            Assert.That(trigger.Template.Bindings[0].Value.NumberValue, Is.EqualTo(25));
        }

        [Test]
        public void PastePlan_DuplicateModuleTriggerIdDoesNotWriteEitherInstance()
        {
            var template = Template(Parameter("amount", TriggerValueType.Number));
            var first = Instance(10, template.TemplateId);
            var second = Instance(10, template.TemplateId);

            var plan = TriggerAuthoringTemplateBindingPastePlan.Create(
                "TriggerId\tamount\n10\t25",
                new[] { first, second },
                template);

            Assert.That(plan.Errors, Has.Count.GreaterThanOrEqualTo(2));
            Assert.That(plan.CanApply, Is.False);
            Assert.That(first.Template.Bindings, Is.Empty);
            Assert.That(second.Template.Bindings, Is.Empty);
        }

        [Test]
        public void TsvCodec_RoundTripsQuotedExcelCells()
        {
            var template = Template(Parameter("label", TriggerValueType.String));
            var trigger = Instance(10, template.TemplateId);
            trigger.Name = "包含\t制表符";
            Bind(trigger, "label", new TriggerValueRefData
            {
                Source = TriggerValueSource.Constant,
                Type = TriggerValueType.String,
                StringValue = "第一行\n第二行"
            });
            var row = new TriggerAuthoringTemplateMatrixRow { RowId = 1, Index = 0, Trigger = trigger };

            var text = TriggerAuthoringTemplateBindingTsvCodec.Build(new[] { row }, template);
            Assert.That(TriggerAuthoringTemplateBindingTsvCodec.TryParseTable(
                text, out var rows, out var error), Is.True, error);

            Assert.That(rows, Has.Count.EqualTo(2));
            Assert.That(rows[1][1], Is.EqualTo("包含\t制表符"));
            Assert.That(rows[1][3], Is.EqualTo("const:第一行\n第二行"));
        }

        private static void AssertValue(
            string text,
            TriggerAuthoringTemplateParameterData parameter,
            TriggerValueSource source,
            string path)
        {
            Assert.That(TriggerAuthoringTemplateValueTextCodec.TryParse(
                text, parameter, out var value, out var error), Is.True, error);
            Assert.That(value.Source, Is.EqualTo(source));
            Assert.That(value.Path, Is.EqualTo(path));
        }

        private static TriggerAuthoringTemplateData Template(
            params TriggerAuthoringTemplateParameterData[] parameters)
        {
            return new TriggerAuthoringTemplateData
            {
                TemplateId = "damage.function",
                Parameters = new List<TriggerAuthoringTemplateParameterData>(parameters)
            };
        }

        private static TriggerAuthoringTemplateParameterData Parameter(string name, TriggerValueType type)
        {
            return new TriggerAuthoringTemplateParameterData
            {
                Name = name,
                Type = type,
                AllowedSources = TriggerTemplateValueSourceMask.InstanceBinding
            };
        }

        private static TriggerDefinitionData Instance(int id, string templateId)
        {
            return new TriggerDefinitionData
            {
                Id = id,
                Name = "Trigger " + id,
                Template = new TriggerTemplateReferenceData { TemplateId = templateId }
            };
        }

        private static TriggerAuthoringTriggerIndex.Entry Entry(int index, TriggerDefinitionData trigger)
        {
            return new TriggerAuthoringTriggerIndex.Entry(
                index,
                trigger,
                new TriggerAuthoringTriggerIndex.DiagnosticSummary(0, 0));
        }

        private static void Bind(TriggerDefinitionData trigger, string name, TriggerValueRefData value)
        {
            trigger.Template.Bindings.Add(new TriggerArgumentData { Name = name, Value = value });
        }

        private static TriggerArgumentData Find(TriggerDefinitionData trigger, string name)
        {
            for (var i = 0; i < trigger.Template.Bindings.Count; i++)
                if (trigger.Template.Bindings[i].Name == name) return trigger.Template.Bindings[i];
            return null;
        }

        private static TriggerValueRefData ConstantNumber(double value)
        {
            return new TriggerValueRefData
            {
                Source = TriggerValueSource.Constant,
                Type = TriggerValueType.Number,
                NumberValue = value
            };
        }

        private static TriggerValueRefData ConstantInteger(long value)
        {
            return new TriggerValueRefData
            {
                Source = TriggerValueSource.Constant,
                Type = TriggerValueType.Integer,
                IntegerValue = value
            };
        }
    }
}
#endif
