namespace RAG.Models.Entities;

/// <summary>
/// 使用者的應用程式權杖（Personal Access Token）：給使用者自己的 AI / 外部服務呼叫 API。
/// 只存雜湊；明文只在建立時回傳一次。權杖只能做 <see cref="Scopes"/> 列出的事，且不能直接寫入知識庫。
/// </summary>
public class ApiToken
{
    public int ID { get; set; }

    public int UserID { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 明文的前幾碼，僅供使用者辨識是哪一把（不足以還原權杖）
    /// </summary>
    public string TokenPrefix { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256（十六進位）；權杖是 256 位元亂數，不需要加鹽或慢雜湊
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// 以逗號分隔的權限範圍（見 ApiTokenScopes）
    /// </summary>
    public string Scopes { get; set; } = string.Empty;

    /// <summary>
    /// 以逗號分隔的可用 Space 編號；null 表示使用者所有的 Space
    /// </summary>
    public string? SpaceIDs { get; set; }

    public DateTime CreateAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public virtual UserCache User { get; set; } = null!;
}

public static class DraftStatus
{
    public const string Pending = "Pending";

    /// <summary>
    /// 有一個請求正在接受這筆草稿（寫入知識庫與向量中）。用來擋住連點 / 重複請求，避免產生兩筆知識
    /// </summary>
    public const string Accepting = "Accepting";
    public const string Accepted = "Accepted";
    public const string Rejected = "Rejected";

    /// <summary>可以查詢的狀態（Accepting 是內部的處理中狀態，不對外）</summary>
    public static readonly IReadOnlyList<string> All = new[] { Pending, Accepted, Rejected };
}

/// <summary>
/// 暫存區的草稿：還沒有 embedding，也不在向量資料庫裡。使用者審查並接受後才會成為正式知識。
/// 草稿屬於建立它的使用者與 Space，同一個 Space 的其他成員看不到。
/// </summary>
public class KnowledgeDraft
{
    public int ID { get; set; }

    public int SpaceID { get; set; }

    public int UserID { get; set; }

    public string Question { get; set; } = string.Empty;

    public string Answer { get; set; } = string.Empty;

    public int? QaItemTypeID { get; set; }

    /// <summary>
    /// 來源：權杖名稱，或 web（使用者自己在前端建立）
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// 來源端自己的識別（例如筆記編號、網址），供對方對應回去
    /// </summary>
    public string? SourceRef { get; set; }

    /// <summary>
    /// 問題與答案的 SHA-256，用於避免同一份內容重複進入暫存區
    /// </summary>
    public string ContentHash { get; set; } = string.Empty;

    public string Status { get; set; } = DraftStatus.Pending;

    public int? AcceptedQuestionID { get; set; }

    public DateTime CreateAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// 樂觀並行控制：每次狀態變更都換新值，兩個請求同時改同一筆時，只有一個會成功
    /// </summary>
    public string Version { get; set; } = Guid.NewGuid().ToString("N");

    public virtual Space Space { get; set; } = null!;

    public virtual UserCache User { get; set; } = null!;
}
