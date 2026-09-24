using AutoMapper;
using Library.Http;
using RAG.Authentication;
using RAG.Commons.Options;
using RAG.Models.Dtos.User;
using RAG.Models.Entities;
using RAG.Repositories.Interfaces;
using RAG.Services.Interfaces;
using System.Net;

namespace RAG.Services
{
    public class UserService : IUserService
    {
        private readonly IMapper _mapper;
        private readonly RagOption _ragOption;

        private readonly IUserCacheRepo _userCacheRepo;
        private readonly IQuestionsRepo _questionsRepo;
        private readonly ICurrentUserAccessor _currentUser;

        public UserService(
            IMapper mapper,
            RagOption ragOption,
            IUserCacheRepo userCacheRepo,
            IQuestionsRepo questionsRepo,
            ICurrentUserAccessor currentUser
        )
        {
            _mapper = mapper;
            _ragOption = ragOption;
            _userCacheRepo = userCacheRepo;
            _questionsRepo = questionsRepo;
            _currentUser = currentUser;
        }

        public async Task<GetUserDto> GetAsyc(int Id)
        {
            var user = await GetSelfAsync(Id);

            return _mapper.Map<GetUserDto>(user);
        }

        public async Task CreateAsync(CreateUserDto dto)
        {
            if (await _userCacheRepo.IncludeAccountAsync(dto.Account)) throw new HttpException((HttpStatusCode)400, "使用者帳號已經存在，請使用其他帳號註冊");

            var entity = _mapper.Map<UserCache>(dto);

            await _userCacheRepo.AddAsync(entity);
        }

        public async Task UpdateAsync(UpdateUserDto dto)
        {
            var user = await GetSelfAsync(dto.ID);

            var entity = _mapper.Map<UserCache>(dto);

            // 權限相關欄位由伺服器端保留，不接受使用者自行變更
            entity.Manage = user.Manage;
            entity.IsActive = user.IsActive;

            await _userCacheRepo.UpdateAsync(entity);
        }

        public async Task DeleteAsync(int Id)
        {
            var user = await GetSelfAsync(Id);

            await _userCacheRepo.TransactionAsync();
            try
            {
                var httpClient = new HttpClient();
                foreach(var item in user.Questions)
                {
                    var url = $"{_ragOption.BaseUrl}{_ragOption.DeleteUrl}{item.VectorID}";

                    var response = await httpClient.DeleteAsync(url);

                    if (!response.IsSuccessStatusCode) throw new HttpException((HttpStatusCode)500,"DeleteVector API Error");
                }
                

                await _questionsRepo.DeleteRangeAsync(user.Questions);
                await _userCacheRepo.DeleteAsync(user);

                await _userCacheRepo.CommitAsync();
            }
            catch
            {
                throw;
            }
        }

        #region Private Methods
        /// <summary>
        /// 僅允許存取本人帳號；非本人與不存在一律回 404，避免洩漏其他帳號是否存在
        /// </summary>
        private async Task<UserCache> GetSelfAsync(int Id)
        {
            if (Id != _currentUser.UserId) throw new HttpException(HttpStatusCode.NotFound, "查無使用者");

            return await _userCacheRepo.GetByIdAsync(Id) ?? throw new HttpException(HttpStatusCode.NotFound, "查無使用者");
        }
        #endregion
    }
}
