using AutoMapper;
using RAG.Models.Dtos.Chat;
using RAG.Models.Dtos.RagServer;
using RAG.Models.Entities;
using RAG.Models.Params.Chat;
using RAG.Models.ViewModels.Chat;

namespace RAG.Extensions.Mapping
{
    public class ChatMapper:Profile
    {
        public ChatMapper() 
        {
            #region SendChat
            CreateMap<SendChatParam, ChatDto>();
            CreateMap<ChatDto, SendChatDto>();
            CreateMap<ChatReponseDto, ChatVM>();
            #endregion

            #region GetChat
            CreateMap<ChatHeader, GetChatHistoryDto>();
            CreateMap<GetChatHistoryDto, GetChatHistoryVM>();
            #endregion

            #region GetChatLog
            CreateMap<ChatLog, GetChatLogHistoryDto>();
            CreateMap<GetChatLogHistoryDto, GetChatLogHistoryVM>();
            #endregion

        }
    }
}
