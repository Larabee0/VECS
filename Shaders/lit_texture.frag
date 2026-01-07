#version 460
#extension GL_ARB_shading_language_include : require
#include "common_structures.glsl"

layout (location = 0) in vec4 fragColour;
layout (location = 1) in vec3 fragPosWorld;
layout (location = 2) in vec3 fragNormalWorld;
layout (location = 3) in vec2 fragUV;

layout (location = 0) out vec4 outColour;

layout(set = 0, binding = 0) uniform LightingInfo {
	vec4 ambientLightColour;
	vec4 ambientLightDir;
	float ambientStrength;
	float diffuseStrength;
	float specularStrength;
	int numPointLights;
} lighting;

layout (set = 0, binding = 1) readonly buffer PointLights {
	PointLight values[];
} pointLightBuffer;

layout(set = 0,binding = 2) readonly buffer CameraInfos {
	CameraInfo values[];
} cameraInfo;

layout(set = 0,binding = 3) readonly buffer CameraInverses {
	CameraInverse values[];
} cameraInverse;

layout (set = 0, binding = 4) readonly buffer AdditionalCameraInfos {
	AdditionalCameraInfo values[];
} cameraPlanes;

layout (set = 0, binding = 5) readonly buffer OrthographicInfos {
	OrthographicInfo values[];
} orthographic;

layout(set = 1, binding = 2) uniform sampler2D texSampler;

layout(set = 1, binding = 3) uniform TexPorps {
	vec4 colour;
	float tiling;
} texProps;

layout(push_constant) uniform Constants{
	uint cameraIndex;
} constants;

void main()
{
	vec3 norm = normalize(fragNormalWorld);
	vec3 ambientLightColour = lighting.ambientLightColour.xyz;
	vec3 lightDir = -lighting.ambientLightDir.xyz;
	vec3 viewPos = cameraInverse.values[constants.cameraIndex].inverseViewMatrix[3].xyz;
	vec3 viewDir = normalize(viewPos - fragPosWorld);
	vec3 reflectDir = reflect(-lightDir, norm);
	
	
	float specularStrength = lighting.specularStrength;

	float spec = pow(max(dot(viewDir,reflectDir),0.0), 32);
	vec3 specular = specularStrength * spec * ambientLightColour;

	float diffuseStrength = lighting.diffuseStrength;

	float diff = max(dot(norm,lightDir),0.0);
	vec3 diffuse = diffuseStrength * diff * ambientLightColour;
	

	float ambientStrength = lighting.ambientStrength;

	vec3 ambient = ambientStrength * ambientLightColour;

	vec3 textureColour = texture(texSampler, fragUV * texProps.tiling).xyz;


	for(int i = 0; i < lighting.numPointLights; i++){		
		PointLight light = pointLightBuffer.values[i];

		float PLdistance = length(light.position.xyz - fragPosWorld);
		float attenuation = 1.0 / (light.constant + light.linear * PLdistance +  light.quadratic * (PLdistance * PLdistance));
		
		float theta = dot(-lightDir, normalize(-light.direction.xyz));
		if(light.direction.w > 0){
			if(theta > light.cutOff){
				float epsilon   = light.cutOff - light.outerCutOff;
				attenuation = clamp((theta - light.outerCutOff) / epsilon, 0.0, 1.0);
			}
			else{
				attenuation = 0;
			}
		}
		ambient  += light.colour.xyz * attenuation * light.ambientStrength; 
		diffuse  += light.colour.xyz * attenuation * light.diffuseStrength;
		specular += light.colour.xyz * attenuation * light.specularStrength;
	}

	vec3 result = (ambient + diffuse + specular) * textureColour;
	
	outColour = vec4(result, 1.0);
	
}