using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// TEST Only
//http://members.chello.at/~easyfilter/bresenham.html
public class Rasterizer4
{
    public static bool checkSubBlocks = false;
    #region 3D
    // For melee actions, camera <-> block collision check
    private static Vector3Int[] _ray1 = new Vector3Int[18];
    private static Vector3Int[] _ray2 = new Vector3Int[18];
    public static Vector3Int[] _rayFinal = new Vector3Int[25];

    public static int Line3D(Vector3 startPoint, Vector3 endPoint)
    {
        return Line3D(ref _ray1, ref _ray2, ref _rayFinal, startPoint, endPoint);
    }


    // For terrain generation (Use sparsely)
    public static void Line3D(out List<Vector3Int> rayFinalList, ref Vector3Int[] ray1, ref Vector3Int[] ray2, ref Vector3Int[] rayFinal, Vector3 startPoint, Vector3 endPoint)
    {
        //int len = Mathf.CeilToInt((startPoint - endPoint).magnitude * 1.9f);
        float len = (startPoint - endPoint).magnitude;
        int arrayLen = Mathf.CeilToInt(len * 1.8f);
        if (ray1 == null || ray1.Length < arrayLen) {
#if UNITY_EDITOR
            Debug.Log("Rasterizer3 Line3D allocating array:" + len);
#endif
            ray1 = new Vector3Int[arrayLen];
            ray2 = new Vector3Int[arrayLen];
            rayFinal = new Vector3Int[Mathf.CeilToInt(len * 2.5f)];    // 2.1 / 1.9
        }

        int cnt = Line3D(ref ray1, ref ray2, ref rayFinal, startPoint, endPoint);
        rayFinalList = new List<Vector3Int>(cnt);

        for (int i = 0; i < cnt; i++)
            rayFinalList.Add(rayFinal[i]);
    }


    public static int Line3D(ref Vector3Int[] ray1, ref Vector3Int[] ray2, ref Vector3Int[] rayFinal, Vector3 startPoint, Vector3 endPoint)
    {
        int ray1Cnt = 0, ray2Cnt = 0, cnt = 0;
        Vector3 diff = endPoint - startPoint;
        float absDx = Mathf.Abs(diff.x);
        float absDy = Mathf.Abs(diff.y);
        float absDz = Mathf.Abs(diff.z);

        if (absDx >= absDz && absDx >= absDy) {
            // ray1:rayXZ, ray2:rayXY
            Line3D_X(ref ray1, out ray1Cnt, ref ray2, out ray2Cnt, startPoint, endPoint);
            cnt = MergeProjectedRays_X(ref ray1, ray1Cnt, ref ray2, ray2Cnt, ref rayFinal);
        }
        else if (absDy > absDx && absDy >= absDz) {
            // ray1:rayYX, ray2:rayYZ
            Line3D_Y(ref ray1, out ray1Cnt, ref ray2, out ray2Cnt, startPoint, endPoint);
            cnt = MergeProjectedRays_Y(ref ray1, ray1Cnt, ref ray2, ray2Cnt, ref rayFinal);
        }
        else if (absDz > absDx && absDz > absDy) {
            // ray1:rayZX, ray2:rayZY
            Line3D_Z(ref ray1, out ray1Cnt, ref ray2, out ray2Cnt, startPoint, endPoint);
            cnt = MergeProjectedRays_Z(ref ray1, ray1Cnt, ref ray2, ray2Cnt, ref rayFinal);
        }
        // Test
        DebugDrawLine(startPoint, endPoint, 0.1f);
        ForceMasters.DebugUtils.InstantiateCubes(ref ray1, ray1Cnt, ref ray2, ray2Cnt, "Cell1");
        ForceMasters.DebugUtils.InstantiateCubes(ref rayFinal, cnt, "Cell0");
        Debug.Log($"ray1 cnt:{ray1Cnt}, ray2 cnt:{ray2Cnt}");
        return cnt;
    }

