
precision highp float;

float evaluateRigid(GlobalNoiseSettings settings, vec3 point){
	float noiseValue = 0.0;
	float frequency = settings.baseRoughness;
	float amplitude = 1.0;
	float weight = 1.0;
	vec3 centre = vec3(settings.centreX, settings.centreY, settings.centreZ);
	for(int i = 0; i < settings.numLayers; i++){
		vec3 gradient;
		float v = 1.0 - abs(snoise(point * frequency + centre, gradient));
		if(settings.gradientWeight == 1){
			v += gradientWeight(gradient)*settings.gradientWeightMul;
		}
		v *= v;
		v *= weight;
		weight = clamp(v * settings.weightMultiplier, 0.0, 1.0);
		noiseValue += v * amplitude;
		frequency *= settings.roughness;
		amplitude *= settings.persistence;
	}
	noiseValue -= settings.minValue;
	return noiseValue * settings.strength;
}