using System;

namespace ADO_Tools_WinUI.Models
{
    public class AttachmentDto
    {
        public string Url { get; set; } = "";
        public string FileName { get; set; } = "";
        public long Length { get; set; }
    }
}