    // XZ & XY Plane (X:major axis)
    private static void Line3D_X(ref Vector3Int[] ray1, out int cnt1, ref Vector3Int[] ray2, out int cnt2, Vector3 startPoint, Vector3 endPoint)
    {
        int fixedY = 0, fixedZ = 0;
        Vector3Int start = new Vector3Int(Mathf.RoundToInt(startPoint.x), Mathf.RoundToInt(startPoint.y), Mathf.RoundToInt(startPoint.z));
        Vector3Int end = new Vector3Int(Mathf.RoundToInt(endPoint.x), Mathf.RoundToInt(endPoint.y), Mathf.RoundToInt(endPoint.z));
        
        int dx = end.x - start.x;
        int xi = 1;
        if (dx < 0) {
            xi = -1;
            dx = -dx;
        }

        int dz = end.z - start.z;
        int zi = 1;
        if (dz < 0) {
            zi = -1;
            dz = -dz;
        }

        int dy = end.y - start.y;
        int yi = 1;
        if(dy < 0) {
            yi = -1;
            dy = -dy;
        }

        int dx2 = dx + dx;
        int dz2 = dz + dz;
        int dy2 = dy + dy;
        int D_z = dz2 - dx;
        int D_y = dy2 - dx;
        int currentZ = start.z;
        int currentY = start.y;

        cnt1 = 0;
        cnt2 = 0;
        if (xi == 1) {
            for (int x = start.x; x <= end.x; x += xi) {
                // XZ Plane
                ray1[cnt1++] = new Vector3Int(x, fixedY, currentZ);
                // Update Z
                if (D_z > 0) {
                    currentZ += zi;
                    // Sub blocks
                    //if (D_z >= dz) {
                    //    ray1[cnt1++] = new Vector3Int(x, fixedY, currentZ);
                    //}
                    //else {
                    //    ray1[cnt1++] = new Vector3Int(x + xi, fixedY, currentZ - zi);
                    //}
                    if (checkSubBlocks) {
                        if (D_z > dz) {
                            ray1[cnt1++] = new Vector3Int(x, fixedY, currentZ);
                        }
                        else if (D_z < dz) {
                            ray1[cnt1++] = new Vector3Int(x + xi, fixedY, currentZ - zi);
                        }
                        else {
                            ray1[cnt1++] = new Vector3Int(x, fixedY, currentZ);
                            ray1[cnt1++] = new Vector3Int(x + xi, fixedY, currentZ - zi);
                        }
                    }
                    D_z -= dx2;
                }
                D_z += dz2;

                // XY Plane
                ray2[cnt2++] = new Vector3Int(x, currentY, fixedZ);
                // Update Y
                if(D_y > 0) {
                    currentY += yi;
                    // Sub blocks
                    //if (D_y >= dy) {
                    //    ray2[cnt2++] = new Vector3Int(x, currentY, fixedZ);
                    //}
                    //else {
                    //    ray2[cnt2++] = new Vector3Int(x + xi, currentY - yi, fixedZ);
                    //}
                    if (checkSubBlocks) {
                        if (D_y > dy) {
                            ray2[cnt2++] = new Vector3Int(x, currentY, fixedZ);
                        }
                        else if (D_y < dy) {
                            ray2[cnt2++] = new Vector3Int(x + xi, currentY - yi, fixedZ);
                        }
                        else {
                            ray2[cnt2++] = new Vector3Int(x, currentY, fixedZ);
                            ray2[cnt2++] = new Vector3Int(x + xi, currentY - yi, fixedZ);
                        }
                    }
                    D_y -= dx2;
                }
                D_y += dy2;
            }
        }
        else {
            for (int x = start.x; x >= end.x; x += xi) {
                // XZ Plane
                ray1[cnt1++] = new Vector3Int(x, fixedY, currentZ);
                // Update Z
                if (D_z > 0) {
                    currentZ += zi;
                    // Sub blocks
                    //if (D_z >= dz) {
                    //    ray1[cnt1++] = new Vector3Int(x, fixedY, currentZ);
                    //}
                    //else {
                    //    ray1[cnt1++] = new Vector3Int(x + xi, fixedY, currentZ - zi);
                    //}
                    if (checkSubBlocks) {
                        if (D_z > dz) {
                            ray1[cnt1++] = new Vector3Int(x, fixedY, currentZ);
                        }
                        else if (D_z < dz) {
                            ray1[cnt1++] = new Vector3Int(x + xi, fixedY, currentZ - zi);
                        }
                        else {
                            ray1[cnt1++] = new Vector3Int(x, fixedY, currentZ);
                            ray1[cnt1++] = new Vector3Int(x + xi, fixedY, currentZ - zi);
                        }
                    }
                    D_z -= dx2;
                }
                D_z += dz2;

                // XY Plane
                ray2[cnt2++] = new Vector3Int(x, currentY, fixedZ);
                // Update Y
                if (D_y > 0) {
                    currentY += yi;
                    // Sub blocks
                    //if (D_y >= dy) {
                    //    ray2[cnt2++] = new Vector3Int(x, currentY, fixedZ);
                    //}
                    //else {
                    //    ray2[cnt2++] = new Vector3Int(x + xi, currentY - yi, fixedZ);
                    //}
                    if (checkSubBlocks) {
                        if (D_y > dy) {
                            ray2[cnt2++] = new Vector3Int(x, currentY, fixedZ);
                        }
                        else if (D_y < dy) {
                            ray2[cnt2++] = new Vector3Int(x + xi, currentY - yi, fixedZ);
                        }
                        else {
                            ray2[cnt2++] = new Vector3Int(x, currentY, fixedZ);
                            ray2[cnt2++] = new Vector3Int(x + xi, currentY - yi, fixedZ);
                        }
                    }
                    D_y -= dx2;
                }
                D_y += dy2;
            }
        }
    }

