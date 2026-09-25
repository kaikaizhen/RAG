using AutoMapper;
using Library.Http;
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
        private readonly IQaTypeItemRepo _qaTypeItemRepo;
        private readonly IQuestionsRepo _questionsRepo;

        public QaTypeItemService(
            IMapper mapper,
            IQaTypeItemRepo qaTypeItemRepo,
            IQuestionsRepo questionsRepo
        )
        {
            _mapper = mapper;
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

            // 全域共用字典：仍有任何 Space 的問題使用時拒絕刪除，不跨 Space 連帶刪除問題與向量
            if (await _questionsRepo.AnyByQaItemTypeIdsAsync(new[] { qaItem.ID }))
            {
                throw new HttpException(HttpStatusCode.Conflict, "仍有問題使用此問題種類細項，無法刪除");
            }

            await _qaTypeItemRepo.DeleteAsync(qaItem);
        }
    }
}
