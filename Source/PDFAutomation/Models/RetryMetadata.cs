using System;
using System.Collections.Generic;

namespace PDFAutomation.Models
{
    public class RetryMetadata
    {
        public string DosyaAdi { get; set; }
        public string DosyaYolu { get; set; }
        public int DenemeSayisi { get; set; }
        public DateTime SonDeneme { get; set; }
        public DateTime SonrakiDeneme { get; set; }
        public List<string> Hatalar { get; set; } = new List<string>();
    }
}
