using System.Collections;
using System.IO;
using MelonLoader;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using UnityEngine;

namespace DieHarder;
public static class AudioManager
{
    public class ClipData
    {
        public WaveOutEvent WaveOut { get; set; }
        public ISampleProvider VolumeProvider { get; set; }
        public AudioFileReader Reader { get; set; }
    }

    private static IEnumerator PlaySound(ClipData clipData)
    {
        if (clipData == null || clipData.WaveOut == null || clipData.Reader == null)
        {
            Debug.Log("clipData is null or has missing components.", false, 2);
            yield break;
        }

        // Reset to the start of the clip.
        clipData.Reader.Position = 0;

        // Start or resume playing the sound.
        clipData.WaveOut.Play();

        // Wait until the sound finishes playing.
        while (clipData.WaveOut != null)
        {
            // If playback is no longer active (finished or stopped), break out.
            if (clipData.WaveOut.PlaybackState != PlaybackState.Playing)
            {
                break;
            }

            yield return null;
        }

        clipData.Reader.Dispose();
        clipData.WaveOut.Dispose();
        clipData.WaveOut = null;
    }
    

    public static ClipData PlaySoundIfFileExists(string soundFilePath, float volume = 1.0f)
    {
        if (!File.Exists(soundFilePath))
        {
            Debug.Log($"Audio file not found: {soundFilePath}", false, 2);
            return null;
        }

        var reader = new AudioFileReader(soundFilePath);
        var volumeProvider = new VolumeSampleProvider(reader)
        {
            Volume = Mathf.Clamp01(volume)
        };

        var waveOut = new WaveOutEvent();
        waveOut.Init(volumeProvider);

        var clipData = new ClipData
        {
            WaveOut = waveOut,
            VolumeProvider = volumeProvider,
            Reader = reader
        };

        MelonCoroutines.Start(PlaySound(clipData));
        return clipData;
    }

    public static void StopPlayback(ClipData clipData)
    {
        if (clipData == null)
        {
            Debug.Log("Attempted to stop playback on a null clipData.", false, 1);
            return;
        }

        if (clipData.WaveOut != null)
        {
            clipData.WaveOut.Stop();
            clipData.WaveOut.Dispose();
        }

        if (clipData.Reader != null)
        {
            clipData.Reader.Dispose();
        }

        clipData.WaveOut = null;
    }
}