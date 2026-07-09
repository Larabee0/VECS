struct ObjectMatrices{
	mat4 modelMatrix; // project * view * model
	mat4 normalMatrix;
};

struct ObjectBounds{
	vec4 bMin;
	vec4 bMax;
};

struct Node
{
    vec4 color;
    float depth;
    uint next;
};


struct CameraInfo {
	mat4 projectionMatrix;
	mat4 viewMatrix;
	mat4 projectionViewMatrix;	
	vec3 position;
	float _pad1;
	vec3 forward;
	float _pad2;
};

struct CameraInverse {
	mat4 inverseProjectionMatrix;
	mat4 inverseViewMatrix;
	mat4 inverseProjectionViewMatrix;
};

struct AdditionalCameraInfo {
	float ratio;
 	float p00;
 	float p11;
 	float nearPlane;
	float farPlane;
 	vec4 frustum;
};

struct OrthographicInfo {
	float orthographic;
	float width;
	float height;
};
