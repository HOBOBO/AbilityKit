namespace AbilityKit.Analyzer
{
    public static class AnalyzerGlobalSetting
    {
        private static bool _enableAnalyzer = true;

        public static bool EnableAnalyzer
        {
            get => _enableAnalyzer;
            set => _enableAnalyzer = value;
        }
    }
}
