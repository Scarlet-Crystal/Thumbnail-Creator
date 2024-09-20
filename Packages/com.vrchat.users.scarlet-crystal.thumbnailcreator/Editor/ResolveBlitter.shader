Shader "ThumbnailCreator/ResolveBlitter"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _JitteredSampleCount ("Jittered Sample Count", float) = 1.0
    }
    SubShader
    {
        LOD 100
        
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            
            #pragma vertex vertexProgram
            #pragma fragment fragmentProgram
            #pragma target 3.5

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _JitteredSampleCount;

            float4 vertexProgram (appdata_base v, out float2 uv : TEXCOORD0) : SV_POSITION
            {
                uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                return UnityObjectToClipPos(v.vertex);
            }

            float4 fragmentProgram (float4 vertex : SV_POSITION, float2 uv : TEXCOORD0) : SV_Target
            {
                return float4(tex2D(_MainTex, uv).rgb / _JitteredSampleCount, 1);
            }
            
            ENDHLSL
        }
    }
}
