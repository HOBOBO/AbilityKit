namespace Emilia.DataBuildPipeline.Editor
{
    public enum OutputReportMode
    {
        /// <summary>
        /// 任何时候都会输出报告
        /// </summary>
        AllOutput,

        /// <summary>
        /// 仅在构建出现错误时输出报告
        /// </summary>
        ErrorOutput,

        /// <summary>
        /// 任何时候都不会输出报告
        /// </summary>
        NoneOutput,
    }
}