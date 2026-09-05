
float InterleavedGradientNoise (vec2 pixCoord, int frameCount) {
	vec3 magic = vec3 (0.06711056f, 0.00583715f, 52.9829189f);
	vec2 frameMagicScale = vec2 (2.083f, 4.867f);
	pixCoord += frameCount * frameMagicScale;
	return fract (magic . z * fract (dot (pixCoord, magic . xy)));
}
vec2 GetDirection (uvec2 positionSS, int offset) {
	float noise = InterleavedGradientNoise (positionSS . xy, 0);
	float rotations [] = {60.0, 300.0, 180.0, 240.0, 120.0, 0.0};
	float rotation = (rotations [offset] / 360.0);
	noise += rotation;
	noise *= 3.14159265358979323846;
	return vec2 (cos (noise), sin (noise));
}

float GetOffset (uvec2 positionSS) {
	float offset = 0.25 * ((positionSS . y - positionSS . x) & 0x3);
	return fract (offset);
}

float LinearEyeDepth (float depth, vec4 zBufferParam) {
	return 1.0 / (zBufferParam . z * depth + zBufferParam . w);
}

vec3 GetPositionVS (vec2 positionSS, float depth) {
	float linearDepth = LinearEyeDepth (depth, constants._ZBufferParams);
	return vec3 ((positionSS * constants._AODepthToViewParams . xy - constants._AODepthToViewParams . zw) * linearDepth, linearDepth);
}

float GetDepthForCentral (vec2 positionSS) {
	return imageLoad(mainDepth, ivec2 (constants._FirstTwoDepthMipOffsets . xy + ivec2( positionSS . xy))) . r;
}

vec3 GetNormalVS (vec3 normalBufferData) {
	vec3 normalVS = normalize (mat3(cameraData.values[constants.cameraIndex].viewMatrix)*normalBufferData);
	return vec3 (normalVS . xy, - normalVS . z);
}

float GTAOFastAcos (float x) {
	float outVal = - 0.156583 * abs (x) + 1.57079632679489661923;
	outVal *= sqrt (1.0 - abs (x));
	return x >= 0 ? outVal : 3.14159265358979323846 - outVal;
}

bool AnyIsNaN (vec2 v) {
	return (isnan (v . x) || isnan (v . y));
}

float IntegrateArcCosWeighted (float horzion1, float horizon2, float n, float cosN) {
	float h1 = horzion1 * 2.0;
	float h2 = horizon2 * 2.0;
	float sinN = sin (n);
	return 0.25 * ((- cos (h1 - n) + cosN + h1 * sinN) + (- cos (h2 - n) + cosN + h2 * sinN));
}

float PackAOOutput (float AO, float depth)
{
    uint packedVal = packHalf2x16(vec2(depth,AO));
	
	return uintBitsToFloat(packedVal);
}

float UpdateHorizon (float maxH, float candidateH, float distSq) {
	float falloff = clamp ((1.0 - (distSq * constants._AOInvRadiusSq)),0.0,1.0);
	return (candidateH > maxH) ? mix (maxH, candidateH, falloff) : mix (maxH, candidateH, 0.03f);
}
float HorizonLoop (vec3 positionVS, vec3 V, vec2 rayStart, vec2 rayDir, float rayOffset, float rayStep, int mipModifier) {
	float maxHorizon = - 1.0f;
	float t = rayOffset * rayStep + rayStep;
	uint startWithLowerRes = min (max (0, constants._AOStepCount / 2 - 2), 3);
	for (uint i = 0; i <  constants._AOStepCount ; i ++)
	{
		vec2 samplePos = max (vec2(2.0), min (rayStart + vec2(t) * rayDir,constants. _AOBufferSize . xy - vec2(2.0)));
		float sampleDepth = GetDepthSample (samplePos, i > startWithLowerRes);
		vec3 samplePosVS = GetPositionVS (samplePos . xy, sampleDepth);
		vec3 deltaPos = samplePosVS - positionVS;
		float deltaLenSq = dot (deltaPos, deltaPos);
		float currHorizon = dot (deltaPos, V) * (1.0/sqrt (deltaLenSq));
		maxHorizon = UpdateHorizon (maxHorizon, currHorizon, deltaLenSq);
		t += rayStep;
	}
	return maxHorizon;
}
