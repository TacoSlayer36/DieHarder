using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace DieHarder;
public static class AudioManager
{
    public static AudioClip LoadWavFile(string filePath)
    {
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
            MelonLoader.MelonLogger.Error("Could not find data chunk in WAV file");
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

    public static IEnumerator SilenceAudioAfter(AudioSource audioSource, float time)
    {
        yield return new WaitForSeconds(time);
        audioSource.volume = 0.1f;
        yield return new WaitForFixedUpdate();
        audioSource.volume = 0.0f;
    }
}