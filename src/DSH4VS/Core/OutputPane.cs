using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;

#pragma warning disable VSEXTPREVIEW_OUTPUTWINDOW // OutputWindow API 处于预览阶段，本扩展按要求使用

namespace DSH4VS.Core;

/// <summary>
/// 通过 VisualStudio.Extensibility 的 OutputWindow 服务向 VS 输出窗口写入 DSH 日志。
/// 输出通道以扩展进程为单位，同名通道只能创建一次，因此使用共享单例。
/// </summary>
public sealed class OutputPane : IDshOutput
{
    #region 字段

    private const string ChannelDisplayName = "DSH";

    private static readonly object createGate = new();
    private static Task<OutputPane> createTask;

    private readonly TextWriter writer;
    private readonly object writeGate = new();

    #endregion

    #region 构造函数

    /// <summary>
    /// 使用指定文本写入器创建输出窗格包装器。
    /// </summary>
    /// <param name="writer">输出窗格对应的文本写入器。</param>
    private OutputPane(TextWriter writer)
    {
        this.writer = writer;
    }

    #endregion

    #region 输出窗格创建

    /// <summary>
    /// 获取共享的 DSH 输出窗格实例。并发调用只会创建一次输出通道。
    /// </summary>
    /// <param name="extensibility">扩展入口提供的 VisualStudioExtensibility 对象。</param>
    /// <param name="cancellationToken">取消标记。</param>
    public static Task<OutputPane> GetAsync(VisualStudioExtensibility extensibility,
        CancellationToken cancellationToken)
    {
        lock (createGate)
        {
            createTask ??= CreateAsync(extensibility, cancellationToken);
            return createTask;
        }
    }

    /// <summary>
    /// 创建 DSH 输出通道并包装其文本写入器。
    /// </summary>
    /// <param name="extensibility">扩展入口提供的 VisualStudioExtensibility 对象。</param>
    /// <param name="cancellationToken">取消标记。</param>
    private static async Task<OutputPane> CreateAsync(VisualStudioExtensibility extensibility,
        CancellationToken cancellationToken)
    {
        var channel = await extensibility.Views().Output.CreateOutputChannelAsync(
            ChannelDisplayName, cancellationToken);
        return new OutputPane(channel.Writer);
    }

    #endregion

    #region 文本写入

    #region 公共方法

    /// <inheritdoc />
    public void Write(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        lock (writeGate)
        {
            try
            {
                writer.Write(text);
                writer.Flush();
            }
            catch
            {
                // 输出窗格不可用时忽略写入失败
    }

    #endregion
        }

    #endregion
    }

    /// <inheritdoc />
    public void WriteLine(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            Write(Environment.NewLine);
            return;
        }

        lock (writeGate)
        {
            try
            {
                writer.WriteLine(text);
                writer.Flush();
            }
            catch
            {
                // 输出窗格不可用时忽略写入失败
            }
        }
    }
}
