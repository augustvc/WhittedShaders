/*
Copyright 2022 August van Casteren & Shreyes Jishnu Suchindran

You may use this software freely for non-commercial purposes. For any commercial purpose, please contact the authors.

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
layout(local_size_x = 64, local_size_y = 1) in;

struct Ray
{
	vec3 origin;
	vec3 dir;
	vec3 energy;
	float t;
	uint pixelX;
	uint pixelY;
	vec3 ambient;
	int primID;
	uint matrixID;
};

layout(std430, binding = 1) buffer rayInBuffer
{
	Ray rays[];
};

layout(std430, binding = 2) buffer rayOutBuffer
{
	Ray raysOut[];
};

layout(std430, binding = 3) buffer shadowRayBuffer
{
	Ray shadowRays[];
};

layout(std430, binding = 5) buffer vertexBufferObj
{
	float vertexBuffer[];
};

layout(std430, binding = 6) buffer indexBufferObj
{
	uint indexBuffer[];
};

struct Material
{
	float r;
	float g;
	float b;
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

struct Triangle
{
	vec3 a, b, c;
	Material mat;
};

vec3 lightPosition = vec3(0.0, 1200000.0, 0.0);
vec3 lightValue = vec3(2000000000000.0, 2000000000000.0, 2000000000000.0);

struct Plane
{
	vec3 normal;
	float offset;
	Material mat;
};

Plane planes[] = Plane[](
	Plane(vec3(0.0, 1.0, 0.0), -3.0, Material(1.0, 1.0, 1.0, 1.0, 0.0))
	);

layout(binding = 4) uniform atomic_uint rayCountIn;
layout(binding = 4, offset = 4) uniform atomic_uint rayCountOut;
layout(binding = 4, offset = 8) uniform atomic_uint shadowRayCount;
layout(binding = 4, offset = 12) uniform atomic_uint intersectionJob;

layout(location = 2) uniform int normalsOffset;

layout(location = 5) uniform mat4 transform;

layout(std430, binding = 8) buffer MatrixBuffer
{
	mat4 matrices[];
};

layout(std430, binding = 9) buffer MaterialBuffer
{
	Material materials[];
};

void main() {
	uint rayNum;
	uint totalRays = atomicCounter(rayCountIn);
	uint iter = 0;
	while (gl_GlobalInvocationID.x + (iter * 262144) < totalRays) {
		rayNum = gl_GlobalInvocationID.x + (iter * 262144);
		iter++;

		int primID = rays[rayNum].primID;

		if (primID < 0) {
			continue;
		}

		vec3 normal = vec3(0.0);
		Material mat = Material(0.0, 0.0, 0.0, 1.0, 0.0);
		if (primID >= 0) {
			//Triangle from a mesh
			mat = materials[rays[rayNum].matrixID];
			uint triAI = indexBuffer[primID++];
			uint triBI = indexBuffer[primID++];
			uint triCI = indexBuffer[primID++];

			vec3 triA = vec3(vertexBuffer[triAI * 3], vertexBuffer[triAI * 3 + 1], vertexBuffer[triAI * 3 + 2]);
			vec3 triB = vec3(vertexBuffer[triBI * 3], vertexBuffer[triBI * 3 + 1], vertexBuffer[triBI * 3 + 2]);
			vec3 triC = vec3(vertexBuffer[triCI * 3], vertexBuffer[triCI * 3 + 1], vertexBuffer[triCI * 3 + 2]);
			
			triA = (matrices[rays[rayNum].matrixID] * vec4(triA, 1)).xyz;
			triB = (matrices[rays[rayNum].matrixID] * vec4(triB, 1)).xyz;
			triC = (matrices[rays[rayNum].matrixID] * vec4(triC, 1)).xyz;
			
			vec3 ab = triB - triA;
			vec3 ac = triC - triA;

			vec3 cross1 = cross(rays[rayNum].dir, ac);
			float det = dot(ab, cross1);

			float detInv = 1.0 / det;
			vec3 diff = rays[rayNum].origin - triA;
			float u = dot(diff, cross1) * detInv;

			vec3 cross2 = cross(diff, ab);
			float v = dot(rays[rayNum].dir, cross2) * detInv;

			normal = normalize(
				(1.0 - u - v) * vec3(vertexBuffer[normalsOffset + triAI * 3], vertexBuffer[normalsOffset + triAI * 3 + 1], vertexBuffer[normalsOffset + triAI * 3 + 2]) +
				u * vec3(vertexBuffer[normalsOffset + triBI * 3], vertexBuffer[normalsOffset + triBI * 3 + 1], vertexBuffer[normalsOffset + triBI * 3 + 2]) +
				v * vec3(vertexBuffer[normalsOffset + triCI * 3], vertexBuffer[normalsOffset + triCI * 3 + 1], vertexBuffer[normalsOffset + triCI * 3 + 2])
			);

			mat3 dir_matrix = transpose(inverse(mat3(matrices[rays[rayNum].matrixID])));
			normal = normalize(dir_matrix * normal);
		}

		//Avoid shadow acne:
		float epsilon = (length(rays[rayNum].origin) + 1.0) * 0.001;
		vec3 newOrigin = rays[rayNum].origin + rays[rayNum].dir * rays[rayNum].t + epsilon * normal;
		
		if (mat.specular > 0.0) {
			float ndotr = -dot(normal, rays[rayNum].dir);
			if (ndotr > 0) {
				raysOut[atomicCounterIncrement(rayCountOut)] = Ray(
					newOrigin,
					rays[rayNum].dir + ndotr * 2 * normal,
					rays[rayNum].energy * mat.specular * vec3(mat.r, mat.g, mat.b),
					100000000, rays[rayNum].pixelX, rays[rayNum].pixelY,
					vec3(0.0),
					-1,
					-1);
			}
		}

		if (mat.diffuse > 0.0) {
			float ndotl = max(dot(normalize(lightPosition - newOrigin), normal), 0);
			float distSq = dot(lightPosition - newOrigin, lightPosition - newOrigin);
			vec3 finalLight = ndotl * (lightValue / distSq);
			for (int i = 0; i < 3; i++) {
				finalLight[i] += 0.05;
			}

			vec3 diffuseEnergy = mat.diffuse * vec3(mat.r, mat.g, mat.b) * finalLight;

			vec3 srdir = normalize(lightPosition - newOrigin);
			Ray shadowRay = Ray(
				newOrigin,
				srdir,
				diffuseEnergy * rays[rayNum].energy,
				length(lightPosition - newOrigin), rays[rayNum].pixelX, rays[rayNum].pixelY,
				vec3(mat.r, mat.g, mat.b) * mat.diffuse * 0.05,
				-1,
				primID);
			shadowRays[atomicCounterIncrement(shadowRayCount)] = shadowRay;
		}
	}
}