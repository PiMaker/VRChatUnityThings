Shader "_pi_/Fishy"
{
    Properties
    {
        [HideInInspector] _AudioLink ("AudioLink Texture", 2D) = "black" {}
    }
    SubShader
    {
        Tags {
            "Queue" = "Transparent"
            "RenderType" = "Opaque"
            "ForceNoShadowCasting" = "True"
            "IgnoreProjector" = "True"
        }

        ZWrite Off
        ZTest LEqual
        Cull Front // render on backfaces of cube to avoid disappearing when inside it

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma target 5.0
            #pragma fragmentoption ARB_precision_hint_fastest
            /* #pragma enable_d3d11_debug_symbols */

            #include "UnityCG.cginc"

            // From: https://gist.github.com/mattatz/86fff4b32d198d0928d0fa4ff32cf6fa
            #include "Matrix.cginc"
            // From: https://gist.github.com/mattatz/40a91588d5fb38240403f198a938a593
            #include "Quaternion.cginc"

            //
            // CONFIGURABLE - FEEL FREE TO CHANGE THESE!
            //
            #define FISH 8
            #define MAX_STEPS 32
            #define EPSILON 0.0022f
            #define SEED 5764
            // CONFIG END

            #include "Packages/com.llealloo.audiolink/Runtime/Shaders/AudioLink.cginc"

            /*
             * STRUCTS
             */
            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float4 clipPos : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float4 ray : TEXCOORD1;
                nointerpolation float3 origin : TEXCOORD2;
                nointerpolation float3 scale : TEXCOORD3;
                nointerpolation float time[4] : TEXCOORD4;

                UNITY_VERTEX_OUTPUT_STEREO
            };

            struct f2s {
                float depth : SV_DepthGreaterEqual;
                float4 color : SV_Target;
            };

            /*
             * STATICS
             */
            float3 get_camera_pos() {
                float3 worldCam;
                worldCam.x = unity_CameraToWorld[0][3];
                worldCam.y = unity_CameraToWorld[1][3];
                worldCam.z = unity_CameraToWorld[2][3];
                return worldCam;
            }
            // _WorldSpaceCameraPos is broken in VR (single pass stereo)
            static float3 camera_pos = get_camera_pos();
            // detects VRChat mirror cameras
            static bool isInMirror = UNITY_MATRIX_P._31 != 0 || UNITY_MATRIX_P._32 != 0;

            v2f vert(appdata v) {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.clipPos = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeNonStereoScreenPos(o.clipPos);

                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.ray.xyz = worldPos.xyz - camera_pos.xyz;
                o.ray.w = o.clipPos.w;
                o.origin = mul(unity_ObjectToWorld, float4(0, 0, 0, 1));

                float3 _p;
                float4 _r;
                decompose(unity_ObjectToWorld, _p, _r, o.scale);

                o.time[0] = _Time.y*0.22 + (AudioLinkDecodeDataAsUInt( ALPASS_CHRONOTENSITY  + uint2( 0, 0 ) ) % 1400000) / 1400000.0 * 6.28;
                o.time[1] = _Time.y*0.22 + (AudioLinkDecodeDataAsUInt( ALPASS_CHRONOTENSITY  + uint2( 0, 1 ) ) % 1800000) / 1800000.0 * 6.28;
                o.time[2] = _Time.y*0.22 + (AudioLinkDecodeDataAsUInt( ALPASS_CHRONOTENSITY  + uint2( 0, 2 ) ) % 1800000) / 1800000.0 * 6.28;
                o.time[3] = _Time.y*0.22 + (AudioLinkDecodeDataAsUInt( ALPASS_CHRONOTENSITY  + uint2( 0, 3 ) ) % 1600000) / 1600000.0 * 6.28;

                return o;
            }

            /*
             * RAYMARCHING
             * mostly based on:
             * https://www.iquilezles.org/www/articles/distfunctions/distfunctions.htm
             */
            static float time[4];

            // https://gist.github.com/wwwtyro/beecc31d65d1004f5a9d
            bool raySphereIntersect(float3 r0, float3 rd, float3 s0, float sr) {
                // - r0: ray origin
                // - rd: normalized ray direction
                // - s0: sphere center
                // - sr: sphere radius
                // - Returns distance from r0 to first intersecion with sphere,
                //   or -1.0 if no intersection.
                float a = dot(rd, rd);
                float3 s0_r0 = r0 - s0;
                float b = 2.0 * dot(rd, s0_r0);
                float c = dot(s0_r0, s0_r0) - (sr * sr);
                return b*b - 4.0*a*c < 0.0;
                /* return (-b - sqrt((b*b) - 4.0*a*c))/(2.0*a); */
            }

            float planeDir(float3 origin, float3 norm, float3 p) {
                p -= origin;
                return dot(norm, p);
            }

            float sdf_sphere(float3 pos, float radius) {
                return length(pos) - radius;
            }

            float sdf_box(float3 pos, float s)
            {
                float3 q = abs(pos) - float3(s, s, s);
                return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0);
            }

            float sdf_fish_body(float3 p, float3 r) {
                float k0 = length(p/r);
                float k1 = length(p/(r*r));
                return k0*(k0-1.0)/k1;
            }

            float ndot(in float2 a, in float2 b) { return a.x*b.x - a.y*b.y; }
            float sdf_rhombus(float3 p, float la, float lb, float h, float ra)
            {
                float2 b = float2(la*(p.x > 0 ? 0 : 1),lb);
                p = abs(p);
                float f = clamp( (ndot(b,b-2.0*p.xz))/dot(b,b), -1.0, 1.0 );
                float2 q = float2(length(p.xz-0.5*b*float2(1.0-f,1.0+f))*sign(p.x*b.y+p.z*b.x-b.x*b.y)-ra, p.y-h);
                return min(max(q.x,q.y),0.0) + length(max(q,0.0));
            }

            float sdf_tri(float3 p){
                float2 h = float2(0.1, 0.01);
                const float k = sqrt(3.0);
                h.x *= 0.5*k;
                p.xy /= h.x;
                p.x = abs(p.x) - 1.0;
                p.y = p.y + 1.0/k;
                if( p.x+k*p.y>0.0 ) p.xy=float2(p.x-k*p.y,-k*p.x-p.y)/2.0;
                p.x -= clamp( p.x, -2.0, 0.0 );
                float d1 = length(p.xy)*sign(-p.y)*h.x;
                float d2 = abs(p.z)-h.y;
                return length(max(float2(d1,d2),0.0)) + min(max(d1,d2), 0.) - 0.008;
            }

            float3 pos_bend(in float3 p, float offset, float d, uint fish)
            {
                /* p.xyz = p.xzy; // select axis */
                const float k = (sin(sin(time[fish%4])*24*(d*0.7+0.4)+d*SEED) + offset)*0.4;
                float c = cos(k*p.x);
                float s = sin(k*p.x);
                float2x2 m = float2x2(c,-s,s,c);
                float3 q = float3(mul(m,p.xy),p.z);
                return q;
            }

            float sminCubic(float a, float b, float k) {
                float h = max(k - abs(a - b), 0.0f) / k;
                return min(a, b) - h * h * h * k * (1.0f / 6.0f);
            }

            float rand(in float2 uv) {
                float2 noise = (frac(sin(dot(uv ,float2(12.9898,78.233)*2.0)) * 43758.5453));
                return abs(noise.x + noise.y) * 0.5;
            }

            float3 rotate(float3 pos, float4 rot) {
                pos = rotate_vector(pos, rot);
                return pos;
            }

            static float4 fishpos[FISH];
            static float4 fishrot[FISH];
            static uint fishes = 0;

            #define FISH_HIT 0
            #define FISH_MISS 1
            #define FISH_CENTER 2
            int sdf_fish_init(float3 center, uint fish, float3 rayDir, float3 scale) {
                float rr = rand(float2(fish * SEED + 0.1, fish * SEED + 0.2));
                float rx = (rand(float2(fish * SEED, SEED/2)) + 0.7) * 0.2;
                float ry = rand(float2(SEED/2, fish * SEED)) - 0.5;
                float rz = (rr + 0.5) * 0.14;

                float negmul = (rr > 0.5 ? -1 : 1);
                float angle = time[fish%4] * negmul + rr*SEED*0.01;
                float4 rot = rotate_angle_axis(angle, float3(0, 1, 0));
                center += rotate_vector(float3(rx * 1.15, ry * 1.15, rz) * scale, rot);
                /* float3 dir = normalize(float3(sin(angle - (negmul > 0 ? 0.5 : 0.95)), 0, */
                /*                               cos(angle - (negmul > 0 ? 0.5 : 0.95)))) * -negmul; */

                /* fishpos[fish] = float4(0, 0, 0, 0); */
                /* fishrot[fish] = float4(0, 0, 0, 0); */
                if (planeDir(camera_pos, rayDir, center) > 0 &&
                    !raySphereIntersect(camera_pos, rayDir, center, 0.145))
                {
                    fishpos[fishes] = float4(center, rr + fish);
                    fishrot[fishes] = qmul(q_inverse(rot),
                        negmul > 0 ?
                            rotate_angle_axis(UNITY_PI*0.66, float3(0, 1, 0)) :
                            rotate_angle_axis(-UNITY_PI*0.26, float3(0, 1, 0)));
                    fishes++;
                    /* return !raySphereIntersect(camera_pos, rayDir, center, 0.03) ? FISH_CENTER : FISH_HIT; */
                    return FISH_HIT;
                } else {
                    return FISH_MISS;
                }
            }

            const static float4 finrot = rotate_angle_axis(UNITY_PI*0.5, float3(1, 0, 0));
            float sdf_fish(float3 pos, uint fish) {
                //float dist = distance(pos, fishpos[fish].xyz);

                pos -= fishpos[fish].xyz;
                pos = rotate(pos, fishrot[fish]);
                /* pos = pos_bend(pos, d < 0 ? -2 : 2); */

                //if (dist > 0.15) {
                //    return max(sdf_sphere(pos, 0.145), EPSILON*1.2);
                //}

                float d = (frac(fishpos[fish].w) - 0.5);

                const float fish_scale = 0.34 + d*0.12;
                pos /= fish_scale;

                float r_eye1 = sdf_sphere(pos + float3(0.115, -0.02, 0.026), 0.008 + d*0.005);
                float r_eye2 = sdf_sphere(pos + float3(0.115, -0.02, -0.026), 0.008 + d*0.005);
                float r_mouth = sdf_sphere(pos + float3(0.141, 0.025 + d*0.04, 0), 0.0175*(abs(sin(_Time.y*(0.5+d*0.5)))+0.5));
                float r_body = sdf_fish_body(pos, float3(0.144+d*0.025, 0.1+d*0.02, 0.038 + d*0.01));
                float r_top_fin = sdf_fish_body(pos + float3(-0.041, 0, 0), float3(0.108, 0.11, 0.020));
                float r_back_fin = sdf_rhombus(pos_bend(rotate(pos, finrot), d < 0 ? 0.75*d*0.1 : -0.75*d*0.1, d, (uint)(fishpos[fish].w)) + float3(-0.31, 0, 0), 0.18+d*0.1, 0.1+d*0.08, 0.008, 0.02);
                /* float r_back_fin_smooth = sdf_sphere(pos + float3(-0.718, 0, 0), 0.41); */

                return fish_scale * sminCubic(
                    /* max(-r_back_fin_smooth, r_back_fin), */
                    r_back_fin,
                    min(max(-r_mouth, min(r_body, r_top_fin)), min(r_eye1, r_eye2)),
                    0.04);
            }

            // this is the main sdf entry point, i.e. this defines the scene
            float2 sdf(float3 pos) {
                float res = 999999;
                float tag = -1;
                float tagDist = 999999;

                [loop]
                for (uint j = 0; j < FISH; j++) {
                    if (!fishpos[j].w) {
                        break;
                    }
                    float ires = sdf_fish(pos, j);
                    res = min(ires, res);
                    if (ires < tagDist) {
                        tag = j;
                        tagDist = ires;
                    }
                }

                return float2(res, tag);
            }

            // do an actual raymarch until we hit something or miss entirely,
            // this is where most performance is used
            float3 raymarch(float3 start, float3 dir, float farPlane, float screenDist) {
                float3 cur = start;
                float dist = _ProjectionParams.y;
                float lastDist = 999999;
                float travelled = 0;

                // "foveated" rendering:
                // increase EPSILON as we get closer to the edge of the screen
                float epsMod = saturate((1 - screenDist) + 0.12f);
                float eps = EPSILON / epsMod;

                [loop]
                for (uint j; j < MAX_STEPS; j++) {
                    cur += dir * dist;
                    travelled += dist;

                    float2 res = sdf(cur);
                    dist = res.x;

                    if (dist < EPSILON) {
                        // hit, return distance to hit and 'tag':
                        // res.y (tag) defines which object instance we hit, approximately anyway
                        return float3(0, travelled, res.y);

                    } else if (travelled > farPlane ||
                               (dist > 0.5f && dist > lastDist * 1.95)) // *
                    {
                        // miss
                        return float3(1, 0, 0);
                    }

                    // * this optimization is not mathematically sound for non-convex shapes,
                    //   it would only be if the second parameter where 2.0f, but alas, it
                    //   produces very minimal artifacts and increases performance ~2-fold

                    lastDist = dist;
                }

                // overrun, miss
                return float3(2, 0, 0);
            }

            // normal estimation, 4 extra sdf calls per hit - gives us nicer 'shading'
            float3 estimateNormal(float3 pos, uint fish) {
                float3 n = float3(0,0,0);
                for( uint i=0; i<4; i++ )
                {
                    float3 e = 0.5773*(2.0*float3((((i+3)>>1)&1),((i>>1)&1),(i&1))-1.0);
                    n += e*sdf_fish(pos+0.0005*e,fish).x;
                }
                return normalize(n);
            }

            /*
             * HELPERS
             */
            float3 hue_to_rgb(float H)
            {
                // inverted colors in mirror, because we can
                if (isInMirror) {
                    H = 1 - H;
                }
                float R = abs(H * 6 - 3) - 1;
                float G = 2 - abs(H * 6 - 2);
                float B = 2 - abs(H * 6 - 4);
                return saturate(float3(R,G,B));
            }

            /*
             * FRAGMENT
             */
            f2s frag(v2f i)
            {
                f2s ret;
                ret.color = ret.depth = 1;

                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // calculate view ray from interpolated vertex ray
                float3 rayDir = normalize((i.ray.xyz / i.ray.w).xyz);
                camera_pos += rayDir * 0.035;

                const float far_plane = 8;
                float screenDist = distance(i.screenPos.xy/i.screenPos.w, float2(0.5f, 0.5f));

                // calculate fish data
                time = i.time;
                bool a = false;
                [loop]
                for (uint j = 0; j < FISH; j++) {
                    int res = sdf_fish_init(i.origin, j, rayDir, i.scale);
                    if (res != FISH_MISS) {
                        a = true;
                    }
                    /* if (res == FISH_CENTER) { */
                    /*     break; */
                    /* } */
                }

                if(!a) {
                    clip(-1);
                    return ret;
                }

                // now do the actual SDF raymarch, aka. the cool part
                float3 raymarchResult = raymarch(camera_pos, rayDir, far_plane, screenDist);

                // miss?
                if (raymarchResult.x) {
                    discard;
                }

                // calculate world-space position of our hit result
                float dist = raymarchResult.y;
                float3 hitPos = camera_pos + rayDir * dist;

                // estimate normals
                float3 normal = estimateNormal(hitPos, (uint)raymarchResult.z);
                float angle = acos(dot(normal, rayDir));

                // calculate shading/color based on _Time and object normals
                float edgeLightAdd = 1 - saturate(angle/PI + 0.25f);
                uint hitfish = (uint)(raymarchResult.z);
                float3 rgb = hue_to_rgb(frac(sin(i.time[((uint)fishpos[hitfish].w) % 4]*0.5) + fishpos[hitfish].w*SEED*0.04));
                ret.color = float4(rgb*0.8, 1);
                ret.color.rgb += edgeLightAdd;

                // and finally, calculate the depth of our hit in clip-space, to make
                // object intersection work
                float4 depthPos = mul(UNITY_MATRIX_VP, float4(hitPos, 1));
                ret.depth = depthPos.z / depthPos.w;

                return ret;
            }
            ENDCG
        }
    }
}
