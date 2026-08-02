using RAG.Commons.Enums;
using RAG.Models.Entities;

namespace RAG.Commons.Helpers
{
    public static class MappingHelper
    {
        public static T ToEnum<T>(object? value, T defaultValue) where T : struct, Enum
        {
            if (value is T e)
                return e;
            if (value is string s && Enum.TryParse(s, true, out T result))
                return result;
            return defaultValue;
        }

        public static string NullToString(params string[] values)
        {
            foreach(var value in values)
            {
                if(!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }
            return string.Empty;
        }

        public static string? UserNameMapping(UserCache entity,LanguageEnum language)
        {
            return language switch
            {
                LanguageEnum.ZhTw=> entity.NameZh,
                LanguageEnum.EnUs=> entity.NameEn,
                LanguageEnum.Other =>entity.NameOther,
                _=> entity.NameEn
            };
        }
    }
}
