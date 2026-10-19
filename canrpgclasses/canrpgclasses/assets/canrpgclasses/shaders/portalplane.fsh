#version 330 core

// Portal plane: the finished frame behind it, bent by moving noise and tinted between two colours.

in vec2 uv;
in float dist;
in vec4 rgbaFog;
in float fogAmount;

out vec4 outColor;

uniform sampler2D primaryFb;
uniform sampler2D depthTex;
uniform vec2 invFrameSize;
uniform vec2 planeSize;
uniform float counter;
uniform vec3 tintA;
uniform vec3 tintB;
// Destination snapshot (top row first) instead of the bent view behind the plane.
uniform sampler2D imageTex;
uniform float hasImage;
uniform float flipU;

#include noise2d.ash
#include fogandlight.fsh

void main()
{
	vec2 screen = gl_FragCoord.xy * invFrameSize;

	// Hidden behind what the scene drew in front (linearDepth is distance / zFar).
	float z1 = linearDepth(texture(depthTex, screen).r);
	float z2 = linearDepth(gl_FragCoord.z);
	if ((z2 - z1) * 1500 > 0.03) discard;

	// In blocks, so the swirl keeps its size whatever the plane's size.
	vec2 p = uv * planeSize;

	float noise =
		cnoise2(vec2(p.x * 1.5, p.y * 1.5 - counter / 2)) / 40.0
		+ cnoise2(vec2(p.x * 4, p.y * 4 + counter / 3)) / 90.0
		+ cnoise2(gl_FragCoord.xy / 2 + vec2(0, -counter)) / 250.0;

	vec4 col;
	if (hasImage > 0.5) {
		vec2 iuv = vec2(flipU > 0.5 ? 1.0 - uv.x : uv.x, 1.0 - uv.y);
		col = texture(imageTex, iuv + noise * 0.6);
	} else {
		col = texture(primaryFb, screen + noise);
	}

	// Stepped edges on a 1/8 block grid: three alpha steps inwards, corners cut as a staircase.
	const float cellSize = 0.125;
	vec2 cell = (floor(p / cellSize) + 0.5) * cellSize;
	vec2 edge = min(cell, planeSize - cell);
	float side = floor(min(edge.x, edge.y) / cellSize) / 3.0;
	float corner = floor((edge.x + edge.y - 0.3) / cellSize) / 3.0;
	float a = clamp(min(side, corner), 0.0, 1.0);

	float swirl = cnoise2(vec2(p.x * 2 + sin(counter / 2), p.y * 2 - counter / 1.5)) * 0.5 + 0.5;
	vec3 tint = mix(tintA, tintB, swirl);
	// A snapshot gets a lighter tint and less see-through, so the destination stays readable.
	float tintAmount = hasImage > 0.5 ? 0.12 + 0.18 * swirl : 0.35 + 0.25 * swirl;
	col.rgb = mix(col.rgb, tint, tintAmount);

	col.a = clamp(a * (hasImage > 0.5 ? 0.95 : 0.75 + 0.25 * swirl), 0, 1);
	col.a -= clamp((dist * (1 + fogAmount / 2.0) - 50) / 40, 0, 0.7 + fogAmount * 0.3);
	if (col.a < 0.02) discard;

	outColor = applyFog(col, fogAmount);
}
