using System;
using System.Collections.Generic;
using System.Threading;
using AVFoundation;
using Foundation;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OpenUtau.Audio;
using OpenUtau.Core;
using Serilog;

namespace OpenUtauMobile.iOS.Audio
{
    /// <summary>
    /// iOS audio output backend using AVAudioEngine + AVAudioPlayerNode.
    /// Pulls samples from an NAudio ISampleProvider and schedules PCM buffers.
    /// </summary>
    public class IOSAudioOutput : IAudioOutput
    {
        private const int SampleRate = 44100;
        private const int Channels = 2;
        private const int BufferFrames = 4096;

        private AVAudioEngine? _engine;
        private AVAudioPlayerNode? _playerNode;
        private AVAudioFormat? _format;
        private ISampleProvider? _sampleProvider;
        private Thread? _playbackThread;
        private volatile bool _isPlaying;
        private long _positionSamples;

        public PlaybackState PlaybackState => _isPlaying ? PlaybackState.Playing : PlaybackState.Stopped;
        public int DeviceNumber { get; set; }

        public IOSAudioOutput()
        {
            try
            {
                _engine = new AVAudioEngine();
                _playerNode = new AVAudioPlayerNode();
                _engine.AttachNode(_playerNode);
                _format = new AVAudioFormat(SampleRate, Channels, false); // deinterleaved float32
                _engine.Connect(_playerNode, _engine.MainMixerNode, _format);

                var session = AVAudioSession.SharedInstance();
                session.SetCategory(AVAudioSessionCategory.Playback);
                session.SetActive(true);

                Log.Information("IOSAudioOutput initialized (AVAudioEngine, {SampleRate}Hz, {Channels}ch)", SampleRate, Channels);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "IOSAudioOutput init failed");
            }
        }

        public void Init(ISampleProvider sampleProvider)
        {
            if (sampleProvider.WaveFormat.SampleRate != SampleRate)
            {
                sampleProvider = new WdlResamplingSampleProvider(sampleProvider, SampleRate);
            }
            _sampleProvider = sampleProvider.ToStereo();
        }

        public void Play()
        {
            if (_isPlaying || _engine == null || _playerNode == null || _sampleProvider == null) return;

            try
            {
                _isPlaying = true;
                _positionSamples = 0;
                _engine.Start();
                _playerNode.Play();
                _playbackThread = new Thread(PlaybackLoop);
                _playbackThread.Start();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "IOSAudioOutput Play failed");
                _isPlaying = false;
            }
        }

        public void Pause()
        {
            if (!_isPlaying) return;
            _isPlaying = false;
            _playbackThread?.Join();
            _playerNode?.Pause();
        }

        public void Stop()
        {
            _isPlaying = false;
            _playbackThread?.Join();
            _playerNode?.Stop();
            _playerNode?.Reset();
            _engine?.Stop();
        }

        public long GetPosition() => _positionSamples;

        public List<AudioOutputDevice> GetOutputDevices() => new List<AudioOutputDevice>();

        public void SelectDevice(Guid guid, int deviceNumber)
        {
            // iOS routes audio through the system; no manual device selection.
        }

        /// <summary>
        /// Core playback loop: read float samples from provider, convert to
        /// deinterleaved AVAudioPCMBuffer, schedule on the player node.
        /// </summary>
        private void PlaybackLoop()
        {
            if (_playerNode == null || _sampleProvider == null || _format == null)
            {
                _isPlaying = false;
                return;
            }

            float[] interleaved = new float[BufferFrames * Channels];
            bool eof = false;

            while (_isPlaying && !eof)
            {
                int samplesRead = _sampleProvider.Read(interleaved, 0, interleaved.Length);
                if (samplesRead <= 0)
                {
                    eof = true;
                    break;
                }
                int frames = samplesRead / Channels;
                if (samplesRead % Channels != 0) frames++;

                var pcmBuffer = new AVAudioPCMBuffer(_format, (uint)frames);
                pcmBuffer.FrameLength = (uint)frames;

                // Copy interleaved float data into deinterleaved channel buffers.
                unsafe
                {
                    float* ch0 = (float*)pcmBuffer.FloatChannelData[0].ToPointer();
                    float* ch1 = (float*)pcmBuffer.FloatChannelData[1].ToPointer();
                    for (int i = 0; i < frames; i++)
                    {
                        int src = i * Channels;
                        float l = src < samplesRead ? interleaved[src] : 0f;
                        float r = (src + 1) < samplesRead ? interleaved[src + 1] : 0f;
                        ch0[i] = l;
                        ch1[i] = r;
                    }
                }

                _positionSamples += frames;
                _playerNode.ScheduleBuffer(pcmBuffer, AVAudioPlayerNodeBufferOptions.Interrupts, null);
            }

            if (eof)
            {
                // Let the tail play out, then stop.
                _playerNode.ScheduleBuffer(new AVAudioPCMBuffer(_format, 0), AVAudioPlayerNodeBufferOptions.Interrupts, () =>
                {
                    _isPlaying = false;
                    _playerNode.Stop();
                    _engine?.Stop();
                });
            }
            else
            {
                _isPlaying = false;
            }
        }
    }
}
