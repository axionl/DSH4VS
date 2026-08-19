import Schema from '@deepseek-ai/schemastery'
import { defineTool } from '@deepseek-ai/dsh-tools'

export const name = 'dsh4vs-visual-studio-context'
export const inject = ['tools']

export const Config = Schema.object({
  bridgeUrl: Schema.string().default('http://127.0.0.1:13091/api/visual-studio/context'),
  timeoutMs: Schema.number().default(2000),
})

type Mode = 'active_document' | 'cursor_position'

interface PluginConfig {
  bridgeUrl: string
  timeoutMs: number
}

interface VisualStudioSnapshot {
  available?: boolean
  message?: string
  solutionPath?: string
  projectPath?: string
  filePath?: string
  fileContent?: string | null
  cursorLine?: number
  cursorColumn?: number
  currentLineText?: string
  selectionText?: string
}

interface ToolArgs {
  mode?: Mode
}

interface ToolContext {
  tools: {
    register(tool: unknown): unknown
  }
}

export function apply(ctx: ToolContext, config: PluginConfig) {
  ctx.tools.register(defineTool({
    name: 'get_visual_studio_context',
    description: '读取最近一次从 Visual Studio 同步的上下文，包括活动文档或光标位置。',
    parameters: {
      mode: {
        type: 'string',
        description: '使用 active_document 获取活动文件和内容，使用 cursor_position 获取光标、选区和当前行。',
        enum: ['active_document', 'cursor_position'],
      },
    },
    output: {
      schema: { type: 'string' },
      render: (_args: unknown, value: unknown) => [{ type: 'text', text: String(value) }],
    },
    async execute(args: ToolArgs = {}, exec: { signal?: AbortSignal }): Promise<string> {
      const timeoutMs = Number.isFinite(config.timeoutMs) && config.timeoutMs > 0 ? config.timeoutMs : 2000
      const timeoutController = new AbortController()
      const timeoutId = setTimeout(() => timeoutController.abort(), timeoutMs)

      try {
        if (!exec?.signal) {
          return '读取 Visual Studio 上下文失败：工具执行上下文不可用。'
        }

        const signal = AbortSignal.any([exec.signal, timeoutController.signal])
        const response = await fetch(config.bridgeUrl, { signal })
        if (!response.ok) {
          return `无法读取 Visual Studio 上下文：桥接服务返回 HTTP ${response.status}。`
        }

        const snapshot: VisualStudioSnapshot = await response.json()
        if (snapshot.available !== true) {
          return String(snapshot.message ?? '尚未同步 Visual Studio 上下文，请先在 Visual Studio 中执行同步命令。')
        }

        const mode: Mode = args.mode ?? 'active_document'
        if (mode === 'active_document') {
          return JSON.stringify({
            solutionPath: snapshot.solutionPath,
            projectPath: snapshot.projectPath,
            filePath: snapshot.filePath,
            fileContent: snapshot.fileContent,
          }, null, 2)
        }

        if (mode === 'cursor_position') {
          return JSON.stringify({
            filePath: snapshot.filePath,
            cursorLine: snapshot.cursorLine,
            cursorColumn: snapshot.cursorColumn,
            currentLineText: snapshot.currentLineText,
            selectionText: snapshot.selectionText,
          }, null, 2)
        }

        return `读取 Visual Studio 上下文失败：未知的 mode "${String(mode)}"，仅支持 active_document 或 cursor_position。`
      } catch (error) {
        if (timeoutController.signal.aborted && !exec?.signal?.aborted) {
          return `读取 Visual Studio 上下文失败：桥接服务在 ${timeoutMs} 毫秒内没有响应。`
        }

        return `读取 Visual Studio 上下文失败：${error instanceof Error ? error.message : String(error)}`
      } finally {
        clearTimeout(timeoutId)
      }
    },
  }))
}
