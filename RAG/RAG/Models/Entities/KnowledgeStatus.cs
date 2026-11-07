namespace RAG.Models.Entities;

/// <summary>
/// 知識狀態：MySQL 為權威來源，檢索後一律回查
/// </summary>
public static class KnowledgeStatus
{
    /// <summary>
    /// 已確認，可作為依據
    /// </summary>
    public const string Verified = "Verified";

    /// <summary>
    /// 可能過時：可提供但須標示較低可信度
    /// </summary>
    public const string Stale = "Stale";

    /// <summary>
    /// 推論所得、未經確認：可提供但須標示較低可信度
    /// </summary>
    public const string Inferred = "Inferred";

    /// <summary>
    /// 已被取代：一律不作為證據，也不列為類似答案
    /// </summary>
    public const string Superseded = "Superseded";

    public static readonly IReadOnlyList<string> All = new[] { Verified, Stale, Inferred, Superseded };

    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}

public partial class Questions
{
    public string Status { get; set; } = KnowledgeStatus.Verified;
}
