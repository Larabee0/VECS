#version 460
#extension GL_ARB_shading_language_include : require
#extension GL_EXT_nonuniform_qualifier : require
#include "../common_structures.glsl"
#include "../lighting.glsl"
#include "../shadows.glsl"

layout (location = 0) in vec2 inUV;
layout (location = 0) out vec4 outColour;

layout(set = 0, binding = 0) uniform LightingInfo {
	int numDirLights;
	int numDirLightsShadows;
	int numPointLights;
	int numPointLightShadows;
	int numSpotLights;
	int numSpotLightShadows;
} lighting;

layout(set = 0, binding = 1) readonly buffer DirectionalLights {
	DirectionalLight values[];
} directionalLightBuffer;

layout (set = 0, binding = 2) readonly buffer PointLights {
	PointLight values[];
} pointLightBuffer;

layout (set = 0, binding = 3) readonly buffer SpotLights {
	SpotLight values[];
} spotLightBuffer;

layout(set = 0,binding = 4) readonly buffer CameraInfos {
	CameraInfo values[];
} cameraInfo;

layout(set = 0, binding = 5) readonly buffer CameraInverses {
	CameraInverse values[];
} cameraInverse;

layout (set = 1, binding = 0) uniform sampler2DArray dirShadow;
layout (set = 1, binding = 1) uniform sampler2DArray[] plShadow;
layout (set = 1, binding = 2) uniform sampler2D[] slShadow;

layout (set = 1, binding = 3) uniform samplerCube samplerIrradiance;
layout (set = 1, binding = 4) uniform sampler2D samplerBRDFLUT;
layout (set = 1, binding = 5) uniform samplerCube prefilteredMap;

layout (set = 2, binding = 0) uniform sampler2D g_PositionIn;
layout (set = 2, binding = 1) uniform sampler2D g_NormalsIn;
layout (set = 2, binding = 2) uniform sampler2D g_AlbedoIn;
layout (set = 2, binding = 3) uniform sampler2D g_MaskIn;

layout(push_constant) uniform Constants{
	uint cameraIndex;
} constants;

#define PI 3.1415926535897932384626433832795
#define ALBEDO pow(texture(g_AlbedoIn, UV).rgb, vec3(2.2))
#define UV vec2(inUV.x,1-inUV.y)

// Normal Distribution function --------------------------------------
float D_GGX(float dotNH, float roughness) {
	float alpha = roughness * roughness;
	float alpha2 = alpha * alpha;
	float denom = dotNH * dotNH * (alpha2 - 1.0) + 1.0;
	return (alpha2)/(PI * denom*denom); 
}

// Geometric Shadowing function --------------------------------------
float G_SchlicksmithGGX(float dotNL, float dotNV, float roughness) {
	float r = (roughness + 1.0);
	float k = (r*r) / 8.0;
	float GL = dotNL / (dotNL * (1.0 - k) + k);
	float GV = dotNV / (dotNV * (1.0 - k) + k);
	return GL * GV;
}

