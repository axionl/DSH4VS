using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Shell;
using DSH4VS.Core;
using DSH4VS.UI;

namespace DSH4VS.Commands
{
    /// <summary>
    /// Ask DSH…：同步当前上下文并打开 DSH Web UI。
    /// </summary>
    [VisualStudioContribution]
    internal sealed class AskDshCommand : Command
    {
        #region 命令配置

        /// <summary>获取 Ask DSH 命令的显示名称、菜单位置和图标配置。</summary>
        public override CommandConfiguration CommandConfiguration =>
            new("%DSH.Commands.AskDsh.DisplayName%")
            {
                ClientContexts = [ClientContextCategory.Shell, ClientContextCategory.Editor],
                Placements = [CommandPlacement.KnownPlacements.ExtensionsMenu],
                Icon = new CommandIconConfiguration(ImageMoniker.KnownValues.QuestionMark,
                    IconSettings.None)
            };

        #endregion

        #region 命令执行

        /// <summary>打开任务对话框并运行 DSH headless 任务。</summary>
        public override Task ExecuteCommandAsync(IClientContext context,
            CancellationToken cancellationToken)
        {
            return DshCommandLogic.RunAskAsync(context, forceSelection: false, cancellationToken);
        }

        #endregion
    }

    /// <summary>
    /// Sync active document：将 Visual Studio 当前活动文档同步给 DSH。
    /// </summary>
    [VisualStudioContribution]
    internal sealed class SyncDshDocumentCommand : Command
    {
        #region 命令配置

        /// <summary>获取同步活动文档命令的显示名称和菜单位置。</summary>
        public override CommandConfiguration CommandConfiguration =>
            new("%DSH.Commands.SyncDshDocument.DisplayName%")
            {
                Placements = [CommandPlacement.KnownPlacements.ExtensionsMenu],
                Icon = new CommandIconConfiguration(ImageMoniker.KnownValues.Document,
                    IconSettings.None)
            };

        #endregion

        #region 命令执行

        /// <summary>捕获并同步当前活动文档。</summary>
        public override Task ExecuteCommandAsync(IClientContext context,
            CancellationToken cancellationToken)
        {
            return DshCommandLogic.SyncContextAsync(context, cancellationToken, "活动文档");
        }

        #endregion
    }

    /// <summary>
    /// Sync cursor position：将 Visual Studio 当前光标和选区位置同步给 DSH。
    /// </summary>
    [VisualStudioContribution]
    internal sealed class SyncDshCursorCommand : Command
    {
        #region 命令配置

        /// <summary>获取同步光标位置命令的显示名称和菜单位置。</summary>
        public override CommandConfiguration CommandConfiguration =>
            new("%DSH.Commands.SyncDshCursor.DisplayName%")
            {
                Placements = [CommandPlacement.KnownPlacements.ExtensionsMenu],
                Icon = new CommandIconConfiguration(ImageMoniker.KnownValues.Select,
                    IconSettings.None)
            };

        #endregion

        #region 命令执行

        /// <summary>捕获并同步当前光标位置和选区。</summary>
        public override Task ExecuteCommandAsync(IClientContext context,
            CancellationToken cancellationToken)
        {
            return DshCommandLogic.SyncContextAsync(context, cancellationToken, "光标位置");
        }

        #endregion
    }

    /// <summary>
    /// Ask DSH about selection：同步当前选中文本并打开 DSH Web UI。
    /// </summary>
    [VisualStudioContribution]
    internal sealed class AskDshSelectionCommand : Command
    {
        #region 命令配置

        /// <summary>获取选中文本提问命令的显示名称、菜单位置和图标配置。</summary>
        public override CommandConfiguration CommandConfiguration =>
            new("%DSH.Commands.AskDshSelection.DisplayName%")
            {
                ClientContexts = [ClientContextCategory.Shell, ClientContextCategory.Editor],
                Placements =
            [
                CommandPlacement.KnownPlacements.ExtensionsMenu,
                CommandPlacement.VsctParent(
                    new Guid("d309f791-903f-11d0-9efc-00a0c911004f"),
                    id: 0x016B,
                    priority: 0x0801)
            ],
                Icon = new CommandIconConfiguration(ImageMoniker.KnownValues.Select,
                IconSettings.None)
            };

        #endregion

        #region 命令执行

        /// <summary>打开任务对话框并针对当前选中文本运行 DSH 任务。</summary>
        public override Task ExecuteCommandAsync(IClientContext context,
            CancellationToken cancellationToken)
        {
            return DshCommandLogic.RunAskAsync(context, forceSelection: true, cancellationToken);
        }

        #endregion
    }

    /// <summary>
    /// Open DSH Web UI：显示 DSH Web 工具窗口。
    /// </summary>
    [VisualStudioContribution]
    internal sealed class OpenDshWebCommand : Command
    {
        #region 命令配置

        /// <summary>获取 DSH Web UI 命令的显示名称、菜单位置和图标配置。</summary>
        public override CommandConfiguration CommandConfiguration =>
            new("%DSH.Commands.OpenDshWeb.DisplayName%")
            {
                Placements =
            [
                CommandPlacement.KnownPlacements.ExtensionsMenu,
                CommandPlacement.KnownPlacements.ViewOtherWindowsMenu
            ],
                Icon = new CommandIconConfiguration(
                    ImageMoniker.KnownValues.OpenWebPortal,
                    IconSettings.None)
            };

