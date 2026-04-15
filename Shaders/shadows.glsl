#define AMBIENT_DIR_SHADOW_FACTOR 0.03

#define CUBEMAPFACE_POSITIVE_X 0
#define CUBEMAPFACE_NEGATIVE_X 1
#define CUBEMAPFACE_POSITIVE_Y 2
#define CUBEMAPFACE_NEGATIVE_Y 3
#define CUBEMAPFACE_POSITIVE_Z 4
#define CUBEMAPFACE_NEGATIVE_Z 5

int CubeMapFaceID(vec3 dir)
{
    int faceID;

    if (abs(dir.z) >= abs(dir.x) && abs(dir.z) >= abs(dir.y))
    {
        faceID = (dir.z < 0.0) ? CUBEMAPFACE_NEGATIVE_Z : CUBEMAPFACE_POSITIVE_Z;
    }
    else if (abs(dir.y) >= abs(dir.x))
    {
        faceID = (dir.y < 0.0) ? CUBEMAPFACE_NEGATIVE_Y : CUBEMAPFACE_POSITIVE_Y;
    }
    else
    {
        faceID = (dir.x < 0.0) ? CUBEMAPFACE_NEGATIVE_X : CUBEMAPFACE_POSITIVE_X;
    }

    return faceID;
}

const mat4 biasMat = mat4( 
	0.5, 0.0, 0.0, 0.0,
	0.0, 0.5, 0.0, 0.0,
	0.0, 0.0, 1.0, 0.0,
	0.5, 0.5, 0.0, 1.0 
);


float textureProj(sampler2D shadowTex, vec4 shadowCoord, vec2 offset, float ambientFactor) {
	float shadow = 1.0;
	float bias =  0.001;

	if ( shadowCoord.z > -1.0 && shadowCoord.z < 1.0 ) {
		float dist = texture(shadowTex, vec2(shadowCoord.st + offset)).r;
		if (shadowCoord.w > 0 && dist < shadowCoord.z - bias) {
			shadow = ambientFactor;
		}
	}

	return shadow;
}

float textureProj(sampler2DArray dirShadowTex, vec4 shadowCoord, vec2 offset, uint cascadeIndex, float ambientFactor)
{
	float shadow = 1.0;
	float bias =  0.001;
	
	if ( shadowCoord.z > -1.0 && shadowCoord.z < 1.0 ) {
		float dist = texture(dirShadowTex, vec3(shadowCoord.st + offset, cascadeIndex)).r;
		if (shadowCoord.w > 0 && dist < shadowCoord.z - bias) {
			shadow = ambientFactor;
		}
	}
	return shadow;

}

float filterPCF(sampler2D shadowTex, vec4 sc, float ambientFactor) {
	ivec2 texDim = textureSize(shadowTex,0);
	float scale = 0.75;
	float dx = scale * 1.0 / float(texDim.x);
	float dy = scale * 1.0 / float(texDim.y);

	float shadowFactor = 0.0;
	int count = 0;
	int range = 1;
	
	for (int x = -range; x <= range; x++) {
		for (int y = -range; y <= range; y++) {
			shadowFactor += textureProj(shadowTex, sc, vec2(dx*x, dy*y),ambientFactor);
			count++;
		}
	}
	return shadowFactor / count;
}

float filterPCF(sampler2DArray shadowTex, vec4 sc, uint textureIndex, float ambientFactor)
{
	ivec2 texDim = textureSize(shadowTex, 0).xy;
	float scale = 0.75;
	float dx = scale * 1.0 / float(texDim.x);
	float dy = scale * 1.0 / float(texDim.y);

	float shadowFactor = 0.0;
	int count = 0;
	int range = 1;
	
	for (int x = -range; x <= range; x++) {
		for (int y = -range; y <= range; y++) {
			shadowFactor += textureProj(shadowTex, sc, vec2(dx*x, dy*y), textureIndex, ambientFactor);
			count++;
		}
	}
	return shadowFactor / count;
}


float DirShadows(sampler2DArray dirShadowMap, mat4[4]lightSpace, vec4 cascadeSplits, int cascadeCount, vec3 fragPosWorld,vec3 fragViewPos, out int cascadeIndex){
	cascadeIndex = 0;
	for(int i = 0; i < cascadeCount - 1; ++i) {
		if(fragViewPos.z < cascadeSplits[i]) {	
			cascadeIndex = i + 1;
		}
	}

	vec4 shadowCoord = (biasMat * lightSpace[cascadeIndex]) * vec4(fragPosWorld, 1.0);
	
	float shadow = 0;
	if (1 == 1) {
		shadow = filterPCF(dirShadowMap, shadowCoord / shadowCoord.w, cascadeIndex, AMBIENT_DIR_SHADOW_FACTOR);
	} else {
		shadow = textureProj(dirShadowMap, shadowCoord / shadowCoord.w, vec2(0.0), cascadeIndex,AMBIENT_DIR_SHADOW_FACTOR);
	}
	return shadow;
}

float ShadowSlCalculationAlt(sampler2D slShadowTex, vec3 fragPos, vec3 viewPos, SpotLight sl){
	float shadow = 0.0;
	vec4 shadowCoord = (biasMat * sl.lightSpace) * vec4(fragPos, 1.0);
	if (1 == 1) {
		shadow = filterPCF(slShadowTex, shadowCoord /  shadowCoord.w,0);
	}
	else {
		shadow = textureProj(slShadowTex, shadowCoord/shadowCoord.w, vec2(0.0),0);
	}
	return shadow;
}

float ShadowPlCalculationAlt(sampler2DArray plShadowTex, vec3 fragPos, vec3 viewPos, PointLight pl){
   float shadow = 0.0;

	vec3 plDir = normalize(fragPos - pl.position.xyz);

	int faceId = CubeMapFaceID(plDir);

	vec4 shadowCoord = (biasMat * pl.plLightSpace[faceId]) * vec4(fragPos, 1.0);
	
	if (1 == 1) {
		shadow = filterPCF(plShadowTex, shadowCoord /  shadowCoord.w, faceId,0);
	}
	else {
		shadow = textureProj(plShadowTex, shadowCoord /  shadowCoord.w, vec2(0.0),faceId,0);
	}
	return shadow;
}
