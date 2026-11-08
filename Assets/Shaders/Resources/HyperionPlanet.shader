Shader "Hyperion/Cloud Planet"
{
    Properties
    {
        _Color("Surface",Color) = (0.2,0.4,0.8,1)
        _ShadowColor("Ocean",Color) = (0.01,0.04,0.1,1)
        _CloudColor("Cloud",Color) = (0.7,0.8,1,1)
        _Seed("Seed",Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            float4 _Color, _ShadowColor, _CloudColor; float _Seed;
            struct v2f { float4 pos:SV_POSITION; float3 normal:TEXCOORD0; float3 sphere:TEXCOORD1; };
            v2f vert(appdata_base v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.normal=UnityObjectToWorldNormal(v.normal); o.sphere=normalize(v.vertex.xyz); return o; }
            float hash(float3 p) { return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453); }
            float noise(float3 p)
            {
                float3 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+1),f.x),f.y),f.z);
            }
            fixed4 frag(v2f i):SV_Target
            {
                float3 p=normalize(i.sphere)*5+_Seed;
                float continental=noise(p)*0.6+noise(p*2.1)*0.28+noise(p*4.5)*0.12;
                float clouds=noise(p*3.5+noise(p))*0.7+noise(p*8.3)*0.3;
                float3 surface=lerp(_ShadowColor.rgb,_Color.rgb,smoothstep(0.3,0.63,continental));
                surface=lerp(surface,_CloudColor.rgb,smoothstep(0.56,0.78,clouds)*0.58);
                float light=saturate(dot(normalize(i.normal),normalize(_WorldSpaceLightPos0.xyz)));
                return float4(surface*(0.04+light*_LightColor0.rgb*1.15),1);
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
