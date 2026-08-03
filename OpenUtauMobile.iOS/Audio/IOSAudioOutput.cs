using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
    /// Pulls samples from an NAudio ISampleProvider and schedules PCM buffers,
    /// throttled to keep the scheduled-but-unplayed queue bounded so
    /// GetPosition() tracks the actual playback position (not the scheduled one).
    /// </summary>
    public class IOSAudioOutput : IAudioOutput
    {
        private const int SampleRate = 44100;
        private const int Channels = 2;
        private const int BufferFrames = 4096;
        // Keep at most ~1.5s of scheduled-but-unplayed audio.
        private const int MaxPendingFrames = SampleRate * 3 / 2;

        private AVAudioEngine? _engine;
        private AVAudioPlayerNode? _playerNode;
        private AVAudioFormat? _format;
        private ISampleProvider? _sampleProvider;
        private Thread? _playbackThread;
        private volatile bool _isPlaying;
        private long _scheduledFrames;

        public PlaybackState PlaybackState => _isPlaying ? PlaybackState.Playing : PlaybackState.Stopped;
        public int DeviceNumber { get; set; }

        public IOSAudioOutput()
        {
            try
            {
                _engine = new AVAudioEngine();
                _playerNode = new AVAudioPlayerNode();
                _engine.AttachNode(_playerNode);
                // AVAudioFormat(double sampleRate, uint channels) -> deinterleaved float32
                _format = new AVAudioFormat(SampleRate, Channels);
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
                NSError? error = null;
                if (!_engine.StartAndReturnError(out error))
                {
                    Log.Error("AVAudioEngine start failed: {Error}", error?.LocalizedDescription ?? "unknown");
                    return;
                }
                _isPlaying = true;
                _scheduledFrames = 0;
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

        /// <summary>
        /// Playback position in bytes (stereo float samples * 4), matching the
        /// convention used by PlaybackManager.UpdatePlayPos().
        /// </summary>
        public long GetPosition()
        {
            return GetPlayedFrames() * sizeof(float);
        }

        public List<AudioOutputDevice> GetOutputDevices() => new List<AudioOutputDevice>();

        public void SelectDevice(Guid guid, int deviceNumber)
        {
            // iOS routes audio through the system; no manual device selection.
        }

        /// <summary>
        /// Actual frames rendered by the player node (0 if not available yet).
        /// </summary>
        private long GetPlayedFrames()
        {
            try
            {
                if (_playerNode == null) return 0;
                var nodeTime = _playerNode.LastRenderTime;
                if (nodeTime == null) return 0;
                var playerTime = _playerNode.GetPlayerTimeFromNodeTime(nodeTime);
                if (playerTime == null) return 0;
                return Math.Max(0, playerTime.SampleTime);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Core playback loop: read float samples from provider, convert to
        /// deinterleaved AVAudioPcmBuffer, schedule on the player node.
        /// Throttles so GetPlayedFrames() stays meaningful (bounded queue).
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
                // Throttle: don't schedule too far ahead of what's been rendered.
                while (_isPlaying && (_scheduledFrames - GetPlayedFrames()) > MaxPendingFrames)
                {
                    Thread.Sleep(20);
                }

                int samplesRead = _sampleProvider.Read(interleaved, 0, interleaved.Length);
                if (samplesRead <= 0)
                {
                    eof = true;
                    break;
                }
                int frames = samplesRead / Channels;
                if (samplesRead % Channels != 0) frames++;

                var pcmBuffer = new AVAudioPcmBuffer(_format, (uint)frames);
                pcmBuffer.FrameLength = (uint)frames;

                // Copy interleaved float data into deinterleaved channel buffers.
                // FloatChannelData is a float** (pointer to per-channel float arrays).
                var channelsPtr = (IntPtr)pcmBuffer.FloatChannelData;
                if (channelsPtr != IntPtr.Zero)
                {
                    unsafe
                    {
                        float* ch0 = (float*)Marshal.ReadIntPtr(channelsPtr, 0);
                        float* ch1 = (float*)Marshal.ReadIntPtr(channelsPtr, IntPtr.Size);
                        for (int i = 0; i < frames; i++)
                        {
                            int src = i * Channels;
                            float l = src < samplesRead ? interleaved[src] : 0f;
                            float r = (src + 1) < samplesRead ? interleaved[src + 1] : 0f;
                            ch0[i] = l;
                            ch1[i] = r;
                        }
                    }
                }

                _scheduledFrames += frames;
                _playerNode.ScheduleBuffer(pcmBuffer, null);
            }

            if (eof)
            {
                // Let the tail play out, then stop.
                var tail = new AVAudioPcmBuffer(_format, 0);
                tail.FrameLength = 0;
                _playerNode.ScheduleBuffer(tail, () =>
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
