Shader "Custom/CalmObsidianOcean"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite Texture", 2D) = "white" {}
        _BaseDarkness ("Wall Base Color", Color) = (0.02, 0.02, 0.02, 1)
        
        [Header(Wave Settings)]
        _WaveScale ("Wave Size", Float) = 4.0
        _WaveSpeed ("Wave Speed", Float) = 0.5
        [HDR] _SheenColor ("Sheen Color", Color) = (1, 1, 1, 1)
        _SheenIntensity ("Sheen Brightness", Range(0, 10)) = 2.5
        _SheenTightness ("Sheen Sharpness", Range(1, 20)) = 10.0
        
        [Header(Refraction)]
        _Distortion ("Liquid Distortion", Range(0, 0.2)) = 0.05
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha 
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; float3 worldPos : TEXCOORD1; };

            sampler2D _MainTex;
            float4 _BaseDarkness;
            float4 _SheenColor;
            float _WaveScale, _WaveSpeed, _SheenIntensity, _SheenTightness, _Distortion;

            float2 hash(float2 p) {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return -1.0 + 2.0 * frac(sin(p) * 43758.5453123);
            }

            float noise(float2 p) {
                float2 i = floor(p); float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(dot(hash(i + float2(0,0)), f - float2(0,0)), 
                                 dot(hash(i + float2(1,0)), f - float2(1,0)), u.x),
                            lerp(dot(hash(i + float2(0,1)), f - float2(0,1)), 
                                 dot(hash(i + float2(1,1)), f - float2(1,1)), u.x), u.y);
            }

            Varyings vert (Attributes IN) {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target {
                // 1. MASK BY SPRITE SHAPE
                half4 sprite = tex2D(_MainTex, IN.uv);
                if (sprite.a < 0.1) discard;

                // 2. GENERATE SMOOTH OCEAN NOISE
                float2 uv = IN.worldPos.xy * (_WaveScale * 0.1);
                float t = _Time.y * _WaveSpeed;
                
                float n = noise(uv + t);
                n += noise(uv * 2.0 - t * 0.5) * 0.5;

                // 3. CALCULATE THE "SHEEN"
                float edge = 0.01;
                float nX = noise(uv + float2(edge, 0) + t);
                float nY = noise(uv + float2(0, edge) + t);
                
                float dist = length(float2(nX - n, nY - n));
                float sheen = pow(saturate(dist * 10.0), _SheenTightness);

                // 4. FINAL COLOR COMPOSITION
                half3 finalColor = _BaseDarkness.rgb;
                
                // Add the colored sheen
                finalColor += sheen * _SheenIntensity * _SheenColor.rgb;

                // RIM GLOW REMOVED HERE to prevent tile borders/lines
                
                return half4(finalColor, sprite.a);
            }
            ENDHLSL
        }
    }
}