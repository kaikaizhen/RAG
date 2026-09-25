namespace RAG.Models.ViewModels
{
    /// <summary>
    /// 統一錯誤回應（401 / 403 / 404 / 409 / 5xx）
    /// </summary>
    public class ErrorVM
    {
        /// <summary>
        /// 錯誤訊息
        /// </summary>
        public string Error { get; set; } = string.Empty;
    }
}
