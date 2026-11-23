using System;
using System.IO;
using UnityEngine;

/// Push-to-talk: StartRecording() / StopRecording() -> WAV PCM 16-bit mono 16 kHz.
public class MicRecorder : MonoBehaviour
{
    public int sampleRate = 16000;
    public int maxSeconds = 30;
    public string deviceName = ""; // gol = microfonul implicit

    AudioClip clip;
    string device;
    public bool IsRecording { get; private set; }

    public void StartRecording()
    {
        if (IsRecording) return;
        if (Microphone.devices.Length == 0) { Debug.LogError("Nu exista microfon."); return; }
        device = string.IsNullOrEmpty(deviceName) ? Microphone.devices[0] : deviceName;
        clip = Microphone.Start(device, false, maxSeconds, sampleRate);
        IsRecording = true;
    }

    /// Opreste si intoarce WAV-ul (null daca e prea scurt).
    public byte[] StopRecording()
    {
        if (!IsRecording) return null;
        int pos = Microphone.GetPosition(device);
        Microphone.End(device);
        IsRecording = false;

        if (pos < sampleRate * 0.3f) { Debug.LogWarning("Inregistrare prea scurta."); return null; }

        var samples = new float[pos * clip.channels];
        clip.GetData(samples, 0);
        return EncodeWav(samples, clip.channels, clip.frequency);
    }

    static byte[] EncodeWav(float[] samples, int channels, int freq)
    {
        using (var ms = new MemoryStream())
        using (var w = new BinaryWriter(ms))
        {
            int dataLen = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + dataLen);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16); w.Write((short)1); w.Write((short)channels);
            w.Write(freq); w.Write(freq * channels * 2);
            w.Write((short)(channels * 2)); w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(dataLen);
            foreach (var s in samples)
                w.Write((short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
            return ms.ToArray();
        }
    }
}