// Fresnel function ----------------------------------------------------
vec3 F_Schlick(float cosTheta, vec3 F0) {
	return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

vec3 F_SchlickR(float cosTheta, vec3 F0, float roughness) {
	return F0 + (max(vec3(1.0 - roughness), F0) - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

vec3 prefilteredReflection(vec3 R, float roughness) {
	const float MAX_REFLECTION_LOD = 9.0; // todo: param/const
	float lod = roughness * MAX_REFLECTION_LOD;
	float lodf = floor(lod);
	float lodc = ceil(lod);
	vec3 a = textureLod(prefilteredMap, R, lodf).rgb;
	vec3 b = textureLod(prefilteredMap, R, lodc).rgb;
	return mix(a, b, lod - lodf);
}

vec3 specularContribution(vec3 L, vec3 V, vec3 N, vec3 F0, float metallic, float roughness, vec3 lightColor) {
	// Precalculate vectors and dot products	
	vec3 H = normalize (V + L);
	float dotNH = clamp(dot(N, H), 0.0, 1.0);
	float dotNV = clamp(dot(N, V), 0.0, 1.0);
	float dotNL = clamp(dot(N, L), 0.0, 1.0);


	vec3 color = vec3(0.0);

	if (dotNL > 0.0) {
		// D = Normal distribution (Distribution of the microfacets)
		float D = D_GGX(dotNH, roughness); 
		// G = Geometric shadowing term (Microfacets shadowing)
		float G = G_SchlicksmithGGX(dotNL, dotNV, roughness);
		// F = Fresnel factor (Reflectance depending on angle of incidence)
		vec3 F = F_Schlick(dotNV, F0);		
		vec3 spec = D * F * G / (4.0 * dotNL * dotNV + 0.001);		
		vec3 kD = (vec3(1.0) - F) * (1.0 - metallic);			
		color += (kD * ALBEDO / PI + spec) * dotNL;
	}

	return color *lightColor;
}

void main(){
    
    vec4 positionWorld = texture(g_PositionIn,UV).rgba;
    vec3 N = vec3(texture(g_NormalsIn,UV).rg, positionWorld.w);
    //N.b = sqrt(1 - N.x * N.x - N.y * N.y);
    vec4 maskValue = texture(g_MaskIn,UV).rgba;
    
    
	vec3 cameraPosWorld = cameraInverse.values[constants.cameraIndex].inverseViewMatrix[3].xyz;
    
    vec3 viewPos = (cameraInfo.values[constants.cameraIndex].viewMatrix *  vec4(positionWorld.xyz,1.0)).xyz;
    
	vec3 V = normalize(cameraPosWorld - positionWorld.xyz);
	vec3 R = reflect(-V, N);
    
	float metallic = maskValue.r;
	float roughness = 1- maskValue.a;
    vec3 ambient = maskValue.ggg;
    
	vec3 F0 = vec3(0.04); 
	F0 = mix(F0, ALBEDO, metallic);
    
	vec3 Lo = vec3(0);
	int cascadeIndex = 0;
	float shadow= 1.0;

    for(int i = 0; i < lighting.numDirLights; i++) {
		DirectionalLight directionalLight = directionalLightBuffer.values[i];
		
		shadow = i < lighting.numDirLightsShadows ? DirShadows(
			dirShadow,
			directionalLight,
			positionWorld.xyz,
			viewPos,
			cascadeIndex) : 1.0;
		Lo += specularContribution(-directionalLight.direction.xyz, V, N, F0, metallic, roughness, directionalLight.specular.rgb)*shadow;
	}
    
    for(int i = 0; i < lighting.numPointLights; i++) {
		PointLight pl = pointLightBuffer.values[i];
		
    	float distance = length(pl.position.xyz - positionWorld.xyz);

		if(distance <= pl.farPlane){
			shadow= i < lighting.numPointLightShadows ? ShadowPlCalculationAlt(plShadow[i], positionWorld.xyz, cameraPosWorld, pl) : 1.0;
	    	vec3 L = normalize(pl.position.xyz - positionWorld.xyz);
	    	Lo += specularContribution(L, V, N, F0, metallic, roughness, pl.specular.rgb) * shadow;
			
		}

    }
    
    for(int i = 0; i < lighting.numSpotLights; i++) {
		SpotLight sl = spotLightBuffer.values[i];
		
    	float distance = length(sl.position.xyz - positionWorld.xyz);
		if(distance <= sl.farPlane) {

			float slShadow = i < lighting.numSpotLightShadows ? ShadowSlCalculationAlt(slShadow[i], positionWorld.xyz, cameraPosWorld, sl) : 1.0;			
	    	vec3 L = normalize(sl.position.xyz - positionWorld.xyz);
	    	Lo += specularContribution(L, V, N, F0, metallic, roughness, sl.specular.rgb * CalcSpotLightIntensity(sl, positionWorld.xyz)) * shadow;
		}
    }

	vec2 brdf = texture(samplerBRDFLUT, vec2(max(dot(N, V), 0.0), roughness)).rg;
	vec3 reflection = prefilteredReflection(R, roughness).rgb ;	
	vec3 irradiance = texture(samplerIrradiance, N).rgb;
    
	// Diffuse based on irradiance
	vec3 diffuse = irradiance * ALBEDO;
    
	vec3 F = F_SchlickR(max(dot(N, V), 0.0), F0, roughness);
    
	// Specular reflectance
	vec3 specular = reflection * (F * brdf.x + brdf.y);
    
	// Ambient part
	vec3 kD = 1.0 - F;
	kD *= 1.0 - metallic;	  
	ambient *= (kD * (diffuse) + specular);

	vec3 color = (ambient + (Lo));
    
	// Tone mapping
	//color = Uncharted2Tonemap(color * pbrProps.exposure);
    
	//color = color * (1.0 / Uncharted2Tonemap(vec3(11.2)));

	// Gamma correction
	//color = pow(color, vec3(1.0 / pbrProps.gamma));
    
	outColour = vec4(color, 1.0);
	//outColour = vec4(ALBEDO,1.0);
}