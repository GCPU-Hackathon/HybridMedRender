Shader "Custom/VolumeDVR"
{
    Properties
    {
        _VolumeTex   ("VolumeTex", 3D) = "" {}
        _TFTex       ("Transfer LUT", 2D) = "" {}

        _StepSize    ("Step Size", Range(0.0005, 0.02)) = 0.003
        _Brightness  ("Brightness", Range(0,5)) = 1.5
        _AlphaScale  ("Alpha Scale", Range(0,2)) = 0.5

        _LightDir        ("Light Dir (world)", Vector) = (0,0,1,0)
        _LightIntensity  ("Light Intensity", Range(0,5)) = 1.0
        _Ambient         ("Ambient", Range(0,1)) = 0.2

        _IsLabelMap ("Is LabelMap (1/0)", Int) = 1
        _P1         ("P1 (cont)", Float) = 0.0
        _P99        ("P99 (cont)", Float) = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Front
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // === Uniforms ===
            sampler3D _VolumeTex;   // volume 3D RFloat
            sampler2D _TFTex;       // LUT 1D (256x1 RGBAFloat)

            float _StepSize;
            float _Brightness;
            float _AlphaScale;

            float4 _LightDir;
            float _LightIntensity;
            float _Ambient;

            int _IsLabelMap;
            float _P1;
            float _P99;

            // === Structs ===
            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            // === Vertex shader ===
            v2f vert (appdata v)
            {
                v2f o;
                o.pos      = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            // === Utils ===

            // Intersection rayon / box en espace objet (cube [-0.5,+0.5]^3)
            bool RayBoxIntersect(float3 rayOrigin, float3 rayDir, out float tEnter, out float tExit)
            {
                float3 boxMin = float3(-0.5, -0.5, -0.5);
                float3 boxMax = float3( 0.5,  0.5,  0.5);

                float3 t0 = (boxMin - rayOrigin) / rayDir;
                float3 t1 = (boxMax - rayOrigin) / rayDir;

                float3 tmin = min(t0, t1);
                float3 tmax = max(t0, t1);

                tEnter = max(max(tmin.x, tmin.y), tmin.z);
                tExit  = min(min(tmax.x, tmax.y), tmax.z);

                return (tExit > max(tEnter, 0.0));
            }

            // Estimation du gradient local => pseudo-normale pour l'éclairage
            float3 EstimateNormal(float3 pObj)
            {
                // eps en espace objet (cube ~1 unité de côté)
                float eps = 0.002;

                float3 uvw_xp = (pObj + float3(+eps,0,0)) + 0.5;
                float3 uvw_xm = (pObj + float3(-eps,0,0)) + 0.5;
                float3 uvw_yp = (pObj + float3(0,+eps,0)) + 0.5;
                float3 uvw_ym = (pObj + float3(0,-eps,0)) + 0.5;
                float3 uvw_zp = (pObj + float3(0,0,+eps)) + 0.5;
                float3 uvw_zm = (pObj + float3(0,0,-eps)) + 0.5;

                float vxp = tex3D(_VolumeTex, uvw_xp).r;
                float vxm = tex3D(_VolumeTex, uvw_xm).r;
                float vyp = tex3D(_VolumeTex, uvw_yp).r;
                float vym = tex3D(_VolumeTex, uvw_ym).r;
                float vzp = tex3D(_VolumeTex, uvw_zp).r;
                float vzm = tex3D(_VolumeTex, uvw_zm).r;

                float3 grad = float3(vxp - vxm, vyp - vym, vzp - vzm);

                float len2 = max(dot(grad, grad), 1e-6);
                return grad / sqrt(len2);
            }

            // Récupère la couleur+alpha pour la valeur du voxel selon la TF
            // _IsLabelMap = 1 : 'val' est un label discret (0,1,2,4,..)
            // _IsLabelMap = 0 : 'val' est une intensité continue (IRM/CT)
            float4 SampleTF(float val)
            {
                if (_IsLabelMap == 1)
                {
                    // segmentation : val ≈ classe
                    float idx = round(val);         // 0..255 possible
                    idx = clamp(idx, 0.0, 255.0);
                    float u = idx / 255.0;
                    float4 tf = tex2D(_TFTex, float2(u, 0.5));
                    return tf; // rgb, a
                }
                else
                {
                    // intensité continue : renormaliser avec P1..P99 en [0,1]
                    float normVal = (val - _P1) / max((_P99 - _P1), 1e-6);
                    normVal = saturate(normVal);
                    float u = normVal;
                    float4 tf = tex2D(_TFTex, float2(u, 0.5));
                    return tf;
                }
            }

            // === Fragment shader (raymarch DVR) ===
            fixed4 frag (v2f i) : SV_Target
            {
                // reconstruire le rayon caméra -> fragment en ESPACE OBJET du cube
                float3 camPosObj  = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos,1)).xyz;
                float3 fragPosObj = mul(unity_WorldToObject, float4(i.worldPos,1)).xyz;
                float3 rayDirObj  = normalize(fragPosObj - camPosObj);

                // intersection avec le volume
                float tEnter, tExit;
                if (!RayBoxIntersect(camPosObj, rayDirObj, tEnter, tExit))
                    return float4(0,0,0,0);

                float t = max(tEnter, 0.0);

                // accumulation front-to-back
                float3 accRGB = float3(0,0,0);
                float  accA   = 0.0;

                // direction de lumière en espace objet (projette la dir monde)
                float3 Lobj = mul((float3x3)unity_WorldToObject, normalize(_LightDir.xyz));

                // boucle de raymarch
                const int MAX_STEPS = 2048;
                [loop]
                for (int step = 0; step < MAX_STEPS; step++)
                {
                    if (t > tExit) break;
                    if (accA >= 0.9999) break; // laisse passer énormément de couches

                    // position courante dans l'espace objet
                    float3 pObj = camPosObj + rayDirObj * t;

                    // coordonnées [0,1]^3 pour sampler la texture 3D
                    float3 uvw = pObj + 0.5;
                    if (any(uvw < 0.0) || any(uvw > 1.0))
                        break;

                    // valeur brute du voxel
                    float voxelVal = tex3D(_VolumeTex, uvw).r;

                    // couleur / alpha depuis la TF
                    float4 tf = SampleTF(voxelVal);

                    // on adoucit l'opacité locale via _AlphaScale
                    float aSample = tf.a * _AlphaScale;

                    if (aSample > 0.001)
                    {
                        // shading local basé sur le gradient du volume
                        float3 N = EstimateNormal(pObj);
                        float lambert = saturate(dot(normalize(N), normalize(Lobj)));
                        float lighting = _Ambient + _LightIntensity * lambert;

                        float3 litColor = tf.rgb * lighting;

                        // intégration front-to-back
                        float remain = 1.0 - accA;
                        accRGB += remain * litColor * aSample;
                        accA   += remain * aSample;
                    }

                    // avancer le long du rayon
                    t += _StepSize;
                }

                // booster un peu la luminosité finale
                accRGB *= _Brightness;

                return float4(accRGB, accA);
            }

            ENDHLSL
        }
    }
}