    // YX & YZ Plane (Y:major axis)
    private static void Line3D_Y(ref Vector3Int[] ray1, out int cnt1, ref Vector3Int[] ray2, out int cnt2, Vector3 startPoint, Vector3 endPoint)
    {
        int fixedX = 0, fixedZ = 0;
        Vector3Int start = new Vector3Int(Mathf.RoundToInt(startPoint.x), Mathf.RoundToInt(startPoint.y), Mathf.RoundToInt(startPoint.z));
        Vector3Int end = new Vector3Int(Mathf.RoundToInt(endPoint.x), Mathf.RoundToInt(endPoint.y), Mathf.RoundToInt(endPoint.z));

        int dx = end.x - start.x;
        int xi = 1;
        if (dx < 0) {
            xi = -1;
            dx = -dx;
        }

        int dz = end.z - start.z;
        int zi = 1;
        if (dz < 0) {
            zi = -1;
            dz = -dz;
        }

        int dy = end.y - start.y;
        int yi = 1;
        if (dy < 0) {
            yi = -1;
            dy = -dy;
        }

        int dx2 = dx + dx;
        int dz2 = dz + dz;
        int dy2 = dy + dy;
        int D_x = dx2 - dy;
        int D_z = dz2 - dy;
        int currentX = start.x;
        int currentZ = start.z;

        cnt1 = 0;
        cnt2 = 0;
        if (yi == 1) {
            for (int y = start.y; y <= end.y; y += yi) {
                // YX Plane (Z:fixed)
                ray1[cnt1++] = new Vector3Int(currentX, y, fixedZ);
                // Update X
                if (D_x > 0) {
                    currentX += xi;
                    // Sub blocks
                    //if (D_x >= dx) {
                    //    ray1[cnt1++] = new Vector3Int(currentX, y, fixedZ);
                    //}
                    //else {
                    //    ray1[cnt1++] = new Vector3Int(currentX - xi, y + yi, fixedZ);
                    //}
                    if (checkSubBlocks) {
                        if (D_x > dx) {
                            ray1[cnt1++] = new Vector3Int(currentX, y, fixedZ);
                        }
                        else if (D_x < dx) {
                            ray1[cnt1++] = new Vector3Int(currentX - xi, y + yi, fixedZ);
                        }
                        else {
                            ray1[cnt1++] = new Vector3Int(currentX, y, fixedZ);
                            ray1[cnt1++] = new Vector3Int(currentX - xi, y + yi, fixedZ);
                        }
                    }
                    D_x -= dy2;
                }
                D_x += dx2;

                // YZ Plane (X:fixed)
                ray2[cnt2++] = new Vector3Int(fixedX, y, currentZ);
                // Update Z
                if (D_z > 0) {
                    currentZ += zi;
                    // Sub blocks
                    //if (D_z >= dz) {
                    //    ray2[cnt2++] = new Vector3Int(fixedX, y, currentZ);
                    //}
                    //else {
                    //    ray2[cnt2++] = new Vector3Int(fixedX, y + yi, currentZ - zi);
                    //}
                    if (checkSubBlocks) {
                        if (D_z > dz) {
                            ray2[cnt2++] = new Vector3Int(fixedX, y, currentZ);
                        }
                        else if (D_z < dz) {
                            ray2[cnt2++] = new Vector3Int(fixedX, y + yi, currentZ - zi);
                        }
                        else {
                            ray2[cnt2++] = new Vector3Int(fixedX, y, currentZ);
                            ray2[cnt2++] = new Vector3Int(fixedX, y + yi, currentZ - zi);
                        }
                    }
                    D_z -= dy2;
                }
                D_z += dz2;
            }
        }
        else {
            for (int y = start.y; y >= end.y; y += yi) {
                // YX Plane
                ray1[cnt1++] = new Vector3Int(currentX, y, fixedZ);
                // Update X
                if (D_x > 0) {
                    currentX += xi;
                    // Sub blocks
                    //if (D_x >= dx) {
                    //    ray1[cnt1++] = new Vector3Int(currentX, y, fixedZ);
                    //}
                    //else {
                    //    ray1[cnt1++] = new Vector3Int(currentX - xi, y + yi, fixedZ);
                    //}
                    if (checkSubBlocks) {
                        if (D_x > dx) {
                            ray1[cnt1++] = new Vector3Int(currentX, y, fixedZ);
                        }
                        else if (D_x < dx) {
                            ray1[cnt1++] = new Vector3Int(currentX - xi, y + yi, fixedZ);
                        }
                        else {
                            ray1[cnt1++] = new Vector3Int(currentX, y, fixedZ);
                            ray1[cnt1++] = new Vector3Int(currentX - xi, y + yi, fixedZ);
                        }
                    }
                    D_x -= dy2;
                }
                D_x += dx2;

                // YZ Plane
                ray2[cnt2++] = new Vector3Int(fixedX, y, currentZ);
                // Update Z
                if (D_z > 0) {
                    currentZ += zi;
                    // Sub blocks
                    //if (D_z >= dz) {
                    //    ray2[cnt2++] = new Vector3Int(fixedX, y, currentZ);
                    //}
                    //else {
                    //    ray2[cnt2++] = new Vector3Int(fixedX, y + yi, currentZ - zi);
                    //}
                    if (checkSubBlocks) {
                        if (D_z > dz) {
                            ray2[cnt2++] = new Vector3Int(fixedX, y, currentZ);
                        }
                        else if (D_z < dz) {
                            ray2[cnt2++] = new Vector3Int(fixedX, y + yi, currentZ - zi);
                        }
                        else {
                            ray2[cnt2++] = new Vector3Int(fixedX, y, currentZ);
                            ray2[cnt2++] = new Vector3Int(fixedX, y + yi, currentZ - zi);
                        }
                    }
                    D_z -= dy2;
                }
                D_z += dz2;
            }
        }
    }

