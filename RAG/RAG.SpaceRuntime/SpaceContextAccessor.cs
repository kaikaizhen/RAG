namespace RAG.SpaceRuntime
{
    /// <summary>
    /// 目前 request 的已解析 ExecutionContext（scoped）；未解析時一律 fail-closed
    /// </summary>
    public interface ISpaceContextAccessor
    {
        /// <summary>
        /// 已解析的 ExecutionContext；未解析時丟出 InvalidOperationException
        /// </summary>
        SpaceExecutionContext Current { get; }

        bool TryGet(out SpaceExecutionContext? context);

        /// <summary>
        /// 由 Space middleware 設定一次；已設定後不可覆寫
        /// </summary>
        void Set(SpaceExecutionContext context);
    }

    public class SpaceContextAccessor : ISpaceContextAccessor
    {
        private SpaceExecutionContext? _context;

        public SpaceExecutionContext Current =>
            _context ?? throw new InvalidOperationException("SpaceContext has not been resolved for this request.");

        public bool TryGet(out SpaceExecutionContext? context)
        {
            context = _context;
            return context is not null;
        }

        public void Set(SpaceExecutionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (_context is not null) throw new InvalidOperationException("SpaceContext is immutable once resolved.");

            _context = context;
        }
    }
}
