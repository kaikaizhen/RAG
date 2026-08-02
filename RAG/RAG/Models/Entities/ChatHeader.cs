using System;
using System.Collections.Generic;

namespace RAG.Models.Entities;

public partial class ChatHeader
{
    public int ID { get; set; }

    public string Name { get; set; } = null!;

    public int? UserID { get; set; }

    public DateTime CreateAt { get; set; }

    public DateTime? UpdateAt { get; set; }

    public virtual ICollection<ChatLog> ChatLog { get; set; } = new List<ChatLog>();

    public virtual UserCache? User { get; set; }
}
