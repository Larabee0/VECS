
struct Meshlet {
    uint vertex_offset;
	uint triangle_offset;
	uint vertex_count;
	uint triangle_count;
};

// escaping my vec3 nightmares heavily
struct Bounds {
    vec4 centerRadius; // radius in w
    float cone_apex_x;
    float cone_apex_y;
    float cone_apex_z;
    vec4 cone_axis_cutoff; // cut off in w
    uint8_t cone_axis_x;
    uint8_t cone_axis_y;
    uint8_t cone_axis_z;
    uint8_t cone_cutoff;
};
