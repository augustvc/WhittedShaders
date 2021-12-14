using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenTK;

namespace P
{
    class TopLevelBVH
    {
        public float[] vertices;
        public BVH TopBVH;

        const int primsPerJob = 128;
        int desiredThreads = 24;

        Thread[] threads = new Thread[0];
        public static List<Action> jobs = new List<Action>();

        public TopLevelBVH(float[] vertices, uint[] indices)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            this.vertices = vertices;
            TopBVH = new BVH(vertices, indices);
            RecursiveSplit( TopBVH);

            threads = new Thread[desiredThreads];

            for (int i = 0; i < desiredThreads; i++)
            {
                threads[i] = new Thread(() =>
                {
                    while (jobs.Count > 0)
                    {
                        Action job;
                        lock (jobs)
                        {
                            if(jobs.Count == 0)
                            {
                                break;
                            }
                            job = jobs[jobs.Count - 1];
                            jobs.RemoveAt(jobs.Count - 1);
                        }
                        job();
                    }
                }
                );
                threads[i].Start();
            }
            for(int i = 0; i < desiredThreads; i++) {
                threads[i].Join();
            }

            sw.Stop();
            Console.WriteLine("BVH building duration in ms: " + sw.ElapsedMilliseconds);
        }

        void RecursiveSplit(BVH bvh)
        {
            int splitAxis = 0;
            float biggestRange = 0f;
            Vector3 range = bvh.AABBMax - bvh.AABBMin;
            for (var a = 0; a < 3; a++)
            {
                if (range[a] > biggestRange)
                {
                    biggestRange = range[a];
                    splitAxis = a;
                }
            }

            float middle = bvh.AABBMin[splitAxis] + 0.5f * range[splitAxis];


            int leftCount = 0;
            int rightCount = 0;
            bool[] wasLeft = new bool[bvh.triangleIndices.Length / 3];
            for (int i = 0; i < bvh.triangleIndices.Length; i += 3)
            {
                wasLeft[i / 3] = true;
                for (int j = i ; j < i + 3; j++)
                {
                    wasLeft[i / 3] = wasLeft[i / 3] && vertices[bvh.triangleIndices[j] * 8 + splitAxis] > middle;
                }
                if(wasLeft[i / 3])
                {
                    leftCount++;
                } else
                {
                    rightCount++;
                }
            }

            if (leftCount == 0 || rightCount == 0)
            {
                return;
            }

            uint[] leftIndices = new uint[leftCount * 3];
            uint[] rightIndices = new uint[rightCount * 3];

            int leftIterator = 0;
            int rightIterator = 0;

            for(int i = 0; i < bvh.triangleIndices.Length / 3; i ++)
            {
                if (wasLeft[i])
                {
                    leftIndices[leftIterator++] = bvh.triangleIndices[i * 3];
                    leftIndices[leftIterator++] = bvh.triangleIndices[i * 3+ 1];
                    leftIndices[leftIterator++] = bvh.triangleIndices[i * 3 + 2];
                }
                else
                {
                    rightIndices[rightIterator++] = bvh.triangleIndices[i * 3];
                    rightIndices[rightIterator++] = bvh.triangleIndices[i * 3 + 1];
                    rightIndices[rightIterator++] = bvh.triangleIndices[i * 3 + 2];
                }
            }
            //);

            BVH leftBVH = new BVH(vertices, leftIndices.ToArray());
            BVH rightBVH = new BVH(vertices, rightIndices.ToArray());

            if (leftBVH.SAH + rightBVH.SAH < bvh.SAH)
            {
                //Good split. Let's finalize it and try to split further
                bvh.leftChild = leftBVH;
                bvh.rightChild = rightBVH;
                bvh.isLeaf = false;

                if (leftCount < primsPerJob)
                {
                    RecursiveSplit( leftBVH);
                } else
                {
                    lock (jobs)
                    {
                        jobs.Add(new Action(() => RecursiveSplit( leftBVH)));
                    }
                }

                if (rightCount < primsPerJob)
                {
                    RecursiveSplit( rightBVH);
                } else
                {
                    lock (jobs)
                    {
                        jobs.Add(new Action(() => RecursiveSplit( rightBVH)));
                    }
                }
            }
        }
    }

    class BVH
    {
        public const int interval = 3000;
        public Vector3 AABBMin;
        public Vector3 AABBMax;
        public bool isLeaf = true;
        public BVH leftChild;
        public BVH rightChild;

        public float SAH;
        public uint[] triangleIndices;

        public BVH(float[] vertices, uint[] indices)
        {
            MakeAABB(vertices, indices, out AABBMin, out AABBMax);
            SAH = CalculateHalfSA(AABBMin, AABBMax) * (indices.Length + 5); // Static cost of intersecting AABB: add 5 to indices
            triangleIndices = indices;
        }

        public static float CalculateHalfSA(Vector3 AABBMin, Vector3 AABBMax)
        {
            Vector3 diff = AABBMax - AABBMin;
            return diff[0] * diff[1] + diff[0] * diff[2] + diff[1] * diff[2];
        }

        public void MakeAABB(float[] vertices, uint[] indices, out Vector3 AABBMin, out Vector3 AABBMax)
        {
            Vector3 AABBMinFinal = new Vector3(float.MaxValue);
            Vector3 AABBMaxFinal = new Vector3(float.MinValue);

            int minLock = 0;
            int maxLock = 0;


            void updateAABBs(int i)
            {
                Vector3 AABBMinTemp = new Vector3(float.MaxValue);
                Vector3 AABBMaxTemp = new Vector3(float.MinValue);

                int baseStart = i * interval;
                for (int j = baseStart; (j < baseStart + interval) && j < indices.Length; j++)
                {
                    for (var a = 0; a < 3; a++)
                    {
                        if (vertices[indices[j] * 8 + a] < AABBMinTemp[a])
                        {
                            AABBMinTemp[a] = vertices[indices[j] * 8 + a];
                        }
                        if (vertices[indices[j] * 8 + a] > AABBMaxTemp[a])
                        {
                            AABBMaxTemp[a] = vertices[indices[j] * 8 + a];
                        }
                    }
                }

                while(Interlocked.Exchange(ref minLock, 1) == 1)
                {
                }
                AABBMinFinal = Vector3.ComponentMin(AABBMinFinal, AABBMinTemp);
                minLock = 0;

                while(Interlocked.Exchange(ref maxLock, 1) == 1)
                {
                }
                AABBMaxFinal = Vector3.ComponentMax(AABBMaxFinal, AABBMaxTemp);
                maxLock = 0;
            }

            if(TopLevelBVH.jobs.Count == 0 && indices.Length >= interval * 8)
            {
                Parallel.For(0, (indices.Length / interval) + 1, (i) => updateAABBs(i));
            } else
            {
                for (int i = 0; i < indices.Length; i++)
                {
                    for (var a = 0; a < 3; a++)
                    {
                        if (vertices[indices[i] * 8 + a] < AABBMinFinal[a])
                        {
                            AABBMinFinal[a] = vertices[indices[i] * 8 + a];
                        }
                        if (vertices[indices[i] * 8 + a] > AABBMaxFinal[a])
                        {
                            AABBMaxFinal[a] = vertices[indices[i] * 8 + a];
                        }
                    }
                }
            }

            AABBMin = AABBMinFinal;
            AABBMax = AABBMaxFinal;
        }

        public void nearestIntersection(Ray ray, float[] vertices)
        {
            ray.bvhChecks++;

            if(isLeaf)
            {
                for(int i = 0; i < triangleIndices.Length; i+=3)
                {
                    Vector3 a = new Vector3(vertices[triangleIndices[i] * 8], vertices[triangleIndices[i] * 8 + 1], vertices[triangleIndices[i] * 8 + 2]);
                    Vector3 b = new Vector3(vertices[triangleIndices[i + 1] * 8], vertices[triangleIndices[i + 1] * 8 + 1], vertices[triangleIndices[i + 1] * 8 + 2]);
                    Vector3 c = new Vector3(vertices[triangleIndices[i + 2] * 8], vertices[triangleIndices[i + 2] * 8 + 1], vertices[triangleIndices[i + 2] * 8 + 2]);

                    Vector3 ab = b - a;
                    Vector3 ac = c - a;
                    Vector3 cross1 = Vector3.Cross(ray.Direction, ac);
                    float det = Vector3.Dot(ab, cross1);
                    if (Math.Abs(det) < 0.0001)
                        continue;

                    float detInv = 1.0f / det;
                    Vector3 diff = ray.Origin - a;
                    float u = Vector3.Dot(diff, cross1) * detInv;
                    if (u < 0 || u > 1)
                    {
                        continue;
                    }

                    Vector3 cross2 = Vector3.Cross(diff, ab);
                    float v = Vector3.Dot(ray.Direction, cross2) * detInv;
                    if (v < 0 || v > 1)
                        continue;

                    if (u + v > 1)
                        continue;
                    float t = Vector3.Dot(ac, cross2) * detInv;
                    if (t <= 0)
                        continue;

                    if (t < ray.t)
                    {
                        ray.t = t;
                        ray.triangleHit = new Vector3[] { a, b, c };
                        ray.objectHit = -1;
                    }
                }
            } else
            {
                float leftD = leftChild.rayAABB(ray);
                float rightD = rightChild.rayAABB(ray);
                if (leftD < rightD)
                {
                    if (leftD < ray.t)
                    {
                        leftChild.nearestIntersection(ray, vertices);
                    }
                    if (rightD < ray.t)
                    {
                        rightChild.nearestIntersection(ray, vertices);
                    }
                } else
                {
                    if (rightD < ray.t)
                    {
                        rightChild.nearestIntersection(ray, vertices);
                    }
                    if (leftD < ray.t)
                    {
                        leftChild.nearestIntersection(ray, vertices);
                    }
                }
            }
        }

        public float rayAABB(Ray ray)
        {
            // Ray-AABB intersection code taken from https://www.scratchapixel.com/lessons/3d-basic-rendering/minimal-ray-tracer-rendering-simple-shapes/ray-box-intersection
            Vector3 inverseD = new Vector3(float.MaxValue);
            int[] signD = new int[3];
            for (int a = 0; a < 3; a++)
            {
                if (ray.Direction[a] > 0.000001f)
                {
                    inverseD[a] = 1f / ray.Direction[a];
                }
                else if (ray.Direction[a] < -0.000001f)
                {
                    inverseD[a] = 1f / ray.Direction[a];
                }

                signD[a] = 0;
                if (inverseD[a] > 0f)
                {
                    signD[a] = 1;
                }
            }

            Vector3[] bounds = new Vector3[] { AABBMax, AABBMin };

            float tmin = (bounds[signD[0]].X - ray.Origin.X) * inverseD.X;
            float tmax = (bounds[1 - signD[0]].X - ray.Origin.X) * inverseD.X;
            float tymin = (bounds[signD[1]].Y - ray.Origin.Y) * inverseD.Y;
            float tymax = (bounds[1 - signD[1]].Y - ray.Origin.Y) * inverseD.Y;

            if ((tmin > tymax) || (tymin > tmax))
                return float.MaxValue;
            if (tymin > tmin)
                tmin = tymin;
            if (tymax < tmax)
                tmax = tymax;

            float tzmin = (bounds[signD[2]].Z - ray.Origin.Z) * inverseD.Z;
            float tzmax = (bounds[1 - signD[2]].Z - ray.Origin.Z) * inverseD.Z;

            if ((tmin > tzmax) || (tzmin > tmax))
                return float.MaxValue;
            if (tzmin > tmin)
                tmin = tzmin;
            if (tzmax < tmax)
                tmax = tzmax;

            return tmin;
        }
    }
}
