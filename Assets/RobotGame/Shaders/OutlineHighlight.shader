Shader "RobotGame/OutlineHighlight"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0, 1, 0.5, 1)
        _OutlineWidth ("Outline Width", Range(0.0, 0.1)) = 0.02
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent+1"
            "RenderPipeline" = "UniversalPipeline"
        }
        
        Pass
        {
            Name "OUTLINE"
            
            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };
            
            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                float3 normalOS = normalize(input.normalOS);
                float3 expandedPos = input.positionOS.xyz + normalOS * _OutlineWidth;
                
                output.positionCS = TransformObjectToHClip(expandedPos);
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
    
    FallBack Off
}
