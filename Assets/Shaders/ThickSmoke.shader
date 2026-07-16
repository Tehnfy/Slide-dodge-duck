// Thick billboard smoke for particle systems (URP, transparent unlit).
// Tuned as "black fire": soot-dark volumes with ridged, gritty noise, an
// upward flow bias, a breathing erosion pulse, and a faint ember rim along
// the eroded edge so the black shape stays readable in dark rooms.
//
// The core recipe:
//  1. Noise EROSION of the alpha through a hard-ish smoothstep - dense
//     rolling edges instead of misty multiplied fog.
//  2. Ridged fbm (+ a high-frequency grit octave) - sinewy filaments that
//     read as licking flame rather than soft cloud. _Ridged blends between
//     cloudy (0) and flame-like (1).
//  3. A pulsing cutoff and per-region phase offset - the volume visibly
//     breathes instead of only scrolling.
//  4. Soft particles (scene-depth fade) + near-camera fade, as before.
//
// All noise is procedural - no textures needed. Vertex colour comes from
// the particle system, so Color/Alpha over Lifetime work.
Shader "SlideDodgeDuck/ThickSmoke"
{
    Properties
    {
        _TopColor ("Body Color (top)", Color) = (0.09, 0.085, 0.09, 1)
        _BottomColor ("Body Color (bottom)", Color) = (0.015, 0.015, 0.02, 1)
        _RimColor ("Ember Rim Color", Color) = (0.42, 0.14, 0.06, 1)
        _RimWidth ("Ember Rim Width", Range(0.0, 0.5)) = 0.14
        _Erosion ("Erosion Cutoff", Range(0, 1)) = 0.5
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.5)) = 0.07
        _Ridged ("Flame Ridging", Range(0, 1)) = 0.75
        _Grit ("Grit (high-freq noise)", Range(0, 1)) = 0.45
        _PulseSpeed ("Pulse Speed", Float) = 1.6
        _PulseAmount ("Pulse Amount", Range(0, 0.3)) = 0.08
        _NoiseScale ("Noise Scale", Float) = 3.4
        _ScrollSpeed ("Scroll Speed (layer A xy, layer B zw)", Vector) = (0.05, -0.24, -0.07, -0.16)
        _Distort ("Layer Distortion", Range(0, 1)) = 0.5
        _Density ("Density Boost", Range(0.5, 4)) = 1.8
        _SoftDistance ("Soft Particle Distance", Range(0.01, 3)) = 0.75
        _CameraFadeStart ("Camera Fade Start", Float) = 0.5
        _CameraFadeEnd ("Camera Fade End", Float) = 2.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ThickSmokeForward"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float eyeDepth : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _BottomColor;
                half4 _RimColor;
                float4 _ScrollSpeed;
                float _RimWidth;
                float _Erosion;
                float _EdgeSoftness;
                float _Ridged;
                float _Grit;
                float _PulseSpeed;
                float _PulseAmount;
                float _NoiseScale;
                float _Distort;
                float _Density;
                float _SoftDistance;
                float _CameraFadeStart;
                float _CameraFadeEnd;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // Ridged variant: folds the noise around its midpoint, producing
            // sharp sinewy creases - the filament structure of flame/soot.
            float RidgedNoise(float2 p)
            {
                return 1.0 - abs(2.0 * ValueNoise(p) - 1.0);
            }

            float Fbm(float2 p)
            {
                float soft = ValueNoise(p) * 0.6
                           + ValueNoise(p * 2.13 + 7.7) * 0.3
                           + ValueNoise(p * 4.71 + 3.1) * 0.1;
                float ridged = RidgedNoise(p) * 0.55
                             + RidgedNoise(p * 2.13 + 7.7) * 0.3
                             + RidgedNoise(p * 4.71 + 3.1) * 0.15;
                return lerp(soft, ridged, _Ridged);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.uv = v.uv;
                o.color = v.color;
                o.eyeDepth = -pos.positionVS.z;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Feathered disc so the sprite has no square silhouette.
                float2 fromCenter = i.uv - 0.5;
                float baseShape = saturate(1.0 - length(fromCenter) * 2.0);
                baseShape = smoothstep(0.0, 0.6, baseShape);

                // Two fbm layers at different scales/speeds, the first
                // distorting the second's UVs. Default scroll is upward so
                // the pattern licks up like flame.
                float2 uvA = i.uv * _NoiseScale + _Time.y * _ScrollSpeed.xy;
                float2 uvB = i.uv * (_NoiseScale * 1.7) + _Time.y * _ScrollSpeed.zw;
                float noiseA = Fbm(uvA);
                float noiseB = Fbm(uvB + noiseA * _Distort);
                float noise = noiseA * 0.5 + noiseB * 0.5;

                // Soot grit: an extra high-frequency octave, centred so it
                // roughens the field without brightening it.
                noise += (ValueNoise(uvA * 8.9 + uvB) - 0.5) * _Grit * 0.35;

                // Breathing: the erosion cutoff oscillates, phase-shifted by
                // the low-frequency noise so regions pulse independently
                // instead of the whole volume throbbing in sync.
                float cutoff = _Erosion
                    + sin(_Time.y * _PulseSpeed + noiseA * 6.2832) * _PulseAmount;

                // Erosion: the +baseShape bias keeps the core solid while the
                // rim breaks up into rolling wisps.
                float field = noise + baseShape * 0.35;
                float alpha = baseShape * smoothstep(
                    cutoff - _EdgeSoftness, cutoff + _EdgeSoftness, field);

                // Body shading: near-black with a slightly lighter top.
                float shade = saturate(i.uv.y * 0.7 + noise * 0.5);
                half3 col = lerp(_BottomColor.rgb, _TopColor.rgb, shade);

                // Ember rim: a thin band just inside the eroded edge, like
                // the smoulder line on burning paper. Keeps the black shape
                // readable in dark rooms and sells "alive".
                float rim = smoothstep(cutoff - _EdgeSoftness, cutoff, field)
                          * (1.0 - smoothstep(cutoff, cutoff + _RimWidth, field));
                col += _RimColor.rgb * rim;

                // Soft particles: fade where the sprite nears scene geometry.
                float2 screenUV = i.positionCS.xy / _ScaledScreenParams.xy;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                alpha *= saturate((sceneDepth - i.eyeDepth) / _SoftDistance);

                // Near-camera fade: no full-screen pop when walking through.
                alpha *= saturate((i.eyeDepth - _CameraFadeStart)
                                / max(_CameraFadeEnd - _CameraFadeStart, 0.001));

                alpha = saturate(alpha * _Density) * i.color.a;
                return half4(col * i.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
