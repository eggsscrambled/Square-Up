Shader "Custom/FOVRadialFade"
{
    Properties
    {
        // Match this to your script's 'viewDistance' for a perfect fade right to the edge
        _Radius ("Vision Radius", Float) = 10.0
        // Controls how close to the edge the fade starts (0.8 = starts fading at 80% distance)
        _FadeStart ("Fade Start Percent", Range(0, 1)) = 0.8
    }
    SubShader
    {
        // We need this to be transparent so it can fade smoothly
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                // We will pass the distance from center to the fragment shader
                float dist : TEXCOORD0;
            };

            float _Radius;
            float _FadeStart;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                // Calculate distance from object center (0,0,0) in object space
                // Since your mesh origin is the player, this works perfectly.
                o.dist = length(v.vertex.xyz);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. Normalize distance to a 0-1 range based on the Radius property
                float normalizedDist = saturate(i.dist / _Radius);

                // 2. Create a smooth fade factor.
                // smoothstep returns 0 if we are below _FadeStart, and interpolates to 1 at the edge.
                float fadeFactor = smoothstep(_FadeStart, 1.0, normalizedDist);

                // 3. Invert it so center is 1 (white) and edge is 0 (black)
                float brightness = 1.0 - fadeFactor;

                // Return solid white with the calculated brightness as the alpha
                // Since your Render Texture is probably read as R or A, this works for both.
                return fixed4(1, 1, 1, brightness);
            }
            ENDCG
        }
    }
}