    // ZX & ZY Plane (Z:major axis)
    private static void Line3D_Z(ref Vector3Int[] ray1, out int cnt1, ref Vector3Int[] ray2, out int cnt2, Vector3 startPoint, Vector3 endPoint)
    {
        int fixedX = 0, fixedY = 0;
        Vector3Int start = new Vector3Int(Mathf.RoundToInt(startPoint.x), Mathf.RoundToInt(startPoint.y), Mathf.RoundToInt(startPoint.z));
        Vector3Int end = new Vector3Int(Mathf.RoundToInt(endPoint.x), Mathf.RoundToInt(endPoint.y), Mathf.RoundToInt(endPoint.z));

        int dx = end.x - start.x;
        int xi = 1;
        if (dx < 0) {
            xi = -1;
            dx = -dx;
        }

        int dz = end.z - start.z;
        int zi = 1;
        if (dz < 0) {
            zi = -1;
            dz = -dz;
        }

        int dy = end.y - start.y;
        int yi = 1;
        if (dy < 0) {
            yi = -1;
            dy = -dy;
        }

        int dx2 = dx + dx;
        int dz2 = dz + dz;
        int dy2 = dy + dy;
        int D_x = dx2 - dz;
        int D_y = dy2 - dz;
        int currentX = start.x;
        int currentY = start.y;

        cnt1 = 0;
        cnt2 = 0;
        if (zi == 1) {
            for (int z = start.z; z <= end.z; z += zi) {
                // ZX Plane (Y:fixed)
                ray1[cnt1++] = new Vector3Int(currentX, fixedY, z);
                // Update X
                if (D_x > 0) {
                    currentX += xi;
                    // Sub blocks
                    //if (D_x >= dx) {
                    //    ray1[cnt1++] = new Vector3Int(currentX, fixedY, z);
                    //}
                    //else {
                    //    ray1[cnt1++] = new Vector3Int(currentX - xi, fixedY, z + zi);
                    //}
                    if (checkSubBlocks) {
                        if (D_x > dx) {
                            ray1[cnt1++] = new Vector3Int(currentX, fixedY, z);
                        }
                        else if (D_x < dx) {
                            ray1[cnt1++] = new Vector3Int(currentX - xi, fixedY, z + zi);
                        }
                        else {
                            ray1[cnt1++] = new Vector3Int(currentX, fixedY, z);
                            ray1[cnt1++] = new Vector3Int(currentX - xi, fixedY, z + zi);
                        }
                    }
                    D_x -= dz2;
                }
                D_x += dx2;

                // ZY Plane (X:fixed)
                ray2[cnt2++] = new Vector3Int(fixedX, currentY, z);
                // Update Y
                if (D_y > 0) {
                    currentY += yi;
                    // Sub blocks
                    //if (D_y >= dy) {
                    //    ray2[cnt2++] = new Vector3Int(fixedX, currentY, z);
                    //}
                    //else {
                    //    ray2[cnt2++] = new Vector3Int(fixedX, currentY - yi, z + zi);
                    //}
                    if (checkSubBlocks) {
                        if (D_y > dy) {
                            ray2[cnt2++] = new Vector3Int(fixedX, currentY, z);
                        }
                        else if (D_y < dy) {
                            ray2[cnt2++] = new Vector3Int(fixedX, currentY - yi, z + zi);
                        }
                        else {
                            ray2[cnt2++] = new Vector3Int(fixedX, currentY, z);
                            ray2[cnt2++] = new Vector3Int(fixedX, currentY - yi, z + zi);
                        }
                    }
                    D_y -= dz2;
                }
                D_y += dy2;
            }
        }
        else {
            for (int z = start.z; z >= end.z; z += zi) {
                // ZX Plane (Y:fixed)
                ray1[cnt1++] = new Vector3Int(currentX, fixedY, z);
                // Update X
                if (D_x > 0) {
                    currentX += xi;
                    // Sub blocks
                    //if (D_x >= dx) {
                    //    ray1[cnt1++] = new Vector3Int(currentX, fixedY, z);
                    //}
                    //else {
                    //    ray1[cnt1++] = new Vector3Int(currentX - xi, fixedY, z + zi);
                    //}
                    if (checkSubBlocks) {
                        if (D_x > dx) {
                            ray1[cnt1++] = new Vector3Int(currentX, fixedY, z);
                        }
                        else if (D_x < dx) {
                            ray1[cnt1++] = new Vector3Int(currentX - xi, fixedY, z + zi);
                        }
                        else {
                            ray1[cnt1++] = new Vector3Int(currentX, fixedY, z);
                            ray1[cnt1++] = new Vector3Int(currentX - xi, fixedY, z + zi);
                        }
                    }
                    D_x -= dz2;
                }
                D_x += dx2;

                // ZY Plane (X:fixed)
                ray2[cnt2++] = new Vector3Int(fixedX, currentY, z);
                // Update Y
                if (D_y > 0) {
                    currentY += yi;
                    // Sub blocks
                    //if (D_y >= dy) {
                    //    ray2[cnt2++] = new Vector3Int(fixedX, currentY, z);
                    //}
                    //else {
                    //    ray2[cnt2++] = new Vector3Int(fixedX, currentY - yi, z + zi);
                    //}
                    if (checkSubBlocks) {
                        if (D_y > dy) {
                            ray2[cnt2++] = new Vector3Int(fixedX, currentY, z);
                        }
                        else if (D_y < dy) {
                            ray2[cnt2++] = new Vector3Int(fixedX, currentY - yi, z + zi);
                        }
                        else {
                            ray2[cnt2++] = new Vector3Int(fixedX, currentY, z);
                            ray2[cnt2++] = new Vector3Int(fixedX, currentY - yi, z + zi);
                        }
                    }
                    D_y -= dz2;
                }
                D_y += dy2;
            }
        }
    }
    
