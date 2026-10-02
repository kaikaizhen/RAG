using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace RAG.Extensions
{
    public static class OpenApiSchemaExtensions
    {
        /// <summary>
        /// .NET 10 的 OpenAPI 會因為 System.Text.Json 允許「從字串讀取數字」而把整數 / 數字標成 <c>integer | string</c>，
        /// 產生的 SDK 型別變成 <c>number | string</c>。執行期解析維持寬鬆，但對外契約只宣告數字，讓 client 型別精確。
        /// </summary>
        public static IServiceCollection AddNumericSchemaNormalization(this IServiceCollection services)
        {
            services.ConfigureAll<OpenApiOptions>(options =>
            {
                options.AddSchemaTransformer((schema, _, _) =>
                {
                    if (schema.Type is { } type
                        && type.HasFlag(JsonSchemaType.String)
                        && (type.HasFlag(JsonSchemaType.Integer) || type.HasFlag(JsonSchemaType.Number)))
                    {
                        schema.Type = type & ~JsonSchemaType.String;
                        schema.Pattern = null;
                    }

                    return Task.CompletedTask;
                });
            });

            return services;
        }
    }
}
