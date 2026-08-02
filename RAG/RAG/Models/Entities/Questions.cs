using System;
using System.Collections.Generic;

namespace RAG.Models.Entities;

public partial class Questions
{
    public int ID { get; set; }

    public int QaItemTypeID { get; set; }

    public int UserID { get; set; }

    public string? Question { get; set; }

    public string? Answer { get; set; }

    public string? VectorID { get; set; }

    public DateTime CreateAt { get; set; }

    public DateTime? UpdateAt { get; set; }

    public virtual QaItemType QaItemType { get; set; } = null!;

    public virtual UserCache User { get; set; } = null!;
}
