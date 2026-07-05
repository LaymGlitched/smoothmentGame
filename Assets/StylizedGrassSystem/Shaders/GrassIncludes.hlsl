#ifndef GRASS_INCLUDES_INCLUDED
#define GRASS_INCLUDES_INCLUDED

// Data generated per blade by the compute shader
struct GrassInstanceData
{
    float4x4 objectToWorld;
    float3 colorVariation;
    float hash; // Random value per blade [0,1]
};

// Painted patch data
struct GrassPatch
{
    float3 positionWS;
    float3 normalWS;
    float radius;
    float density;
};

// Pseudo-random noise functions
float hash11(float p)
{
    p = frac(p * .1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float hash12(float2 p)
{
    float3 p3  = frac(float3(p.xyx) * .1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float hash13(float3 p3)
{
    p3  = frac(p3 * .1031);
    p3 += dot(p3, p3.zyx + 31.32);
    return frac((p3.x + p3.y) * p3.z);
}

float2 hash22(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(.1031, .1030, .0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

// 2D Value Noise
float valueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);

    return lerp(lerp(hash12(i + float2(0.0, 0.0)),
                     hash12(i + float2(1.0, 0.0)), u.x),
                lerp(hash12(i + float2(0.0, 1.0)),
                     hash12(i + float2(1.0, 1.0)), u.x), u.y);
}

// Matrix construction utilities
float4x4 CreateTransformMatrix(float3 position, float3 eulerAngles, float3 scale)
{
    float c = cos(eulerAngles.y);
    float s = sin(eulerAngles.y);
    
    float4x4 rotY = float4x4(
        c, 0, s, 0,
        0, 1, 0, 0,
        -s, 0, c, 0,
        0, 0, 0, 1
    );

    c = cos(eulerAngles.x);
    s = sin(eulerAngles.x);
    float4x4 rotX = float4x4(
        1, 0, 0, 0,
        0, c, -s, 0,
        0, s, c, 0,
        0, 0, 0, 1
    );

    c = cos(eulerAngles.z);
    s = sin(eulerAngles.z);
    float4x4 rotZ = float4x4(
        c, -s, 0, 0,
        s, c, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1
    );

    float4x4 scaleMat = float4x4(
        scale.x, 0, 0, 0,
        0, scale.y, 0, 0,
        0, 0, scale.z, 0,
        0, 0, 0, 1
    );

    float4x4 transMat = float4x4(
        1, 0, 0, position.x,
        0, 1, 0, position.y,
        0, 0, 1, position.z,
        0, 0, 0, 1
    );

    return mul(transMat, mul(rotY, mul(rotX, mul(rotZ, scaleMat))));
}

// Construct matrix from position, up vector, and forward/yaw
float4x4 MatrixFromUpAndYaw(float3 position, float3 up, float yaw, float3 scale)
{
    float3 forward = normalize(float3(sin(yaw), 0, cos(yaw)));
    
    // Project forward onto plane defined by up vector
    forward = normalize(forward - up * dot(forward, up));
    
    // If up and forward are parallel (shouldn't happen with normal terrain), fallback
    if (length(forward) < 0.01) {
        forward = float3(1, 0, 0);
    }
    
    float3 right = normalize(cross(up, forward));
    
    float4x4 rotScale = float4x4(
        right.x * scale.x, up.x * scale.y, forward.x * scale.z, position.x,
        right.y * scale.x, up.y * scale.y, forward.y * scale.z, position.y,
        right.z * scale.x, up.z * scale.y, forward.z * scale.z, position.z,
        0,                 0,              0,                   1
    );
    
    return rotScale;
}

#endif
