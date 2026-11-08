Shader "Hyperion/Nebula Sky"
{
    Properties { _Exposure("Exposure", Range(0, 2)) = 0.8 }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            float _Exposure;
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.dir = v.vertex.xyz; return o; }
            float hash(float3 p) { p = frac(p * 0.1031); p += dot(p, p.yzx + 33.33); return frac((p.x + p.y) * p.z); }
            float noise(float3 p)
            {
                float3 i = floor(p), f = frac(p); f = f*f*(3.0-2.0*f);
                return lerp(lerp(lerp(hash(i), hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+1),f.x),f.y),f.z);
            }
            float fbm(float3 p)
            {
                float result=0, amplitude=0.5;
                for(int i=0;i<4;i++) { result += noise(p)*amplitude; p = p*2.07 + float3(11.7,3.1,8.2); amplitude *= 0.5; }
                return result;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float cloud = fbm(d*4.2 + float3(1.2,5.1,3.4));
                float filament = fbm(d*15 + cloud*3);
                float band = pow(saturate(1.0-abs(dot(d,normalize(float3(0.3,0.85,-0.25))))),8);
                float density = smoothstep(0.28,0.72,cloud)*band;
                float3 cyan = float3(0.012,0.11,0.15), violet = float3(0.11,0.024,0.18);
                float3 nebula = lerp(cyan,violet,saturate(d.x*0.6+0.5)) * density * (0.35+filament);
                float dust = smoothstep(0.42,0.75,filament);
                float3 color = float3(0.002,0.004,0.012) + nebula*(1-dust*0.65);
                float3 starCell = floor(d*1200);
                float star = step(0.9989,hash(starCell)) * pow(saturate(hash(starCell+17)),6) * 0.4;
                color += star * float3(0.7,0.82,1);
                return float4(color * _Exposure,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
