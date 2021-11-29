namespace KdSoft.EtwLogging
{
    partial class ProcessingState
    {
        partial void OnConstruction() {
            this.BatchSize = 100;
            this.MaxWriteDelayMSecs = 400;
        }
    }
}
