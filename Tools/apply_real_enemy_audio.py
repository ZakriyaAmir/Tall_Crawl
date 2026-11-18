import numpy as np
import scipy.io.wavfile as w
from scipy.signal import butter, sosfilt
import os
import subprocess

SFX_DIR = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'TC', 'Resources', 'Sfx')
SOUNDS_DIR = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'resources', 'sounds')

# 1. REAL WASP SOUND
# Extract authentic wasp recording from git history
out_wasp = subprocess.check_output(['git', 'show', '9755b59:Assets/TC/Resources/Sfx/wasp.wav'])
temp_orig_wasp = '/tmp/orig_wasp_raw.wav'
with open(temp_orig_wasp, 'wb') as f:
    f.write(out_wasp)

sr_w, d_w = w.read(temp_orig_wasp)
d_w_float = d_w.astype(float)

# Warm lowpass filter at 1500 Hz to remove harsh grating distortion while retaining 100% authentic wasp buzz
sos_w = butter(3, 1500, 'lowpass', fs=sr_w, output='sos')
clean_wasp = sosfilt(sos_w, d_w_float)

# Highpass filter at 100 Hz to remove sub-bass muddiness
sos_w_hp = butter(2, 100, 'highpass', fs=sr_w, output='sos')
clean_wasp = sosfilt(sos_w_hp, clean_wasp)

# Smooth natural envelope so entrance doesn't have an abrasive click/pop
t_w = np.arange(len(clean_wasp)) / sr_w
dur_w = len(clean_wasp) / sr_w
att = np.minimum(1.0, t_w / 0.035)
rel = np.minimum(1.0, (dur_w - t_w) / 0.10)
clean_wasp = clean_wasp * att * rel

# Comfortable peak level (~0.50) so it sounds natural without blowing out speakers
clean_wasp = clean_wasp / np.max(np.abs(clean_wasp)) * 0.50
wasp_out_path = os.path.join(SFX_DIR, 'wasp.wav')
w.write(wasp_out_path, sr_w, (clean_wasp * 32767).astype(np.int16))
print(f"Restored & polished authentic wasp sound -> {wasp_out_path}")


# 2. REAL BEES SOUND
# Extract authentic bee swarm audio from git history
out_bees = subprocess.check_output(['git', 'show', 'e3d18ca:Assets/resources/sounds/beesPass.mp3'])
temp_orig_mp3 = '/tmp/orig_bees_raw.mp3'
with open(temp_orig_mp3, 'wb') as f:
    f.write(out_bees)

temp_orig_wav = '/tmp/orig_bees_raw.wav'
subprocess.run(['ffmpeg', '-y', '-i', temp_orig_mp3, temp_orig_wav], stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True)

sr_b, d_b = w.read(temp_orig_wav)
# Convert stereo to mono for 3D positional audio
if d_b.ndim > 1:
    d_b_mono = np.mean(d_b.astype(float), axis=1)
else:
    d_b_mono = d_b.astype(float)

# Warm lowpass filter at 1600 Hz: keeps the natural wing frequency (~185-400 Hz) and body, taming harsh high frequencies
sos_b = butter(3, 1600, 'lowpass', fs=sr_b, output='sos')
clean_bees = sosfilt(sos_b, d_b_mono)

# Highpass at 80 Hz
sos_b_hp = butter(2, 80, 'highpass', fs=sr_b, output='sos')
clean_bees = sosfilt(sos_b_hp, clean_bees)

# Smooth 0.20s seamless loop crossfade at ends
fade_len = int(sr_b * 0.20)
fade_in = np.linspace(0, 1, fade_len)
fade_out = np.linspace(1, 0, fade_len)
clean_bees[:fade_len] *= fade_in
clean_bees[-fade_len:] *= fade_out

clean_bees = clean_bees / np.max(np.abs(clean_bees)) * 0.48

# Write to beesPass.mp3 (used by beesParent prefab AudioSource)
temp_clean_wav = '/tmp/clean_bees_temp.wav'
w.write(temp_clean_wav, sr_b, (clean_bees * 32767).astype(np.int16))

bees_mp3_path = os.path.join(SOUNDS_DIR, 'beesPass.mp3')
subprocess.run(['ffmpeg', '-y', '-i', temp_clean_wav, '-b:a', '192k', bees_mp3_path], stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True)
print(f"Restored & polished authentic beesPass.mp3 -> {bees_mp3_path}")

# Write to Sfx/bees.wav (resampled to 44.1kHz if needed)
bees_wav_path = os.path.join(SFX_DIR, 'bees.wav')
subprocess.run(['ffmpeg', '-y', '-i', temp_clean_wav, '-ar', '44100', bees_wav_path], stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True)
print(f"Restored & polished authentic bees.wav -> {bees_wav_path}")

# Write to sounds/bee.wav
bee_sound_wav = os.path.join(SOUNDS_DIR, 'bee.wav')
subprocess.run(['ffmpeg', '-y', '-i', temp_clean_wav, '-ar', '44100', bee_sound_wav], stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True)
print(f"Restored & polished authentic bee.wav -> {bee_sound_wav}")

# Cleanup temp files
for p in [temp_orig_wasp, temp_orig_mp3, temp_orig_wav, temp_clean_wav]:
    if os.path.exists(p):
        os.remove(p)

print("Done! Real authentic wasp & bees sounds successfully applied.")
