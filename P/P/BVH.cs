using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenTK;
using P;

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

            SlowSplit(TopBVH);
            //RecursiveSplit(TopBVH, 8);
            Console.WriteLine("leftchild of root: " + TopBVH.leftChild.SAH);
            Console.WriteLine("rightchild of root: " + TopBVH.rightChild.SAH);


            //BVH TopBVH2 = new BVH(vertices, indices);
            //RecursiveSplit(TopBVH2, 31);

            //Console.WriteLine("Top SAH  : " + TopBVH.SAH);
            //Console.WriteLine("Top SAH 2: " + TopBVH2.SAH);

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
                            if (jobs.Count == 0)
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
            for (int i = 0; i < desiredThreads; i++) {
                threads[i].Join();
            }

            sw.Stop();
            Console.WriteLine("BVH building duration in ms: " + sw.ElapsedMilliseconds);

        }

        static float max(float a, float b, float c)
        {
            if (a > b && a > c)
                return a;
            if (b > c)
                return b;
            return c;
        }

        static float[] usedVertices;
        static public int usedAxis = 0;
        struct Tri : IComparable<Tri>
        {
            public uint A;
            public uint B;
            public uint C;
            public Tri(uint a, uint b, uint c)
            {
                A = a; B = b; C = c;
            }

            public int CompareTo(Tri other)
            {
                float A1 = usedVertices[A * 3 + usedAxis];
                float B1 = usedVertices[B * 3 + usedAxis];
                float C1 = usedVertices[C * 3 + usedAxis];
                float largest1 = max(A1, B1, C1);

                float A2 = usedVertices[other.A * 3 + usedAxis];
                float B2 = usedVertices[other.B * 3 + usedAxis];
                float C2 = usedVertices[other.C * 3 + usedAxis];
                float largest2 = max(A2, B2, C2);

                if (largest1 > largest2)
                    return 1;
                if (largest1 < largest2)
                    return -1;
                return 0;
            }
        }

        bool firstSlowSplit = true;
        const int desiredTris = 8;
        void SlowSplit(BVH bvh)
        {
            if(firstSlowSplit)
            {
                Console.WriteLine("Performing SlowSplit function. This might take a while (10s on my machine)..... Consider replacing with RecursiveSplit if it's too slow");
                firstSlowSplit = false;
            }
            if(bvh.triangleIndices.Length <= desiredTris * 3)
            {
                bvh.isLeaf = true;
                return;
            }

            Tri[] initialTris = new Tri[bvh.triangleIndices.Length / 3];
            for (int i = 0; i < bvh.triangleIndices.Length; i+=3)
            {
                initialTris[i / 3] = new Tri(bvh.triangleIndices[i], bvh.triangleIndices[i + 1], bvh.triangleIndices[i + 2]);
            }

            float bestScore = float.MaxValue;
            Tri[] bestTris = initialTris;
            int bestI = 0;

            for (usedAxis = 0; usedAxis < 3; usedAxis++)
            {
                usedVertices = vertices;
                Tri[] tris = new Tri[initialTris.Length];

                initialTris.CopyTo(tris, 0);
                Array.Sort(tris);

                Vector3 MinL = new Vector3(float.MaxValue);
                Vector3 MaxL = new Vector3(float.MinValue);
                Vector3 MinR = new Vector3(float.MaxValue);
                Vector3 MaxR = new Vector3(float.MinValue);

                void TriMinMax(Tri tri, ref Vector3 min, ref Vector3 max)
                {
                    min = Vector3.ComponentMin(min, new Vector3(vertices[tri.A * 3], vertices[tri.A * 3 + 1], vertices[tri.A * 3 + 2]));
                    min = Vector3.ComponentMin(min, new Vector3(vertices[tri.B * 3], vertices[tri.B * 3 + 1], vertices[tri.B * 3 + 2]));
                    min = Vector3.ComponentMin(min, new Vector3(vertices[tri.C * 3], vertices[tri.C * 3 + 1], vertices[tri.C * 3 + 2]));
                    
                    max = Vector3.ComponentMax(max, new Vector3(vertices[tri.A * 3], vertices[tri.A * 3 + 1], vertices[tri.A * 3 + 2]));
                    max = Vector3.ComponentMax(max, new Vector3(vertices[tri.B * 3], vertices[tri.B * 3 + 1], vertices[tri.B * 3 + 2]));
                    max = Vector3.ComponentMax(max, new Vector3(vertices[tri.C * 3], vertices[tri.C * 3 + 1], vertices[tri.C * 3 + 2]));
                }

                float[] leftScores = new float[tris.Length];
                float[] rightScores = new float[tris.Length];
                Vector3[] minLs = new Vector3[tris.Length];
                Vector3[] maxLs = new Vector3[tris.Length];
                Vector3[] minRs = new Vector3[tris.Length];
                Vector3[] maxRs = new Vector3[tris.Length];

                float scorePrims(int primAmnt)
                {
                    int retval = ((7+primAmnt) / 8) * 8;
                    return retval;
                }

                for (int i = 0; i < tris.Length; i++)
                {
                    TriMinMax(tris[i], ref MinL, ref MaxL);
                    minLs[i] = MinL;
                    maxLs[i] = MaxL;
                    leftScores[i] = BVH.CalculateHalfSA(MinL, MaxL) * (i+1);
                }
                int rightsize = 0;
                for (int i = tris.Length - 1; i >= 0; i--)
                {
                    rightScores[i] = BVH.CalculateHalfSA(MinR, MaxR) * (rightsize);
                    TriMinMax(tris[i], ref MinR, ref MaxR);
                    minRs[i] = MinR;
                    maxRs[i] = MaxR;
                    rightsize++;
                }
                for (int i = 0; i < tris.Length; i++)
                {
                    if (leftScores[i] + rightScores[i] < bestScore)
                    {
                        bestScore = leftScores[i] + rightScores[i];
                        bestI = i;
                        bestTris = tris;
                    }
                }
            }

            List<uint> leftIndices = new List<uint>();
            List<uint> rightIndices = new List<uint>();
            for(int i = 0; i <= bestI; i++)
            {
                leftIndices.Add(bestTris[i].A);
                leftIndices.Add(bestTris[i].B);
                leftIndices.Add(bestTris[i].C);
            }
            for (int i = bestI + 1; i < bestTris.Length; i++)
            {
                rightIndices.Add(bestTris[i].A);
                rightIndices.Add(bestTris[i].B);
                rightIndices.Add(bestTris[i].C);
            }

            BVH bestLeft = new BVH(vertices, leftIndices.ToArray());
            BVH bestRight = new BVH(vertices, rightIndices.ToArray());

            if (bestLeft.triangleIndices.Length == 0 || bestRight.triangleIndices.Length == 0)
            {
                bvh.isLeaf = true;
                return;
            }

            bvh.isLeaf = false;
            bvh.leftChild = bestLeft;
            bvh.rightChild = bestRight;

            SlowSplit(bvh.leftChild);
            SlowSplit(bvh.rightChild);
        }

        void RecursiveSplit(BVH bvh, int binCount)
        {
            if(bvh.triangleIndices.Length <= desiredTris * 3)
            {
                return;
            }

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
                    int newBinNr = (int)((vertices[bvh.triangleIndices[j] * 3 + splitAxis] - bvh.AABBMin[splitAxis]) * binNumberMult);
                    if (newBinNr > binNr) {
                        binNr = newBinNr;
                    }
                }
                bins[binNr]++;
                for(int j = i; j < i + 3; j++)
                {
                    for(int a = 0; a < 3; a++)
                    {
                        if (vertices[bvh.triangleIndices[j] * 3 + a] < binMins[binNr][a]) {
                            binMins[binNr][a] = vertices[bvh.triangleIndices[j] * 3 + a];
                        }
                        if (vertices[bvh.triangleIndices[j] * 3 + a] > binMaxes[binNr][a])
                        {
                            binMaxes[binNr][a] = vertices[bvh.triangleIndices[j] * 3 + a];
                        }
                    }
                }
            }

            Vector3 leftMins = binMins[0];
            Vector3 leftMaxes = binMaxes[0];
            Vector3 rightMins = binMins[binCount - 1];
            Vector3 rightMaxes = binMaxes[binCount - 1];



            float SAHLeft = BVH.CalculateHalfSA(leftMins, rightMaxes) * (bins[binCount - 1] + constantCost);
            float SAHRight = BVH.CalculateHalfSA(rightMins,rightMaxes)*(bins[binCount-2]+constantCost);
   
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

                
                float changedSAHLeft = changedSALeft * ((float)(leftPrims + bins[j]) + constantCost);
                float changedSAHRight = changedSARight * ((float)(rightPrims + bins[j]) + constantCost);


               if ((changedSAHLeft - SAHLeft) < (changedSAHRight - SAHRight))
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

            if (leftPrims == 0 || rightPrims == 0 )
            {
                return;
            }

            if (SAHLeft + SAHRight  >= bvh.SAH)
            {
                if (bvh.triangleIndices.Length < desiredTris * 3)
                {
                    return;
                }
            }
            SAHLeft *= ((float)(leftPrims) - constantCost) / ((float)leftPrims);
            SAHRight *= ((float)(rightPrims) - constantCost) / ((float)rightPrims);
           


            uint[] leftIndices = new uint[leftPrims * 3];
            uint[] rightIndices = new uint[rightPrims * 3];

            int leftIterator = 0;
            int rightIterator = 0;


            for (int i = 0; i < bvh.triangleIndices.Length; i+=3)
            {
                int binNr = 0;
                for (int j = i; j < i + 3; j++)
                {
                    int newBinNr = (int)((vertices[bvh.triangleIndices[j] * 3 + splitAxis] - bvh.AABBMin[splitAxis]) * binNumberMult);
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
                    rightIndices[rightIterator++] = bvh.triangleIndices[i];
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
                RecursiveSplit( leftBVH, binCount);
            } else
            {
                lock (jobs)
                {
                    jobs.Add(new Action(() => RecursiveSplit( leftBVH, binCount)));
                }
            }

            if (rightPrims < primsPerJob)
            {
                RecursiveSplit( rightBVH, binCount);
            } else
            {
                lock (jobs)
                {
                    jobs.Add(new Action(() => RecursiveSplit( rightBVH, binCount)));
                }
            }

        }
    }
    



}