    public static int MergeProjectedRays_X(ref Vector3Int[] rayXZ, int cntXZ, ref Vector3Int[] rayXY, int cntXY, ref Vector3Int[] rayFinal)
    {
        int idx = 0;
        int j = 0;
        int cnt = 0;

        for (int i = 0; i < cntXZ; i++) {
            Vector3Int p1 = rayXZ[i];
            if (idx == 0 || p1.x == rayFinal[idx - 1].x) {
                j -= cnt;
            }
            cnt = 0;
            for (; j < cntXY;) {
                Vector3Int p2 = rayXY[j];
                if (p1.x == p2.x) {
                    rayFinal[idx++] = new Vector3Int(p1.x, p2.y, p1.z);
                    //rayFinal[idx].x = p1.x;
                    //rayFinal[idx].y = p2.y;
                    //rayFinal[idx++].z = p1.z;
                    j++;
                    cnt++;
                }
                else {
                    break;
                }
            }
        }
        return idx;
    }

    // ray1:rayYX, ray2:rayYZ
    public static int MergeProjectedRays_Y(ref Vector3Int[] rayYX, int cntYX, ref Vector3Int[] rayYZ, int cntYZ, ref Vector3Int[] rayFinal)
    {
        int idx = 0;
        int j = 0;
        int cnt = 0;

        for (int i = 0; i < cntYX; i++) {
            Vector3Int p1 = rayYX[i];
            if (idx == 0 || p1.y == rayFinal[idx - 1].y) {
                j -= cnt;
            }
            cnt = 0;
            for (; j < cntYZ;) {
                Vector3Int p2 = rayYZ[j];
                if (p1.y == p2.y) {
                    rayFinal[idx++] = new Vector3Int(p1.x, p1.y, p2.z);
                    j++;
                    cnt++;
                }
                else {
                    break;
                }
            }
        }
        return idx;
    }

