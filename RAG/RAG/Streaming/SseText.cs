using System.Globalization;
using System.Text;

namespace RAG.Streaming
{
    /// <summary>
    /// 把已經算好的答案切成 SSE 事件（逐段送出的「打字機」效果）。
    /// 以「文字元素」為單位切（emoji、組合字元不會被切碎），段數有上限，所以答案再長也能在短時間內送完。
    /// </summary>
    public static class SseText
    {
        /// <summary>
        /// 整段答案最多切成幾個事件
        /// </summary>
        public const int MaxEvents = 120;

        /// <summary>
        /// 每個事件之間的等待（毫秒）。總時間上限約 MaxEvents × 這個值 ≈ 1.5 秒
        /// </summary>
        public const int DelayMilliseconds = 12;

        public static IReadOnlyList<string> Chunk(string text, int maxEvents = MaxEvents)
        {
            // SSE 以 \n / \r 分行；統一成 \n，避免答案中的 \r 破壞事件格式
            var normalized = (text ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");
            var elements = new List<string>();
            var enumerator = StringInfo.GetTextElementEnumerator(normalized);
            while (enumerator.MoveNext()) elements.Add((string)enumerator.Current);

            var perChunk = Math.Max(1, (int)Math.Ceiling(elements.Count / (double)Math.Max(1, maxEvents)));
            var chunks = new List<string>();
            for (var i = 0; i < elements.Count; i += perChunk)
            {
                chunks.Add(string.Concat(elements.Skip(i).Take(perChunk)));
            }
            return chunks;
        }

        /// <summary>
        /// 格式化成一個 SSE 事件。文字中的換行依規格拆成多個 data 行（接收端以 \n 串回），
        /// 所以任何內容都不會破壞事件分隔（空行）。
        /// </summary>
        public static string FormatData(string chunk)
        {
            var builder = new StringBuilder();
            foreach (var line in chunk.Split('\n')) builder.Append("data: ").Append(line).Append('\n');
            return builder.Append('\n').ToString();
        }
    }
}
