namespace DSH4VS.Core;

/// <summary>
/// DSH 输出通道抽象，可在任意线程安全调用。
/// 由 VisualStudio.Extensibility 输出窗格或测试桩实现。
/// </summary>
public interface IDshOutput
{
    /// <summary>写入文本（不换行）。</summary>
    void Write(string text);

    /// <summary>写入一行文本。</summary>
    void WriteLine(string text);
}
