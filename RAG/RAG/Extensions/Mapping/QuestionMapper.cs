using AutoMapper;
using Microsoft.Identity.Client;
using RAG.Models.Dtos.Question;
using RAG.Models.Entities;
using RAG.Models.Params.Question;
using RAG.Models.ViewModels.Question;

namespace RAG.Extensions.Mapping
{
    public class QuestionMapper:Profile
    {
        public QuestionMapper() 
        {
            #region Get
            CreateMap<Questions, GetQuestionDto>();
            CreateMap<GetQuestionDto, GetQuestionVM>();
            #endregion

            #region Create
            CreateMap<CreateQuestionParam, CreateQuestionDto>();
            CreateMap<CreateQuestionDto, Questions>();
            #endregion

            #region Update
            CreateMap<UpdateQuestionParam, UpdateQuestionDto>();
            CreateMap<UpdateQuestionDto, Questions>();
            #endregion

        }
    }
}
