namespace NBC
{
    public enum TaskStatus
    {
        /// <summary>
        /// 任务还未执行
        /// </summary>
        None = 0,

        /// <summary>
        /// 任务运行
        /// </summary>
        Running,

        /// <summary>
        /// 任务执行成功
        /// </summary>
        Success,

        /// <summary>
        /// 任务执行失败
        /// </summary>
        Fail
    }
}