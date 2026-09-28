// OKLAB color space conversion
//
// Conversion matrices from Björn Ottosson, "A perceptual color space for image
// processing" (https://bottosson.github.io/posts/oklab/), Copyright (c) 2020
// Björn Ottosson, MIT License (also released into the public domain).
// See THIRD_PARTY_NOTICES.md at the package root for the full notice.
// OKLAB is a perceptually uniform color space that provides
// better hue shifting than RGB or HSV

#ifndef OKLAB_INCLUDED
#define OKLAB_INCLUDED

#define PI 3.14159265359

// Convert linear sRGB to OKLAB color space
float3 linear_srgb_to_oklab(float3 c)
{
    // RGB to LMS (cone response)
    float l = 0.4122214708 * c.r + 0.5363325363 * c.g + 0.0514459929 * c.b;
    float m = 0.2119034982 * c.r + 0.6806995451 * c.g + 0.1073969566 * c.b;
    float s = 0.0883024619 * c.r + 0.2817188376 * c.g + 0.6299787005 * c.b;

    // Nonlinear transformation (cube root)
    float l_ = pow(abs(l), 1.0 / 3.0) * sign(l);
    float m_ = pow(abs(m), 1.0 / 3.0) * sign(m);
    float s_ = pow(abs(s), 1.0 / 3.0) * sign(s);

    // LMS to OKLAB
    return float3(
        0.2104542553 * l_ + 0.7936177850 * m_ - 0.0040720468 * s_,  // L (lightness)
        1.9779984951 * l_ - 2.4285922050 * m_ + 0.4505937099 * s_,  // a (red-green)
        0.0259040371 * l_ + 0.7827717662 * m_ - 0.8086757660 * s_   // b (yellow-blue)
    );
}

// Convert OKLAB to linear sRGB color space
float3 oklab_to_linear_srgb(float3 c)
{
    // OKLAB to LMS
    float l_ = c.x + 0.3963377774 * c.y + 0.2158037573 * c.z;
    float m_ = c.x - 0.1055613458 * c.y - 0.0638541728 * c.z;
    float s_ = c.x - 0.0894841775 * c.y - 1.2914855480 * c.z;

    // Nonlinear transformation (cube)
    float l = l_ * l_ * l_;
    float m = m_ * m_ * m_;
    float s = s_ * s_ * s_;

    // LMS to RGB
    return float3(
        +4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
        -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
        -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s
    );
}

// Hue shift in OKLAB color space
// shift: 0-1 range (0 = no shift, 0.5 = 180 degrees, 1.0 = 360 degrees)
float4 OKLcolShift(float4 colorin, float shift)
{
    // Convert RGB to OKLAB
    float3 oklab = linear_srgb_to_oklab(colorin.rgb);

    // Extract chroma and hue from a and b components (polar coordinates)
    float chroma = sqrt(oklab.g * oklab.g + oklab.b * oklab.b);
    float hue = (chroma > 1e-6) ? atan2(oklab.b, oklab.g) : 0.0;

    // Shift hue
    hue += shift * (2.0 * PI);

    // Convert back to Cartesian coordinates
    float a = chroma * cos(hue);
    float b = chroma * sin(hue);

    // Convert OKLAB back to RGB
    float3 rgb = oklab_to_linear_srgb(float3(oklab.r, a, b));

    return float4(rgb, colorin.a);
}

// Adjust saturation in OKLAB space
float4 OKLsaturation(float4 colorin, float saturation)
{
    // Convert RGB to OKLAB
    float3 oklab = linear_srgb_to_oklab(colorin.rgb);

    // Scale chroma (saturation)
    oklab.g *= saturation;
    oklab.b *= saturation;

    // Convert back to RGB
    float3 rgb = oklab_to_linear_srgb(oklab);

    return float4(rgb, colorin.a);
}

// Adjust lightness in OKLAB space
float4 OKLlightness(float4 colorin, float lightness)
{
    // Convert RGB to OKLAB
    float3 oklab = linear_srgb_to_oklab(colorin.rgb);

    // Adjust L component
    oklab.r *= lightness;

    // Convert back to RGB
    float3 rgb = oklab_to_linear_srgb(oklab);

    return float4(rgb, colorin.a);
}

#endif // OKLAB_INCLUDED
