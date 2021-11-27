layout(local_size_x = 64, local_size_y = 1) in;

layout(std430, binding = 2) buffer rayOutBuffer
{
	Ray raysOut[];
};

layout(binding = 4) uniform atomic_uint rayCountIn;
layout(binding = 4, offset = 4) uniform atomic_uint rayCountOut;
layout(binding = 4, offset = 8) uniform atomic_uint shadowRayCount;


layout(std430, binding = 3) buffer shadowRayBuffer
{
	Ray shadowRays[];
};

//Light lights[] = Light[](
	//  Light(vec3(0.0, 12.0, 0.0), vec3(156.0, 156.0, 156.0))
	//, Light(vec3(0.0, -8.0, 0.0), vec3(2.0, 0.0, 16.0))
//);

vec3 lightPosition = vec3(0.0, 120.0, 0.0);
vec3 lightValue = vec3(6000.0, 6000.0, 6000.0);

void main() {
	uint iter = 0;
	uint totalRays = atomicCounter(rayCountIn);
	while (gl_GlobalInvocationID.x + (iter * 262144) < totalRays) {
		uint rayNum = gl_GlobalInvocationID.x + (iter * 262144);
		iter++;

		int primID = intersect(rays[rayNum]);	

		if (primID < 0) {
			continue;
		}
		
		vec3 normal = vec3(0.0);
		Material mat = Material(vec3(0.0), 1.0, 0.0);
		if (primID >= 20000) {
			mat = triangles[primID - 20000].mat;
			Triangle tri = triangles[primID - 20000];
			normal = normalize(cross(tri.b - tri.a, tri.c - tri.a));
			if (dot(rays[rayNum].dir, normal) > 0)
				normal = -normal;
		}
		else if (primID >= 10000) {
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
				rays[rayNum].energy * mat.specular,
				100000, rays[rayNum].pixelX, rays[rayNum].pixelY,
				vec3(0.0));
		}

		if (mat.diffuse > 0.0) {
			vec3 srOrigin = rays[rayNum].origin + rays[rayNum].dir * rays[rayNum].t + 0.001 * normal;
			float ndotl = max(dot(normalize(lightPosition - srOrigin), normal), 0);
			float distSq = dot(lightPosition - srOrigin, lightPosition - srOrigin);
			vec3 finalLight = ndotl * (lightValue / distSq);
			for (int i = 0; i < 3; i++) {
				finalLight[i] += 0.05;
			}

			vec3 diffuseEnergy = mat.diffuse * mat.color * finalLight;

			Ray shadowRay = Ray(
				srOrigin,
				normalize(lightPosition - srOrigin),
				diffuseEnergy * rays[rayNum].energy,
				length(lightPosition - srOrigin), rays[rayNum].pixelX, rays[rayNum].pixelY,
				mat.color * mat.diffuse * 0.05);
			shadowRays[atomicCounterIncrement(shadowRayCount)] = shadowRay;
		}
	}
}

/*
*/