
struct PointLight {
	vec4 position;

	vec4 ambient;
	vec4 diffuse;
	vec4 specular;

	float constant;
	float linear;
	float quadratic;
};

struct SpotLight{
	vec3 position;
    float cutOff;
    vec3 direction;
    float outerCutOff; 

	vec4 ambient;
	vec4 diffuse;
	vec4 specular;

	float constant;
	float linear;
	float quadratic;
   
};

struct DirectionalLight{
    vec4 direction;

    vec4 ambient;
    vec4 diffuse;
    vec4 specular;
};

vec3 CalcDirLight(DirectionalLight light, vec3 normal, vec3 viewDir, float shininess, vec3 ambientCol, vec3 diffuseCol, vec3 specularCol){
    vec3 lightDir = normalize(-light.direction.xyz);
    // diffuse shading
    float diff = max(dot(normal, lightDir), 0.0);
    // specular shading
    vec3 reflectDir = reflect(-lightDir, normal);
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), shininess);
    // combine results
    vec3 ambient  = light.ambient.xyz * ambientCol;
    vec3 diffuse  = light.diffuse.xyz  * diff * diffuseCol;
    vec3 specular = light.specular.xyz * spec * specularCol;
    return (ambient + diffuse + specular);
}

vec3 CalcPointLight(PointLight light, vec3 normal, vec3 fragPos, vec3 viewDir, float shininess, vec3 ambientCol, vec3 diffuseCol, vec3 specularCol) {
    vec3 lightDir = normalize(light.position.xyz - fragPos);
    // diffuse shading
    float diff = max(dot(normal, lightDir), 0.0);
    // specular shading
    vec3 reflectDir = reflect(-lightDir, normal);
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), shininess);
    // attenuation
    float distance    = length(light.position.xyz - fragPos);
    float attenuation = 1.0 / (light.constant + light.linear * distance + 
  			     light.quadratic * (distance * distance));    
    // combine results
    vec3 ambient  = light.ambient.xyz * ambientCol;
    vec3 diffuse  = light.diffuse.xyz  * diff * diffuseCol;
    vec3 specular = light.specular.xyz * spec * specularCol;
    ambient  *= attenuation;
    diffuse  *= attenuation;
    specular *= attenuation;
    return (ambient + diffuse + specular);
}


vec3 CalcSpotLight(SpotLight light, vec3 normal, vec3 fragPos, vec3 viewDir, float shininess, vec3 ambientCol, vec3 diffuseCol, vec3 specularCol) {
    vec3 lightDir = normalize(light.position.xyz - fragPos);
    float theta = dot(lightDir, normalize(-light.direction.xyz));
    float epsilon   = light.cutOff - light.outerCutOff;
    float intensity = clamp((theta - light.outerCutOff) / epsilon, 0.0, 1.0);

    // diffuse shading
    float diff = max(dot(normal, lightDir), 0.0);
    // specular shading
    vec3 reflectDir = reflect(-lightDir, normal);
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), shininess);
    // attenuatio   n
    float distance    = length(light.position.xyz - fragPos);
    float attenuation = 1.0 / (light.constant + light.linear * distance + light.quadratic * (distance * distance));
    // combine results
    vec3 ambient = light.ambient.xyz * ambientCol;
    vec3 diffuse = light.diffuse.xyz  * diff * diffuseCol;
    vec3 specular = light.specular.xyz * spec * specularCol;
    ambient *= attenuation;
    diffuse *= attenuation;
    specular *= attenuation;

    diffuse *= intensity;
    specular *= intensity;
    return (ambient + diffuse + specular);
} 