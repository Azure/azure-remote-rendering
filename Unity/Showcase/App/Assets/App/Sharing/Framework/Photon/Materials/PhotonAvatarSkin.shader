// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

Shader "Photon Avatar Skin"
{
    Properties
    {
        [PerRendererData]_Color("Color", Color) = (1,1,1,1)
        _AmbientColor("Ambient Color", Color) = (0, 0, 0, 1)
        _MainTex("Main Texture", 2D) = "black" {}
        [Toggle(_CLIP)] _EnableClipping("Enable clipping", Float) = 1.0
        _ClipTex("Clip Texture", 2D) = "black" {}
        _ClipTexRepeat("Clip texture repeat", Float) = 5
        _ClipNear("Clip near", Float) = .5
        _ClipFar("Clip far", Float) = 1.5

        _SpecularColor("Specular Color", Color) = (0.9,0.9,0.9,1)
        // Controls the size of the specular reflection.
        _Glossiness("Glossiness", Float) = 32
        _RimColor("Rim Color", Color) = (1,1,1,1)
        _RimAmount("Rim Amount", Range(0, 1)) = 0.716
    }

    SubShader
    {
        Pass
        {
            Tags
            {
                "RenderType" = "Opaque"
                "RenderPipeline" = "UniversalPipeline"
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature _CLIP

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _AmbientColor;
            float4 _SpecularColor;
            float _Glossiness;		
            float4 _RimColor;
            float _RimAmount;
            float _RimThreshold;

#if defined(_CLIP)
            sampler2D _ClipTex;
            float _ClipNear;
            float _ClipFar;
            float _ClipTexRepeat;
#endif
            struct appdata
            {
                float4 vertex : POSITION;
                float4 uv : TEXCOORD0;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : NORMAL;
                float2 uv : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
#if defined(_CLIP)
                float4 worldPos: TEXCOORD2;
                float4 localPos: TEXCOORD3;
#endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v); //Insert
                UNITY_INITIALIZE_OUTPUT(v2f, o); //Insert
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); //Insert

                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = normalize(UnityObjectToWorldNormal(v.normal));
                o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
#if defined(_CLIP)
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.localPos = v.vertex;
#endif
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
#if defined(_CLIP)
                // Fade out when too close

                float cameraDist = distance(i.worldPos, _WorldSpaceCameraPos);
                float4 st = (i.localPos * _ClipTexRepeat);
                float noise = tex2D(_ClipTex, st); // [0, 1]

                noise = noise * 2 - 1 + (_SinTime.w*_CosTime.w*.2); // [-1, 1]

                float width = abs(_ClipFar - _ClipNear);
                float center = (width * .5) + _ClipNear;
                float noisyClipDist = center + noise * width;

                if (cameraDist < noisyClipDist)
                {
                    discard;
                }
#endif
                float3 normal = normalize(i.worldNormal);
                float3 viewDir = normalize(i.viewDir);

                // Lighting below is calculated using Blinn-Phong,
                // with values thresholded to creat the "toon" look.
                // https://en.wikipedia.org/wiki/Blinn-Phong_shading_model

                // Calculate illumination from directional light.
                // _WorldSpaceLightPos0 is a vector pointing the OPPOSITE
                // direction of the main directional light.
                float3 fakeLightPos = float3(0.5,2,0.5);
                float NdotL = dot(fakeLightPos, normal);

                // Partition the intensity into light and dark, smoothly interpolated
                // between the two to avoid a jagged break.
                float lightIntensity = smoothstep(0, 0.01, NdotL);	
                // Multiply by the main directional light's intensity and color.
                float4 light = lightIntensity * float4(0,0,0,0);// * _LightColor0;

                // Calculate specular reflection.
                float3 halfVector = normalize(fakeLightPos + viewDir);
                float NdotH = dot(normal, halfVector);
                // Multiply _Glossiness by itself to allow artist to use smaller
                // glossiness values in the inspector.
                float specularIntensity = pow(NdotH * lightIntensity, _Glossiness * _Glossiness);
                float specularIntensitySmooth = smoothstep(0.005, 0.01, specularIntensity);
                float4 specular = specularIntensitySmooth * _SpecularColor;				

                // Calculate rim lighting.
                float rimDot = 1 - dot(viewDir, normal);
                // We only want rim to appear on the lit side of the surface,
                // so multiply it by NdotL, raised to a power to smoothly blend it.
                float rimIntensity = smoothstep(_RimAmount - 0.01, _RimAmount + 0.01, rimDot);

                float4 rim = rimIntensity * _RimColor;

                float4 sample = tex2D(_MainTex, i.uv)*2; // We are using the same texture to both lighten and darken the image. We use 50% gray (0.5) to represent neutral effect antyhting above this lightens the texture.

                return (light + _AmbientColor + specular + rim) * _Color * sample;

            }
            ENDCG
        }
    }
}
