using AutoMapper;
using Library.Http;
using RAG.Commons.Options;
using RAG.Models.Dtos.QaTypeItem;
using RAG.Models.Entities;
using RAG.Repositories.Interfaces;
using RAG.Services.Interfaces;
using System.Net;

namespace RAG.Services
{
    public class QaTypeItemService : IQaTypeItemService
    {
        private readonly IMapper _mapper;
        private readonly RagOption _ragOption;
        private readonly IQaTypeItemRepo _qaTypeItemRepo;
        private readonly IQuestionsRepo _questionsRepo;

        public QaTypeItemService(
            IMapper mapper,
            RagOption ragOption,
            IQaTypeItemRepo qaTypeItemRepo,
            IQuestionsRepo questionsRepo
        )
        {
            _mapper = mapper;
            _ragOption = ragOption;
            _qaTypeItemRepo = qaTypeItemRepo;
            _questionsRepo = questionsRepo;
        }

        public async Task<List<GetQaTypeItemDto>> Get()
        {
            var entities = await _qaTypeItemRepo.GetList();
            var dtos = _mapper.Map<List<GetQaTypeItemDto>>(entities);

            return dtos;
        }

        public async Task CreateAsync(CreateQaTypeItemDto dto)
        {
            var entity = _mapper.Map<QaItemType>(dto);
            await _qaTypeItemRepo.AddAsync(entity);
        }

        public async Task UpdateAsync(UpdateQaTypeItemDto dto)
        {
            var qaItem = await _qaTypeItemRepo.GetByIdAsync(dto.ID) ?? throw new HttpException((HttpStatusCode)400,"查無此問題種類細項");

            var entity = _mapper.Map<QaItemType>(dto);
            await _qaTypeItemRepo.UpdateAsync(entity);
        }

        public async Task DeleteAsync(int Id)
        {
            var qaItem = await _qaTypeItemRepo.GetByIdAsync(Id) ?? throw new HttpException((HttpStatusCode)400, "查無此問題種類細項");

            await _qaTypeItemRepo.TransactionAsync();
            try
            {
                foreach (var item in qaItem.Questions)
                {
                    var url = $"{_ragOption.BaseUrl}{_ragOption.DeleteUrl}{item.VectorID}";
                    var httpClient = new HttpClient();
                    var response = await httpClient.DeleteAsync(url);

                    if (!response.IsSuccessStatusCode) throw new HttpException((HttpStatusCode)500, "Delete Question API Error");
                }

                await _questionsRepo.DeleteRangeAsync(qaItem.Questions);
                await _qaTypeItemRepo.DeleteAsync(qaItem);

                await _qaTypeItemRepo.CommitAsync();
            }
            catch
            {
                throw;
            }
        }
    }
}
