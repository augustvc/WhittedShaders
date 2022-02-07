/*
Copyright 2022 August van Casteren & Shreyes Jishnu Suchindran

You may use this software freely for non-commercial purposes. For any commercial purpose, please contact the authors.

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
layout(local_size_x = 64, local_size_y = 1) in;
layout(rgba32f, binding = 0) uniform image2D img_output;

struct Ray
{
	vec3 origin;
	vec3 dir;
	vec3 invdir;
	vec3 energy;
	float t;
	uint pixelX;
	uint pixelY;
	vec3 ambient;
	int primID;
	uint matrixID;
};

layout(std430, binding = 3) buffer shadowRayBuffer
{
	Ray shadowRays[];
};

layout(binding = 4, offset = 8) uniform atomic_uint shadowRayCount;

void main() {
	Ray ray;

	uint totalRays = atomicCounter(shadowRayCount);
	uint iter = 0;
	while (gl_GlobalInvocationID.x + (iter * 262144) < totalRays) {
		ray = shadowRays[gl_GlobalInvocationID.x + (iter * 262144)];

		iter++;
		//intersect(ray);

		vec4 pixel = vec4(max(ray.ambient, ray.energy), 1.0);
		if (ray.primID >= 0) {
			pixel = vec4(ray.ambient, 1.0);
			//pixel = vec4(0.0, 1.0, 0.0, 1.0);
		}

		ivec2 pixel_coords = ivec2(ray.pixelX, ray.pixelY);
		vec4 clr = imageLoad(img_output, pixel_coords);
		imageStore(img_output, pixel_coords, clr + pixel);
	}
}