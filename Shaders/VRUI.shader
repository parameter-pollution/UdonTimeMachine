Shader "VRUI/UnlitDoubleSided" {
    Properties {
        _Color ("Main Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
    }
 
    SubShader {
        Tags { "Queue" = "Transparent" "RenderType"="Transparent" }
        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha
        
        Cull Off

        CGPROGRAM
       
        //#pragma surface surf Lambert alpha:fade
        #pragma surface surf NoLighting noambient alpha:fade

        struct Input {
          float2 uv_MainTex;
        };
        float4 _Color;
        sampler2D _MainTex;

        fixed4 LightingNoLighting(SurfaceOutput s, fixed3 lightDir, fixed atten) {
            return fixed4(s.Albedo, s.Alpha);
        }

        void surf (Input IN, inout SurfaceOutput o) {
          float4 color = tex2D (_MainTex, IN.uv_MainTex) * _Color;
          o.Emission = color.rgb;
          o.Alpha = color.a;
      }
        ENDCG
    }
    //FallBack "Diffuse"
}