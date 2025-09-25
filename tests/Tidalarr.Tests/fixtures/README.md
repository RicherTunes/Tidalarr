# Test Audio Fixtures

sample_tone.m4a is a one-second AAC tone generated from a synthesized sine wave. Generate it locally with FFmpeg 7.0.2 after writing a temporary stereo WAV at 44.1 kHz, 16-bit:
ffmpeg -y -i tone.wav -c:a aac -b:a 96k sample_tone.m4a

Re-running the command overwrites the asset with an equivalent, license-free sample.
