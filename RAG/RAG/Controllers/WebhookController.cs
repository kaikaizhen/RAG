using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAG.Authentication;
using RAG.Models.Entities;
using RAG.Models.Params.Webhook;
using RAG.Models.ViewModels;
using RAG.Models.ViewModels.Webhook;
using RAG.Webhooks;

namespace RAG.Controllers
{
    /// <summary>
    /// Webhook：事件發生時，系統用簽章的 HTTPS 請求通知你設定的網址，把功能接到你自己的服務。
    /// 只接受登入 token（應用程式權杖不能設定 webhook）
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [ApiVersion("1.0")]
    [Authorize]
    [Tags("Webhook 模組")]
    public class WebhookController : ControllerBase
    {
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IWebhookService _webhooks;

        public WebhookController(ICurrentUserAccessor currentUser, IWebhookService webhooks)
        {
            _currentUser = currentUser;
            _webhooks = webhooks;
        }

        /// <summary>
        /// 查詢我的 webhook
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(List<WebhookVM>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Get()
        {
            var hooks = await _webhooks.ListAsync(_currentUser.UserId, HttpContext.RequestAborted);
            return Ok(hooks.Select(h => Fill(new WebhookVM(), h)).ToList());
        }

        /// <summary>
        /// 建立 webhook。簽章密鑰只會在這次回應出現一次
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(CreateWebhookVM), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorVM), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] SaveWebhookParam request)
        {
            var (hook, secret) = await _webhooks.CreateAsync(_currentUser.UserId, request.Name, request.Url, request.Events, HttpContext.RequestAborted);
            var vm = Fill(new CreateWebhookVM(), hook);
            vm.Secret = secret;
            return Ok(vm);
        }

        /// <summary>
        /// 更新 webhook（名稱、網址、事件、是否啟用）
        /// </summary>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(WebhookVM), StatusCodes.Status200OK)]
        public async Task<IActionResult> Update([FromRoute] int id, [FromBody] SaveWebhookParam request)
        {
            var hook = await _webhooks.UpdateAsync(_currentUser.UserId, id, request.Name, request.Url, request.Events, request.IsActive, HttpContext.RequestAborted);
            return Ok(Fill(new WebhookVM(), hook));
        }

        /// <summary>
        /// 刪除 webhook（連同送出紀錄）
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete([FromRoute] int id)
        {
            await _webhooks.DeleteAsync(_currentUser.UserId, id, HttpContext.RequestAborted);
            return Ok(new { State = "Success", Message = $"Delete Webhook{id} Success" });
        }

        /// <summary>
        /// 送出一則測試事件（ping）並立刻回報結果
        /// </summary>
        [HttpPost("{id}/test")]
        [ProducesResponseType(typeof(WebhookDeliveryVM), StatusCodes.Status200OK)]
        public async Task<IActionResult> Test([FromRoute] int id)
        {
            var delivery = await _webhooks.TestAsync(_currentUser.UserId, id, HttpContext.RequestAborted);
            return Ok(ToVM(delivery));
        }

        /// <summary>
        /// 最近的送出紀錄（含重試狀態與失敗原因）
        /// </summary>
        [HttpGet("{id}/deliveries")]
        [ProducesResponseType(typeof(List<WebhookDeliveryVM>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Deliveries([FromRoute] int id)
        {
            var deliveries = await _webhooks.DeliveriesAsync(_currentUser.UserId, id, HttpContext.RequestAborted);
            return Ok(deliveries.Select(ToVM).ToList());
        }

        /// <summary>
        /// 手動重送一筆通知（重試次數歸零，立刻送出並回報結果）
        /// </summary>
        [HttpPost("{id}/deliveries/{deliveryId}/redeliver")]
        [ProducesResponseType(typeof(WebhookDeliveryVM), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorVM), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Redeliver([FromRoute] int id, [FromRoute] int deliveryId)
        {
            var delivery = await _webhooks.RedeliverAsync(_currentUser.UserId, id, deliveryId, HttpContext.RequestAborted);
            return Ok(ToVM(delivery));
        }

        private static T Fill<T>(T vm, Webhook hook) where T : WebhookVM
        {
            vm.ID = hook.ID;
            vm.Name = hook.Name;
            vm.Url = hook.Url;
            vm.Events = hook.Events.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            vm.IsActive = hook.IsActive;
            vm.ConsecutiveFailures = hook.ConsecutiveFailures;
            vm.DisabledReason = hook.DisabledReason;
            vm.CreateAt = hook.CreateAt;
            return vm;
        }

        private static WebhookDeliveryVM ToVM(WebhookDelivery d) => new()
        {
            ID = d.ID,
            WebhookID = d.WebhookID,
            EventID = d.EventID,
            Event = d.Event,
            Status = d.Status,
            Attempts = d.Attempts,
            NextAttemptAt = d.NextAttemptAt,
            LastStatusCode = d.LastStatusCode,
            LastError = d.LastError,
            CreateAt = d.CreateAt,
            DeliveredAt = d.DeliveredAt
        };
    }
}
