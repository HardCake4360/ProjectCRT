"""Original temporary SC01 sound design: no downloaded or copyrighted music recordings."""
from pathlib import Path
import math, random, wave, struct

ROOT=Path(__file__).resolve().parents[1]
FOLDER=ROOT/'Assets/Settings/Prologue/SC01/Audio'
RATE=22050
FOLDER.mkdir(parents=True,exist_ok=True)

def save(name,seconds,fn):
    rng=random.Random(1841)
    data=bytearray()
    for i in range(int(seconds*RATE)):
        t=i/RATE
        value=max(-1,min(1,fn(t,rng)))
        data.extend(struct.pack('<h',int(value*32760)))
    with wave.open(str(FOLDER/(name+'.wav')),'wb') as output:
        output.setnchannels(1);output.setsampwidth(2);output.setframerate(RATE);output.writeframes(data)

save('SanBlip',.068,lambda t,r: math.sin(math.pi*min(1,t/.068))*(math.sin(2*math.pi*163*t)+.24*math.sin(2*math.pi*326*t))*.26)
save('DriverBlip',.075,lambda t,r: math.sin(math.pi*min(1,t/.075))*(math.sin(2*math.pi*112*t)+.4*math.sin(2*math.pi*224*t)+.18*math.sin(2*math.pi*448*t))*.24)
save('Rain',8,lambda t,r: (r.random()*2-1)*(.30+.05*math.sin(2*math.pi*.375*t))+.04*math.sin(2*math.pi*84*t))
save('Engine',8,lambda t,r: .22*math.sin(2*math.pi*48*t)+.12*math.sin(2*math.pi*96*t)+.08*math.sin(2*math.pi*144*t)+(r.random()*2-1)*.045)
save('Wipers',1.8,lambda t,r: (r.random()*2-1)*(.045+.22*math.exp(-((t-.45)/.20)**2)+.22*math.exp(-((t-1.3)/.20)**2)))
save('CD',.4,lambda t,r: (r.random()*2-1)*math.exp(-t*18)*.35+.05*math.sin(2*math.pi*900*t)*math.exp(-((t-.15)/.05)**2))
save('DoorOpen',.65,lambda t,r: (r.random()*2-1)*(.36*math.exp(-t*30)+.06*math.exp(-((t-.3)/.17)**2)))
save('DoorClose',.55,lambda t,r: ((r.random()*2-1)*.35+math.sin(2*math.pi*76*t)*.45)*math.exp(-t*12))

# An original eight-bar electric-piano/bass sketch, 80 BPM, with soft brushed noise.
chords=[[48,55,58,62,67],[53,57,63,67,74],[46,53,57,60,65],[51,58,62,65,70]]
def note(midi): return 440*2**((midi-69)/12)
def jazz(t,r):
    bar=int(t/3)%8;within=t%3;chord=chords[(bar//2)%4];value=0
    for start in [0,1.125,2.25]:
        d=within-start
        if d>=0:
            env=(1-math.exp(-d*60))*math.exp(-d*2.5)
            value+=sum((math.sin(2*math.pi*note(n)*d)+.2*math.sin(2*math.pi*note(n)*2*d))*env*.028 for n in chord)
    beat=int(within/.75);d=within% .75;freq=note(chord[0]-12+[0,7,12,7][beat])
    value+=(math.sin(2*math.pi*freq*d)+.15*math.sin(2*math.pi*freq*2*d))*.11*math.exp(-d*4)*(1-math.exp(-d*80))
    value+=(r.random()*2-1)*.018*math.exp(-(within%.375)*18)
    # Keep loop boundary quiet enough to avoid clicks.
    return value*min(1,t/.03,(24-t)/.03)
save('Jazz',24,jazz)
(FOLDER/'README.md').write_text('SC01 original placeholder audio\n\nCreated procedurally for ProjectCRT. Rain, engine, wipers, CD/door effects, two character blips and an original short jazz-style piano/bass sketch. No third-party recording or musical composition was sampled. Not recorded dialogue or Korean TTS. Replace as final sound design progresses.\n',encoding='utf-8')
print('Created nine original WAV files including character blips, effects and one music loop in',FOLDER)
