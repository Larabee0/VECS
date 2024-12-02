
struct Vertex{
	vec3 position;
	vec3 normal;
	float elevation;
};


struct GlobalNoiseSettings{
	int filterType;
	
    float strength;
    int numLayers;
    float baseRoughness;
    float roughness;
    float persistence;
    vec3 centre;
    float offset;

    float minValue;
    bool gradientWeight;
    float gradientWeightMul;

    bool enabled;
    bool useFirstlayerAsMask;

    float weightMultiplier;
};

float gradientWeight(vec3 gradient){
    return 1.0 / (1.0 + dot(gradient, gradient));
}