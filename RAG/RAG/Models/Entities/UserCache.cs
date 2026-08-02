using System;
using System.Collections.Generic;

namespace RAG.Models.Entities;

public partial class UserCache
{
    public int ID { get; set; }

    public string Account { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string? NameZh { get; set; }

    public string? NameEn { get; set; }

    public string? NameOther { get; set; }

    public bool IsActive { get; set; }

    public string Manage { get; set; } = null!;

    public virtual ICollection<ChatHeader> ChatHeader { get; set; } = new List<ChatHeader>();

    public virtual ICollection<Questions> Questions { get; set; } = new List<Questions>();
}
