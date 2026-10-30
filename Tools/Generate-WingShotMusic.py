"""Create Skybound, an original looping Wing Shot tune using only Python's standard library.
The composition, synthesized recording, and this generator are dedicated under CC0-1.0.
No external recordings, instrument samples, or downloaded melodies are used.
"""
from array import array
import json
import math
from pathlib import Path
import random
import sys
import wave

ROOT = Path(__file__).resolve().parent.parent
OUTPUT = ROOT / "Assets/Resources/Audio/Skybound.wav"
RATE = 44100
BPM = 104
BEAT = 60.0 / BPM
BARS = 16
FRAMES = round(BARS * 4 * BEAT * RATE)
left = array("f", [0.0]) * FRAMES
right = array("f", [0.0]) * FRAMES
random_source = random.Random(731940)
TAU = math.tau


# Convert a piano-style MIDI note number to its pitch in hertz.
def frequency(note):
    return 440.0 * 2.0 ** ((note - 69) / 12.0)


# Add one handmade instrument sound to the song, with gentle stereo positioning.
def note(start, pitch, beats, volume, pan=0.0, instrument="pluck"):
    duration = beats * BEAT
    count = round(duration * RATE)
    first = round(start * BEAT * RATE)
    pitch_hz = frequency(pitch)
    gain_l = math.cos((pan + 1.0) * math.pi / 4.0) * volume
    gain_r = math.sin((pan + 1.0) * math.pi / 4.0) * volume
    for i in range(count):
        time = i / RATE
        phase = TAU * pitch_hz * time
        # Fade the beginning and end of every note to avoid little clicks.
        edge = min(1.0, time / .007) * min(1.0, (duration - time) / .025)
        if instrument == "bass":
            sound = (math.sin(phase) + .12 * math.sin(2 * phase)) * math.exp(-time * 3.5)
        elif instrument == "bell":
            sound = (math.sin(phase) + .24 * math.sin(2 * phase) * math.exp(-time * 8)
                     + .10 * math.sin(3 * phase) * math.exp(-time * 12)) * math.exp(-time * 3.8)
        else:
            sound = (math.sin(phase) + .24 * math.sin(2 * phase)
                     + .08 * math.sin(3 * phase)) * math.exp(-time * 7.0)
        # Wrap note tails into the beginning so the repeating file has a smooth join.
        index = (first + i) % FRAMES
        left[index] += sound * edge * gain_l
        right[index] += sound * edge * gain_r


# Make quiet percussion from generated noise and sine waves instead of drum samples.
def percussion(start, kind, volume, pan=0.0):
    duration = .13 if kind == "kick" else .055
    first = round(start * BEAT * RATE)
    phase = 0.0
    previous_noise = 0.0
    for i in range(round(duration * RATE)):
        time = i / RATE
        if kind == "kick":
            phase += TAU * (48 + 95 * math.exp(-time * 45)) / RATE
            sound = math.sin(phase) * math.exp(-time * 32)
        else:
            noise = random_source.uniform(-1, 1)
            sound = (noise - previous_noise) * math.exp(-time * 90)
            previous_noise = noise
            if kind == "tap":
                sound = sound * .35 + math.sin(TAU * 1350 * time) * math.exp(-time * 100) * .6
        sound *= min(1.0, time / .002)
        index = (first + i) % FRAMES
        left[index] += sound * volume * (1 - pan) * .5
        right[index] += sound * volume * (1 + pan) * .5


# A bright C-major chord sequence supports the game's sunny meadow setting.
chords = [
    (60, 64, 67, 69), (59, 62, 67, 71), (57, 60, 64, 67), (53, 57, 60, 64),
    (62, 65, 69, 72), (55, 59, 62, 64), (53, 57, 60, 64), (55, 60, 62, 67),
]
bass_roots = [36, 35, 33, 29, 38, 31, 29, 31]
arpeggio = [0, 2, 1, 3, 2, 1, 3, 2]

