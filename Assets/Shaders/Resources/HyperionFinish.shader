Shader "Hidden/Hyperion/Finish"
{
    Properties { _MainTex("Source",2D)="white"{} _BloomTex("Bloom",2D)="black"{} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex, _BloomTex;
        float4 _MainTex_TexelSize;
        float4 Extract(v2f_img i):SV_Target
        {
            float3 color=tex2D(_MainTex,i.uv).rgb;
            float brightness=max(color.r,max(color.g,color.b));
            return float4(color * saturate((brightness-1.1)/max(brightness,0.001)),1);
        }
        float4 Blur(v2f_img i,float2 direction)
        {
            float2 d=direction*_MainTex_TexelSize.xy*1.6;
            float3 c=tex2D(_MainTex,i.uv).rgb*0.227027;
            c+=tex2D(_MainTex,i.uv+d*1.384615).rgb*0.316216;
            c+=tex2D(_MainTex,i.uv-d*1.384615).rgb*0.316216;
            c+=tex2D(_MainTex,i.uv+d*3.230769).rgb*0.070270;
            c+=tex2D(_MainTex,i.uv-d*3.230769).rgb*0.070270;
            return float4(c,1);
        }
        float4 Horizontal(v2f_img i):SV_Target { return Blur(i,float2(1,0)); }
        float4 Vertical(v2f_img i):SV_Target { return Blur(i,float2(0,1)); }
        fixed4 Finish(v2f_img i):SV_Target
        {
            float3 c=tex2D(_MainTex,i.uv).rgb + tex2D(_BloomTex,i.uv).rgb*0.16;
            // Mild filmic highlight rolloff; retain dark asphalt detail.
            c=saturate((c*(2.51*c+0.03))/(c*(2.43*c+0.59)+0.14));
            float2 edge=(i.uv-0.5)*2;
            float vignette=1-0.11*dot(edge,edge);
            return float4(c*vignette,1);
        }
        ENDCG
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Extract
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Horizontal
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Vertical
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Finish
            #pragma target 3.0
            ENDCG
        }
    }
    Fallback Off
}
