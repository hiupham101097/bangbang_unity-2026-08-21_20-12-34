"""Build a punchier, faster in-game mix from the current western theme."""

from array import array
import math
from pathlib import Path
import random
import wave


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets/GameAssets/Audio/western_theme.wav"
TARGETS = (
    ROOT / "Assets/GameAssets/Audio/western_theme.wav",
    ROOT / "Assets/GameAssets/Resources/western_theme.wav",
)


def read_mono_16(path: Path) -> tuple[int, array]:
    with wave.open(str(path), "rb") as wav:
        if wav.getnchannels() != 1 or wav.getsampwidth() != 2:
            raise ValueError("Expected a mono, 16-bit PCM WAV")
        rate = wav.getframerate()
        samples = array("h")
        samples.frombytes(wav.readframes(wav.getnframes()))
    return rate, samples


def write_mono_16(path: Path, rate: int, samples: array) -> None:
    with wave.open(str(path), "wb") as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(rate)
        wav.writeframes(samples.tobytes())


def main() -> None:
    rate, source = read_mono_16(SOURCE)
    speed = 1.12
    frame_count = int(len(source) / speed)
    mix = [0.0] * frame_count

    # Resample for more forward momentum while retaining the original melody.
    for i in range(frame_count):
        position = i * speed
        left = int(position)
        fraction = position - left
        right = min(left + 1, len(source) - 1)
        mix[i] = (source[left] * (1.0 - fraction) + source[right] * fraction) * 0.80

    bpm = 132.0
    beat = 60.0 / bpm
    rng = random.Random(1880)

    def add_kick(at: float, strength: float = 1.0) -> None:
        start = int(at * rate)
        length = int(0.20 * rate)
        phase = 0.0
        for n in range(length):
            idx = start + n
            if idx >= frame_count:
                break
            t = n / rate
            frequency = 115.0 * math.exp(-15.0 * t) + 42.0
            phase += 2.0 * math.pi * frequency / rate
            body = math.sin(phase) * math.exp(-19.0 * t)
            click = (1.0 if n < 10 else 0.0) * math.exp(-130.0 * t)
            mix[idx] += strength * (body * 10800.0 + click * 3600.0)

    def add_snare(at: float, strength: float = 1.0) -> None:
        start = int(at * rate)
        length = int(0.14 * rate)
        previous = 0.0
        for n in range(length):
            idx = start + n
            if idx >= frame_count:
                break
            t = n / rate
            noise = rng.uniform(-1.0, 1.0)
            bright = noise - previous * 0.72
            previous = noise
            tone = math.sin(2.0 * math.pi * 185.0 * t)
            mix[idx] += strength * (bright * 3900.0 + tone * 1600.0) * math.exp(-25.0 * t)

    def add_shaker(at: float, strength: float = 1.0) -> None:
        start = int(at * rate)
        length = int(0.045 * rate)
        previous = 0.0
        for n in range(length):
            idx = start + n
            if idx >= frame_count:
                break
            t = n / rate
            noise = rng.uniform(-1.0, 1.0)
            bright = noise - previous
            previous = noise
            mix[idx] += strength * bright * 1900.0 * math.exp(-65.0 * t)

    total_beats = int((frame_count / rate) / beat) + 1
    for b in range(total_beats):
        at = b * beat
        bar_beat = b % 4
        add_kick(at, 1.08 if bar_beat == 0 else 0.84)
        if bar_beat in (1, 3):
            add_snare(at, 0.92 if bar_beat == 1 else 1.08)
        add_shaker(at, 0.65 if b % 2 == 0 else 0.48)
        add_shaker(at + beat / 2.0, 0.76)

    # Add a bus-style low-end reinforcement based on a smoothed source signal.
    low = 0.0
    for i, value in enumerate(mix):
        low += 0.035 * (value - low)
        mix[i] = value + low * 0.16

    # Short fade-in/out avoids clicks, then soft clip and normalize the master.
    fade = int(0.035 * rate)
    peak = 1.0
    processed = [0.0] * frame_count
    for i, value in enumerate(mix):
        gain = min(1.0, i / fade, (frame_count - 1 - i) / fade)
        saturated = math.tanh((value / 19000.0) * 1.35) * gain
        processed[i] = saturated
        peak = max(peak, abs(saturated))

    master = 0.94 * 32767.0 / peak
    output = array("h", (int(max(-32768, min(32767, value * master))) for value in processed))
    for target in TARGETS:
        write_mono_16(target, rate, output)

    print(f"Rendered {frame_count / rate:.2f}s at {rate} Hz to {len(TARGETS)} targets")


if __name__ == "__main__":
    main()