    // ray1:rayZX, ray2:rayZY
    public static int MergeProjectedRays_Z(ref Vector3Int[] rayZX, int cntZX, ref Vector3Int[] rayZY, int cntZY, ref Vector3Int[] rayFinal)
    {
        int idx = 0;
        int j = 0;
        int cnt = 0;

        for (int i = 0; i < cntZX; i++) {
            Vector3Int p1 = rayZX[i];
            if (idx == 0 || p1.z == rayFinal[idx - 1].z) {
                j -= cnt;
            }
            cnt = 0;
            for (; j < cntZY;) {
                Vector3Int p2 = rayZY[j];
                if (p1.z == p2.z) {
                    rayFinal[idx++] = new Vector3Int(p1.x, p2.y, p1.z);
                    j++;
                    cnt++;
                }
                else {
                    break;
                }
            }
        }
        return idx;
    }

    private static void DebugDrawLine(Vector3 start, Vector3 end, float duration)
    {
        Debug.DrawLine(start, end, Color.green, duration);
        Vector3 iStart = new Vector3(Mathf.Round(start.x), Mathf.Round(start.y), Mathf.Round(start.z));
        Vector3 iEnd = new Vector3(Mathf.Round(end.x), Mathf.Round(end.y), Mathf.Round(end.z));
        Debug.DrawLine(iStart, iEnd, Color.red, duration);
    }
    #endregion

