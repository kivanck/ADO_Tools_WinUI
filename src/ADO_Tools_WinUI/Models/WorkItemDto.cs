using System;
using System.Collections.Generic;

namespace ADO_Tools.Models
{
    public class WorkItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string State { get; set; } = "";
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedDate { get; set; }
        public string TypeName { get; set; } = "";
        public string IterationPath { get; set; } = "";
        public string HtmlUrl { get; set; } = "";
        public List<AttachmentDto> Attachments { get; set; } = new List<AttachmentDto>();
        public Dictionary<string, object> Fields { get; set; } = new Dictionary<string, object>();
    }
}