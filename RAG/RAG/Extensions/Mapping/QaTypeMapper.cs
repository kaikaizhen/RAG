using AutoMapper;
using RAG.Commons.Helpers;
using RAG.Models.Dtos.QaType;
using RAG.Models.Entities;
using RAG.Models.Params.QaType;
using RAG.Models.ViewModels.QaType;

namespace RAG.Extensions.Mapping
{
    public class QaTypeMapper:Profile
    {
        public QaTypeMapper()
        {
            #region Get
            CreateMap<QaType, GetQaTypeDto>();
            CreateMap<GetQaTypeDto, GetQaTypeVM>();
            #endregion

            #region Create
            CreateMap<CreateQaTypeParam, CreateQaTypeDto>()
                .ForMember(x => x.NameZh, y => y.MapFrom(src => MappingHelper.NullToString(src.NameZh, src.NameEn, src.NameOther)))
                .ForMember(x => x.NameEn, y => y.MapFrom(src => MappingHelper.NullToString(src.NameEn, src.NameOther, src.NameZh)))
                .ForMember(x => x.NameOther, y => y.MapFrom(src => MappingHelper.NullToString(src.NameOther, src.NameEn, src.NameZh)));
            CreateMap<CreateQaTypeDto, QaType>();
            #endregion

            #region Update
            CreateMap<UpdateQaTypeParam, UpdateQaTypeDto>()
                .ForMember(x => x.NameZh, y => y.MapFrom(src => MappingHelper.NullToString(src.NameZh, src.NameEn, src.NameOther)))
                .ForMember(x => x.NameEn, y => y.MapFrom(src => MappingHelper.NullToString(src.NameEn, src.NameOther, src.NameZh)))
                .ForMember(x => x.NameOther, y => y.MapFrom(src => MappingHelper.NullToString(src.NameOther, src.NameEn, src.NameZh)));
            CreateMap<UpdateQaTypeDto, QaType>();
            #endregion
        }
    }
}
