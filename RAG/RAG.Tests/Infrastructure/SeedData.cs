using Microsoft.EntityFrameworkCore;
using RAG.Models.Entities;

namespace RAG.Tests.Infrastructure
{
    /// <summary>
    /// 兩位使用者 A / B 各自擁有 chat、chat log、question
    /// </summary>
    public static class SeedData
    {
        public const int UserA = 1;
        public const int UserB = 2;
        public const string UserAAccount = "userA";
        public const string UserAPassword = "passA";

        public const int ChatA = 10;
        public const int ChatB = 20;
        public const int ChatLogB = 200;
        public const int QuestionA = 31;
        public const int QuestionB = 30;
        public const int QaItemType = 1;

        public static async Task ResetAsync(RagApiFactory factory)
        {
            await factory.WithDbAsync(async db =>
            {
                await db.Database.EnsureDeletedAsync();
                await db.Database.EnsureCreatedAsync();

                db.QaType.Add(new QaType { ID = 1, NameZh = "類別", IsActive = true });
                db.QaItemType.Add(new QaItemType { ID = QaItemType, QaTypeID = 1, NameZh = "細項", IsActive = true });

                db.UserCache.AddRange(
                    new UserCache { ID = UserA, Account = UserAAccount, Password = UserAPassword, NameZh = "A", IsActive = true, Manage = "free" },
                    new UserCache { ID = UserB, Account = "userB", Password = "passB", NameZh = "B", IsActive = true, Manage = "admin" });

                db.ChatHeader.AddRange(
                    new ChatHeader { ID = ChatA, Name = "chat-a", UserID = UserA, CreateAt = DateTime.Now },
                    new ChatHeader { ID = ChatB, Name = "chat-b", UserID = UserB, CreateAt = DateTime.Now });
                db.ChatLog.AddRange(
                    new ChatLog { ID = 100, ChatHeaderID = ChatA, Questions = "qa", Answer = "aa" },
                    new ChatLog { ID = ChatLogB, ChatHeaderID = ChatB, Questions = "qb", Answer = "ab" });

                db.Questions.AddRange(
                    new Questions { ID = QuestionA, QaItemTypeID = QaItemType, UserID = UserA, Question = "question-a", Answer = "answer-a", VectorID = "va" },
                    new Questions { ID = QuestionB, QaItemTypeID = QaItemType, UserID = UserB, Question = "question-b", Answer = "answer-b", VectorID = "vb" });

                await db.SaveChangesAsync();
            });
        }

        public static Task<UserCache?> GetUserAsync(RagApiFactory factory, int id) =>
            factory.WithDbAsync(db => db.UserCache.AsNoTracking().FirstOrDefaultAsync(x => x.ID == id));

        public static Task<Questions?> GetQuestionAsync(RagApiFactory factory, int id) =>
            factory.WithDbAsync(db => db.Questions.AsNoTracking().FirstOrDefaultAsync(x => x.ID == id));

        public static Task<bool> ChatExistsAsync(RagApiFactory factory, int id) =>
            factory.WithDbAsync(db => db.ChatHeader.AnyAsync(x => x.ID == id));

        public static Task<int> ChatLogCountAsync(RagApiFactory factory, int chatId) =>
            factory.WithDbAsync(db => db.ChatLog.CountAsync(x => x.ChatHeaderID == chatId));
    }
}
