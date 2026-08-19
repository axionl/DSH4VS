/**
 * 本地模块声明：让 DshPlugin 在独立编译（不依赖 DSH 运行时 node_modules）时
 * 也能通过类型检查。运行期这些模块由 DSH profile 的共享依赖镜像解析。
 */

declare module '@deepseek-ai/schemastery' {
  interface SchemaAnnotation {
    description?: string
    title?: string
    default?: unknown
    examples?: unknown
  }

  interface SchemaChain<T = unknown> extends SchemaAnnotation {
    (value: unknown): unknown
    default(value: T): SchemaChain<T>
    description(value: string): SchemaChain<T>
    required(value?: boolean): SchemaChain<T>
  }

  interface SchemaInstance extends SchemaChain {
    object<T extends Record<string, unknown>>(spec: T): SchemaChain<T>
    string(): SchemaChain<string>
    number(): SchemaChain<number>
    boolean(): SchemaChain<boolean>
    array(): SchemaChain<unknown[]>
  }

  const Schema: SchemaInstance
  export default Schema
}

declare module '@deepseek-ai/dsh-tools' {
  export type InferArgs<S> = { [K in keyof S]?: unknown }

  export interface ContentBlock {
    type: string
    text?: string
    [key: string]: unknown
  }

  export interface ToolRunContext {
    signal: AbortSignal
    [key: string]: unknown
  }

  export interface ToolOutputDefinition<S, O> {
    schema: O
    render(args: InferArgs<S>, value: unknown): ContentBlock[]
    presentationMeta?(args: InferArgs<S>, value: unknown): unknown
  }

  export interface DefineToolOptions<S extends Record<string, unknown>, O extends { type: string }> {
    name: string
    description: string
    parameters: S
    output: ToolOutputDefinition<S, O>
    timeoutMs?: number
    execute(args: any, exec: ToolRunContext): Promise<unknown>
    isConcurrencySafe?(args: InferArgs<S>): boolean
    presentCall?(args: InferArgs<S>): unknown
    presentResult?(args: InferArgs<S>, result: unknown): unknown
    finalizeContent?(exec: unknown, result: unknown): unknown
  }

  export function defineTool<S extends Record<string, unknown>, O extends { type: string }>(
    options: DefineToolOptions<S, O>,
  ): unknown
}
