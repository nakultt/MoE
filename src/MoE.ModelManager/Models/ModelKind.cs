namespace MoE.ModelManager.Models;

/// <summary>
/// Identifies which managed runtime serves a model.
/// </summary>
public enum ModelKind
{
    /// <summary>The llama.cpp chat runtime.</summary>
    Text,

    /// <summary>The stable-diffusion.cpp image runtime.</summary>
    Image,

    /// <summary>The FastVideo WSL video runtime.</summary>
    Video,

    /// <summary>The in-process Parakeet speech-to-text runtime.</summary>
    SpeechToText,

    /// <summary>The in-process Kokoro text-to-speech runtime.</summary>
    TextToSpeech,
}
