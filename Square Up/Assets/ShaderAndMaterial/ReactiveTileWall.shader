Shader "Custom/VoronoiObsidianFlow"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite Texture", 2D) = "white" {}
        _BaseDarkness ("Base Tile Darkness", Range(0, 1)) = 0.04
        
        [Header(Fluid Blobs)]
        _BlobScale ("Blob Size", Float) = 5.0
        _BlobSpeed ("Flow Speed", Float) = 0.8
        _ColorVibrancy ("Color Vibrancy", Range(0, 10)) = 5.0
        
        [Header(Refraction)]
        _RefractionDepth ("Refraction Depth", Range(0, 1)) = 0.4
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
            float _BaseDarkness, _BlobScale, _BlobSpeed, _ColorVibrancy, _RefractionDepth;

            // --- PROCEDURAL VORONOI ---
            float2 hash22(float2 p) {
                float3 p3 = frac(float3(p.xyx) * float3(.1031, .1030, .0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            float voronoi(float2 x) {
                float2 n = floor(x);
                float2 f = frac(x);
                float m = 8.0;
                for(int j=-1; j<=1; j++)
                for(int i=-1; i<=1; i++) {
                    float2 g = float2(float(i),float(j));
                    float2 o = hash22(n + g);
                    // Animate the cell points for the "blob" movement
                    o = 0.5 + 0.5 * sin(_Time.y * _BlobSpeed + 6.2831 * o);
                    float2 r = g + o - f;
                    float d = dot(r,r);
                    m = min(m, d);
                }
                return sqrt(m);
            }

            half3 SpectralColor (float hue) {
                half3 rgb = saturate(half3(abs(hue * 6.0 - 3.0) - 1.0, 2.0 - abs(hue * 6.0 - 2.0), 2.0 - abs(hue * 6.0 - 4.0)));
                return rgb;
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

                // 2. REFRACTION MATH
                // We use a secondary slower noise to warp the main Voronoi coords
                float2 refractCoords = IN.worldPos.xy * 0.5;
                float warp = voronoi(refractCoords + _Time.y * 0.2);
                float2 finalCoords = (IN.worldPos.xy + (warp * _RefractionDepth)) * (_BlobScale * 0.2);

                // 3. GENERATE FLUID BLOBS
                // Layering two voronoi patterns creates the "merging" effect
                float v1 = voronoi(finalCoords + _Time.y * _BlobSpeed);
                float v2 = voronoi(finalCoords - _Time.y * (_BlobSpeed * 0.5));
                
                // Invert voronoi so centers of blobs are bright
                float mask = pow(saturate(1.0 - (v1 * v2)), 8.0);

                // 4. IRIDESCENT COLORING
                // Hue shifts based on world position and the blob intensity
                float hue = frac(mask * 0.2 + _Time.y * 0.05 + (IN.worldPos.x * 0.05));
                half3 bloomColor = SpectralColor(hue) * _ColorVibrancy;

                // 5. FINAL COLOR
                half3 obsidianBase = half3(_BaseDarkness, _BaseDarkness, _BaseDarkness);
                half3 result = obsidianBase + (bloomColor * mask);

                return half4(result, sprite.a);
            }
            ENDHLSL
        }
    }
}