    #region 2D
    public static int Line2D(ref Vector2Int[] rayFinal, Vector3 startPoint, Vector3 endPoint)
    {
        return Line2D(ref rayFinal, new Vector2(startPoint.x, startPoint.y), new Vector2(endPoint.x, endPoint.y));
    }

    // For terrain generation (use sparsely)
    public static void Line2D(ref List<Vector2Int> rayFinalList, ref Vector2Int[] rayFinal, Vector2 startPoint, Vector2 endPoint)
    {
        float len = (startPoint - endPoint).magnitude;
        int arrayLen = Mathf.CeilToInt(len * 1.8f);
        if (rayFinal == null || rayFinal.Length < arrayLen) {
            rayFinal = new Vector2Int[arrayLen];
        }
        int cnt = Line2D(ref rayFinal, startPoint, endPoint);
        rayFinalList = new List<Vector2Int>(cnt);
        for (int i = 0; i < cnt; i++)
            rayFinalList.Add(rayFinal[i]);
    }

    public static int Line2D(ref Vector2Int[] rayFinal, Vector2 startPoint, Vector2 endPoint)
    {
        int cnt = 0;
        Vector2 diff = endPoint - startPoint;
        float absDx = Mathf.Abs(diff.x);
        float absDy = Mathf.Abs(diff.y);

        if (absDx >= absDy) { 
            cnt = Line2D_X(ref rayFinal, startPoint, endPoint);
        }
        else { 
            cnt = Line2D_Y(ref rayFinal, startPoint, endPoint);
        }

        //// Test
        //Vector3Int[] rayFinal3D = new Vector3Int[cnt];
        //for(int i = 0; i < cnt; i++) {
        //    rayFinal3D[i] = new Vector3Int(rayFinal[i].x, rayFinal[i].y, 0);
        //}
        //DebugDrawLine(startPoint, endPoint, 0.1f);
        //ForceMasters.DebugUtils.InstantiateCubes(ref rayFinal3D, cnt, "Cell0");

        return cnt;
    }

