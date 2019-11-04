namespace EtwEvents.WebClient.Models
{
    public class SetFilterRequest
    {
        public string? SessionName { get; set; }
        public string? CSharpFilter { get; set; }
    }

    public class TestFilterRequest
    {
        public string? Host { get; set; }
        public string? CSharpFilter { get; set; }
    }
}
