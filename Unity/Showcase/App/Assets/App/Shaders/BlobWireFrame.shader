// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

Shader "BlobWireFrame"
{
    Properties
    {
        _DispTex("Disp Texture", 2D) = "gray" {}
        _Displacement("Displacement", Range(0, 1.0)) = 0.3
        _Offset("Offset", Vector) = (0,0,0,0)
        _WireColor("WireColor", Color) = (1,0,0,1)
        _Color("Color", Color) = (1,1,1,1)
        [HDR]_RimColor("Rim Color", Color) = (1,1,1,1)
        _RimPow("Rim Power", Float) = .7
    }
        SubShader
        {
            Tags{"RenderType" = "Opaque" "PerformanceChecks" = "False" }
            Pass
            {
                ZWrite On
                CGPROGRAM
                #include "UnityCG.cginc"

                #pragma target 5.0
                #pragma vertex vert
                #pragma fragment frag

                half4 _WireColor, _Color;
                float4 _RimColor;
                float _RimPow;

                struct appdata
                {
                    float4 position : POSITION;
                    uint   vertexId : SV_VertexID;
                    UNITY_VERTEX_INPUT_INSTANCE_ID
                };

                struct v2f
                {
                    float4 pos : SV_POSITION;
                    float4 color : COLOR;
                    float3 dist : TEXCOORD0;
                    UNITY_VERTEX_OUTPUT_STEREO
                };

                sampler2D _DispTex;
                float _Displacement;
                float2 _Offset;

                v2f vert(appdata v)
                {
                    v2f OUT;
                    UNITY_SETUP_INSTANCE_ID(v);
                    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                    // Bayricentrics for the fragment stage are computed by assigning the vertices
                    // in the triangle the edge coordinates (1, 0, 0), (0, 1, 0), and (0, 0, 1) 
                    // respectively and letting the rasterizer do the rest. This requires a mesh
                    // where vertices that are shared between triangles have the same local index.
                    uint localVertex = v.vertexId % 3;
                    OUT.dist = float3(localVertex == 0, localVertex == 1, localVertex == 2);

                    float3 normal = v.position.xyz;
                    float4 position = float4(v.position.xyz * 0.05, 1);
                    float2 texcoord = float2(atan2(normal.y, normal.x) * 0.5, acos(normal.z)) * UNITY_INV_PI;

                    // Creating the wobbly effect.
                    float d = tex2Dlod(_DispTex, float4(texcoord + _Offset, 0, 0)).r * _Displacement;
                    position.xyz += normal * d;
                    OUT.pos = UnityObjectToClipPos(position);

                    // Computing the rim glow. 
                    float3 viewDir = normalize(ObjSpaceViewDir(position));
                    float dotProduct = 1 - dot(normal, viewDir);
                    OUT.color = _RimColor * smoothstep(1 - _RimPow, 1.0, dotProduct);

                    return OUT;
                }

                half4 frag(v2f IN) : COLOR
                {
                    // Since we're dealing with barycentric coordinates, meaning 
                    // x + y + z == 1, their gradients satisfy dx + dy + dz == 0,
                    // so we can compute the third gradient from the other two.
                    float2 dx = ddx_coarse(IN.dist.xy);
                    float2 dy = ddy_coarse(IN.dist.xy);

                    float3 dist_ddx = float3(dx, -dx.x - dx.y);
                    float3 dist_ddy = float3(dy, -dy.x - dy.y);

                    // Making sure the outline wire always has the same pixel width
                    float3 fwidth_coarse_dist = abs(dist_ddx) + abs(dist_ddy);
                    float3 d3 = IN.dist * rcp(fwidth_coarse_dist);

                    // Distance of frag from triangles center
                    float d = min(d3.x, min(d3.y, d3.z)) * 1.5;

                    // Fade based on dist from center
                    float I = exp2(-1 * d * d);
                    
                    half4 output = _Color;
                    half4 emission = IN.color;
                    output.rgb += emission.rgb;

                    return lerp(output, _WireColor, I);
                }

                ENDCG
            }
        }
            FallBack "Unlit/Texture"
}