        #endregion

        #region 命令执行

        /// <summary>显示 DSH Web 工具窗口。</summary>
        public override async Task ExecuteCommandAsync(IClientContext context,
            CancellationToken cancellationToken)
        {
            try
            {
                DshWebToolWindow.SetClientContext(context);
                await context.Extensibility.Shell().ShowToolWindowAsync<DshWebToolWindow>(
                    activate: true, cancellationToken);
            }
            catch (Exception ex)
            {
                await DshCommandLogic.LogAsync(context, ex);
            }
        }

        #endregion
    }

    /// <summary>
    /// Stop DSH Web UI：停止由扩展启动的 DSH Web UI 服务。
    /// </summary>
    [VisualStudioContribution]
    internal sealed class StopDshWebCommand : Command
    {
        #region 命令配置

        /// <summary>获取停止 DSH Web UI 命令的显示名称、菜单位置和图标配置。</summary>
        public override CommandConfiguration CommandConfiguration =>
            new("%DSH.Commands.StopDshWeb.DisplayName%")
            {
                Placements = [CommandPlacement.KnownPlacements.ExtensionsMenu],
                Icon = new CommandIconConfiguration(
                    ImageMoniker.KnownValues.Stop, IconSettings.None)
            };

        #endregion

        #region 命令执行

        /// <summary>停止由扩展启动的 DSH Web UI 服务。</summary>
        public override Task ExecuteCommandAsync(IClientContext context,
            CancellationToken cancellationToken)
        {
            DshRunner.StopWeb();
            return Task.CompletedTask;
        }

        #endregion
    }

    /// <summary>
    /// Cancel DSH Task：取消正在运行的 DSH headless 任务。
    /// </summary>
    [VisualStudioContribution]
    internal sealed class CancelDshCommand : Command
    {
        #region 命令配置

        /// <summary>获取取消 DSH 任务命令的显示名称、菜单位置和图标配置。</summary>
        public override CommandConfiguration CommandConfiguration =>
            new("%DSH.Commands.CancelDsh.DisplayName%")
            {
                Placements = [CommandPlacement.KnownPlacements.ExtensionsMenu],
                Icon = new CommandIconConfiguration(
                    ImageMoniker.KnownValues.Cancel, IconSettings.None)
            };

        #endregion

        #region 命令执行

        /// <summary>取消当前正在运行的 DSH 任务。</summary>
        public override Task ExecuteCommandAsync(IClientContext context,
            CancellationToken cancellationToken)
        {
            DshRunner.Cancel();
            return Task.CompletedTask;
        }

        #endregion
    }

    /// <summary>
    /// DSH 命令共享逻辑：同步上下文并打开 DSH Web UI。
    /// </summary>
    internal static class DshCommandLogic
    {
        #region 任务执行

        /// <summary>
        /// 同步 IDE 上下文并显示 DSH Web 工具窗口，任务输入由 Web UI 承载。
        /// </summary>
        /// <param name="context">命令执行时的客户端上下文。</param>
        /// <param name="forceSelection">保留参数以兼容命令入口；Web UI 始终读取最新上下文。</param>
        /// <param name="cancellationToken">命令取消标记。</param>
        public static async Task RunAskAsync(IClientContext context,
            bool forceSelection, CancellationToken cancellationToken)
        {
            try
            {
                await SyncContextAsync(context, cancellationToken,
                    forceSelection ? "选中文本" : "当前上下文");
                DshWebToolWindow.SetClientContext(context);
                await context.Extensibility.Shell().ShowToolWindowAsync<DshWebToolWindow>(
                    activate: true, cancellationToken);
            }
            catch (Exception ex)
            {
                await LogAsync(context, ex);
            }
        }

        /// <summary>捕获 Visual Studio 上下文并同步到 Harness bridge。</summary>
        /// <param name="context">命令执行时的客户端上下文。</param>
        /// <param name="cancellationToken">命令取消标记。</param>
        /// <param name="kind">用于输出日志的同步类型。</param>
        public static async Task SyncContextAsync(IClientContext context,
            CancellationToken cancellationToken, string kind)
        {
            try
            {
                var output = await OutputPane.GetAsync(context.Extensibility, cancellationToken);
                DshContextBridge.Start(output);
                var askContext = await DshContextBridge.SyncAsync(
                    context.Extensibility, context, cancellationToken);
                output.WriteLine($"[DSH] 已同步{kind}: {askContext.FilePath ?? "无活动文档"}");
            }
            catch (Exception ex)
            {
                await LogAsync(context, ex);
            }
        }

        #endregion

        #region 日志与错误提示

        /// <summary>将异常写入 DSH 输出窗格。</summary>
        /// <param name="context">命令执行时的客户端上下文。</param>
        /// <param name="ex">需要记录的异常。</param>
        internal static async Task LogAsync(IClientContext context, Exception ex)
        {
            try
            {
                var output = await OutputPane.GetAsync(context.Extensibility, CancellationToken.None);
                output.WriteLine("[DSH] " + ex);
            }
            catch
            {
                // 输出不可用时忽略
            }
        }

        #endregion
    }
}