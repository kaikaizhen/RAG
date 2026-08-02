using System;
using System.Collections.Generic;

namespace RAG.Models.Entities;

public partial class QaItemType
{
    public int ID { get; set; }

    public int QaTypeID { get; set; }

    public string? NameZh { get; set; }

    public string? NameEn { get; set; }

    public string? NameOther { get; set; }

    public bool IsActive { get; set; }

    public virtual QaType QaType { get; set; } = null!;

    public virtual ICollection<Questions> Questions { get; set; } = new List<Questions>();
}
