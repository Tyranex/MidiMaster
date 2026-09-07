using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NAudio.Wave;

namespace SS14_MIDI_IDE
{
    public class WaveformRenderer
    {
        public static async Task<float[]> GeneratePeaksAsync(string filePath, int samplesPerPeak)
        {
            return await Task.Run(() =>
            {
                var peaks = new List<float>();

                try
                {
                    using (var reader = new AudioFileReader(filePath))
                    {
                        var buffer = new float[reader.WaveFormat.SampleRate * reader.WaveFormat.Channels];
                        int read;
                        float max = 0;
                        int sampleCount = 0;

                        var provider = (ISampleProvider)reader;
                        while ((read = provider.Read(buffer.AsSpan(0, buffer.Length))) > 0)
                        {
                            for (int i = 0; i < read; i += reader.WaveFormat.Channels)
                            {
                                // Mono mix for peak detection
                                float sample = 0;
                                for (int c = 0; c < reader.WaveFormat.Channels; c++)
                                {
                                    sample += Math.Abs(buffer[i + c]);
                                }
                                sample /= reader.WaveFormat.Channels;

                                if (sample > max) max = sample;
                                
                                sampleCount++;

                                if (sampleCount >= samplesPerPeak)
                                {
                                    peaks.Add(max);
                                    max = 0;
                                    sampleCount = 0;
                                }
                            }
                        }
                        
                        if (sampleCount > 0)
                        {
                            peaks.Add(max);
                        }
                    }
                }
                catch
                {
                    // If error loading, return empty peaks
                }

                return peaks.ToArray();
            });
        }
    }
}
