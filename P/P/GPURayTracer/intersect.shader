struct Ray
{
	vec3 origin;
	vec3 dir;
	vec3 mul;
	float t;
	uint pixelX;
	uint pixelY;
};

layout(std430, binding = 1) buffer rayInBuffer
{
	Ray rays[];
};

struct Material
{
	vec3 color;
	float diffuse;
	float specular;
};

struct Sphere
{
	vec3 position;
	Material mat;
	float r2;
};

struct Light
{
	vec3 position;
	vec3 brightness;
};

Sphere spheres[] = Sphere[](
	  Sphere(vec3(-3.0, -1.5, 12.0), Material(vec3(1.0, 0.0, 0.0), 1.0, 0.0), 1.5 * 1.5)
	, Sphere(vec3(3.0, -1.5, 12.0), Material(vec3(1.0, 0.0, 0.0), 1.0, 0.0), 1.5 * 1.5)
	, Sphere(vec3(0.0, -1.5, 16.0), Material(vec3(0.0, 0.0, 1.0), 0.3, 0.7), 2)
);

struct Plane
{
	vec3 normal;
	float offset;
	Material mat;
};

Plane planes[] = Plane[](
	Plane(vec3(0.0, 1.0, 0.0), -3.0, Material(vec3(0.0, 1.0, 0.0), 1.0, 0.0))
);

int intersect(uint rayNum) {
	int primID = -1;
	for (int i = 0; i < spheres.length(); i++) {
		Sphere sphere = spheres[i];

		vec3 C = sphere.position - rays[rayNum].origin;
		float t = dot(C, rays[rayNum].dir);
		vec3 Q = C - (t * rays[rayNum].dir);
		float dsq = dot(Q, Q);

		if (dsq > sphere.r2)
			continue;
		t -= sqrt(sphere.r2 - dsq);

		if (t < 0) continue;
		if (t > rays[rayNum].t) continue;
		rays[rayNum].t = t;
		primID = i;
	}

	for (int i = 0; i < planes.length(); i++) {
		Plane plane = planes[i];

		float denom = dot(-plane.normal, rays[rayNum].dir);
		if (denom > 0.0001) {
			vec3 planeOrigin = plane.normal * plane.offset;
			vec3 diff = planeOrigin - rays[rayNum].origin;
			float t = dot(diff, -plane.normal) / denom;
			if (t < 0) {
				continue;
			}
			if (t > rays[rayNum].t) {
				continue;
			}
			rays[rayNum].t = t;
			primID = 10000 + i;
		}
	}
	return primID;
}