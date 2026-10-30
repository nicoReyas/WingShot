using UnityEditor;
using UnityEngine;

// Import the game's own music without lossy compression or loop-padding artifacts.
public class WingShotMusicImporter : AssetPostprocessor
{
    // This hook runs automatically when Unity imports or reimports an audio file.
    void OnPreprocessAudio()
    {
        // Leave every other audio asset and the project's global audio settings alone.
        if (assetPath != "Assets/Resources/Audio/Skybound.wav") return;
        var importer = (AudioImporter)assetImporter;
        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.PCM;
        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        settings.preloadAudioData = true;
        importer.defaultSampleSettings = settings;
        importer.forceToMono = false;

        importer.loadInBackground = false;
    }
}

