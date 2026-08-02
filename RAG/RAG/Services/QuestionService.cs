using AutoMapper;
using Library.Http;
using Newtonsoft.Json;
using RAG.Commons.Enums;
using RAG.Commons.Options;
using RAG.Models.Dtos.Question;
using RAG.Models.Dtos.RagServer;
using RAG.Models.Entities;
using RAG.Repositories.Interfaces;
using RAG.Services.Interfaces;
using System.Net;
using System.Text;

namespace RAG.Services
{
    public class QuestionService : IQuestionService
    {
        private readonly IMapper _mapper;
        private readonly RagOption _ragOption;
        private readonly IQuestionsRepo _questionsRepo;

        public QuestionService(
            IMapper mapper,
            RagOption ragOption,
            IQuestionsRepo questionsRepo
        )
        {
            _mapper = mapper;
            _ragOption = ragOption;
            _questionsRepo = questionsRepo;
        }



        public async Task<List<GetQuestionDto>> GetAsync(int userId, int? Id, QuestionEnum type)
        {
            var entitys = type switch
            {
                QuestionEnum.All => await GetByAllAsync(userId),
                QuestionEnum.Type => await GetByTypeAsync(userId, Id),
                _ => throw new HttpException((HttpStatusCode)400,"不支援查詢類型")
            };

            return _mapper.Map<List<GetQuestionDto>>(entitys);
        }

        public async Task CreateAsync(CreateQuestionDto dto)
        {
            var entity = _mapper.Map<Questions>(dto);
            await _questionsRepo.TransactionAsync();
            try
            {
                await _questionsRepo.AddAsync(entity);
                await SendRagToVectorAsync(entity);

                await _questionsRepo.CommitAsync();
            }
            catch
            {
                throw;
            }
        }

        public async Task UpdateAsync(UpdateQuestionDto dto)
        {
            var question = await _questionsRepo.GetByIdAsync(dto.ID) ?? throw new HttpException((HttpStatusCode)400,"查無問題");
            var entity = _mapper.Map<Questions>(dto);
            entity.CreateAt = question.CreateAt;
            await SendRagToVectorAsync(entity);
        }

        public async Task DeleteAsync(int Id)
        {
            var question = await _questionsRepo.GetByIdAsync(Id) ?? throw new HttpException((HttpStatusCode)400,"查無問題");

            await _questionsRepo.TransactionAsync();
            try
            {
                await _questionsRepo.DeleteAsync(question);

                var url = $"{_ragOption.BaseUrl}{_ragOption.DeleteUrl}{question.VectorID}";
                var httpClient = new HttpClient();
                var response = await httpClient.DeleteAsync(url);
                if (!response.IsSuccessStatusCode) throw new HttpException((HttpStatusCode)500, "DeleteToVector API Error");
                await _questionsRepo.CommitAsync();
            }
            catch
            {
                throw;
            }
        }

        #region Private Methods
        private async Task<List<Questions>> GetByAllAsync(int userId)
        {
            return await _questionsRepo.Get(userId);
        }

        private async Task<List<Questions>> GetByTypeAsync(int userId, int? Id)
        {
            if (Id is not int typeId) throw new HttpException((HttpStatusCode)400,"使用種類查詢功能，請帶入Id");

            return await _questionsRepo.GetByTypeIdAsync(userId, typeId);
        }

        private async Task SendRagToVectorAsync(Questions entity)
        {
            var question = await _questionsRepo.GetByIdAsync(entity.ID);

            var httpClient = new HttpClient();
            var url = $"{_ragOption.BaseUrl}{_ragOption.ToVectorUrl}";
            var content = new ToVectorDto()
            {
                Questions = new List<ToVectorDto.QuestionRequest>()
                {
                    new ToVectorDto.QuestionRequest(){
                        Id =question.ID,
                        ReferenceID = question.VectorID,
                        QaTypeId=question.QaItemType.QaType.ID,
                        QaItemTypeId =question.QaItemType.ID,
                        Question = question.Question,
                        Answer = question.Answer
                    }
                }
            };
            var json = JsonConvert.SerializeObject(content);
            var jsonContent = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(url, jsonContent);

            if (!response.IsSuccessStatusCode) throw new HttpException((HttpStatusCode)500, "ToVector API Error");

            var vectorIdString = await response.Content.ReadAsStringAsync();
            var vectorIds = JsonConvert.DeserializeObject<VectorReponseDto>(vectorIdString);

            if (vectorIds is null) throw new Exception("Exstion VectorReponseDto Error");

            entity.VectorID = vectorIds.VectorIds.First();
            await _questionsRepo.UpdateAsync(entity);
        }
        #endregion
    }
}
