Shader "Custom/SoftDarkness"
{
    Properties
    {
        _Color ("Darkness Color", Color) = (0,0,0,0.5)
        // This is where we plug in the Render Texture we created
        _VisionTex ("Vision Texture Mask", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent+1" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; };
            struct v2f {
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD0; // Needed to find where we are on screen
            };

            float4 _Color;
            sampler2D _VisionTex;

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                // Calculate screen position for texture mapping
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                // Calculate UV coordinates for the screen space
                float2 uv = i.screenPos.xy / i.screenPos.w;
                
                // Read the darkness value from the mask texture (R channel)
                // White in texture (1.0) means fully visible light, so darkness alpha should be 0.
                float maskValue = tex2D(_VisionTex, uv).r;
                
                float4 finalColor = _Color;
                // The brighter the mask, the lower the alpha of the darkness
                finalColor.a *= (1.0 - maskValue); 
                
                return finalColor;
            }
            ENDHLSL
        }
    }
}