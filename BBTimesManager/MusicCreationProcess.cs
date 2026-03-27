using System.Collections;
using System.Collections.Generic;
using System.IO;
using BBTimes.ModPatches.EnvironmentPatches;
using HarmonyLib;
using MTM101BaldAPI;
using MTM101BaldAPI.AssetTools;
using PixelInternalAPI.Extensions;
using UnityEngine;
using UnityEngine.Audio;

namespace BBTimes.Manager
{
    internal static partial class BBTimesManager
    {
        // This should be called during your mod's initialization
        // or via a prefix on MainGameManager.Initialize
        static void GetMusics()
        {
            // --- 1. Audio Mixer Setup ---
            // We grab the "Effects" group for things like explosions and the "Master" or "Music" for loops
            AudioMixerGroup effectGroup = GenericExtensions.FindResourceObjectByName<AudioMixerGroup>("Effects");

            // --- 2. All Notebooks Notification Sounds ---
            var soundNormal = ObjectCreators.CreateSoundObject(
                AssetLoader.AudioClipFromFile(Path.Combine(MiscPath, AudioFolder, "BAL_AllNotebooksNormal.wav")),
                "Vfx_BAL_CongratsNormal_0",
                SoundType.Effect, Color.green);

            soundNormal.additionalKeys = new SubtitleTimedKey[] {
                new() { key = "Vfx_BAL_CongratsNormal_1", time = 2.17f},
                new() { key = "Vfx_BAL_AllNotebooks_3", time = 4.89f},
                new() { key = "Vfx_BAL_AllNotebooks_4", time = 8.201f},
                new() { key = ".", time = 11.337f},
                new() { key = "..", time = 12.78f},
                new() { key = "...", time = 14.061f},
                new() { key = "Vfx_BAL_AllNotebooks_5", time = 14.602f}
            };

            var soundFinal = ObjectCreators.CreateSoundObject(
                AssetLoader.AudioClipFromFile(Path.Combine(MiscPath, AudioFolder, "BAL_AllNotebooksFinal.wav")),
                "Vfx_BAL_CongratsNormal_0",
                SoundType.Effect, Color.green);

            soundFinal.additionalKeys = new SubtitleTimedKey[] {
                new() { key = "Vfx_BAL_CongratsNormal_1", time = 2.17f},
                new() { key = "Vfx_BAL_AllNotebooks_3", time = 4.89f},
                new() { key = "Vfx_BAL_CongratsAngry_0", time = 7.233f},
                new() { key = ".", time = 12.653f},
                new() { key = "..", time = 13.5f},
                new() { key = "...", time = 14.302f},
                new() { key = "Vfx_BAL_AllNotebooks_5", time = 14.382f}
            };

            // Apply these sounds to existing MainGameManager resources
            GenericExtensions.FindResourceObjects<MainGameManager>().Do(man =>
                man.allNotebooksNotification = man.name.StartsWith("Lvl99999_") ? soundFinal : soundNormal);


            // --- 3. Chaos/Escape Looping Music ---
            // Chaos 0: Initial realization
            var loop0 = ScriptableObject.CreateInstance<LoopingSoundObject>();
            loop0.clips = new AudioClip[] { AssetLoader.AudioClipFromFile(Path.Combine(MiscPath, AudioFolder, "Quiet_noise_loop.wav")) };
            loop0.mixer = effectGroup;
            MainGameManagerPatches.chaos0 = loop0;

            // Chaos 1: Escalation
            var loop1 = ScriptableObject.CreateInstance<LoopingSoundObject>();
            loop1.clips = new AudioClip[] {
                AssetLoader.AudioClipFromFile(Path.Combine(MiscPath, AudioFolder, "Chaos_EarlyLoopStart.wav")),
                AssetLoader.AudioClipFromFile(Path.Combine(MiscPath, AudioFolder, "Chaos_EarlyLoop.wav"))
            };
            loop1.mixer = effectGroup;
            MainGameManagerPatches.chaos1 = loop1;

            // Chaos 2: Final Escape
            var loop2 = ScriptableObject.CreateInstance<LoopingSoundObject>();
            loop2.clips = new AudioClip[] {
                AssetLoader.AudioClipFromFile(Path.Combine(MiscPath, AudioFolder, "Chaos_FinalLoop.wav")),
                AssetLoader.AudioClipFromFile(Path.Combine(MiscPath, AudioFolder, "Chaos_FinalLoopNoise.wav"))
            };
            loop2.mixer = effectGroup;
            MainGameManagerPatches.chaos2 = loop2;


            // --- 4. Cutscene and Animation SFX ---
            MainGameManagerPatches.angryBal = ObjectCreators.CreateSoundObject(
                AssetLoader.AudioClipFromFile(Path.Combine(MiscPath, AudioFolder, "BAL_AngryGetOut.wav")),
                "Vfx_BAL_ANGRY_0",
                SoundType.Voice, Color.green);

            MainGameManagerPatches.angryBal.additionalKeys = new SubtitleTimedKey[] {
                new() { key = "Vfx_BAL_ANGRY_1", time = 0.358f},
                new() { key = "Vfx_BAL_ANGRY_2", time = 0.681f },
                new() { key = "Vfx_BAL_ANGRY_3", time = 0.934f },
                new() { key = "Vfx_BAL_ANGRY_4", time = 1.113f },
                new() { key = "Vfx_BAL_ANGRY_5", time = 1.738f }
            };
        }
    }
}
