using System;
using System.Collections.Generic;

namespace RAG.Models.Entities;

public partial class QaType
{
    public int ID { get; set; }

    public string? NameZh { get; set; }

    public string? NameEn { get; set; }

    public string? NameOther { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<QaItemType> QaItemType { get; set; } = new List<QaItemType>();
}
