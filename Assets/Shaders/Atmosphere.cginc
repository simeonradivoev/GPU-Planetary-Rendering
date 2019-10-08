
//
//  Precomputed Atmospheric Scattering
//  Copyright (c) 2008 INRIA
//  All rights reserved.
// 
//  Redistribution and use in source and binary forms, with or without
//  modification, are permitted provided that the following conditions
//  are met:
//  1. Redistributions of source code must retain the above copyright
//     notice, this list of conditions and the following disclaimer.
//  2. Redistributions in binary form must reproduce the above copyright
//     notice, this list of conditions and the following disclaimer in the
//     documentation and/or other materials provided with the distribution.
//  3. Neither the name of the copyright holders nor the names of its
//     contributors may be used to endorse or promote products derived from
//     this software without specific prior written permission.
// 
//  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
//  AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
//  IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
//  ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
//  LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
//  CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
//  SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
//  INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
//  CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
//  ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF
//  THE POSSIBILITY OF SUCH DAMAGE.
// 
//  Author: Eric Bruneton
//  Modified and ported to Unity by Justin Hawkins 2013
//
uniform sampler2D _Transmittance,_Irradiance;
uniform sampler3D _Inscatter;

uniform float M_PI;
uniform float3 EARTH_POS;
uniform float3 CAMERA_POS;
uniform float SCALE;
uniform float Rg;
uniform float Rt;
uniform float RL;
uniform float RES_R;
uniform float RES_MU;
uniform float RES_MU_S;
uniform float RES_NU;
uniform float3 SUN_DIR;
uniform float SUN_INTENSITY;
uniform float3 betaR;
uniform float mieG;
uniform float HR;
uniform float HM;
//uniform float3 betaMSca;
uniform float3 betaMEx;


static const float EPSILON_ATMOSPHERE = 0.002; 
static const float EPSILON_INSCATTER = 0.015;

float3 hdr(float3 L) 
{
    L = L * 0.4;
    L.r = L.r < 1.413 ? pow(L.r * 0.38317, 1.0 / 2.2) : 1.0 - exp(-L.r);
    L.g = L.g < 1.413 ? pow(L.g * 0.38317, 1.0 / 2.2) : 1.0 - exp(-L.g);
    L.b = L.b < 1.413 ? pow(L.b * 0.38317, 1.0 / 2.2) : 1.0 - exp(-L.b);
    return L;
}
 
float4 Texture4D(sampler3D table, float r, float mu, float muS, float nu)
{
   	float H = sqrt(Rt * Rt - Rg * Rg);
   	float rho = sqrt(r * r - Rg * Rg);

    float rmu = r * mu;
    float delta = rmu * rmu - r * r + Rg * Rg;
    float4 cst = rmu < 0.0 && delta > 0.0 ? float4(1.0, 0.0, 0.0, 0.5 - 0.5 / RES_MU) : float4(-1.0, H * H, H, 0.5 + 0.5 / RES_MU);
    float uR = 0.5 / RES_R + rho / H * (1.0 - 1.0 / RES_R);
    float uMu = cst.w + (rmu * cst.x + sqrt(delta + cst.y)) / (rho + cst.z) * (0.5 - 1.0 / float(RES_MU));
    // paper formula
    //float uMuS = 0.5 / RES_MU_S + max((1.0 - exp(-3.0 * muS - 0.6)) / (1.0 - exp(-3.6)), 0.0) * (1.0 - 1.0 / RES_MU_S);
    // better formula
    float uMuS = 0.5 / RES_MU_S + (atan(max(muS, -0.1975) * tan(1.26 * 1.1)) / 1.1 + (1.0 - 0.26)) * 0.5 * (1.0 - 1.0 / RES_MU_S);

    float lep = (nu + 1.0) / 2.0 * (RES_NU - 1.0);
    float uNu = floor(lep);
    lep = lep - uNu;

    return tex3D(table, float3((uNu + uMuS) / RES_NU, uMu, uR)) * (1.0 - lep) + tex3D(table, float3((uNu + uMuS + 1.0) / RES_NU, uMu, uR)) * lep;
}

float3 Irradiance(float r, float muS) 
{
	float uR = (r - Rg) / (Rt - Rg);
   	float uMuS = (muS + 0.2) / (1.0 + 0.2);

    return tex2D(_Irradiance, float2(uMuS, uR)).rgb;
}

float3 GetMie(float4 rayMie) 
{	
	// approximated single Mie scattering (cf. approximate Cm in paragraph "Angular precision")
	// rayMie.rgb=C*, rayMie.w=Cm,r
   	return rayMie.rgb * rayMie.w / max(rayMie.r, 1e-4) * (betaR.r / betaR);
}

float PhaseFunctionR(float mu) 
{
	// Rayleigh phase function
    return (3.0 / (16.0 * M_PI)) * (1.0 + mu * mu);
}

float PhaseFunctionM(float mu) 
{
	// Mie phase function
   	 return 1.5 * 1.0 / (4.0 * M_PI) * (1.0 - mieG*mieG) * pow(1.0 + (mieG*mieG) - 2.0*mieG*mu, -3.0/2.0) * (1.0 + mu * mu) / (2.0 + mieG*mieG);
}

