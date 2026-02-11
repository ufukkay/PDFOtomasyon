using System;

namespace PDFAutomation.Models
{
    public class EmailMessage
    {
        public string DosyaYolu { get; set; }
        public string DosyaAdi { get; set; }
        public string Gonderen { get; set; }
        public string Alici { get; set; }
        public string Konu { get; set; }
        public string Govde { get; set; }
        public long DosyaBoyutu { get; set; }
        public string Kullanici { get; set; }
        public DateTime Tarih { get; set; }
    }
}
