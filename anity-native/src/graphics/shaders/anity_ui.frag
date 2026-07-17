#version 450

layout(location = 0) in vec4 vertexColor;
layout(location = 1) in vec2 textureUv;
layout(set = 0, binding = 0) uniform sampler2D mainTexture;
layout(set = 1, binding = 0) uniform sampler2D alphaTexture;
layout(location = 0) out vec4 outColor;

void main()
{
    vec4 color = vertexColor * texture(mainTexture, textureUv);
    color.a *= texture(alphaTexture, textureUv).r;
    outColor = color;
}
