using System;

namespace PDFAutomation.Models
{
    public class LogEntry
    {
        public DateTime Zaman { get; set; }
        public string DosyaAdi { get; set; }
        public string OrijinalYol { get; set; }
        public string Durum { get; set; } // "basarili", "basarisiz", "tekrar"
        public string Alici { get; set; }
        public string Gonderen { get; set; }
        public int DenemeSayisi { get; set; }
        public double Sure { get; set; } // saniye
        public long DosyaBoyutu { get; set; }
        public string Hata { get; set; }
        public string Kullanici { get; set; }
    }
}
