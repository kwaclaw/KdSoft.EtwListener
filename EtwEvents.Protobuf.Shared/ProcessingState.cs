namespace KdSoft.EtwLogging
{
    partial class ProcessingState
    {
        partial void OnConstruction() {
            this.FilterSource = new FilterSource();
            this.BatchSize = 100;
            this.MaxWriteDelayMSecs = 400;
        }
    }
}
