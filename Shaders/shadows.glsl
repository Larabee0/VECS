
const mat4 biasMat = mat4( 
	0.5, 0.0, 0.0, 0.0,
	0.0, 0.5, 0.0, 0.0,
	0.0, 0.0, 1.0, 0.0,
	0.5, 0.5, 0.0, 1.0 
);


const vec3 sampleOffsetDirections[20] = vec3[]
(
   vec3( 1,  1,  1), vec3( 1, -1,  1), vec3(-1, -1,  1), vec3(-1,  1,  1), 
   vec3( 1,  1, -1), vec3( 1, -1, -1), vec3(-1, -1, -1), vec3(-1,  1, -1),
   vec3( 1,  1,  0), vec3( 1, -1,  0), vec3(-1, -1,  0), vec3(-1,  1,  0),
   vec3( 1,  0,  1), vec3(-1,  0,  1), vec3( 1,  0, -1), vec3(-1,  0, -1),
   vec3( 0,  1,  1), vec3( 0, -1,  1), vec3( 0, -1, -1), vec3( 0,  1, -1)
);   


#define ambientDIRShadow 0.03

float textureProj(sampler2DArray dirShadow, vec4 shadowCoord, vec2 offset, uint cascadeIndex, vec3 lightDir, vec3 normal)
{
	float shadow = 1.0;
	float bias =  0.001;
	//bias = max(0.05 * (1.0 - dot(normal, lightDir)), 0.005);

	if ( shadowCoord.z > -1.0 && shadowCoord.z < 1.0 ) {
		float dist = texture(dirShadow, vec3(shadowCoord.st + offset, cascadeIndex)).r;
		if (shadowCoord.w > 0 && dist < shadowCoord.z - bias) {
			shadow = ambientDIRShadow;
		}
	}
	return shadow;

}

float filterPCF(sampler2DArray dirShadow, vec4 sc, uint cascadeIndex, vec3 lightDir, vec3 normal)
{
	ivec2 texDim = textureSize(dirShadow, 0).xy;
	float scale = 0.75;
	float dx = scale * 1.0 / float(texDim.x);
	float dy = scale * 1.0 / float(texDim.y);

	float shadowFactor = 0.0;
	int count = 0;
	int range = 1;
	
	for (int x = -range; x <= range; x++) {
		for (int y = -range; y <= range; y++) {
			shadowFactor += textureProj(dirShadow, sc, vec2(dx*x, dy*y), cascadeIndex,lightDir,normal);
			count++;
		}
	}
	return shadowFactor / count;
}


float DirShadows(sampler2DArray dirShadowMap, mat4[4]lightSpace, vec4 cascadeSplits, int cascadeCount, vec3 lightDir, mat4 cameraView, vec3 fragPosWorld,vec3 fragViewPos, vec3 normal, out int cascadeIndex  ){
	cascadeIndex = 0;
	vec3 inViewPos =(cameraView * vec4(fragPosWorld,0.0)).xyz;
	for(int i = 0; i < cascadeCount - 1; ++i) {
		if(fragViewPos.z < cascadeSplits[i]) {	
			cascadeIndex = i + 1;
		}
	}
	vec4 shadowCoord = (biasMat * lightSpace[cascadeIndex]) * vec4(fragPosWorld, 1.0);
	
	float shadow = 0;
	if (1 == 1) {
		shadow = filterPCF(dirShadowMap, shadowCoord / shadowCoord.w, cascadeIndex, lightDir, normal);
	} else {
		shadow = textureProj(dirShadowMap, shadowCoord / shadowCoord.w, vec2(0.0), cascadeIndex, lightDir, normal);
	}
	return shadow;
}

float FilterPLPCF(samplerCube plShadow, vec3 fragPos, vec3 viewPos, vec3 lightPos, float farPlane, int textureIndex){
    vec3 fragToLight = fragPos - lightPos;
	float currentDepth = length(fragToLight);
	float shadow = 0.0;
	int samples  = 20;
	float viewDistance = length(viewPos - fragPos);
	float diskRadius = (1.0 + (viewDistance / farPlane)) / farPlane;
	for(int i = 0; i < samples; ++i)
	{
		vec3 coord = vec3(fragToLight + sampleOffsetDirections[i] * diskRadius);
	    float closestDepth = texture(plShadow, coord).r;
	    closestDepth *= farPlane;   // undo mapping [0;1]
	    if(currentDepth > closestDepth)
	        shadow += 1.0;
		//shadow+=closestDepth;
	}
	shadow /= float(samples);  
	return (1.0-shadow);
}

float ShadowSlCalculation(sampler2DArray slShadow, vec3 fragPos, vec3 viewPos, SpotLight sl, int textureIndex){
	float shadow = 0.0;
	vec4 lightSpacePos = sl.lightSpace * vec4(fragPos,1);
	vec3 lightCoords = lightSpacePos.xyz / lightSpacePos.w;
	
	vec3 fragToLight = fragPos - sl.position;
	float currentDepth = length(fragToLight);
	
	lightCoords = (lightCoords + 1.0) / 2.0;
	
	float closestDepth = texture(slShadow, vec3(lightCoords.xy,textureIndex)).r;
	closestDepth *= sl.farPlane;
	if (currentDepth > closestDepth+0.005){
		shadow += 1.0;   
	}

	return (1.0-shadow);
}
