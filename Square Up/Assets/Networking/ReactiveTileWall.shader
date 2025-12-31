Shader "Custom/ReactiveTileWall"
{
    Properties
    {
        [Header(Base Textures)]
        _MainTex ("Wall Sprite", 2D) = "white" {}
        _NoiseTex ("Fluid Noise", 2D) = "white" {}
        
        [Header(Fluid Settings)]
        [HDR] _FluidColor ("Fluid Color", Color) = (0, 1, 2, 1)
        _FlowSpeed ("Flow Speed", Vector) = (0.05, 0.05, 0, 0)
        _Distortion ("Liquid Distortion", Range(0, 0.5)) = 0.1
        
        [Header(Border Settings)]
        _BorderThickness ("Border Thickness", Range(0, 0.5)) = 0.1
        [HDR] _BorderColor ("Edge Glow Color", Color) = (1, 1, 1, 1)
        
        [Header(Player Reaction)]
        _GlowRange ("Reaction Radius", Float) = 4.0
        _GlowIntensity ("Glow Power", Range(0, 10)) = 2.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZWrite Off
        Blend One One 

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; float3 worldPos : TEXCOORD1; };

            sampler2D _MainTex; sampler2D _NoiseTex;
            float4 _FlowSpeed, _FluidColor, _BorderColor;
            float _Distortion, _BorderThickness, _GlowRange, _GlowIntensity;
            float4 _PlayerPositions[2]; 
            int _PlayerCount;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // 1. GENERATE BORDER MASK (Procedural)
                // This creates a square frame based on the UVs (0 to 1)
                float2 borderUV = abs(IN.uv - 0.5) * 2.0;
                float borderMask = smoothstep(1.0 - _BorderThickness, 1.0, max(borderUV.x, borderUV.y));
                
                // 2. LIQUID UV DISTORTION
                float2 distortion = tex2D(_NoiseTex, IN.uv + _Time.y * _FlowSpeed.xy).rg * _Distortion;
                float2 animatedUV = IN.uv + distortion + (_Time.y * _FlowSpeed.xy);
                half liquidNoise = tex2D(_NoiseTex, animatedUV).r;

                // 3. MULTI-PLAYER PROXIMITY
                float totalProximity = 0;
                for (int i = 0; i < _PlayerCount; i++) {
                    float dist = distance(IN.worldPos.xy, _PlayerPositions[i].xy);
                    totalProximity += pow(saturate(1.0 - (dist / _GlowRange)), 2.0);
                }
                totalProximity = saturate(totalProximity);

                // 4. FINAL COMPOSITION
                half4 baseTex = tex2D(_MainTex, IN.uv);
                
                // The "Inner" part gets the fluid, the "Edge" gets the border color
                half3 fluidPart = liquidNoise * _FluidColor.rgb * (1.0 - borderMask);
                half3 edgePart = borderMask * _BorderColor.rgb;
                
                half3 finalRGB = (fluidPart + edgePart) * _GlowIntensity * totalProximity;
                
                // Subtle mix with the actual tile texture
                finalRGB += baseTex.rgb * 0.2;

                return half4(finalRGB, 1.0);
            }
            ENDHLSL
        }
    }
}