    // XY (X:major axis)
    private static int Line2D_X(ref Vector2Int[] rayFinal, Vector2 startPoint, Vector2 endPoint)
    {
        Vector2Int start = new Vector2Int(Mathf.RoundToInt(startPoint.x), Mathf.RoundToInt(startPoint.y));
        Vector2Int end = new Vector2Int(Mathf.RoundToInt(endPoint.x), Mathf.RoundToInt(endPoint.y));

        int dx = end.x - start.x;
        int xi = 1;
        if (dx < 0) {
            xi = -1;
            dx = -dx;
        }

        int dy = end.y - start.y;
        int yi = 1;
        if (dy < 0) {
            yi = -1;
            dy = -dy;
        }

        int dx2 = dx + dx;
        int dy2 = dy + dy;
        int D_y = dy2 - dx;
        int currentY = start.y;

        int cnt = 0;
        if (xi == 1) {
            for (int x = start.x; x <= end.x; x += xi) {
                // XY Plane
                rayFinal[cnt++] = new Vector2Int(x, currentY);
                // Update Y
                if (D_y > 0) {
                    currentY += yi;
                    if (D_y > dy) {
                        rayFinal[cnt++] = new Vector2Int(x, currentY);
                    }
                    else if (D_y < dy) {
                        rayFinal[cnt++] = new Vector2Int(x + xi, currentY - yi);
                    }
                    else {
                        rayFinal[cnt++] = new Vector2Int(x, currentY);
                        rayFinal[cnt++] = new Vector2Int(x + xi, currentY - yi);
                    }
                    D_y -= dx2;
                }
                D_y += dy2;
            }
        }
        else {
            for (int x = start.x; x >= end.x; x += xi) {
                // XY Plane
                rayFinal[cnt++] = new Vector2Int(x, currentY);
                // Update Y
                if (D_y > 0) {
                    currentY += yi;
                    if (D_y > dy) {
                        rayFinal[cnt++] = new Vector2Int(x, currentY);
                    }
                    else if (D_y < dy) {
                        rayFinal[cnt++] = new Vector2Int(x + xi, currentY - yi);
                    }
                    else {
                        rayFinal[cnt++] = new Vector2Int(x, currentY);
                        rayFinal[cnt++] = new Vector2Int(x + xi, currentY - yi);
                    }
                    D_y -= dx2;
                }
                D_y += dy2;
            }
        }
        return cnt;
    }

    // YX (Y:major axis)
    private static int Line2D_Y(ref Vector2Int[] rayFinal, Vector2 startPoint, Vector2 endPoint)
    {
        Vector2Int start = new Vector2Int(Mathf.RoundToInt(startPoint.x), Mathf.RoundToInt(startPoint.y));
        Vector2Int end = new Vector2Int(Mathf.RoundToInt(endPoint.x), Mathf.RoundToInt(endPoint.y));

        int dx = end.x - start.x;
        int xi = 1;
        if (dx < 0) {
            xi = -1;
            dx = -dx;
        }

        int dy = end.y - start.y;
        int yi = 1;
        if (dy < 0) {
            yi = -1;
            dy = -dy;
        }

        int dx2 = dx + dx;
        int dy2 = dy + dy;
        int D_x = dx2 - dy;
        int currentX = start.x;

        int cnt = 0;
        if (yi == 1) {
            for (int y = start.y; y <= end.y; y += yi) {
                // YX Plane
                rayFinal[cnt++] = new Vector2Int(currentX, y);
                // Update X
                if (D_x > 0) {
                    currentX += xi;
                    if (D_x > dx) {
                        rayFinal[cnt++] = new Vector2Int(currentX, y);
                    }
                    else if (D_x < dx) {
                        rayFinal[cnt++] = new Vector2Int(currentX - xi, y + yi);
                    }
                    else {
                        rayFinal[cnt++] = new Vector2Int(currentX, y);
                        rayFinal[cnt++] = new Vector2Int(currentX - xi, y + yi);
                    }
                    D_x -= dy2;
                }
                D_x += dx2;
            }
        }
        else {
            for (int y = start.y; y >= end.y; y += yi) {
                // YX Plane
                rayFinal[cnt++] = new Vector2Int(currentX, y);
                // Update X
                if (D_x > 0) {
                    currentX += xi;
                    if (D_x > dx) {
                        rayFinal[cnt++] = new Vector2Int(currentX, y);
                    }
                    else if (D_x < dx) {
                        rayFinal[cnt++] = new Vector2Int(currentX - xi, y + yi);
                    }
                    else {
                        rayFinal[cnt++] = new Vector2Int(currentX, y);
                        rayFinal[cnt++] = new Vector2Int(currentX - xi, y + yi);
                    }
                    D_x -= dy2;
                }
                D_x += dx2;
            }
        }
        return cnt;
    }
    #endregion
}
