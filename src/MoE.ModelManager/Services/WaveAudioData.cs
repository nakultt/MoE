namespace MoE.ModelManager.Services;

internal sealed record WaveAudioData(int SampleRate, float[] Samples)
{
    public double DurationSeconds => Samples.Length / (double)SampleRate;
}
