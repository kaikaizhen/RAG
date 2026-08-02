using RAG.Models.Dtos.QaTypeItem;

namespace RAG.Services.Interfaces
{
    public interface IQaTypeItemService
    {
        /// <summary>
        /// 查詢問題種類細項列表
        /// </summary>
        /// <returns></returns>
        Task<List<GetQaTypeItemDto>> Get();

        /// <summary>
        /// 新增問題種類細項
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        Task CreateAsync(CreateQaTypeItemDto dto);

        /// <summary>
        /// 更新問題種類細項
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        Task UpdateAsync(UpdateQaTypeItemDto dto);

        /// <summary>
        /// 刪除問題種類細項
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        Task DeleteAsync(int Id);
    }
}
