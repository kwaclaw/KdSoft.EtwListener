namespace KdSoft.EtwLogging
{
    partial class ProcessingOptions
    {
        partial void OnConstruction() {
            this.Filter = new Filter();
            this.BatchSize = 100;
            this.MaxWriteDelayMSecs = 400;
        }
    }
}