float3 Transmittance(float r, float mu) 
{
	// transmittance(=transparency) of atmosphere for infinite ray (r,mu)
	// (mu=cos(view zenith angle)), intersections with ground ignored
   	float uR, uMu;
    uR = sqrt((r - Rg) / (Rt - Rg));
    uMu = atan((mu + 0.15) / (1.0 + 0.15) * tan(1.5)) / 1.5;
    
    return tex2D(_Transmittance, float2(uMu, uR)).rgb;
}

float3 Transmittance(float r, float mu, float d) {
    float3 result;
    float r1 = sqrt(r * r + d * d + 2.0 * r * mu * d);
    float mu1 = (r * mu + d) / r1;
    
    if (mu > 0.0) {
        result = min(Transmittance(r, mu) / Transmittance(r1, mu1), 1.0);
    } else {
        result = min(Transmittance(r1, -mu1) / Transmittance(r, -mu), 1.0);
    }
    return result;
}

// transmittance(=transparency) of atmosphere for infinite ray (r,mu)
// (mu=cos(view zenith angle)), or zero if ray intersects ground
float3 transmittanceWithShadow(float r, float mu) {
    return mu < -sqrt(1.0 - (Rg / r) * (Rg / r)) ? float3(0,0,0) : Transmittance(r, mu);
}

// optical depth for ray (r,mu) of length d, using analytic formula
// (mu=cos(view zenith angle)), intersections with ground ignored
// H=height scale of exponential density function
float opticalDepth(float H, float r, float mu, float d) {
    float a = sqrt((0.5/H)*r);
    float2 a01 = a*float2(mu, mu + d / r);
    float2 a01s = sign(a01);
    float2 a01sq = a01*a01;
    float x = a01s.y > a01s.x ? exp(a01sq.x) : 0.0;
    float2 y = a01s / (2.3193*abs(a01) + sqrt(1.52*a01sq + 4.0)) * float2(1.0, exp(-d/H*(d/(2.0*r)+mu)));
    return sqrt((6.2831*H)*r) * exp((Rg-r)/H) * (x + dot(y, float2(1.0, -1.0)));
}

float3 analyticTransmittance(float r, float mu, float d) {
    return exp(-betaR * opticalDepth(HR, r, mu, d) - betaMEx * opticalDepth(HM, r, mu, d));
}

bool intersectAtmosphere(float3 dir, out float offset, out float maxPathLength) 
{ 
	 offset = 0.0f; 
	 maxPathLength = 0.0f; 
 
	 // vector from ray origin to center of the sphere 
	 float3 l = -CAMERA_POS; 
	 float l2 = dot(l,l); 
	 float s = dot(l,dir); 
	 // adjust top atmosphere boundary by small epsilon to prevent artifacts 
	 float r = Rt - EPSILON_ATMOSPHERE;
	 float r2 = r*r; 
 
	 if(l2 <= r2) 
	 { 
		 // ray origin inside sphere, hit is ensured 
		 float m2 = l2 - (s * s); 
		 float q = sqrt(r2 - m2); 
		 maxPathLength = s + q; 
 
		 return true; 
	 } 
	 else if(s >= 0) 
	 { 
		 // ray starts outside in front of sphere, hit is possible 
		 float m2 = l2 - (s * s); 
 
		 if(m2 <= r2) 
		 { 
			 // ray hits atmosphere definitely 
			 float q = sqrt(r2 - m2); 
			 offset = s - q; 
			 maxPathLength = (s + q) - offset; 
 
			 return true; 
		 } 
	 } 
 
 return false; 
}

