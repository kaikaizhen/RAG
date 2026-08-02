using RAG.Models.Dtos.Auth;

namespace RAG.Services.Interfaces
{
    public interface IAuthService
    {
        /// <summary>
        /// 登入
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        Task<LoginInfoDto> LoginAsync(LoginDto dto);
    }
}
