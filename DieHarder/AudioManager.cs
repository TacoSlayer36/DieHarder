using Il2CppRUMBLE.Audio;
using Il2CppRUMBLE.Pools;
using System;
using System.IO;
using UnityEngine;
using static Il2CppRUMBLE.Audio.AudioCall;

namespace DieHarder;

/// <summary>
/// Audio Manager to Create AudioCalls
/// </summary>
public static class AudioManager
{
    /// <summary>
    /// CreateAudioCalls Triggers this but leaving this visible for others if they want it
    /// </summary>
    public static AudioClip LoadWavFile(string filePath)
    {
        if (!File.Exists(filePath)) return null;

        byte[] fileBytes = File.ReadAllBytes(filePath);
        return LoadWavFromBytes(fileBytes, Path.GetFileNameWithoutExtension(filePath));
    }

    private static AudioClip LoadWavFromBytes(byte[] fileBytes, string name)
    {
        // Find the "data" chunk
        int headerOffset = 12; // Start after RIFF header
        int dataOffset = -1;
        int dataSize = 0;

        while (headerOffset < fileBytes.Length - 8)
        {
            string chunkName = System.Text.Encoding.ASCII.GetString(fileBytes, headerOffset, 4);
            int chunkSize = BitConverter.ToInt32(fileBytes, headerOffset + 4);

            if (chunkName == "data")
            {
                dataOffset = headerOffset + 8;
                dataSize = chunkSize;
                break;
            }

            headerOffset += 8 + chunkSize;
        }

        if (dataOffset == -1)
        {
            return null;
        }

        // Get format info from "fmt " chunk
        int channels = fileBytes[22];
        int sampleRate = BitConverter.ToInt32(fileBytes, 24);

        int sampleCount = dataSize / 2 / channels;
        float[] samples = new float[sampleCount * channels];

        for (int i = 0; i < sampleCount * channels; i++)
        {
            short sample = BitConverter.ToInt16(fileBytes, dataOffset + i * 2);
            samples[i] = sample / 32768f;
        }

        AudioClip clip = AudioClip.Create(name, sampleCount, channels, sampleRate, false);
        clip.SetData(samples, 0);

        return clip;
    }

    /// <summary>
    /// Plays a specified AudioCall at a Position
    /// </summary>
    public static PooledAudioSource PlaySound(AudioCall audioCall, Vector3 position, bool isLooping = false)
    {
        return Il2CppRUMBLE.Managers.AudioManager.instance.Play(audioCall, position, isLooping);
    }

    /// <summary>
    /// Creates an AudioCall and sets the clip to the file. Must be wav file. Sets the AudioCall/clip HideFlags to HideAndDontSave
    /// </summary>
    public static AudioCall CreateAudioCall(string filePath, float volume)
    {
        string[] filePathSplit = filePath.Replace(".wav", "").Split('\\');
        string name = filePathSplit[filePathSplit.Length - 1];
        bool fileExists = File.Exists(filePath);
        if (fileExists)
        {
            AudioCall audioCall = ScriptableObject.CreateInstance<AudioCall>();
            audioCall.name = name;

            GeneralAudioSettings generalSettings = new GeneralAudioSettings();
            generalSettings.SetVolume(volume);
            generalSettings.Pitch = 1;
            audioCall.generalSettings = generalSettings;

            SpatialAudioSettings spatialSettings = new SpatialAudioSettings();
            spatialSettings.CustomReverbZoneMixCurve = new AnimationCurve();
            spatialSettings.CustomRolloffCurve = new AnimationCurve();
            spatialSettings.CustomSpatialBlendCurve = new AnimationCurve();
            spatialSettings.CustomSpreadCurve = new AnimationCurve();
            audioCall.spatialSettings = spatialSettings;

            AudioCall.WeightedClip weightedClip = new AudioCall.WeightedClip();
            AudioClip clip = AudioManager.LoadWavFile(filePath);

            if (clip == null)
            {
                fileExists = false;
                return null;
            }

            clip.name = name;
            clip.hideFlags = HideFlags.HideAndDontSave;
            clip.LoadAudioData();
            weightedClip.Clip = clip;

            AudioCall.WeightedClip[] weightedClips = new AudioCall.WeightedClip[1] { weightedClip };
            weightedClips[0].Weight = 1;
            audioCall.clips = weightedClips;
            audioCall.clips[0] = audioCall.GetRandomClip();
            audioCall.clips[0].Clip = clip;

            audioCall.hideFlags = HideFlags.HideAndDontSave;
            audioCall.clips[0].Clip.hideFlags = HideFlags.HideAndDontSave;
            return audioCall;
        }
        else
        {
            return null;
        }
    }

    /// <summary>
    /// Creates a list of AudioCalls and sets the clips to the files. Must be wav files. Sets the AudioCalls/clips HideFlags to HideAndDontSave
    /// </summary>
    public static AudioCall[] CreateAudioCalls(string[] filePaths, float volume)
    {
        AudioCall[] results = new AudioCall[filePaths.Length];
        for (int i = 0; i < filePaths.Length; i++)
        {
            string[] filePathSplit = filePaths[i].Replace(".wav", "").Split('\\');
            string name = filePathSplit[filePathSplit.Length - 1];
            bool fileExists = File.Exists(filePaths[i]);
            if (fileExists)
            {
                AudioCall audioCall = ScriptableObject.CreateInstance<AudioCall>();
                audioCall.name = name;

                GeneralAudioSettings generalSettings = new GeneralAudioSettings();
                generalSettings.SetVolume(volume);
                generalSettings.Pitch = 1;
                audioCall.generalSettings = generalSettings;

                SpatialAudioSettings spatialSettings = new SpatialAudioSettings();
                spatialSettings.CustomReverbZoneMixCurve = new AnimationCurve();
                spatialSettings.CustomRolloffCurve = new AnimationCurve();
                spatialSettings.CustomSpatialBlendCurve = new AnimationCurve();
                spatialSettings.CustomSpreadCurve = new AnimationCurve();
                audioCall.spatialSettings = spatialSettings;

                AudioCall.WeightedClip weightedClip = new AudioCall.WeightedClip();
                AudioClip clip = AudioManager.LoadWavFile(filePaths[i]);

                if (clip == null)
                {
                    results[i] = null;
                    continue;
                }

                clip.name = name;
                clip.hideFlags = HideFlags.HideAndDontSave;
                clip.LoadAudioData();
                weightedClip.Clip = clip;

                AudioCall.WeightedClip[] weightedClips = new AudioCall.WeightedClip[1] { weightedClip };
                weightedClips[0].Weight = 1;
                audioCall.clips = weightedClips;
                audioCall.clips[0] = audioCall.GetRandomClip();
                audioCall.clips[0].Clip = clip;

                audioCall.hideFlags = HideFlags.HideAndDontSave;
                audioCall.clips[0].Clip.hideFlags = HideFlags.HideAndDontSave;
                results[i] = audioCall;
                continue;
            }
            else
            {
                results[i] = null;
                continue;
            }
        }
        return results;
    }
}
