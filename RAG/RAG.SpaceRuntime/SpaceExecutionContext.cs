namespace RAG.SpaceRuntime
{
    /// <summary>
    /// ExecutionContext = SpaceContext + ResourceScope（命名避開 System.Threading.ExecutionContext）
    /// </summary>
    public sealed record SpaceExecutionContext(SpaceContext Space, ResourceScope Resource);
}
