using UnityEngine;

namespace StellarCommand.Galaxy
{
    /// <summary>
    /// Draws a mirror image (about the floor plane y = 0) of another mesh, for the glossy floor.
    /// It lives under a parent scaled (1, -1, 1) at the origin, so copying the source's world pose into
    /// its own local pose is exactly the reflection. It also follows the source's mesh (the galaxy map
    /// builds its meshes at runtime and rebuilds them on every save) and its clip settings.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class MirrorProxy : MonoBehaviour
    {
        // Mirrored copies must be drawn BEFORE the floor (queue 2450) so the floor can blend over them
        private const int MirrorRenderQueue = 2400;

        public MeshFilter source;

        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private MeshRenderer _sourceRenderer;
        private Material _material;
        private MaterialPropertyBlock _block;

        private static readonly int ClipCenterId = Shader.PropertyToID("_ClipCenter");
        private static readonly int ClipNormalId = Shader.PropertyToID("_ClipNormal");

        private void Awake()
        {
            _filter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            _block = new MaterialPropertyBlock();

            if (source != null)
            {
                _sourceRenderer = source.GetComponent<MeshRenderer>();
                if (_sourceRenderer != null && _sourceRenderer.sharedMaterial != null)
                {
                    _material = new Material(_sourceRenderer.sharedMaterial) { renderQueue = MirrorRenderQueue };
                    _renderer.sharedMaterial = _material;
                }
            }
        }

        private void LateUpdate()
        {
            if (source == null || _sourceRenderer == null || _material == null) return;

            bool visible = _sourceRenderer.enabled && source.gameObject.activeInHierarchy && source.sharedMesh != null;
            _renderer.enabled = visible;
            if (!visible) return;

            if (_filter.sharedMesh != source.sharedMesh) _filter.sharedMesh = source.sharedMesh;

            // The parent mirrors y, so the source's world pose as a local pose is its reflection
            Transform t = source.transform;
            transform.localPosition = t.position;
            transform.localRotation = t.rotation;
            transform.localScale = t.lossyScale;

            // Keep the circular clip, reflected too (centre and plane normal flip in y)
            _sourceRenderer.GetPropertyBlock(_block);
            Vector4 center = _block.GetVector(ClipCenterId);
            Vector4 normal = _block.GetVector(ClipNormalId);
            if (center != Vector4.zero || normal != Vector4.zero)
            {
                center.y = -center.y;
                normal.y = -normal.y;
                _block.SetVector(ClipCenterId, center);
                _block.SetVector(ClipNormalId, normal);
            }
            _renderer.SetPropertyBlock(_block);
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
