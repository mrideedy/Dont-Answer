using UnityEngine;

namespace DontAnswer
{
    /// <summary>Scales a renderer's texture tiling to its size, so walls and floors share materials without stretching.</summary>
    [ExecuteAlways]
    public class AutoTile : MonoBehaviour
    {
        public Vector2 tiling = Vector2.one;

        static MaterialPropertyBlock block;
        static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");

        void OnEnable() => Apply();
        void OnValidate() => Apply();

        public void Apply()
        {
            var r = GetComponent<Renderer>();
            if (r == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetVector(BaseMapST, new Vector4(tiling.x, tiling.y, 0f, 0f));
            r.SetPropertyBlock(block);
        }
    }
}
