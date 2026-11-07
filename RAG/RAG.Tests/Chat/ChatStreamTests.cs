using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using RAG.Streaming;
using RAG.Tests.Infrastructure;

namespace RAG.Tests.Chat
{
    public class ChatStreamTests : IClassFixture<RagApiFactory>, IAsyncLifetime
    {
        private readonly RagApiFactory _factory;
        private readonly HttpClient _client;

        public ChatStreamTests(RagApiFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClientWithToken(TestTokens.Create(factory.SecretKey, subject: SeedData.UserA.ToString()));
        }

        public Task InitializeAsync()
        {
            _factory.RagGateway.Reset();
            return SeedData.ResetAsync(_factory);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        /// <summary>
        /// 依 SSE 規格解析：以空行分隔事件，同一事件的多個 data 行以 \n 串接
        /// </summary>
        private static (string? ChatId, string Text, int TextEvents) Parse(string body)
        {
            string? chatId = null;
            var text = new System.Text.StringBuilder();
            var textEvents = 0;
            foreach (var block in body.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
            {
                var lines = block.Split('\n');
                // SSE 規格：data: 後面若有一個空白就去掉那一個（只去一個），多個 data 行以 \n 串接
                var data = string.Join("\n", lines.Where(l => l.StartsWith("data:")).Select(l =>
                {
                    var value = l["data:".Length..];
                    return value.StartsWith(' ') ? value[1..] : value;
                }));
                if (lines.Any(l => l == "event: chat")) chatId = data;
                else { text.Append(data); textEvents++; }
            }
            return (chatId, text.ToString(), textEvents);
        }

        private async Task<(string Body, TimeSpan Elapsed)> StreamAsync(string answer)
        {
            _factory.RagGateway.AnswerOverride = answer;
            var watch = Stopwatch.StartNew();
            var response = await _client.PostAsJsonAsync("/api/v1/Chat/ChatStream", new { Question = "q" });
            var body = await response.Content.ReadAsStringAsync();
            watch.Stop();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (body, watch.Elapsed);
        }

        [Fact]
        public async Task Long_answer_streams_within_a_couple_of_seconds_and_is_reconstructed_exactly()
        {
            var answer = string.Concat(Enumerable.Repeat("出差報帳需要準備出差申請單與發票。", 200)); // 約 3,400 字

            var (body, elapsed) = await StreamAsync(answer);

            var (chatId, text, events) = Parse(body);
            Assert.False(string.IsNullOrEmpty(chatId));
            Assert.Equal(answer, text);
            // 以前每字元隨機等 15-50ms，這個長度要超過 100 秒；現在事件數有上限，總時間約 1.5 秒
            Assert.InRange(events, 1, SseText.MaxEvents + 1);
            Assert.True(elapsed < TimeSpan.FromSeconds(6), $"stream took {elapsed.TotalSeconds:F1}s");
        }

        [Fact]
        public async Task Newlines_emoji_and_whitespace_survive_the_stream()
        {
            // 換行、連續空行、結尾換行、組合 emoji（多個 UTF-16 碼位）、CRLF（會被正規化成 \n）
            var answer = "第一行\n\n第三行 有空白  \n👍🏽 與 👨‍👩‍👧‍👦 完整\r\n最後一行\n";

            var (body, _) = await StreamAsync(answer);

            var (_, text, _) = Parse(body);
            Assert.Equal(answer.Replace("\r\n", "\n"), text);
        }

        [Fact]
        public void Chunks_never_split_a_text_element()
        {
            var answer = string.Concat(Enumerable.Repeat("👍🏽👨‍👩‍👧‍👦é", 80)); // 每組都是多碼位的文字元素

            var chunks = SseText.Chunk(answer, maxEvents: 7);

            Assert.True(chunks.Count <= 7);
            Assert.Equal(answer, string.Concat(chunks));
            foreach (var chunk in chunks)
            {
                // 每段單獨看，都必須是完整的文字元素（沒有被切在中間）
                Assert.Equal(StringInfo.ParseCombiningCharacters(chunk).Length, new StringInfo(chunk).LengthInTextElements);
                Assert.DoesNotContain(chunk, c => char.IsLowSurrogate(c) && chunk.IndexOf(c) == 0);
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData("a")]
        [InlineData("\n")]
        public void Tiny_inputs_produce_valid_events(string answer)
        {
            var events = SseText.Chunk(answer).Select(SseText.FormatData).ToList();

            foreach (var evt in events)
            {
                Assert.EndsWith("\n\n", evt);
                Assert.All(evt.TrimEnd('\n').Split('\n'), line => Assert.StartsWith("data:", line));
            }
            Assert.Equal(answer, string.Concat(SseText.Chunk(answer)));
        }
    }
}