float3 GetInscatteredLight(float3 surfacePos,float3 viewDir,inout float3 attenuation,inout float irradianceFactor) 
{ 
	float3 inscatteredLight = float3(0.0f, 0.0f, 0.0f);
	float4 inscatterSurface;
 	
	 float offset;
	 float maxPathLength; 
	 
	 if(intersectAtmosphere(viewDir, offset, maxPathLength)) 
	 { 
		 float pathLength = distance(CAMERA_POS, surfacePos); 
		// check if object occludes atmosphere
		
		if(pathLength > offset) 
		{ 
			 float cameraHeight = length(CAMERA_POS);
			 float muOriginal = dot(normalize(CAMERA_POS), viewDir);
			 float originalPathLength = pathLength;

			 // offsetting camera 
			 float3 startPos = CAMERA_POS + offset * viewDir; 
			 float startPosHeight = length(startPos); 
			 pathLength -= offset; 
 
			 // starting position of path is now ensured to be inside atmosphere 
			 // was either originally there or has been moved to top boundary 
			 float muStartPos = dot(startPos,viewDir) / startPosHeight;
			 float nuStartPos = dot(viewDir, SUN_DIR); 
			 float musStartPos = dot(startPos, SUN_DIR) / startPosHeight; 
 
			 // in-scattering for infinite ray (light in-scattered when 
			 // no surface hit or object behind atmosphere) 
			 float4 inscatter = max(Texture4D(_Inscatter, startPosHeight, muStartPos, musStartPos, nuStartPos), 0.0f); 

 			 float surfacePosHeight = length(surfacePos);
			 float muEndPos = dot(surfacePos, viewDir) / surfacePosHeight;
			 float musEndPos = dot(surfacePos, SUN_DIR) / surfacePosHeight;
			 
			 // check if object hit is inside atmosphere 
			 if(pathLength < maxPathLength) 
			 { 
				 // reduce total in-scattered light when surface hit 
				 // within atmosphere 
				 // fíx described in chapter 5.1.1 
				 attenuation = Transmittance(startPosHeight, muStartPos, pathLength);
 
				 float muEndPos = dot(surfacePos, viewDir) / surfacePosHeight; 
				 inscatterSurface = Texture4D(_Inscatter, surfacePosHeight, muEndPos, musEndPos, nuStartPos);
				 inscatter = max(inscatter-attenuation.rgbr*inscatterSurface, 0.0f); 
				 //inscatter = inscatter - inscatterSurface;
				 irradianceFactor = 1.0f;
			} 
			else 
			{ 
				 // retrieve extinction factor for inifinte ray 
				 // fíx described in chapter 5.1.1 
				attenuation = min(Transmittance(startPosHeight, muStartPos), 1.0);
			}
			
			// avoids imprecision problems near horizon by interpolating between 
			 // two points above and below horizon 
			 // fíx described in chapter 5.1.2 
			 float muHorizon = -sqrt(1.0 - (Rg / startPosHeight) * (Rg / startPosHeight)); 

			 if (abs(muStartPos - muHorizon) < EPSILON_INSCATTER) 
			 { 
				 float mu = muHorizon - EPSILON_INSCATTER; 
				 float samplePosHeight = sqrt(startPosHeight*startPosHeight + pathLength*pathLength + 2.0f * startPosHeight * pathLength *mu); 
 
				 float muSamplePos = (startPosHeight * mu + pathLength) / samplePosHeight; 

				 float4 inScatter0 = Texture4D(_Inscatter, startPosHeight, mu, musStartPos, nuStartPos); 
				 float4 inScatter1 = Texture4D(_Inscatter, samplePosHeight, muSamplePos, musEndPos, nuStartPos); 
				 float4 inScatterA = max(inScatter0-attenuation.rgbr * inScatter1,0.0); 
 
				 mu = muHorizon + EPSILON_INSCATTER; 
				 samplePosHeight = sqrt(startPosHeight * startPosHeight + pathLength * pathLength + 2.0f * startPosHeight * pathLength * mu); 
				 muSamplePos = (startPosHeight * mu + pathLength) / samplePosHeight; 
 
				 inScatter0 = Texture4D(_Inscatter, startPosHeight, mu, musStartPos, nuStartPos); 
				 inScatter1 = Texture4D(_Inscatter, samplePosHeight, muSamplePos, musEndPos, nuStartPos); 
				 float4 inScatterB = max(inScatter0 - attenuation.rgbr * inScatter1, 0.0f); 
				 float t = ((muStartPos - muHorizon) + EPSILON_INSCATTER) / (2.0 * EPSILON_INSCATTER); 
 
				 inscatter = lerp(inScatterA, inScatterB, t); 
			}
				 
				 // avoids imprecision problems in Mie scattering when sun is below 
				 //horizon 
				 // fíx described in chapter 5.1.3 
				 //inscatter.w *= smoothstep(0.00, 0.02, musStartPos);
				 inscatter.w *= smoothstep(0.35, 0.7, musStartPos);
				 
				 float phaseR = PhaseFunctionR(nuStartPos); 
				 float phaseM = PhaseFunctionM(nuStartPos); 
				 inscatteredLight = max(inscatter.rgb * phaseR + GetMie(inscatter)* phaseM, 0.0f);
				 inscatteredLight *= SUN_INTENSITY;
		} 
	} 
 
	return inscatteredLight; 
}

float3 GetReflectedLight(float3 surfacePos,float depth,float3 attenuation,float irradianceFactor,float4 normalData,float3 surfaceColor) 
{ 
 // decode normal and determine intensity of refected light at 
 // surface postiion 
	if (depth > 0.0)
	{
		float3 normal = 2.0f * normalData.xyz - 1.0f;
		float lightScale = max(dot(normal, SUN_DIR), 0.0f);
		float lightIntensity = SUN_INTENSITY * normalData.w;


		// irradiance at surface position due to sky light 
		float surfacePosHeight = length(surfacePos);
		float musSurfacePos = dot(surfacePos / surfacePosHeight, SUN_DIR);

		float3 sunLight = transmittanceWithShadow(surfacePosHeight, musSurfacePos) * lightScale;
		float groundSkyLight = Irradiance(surfacePosHeight, musSurfacePos);

		float3 groundColor = surfaceColor * (max(musSurfacePos, 0)*sunLight + groundSkyLight) * lightIntensity / M_PI;

		//return attenuation;
		return attenuation * groundColor;
	}
	else
	{
		return 0;
	}
 //return attenuation * groundColor * lightIntensity * musSurfacePos;
 //return reflectedLight * attenuation * irradianceFactor * lightIntensity + irradianceSurface + reflectedLight;
}


