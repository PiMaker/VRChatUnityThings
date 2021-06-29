// Based on the built-in Unity Skybox shader
Shader "_pi_/SkyboxNight" {
    Properties {
        [Gamma] _Exposure ("Exposure", Range(0, 8)) = 1.0
        _Rotation ("Rotation", Float) = 0
        _RotationSpeed ("Rotation Speed", Float) = 0

        _StarScale ("StarScale", Range(0, 100)) = 20
        _StarPow ("StarPow", Range(0, 200)) = 100
        _StarFade ("StarFade", Range(0, 1)) = 0.5

        _Horizon ("Horizon", Color) = (.5,.5,.5,1)
        _Sky ("Sky", Color) = (.3,.3,.3,1)
        _Fog ("Fog", Color) = (.9,.9,.9,1)
        _FogHeight ("FogHeight", Range(0, 32)) = 8

        _MoonDir ("MoonDir", Vector) = (.6, .5, .5, 0)
        _MoonMaskDir ("MoonMaskDir", Vector) = (.5, .5, .5, 0)
        _MoonSize ("MoonSize", Range(0, 0.5)) = 0.1
        _MoonMaskSize ("MoonMaskSize", Range(0, 0.5)) = 0.1
    }

    SubShader {
        Tags { "Queue"="Background+10" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        CGINCLUDE
        #include "UnityCG.cginc"

        half4 _Tint;
        half _Exposure;
        float _Rotation;

        float3 RotateAroundYInRad (float3 vertex, float alpha)
        {
            float sina, cosa;
            sincos(alpha, sina, cosa);
            float2x2 m = float2x2(cosa, -sina, sina, cosa);
            return float3(mul(m, vertex.xz), vertex.y).xzy;
        }

        struct appdata_t {
            float4 vertex : POSITION;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct v2f {
            float4 vertex : SV_POSITION;
            float3 texcoord : TEXCOORD0;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        v2f skybox_vert (appdata_t v, float rotationSpeed)
        {
            v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            float3 rotated = RotateAroundYInRad(v.vertex, _Rotation - _Time.x*0.1*rotationSpeed);
            o.vertex = UnityObjectToClipPos(rotated);
            o.texcoord = v.vertex;
            return o;
        }

        float rand3dTo1d(float3 value, float3 dotDir = float3(12.9898, 78.233, 37.719)){
            //make value smaller to avoid artefacts
            float3 smallValue = sin(value);
            //get scalar value from 3d vector
            float random = dot(smallValue, dotDir);
            //make value more random by making it bigger and then taking teh factional part
            random = frac(sin(random) * 143758.5453);
            return random;
        }

        float3 rand3dTo3d(float3 value){
            return float3(
                rand3dTo1d(value, float3(12.989, 78.233, 37.719)),
                rand3dTo1d(value, float3(39.346, 11.135, 83.155)),
                rand3dTo1d(value, float3(73.156, 52.235, 9.151))
            );
        }

        float3 voronoiNoise(float3 value){
        float3 baseCell = floor(value);

        //first pass to find the closest cell
        float minDistToCell = 10;
        float3 toClosestCell;
        float3 closestCell;
        [unroll]
        for(int x1=-1; x1<=1; x1++){
            [unroll]
            for(int y1=-1; y1<=1; y1++){
                [unroll]
                for(int z1=-1; z1<=1; z1++){
                    float3 cell = baseCell + float3(x1, y1, z1);
                    float3 cellPosition = cell + rand3dTo3d(cell);
                    float3 toCell = cellPosition - value;
                    float distToCell = length(toCell);
                    if(distToCell < minDistToCell){
                    minDistToCell = distToCell;
                    closestCell = cell;
                    toClosestCell = toCell;
                    }
                }
            }
        }

        //second pass to find the distance to the closest edge
        float minEdgeDistance = 10;
        [unroll]
        for(int x2=-1; x2<=1; x2++){
            [unroll]
            for(int y2=-1; y2<=1; y2++){
                [unroll]
                for(int z2=-1; z2<=1; z2++){
                    float3 cell = baseCell + float3(x2, y2, z2);
                    float3 cellPosition = cell + rand3dTo3d(cell);
                    float3 toCell = cellPosition - value;

                    float3 diffToClosestCell = abs(closestCell - cell);
                    bool isClosestCell = diffToClosestCell.x + diffToClosestCell.y + diffToClosestCell.z < 0.1;
                    if(!isClosestCell){
                    float3 toCenter = (toClosestCell + toCell) * 0.5;
                    float3 cellDifference = normalize(toCell - toClosestCell);
                    float edgeDistance = dot(toCenter, cellDifference);
                    minEdgeDistance = min(minEdgeDistance, edgeDistance);
                    }
                }
            }
        }

        float random = rand3dTo1d(closestCell);
            return float3(minDistToCell, random, minEdgeDistance);
        }

        float4 setAxisAngle (float3 axis, float rad) {
            rad = rad * 0.5;
            float s = sin(rad);
            return float4(s * axis[0], s * axis[1], s * axis[2], cos(rad));
        }

        float3 xUnitVec3 = float3(1.0, 0.0, 0.0);
        float3 yUnitVec3 = float3(0.0, 1.0, 0.0);

        float4 rotationTo (float3 a, float3 b) {
            float vecDot = dot(a, b);
            float3 tmpvec3 = float3(0, 0, 0);
            if (vecDot < -0.999999) {
            tmpvec3 = cross(xUnitVec3, a);
            if (length(tmpvec3) < 0.000001) {
                tmpvec3 = cross(yUnitVec3, a);
            }
            tmpvec3 = normalize(tmpvec3);
            return setAxisAngle(tmpvec3, UNITY_PI);
            } else if (vecDot > 0.999999) {
            return float4(0,0,0,1);
            } else {
            tmpvec3 = cross(a, b);
            float4 _out = float4(tmpvec3[0], tmpvec3[1], tmpvec3[2], 1.0 + vecDot);
            return normalize(_out);
            }
        }

        float4 multQuat(float4 q1, float4 q2) {
            return float4(
            q1.w * q2.x + q1.x * q2.w + q1.z * q2.y - q1.y * q2.z,
            q1.w * q2.y + q1.y * q2.w + q1.x * q2.z - q1.z * q2.x,
            q1.w * q2.z + q1.z * q2.w + q1.y * q2.x - q1.x * q2.y,
            q1.w * q2.w - q1.x * q2.x - q1.y * q2.y - q1.z * q2.z
            );
        }

        float3 rotateVector( float4 quat, float3 vec ) {
            // https://twistedpairdevelopment.wordpress.com/2013/02/11/rotating-a-vector-by-a-quaternion-in-glsl/
            float4 qv = multQuat( quat, float4(vec, 0.0) );
            return multQuat( qv, float4(-quat.x, -quat.y, -quat.z, quat.w) ).xyz;
        }

        float4 skybox_frag (v2f i, float4 sky, float4 horizon, float starScale, float starPow, float3 fogColor, float fogHeight, float3 moonMaskDir, float3 moonDir, float moonMaskSize, float moonSize, float starFade, float rotationSpeed)
        {
            float height = i.texcoord.y;

            // horizon
            float3 base = lerp(horizon, sky, height*0.85);

            // moon
            float3 viewDir = normalize(ObjSpaceViewDir(float4(i.texcoord, 0)));
            viewDir.y = abs(viewDir.y);

            float4 starQuat = setAxisAngle(float3(0, -1, 0), _Time.x*0.1*rotationSpeed);
            viewDir = rotateVector(starQuat, viewDir);

            float moonDist = distance(normalize(moonDir), viewDir);
            float moonMaskDist = distance(normalize(moonMaskDir), viewDir);

            fixed moonInRange = smoothstep(moonSize, moonSize + 0.0025f, moonDist);
            fixed moonMaskInRange = smoothstep(moonMaskSize, moonMaskSize + 0.0025f, moonMaskDist);
            fixed doDraw = height < 0 ? 0 : clamp(moonInRange - moonMaskInRange, 0, 1);

            base = lerp(base, float3(0.7, 0.7, 0.8), doDraw);

            // stars
            if (height < 0 || moonMaskDist > moonMaskSize*1.3) {
                float v = pow(1 - saturate(voronoiNoise(i.texcoord*starScale).r), starPow);
                base += lerp(0, v, step(0.04f, v)); // v < 0.04f ? 0 : v;
            }

            // fog
            float starFog = pow(saturate(1 - (abs(height+0.04)-0.4) - starFade), 4);
            base = lerp(base, horizon, starFog);

            float fog = pow(saturate(1 - (abs(height+0.04)+0.1)), fogHeight);
            base = lerp(base, fogColor, fog);

            float3 c = base * _Exposure;
            return float4(c, 1);
        }
        ENDCG

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            float4 _Sky, _Horizon, _Fog, _MoonDir, _MoonMaskDir;
            float _StarScale, _StarPow, _FogHeight, _MoonSize, _MoonMaskSize, _StarFade, _RotationSpeed;
            v2f vert(appdata_t v) { return skybox_vert(v, _RotationSpeed); }
            float4 frag (v2f i) : SV_Target { return skybox_frag(i,_Sky,_Horizon,_StarScale,_StarPow,_Fog,_FogHeight,_MoonDir,_MoonMaskDir,_MoonSize,_MoonMaskSize,_StarFade,_RotationSpeed); }
            ENDCG
        }
    }
}
