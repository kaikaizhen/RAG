namespace RAG.Models.Entities;

/// <summary>
/// 帳號 ↔ Space membership（many-to-many）；Space authorization 的權威資料
/// </summary>
public partial class AccountSpace
{
    public int ID { get; set; }

    public int UserID { get; set; }

    public int SpaceID { get; set; }

    /// <summary>
    /// owner / member / viewer
    /// </summary>
    public string Role { get; set; } = null!;

    public bool IsActive { get; set; }

    /// <summary>
    /// 帳號的預設 Space（未指定 Space 時使用；多筆預設視為無法決定）
    /// </summary>
    public bool IsDefault { get; set; }

    public DateTime CreateAt { get; set; }

    public virtual UserCache User { get; set; } = null!;

    public virtual Space Space { get; set; } = null!;
}
