using AutoMapper;
using RAG.Commons.Enums;
using RAG.Commons.Helpers;
using RAG.Models.Dtos.Auth;
using RAG.Models.Entities;
using RAG.Models.Params.Auth;
using RAG.Models.ViewModels.Auth;

namespace RAG.Extensions.Mapping
{
    public class AuthMapper:Profile
    {
        public AuthMapper()
        {
            #region 登入
            CreateMap<LoginParam, LoginDto>();
            CreateMap<UserCache, LoginInfoDto>()
                .ForMember(x=>x.UserID,y=>y.MapFrom(src=>src.ID))
                .ForMember(x=>x.Name, y => y.MapFrom((src, dest, member, context) =>
                {
                    var lang = MappingHelper.ToEnum(context.Items["Language"], LanguageEnum.Other);

                    return MappingHelper.UserNameMapping(src, lang)??string.Empty;
                }));

            CreateMap<LoginInfoDto, LoginVM>();
            #endregion
        }
    }
}
