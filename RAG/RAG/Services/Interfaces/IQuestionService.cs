using RAG.Commons.Enums;
using RAG.Models.Dtos.Question;

namespace RAG.Services.Interfaces
{
    public interface IQuestionService
    {
        /// <summary>
        /// 查詢問題列表
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="Id"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        Task<List<GetQuestionDto>> GetAsync(int userId, int? Id, QuestionEnum type);

        /// <summary>
        /// 新增問題
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        Task CreateAsync(CreateQuestionDto dto);

        /// <summary>
        /// 更新問題
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        Task UpdateAsync(UpdateQuestionDto dto);

        /// <summary>
        /// 刪除問題
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        Task DeleteAsync(int Id);
    }
}
