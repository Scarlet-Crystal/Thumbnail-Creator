Shader "ThumbnailCreator/SupersamplingBlitter"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            
            #pragma vertex vertexProgram
            #pragma fragment fragmentProgram
            #pragma target 3.5
            #pragma multi_compile _SSAAx1 _SSAAx16 _SSAAx36 _SSAAx64 _SSAAx144

            #include "UnityCG.cginc"
            
            #if defined(_SSAAx144)
                #define SIDE_LENGTH 12
            #elif defined(_SSAAx64)
                #define SIDE_LENGTH 8
            #elif defined(_SSAAx36)
                #define SIDE_LENGTH 6
            #elif defined(_SSAAx16)
                #define SIDE_LENGTH 4
            #elif defined(_SSAAx1)
                #define SIDE_LENGTH 1
            #else
                #error Unsupported SSAA level
            #endif

            sampler2D _MainTex;
            float4 _MainTex_ST;

            void vertexProgram (appdata_base v, out float4 vertex : SV_POSITION, out float2 uv : TEXCOORD0)
            {
                vertex = UnityObjectToClipPos(v.vertex);
                uv = TRANSFORM_TEX(v.texcoord, _MainTex);
            }

            float4 fragmentProgram (float4 vertex : SV_POSITION, float2 uv : TEXCOORD0) : SV_Target
            {
                float2 x = ddx(uv);
                float2 y = ddy(uv);
                float3 color = 0;
                
                const float doubleLength = SIDE_LENGTH * 2;
                const int sideLengthMinusOne = SIDE_LENGTH - 1;
                
                int i = -sideLengthMinusOne;
                [unroll(SIDE_LENGTH)] while (true)
                {
                    float2 iOffset = (i / doubleLength) * x;
                    
                    int j = -sideLengthMinusOne;
                    [unroll(SIDE_LENGTH)] while (true)
                    {
                        float2 jOffset = (j / doubleLength) * y;
                        
                        color += saturate(tex2D(_MainTex, uv + iOffset + jOffset).rgb);
                        
                        j += 2;
                    }
                    
                    i += 2;
                }
                
                return float4(color / float(SIDE_LENGTH * SIDE_LENGTH), 1);
            }
            
            ENDHLSL
        }
    }
}
