using AutoMapper;
using RAG.Commons.Helpers;
using RAG.Models.Dtos.QaType;
using RAG.Models.Dtos.QaTypeItem;
using RAG.Models.Entities;
using RAG.Models.Params.QaTypeItem;
using RAG.Models.ViewModels.QaTypeItem;

namespace RAG.Extensions.Mapping
{
    public class QaTypeItemMapper:Profile
    {
        public QaTypeItemMapper()
        {
            #region Get
            CreateMap<QaItemType, GetQaTypeItemDto>();
            CreateMap<GetQaTypeItemDto, GetQaTypeItemVM>();
            #endregion

            #region Create
            CreateMap<CreateQaTypeItemParam, CreateQaTypeItemDto>()
                .ForMember(x => x.NameZh, y => y.MapFrom(src => MappingHelper.NullToString(src.NameZh, src.NameEn, src.NameOther)))
                .ForMember(x => x.NameEn, y => y.MapFrom(src => MappingHelper.NullToString(src.NameEn, src.NameOther, src.NameZh)))
                .ForMember(x => x.NameOther, y => y.MapFrom(src => MappingHelper.NullToString(src.NameOther, src.NameEn, src.NameZh)));
            CreateMap<CreateQaTypeItemDto, QaItemType>();
            #endregion

            #region Update
            CreateMap<UpdateQaTypeItemParam, UpdateQaTypeItemDto>()
                .ForMember(x => x.NameZh, y => y.MapFrom(src => MappingHelper.NullToString(src.NameZh, src.NameEn, src.NameOther)))
                .ForMember(x => x.NameEn, y => y.MapFrom(src => MappingHelper.NullToString(src.NameEn, src.NameOther, src.NameZh)))
                .ForMember(x => x.NameOther, y => y.MapFrom(src => MappingHelper.NullToString(src.NameOther, src.NameEn, src.NameZh)));
            CreateMap<UpdateQaTypeItemDto, QaItemType>();
            #endregion
        }
    }
}
