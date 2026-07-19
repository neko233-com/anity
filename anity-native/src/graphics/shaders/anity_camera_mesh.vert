#version 450

layout(location = 0) in vec3 inPosition;
layout(location = 1) in vec2 inUv;
layout(location = 2) in vec4 inColor;
layout(location = 3) in vec3 inNormal;
layout(location = 4) in vec4 inTangent;

layout(push_constant) uniform CameraMeshMatrices
{
    mat4 objectToClip;
    vec4 baseMapST;
    vec4 material;
} matrices;

layout(location = 0) out vec4 vertexColor;
layout(location = 1) out vec2 textureUv;
layout(location = 2) out vec3 vertexNormal;
layout(location = 3) out vec4 vertexTangent;

void main()
{
    gl_Position = matrices.objectToClip * vec4(inPosition, 1.0);
    vertexColor = inColor;
    textureUv = inUv;
    vertexNormal = inNormal;
    vertexTangent = inTangent;
}
