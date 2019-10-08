Shader "LostInMind/Planet/Ground/Bumped Specular" {
    Properties {
	 _Tess ("Tessellation", Range(1,32)) = 4
      _MainTex ("Texture", 2D) = "white" {}
	  _TerrainHeight("Terrain Height",Float) = 128
	  _TerrainBump("Terrain Bump",Float) = 2
	  _TerrainBumpSample("Terrain Bump Sample",Float) = 1
	  _Frequency("Frequency",Float) = 0.01
	}
		SubShader{
		  Tags { "RenderType" = "Opaque" }
		  CGPROGRAM
		  #pragma surface surf Standard fullforwardshadows vertex:vert
	  #pragma target 3.0
		#include "ImprovedPerlinNoise3D.cginc"

	  struct SurfaceOutputCustom {
		fixed3 Albedo;
		fixed3 Normal;
		fixed3 Emission;
		half Specular;
		fixed Gloss;
		fixed Alpha;
		fixed3 LightDir;
	   };

	  float _TerrainHeight;
	  float3 _LightDir;
	  float _TerrainBump;
	  float _TerrainBumpSample;
	  float _PlanetRadius;

      struct Input {
          float2 uv_MainTex;
		  float3 Pos;
		  float4 Tangent;
      };

	  float Noise(float3 P,int o)
	  {
			return 1 - turbulence(P,o);
	  }

	  float3 GetNormal(float3 N,float3 P)
		{
			float e = _TerrainBumpSample;
			int o = 8;
			float F = Noise(float3(P.x,P.y,P.z),o) * _TerrainBump;
			float Fx = Noise(float3(P.x+e,P.y,P.z),o)* _TerrainBump;
			float Fy = Noise(float3(P.x,P.y+e,P.z),o)* _TerrainBump;
			float Fz = Noise(float3(P.x,P.y,P.z+e),o)* _TerrainBump;

			float3 dF = float3((Fx-F)/e, (Fy-F)/e, (Fz-F)/e);

			return normalize(N-dF);
		}

	  void vert (inout appdata_full v,out Input o)
	  {
			UNITY_INITIALIZE_OUTPUT(Input,o);
			v.normal = normalize(mul(unity_ObjectToWorld, v.vertex));
			o.Pos = normalize(mul(unity_ObjectToWorld, v.vertex)).xyz * _PlanetRadius;
			o.Tangent = v.tangent;
	  }

      sampler2D _MainTex;

      void surf (Input IN, inout SurfaceOutputStandard o) {
		  //o.LightDir = normalize(_WorldSpaceLightPos0).xyz;
		  float4 c = tex2D(_MainTex, IN.uv_MainTex);
          o.Albedo = c.rgb;
		  float3 n = GetNormal(normalize(IN.Pos),IN.Pos);
		  float3 binormal = cross(normalize(IN.Pos), IN.Tangent) * IN.Tangent.w;
		  float3 normalW = (IN.Tangent * n.x) + (binormal * n.y) + (n * n.z);
		  o.Normal = normalW;
		  o.Alpha = c.a;
      }
      ENDCG
    }
    Fallback "Diffuse"
  }