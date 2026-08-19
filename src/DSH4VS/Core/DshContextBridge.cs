using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;

namespace DSH4VS.Core
{
    /// <summary>
    /// 在 Visual Studio 与 DeepSeek Harness 插件之间传递最新编辑器上下文的本地桥接服务。
    /// </summary>
    /// <remarks>
    /// 监听端口固定为 <see cref="Port" />，与插件配置中的 <c>bridgeUrl</c>（硬编码为
    /// <c>http://127.0.0.1:13091/api/visual-studio/context</c>）保持一致。
    /// 使用 .NET 内置 <see cref="HttpListener" /> 提供标准 HTTP 服务，端口被占用时启动失败
    /// 会在输出窗格明确报错，而不是静默换端口。
    /// </remarks>
    internal static class DshContextBridge
    {
        #region 常量

        /// <summary>桥接服务实际使用的回环端口（与插件 bridgeUrl 保持一致）。</summary>
        public const int Port = 13091;

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
                    listener = new HttpListener();
                    listener.Prefixes.Add("http://127.0.0.1:" + Port + "/");
                    listener.Start();
                    listenerCts = new CancellationTokenSource();
                    _ = ListenAsync(listener, listenerCts.Token);
                    output?.WriteLine("[DSH] Visual Studio 上下文桥接已启动: " + Endpoint);
                }
                catch (Exception ex)
                {
                    StopListener();
                    output?.WriteLine("[DSH] 上下文桥接启动失败: " + ex.Message);
                }
            }
        }

        /// <summary>停止由扩展启动的本地桥接服务。</summary>
        public static void Stop()
        {
            lock (syncRoot)
            {
                StopListener();
            }
        }

        /// <summary>释放监听器及其取消令牌（调用方需持有 <see cref="syncRoot" /> 锁）。</summary>
        private static void StopListener()
        {
            listenerCts?.Cancel();
            listenerCts?.Dispose();
            listenerCts = null;
            listener?.Stop();
            listener = null;
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

        /// <summary>
        /// 异步监听服务
        /// </summary>
        /// <param name="activeListener"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private static async Task ListenAsync(HttpListener activeListener, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await activeListener.GetContextAsync();
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (HttpListenerException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                _ = HandleRequestAsync(context, cancellationToken);
            }
        }

        /// <summary>处理单个 HTTP 请求并返回当前快照 JSON。</summary>
        /// <param name="context">已接受的 HTTP 请求上下文。</param>
        /// <param name="cancellationToken">取消标记。</param>
        private static async Task HandleRequestAsync(
            HttpListenerContext context, CancellationToken cancellationToken)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;
                response.Headers["Cache-Control"] = "no-store";
                response.Headers["Access-Control-Allow-Origin"] = "*";
                response.Headers["Access-Control-Allow-Methods"] = "GET, OPTIONS";
                response.Headers["Access-Control-Allow-Headers"] = "Content-Type";

                if (string.Equals(request.HttpMethod, "OPTIONS", StringComparison.OrdinalIgnoreCase))
                {
                    response.StatusCode = 204;
                    response.Close();
                    return;
                }

                if (!string.Equals(request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase)
                    || !request.Url.AbsolutePath.EndsWith(
                        "/api/visual-studio/context", StringComparison.OrdinalIgnoreCase))
                {
                    response.StatusCode = 404;
                    response.Close();
                    return;
                }

                var body = System.Text.Encoding.UTF8.GetBytes(Volatile.Read(ref snapshotJson));
                response.StatusCode = 200;
                response.ContentType = "application/json; charset=utf-8";
                response.ContentLength64 = body.Length;
                await response.OutputStream.WriteAsync(body, 0, body.Length, cancellationToken);
                response.Close();
            }
            catch (OperationCanceledException)
            {
            }
            catch (IOException)
            {
            }
            catch (HttpListenerException)
            {
            }
        }

        /// <summary>
        /// 读取文件内容
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
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
