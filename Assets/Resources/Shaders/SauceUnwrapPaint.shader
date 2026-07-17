// ─────────────────────────────────────────────────────────────
// SauceUnwrapPaint — texture-space 醬料染色用的 UV 攤開 shader
// 把 SkinnedMeshRenderer「攤開」在 UV 空間 rasterize:
//   頂點輸出位置 = 該頂點的 UV(而不是螢幕位置),
//   fragment 拿蒙皮後的世界座標,判斷是否落在醬料投影範圍內。
// 由 SaucePaintable 透過 CommandBuffer.DrawRenderer 使用,
// 畫進角色專屬的 albedo RenderTexture。放 Resources 確保進 build。
// ─────────────────────────────────────────────────────────────
Shader "Hidden/Pizzala/SauceUnwrapPaint"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "SauceUnwrapPaint"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_SplatTex);
            SAMPLER(sampler_SplatTex);

            float3 _PaintPos;     // 命中點(世界座標)
            float3 _PaintNormal;  // 命中法線(指向身體外側)
            float3 _PaintTangent; // 醬料圖樣的旋轉方向(隨機)
            float  _PaintSize;    // 醬料直徑(公尺)
            float  _PaintDepth;   // 沿法線的作用厚度(公尺),防染穿到背面

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                // 用 UV 當輸出位置 → 畫進貼圖的對應位置
                float2 p = input.uv * 2.0 - 1.0;
                #if UNITY_UV_STARTS_AT_TOP
                p.y = -p.y;
                #endif
                o.positionCS = float4(p, 0.0, 1.0);
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float3 n = normalize(_PaintNormal);
                float3 t = normalize(_PaintTangent - n * dot(_PaintTangent, n));
                float3 b = cross(n, t);
                float3 d = i.positionWS - _PaintPos;

                // 這個表面點在醬料圖樣上的 UV
                float2 suv = float2(dot(d, t), dot(d, b)) / _PaintSize + 0.5;
                if (suv.x < 0.0 || suv.x > 1.0 || suv.y < 0.0 || suv.y > 1.0) discard;

                // 離命中面太深(身體另一側)漸淡;背對命中方向的面不染
                float along  = abs(dot(d, n));
                float fade   = 1.0 - smoothstep(_PaintDepth * 0.5, _PaintDepth, along);
                float facing = saturate(dot(normalize(i.normalWS), n) * 4.0);

                float4 s = SAMPLE_TEXTURE2D(_SplatTex, sampler_SplatTex, suv);
                s.a *= fade * facing;
                if (s.a <= 0.001) discard;
                return s;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
