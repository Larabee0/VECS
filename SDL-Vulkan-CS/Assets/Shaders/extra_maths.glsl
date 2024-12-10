
float lerp(float a, float b, float t){
	return a + (b-a)*clamp(t,0,1);
}

float inverseLerp(float a, float b, float value){

	return a != b ? clamp((value-a)/(b-a),0,1) : 0;
}