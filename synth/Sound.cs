using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Input;

namespace synth
{
    public partial class Game1 : Game
    {
        private static void ConvertBuffer(float[,] from, byte[] to) {
            const int bytesPerSample = 2;
            int channels = from.GetLength(0);
            int samplesPerBuffer = from.GetLength(1);

            // Make sure the buffer sizes are correct
            System.Diagnostics.Debug.Assert(to.Length == samplesPerBuffer * channels * bytesPerSample, "Buffer sizes are mismatched.");

            for (int i = 0; i < samplesPerBuffer; i++) {
                for (int c = 0; c < channels; c++) {
                    // First clamp the value to the [-1.0..1.0] range
                    float floatSample = MathHelper.Clamp(from[c, i], -1.0f, 1.0f);

                    // Convert it to the 16 bit [short.MinValue..short.MaxValue] range
                    short shortSample = (short)(floatSample >= 0.0f ? floatSample * short.MaxValue : floatSample * short.MinValue * -1);

                    // Calculate the right index based on the PCM format of interleaved samples per channel [L-R-L-R]
                    int index = i * channels * bytesPerSample + c * bytesPerSample;

                    // Store the 16 bit sample as two consecutive 8 bit values in the buffer with regard to endian-ness
                    if (!BitConverter.IsLittleEndian) {
                        to[index] = (byte)(shortSample >> 8);
                        to[index + 1] = (byte)shortSample;
                    } else {
                        to[index] = (byte)shortSample;
                        to[index + 1] = (byte)(shortSample >> 8);
                    }
                }
            }
        }

        public static class Oscillator
        {
            public static float Sine(float frequency, float time) {
                return (float)Math.Sin(frequency * time * 2 * Math.PI);
            }

            public static float Square(float frequency, float time) {
                return Sine(frequency, time) >= 0 ? 1.0f : -1.0f;
            }

            public static float Sawtooth(float frequency, float time) {
                return (float)(2 * (time * frequency - Math.Floor(time * frequency + 0.5)));
            }

            public static float Triangle(float frequency, float time) {
                return Math.Abs(Sawtooth(frequency, time)) * 2.0f - 1.0f;
            }

            public static float Line(float frequency, float time) {
                return frequency / _height;
            }
        }

        public delegate float OscillatorDelegate(float frequency, float time);

        public class Voice
        {
            private enum VoiceState { Attack, Decay, Sustain, Release }
            private VoiceState _state;

            public Voice(Synth synth) {
                _synth = synth;
            }

            public bool IsAlive { get; private set; }

            public void Start(float frequency) {
                _frequency = frequency;
                _time = 0.0f;
                _fadeMultiplier = 0.0f;

                _fadeCounter = 0;

                if (_synth.FadeInDuration == 0) {
                    _state = VoiceState.Sustain;
                } else {
                    _state = VoiceState.Attack;
                }

                IsAlive = true;
            }

            public void Stop() {
                if (_synth.FadeOutDuration == 0) {
                    IsAlive = false;
                } else {
                    _fadeCounter = (int)((1.0f - _fadeMultiplier) * _synth.FadeOutDuration);
                    _state = VoiceState.Release;
                }
            }

            public void Process(float[,] buffer) {
                if (!IsAlive) return;

                int samplesPerBuffer = buffer.GetLength(1);
                for (int i = 0; i < samplesPerBuffer; i++) {
                    switch (_state) {
                        case VoiceState.Attack:

                            _fadeMultiplier = (float)_fadeCounter / _synth.FadeInDuration;

                            ++_fadeCounter;
                            if (_fadeCounter >= _synth.FadeInDuration) {
                                _state = VoiceState.Sustain;
                            }

                            break;
                        case VoiceState.Sustain:

                            _fadeMultiplier = 1.0f;

                            break;
                        case VoiceState.Release:

                            _fadeMultiplier = 1.0f - (float)_fadeCounter / _synth.FadeOutDuration;

                            ++_fadeCounter;
                            if (_fadeCounter >= _synth.FadeOutDuration) {
                                IsAlive = false;
                                return;
                            }

                            break;
                    }

                    float sample = _synth.Oscillator(_frequency, _time);
                    buffer[0, i] += sample * 0.2f * _fadeMultiplier;
                    _time += 1.0f / Synth.SampleRate;
                }
            }

