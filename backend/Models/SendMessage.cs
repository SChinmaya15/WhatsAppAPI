using Newtonsoft.Json.Serialization;
using System.Text.Json.Serialization;

namespace backend.Models
{
    public class SendMessage
    {
        public string? To { get; set; }
        public string? From { get; set; }
        public string? Message { get; set; }
    }
}
