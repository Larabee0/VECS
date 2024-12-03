
struct Vertex{
	vec3 position;
	vec3 normal;
	float elevation;
};


struct GlobalNoiseSettings{
	int filterType; // 4
	
    float strength; // 8
    int numLayers; // 12
    float baseRoughness; //16
    float roughness; // 20
    float persistence; // 24
    //vec4 centre; // 36
    float offset; // 40

    float minValue; // 44
    int gradientWeight; // 48?
    float gradientWeightMul; // 52

    int enabled; // 56
    int useFirstlayerAsMask; // 60

    float weightMultiplier; // 64
};

float gradientWeight(vec3 gradient){
    return 1.0 / (1.0 + dot(gradient, gradient));
}