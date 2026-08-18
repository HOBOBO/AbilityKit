using Xunit;

// FlowPools 是进程级静态池（collectionCheck=true），多个测试类并行租借/释放同一池
// 会让"释放后再租借"类断言产生偶发失败。关闭本测试程序集内的测试并行化，保证确定性。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
