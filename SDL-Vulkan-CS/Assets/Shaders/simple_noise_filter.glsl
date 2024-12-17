
float evaluateSimple(GlobalNoiseSettings settings, vec3 point){
	float noiseValue = 0;
	float frequency = settings.baseRoughness;
	float amplitude = 1.0;
	vec3 centre = vec3(settings.centreX, settings.centreY, settings.centreZ);

	for(int i = 0; i < settings.numLayers; i++){
		vec3 gradient;
		float v = snoise(point * frequency + centre, gradient);
		if(settings.gradientWeight == 1){
			v += gradientWeight(gradient) * settings.gradientWeightMul;
		}
		noiseValue += (v + 1) * 0.5 * amplitude;
		frequency *= settings.roughness;
		amplitude *= settings.persistence;
	}
	noiseValue -= settings.minValue;
	return noiseValue * settings.strength;
}