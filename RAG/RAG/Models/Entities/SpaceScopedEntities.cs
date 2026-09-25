namespace RAG.Models.Entities;

/// <summary>
/// Tenant-owned：聊天室屬於某個 Space（ChatLog 經由 ChatHeader 界定範圍）
/// </summary>
public partial class ChatHeader
{
    public int SpaceID { get; set; }

    public virtual Space Space { get; set; } = null!;
}

/// <summary>
/// Tenant-owned knowledge：問題屬於某個 Space；UserID 保留為建立者 / 擁有者
/// </summary>
public partial class Questions
{
    public int SpaceID { get; set; }

    public virtual Space Space { get; set; } = null!;
}
