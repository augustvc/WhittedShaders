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

        const int primsPerJob = 256;
        int desiredThreads = 24;

        Thread[] threads = new Thread[0];
        public static List<Action> jobs = new List<Action>();

        const float constantCost = 5f;

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
            int binCount = 8;

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

            int[] bins = new int[binCount];
            Vector3[] binMins = new Vector3[binCount];
            Vector3[] binMaxes = new Vector3[binCount];
            for (int i = 0; i < binCount; i++)
            {
                binMins[i] = new Vector3(float.MaxValue);
                binMaxes[i] = new Vector3(float.MinValue);
            }

            float binNumberMult = ((float)binCount) / (biggestRange * 1.0001f);

            for (int i = 0; i < bvh.triangleIndices.Length; i += 3)
            {
                int binNr = 0;
                for (int j = i ; j < i + 3; j++)
                {
                    int newBinNr = (int)((vertices[bvh.triangleIndices[j] * 8 + splitAxis] - bvh.AABBMin[splitAxis]) * binNumberMult);
                    if (newBinNr > binNr) {
                        binNr = newBinNr;
                    }
                }
                bins[binNr]++;
                for(int j = i; j < i + 3; j++)
                {
                    for(int a = 0; a < 3; a++)
                    {
                        if (vertices[bvh.triangleIndices[j] * 8 + a] < binMins[binNr][a]) {
                            binMins[binNr][a] = vertices[bvh.triangleIndices[j] * 8 + a];
                        }
                        if (vertices[bvh.triangleIndices[j] * 8 + a] > binMaxes[binNr][a])
                        {
                            binMaxes[binNr][a] = vertices[bvh.triangleIndices[j] * 8 + a];
                        }
                    }
                }
            }

            Vector3 leftMins = binMins[0];
            Vector3 leftMaxes = binMaxes[0];
            Vector3 rightMins = binMins[binCount - 1];
            Vector3 rightMaxes = binMaxes[binCount - 1];
            float SAHLeft = BVH.CalculateHalfSA(leftMins, leftMaxes) * (bins[0] + constantCost);
            float SAHRight = BVH.CalculateHalfSA(rightMins, rightMaxes) * (bins[binCount - 1] + constantCost);
            int leftPrims = bins[0];
            int rightPrims = bins[binCount - 1];

            bool[] binIsLeft = new bool[binCount];
            binIsLeft[0] = true;
            binIsLeft[binCount - 1] = false;

            bool highBin = false;
            for(int i = 2; i < binCount; i++)
            {
                int j = i / 2;
                if (highBin)
                {
                    j = (binCount-1) - j;
                }
                float changedSALeft = BVH.CalculateHalfSA(Vector3.ComponentMin(leftMins, binMins[j]), Vector3.ComponentMax(leftMaxes, binMaxes[j]));
                float changedSARight = BVH.CalculateHalfSA(Vector3.ComponentMin(rightMins, binMins[j]), Vector3.ComponentMax(rightMaxes, binMaxes[j]));
                float changedSAHLeft = changedSALeft * (leftPrims + bins[j] + constantCost);
                float changedSAHRight = changedSARight * (rightPrims + bins[j] + constantCost);

                if((changedSAHLeft - SAHLeft) < (changedSAHRight - SAHRight))
                {
                    leftMins = Vector3.ComponentMin(leftMins, binMins[j]);
                    leftMaxes = Vector3.ComponentMax(leftMaxes, binMaxes[j]);
                    leftPrims = leftPrims + bins[j];
                    SAHLeft = changedSAHLeft;
                    binIsLeft[j] = true;
                }
                else
                {
                    rightMins = Vector3.ComponentMin(rightMins, binMins[j]);
                    rightMaxes = Vector3.ComponentMax(rightMaxes, binMaxes[j]);
                    rightPrims = rightPrims + bins[j];
                    SAHRight = changedSAHRight;
                    binIsLeft[j] = false;
                }
                highBin = !highBin;
            }

            if (leftPrims == 0 || rightPrims == 0)
            {
                return;
            }

            if (SAHLeft + SAHRight >= bvh.SAH)
            {
                return;
            }
            SAHLeft *= ((float)leftPrims - constantCost) / leftPrims;
            SAHRight *= ((float)rightPrims - constantCost) / rightPrims;


            uint[] leftIndices = new uint[leftPrims * 3];
            uint[] rightIndices = new uint[rightPrims * 3];

            int leftIterator = 0;
            int rightIterator = 0;

            for (int i = 0; i < bvh.triangleIndices.Length; i+=3)
            {
                int binNr = 0;
                for (int j = i; j < i + 3; j++)
                {
                    int newBinNr = (int)((vertices[bvh.triangleIndices[j] * 8 + splitAxis] - bvh.AABBMin[splitAxis]) * binNumberMult);
                    if (newBinNr > binNr)
                    {
                        binNr = newBinNr;
                    }
                }

                if (binIsLeft[binNr])
                {
                    leftIndices[leftIterator++] = bvh.triangleIndices[i];
                    leftIndices[leftIterator++] = bvh.triangleIndices[i + 1];
                    leftIndices[leftIterator++] = bvh.triangleIndices[i + 2];
                }
                else
                {
                    rightIndices[rightIterator++] = bvh.triangleIndices[i ];
                    rightIndices[rightIterator++] = bvh.triangleIndices[i + 1];
                    rightIndices[rightIterator++] = bvh.triangleIndices[i + 2];
                }
            }

            BVH leftBVH = new BVH(leftIndices, leftMins, leftMaxes, SAHLeft);
            BVH rightBVH = new BVH(rightIndices, rightMins, rightMaxes, SAHRight);

            bvh.leftChild = leftBVH;
            bvh.rightChild = rightBVH;
            bvh.isLeaf = false;

            if (leftPrims < primsPerJob)
            {
                RecursiveSplit( leftBVH);
            } else
            {
                lock (jobs)
                {
                    jobs.Add(new Action(() => RecursiveSplit( leftBVH)));
                }
            }

            if (rightPrims < primsPerJob)
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
            SAH = CalculateHalfSA(AABBMin, AABBMax) * (indices.Length * 0.3333f); // Static cost of intersecting AABB: add 5 to indices
            triangleIndices = indices;
        }
        
        public BVH(uint[] indices, Vector3 AABBMin, Vector3 AABBMax, float SAH)
        {
            this.AABBMin = AABBMin;
            this.AABBMax = AABBMax;
            this.SAH = SAH;
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
                    if (leftD < ray.t && leftD >= 0f)
                    {
                        leftChild.nearestIntersection(ray, vertices);
                    }
                    if (rightD < ray.t && rightD >= 0f)
                    {
                        rightChild.nearestIntersection(ray, vertices);
                    }
                } else
                {
                    if (rightD < ray.t && rightD >= 0f)
                    {
                        rightChild.nearestIntersection(ray, vertices);
                    }
                    if (leftD < ray.t && leftD >= 0f)
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

            if(tmin < 0f && tmax >= 0f)
            {
                return 0f;
            }
            return tmin;
        }
    }
}
