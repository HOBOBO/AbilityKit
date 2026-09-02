namespace AbilityKit.EnvironmentModel
{
/// <summary>载体中立的 3D 向量，仅用于描述性数据（场景布局、实体位置），不参与模拟计算。</summary>
public readonly struct EnvironmentVector3
{
    /// <summary>构造一个 3D 向量。</summary>
    public EnvironmentVector3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>X 分量。</summary>
    public float X { get; }

    /// <summary>Y 分量。</summary>
    public float Y { get; }

    /// <summary>Z 分量。</summary>
    public float Z { get; }

    /// <summary>(0, 0, 0) 零向量。</summary>
    public static readonly EnvironmentVector3 Zero = new(0f, 0f, 0f);
}
}
