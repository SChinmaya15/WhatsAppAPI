namespace backend.Models
{
    public class WhatsAppRequest
    {        
        public string to { get; set; }
        public string type { get; set; }
        public object text { get; set; }
        public object template { get; set; }
        public string messaging_product { get; set; }
    }
}
