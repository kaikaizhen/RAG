using System.ComponentModel;

namespace RAG.Commons.Enums
{
    public enum LanguageEnum
    {
        /// <summary>
        /// 中文
        /// </summary>
        [Description("zh-TW")]
        ZhTw,

        /// <summary>
        /// 英文
        /// </summary>
        [Description("en-US")]
        EnUs,

        /// <summary>
        /// 其他語系
        /// </summary>
        [Description("other")]
        Other
    }
}
