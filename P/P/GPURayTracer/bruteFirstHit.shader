layout(local_size_x = 64, local_size_y = 1) in;

struct ShadowRay
{
	vec3 origin;
	vec3 dir;
	vec3 energy;
	float t;
	uint pixelX;
	uint pixelY;
};


layout(std430, binding = 2) buffer rayOutBuffer
{
	Ray raysOut[];
};

layout(binding = 4) uniform atomic_uint rayCountIn;
layout(binding = 4, offset = 4) uniform atomic_uint rayCountOut;
layout(binding = 4, offset = 8) uniform atomic_uint shadowRayCount;


layout(std430, binding = 3) buffer shadowRayBuffer
{
	ShadowRay shadowRays[];
};

//Light lights[] = Light[](
	//  Light(vec3(0.0, 12.0, 0.0), vec3(156.0, 156.0, 156.0))
	//, Light(vec3(0.0, -8.0, 0.0), vec3(2.0, 0.0, 16.0))
//);

vec3 lightPosition = vec3(0.0, 12.0, 0.0);
vec3 lightValue = vec3(70.0, 70.0, 70.0);

void main() {
	uint iter = 0;
	uint totalRays = atomicCounter(rayCountIn);
	while (gl_GlobalInvocationID.x + (iter * 262144) < totalRays) {
		uint rayNum = gl_GlobalInvocationID.x + (iter * 262144);
		iter++;

		int primID = intersect(rayNum);
		

		if (primID < 0) {
			continue;
		}

		
		vec3 normal = vec3(0.0);
		Material mat = Material(vec3(0.0), 1.0, 0.0);
		if (primID >= 10000) {
			mat = planes[primID - 10000].mat;
			normal = planes[primID - 10000].normal;
		}
		else {
			mat = spheres[primID].mat;
			normal = normalize((rays[rayNum].origin + rays[rayNum].dir * rays[rayNum].t) - spheres[primID].position);
		}

		if (mat.specular > 0.0) {
			float ndotr = -dot(normal, rays[rayNum].dir);
			raysOut[atomicCounterIncrement(rayCountOut)] = Ray(
				rays[rayNum].origin + rays[rayNum].dir * rays[rayNum].t + normal * 0.0001,
				rays[rayNum].dir + ndotr * 2 * normal,
				rays[rayNum].mul * mat.specular,
				100000, rays[rayNum].pixelX, rays[rayNum].pixelY);
		}

		if (mat.diffuse > 0.0) {
			vec3 srOrigin = rays[rayNum].origin + rays[rayNum].dir * rays[rayNum].t + 0.0001 * normal;
			float ndotl = max(dot(normalize(lightPosition - srOrigin), normal), 0);
			float distSq = dot(lightPosition - srOrigin, lightPosition - srOrigin);
			vec3 finalLight = ndotl * (lightValue / distSq);
			for (int i = 0; i < 3; i++) {
				finalLight[i] += 0.05;
			}

			vec3 diffuseEnergy = mat.diffuse * mat.color * finalLight;

			ShadowRay shadowRay = ShadowRay(
				srOrigin,
				normalize(lightPosition - srOrigin),
				diffuseEnergy * rays[rayNum].mul,
				length(lightPosition - srOrigin), rays[rayNum].pixelX, rays[rayNum].pixelY);
			shadowRays[atomicCounterIncrement(shadowRayCount)] = shadowRay;
		}
	}
}

/*
*/