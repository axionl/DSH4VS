using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DSH4VS.Core
{
    /// <summary>
    /// 运行 DSH CLI：使用 headless profile 执行一次性 Agent 任务，
    /// 使用 `dsh web --no-open --port 13080` 启动嵌入式 Web UI。标准输出和标准错误
    /// 会流式写入 Visual Studio 的“DSH”输出窗格，任务支持取消。
    /// </summary>
    public static class DshRunner
    {
        #region 常量

        /// <summary>
        /// DSH Web UI 的本地访问地址。
        /// </summary>
        public const string WebUrl = "http://127.0.0.1:13080";

        /// <summary>
        /// DSH Web UI 使用的本地端口。
        /// </summary>
        public const int WebPort = 13080;

        /// <summary>Windows 命令行工具常用的系统代码页，用于读取 pnpm/cmd 输出。</summary>
        private static System.Text.Encoding DshProcessEncoding =>
            System.Text.Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.ANSICodePage);

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
        private static int webPort = WebPort;

        static DshRunner()
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            AppDomain.CurrentDomain.ProcessExit += (_, _) => CleanupOnExit();
            AppDomain.CurrentDomain.DomainUnload += (_, _) => CleanupOnExit();
        }

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
        /// 按 Web UI 监听端口停止 DSH Web UI 服务，不区分进程是否由当前扩展启动。
        /// </summary>
        /// <param name="port">需要停止的 Web UI 监听端口。</param>
        /// <returns>如果找到并停止了监听进程，则返回 <see langword="true" />。</returns>
        public static bool StopWeb(int port = 0)
        {
            var process = webProcess;
            webProcess = null;
            var targetPort = port is >= 1 and <= 65535 ? port : webPort;
            webPort = WebPort;
            DshContextBridge.Stop();
            if (process == null)
            {
                process = FindListeningProcess(targetPort);
            }

            if (process == null)
            {
                return false;
            }

            try
            {
                KillTree(process);
                if (!process.HasExited)
                {
                    process.WaitForExit(5000);
                }
            }
            catch
            {
                // 进程可能已经退出，停止操作仍应完成桥接服务清理。
            }
            finally
            {
                process.Dispose();
            }

            return true;
        }

        /// <summary>停止扩展持有的 Web 进程和上下文 bridge。</summary>
        public static void StopAll()
        {
            StopWeb();
            DshContextAutoSync.Stop();
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
                if (!response.IsSuccessStatusCode)
                {
                    return false;
                }

                var contentType = response.Content.Headers.ContentType?.MediaType;
                if (!string.IsNullOrWhiteSpace(contentType)
                    && contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var content = await response.Content.ReadAsStringAsync();
                return content.Contains("<html", StringComparison.OrdinalIgnoreCase)
                    || content.Contains("<!doctype", StringComparison.OrdinalIgnoreCase)
                    || content.Contains("DeepSeek", StringComparison.OrdinalIgnoreCase)
                    || content.Contains("Harness", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 确保 dsh web profile 在 13080 端口监听；如未运行则启动它。
        /// </summary>
        /// <param name="output">输出通道。</param>
        /// <param name="port">Web UI 监听端口。</param>
        public static async Task<int> StartWebAsync(IDshOutput output, int port = WebPort)
        {
            DshContextBridge.Start(output);
            if (port == DshContextBridge.Port)
            {
                port = FindAvailableWebPort(port + 1);
                if (port == 0)
                {
                    DshContextBridge.Stop();
                    output.WriteLine("[DSH] 无法为 Web UI 找到不与上下文桥接冲突的端口。");
                    return 0;
                }

                output.WriteLine($"[DSH] Web UI 端口与上下文桥接端口冲突，已切换到 {port}。");
            }

            RemoveLegacyProfile(output);
            if (!EnsurePluginCopied(output))
            {
                output.WriteLine("[DSH] Visual Studio Harness 插件复制失败，已取消 Web UI 启动。");
                DshContextBridge.Stop();
                return 0;
            }

            if (await IsWebUpAsync(port))
            {
                    webPort = port;
                output.WriteLine($"[DSH] Web UI 已在运行: {GetWebUrl(port)}");
                return port;
            }

            var exe = DshLocator.LocateWeb();
            if (exe == null)
            {
                output.WriteLine("[DSH] 未找到 npx，无法启动 web UI。请先安装 Node.js。");
                return 0;
            }

            output.WriteLine($"[DSH] 启动 Web UI（端口 {port}）…");
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exe.FileName,
                    Arguments = exe.ArgumentsPrefix + $" --no-open --port {port}",
                    WorkingDirectory = Environment.CurrentDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = DshProcessEncoding,
                    StandardErrorEncoding = DshProcessEncoding
                };
                var process = Process.Start(psi); // 独立启动，Visual Studio 退出后进程仍会继续运行
                if (process == null)
                {
                    output.WriteLine("[DSH] 无法启动 Web UI 进程。");
                    return 0;
                }

                webProcess = process;
                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null) output.WriteLine(e.Data);
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null) output.WriteLine("[err] " + e.Data);
                };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                webProcess = null;
                output.WriteLine("[DSH] 启动失败: " + ex.Message);
                return 0;
            }

            for (var i = 0; i < 40; i++)
            {
                await Task.Delay(750);
                if (await IsWebUpAsync(port))
                {
                    webPort = port;
                    output.WriteLine($"[DSH] Web UI 就绪: {GetWebUrl(port)}");
                    return port;
                }

            }
            output.WriteLine("[DSH] 等待 Web UI 超时。请检查 DSH 输出中的启动错误。");
            return 0;
        }

        /// <summary>将本地插件复制到默认 Web profile 的插件目录。</summary>
        /// <param name="output">用于记录复制过程的输出通道。</param>
        /// <returns>如果插件复制成功，则返回 <see langword="true" />。</returns>
        private static bool EnsurePluginCopied(IDshOutput output)
        {
            var source = GetBundlePath();
            var target = Path.Combine(GetProfileDirectory("web"), "plugins", "dsh4vs-visual-studio-context");
            if (!File.Exists(Path.Combine(source, "index.js")))
            {
                output.WriteLine("[DSH] 未找到本地 Harness 插件入口: " + Path.Combine(source, "index.js"));
                return false;
            }

            try
            {
                Directory.CreateDirectory(target);
                CopyDirectoryContents(source, target, overwrite: true);
                output.WriteLine("[DSH] 已复制本地插件到 Web profile 插件目录: " + target);
                return true;
            }
            catch (Exception ex)
            {
                output.WriteLine("[DSH] 复制 Harness 插件失败: " + ex.Message);
                return false;
            }
        }

        /// <summary>获取扩展程序集目录中的本地 Harness bundle 路径。</summary>
        private static string GetBundlePath()
        {
            var extensionDirectory = Path.GetDirectoryName(typeof(DshRunner).Assembly.Location);
            return Path.Combine(
                string.IsNullOrEmpty(extensionDirectory) ? AppContext.BaseDirectory : extensionDirectory,
                "DshPlugin");
        }

        /// <summary>删除旧版本创建的 dsh4vs profile。</summary>
        /// <param name="output">用于记录清理过程的输出通道。</param>
        private static void RemoveLegacyProfile(IDshOutput output)
        {
            var directory = GetProfileDirectory("dsh4vs");
            if (!Directory.Exists(directory))
            {
                return;
            }

            try
            {
                Directory.Delete(directory, recursive: true);
                output.WriteLine("[DSH] 已删除旧的 dsh4vs profile。");
            }
            catch (Exception ex)
            {
                output.WriteLine("[DSH] 删除旧的 dsh4vs profile 失败: " + ex.Message);
            }
        }

        /// <summary>解析指定的 DSH profile 目录。</summary>
        /// <param name="profileName">profile 名称。</param>
        /// <returns>profile 目录的绝对路径。</returns>
        private static string GetProfileDirectory(string profileName)
        {
            var home = Environment.GetEnvironmentVariable("DSH_HOME");
            if (string.IsNullOrWhiteSpace(home))
            {
                home = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".dsh");
            }

            return Path.Combine(home, "profiles", profileName);
        }

        /// <summary>把源目录的全部内容复制到目标目录。</summary>
        /// <param name="sourceDir">源目录。</param>
        /// <param name="targetDir">目标目录。</param>
        /// <param name="overwrite">是否覆盖已存在的目标文件。</param>
        private static void CopyDirectoryContents(string sourceDir, string targetDir, bool overwrite)
        {
            foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(directory.Replace(sourceDir, targetDir));
            }

            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var target = file.Replace(sourceDir, targetDir);
                if (File.Exists(target) && !overwrite)
                {
                    continue;
                }

                File.Copy(file, target, overwrite: true);
            }
        }

        /// <summary>输出外部进程文本，并对空白输出进行忽略。</summary>
        /// <param name="output">扩展输出通道。</param>
        /// <param name="text">进程输出文本。</param>
        /// <param name="error">是否为标准错误。</param>
        private static void WriteProcessOutput(IDshOutput output, string text, bool error)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            foreach (var line in text.Split(["\r\n", "\n"],
                StringSplitOptions.RemoveEmptyEntries))
            {
                output.WriteLine(error ? "[DSH] [err] " + line : "[DSH] " + line);
            }
        }

        /// <summary>扩展进程退出时清理 Web 进程和上下文 bridge。</summary>
        private static void CleanupOnExit()
        {
            try
            {
                StopAll();
            }
            catch
            {
                DshContextBridge.Stop();
            }
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

        /// <summary>查找指定 TCP 端口的监听进程。</summary>
        /// <param name="port">需要查找的本地监听端口。</param>
        /// <returns>监听进程；未找到或无法读取时返回空值。</returns>
        private static Process FindListeningProcess(int port)
        {
            if (port is < 1 or > 65535)
            {
                return null;
            }

            try
            {
                using var netstat = Process.Start(new ProcessStartInfo
                {
                    FileName = "netstat.exe",
                    Arguments = "-ano -p tcp",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });
                if (netstat == null)
                {
                    return null;
                }

                var output = netstat.StandardOutput.ReadToEnd();
                netstat.WaitForExit(2000);
                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = line.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 5 || !string.Equals(parts[0], "TCP", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var localEndpoint = parts[1];
                    var state = parts[^2];
                    if ((!state.Contains("LISTEN", StringComparison.OrdinalIgnoreCase)
                            && !state.Contains("侦听", StringComparison.OrdinalIgnoreCase))
                        || !localEndpoint.EndsWith(":" + port, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (int.TryParse(parts[^1], out var processId))
                    {
                        try
                        {
                            return Process.GetProcessById(processId);
                        }
                        catch
                        {
                            return null;
                        }
                    }
                }
            }
            catch
            {
                // netstat 不可用或进程已退出时不影响停止流程。
            }

            return null;
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 终止指定进程及其子进程树。
        /// </summary>
        /// <param name="process">需要终止的进程。</param>
        private static void KillTree(Process process)
        {
            if (process == null)
            {
                return;
            }

            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    if (process.WaitForExit(5000))
                    {
                        return;
                    }
                }
            }
            catch
            {
                // 进程可能已经退出，继续使用 taskkill 清理仍存活的子进程。
            }

            try
            {
                using (var killer = Process.Start(new ProcessStartInfo("taskkill", $"/PID {process.Id} /T /F")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }))
                {
                    killer?.WaitForExit(1000);
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
                    process.WaitForExit(5000);
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