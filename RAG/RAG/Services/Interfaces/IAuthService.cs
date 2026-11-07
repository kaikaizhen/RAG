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

        /// <summary>
        /// 以 refresh token 換發新的 access / refresh token（帳號必須仍存在）
        /// </summary>
        /// <param name="refreshToken"></param>
        /// <returns></returns>
        Task<LoginInfoDto> RefreshAsync(string refreshToken);
    }
}
