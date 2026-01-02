Shader "Custom/SoftEnemy"
{
    Properties
    {
        [MainTexture] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        // Add the vision texture slot here too
        _VisionTex ("Vision Texture Mask", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float4 screenPos : TEXCOORD1; // Add screen position
            };

            sampler2D _MainTex;
            float4 _Color;
            sampler2D _VisionTex;

            v2f vert(appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                // Calculate screen pos
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                float4 texColor = tex2D(_MainTex, i.uv) * i.color;

                // Calculate screen UVs
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                // Read mask value. White = 1.0 (fully visible), Black = 0.0 (invisible)
                float maskValue = tex2D(_VisionTex, screenUV).r;

                // Multiply sprite alpha by the mask brightness
                texColor.a *= maskValue;

                return texColor;
            }
            ENDHLSL
        }
    }
}