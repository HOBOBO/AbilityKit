using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Config
{
    /// <summary>
    /// 暂存单表增量更新，先验证替换能力，再按数据库既定顺序提交。
    /// </summary>
    internal sealed class PreparedIncrementalTableUpdate
    {
        private readonly ConfigTableDefinition _definition;
        private readonly bool _replacesExisting;
        private readonly object _existingDtoTable;
        private readonly object _existingEntryTable;
        private IConfigTableContentReplacer _dtoReplacer;
        private IConfigTableContentReplacer _entryReplacer;

        public PreparedIncrementalTableUpdate(
            ConfigTableDefinition definition,
            bool replacesExisting,
            object existingDtoTable,
            object existingEntryTable)
        {
            _definition = definition;
            _replacesExisting = replacesExisting;
            _existingDtoTable = existingDtoTable;
            _existingEntryTable = existingEntryTable;
        }

        public object NextDtoTable { get; set; }
        public object NextEntryTable { get; set; }

        public void ValidateCommitTargets()
        {
            if (!_replacesExisting) return;

            _dtoReplacer = GetTableContentReplacer(
                _existingDtoTable,
                NextDtoTable,
                _definition.DtoType);
            _entryReplacer = GetTableContentReplacer(
                _existingEntryTable,
                NextEntryTable,
                _definition.EntryType);
        }

        public void Commit(
            Dictionary<Type, object> tables,
            Dictionary<Type, object> dtoTables)
        {
            if (_replacesExisting)
            {
                _dtoReplacer.ReplaceContentsFrom(NextDtoTable);
                _entryReplacer.ReplaceContentsFrom(NextEntryTable);
                return;
            }

            dtoTables[_definition.DtoType] = NextDtoTable;
            tables[_definition.EntryType] = NextEntryTable;
        }

        private static IConfigTableContentReplacer GetTableContentReplacer(
            object target,
            object source,
            Type valueType)
        {
            if (!(target is IConfigTableContentReplacer replacer) || target.GetType() != source.GetType())
                throw new InvalidOperationException($"Table replacement is not supported for {valueType.FullName}");
            return replacer;
        }
    }
}
