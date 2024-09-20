Shader "ThumbnailCreator/DownsamplingBlitter"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        LOD 100
        
        Cull Off
        ZWrite Off
        ZTest Always
        Blend One One

        Pass
        {
            HLSLPROGRAM
            
            #pragma vertex vertexProgram
            #pragma fragment fragmentProgram
            #pragma target 5.0
            #pragma multi_compile_local _SIDE_LENGTH_1 _SIDE_LENGTH_2 _SIDE_LENGTH_4

            #include "UnityCG.cginc"
            
            #if defined(_SIDE_LENGTH_4)
                #define SIDE_LENGTH 4

            #elif defined(_SIDE_LENGTH_2)
                #define SIDE_LENGTH 2

            #elif defined(_SIDE_LENGTH_1)
                #define SIDE_LENGTH 1

            #else
                #error Unsupported side length

            #endif


            Texture2D _MainTex;
            float4 _MainTex_TexelSize;

            float4 vertexProgram (float4 v : POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(v);
            }

            float4 fragmentProgram (float4 screenPos : SV_POSITION) : SV_Target
            {
                //Check to make sure that the destination texture has the
                // correct dimensions. 
                if(any((_ScreenParams.xy * SIDE_LENGTH) != _MainTex_TexelSize.zw))
                {
                    //Dimensions are wrong, draw a checker board texture instead.
                    float2 p = frac(floor(screenPos.xy / 32) / 2);
                    
                    return (p.x == p.y) ? float4(0,1,1,1) : float4(0,0,0,1);
                }


                uint2 baseIndex = uint2(floor(screenPos.xy)) * SIDE_LENGTH;
                
                float4 color = 0;
                                
                [unroll] for(int y = 0; y < SIDE_LENGTH; y += 1)
                {
                    [unroll] for(int x = 0; x < SIDE_LENGTH; x += 1)
                    {
                        color += saturate(_MainTex[baseIndex + uint2(x, y)]);
                    }
                }
                
                return color / float(SIDE_LENGTH * SIDE_LENGTH);
            }
            
            ENDHLSL
        }
    }
}