# The second half answers the first with a slightly different original melody.
melodies = [
    [(0,76,.7), (1,79,.45), (1.75,81,.7), (3,79,.8)],
    [(.25,74,.65), (1.25,76,.5), (2,79,.8), (3.25,74,.65)],
    [(0,72,.85), (1.5,76,.6), (2.5,79,.6), (3.25,76,.6)],
    [(.5,77,.65), (1.5,76,.6), (2.5,72,1.0)],
    [(0,74,.7), (.75,77,.45), (1.5,81,.8), (3,77,.7)],
    [(.25,79,.75), (1.5,76,.6), (2.5,74,.95)],
    [(0,72,.7), (1,77,.65), (2,76,.55), (3,72,.8)],
    [(.5,74,.65), (1.5,79,.55), (2.5,74,.65), (3.25,71,.8)],
    [(0,79,.75), (1,76,.5), (2,81,.65), (3,79,.8)],
    [(.25,83,.7), (1.25,79,.5), (2,76,.7), (3,74,.8)],
    [(0,76,.85), (1.5,72,.6), (2.5,79,.55), (3.25,76,.65)],
    [(.5,77,.65), (1.5,81,.6), (2.5,79,1.0)],
    [(0,81,.65), (.75,77,.5), (1.5,74,.8), (3,77,.75)],
    [(.25,79,.75), (1.5,74,.6), (2.5,76,1.0)],
    [(0,77,.7), (1,76,.5), (2,72,.7), (3,69,.8)],
    [(0,74,.7), (1,79,.55), (2,74,.6), (3,71,1.2)],
]

# Arrange the melody, plucked chords, bass, and a light backbeat across sixteen bars.
for bar in range(BARS):
    start = bar * 4
    chord = chords[bar % 8]
    root = bass_roots[bar % 8]
    for step, chord_index in enumerate(arpeggio):
        note(start + step * .5, chord[chord_index], .48, .047, -.4 if step % 2 == 0 else .4)
    note(start, root, 1.6, .12, -.08, "bass")
    note(start + 2, root + 7, 1.45, .088, .08, "bass")
    for onset, pitch, length in melodies[bar]:
        note(start + onset, pitch, length + .16, .12, .1, "bell")
    for step in range(8):
        percussion(start + step * .5, "shaker", .009 if step % 2 else .006, .3)
    for beat in (0, 2):
        percussion(start + beat, "kick", .075)
    for beat in (1, 3):
        percussion(start + beat, "tap", .033, -.2)

# Leave headroom so the music and game sound effects can play together cleanly.
peak = max(max(abs(value) for value in left), max(abs(value) for value in right))
gain = .82 / peak
pcm = array("h")
energy = 0.0
for l_value, r_value in zip(left, right):
    l_value *= gain
    r_value *= gain
    energy += l_value * l_value + r_value * r_value
    pcm.extend((round(l_value * 32767), round(r_value * 32767)))
if sys.byteorder != "little":
    pcm.byteswap()
OUTPUT.parent.mkdir(parents=True, exist_ok=True)
with wave.open(str(OUTPUT), "wb") as wav:
    wav.setnchannels(2)
    wav.setsampwidth(2)
    wav.setframerate(RATE)
    wav.writeframes(pcm.tobytes())

# Save measurable checks for silence, clipping, format, and the loop boundary.
report = {
    "title": "Skybound", "license": "CC0-1.0", "bpm": BPM, "bars": BARS,
    "seconds": FRAMES / RATE, "sampleRate": RATE, "channels": 2,
    "peak": .82, "rms": math.sqrt(energy / (FRAMES * 2)),
    "loopSeamDelta": max(abs(left[0] - left[-1]), abs(right[0] - right[-1])) * gain,
    "thirdPartySamples": False,
}
assert .01 < report["rms"] < .5
assert report["peak"] < 1.0 and report["loopSeamDelta"] < .01
report_path = ROOT / "Verification/WingShotMusic.json"
report_path.parent.mkdir(parents=True, exist_ok=True)
report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print(json.dumps(report, indent=2))
print("Wrote", OUTPUT)
