Shader "Hyperion/Atmosphere Rim"
{
    Properties { _Color("Atmosphere", Color) = (0.1,0.6,1,1) _Intensity("Intensity", Range(0,3)) = 0.7 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha One ZWrite Off Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color; float _Intensity;
            struct v2f { float4 pos : SV_POSITION; float3 normal : TEXCOORD0; float3 view : TEXCOORD1; };
            v2f vert(appdata_base v)
            {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.normal = UnityObjectToWorldNormal(v.normal);
                o.view = _WorldSpaceCameraPos.xyz - mul(unity_ObjectToWorld,v.vertex).xyz; return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float rim = pow(1-saturate(dot(normalize(i.normal),normalize(i.view))),5);
                return float4(_Color.rgb*_Intensity, rim*0.65);
            }
            ENDCG
        }
    }
}
