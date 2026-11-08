#!/usr/bin/env python3
"""Source-derived numerical smoke of the free-running AI speed/brake envelope.
Not an execution of Unity: no collisions, lane changes, traffic or random mistakes.
"""
import contextlib,io,json,math,re,runpy
from pathlib import Path
import numpy as np
root=Path(__file__).resolve().parent
with contextlib.redirect_stdout(io.StringIO()):
 track=runpy.run_path(str(root/'track_validation.py'))
length=track['L']; curvature=track['curvature']; n=len(curvature)
car_source=(root.parent/'Assets/Scripts/StellarCarFactory.cs').read_text()
ai_source=(root.parent/'Assets/Scripts/StellarAICar.cs').read_text()
preview_steps=int(re.search(r'i <= (\d+)',ai_source)[1])+1
preview_spacing=float(re.search(r'distance = i \* ([\d.]+)f',ai_source)[1])
safety_match=re.search(r'cornerSpeed = Mathf.Sqrt\(lateralAcceleration / Mathf.Max\(0.0001f, curvature\)\) \* ([\d.]+)f',ai_source)
safety=float(safety_match[1]) if safety_match else 1.0
game_source=(root.parent/'Assets/Scripts/HyperionGame.cs').read_text()
cars=[tuple(map(float,row)) for row in re.findall(r'\n\s+(\d{3})f, ([\d.]+)f, ([\d.]+)f, ([\d.]+)f, [0-3]\)',car_source)]
difficulties=[(name,float(lo),float(hi)) for name,lo,hi in re.findall(r'new DifficultySpec\("([A-Z]+)", "[^"]+", ([\d.]+)f, ([\d.]+)f',game_source)]
assert len(cars)==4 and len(difficulties)==3
rows=[]
for level,low,high in difficulties:
 for car,(top,accel,handling,nitro) in enumerate(cars):
  pace=(low+high)/2
  lateral=(27+handling*1.1)*min(1.08,max(.8,pace))
  position=-28/length; speed=0; elapsed=0; top_seen=0; max_load=0
  dt=.02
  while position<3 and elapsed<300:
   desired=top/3.6*pace
   for i in range(preview_steps):
    distance=i*preview_spacing
    s=((position+distance/length)%1)*n
    a=int(s)%n
    curve=abs(curvature[a]*(1-(s-math.floor(s)))+curvature[(a+1)%n]*(s-math.floor(s)))
    corner=math.sqrt(lateral/max(.0001,curve))*safety
    entry=math.sqrt(corner*corner+2*38*distance)
    desired=min(desired,entry)
   step=(48 if desired<speed else 12+accel*1.4)*dt
   speed+=max(-step,min(step,desired-speed))
   position+=speed*dt/length
   elapsed+=dt; top_seen=max(top_seen,speed)
   s=(position%1)*n;a=int(s)%n
   curve=abs(curvature[a]*(1-(s-math.floor(s)))+curvature[(a+1)%n]*(s-math.floor(s)))
   max_load=max(max_load,speed*speed*curve/lateral)
  assert position>=3 and elapsed<180
  assert top_seen<=top/3.6*pace+.001
  assert math.isfinite(elapsed)
  assert max_load <= 1.15, (level,car,max_load)
  rows.append(dict(difficulty=level,car_index=car,finish_3laps_seconds=round(elapsed,2),max_speed_kph=round(top_seen*3.6,1),max_corner_load_vs_configured=round(max_load,3)))
result=dict(description='Numerical free-running speed/brake smoke; not Unity gameplay execution. Traffic, lane changes, rubber banding and random mistakes excluded.',cases=len(rows),passed=True,results=rows)
(root/'ai_metrics.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps(result,indent=2))
