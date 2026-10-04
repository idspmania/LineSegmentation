using System.Collections.Generic;
using Unity.Mathematics;

namespace VoxelRay
{
    // 방문한 복셀을 리스트에 이어 붙인다. 관리 참조를 들고 있으므로 메인 스레드 전용(Burst 불가).
    public struct VoxelListCollector : IVoxelVisitor
    {
        public List<int3> Voxels;

        public VoxelListCollector(List<int3> voxels) { Voxels = voxels; }

        public bool Visit(int3 voxel)
        {
            Voxels.Add(voxel);
            return false;
        }
    }
}
