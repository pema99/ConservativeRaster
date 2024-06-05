Shader "Unlit/ConservativeRasterVertex"
{
    SubShader
    {
        Cull Off

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
                nointerpolation float4 aabb : TEXCOORD1;
            };

            StructuredBuffer<float3> _VertexBuffer;

            void PerpendicularOffset(float2 v1, float2 v2, float pixelSize, out float2 v1Offset, out float2 v2Offset)
            {
                // Find the normal of the edge
                float2 edge = v2 - v1;
                float2 normal = normalize(float2(-edge.y, edge.x));

                // Find the amount to offset by. This is the semidiagonal of the pixel box in the same quadrant as the normal.
                float2 semidiagonal = pixelSize / sqrt(2.0);
                semidiagonal *= float2(normal.x > 0 ? 1 : -1, normal.y > 0 ? 1 : -1);

                // Offset the edge
                v1Offset = v1 + semidiagonal;
                v2Offset = v2 + semidiagonal;
            }

            float2 LineIntersect(float2 p1, float2 p2, float2 p3, float2 p4)
            {
                // Line p1p2 represented as a1x + b1y = c1
                float a1 = p2.y - p1.y;
                float b1 = p1.x - p2.x;
                float c1 = a1 * p1.x + b1 * p1.y;

                // Line p3p4 represented as a2x + b2y = c2
                float a2 = p4.y - p3.y;
                float b2 = p3.x - p4.x;
                float c2 = a2 * p3.x + b2 * p3.y;

                float determinant = a1 * b2 - a2 * b1; // TODO: Might be 0 (parallel lines)

                float x = (b2 * c1 - b1 * c2) / determinant;
                float y = (a1 * c2 - a2 * c1) / determinant;
                return float2(x, y);
            }

            v2f vert (appdata v, uint vertexId : SV_VertexID)
            {
                v2f o;

                uint resolution = 1024;
                float pixelSize = (1.0 / resolution);

                // Read triangle
                uint triId = vertexId / 3;
                uint baseVertexId = triId * 3;
                float2 tri[3] =
                {
                    _VertexBuffer[baseVertexId + 2].xy, // Winding order flipped for whatever fucking reason
                    _VertexBuffer[baseVertexId + 1].xy,
                    _VertexBuffer[baseVertexId + 0].xy,
                };

                // Calculate the AABB to clip off the overly conservative edges
                float2 aabbMin = min(min(tri[0], tri[1]), tri[2]);
                float2 aabbMax = max(max(tri[0], tri[1]), tri[2]);
                o.aabb = (float4(aabbMin - (pixelSize / 2), aabbMax + (pixelSize / 2))) * resolution;

                // Get offset lines
                float2 v1Off1, v2Off1;
                PerpendicularOffset(tri[0], tri[1], pixelSize, v1Off1, v2Off1);
                float2 v2Off2, v3Off2;
                PerpendicularOffset(tri[1], tri[2], pixelSize, v2Off2, v3Off2);
                float2 v3Off3, v1Off3;
                PerpendicularOffset(tri[2], tri[0], pixelSize, v3Off3, v1Off3);

                // Find their intersections. This is the new triangle
                tri[0] = LineIntersect(v1Off1, v2Off1, v3Off3, v1Off3);
                tri[1] = LineIntersect(v2Off2, v3Off2, v1Off1, v2Off1);
                tri[2] = LineIntersect(v3Off3, v1Off3, v2Off2, v3Off2);
                o.vertex = UnityObjectToClipPos(float3(tri[vertexId % 3], 0));

                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float2 pos = i.vertex.xy;
                if (pos.x < i.aabb.x || pos.y < i.aabb.y ||
                    pos.x > i.aabb.z || pos.y > i.aabb.w)
                    discard;

                return 1;
            }
            ENDCG
        }
    }
}
