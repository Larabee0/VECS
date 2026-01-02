/* Copyright (c) 2021, Sascha Willems
 *
 * SPDX-License-Identifier: MIT
 *
 */

#version 450
 
layout (location = 0) in VertexInput {
  vec4 color;
} vertexInput;

layout(location = 0) out vec4 outFragColor;


layout(set = 0,binding = 0) uniform CameraInfo{
	mat4 projectionMatrix;
	mat4 viewMatrix;
	mat4 projectionViewMatrix;	
	vec4 position;
	vec4 forward;
} cameraMain;

void main()
{
	outFragColor = vec4(1);
}