#version 460
#extension GL_ARB_shading_language_include : require
#include "../common_structures.glsl"
#include "../lighting.glsl"
#include "../shadows.glsl"

layout (location = 0) in vec3 fragPosWorld;
layout (location = 1) in vec3 fragNormalWorld;
layout (location = 2) in vec2 fragUV;
layout (location = 3) in vec3 fragViewPos;
layout (location = 4) in mat3 TBN;
layout (location = 7) in vec4 fragTangentWorld;
layout (location = 8) in vec3 fragNormalAlt;

layout (location = 0) out vec4 outColour;

layout(set = 0, binding = 0) uniform LightingInfo {
	DirectionalLight directionalLight;
	int numPointLights;
	int numSpotLights;
} lighting;

layout (set = 0, binding = 1) readonly buffer PointLights {
	PointLight values[];
} pointLightBuffer;

layout (set = 0, binding = 2) readonly buffer SpotLights {
	SpotLight values[];
} spotLightBuffer;

layout(set = 0,binding = 3) readonly buffer CameraInfos {
	CameraInfo values[];
} cameraInfo;

layout(set = 0,binding = 4) readonly buffer CameraInverses {
	CameraInverse values[];
} cameraInverse;

layout (set = 1, binding = 2) uniform sampler2DArray dirShadow;
layout (set = 1, binding = 3) uniform samplerCube[] plShadow;
layout (set = 1, binding = 4) uniform sampler2DArray slShadow;

layout (set = 1, binding = 5) uniform samplerCube samplerIrradiance;
layout (set = 1, binding = 6) uniform sampler2D samplerBRDFLUT;
layout (set = 1, binding = 7) uniform samplerCube prefilteredMap;

layout(set = 2, binding = 0) uniform TexPorps {
	vec4 colour;
	float tiling;
    float exposure;
    float gamma;
} texProps;

layout (set = 2, binding = 1) uniform sampler2D albedoMap;
layout (set = 2, binding = 2) uniform sampler2D normalMap;
layout (set = 2, binding = 3) uniform sampler2D aoMap;
layout (set = 2, binding = 4) uniform sampler2D metallicMap;
layout (set = 2, binding = 5) uniform sampler2D smoothnessMap;
layout (set = 2, binding = 6) uniform sampler2D maskMap;

layout(push_constant) uniform Constants{
	uint cameraIndex;
} constants;


#define PI 3.1415926535897932384626433832795
#define TILED_UV fragUV * texProps.tiling
#define ALBEDO pow(texture(albedoMap, TILED_UV).rgb, vec3(2.2))

// From http://filmicgames.com/archives/75
vec3 Uncharted2Tonemap(vec3 x) {
	float A = 0.15;
	float B = 0.50;
	float C = 0.10;
	float D = 0.20;
	float E = 0.02;
	float F = 0.30;
	return ((x*(A*x+C*B)+D*E)/(x*(A*x+B)+D*F))-E/F;
}

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

	return color;
}

vec3 calculateNormal() {

	vec3 texNormal = vec3(texture(normalMap, TILED_UV).rg, 0.0);
	texNormal.b = sqrt(1 - texNormal.x * texNormal.x - texNormal.y * texNormal.y);

	vec3 tangentNormal = texNormal * 2.0 - 1.0;

	vec3 N = normalize(fragNormalWorld);
	vec3 T = normalize(fragTangentWorld.xyz);
	vec3 B = normalize(cross(N, T));
	mat3 TBN = mat3(T, B, N);
	return normalize(TBN * tangentNormal);
}

void main() {
	vec3 N = calculateNormal();

	//vec3 cameraPosWorld = cameraInfo.values[constants.cameraIndex].position;// * -1.0;
	vec3 cameraPosWorld = cameraInverse.values[constants.cameraIndex].inverseViewMatrix[3].xyz;

	vec3 V = normalize(cameraPosWorld - fragPosWorld);
	vec3 R = reflect(-V, N);
	vec4 mashVal = texture(maskMap,TILED_UV).rgba;

	float metallic = mashVal.r;
	float roughness = 1- mashVal.a;
    vec3 ambient = mashVal.ggg;
    
	vec3 F0 = vec3(0.04); 
	F0 = mix(F0, ALBEDO, metallic);
    
	int cascadeIndex = 0;
	vec3 Lo = specularContribution(lighting.directionalLight.direction.xyz, V, N, F0, metallic, roughness, lighting.directionalLight.specular.rgb);
	//Lo = specularContribution(normalize(vec3(-15,-7.5,15)), V, N, F0, metallic, roughness, lighting.directionalLight.specular.rgb);
	float shadow = DirShadows(
		dirShadow,
		lighting.directionalLight.lightSpace,
		lighting.directionalLight.cascadeSplits,
		lighting.directionalLight.cascadeCount,
		lighting.directionalLight.direction.xyz,
		cameraInfo.values[constants.cameraIndex].viewMatrix,
		fragPosWorld,
		fragViewPos,
		N,
		cascadeIndex
	);

	

    for(int i = 0; i < lighting.numPointLights; i++) {
		PointLight pl = pointLightBuffer.values[i];
	    vec3 L = normalize(pl.position.xyz - fragPosWorld);
	    Lo += specularContribution(L, V, N, F0, metallic, roughness, pl.specular.rgb);
    }
    
    for(int i = 0; i < lighting.numSpotLights; i++) {
		SpotLight sl = spotLightBuffer.values[i];
	    vec3 L = normalize(sl.position.xyz - fragPosWorld);
	    Lo += specularContribution(L, V, N, F0, metallic, roughness, sl.specular.rgb * CalcSpotLightIntensity(sl, fragPosWorld));
    }

	vec2 brdf = texture(samplerBRDFLUT, vec2(max(dot(N, V), 0.0), roughness)).rg;
	vec3 reflection = prefilteredReflection(R, roughness).rgb * shadow;	
	vec3 irradiance = texture(samplerIrradiance, N).rgb;
    
	// Diffuse based on irradiance
	vec3 diffuse = irradiance * ALBEDO;
	//diffuse = ALBEDO;

	vec3 F = F_SchlickR(max(dot(N, V), 0.0), F0, roughness);
    
	// Specular reflectance
	vec3 specular = reflection * (F * brdf.x + brdf.y);
    
	// Ambient part
	vec3 kD = 1.0 - F;
	kD *= 1.0 - metallic;	  
	ambient *= (kD * diffuse + specular);

	vec3 color = (ambient + Lo)*shadow;
    
	// Tone mapping
	//color = Uncharted2Tonemap(color * texProps.exposure);
	color = Uncharted2Tonemap(color * 1.5);
	color = color * (1.0 / Uncharted2Tonemap(vec3(11.2)));	
	// // Gamma correction
	//color = pow(color, vec3(1.0 / texProps.gamma));
	//color = pow(color, vec3(1.0 / 1));
	
	outColour = vec4(color, 1.0);
	//outColour = vec4((vec3(1) * brdf.x + brdf.y), 1.0);
}