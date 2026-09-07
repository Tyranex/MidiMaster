using System;
using NAudio.Wave;
using NVorbis;

namespace SS14_MIDI_IDE;

internal class VorbisSampleProvider : ISampleProvider
{
	private VorbisReader _reader;

	public WaveFormat WaveFormat { get; }

	public VorbisSampleProvider(VorbisReader reader)
	{
		_reader = reader;
		WaveFormat = NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(reader.SampleRate, reader.Channels);
	}

	public int Read(float[] buffer, int offset, int count)
	{
		return _reader.ReadSamples(buffer, offset, count);
	}

	public int Read(Span<float> buffer)
	{
		float[] array = new float[buffer.Length];
		int num = _reader.ReadSamples(array, 0, array.Length);
		new Span<float>(array, 0, num).CopyTo(buffer);
		return num;
	}
}