class FourWayBVH
{
    public const int interval = 3000;
    public Vector3 AABBMin;
    public Vector3 AABBMax;
    public bool isLeaf = true;

    public FourWayBVH[] fourwayChildren;
    public FourWayBVH leftleft;
    public FourWayBVH leftright;
    public FourWayBVH rightleft;
    public FourWayBVH rightright;
    public bool isLeafParent = false;

    public float SAH =0f;
    public uint[] triangleIndices;
    

    public FourWayBVH()
    {
        this.AABBMax = new Vector3(float.MinValue);
        this.AABBMin = new Vector3(float.MaxValue);
        isLeaf = true;
        triangleIndices = new uint[0];
    }

    public FourWayBVH(BVH oldVersion)
    {
        this.AABBMax = oldVersion.AABBMax;
        this.AABBMin = oldVersion.AABBMin;
        this.SAH = oldVersion.SAH;
        
        if (oldVersion.isLeaf)
        {
            this.isLeaf = true;
            this.triangleIndices = oldVersion.triangleIndices;
            return;
        }
        triangleIndices = new uint[0];
        this.isLeaf = false;
        if (!oldVersion.leftChild.isLeaf)
        {
            
            //leftleft
            leftleft = new FourWayBVH(oldVersion.leftChild.leftChild);
            leftleft.SAH = oldVersion.leftChild.leftChild.SAH;
            leftleft.triangleIndices = oldVersion.leftChild.leftChild.triangleIndices;
            leftleft.AABBMax = oldVersion.leftChild.leftChild.AABBMax;
            leftleft.AABBMin = oldVersion.leftChild.leftChild.AABBMin;
            

            //leftright
            leftright = new FourWayBVH(oldVersion.leftChild.rightChild);
            leftright.SAH = oldVersion.leftChild.rightChild.SAH;
            leftright.triangleIndices = oldVersion.leftChild.rightChild.triangleIndices;
            leftright.AABBMax = oldVersion.leftChild.rightChild.AABBMax;
            leftright.AABBMin = oldVersion.leftChild.rightChild.AABBMin;
        }
        else
        {
            
            leftleft = new FourWayBVH(oldVersion.leftChild);
            leftright = new FourWayBVH();
        }

        if (!oldVersion.rightChild.isLeaf)
        {
            
            //rightleft
            rightleft = new FourWayBVH(oldVersion.rightChild.leftChild);
            rightleft.SAH = oldVersion.rightChild.leftChild.SAH;
            rightleft.triangleIndices = oldVersion.rightChild.leftChild.triangleIndices;
            rightleft.AABBMax = oldVersion.rightChild.leftChild.AABBMax;
            rightleft.AABBMin = oldVersion.rightChild.leftChild.AABBMin;


            //rightright
            rightright = new FourWayBVH(oldVersion.rightChild.rightChild);
            rightright.SAH = oldVersion.rightChild.rightChild.SAH;
            rightright.triangleIndices = oldVersion.rightChild.rightChild.triangleIndices;
            rightright.AABBMax = oldVersion.rightChild.rightChild.AABBMax;
            rightright.AABBMin = oldVersion.rightChild.rightChild.AABBMin;
        }
        else
        {
            
            rightleft = new FourWayBVH(oldVersion.rightChild);
            rightright = new FourWayBVH();
        }
        //Console.WriteLine("fourway created");
        
        fourwayChildren = new FourWayBVH[] { leftleft, leftright, rightleft, rightright };
    }

