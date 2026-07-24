Shader "UI/DatamoshUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _MoshAmount ("Datamosh Amount", Range(0,1)) = 0

        _PixelSize ("Pixel Block Size", Range(1,100)) = 30
        _GlitchStrength ("Glitch Strength", Range(0,0.1)) = 0.03

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
            float _CorruptionSoftness;

            v2f vert(appdata_t v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color;

                return o;
            }

            float random(float2 p)
            {
                return frac(sin(dot(p,float2(12.9898,78.233))) * 43758.5453);
            }

            // Smooth value noise
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = random(i);
                float b = random(i + float2(1,0));
                float c = random(i + float2(0,1));
                float d = random(i + float2(1,1));

                f = f * f * (3.0 - 2.0 * f);

                return lerp(
                    lerp(a,b,f.x),
                    lerp(c,d,f.x),
                    f.y
                );
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                //----------------------------------------
                // Animated warp field
                //----------------------------------------

                float t = _Time.y * 0.25;

                float2 warp;

                warp.x = noise(uv * 5 + float2(t,0));
                warp.y = noise(uv * 5 + float2(31.7,t));

                warp = (warp - 0.5) * 0.12;

                float2 warpedUV = uv + warp;

                //----------------------------------------
                // Organic corruption front
                //----------------------------------------

                float edgeDistance = min(
                    min(warpedUV.x, 1 - warpedUV.x),
                    min(warpedUV.y, 1 - warpedUV.y)
                );

                float corruption = 1 - smoothstep(
                    _MoshAmount * 0.5,
                    (_MoshAmount * 0.5) + _CorruptionSoftness,
                    edgeDistance
                );

                //----------------------------------------
                // Datamosh blocks follow corruption
                //----------------------------------------

                float2 blocks = floor(warpedUV * _PixelSize);

                float glitchNoise =
                    random(blocks + floor(_Time.y * 12));

                //----------------------------------------
                // Horizontal tearing
                //----------------------------------------

                float tear =
                    step(0.82, glitchNoise)
                    * _GlitchStrength
                    * corruption;

                warpedUV.x += tear;

                //----------------------------------------
                // RGB separation
                //----------------------------------------

                float rgbOffset =
                    corruption * 0.02;

                float r = tex2D(
                    _MainTex,
                    warpedUV + float2(rgbOffset,0)
                ).r;

                float g = tex2D(
                    _MainTex,
                    warpedUV
                ).g;

                float b = tex2D(
                    _MainTex,
                    warpedUV - float2(rgbOffset,0)
                ).b;

                fixed4 col;

                col.r = r;
                col.g = g;
                col.b = b;
                col.a = tex2D(_MainTex, warpedUV).a * i.color.a;

                return col;
            }

            ENDCG
        }
    }
}