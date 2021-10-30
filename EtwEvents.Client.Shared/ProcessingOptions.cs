namespace KdSoft.EtwEvents
{
    public class ProcessingOptions
    {
        public int BatchSize { get; set; } = 100;
        public int MaxWriteDelayMSecs { get; set; } = 400;
        public FilterModel Filter { get; set; } = new FilterModel();
    }
}
