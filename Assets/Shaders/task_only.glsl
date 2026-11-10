
// escaping my vec3 nightmares heavily
struct Bounds {
    vec4 min; // radius in w
    vec4 max; // cut off in w
};

uint GetPerInvoke(uint count){
	uint perInvoke = max(1, count / MESH_LOCAL_SIZE_X);
	if(count % MESH_LOCAL_SIZE_X != 0){
		perInvoke++;
	}
	return perInvoke;
}
