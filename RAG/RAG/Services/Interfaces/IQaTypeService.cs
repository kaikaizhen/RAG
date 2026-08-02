using RAG.Models.Dtos.QaType;

namespace RAG.Services.Interfaces
{
    public interface IQaTypeService
    {
        /// <summary>
        /// 查詢問題種類列表
        /// </summary>
        /// <returns></returns>
        Task<List<GetQaTypeDto>> GetAsync();

        /// <summary>
        /// 新增問題種類
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        Task CreateAsync(CreateQaTypeDto dto);

        /// <summary>
        /// 更新問題種類
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        Task UpdateAsync(UpdateQaTypeDto dto);

        /// <summary>
        /// 刪除問題種類
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        Task DeleteAsync(int Id);
    }
}
