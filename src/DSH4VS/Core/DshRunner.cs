using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DSH4VS.Core
{
    /// <summary>
    /// 运行 DSH CLI：使用 `dsh --profile headless "<task>"` 执行一次性 Agent 任务，
    /// 使用 `dsh --profile web --port 3080` 启动嵌入式 Web UI。标准输出和标准错误
    /// 会流式写入 Visual Studio 的“DSH”输出窗格，任务支持取消。
    /// </summary>
    public static class DshRunner
    {
        #region 常量

        /// <summary>
        /// DSH Web UI 的本地访问地址。
        /// </summary>
        public const string WebUrl = "http://127.0.0.1:3080";

        /// <summary>
        /// DSH Web UI 使用的本地端口。
        /// </summary>
        public const int WebPort = 3080;

        /// <summary>根据端口生成 DSH Web UI 的本地访问地址。</summary>
        /// <param name="port">Web UI 监听端口。</param>
        public static string GetWebUrl(int port) => "http://127.0.0.1:" + port;

        /// <summary>
        /// 查找可以绑定本地回环地址的 Web UI 端口。
        /// </summary>
        /// <param name="preferredPort">优先尝试的端口。</param>
        /// <returns>可用端口；找不到时返回零。</returns>
        public static int FindAvailableWebPort(int preferredPort)
        {
            var startPort = preferredPort is >= 1 and <= 65535 ? preferredPort : WebPort;
            for (var port = startPort; port <= 65535; port++)
            {
                try
                {
                    using var listener = new TcpListener(IPAddress.Loopback, port);
                    listener.Start();
                    listener.Stop();
                    return port;
                }
                catch (SocketException)
                {
                }
            }

            return 0;
        }

        #endregion

        #region 私有字段

        private static Process currentProcess;
        private static CancellationTokenSource currentCts;
        private static Process webProcess;

        #endregion

        #region 公共属性

        /// <summary>
        /// 获取当前是否存在正在运行的 DSH 任务进程。
        /// </summary>
        public static bool IsRunning => currentProcess != null;

        /// <summary>
        /// 获取当前由扩展启动的 DSH Web UI 进程 ID；未由扩展启动时返回零。
        /// </summary>
        public static int WebProcessId => webProcess?.Id ?? 0;

        #endregion

        #region 公共方法

        /// <summary>
        /// 取消当前正在运行的 DSH 任务及其子进程。
        /// </summary>
        public static void Cancel()
        {
            var cts = currentCts;
            cts?.Cancel();
            var process = currentProcess;
            if (process != null)
            {
                KillTree(process);
            }
        }

        /// <summary>
        /// 停止由当前扩展启动的 DSH Web UI 服务。
        /// </summary>
        /// <returns>如果扩展持有并停止了 Web 进程，则返回 <see langword="true" />。</returns>
        public static bool StopWeb()
        {
            var process = webProcess;
            webProcess = null;
            if (process == null)
            {
                return false;
            }

            KillTree(process);
            process.Dispose();
            return true;
        }

        /// <summary>
        /// 检查 DSH Web UI 是否已经在本地端口启动并可访问。
        /// </summary>
        /// <returns>
        /// 如果 Web UI 返回成功状态码，则返回 <see langword="true" />；
        /// 否则返回 <see langword="false" />。
        /// </returns>
        public static async Task<bool> IsWebUpAsync(int port = WebPort)
        {
            try
            {
                using var client = new HttpClient();

                client.Timeout = TimeSpan.FromSeconds(1.5);

                using var response = await client.GetAsync(GetWebUrl(port));

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 确保 dsh web profile 在 3080 端口监听；如未运行则启动它。
        /// </summary>
        /// <param name="output">输出通道。</param>
        public static async Task StartWebAsync(IDshOutput output, int port = WebPort)
        {
            if (await IsWebUpAsync(port))
            {
                return;
            }

            var exe = DshLocator.LocateWeb();
            if (exe == null)
            {
                output.WriteLine("[DSH] 未找到 npx，无法启动 web UI。请先安装 Node.js。");
                return;
            }

            output.WriteLine($"[DSH] 使用 npx @deepseek-ai/dsh web --port {port} 启动 Web UI …");
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exe.FileName,
                    Arguments = exe.ArgumentsPrefix + $" --port {port}",
                    WorkingDirectory = Environment.CurrentDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };
                webProcess = Process.Start(psi); // 独立启动，Visual Studio 退出后进程仍会继续运行
                webProcess.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null) output.WriteLine(e.Data);
                };
                webProcess.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null) output.WriteLine("[err] " + e.Data);
                };
                webProcess.BeginOutputReadLine();
                webProcess.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                output.WriteLine("[DSH] 启动失败: " + ex.Message);
                return;
            }

            for (var i = 0; i < 40; i++)
            {
                await Task.Delay(750);
                if (await IsWebUpAsync(port))
                {
                    output.WriteLine($"[DSH] Web UI 就绪: {GetWebUrl(port)}");
                    return;
                }
            }
            output.WriteLine("[DSH] 等待 Web UI 超时。请手动在终端运行: dsh web");
        }

        /// <summary>
        /// 以 headless profile 运行一个 DSH 任务，将输出流式写入 DSH 输出窗格。
        /// </summary>
        /// <param name="output">输出通道。</param>
        /// <param name="task">任务文本。</param>
        /// <param name="workingDir">DSH 工作目录。</param>
        /// <param name="cts">取消任务的控制信号。</param>
        public static async Task RunTaskAsync(IDshOutput output, string task, string workingDir, CancellationTokenSource cts)
        {
            if (string.IsNullOrWhiteSpace(task))
            {
                output.WriteLine("DSH: 任务为空");
                return;
            }
            if (currentProcess != null)
            {
                output.WriteLine("DSH: 已有任务在运行（DSH > Cancel DSH Task）");
                return;
            }

            var exe = DshLocator.Locate();
            if (exe == null)
            {
                output.WriteLine("[DSH] 未找到 dsh CLI。安装: npm install -g @deepseek-ai/dsh，或设置环境变量 DSH_CLI 指向可执行文件。");
                return;
            }

            var escaped = task.Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ");
            var args = (exe.ArgumentsPrefix + " --profile headless \"" + escaped + "\"").Trim();

            output.WriteLine(Environment.NewLine + "════════ DSH headless ════════");
            output.WriteLine("> " + task);
            output.WriteLine("工作目录: " + workingDir);
            output.WriteLine("调用: " + exe.Display + " --profile headless \"…\"");

            var psi = new ProcessStartInfo
            {
                FileName = exe.FileName,
                Arguments = args,
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, e) => { if (e.Data != null) output.WriteLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) output.WriteLine("[err] " + e.Data); };

            currentProcess = process;
            currentCts = cts;

            try
            {
                output.WriteLine("DSH: 任务运行中… (DSH > Cancel DSH Task 取消)");
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using (var registration = cts.Token.Register(() => KillTree(process)))
                {
                    await Task.Run(() => { process.WaitForExit(); process.WaitForExit(); });
                }

                var code = process.ExitCode;
                output.WriteLine(code == 0
                    ? "════ DSH 完成 (exit 0) ════"
                    : "════ DSH 结束 (exit " + code + ") ════");
            }
            catch (Exception ex)
            {
                output.WriteLine("[DSH] " + ex);
            }
            finally
            {
                currentProcess = null;
                currentCts = null;
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 终止指定进程及其子进程树。
        /// </summary>
        /// <param name="process">需要终止的进程。</param>
        private static void KillTree(Process process)
        {
            try
            {
                using (var killer = Process.Start(new ProcessStartInfo("taskkill", $"/PID {process.Id} /T /F")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                }))
                {
                    killer?.WaitForExit(3000);
                }
            }
            catch
            {
                // 进程可能已经退出
            }
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch
            {
                // 进程可能已经退出
            }
        }

        #endregion
    }
}