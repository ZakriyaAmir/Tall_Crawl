import numpy as np
import scipy.io.wavfile as w
import os
import subprocess

SR = 44100
SFX_DIR = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'TC', 'Resources', 'Sfx')
SOUNDS_DIR = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'resources', 'sounds')

def create_sweet_bee_sound():
    # 5.0 seconds seamless loopable gentle garden bumblebee drone
    # Pure acoustic soothing warm sine hums with soft airy fluttering wings
    duration = 5.0
    t = np.linspace(0, duration, int(SR * duration), endpoint=False)
    
    # Fundamental frequencies: peaceful warm major third / fifth drone
    # Low C3 (130.81 Hz), G3 (196.00 Hz), E3 (164.81 Hz) - warm, organic cello/reed hum
    f1, f2, f3 = 130.81, 196.00, 164.81
    
    # Delicate natural drift
    drift1 = 0.006 * np.sin(2 * np.pi * 0.8 * t)
    drift2 = 0.005 * np.sin(2 * np.pi * 1.1 * t + 1.0)
    drift3 = 0.004 * np.sin(2 * np.pi * 0.6 * t + 2.0)
    
    phase1 = 2 * np.pi * f1 * (t + drift1)
    phase2 = 2 * np.pi * f2 * (t + drift2)
    phase3 = 2 * np.pi * f3 * (t + drift3)
    
    # Pure, mellow waveforms (90% fundamental sine, 10% 2nd harmonic, 0% harsh odd harmonics)
    tone1 = np.sin(phase1) + 0.12 * np.sin(2 * phase1)
    tone2 = np.sin(phase2) + 0.08 * np.sin(2 * phase2)
    tone3 = np.sin(phase3) + 0.06 * np.sin(2 * phase3)
    
    # Very gentle, soft wing flutter (airy 14 Hz amplitude modulation, depth 12%)
    wing_flutter = 0.88 + 0.12 * np.sin(2 * np.pi * 14.0 * t)
    
    # Soft filtered airy breath/shimmer (like gentle wind through leaves)
    breeze = 0.04 * np.sin(2 * np.pi * 392.0 * t) * (0.8 + 0.2 * np.sin(2 * np.pi * 1.5 * t))
    
    bee_mix = (tone1 * 0.50 + tone2 * 0.30 + tone3 * 0.20) * wing_flutter + breeze
    
    # Seamless loop envelope: 0.15s smooth crossfade at boundaries
    fade_len = int(SR * 0.15)
    fade_in = np.linspace(0, 1, fade_len)
    fade_out = np.linspace(1, 0, fade_len)
    bee_mix[:fade_len] *= fade_in
    bee_mix[-fade_len:] *= fade_out
    
    # Keep it at a gentle, non-fatiguing volume
    bee_mix = bee_mix / np.max(np.abs(bee_mix)) * 0.45
    return bee_mix

def create_sweet_wasp_sound():
    # 0.65 seconds soothing, charming chime / music-box notification for wasp
    # Soft wooden marimba / music-box chime: E5 -> G#5 -> B5 (peaceful uplifting E major)
    duration = 0.65
    t = np.linspace(0, duration, int(SR * duration), endpoint=False)
    
    # Notes: E5 (659.25 Hz), G#5 (830.61 Hz), B5 (987.77 Hz), E6 (1318.51 Hz)
    notes = [659.25, 830.61, 987.77, 1318.51]
    chime_layer = np.zeros_like(t)
    
    for i, freq in enumerate(notes):
        start_t = i * 0.085
        idx_start = int(start_t * SR)
        dur_note = duration - start_t
        t_note = np.linspace(0, dur_note, len(t) - idx_start, endpoint=False)
        
        # Soft music-box hammer: instant attack, natural bell decay
        attack = np.minimum(1.0, t_note / 0.003)
        decay = np.exp(-t_note * 9.0)
        
        # Pure round bell tone with gentle overtone
        sig = (np.sin(2 * np.pi * freq * t_note) +
               0.22 * np.sin(2 * np.pi * 2 * freq * t_note) * np.exp(-t_note * 18.0) +
               0.05 * np.sin(2 * np.pi * 3 * freq * t_note) * np.exp(-t_note * 30.0))
        
        chime_layer[idx_start:] += sig * attack * decay * 0.32
        
    # Gentle soft woosh underneath (like a cute sprite gliding in)
    woosh_env = np.exp(-t * 6.5) * np.sin(np.pi * np.clip(t / 0.25, 0, 1))
    woosh = 0.08 * np.sin(2 * np.pi * 260.0 * t) * woosh_env
    
    wasp_mix = chime_layer + woosh
    end_fade = np.minimum(1.0, (duration - t) / 0.05)
    wasp_mix *= end_fade
    
    wasp_mix = wasp_mix / np.max(np.abs(wasp_mix)) * 0.48
    return wasp_mix

def main():
    os.makedirs(SFX_DIR, exist_ok=True)
    os.makedirs(SOUNDS_DIR, exist_ok=True)
    
    # 1. Generate soothing wasp entrance sound (wasp.wav in Sfx)
    wasp_audio = create_sweet_wasp_sound()
    wasp_wav_path = os.path.join(SFX_DIR, 'wasp.wav')
    w.write(wasp_wav_path, SR, (wasp_audio * 32767).astype(np.int16))
    print(f"Generated soothing wasp sound -> {wasp_wav_path}")
    
    # 2. Generate soothing bees swarm audio (for bees.wav in Sfx)
    bee_audio = create_sweet_bee_sound()
    bee_wav_path = os.path.join(SFX_DIR, 'bees.wav')
    w.write(bee_wav_path, SR, (bee_audio * 32767).astype(np.int16))
    print(f"Generated soothing bees.wav -> {bee_wav_path}")
    
    # 3. Generate soothing beesPass.mp3 for the beesParent prefab audio source
    temp_bee_wav = '/tmp/soothing_bees.wav'
    w.write(temp_bee_wav, SR, (bee_audio * 32767).astype(np.int16))
    
    bees_pass_mp3 = os.path.join(SOUNDS_DIR, 'beesPass.mp3')
    cmd = ['ffmpeg', '-y', '-i', temp_bee_wav, '-b:a', '192k', bees_pass_mp3]
    subprocess.run(cmd, check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    print(f"Generated soothing beesPass.mp3 -> {bees_pass_mp3}")
    
    # Also update bee.wav in Assets/resources/sounds/
    bee_res_wav = os.path.join(SOUNDS_DIR, 'bee.wav')
    w.write(bee_res_wav, SR, (bee_audio * 32767).astype(np.int16))
    print(f"Updated bee.wav -> {bee_res_wav}")
    
    if os.path.exists(temp_bee_wav):
        os.remove(temp_bee_wav)

if __name__ == '__main__':
    main()
