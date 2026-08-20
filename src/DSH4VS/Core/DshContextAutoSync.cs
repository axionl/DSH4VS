using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;

namespace DSH4VS.Core
{
    /// <summary>
    /// 低频监测 Visual Studio 当前编辑器上下文，并在活动文件、光标或选区变化后自动同步。
    /// </summary>
    internal static class DshContextAutoSync
    {
        #region 常量

        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(1000);

        #endregion

        #region 私有字段

        private static readonly object syncRoot = new();
        private static readonly SemaphoreSlim syncGate = new(1, 1);
        private static CancellationTokenSource cancellationSource;
        private static Task monitorTask;
        private static VisualStudioExtensibility extensibility;
        private static IClientContext clientContext;
        private static IDshOutput output;
        private static string lastFingerprint;

        #endregion

        #region 生命周期

        /// <summary>启动上下文自动监听；重复启动只更新客户端上下文和输出通道。</summary>
        /// <param name="visualStudioExtensibility">Visual Studio 扩展 API 实例。</param>
        /// <param name="context">用于读取活动编辑器的客户端上下文。</param>
        /// <param name="dshOutput">用于记录同步错误的输出通道。</param>
        public static void Start(
            VisualStudioExtensibility visualStudioExtensibility,
            IClientContext context,
            IDshOutput dshOutput)
        {
            if (visualStudioExtensibility == null || context == null)
            {
                return;
            }

            lock (syncRoot)
            {
                extensibility = visualStudioExtensibility;
                clientContext = context;
                output = dshOutput;
                if (monitorTask != null && !monitorTask.IsCompleted)
                {
                    return;
                }

                cancellationSource = new CancellationTokenSource();
                lastFingerprint = null;
                monitorTask = MonitorAsync(cancellationSource.Token);
            }
        }

        /// <summary>更新自动监听使用的 Visual Studio 客户端上下文。</summary>
        /// <param name="context">最新的客户端上下文。</param>
        public static void UpdateContext(IClientContext context)
        {
            if (context == null)
            {
                return;
            }

            lock (syncRoot)
            {
                clientContext = context;
            }
        }

        /// <summary>停止上下文自动监听并取消尚未完成的同步。</summary>
        public static void Stop()
        {
            CancellationTokenSource source;
            lock (syncRoot)
            {
                source = cancellationSource;
                cancellationSource = null;
                monitorTask = null;
                extensibility = null;
                clientContext = null;
                output = null;
                lastFingerprint = null;
            }

            source?.Cancel();
            source?.Dispose();
        }

        #endregion

        #region 自动同步

        private static async Task MonitorAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var timer = new PeriodicTimer(PollInterval);
                await SynchronizeIfChangedAsync(cancellationToken);
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    await SynchronizeIfChangedAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                WriteError("上下文自动监听已停止", ex);
            }
        }

        private static async Task SynchronizeIfChangedAsync(CancellationToken cancellationToken)
        {
            VisualStudioExtensibility currentExtensibility;
            IClientContext currentContext;
            IDshOutput currentOutput;
            lock (syncRoot)
            {
                currentExtensibility = extensibility;
                currentContext = clientContext;
                currentOutput = output;
            }

            if (currentExtensibility == null || currentContext == null
                || !await syncGate.WaitAsync(0, cancellationToken))
            {
                return;
            }

            try
            {
                var context = await DSHAskContext.FromClientContextAsync(
                    currentExtensibility, currentContext, cancellationToken);
                var fingerprint = string.Join("\u001f",
                    context.FilePath,
                    context.SolutionPath,
                    string.Join("\u001e", (context.OpenedDocuments ?? Array.Empty<DSHOpenDocument>())
                        .Select(document => string.Join("\u001d", document.FilePath, document.Name, document.IsActive))),
                    context.CursorLine,
                    context.CursorColumn,
                    context.SelectionText,
                    context.CurrentLineText);
                if (string.Equals(fingerprint, lastFingerprint, StringComparison.Ordinal))
                {
                    return;
                }

                await DshContextBridge.SyncAsync(
                    currentExtensibility, currentContext, cancellationToken);
                lastFingerprint = fingerprint;
                currentOutput?.WriteLine("[DSH] Visual Studio 上下文已自动同步。");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                WriteError("自动同步 Visual Studio 上下文失败", ex);
            }
            finally
            {
                syncGate.Release();
            }
        }

        private static void WriteError(string message, Exception exception)
        {
            lock (syncRoot)
            {
                output?.WriteLine("[DSH] " + message + "：" + exception.Message);
            }
        }

        #endregion
    }
}
