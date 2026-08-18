using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Shell;
using DSH4VS.Core;
using DSH4VS.UI;

namespace DSH4VS.Commands
{
    /// <summary>
    /// Ask DSH…：打开任务对话框并运行 DSH headless 任务。
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
    /// Ask DSH about selection：在编辑器上下文菜单与工具菜单中针对选中文本提问。
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
                await context.Extensibility.Shell().ShowToolWindowAsync<DshWebToolWindow>(
                    activate: true, cancellationToken);
            }
            catch (Exception ex)
            {
                await DshCommandLogic.LogAsync(context, ex);
                await DshCommandLogic.ShowErrorAsync(
                    context,
                    "无法打开 DSH Web UI。详细信息已写入“输出”窗口的 DSH 面板。\n\n" + ex.Message,
                    cancellationToken);
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
    /// DSH 命令共享逻辑：获取输出窗格、捕获上下文并运行任务对话框。
    /// </summary>
    internal static class DshCommandLogic
    {
        #region 任务执行

        /// <summary>
        /// 获取 IDE 上下文，显示任务对话框并运行 DSH headless 任务。
        /// </summary>
        /// <param name="context">命令执行时的客户端上下文。</param>
        /// <param name="forceSelection">是否强制包含当前选中文本。</param>
        /// <param name="cancellationToken">命令取消标记。</param>
        public static async Task RunAskAsync(IClientContext context,
            bool forceSelection, CancellationToken cancellationToken)
        {
            try
            {
                var extensibility = context.Extensibility;
                var output = await OutputPane.GetAsync(extensibility, cancellationToken);
                var askContext = await DSHAskContext.FromClientContextAsync(
                    extensibility, context, cancellationToken);

                var dialog = new PromptDialog(askContext, forceSelection)
                {
                    Owner = Application.Current?.MainWindow
                };
                dialog.ShowDialog();
                if (dialog.DialogResult != true)
                {
                    return;
                }

                var taskText = dialog.BuildTaskText();
                if (string.IsNullOrWhiteSpace(taskText))
                {
                    return;
                }

                await DshRunner.RunTaskAsync(output, taskText, askContext.WorkspaceRoot,
                    new CancellationTokenSource());
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

        /// <summary>显示错误提示，并在提示不可用时记录异常。</summary>
        /// <param name="context">命令执行时的客户端上下文。</param>
        /// <param name="message">需要显示的错误信息。</param>
        /// <param name="cancellationToken">提示框取消标记。</param>
        internal static async Task ShowErrorAsync(IClientContext context, string message,
            CancellationToken cancellationToken)
        {
            try
            {
                await context.Extensibility.Shell().ShowPromptAsync(message, PromptOptions.OK,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                await LogAsync(context, ex);
            }
        }

        #endregion
    }
}