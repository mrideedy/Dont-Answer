using UnityEditor;
using UnityEngine;

namespace DontAnswer.EditorTools
{
    /// <summary>Keeps the game's audio small: mono, compressed, and loaded the right way for its length.</summary>
    class AudioImportRules : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.Contains("/_Game/Resources/")) return;
            var imp = (AudioImporter)assetImporter;
            imp.forceToMono = true;
            bool voice = assetPath.Contains("/Resources/Voice/");
            bool loop = assetPath.Contains("_loop") || assetPath.Contains("rain_window") || assetPath.Contains("house_tone") ||
                        assetPath.Contains("basement_tone") || assetPath.Contains("drone_dread") || assetPath.Contains("music_box") ||
                        assetPath.Contains("heartbeat") || assetPath.Contains("faucet_drip") || assetPath.Contains("breath_door");
            var s = imp.defaultSampleSettings;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = voice ? 0.45f : 0.6f;
            s.loadType = voice || loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            imp.defaultSampleSettings = s;
        }
    }
}