    public void nearestIntersection(Ray ray, float[] vertices)
    {
        ray.bvhChecks++;
        if (isLeaf)
        {
            //if (ray.debuggingRay) Console.WriteLine("Leaf");
            for (int i = 0; i < triangleIndices.Length; i += 3)
            {
                
                Vector3 a = new Vector3(vertices[triangleIndices[i] * 3], vertices[triangleIndices[i] * 3 + 1], vertices[triangleIndices[i] * 3 + 2]);
                Vector3 b = new Vector3(vertices[triangleIndices[i + 1] * 3], vertices[triangleIndices[i + 1] * 3 + 1], vertices[triangleIndices[i + 1] * 3 + 2]);
                Vector3 c = new Vector3(vertices[triangleIndices[i + 2] * 3], vertices[triangleIndices[i + 2] * 3 + 1], vertices[triangleIndices[i + 2] * 3 + 2]);

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
        }
        else
        {
            float firstD = leftleft.rayAABB(ray);
            int first = 0;
            float secondD = leftright.rayAABB(ray);
            int second = 1;
            float thirdD = rightleft.rayAABB(ray);
            int third = 2;
            float fourthD = rightright.rayAABB(ray);
            int fourth = 3;
            float temp = 0f;
            int tempBVH;
            if (!this.isLeafParent) {
                if (firstD > secondD)
                {
                    temp = firstD;
                    firstD = secondD;
                    secondD = temp;
                    tempBVH = first;
                    first = second;
                    second = tempBVH;

                }
                if (firstD > thirdD)
                {
                    temp = firstD;
                    firstD = thirdD;
                    thirdD = firstD;
                    tempBVH = first;
                    first = third;
                    third = tempBVH;
                }
                if (firstD > fourthD)
                {
                    temp = firstD;
                    firstD = fourthD;
                    fourthD = temp;
                    tempBVH = first;
                    first = fourth;
                    fourth = tempBVH;
                }
                if (secondD > thirdD)
                {
                    temp = secondD;
                    secondD = thirdD;
                    thirdD = temp;
                    tempBVH = second;
                    second = third;
                    third = tempBVH;
                }
                if (secondD > fourthD)
                {
                    temp = secondD;
                    secondD = fourthD;
                    fourthD = temp;
                    tempBVH = second;
                    second = fourth;
                    fourth = tempBVH;
                }
                if (thirdD > fourthD)
                {
                    temp = thirdD;
                    thirdD = fourthD;
                    fourthD = temp;
                    tempBVH = third;
                    third = fourth;
                    fourth = tempBVH;
                }
                if (firstD >= ray.t)
                    return;
                if (firstD >= 0f)
                    fourwayChildren[first].nearestIntersection(ray, vertices);
                if (secondD >= ray.t)
                    return;
                if (secondD >= 0f)
                    fourwayChildren[second].nearestIntersection(ray, vertices);
                if (thirdD >= ray.t)
                    return;
                if (thirdD >= 0)
                    fourwayChildren[third].nearestIntersection(ray, vertices);
                if (fourthD >= ray.t)
                    return;
                if (fourthD >= 0f)
                    fourwayChildren[fourth].nearestIntersection(ray, vertices);
            }
            else
            {
                
                if (firstD >= 0f&& firstD<ray.t)
                    fourwayChildren[first].nearestIntersection(ray, vertices);
                
                if (secondD >= 0f && secondD <ray.t)
                    fourwayChildren[second].nearestIntersection(ray, vertices);
                
                if (thirdD >= 0 && thirdD  <ray.t)
                    fourwayChildren[third].nearestIntersection(ray, vertices);
                
                if (fourthD >= 0f && fourthD < ray.t )
                    fourwayChildren[fourth].nearestIntersection(ray, vertices);
            }
            
            
            //if (ray.debuggingRay) Console.WriteLine("Hhhhhhhhhhhhhhhhhh");
            

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

        if (tmin < 0f && tmax >= 0f)
        {
            return 0f;
        }
        return tmin;
    }
}




class BVH
{
    static int nodeCount = 0;
    public int nodeNumber = 0;
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
        SAH = CalculateHalfSA(AABBMin, AABBMax) * (indices.Length * 0.3333f);
        triangleIndices = indices;
        nodeNumber = Interlocked.Increment(ref nodeCount);
    }

    public BVH(uint[] indices, Vector3 AABBMin, Vector3 AABBMax, float SAH)
    {
        this.AABBMin = AABBMin;
        this.AABBMax = AABBMax;
        this.SAH = SAH;
        triangleIndices = indices;
        nodeNumber = Interlocked.Increment(ref nodeCount);
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

        for (int i = 0; i < indices.Length; i++)
        {
            for (var a = 0; a < 3; a++)
            {
                if (vertices[indices[i] * 3 + a] < AABBMinFinal[a])
                {
                    AABBMinFinal[a] = vertices[indices[i] * 3 + a];
                }
                if (vertices[indices[i] * 3 + a] > AABBMaxFinal[a])
                {
                    AABBMaxFinal[a] = vertices[indices[i] * 3 + a];
                }
            }
        }

        AABBMin = AABBMinFinal;
        AABBMax = AABBMaxFinal;
    }

    public void nearestIntersection(Ray ray, float[] vertices)
    {
        ray.bvhChecks++;

        if (isLeaf)
        {
            for (int i = 0; i < triangleIndices.Length; i += 3)
            {
                Vector3 a = new Vector3(vertices[triangleIndices[i] * 3], vertices[triangleIndices[i] * 3 + 1], vertices[triangleIndices[i] * 3 + 2]);
                Vector3 b = new Vector3(vertices[triangleIndices[i + 1] * 3], vertices[triangleIndices[i + 1] * 3 + 1], vertices[triangleIndices[i + 1] * 3 + 2]);
                Vector3 c = new Vector3(vertices[triangleIndices[i + 2] * 3], vertices[triangleIndices[i + 2] * 3 + 1], vertices[triangleIndices[i + 2] * 3 + 2]);

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
        }
        else
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
            }
            else
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

        if (tmin < 0f && tmax >= 0f)
        {
            return 0f;
        }
        return tmin;
    }
}



