using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;

namespace DSH4VS.Core
{
    /// <summary>
    /// 在 Visual Studio 与 DeepSeek Harness 插件之间传递最新编辑器上下文的本地桥接服务。
    /// </summary>
    internal static class DshContextBridge
    {
        #region 常量

        /// <summary>桥接服务实际使用的回环端口。</summary>
        public static int Port { get; private set; } = 3091;

        /// <summary>Harness 插件读取上下文的 endpoint。</summary>
        public static string Endpoint => "http://127.0.0.1:" + Port + "/api/visual-studio/context";

        #endregion

        #region 私有字段

        private static readonly object syncRoot = new();
        private static HttpListener listener;
        private static CancellationTokenSource listenerCts;
        private static string snapshotJson = "{\"available\":false,\"message\":\"尚未同步 Visual Studio 上下文，请先在 Visual Studio 中执行同步命令。\",\"isError\":false}";

        #endregion

        #region 生命周期

        /// <summary>启动本地回环 HTTP 服务；重复调用不会创建多个监听器。</summary>
        /// <param name="output">用于记录启动状态的输出通道。</param>
        public static void Start(IDshOutput output)
        {
            lock (syncRoot)
            {
                if (listener != null)
                {
                    return;
                }

                try
                {
                    Port = FindAvailablePort(3091);
                    if (Port == 0)
                    {
                        throw new InvalidOperationException("没有可用的上下文桥接端口。");
                    }

                    listener = new HttpListener();
                    listener.Prefixes.Add("http://127.0.0.1:" + Port + "/");
                    listener.Start();
                    listenerCts = new CancellationTokenSource();
                    _ = ListenAsync(listener, listenerCts.Token);
                    output?.WriteLine("[DSH] Visual Studio 上下文桥接已启动: " + Endpoint);
                }
                catch (Exception ex)
                {
                    listener?.Close();
                    listener = null;
                    listenerCts?.Dispose();
                    listenerCts = null;
                    output?.WriteLine("[DSH] 上下文桥接启动失败: " + ex.Message);
                }
            }
        }

        /// <summary>停止由扩展启动的本地桥接服务。</summary>
        public static void Stop()
        {
            lock (syncRoot)
            {
                listenerCts?.Cancel();
                listenerCts?.Dispose();
                listenerCts = null;
                listener?.Close();
                listener = null;
            }
        }

        #endregion

        #region 上下文同步

        /// <summary>捕获并保存当前 Visual Studio 活动文档、选区和光标位置。</summary>
        /// <param name="extensibility">扩展 API 实例。</param>
        /// <param name="context">命令客户端上下文。</param>
        /// <param name="cancellationToken">取消标记。</param>
        public static async Task<DSHAskContext> SyncAsync(
            VisualStudioExtensibility extensibility,
            IClientContext context,
            CancellationToken cancellationToken)
        {
            var askContext = await DSHAskContext.FromClientContextAsync(
                extensibility, context, cancellationToken);
            var fileContent = ReadFileContent(askContext.FilePath);
            var snapshot = new
            {
                available = !string.IsNullOrWhiteSpace(askContext.FilePath),
                solutionPath = askContext.SolutionPath,
                projectPath = askContext.ProjectPath,
                filePath = askContext.FilePath,
                fileContent,
                selectionText = askContext.SelectionText,
                cursorLine = askContext.CursorLine,
                cursorColumn = askContext.CursorColumn,
                currentLineText = askContext.CurrentLineText,
                synchronizedAt = DateTimeOffset.UtcNow
            };
            var json = JsonSerializer.Serialize(snapshot);
            Interlocked.Exchange(ref snapshotJson, json);
            return askContext;
        }

        #endregion

        #region HTTP 服务

        private static int FindAvailablePort(int preferredPort)
        {
            var startPort = preferredPort is >= 1024 and <= 65535 ? preferredPort : 3091;
            for (var port = startPort; port <= 65535; port++)
            {
                try
                {
                    using var probe = new TcpListener(IPAddress.Loopback, port);
                    probe.Start();
                    probe.Stop();
                    return port;
                }
                catch (SocketException)
                {
                }
            }

            return 0;
        }

        private static async Task ListenAsync(HttpListener activeListener, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var getContextTask = activeListener.GetContextAsync();
                    var completed = await Task.WhenAny(getContextTask, Task.Delay(Timeout.Infinite, cancellationToken));
                    if (completed != getContextTask)
                    {
                        return;
                    }

                    var httpContext = await getContextTask;
                    var response = httpContext.Response;
                    response.StatusCode = 200;
                    response.ContentType = "application/json; charset=utf-8";
                    response.Headers["Cache-Control"] = "no-store";
                    var bytes = Encoding.UTF8.GetBytes(Volatile.Read(ref snapshotJson));
                    response.ContentLength64 = bytes.Length;
                    await response.OutputStream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
                    response.Close();
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (HttpListenerException)
                {
                    return;
                }
                catch
                {
                    // 单个请求失败不应使桥接服务停止。
                }
            }
        }

        private static string ReadFileContent(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            try
            {
                return File.ReadAllText(filePath);
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}
