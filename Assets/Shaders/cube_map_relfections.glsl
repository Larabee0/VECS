
struct CubeRelfectionData {
    mat4[6] reflectionSpace;
    
	vec3 position;
    float farPlane;
};


vec4 CubeRelfection(samplerCube relectionCube, vec3 toCamera, vec3 normalWS, CubeRelfectionData relectionProbe){
    vec4 reflectionColour = vec4(0.0);
    
	mat4 translate = mat4(
        vec4(1.0,0.0,0.0,relectionProbe.position.x),
        vec4(0.0,-1.0,0.0,relectionProbe.position.y),
        vec4(0.0,0.0,1.0,relectionProbe.position.z),
        vec4(0.0,0.0,0.0,1.0)
    );
    toCamera = (translate * vec4(toCamera, 1.0)).xyz;
    normalWS = (translate * vec4(normalWS, 1.0)).xyz;
	vec3 dir = reflect(-toCamera, normalWS);
	
    reflectionColour = texture(relectionCube, dir).rgba;

	return reflectionColour;
}