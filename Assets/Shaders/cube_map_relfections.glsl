
struct CubeRelfectionData {
    mat4[6] reflectionSpace;
    
	vec3 position;
    float farPlane;
};


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

vec4 CubeRelfection(samplerCube relectionCube, vec3 toCamera, vec3 normalWS, CubeRelfectionData relectionProbe){
    vec4 reflectionColour = vec4(0.0);
    
	mat4 translate = mat4(
        vec4(1.0,0.0,0.0,relectionProbe.position.x),
        vec4(0.0,1.0,0.0,relectionProbe.position.y),
        vec4(0.0,0.0,1.0,relectionProbe.position.z),
        vec4(0.0,0.0,0.0,1.0)
    );
    toCamera = (translate * vec4(toCamera, 1.0)).xyz;
    normalWS = (translate * vec4(normalWS, 1.0)).xyz;
	vec3 dir = reflect(-toCamera, normalWS);
	int faceId = CubeMapFaceID(dir);
    

    mat4 relfectSpace = relectionProbe.reflectionSpace[faceId];

    reflectionColour = texture(relectionCube, dir).rgba;

	return reflectionColour;
}