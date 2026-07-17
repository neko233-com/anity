#version 450

layout(location = 0) in vec3 inPosition;
layout(location = 1) in vec4 inColor;
layout(location = 2) in vec4 inUv0;

layout(push_constant) uniform Viewport
{
    vec2 size;
} viewport;

layout(location = 0) out vec4 vertexColor;
layout(location = 1) out vec2 textureUv;

void main()
{
    vec2 ndc = vec2(
        (inPosition.x / viewport.size.x) * 2.0 - 1.0,
        (inPosition.y / viewport.size.y) * 2.0 - 1.0);
    gl_Position = vec4(ndc, 0.0, 1.0);
    vertexColor = inColor;
    textureUv = inUv0.xy;
}