            private float _frequency;
            private float _time;
            private readonly Synth _synth;
            private float _fadeMultiplier;
            private int _fadeCounter;
            //private float _amp = 1f;
        }

        public class Synth
        {
            private readonly Voice[] _voicePool;
            private readonly List<Voice> _activeVoices;
            private readonly Stack<Voice> _freeVoices;
            private readonly Dictionary<int, Voice> _noteRegistry;

            private DynamicSoundEffectInstance _instance;

            private float[,] _workingBuffer;
            private byte[] _xnaBuffer;

            public const int Channels = 1;
            public const int SampleRate = 44100;
            public const int BufferSamples = 2000;
            public const int Polyphony = 32;
            public int FadeInDuration = 200;
            public int FadeOutDuration = 200;
            public OscillatorDelegate Oscillator;

            public void NoteOn(int frequency) {
                if (_noteRegistry.ContainsKey(frequency)) {
                    return;
                }

                // get free voice
                if (_freeVoices.Count == 0) {
                    return;
                }

                Voice freeVoice = _freeVoices.Pop();

                _noteRegistry[frequency] = freeVoice;

                freeVoice.Start(frequency);

                _activeVoices.Add(freeVoice);
            }

            public void NoteOff(int frequency) {
                Voice voice;
                if (_noteRegistry.TryGetValue(frequency, out voice)) {
                    voice.Stop();
                    _noteRegistry.Remove(frequency);
                }
            }

            public Synth(OscillatorDelegate oscillator) {
                Oscillator = oscillator;

                // Create DynamicSoundEffectInstance object and start it
                _instance = new DynamicSoundEffectInstance(SampleRate, Channels == 2 ? AudioChannels.Stereo : AudioChannels.Mono);
                _instance.Play();

                // Create buffers
                const int bytesPerSample = 2;
                _xnaBuffer = new byte[Channels * BufferSamples * bytesPerSample];
                _workingBuffer = new float[Channels, BufferSamples];

                // Create voice structures
                _voicePool = new Voice[Polyphony];
                for (int i = 0; i < Polyphony; ++i) {
                    _voicePool[i] = new Voice(this);
                }
                _freeVoices = new Stack<Voice>(_voicePool);
                _activeVoices = new List<Voice>();
                _noteRegistry = new Dictionary<int, Voice>();
            }

            private void ClearWorkingBuffer() {
                Array.Clear(_workingBuffer, 0, Channels * BufferSamples);
            }

            private void FillWorkingBuffer() {
                for (int i = _activeVoices.Count - 1; i >= 0; --i) {
                    Voice voice = _activeVoices[i];
                    voice.Process(_workingBuffer);

                    if (!voice.IsAlive) {
                        _activeVoices.RemoveAt(i);
                        _freeVoices.Push(voice);
                    }
                }
            }

            private void SubmitBuffer() {
                ClearWorkingBuffer();
                FillWorkingBuffer();
                ConvertBuffer(_workingBuffer, _xnaBuffer);
                _instance.SubmitBuffer(_xnaBuffer);
            }

            public void Update(GameTime gameTime, byte[,] board) {
                while (_instance.PendingBufferCount < 3) {
                    SubmitBuffer();
                }

                for (int i = 0; i < _width; i++) {
                    int v = (int)(_workingBuffer[0, i * BufferSamples / _width] * _height / 2) + _height / 2;

                    if (v >= 0 && v < _height)
                        board[v, i] = 1;
                }
            }
        }
    }
}
