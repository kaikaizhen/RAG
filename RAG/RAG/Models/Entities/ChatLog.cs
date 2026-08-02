using System;
using System.Collections.Generic;

namespace RAG.Models.Entities;

public partial class ChatLog
{
    public int ID { get; set; }

    public int ChatHeaderID { get; set; }

    public string? Questions { get; set; }

    public string? Answer { get; set; }

    public DateTime? CreateAt { get; set; }

    public virtual ChatHeader ChatHeader { get; set; } = null!;
}
