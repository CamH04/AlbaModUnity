Shader "UI/DatamoshUI"
{
    Properties
{
    [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

    _MoshAmount ("Datamosh Amount", Range(0,1)) = 0

    _PixelSize ("Pixel Block Size", Range(1,100)) = 30
    _GlitchStrength ("Glitch Strength", Range(0,0.1)) = 0.03

    _CorruptionDirection ("Corruption Direction", Range(0,1)) = 0
    _CorruptionSoftness ("Corruption Edge Softness", Range(0.01,0.5)) = 0.05
}

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float _MoshAmount;
            float _PixelSize;
            float _GlitchStrength;

            float _CorruptionDirection;
            float _CorruptionSoftness;

            v2f vert(appdata_t v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color;

                return o;
            }


            float random(float2 seed)
            {
                return frac(sin(dot(seed, float2(12.9898,78.233))) * 43758.5453);
            }


            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float edgeDistance = min(
                    min(uv.x, 1 - uv.x),
                    min(uv.y, 1 - uv.y)
                );


                // Expand corruption inward from edges
                float corruption = 1 - smoothstep(
                    _MoshAmount * 0.5,
                    (_MoshAmount * 0.5) + _CorruptionSoftness,
                    edgeDistance
                );


                // Pixel block coordinates
                float2 blocks = floor(uv * _PixelSize);

                float glitchNoise = random(blocks + floor(_Time.y * 10));


                // Horizontal tearing
                float tear = step(0.85, glitchNoise)
                    * _GlitchStrength
                    * corruption;


                uv.x += tear;


                // RGB channel separation
                float rgbOffset = corruption * 0.02;


                float r = tex2D(
                    _MainTex,
                    uv + float2(rgbOffset,0)
                ).r;

                float g = tex2D(
                    _MainTex,
                    uv
                ).g;

                float b = tex2D(
                    _MainTex,
                    uv - float2(rgbOffset,0)
                ).b;


                fixed4 col;
                col.r = r;
                col.g = g;
                col.b = b;
                col.a = tex2D(_MainTex, uv).a * i.color.a;


                return col;
            }

            ENDCG
        }
    }
}