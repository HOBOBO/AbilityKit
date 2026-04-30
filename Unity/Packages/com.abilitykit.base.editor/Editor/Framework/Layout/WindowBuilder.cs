#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace AbilityKit.Editor.Framework
{
    /// <summary>
    /// 窗口构建器 - 链式 API
    /// </summary>
    public class WindowBuilder<TData, TConfig>
        where TConfig : class, IWindowConfig, new()
    {
        private readonly List<IWindowPlugin<TData>> _plugins = new();
        private Action<IEnumerable<TData>> _loadData;
        private Action<TData> _drawDetail;
        private Func<TData, string, bool> _filterPredicate;
        private Action<TConfig> _configAction;
        private string _title = "Window";

        public WindowBuilder<TData, TConfig> Title(string title) { _title = title; return this; }
        public WindowBuilder<TData, TConfig> LoadData(Action<IEnumerable<TData>> loadData) { _loadData = loadData; return this; }
        public WindowBuilder<TData, TConfig> DrawDetail(Action<TData> action) { _drawDetail = action; return this; }
        public WindowBuilder<TData, TConfig> Filter(Func<TData, string, bool> predicate) { _filterPredicate = predicate; return this; }
        public WindowBuilder<TData, TConfig> Config(Action<TConfig> action) { _configAction = action; return this; }
        public WindowBuilder<TData, TConfig> AddPlugin(IWindowPlugin<TData> plugin) { _plugins.Add(plugin); return this; }

        public PlugableWindow<TData, TConfig> Build()
        {
            var window = UnityEngine.ScriptableObject.CreateInstance<PlugableWindow<TData, TConfig>>();
            window.titleContent = new UnityEngine.GUIContent(_title);

            var config = new TConfig();
            _configAction?.Invoke(config);
            config.Validate();

            IEnumerable<TData> data = null;
            _loadData?.Invoke(data);

            window.Initialize(data, _plugins);
            return window;
        }

        public void Show() => Build().Show();
    }
}
#endif
