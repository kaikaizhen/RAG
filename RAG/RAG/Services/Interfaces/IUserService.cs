using RAG.Models.Dtos.User;

namespace RAG.Services.Interfaces
{
    public interface IUserService
    {
        /// <summary>
        /// 查詢使用者
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        Task<GetUserDto> GetAsyc(int Id);

        /// <summary>
        /// 新增使用者
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        Task CreateAsync(CreateUserDto dto);

        /// <summary>
        /// 更新使用者
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        Task UpdateAsync(UpdateUserDto dto);

        /// <summary>
        /// 刪除使用者
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        Task DeleteAsync(int Id);
    }
}
