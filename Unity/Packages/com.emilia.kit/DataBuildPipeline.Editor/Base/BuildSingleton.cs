namespace Emilia.DataBuildPipeline.Editor
{
    public class BuildSingleton<T> where T : BuildSingleton<T>, new()
    {
        private static T _instance;

        public static T instance
        {
            get
            {
                if (_instance == default) _instance = new T();
                return _instance;
            }
        }
    }
}