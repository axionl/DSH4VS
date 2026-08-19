import Schema from '@deepseek-ai/schemastery'
import { defineTool } from '@deepseek-ai/dsh-tools'

export const name = 'dsh4vs-visual-studio-context'
export const inject = ['tools']

export const Config = Schema.object({
  bridgeUrl: Schema.string().default('http://127.0.0.1:13091/api/visual-studio/context'),
  timeoutMs: Schema.number().default(2000),
})

export function apply(ctx, config) {
  ctx.tools.register(defineTool({
    name: 'get_visual_studio_context',
    description: '读取最近一次从 Visual Studio 同步的上下文，包括活动文档或光标位置。',
    parameters: {
      mode: {
        type: 'string',
        description: '使用 active_document 获取活动文件和内容，使用 cursor_position 获取光标、选区和当前行。',
      },
    },
    output: {
      schema: { type: 'string' },
      render: (_args, value) => [{ type: 'text', text: value }],
    },
    async execute(args = {}, exec) {
      try {
        const signal = exec?.signal ?? AbortSignal.timeout(config.timeoutMs)
        const response = await fetch(config.bridgeUrl, {
          signal,
        })
        if (!response.ok) {
          return `无法读取 Visual Studio 上下文：桥接服务返回 HTTP ${response.status}。`
        }

        const snapshot = await response.json()
        if (snapshot.available !== true) {
          return String(snapshot.message ?? '尚未同步 Visual Studio 上下文，请先在 Visual Studio 中执行同步命令。')
        }

        if (args.mode === 'cursor_position') {
          return JSON.stringify({
            filePath: snapshot.filePath,
            cursorLine: snapshot.cursorLine,
            cursorColumn: snapshot.cursorColumn,
            currentLineText: snapshot.currentLineText,
            selectionText: snapshot.selectionText,
          }, null, 2)
        }

        return JSON.stringify({
          solutionPath: snapshot.solutionPath,
          projectPath: snapshot.projectPath,
          filePath: snapshot.filePath,
          fileContent: snapshot.fileContent,
        }, null, 2)
      } catch (error) {
        return `读取 Visual Studio 上下文失败：${error instanceof Error ? error.message : String(error)}`
      }
    },
  }))
}
