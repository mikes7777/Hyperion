import json, re
from pathlib import Path
verification_dir = Path(__file__).resolve().parent
project_dir = verification_dir.parent
import numpy as np
source=(project_dir/'Assets/Scripts/StellarTrack.cs').read_text()
block=source.split('private static readonly Vector3[] ControlPoints =')[1].split('};')[0]
p=np.array([[float(x) for x in row] for row in re.findall(r'new Vector3\(([-\d.]+)f, ([-\d.]+)f, ([-\d.]+)f\)',block)])
t=np.linspace(0,1,8193)%1*len(p);i=t.astype(int);t=(t-i)[:,None]
a,b,c,d=[p[(i+j)%len(p)] for j in [-1,0,1,2]]
dense=.5*(2*b+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t)
dis=np.r_[0,np.linalg.norm(np.diff(dense,axis=0),axis=1).cumsum()];L=dis[-1];s=np.arange(768)*L/768
centers=np.array([np.interp(s,dis,dense[:,j]) for j in range(3)]).T
def norm(x):return x/np.linalg.norm(x,axis=-1,keepdims=True)
tangent=norm(np.roll(centers,-1,axis=0)-np.roll(centers,1,axis=0));v=np.roll(tangent,2,axis=0);w=np.roll(tangent,-2,axis=0)
angle=np.arccos(np.clip(np.sum(v*w,axis=1),-1,1))*np.sign(np.cross(v,w)[:,1]);curvature=angle/(4*L/768)
bank_curvature=sum(np.roll(curvature,k)*(9-abs(k)) for k in range(-8,9))/sum(9-abs(k) for k in range(-8,9))
right=norm(np.cross(np.array([0,1,0]),tangent));up=norm(np.cross(tangent,right));bank=-np.clip(bank_curvature*1550,-19,19)*np.pi/180
right=right*np.cos(bank[:,None])+np.cross(tangent,right)*np.sin(bank[:,None]);up=norm(np.cross(tangent,right))
normal_dots=[];rail_dots=[]
for j in range(768):
 n=(j+1)%768;a=centers[j]-right[j]*11;b=centers[n]-right[n]*11;c=centers[n]+right[n]*11
 normal_dots.append(np.dot(norm(np.cross(b-a,c-a)),up[j]))
 d=centers[j]+right[j]*11;normal_dots.append(np.dot(norm(np.cross(c-a,d-a)),up[j]))
 for side in [-1,1]:
  edge=centers[j]+right[j]*11.2*side;next_edge=centers[n]+right[n]*11.2*side
  if side<0:
   a=edge+right[j]*.22+up[j]*1.35;b=next_edge+right[n]*.22+up[n]*1.35;c=next_edge+right[n]*.22
  else:
   a=edge-right[j]*.22;b=next_edge-right[n]*.22;c=next_edge-right[n]*.22+up[n]*1.35
  rail_dots.append(np.dot(norm(np.cross(b-a,c-a)),-side*right[j]))
separation=np.linalg.norm(centers[:,None,:]-centers[None,:,:],axis=2);delta=np.abs(np.arange(768)[:,None]-np.arange(768)[None,:]);delta=np.minimum(delta,768-delta);separation[delta*L/768<150]=np.inf
spacing=np.linalg.norm(np.roll(centers,-1,axis=0)-centers,axis=1)
# Segment projection test: a point 37% along every segment should project to that
# segment, including the final wrapping segment, without half-segment jumps.
worst=0
for j in range(768):
 n=(j+1)%768;point=centers[j]*.63+centers[n]*.37;idx=(np.arange(j-22,j+23)%768);D=centers[(idx+1)%768]-centers[idx]
 blend=np.clip(np.sum((point-centers[idx])*D,axis=1)/np.sum(D*D,axis=1),0,1);dist=np.linalg.norm(point-centers[idx]-blend[:,None]*D,axis=1);best=np.argmin(dist);progress=((idx[best]+blend[best])/768)%1;expected=(j+.37)/768;err=(progress-expected+.5)%1-.5;worst=max(worst,abs(err)*L)
metrics=dict(length_m=float(L),width_m=22,samples=768,spacing_min_m=float(spacing.min()),spacing_max_m=float(spacing.max()),spacing_variation_percent=float((spacing.max()-spacing.min())/spacing.mean()*100),max_curvature_rad_per_m=float(abs(curvature).max()),min_sampled_radius_m=float(1/abs(curvature).max()),bank_max_degrees=float(abs(bank).max()*180/np.pi),min_nonlocal_center_separation_m=float(separation.min()),min_up_normal_dot=float(min(normal_dots)),min_inward_rail_normal_dot=float(min(rail_dots)),frame_orthogonality_error=float(abs(np.sum(right*tangent,axis=1)).max()),segment_projection_error_m=float(worst),elevation_min_m=float(centers[:,1].min()),elevation_max_m=float(centers[:,1].max()))
assert metrics['spacing_variation_percent']<.1
assert metrics['min_nonlocal_center_separation_m']>50
assert metrics['min_up_normal_dot']>.95
assert metrics['min_inward_rail_normal_dot']>.95
assert metrics['frame_orthogonality_error']<1e-6
assert worst<1e-5
print(json.dumps(metrics,indent=2))
(verification_dir/'track_metrics.json').write_text(json.dumps(metrics,indent=2)+'\n')
