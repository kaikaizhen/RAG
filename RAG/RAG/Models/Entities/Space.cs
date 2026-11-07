namespace RAG.Models.Entities;

public partial class Space
{
    public int ID { get; set; }

    /// <summary>
    /// 唯一識別字（例如 default、team-a）
    /// </summary>
    public string Key { get; set; } = null!;

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreateAt { get; set; }

    public virtual ICollection<AccountSpace> AccountSpace { get; set; } = new List<AccountSpace>();
}
