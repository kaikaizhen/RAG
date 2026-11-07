using AutoMapper;
using Library.Http;
using RAG.Models.Dtos.QaType;
using RAG.Models.Entities;
using RAG.Repositories.Interfaces;
using RAG.Services.Interfaces;
using System.Net;

namespace RAG.Services
{
    public class QaTypeService:IQaTypeService
    {
        private readonly IMapper _mapper;

        private readonly IQaTypeRepo _qaTypeRepo;
        private readonly IQaTypeItemRepo _qaTypeItemRepo;
        private readonly IQuestionsRepo _questionsRepo;


        public QaTypeService(
            IMapper mapper, 
            IQaTypeRepo qaTypeRepo,
            IQaTypeItemRepo qaTypeItemRepo,
            IQuestionsRepo questionsRepo
        )
        {
            _mapper = mapper;
            _qaTypeRepo = qaTypeRepo;
            _qaTypeItemRepo= qaTypeItemRepo;
            _questionsRepo = questionsRepo;
        }

        public async Task<List<GetQaTypeDto>> GetAsync()
        {
            var entities = await _qaTypeRepo.GetAsync();
            var dtos = _mapper.Map<List<GetQaTypeDto>>(entities);

            return dtos;
        }

        public async Task CreateAsync(CreateQaTypeDto dto)
        {
            var entity = _mapper.Map<QaType>(dto);
            await _qaTypeRepo.AddAsync(entity);
        }

        public async Task UpdateAsync(UpdateQaTypeDto dto)
        {
            var qa = await _qaTypeRepo.GetByIdAsync(dto.ID) ?? throw new HttpException((HttpStatusCode)400,"查無問題種類");

            var entity = _mapper.Map<QaType>(dto);
            await _qaTypeRepo.UpdateAsync(entity);
        }

        public async Task DeleteAsync(int Id)
        {
            var qa = await _qaTypeRepo.GetByIdAsync(Id) ?? throw new HttpException((HttpStatusCode)400, "查無問題種類");

            // 全域共用字典：仍有任何 Space 的問題使用時拒絕刪除，不跨 Space 連帶刪除問題與向量
            if (await _questionsRepo.AnyByQaItemTypeIdsAsync(qa.QaItemType.Select(i => i.ID)))
            {
                throw new HttpException(HttpStatusCode.Conflict, "仍有問題使用此問題種類，無法刪除");
            }

            await _qaTypeRepo.TransactionAsync();

            await _qaTypeItemRepo.DeleteRangeAsync(qa.QaItemType.ToList());
            await _qaTypeRepo.DeleteAsync(qa);

            await _qaTypeRepo.CommitAsync();
        }
    }
}
