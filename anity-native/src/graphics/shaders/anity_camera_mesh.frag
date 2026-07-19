#version 450

layout(location = 0) in vec4 vertexColor;
layout(location = 1) in vec2 textureUv;
layout(location = 2) in vec3 vertexNormal;
layout(location = 3) in vec4 vertexTangent;
layout(set = 0, binding = 0) uniform sampler2D baseTexture;

layout(push_constant) uniform CameraMeshMatrices
{
    mat4 objectToClip;
    vec4 baseMapST;
    vec4 material;
} matrices;
layout(location = 0) out vec4 outColor;
layout(location = 1) out vec4 outNormal;

void main()
{
    vec4 shaded = vertexColor * texture(baseTexture, textureUv * matrices.baseMapST.xy + matrices.baseMapST.zw);
    if (matrices.material.y > 0.5 && shaded.a < matrices.material.x)
        discard;
    outColor = shaded;
    outNormal = vec4(normalize(vertexNormal) * 0.5 + 0.5, 1.0);
}
