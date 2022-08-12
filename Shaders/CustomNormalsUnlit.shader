Shader "CustomNormalsUnlit" {
    Properties {
        _Color ("Main Color", Color) = (1,1,1,1)
    }
 
    SubShader {
        Tags { "RenderType"="Opaque" }

        CGPROGRAM
       
        #pragma surface surf NoLighting noambient

        float4 _Color;
        float _NormalPower;
 
        struct Input {
            float3 viewDir;
            float3 worldPos;
            float4 screenPos;
        };

        fixed4 LightingNoLighting(SurfaceOutput s, fixed3 lightDir, fixed atten) {
            return fixed4(s.Albedo, s.Alpha);
        }

        void surf (Input i, inout SurfaceOutput o) {
            half dotp = dot(normalize(i.viewDir), o.Normal);
            half rim = 1 - saturate(dotp);

            o.Emission = _Color.rgb * rim;
        }
        ENDCG
    }
    //FallBack "Diffuse"
}