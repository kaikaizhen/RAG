using AutoMapper;
using Library.Http;
using RAG.Commons.Options;
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
        private readonly RagOption _ragOption;

        private readonly IQaTypeRepo _qaTypeRepo;
        private readonly IQaTypeItemRepo _qaTypeItemRepo;
        private readonly IQuestionsRepo _questionsRepo;


        public QaTypeService(
            IMapper mapper, 
            RagOption ragOption,
            IQaTypeRepo qaTypeRepo,
            IQaTypeItemRepo qaTypeItemRepo,
            IQuestionsRepo questionsRepo
        )
        {
            _mapper = mapper;
            _ragOption = ragOption;
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

            await _qaTypeRepo.TransactionAsync();
            try
            {
                await _questionsRepo.DeleteRangeAsync(qa.QaItemType.SelectMany(q => q.Questions));
                await _qaTypeItemRepo.DeleteRangeAsync(qa.QaItemType);
                await _qaTypeRepo.DeleteAsync(qa);

                var url = $"{_ragOption.BaseUrl}{_ragOption.DeleteTypeUrl}{Id}";
                HttpClient client = new HttpClient();
                var response = await client.DeleteAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpException((HttpStatusCode)500, "RAG Delete API Error");
                }

                await _qaTypeRepo.CommitAsync();
            }
            catch
            {
                throw;
            }
        }
    }
}
