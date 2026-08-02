using AutoMapper;
using RAG.Commons.Helpers;
using RAG.Models.Dtos.User;
using RAG.Models.Entities;
using RAG.Models.Params.User;
using RAG.Models.ViewModels.User;

namespace RAG.Extensions.Mapping
{
    public class UserMapper:Profile
    {
        public UserMapper()
        {
            #region Get
            CreateMap<UserCache, GetUserDto>();
            CreateMap<GetUserDto, GetUserVM>();
            #endregion

            #region Create
            CreateMap<CreateUserParam, CreateUserDto>()
                .ForMember(x => x.NameZh, y => y.MapFrom(src => MappingHelper.NullToString(src.NameZh, src.NameEn, src.NameOther)))
                .ForMember(x => x.NameEn, y => y.MapFrom(src => MappingHelper.NullToString(src.NameEn, src.NameOther, src.NameZh)))
                .ForMember(x => x.NameOther, y => y.MapFrom(src => MappingHelper.NullToString(src.NameOther, src.NameEn, src.NameZh)));
            CreateMap<CreateUserDto, UserCache>();
            #endregion

            #region Update
            CreateMap<UpdateUserParam, UpdateUserDto>()
                .ForMember(x => x.NameZh, y => y.MapFrom(src => MappingHelper.NullToString(src.NameZh, src.NameEn, src.NameOther)))
                .ForMember(x => x.NameEn, y => y.MapFrom(src => MappingHelper.NullToString(src.NameEn, src.NameOther, src.NameZh)))
                .ForMember(x => x.NameOther, y => y.MapFrom(src => MappingHelper.NullToString(src.NameOther, src.NameEn, src.NameZh)));
            CreateMap<UpdateUserDto, UserCache>();
            #endregion
        }
    }
}
