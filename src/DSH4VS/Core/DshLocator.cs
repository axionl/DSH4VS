using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace DSH4VS.Core
{
    /// <summary>
    /// DSH CLI 的调用信息。优先使用 `node.exe &lt;dsh&gt;/lib/bin.js`，
    /// 以避免 cmd.exe 参数处理导致的引号和百分号展开问题。
    /// </summary>
    public sealed class DshExecutable
    {
        /// <summary>需要启动的可执行文件路径。</summary>
        public string FileName { get; set; }

        /// <summary>每次调用前必须追加的参数，例如带引号的 bin.js 路径。</summary>
        public string ArgumentsPrefix { get; set; } = "";

        /// <summary>用于输出提示的可读描述。</summary>
        public string Display { get; set; }
    }

    /// <summary>
    /// Node.js、npx 和 DSH CLI 的环境检测结果。
    /// </summary>
    public sealed class DshEnvironmentStatus
    {
        /// <summary>检测到的 Node.js 版本。</summary>
        public string NodeVersion { get; set; }

        /// <summary>检测到的 npx 版本。</summary>
        public string NpxVersion { get; set; }

        /// <summary>检测到的 DSH CLI 版本。</summary>
        public string DshVersion { get; set; }

        /// <summary>当前是否能够定位 DSH CLI。</summary>
        public bool HasDsh { get; set; }

        /// <summary>当前是否检测到 Node.js。</summary>
        public bool HasNode => !string.IsNullOrEmpty(NodeVersion);

        /// <summary>当前是否检测到 npx。</summary>
        public bool HasNpx => !string.IsNullOrEmpty(NpxVersion);
    }

    /// <summary>
    /// 定位、检测和安装 DSH CLI 及其 Node.js 运行环境。
    /// </summary>
    public static class DshLocator
    {
        static DshLocator()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        #region CLI 定位

        /// <summary>
        /// 定位 DSH CLI。顺序为：DSH_CLI 覆盖路径、node.exe 加 DSH bin.js、
        /// npx 缓存或全局 npm 路径，最后查找 PATH 中的 dsh.cmd 或 dsh.ps1。
        /// </summary>
        public static DshExecutable Locate()
        {
            var overridePath = Environment.GetEnvironmentVariable("DSH_CLI");
            if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
            {
                return new DshExecutable
                {
                    FileName = overridePath,
                    Display = overridePath
                };
            }

            var node = FindNodePath();
            if (node != null)
            {
                var binJs = FindDshBinJs();
                if (binJs != null)
                {
                    return new DshExecutable
                    {
                        FileName = node,
                        ArgumentsPrefix = Quote(binJs),
                        Display = node + " " + binJs
                    };
                }
            }

            var dshCmd = FindOnPath("dsh.cmd") ?? FindOnPath("dsh.ps1");
            if (dshCmd != null)
            {
                return new DshExecutable { FileName = dshCmd, Display = dshCmd };
            }

            return null;
        }

        /// <summary>
        /// 定位用于调用 DSH CLI 的 npx 基础命令，供 profile 管理和启动复用。
        /// </summary>
        public static DshExecutable LocateNpx()
        {
            var npx = FindNpxPath();
            return npx == null
                ? null
                : new DshExecutable
                {
                    FileName = npx,
                    ArgumentsPrefix = "--yes @deepseek-ai/dsh",
                    Display = npx + " --yes @deepseek-ai/dsh"
                };
        }

        /// <summary>定位用于启动 DSH Web UI 的 npx 调用。</summary>
        /// <param name="profileName">需要启动的 profile 名称；为空时使用默认 Web profile。</param>
        public static DshExecutable LocateWeb(string profileName = null)
        {
            var dsh = LocateNpx();
            if (dsh == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(profileName))
            {
                dsh.ArgumentsPrefix += " web";
                dsh.Display += " web";
            }
            else
            {
                dsh.ArgumentsPrefix += " --profile " + profileName;
                dsh.Display += " --profile " + profileName;
            }

            return dsh;
        }

        #endregion

        #region 环境检测与安装

        /// <summary>
        /// 异步检测 Node.js、npx 和 DSH CLI 的可用状态。
        /// </summary>
        public static async Task<DshEnvironmentStatus> CheckEnvironmentAsync()
        {
            var status = new DshEnvironmentStatus();
            var dsh = Locate();
            if (dsh != null)
            {
                status.DshVersion = await RunVersionAsync(dsh.FileName,
                    dsh.ArgumentsPrefix + (string.IsNullOrWhiteSpace(dsh.ArgumentsPrefix) ? "--version" : " --version"));
                status.HasDsh = true;
            }

            var node = FindNodePath();
            if (node != null)
                status.NodeVersion = await RunVersionAsync(node);

            var npx = FindNpxPath();
            if (npx != null)
                status.NpxVersion = await RunVersionAsync(npx);

            return status;
        }

        /// <summary>
        /// 通过 npx 下载并安装 DSH CLI。
        /// </summary>
        /// <returns>安装错误信息；安装成功时返回空值。</returns>
        public static async Task<string> InstallDshAsync()
        {
            var npx = FindNpxPath();
            if (npx == null)
                return "未找到 npx，请先安装 Node.js。";

            var result = await RunProcessAsync(npx, "--yes @deepseek-ai/dsh --help");
            if (result.ExitCode != 0)
            {
                var detail = string.IsNullOrWhiteSpace(result.Error)
                    ? result.Output
                    : result.Error;
                return "npx 安装 DSH 失败：" + detail.Trim();
            }

            return Locate() != null
                ? null
                : "npx 已执行，但未能定位安装后的 dsh。请检查 npm 缓存或 PATH。";
        }

        /// <summary>
        /// 从 Node.js 官方源下载并静默安装 Node.js。
        /// </summary>
        /// <returns>安装错误信息；安装成功时返回空值。</returns>
        public static async Task<string> InstallNodeJsAsync()
        {
            if (FindNodePath() != null)
                return null;

            var architecture = Environment.Is64BitOperatingSystem ? "x64" : "x86";
            var tempMsi = Path.Combine(Path.GetTempPath(), "dsh-nodejs.msi");
            try
            {
                string msiUrl;
                using (var client = new HttpClient())
                {
                    client.Timeout = System.TimeSpan.FromMinutes(5);
                    var listing = await client.GetStringAsync("https://nodejs.org/dist/latest-v22.x/");
                    var match = Regex.Match(listing, "node-v([0-9.]+)-" + architecture + "\\.msi", RegexOptions.IgnoreCase);
                    if (!match.Success)
                        return "无法从 Node.js 官方源获取适用的安装包。";

                    msiUrl = "https://nodejs.org/dist/latest-v22.x/node-v" + match.Groups[1].Value + "-" + architecture + ".msi";
                    var bytes = await client.GetByteArrayAsync(msiUrl);
                    File.WriteAllBytes(tempMsi, bytes);
                }

                var result = await RunInstallerAsync(tempMsi);
                if (result != 0 && result != 3010)
                    return "Node.js 安装失败，msiexec 返回码：" + result;

                return FindNodePath() != null
                    ? null
                    : "Node.js 安装完成，但当前进程尚未发现 node.exe；请重启 Visual Studio 后重试。";
            }
            catch (Exception ex)
            {
                return "Node.js 自动安装失败：" + ex.Message;
            }
            finally
            {
                try { if (File.Exists(tempMsi)) File.Delete(tempMsi); } catch { }
            }
        }

        #endregion

        #region 进程执行辅助

        /// <summary>
        /// 使用 Windows Installer 安装指定 MSI 文件。
        /// </summary>
        /// <param name="msiPath">MSI 安装包路径。</param>
        /// <returns>msiexec 进程退出码。</returns>
        private static async Task<int> RunInstallerAsync(string msiPath)
        {
            using (var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "msiexec.exe",
                    Arguments = "/i " + Quote(msiPath) + " /qn /norestart",
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            })
            {
                if (!process.Start()) return -1;
                await Task.Run(() => process.WaitForExit());
                return process.ExitCode;
            }
        }

        /// <summary>
        /// 执行版本查询并返回标准输出内容。
        /// </summary>
        /// <param name="fileName">需要查询的可执行文件。</param>
        /// <returns>版本文本；执行失败时返回空值。</returns>
        private static async Task<string> RunVersionAsync(string fileName)
        {
            return await RunVersionAsync(fileName, "--version");
        }

        /// <summary>执行带前置参数的版本查询，例如 node.exe 加载 DSH bin.js。</summary>
        private static async Task<string> RunVersionAsync(string fileName, string arguments)
        {
            var result = await RunProcessAsync(fileName, arguments);
            return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output)
                ? result.Output.Trim()
                : null;
        }

        /// <summary>
        /// 启动进程并同时读取标准输出和标准错误。
        /// </summary>
        /// <param name="fileName">可执行文件路径。</param>
        /// <param name="arguments">进程参数。</param>
        /// <returns>进程退出码及输出内容。</returns>
        private static async Task<ProcessResult> RunProcessAsync(string fileName, string arguments)
        {
            try
            {
                using (var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                })
                {
                    if (!process.Start())
                        return new ProcessResult { ExitCode = -1, Error = "无法启动进程。" };

                    var outputTask = ReadProcessStreamAsync(process.StandardOutput.BaseStream);
                    var errorTask = ReadProcessStreamAsync(process.StandardError.BaseStream);
                    await Task.WhenAll(outputTask, errorTask, Task.Run(() => process.WaitForExit()));
                    return new ProcessResult
                    {
                        ExitCode = process.ExitCode,
                        Output = DecodeProcessOutput(await outputTask),
                        Error = DecodeProcessOutput(await errorTask)
                    };
                }
            }

            catch (Exception ex)
            {
                return new ProcessResult { ExitCode = -1, Error = ex.Message };
            }
        }

        /// <summary>读取外部进程的原始输出，避免 StreamReader 提前使用错误的代码页。</summary>
        private static async Task<byte[]> ReadProcessStreamAsync(Stream stream)
        {
            using (var buffer = new MemoryStream())
            {
                await stream.CopyToAsync(buffer);
                return buffer.ToArray();
            }
        }

        /// <summary>优先按 UTF-8 解码，遇到 Windows 本地代码页时回退到 GB18030。</summary>
        private static string DecodeProcessOutput(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true);
            try
            {
                return utf8.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.GetEncoding(936).GetString(bytes);
            }
        }

        /// <summary>
        /// 保存外部进程执行结果的内部数据对象。
        /// </summary>
        private sealed class ProcessResult
        {
            /// <summary>进程退出码。</summary>
            public int ExitCode { get; set; }

            /// <summary>进程标准输出。</summary>
            public string Output { get; set; }

            /// <summary>进程标准错误。</summary>
            public string Error { get; set; }
        }

        #endregion

        #region 路径查找辅助

        /// <summary>
        /// 查找 DSH npm 包中的 bin.js 文件。
        /// </summary>
        private static string FindDshBinJs()
        {
            var candidates = new List<string>();

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var npxRoot = Path.Combine(localAppData, "npm-cache", "_npx");
            if (Directory.Exists(npxRoot))
            {
                try
                {
                    foreach (var hashDir in Directory.EnumerateDirectories(npxRoot))
                    {
                        var bin = Path.Combine(hashDir, "node_modules", "@deepseek-ai", "dsh", "lib", "bin.js");
                        if (File.Exists(bin)) candidates.Add(bin);
                    }
                }
                catch { /* ignore unreadable entries */ }
            }

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(appData))
            {
                var global = Path.Combine(appData, "npm", "node_modules", "@deepseek-ai", "dsh", "lib", "bin.js");
                if (File.Exists(global)) candidates.Add(global);
            }

            return candidates
                .OrderByDescending(c => { try { return File.GetLastWriteTimeUtc(c); } catch { return DateTime.MinValue; } })
                .FirstOrDefault();
        }

        /// <summary>
        /// 在 PATH 环境变量包含的目录中查找指定文件。
        /// </summary>
        /// <param name="fileName">需要查找的文件名。</param>
        private static string FindOnPath(string fileName)
        {
            var path = string.Join(Path.PathSeparator.ToString(), GetEffectivePathValues());
            foreach (var rawDir in path.Split(';'))
            {
                var dir = rawDir.Trim().Trim('"');
                if (dir.Length == 0) continue;
                try
                {
                    var full = Path.Combine(dir, fileName);
                    if (File.Exists(full)) return full;
                }
                catch { /* ignore unreadable entries */ }
            }
            return null;
        }

        /// <summary>合并扩展进程 PATH 与用户/计算机环境变量中的最新 PATH。</summary>
        private static IEnumerable<string> GetEffectivePathValues()
        {
            var values = new List<string>
            {
                Environment.GetEnvironmentVariable("PATH") ?? string.Empty,
                Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? string.Empty,
                Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine) ?? string.Empty
            };

            try
            {
                using (var userKey = Registry.CurrentUser.OpenSubKey(@"Environment"))
                {
                    values.Add(userKey?.GetValue("Path") as string ?? string.Empty);
                }

                using (var machineKey = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment"))
                {
                    values.Add(machineKey?.GetValue("Path") as string ?? string.Empty);
                }
            }
            catch
            {
                // 注册表不可访问时仍使用进程环境变量。
            }

            return values;
        }

        /// <summary>查找 node.exe 的安装路径。</summary>
        private static string FindNodePath()
        {
            return FindOnPath("node.exe")
                ?? FindInDirectories("node.exe", GetNodeDirectories());
        }

        /// <summary>查找 npx 的安装路径。</summary>
        private static string FindNpxPath()
        {
            return FindOnPath("npx.cmd")
                ?? FindOnPath("npx.exe")
                ?? FindInDirectories("npx.cmd", GetNodeDirectories());
        }

        /// <summary>获取常见的 Node.js 安装目录。</summary>
        private static IEnumerable<string> GetNodeDirectories()
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            return new[]
            {
                Path.Combine(programFiles, "nodejs"),
                Path.Combine(programFilesX86, "nodejs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "nodejs"),
                Environment.GetEnvironmentVariable("NVM_SYMLINK"),
                Environment.GetEnvironmentVariable("NVM_HOME"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "nvm", "current")
            }.Where(Directory.Exists);
        }

        /// <summary>
        /// 在指定目录集合中查找文件。
        /// </summary>
        /// <param name="fileName">需要查找的文件名。</param>
        /// <param name="directories">待搜索的目录集合。</param>
        private static string FindInDirectories(string fileName, IEnumerable<string> directories)
        {
            foreach (var directory in directories)
            {
                var path = Path.Combine(directory, fileName);
                if (File.Exists(path)) return path;
            }
            return null;
        }

        /// <summary>
        /// 按 Windows CRT 命令行规则为参数添加引号并转义内部引号。
        /// </summary>
        /// <param name="s">需要转义的参数。</param>
        public static string Quote(string s) => "\"" + s.Replace("\"", "\\\"") + "\"";

        #endregion
    }
}
