namespace AFrame.Unity
{
    public interface IAFrameSmokeScenario
    {
        void BeginSmoke();
        bool SmokeComplete { get; }
        void VerifySmoke();
    }
}
